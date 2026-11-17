using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRime.Core.Grip;

namespace VRime.App.View
{
    /// <summary>グリップのパネルの1マス。背景・点灯のバー・文字・機能ページのキーのヒント（右下）を持つ。</summary>
    public sealed class GripPanelCell : MonoBehaviour
    {
        [SerializeField] Image _background;
        [SerializeField] GameObject _litBar;
        [SerializeField] TextMeshProUGUI _label;

        [Tooltip("機能ページのキーの頭文字。主の文字を妨げないよう、小さく薄く示す。")]
        [SerializeField] TextMeshProUGUI _hint;

        KeyboardTheme _theme;

        public void Validate()
        {
            SceneChecks.Require(_background, this, nameof(_background));
            SceneChecks.Require(_litBar, this, nameof(_litBar));
            SceneChecks.Require(_label, this, nameof(_label));
            SceneChecks.Require(_hint, this, nameof(_hint));
        }

        public void Initialize(KeyboardTheme theme)
        {
            Validate();
            _theme = theme != null ? theme : throw new ArgumentNullException(nameof(theme));
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
            SetHint(hint);
        }

        public void Set(GripCellView cell, bool selected, string hint)
        {
            bool placeholder = cell.Label == GripCell.Empty.Label;
            // 点灯中の文字は点灯色にする。選択中は背景が変わるため文字の色を保つ。
            Color textColor;
            if (cell.Disabled || placeholder) textColor = selected ? _theme.CellTextDisabledSelected : _theme.CellTextDisabled;
            else if (cell.Lit && !selected) textColor = _theme.CellTextLit;
            else textColor = _theme.CellText;

            _background.color = selected ? _theme.CellSelected : (cell.Disabled ? _theme.CellDisabled : _theme.Cell);
            _litBar.SetActive(cell.Lit);
            _label.color = textColor;
            _label.fontSize = cell.Label.Length > 1 ? _theme.CellFunctionFontSize : _theme.CellFontSize;
            if (_label.text != cell.Label) _label.text = cell.Label;
            SetHint(hint);
        }

        void SetHint(string hint)
        {
            if (hint == null) throw new ArgumentNullException(nameof(hint));
            if (_hint.text != hint) _hint.text = hint;
        }
    }
}
