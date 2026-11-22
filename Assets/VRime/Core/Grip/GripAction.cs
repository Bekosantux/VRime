namespace VRime.Core.Grip
{
    /// <summary>機能レイヤーのキーの動作。</summary>
    public enum GripAction
    {
        /// <summary>消去。</summary>
        Erase,

        /// <summary>空白。</summary>
        Space,

        /// <summary>小゛゜（直前の文字の切り替え。子音側のトリガーと同じ動作）。</summary>
        CharacterModifier,

        /// <summary>全/半。</summary>
        ToggleWidth,

        /// <summary>数字モードの切り替え。</summary>
        ToggleNumber,

        CursorLeft,
        CursorRight,

        /// <summary>大文字の オン / オフ（↑）。</summary>
        ToggleCase,

        /// <summary>言語。</summary>
        Language,

        Convert,
        Commit,

        /// <summary>貼付（貼り付けモードの オン / オフ）。</summary>
        TogglePasteMode,
    }
}
