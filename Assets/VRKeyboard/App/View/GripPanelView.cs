using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRKeyboard.Core.Grip;
using VRKeyboard.Core.Input;

namespace VRKeyboard.App.View
{
    /// <summary>
    /// グリップ入力のパネル（子音側 3×3・母音側 5方向または 3×3）とページ・全角/半角・大文字/小文字の状態。
    /// <see cref="GripViewState"/> を描くだけで、入力の判断は持たない。
    /// 子音側のパネルは、子音を担当する手の側に置く。
    /// </summary>
    public sealed class GripPanelView
    {
        const int GridSize = 3;

        // 母音側の通常ページで、3×3 の各セルに置く5方向（StickDirection の値。中央=0・左=1・上=2・右=3・下=4）。
        // 方向を置かない四隅は -1。
        static readonly int[] CellToDirection = { -1, 2, -1, 1, 0, 3, -1, 4, -1 };

        sealed class PanelCell
        {
            readonly RectTransform _rect;
            readonly Image _background;
            readonly Image _litBar;
            readonly TextMeshProUGUI _label;
            readonly TextMeshProUGUI _hint;

            public PanelCell(Transform parent, TMP_FontAsset labelFont, TMP_FontAsset hintFont, float x, float y)
            {
                _rect = UiBuilder.Rect(parent, "Cell", x, y, KeyboardTheme.CellWidth, KeyboardTheme.CellHeight);
                _background = UiBuilder.AddImage(_rect.gameObject, KeyboardTheme.Cell);
                _litBar = UiBuilder.Image(_rect, "Lit", 14f, KeyboardTheme.CellHeight - 12f, KeyboardTheme.CellWidth - 28f, 6f, KeyboardTheme.Lit);
                RectTransform labelRect = UiBuilder.Rect(_rect, "Label", 0f, 0f, KeyboardTheme.CellWidth, KeyboardTheme.CellHeight);
                UiBuilder.Stretch(labelRect);
                _label = UiBuilder.AddText(labelRect.gameObject, labelFont, KeyboardTheme.CellFontSize, KeyboardTheme.Text, TextAlignmentOptions.Center);

                RectTransform hintRect = UiBuilder.Rect(_rect, "Hint", 0f, 0f, KeyboardTheme.CellWidth, KeyboardTheme.CellHeight);
                UiBuilder.Stretch(hintRect);
                hintRect.offsetMin = new Vector2(KeyboardTheme.CellHintPaddingX, KeyboardTheme.CellHintPaddingY);
                hintRect.offsetMax = new Vector2(-KeyboardTheme.CellHintPaddingX, -KeyboardTheme.CellHintPaddingY);
                _hint = UiBuilder.AddText(hintRect.gameObject, hintFont, KeyboardTheme.CellHintFontSize, KeyboardTheme.CellHint, TextAlignmentOptions.BottomRight);
            }

            public void SetActive(bool active)
            {
                if (_rect.gameObject.activeSelf != active) _rect.gameObject.SetActive(active);
            }

            /// <summary>セルを置かず、機能ページのキーのヒントだけを示す（母音側の通常ページの四隅）。</summary>
            public void SetHintOnly(string hint)
            {
                _background.color = KeyboardTheme.Transparent;
                _litBar.gameObject.SetActive(false);
                if (_label.text.Length != 0) _label.text = string.Empty;
                SetHint(hint);
            }

            public void Set(GripCellView cell, bool selected, string hint)
            {
                bool placeholder = cell.Label == GripCell.Empty.Label;
                // 点灯中の文字は点灯色にする。選択中は背景が青になるため文字を白に保つ。
                Color textColor;
                if (cell.Disabled || placeholder) textColor = selected ? KeyboardTheme.TextDim : KeyboardTheme.TextDisabled;
                else if (cell.Lit && !selected) textColor = KeyboardTheme.Lit;
                else textColor = KeyboardTheme.Text;

                _background.color = selected ? KeyboardTheme.CellSelected : (cell.Disabled ? KeyboardTheme.CellDisabled : KeyboardTheme.Cell);
                _litBar.gameObject.SetActive(cell.Lit);
                _label.color = textColor;
                _label.fontSize = cell.Label.Length > 1 ? KeyboardTheme.CellFunctionFontSize : KeyboardTheme.CellFontSize;
                if (_label.text != cell.Label) _label.text = cell.Label;
                SetHint(hint);
            }

