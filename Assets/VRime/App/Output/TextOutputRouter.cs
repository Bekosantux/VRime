using System;

namespace VRime.App.Output
{
    /// <summary>
    /// 送信先（<see cref="OutputTarget"/>）に応じて送る（docs/spec/output.md「送信先」）。
    /// VRChat なら OSC、それ以外ならデスクトップへ送る。前面が本アプリ自身のときは送らない。
    /// 失敗の通知に送信内容は含めない。
    /// </summary>
    public sealed class TextOutputRouter
    {
        readonly AppSettings _settings;
        readonly IChatboxOutput _chatbox;
        readonly IDesktopOutput _desktop;
        readonly Func<OutputTarget> _target;

        public TextOutputRouter(AppSettings settings, IChatboxOutput chatbox, IDesktopOutput desktop, Func<OutputTarget> target)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _chatbox = chatbox ?? throw new ArgumentNullException(nameof(chatbox));
            _desktop = desktop ?? throw new ArgumentNullException(nameof(desktop));
            _target = target ?? throw new ArgumentNullException(nameof(target));
        }

        /// <summary>前面のウィンドウの監視に従うルーター。</summary>
        public static TextOutputRouter Create(AppSettings settings, VRChatChatbox chatbox, DesktopTextSender desktop,
            ForegroundWindowMonitor monitor)
        {
            if (monitor == null) throw new ArgumentNullException(nameof(monitor));
            return new TextOutputRouter(settings, chatbox, desktop, () => monitor.Target);
        }

        public OutputResult Send(string text)
        {
            if (string.IsNullOrEmpty(text)) throw new ArgumentException("送信する文字列が空です。", nameof(text));

            OutputTarget target = _target() ?? throw new InvalidOperationException("送信先が null です。");
            switch (target.Kind)
            {
                case OutputTargetKind.None:
                    return OutputResult.Failed(OutputFailure.NoWindow, "送信先のウィンドウがありません");
                case OutputTargetKind.OwnWindow:
                    return OutputResult.Failed(OutputFailure.OwnWindow, "キーボード自身には送信できません");
                case OutputTargetKind.VRChat:
                    _chatbox.Send(text);
                    return OutputResult.Succeeded;
                case OutputTargetKind.Desktop:
                    return SendToDesktop(target, text);
                default:
                    throw new InvalidOperationException("未知の送信先: " + target.Kind);
            }
        }

        OutputResult SendToDesktop(OutputTarget target, string text)
        {
            switch (_desktop.Send(target.Window, text, _settings.PressEnterAfterDesktopSend))
            {
                case DesktopSendResult.Sent:
                    return OutputResult.Succeeded;
                case DesktopSendResult.TargetElevated:
                    return OutputResult.Failed(OutputFailure.ElevatedWindow, "管理者権限のウィンドウには送信できません");
                case DesktopSendResult.ClipboardBusy:
                    return OutputResult.Failed(OutputFailure.ClipboardBusy, "クリップボードが使用中のため送信できません");
                case DesktopSendResult.Failed:
                    return OutputResult.Failed(OutputFailure.SendFailed, "送信に失敗しました");
                default:
                    throw new InvalidOperationException("未知の送信結果");
            }
        }
    }
}
