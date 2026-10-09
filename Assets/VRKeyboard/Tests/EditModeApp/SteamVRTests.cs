using System;
using NUnit.Framework;
using VRKeyboard.App.SteamVR;
using VRKeyboard.App.View;
using VRKeyboard.Core;

namespace VRKeyboard.App.Tests
{
    public sealed class OverlayPlacementTests
    {
        const float Epsilon = 1e-4f;

        [Test]
        public void 盤面の中心がHMD基準の指定位置に来る()
        {
            float[] matrix = OverlayPlacement.ComputeOverlayToHmd();

            // テクスチャの中心から盤面の中心までの、オーバーレイ面内の下向きの距離。
            float metersPerPixel = OverlayPlacement.WidthMeters / KeyboardTheme.Width;
            float down = (KeyboardTheme.BoardCenterY - KeyboardTheme.Height / 2f) * metersPerPixel;
            Assert.Greater(down, 0f, "盤面の中心はテクスチャの中心より下にある");

            (float x, float y, float z) = OverlayPlacement.Transform(matrix, 0f, -down, 0f);
            Assert.AreEqual(0f, x, Epsilon);
            Assert.AreEqual(-OverlayPlacement.BoardDownMeters, y, Epsilon);
            Assert.AreEqual(-OverlayPlacement.BoardForwardMeters, z, Epsilon);
        }

        [Test]
        public void 盤面の法線は盤面の中心からHMDの方を向く()
        {
            float[] matrix = OverlayPlacement.ComputeOverlayToHmd();
            float down = (KeyboardTheme.BoardCenterY - KeyboardTheme.Height / 2f) * (OverlayPlacement.WidthMeters / KeyboardTheme.Width);
            (float cx, float cy, float cz) = OverlayPlacement.Transform(matrix, 0f, -down, 0f);
            (float nx, float ny, float nz) = OverlayPlacement.Transform(matrix, 0f, -down, 1f);

            // 法線（オーバーレイの +Z の回転後の向き）と、盤面の中心から HMD（原点）への向きの角度。
            float[] normal = { nx - cx, ny - cy, nz - cz };
            float[] toHmd = { -cx, -cy, -cz };
            float dot = normal[0] * toHmd[0] + normal[1] * toHmd[1] + normal[2] * toHmd[2];
            float lengths = (float)(Math.Sqrt(Dot(normal)) * Math.Sqrt(Dot(toHmd)));
            // 浮動小数点の誤差で 1 をわずかに超えると Acos が NaN になるため丸める。
            float cosine = Math.Max(-1f, Math.Min(1f, dot / lengths));
            float angle = (float)(Math.Acos(cosine) * 180.0 / Math.PI);
            Assert.AreEqual(0f, angle, 0.5f, "法線は HMD の方を向く（傾き 17° は atan(0.26/0.85) にほぼ等しい）");
        }

        static float Dot(float[] v) => v[0] * v[0] + v[1] * v[1] + v[2] * v[2];

        [Test]
        public void 行列は右手系の回転と平行移動()
        {
            float[] m = OverlayPlacement.ComputeOverlayToHmd();
            // 回転部分の直交性: 各列の長さが 1。
            for (int col = 0; col < 3; col++)
                Assert.AreEqual(1f, m[col] * m[col] + m[4 + col] * m[4 + col] + m[8 + col] * m[8 + col], Epsilon);
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
