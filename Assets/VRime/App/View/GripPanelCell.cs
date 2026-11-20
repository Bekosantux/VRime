using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRime.Core.Grip;

namespace VRime.App.View
{
    /// <summary>
    /// グリップのパネルのセル。背景・点灯のバー・文字と、文字に重ねる2種類のヒントを持つ。
    /// ヒントは、上下左右の辺に置く方向の文字と、右下に置く機能ページのキーの頭文字。
    /// </summary>
    public sealed class GripPanelCell : MonoBehaviour
    {
        [SerializeField] Image _background;
        [SerializeField] GameObject _litBar;
        [SerializeField] TextMeshProUGUI _label;

        [Tooltip("機能ページのキーの頭文字。メインの文字を妨げないよう、小さく薄く示す。")]
        [SerializeField] TextMeshProUGUI _hint;

        [Tooltip("方向のヒント。左・上・右・下の順で、それぞれセルの同じ側の辺に置く。")]
        [SerializeField] TextMeshProUGUI[] _directionHints = new TextMeshProUGUI[GripCellView.DirectionHintCount];

        ActiveTheme _theme;

        public void Validate()
        {
            SceneChecks.Require(_background, this, nameof(_background));
            SceneChecks.Require(_litBar, this, nameof(_litBar));
            SceneChecks.Require(_label, this, nameof(_label));
            SceneChecks.Require(_hint, this, nameof(_hint));
            SceneChecks.RequireAll(_directionHints, GripCellView.DirectionHintCount, this, nameof(_directionHints));
        }

        public void Initialize(ActiveTheme theme)
        {
            Validate();
            _theme = theme ?? throw new ArgumentNullException(nameof(theme));
        }

        public void SetActive(bool active)
        {
            if (gameObject.activeSelf != active) gameObject.SetActive(active);
        }

        /// <summary>セルを置かず、機能ページのキーのヒントだけを示す（母音側の通常ページの四隅）。</summary>
        public void SetHintOnly(string hint)
        {
            _background.color = Color.clear;
            _litBar.SetActive(false);
            if (_label.text.Length != 0) _label.text = string.Empty;
            for (int i = 0; i < _directionHints.Length; i++) SetText(_directionHints[i], string.Empty);
            SetHint(hint, _theme.Current.CellHint);
        }

        public void Set(GripCellView cell, bool selected, string hint)
        {
            bool placeholder = cell.Label == GripCell.Empty.Label;
            // 点灯中の文字は点灯色にする。選択中は背景が変わるため文字の色を保つ。
            Color textColor;
            if (cell.Disabled || placeholder) textColor = selected ? _theme.Current.CellTextDisabledSelected : _theme.Current.CellTextDisabled;
            else if (cell.Lit && !selected) textColor = _theme.Current.CellTextLit;
            else textColor = _theme.Current.CellText;

            _background.color = selected ? _theme.Current.CellSelected : (cell.Disabled ? _theme.Current.CellDisabled : _theme.Current.Cell);
            _litBar.SetActive(cell.Lit);
            _label.color = textColor;
            _label.fontSize = cell.Label.Length > 1 ? _theme.Current.CellFunctionFontSize : _theme.Current.CellFontSize;
            if (_label.text != cell.Label) _label.text = cell.Label;

            Color hintColor = selected ? _theme.Current.CellHintSelected : _theme.Current.CellHint;
            for (int i = 0; i < _directionHints.Length; i++)
            {
                _directionHints[i].color = hintColor;
                SetText(_directionHints[i], cell.DirectionHint((StickDirection)(i + 1)));
            }
            SetHint(hint, hintColor);
        }

        void SetHint(string hint, Color color)
        {
            if (hint == null) throw new ArgumentNullException(nameof(hint));
            _hint.color = color;
            SetText(_hint, hint);
        }

        static void SetText(TextMeshProUGUI text, string value)
        {
            if (text.text != value) text.text = value;
        }
    }
}
