using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using VRime.App.Settings;
using VRime.App.SteamVR;
using VRime.Core;

namespace VRime.App.Tests
{
    public sealed class OverlayPlacementTests
    {
        const float Epsilon = 1e-4f;

        // 1024px 四方のテクスチャで、ボードの中心が中心から 172px 下にある配置（シーンの既定の配置と同じ）。
        const float BoardCenterOffset = 172f / 1024f;

        static readonly OverlayPlacement Overlay = new OverlayPlacement(BoardCenterOffset);

        [Test]
        public void ボードの中心がHMD基準の指定位置に来る()
        {
            KeyboardPlacement placement = KeyboardPlacement.Default;
            float[] matrix = Overlay.ComputeOverlayToHmd(placement);

            // テクスチャの中心からボードの中心までの、オーバーレイ面内の下向きの距離。
            float down = BoardCenterOffset * placement.WidthMeters;
            Assert.AreEqual(down, Overlay.BoardOffsetDownMeters(placement), Epsilon);

            (float x, float y, float z) = OverlayPlacement.Transform(matrix, 0f, -down, 0f);
            Assert.AreEqual(0f, x, Epsilon);
            Assert.AreEqual(-placement.DownMeters, y, Epsilon);
            Assert.AreEqual(-placement.ForwardMeters, z, Epsilon);
        }

        [Test]
        public void 左右の位置と傾きと幅を変えてもボードの中心が指定位置に来る()
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
        public void 幅に比例してボードの中心までの距離が変わる()
        {
            float narrow = Overlay.BoardOffsetDownMeters(KeyboardPlacement.Default.WithWidth(0.30f));
            float wide = Overlay.BoardOffsetDownMeters(KeyboardPlacement.Default.WithWidth(0.60f));
            Assert.AreEqual(narrow * 2f, wide, Epsilon);
        }

        [Test]
        public void 既定のボードの法線はボードの中心からHMDの方を向く()
        {
            float angle = NormalToHmdAngle(KeyboardPlacement.Default);
            Assert.AreEqual(0f, angle, 0.5f, "法線は HMD の方を向く（傾き 17° は atan(0.26/0.85) にほぼ等しい）");
        }

        [Test]
        public void 傾き0ではボードがHMDの正面に垂直に立つ()
        {
            float[] matrix = Overlay.ComputeOverlayToHmd(KeyboardPlacement.Default.WithTilt(0f));
            (float cx, float cy, float cz) = OverlayPlacement.Transform(matrix, 0f, 0f, 0f);
            (float nx, float ny, float nz) = OverlayPlacement.Transform(matrix, 0f, 0f, 1f);
            Assert.AreEqual(0f, nx - cx, Epsilon);
            Assert.AreEqual(0f, ny - cy, Epsilon);
            Assert.AreEqual(1f, nz - cz, Epsilon, "法線は +Z（HMD の後ろ向き。ボードの表が HMD を向く）");
        }

