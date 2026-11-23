using NUnit.Framework;
using VRime.Core.Grip;
using VRime.Core.Input;

namespace VRime.Core.Tests
{
    /// <summary>グリップ入力（docs/spec/grip-input.md）の振る舞い。Rig を通して、実際の操作と同じ入力列で確認する。</summary>
    public class GripInputTests
    {
        static readonly string[] NoHints = { "", "", "", "", "", "", "", "", "" };

        static void ToEnglish(Rig rig)
        {
            rig.VowelFunction(VowelFunctionCells.Language);
        }

        [Test]
        public void かなは未確定に入る()
        {
            var rig = new Rig();
            rig.Type(0, StickDirection.Center);
            rig.Type(1, StickDirection.Left);
            Assert.AreEqual("あき", rig.Session.Composition);
            Assert.AreEqual("", rig.Session.Committed);
            Assert.AreEqual(2, rig.Session.CompositionCursor);
        }

        [Test]
        public void かぎ括弧も未確定に入る()
        {
            var rig = new Rig();
            rig.Type(7, StickDirection.Left);
            rig.Type(7, StickDirection.Right);
            Assert.AreEqual("「」", rig.Session.Composition);
        }

        [Test]
        public void 子音の手を左にすると左右の役割が入れ替わる()
        {
            var rig = new Rig(new KeyboardSettings(consonantHand: Hand.Left));
            rig.Type(1, StickDirection.Up);
            Assert.AreEqual("く", rig.Session.Composition);
            Assert.AreEqual(1, rig.Effects.CountHaptics(Hand.Right, HapticKind.Input));
            rig.Modify();
            Assert.AreEqual("ぐ", rig.Session.Composition);
            Assert.AreEqual(1, rig.Effects.CountHaptics(Hand.Left, HapticKind.Input));
        }

        [Test]
        public void わと記号は機能レイヤーから入力できる()
        {
            var rig = new Rig();
            rig.ConsonantFunction(ConsonantFunctionCells.Wa, StickDirection.Left);
            rig.ConsonantFunction(ConsonantFunctionCells.Symbol, StickDirection.Up);
            Assert.AreEqual("を？", rig.Session.Composition);
        }

        [Test]
        public void 子音側のトリガーで直前のかなを巡回する()
        {
            var rig = new Rig();
            rig.Type(5, StickDirection.Center); // は
            rig.Modify();
            Assert.AreEqual("ば", rig.Session.Composition);
            rig.Modify();
            Assert.AreEqual("ぱ", rig.Session.Composition);
            rig.Modify();
            Assert.AreEqual("は", rig.Session.Composition);
        }

        [Test]
        public void 子音側のトリガーは直前の文字にだけ作用する()
        {
            var rig = new Rig();
            rig.Type(1, StickDirection.Center); // か
            rig.Type(5, StickDirection.Down);   // ほ。直前の文字だけが対象で、手前の「か」には作用しない
            rig.Modify();
            Assert.AreEqual("かぼ", rig.Session.Composition);
        }

        [Test]
        public void 子音側のトリガーは選んでいる文字を入力しない()
        {
            var rig = new Rig();
            rig.SetStick(rig.Consonant, Sticks.Cell(1));
            rig.Tick();
            rig.Modify();
            Assert.AreEqual("", rig.Session.Composition);
            Assert.AreEqual(1, rig.Effects.CountHaptics(rig.Consonant, HapticKind.Invalid));
        }

        [Test]
        public void 子音側のトリガーで英字の大文字と小文字を切り替える()
        {
            var rig = new Rig();
            ToEnglish(rig);
            rig.Type(1, StickDirection.Center); // a
            rig.Modify();
            Assert.AreEqual("A", rig.Session.Committed);
            rig.Modify();
            Assert.AreEqual("a", rig.Session.Committed);

            rig.VowelFunction(VowelFunctionCells.ToggleWidth);
            rig.Type(2, StickDirection.Center); // ｄ
            rig.Modify();
            Assert.AreEqual("aＤ", rig.Session.Committed);
        }

        [Test]
        public void 切り替えの対象がなければ振動で知らせる()
        {
            var rig = new Rig();
            rig.Modify();
            Assert.AreEqual(1, rig.Effects.CountHaptics(rig.Consonant, HapticKind.Invalid));

            rig.VowelFunction(VowelFunctionCells.Number);
            rig.Type(0, StickDirection.Center); // 1
            rig.Modify();
            Assert.AreEqual("1", rig.Session.Committed);
            Assert.AreEqual(2, rig.Effects.CountHaptics(rig.Consonant, HapticKind.Invalid));
        }

        [Test]
        public void 両方のトリガーを同時に引くと入力してから切り替える()
        {
            var rig = new Rig();
            rig.SetStick(rig.Consonant, Sticks.Cell(1));
            rig.Tick();
            rig.SetTrigger(rig.Consonant, true);
            rig.SetTrigger(rig.Vowel, true);
            rig.Tick();
            Assert.AreEqual("が", rig.Session.Composition);
            Assert.AreEqual(1, rig.Effects.CountHaptics(rig.Vowel, HapticKind.Input));
            Assert.AreEqual(1, rig.Effects.CountHaptics(rig.Consonant, HapticKind.Input));
        }

        [Test]
        public void 機能レイヤーの小濁点半濁点は子音側のトリガーと同じ切り替えをする()
        {
            var rig = new Rig();
            rig.Type(3, StickDirection.Up); // つ
            rig.ConsonantFunction(ConsonantFunctionCells.Modifier);
            Assert.AreEqual("っ", rig.Session.Composition);

            // 英字を入れてからかなモードへ戻ると、直前の英字が対象になる。
            ToEnglish(rig);
            rig.Type(1, StickDirection.Center); // a（未確定の「っ」を先に確定する）
            ToEnglish(rig);
            rig.ConsonantFunction(ConsonantFunctionCells.Modifier);
            Assert.AreEqual("っA", rig.Session.Committed);
        }

