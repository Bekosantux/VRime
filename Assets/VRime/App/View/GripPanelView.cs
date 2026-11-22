using System;
using UnityEngine;
using VRime.Core.Grip;
using VRime.Core.Input;

namespace VRime.App.View
{
    /// <summary>
    /// グリップのパネル（子音側 3×3・母音側 5方向または 3×3）と入力モード・全角/半角・大文字/小文字の状態。
    /// <see cref="GripViewState"/> を描画するだけで、入力の判断は持たない。
    /// 左右のパネルはシーンに置き、子音を担当する手の側を子音側、反対側を母音側にする。
    /// </summary>
    public sealed class GripPanelView : MonoBehaviour
    {
        const int GridSize = 3;
        const int CellCount = GridSize * GridSize;
        const int ModeCount = 3;

        // 母音側の基本レイヤーで、3×3 の各セルに置く5方向（StickDirection の値。中央=0・左=1・上=2・右=3・下=4）。
        // 方向を置かない四隅は -1。
        static readonly int[] CellToDirection = { -1, 2, -1, 1, 0, 3, -1, 4, -1 };

        [Tooltip("変換中などにパネル全体を暗くするためのもの。")]
        [SerializeField] CanvasGroup _group;

        [Tooltip("左のパネルの 3×3 のセル。左上から行ごとの順。")]
        [SerializeField] GripPanelCell[] _leftCells = new GripPanelCell[CellCount];

        [Tooltip("右のパネルの 3×3 のセル。左上から行ごとの順。")]
        [SerializeField] GripPanelCell[] _rightCells = new GripPanelCell[CellCount];

        [Tooltip("左のパネルの見出し（役割）。")]
        [SerializeField] GripPanelCaption _leftCaption;

        [Tooltip("右のパネルの見出し（役割）。")]
        [SerializeField] GripPanelCaption _rightCaption;

        [Tooltip("かな・英字・数字の順。")]
        [SerializeField] GripPanelChip[] _modeChips = new GripPanelChip[ModeCount];

        [SerializeField] GripPanelChip _widthChip;
        [SerializeField] GripPanelChip _caseChip;

        ActiveTheme _theme;
        GripPanelCell[] _consonantCells;
        GripPanelCell[] _vowelCells;
        GripPanelCaption _consonantCaption;
        GripPanelCaption _vowelCaption;

        /// <summary>
        /// 母音側の基本レイヤーで、3×3 のその位置（行優先の 0..8）に5方向のセルを置くか。四隅には置かず、機能レイヤーのヒントだけを示す。
        /// </summary>
        public static bool HasVowelDirection(int cell)
        {
            if (cell < 0 || cell >= CellCount) throw new ArgumentOutOfRangeException(nameof(cell), cell, null);
            return CellToDirection[cell] >= 0;
        }

        public void Validate()
        {
            SceneChecks.Require(_group, this, nameof(_group));
            SceneChecks.RequireAll(_leftCells, CellCount, this, nameof(_leftCells));
            SceneChecks.RequireAll(_rightCells, CellCount, this, nameof(_rightCells));
            SceneChecks.Require(_leftCaption, this, nameof(_leftCaption));
            SceneChecks.Require(_rightCaption, this, nameof(_rightCaption));
            SceneChecks.RequireAll(_modeChips, ModeCount, this, nameof(_modeChips));
            SceneChecks.Require(_widthChip, this, nameof(_widthChip));
            SceneChecks.Require(_caseChip, this, nameof(_caseChip));
            foreach (GripPanelCell cell in _leftCells) cell.Validate();
            foreach (GripPanelCell cell in _rightCells) cell.Validate();
            _leftCaption.Validate();
            _rightCaption.Validate();
            foreach (GripPanelChip chip in _modeChips) chip.Validate();
            _widthChip.Validate();
            _caseChip.Validate();
        }

        public void Initialize(Hand consonantHand, ActiveTheme theme)
        {
            Validate();
            _theme = theme ?? throw new ArgumentNullException(nameof(theme));
            bool consonantLeft = consonantHand == Hand.Left;
            _consonantCells = consonantLeft ? _leftCells : _rightCells;
            _vowelCells = consonantLeft ? _rightCells : _leftCells;
            _consonantCaption = consonantLeft ? _leftCaption : _rightCaption;
            _vowelCaption = consonantLeft ? _rightCaption : _leftCaption;

            foreach (GripPanelCell cell in _leftCells) cell.Initialize(theme);
            foreach (GripPanelCell cell in _rightCells) cell.Initialize(theme);
            _leftCaption.Initialize(theme);
            _rightCaption.Initialize(theme);
            foreach (GripPanelChip chip in _modeChips) chip.Initialize(theme);
            _widthChip.Initialize(theme);
            _caseChip.Initialize(theme);
        }

        /// <param name="dimmed">変換中など、パネルが操作を受けない間は暗くする。</param>
        public void Refresh(GripInputMethod input, bool dimmed)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            GripViewState view = input.View;
            _group.alpha = dimmed ? _theme.Current.PanelDimAlpha : 1f;

            for (int i = 0; i < _consonantCells.Length; i++)
            {
                _consonantCells[i].Set(view.ConsonantCells[i], i == view.ConsonantSelected, view.ConsonantHints[i]);
            }

            if (view.VowelFunctionLayer)
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
                    if (HasVowelDirection(i))
                    {
                        int direction = CellToDirection[i];
                        cell.SetActive(true);
                        cell.Set(view.VowelCells[direction], direction == view.VowelSelected, hint);
                    }
                    else
                    {
                        // 5方向のセルがない位置（四隅）には、ヒントがあればヒントだけを置く。
                        cell.SetActive(hint.Length != 0);
                        if (hint.Length != 0) cell.SetHintOnly(hint);
                    }
                }
            }

            _consonantCaption.Set(view.ConsonantFunctionLayer ? "機能" : "子音", view.ConsonantFunctionLayer);
            _vowelCaption.Set(view.VowelFunctionLayer ? "機能" : "母音", view.VowelFunctionLayer);

            RefreshChips(input, view.InputMode);
        }

        void RefreshChips(GripInputMethod input, GripInputMode mode)
        {
            _modeChips[0].Set("かな", mode == GripInputMode.Kana ? GripChipState.Selected : GripChipState.Inactive);
            _modeChips[1].Set("英字", mode == GripInputMode.Alpha ? GripChipState.Selected : GripChipState.Inactive);
            _modeChips[2].Set("数字", mode == GripInputMode.Number ? GripChipState.Selected : GripChipState.Inactive);

            // 全角/半角は英字モード・数字モード、大文字/小文字は英字モードでのみ作用する（docs/spec/grip-input.md「母音側の機能レイヤー」）。
            bool widthApplies = mode != GripInputMode.Kana;
            _widthChip.Set(input.IsFullWidth ? "全角" : "半角", widthApplies ? GripChipState.Active : GripChipState.Inactive);
            bool caseApplies = mode == GripInputMode.Alpha;
            _caseChip.Set(input.IsUpperCase ? "ABC" : "abc", caseApplies ? GripChipState.Active : GripChipState.Inactive);
        }
    }
}
