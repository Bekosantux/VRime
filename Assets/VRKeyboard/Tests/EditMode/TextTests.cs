using System;
using NUnit.Framework;
using VRKeyboard.Core.Text;

namespace VRKeyboard.Core.Tests
{
    public class KanaModifierTests
    {
        static string Cycle(char start, int steps)
        {
            var result = new System.Text.StringBuilder();
            char current = start;
            for (int i = 0; i < steps; i++)
            {
                Assert.IsTrue(KanaModifier.TryNext(current, out current));
                result.Append(current);
            }
            return result.ToString();
        }

        [Test]
        public void か_が_か()
        {
            Assert.AreEqual("がかが", Cycle('か', 3));
        }

        [Test]
        public void は_ば_ぱ_は()
        {
            Assert.AreEqual("ばぱはば", Cycle('は', 4));
        }

        [Test]
        public void つ_っ_づ_つ()
        {
            Assert.AreEqual("っづつっ", Cycle('つ', 4));
        }

        [Test]
        public void あ_ぁ_あ()
        {
            Assert.AreEqual("ぁあぁ", Cycle('あ', 3));
        }

        [Test]
        public void う_ぅ_ゔ_う()
        {
            Assert.AreEqual("ぅゔう", Cycle('う', 3));
        }

        [Test]
        public void や_ゃ_や()
        {
            Assert.AreEqual("ゃや", Cycle('や', 2));
        }

        [TestCase('な')]
        [TestCase('ん')]
        [TestCase('ー')]
        [TestCase('A')]
        public void 対象外の文字は巡回しない(char c)
        {
            Assert.IsFalse(KanaModifier.TryNext(c, out char next));
            Assert.AreEqual(c, next);
        }
    }

    public class CharacterWidthTests
    {
        [Test]
        public void ASCII印字可能文字は全角にする()
        {
            for (char c = '!'; c <= '~'; c++)
            {
                Assert.AreEqual((char)(0xFF01 + (c - '!')), CharacterWidth.ToFullWidth(c));
            }
            Assert.AreEqual('Ａ', CharacterWidth.ToFullWidth('A'));
            Assert.AreEqual('～', CharacterWidth.ToFullWidth('~'));
            Assert.AreEqual('０', CharacterWidth.ToFullWidth('0'));
        }

        [Test]
        public void スペースはU3000()
        {
            Assert.AreEqual('　', CharacterWidth.ToFullWidth(' '));
        }

        [Test]
        public void ASCII以外はそのまま()
        {
            Assert.AreEqual('あ', CharacterWidth.ToFullWidth('あ'));
            Assert.AreEqual('Ａ', CharacterWidth.ToFullWidth('Ａ'));
        }

        [Test]
        public void 文字列を全角にする()
        {
            Assert.AreEqual("ＡＢ　１あ", CharacterWidth.ToFullWidth("AB 1あ"));
            Assert.Throws<ArgumentNullException>(() => CharacterWidth.ToFullWidth((string)null));
        }
    }

    public class KatakanaTests
    {
        [Test]
        public void ひらがなをカタカナにする()
        {
            Assert.AreEqual("キョウハイイテンキ", Katakana.FromHiragana("きょうはいいてんき"));
            Assert.AreEqual("ヴァ", Katakana.FromHiragana("ゔぁ"));
            Assert.AreEqual("ァ", Katakana.FromHiragana("ぁ"));
            Assert.AreEqual("ン", Katakana.FromHiragana("ん"));
        }

        [Test]
        public void ひらがな以外はそのまま()
        {
            Assert.AreEqual("ー、。A1漢", Katakana.FromHiragana("ー、。A1漢"));
            Assert.AreEqual("", Katakana.FromHiragana(""));
        }
    }

    public class InputBufferTests
    {
        [Test]
        public void 未確定のかなを挿入しカーソルが進む()
        {
            var buffer = new InputBuffer(144);
            Assert.AreEqual(EditResult.Done, buffer.InsertComposition("あ"));
            buffer.InsertComposition("い");
            Assert.AreEqual("あい", buffer.Composition);
            Assert.AreEqual(2, buffer.CompositionCursor);
            Assert.AreEqual("", buffer.Committed);
            Assert.AreEqual(2, buffer.Length);
        }

        [Test]
        public void 未確定のカーソル位置に挿入する()
        {
            var buffer = new InputBuffer(144);
            buffer.InsertComposition("あう");
            buffer.MoveCursorLeft();
            buffer.InsertComposition("い");
            Assert.AreEqual("あいう", buffer.Composition);
            Assert.AreEqual(2, buffer.CompositionCursor);
        }

        [Test]
        public void 未確定をそのまま確定するとカーソルは確定した文字の後ろ()
        {
            var buffer = new InputBuffer(144);
            buffer.InsertComposition("あい");
            buffer.MoveCursorLeft(); // 未確定の中のカーソルは確定後に影響しない
            Assert.AreEqual(EditResult.Done, buffer.CommitComposition());
            Assert.AreEqual("あい", buffer.Committed);
            Assert.AreEqual(2, buffer.CommittedCursor);
            Assert.AreEqual("", buffer.Composition);
            Assert.AreEqual(0, buffer.CompositionCursor);
            Assert.AreEqual(EditResult.NothingToDo, buffer.CommitComposition());
        }

