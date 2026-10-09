using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace VRKeyboard.App.Output
{
    internal enum ElevationState
    {
        NotElevated,
        Elevated,

        /// <summary>プロセスまたはトークンを開く権限がなく、状態を調べられなかった。</summary>
        AccessDenied,
    }

    /// <summary>
    /// プロセスのトークンの昇格状態（TokenElevation）を調べる。
    /// 昇格した（管理者権限の）プロセスへの SendInput は UIPI により黙って捨てられ、戻り値でも失敗を検出できない。
    /// そのため送信前に対象の状態を調べる。昇格したプロセスのトークンは、昇格していない呼び出し側からは
    /// OpenProcessToken がアクセス拒否になることがあり、その場合も「送れない」側に倒す。
    /// </summary>
    internal static class ProcessElevation
    {
        static bool? s_selfElevated;

        /// <summary>本アプリ自身が昇格して動いているか。昇格していれば、昇格した相手にも送れる。</summary>
        public static bool IsSelfElevated
        {
            get
            {
                if (!s_selfElevated.HasValue)
                {
                    ElevationState state = QueryHandle(Win32.GetCurrentProcess());
                    if (state == ElevationState.AccessDenied)
                        throw new InvalidOperationException("自プロセスのトークンを開けません。");
                    s_selfElevated = state == ElevationState.Elevated;
                }
                return s_selfElevated.Value;
            }
        }

        /// <summary>対象プロセスの昇格状態。プロセスが存在しないなど、アクセス拒否以外の失敗は <see cref="Win32Exception"/>。</summary>
        public static ElevationState Query(int processId)
        {
            IntPtr process = Win32.OpenProcess(Win32.PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)processId);
            if (process == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                if (error == Win32.ERROR_ACCESS_DENIED) return ElevationState.AccessDenied;
                throw new Win32Exception(error, "プロセスを開けません。");
            }

            try
            {
                return QueryHandle(process);
            }
            finally
            {
                Win32.CloseHandle(process);
            }
        }

        static ElevationState QueryHandle(IntPtr process)
        {
            if (!Win32.OpenProcessToken(process, Win32.TOKEN_QUERY, out IntPtr token))
            {
                int error = Marshal.GetLastWin32Error();
                if (error == Win32.ERROR_ACCESS_DENIED) return ElevationState.AccessDenied;
                throw new Win32Exception(error, "プロセスのトークンを開けません。");
            }

            try
            {
                if (!Win32.GetTokenInformation(token, Win32.TokenElevation, out Win32.TOKEN_ELEVATION elevation,
                        Marshal.SizeOf<Win32.TOKEN_ELEVATION>(), out _))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "トークンの昇格状態を取得できません。");
                }
                return elevation.TokenIsElevated != 0 ? ElevationState.Elevated : ElevationState.NotElevated;
            }
            finally
            {
                Win32.CloseHandle(token);
            }
        }
    }
}
