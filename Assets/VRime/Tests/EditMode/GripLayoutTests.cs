using System;
using NUnit.Framework;
using VRime.Core.Grip;

namespace VRime.Core.Tests
{
    /// <summary>docs/spec/grip-input.md の文字の配置の表との一致を確認する。表は仕様書の文字を \u 表記でも照合できるよう、そのまま書き写している。</summary>
    public class GripLayoutTests
    {
        static readonly StickDirection[] Directions =
        {
            StickDirection.Center, StickDirection.Left, StickDirection.Up, StickDirection.Right, StickDirection.Down,
        };

        // 各行は 中央・左・上・右・下
        static readonly string[] KanaRows =
        {
            "あいうえお", "かきくけこ", "さしすせそ",
            "たちつてと", "なにぬねの", "はひふへほ",
            "まみむめも", "や「ゆ」よ", "らりるれろ",
        };

        static readonly string[] AlphaRows =
        {
            "./@:1", "abc#2", "def_3",
            "ghi%4", "jkl&5", "mno=6",
            "pqrs7", "tuv~8", "wxyz9",
        };

        static void AssertGroup(GripCell cell, string expected)
        {
            Assert.AreEqual(GripCellKind.Characters, cell.Kind);
            for (int i = 0; i < Directions.Length; i++)
            {
                Assert.AreEqual(expected[i].ToString(), cell.CharacterAt(Directions[i]), "方向 " + Directions[i]);
            }
        }

        [Test]
        public void かなモードの全セル全方向()
        {
            for (int cell = 0; cell < 9; cell++)
            {
                AssertGroup(GripLayout.GetConsonantCell(GripInputMode.Kana, false, cell), KanaRows[cell]);
            }
        }

        [Test]
        public void 英字モードの全セル全方向()
        {
            for (int cell = 0; cell < 9; cell++)
            {
                AssertGroup(GripLayout.GetConsonantCell(GripInputMode.Alpha, false, cell), AlphaRows[cell]);
            }
        }

        [Test]
        public void 数字モードは中央のみ文字があり他は未割り当て()
        {
            for (int cell = 0; cell < 9; cell++)
            {
                GripCell group = GripLayout.GetConsonantCell(GripInputMode.Number, false, cell);
                Assert.AreEqual(GripCellKind.Characters, group.Kind);
                Assert.AreEqual((cell + 1).ToString(), group.CharacterAt(StickDirection.Center));
                foreach (StickDirection direction in new[] { StickDirection.Left, StickDirection.Up, StickDirection.Right, StickDirection.Down })
                {
                    Assert.IsNull(group.CharacterAt(direction), "セル " + cell + " " + direction);
                }
            }
        }

        [TestCase(GripInputMode.Kana)]
        [TestCase(GripInputMode.Alpha)]
        [TestCase(GripInputMode.Number)]
        public void 機能レイヤーの上段と下段は全入力モード共通(GripInputMode mode)
        {
            int[] empty = { 0, 1, 6, 7 };
            foreach (int cell in empty)
            {
                Assert.AreEqual(GripCellKind.Empty, GripLayout.GetConsonantCell(mode, true, cell).Kind, "セル " + cell);
            }
            GripCell erase = GripLayout.GetConsonantCell(mode, true, 2);
            Assert.AreEqual(GripCellKind.Action, erase.Kind);
            Assert.AreEqual(GripAction.Erase, erase.Action);
            GripCell space = GripLayout.GetConsonantCell(mode, true, 8);
            Assert.AreEqual(GripCellKind.Action, space.Kind);
            Assert.AreEqual(GripAction.Space, space.Action);
        }

        [Test]
        public void かなモードの機能レイヤーの中段の3つのセル()
        {
            GripCell modifier = GripLayout.GetConsonantCell(GripInputMode.Kana, true, 3);
            Assert.AreEqual(GripCellKind.Action, modifier.Kind);
            Assert.AreEqual(GripAction.CharacterModifier, modifier.Action);
            Assert.AreEqual("小゛゜", modifier.Label);

            GripCell wa = GripLayout.GetConsonantCell(GripInputMode.Kana, true, 4);
            AssertGroup(wa, "わをんー〜");
            Assert.AreEqual("わ", wa.Label);

            GripCell symbol = GripLayout.GetConsonantCell(GripInputMode.Kana, true, 5);
            AssertGroup(symbol, "、。？！…");
            Assert.AreEqual("記号", symbol.Label);
        }

        [Test]
        public void 英字モードの機能レイヤーの中段の3つのセル()
        {
            Assert.AreEqual(GripCellKind.Empty, GripLayout.GetConsonantCell(GripInputMode.Alpha, true, 3).Kind);
            Assert.AreEqual(GripCellKind.Empty, GripLayout.GetConsonantCell(GripInputMode.Alpha, true, 4).Kind);
            AssertGroup(GripLayout.GetConsonantCell(GripInputMode.Alpha, true, 5), ",.?!'");
        }

        [Test]
        public void 数字モードの機能レイヤーの中段の3つのセル()
        {
            AssertGroup(GripLayout.GetConsonantCell(GripInputMode.Number, true, 3), "-+*/~");
            AssertGroup(GripLayout.GetConsonantCell(GripInputMode.Number, true, 4), "0(=)&");
            AssertGroup(GripLayout.GetConsonantCell(GripInputMode.Number, true, 5), ".,:;%");
        }

        [Test]
        public void 母音側の機能レイヤー()
        {
            GripAction?[] expected =
            {
                GripAction.ToggleWidth, GripAction.Language, GripAction.ToggleNumber,
                GripAction.CursorLeft, GripAction.Commit, GripAction.CursorRight,
                GripAction.ToggleCase, GripAction.Convert, GripAction.TogglePasteMode,
            };
            string[] labels = { "全/半", "言語", "数字", "←", "確定", "→", "↑", "変換", "貼付" };
            for (int cell = 0; cell < 9; cell++)
            {
                GripCell actual = GripLayout.GetVowelFunctionCell(cell);
                if (expected[cell] == null)
                {
                    Assert.AreEqual(GripCellKind.Empty, actual.Kind, "セル " + cell);
                }
                else
                {
                    Assert.AreEqual(GripCellKind.Action, actual.Kind, "セル " + cell);
                    Assert.AreEqual(expected[cell].Value, actual.Action, "セル " + cell);
                }
                Assert.AreEqual(labels[cell], actual.Label, "セル " + cell);
            }
        }

        [Test]
        public void 範囲外のセルは例外()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => GripLayout.GetConsonantCell(GripInputMode.Kana, false, 9));
            Assert.Throws<ArgumentOutOfRangeException>(() => GripLayout.GetConsonantCell(GripInputMode.Kana, false, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => GripLayout.GetVowelFunctionCell(9));
        }

        [Test]
        public void セルの種類に合わないアクセスは例外()
        {
            Assert.Throws<InvalidOperationException>(() => { var _ = GripCell.Empty.Action; });
            Assert.Throws<InvalidOperationException>(() => GripCell.Empty.CharacterAt(StickDirection.Center));
            GripCell action = GripLayout.GetVowelFunctionCell(0);
            Assert.Throws<InvalidOperationException>(() => action.CharacterAt(StickDirection.Center));
            GripCell group = GripLayout.GetConsonantCell(GripInputMode.Kana, false, 0);
            Assert.Throws<InvalidOperationException>(() => { var _ = group.Action; });
        }
    }
}
