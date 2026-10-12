using System;
using NUnit.Framework;
using VRKeyboard.App.Settings;
using VRKeyboard.App.SteamVR;
using VRKeyboard.Core;

namespace VRKeyboard.App.Tests
{
    public sealed class OverlayPlacementTests
    {
        const float Epsilon = 1e-4f;

        // 1024px 四方のテクスチャで、盤面の中心が中心から 172px 下にある配置（シーンの既定の配置と同じ）。
        const float BoardCenterOffset = 172f / 1024f;

        static readonly OverlayPlacement Overlay = new OverlayPlacement(BoardCenterOffset);

        [Test]
        public void 盤面の中心がHMD基準の指定位置に来る()
        {
            KeyboardPlacement placement = KeyboardPlacement.Default;
            float[] matrix = Overlay.ComputeOverlayToHmd(placement);

            // テクスチャの中心から盤面の中心までの、オーバーレイ面内の下向きの距離。
            float down = BoardCenterOffset * placement.WidthMeters;
            Assert.AreEqual(down, Overlay.BoardOffsetDownMeters(placement), Epsilon);

            (float x, float y, float z) = OverlayPlacement.Transform(matrix, 0f, -down, 0f);
            Assert.AreEqual(0f, x, Epsilon);
            Assert.AreEqual(-placement.DownMeters, y, Epsilon);
            Assert.AreEqual(-placement.ForwardMeters, z, Epsilon);
        }

        [Test]
        public void 左右の位置と傾きと幅を変えても盤面の中心が指定位置に来る()
        {
            var placement = new KeyboardPlacement(0.40f, 0.60f, -0.20f, 35f, 0.70f);
            float[] matrix = Overlay.ComputeOverlayToHmd(placement);
            float down = Overlay.BoardOffsetDownMeters(placement);

            (float x, float y, float z) = OverlayPlacement.Transform(matrix, 0f, -down, 0f);
            Assert.AreEqual(-0.20f, x, Epsilon, "左へ 20cm");
            Assert.AreEqual(-0.40f, y, Epsilon);
            Assert.AreEqual(-0.60f, z, Epsilon);
        }

        [Test]
        public void 幅に比例して盤面の中心までの距離が変わる()
        {
            float narrow = Overlay.BoardOffsetDownMeters(KeyboardPlacement.Default.WithWidth(0.30f));
            float wide = Overlay.BoardOffsetDownMeters(KeyboardPlacement.Default.WithWidth(0.60f));
            Assert.AreEqual(narrow * 2f, wide, Epsilon);
        }

        [Test]
        public void 既定の盤面の法線は盤面の中心からHMDの方を向く()
        {
            float angle = NormalToHmdAngle(KeyboardPlacement.Default);
            Assert.AreEqual(0f, angle, 0.5f, "法線は HMD の方を向く（傾き 17° は atan(0.26/0.85) にほぼ等しい）");
        }

        [Test]
        public void 傾き0では盤面がHMDの正面に垂直に立つ()
        {
            float[] matrix = Overlay.ComputeOverlayToHmd(KeyboardPlacement.Default.WithTilt(0f));
            (float cx, float cy, float cz) = OverlayPlacement.Transform(matrix, 0f, 0f, 0f);
            (float nx, float ny, float nz) = OverlayPlacement.Transform(matrix, 0f, 0f, 1f);
            Assert.AreEqual(0f, nx - cx, Epsilon);
            Assert.AreEqual(0f, ny - cy, Epsilon);
            Assert.AreEqual(1f, nz - cz, Epsilon, "法線は +Z（HMD の後ろ向き。盤面の表が HMD を向く）");
        }

        static float NormalToHmdAngle(KeyboardPlacement placement)
        {
            float[] matrix = Overlay.ComputeOverlayToHmd(placement);
            float down = Overlay.BoardOffsetDownMeters(placement);
            (float cx, float cy, float cz) = OverlayPlacement.Transform(matrix, 0f, -down, 0f);
            (float nx, float ny, float nz) = OverlayPlacement.Transform(matrix, 0f, -down, 1f);

            // 法線（オーバーレイの +Z の回転後の向き）と、盤面の中心から HMD（原点）への向きの角度。
            float[] normal = { nx - cx, ny - cy, nz - cz };
            float[] toHmd = { -cx, -cy, -cz };
            float dot = normal[0] * toHmd[0] + normal[1] * toHmd[1] + normal[2] * toHmd[2];
            float lengths = (float)(Math.Sqrt(Dot(normal)) * Math.Sqrt(Dot(toHmd)));
            // 浮動小数点の誤差で 1 をわずかに超えると Acos が NaN になるため丸める。
            float cosine = Math.Max(-1f, Math.Min(1f, dot / lengths));
            return (float)(Math.Acos(cosine) * 180.0 / Math.PI);
        }

        static float Dot(float[] v) => v[0] * v[0] + v[1] * v[1] + v[2] * v[2];

        [Test]
        public void 行列は右手系の回転と平行移動()
        {
            float[] m = Overlay.ComputeOverlayToHmd(new KeyboardPlacement(0.1f, 1.0f, 0.3f, 50f, 0.5f));
            // 回転部分の直交性: 各列の長さが 1。
            for (int col = 0; col < 3; col++)
                Assert.AreEqual(1f, m[col] * m[col] + m[4 + col] * m[4 + col] + m[8 + col] * m[8 + col], Epsilon);
        }

        [Test]
        public void 位置がないと例外()
        {
            Assert.Throws<ArgumentNullException>(() => Overlay.ComputeOverlayToHmd(null));
        }

        [Test]
        public void 盤面の中心までの比が有限でないと例外()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new OverlayPlacement(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new OverlayPlacement(float.PositiveInfinity));
        }
    }

    public sealed class OpenVRHapticPatternTests
    {
        [Test]
        public void すべての種類に異なるパターンがある()
        {
            var seen = new System.Collections.Generic.HashSet<(float, float, float)>();
            foreach (HapticKind kind in Enum.GetValues(typeof(HapticKind)))
            {
                OpenVRHapticOutput.Pattern pattern = OpenVRHapticOutput.GetPattern(kind);
                Assert.Greater(pattern.Seconds, 0f, kind.ToString());
                Assert.Greater(pattern.Frequency, 0f, kind.ToString());
                Assert.That(pattern.Amplitude, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f), kind.ToString());
                Assert.IsTrue(seen.Add((pattern.Seconds, pattern.Frequency, pattern.Amplitude)), kind + " が他の種類と同じパターン");
            }
        }

        [Test]
        public void 試作2の値を保つ()
        {
            OpenVRHapticOutput.Pattern move = OpenVRHapticOutput.GetPattern(HapticKind.Move);
            Assert.AreEqual(0.01f, move.Seconds);
            Assert.AreEqual(160f, move.Frequency);
            Assert.AreEqual(0.6f, move.Amplitude);

            OpenVRHapticOutput.Pattern openClose = OpenVRHapticOutput.GetPattern(HapticKind.OpenClose);
            Assert.AreEqual(0.08f, openClose.Seconds);
            Assert.AreEqual(160f, openClose.Frequency);
            Assert.AreEqual(0.6f, openClose.Amplitude);
        }

        [Test]
        public void 未知の種類は例外()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => OpenVRHapticOutput.GetPattern((HapticKind)99));
        }
    }
}
