namespace VRime.App.View
{
    /// <summary>
    /// 状態によらない色の役割。シーンの uGUI に <see cref="ThemeColor"/> で指定し、色はテーマ（<see cref="KeyboardTheme.ColorOf"/>）から取得する。
    /// 値はシーンに数値で保存されるため、途中に足したり並べ替えたりしない（足すときは末尾に置く）。
    /// 10 は欠番（セルのヒントの色は、選択の状態で変わるため <see cref="GripPanelCell"/> が設定する）。古いシーンの値と取り違えないよう、再利用しない。
    /// </summary>
    public enum ThemeColorRole
    {
        /// <summary>ボード（キーボードの本体）。</summary>
        KeyboardBoard = 0,

        /// <summary>ボードの縁。</summary>
        KeyboardRim = 1,

        /// <summary>入力欄と、チップを並べた背景。</summary>
        KeyboardField = 2,

        /// <summary>入力した文字など、メインの文字。</summary>
        KeyboardText = 3,

        /// <summary>送信先の名前・「変換中…」。</summary>
        KeyboardTextSub = 4,

        /// <summary>「送信先」の見出し。</summary>
        KeyboardTextMuted = 5,

        KeyboardCaret = 6,

        /// <summary>通知のバッジ。</summary>
        KeyboardNoticeBackground = 7,

        KeyboardNoticeText = 8,

        /// <summary>セルの下の点灯のバー。</summary>
        KeyboardLitBar = 9,

        /// <summary>候補欄のカード。</summary>
        KeyboardCard = 11,

        /// <summary>候補欄の見出しと候補の文字。</summary>
        KeyboardCardText = 12,

        /// <summary>候補欄の見出しと候補の間の線。</summary>
        KeyboardSeparator = 13,

        /// <summary>変換に失敗したときのバナー。</summary>
        KeyboardBannerBackground = 14,

        KeyboardBannerText = 15,

        /// <summary>設定画面の背景。</summary>
        SettingsBackground = 16,

        /// <summary>設定画面のタイトル・項目名・値・ボタンの文字。</summary>
        SettingsText = 17,

        /// <summary>設定画面のページの見出し。</summary>
        SettingsNote = 18,

        /// <summary>設定画面の下部の通知。</summary>
        SettingsNotice = 19,

        /// <summary>スライダーのトラック。</summary>
        SettingsTrack = 20,

        /// <summary>スライダーの値までのフィル。</summary>
        SettingsSliderFill = 21,

        /// <summary>同意の画面の後ろ。</summary>
        SettingsBackdrop = 22,

        /// <summary>同意の画面と、選択肢の一覧の背景。</summary>
        SettingsDialog = 23,

        /// <summary>選択肢の一覧の枠。</summary>
        SettingsListBorder = 24,

        /// <summary>設定画面のタブの列と内容を載せるパネル。</summary>
        SettingsPanel = 25,

        /// <summary>設定画面の行の区切り線。</summary>
        SettingsSeparator = 26,

        /// <summary>設定画面の下部の通知の背景。</summary>
        SettingsNoticeBackground = 27,
    }
}
