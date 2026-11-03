using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace VRKeyboard.App.Output
{
    /// <summary>前面ウィンドウの情報。</summary>
    public sealed class ForegroundWindowInfo
    {
        public ForegroundWindowInfo(IntPtr handle, int processId, string processName, string displayName)
        {
            if (handle == IntPtr.Zero) throw new ArgumentException("ウィンドウハンドルが 0 です。", nameof(handle));
            if (processId <= 0) throw new ArgumentOutOfRangeException(nameof(processId), processId, "プロセス ID は正の値である必要があります。");
            Handle = handle;
            ProcessId = processId;
            ProcessName = processName;
            DisplayName = displayName;
        }

        public IntPtr Handle { get; }
        public int ProcessId { get; }

        /// <summary>実行ファイル名（拡張子なし）。プロセスを開けず取得できなかったときは null。</summary>
        public string ProcessName { get; }

        /// <summary>利用者に見せるアプリ名（実行ファイルの説明。例: メモ帳）。取得できなかったときは null。</summary>
        public string DisplayName { get; }

        public static int CurrentProcessId => (int)Win32.GetCurrentProcessId();

        /// <summary>ウィンドウを持つプロセスの名前を調べる。プロセスの照会とファイルの読み取りを伴うため、毎フレームは呼ばない。</summary>
        public static ForegroundWindowInfo Describe(IntPtr handle, int processId)
        {
            string processName = null;
            string displayName = null;
            string path = QueryProcessPath((uint)processId);
            if (path != null)
            {
                processName = Path.GetFileNameWithoutExtension(path);
                displayName = QueryFileDescription(path);
            }
            return new ForegroundWindowInfo(handle, processId, processName, displayName);
        }

        // 昇格したプロセスでも、制限付きの照会権限なら開ける。開けないもの（保護されたプロセスなど）は null。
        static string QueryProcessPath(uint pid)
        {
            IntPtr process = Win32.OpenProcess(Win32.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
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

        static string QueryFileDescription(string path)
        {
            try
            {
                string description = FileVersionInfo.GetVersionInfo(path).FileDescription;
                return string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (Win32Exception)
            {
                return null;
            }
        }
    }
}
