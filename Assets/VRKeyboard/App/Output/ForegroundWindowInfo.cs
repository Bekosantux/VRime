using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace VRKeyboard.App.Output
{
    /// <summary>送信時点の前面ウィンドウの情報。タイトルには文書名などが含まれるため、ログに出さない。</summary>
    public sealed class ForegroundWindowInfo
    {
        public ForegroundWindowInfo(IntPtr handle, int processId, string processName, string displayName, string title)
        {
            if (handle == IntPtr.Zero) throw new ArgumentException("ウィンドウハンドルが 0 です。", nameof(handle));
            if (processId <= 0) throw new ArgumentOutOfRangeException(nameof(processId), processId, "プロセス ID は正の値である必要があります。");
            Handle = handle;
            ProcessId = processId;
            ProcessName = processName;
            DisplayName = displayName;
            Title = title ?? throw new ArgumentNullException(nameof(title));
        }

        public IntPtr Handle { get; }
        public int ProcessId { get; }

        /// <summary>実行ファイル名（拡張子なし）。プロセスを開けず取得できなかったときは null。</summary>
        public string ProcessName { get; }

        /// <summary>利用者に見せるアプリ名（実行ファイルの説明。例: メモ帳）。取得できなかったときは null。</summary>
        public string DisplayName { get; }

        public string Title { get; }

        public static int CurrentProcessId => (int)Win32.GetCurrentProcessId();

        /// <summary>現在の前面ウィンドウを取得する。前面のウィンドウがなければ null。</summary>
        public static ForegroundWindowInfo Capture()
        {
            IntPtr handle = Win32.GetForegroundWindow();
            if (handle == IntPtr.Zero) return null;

            if (Win32.GetWindowThreadProcessId(handle, out uint pid) == 0 || pid == 0) return null;

            string processName = null;
            string displayName = null;
            string path = QueryProcessPath(pid);
            if (path != null)
            {
                processName = Path.GetFileNameWithoutExtension(path);
                displayName = QueryFileDescription(path);
            }

            return new ForegroundWindowInfo(handle, (int)pid, processName, displayName, QueryTitle(handle));
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

        static string QueryTitle(IntPtr handle)
        {
            int length = Win32.GetWindowTextLength(handle);
            if (length <= 0) return string.Empty;
            var buffer = new StringBuilder(length + 1);
            Win32.GetWindowText(handle, buffer, buffer.Capacity);
            return buffer.ToString();
        }
    }
}
