using System;
using System.IO;
using System.Text;
using VRKeyboard.App.Native;

namespace VRKeyboard.App.Output
{
    /// <summary>
    /// プロセスの実行ファイルを調べる。
    /// <see cref="System.Diagnostics.Process"/> は Mono では VRChat などの保護されたプロセスを読めないため、制限付きの照会権限で開いて調べる。
    /// 昇格したプロセスもこの権限なら開ける。
    /// </summary>
    public static class ProcessImage
    {
        /// <summary>実行ファイルのフルパス。プロセスを開けないときは null。</summary>
        public static string QueryPath(uint processId)
        {
            IntPtr process = Win32.OpenProcess(Win32.PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
            if (process == IntPtr.Zero) return null;
            try
            {
                var buffer = new StringBuilder(1024);
                uint size = (uint)buffer.Capacity;
                return Win32.QueryFullProcessImageName(process, 0, buffer, ref size) ? buffer.ToString() : null;
            }
            finally
            {
                Win32.CloseHandle(process);
            }
        }

        /// <summary>実行ファイル名（拡張子なし）。プロセスを開けないときは null。</summary>
        public static string QueryName(uint processId)
        {
            string path = QueryPath(processId);
            return path != null ? NameOf(path) : null;
        }

        /// <summary>実行ファイルのパスから、プロセス名（拡張子なしのファイル名）を得る。</summary>
        public static string NameOf(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            return Path.GetFileNameWithoutExtension(path);
        }
    }
}