        [Test]
        public void 機能レイヤーでは左右どちらのトリガーでも実行する()
        {
            var rig = new Rig();
            rig.Type(1, StickDirection.Center); // か

            // 子音側の機能レイヤーを、子音側のトリガーで実行する（切り替えにはならない）。
            rig.SetGrip(rig.Consonant, true);
            rig.SetStick(rig.Consonant, Sticks.Cell(ConsonantFunctionCells.Space));
            rig.Tick();
            rig.Pull(rig.Consonant);
            rig.SetGrip(rig.Consonant, false);
            rig.SetStick(rig.Consonant, StickValue.Zero);
            rig.Tick();
            Assert.AreEqual("か　", rig.Session.Committed);

            // 母音側の機能レイヤーを、母音側のトリガーで実行する。
            rig.SetGrip(rig.Vowel, true);
            rig.SetStick(rig.Vowel, Sticks.Cell(VowelFunctionCells.Language));
            rig.Tick();
            rig.Pull(rig.Vowel);
            rig.SetGrip(rig.Vowel, false);
            rig.SetStick(rig.Vowel, StickValue.Zero);
            rig.Tick();
            Assert.AreEqual(GripInputMode.Alpha, rig.Session.GripView.InputMode);
        }

        [Test]
        public void 消去は未確定の文字を消す()
        {
            var rig = new Rig();
            rig.Type(0, StickDirection.Center);
            rig.Type(0, StickDirection.Left);
            rig.ConsonantFunction(ConsonantFunctionCells.Erase);
            Assert.AreEqual("あ", rig.Session.Composition);
            rig.ConsonantFunction(ConsonantFunctionCells.Erase);
            Assert.AreEqual("", rig.Session.Composition);
        }

        [Test]
        public void 消去する文字がなければ振動で知らせる()
        {
            var rig = new Rig();
            rig.ConsonantFunction(ConsonantFunctionCells.Erase);
            Assert.AreEqual(1, rig.Effects.CountHaptics(HapticKind.Invalid));
        }

        [Test]
        public void 左右キーは未確定の中でカーソルを動かす()
        {
            var rig = new Rig();
            rig.Type(0, StickDirection.Center); // あ
            rig.Type(0, StickDirection.Left);   // い
            rig.VowelFunction(VowelFunctionCells.CursorLeft);
            Assert.AreEqual(1, rig.Session.CompositionCursor);
            rig.Type(0, StickDirection.Up);     // う をカーソル位置に挿入
            Assert.AreEqual("あうい", rig.Session.Composition);
            Assert.AreEqual(2, rig.Session.CompositionCursor);
            rig.VowelFunction(VowelFunctionCells.CursorRight);
            Assert.AreEqual(3, rig.Session.CompositionCursor);
        }

        [Test]
        public void カーソルが端なら振動で知らせる()
        {
            var rig = new Rig();
            rig.Type(0, StickDirection.Center);
            rig.VowelFunction(VowelFunctionCells.CursorRight);
            Assert.AreEqual(1, rig.Effects.CountHaptics(HapticKind.Invalid));
        }

        [Test]
        public void かなモードの空白は未確定をそのまま確定して全角空白を入れる()
        {
            var rig = new Rig();
            rig.Type(0, StickDirection.Center);
            rig.ConsonantFunction(ConsonantFunctionCells.Space);
            Assert.AreEqual("あ　", rig.Session.Committed);
            Assert.AreEqual("", rig.Session.Composition);
        }

        [Test]
        public void 機能レイヤーの空のセルは何もしない()
        {
            var rig = new Rig();
            rig.ConsonantFunction(0);
            rig.ConsonantFunction(1);
            rig.ConsonantFunction(6);
            rig.ConsonantFunction(7);
            Assert.AreEqual("", rig.Session.Composition);
            Assert.AreEqual("", rig.Session.Committed);
            Assert.AreEqual(0, rig.Effects.CountHaptics(HapticKind.Input));
            Assert.AreEqual(0, rig.Effects.CountHaptics(HapticKind.Invalid));
        }

        [Test]
        public void 言語を切り替えると英字モードになり確定済みに直接入る()
        {
            var rig = new Rig();
            rig.Type(0, StickDirection.Center); // あ
            ToEnglish(rig);
            Assert.AreEqual(InputLanguage.English, rig.Session.GripInput.Language);
            Assert.AreEqual(GripInputMode.Alpha, rig.Session.GripInput.InputMode);
            rig.Type(1, StickDirection.Left);   // b
            // 英字は確定済みへ入る。未確定のかなは先に確定される。
            Assert.AreEqual("あb", rig.Session.Committed);
            Assert.AreEqual("", rig.Session.Composition);
            rig.Type(0, StickDirection.Down);
            Assert.AreEqual("あb1", rig.Session.Committed);
        }

        [Test]
        public void もう一度言語を切り替えるとかなモードに戻る()
        {
            var rig = new Rig();
            ToEnglish(rig);
            ToEnglish(rig);
            Assert.AreEqual(InputLanguage.Japanese, rig.Session.GripInput.Language);
            Assert.AreEqual(GripInputMode.Kana, rig.Session.GripInput.InputMode);
        }

        [Test]
        public void 大文字にすると英字が大文字で入る()
        {
            var rig = new Rig();
            ToEnglish(rig);
            rig.VowelFunction(VowelFunctionCells.ToggleCase);
            Assert.IsTrue(rig.Session.GripInput.IsUpperCase);
            rig.Type(1, StickDirection.Left);
            rig.Type(8, StickDirection.Down);
            Assert.AreEqual("B9", rig.Session.Committed);
            rig.VowelFunction(VowelFunctionCells.ToggleCase);
            rig.Type(1, StickDirection.Left);
            Assert.AreEqual("B9b", rig.Session.Committed);
        }