        [Test]
        public void 未確定のかなは確定済みのカーソル位置にある()
        {
            var buffer = new InputBuffer(144);
            buffer.InsertCommitted("ab");
            buffer.MoveCursorLeft(); // 確定済みのカーソルは 1
            buffer.InsertComposition("あ");
            buffer.CommitComposition();
            Assert.AreEqual("aあb", buffer.Committed);
            Assert.AreEqual(2, buffer.CommittedCursor);
        }

        [Test]
        public void 確定済みへの直接挿入は未確定があれば先にそのまま確定する()
        {
            var buffer = new InputBuffer(144);
            buffer.InsertComposition("あ");
            Assert.AreEqual(EditResult.Done, buffer.InsertCommitted("A"));
            Assert.AreEqual("あA", buffer.Committed);
            Assert.AreEqual(2, buffer.CommittedCursor);
            Assert.AreEqual("", buffer.Composition);
        }

        [Test]
        public void 変換結果で置き換えて確定する()
        {
            var buffer = new InputBuffer(144);
            buffer.InsertCommitted("前");
            buffer.InsertComposition("きょう");
            Assert.AreEqual(EditResult.Done, buffer.CommitConverted("今日"));
            Assert.AreEqual("前今日", buffer.Committed);
            Assert.AreEqual(3, buffer.CommittedCursor);
            Assert.AreEqual("", buffer.Composition);
            Assert.AreEqual(EditResult.NothingToDo, buffer.CommitConverted("x"));
        }

        [Test]
        public void 消去は未確定があればその中で動作する()
        {
            var buffer = new InputBuffer(144);
            buffer.InsertCommitted("確定");
            buffer.InsertComposition("あい");
            Assert.AreEqual(EditResult.Done, buffer.Erase());
            Assert.AreEqual("あ", buffer.Composition);
            Assert.AreEqual("確定", buffer.Committed);
            buffer.MoveCursorLeft();
            // 未確定の先頭では、確定済みの文字を消さない。
            Assert.AreEqual(EditResult.NothingToDo, buffer.Erase());
            Assert.AreEqual("確定", buffer.Committed);
            buffer.MoveCursorRight();
            buffer.Erase();
            // 未確定が空になれば、確定済みで動作する。
            Assert.AreEqual(EditResult.Done, buffer.Erase());
            Assert.AreEqual("確", buffer.Committed);
            Assert.AreEqual(1, buffer.CommittedCursor);
        }

        [Test]
        public void 消去はカーソルの直前を消す()
        {
            var buffer = new InputBuffer(144);
            buffer.InsertCommitted("abc");
            buffer.MoveCursorLeft();
            buffer.Erase();
            Assert.AreEqual("ac", buffer.Committed);
            Assert.AreEqual(1, buffer.CommittedCursor);
            buffer.Erase();
            Assert.AreEqual("c", buffer.Committed);
            Assert.AreEqual(EditResult.NothingToDo, buffer.Erase());
        }

        [Test]
        public void カーソル移動は未確定があればその中で動作する()
        {
            var buffer = new InputBuffer(144);
            buffer.InsertCommitted("ab");
            buffer.InsertComposition("あい");
            Assert.AreEqual(EditResult.NothingToDo, buffer.MoveCursorRight());
            Assert.AreEqual(EditResult.Done, buffer.MoveCursorLeft());
            Assert.AreEqual(EditResult.Done, buffer.MoveCursorLeft());
            Assert.AreEqual(0, buffer.CompositionCursor);
            // 未確定の外へは出ない。
            Assert.AreEqual(EditResult.NothingToDo, buffer.MoveCursorLeft());
            Assert.AreEqual(2, buffer.CommittedCursor);
            buffer.CommitComposition();
            Assert.AreEqual(EditResult.Done, buffer.MoveCursorLeft());
            Assert.AreEqual(3, buffer.CommittedCursor);
        }

        [Test]
        public void カーソル移動は確定済みの両端で止まる()
        {
            var buffer = new InputBuffer(144);
            buffer.InsertCommitted("ab");
            Assert.AreEqual(EditResult.NothingToDo, buffer.MoveCursorRight());
            buffer.MoveCursorLeft();
            buffer.MoveCursorLeft();
            Assert.AreEqual(EditResult.NothingToDo, buffer.MoveCursorLeft());
            Assert.AreEqual(0, buffer.CommittedCursor);
        }

        [Test]
        public void 小濁点半濁点は未確定の中で動作する()
        {
            var buffer = new InputBuffer(144);
            buffer.InsertCommitted("か");
            buffer.InsertComposition("は");
            Assert.AreEqual(EditResult.Done, buffer.ApplyKanaModifier());
            Assert.AreEqual("ば", buffer.Composition);
            buffer.ApplyKanaModifier();
            Assert.AreEqual("ぱ", buffer.Composition);
            Assert.AreEqual("か", buffer.Committed);
        }

