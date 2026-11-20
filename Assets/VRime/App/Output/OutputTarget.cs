using System;

namespace VRime.App.Output
{
    public enum OutputTargetKind
    {
        /// <summary>前面のウィンドウがない。</summary>
        None,

        /// <summary>前面が本アプリ自身。送らない。</summary>
        OwnWindow,

        /// <summary>前面が VRChat。チャットボックスへ送る。</summary>
        VRChat,

        /// <summary>前面がそれ以外のアプリ。キー入力として送る。</summary>
        Desktop,
    }

    /// <summary>前面のウィンドウから決まる送信先（docs/spec/output.md「送信先」）。</summary>
    public sealed class OutputTarget
    {
        const string VRChatProcessName = "VRChat";

        public static readonly OutputTarget None = new OutputTarget(OutputTargetKind.None, null);

        OutputTarget(OutputTargetKind kind, ForegroundWindowInfo window)
        {
            Kind = kind;
            Window = window;
        }

        public OutputTargetKind Kind { get; }

        /// <summary>前面のウィンドウ。<see cref="OutputTargetKind.None"/> のときは null。</summary>
        public ForegroundWindowInfo Window { get; }

        /// <summary>利用者に見せる送信先の名前。</summary>
        public string Name
        {
            get
            {
                switch (Kind)
                {
                    case OutputTargetKind.None: return "なし";
                    case OutputTargetKind.OwnWindow: return "このアプリ";
                    case OutputTargetKind.VRChat: return "VRChat";
                    case OutputTargetKind.Desktop:
                        if (!string.IsNullOrEmpty(Window.DisplayName)) return Window.DisplayName;
                        if (!string.IsNullOrEmpty(Window.ProcessName)) return Window.ProcessName;
                        return "デスクトップのアプリ";
                    default: throw new InvalidOperationException("未知の送信先: " + Kind);
                }
            }
        }

        /// <summary>前面のウィンドウ（なければ null）から送信先を決める。</summary>
        public static OutputTarget Classify(ForegroundWindowInfo window, int ownProcessId)
        {
            if (window == null) return None;
            if (window.ProcessId == ownProcessId) return new OutputTarget(OutputTargetKind.OwnWindow, window);
            if (IsVRChatProcess(window.ProcessName)) return new OutputTarget(OutputTargetKind.VRChat, window);
            return new OutputTarget(OutputTargetKind.Desktop, window);
        }

        /// <summary>プロセス名（拡張子なし。調べられなかったときは null）が VRChat か。</summary>
        public static bool IsVRChatProcess(string processName)
        {
            return string.Equals(processName, VRChatProcessName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