        static float NormalToHmdAngle(KeyboardPlacement placement)
        {
            float[] matrix = Overlay.ComputeOverlayToHmd(placement);
            float down = Overlay.BoardOffsetDownMeters(placement);
            (float cx, float cy, float cz) = OverlayPlacement.Transform(matrix, 0f, -down, 0f);
            (float nx, float ny, float nz) = OverlayPlacement.Transform(matrix, 0f, -down, 1f);

            // 法線（オーバーレイの +Z の回転後の向き）と、ボードの中心から HMD（原点）への向きの角度。
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
        public void ボードの中心までの比が有限でないと例外()
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
            var seen = new HashSet<(float, float, float)>();
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
        public void MoveとOpenCloseは実機で確かめた値を保つ()
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

    /// <summary>アクションマニフェストと同梱の既定のバインド（docs/spec/grip-input.md「コントローラーの割り当て」）。</summary>
    public sealed class ActionManifestTests
    {
        static readonly string ManifestPath = Path.Combine(Application.streamingAssetsPath, OpenVRPlatform.ActionManifestRelativePath);

        static JObject Load(string path)
        {
            Assert.IsTrue(File.Exists(path), "ファイルがない: " + path);
            return JObject.Parse(File.ReadAllText(path));
        }

        static JObject Manifest => Load(ManifestPath);

        static IEnumerable<string> ActionNames(JObject manifest)
        {
            return manifest["actions"].Select(action => (string)action["name"]);
        }

        [Test]
        public void アクションはアプリが読むものと過不足なく一致する()
        {
            JObject manifest = Manifest;
            CollectionAssert.AreEquivalent(OpenVRActions.Names, ActionNames(manifest));
            CollectionAssert.AreEqual(
                new[] { OpenVRActions.ActionSetName },
                manifest["action_sets"].Select(set => (string)set["name"]));
        }

        [Test]
        public void 入力のアクションはすべて必須()
        {
            foreach (JToken action in Manifest["actions"].Where(action => (string)action["type"] != "vibration"))
                Assert.AreEqual("mandatory", (string)action["requirement"], (string)action["name"]);
        }

        [Test]
        public void 表示名はすべての言語ですべてのアクションにある()
        {
            JObject manifest = Manifest;
            string[] keys = ActionNames(manifest).Prepend(OpenVRActions.ActionSetName).ToArray();
            CollectionAssert.AreEquivalent(new[] { "en_US", "ja_JP" }, manifest["localization"].Select(l => (string)l["language_tag"]));
            foreach (JToken localization in manifest["localization"])
            {
                foreach (string key in keys)
                    Assert.IsFalse(string.IsNullOrEmpty((string)localization[key]), localization["language_tag"] + " に " + key + " の表示名がない");
            }
        }

        [Test]
        public void 既定のバインドはすべてのアクションを割り当て_宣言にない出力を持たない()
        {
            JObject manifest = Manifest;
            string[] declared = ActionNames(manifest).ToArray();
            string directory = Path.GetDirectoryName(ManifestPath);
            JToken[] defaults = manifest["default_bindings"].ToArray();
            Assert.IsNotEmpty(defaults);

            foreach (JToken entry in defaults)
            {
                string controllerType = (string)entry["controller_type"];
                JObject binding = Load(Path.Combine(directory, (string)entry["binding_url"]));
                Assert.AreEqual(controllerType, (string)binding["controller_type"]);

                JToken set = binding["bindings"][OpenVRActions.ActionSetName];
                Assert.IsNotNull(set, controllerType + " にアクションセットの割り当てがない");
                var outputs = new List<string>();
                outputs.AddRange(set["sources"].SelectMany(source => ((JObject)source["inputs"]).Properties())
                    .Select(input => (string)input.Value["output"]));
                outputs.AddRange(set["haptics"].Select(haptic => (string)haptic["output"]));

                CollectionAssert.IsSubsetOf(outputs, declared, controllerType + " に宣言にない出力がある");
                CollectionAssert.IsSubsetOf(declared, outputs, controllerType + " に割り当てのないアクションがある");
            }
        }
    }

    public sealed class ApplicationManifestTests
    {
        const string Binary = @"C:\Games\VRime\VRime.exe";
        const string Actions = @"C:\Games\VRime\VRime_Data\StreamingAssets\SteamVR\actions.json";
        const string Image = @"C:\Games\VRime\VRime_Data\StreamingAssets\SteamVR\dashboard_icon.png";

        string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "VRimeTests-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        [Test]
        public void 自動で起動できるダッシュボードのオーバーレイとして絶対パスで登録する()
        {
            JObject manifest = JObject.Parse(OpenVRApplicationManifest.Create(Binary, Actions, Image));
            JToken application = manifest["applications"].Single();
            Assert.AreEqual(OpenVRApplicationManifest.AppKey, (string)application["app_key"]);
            Assert.AreEqual("binary", (string)application["launch_type"]);
            Assert.AreEqual(Binary, (string)application["binary_path_windows"]);
            Assert.IsTrue((bool)application["is_dashboard_overlay"]);
            Assert.AreEqual(Actions, (string)application["action_manifest_path"]);
            Assert.AreEqual(Image, (string)application["image_path"]);
            Assert.AreEqual(AppInfo.DisplayName, (string)application["strings"]["en_us"]["name"]);
        }

        [Test]
        public void パスが空か相対パスなら例外()
        {
            Assert.Throws<ArgumentException>(() => OpenVRApplicationManifest.Create("VRime.exe", Actions, Image));
            Assert.Throws<ArgumentException>(() => OpenVRApplicationManifest.Create(Binary, "", Image));
            Assert.Throws<ArgumentException>(() => OpenVRApplicationManifest.Create(Binary, Actions, null));
        }

        [Test]
        public void フォルダを作って書き_同じ内容なら書き換えない()
        {
            string path = Path.Combine(_directory, OpenVRApplicationManifest.FileName);
            string content = OpenVRApplicationManifest.Create(Binary, Actions, Image);
            OpenVRApplicationManifest.Write(path, content);
            Assert.AreEqual(content, File.ReadAllText(path));

            var written = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(path, written);
            OpenVRApplicationManifest.Write(path, content);
            Assert.AreEqual(written, File.GetLastWriteTimeUtc(path));

            string moved = OpenVRApplicationManifest.Create(@"D:\VRime\VRime.exe", Actions, Image);
            OpenVRApplicationManifest.Write(path, moved);
            Assert.AreEqual(moved, File.ReadAllText(path));
        }
    }
}