        [Test]
        public void 小濁点半濁点は未確定がなければ確定済みで動作する()
        {
            var buffer = new InputBuffer(144);
            buffer.InsertCommitted("かつ");
            buffer.ApplyKanaModifier();
            Assert.AreEqual("かっ", buffer.Committed);
            buffer.ApplyKanaModifier();
            Assert.AreEqual("かづ", buffer.Committed);
            buffer.ApplyKanaModifier();
            Assert.AreEqual("かつ", buffer.Committed);
        }

        [Test]
        public void 小濁点半濁点は対象がなければ何もしない()
        {
            var buffer = new InputBuffer(144);
            Assert.AreEqual(EditResult.NothingToDo, buffer.ApplyKanaModifier());
            buffer.InsertComposition("な");
            Assert.AreEqual(EditResult.NothingToDo, buffer.ApplyKanaModifier());
            Assert.AreEqual("な", buffer.Composition);
        }

        [Test]
        public void 全消去()
        {
            var buffer = new InputBuffer(144);
            buffer.InsertCommitted("ab");
            buffer.InsertComposition("あ");
            buffer.Clear();
            Assert.AreEqual(0, buffer.Length);
            Assert.AreEqual(0, buffer.CommittedCursor);
            Assert.AreEqual(0, buffer.CompositionCursor);
            Assert.IsTrue(buffer.IsEmpty);
        }

        [Test]
        public void 合計長が最大長を超える操作は実行しない()
        {
            var buffer = new InputBuffer(5);
            buffer.InsertCommitted("abc");
            buffer.InsertComposition("あ");
            Assert.AreEqual(1, buffer.Remaining);
            Assert.AreEqual(EditResult.Done, buffer.InsertComposition("い"));
            Assert.AreEqual(0, buffer.Remaining);

            Assert.AreEqual(EditResult.TooLong, buffer.InsertComposition("う"));
            Assert.AreEqual(EditResult.TooLong, buffer.InsertCommitted("x"));
            Assert.AreEqual("abc", buffer.Committed);
            // 失敗した挿入で、未確定が先に確定されることもない。
            Assert.AreEqual("あい", buffer.Composition);
            Assert.AreEqual(5, buffer.Length);
        }

        [Test]
        public void 最大長ちょうどまでは入力できる()
        {
            var buffer = new InputBuffer(144);
            for (int i = 0; i < 144; i++) Assert.AreEqual(EditResult.Done, buffer.InsertComposition("あ"), "文字 " + i);
            Assert.AreEqual(EditResult.TooLong, buffer.InsertComposition("あ"));
            Assert.AreEqual(144, buffer.Length);
        }

        [Test]
        public void 長さはUTF16単位で数える()
        {
            var buffer = new InputBuffer(3);
            Assert.AreEqual(EditResult.Done, buffer.InsertCommitted("\U0001F600")); // 絵文字は2
            Assert.AreEqual(2, buffer.Length);
            Assert.AreEqual(EditResult.TooLong, buffer.InsertCommitted("\U0001F600"));
            Assert.AreEqual(EditResult.Done, buffer.InsertCommitted("a"));
        }

        [Test]
        public void 変換結果が最大長を超えるなら確定しない()
        {
            var buffer = new InputBuffer(3);
            buffer.InsertComposition("あい");
            Assert.AreEqual(EditResult.TooLong, buffer.CommitConverted("あいうえ"));
            Assert.AreEqual("あい", buffer.Composition);
            Assert.AreEqual(EditResult.Done, buffer.CommitConverted("愛"));
        }

        [Test]
        public void サロゲートペアは1文字として消去とカーソル移動をする()
        {
            var buffer = new InputBuffer(144);
            buffer.InsertCommitted("a\U0001F600");
            buffer.MoveCursorLeft();
            Assert.AreEqual(1, buffer.CommittedCursor);
            buffer.MoveCursorRight();
            Assert.AreEqual(3, buffer.CommittedCursor);
            buffer.Erase();
            Assert.AreEqual("a", buffer.Committed);
        }

        [Test]
        public void 空の挿入と不正な最大長は例外()
        {
            var buffer = new InputBuffer(10);
            Assert.Throws<ArgumentException>(() => buffer.InsertComposition(""));
            Assert.Throws<ArgumentException>(() => buffer.InsertCommitted(null));
            Assert.Throws<ArgumentNullException>(() => buffer.CommitConverted(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new InputBuffer(0));
        }

        [Test]
        public void 内容が変わるとVersionが増え_変わらないと増えない()
        {
            var buffer = new InputBuffer(144);
            int version = buffer.Version;
            buffer.InsertComposition("あ");
            Assert.Greater(buffer.Version, version);
            version = buffer.Version;
            buffer.MoveCursorRight(); // 末尾なので何も変わらない
            Assert.AreEqual(version, buffer.Version);
        }
    }
}
