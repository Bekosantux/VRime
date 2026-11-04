using System;
using NUnit.Framework;
using VRKeyboard.Core.Conversion;
using VRKeyboard.Core.Grip;
using VRKeyboard.Core.Input;

namespace VRKeyboard.Core.Tests
{
    /// <summary>開閉・ダッシュボード・入力バッファの制限・確定キー。</summary>
    public class SessionTests
    {
        const int Commit = 8;
        const int Language = 6;

        // 左右のグリップを gap 秒の差で押して離す（左が先）。
        static void Chord(Rig rig, double gap, double lead = 0.0625)
        {
            rig.Grips(true, false); rig.Tick(lead);
            rig.Grips(true, true); rig.Tick(gap);
            rig.Grips(false, false); rig.Tick(lead);
        }

        [Test]
        public void 最初は閉じている()
        {
            var rig = new Rig(open: false);
            Assert.IsFalse(rig.Session.IsOpen);
            Assert.AreEqual(KeyboardMode.Input, rig.Session.Mode);
        }

        [Test]
        public void グリップの同時押しを2回で開く()
        {
            var rig = new Rig(open: false);
            rig.ChordTwice();
            Assert.IsTrue(rig.Session.IsOpen);
            CollectionAssert.AreEqual(new[] { true }, rig.Effects.OpenChanges);
            Assert.AreEqual(1, rig.Effects.CountHaptics(Hand.Left, HapticKind.OpenClose));
            Assert.AreEqual(1, rig.Effects.CountHaptics(Hand.Right, HapticKind.OpenClose));
        }

        [Test]
        public void 同時押しを1回では開かない()
        {
            var rig = new Rig(open: false);
            rig.Grips(true, true); rig.Tick();
            rig.Grips(false, false); rig.Tick();
            Assert.IsFalse(rig.Session.IsOpen);
        }

        [Test]
        public void 開いている間の2回の同時押しで閉じる()
        {
            var rig = new Rig();
            rig.TickMany(50);
            rig.ChordTwice();
            Assert.IsFalse(rig.Session.IsOpen);
            CollectionAssert.AreEqual(new[] { false }, rig.Effects.OpenChanges);
            Assert.AreEqual(2, rig.Effects.CountHaptics(HapticKind.OpenClose));
        }

        [Test]
        public void 片手だけのグリップでは開閉しない()
        {
            var rig = new Rig(open: false);
            for (int i = 0; i < 3; i++)
            {
                rig.Grips(true, false); rig.Tick();
                rig.Grips(false, false); rig.Tick();
                rig.Grips(false, true); rig.Tick();
                rig.Grips(false, false); rig.Tick();
            }
            Assert.IsFalse(rig.Session.IsOpen);
        }

        [Test]
        public void 同時押しの時間差は窓の境界で判定する()
        {
            // 2進数で正確に表せる値にして、境界を丸め誤差なしに確かめる。
            var settings = new KeyboardSettings(chordWindowSeconds: 0.25, doubleChordWindowSeconds: 0.5);

            var within = new Rig(settings, open: false);
            Chord(within, 0.25);
            Chord(within, 0.25);
            Assert.IsTrue(within.Session.IsOpen, "時間差がちょうど窓なら同時押し");

            var beyond = new Rig(settings, open: false);
            Chord(beyond, 0.25);
            Chord(beyond, 0.3125);
            Assert.IsFalse(beyond.Session.IsOpen, "時間差が窓を超えたら同時押しではない");
        }

        [Test]
        public void 同時押しの間隔は窓の境界で判定する()
        {
            var settings = new KeyboardSettings(chordWindowSeconds: 0.25, doubleChordWindowSeconds: 0.5);

            // 1回目の同時押しの成立から 2回目の成立までが 0.25 + 待ち時間 になる。
            var within = new Rig(settings, open: false);
            Chord(within, 0.125);
            within.Tick(0.25);
            Chord(within, 0.125);
            Assert.IsTrue(within.Session.IsOpen, "間隔がちょうど窓なら開く");

            var beyond = new Rig(settings, open: false);
            Chord(beyond, 0.125);
            beyond.Tick(0.3125);
            Chord(beyond, 0.125);
            Assert.IsFalse(beyond.Session.IsOpen, "間隔が窓を超えたら開かない");
        }

        [Test]
        public void 既定の窓は0_20秒と0_60秒()
        {
            var within = new Rig(open: false);
            Chord(within, 0.19, 0.01);
            Chord(within, 0.19, 0.01);
            Assert.IsTrue(within.Session.IsOpen);

            var beyondChord = new Rig(open: false);
            Chord(beyondChord, 0.19, 0.01);
            Chord(beyondChord, 0.21, 0.01);
            Assert.IsFalse(beyondChord.Session.IsOpen);

            // 1回目の成立から 2回目の成立までが 0.59 秒（開く）/ 0.61 秒（開かない）になるよう、待ち時間で調整する。
            var nearDouble = new Rig(open: false);
            Chord(nearDouble, 0.1, 0.01);
            nearDouble.Tick(0.47); // 0.01 + 0.47 + 0.01 + 0.10 = 0.59
            Chord(nearDouble, 0.1, 0.01);
            Assert.IsTrue(nearDouble.Session.IsOpen);

            var farDouble = new Rig(open: false);
            Chord(farDouble, 0.1, 0.01);
            farDouble.Tick(0.49); // 0.01 + 0.49 + 0.01 + 0.10 = 0.61
            Chord(farDouble, 0.1, 0.01);
            Assert.IsFalse(farDouble.Session.IsOpen);
        }

