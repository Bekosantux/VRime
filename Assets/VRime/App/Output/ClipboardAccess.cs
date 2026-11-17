using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using VRime.App.Native;

namespace VRime.App.Output
{
    /// <summary>
    /// クリップボードの開閉と、送信用の文字列の書き込み（SPEC 7.2）。メインスレッド専用。
    /// 所有者のウィンドウなしで開くと、空にした後の SetClipboardData が失敗するため、メッセージ専用のウィンドウを所有者にする。
    /// </summary>
    internal sealed class ClipboardAccess : IDisposable
    {
        // 他のアプリが開いている間は開けない。開いている時間は通常ごく短いため、少し待って試し直す。
        const int OpenAttempts = 5;
        const int OpenRetryIntervalMilliseconds = 10;

        // クリップボードの履歴・クラウドへの同期・監視アプリに、送信した文字列と、戻した内容を記録させないための形式。
        readonly uint _excludeFromMonitor = Register("ExcludeClipboardContentFromMonitorProcessing");
        readonly uint _canIncludeInHistory = Register("CanIncludeInClipboardHistory");
        readonly uint _canUploadToCloud = Register("CanUploadToCloudClipboard");

        IntPtr _owner;
        bool _open;

        public ClipboardAccess()
        {
            _owner = Win32.CreateWindowEx(0, "STATIC", "VRimeClipboardOwner", 0, 0, 0, 0, 0,
                Win32.HWND_MESSAGE, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (_owner == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "クリップボードの所有者のウィンドウを作れません。");
        }

        /// <summary>クリップボードの通番。内容が変わるたびに増える。</summary>
        public static uint SequenceNumber => Win32.GetClipboardSequenceNumber();

        /// <summary>
        /// クリップボードを開く。<paramref name="retry"/> が true なら、他のアプリが開いている間は少し待って試し直す。
        /// 開けなければ false。開けたら必ず <see cref="Close"/> を呼ぶ。
        /// </summary>
        public bool TryOpen(bool retry)
        {
            ThrowIfDisposed();
            if (_open) throw new InvalidOperationException("クリップボードは既に開いています。");

            int attempts = retry ? OpenAttempts : 1;
            for (int i = 0; i < attempts; i++)
            {
                if (i > 0) Thread.Sleep(OpenRetryIntervalMilliseconds);
                if (Win32.OpenClipboard(_owner))
                {
                    _open = true;
                    return true;
                }
            }
            return false;
        }

        public void Close()
        {
            if (!_open) throw new InvalidOperationException("クリップボードは開いていません。");
            _open = false;
            if (!Win32.CloseClipboard()) throw new Win32Exception(Marshal.GetLastWin32Error(), "クリップボードを閉じられません。");
        }

        /// <summary>開いているクリップボードを空にし、文字列だけを置く。</summary>
        public void WriteText(string text)
        {
            if (string.IsNullOrEmpty(text)) throw new ArgumentException("書き込む文字列が空です。", nameof(text));
            RequireOpen();
            if (!Win32.EmptyClipboard()) throw new Win32Exception(Marshal.GetLastWin32Error(), "クリップボードを空にできません。");

            // CF_UNICODETEXT は終端の NUL を含める。
            var data = new byte[(text.Length + 1) * sizeof(char)];
            Buffer.BlockCopy(text.ToCharArray(), 0, data, 0, text.Length * sizeof(char));
            SetGlobal(Win32.CF_UNICODETEXT, data);
            MarkAsTransient();
        }

        /// <summary>開いているクリップボードに、退避した内容を戻す。</summary>
        public void Restore(ClipboardSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            RequireOpen();
            snapshot.Restore();
            // 戻した内容は既に履歴にあるため、同じ内容を二重に記録させない。
            MarkAsTransient();
        }

        /// <summary>開いているクリップボードの内容を退避する。</summary>
        public ClipboardSnapshot Capture()
        {
            RequireOpen();
            return ClipboardSnapshot.Capture();
        }

        /// <summary>メモリの形式を置く。置けたときはクリップボードがメモリを所有する。</summary>
        internal static void SetGlobal(uint format, byte[] data)
        {
            IntPtr memory = Win32.GlobalAlloc(Win32.GMEM_MOVEABLE, new UIntPtr((uint)data.Length));
            if (memory == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "クリップボード用のメモリを確保できません。");

            IntPtr pointer = Win32.GlobalLock(memory);
            if (pointer == IntPtr.Zero)
            {
                int lockError = Marshal.GetLastWin32Error();
                Win32.GlobalFree(memory);
                throw new Win32Exception(lockError, "クリップボード用のメモリをロックできません。");
            }
            Marshal.Copy(data, 0, pointer, data.Length);
            Win32.GlobalUnlock(memory);

            if (Win32.SetClipboardData(format, memory) != IntPtr.Zero) return;
            int error = Marshal.GetLastWin32Error();
            Win32.GlobalFree(memory);
            throw new Win32Exception(error, "クリップボードに内容を置けません（形式 " + format + "）。");
        }

        void MarkAsTransient()
        {
            // 形式の存在だけで意味を持つ（中身は問われない）。
            SetGlobal(_excludeFromMonitor, BitConverter.GetBytes(0));
            // DWORD の 0 で「含めない」。
            SetGlobal(_canIncludeInHistory, BitConverter.GetBytes(0));
            SetGlobal(_canUploadToCloud, BitConverter.GetBytes(0));
        }

        static uint Register(string name)
        {
            uint format = Win32.RegisterClipboardFormat(name);
            if (format == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "クリップボードの形式を登録できません: " + name);
            return format;
        }

        void RequireOpen()
        {
            ThrowIfDisposed();
            if (!_open) throw new InvalidOperationException("クリップボードが開いていません。");
        }

        void ThrowIfDisposed()
        {
            if (_owner == IntPtr.Zero) throw new ObjectDisposedException(nameof(ClipboardAccess));
        }

        public void Dispose()
        {
            if (_owner == IntPtr.Zero) return;
            if (_open)
            {
                _open = false;
                Win32.CloseClipboard();
            }
            Win32.DestroyWindow(_owner);
            _owner = IntPtr.Zero;
        }
    }
}
