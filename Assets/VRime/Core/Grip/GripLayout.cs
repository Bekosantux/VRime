using System;

namespace VRime.Core.Grip
{
    /// <summary>
    /// docs/spec/grip-input.md の文字の配置の表。子音パネルの各ページと母音パネルの機能ページのセルを返す。
    /// 文字は中央・左・上・右・下の順。英字は小文字で持ち、大文字・全角は入力時に変換する。
    /// </summary>
    public static class GripLayout
    {
        public const int CellCount = 9;

        static readonly GripCell[] KanaCells =
        {
            Group("あいうえお"), Group("かきくけこ"), Group("さしすせそ"),
            Group("たちつてと"), Group("なにぬねの"), Group("はひふへほ"),
            Group("まみむめも"), Group("や「ゆ」よ"), Group("らりるれろ"),
        };

        static readonly GripCell[] AlphaCells =
        {
            Group("./@:1"), Group("abc#2"), Group("def_3"),
            Group("ghi%4"), Group("jkl&5"), Group("mno=6"),
            Group("pqrs7"), Group("tuv~8"), Group("wxyz9"),
        };

        // 中央のみ文字があり、他の方向は未割り当て（docs/ROADMAP.md の U-12）。
        static readonly GripCell[] NumberCells =
        {
            Single("1"), Single("2"), Single("3"),
            Single("4"), Single("5"), Single("6"),
            Single("7"), Single("8"), Single("9"),
        };

        // 機能ページの上段と下段はすべてのページで共通。中段はページごとに異なる。
        static readonly GripCell Erase = GripCell.ForAction(GripAction.Erase, "消去");
        static readonly GripCell Space = GripCell.ForAction(GripAction.Space, "空白");

        static readonly GripCell[] KanaFunctionCells = BuildFunctionCells(
            GripCell.ForAction(GripAction.CharacterModifier, "小゛゜"),
            Group("わをんー〜", "わ"),
            Group("、。？！…", "記号"));

        static readonly GripCell[] AlphaFunctionCells = BuildFunctionCells(
            GripCell.Empty,
            GripCell.Empty,
            Group(",.?!'"));

        static readonly GripCell[] NumberFunctionCells = BuildFunctionCells(
            Group("-+*/~"),
            Group("0(=)&"),
            Group(".,:;%"));

        static readonly GripCell[] VowelFunctionCells =
        {
            GripCell.ForAction(GripAction.ToggleWidth, "全/半"), GripCell.ForAction(GripAction.Language, "言語"), GripCell.ForAction(GripAction.NumberPage, "数字"),
            GripCell.ForAction(GripAction.CursorLeft, "←"), GripCell.ForAction(GripAction.ToggleCase, "↑"), GripCell.ForAction(GripAction.CursorRight, "→"),
            GripCell.ForAction(GripAction.TogglePasteMode, "貼付"), GripCell.ForAction(GripAction.Convert, "変換"), GripCell.ForAction(GripAction.Commit, "確定"),
        };

        /// <summary>子音パネルのセル。cell は 0..8（行優先、左上が 0、中央が 4）。</summary>
        public static GripCell GetConsonantCell(GripPage page, bool functionPage, int cell)
        {
            if (cell < 0 || cell >= CellCount) throw new ArgumentOutOfRangeException(nameof(cell), cell, null);
            return Cells(page, functionPage)[cell];
        }

        /// <summary>母音側の機能ページのセル。cell は 0..8。</summary>
        public static GripCell GetVowelFunctionCell(int cell)
        {
            if (cell < 0 || cell >= CellCount) throw new ArgumentOutOfRangeException(nameof(cell), cell, null);
            return VowelFunctionCells[cell];
        }

        static GripCell[] Cells(GripPage page, bool functionPage)
        {
            switch (page)
            {
                case GripPage.Kana: return functionPage ? KanaFunctionCells : KanaCells;
                case GripPage.Alpha: return functionPage ? AlphaFunctionCells : AlphaCells;
                case GripPage.Number: return functionPage ? NumberFunctionCells : NumberCells;
                default: throw new ArgumentOutOfRangeException(nameof(page), page, null);
            }
        }

        static GripCell[] BuildFunctionCells(GripCell middleLeft, GripCell middle, GripCell middleRight)
        {
            return new[]
            {
                GripCell.Empty, GripCell.Empty, Erase,
                middleLeft, middle, middleRight,
                GripCell.Empty, GripCell.Empty, Space,
            };
        }

        static GripCell Group(string fiveCharacters, string label = null)
        {
            if (fiveCharacters.Length != GripCell.DirectionCount)
                throw new ArgumentException("5文字が必要です: " + fiveCharacters, nameof(fiveCharacters));
            var characters = new string[GripCell.DirectionCount];
            for (int i = 0; i < characters.Length; i++) characters[i] = fiveCharacters[i].ToString();
            return GripCell.ForCharacters(characters, label);
        }

        static GripCell Single(string center)
        {
            return GripCell.ForCharacters(new[] { center, null, null, null, null });
        }
    }
}