        [Test]
        public void 閉じている間はトリガーを受け付けない()
        {
            var rig = new Rig(open: false);
            rig.Type(0, StickDirection.Center);
            Assert.AreEqual("", rig.Session.Composition);
            Assert.AreEqual(0, rig.Effects.Haptics.Count);
        }

        [Test]
        public void 閉じても入力バッファは保たれ_開き直すと続きから入力できる()
        {
            var rig = new Rig();
            rig.Type(0, StickDirection.Center);
            rig.TickMany(50);
            rig.ChordTwice(); // 閉じる
            Assert.IsFalse(rig.Session.IsOpen);
            Assert.AreEqual("あ", rig.Session.Composition);
            rig.TickMany(50);
            rig.ChordTwice(); // 開く
            Assert.IsTrue(rig.Session.IsOpen);
            rig.Type(0, StickDirection.Left);
            Assert.AreEqual("あい", rig.Session.Composition);
        }

        [Test]
        public void ダッシュボードを開くと閉じ_閉じると元の状態に戻る()
        {
            var rig = new Rig();
            rig.Type(0, StickDirection.Center);
            rig.Effects.ClearAll();

            rig.Session.SetDashboardVisible(true);
            Assert.IsFalse(rig.Session.IsOpen);
            Assert.IsTrue(rig.Session.IsDashboardVisible);
            CollectionAssert.AreEqual(new[] { false }, rig.Effects.OpenChanges);

            rig.Session.SetDashboardVisible(false);
            Assert.IsTrue(rig.Session.IsOpen);
            CollectionAssert.AreEqual(new[] { false, true }, rig.Effects.OpenChanges);
            Assert.AreEqual("あ", rig.Session.Composition);
        }

        [Test]
        public void ダッシュボードの表示中は開閉の操作を受け付けない()
        {
            var rig = new Rig();
            rig.Session.SetDashboardVisible(true);
            rig.TickMany(50);
            rig.ChordTwice();
            Assert.IsFalse(rig.Session.IsOpen);
            rig.Type(0, StickDirection.Center);
            Assert.AreEqual("", rig.Session.Composition);
        }

        [Test]
        public void 手動で閉じていたらダッシュボードを閉じても再開しない()
        {
            var rig = new Rig();
            rig.TickMany(50);
            rig.ChordTwice();
            Assert.IsFalse(rig.Session.IsOpen);
            rig.Effects.ClearAll();

            rig.Session.SetDashboardVisible(true);
            rig.Session.SetDashboardVisible(false);
            Assert.IsFalse(rig.Session.IsOpen);
            Assert.AreEqual(0, rig.Effects.OpenChanges.Count);
        }

        [Test]
        public void ダッシュボードを閉じて再開したとき握っていたグリップは無視する()
        {
            var rig = new Rig();
            rig.TickMany(5);
            rig.Session.SetDashboardVisible(true);
            rig.SetGrip(rig.Consonant, true);
            rig.Tick();
            rig.Session.SetDashboardVisible(false);
            rig.SetStick(rig.Consonant, Sticks.Cell(5));
            rig.Tick();
            Assert.IsFalse(rig.Session.GripView.ConsonantFunctionPage);
            rig.SetGrip(rig.Consonant, false);
            rig.Tick();
            rig.SetGrip(rig.Consonant, true);
            rig.Tick();
            Assert.IsTrue(rig.Session.GripView.ConsonantFunctionPage);
        }

        [Test]
        public void 再開した直後に押されていたトリガーは入力にならない()
        {
            var rig = new Rig();
            rig.Session.SetDashboardVisible(true);
            rig.SetStick(rig.Consonant, Sticks.Cell(0));
            rig.SetTrigger(rig.Vowel, true);
            rig.Tick();
            rig.Session.SetDashboardVisible(false);
            rig.Tick();
            rig.Tick();
            Assert.AreEqual("", rig.Session.Composition);
        }

        [Test]
        public void ダッシュボードの状態が同じなら何もしない()
        {
            var rig = new Rig();
            rig.Effects.ClearAll();
            rig.Session.SetDashboardVisible(false);
            Assert.AreEqual(0, rig.Effects.OpenChanges.Count);
            Assert.IsTrue(rig.Session.IsOpen);

            var closed = new Rig(open: false);
            closed.Session.SetDashboardVisible(true);
            closed.Session.SetDashboardVisible(false);
            Assert.IsFalse(closed.Session.IsOpen);
            Assert.AreEqual(0, closed.Effects.OpenChanges.Count);
        }

        [Test]
        public void 既定では文字数の上限がない()
        {
            var rig = new Rig();
            Assert.IsNull(rig.Session.LengthLimit);
            for (int i = 0; i < 200; i++) rig.Type(0, StickDirection.Center);
            Assert.AreEqual(200, rig.Session.Length);
            Assert.IsFalse(rig.Session.IsOverLengthLimit);
            Assert.AreEqual(0, rig.Effects.CountHaptics(HapticKind.Invalid));
        }

        [Test]
        public void 上限を超える入力は拒否して振動で知らせる()
        {
            var rig = new Rig();
            rig.Session.SetLengthLimit(3);
            rig.Type(0, StickDirection.Center);
            rig.Type(0, StickDirection.Left);
            rig.Type(0, StickDirection.Up);
            Assert.AreEqual(3, rig.Session.Length);
            Assert.AreEqual(0, rig.Effects.CountHaptics(HapticKind.Invalid));

            rig.Type(0, StickDirection.Right);
            Assert.AreEqual("あいう", rig.Session.Composition);
            Assert.AreEqual(1, rig.Effects.CountHaptics(HapticKind.Invalid));
        }

