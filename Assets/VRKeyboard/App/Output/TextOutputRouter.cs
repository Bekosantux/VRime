using System;

namespace VRKeyboard.App.Output
{
    /// <summary>
    /// 送信時点の前面ウィンドウで出力先を決めて送る（SPEC 7.2）。
    /// VRChat なら OSC、それ以外ならデスクトップへ送る。前面が本アプリ自身のときは送らない。
    /// 結果の通知に送信内容は含めない。
    /// </summary>
    public sealed class TextOutputRouter
    {
        public const string VRChatProcessName = "VRChat";

        readonly AppSettings _settings;
        readonly IChatboxOutput _chatbox;
        readonly IDesktopOutput _desktop;
        readonly Func<ForegroundWindowInfo> _foreground;
        readonly int _ownProcessId;

        public TextOutputRouter(AppSettings settings, IChatboxOutput chatbox, IDesktopOutput desktop,
            Func<ForegroundWindowInfo> foreground, int ownProcessId)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _chatbox = chatbox ?? throw new ArgumentNullException(nameof(chatbox));
            _desktop = desktop ?? throw new ArgumentNullException(nameof(desktop));
            _foreground = foreground ?? throw new ArgumentNullException(nameof(foreground));
            _ownProcessId = ownProcessId;
        }

        /// <summary>実際の前面ウィンドウと自プロセスを使うルーター。</summary>
        public static TextOutputRouter Create(AppSettings settings, VRChatChatbox chatbox, DesktopTextSender desktop)
        {
            return new TextOutputRouter(settings, chatbox, desktop, ForegroundWindowInfo.Capture, ForegroundWindowInfo.CurrentProcessId);
        }

        public OutputResult Send(string text)
        {
            if (string.IsNullOrEmpty(text)) throw new ArgumentException("送信する文字列が空です。", nameof(text));

            ForegroundWindowInfo window = _foreground();
            if (window == null)
                return OutputResult.Failed(OutputFailure.NoWindow, "送信先のウィンドウがありません");
            if (window.ProcessId == _ownProcessId)
                return OutputResult.Failed(OutputFailure.OwnWindow, "キーボード自身には送信できません");

            if (string.Equals(window.ProcessName, VRChatProcessName, StringComparison.OrdinalIgnoreCase))
            {
                _chatbox.Send(text);
                return OutputResult.Succeeded("VRChat に送信しました");
            }

            switch (_desktop.Send(window, text, _settings.PressEnterAfterDesktopSend))
            {
                case DesktopSendResult.Sent:
                    return OutputResult.Succeeded(DisplayName(window) + " に送信しました");
                case DesktopSendResult.TargetElevated:
                    return OutputResult.Failed(OutputFailure.ElevatedWindow, "管理者権限のウィンドウには送信できません");
                case DesktopSendResult.Failed:
                    return OutputResult.Failed(OutputFailure.SendFailed, "送信に失敗しました");
                default:
                    throw new InvalidOperationException("未知の送信結果");
            }
        }

        static string DisplayName(ForegroundWindowInfo window)
        {
            if (!string.IsNullOrEmpty(window.DisplayName)) return window.DisplayName;
            if (!string.IsNullOrEmpty(window.ProcessName)) return window.ProcessName;
            return "デスクトップのアプリ";
        }
    }
}