            void SetHint(string hint)
            {
                if (hint == null) throw new ArgumentNullException(nameof(hint));
                if (_hint.text != hint) _hint.text = hint;
            }
        }

        enum ChipState
        {
            Selected,
            Active,
            Inactive,
        }

        sealed class Chip
        {
            readonly Image _background;
            readonly TextMeshProUGUI _label;

            public Chip(Transform parent, TMP_FontAsset font, float x, float width)
            {
                RectTransform rect = UiBuilder.Rect(parent, "Chip", x, KeyboardTheme.PageRowTop, width, KeyboardTheme.PageRowHeight);
                _background = UiBuilder.AddImage(rect.gameObject, KeyboardTheme.Chip);
                RectTransform labelRect = UiBuilder.Rect(rect, "Label", 0f, 0f, width, KeyboardTheme.PageRowHeight);
                UiBuilder.Stretch(labelRect);
                _label = UiBuilder.AddText(labelRect.gameObject, font, KeyboardTheme.StatusFontSize, KeyboardTheme.Text, TextAlignmentOptions.Center);
            }

            public void Set(string label, ChipState state)
            {
                switch (state)
                {
                    case ChipState.Selected:
                        _background.color = KeyboardTheme.ChipSelected;
                        _label.color = KeyboardTheme.Text;
                        break;
                    case ChipState.Active:
                        _background.color = KeyboardTheme.ChipActive;
                        _label.color = KeyboardTheme.Text;
                        break;
                    case ChipState.Inactive:
                        _background.color = KeyboardTheme.Chip;
                        _label.color = KeyboardTheme.TextDisabled;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(state), state, null);
                }
                if (_label.text != label) _label.text = label;
            }
        }

        readonly Hand _consonantHand;
        readonly CanvasGroup _group;
        readonly PanelCell[] _consonantCells = new PanelCell[GridSize * GridSize];
        readonly PanelCell[] _vowelCells = new PanelCell[GridSize * GridSize];
        readonly TextMeshProUGUI _consonantCaption;
        readonly TextMeshProUGUI _vowelCaption;
        readonly Chip[] _pageChips = new Chip[3];
        readonly Chip _widthChip;
        readonly Chip _caseChip;

        public GripPanelView(Transform parent, KeyboardFonts fonts, Hand consonantHand)
        {
            if (fonts == null) throw new ArgumentNullException(nameof(fonts));
            _consonantHand = consonantHand;

            RectTransform root = UiBuilder.Rect(parent, "GripPanels", 0f, 0f, KeyboardTheme.Width, KeyboardTheme.Height);
            _group = root.gameObject.AddComponent<CanvasGroup>();

            float leftX = KeyboardTheme.Margin;
            float rightX = KeyboardTheme.Width - KeyboardTheme.Margin - KeyboardTheme.PanelWidth;
            float consonantX = consonantHand == Hand.Left ? leftX : rightX;
            float vowelX = consonantHand == Hand.Left ? rightX : leftX;

            BuildCells(root, fonts, consonantX, _consonantCells);
            BuildCells(root, fonts, vowelX, _vowelCells);

            _consonantCaption = BuildCaption(root, fonts.Regular, consonantX, consonantHand == Hand.Left);
            _vowelCaption = BuildCaption(root, fonts.Regular, vowelX, consonantHand != Hand.Left);

            // ページ・全角/半角・大文字/小文字を中央に並べる。
            const float pageChipWidth = 92f;
            const float optionChipWidth = 108f;
            const float gap = 8f;
            float total = pageChipWidth * 3 + optionChipWidth * 2 + gap * 4;
            float x = (KeyboardTheme.Width - total) / 2f;
            for (int i = 0; i < _pageChips.Length; i++)
            {
                _pageChips[i] = new Chip(root, fonts.Regular, x, pageChipWidth);
                x += pageChipWidth + gap;
            }
            _widthChip = new Chip(root, fonts.Regular, x, optionChipWidth);
            x += optionChipWidth + gap;
            _caseChip = new Chip(root, fonts.Regular, x, optionChipWidth);
        }