        [Test]
        public void 上限は確定済みと未確定の合計に適用する()
        {
            var rig = new Rig();
            rig.Session.SetLengthLimit(3);
            rig.Type(0, StickDirection.Center);       // 未確定 あ
            rig.VowelFunction(Language);              // 英字へ
            rig.Type(1, StickDirection.Center);       // a（先に あ が確定される）
            rig.Type(1, StickDirection.Center);       // a
            Assert.AreEqual("あaa", rig.Session.Committed);
            Assert.AreEqual(3, rig.Session.Length);
            rig.Effects.ClearAll();

            rig.Type(1, StickDirection.Center);
            Assert.AreEqual("あaa", rig.Session.Committed);
            Assert.AreEqual(1, rig.Effects.CountHaptics(HapticKind.Invalid));
        }

        [Test]
        public void 上限に達した後でも消去はできる()
        {
            var rig = new Rig();
            rig.Session.SetLengthLimit(2);
            rig.Type(0, StickDirection.Center);
            rig.Type(0, StickDirection.Left);
            rig.ConsonantFunction(2);
            Assert.AreEqual(1, rig.Session.Length);
            rig.Type(0, StickDirection.Up);
            Assert.AreEqual("あう", rig.Session.Composition);
        }

        [Test]
        public void 上限を下げても入力済みの内容は切り詰めない()
        {
            var rig = new Rig();
            rig.Type(0, StickDirection.Center);
            rig.Type(0, StickDirection.Left);
            rig.Type(0, StickDirection.Up);
            rig.Session.SetLengthLimit(2);
            Assert.AreEqual("あいう", rig.Session.Composition);
            Assert.IsTrue(rig.Session.IsOverLengthLimit);

            rig.Session.SetLengthLimit(null);
            Assert.IsFalse(rig.Session.IsOverLengthLimit);
        }

        [Test]
        public void 上限を超えている間は送信せず振動で知らせる()
        {
            var rig = new Rig();
            rig.Type(0, StickDirection.Center);
            rig.Type(0, StickDirection.Left);
            rig.VowelFunction(Commit);
            rig.Session.SetLengthLimit(1);
            rig.Effects.ClearAll();

            rig.VowelFunction(Commit);
            Assert.AreEqual(0, rig.Effects.Sends.Count);
            Assert.AreEqual("あい", rig.Session.Committed);
            Assert.AreEqual(1, rig.Effects.CountHaptics(HapticKind.Invalid));
            Assert.AreEqual(0, rig.Effects.CountHaptics(HapticKind.Input));

            // 上限以内に戻せば送信できる。
            rig.ConsonantFunction(2);
            rig.VowelFunction(Commit);
            CollectionAssert.AreEqual(new[] { "あ" }, rig.Effects.Sends);
        }

        [Test]
        public void 上限を超えていても未確定のかなは確定できる()
        {
            var rig = new Rig();
            rig.Type(0, StickDirection.Center);
            rig.Type(0, StickDirection.Left);
            rig.Session.SetLengthLimit(1);
            rig.Effects.ClearAll();

            rig.VowelFunction(Commit);
            Assert.AreEqual("あい", rig.Session.Committed);
            Assert.AreEqual(0, rig.Effects.CountHaptics(HapticKind.Invalid));
        }

        [Test]
        public void 上限を変えるとViewVersionが増え_同じ値では増えない()
        {
            var rig = new Rig();
            int version = rig.Session.ViewVersion;
            rig.Session.SetLengthLimit(144);
            Assert.Greater(rig.Session.ViewVersion, version);
            version = rig.Session.ViewVersion;
            rig.Session.SetLengthLimit(144);
            Assert.AreEqual(version, rig.Session.ViewVersion);
            rig.Session.SetLengthLimit(null);
            Assert.Greater(rig.Session.ViewVersion, version);
        }

        [Test]
        public void 上限の変更では通知を消さない()
        {
            var rig = new Rig();
            rig.Session.SetNotice("通知");
            rig.Session.SetLengthLimit(144);
            rig.Tick();
            Assert.AreEqual("通知", rig.Session.Notice);
        }

