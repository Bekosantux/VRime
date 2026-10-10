using System;
using System.Collections.Generic;

namespace VRKeyboard.Core.Grip
{
    /// <summary>パネルの1マスの表示。</summary>
    public readonly struct GripCellView
    {
        public GripCellView(string label, bool disabled, bool lit)
        {
            Label = label ?? throw new ArgumentNullException(nameof(label));
            Disabled = disabled;
            Lit = lit;
        }

        /// <summary>表示する文字。入力される文字（大文字・全角を反映）または動作名。</summary>
        public string Label { get; }

        /// <summary>灰色で表示し、トリガーを引いても何もしない。</summary>
        public bool Disabled { get; }

        /// <summary>オンの状態を示す点灯。</summary>
        public bool Lit { get; }
    }

    /// <summary>グリップ入力のビュー用の状態。</summary>
    public sealed class GripViewState
    {
        public GripViewState(
            GripPage page,
            bool consonantFunctionPage,
            bool vowelFunctionPage,
            IReadOnlyList<GripCellView> consonantCells,
            int consonantSelected,
            IReadOnlyList<GripCellView> vowelCells,
            int vowelSelected,
            IReadOnlyList<string> consonantHints,
            IReadOnlyList<string> vowelHints)
        {
            Page = page;
            ConsonantFunctionPage = consonantFunctionPage;
            VowelFunctionPage = vowelFunctionPage;
            ConsonantCells = consonantCells ?? throw new ArgumentNullException(nameof(consonantCells));
            ConsonantSelected = consonantSelected;
            VowelCells = vowelCells ?? throw new ArgumentNullException(nameof(vowelCells));
            VowelSelected = vowelSelected;
            ConsonantHints = ValidateHints(consonantHints, nameof(consonantHints));
            VowelHints = ValidateHints(vowelHints, nameof(vowelHints));
        }

        static IReadOnlyList<string> ValidateHints(IReadOnlyList<string> hints, string name)
        {
            if (hints == null) throw new ArgumentNullException(name);
            if (hints.Count != GripLayout.CellCount) throw new ArgumentException("ヒントは " + GripLayout.CellCount + " マス分が必要です。", name);
            foreach (string hint in hints)
            {
                if (hint == null) throw new ArgumentException("ヒントのない位置は空文字列にします。", name);
            }
            return hints;
        }

        public GripPage Page { get; }

        /// <summary>子音側のグリップを押していて、子音パネルが機能ページを表示している。</summary>
        public bool ConsonantFunctionPage { get; }

        /// <summary>母音側のグリップを押していて、母音パネルが機能ページ（3×3）を表示している。</summary>
        public bool VowelFunctionPage { get; }

        /// <summary>子音パネルの9マス（行優先）。</summary>
        public IReadOnlyList<GripCellView> ConsonantCells { get; }

        /// <summary>選択中のマス（0..8）。</summary>
        public int ConsonantSelected { get; }

        /// <summary>
        /// 母音パネルのマス。機能ページでは9マス（行優先）、それ以外は5方向（中央・左・上・右・下）で、
        /// 文字のないマスのラベルは空文字列。
        /// </summary>
        public IReadOnlyList<GripCellView> VowelCells { get; }

        /// <summary>選択中のマス。機能ページでは 0..8、それ以外は StickDirection の値。</summary>
        public int VowelSelected { get; }

        /// <summary>
        /// 子音パネルが通常のページのとき、各マス（行優先の9マス）に重ねて示す、同じ位置の機能ページのキーの頭文字（1文字）。
        /// 機能ページの空きマスと、機能ページを表示している間は空文字列。
        /// </summary>
        public IReadOnlyList<string> ConsonantHints { get; }

        /// <summary>
        /// 母音パネルが通常のページのとき、3×3 の各位置（行優先の9マス。5方向のマスがない四隅を含む）に示す、
        /// 同じ位置の機能ページのキーの頭文字（1文字）。機能ページの空きマスと、機能ページを表示している間は空文字列。
        /// </summary>
        public IReadOnlyList<string> VowelHints { get; }
    }
}