        [Test]
        public void 大文字の切り替えはかなモードでは無効()
        {
            var rig = new Rig();
            rig.VowelFunction(VowelFunctionCells.ToggleCase);
            Assert.IsFalse(rig.Session.GripInput.IsUpperCase);
            Assert.AreEqual(1, rig.Effects.CountHaptics(HapticKind.Invalid));
        }

        [Test]
        public void 全角にすると英字が全角で入る()
        {
            var rig = new Rig();
            ToEnglish(rig);
            rig.VowelFunction(VowelFunctionCells.ToggleWidth);
            Assert.IsTrue(rig.Session.GripInput.IsFullWidth);
            rig.Type(1, StickDirection.Center);
            rig.Type(0, StickDirection.Center); // .
            Assert.AreEqual("ａ．", rig.Session.Committed);
        }

        [Test]
        public void 大文字と全角は組み合わせられる()
        {
            var rig = new Rig();
            ToEnglish(rig);
            rig.VowelFunction(VowelFunctionCells.ToggleCase);
            rig.VowelFunction(VowelFunctionCells.ToggleWidth);
            rig.Type(1, StickDirection.Left);
            Assert.AreEqual("Ｂ", rig.Session.Committed);
        }

        [Test]
        public void 全角の英字モードの空白は全角空白_半角なら半角空白()
        {
            var rig = new Rig();
            ToEnglish(rig);
            rig.ConsonantFunction(ConsonantFunctionCells.Space);
            rig.VowelFunction(VowelFunctionCells.ToggleWidth);
            rig.ConsonantFunction(ConsonantFunctionCells.Space);
            Assert.AreEqual(" 　", rig.Session.Committed);
        }

        [Test]
        public void 全半角の切り替えはかなモードでは無効()
        {
            var rig = new Rig();
            rig.VowelFunction(VowelFunctionCells.ToggleWidth);
            Assert.IsFalse(rig.Session.GripInput.IsFullWidth);
            Assert.AreEqual(1, rig.Effects.CountHaptics(HapticKind.Invalid));
        }

        [Test]
        public void 大文字と全角の設定は言語を切り替えても保たれる()
        {
            var rig = new Rig();
            ToEnglish(rig);
            rig.VowelFunction(VowelFunctionCells.ToggleCase);
            ToEnglish(rig);
            ToEnglish(rig);
            Assert.IsTrue(rig.Session.GripInput.IsUpperCase);
        }

        [Test]
        public void 数字モードは確定済みへ中央の数字を入れる()
        {
            var rig = new Rig();
            rig.VowelFunction(VowelFunctionCells.Number);
            Assert.AreEqual(GripInputMode.Number, rig.Session.GripInput.InputMode);
            Assert.AreEqual(GripInputMode.Number, rig.Session.GripInput.InputMode);
            for (int cell = 0; cell < 9; cell++) rig.Type(cell, StickDirection.Center);
            Assert.AreEqual("123456789", rig.Session.Committed);
        }

        [Test]
        public void 数字モードの中央以外の方向は何も入らない()
        {
            var rig = new Rig();
            rig.VowelFunction(VowelFunctionCells.Number);
            rig.Effects.ClearAll();
            rig.Type(0, StickDirection.Left);
            rig.Type(0, StickDirection.Up);
            rig.Type(0, StickDirection.Right);
            rig.Type(0, StickDirection.Down);
            Assert.AreEqual("", rig.Session.Committed);
            Assert.AreEqual(0, rig.Effects.CountHaptics(HapticKind.Input));
            Assert.AreEqual(0, rig.Effects.CountHaptics(HapticKind.Invalid));
        }

        [Test]
        public void 数字モードの機能レイヤーから記号と0を入れる()
        {
            var rig = new Rig();
            rig.VowelFunction(VowelFunctionCells.Number);
            rig.ConsonantFunction(4, StickDirection.Center); // 0
            rig.ConsonantFunction(4, StickDirection.Left);   // (
            rig.ConsonantFunction(3, StickDirection.Up);     // *
            rig.ConsonantFunction(5, StickDirection.Right);  // ;
            Assert.AreEqual("0(*;", rig.Session.Committed);
        }

        [Test]
        public void 数字モードは全角にできる()
        {
            var rig = new Rig();
            rig.VowelFunction(VowelFunctionCells.Number);
            rig.VowelFunction(VowelFunctionCells.ToggleWidth);
            rig.Type(0, StickDirection.Center);
            Assert.AreEqual("１", rig.Session.Committed);
        }

        [Test]
        public void 数字モードでは大文字の切り替えが無効()
        {
            var rig = new Rig();
            rig.VowelFunction(VowelFunctionCells.Number);
            rig.VowelFunction(VowelFunctionCells.ToggleCase);
            Assert.IsFalse(rig.Session.GripInput.IsUpperCase);
            Assert.AreEqual(1, rig.Effects.CountHaptics(HapticKind.Invalid));
        }

        [Test]
        public void 数字をもう一度押すと元の入力モードに戻る()
        {
            var rig = new Rig();
            rig.VowelFunction(VowelFunctionCells.Number);
            rig.VowelFunction(VowelFunctionCells.Number);
            Assert.AreEqual(GripInputMode.Kana, rig.Session.GripInput.InputMode);

            ToEnglish(rig);
            rig.VowelFunction(VowelFunctionCells.Number);
            rig.VowelFunction(VowelFunctionCells.Number);
            Assert.AreEqual(GripInputMode.Alpha, rig.Session.GripInput.InputMode);
        }