        [Test]
        public void 不正な上限は例外()
        {
            var rig = new Rig();
            Assert.Throws<ArgumentOutOfRangeException>(() => rig.Session.SetLengthLimit(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => rig.Session.SetLengthLimit(-1));
        }

        [Test]
        public void 確定キーは未確定があればそのまま確定する()
        {
            var rig = new Rig();
            rig.Type(0, StickDirection.Center);
            rig.VowelFunction(Commit);
            Assert.AreEqual("あ", rig.Session.Committed);
            Assert.AreEqual("", rig.Session.Composition);
            Assert.AreEqual(0, rig.Effects.Sends.Count);
        }

        [Test]
        public void 確定キーは未確定がなければ確定済みを送信して空にする()
        {
            var rig = new Rig();
            rig.Type(0, StickDirection.Center);
            rig.VowelFunction(Commit);
            rig.VowelFunction(Commit);
            CollectionAssert.AreEqual(new[] { "あ" }, rig.Effects.Sends);
            Assert.AreEqual("", rig.Session.Committed);
            Assert.AreEqual(0, rig.Session.CommittedCursor);
            Assert.AreEqual(0, rig.Session.Length);
        }

        [Test]
        public void 送信に失敗したらバッファを残して振動で知らせる()
        {
            var rig = new Rig();
            rig.Type(0, StickDirection.Center);
            rig.VowelFunction(Commit);
            rig.Effects.SendSucceeds = false;
            rig.Effects.Haptics.Clear();
            rig.VowelFunction(Commit);
            CollectionAssert.AreEqual(new[] { "あ" }, rig.Effects.Sends);
            Assert.AreEqual("あ", rig.Session.Committed);
            Assert.AreEqual(1, rig.Session.CommittedCursor);
            Assert.AreEqual(1, rig.Effects.CountHaptics(HapticKind.Invalid));
            Assert.AreEqual(0, rig.Effects.CountHaptics(HapticKind.Input));
        }

        [Test]
        public void 送信に失敗した後に再送信できる()
        {
            var rig = new Rig();
            rig.Type(0, StickDirection.Center);
            rig.VowelFunction(Commit);
            rig.Effects.SendSucceeds = false;
            rig.VowelFunction(Commit);
            rig.Effects.SendSucceeds = true;
            rig.VowelFunction(Commit);
            CollectionAssert.AreEqual(new[] { "あ", "あ" }, rig.Effects.Sends);
            Assert.AreEqual("", rig.Session.Committed);
        }

        [Test]
        public void 確定キーはバッファが空なら何もせず振動で知らせる()
        {
            var rig = new Rig();
            rig.VowelFunction(Commit);
            Assert.AreEqual(0, rig.Effects.Sends.Count);
            Assert.AreEqual(1, rig.Effects.CountHaptics(HapticKind.Invalid));
        }

        [Test]
        public void 送信の後に続けて入力できる()
        {
            var rig = new Rig();
            rig.Type(0, StickDirection.Center);
            rig.VowelFunction(Commit);
            rig.VowelFunction(Commit);
            rig.Type(0, StickDirection.Left);
            rig.VowelFunction(Commit);
            rig.VowelFunction(Commit);
            CollectionAssert.AreEqual(new[] { "あ", "い" }, rig.Effects.Sends);
        }

        [Test]
        public void 確定済みの英字も確定キーで送信できる()
        {
            var rig = new Rig();
            rig.VowelFunction(Language);
            rig.Type(1, StickDirection.Center);
            rig.VowelFunction(Commit);
            CollectionAssert.AreEqual(new[] { "a" }, rig.Effects.Sends);
        }

        [Test]
        public void ViewVersionは表示が変わるときだけ増える()
        {
            var rig = new Rig();
            int version = rig.Session.ViewVersion;
            rig.TickMany(5);
            Assert.AreEqual(version, rig.Session.ViewVersion);

            rig.Type(0, StickDirection.Center);
            Assert.Greater(rig.Session.ViewVersion, version);

            version = rig.Session.ViewVersion;
            rig.TickMany(5);
            Assert.AreEqual(version, rig.Session.ViewVersion);

            rig.Session.SetNotice("通知");
            Assert.Greater(rig.Session.ViewVersion, version);
            Assert.AreEqual("通知", rig.Session.Notice);
        }

        [Test]
        public void 通知は入力バッファの内容が変わると消える()
        {
            var rig = new Rig();
            rig.Session.SetNotice("通知");
            rig.TickMany(5);
            Assert.AreEqual("通知", rig.Session.Notice);

            int version = rig.Session.ViewVersion;
            rig.Type(0, StickDirection.Center);
            Assert.AreEqual(string.Empty, rig.Session.Notice);
            Assert.Greater(rig.Session.ViewVersion, version);
        }

        [Test]
        public void 通知は操作が無効で入力バッファが変わらないときは消えない()
        {
            var rig = new Rig();
            rig.Session.SetNotice("通知");
            rig.VowelFunction(Commit);
            Assert.AreEqual(1, rig.Effects.CountHaptics(HapticKind.Invalid));
            Assert.AreEqual("通知", rig.Session.Notice);
        }

        [Test]
        public void 通知はキーボードを開くと消える()
        {
            var rig = new Rig();
            rig.ChordTwice();
            Assert.IsFalse(rig.Session.IsOpen);
            rig.Session.SetNotice("通知");
            rig.ChordTwice();
            Assert.IsTrue(rig.Session.IsOpen);
            Assert.AreEqual(string.Empty, rig.Session.Notice);
        }

        [Test]
        public void 通知は閉じても残りダッシュボードからの再開で消える()
        {
            var rig = new Rig();
            rig.Session.SetNotice("通知");
            rig.Session.SetDashboardVisible(true);
            Assert.AreEqual("通知", rig.Session.Notice);
            rig.Session.SetDashboardVisible(false);
            Assert.IsTrue(rig.Session.IsOpen);
            Assert.AreEqual(string.Empty, rig.Session.Notice);
        }

        [Test]
        public void 送信の成功で入力バッファが空になると前の失敗の通知は消える()
        {
            var rig = new Rig();
            rig.Effects.SendSucceeds = false;
            rig.Type(0, StickDirection.Center);
            rig.VowelFunction(Commit);
            rig.Session.SetNotice("送信できません");
            rig.VowelFunction(Commit);
            Assert.AreEqual("送信できません", rig.Session.Notice);

            rig.Effects.SendSucceeds = true;
            rig.VowelFunction(Commit);
            Assert.AreEqual(2, rig.Effects.Sends.Count);
            Assert.AreEqual(string.Empty, rig.Session.Committed);
            Assert.AreEqual(string.Empty, rig.Session.Notice);
        }

        [Test]
        public void 送信の失敗の通知は入力バッファが変わらないので残る()
        {
            var rig = new Rig();
            rig.Effects.SendSucceeds = false;
            rig.Type(0, StickDirection.Center);
            rig.VowelFunction(Commit);
            rig.Session.SetNotice("送信できません");
            rig.VowelFunction(Commit);
            Assert.AreEqual("送信できません", rig.Session.Notice);
            Assert.AreEqual("あ", rig.Session.Committed);
        }

        [Test]
        public void 引数が不正なら例外()
        {
            var rig = new Rig();
            Assert.Throws<ArgumentNullException>(() => rig.Session.SetNotice(null));
            Assert.Throws<ArgumentNullException>(() => new KeyboardSession(null, rig.Effects));
            Assert.Throws<ArgumentNullException>(() => new KeyboardSession(rig.Settings, null));
        }
    }

