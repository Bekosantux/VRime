using System;
using UnityEngine;
using UnityEngine.Serialization;
using VRime.App.Settings;

namespace VRime.App.View
{
    /// <summary>
    /// 1 つの色のテーマ（<see cref="ColorTheme"/>）の、キーボードと設定画面の色と文字の大きさ。テーマごとにアセットを置き、<see cref="ThemeCatalog"/> にまとめる。
    /// 状態によって切り替わる色は各ビューが読み、状態によらない色（背景・文字など）は役割（<see cref="ThemeColorRole"/>）として <see cref="ThemeColor"/> が設定する。
    /// 配置・大きさはシーンに置く。フィールドの初期値はダークの値。
    /// </summary>
    [CreateAssetMenu(menuName = "VRime/Theme", fileName = "KeyboardTheme")]
    public sealed class KeyboardTheme : ScriptableObject
    {
        [Tooltip("このアセットがどのテーマか。")]
        [SerializeField] ColorTheme _id = ColorTheme.Dark;

        [Header("キーボードの背景と文字")]
        [Tooltip("ボード（キーボードの本体）。")]
        [SerializeField] Color _keyboardBoard = new Color(0.0706f, 0.0824f, 0.1059f, 0.949f);

        [Tooltip("ボードの縁。")]
        [SerializeField] Color _keyboardRim = new Color(0.1255f, 0.1373f, 0.1608f, 1f);

        [Tooltip("入力欄と、チップを並べた背景。")]
        [SerializeField] Color _keyboardField = new Color(0.1059f, 0.1216f, 0.1529f, 1f);

        [Tooltip("入力した文字など、メインの文字。")]
        [SerializeField] Color _keyboardText = new Color(0.96f, 0.97f, 0.98f, 1f);

        [Tooltip("送信先の名前・「変換中…」。")]
        [SerializeField] Color _keyboardTextSub = new Color(0.788f, 0.812f, 0.847f, 1f);

        [Tooltip("「送信先」の見出し。")]
        [SerializeField] Color _keyboardTextMuted = new Color(0.557f, 0.596f, 0.659f, 1f);

        [SerializeField] Color _keyboardCaret = new Color(0.451f, 0.78f, 1f, 1f);

        [Tooltip("送信できなかったときなどの通知のバッジ。")]
        [SerializeField] Color _keyboardNoticeBackground = new Color(0.180f, 0.176f, 0.153f, 1f);

        [SerializeField] Color _keyboardNoticeText = new Color(1f, 0.878f, 0.51f, 1f);

        [Tooltip("オンの状態を示す、セルの下の点灯のバー。")]
        [SerializeField] Color _keyboardLitBar = new Color(1f, 0.78f, 0.2f, 1f);

        [Tooltip("候補欄のカード（文節の列・応答待ち）。")]
        [SerializeField] Color _keyboardCard = new Color(0.094f, 0.110f, 0.137f, 0.969f);

        [Tooltip("候補欄の見出しと候補の文字。")]
        [SerializeField] Color _keyboardCardText = new Color(0.96f, 0.97f, 0.98f, 1f);

        [Tooltip("候補欄の見出しと候補の間の線。")]
        [SerializeField] Color _keyboardSeparator = new Color(0.149f, 0.165f, 0.188f, 1f);

        [Tooltip("変換に失敗したときのバナー。")]
        [SerializeField] Color _keyboardBannerBackground = new Color(0.227f, 0.141f, 0.094f, 0.969f);

        [SerializeField] Color _keyboardBannerText = new Color(1f, 0.627f, 0.4f, 1f);

        [Header("グリップのパネルのセル")]
        [SerializeField] Color _cell = new Color(0.180f, 0.208f, 0.259f, 1f);
        [SerializeField] Color _cellDisabled = new Color(0.098f, 0.11f, 0.137f, 1f);
        [SerializeField] Color _cellSelected = new Color(0.14f, 0.50f, 0.95f, 1f);
        [SerializeField] Color _cellText = new Color(0.96f, 0.97f, 0.98f, 1f);

        [Tooltip("オンの状態を示す点灯中のセルの文字。選択中は背景が変わるため使わない。")]
        [SerializeField] Color _cellTextLit = new Color(1.00f, 0.78f, 0.20f, 1f);

        [Tooltip("無効なセルと、文字のないセルの文字。")]
        [SerializeField] Color _cellTextDisabled = new Color(0.31f, 0.337f, 0.388f, 1f);

        [Tooltip("無効なセルと、文字のないセルを選んでいるときの文字。")]
        [SerializeField] Color _cellTextDisabledSelected = new Color(0.737f, 0.827f, 0.969f, 1f);