        [Test]
        public void 数字モードで言語を切り替えると数字モードを解除して切り替える()
        {
            var rig = new Rig();
            rig.VowelFunction(VowelFunctionCells.Number);
            ToEnglish(rig);
            Assert.AreNotEqual(GripInputMode.Number, rig.Session.GripInput.InputMode);
            Assert.AreEqual(GripInputMode.Alpha, rig.Session.GripInput.InputMode);
        }

        [Test]
        public void 数字モードの数字は未確定のかなを先に確定する()
        {
            var rig = new Rig();
            rig.Type(0, StickDirection.Center);
            rig.VowelFunction(VowelFunctionCells.Number);
            rig.Type(0, StickDirection.Center);
            Assert.AreEqual("あ1", rig.Session.Committed);
            Assert.AreEqual("", rig.Session.Composition);
        }

        [Test]
        public void 両手のグリップを握っている間は子音側のトリガーで子音側の機能レイヤーのセルを実行する()
        {
            var rig = new Rig();
            rig.Type(0, StickDirection.Center);
            rig.Type(1, StickDirection.Center);
            rig.TickMany(50);
            rig.Grips(true, true);
            rig.SetStick(rig.Consonant, Sticks.Cell(ConsonantFunctionCells.Erase));
            rig.Tick();
            rig.Pull(rig.Consonant);
            Assert.AreEqual("あ", rig.Session.Composition);

            rig.SetStick(rig.Consonant, Sticks.Cell(ConsonantFunctionCells.Space));
            rig.Tick();
            rig.Pull(rig.Consonant);
            Assert.AreEqual("あ　", rig.Session.Committed);
            Assert.AreEqual(2, rig.Effects.CountHaptics(rig.Consonant, HapticKind.Input));
        }

        [Test]
        public void 両手のグリップを握っている間は母音側のトリガーで母音側の機能レイヤーのセルを実行する()
        {
            var rig = new Rig();
            rig.TickMany(50);
            rig.Grips(true, true);
            rig.SetStick(rig.Consonant, Sticks.Cell(ConsonantFunctionCells.Erase));
            rig.SetStick(rig.Vowel, Sticks.Cell(VowelFunctionCells.Number));
            rig.Tick();
            rig.Pull(rig.Vowel);
            Assert.AreEqual(GripInputMode.Number, rig.Session.GripInput.InputMode);
            Assert.AreEqual(1, rig.Effects.CountHaptics(rig.Vowel, HapticKind.Input));
            Assert.AreEqual(0, rig.Effects.CountHaptics(HapticKind.Invalid), "子音側の「消去」は実行しない");
        }

        [Test]
        public void 両手の機能レイヤーでは子音側の文字のセルを無効にする()
        {
            var rig = new Rig();
            rig.TickMany(50);
            rig.Grips(true, true);
            rig.SetStick(rig.Consonant, Sticks.Cell(ConsonantFunctionCells.Wa));
            rig.Tick();
            Assert.IsTrue(rig.Session.GripView.ConsonantCells[ConsonantFunctionCells.Wa].Disabled);
            Assert.IsTrue(rig.Session.GripView.ConsonantCells[ConsonantFunctionCells.Symbol].Disabled);
            Assert.IsFalse(rig.Session.GripView.ConsonantCells[ConsonantFunctionCells.Erase].Disabled);
            rig.Pull(rig.Consonant);
            Assert.AreEqual("", rig.Session.Composition);
            Assert.AreEqual(1, rig.Effects.CountHaptics(rig.Consonant, HapticKind.Invalid));

            // 母音側のグリップを離せば、母音パネルで文字を選べるため有効に戻る。
            rig.SetGrip(rig.Vowel, false);
            rig.Tick();
            Assert.IsFalse(rig.Session.GripView.ConsonantCells[ConsonantFunctionCells.Wa].Disabled);
            rig.Pull(rig.Consonant);
            Assert.AreEqual("わ", rig.Session.Composition);
        }

        [Test]
        public void 空きセルは無効として表示する()
        {
            var rig = new Rig();
            rig.SetGrip(rig.Consonant, true);
            rig.Tick();
            Assert.IsTrue(rig.Session.GripView.ConsonantCells[0].Disabled);
            Assert.IsFalse(rig.Session.GripView.ConsonantCells[ConsonantFunctionCells.Erase].Disabled);

            // 貼り付けモードを使えない間（既定）の「貼付」は空きセルになる。
            rig.SetGrip(rig.Consonant, false);
            rig.SetGrip(rig.Vowel, true);
            rig.Tick();
            Assert.AreEqual(GripCell.Empty.Label, rig.Session.GripView.VowelCells[VowelFunctionCells.PasteMode].Label);
            Assert.IsTrue(rig.Session.GripView.VowelCells[VowelFunctionCells.PasteMode].Disabled);
            Assert.IsFalse(rig.Session.GripView.VowelCells[VowelFunctionCells.Commit].Disabled);
        }

        [Test]
        public void 両手の機能レイヤーで両方のトリガーを同じフレームで引くと子音側から実行し連続入力しない()
        {
            var rig = RepeatRig();
            rig.TickMany(50);
            rig.Grips(true, true);
            rig.SetStick(rig.Consonant, Sticks.Cell(ConsonantFunctionCells.Space));
            rig.SetStick(rig.Vowel, Sticks.Cell(VowelFunctionCells.CursorLeft));
            rig.Tick();
            rig.SetTrigger(rig.Consonant, true);
            rig.SetTrigger(rig.Vowel, true);
            rig.Tick();
            // 空白を入れてからカーソルを左へ移動する（逆順なら、カーソルが先頭にあるため移動できない）。
            Assert.AreEqual("　", rig.Session.Committed);
            Assert.AreEqual(0, rig.Session.CommittedCursor);
            Assert.AreEqual(0, rig.Effects.CountHaptics(HapticKind.Invalid));

            rig.Tick(1.0);
            rig.Tick(0.2);
            Assert.AreEqual("　", rig.Session.Committed);
            Assert.AreEqual(0, rig.Session.CommittedCursor);
        }

