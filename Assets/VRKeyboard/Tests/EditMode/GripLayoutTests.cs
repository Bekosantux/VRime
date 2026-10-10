using System;
using NUnit.Framework;
using VRKeyboard.Core.Grip;

namespace VRKeyboard.Core.Tests
{
    /// <summary>SPEC 5.2 の表との一致を確認する。表は SPEC の文字を \u 表記でも照合できるよう、そのまま書き写している。</summary>
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
        public void かなページの全セル全方向()
        {
            for (int cell = 0; cell < 9; cell++)
            {
                AssertGroup(GripLayout.GetConsonantCell(GripPage.Kana, false, cell), KanaRows[cell]);
            }
        }

        [Test]
        public void 英字ページの全セル全方向()
        {
            for (int cell = 0; cell < 9; cell++)
            {
                AssertGroup(GripLayout.GetConsonantCell(GripPage.Alpha, false, cell), AlphaRows[cell]);
            }
        }

        [Test]
        public void 数字ページは中央のみ文字があり他は未割り当て()
        {
            for (int cell = 0; cell < 9; cell++)
            {
                GripCell group = GripLayout.GetConsonantCell(GripPage.Number, false, cell);
                Assert.AreEqual(GripCellKind.Characters, group.Kind);
                Assert.AreEqual((cell + 1).ToString(), group.CharacterAt(StickDirection.Center));
                foreach (StickDirection direction in new[] { StickDirection.Left, StickDirection.Up, StickDirection.Right, StickDirection.Down })
                {
                    Assert.IsNull(group.CharacterAt(direction), "セル " + cell + " " + direction);
                }
            }
        }

        [TestCase(GripPage.Kana)]
        [TestCase(GripPage.Alpha)]
        [TestCase(GripPage.Number)]
        public void 機能ページの上6マスは全ページ共通(GripPage page)
        {
            int[] empty = { 0, 1, 3, 4 };
            foreach (int cell in empty)
            {
                Assert.AreEqual(GripCellKind.Empty, GripLayout.GetConsonantCell(page, true, cell).Kind, "セル " + cell);
            }
            GripCell erase = GripLayout.GetConsonantCell(page, true, 2);
            Assert.AreEqual(GripCellKind.Action, erase.Kind);
            Assert.AreEqual(GripAction.Erase, erase.Action);
            GripCell space = GripLayout.GetConsonantCell(page, true, 5);
            Assert.AreEqual(GripCellKind.Action, space.Kind);
            Assert.AreEqual(GripAction.Space, space.Action);
        }

        [Test]
        public void かなの機能ページの下3マス()
        {
            GripCell modifier = GripLayout.GetConsonantCell(GripPage.Kana, true, 6);
            Assert.AreEqual(GripCellKind.Action, modifier.Kind);
            Assert.AreEqual(GripAction.CharacterModifier, modifier.Action);
            Assert.AreEqual("小゛゜", modifier.Label);

            GripCell wa = GripLayout.GetConsonantCell(GripPage.Kana, true, 7);
            AssertGroup(wa, "わをんー〜");
            Assert.AreEqual("わ", wa.Label);

            GripCell symbol = GripLayout.GetConsonantCell(GripPage.Kana, true, 8);
            AssertGroup(symbol, "、。？！…");
            Assert.AreEqual("記号", symbol.Label);
        }

        [Test]
        public void 英字の機能ページの下3マス()
        {
            Assert.AreEqual(GripCellKind.Empty, GripLayout.GetConsonantCell(GripPage.Alpha, true, 6).Kind);
            Assert.AreEqual(GripCellKind.Empty, GripLayout.GetConsonantCell(GripPage.Alpha, true, 7).Kind);
            AssertGroup(GripLayout.GetConsonantCell(GripPage.Alpha, true, 8), ",.?!'");
        }

        [Test]
        public void 数字の機能ページの下3マス()
        {
            AssertGroup(GripLayout.GetConsonantCell(GripPage.Number, true, 6), "-+*/~");
            AssertGroup(GripLayout.GetConsonantCell(GripPage.Number, true, 7), "0(=)&");
            AssertGroup(GripLayout.GetConsonantCell(GripPage.Number, true, 8), ".,:;%");
        }

        [Test]
        public void 母音側の機能ページ()
        {
            GripAction?[] expected =
            {
                GripAction.ToggleWidth, null, GripAction.NumberPage,
                GripAction.CursorLeft, GripAction.ToggleCase, GripAction.CursorRight,
                GripAction.Language, GripAction.Convert, GripAction.Commit,
            };
            string[] labels = { "全/半", "・", "数字", "←", "↑", "→", "言語", "変換", "確定" };
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
            Assert.Throws<ArgumentOutOfRangeException>(() => GripLayout.GetConsonantCell(GripPage.Kana, false, 9));
            Assert.Throws<ArgumentOutOfRangeException>(() => GripLayout.GetConsonantCell(GripPage.Kana, false, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => GripLayout.GetVowelFunctionCell(9));
        }

        [Test]
        public void セルの種類に合わないアクセスは例外()
        {
            Assert.Throws<InvalidOperationException>(() => { var _ = GripCell.Empty.Action; });
            Assert.Throws<InvalidOperationException>(() => GripCell.Empty.CharacterAt(StickDirection.Center));
            GripCell action = GripLayout.GetVowelFunctionCell(0);
            Assert.Throws<InvalidOperationException>(() => action.CharacterAt(StickDirection.Center));
            GripCell group = GripLayout.GetConsonantCell(GripPage.Kana, false, 0);
            Assert.Throws<InvalidOperationException>(() => { var _ = group.Action; });
        }
    }
}