        [Tooltip("セルに重ねるヒント（方向の文字と、機能ページのキーの頭文字）。メインの文字を妨げないよう薄くする。")]
        [FormerlySerializedAs("_keyboardHint")]
        [SerializeField] Color _cellHint = new Color(0.96f, 0.97f, 0.98f, 0.5f);

        [Tooltip("選択中のセルのヒント。選択中の背景の上で読めるよう、通常のヒントとは別に決める。")]
        [SerializeField] Color _cellHintSelected = new Color(0.96f, 0.97f, 0.98f, 0.8f);

        [Tooltip("1文字のラベル。")]
        [SerializeField] float _cellFontSize = 54f;

        [Tooltip("2文字以上のラベル（機能ページのキー）。")]
        [SerializeField] float _cellFunctionFontSize = 38f;

        [Tooltip("変換中など、パネルが操作を受けない間の不透明度。")]
        [Range(0f, 1f)]
        [SerializeField] float _panelDimAlpha = 0.35f;

        [Header("パネルの見出し（役割）")]
        [Tooltip("役割（子音・母音）。")]
        [SerializeField] Color _captionRole = new Color(0.96f, 0.97f, 0.98f, 1f);

        [Tooltip("機能ページを開いている間の役割。")]
        [SerializeField] Color _captionRoleFunction = new Color(0.451f, 0.780f, 1.00f, 1f);

        [Header("ページ行（ページ・全角/半角・大文字/小文字）")]
        [Tooltip("作用しないチップ。チップを並べた背景が見えるよう、既定は透明。")]
        [SerializeField] Color _chip = new Color(0f, 0f, 0f, 0f);

        [SerializeField] Color _chipSelected = new Color(0.14f, 0.50f, 0.95f, 1f);

        [Tooltip("いまのページで作用する全角/半角・大文字/小文字。")]
        [SerializeField] Color _chipActive = new Color(0.20f, 0.34f, 0.52f, 1f);

        [SerializeField] Color _chipText = new Color(0.96f, 0.97f, 0.98f, 1f);
        [SerializeField] Color _chipTextInactive = new Color(0.494f, 0.525f, 0.584f, 1f);

        [Header("入力欄")]
        [Tooltip("未確定のかな（下線を付ける）。")]
        [SerializeField] Color _composition = new Color(1.00f, 0.88f, 0.51f, 1f);

        [Tooltip("文字数。")]
        [SerializeField] Color _length = new Color(0.557f, 0.596f, 0.659f, 1f);

        [Tooltip("文字数が上限ちょうどのとき。")]
        [SerializeField] Color _lengthAtLimit = new Color(1.00f, 0.60f, 0.35f, 1f);

        [Tooltip("文字数が上限を超えているとき（送信できない）。")]
        [SerializeField] Color _lengthOverLimit = new Color(1.00f, 0.36f, 0.36f, 1f);

        [Header("候補欄")]
        [Tooltip("選んでいない文節の見出しと候補。文節のカードが見えるよう、既定は透明。")]
        [SerializeField] Color _candidate = new Color(0f, 0f, 0f, 0f);

        [SerializeField] Color _candidateSelected = new Color(0.14f, 0.50f, 0.95f, 1f);

        [Header("設定画面の背景と文字")]
        [SerializeField] Color _settingsBackground = new Color(0.06f, 0.07f, 0.09f, 1f);

        [Tooltip("タイトル・項目名・値・ボタンの文字。")]
        [SerializeField] Color _settingsText = new Color(0.96f, 0.97f, 0.98f, 1f);

        [Tooltip("ページの見出し。")]
        [SerializeField] Color _settingsNote = new Color(0.6f, 0.64f, 0.7f, 1f);

        [Tooltip("画面の下部の通知（設定ファイルの問題・保存の失敗）。")]
        [SerializeField] Color _settingsNotice = new Color(1f, 0.6f, 0.35f, 1f);

        [Tooltip("タブの列と内容を載せるパネル。")]
        [SerializeField] Color _settingsPanel = new Color(0.106f, 0.122f, 0.153f, 1f);

        [Tooltip("行の区切り線。パネルの上に置くため、混ぜた後の色を不透明で指定する。")]
        [SerializeField] Color _settingsSeparator = new Color(0.149f, 0.165f, 0.188f, 1f);

        [Tooltip("通知の背景。パネルの上に置くため、混ぜた後の色を不透明で指定する。")]
        [SerializeField] Color _settingsNoticeBackground = new Color(0.212f, 0.180f, 0.176f, 1f);

