using System;
using TMPro;
using UnityEngine;
using VRKeyboard.Core.Grip;
using VRKeyboard.Core.Input;

namespace VRKeyboard.App.View
{
    /// <summary>
    /// グリップ入力のパネル（子音側 3×3・母音側 5方向または 3×3）とページ・全角/半角・大文字/小文字の状態。
    /// <see cref="GripViewState"/> を描くだけで、入力の判断は持たない。
    /// 左右のパネルはシーンに置き、子音を担当する手の側を子音側、反対側を母音側にする。
    /// </summary>
    public sealed class GripPanelView : MonoBehaviour
    {
        const int GridSize = 3;
        const int CellCount = GridSize * GridSize;
        const int PageCount = 3;

        // 母音側の通常ページで、3×3 の各セルに置く5方向（StickDirection の値。中央=0・左=1・上=2・右=3・下=4）。
        // 方向を置かない四隅は -1。
        static readonly int[] CellToDirection = { -1, 2, -1, 1, 0, 3, -1, 4, -1 };

        [Tooltip("変換中などにパネル全体を暗くするためのもの。")]
        [SerializeField] CanvasGroup _group;

        [Tooltip("左のパネルの 3×3 のセル。左上から行ごとの順。")]
        [SerializeField] GripPanelCell[] _leftCells = new GripPanelCell[CellCount];

        [Tooltip("右のパネルの 3×3 のセル。左上から行ごとの順。")]
        [SerializeField] GripPanelCell[] _rightCells = new GripPanelCell[CellCount];

        [Tooltip("左のパネルの役割（「左手 子音」など）。")]
        [SerializeField] TextMeshProUGUI _leftCaption;

        [Tooltip("右のパネルの役割（「右手 母音」など）。")]
        [SerializeField] TextMeshProUGUI _rightCaption;

        [Tooltip("かな・英字・数字の順。")]
        [SerializeField] GripPanelChip[] _pageChips = new GripPanelChip[PageCount];

        [SerializeField] GripPanelChip _widthChip;
        [SerializeField] GripPanelChip _caseChip;

        KeyboardTheme _theme;
        Hand _consonantHand;
        GripPanelCell[] _consonantCells;
        GripPanelCell[] _vowelCells;
        TextMeshProUGUI _consonantCaption;
        TextMeshProUGUI _vowelCaption;

        public void Validate()
        {
            SceneChecks.Require(_group, this, nameof(_group));
            SceneChecks.RequireAll(_leftCells, CellCount, this, nameof(_leftCells));
            SceneChecks.RequireAll(_rightCells, CellCount, this, nameof(_rightCells));
            SceneChecks.Require(_leftCaption, this, nameof(_leftCaption));
            SceneChecks.Require(_rightCaption, this, nameof(_rightCaption));
            SceneChecks.RequireAll(_pageChips, PageCount, this, nameof(_pageChips));
            SceneChecks.Require(_widthChip, this, nameof(_widthChip));
            SceneChecks.Require(_caseChip, this, nameof(_caseChip));
            foreach (GripPanelCell cell in _leftCells) cell.Validate();
            foreach (GripPanelCell cell in _rightCells) cell.Validate();
            foreach (GripPanelChip chip in _pageChips) chip.Validate();
            _widthChip.Validate();
            _caseChip.Validate();
        }

        public void Initialize(Hand consonantHand, KeyboardTheme theme)
        {
            Validate();
            _theme = theme != null ? theme : throw new ArgumentNullException(nameof(theme));
            _consonantHand = consonantHand;
            bool consonantLeft = consonantHand == Hand.Left;
            _consonantCells = consonantLeft ? _leftCells : _rightCells;
            _vowelCells = consonantLeft ? _rightCells : _leftCells;
            _consonantCaption = consonantLeft ? _leftCaption : _rightCaption;
            _vowelCaption = consonantLeft ? _rightCaption : _leftCaption;

            foreach (GripPanelCell cell in _leftCells) cell.Initialize(theme);
            foreach (GripPanelCell cell in _rightCells) cell.Initialize(theme);
            foreach (GripPanelChip chip in _pageChips) chip.Initialize(theme);
            _widthChip.Initialize(theme);
            _caseChip.Initialize(theme);
        }

        /// <param name="dimmed">変換中など、パネルが操作を受けない間は暗くする。</param>
        public void Refresh(GripInputMethod input, bool dimmed)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            GripViewState view = input.View;
            _group.alpha = dimmed ? _theme.PanelDimAlpha : 1f;

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
                    GripPanelCell cell = _vowelCells[i];
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
            _pageChips[0].Set("かな", page == GripPage.Kana ? GripChipState.Selected : GripChipState.Inactive);
            _pageChips[1].Set("英字", page == GripPage.Alpha ? GripChipState.Selected : GripChipState.Inactive);
            _pageChips[2].Set("数字", page == GripPage.Number ? GripChipState.Selected : GripChipState.Inactive);

            // 全角/半角は英字・数字ページ、大文字/小文字は英字ページでのみ作用する（SPEC 5.2）。
            bool widthApplies = page != GripPage.Kana;
            _widthChip.Set(input.IsFullWidth ? "全角" : "半角", widthApplies ? GripChipState.Active : GripChipState.Inactive);
            bool caseApplies = page == GripPage.Alpha;
            _caseChip.Set(input.IsUpperCase ? "ABC" : "abc", caseApplies ? GripChipState.Active : GripChipState.Inactive);
        }

        static void SetCaption(TextMeshProUGUI caption, Hand hand, string role)
        {
            string text = (hand == Hand.Left ? "左手 " : "右手 ") + role;
            if (caption.text != text) caption.text = text;
        }
    }
}
