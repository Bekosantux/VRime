using System;
using System.Collections.Generic;

namespace VRime.Core.Grip
{
    /// <summary>パネルのセルの表示。</summary>
    public readonly struct GripCellView
    {
        /// <summary>方向のヒントを持つ方向の数（左・上・右・下）。</summary>
        public const int DirectionHintCount = GripCell.DirectionCount - 1;

        static readonly string[] NoDirectionHints = { "", "", "", "" };

        // 左・上・右・下の順（StickDirection の値 - 1 を添字にする）。
        readonly IReadOnlyList<string> _directionHints;

        /// <summary>方向のヒントのないセル。</summary>
        public GripCellView(string label, bool disabled, bool lit)
            : this(label, disabled, lit, NoDirectionHints)
        {
        }

        /// <param name="directionHints">左・上・右・下の順の4つ。ヒントのない方向は空文字列。</param>
        public GripCellView(string label, bool disabled, bool lit, IReadOnlyList<string> directionHints)
        {
            Label = label ?? throw new ArgumentNullException(nameof(label));
            Disabled = disabled;
            Lit = lit;
            if (directionHints == null) throw new ArgumentNullException(nameof(directionHints));
            if (directionHints.Count != DirectionHintCount)
                throw new ArgumentException("方向のヒントは左・上・右・下の " + DirectionHintCount + " つが必要です。", nameof(directionHints));
            foreach (string hint in directionHints)
            {
                if (hint == null) throw new ArgumentException("ヒントのない方向は空文字列にします。", nameof(directionHints));
            }
            _directionHints = directionHints;
        }

        /// <summary>表示する文字。入力される文字（大文字・全角を反映）または動作名。</summary>
        public string Label { get; }

        /// <summary>灰色で表示し、トリガーを引いても何もしない。</summary>
        public bool Disabled { get; }

        /// <summary>オンの状態を示す点灯。</summary>
        public bool Lit { get; }

        /// <summary>
        /// 文字グループのセルで、母音パネルをその方向に倒したときに入力される文字（大文字・全角を反映）。
        /// セルの同じ側の辺に示す。未割り当ての方向と、文字グループ以外のセルは空文字列。
        /// </summary>
        public string DirectionHint(StickDirection direction)
        {
            if (direction == StickDirection.Center || !Enum.IsDefined(typeof(StickDirection), direction))
                throw new ArgumentOutOfRangeException(nameof(direction), direction, "方向のヒントは左・上・右・下にだけあります。");
            if (_directionHints == null) throw new InvalidOperationException("初期化されていないセルです。");
            return _directionHints[(int)direction - 1];
        }
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
            if (hints.Count != GripLayout.CellCount) throw new ArgumentException("ヒントは " + GripLayout.CellCount + " セル分が必要です。", name);
            foreach (string hint in hints)
            {
                if (hint == null) throw new ArgumentException("ヒントのない位置は空文字列にします。", name);
            }
            return hints;
        }

        public GripPage Page { get; }

        /// <summary>子音側のパネルの切り替えのボタンを押していて、子音パネルが機能ページを表示している。</summary>
        public bool ConsonantFunctionPage { get; }

        /// <summary>母音側のパネルの切り替えのボタンを押していて、母音パネルが機能ページ（3×3）を表示している。</summary>
        public bool VowelFunctionPage { get; }

        /// <summary>子音パネルの9つのセル（行優先）。</summary>
        public IReadOnlyList<GripCellView> ConsonantCells { get; }

        /// <summary>選択中のセル（0..8）。</summary>
        public int ConsonantSelected { get; }

        /// <summary>
        /// 母音パネルのセル。機能ページでは9つのセル（行優先）、それ以外は5方向（中央・左・上・右・下）で、
        /// 文字のないセルのラベルは空文字列。
        /// </summary>
        public IReadOnlyList<GripCellView> VowelCells { get; }

        /// <summary>選択中のセル。機能ページでは 0..8、それ以外は StickDirection の値。</summary>
        public int VowelSelected { get; }

        /// <summary>
        /// 子音パネルが通常のページのとき、各セル（行優先の9つのセル）に重ねて示す、同じ位置の機能ページのキーの頭文字（1文字）。
        /// 機能ページの空きセルと、機能ページを表示している間は空文字列。
        /// </summary>
        public IReadOnlyList<string> ConsonantHints { get; }

        /// <summary>
        /// 母音パネルが通常のページのとき、3×3 の各位置（行優先の9つのセル。5方向のセルがない四隅を含む）に示す、
        /// 同じ位置の機能ページのキーの頭文字（1文字）。機能ページの空きセルと、機能ページを表示している間は空文字列。
        /// </summary>
        public IReadOnlyList<string> VowelHints { get; }
    }
}
