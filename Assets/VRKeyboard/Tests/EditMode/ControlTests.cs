using System;
using NUnit.Framework;
using VRKeyboard.Core.Control;
using VRKeyboard.Core.Input;

namespace VRKeyboard.Core.Tests
{
    public class DoubleChordDetectorTests
    {
        // 2進数で正確に表せる値にして、境界の判定が丸め誤差に左右されないようにする。
        const double Chord = 0.25;
        const double Double = 0.5;

        static DoubleChordDetector Create()
        {
            return new DoubleChordDetector(Chord, Double);
        }

        // 左 → 右の順にグリップを押す。押した時刻は leftAt, rightAt。離すのは release で行う。
        static bool PressBoth(DoubleChordDetector detector, double leftAt, double rightAt)
        {
            bool first = detector.Update(true, false, leftAt);
            bool second = detector.Update(true, true, rightAt);
            return first || second;
        }

        static void ReleaseBoth(DoubleChordDetector detector, double at)
        {
            detector.Update(false, false, at);
        }

        [Test]
        public void 同時押しを1回だけでは開閉しない()
        {
            var detector = Create();
            Assert.IsFalse(PressBoth(detector, 1.0, 1.1));
        }

        [Test]
        public void 時間差が窓以内の同時押しを窓以内に2回で開閉する()
        {
            var detector = Create();
            Assert.IsFalse(PressBoth(detector, 1.0, 1.25));
            ReleaseBoth(detector, 1.3);
            Assert.IsTrue(PressBoth(detector, 1.5, 1.75));
        }

        [Test]
        public void 時間差が窓を超えると同時押しとみなさない()
        {
            var detector = Create();
            PressBoth(detector, 1.0, 1.25);
            ReleaseBoth(detector, 1.3);
            // 1回目は成立、2回目は 0.3 秒差で不成立。
            Assert.IsFalse(PressBoth(detector, 1.5, 1.8));
        }

        [Test]
        public void 同時押しの間隔が窓を超えると開閉しない()
        {
            var detector = Create();
            PressBoth(detector, 1.0, 1.0);
            ReleaseBoth(detector, 1.1);
            // 1回目の成立（1.0）から 0.75 秒後。
            Assert.IsFalse(PressBoth(detector, 1.75, 1.75));
        }

        [Test]
        public void 同時押しの間隔が窓ちょうどなら開閉する()
        {
            var detector = Create();
            PressBoth(detector, 1.0, 1.0);
            ReleaseBoth(detector, 1.1);
            Assert.IsTrue(PressBoth(detector, 1.5, 1.5));
        }

        [Test]
        public void 開閉の後は1回目から数え直す()
        {
            var detector = Create();
            PressBoth(detector, 1.0, 1.0);
            ReleaseBoth(detector, 1.1);
            Assert.IsTrue(PressBoth(detector, 1.2, 1.2));
            ReleaseBoth(detector, 1.3);
            // 直後の同時押しは新しい1回目。
            Assert.IsFalse(PressBoth(detector, 1.4, 1.4));
        }

        [Test]
        public void 片方を押したままもう片方を押し直しても同時押しになる()
        {
            var detector = Create();
            detector.Update(true, false, 1.0);
            Assert.IsFalse(detector.Update(true, true, 1.1)); // 同時押しの1回目
            detector.Update(true, false, 1.15); // 右だけ離す
            // 右を押し直す。左を押した 1.0 との差が窓以内で、2回目の同時押しになる。
            Assert.IsTrue(detector.Update(true, true, 1.2));
        }

        [Test]
        public void 両方を同じフレームで押しても同時押しになる()
        {
            var detector = Create();
            Assert.IsFalse(detector.Update(true, true, 1.0));
            detector.Update(false, false, 1.1);
            Assert.IsTrue(detector.Update(true, true, 1.2));
        }

        [Test]
        public void 片手だけを2回押しても開閉しない()
        {
            var detector = Create();
            detector.Update(true, false, 1.0);
            detector.Update(false, false, 1.1);
            detector.Update(true, false, 1.2);
            detector.Update(false, false, 1.3);
            Assert.IsFalse(detector.Update(true, false, 1.4));
        }

