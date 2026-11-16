using System;
using UnityEngine;

namespace VRKeyboard.App.View
{
    /// <summary>
    /// 状態によって切り替わる色と文字の大きさ。状態によらない見た目（配置・大きさ・固定の色）は、シーンの uGUI に置く。
    /// </summary>
    [CreateAssetMenu(menuName = "VR Keyboard/Theme", fileName = "KeyboardTheme")]
    public sealed class KeyboardTheme : ScriptableObject
    {
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

        [Tooltip("1文字のラベル。")]
        [SerializeField] float _cellFontSize = 54f;

        [Tooltip("2文字以上のラベル（機能ページのキー）。")]
        [SerializeField] float _cellFunctionFontSize = 38f;

        [Tooltip("変換中など、パネルが操作を受けない間の不透明度。")]
        [Range(0f, 1f)]
        [SerializeField] float _panelDimAlpha = 0.35f;

        [Header("パネルの見出し（手の名前と役割）")]
        [Tooltip("役割（子音・母音）。")]
        [SerializeField] Color _captionRole = new Color(0.96f, 0.97f, 0.98f, 1f);

        [Tooltip("機能ページを開いている間の役割。")]
        [SerializeField] Color _captionRoleFunction = new Color(0.451f, 0.780f, 1.00f, 1f);

        [Header("ページ行（ページ・全角/半角・大文字/小文字）")]
        [Tooltip("作用しない札。札を並べた地（シーン）を見せるため、既定は透明。")]
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
        [Tooltip("選んでいない文節の見出しと候補。文節のカード（シーン）を見せるため、既定は透明。")]
        [SerializeField] Color _candidate = new Color(0f, 0f, 0f, 0f);

        [SerializeField] Color _candidateSelected = new Color(0.14f, 0.50f, 0.95f, 1f);

        [Header("設定画面")]
        [SerializeField] Color _settingsButton = new Color(0.20f, 0.23f, 0.29f, 1f);
        [SerializeField] Color _settingsButtonHover = new Color(0.27f, 0.31f, 0.39f, 1f);
        [SerializeField] Color _settingsButtonPressed = new Color(0.14f, 0.50f, 0.95f, 1f);
        [SerializeField] Color _settingsThumb = new Color(0.96f, 0.97f, 0.98f, 1f);

        [Tooltip("つまんでいる・指しているスライダーのつまみ。")]
        [SerializeField] Color _settingsThumbActive = new Color(0.45f, 0.78f, 1.00f, 1f);

        [Tooltip("選んでいないカテゴリのタブ。指している・押しているときはボタンと同じ色にする。")]
        [SerializeField] Color _settingsTab = new Color(0.14f, 0.16f, 0.20f, 1f);

        [SerializeField] Color _settingsTabSelected = new Color(0.14f, 0.50f, 0.95f, 1f);

        [Tooltip("オンのスイッチの地。")]
        [SerializeField] Color _settingsSwitchOn = new Color(0.14f, 0.50f, 0.95f, 1f);

        [Tooltip("オフのスイッチの地。")]
        [SerializeField] Color _settingsSwitchOff = new Color(0.27f, 0.31f, 0.39f, 1f);

        public Color Cell => _cell;
        public Color CellDisabled => _cellDisabled;
        public Color CellSelected => _cellSelected;
        public Color CellText => _cellText;
        public Color CellTextLit => _cellTextLit;
        public Color CellTextDisabled => _cellTextDisabled;
        public Color CellTextDisabledSelected => _cellTextDisabledSelected;
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

        public Color SettingsButton => _settingsButton;
        public Color SettingsButtonHover => _settingsButtonHover;
        public Color SettingsButtonPressed => _settingsButtonPressed;
        public Color SettingsThumb => _settingsThumb;
        public Color SettingsThumbActive => _settingsThumbActive;
        public Color SettingsTab => _settingsTab;
        public Color SettingsTabSelected => _settingsTabSelected;
        public Color SettingsSwitchOn => _settingsSwitchOn;
        public Color SettingsSwitchOff => _settingsSwitchOff;

        /// <summary>値が使えない範囲にあれば例外にする。</summary>
        public void Validate()
        {
            if (!(_cellFontSize > 0f)) throw new InvalidOperationException(name + " の文字の大きさ（1文字）が正ではありません: " + _cellFontSize);
            if (!(_cellFunctionFontSize > 0f)) throw new InvalidOperationException(name + " の文字の大きさ（2文字以上）が正ではありません: " + _cellFunctionFontSize);
            if (!(_panelDimAlpha >= 0f && _panelDimAlpha <= 1f)) throw new InvalidOperationException(name + " の暗くしたパネルの不透明度が 0〜1 ではありません: " + _panelDimAlpha);
        }
    }
}
