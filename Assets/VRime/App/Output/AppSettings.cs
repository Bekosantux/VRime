using System;

namespace VRime.App.Output
{
    /// <summary>App 層の設定値。不正な値は例外。設定画面と設定ファイルにはまだ含めていない（docs/ROADMAP.md「設定項目」）。</summary>
    public sealed class AppSettings
    {
        public const string DefaultOscHost = "127.0.0.1";
        public const int DefaultOscPort = 9000;

        string _oscHost = DefaultOscHost;
        int _oscPort = DefaultOscPort;

        /// <summary>VRChat へ送るときに通知音を鳴らすか。</summary>
        public bool PlayChatboxNotificationSound { get; set; } = true;

        /// <summary>デスクトップへ送った後に Enter を押すか。</summary>
        public bool PressEnterAfterDesktopSend { get; set; }

        /// <summary>OSC の宛先ホスト（IP アドレスまたはホスト名）。</summary>
        public string OscHost
        {
            get => _oscHost;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException("OSC の宛先ホストが空です。", nameof(value));
                if (Uri.CheckHostName(value) == UriHostNameType.Unknown)
                    throw new ArgumentException("OSC の宛先ホストの形式が正しくありません。", nameof(value));
                _oscHost = value;
            }
        }

        public int OscPort
        {
            get => _oscPort;
            set
            {
                if (value < 1 || value > 65535)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "ポート番号は 1〜65535 である必要があります。");
                _oscPort = value;
            }
        }
    }
}