        [Tooltip("スライダーのトラック。")]
        [SerializeField] Color _settingsTrack = new Color(0.165f, 0.188f, 0.231f, 1f);

        [Tooltip("スライダーの値までのフィル。")]
        [SerializeField] Color _settingsSliderFill = new Color(0.14f, 0.50f, 0.95f, 1f);

        [Tooltip("同意の画面の後ろ（ページを暗くする）。")]
        [SerializeField] Color _settingsBackdrop = new Color(0f, 0f, 0f, 0.7f);

        [Tooltip("同意の画面と、選択肢の一覧の背景。")]
        [SerializeField] Color _settingsDialog = new Color(0.137f, 0.157f, 0.2f, 1f);

        [Tooltip("選択肢の一覧の枠。")]
        [SerializeField] Color _settingsListBorder = new Color(0.227f, 0.263f, 0.329f, 1f);

        [Tooltip("デスクトップの設定ウィンドウで、縦横比が違うときに設定画面の周りに見える色。")]
        [SerializeField] Color _settingsWindowMargin = new Color(0.08f, 0.09f, 0.11f, 1f);

        [Header("設定画面の操作の対象")]
        [SerializeField] Color _settingsButton = new Color(0.180f, 0.208f, 0.259f, 1f);
        [SerializeField] Color _settingsButtonHover = new Color(0.227f, 0.263f, 0.329f, 1f);
        [SerializeField] Color _settingsButtonPressed = new Color(0.14f, 0.50f, 0.95f, 1f);

        [Tooltip("「アプリを終了」のボタン。ほかのボタンと見分けられる色にする。")]
        [SerializeField] Color _settingsQuit = new Color(0.290f, 0.200f, 0.200f, 1f);

        [SerializeField] Color _settingsQuitHover = new Color(0.38f, 0.25f, 0.25f, 1f);
        [SerializeField] Color _settingsQuitPressed = new Color(0.86f, 0.27f, 0.27f, 1f);
        [SerializeField] Color _settingsThumb = new Color(0.96f, 0.97f, 0.98f, 1f);

        [Tooltip("ドラッグしている・指しているスライダーのハンドル。")]
        [SerializeField] Color _settingsThumbActive = new Color(0.45f, 0.78f, 1.00f, 1f);

        [Tooltip("スライダーのハンドルの枠。パネルと同じ明るさのハンドルが見えるようにする。ドラッグしている・指しているときはハンドルと同じ色にする。")]
        [SerializeField] Color _settingsThumbRing = new Color(0.96f, 0.97f, 0.98f, 1f);

        [Tooltip("選んでいないカテゴリのタブ。タブの列のパネルが見えるよう、既定は透明。指している・押しているときはボタンと同じ色にする。")]
        [SerializeField] Color _settingsTab = new Color(0f, 0f, 0f, 0f);

        [SerializeField] Color _settingsTabSelected = new Color(0.14f, 0.50f, 0.95f, 1f);

        [Tooltip("オンのスイッチの背景。")]
        [SerializeField] Color _settingsSwitchOn = new Color(0.14f, 0.50f, 0.95f, 1f);

        [Tooltip("オフのスイッチの背景。")]
        [SerializeField] Color _settingsSwitchOff = new Color(0.27f, 0.31f, 0.39f, 1f);

        [Tooltip("選択肢の一覧の、選んでいない選択肢。一覧の背景が見えるよう、既定は透明。指している・押しているときはボタンと同じ色にする。")]
        [SerializeField] Color _settingsOption = new Color(0f, 0f, 0f, 0f);

        [Tooltip("選択肢の一覧の、いまの値の選択肢。")]
        [SerializeField] Color _settingsOptionSelected = new Color(0.14f, 0.50f, 0.95f, 1f);

        /// <summary>このアセットがどのテーマか。</summary>
        public ColorTheme Id => _id;

        public Color Cell => _cell;
        public Color CellDisabled => _cellDisabled;
        public Color CellSelected => _cellSelected;
        public Color CellText => _cellText;
        public Color CellTextLit => _cellTextLit;
        public Color CellTextDisabled => _cellTextDisabled;
        public Color CellTextDisabledSelected => _cellTextDisabledSelected;
        public Color CellHint => _cellHint;
        public Color CellHintSelected => _cellHintSelected;
        public float CellFontSize => _cellFontSize;
        public float CellFunctionFontSize => _cellFunctionFontSize;
        public float PanelDimAlpha => _panelDimAlpha;

        public Color CaptionRole => _captionRole;
        public Color CaptionRoleFunction => _captionRoleFunction;

