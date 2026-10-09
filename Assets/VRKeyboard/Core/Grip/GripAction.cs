namespace VRKeyboard.Core.Grip
{
    /// <summary>機能ページのキーの動作。</summary>
    public enum GripAction
    {
        /// <summary>消去。</summary>
        Erase,

        /// <summary>空白。</summary>
        Space,

        /// <summary>小゛゜。</summary>
        KanaModifier,

        /// <summary>全/半。</summary>
        ToggleWidth,

        /// <summary>数字ページの開閉。</summary>
        NumberPage,

        CursorLeft,
        CursorRight,

        /// <summary>大文字の オン / オフ（↑）。</summary>
        ToggleCase,

        /// <summary>言語。</summary>
        Language,

        Convert,
        Commit,
    }
}