        /// <param name="dimmed">変換中など、パネルが操作を受けない間は暗くする。</param>
        public void Refresh(GripInputMethod input, bool dimmed)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            GripViewState view = input.View;
            _group.alpha = dimmed ? KeyboardTheme.PanelDimAlpha : 1f;

            for (int i = 0; i < _consonantCells.Length; i++)
            {
                _consonantCells[i].Set(view.ConsonantCells[i], i == view.ConsonantSelected, view.ConsonantHints[i]);
            }

            if (view.VowelFunctionPage)
            {
                for (int i = 0; i < _vowelCells.Length; i++)
                {
                    _vowelCells[i].SetActive(true);
                    _vowelCells[i].Set(view.VowelCells[i], i == view.VowelSelected, view.VowelHints[i]);
                }
            }
            else
            {
                for (int i = 0; i < _vowelCells.Length; i++)
                {
                    PanelCell cell = _vowelCells[i];
                    string hint = view.VowelHints[i];
                    int direction = CellToDirection[i];
                    if (direction >= 0)
                    {
                        cell.SetActive(true);
                        cell.Set(view.VowelCells[direction], direction == view.VowelSelected, hint);
                    }
                    else
                    {
                        // 5方向のマスがない位置（四隅）には、ヒントがあればヒントだけを置く。
                        cell.SetActive(hint.Length != 0);
                        if (hint.Length != 0) cell.SetHintOnly(hint);
                    }
                }
            }

            string consonantRole = view.ConsonantFunctionPage ? "機能" : "子音";
            string vowelRole = view.VowelFunctionPage ? "機能" : "母音";
            SetCaption(_consonantCaption, _consonantHand, consonantRole);
            SetCaption(_vowelCaption, _consonantHand.Opposite(), vowelRole);

            RefreshChips(input, view.Page);
        }

        void RefreshChips(GripInputMethod input, GripPage page)
        {
            _pageChips[0].Set("かな", page == GripPage.Kana ? ChipState.Selected : ChipState.Inactive);
            _pageChips[1].Set("英字", page == GripPage.Alpha ? ChipState.Selected : ChipState.Inactive);
            _pageChips[2].Set("数字", page == GripPage.Number ? ChipState.Selected : ChipState.Inactive);

            // 全角/半角は英字・数字ページ、大文字/小文字は英字ページでのみ作用する（SPEC 5.2）。
            bool widthApplies = page != GripPage.Kana;
            _widthChip.Set(input.IsFullWidth ? "全角" : "半角", widthApplies ? ChipState.Active : ChipState.Inactive);
            bool caseApplies = page == GripPage.Alpha;
            _caseChip.Set(input.IsUpperCase ? "ABC" : "abc", caseApplies ? ChipState.Active : ChipState.Inactive);
        }

        static void BuildCells(Transform parent, KeyboardFonts fonts, float originX, PanelCell[] cells)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                float x = originX + (i % GridSize) * (KeyboardTheme.CellWidth + KeyboardTheme.CellGap);
                float y = KeyboardTheme.PanelTop + (i / GridSize) * (KeyboardTheme.CellHeight + KeyboardTheme.CellGap);
                cells[i] = new PanelCell(parent, fonts.Bold, fonts.Regular, x, y);
            }
        }

        static TextMeshProUGUI BuildCaption(Transform parent, TMP_FontAsset font, float panelX, bool alignLeft)
        {
            const float width = 170f;
            float x = alignLeft ? panelX : panelX + KeyboardTheme.PanelWidth - width;
            return UiBuilder.Text(parent, "Caption", x, KeyboardTheme.PageRowTop, width, KeyboardTheme.PageRowHeight,
                font, KeyboardTheme.StatusFontSize, KeyboardTheme.TextDim,
                alignLeft ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight);
        }

        static void SetCaption(TextMeshProUGUI caption, Hand hand, string role)
        {
            string text = (hand == Hand.Left ? "左手 " : "右手 ") + role;
            if (caption.text != text) caption.text = text;
        }
    }
}