    /// <summary>変換の流れ（SPEC 6.3）。</summary>
    public class ConversionFlowTests
    {
        const int Convert = 7;
        const int Language = 6;
        const int Commit = 8;

        static ConversionSegment Segment(string reading, params string[] candidates)
        {
            return new ConversionSegment(reading, candidates);
        }

        static ConversionSegment[] Sample()
        {
            return new[]
            {
                Segment("あい", "愛", "相", "あい"),
                Segment("う", "雨", "鵜"),
            };
        }

        // 「あい」を入力して変換を押した状態にする。変換の要求 id を返す。
        static int StartConversion(Rig rig)
        {
            rig.Type(0, StickDirection.Center);
            rig.Type(0, StickDirection.Left);
            rig.VowelFunction(Convert);
            Assert.AreEqual(KeyboardMode.WaitingConversion, rig.Session.Mode);
            return rig.Effects.ConversionRequests[rig.Effects.ConversionRequests.Count - 1].Key;
        }

        static void Push(Rig rig, Hand hand, StickDirection direction)
        {
            rig.SetStick(hand, Sticks.Dir(direction));
            rig.Tick();
            rig.SetStick(hand, StickValue.Zero);
            rig.Tick();
        }

        [Test]
        public void 変換を押すと読みを渡して応答待ちになる()
        {
            var rig = new Rig();
            int id = StartConversion(rig);
            Assert.AreEqual(1, rig.Effects.ConversionRequests.Count);
            Assert.AreEqual("あい", rig.Effects.ConversionRequests[0].Value);
            Assert.AreEqual(id, rig.Effects.ConversionRequests[0].Key);
            Assert.IsNull(rig.Session.CurrentConversion);
            Assert.AreEqual("あい", rig.Session.Composition);
        }

        [Test]
        public void 応答を渡すと変換中になり初期選択は第1候補()
        {
            var rig = new Rig();
            int id = StartConversion(rig);
            rig.Session.CompleteConversion(id, Sample());
            Assert.AreEqual(KeyboardMode.Converting, rig.Session.Mode);
            Assert.AreEqual("愛雨", rig.Session.CurrentConversion.Result);
            Assert.IsNull(rig.Session.ConversionFailureReason);
        }

        [Test]
        public void スティックで文節と候補を選びトリガーで確定する()
        {
            var rig = new Rig();
            int id = StartConversion(rig);
            rig.Session.CompleteConversion(id, Sample());

            Push(rig, Hand.Right, StickDirection.Down);  // 愛 → 相
            Push(rig, Hand.Left, StickDirection.Down);   // 相 → あい（どちらの手でもよい）
            Push(rig, Hand.Right, StickDirection.Up);    // あい → 相
            Push(rig, Hand.Left, StickDirection.Right);  // 次の文節
            Push(rig, Hand.Right, StickDirection.Down);  // 雨 → 鵜
            Assert.AreEqual("相鵜", rig.Session.CurrentConversion.Result);

            rig.Pull(Hand.Left);
            Assert.AreEqual(KeyboardMode.Input, rig.Session.Mode);
            Assert.IsNull(rig.Session.CurrentConversion);
            Assert.AreEqual("相鵜", rig.Session.Committed);
            Assert.AreEqual("", rig.Session.Composition);
            Assert.AreEqual(2, rig.Session.CommittedCursor);
        }

        [Test]
        public void 文節の移動で前の文節へ戻れる()
        {
            var rig = new Rig();
            int id = StartConversion(rig);
            rig.Session.CompleteConversion(id, Sample());
            Push(rig, Hand.Right, StickDirection.Right);
            Push(rig, Hand.Right, StickDirection.Left);
            Assert.AreEqual(0, rig.Session.CurrentConversion.SelectedSegment);
        }

        [Test]
        public void 候補と文節の移動は端で止まり振動も出ない()
        {
            var rig = new Rig();
            int id = StartConversion(rig);
            rig.Session.CompleteConversion(id, Sample());
            rig.Effects.ClearAll();

            Push(rig, Hand.Right, StickDirection.Up);
            Push(rig, Hand.Right, StickDirection.Left);
            Assert.AreEqual(0, rig.Effects.CountHaptics(HapticKind.Move));
            Assert.AreEqual("愛雨", rig.Session.CurrentConversion.Result);

            Push(rig, Hand.Right, StickDirection.Down);
            Push(rig, Hand.Right, StickDirection.Down);
            Push(rig, Hand.Right, StickDirection.Down); // 端
            Assert.AreEqual(2, rig.Effects.CountHaptics(HapticKind.Move));
            Assert.AreEqual(2, rig.Session.CurrentConversion.SelectedCandidate(0));
        }