        [Test]
        public void 両手の機能レイヤーでも引いた手のパネルのセルを連続入力する()
        {
            var rig = RepeatRig();
            rig.TickMany(50);
            rig.Grips(true, true);
            rig.SetStick(rig.Consonant, Sticks.Cell(ConsonantFunctionCells.Space));
            rig.Tick();
            rig.SetTrigger(rig.Consonant, true);
            rig.Tick();
            Assert.AreEqual("　", rig.Session.Committed);
            rig.Tick(0.52);
            Assert.AreEqual("　　", rig.Session.Committed);
            rig.Tick(0.1);
            Assert.AreEqual("　　　", rig.Session.Committed);
        }

        [Test]
        public void 開いたときに握っていたグリップは離すまで無視する()
        {
            var rig = new Rig(open: false);
            rig.Grips(true, true); rig.Tick();
            rig.Grips(false, false); rig.Tick();
            rig.Grips(true, true); rig.Tick();
            Assert.IsTrue(rig.Session.IsOpen);

            // 母音側だけ離す。子音側のグリップは握ったままだが、機能レイヤーにはならない。
            rig.SetGrip(rig.Vowel, false);
            rig.SetStick(rig.Consonant, Sticks.Cell(5));
            rig.Tick();
            Assert.IsFalse(rig.Session.GripView.ConsonantFunctionLayer);
            rig.Pull(rig.Vowel);
            Assert.AreEqual("は", rig.Session.Composition);

            // 一度離して握り直せば機能レイヤーになる。
            rig.SetGrip(rig.Consonant, false);
            rig.Tick();
            rig.ConsonantFunction(ConsonantFunctionCells.Space);
            Assert.AreEqual("は　", rig.Session.Committed);
        }

        [Test]
        public void 振動はセルの移動で選択した手に出る()
        {
            var rig = new Rig();
            rig.SetStick(rig.Consonant, Sticks.Cell(0));
            rig.Tick();
            Assert.AreEqual(1, rig.Effects.CountHaptics(rig.Consonant, HapticKind.Move));
            Assert.AreEqual(0, rig.Effects.CountHaptics(rig.Vowel, HapticKind.Move));
            rig.Tick();
            Assert.AreEqual(1, rig.Effects.CountHaptics(HapticKind.Move));

            rig.SetStick(rig.Vowel, Sticks.Dir(StickDirection.Left));
            rig.Tick();
            Assert.AreEqual(1, rig.Effects.CountHaptics(rig.Vowel, HapticKind.Move));
        }

        [Test]
        public void 入力の振動はトリガーを引いた手に出る()
        {
            var rig = new Rig();
            rig.Type(0, StickDirection.Center);
            Assert.AreEqual(1, rig.Effects.CountHaptics(rig.Vowel, HapticKind.Input));
            Assert.AreEqual(0, rig.Effects.CountHaptics(rig.Consonant, HapticKind.Input));

            // 母音側の機能レイヤーを子音側のトリガーで実行すると、子音側に出る。
            rig.Effects.ClearAll();
            rig.VowelFunction(VowelFunctionCells.CursorLeft);
            Assert.AreEqual(1, rig.Effects.CountHaptics(rig.Consonant, HapticKind.Input));
            Assert.AreEqual(0, rig.Effects.CountHaptics(rig.Vowel, HapticKind.Input));
        }

        [Test]
        public void ビューは子音パネルの文字を表示する()
        {
            var rig = new Rig();
            Assert.AreEqual(GripInputMode.Kana, rig.Session.GripView.InputMode);
            Assert.AreEqual(9, rig.Session.GripView.ConsonantCells.Count);
            Assert.AreEqual("あ", rig.Session.GripView.ConsonantCells[0].Label);
            Assert.AreEqual("や", rig.Session.GripView.ConsonantCells[7].Label);
            Assert.AreEqual(4, rig.Session.GripView.ConsonantSelected);

            rig.SetStick(rig.Consonant, Sticks.Cell(5));
            rig.Tick();
            Assert.AreEqual(5, rig.Session.GripView.ConsonantSelected);
            // 母音パネルは選択中のセルの5文字
            Assert.AreEqual(5, rig.Session.GripView.VowelCells.Count);
            Assert.AreEqual("は", rig.Session.GripView.VowelCells[0].Label);
            Assert.AreEqual("ほ", rig.Session.GripView.VowelCells[4].Label);
        }

        [Test]
        public void ビューはグリップで機能レイヤーに切り替わる()
        {
            var rig = new Rig();
            rig.SetGrip(rig.Consonant, true);
            rig.Tick();
            GripViewState view = rig.Session.GripView;
            Assert.IsTrue(view.ConsonantFunctionLayer);
            Assert.AreEqual(GripLayout.GetConsonantCell(GripInputMode.Kana, true, ConsonantFunctionCells.Modifier).Label, view.ConsonantCells[ConsonantFunctionCells.Modifier].Label);

            rig.SetGrip(rig.Consonant, false);
            rig.SetGrip(rig.Vowel, true);
            rig.Tick();
            view = rig.Session.GripView;
            Assert.IsFalse(view.ConsonantFunctionLayer);
            Assert.IsTrue(view.VowelFunctionLayer);
            Assert.AreEqual(9, view.VowelCells.Count);
        }

