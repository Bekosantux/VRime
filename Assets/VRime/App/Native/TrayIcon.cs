using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace VRime.App.Native
{
    /// <summary>タスクトレイのアイコンで選ばれた操作。</summary>
    public enum TrayCommand
    {
        /// <summary>設定ウィンドウを開く（メニューの「設定を開く」、アイコンの左クリック・Space・Enter）。</summary>
        OpenSettings,

        /// <summary>アプリを終了する（メニューの「終了」）。</summary>
        Quit,
    }

    /// <summary>
    /// タスクトレイ（通知領域）のアイコンと、右クリックのメニュー（docs/spec/lifecycle.md「システムトレイ」）。
    /// アイコンの通知を受けるウィンドウとメッセージループは専用のスレッドに置く。メニュー（<c>TrackPopupMenuEx</c>）は閉じるまで
    /// 呼び出し元のスレッドを止めるため、メインスレッドで出すと、その間 Unity のフレームが止まり、VR 内のキーボードが応答しなくなる。
    /// 選ばれた操作はキューに積み、メインスレッドが <see cref="TryTakeCommand"/> で取り出す。
    /// エクスプローラーが再起動したとき（<c>TaskbarCreated</c>）は、アイコンを再登録する。
    /// ウィンドウのクラスを登録するため、プロセスに同時に 1 つだけ作れる。
    /// </summary>
    public sealed class TrayIcon : IDisposable
    {
        const string WindowClassName = "VRimeTrayWindow";
        const uint IconId = 1;
        const uint CallbackMessage = Win32.WM_APP + 1;

        // Dispose からスレッドへ送る、終了の依頼。WM_CLOSE は他のプロセスからも届きうるため、終了の依頼には使わない。
        const uint ShutdownMessage = Win32.WM_APP + 2;

        const uint OpenSettingsItemId = 1;
        const uint QuitItemId = 2;
        const string OpenSettingsLabel = "設定を開く";
        const string QuitLabel = "終了";

        // NOTIFYICONDATA の szTip の長さ（終端を含む）。
        const int MaxTooltipLength = 128;

        static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(5);

        // ネイティブのコードが呼ぶため、ガベージコレクションで回収されないよう静的に持つ。
        static readonly Win32.WndProc WindowProcedure = OnMessage;

        readonly string _iconPath;
        readonly string _tooltip;
        readonly Thread _thread;
        readonly ManualResetEventSlim _started = new ManualResetEventSlim();
        readonly ConcurrentQueue<TrayCommand> _commands = new ConcurrentQueue<TrayCommand>();
        readonly ConcurrentQueue<Exception> _failures = new ConcurrentQueue<Exception>();
        Exception _startupError;
        bool _disposed;

        // 以下はアイコンのスレッドだけが書き換える。_window は起動を待った後、メインスレッドも読む。
        IntPtr _instance;
        IntPtr _window;
        IntPtr _icon;
        IntPtr _menu;
        uint _taskbarCreatedMessage;
        bool _classRegistered;
        bool _windowDestroyed;
        bool _iconAdded;
        bool _menuOpen;
        bool _shutdownRequested;

        /// <summary>アイコンを登録し、表示されるまで待つ。登録できなければ例外。</summary>
        /// <param name="iconPath">アイコン（.ico）の絶対パス。小さいアイコンの大きさ（表示倍率による）に合うものを読む。</param>
        /// <param name="tooltip">アイコンを指したときに表示する文字列。</param>
        public TrayIcon(string iconPath, string tooltip)
        {
            if (string.IsNullOrEmpty(iconPath)) throw new ArgumentException("パスが空です。", nameof(iconPath));
            if (!Path.IsPathRooted(iconPath)) throw new ArgumentException("アイコンのパスは絶対パスで指定してください: " + iconPath, nameof(iconPath));
            if (!File.Exists(iconPath)) throw new FileNotFoundException("タスクトレイのアイコンが見つかりません。", iconPath);
            if (string.IsNullOrEmpty(tooltip)) throw new ArgumentException("文字列が空です。", nameof(tooltip));
            if (tooltip.Length >= MaxTooltipLength)
                throw new ArgumentException("文字列は " + (MaxTooltipLength - 1) + " 文字以内にしてください: " + tooltip.Length + " 文字", nameof(tooltip));

            _iconPath = iconPath;
            _tooltip = tooltip;
            // メインスレッドが終わった後に、このスレッドがプロセスの終了を妨げないようにする。
            _thread = new Thread(Run) { IsBackground = true, Name = "VRime TrayIcon" };
            _thread.Start();
            _started.Wait();

            if (_startupError != null)
            {
                _thread.Join();
                _started.Dispose();
                ExceptionDispatchInfo.Capture(_startupError).Throw();
            }
        }

        /// <summary>
        /// 選ばれた操作を 1 つ取り出す。メインスレッドで毎フレーム呼ぶ。
        /// アイコンのスレッドで失敗が起きていれば、1 件ずつ例外にする（アイコンの再登録の失敗など）。
        /// </summary>
        public bool TryTakeCommand(out TrayCommand command)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(TrayIcon));
            ThrowFailure();
            return _commands.TryDequeue(out command);
        }

        /// <summary>
        /// アイコンを削除し、スレッドの終了を待つ。スレッドが終わらない、または取り出されていない失敗が残っていれば例外。
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // 失敗でスレッドが既に終わっているときは、ウィンドウもなく送れないが、待つ必要もない。
            Win32.PostMessage(_window, ShutdownMessage, IntPtr.Zero, IntPtr.Zero);
            if (!_thread.Join(ShutdownTimeout))
                throw new TimeoutException("タスクトレイのアイコンのスレッドが " + ShutdownTimeout.TotalSeconds + " 秒以内に終わりませんでした。");
            _started.Dispose();
            ThrowFailure();
        }

        void ThrowFailure()
        {
            if (_failures.TryDequeue(out Exception failure))
                throw new InvalidOperationException("タスクトレイのアイコンの処理に失敗しました: " + failure.Message, failure);
        }

        // アイコンのスレッドの本体。
        void Run()
        {
            GCHandle self = GCHandle.Alloc(this);
            try
            {
                try
                {
                    Start(self);
                }
                catch (Exception e)
                {
                    _startupError = e;
                    return;
                }
                finally
                {
                    _started.Set();
                }
                PumpMessages();
            }
            catch (Exception e)
            {
                _failures.Enqueue(e);
            }
            finally
            {
                ReleaseResources();
                self.Free();
            }
        }

        void Start(GCHandle self)
        {
            _instance = Win32.GetModuleHandle(null);
            if (_instance == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "モジュールのハンドルを取得できません。");

            // ウィンドウを作る前に取得する。作った直後から通知が届きうるため。
            _taskbarCreatedMessage = Win32.RegisterWindowMessage("TaskbarCreated");
            if (_taskbarCreatedMessage == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "TaskbarCreated のメッセージを登録できません。");

            var windowClass = new Win32.WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf<Win32.WNDCLASSEX>(),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(WindowProcedure),
                hInstance = _instance,
                lpszClassName = WindowClassName,
            };
            if (Win32.RegisterClassEx(ref windowClass) == 0)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "タスクトレイのウィンドウのクラスを登録できません。");
            _classRegistered = true;

            // メッセージ専用のウィンドウ（HWND_MESSAGE）には TaskbarCreated（全トップレベルウィンドウへの通知）が届かないため、
            // 表示しないトップレベルのウィンドウにする。
            _window = Win32.CreateWindowEx(0, WindowClassName, WindowClassName, 0, 0, 0, 0, 0,
                IntPtr.Zero, IntPtr.Zero, _instance, IntPtr.Zero);
            if (_window == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "タスクトレイのウィンドウを作れません。");

            // ウィンドウプロシージャ（静的）から、このインスタンスを取得できるようにする。
            IntPtr selfPointer = GCHandle.ToIntPtr(self);
            Win32.SetWindowLongPtr(_window, Win32.GWLP_USERDATA, selfPointer);
            if (Win32.GetWindowLongPtr(_window, Win32.GWLP_USERDATA) != selfPointer)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "タスクトレイのウィンドウにインスタンスを関連付けられません。");

            // 管理者として実行していると、昇格していないエクスプローラーからの TaskbarCreated が UIPI で遮られるため、受け取りを許可する。
            if (!Win32.ChangeWindowMessageFilterEx(_window, _taskbarCreatedMessage, Win32.MSGFLT_ALLOW, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "TaskbarCreated の受け取りを許可できません。");

            _icon = Win32.LoadImage(IntPtr.Zero, _iconPath, Win32.IMAGE_ICON,
                Win32.GetSystemMetrics(Win32.SM_CXSMICON), Win32.GetSystemMetrics(Win32.SM_CYSMICON), Win32.LR_LOADFROMFILE);
            if (_icon == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "タスクトレイのアイコンを読み込めません: " + _iconPath);

            _menu = CreateMenu();
            AddIcon();
        }

        static IntPtr CreateMenu()
        {
            IntPtr menu = Win32.CreatePopupMenu();
            if (menu == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "メニューを作れません。");
            try
            {
                AppendItem(menu, OpenSettingsItemId, OpenSettingsLabel);
                AppendItem(menu, QuitItemId, QuitLabel);
                // 左クリックで行う操作を太字で示す。
                if (!Win32.SetMenuDefaultItem(menu, OpenSettingsItemId, 0))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "メニューの既定の項目を設定できません。");
                return menu;
            }
            catch
            {
                // 元の例外を優先するため、破棄の失敗は無視する。
                Win32.DestroyMenu(menu);
                throw;
            }
        }

        static void AppendItem(IntPtr menu, uint id, string label)
        {
            if (!Win32.AppendMenu(menu, Win32.MF_STRING, new UIntPtr(id), label))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "メニューに項目を追加できません: " + label);
        }

        Win32.NOTIFYICONDATA CreateIconData()
        {
            return new Win32.NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf<Win32.NOTIFYICONDATA>(),
                hWnd = _window,
                uID = IconId,
            };
        }

        // Shell_NotifyIcon は失敗の理由を GetLastError に残さないため、失敗は理由なしの例外にする。
        void AddIcon()
        {
            Win32.NOTIFYICONDATA data = CreateIconData();
            // NOTIFYICON_VERSION_4 では、NIF_SHOWTIP がないと標準のツールチップを表示しない。
            data.uFlags = Win32.NIF_MESSAGE | Win32.NIF_ICON | Win32.NIF_TIP | Win32.NIF_SHOWTIP;
            data.uCallbackMessage = CallbackMessage;
            data.hIcon = _icon;
            data.szTip = _tooltip;
            if (!Win32.Shell_NotifyIcon(Win32.NIM_ADD, ref data))
                throw new InvalidOperationException("タスクトレイにアイコンを追加できません。");
            _iconAdded = true;

            // 右クリックとキーボードによるメニューの要求を WM_CONTEXTMENU、左クリックを NIN_SELECT で受け取る形式にする。
            data.uVersion = Win32.NOTIFYICON_VERSION_4;
            if (!Win32.Shell_NotifyIcon(Win32.NIM_SETVERSION, ref data))
                throw new InvalidOperationException("タスクトレイのアイコンの形式（NOTIFYICON_VERSION_4）を設定できません。");
        }

        void RemoveIcon()
        {
            if (!_iconAdded) return;
            _iconAdded = false;
            Win32.NOTIFYICONDATA data = CreateIconData();
            if (!Win32.Shell_NotifyIcon(Win32.NIM_DELETE, ref data))
                throw new InvalidOperationException("タスクトレイのアイコンを削除できません。");
        }

        void PumpMessages()
        {
            while (true)
            {
                int result = Win32.GetMessage(out Win32.MSG message, IntPtr.Zero, 0, 0);
                if (result == 0) return;
                if (result == -1) throw new Win32Exception(Marshal.GetLastWin32Error(), "タスクトレイのウィンドウのメッセージを取得できません。");
                Win32.TranslateMessage(ref message);
                Win32.DispatchMessage(ref message);
            }
        }

        [AOT.MonoPInvokeCallback(typeof(Win32.WndProc))]
        static IntPtr OnMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam)
        {
            // インスタンスを関連付ける前（ウィンドウの作成中）に届くメッセージは、既定の処理に任せる。
            IntPtr selfPointer = Win32.GetWindowLongPtr(window, Win32.GWLP_USERDATA);
            if (selfPointer == IntPtr.Zero) return Win32.DefWindowProc(window, message, wParam, lParam);

            var self = (TrayIcon)GCHandle.FromIntPtr(selfPointer).Target;
            // コールバックの中で送出した例外は、ネイティブのコードを越えて伝わらないため、キューに積んでメインスレッドで送出する。
            try
            {
                if (self.Handle(message, wParam, lParam)) return IntPtr.Zero;
            }
            catch (Exception e)
            {
                self._failures.Enqueue(e);
                return IntPtr.Zero;
            }
            return Win32.DefWindowProc(window, message, wParam, lParam);
        }

        // 処理したメッセージなら true。false なら既定の処理に任せる。
        bool Handle(uint message, IntPtr wParam, IntPtr lParam)
        {
            if (message == CallbackMessage)
            {
                OnIconEvent(wParam, lParam);
                return true;
            }
            if (message == ShutdownMessage)
            {
                RequestShutdown();
                return true;
            }
            if (message == _taskbarCreatedMessage)
            {
                // エクスプローラーが再起動すると、登録していたアイコンは失われている。
                _iconAdded = false;
                AddIcon();
                return true;
            }

            switch (message)
            {
                case Win32.WM_CLOSE:
                    // 終了は Dispose からの依頼でだけ行う。他のプロセスから届いても閉じない。
                    return true;
                case Win32.WM_DESTROY:
                    _windowDestroyed = true;
                    try
                    {
                        RemoveIcon();
                    }
                    finally
                    {
                        Win32.PostQuitMessage(0);
                    }
                    return true;
                default:
                    return false;
            }
        }

        // NOTIFYICON_VERSION_4 の通知。lParam の下位 16 ビットがイベント、wParam がメニューを出す位置（スクリーン座標）。
        void OnIconEvent(IntPtr wParam, IntPtr lParam)
        {
            uint iconEvent = (uint)((long)lParam & 0xFFFF);
            switch (iconEvent)
            {
                case Win32.NIN_SELECT:
                case Win32.NIN_KEYSELECT:
                    _commands.Enqueue(TrayCommand.OpenSettings);
                    break;
                case Win32.WM_CONTEXTMENU:
                    long position = (long)wParam;
                    ShowMenu((short)(position & 0xFFFF), (short)((position >> 16) & 0xFFFF));
                    break;
            }
        }

        void ShowMenu(int x, int y)
        {
            // メニューの外をクリックしたときにメニューが閉じるよう、メニューの所有者を前面にする（TrackPopupMenu の仕様上の制約）。
            Win32.SetForegroundWindow(_window);
            int item;
            _menuOpen = true;
            try
            {
                // TPM_RETURNCMD では、選ばれなかったときと失敗したときの両方が 0 になり、区別できない。
                item = Win32.TrackPopupMenuEx(_menu, Win32.TPM_RIGHTBUTTON | Win32.TPM_RETURNCMD | Win32.TPM_NONOTIFY, x, y, _window, IntPtr.Zero);
            }
            finally
            {
                _menuOpen = false;
            }

            if (_shutdownRequested)
            {
                DestroyWindow();
                return;
            }
            // 同じく仕様上の制約で、次にメニューを開いたときにすぐ閉じないよう、空のメッセージを送る。
            Win32.PostMessage(_window, Win32.WM_NULL, IntPtr.Zero, IntPtr.Zero);

            switch ((uint)item)
            {
                case 0:
                    break;
                case OpenSettingsItemId:
                    _commands.Enqueue(TrayCommand.OpenSettings);
                    break;
                case QuitItemId:
                    _commands.Enqueue(TrayCommand.Quit);
                    break;
                default:
                    throw new InvalidOperationException("未知のメニューの項目: " + item);
            }
        }

        void RequestShutdown()
        {
            _shutdownRequested = true;
            // メニューを開いている間は、閉じた後に ShowMenu がウィンドウを破棄する。
            if (_menuOpen)
            {
                Win32.EndMenu();
                return;
            }
            DestroyWindow();
        }

        void DestroyWindow()
        {
            if (!Win32.DestroyWindow(_window))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "タスクトレイのウィンドウを破棄できません。");
        }

        // アイコンのスレッドの終わりに呼ぶ。起動の途中で失敗したときは、作ったものだけを解放する。
        void ReleaseResources()
        {
            if (_window != IntPtr.Zero && !_windowDestroyed && !Win32.DestroyWindow(_window))
                _failures.Enqueue(new Win32Exception(Marshal.GetLastWin32Error(), "タスクトレイのウィンドウを破棄できません。"));
            if (_menu != IntPtr.Zero && !Win32.DestroyMenu(_menu))
                _failures.Enqueue(new Win32Exception(Marshal.GetLastWin32Error(), "メニューを破棄できません。"));
            if (_icon != IntPtr.Zero && !Win32.DestroyIcon(_icon))
                _failures.Enqueue(new Win32Exception(Marshal.GetLastWin32Error(), "タスクトレイのアイコンを破棄できません。"));
            if (_classRegistered && !Win32.UnregisterClass(WindowClassName, _instance))
                _failures.Enqueue(new Win32Exception(Marshal.GetLastWin32Error(), "タスクトレイのウィンドウのクラスの登録を解除できません。"));
        }
    }
}
