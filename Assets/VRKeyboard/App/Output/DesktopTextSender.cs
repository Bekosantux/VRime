using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace VRKeyboard.App.Output
{
    public enum DesktopSendResult
    {
        Sent,

        /// <summary>対象が管理者権限で動いている（または昇格状態を調べられない）ため、送らなかった。</summary>
        TargetElevated,

        /// <summary>他のアプリがクリップボードを開いたままのため、送らなかった。</summary>
        ClipboardBusy,

        /// <summary>SendInput が要求した数を受け付けなかった。</summary>
        Failed,
    }

    public interface IDesktopOutput
    {
        DesktopSendResult Send(ForegroundWindowInfo target, string text, bool pressEnter);
    }

    /// <summary>
    /// 前面ウィンドウへ、クリップボードに置いた文字列を Ctrl+V で貼り付けて送る（SPEC 7.2）。
    /// 文字をキー入力として送ると、送信先の IME がオンのときに未確定の文字として取り込まれ、アプリによっては順序も崩れるため、
    /// IME を通らない貼り付けを使う。
    /// 元のクリップボードの内容は退避し、一定時間の後に <see cref="Update"/> で戻す。メインスレッド専用。
    /// </summary>
    public sealed class DesktopTextSender : IDesktopOutput, IDisposable
    {
        /// <summary>貼り付けてから元の内容を戻すまでの時間。送信先が貼り付けを処理し終えるのを待つ。</summary>
        public const double RestoreDelaySeconds = 1.0;

        /// <summary>戻す時刻を過ぎてから、クリップボードを開けないまま諦めるまでの時間。</summary>
        public const double RestoreGiveUpSeconds = 5.0;

        readonly Func<double> _clock;
        readonly ClipboardAccess _clipboard;
        readonly ClipboardRestoreSchedule<ClipboardSnapshot> _restore =
            new ClipboardRestoreSchedule<ClipboardSnapshot>(RestoreDelaySeconds, RestoreGiveUpSeconds);

        /// <param name="clock">秒単位の単調な時刻。</param>
        public DesktopTextSender(Func<double> clock)
        {
            // INPUT 構造体のオフセットは x64 を前提にしている。
            if (IntPtr.Size != 8) throw new PlatformNotSupportedException("64bit の環境が必要です。");
            if (Marshal.SizeOf<Win32.INPUT>() != 40) throw new InvalidOperationException("INPUT 構造体の大きさが想定と異なります。");
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _clipboard = new ClipboardAccess();
        }

        /// <summary>元のクリップボードの内容を戻すのを待っているか。</summary>
        public bool IsRestorePending => _restore.HasPending;

        public DesktopSendResult Send(ForegroundWindowInfo target, string text, bool pressEnter)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (string.IsNullOrEmpty(text)) throw new ArgumentException("送信する文字列が空です。", nameof(text));

            if (IsUnreachable(target.ProcessId)) return DesktopSendResult.TargetElevated;

            if (!_clipboard.TryOpen(retry: true)) return DesktopSendResult.ClipboardBusy;
            ClipboardSnapshot snapshot;
            try
            {
                // 復元待ちのまま続けて送ったときは、送信で置いた内容ではなく、利用者の元の内容を退避し続ける。
                if (!_restore.TryTakePending(ClipboardAccess.SequenceNumber, out snapshot))
                    snapshot = _clipboard.Capture();
                WriteOrRestore(text, snapshot);
            }
            finally
            {
                _clipboard.Close();
            }
            // 通番は閉じた後に読む。開いている間の書き込みで増えた後の値を、書き込んだときの値とする。
            _restore.Arm(snapshot, ClipboardAccess.SequenceNumber, _clock());

            Win32.INPUT[] inputs = BuildPasteInputs(pressEnter);
            uint sent = Win32.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Win32.INPUT>());
            return sent == (uint)inputs.Length ? DesktopSendResult.Sent : DesktopSendResult.Failed;
        }

        // 書き込みの途中で失敗すると、空にした後で利用者の内容が失われるため、退避した内容を戻してから例外を上げる。
        void WriteOrRestore(string text, ClipboardSnapshot snapshot)
        {
            try
            {
                _clipboard.WriteText(text);
            }
            catch (Win32Exception)
            {
                _clipboard.Restore(snapshot);
                throw;
            }
        }

        /// <summary>
        /// 毎フレーム呼ぶ。戻す時刻になっていれば、元のクリップボードの内容を戻す。
        /// 戻したときは <see cref="ClipboardRestoreAction.Restore"/>、戻さずに退避を捨てたときは Discarded / GaveUp を返す。
        /// </summary>
        public ClipboardRestoreAction Update()
        {
            ClipboardRestoreAction action = _restore.Poll(_clock(), ClipboardAccess.SequenceNumber);
            if (action != ClipboardRestoreAction.Restore) return action;
            // 開けなければ、次のフレームで試し直す。フレームを止めないよう、ここでは待たない。
            if (!_clipboard.TryOpen(retry: false)) return ClipboardRestoreAction.None;
            return RestoreWhileOpen();
        }

        /// <summary>
        /// 復元待ちがあれば、時刻を待たずに戻す。終了の前に呼ぶ。
        /// 戻した、または戻す必要がなければ true。クリップボードを開けずに戻せなかったときは false。
        /// </summary>
        public bool RestoreNow()
        {
            if (!_restore.HasPending) return true;
            if (!_clipboard.TryOpen(retry: true)) return false;
            RestoreWhileOpen();
            return true;
        }

        // 開いた後で通番を確かめ直す（開くまでの間に別のアプリが書き換えていることがあるため）。開いている間は他から書き換えられない。
        ClipboardRestoreAction RestoreWhileOpen()
        {
            try
            {
                if (!_restore.IsUnchanged(ClipboardAccess.SequenceNumber))
                {
                    _restore.Complete();
                    return ClipboardRestoreAction.Discarded;
                }
                ClipboardSnapshot snapshot = _restore.Pending;
                // 戻すのに失敗しても、同じ失敗を毎フレーム繰り返さないよう、復元待ちは捨てる。
                _restore.Complete();
                _clipboard.Restore(snapshot);
                return ClipboardRestoreAction.Restore;
            }
            finally
            {
                _clipboard.Close();
            }
        }

        public void Dispose()
        {
            _clipboard.Dispose();
        }

        // 昇格した相手には届かない（UIPI）。自身が昇格していれば相手が昇格していても送れる。
        static bool IsUnreachable(int processId)
        {
            ElevationState state;
            try
            {
                state = ProcessElevation.Query(processId);
            }
            catch (Win32Exception)
            {
                // 照会の間にプロセスが終了したなど。届く保証がないため送らない。
                return true;
            }

            switch (state)
            {
                case ElevationState.NotElevated: return false;
                case ElevationState.Elevated: return !ProcessElevation.IsSelfElevated;
                case ElevationState.AccessDenied: return true;
                default: throw new InvalidOperationException("未知の昇格状態: " + state);
            }
        }

        static Win32.INPUT[] BuildPasteInputs(bool pressEnter)
        {
            var inputs = new Win32.INPUT[pressEnter ? 6 : 4];
            inputs[0] = Key(Win32.VK_CONTROL, 0);
            inputs[1] = Key(Win32.VK_V, 0);
            inputs[2] = Key(Win32.VK_V, Win32.KEYEVENTF_KEYUP);
            inputs[3] = Key(Win32.VK_CONTROL, Win32.KEYEVENTF_KEYUP);
            if (pressEnter)
            {
                inputs[4] = Key(Win32.VK_RETURN, 0);
                inputs[5] = Key(Win32.VK_RETURN, Win32.KEYEVENTF_KEYUP);
            }
            return inputs;
        }

        // スキャンコードを読むアプリがあるため、仮想キーと合わせて送る。
        static Win32.INPUT Key(ushort virtualKey, uint flags)
        {
            ushort scan = (ushort)Win32.MapVirtualKey(virtualKey, Win32.MAPVK_VK_TO_VSC);
            return new Win32.INPUT { type = Win32.INPUT_KEYBOARD, wVk = virtualKey, wScan = scan, dwFlags = flags };
        }
    }
}