        [Test]
        public void ビューの数字モードの機能レイヤーと設定の表示()
        {
            var rig = new Rig();
            rig.SetGrip(rig.Vowel, true);
            rig.Tick();
            // かなモードでは全/半と大文字が無効
            Assert.IsTrue(rig.Session.GripView.VowelCells[VowelFunctionCells.ToggleWidth].Disabled);
            Assert.IsTrue(rig.Session.GripView.VowelCells[VowelFunctionCells.ToggleCase].Disabled);
            rig.SetGrip(rig.Vowel, false);
            rig.Tick();

            ToEnglish(rig);
            rig.VowelFunction(VowelFunctionCells.ToggleCase);
            rig.SetGrip(rig.Vowel, true);
            rig.Tick();
            Assert.IsFalse(rig.Session.GripView.VowelCells[VowelFunctionCells.ToggleCase].Disabled);
            Assert.IsTrue(rig.Session.GripView.VowelCells[VowelFunctionCells.ToggleCase].Lit);
            Assert.IsFalse(rig.Session.GripView.VowelCells[VowelFunctionCells.ToggleWidth].Lit);
        }

        [Test]
        public void ビューの文字は大文字と全角を反映する()
        {
            var rig = new Rig();
            ToEnglish(rig);
            rig.VowelFunction(VowelFunctionCells.ToggleCase);
            Assert.AreEqual("A", rig.Session.GripView.ConsonantCells[1].Label);
            rig.VowelFunction(VowelFunctionCells.ToggleWidth);
            Assert.AreEqual("Ａ", rig.Session.GripView.ConsonantCells[1].Label);
        }

        [Test]
        public void 基本レイヤーには機能レイヤーのキーをヒントとして示す()
        {
            var rig = new Rig();
            GripViewState view = rig.Session.GripView;
            string[] consonant = { "", "", "消", "小", "わ", "記", "", "", "空" };
            // 貼り付けモードを使えない間（既定）は、「貼付」のヒントを示さない。
            string[] vowel = { "全", "言", "数", "←", "確", "→", "↑", "変", "" };
            CollectionAssert.AreEqual(consonant, view.ConsonantHints);
            CollectionAssert.AreEqual(vowel, view.VowelHints);
        }

        [Test]
        public void 機能レイヤーを表示している側にはヒントがない()
        {
            var rig = new Rig();
            rig.SetGrip(rig.Consonant, true);
            rig.Tick();
            GripViewState view = rig.Session.GripView;
            CollectionAssert.AreEqual(NoHints, view.ConsonantHints);
            Assert.AreEqual("確", view.VowelHints[VowelFunctionCells.Commit]);

            rig.SetGrip(rig.Consonant, false);
            rig.SetGrip(rig.Vowel, true);
            rig.Tick();
            view = rig.Session.GripView;
            Assert.AreEqual("消", view.ConsonantHints[ConsonantFunctionCells.Erase]);
            CollectionAssert.AreEqual(NoHints, view.VowelHints);
        }

        [Test]
        public void ヒントは入力モードと全角を反映する()
        {
            var rig = new Rig();
            ToEnglish(rig);
            Assert.AreEqual("", rig.Session.GripView.ConsonantHints[ConsonantFunctionCells.Modifier]);
            Assert.AreEqual(",", rig.Session.GripView.ConsonantHints[ConsonantFunctionCells.Symbol]);
            rig.VowelFunction(VowelFunctionCells.ToggleWidth);
            Assert.AreEqual("，", rig.Session.GripView.ConsonantHints[ConsonantFunctionCells.Symbol]);

            rig.VowelFunction(VowelFunctionCells.Number);
            Assert.AreEqual("－", rig.Session.GripView.ConsonantHints[ConsonantFunctionCells.Modifier]);
            Assert.AreEqual("０", rig.Session.GripView.ConsonantHints[ConsonantFunctionCells.Wa]);
        }

        static string[] DirectionHintsOf(GripCellView cell)
        {
            return new[]
            {
                cell.DirectionHint(StickDirection.Left), cell.DirectionHint(StickDirection.Up),
                cell.DirectionHint(StickDirection.Right), cell.DirectionHint(StickDirection.Down),
            };
        }

        static readonly string[] NoDirectionHints = { "", "", "", "" };

        [Test]
        public void 子音パネルの文字のセルには方向の文字をヒントとして示す()
        {
            var rig = new Rig();
            GripViewState view = rig.Session.GripView;
            CollectionAssert.AreEqual(new[] { "い", "う", "え", "お" }, DirectionHintsOf(view.ConsonantCells[0]));
            CollectionAssert.AreEqual(new[] { "に", "ぬ", "ね", "の" }, DirectionHintsOf(view.ConsonantCells[4]));
            CollectionAssert.AreEqual(new[] { "「", "ゆ", "」", "よ" }, DirectionHintsOf(view.ConsonantCells[7]));
        }

        [Test]
        public void 方向のヒントは大文字と全角を反映する()
        {
            var rig = new Rig();
            ToEnglish(rig);
            CollectionAssert.AreEqual(new[] { "b", "c", "#", "2" }, DirectionHintsOf(rig.Session.GripView.ConsonantCells[1]));
            rig.VowelFunction(VowelFunctionCells.ToggleCase);
            CollectionAssert.AreEqual(new[] { "B", "C", "#", "2" }, DirectionHintsOf(rig.Session.GripView.ConsonantCells[1]));
            rig.VowelFunction(VowelFunctionCells.ToggleWidth);
            CollectionAssert.AreEqual(new[] { "Ｂ", "Ｃ", "＃", "２" }, DirectionHintsOf(rig.Session.GripView.ConsonantCells[1]));
        }

