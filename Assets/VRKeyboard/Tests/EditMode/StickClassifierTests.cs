using System;
using NUnit.Framework;
using VRKeyboard.Core.Grip;
using VRKeyboard.Core.Input;

namespace VRKeyboard.Core.Tests
{
    public class StickClassifierTests
    {
        const float Enter = 0.5f;
        const float Exit = 0.3f;

        static StickValue Polar(double degrees, float length)
        {
            double radians = degrees * Math.PI / 180.0;
            return new StickValue((float)(Math.Cos(radians) * length), (float)(Math.Sin(radians) * length));
        }

        // 角度（右が 0°、反時計回り）→ セル番号。
        [TestCase(0, 5)]
        [TestCase(45, 2)]
        [TestCase(90, 1)]
        [TestCase(135, 0)]
        [TestCase(180, 3)]
        [TestCase(225, 6)]
        [TestCase(270, 7)]
        [TestCase(315, 8)]
        [TestCase(20, 5)]
        [TestCase(25, 2)]
        [TestCase(65, 2)]
        [TestCase(70, 1)]
        [TestCase(-20, 5)]
        [TestCase(-25, 8)]
        public void ClassifyCell_8方向を45度ずつに分ける(double degrees, int expected)
        {
            Assert.AreEqual(expected, StickClassifier.ClassifyCell(Polar(degrees, 1f), 4, Enter, Exit));
        }

        [Test]
        public void ClassifyCell_倒し量がenter以上で方向を決める()
        {
            Assert.AreEqual(5, StickClassifier.ClassifyCell(new StickValue(0.5f, 0f), 4, Enter, Exit));
            Assert.AreEqual(4, StickClassifier.ClassifyCell(new StickValue(0.49f, 0f), 4, Enter, Exit));
        }

        [Test]
        public void ClassifyCell_exit未満で中央に戻る()
        {
            Assert.AreEqual(4, StickClassifier.ClassifyCell(new StickValue(0.29f, 0f), 5, Enter, Exit));
            Assert.AreEqual(4, StickClassifier.ClassifyCell(StickValue.Zero, 0, Enter, Exit));
        }

        [Test]
        public void ClassifyCell_enterとexitの間は直前を保つ()
        {
            Assert.AreEqual(5, StickClassifier.ClassifyCell(new StickValue(0.4f, 0f), 5, Enter, Exit));
            Assert.AreEqual(4, StickClassifier.ClassifyCell(new StickValue(0.4f, 0f), 4, Enter, Exit));
            Assert.AreEqual(5, StickClassifier.ClassifyCell(new StickValue(0.31f, 0f), 5, Enter, Exit));
            // 保っている間は、傾けた向きが変わっても切り替わらない。
            Assert.AreEqual(5, StickClassifier.ClassifyCell(new StickValue(0f, 0.4f), 5, Enter, Exit));
        }

        [Test]
        public void ClassifyCell_範囲外の直前の値は例外()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => StickClassifier.ClassifyCell(StickValue.Zero, 9, Enter, Exit));
            Assert.Throws<ArgumentOutOfRangeException>(() => StickClassifier.ClassifyCell(StickValue.Zero, -1, Enter, Exit));
        }

        [TestCase(0, StickDirection.Right)]
        [TestCase(40, StickDirection.Right)]
        [TestCase(50, StickDirection.Up)]
        [TestCase(90, StickDirection.Up)]
        [TestCase(130, StickDirection.Up)]
        [TestCase(140, StickDirection.Left)]
        [TestCase(180, StickDirection.Left)]
        [TestCase(220, StickDirection.Left)]
        [TestCase(230, StickDirection.Down)]
        [TestCase(270, StickDirection.Down)]
        [TestCase(310, StickDirection.Down)]
        [TestCase(320, StickDirection.Right)]
        public void ClassifyDirection_4方向を90度ずつに分ける(double degrees, StickDirection expected)
        {
            Assert.AreEqual(expected, StickClassifier.ClassifyDirection(Polar(degrees, 1f), StickDirection.Center, Enter, Exit));
        }

        [Test]
        public void ClassifyDirection_ヒステリシス()
        {
            Assert.AreEqual(StickDirection.Center, StickClassifier.ClassifyDirection(new StickValue(0.49f, 0f), StickDirection.Center, Enter, Exit));
            Assert.AreEqual(StickDirection.Right, StickClassifier.ClassifyDirection(new StickValue(0.5f, 0f), StickDirection.Center, Enter, Exit));
            Assert.AreEqual(StickDirection.Right, StickClassifier.ClassifyDirection(new StickValue(0.4f, 0f), StickDirection.Right, Enter, Exit));
            Assert.AreEqual(StickDirection.Center, StickClassifier.ClassifyDirection(new StickValue(0.29f, 0f), StickDirection.Right, Enter, Exit));
        }

        [Test]
        public void ClassifyDirection_未定義の直前の値は例外()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => StickClassifier.ClassifyDirection(StickValue.Zero, (StickDirection)9, Enter, Exit));
        }

        [Test]
        public void Tracker_中央から倒した瞬間だけ方向を返す()
        {
            var tracker = new StickDirectionTracker();
            Assert.AreEqual(StickDirection.Center, tracker.Update(StickValue.Zero, Enter, Exit));
            Assert.AreEqual(StickDirection.Right, tracker.Update(new StickValue(1f, 0f), Enter, Exit));
            Assert.AreEqual(StickDirection.Center, tracker.Update(new StickValue(1f, 0f), Enter, Exit));
            Assert.AreEqual(StickDirection.Right, tracker.Current);
            // enter と exit の間にいる間は倒したままとみなす。
            Assert.AreEqual(StickDirection.Center, tracker.Update(new StickValue(0.4f, 0f), Enter, Exit));
            Assert.AreEqual(StickDirection.Center, tracker.Update(StickValue.Zero, Enter, Exit));
            Assert.AreEqual(StickDirection.Up, tracker.Update(new StickValue(0f, 1f), Enter, Exit));
        }

        [Test]
        public void Tracker_中央を経由しない方向の切り替えは返さない()
        {
            var tracker = new StickDirectionTracker();
            tracker.Update(new StickValue(1f, 0f), Enter, Exit);
            Assert.AreEqual(StickDirection.Center, tracker.Update(new StickValue(0f, 1f), Enter, Exit));
            Assert.AreEqual(StickDirection.Up, tracker.Current);
        }
    }
}