        [Test]
        public void スティックは倒したままでは動き続けない()
        {
            var rig = new Rig();
            int id = StartConversion(rig);
            rig.Session.CompleteConversion(id, Sample());
            rig.SetStick(Hand.Right, Sticks.Dir(StickDirection.Down));
            rig.TickMany(20);
            Assert.AreEqual(1, rig.Session.CurrentConversion.SelectedCandidate(0));
        }

        [Test]
        public void 変換を押したとき倒していたスティックは倒した瞬間として扱わない()
        {
            var rig = new Rig();
            rig.Type(0, StickDirection.Center);
            rig.SetGrip(rig.Vowel, true);
            rig.SetStick(rig.Vowel, Sticks.Cell(Convert)); // 下に倒した状態でトリガーを引く
            rig.SetStick(rig.Consonant, Sticks.Dir(StickDirection.Down));
            rig.Tick();
            rig.Pull(rig.Consonant);
            int id = rig.Effects.ConversionRequests[0].Key;
            rig.Session.CompleteConversion(id, Sample());
            rig.TickMany(5);
            Assert.AreEqual(0, rig.Session.CurrentConversion.SelectedCandidate(0));
            Assert.AreEqual(0, rig.Session.CurrentConversion.SelectedSegment);
        }

        [Test]
        public void 変換を押したとき握っていたグリップは離すまで無視する()
        {
            var rig = new Rig();
            rig.Type(0, StickDirection.Center);
            rig.SetGrip(rig.Vowel, true);
            rig.SetStick(rig.Vowel, Sticks.Cell(Convert));
            rig.Tick();
            rig.Pull(rig.Consonant);
            Assert.AreEqual(KeyboardMode.WaitingConversion, rig.Session.Mode);

            rig.TickMany(5);
            Assert.AreEqual(KeyboardMode.WaitingConversion, rig.Session.Mode, "握ったままでは取り消されない");

            rig.SetGrip(rig.Vowel, false);
            rig.Tick();
            Assert.AreEqual(KeyboardMode.WaitingConversion, rig.Session.Mode, "離しただけでは取り消されない");

            rig.SetGrip(rig.Vowel, true);
            rig.Tick();
            Assert.AreEqual(KeyboardMode.Input, rig.Session.Mode, "離してから押し直すと取り消し");
        }

        [Test]
        public void グリップで変換を取り消すと未確定に戻り機能ページは開かない()
        {
            var rig = new Rig();
            int id = StartConversion(rig);
            rig.Session.CompleteConversion(id, Sample());
            Push(rig, Hand.Right, StickDirection.Down);

            rig.SetGrip(rig.Consonant, true);
            rig.Tick();
            Assert.AreEqual(KeyboardMode.Input, rig.Session.Mode);
            Assert.IsNull(rig.Session.CurrentConversion);
            Assert.AreEqual("あい", rig.Session.Composition);
            Assert.AreEqual("", rig.Session.Committed);
            Assert.IsFalse(rig.Session.GripView.ConsonantFunctionPage);

            rig.TickMany(3);
            Assert.IsFalse(rig.Session.GripView.ConsonantFunctionPage, "握ったままでは機能ページにならない");

            rig.SetGrip(rig.Consonant, false);
            rig.Tick();
            rig.SetGrip(rig.Consonant, true);
            rig.Tick();
            Assert.IsTrue(rig.Session.GripView.ConsonantFunctionPage);
        }

        [Test]
        public void どちらのグリップでも取り消せる()
        {
            var rig = new Rig();
            int id = StartConversion(rig);
            rig.Session.CompleteConversion(id, Sample());
            rig.SetGrip(rig.Vowel, true);
            rig.Tick();
            Assert.AreEqual(KeyboardMode.Input, rig.Session.Mode);
        }

        [Test]
        public void 取り消した後は再び変換できる_古い応答は無視する()
        {
            var rig = new Rig();
            int first = StartConversion(rig);
            rig.SetGrip(rig.Consonant, true);
            rig.Tick();
            rig.SetGrip(rig.Consonant, false);
            rig.Tick();
            Assert.AreEqual(KeyboardMode.Input, rig.Session.Mode);

            // 取り消した後に届いた応答は無視する。
            rig.Session.CompleteConversion(first, Sample());
            Assert.AreEqual(KeyboardMode.Input, rig.Session.Mode);
            Assert.IsNull(rig.Session.CurrentConversion);

            rig.VowelFunction(Convert);
            Assert.AreEqual(KeyboardMode.WaitingConversion, rig.Session.Mode);
            int second = rig.Effects.ConversionRequests[1].Key;
            Assert.AreNotEqual(first, second);

            rig.Session.CompleteConversion(first, Sample());
            Assert.AreEqual(KeyboardMode.WaitingConversion, rig.Session.Mode, "古い id の応答は無視する");
            rig.Session.CompleteConversion(second, Sample());
            Assert.AreEqual(KeyboardMode.Converting, rig.Session.Mode);
        }

        [Test]
        public void 応答待ちの間はトリガーを無視する()
        {
            var rig = new Rig();
            StartConversion(rig);
            rig.Pull(rig.Consonant);
            rig.Pull(rig.Vowel);
            Assert.AreEqual(KeyboardMode.WaitingConversion, rig.Session.Mode);
            Assert.AreEqual("あい", rig.Session.Composition);
            Assert.AreEqual("", rig.Session.Committed);
        }

