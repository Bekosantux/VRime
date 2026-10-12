using System;
using NUnit.Framework;
using VRKeyboard.Core.Grip;
using VRKeyboard.Core.Input;

namespace VRKeyboard.Core.Tests
{
    public class StickClassifierTests
    {
        const float Threshold = 0.5f;

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
            Assert.AreEqual(expected, StickClassifier.ClassifyCell(Polar(degrees, 1f), Threshold));
        }

        [Test]
        public void ClassifyCell_倒し量がしきい値以上で方向を決め未満なら中央()
        {
            Assert.AreEqual(5, StickClassifier.ClassifyCell(new StickValue(0.5f, 0f), Threshold));
            Assert.AreEqual(4, StickClassifier.ClassifyCell(new StickValue(0.49f, 0f), Threshold));
            Assert.AreEqual(4, StickClassifier.ClassifyCell(StickValue.Zero, Threshold));
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
            Assert.AreEqual(expected, StickClassifier.ClassifyDirection(Polar(degrees, 1f), Threshold));
        }

        [Test]
        public void ClassifyDirection_倒し量がしきい値以上で方向を決め未満なら中央()
        {
            Assert.AreEqual(StickDirection.Right, StickClassifier.ClassifyDirection(new StickValue(0.5f, 0f), Threshold));
            Assert.AreEqual(StickDirection.Center, StickClassifier.ClassifyDirection(new StickValue(0.49f, 0f), Threshold));
            Assert.AreEqual(StickDirection.Center, StickClassifier.ClassifyDirection(StickValue.Zero, Threshold));
        }

        [Test]
        public void Tracker_中央から倒した瞬間だけ方向を返す()
        {
            var tracker = new StickDirectionTracker();
            Assert.AreEqual(StickDirection.Center, tracker.Update(StickValue.Zero, Threshold));
            Assert.AreEqual(StickDirection.Right, tracker.Update(new StickValue(1f, 0f), Threshold));
            Assert.AreEqual(StickDirection.Center, tracker.Update(new StickValue(1f, 0f), Threshold));
            Assert.AreEqual(StickDirection.Right, tracker.Current);
            // しきい値を下回ると中央に戻り、再びしきい値以上に倒すと方向を返す。
            Assert.AreEqual(StickDirection.Center, tracker.Update(new StickValue(0.49f, 0f), Threshold));
            Assert.AreEqual(StickDirection.Center, tracker.Current);
            Assert.AreEqual(StickDirection.Right, tracker.Update(new StickValue(0.5f, 0f), Threshold));
            Assert.AreEqual(StickDirection.Center, tracker.Update(StickValue.Zero, Threshold));
            Assert.AreEqual(StickDirection.Up, tracker.Update(new StickValue(0f, 1f), Threshold));
        }

        [Test]
        public void Tracker_中央を経由しない方向の切り替えは返さない()
        {
            var tracker = new StickDirectionTracker();
            tracker.Update(new StickValue(1f, 0f), Threshold);
            Assert.AreEqual(StickDirection.Center, tracker.Update(new StickValue(0f, 1f), Threshold));
            Assert.AreEqual(StickDirection.Up, tracker.Current);
        }
    }
}