        [Test]
        public void 窓が不正なら例外()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DoubleChordDetector(0.0, 0.6));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DoubleChordDetector(0.2, -1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DoubleChordDetector(double.NaN, 0.6));
        }

        [Test]
        public void 既定の窓_0_20秒と0_60秒()
        {
            var settings = new KeyboardSettings();
            var detector = new DoubleChordDetector(settings.ChordWindowSeconds, settings.DoubleChordWindowSeconds);
            // 時間差 0.19 は同時押し、0.21 は同時押しではない。
            Assert.IsFalse(PressBoth(detector, 10.0, 10.19));
            ReleaseBoth(detector, 10.3);
            Assert.IsFalse(PressBoth(detector, 10.4, 10.61)); // 0.21 差 → 不成立
            ReleaseBoth(detector, 10.7);
            // 最後に成立した同時押し（10.19）から 0.59 秒後に成立
            Assert.IsTrue(PressBoth(detector, 10.7, 10.78));
        }
    }

    public class KeyboardSettingsTests
    {
        [Test]
        public void 既定値()
        {
            var settings = new KeyboardSettings();
            Assert.AreEqual(Hand.Right, settings.ConsonantHand);
            Assert.AreEqual(Hand.Left, settings.VowelHand);
            Assert.AreEqual(0.5f, settings.StickThreshold);
            Assert.AreEqual(0.7f, settings.TriggerThreshold);
            Assert.AreEqual(0.20, settings.ChordWindowSeconds);
            Assert.AreEqual(0.60, settings.DoubleChordWindowSeconds);
            Assert.AreEqual(144, settings.MaxLength);
            Assert.AreEqual(0.50, settings.RepeatDelaySeconds);
            Assert.AreEqual(0.10, settings.RepeatIntervalSeconds);
        }

        [Test]
        public void 子音の手を左にすると母音は右()
        {
            var settings = new KeyboardSettings(consonantHand: Hand.Left);
            Assert.AreEqual(Hand.Right, settings.VowelHand);
        }

        [Test]
        public void 不正な値は例外()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new KeyboardSettings(stickThreshold: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KeyboardSettings(stickThreshold: -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KeyboardSettings(stickThreshold: 1.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KeyboardSettings(stickThreshold: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KeyboardSettings(triggerThreshold: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KeyboardSettings(triggerThreshold: 1.01f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KeyboardSettings(triggerThreshold: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KeyboardSettings(chordWindowSeconds: 0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KeyboardSettings(chordWindowSeconds: -0.2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KeyboardSettings(doubleChordWindowSeconds: 0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KeyboardSettings(maxLength: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KeyboardSettings(maxLength: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KeyboardSettings(consonantHand: (Hand)5));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KeyboardSettings(repeatDelaySeconds: 0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KeyboardSettings(repeatDelaySeconds: -0.1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KeyboardSettings(repeatDelaySeconds: double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KeyboardSettings(repeatDelaySeconds: double.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KeyboardSettings(repeatIntervalSeconds: 0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KeyboardSettings(repeatIntervalSeconds: -0.1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KeyboardSettings(repeatIntervalSeconds: double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KeyboardSettings(repeatIntervalSeconds: double.PositiveInfinity));
        }

        [Test]
        public void しきい値は1ちょうどを許す()
        {
            Assert.DoesNotThrow(() => new KeyboardSettings(stickThreshold: 1f, triggerThreshold: 1f));
        }
    }

    public class EdgeTrackerTests
    {
        const float Threshold = 0.7f;

        static ControllerFrame LeftTrigger(float pull)
        {
            return new ControllerFrame(new HandState(StickValue.Zero, pull, false), new HandState(StickValue.Zero, 0f, false));
        }

        [Test]
        public void しきい値以上で押下エッジが出る()
        {
            var tracker = new EdgeTracker();
            Assert.IsFalse(tracker.Update(LeftTrigger(0.69f), Threshold).LeftTrigger);
            Assert.IsTrue(tracker.Update(LeftTrigger(0.7f), Threshold).LeftTrigger);
            Assert.IsFalse(tracker.Update(LeftTrigger(1f), Threshold).LeftTrigger, "押し続けている間はエッジが出ない");
        }

        [Test]
        public void しきい値を下回ると離したとみなす()
        {
            var tracker = new EdgeTracker();
            tracker.Update(LeftTrigger(0.8f), Threshold);
            Assert.IsFalse(tracker.Update(LeftTrigger(0.69f), Threshold).LeftTrigger);
            Assert.IsTrue(tracker.Update(LeftTrigger(0.7f), Threshold).LeftTrigger, "離した後に再びしきい値以上にするとエッジが出る");
        }
    }

    public class KeyRepeaterTests
    {
        // 2進数で正確に表せる時刻を使い、境界ちょうどの判定を確かめる。
        const double Delay = 0.5;
        const double Interval = 0.25;

        [Test]
        public void 遅延の後に最初の連続入力_以降は間隔ごと()
        {
            var repeater = new KeyRepeater();
            repeater.Start(Hand.Left, 1.0, Delay);
            Assert.IsTrue(repeater.IsActive);
            Assert.AreEqual(Hand.Left, repeater.Hand);
            Assert.IsFalse(repeater.Poll(1.25, Interval));
            Assert.IsTrue(repeater.Poll(1.5, Interval));
            Assert.IsFalse(repeater.Poll(1.5, Interval));
            Assert.IsFalse(repeater.Poll(1.625, Interval));
            Assert.IsTrue(repeater.Poll(1.75, Interval));
            Assert.IsTrue(repeater.Poll(2.0, Interval));
        }

        [Test]
        public void 間隔を超えて遅れても1回だけで_次は遅れた時刻から間隔の後()
        {
            var repeater = new KeyRepeater();
            repeater.Start(Hand.Right, 0.0, Delay);
            Assert.IsTrue(repeater.Poll(2.0, Interval));
            Assert.IsFalse(repeater.Poll(2.125, Interval));
            Assert.IsTrue(repeater.Poll(2.25, Interval));
        }

        [Test]
        public void 止めると連続入力しない()
        {
            var repeater = new KeyRepeater();
            Assert.IsFalse(repeater.IsActive);
            Assert.IsFalse(repeater.Poll(10.0, Interval));
            repeater.Start(Hand.Left, 0.0, Delay);
            repeater.Stop();
            Assert.IsFalse(repeater.IsActive);
            Assert.IsFalse(repeater.Poll(10.0, Interval));
            Assert.Throws<InvalidOperationException>(() => { var _ = repeater.Hand; });
        }

        [Test]
        public void 不正な引数は例外()
        {
            var repeater = new KeyRepeater();
            Assert.Throws<ArgumentOutOfRangeException>(() => repeater.Start((Hand)5, 0.0, Delay));
            Assert.Throws<ArgumentOutOfRangeException>(() => repeater.Start(Hand.Left, 0.0, 0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => repeater.Start(Hand.Left, 0.0, double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => repeater.Poll(0.0, 0.0));
        }
    }
}
