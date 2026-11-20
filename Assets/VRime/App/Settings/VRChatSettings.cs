namespace VRime.App.Settings
{
    /// <summary>VRChat に関わる設定（docs/spec/output.md「VRChat」）。</summary>
    public sealed class VRChatSettings
    {
        public static readonly VRChatSettings Default = new VRChatSettings(focusOnStartup: true, showTypingText: true);

        public VRChatSettings(bool focusOnStartup, bool showTypingText)
        {
            FocusOnStartup = focusOnStartup;
            ShowTypingText = showTypingText;
        }

        /// <summary>起動時に VRChat が起動していれば、VRChat を前面にする。</summary>
        public bool FocusOnStartup { get; }

        /// <summary>VRChat が前面の間、入力中の文字をチャットボックスに表示する。</summary>
        public bool ShowTypingText { get; }

        public VRChatSettings WithFocusOnStartup(bool value) => new VRChatSettings(value, ShowTypingText);
        public VRChatSettings WithShowTypingText(bool value) => new VRChatSettings(FocusOnStartup, value);

        public bool Equals(VRChatSettings other)
        {
            return other != null && FocusOnStartup == other.FocusOnStartup && ShowTypingText == other.ShowTypingText;
        }

        public override bool Equals(object obj) => Equals(obj as VRChatSettings);

        public override int GetHashCode() => (FocusOnStartup ? 1 : 0) | (ShowTypingText ? 2 : 0);
    }
}