        [Test]
        public void 応答待ちの間は入力できない()
        {
            var rig = new Rig();
            StartConversion(rig);
            rig.Type(0, StickDirection.Up);
            Assert.AreEqual("あい", rig.Session.Composition);
        }

        [Test]
        public void 応答待ちの間にグリップで取り消せる()
        {
            var rig = new Rig();
            StartConversion(rig);
            rig.SetGrip(rig.Consonant, true);
            rig.Tick();
            Assert.AreEqual(KeyboardMode.Input, rig.Session.Mode);
            Assert.AreEqual("あい", rig.Session.Composition);
        }

        [Test]
        public void 変換に失敗するとひらがなのままとカタカナの候補になる()
        {
            var rig = new Rig();
            int id = StartConversion(rig);
            rig.Session.FailConversion(id, "タイムアウトしました");
            Assert.AreEqual(KeyboardMode.Converting, rig.Session.Mode);
            Assert.AreEqual("タイムアウトしました", rig.Session.ConversionFailureReason);
            CollectionAssert.AreEqual(new[] { "あい", "アイ" }, rig.Session.CurrentConversion.Segments[0].Candidates);

            Push(rig, Hand.Right, StickDirection.Down);
            rig.Pull(rig.Consonant);
            Assert.AreEqual("アイ", rig.Session.Committed);
            Assert.IsNull(rig.Session.ConversionFailureReason);
        }

        [Test]
        public void 失敗したときもひらがなのまま確定できる()
        {
            var rig = new Rig();
            int id = StartConversion(rig);
            rig.Session.FailConversion(id, "失敗");
            rig.Pull(rig.Vowel);
            Assert.AreEqual("あい", rig.Session.Committed);
        }

        [Test]
        public void 古い失敗の通知は無視する()
        {
            var rig = new Rig();
            int id = StartConversion(rig);
            rig.Session.FailConversion(id + 100, "失敗");
            Assert.AreEqual(KeyboardMode.WaitingConversion, rig.Session.Mode);
            Assert.IsNull(rig.Session.ConversionFailureReason);
        }

        [Test]
        public void 未確定がなければ変換は何もせず振動で知らせる()
        {
            var rig = new Rig();
            rig.VowelFunction(Convert);
            Assert.AreEqual(KeyboardMode.Input, rig.Session.Mode);
            Assert.AreEqual(0, rig.Effects.ConversionRequests.Count);
            Assert.AreEqual(1, rig.Effects.CountHaptics(HapticKind.Invalid));
        }

        [Test]
        public void 確定済みだけのときも変換は何もしない()
        {
            var rig = new Rig();
            rig.Type(0, StickDirection.Center);
            rig.VowelFunction(Commit);
            rig.Effects.ClearAll();
            rig.VowelFunction(Convert);
            Assert.AreEqual(0, rig.Effects.ConversionRequests.Count);
            Assert.AreEqual(1, rig.Effects.CountHaptics(HapticKind.Invalid));
        }

        [Test]
        public void 変換結果が上限を超えるなら確定せず選び直せる()
        {
            var rig = new Rig();
            rig.Session.SetLengthLimit(4);
            int id = StartConversion(rig);
            rig.Session.CompleteConversion(id, new[] { Segment("あい", "あいうえお", "愛") });
            rig.Effects.ClearAll();

            rig.Pull(rig.Consonant);
            Assert.AreEqual(KeyboardMode.Converting, rig.Session.Mode);
            Assert.AreEqual("", rig.Session.Committed);
            Assert.AreEqual(1, rig.Effects.CountHaptics(HapticKind.Invalid));

            Push(rig, Hand.Right, StickDirection.Down);
            rig.Pull(rig.Consonant);
            Assert.AreEqual(KeyboardMode.Input, rig.Session.Mode);
            Assert.AreEqual("愛", rig.Session.Committed);
        }

        [Test]
        public void 変換の確定は未確定の位置の確定済みへ入る()
        {
            var rig = new Rig();
            rig.VowelFunction(Language);
            rig.Type(1, StickDirection.Center);  // a（確定済み）
            rig.VowelFunction(Language);
            rig.Type(0, StickDirection.Center);
            rig.Type(0, StickDirection.Left);
            rig.VowelFunction(Convert);
            int id = rig.Effects.ConversionRequests[0].Key;
            rig.Session.CompleteConversion(id, Sample());
            rig.Pull(rig.Consonant);
            Assert.AreEqual("a愛雨", rig.Session.Committed);
        }

        [Test]
        public void 変換中にダッシュボードで閉じると未確定のかなに戻る()
        {
            var rig = new Rig();
            int id = StartConversion(rig);
            rig.Session.CompleteConversion(id, Sample());

            rig.Session.SetDashboardVisible(true);
            Assert.IsFalse(rig.Session.IsOpen);
            Assert.AreEqual(KeyboardMode.Input, rig.Session.Mode);
            Assert.IsNull(rig.Session.CurrentConversion);
            Assert.AreEqual("あい", rig.Session.Composition);

            rig.Session.SetDashboardVisible(false);
            Assert.IsTrue(rig.Session.IsOpen);
            Assert.AreEqual(KeyboardMode.Input, rig.Session.Mode);
        }