        [Test]
        public void 機能レイヤーの文字のセルにも方向のヒントを示し_動作のセルには示さない()
        {
            var rig = new Rig();
            rig.SetGrip(rig.Consonant, true);
            rig.Tick();
            GripViewState view = rig.Session.GripView;
            CollectionAssert.AreEqual(new[] { "を", "ん", "ー", "〜" }, DirectionHintsOf(view.ConsonantCells[ConsonantFunctionCells.Wa]));
            CollectionAssert.AreEqual(new[] { "。", "？", "！", "…" }, DirectionHintsOf(view.ConsonantCells[ConsonantFunctionCells.Symbol]));
            CollectionAssert.AreEqual(NoDirectionHints, DirectionHintsOf(view.ConsonantCells[ConsonantFunctionCells.Modifier]));
            CollectionAssert.AreEqual(NoDirectionHints, DirectionHintsOf(view.ConsonantCells[ConsonantFunctionCells.Erase]));
            CollectionAssert.AreEqual(NoDirectionHints, DirectionHintsOf(view.ConsonantCells[0]));
        }

        [Test]
        public void 未割り当ての方向と母音パネルには方向のヒントがない()
        {
            var rig = new Rig();
            GripViewState view = rig.Session.GripView;
            foreach (GripCellView cell in view.VowelCells)
                CollectionAssert.AreEqual(NoDirectionHints, DirectionHintsOf(cell));

            rig.VowelFunction(VowelFunctionCells.Number);
            view = rig.Session.GripView;
            CollectionAssert.AreEqual(NoDirectionHints, DirectionHintsOf(view.ConsonantCells[0]));

            rig.SetGrip(rig.Vowel, true);
            rig.Tick();
            foreach (GripCellView cell in rig.Session.GripView.VowelCells)
                CollectionAssert.AreEqual(NoDirectionHints, DirectionHintsOf(cell));
        }

        [Test]
        public void 方向のヒントは中央を受け付けない()
        {
            GripCellView cell = new Rig().Session.GripView.ConsonantCells[0];
            Assert.Throws<System.ArgumentOutOfRangeException>(() => cell.DirectionHint(StickDirection.Center));
        }

        [Test]
        public void 方向のヒントは4方向分が必要()
        {
            Assert.Throws<System.ArgumentException>(() => new GripCellView("あ", false, false, new[] { "い", "う", "え" }));
            Assert.Throws<System.ArgumentException>(() => new GripCellView("あ", false, false, new[] { "い", null, "え", "お" }));
            Assert.Throws<System.ArgumentNullException>(() => new GripCellView("あ", false, false, null));
        }

        [Test]
        public void 数字モードのビューは中央以外の方向が空になる()
        {
            var rig = new Rig();
            rig.VowelFunction(VowelFunctionCells.Number);
            Assert.AreEqual("1", rig.Session.GripView.ConsonantCells[0].Label);
            rig.SetStick(rig.Consonant, Sticks.Cell(0));
            rig.Tick();
            Assert.AreEqual("1", rig.Session.GripView.VowelCells[0].Label);
            Assert.AreEqual("", rig.Session.GripView.VowelCells[1].Label);
            Assert.IsTrue(rig.Session.GripView.VowelCells[1].Disabled);
        }

        // 連続入力（長押し）。遅延 0.5 秒・間隔 0.1 秒。時刻の境界の前後 0.02 秒で確かめ、浮動小数の誤差の影響を避ける。

        static Rig RepeatRig()
        {
            return new Rig(new KeyboardSettings(repeatDelaySeconds: 0.5, repeatIntervalSeconds: 0.1));
        }

        /// <summary>基本レイヤーで「あ」を選び、母音側のトリガーを引いたままにする（1回目が入る）。</summary>
        static void HoldA(Rig rig)
        {
            rig.SetStick(rig.Consonant, Sticks.Cell(0));
            rig.SetStick(rig.Vowel, Sticks.Dir(StickDirection.Center));
            rig.Tick();
            rig.SetTrigger(rig.Vowel, true);
            rig.Tick();
        }

        /// <summary>機能レイヤーのセルを選び、実行するトリガーを引いたままにする（1回目が実行される）。</summary>
        static void HoldFunction(Rig rig, Hand gripHand, int cell)
        {
            rig.SetGrip(gripHand, true);
            rig.SetStick(gripHand, Sticks.Cell(cell));
            rig.Tick();
            rig.SetTrigger(gripHand == rig.Consonant ? rig.Vowel : rig.Consonant, true);
            rig.Tick();
        }

        [Test]
        public void 文字はトリガーを引いたままにすると遅延の後に間隔ごとに連続入力される()
        {
            var rig = RepeatRig();
            HoldA(rig);
            Assert.AreEqual("あ", rig.Session.Composition);
            rig.Tick(0.48);
            Assert.AreEqual("あ", rig.Session.Composition, "遅延の前");
            rig.Tick(0.04); // 0.52
            Assert.AreEqual("ああ", rig.Session.Composition);
            rig.Tick(0.06); // 0.58
            Assert.AreEqual("ああ", rig.Session.Composition, "間隔の前");
            rig.Tick(0.04); // 0.62
            Assert.AreEqual("あああ", rig.Session.Composition);
            rig.Tick(0.1); // 0.72
            Assert.AreEqual("ああああ", rig.Session.Composition);
            Assert.AreEqual(4, rig.Effects.CountHaptics(rig.Vowel, HapticKind.Input));
        }

        [Test]
        public void 間隔より長いフレームが挟まっても連続入力は1フレームに1回()
        {
            var rig = RepeatRig();
            HoldA(rig);
            rig.Tick(1.0);
            Assert.AreEqual("ああ", rig.Session.Composition);
            rig.Tick(0.08);
            Assert.AreEqual("ああ", rig.Session.Composition, "次は遅れたフレームから間隔の後");
            rig.Tick(0.04);
            Assert.AreEqual("あああ", rig.Session.Composition);
        }

