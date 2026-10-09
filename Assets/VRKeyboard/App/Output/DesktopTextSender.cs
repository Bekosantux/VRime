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

        /// <summary>SendInput が要求した数を受け付けなかった。</summary>
        Failed,
    }

    public interface IDesktopOutput
    {
        DesktopSendResult Send(ForegroundWindowInfo target, string text, bool pressEnter);
    }

    /// <summary>
    /// 前面ウィンドウへ SendInput + KEYEVENTF_UNICODE で文字を送る（SPEC 7.2）。相手の IME を通らず文字そのものが届く。
    /// サロゲートペアは UTF-16 の単位ごとにそのまま送る（絵文字の到達を試作で確認済み）。
    /// </summary>
    public sealed class DesktopTextSender : IDesktopOutput
    {
        public DesktopTextSender()
        {
            // INPUT 構造体のオフセットは x64 を前提にしている。
            if (IntPtr.Size != 8) throw new PlatformNotSupportedException("64bit の環境が必要です。");
            if (Marshal.SizeOf<Win32.INPUT>() != 40) throw new InvalidOperationException("INPUT 構造体の大きさが想定と異なります。");
        }

        public DesktopSendResult Send(ForegroundWindowInfo target, string text, bool pressEnter)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (string.IsNullOrEmpty(text)) throw new ArgumentException("送信する文字列が空です。", nameof(text));

            if (IsUnreachable(target.ProcessId)) return DesktopSendResult.TargetElevated;

            Win32.INPUT[] inputs = BuildInputs(text, pressEnter);
            uint sent = Win32.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Win32.INPUT>());
            return sent == (uint)inputs.Length ? DesktopSendResult.Sent : DesktopSendResult.Failed;
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

        static Win32.INPUT[] BuildInputs(string text, bool pressEnter)
        {
            var inputs = new Win32.INPUT[text.Length * 2 + (pressEnter ? 2 : 0)];
            for (int i = 0; i < text.Length; i++)
            {
                inputs[i * 2] = Unicode(text[i], 0);
                inputs[i * 2 + 1] = Unicode(text[i], Win32.KEYEVENTF_KEYUP);
            }
            if (pressEnter)
            {
                inputs[text.Length * 2] = new Win32.INPUT { type = Win32.INPUT_KEYBOARD, wVk = Win32.VK_RETURN };
                inputs[text.Length * 2 + 1] = new Win32.INPUT { type = Win32.INPUT_KEYBOARD, wVk = Win32.VK_RETURN, dwFlags = Win32.KEYEVENTF_KEYUP };
            }
            return inputs;
        }

        static Win32.INPUT Unicode(char unit, uint extraFlags)
        {
            return new Win32.INPUT { type = Win32.INPUT_KEYBOARD, wScan = unit, dwFlags = Win32.KEYEVENTF_UNICODE | extraFlags };
        }
    }
}