        public Color Chip => _chip;
        public Color ChipSelected => _chipSelected;
        public Color ChipActive => _chipActive;
        public Color ChipText => _chipText;
        public Color ChipTextInactive => _chipTextInactive;

        public Color Composition => _composition;
        public Color Length => _length;
        public Color LengthAtLimit => _lengthAtLimit;
        public Color LengthOverLimit => _lengthOverLimit;

        public Color Candidate => _candidate;
        public Color CandidateSelected => _candidateSelected;

        public Color SettingsWindowMargin => _settingsWindowMargin;
        public Color SettingsButton => _settingsButton;
        public Color SettingsButtonHover => _settingsButtonHover;
        public Color SettingsButtonPressed => _settingsButtonPressed;
        public Color SettingsQuit => _settingsQuit;
        public Color SettingsQuitHover => _settingsQuitHover;
        public Color SettingsQuitPressed => _settingsQuitPressed;
        public Color SettingsThumb => _settingsThumb;
        public Color SettingsThumbActive => _settingsThumbActive;
        public Color SettingsThumbRing => _settingsThumbRing;
        public Color SettingsTab => _settingsTab;
        public Color SettingsTabSelected => _settingsTabSelected;
        public Color SettingsSwitchOn => _settingsSwitchOn;
        public Color SettingsSwitchOff => _settingsSwitchOff;
        public Color SettingsOption => _settingsOption;
        public Color SettingsOptionSelected => _settingsOptionSelected;

        /// <summary>状態によらない色の役割の色。</summary>
        public Color ColorOf(ThemeColorRole role)
        {
            switch (role)
            {
                case ThemeColorRole.KeyboardBoard: return _keyboardBoard;
                case ThemeColorRole.KeyboardRim: return _keyboardRim;
                case ThemeColorRole.KeyboardField: return _keyboardField;
                case ThemeColorRole.KeyboardText: return _keyboardText;
                case ThemeColorRole.KeyboardTextSub: return _keyboardTextSub;
                case ThemeColorRole.KeyboardTextMuted: return _keyboardTextMuted;
                case ThemeColorRole.KeyboardCaret: return _keyboardCaret;
                case ThemeColorRole.KeyboardNoticeBackground: return _keyboardNoticeBackground;
                case ThemeColorRole.KeyboardNoticeText: return _keyboardNoticeText;
                case ThemeColorRole.KeyboardLitBar: return _keyboardLitBar;
                case ThemeColorRole.KeyboardCard: return _keyboardCard;
                case ThemeColorRole.KeyboardCardText: return _keyboardCardText;
                case ThemeColorRole.KeyboardSeparator: return _keyboardSeparator;
                case ThemeColorRole.KeyboardBannerBackground: return _keyboardBannerBackground;
                case ThemeColorRole.KeyboardBannerText: return _keyboardBannerText;
                case ThemeColorRole.SettingsBackground: return _settingsBackground;
                case ThemeColorRole.SettingsText: return _settingsText;
                case ThemeColorRole.SettingsNote: return _settingsNote;
                case ThemeColorRole.SettingsNotice: return _settingsNotice;
                case ThemeColorRole.SettingsTrack: return _settingsTrack;
                case ThemeColorRole.SettingsSliderFill: return _settingsSliderFill;
                case ThemeColorRole.SettingsBackdrop: return _settingsBackdrop;
                case ThemeColorRole.SettingsDialog: return _settingsDialog;
                case ThemeColorRole.SettingsListBorder: return _settingsListBorder;
                case ThemeColorRole.SettingsPanel: return _settingsPanel;
                case ThemeColorRole.SettingsSeparator: return _settingsSeparator;
                case ThemeColorRole.SettingsNoticeBackground: return _settingsNoticeBackground;
                default: throw new ArgumentOutOfRangeException(nameof(role), role, "未知の色の役割です。");
            }
        }

        /// <summary>値が使えない範囲にあれば例外にする。</summary>
        public void Validate()
        {
            if (!Enum.IsDefined(typeof(ColorTheme), _id)) throw new InvalidOperationException(name + " のテーマが未知の値です: " + _id);
            if (!(_cellFontSize > 0f)) throw new InvalidOperationException(name + " の文字の大きさ（1文字）が正ではありません: " + _cellFontSize);
            if (!(_cellFunctionFontSize > 0f)) throw new InvalidOperationException(name + " の文字の大きさ（2文字以上）が正ではありません: " + _cellFunctionFontSize);
            if (!(_panelDimAlpha >= 0f && _panelDimAlpha <= 1f)) throw new InvalidOperationException(name + " の暗くしたパネルの不透明度が 0〜1 ではありません: " + _panelDimAlpha);
        }
    }
}