        [Test]
        public void トリガーを離すと連続入力が止まり_引き直すと遅延からやり直す()
        {
            var rig = RepeatRig();
            HoldA(rig);
            rig.Tick(0.52);
            Assert.AreEqual("ああ", rig.Session.Composition);
            rig.SetTrigger(rig.Vowel, false);
            rig.Tick();
            rig.Tick(1.0);
            Assert.AreEqual("ああ", rig.Session.Composition);

            rig.SetTrigger(rig.Vowel, true);
            rig.Tick();
            Assert.AreEqual("あああ", rig.Session.Composition);
            rig.Tick(0.48);
            Assert.AreEqual("あああ", rig.Session.Composition);
            rig.Tick(0.04);
            Assert.AreEqual("ああああ", rig.Session.Composition);
        }

        [Test]
        public void トリガーをしきい値未満まで戻すと連続入力が止まる()
        {
            var rig = RepeatRig();
            HoldA(rig);
            rig.SetTriggerPull(rig.Vowel, 0.6f); // 既定のしきい値 0.7 未満
            rig.Tick();
            rig.Tick(1.0);
            Assert.AreEqual("あ", rig.Session.Composition);
        }

        [Test]
        public void 選択を変えると連続入力が止まり_新しい選択は引き直すまで入らない()
        {
            var rig = RepeatRig();
            HoldA(rig);
            rig.SetStick(rig.Vowel, Sticks.Dir(StickDirection.Left)); // い
            rig.Tick();
            rig.Tick(1.0);
            Assert.AreEqual("あ", rig.Session.Composition);

            rig.SetStick(rig.Vowel, Sticks.Dir(StickDirection.Center));
            rig.SetStick(rig.Consonant, Sticks.Cell(1)); // か
            rig.Tick();
            rig.Tick(1.0);
            Assert.AreEqual("あ", rig.Session.Composition);
        }

        [Test]
        public void グリップを押すと連続入力が止まる()
        {
            var rig = RepeatRig();
            HoldA(rig);
            rig.SetGrip(rig.Consonant, true);
            rig.Tick();
            rig.Tick(1.0);
            rig.SetGrip(rig.Consonant, false);
            rig.Tick();
            rig.Tick(1.0);
            Assert.AreEqual("あ", rig.Session.Composition);
        }

        [Test]
        public void 子音側のトリガーを引くと連続入力が止まる()
        {
            var rig = RepeatRig();
            rig.SetStick(rig.Consonant, Sticks.Cell(1)); // か
            rig.Tick();
            rig.SetTrigger(rig.Vowel, true);
            rig.Tick();
            rig.Tick(0.52);
            Assert.AreEqual("かか", rig.Session.Composition);

            rig.SetTrigger(rig.Consonant, true);
            rig.Tick();
            Assert.AreEqual("かが", rig.Session.Composition);
            rig.Tick(1.0);
            Assert.AreEqual("かが", rig.Session.Composition);
        }

        [Test]
        public void 同じフレームで子音側のトリガーで切り替えた文字は連続入力しない()
        {
            var rig = RepeatRig();
            rig.SetStick(rig.Consonant, Sticks.Cell(1)); // か
            rig.Tick();
            rig.SetTrigger(rig.Vowel, true);
            rig.SetTrigger(rig.Consonant, true);
            rig.Tick();
            Assert.AreEqual("が", rig.Session.Composition);
            rig.Tick(1.0);
            Assert.AreEqual("が", rig.Session.Composition);
        }

        [Test]
        public void 消去は連続入力され_消すものがなくなると1回だけ無効の振動で知らせて止まる()
        {
            var rig = RepeatRig();
            rig.Type(0, StickDirection.Center);
            rig.Type(0, StickDirection.Left);
            rig.Effects.ClearAll();

            HoldFunction(rig, rig.Consonant, ConsonantFunctionCells.Erase);
            Assert.AreEqual("あ", rig.Session.Composition);
            rig.Tick(0.52);
            Assert.AreEqual("", rig.Session.Composition);
            rig.Tick(0.1);
            rig.Tick(1.0);
            Assert.AreEqual(2, rig.Effects.CountHaptics(HapticKind.Input));
            Assert.AreEqual(1, rig.Effects.CountHaptics(HapticKind.Invalid));
        }

        [Test]
        public void 空白は連続入力される()
        {
            var rig = RepeatRig();
            HoldFunction(rig, rig.Consonant, ConsonantFunctionCells.Space);
            rig.Tick(0.52);
            Assert.AreEqual("　　", rig.Session.Committed);
        }

        [Test]
        public void 機能レイヤーの文字は連続入力される()
        {
            var rig = RepeatRig();
            HoldFunction(rig, rig.Consonant, ConsonantFunctionCells.Wa);
            rig.Tick(0.52);
            Assert.AreEqual("わわ", rig.Session.Composition);
        }

        [Test]
        public void カーソル移動は連続入力される()
        {
            var rig = RepeatRig();
            rig.Type(0, StickDirection.Center);
            rig.Type(0, StickDirection.Left);
            rig.Type(0, StickDirection.Up);

            HoldFunction(rig, rig.Vowel, VowelFunctionCells.CursorLeft);
            Assert.AreEqual(2, rig.Session.CompositionCursor);
            rig.Tick(0.52);
            Assert.AreEqual(1, rig.Session.CompositionCursor);
        }

        [Test]
        public void 文字の切り替えと入力モードの操作は連続入力しない()
        {
            var rig = RepeatRig();
            rig.Type(1, StickDirection.Center); // か
            HoldFunction(rig, rig.Consonant, ConsonantFunctionCells.Modifier);
            rig.Tick(1.0);
            Assert.AreEqual("が", rig.Session.Composition);
            rig.SetTrigger(rig.Vowel, false);
            rig.SetGrip(rig.Consonant, false);
            rig.SetStick(rig.Consonant, StickValue.Zero);
            rig.Tick();

            HoldFunction(rig, rig.Vowel, VowelFunctionCells.Number);
            rig.Tick(1.0);
            Assert.AreEqual(GripInputMode.Number, rig.Session.GripInput.InputMode);
        }
    }
}