        [Test]
        public void 応答待ちに閉じたとき_後から届いた応答は無視する()
        {
            var rig = new Rig();
            int id = StartConversion(rig);
            rig.Session.SetDashboardVisible(true);
            rig.Session.SetDashboardVisible(false);
            rig.Session.CompleteConversion(id, Sample());
            Assert.AreEqual(KeyboardMode.Input, rig.Session.Mode);
            Assert.IsNull(rig.Session.CurrentConversion);
        }

        [Test]
        public void 変換中に同時押しの1回目のグリップで取り消し_2回で閉じる()
        {
            var rig = new Rig();
            int id = StartConversion(rig);
            rig.Session.CompleteConversion(id, Sample());
            rig.TickMany(50);
            rig.ChordTwice();
            Assert.IsFalse(rig.Session.IsOpen);
            Assert.AreEqual(KeyboardMode.Input, rig.Session.Mode);
            Assert.AreEqual("あい", rig.Session.Composition);
        }

        [Test]
        public void 変換の応答を渡す引数が不正なら例外()
        {
            var rig = new Rig();
            Assert.Throws<ArgumentNullException>(() => rig.Session.CompleteConversion(1, null));
            Assert.Throws<ArgumentNullException>(() => rig.Session.FailConversion(1, null));
        }
    }

    /// <summary>トリガーのしきい値と、実行中の設定の差し替え。</summary>
    public class SettingsApplyTests
    {
        static void SelectA(Rig rig)
        {
            rig.SetStick(rig.Consonant, Sticks.Cell(0));
            rig.SetStick(rig.Vowel, StickValue.Zero);
            rig.Tick();
        }

        [Test]
        public void トリガーはしきい値以上に引くと入力する()
        {
            var rig = new Rig(new KeyboardSettings(triggerThreshold: 0.6f));
            SelectA(rig);
            rig.SetTriggerPull(rig.Vowel, 0.55f); rig.Tick();
            Assert.AreEqual("", rig.Session.Composition, "しきい値未満では入力しない");
            rig.SetTriggerPull(rig.Vowel, 0.6f); rig.Tick();
            Assert.AreEqual("あ", rig.Session.Composition);
        }

        [Test]
        public void トリガーのしきい値を差し替えると次のフレームから使われる()
        {
            var rig = new Rig();
            SelectA(rig);
            rig.SetTriggerPull(rig.Vowel, 0.5f); rig.Tick();
            Assert.AreEqual("", rig.Session.Composition, "既定のしきい値 0.7 未満");
            rig.SetTriggerPull(rig.Vowel, 0f); rig.Tick();

            rig.Session.ApplySettings(new KeyboardSettings(triggerThreshold: 0.4f));
            rig.SetTriggerPull(rig.Vowel, 0.5f); rig.Tick();
            Assert.AreEqual("あ", rig.Session.Composition);
            Assert.AreEqual(0.4f, rig.Session.Settings.TriggerThreshold);
        }

        [Test]
        public void スティックのしきい値を差し替えると次のフレームから使われる()
        {
            var rig = new Rig();
            var tilt = new StickValue(0.45f, 0f);
            rig.SetStick(rig.Consonant, tilt); rig.Tick();
            Assert.AreEqual(StickClassifier.NeutralCell, rig.Session.GripInput.ConsonantCell, "既定のしきい値 0.5 未満");

            rig.Session.ApplySettings(new KeyboardSettings(stickThreshold: 0.4f));
            rig.Tick();
            Assert.AreEqual(5, rig.Session.GripInput.ConsonantCell, "右に倒したセル");
        }

        [Test]
        public void 連続入力の時間を差し替えると次の長押しから使われる()
        {
            var rig = new Rig();
            rig.Session.ApplySettings(new KeyboardSettings(repeatDelaySeconds: 0.25, repeatIntervalSeconds: 0.5));
            SelectA(rig);
            rig.SetTrigger(rig.Vowel, true); rig.Tick();
            rig.Tick(0.27);
            Assert.AreEqual("ああ", rig.Session.Composition);
            rig.Tick(0.46); // 0.73
            Assert.AreEqual("ああ", rig.Session.Composition, "間隔の前");
            rig.Tick(0.04); // 0.77
            Assert.AreEqual("あああ", rig.Session.Composition);
            Assert.AreEqual(0.25, rig.Session.Settings.RepeatDelaySeconds);
        }

        [Test]
        public void ダッシュボードで一時停止すると連続入力が止まり_再開後も引き直すまで入らない()
        {
            var rig = new Rig();
            SelectA(rig);
            rig.SetTrigger(rig.Vowel, true); rig.Tick();
            rig.Session.SetDashboardVisible(true);
            rig.Tick(1.0);
            rig.Session.SetDashboardVisible(false);
            rig.Tick();
            rig.Tick(1.0);
            Assert.AreEqual("あ", rig.Session.Composition);
        }

        [Test]
        public void しきい値と連続入力の時間以外の変更は例外()
        {
            var rig = new Rig();
            Assert.Throws<NotSupportedException>(() => rig.Session.ApplySettings(new KeyboardSettings(consonantHand: Hand.Left)));
            Assert.Throws<NotSupportedException>(() => rig.Session.ApplySettings(new KeyboardSettings(chordWindowSeconds: 0.3)));
            Assert.Throws<NotSupportedException>(() => rig.Session.ApplySettings(new KeyboardSettings(doubleChordWindowSeconds: 0.8)));
            Assert.Throws<ArgumentNullException>(() => rig.Session.ApplySettings(null));
        }
    }
}
