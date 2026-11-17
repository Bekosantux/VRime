using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using VRime.App.Native;

namespace VRime.App.Output
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
            string path = ProcessImage.QueryPath((uint)processId);
            if (path != null)
            {
                processName = ProcessImage.NameOf(path);
                displayName = QueryFileDescription(path);
            }
            return new ForegroundWindowInfo(handle, processId, processName, displayName);
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
