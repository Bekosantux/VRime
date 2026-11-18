using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using VRime.App.Settings;
using VRime.Core;

namespace VRime.App.Tests
{
    public sealed class SettingRangeTests
    {
        [Test]
        public void 刻みに揃えて範囲に収める()
        {
            var range = new SettingRange(0.30f, 1.50f, 0.01f);
            Assert.AreEqual(0.85f, range.Snap(0.8512f));
            Assert.AreEqual(0.30f, range.Snap(0.1f));
            Assert.AreEqual(1.50f, range.Snap(9f));
        }

        [Test]
        public void 刻みの桁数で丸め誤差を除く()
        {
            var range = new SettingRange(0f, 0.60f, 0.01f);
            // 0.01 を 26回足すと float では 0.26 ちょうどにならないが、刻みの桁数で丸めて 0.26f にする。
            float value = 0f;
            for (int i = 0; i < 26; i++) value = range.Offset(value, 1);
            Assert.AreEqual(0.26f, value);
            Assert.AreEqual("0.26", value.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        }

        [Test]
        public void ずらすと範囲の端で止まる()
        {
            var range = new SettingRange(0f, 60f, 1f);
            Assert.AreEqual(18f, range.Offset(17f, 1));
            Assert.AreEqual(0f, range.Offset(0f, -1));
            Assert.AreEqual(60f, range.Offset(60f, 1));
        }

        [Test]
        public void 位置と値の相互変換()
        {
            var range = new SettingRange(-0.50f, 0.50f, 0.01f);
            Assert.AreEqual(0.5f, range.FractionOf(0f), 1e-6f);
            Assert.AreEqual(0f, range.FromFraction(0.5f));
            Assert.AreEqual(-0.50f, range.FromFraction(-1f));
            Assert.AreEqual(0.50f, range.FromFraction(2f));
        }

        [Test]
        public void 範囲の外の値は含まない()
        {
            var range = new SettingRange(0.25f, 0.95f, 0.05f);
            Assert.IsTrue(range.Contains(0.25f));
            Assert.IsTrue(range.Contains(0.95f));
            Assert.IsFalse(range.Contains(0.2f));
            Assert.IsFalse(range.Contains(float.NaN));
        }

        [Test]
        public void 不正な範囲は例外()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SettingRange(1f, 1f, 0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SettingRange(0f, 1f, 0f));
            Assert.Throws<ArgumentException>(() => new SettingRange(0f, 1f, 0.3f));
        }
    }

    public sealed class UserSettingsTests
    {
        [Test]
        public void 既定のしきい値と長押しの時間はCoreの既定値と同じ()
        {
            var core = new KeyboardSettings();
            Assert.AreEqual(core.TriggerThreshold, UserSettings.Default.TriggerThreshold);
            Assert.AreEqual(core.StickThreshold, UserSettings.Default.StickThreshold);
            Assert.AreEqual(core.RepeatDelaySeconds, UserSettings.Default.RepeatDelaySeconds);
            Assert.AreEqual(KeyboardPlacement.Default, UserSettings.Default.Placement);
        }

        [Test]
        public void 既定の位置は試作2の値()
        {
            KeyboardPlacement p = KeyboardPlacement.Default;
            Assert.AreEqual(0.26f, p.DownMeters);
            Assert.AreEqual(0.85f, p.ForwardMeters);
            Assert.AreEqual(0f, p.RightMeters);
            Assert.AreEqual(17f, p.TiltDegrees);
            Assert.AreEqual(0.45f, p.WidthMeters);
        }

        [Test]
        public void Coreの設定にしきい値を渡す()
        {
            KeyboardSettings core = UserSettings.Default.WithTriggerThreshold(0.6f).WithStickThreshold(0.4f).ToKeyboardSettings();
            Assert.AreEqual(0.6f, core.TriggerThreshold);
            Assert.AreEqual(0.4f, core.StickThreshold);
        }

        [Test]
        public void Coreの設定に長押しの時間を10進表記のまま渡す()
        {
            KeyboardSettings core = UserSettings.Default.WithRepeatDelaySeconds(0.35f).ToKeyboardSettings();
            Assert.AreEqual(0.35, core.RepeatDelaySeconds);
            Assert.AreEqual(new KeyboardSettings().RepeatIntervalSeconds, core.RepeatIntervalSeconds);
        }

        [Test]
        public void すべての範囲のしきい値と長押しの時間をCoreが受け付ける()
        {
            Assert.DoesNotThrow(() => UserSettings.Default.WithTriggerThreshold(UserSettings.TriggerThresholdRange.Min)
                .WithStickThreshold(UserSettings.StickThresholdRange.Min)
                .WithRepeatDelaySeconds(UserSettings.RepeatDelaySecondsRange.Min).ToKeyboardSettings());
            Assert.DoesNotThrow(() => UserSettings.Default.WithTriggerThreshold(UserSettings.TriggerThresholdRange.Max)
                .WithStickThreshold(UserSettings.StickThresholdRange.Max)
                .WithRepeatDelaySeconds(UserSettings.RepeatDelaySecondsRange.Max).ToKeyboardSettings());
        }

        [Test]
        public void 範囲外の値は例外()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => UserSettings.Default.WithTriggerThreshold(0.04f));
            Assert.Throws<ArgumentOutOfRangeException>(() => UserSettings.Default.WithStickThreshold(1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => UserSettings.Default.WithRepeatDelaySeconds(0.15f));
            Assert.Throws<ArgumentOutOfRangeException>(() => UserSettings.Default.WithRepeatDelaySeconds(1.05f));
            Assert.Throws<ArgumentOutOfRangeException>(() => UserSettings.Default.WithRepeatDelaySeconds(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => KeyboardPlacement.Default.WithTilt(61f));
            Assert.Throws<ArgumentOutOfRangeException>(() => KeyboardPlacement.Default.WithRight(-0.51f));
            Assert.Throws<ArgumentNullException>(() => UserSettings.Default.WithPlacement(null));
        }

        [Test]
        public void 同じ値なら等しい()
        {
            UserSettings a = UserSettings.Default.WithStickThreshold(0.4f);
            UserSettings b = UserSettings.Default.WithStickThreshold(0.4f);
            Assert.AreEqual(a, b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.AreNotEqual(a, UserSettings.Default);
            Assert.AreNotEqual(UserSettings.Default.WithRepeatDelaySeconds(0.6f), UserSettings.Default);
            Assert.AreNotEqual(UserSettings.Default.WithVRChat(new VRChatSettings(true, false)), UserSettings.Default);
            Assert.AreEqual(UserSettings.Default.WithVRChat(new VRChatSettings(true, true)), UserSettings.Default);
        }

        [Test]
        public void テーマは既定でライム_変えてもほかの値は変わらない()
        {
            Assert.AreEqual(ColorTheme.Lime, UserSettings.Default.Theme);
            UserSettings dark = UserSettings.Default.WithTheme(ColorTheme.Dark);
            Assert.AreEqual(ColorTheme.Dark, dark.Theme);
            Assert.AreNotEqual(UserSettings.Default, dark);
            Assert.AreEqual(UserSettings.Default, dark.WithTheme(ColorTheme.Lime));
            Assert.AreEqual(ColorTheme.Dark, dark.WithStickThreshold(0.4f).WithPlacement(KeyboardPlacement.Default.WithTilt(30f)).Theme);
        }

        [Test]
        public void 未知のテーマは例外()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => UserSettings.Default.WithTheme((ColorTheme)99));
        }

        [Test]
        public void VRChatの設定は既定でどちらもオン()
        {
            Assert.IsTrue(UserSettings.Default.VRChat.FocusOnStartup);
            Assert.IsTrue(UserSettings.Default.VRChat.ShowTypingText);
        }

        [Test]
        public void Googleの変換は既定でオフ_Coreの設定では変換を使えない()
        {
            Assert.IsFalse(UserSettings.Default.Privacy.UseGoogleConversion);
            Assert.IsFalse(UserSettings.Default.ToKeyboardSettings().ConversionEnabled);
            Assert.IsTrue(UserSettings.Default.WithPrivacy(new PrivacySettings(true)).ToKeyboardSettings().ConversionEnabled);
        }

        [Test]
        public void ほかの値を変えても個人情報の設定は変わらない()
        {
            var privacy = new PrivacySettings(true);
            UserSettings settings = UserSettings.Default.WithPrivacy(privacy)
                .WithPlacement(KeyboardPlacement.Default.WithTilt(30f))
                .WithTriggerThreshold(0.6f)
                .WithStickThreshold(0.4f)
                .WithRepeatDelaySeconds(0.7f)
                .WithVRChat(new VRChatSettings(false, false));
            Assert.AreEqual(privacy, settings.Privacy);
            Assert.AreNotEqual(UserSettings.Default.WithPrivacy(privacy), UserSettings.Default);
            Assert.Throws<ArgumentNullException>(() => UserSettings.Default.WithPrivacy(null));
        }

        [Test]
        public void ほかの値を変えてもVRChatの設定は変わらない()
        {
            var vrchat = new VRChatSettings(false, true);
            UserSettings settings = UserSettings.Default.WithVRChat(vrchat)
                .WithPlacement(KeyboardPlacement.Default.WithTilt(30f))
                .WithTriggerThreshold(0.6f)
                .WithStickThreshold(0.4f)
                .WithRepeatDelaySeconds(0.7f);
            Assert.AreEqual(vrchat, settings.VRChat);
            Assert.Throws<ArgumentNullException>(() => UserSettings.Default.WithVRChat(null));
        }
    }

    public sealed class SettingsSerializerTests
    {
        [Test]
        public void 書いて読むと同じ値に戻る()
        {
            UserSettings settings = UserSettings.Default
                .WithPlacement(new KeyboardPlacement(0.31f, 0.72f, -0.12f, 25f, 0.52f))
                .WithTheme(ColorTheme.Lime)
                .WithTriggerThreshold(0.55f)
                .WithStickThreshold(0.35f)
                .WithRepeatDelaySeconds(0.65f)
                .WithVRChat(new VRChatSettings(false, true))
                .WithPrivacy(new PrivacySettings(true));
            Assert.AreEqual(settings, SettingsSerializer.Deserialize(SettingsSerializer.Serialize(settings)));
        }

        [Test]
        public void 値は短い10進表記で書く()
        {
            string json = SettingsSerializer.Serialize(UserSettings.Default);
            StringAssert.Contains("\"downMeters\": 0.26", json);
            StringAssert.DoesNotContain("0.2599", json);
            StringAssert.Contains("\"version\": 1", json);
            StringAssert.Contains("\"repeatDelaySeconds\": 0.5", json);
            StringAssert.Contains("\"focusOnStartup\": true", json);
            StringAssert.Contains("\"showTypingText\": true", json);
            StringAssert.Contains("\"useGoogleConversion\": false", json);
            StringAssert.Contains("\"theme\": \"lime\"", json);
            StringAssert.Contains("\"theme\": \"dark\"", SettingsSerializer.Serialize(UserSettings.Default.WithTheme(ColorTheme.Dark)));
        }

        [Test]
        public void 欠けている項目は既定値()
        {
            UserSettings settings = SettingsSerializer.Deserialize("{\"version\":1,\"display\":{\"tiltDegrees\":30}}");
            Assert.AreEqual(KeyboardPlacement.Default.WithTilt(30f), settings.Placement);
            Assert.AreEqual(UserSettings.Default.TriggerThreshold, settings.TriggerThreshold);
            Assert.AreEqual(UserSettings.Default.RepeatDelaySeconds, settings.RepeatDelaySeconds);

            Assert.AreEqual(VRChatSettings.Default, settings.VRChat);
            Assert.AreEqual(PrivacySettings.Default, settings.Privacy);

            Assert.AreEqual(new VRChatSettings(true, false),
                SettingsSerializer.Deserialize("{\"version\":1,\"vrchat\":{\"showTypingText\":false}}").VRChat);

            Assert.AreEqual(UserSettings.Default, SettingsSerializer.Deserialize("{\"version\":1}"));
            Assert.AreEqual(ColorTheme.Lime, settings.Theme);
            Assert.AreEqual(ColorTheme.Dark, SettingsSerializer.Deserialize("{\"version\":1,\"display\":{\"theme\":\"dark\"}}").Theme);
        }

        [TestCase("")]
        [TestCase("not json")]
        [TestCase("[]")]
        [TestCase("{}")]
        [TestCase("{\"version\":2}")]
        [TestCase("{\"version\":\"1\"}")]
        [TestCase("{\"version\":1,\"display\":[]}")]
        [TestCase("{\"version\":1,\"display\":{\"downMeters\":\"0.3\"}}")]
        [TestCase("{\"version\":1,\"display\":{\"downMeters\":0.61}}")]
        [TestCase("{\"version\":1,\"grip\":{\"triggerThreshold\":0.01}}")]
        [TestCase("{\"version\":1,\"grip\":{\"stickThreshold\":null}}")]
        [TestCase("{\"version\":1,\"grip\":{\"repeatDelaySeconds\":1.5}}")]
        [TestCase("{\"version\":1,\"vrchat\":true}")]
        [TestCase("{\"version\":1,\"vrchat\":{\"focusOnStartup\":\"true\"}}")]
        [TestCase("{\"version\":1,\"vrchat\":{\"showTypingText\":1}}")]
        [TestCase("{\"version\":1,\"vrchat\":{\"showTypingText\":null}}")]
        [TestCase("{\"version\":1,\"privacy\":false}")]
        [TestCase("{\"version\":1,\"privacy\":{\"useGoogleConversion\":\"true\"}}")]
        [TestCase("{\"version\":1,\"display\":{\"theme\":\"blue\"}}")]
        [TestCase("{\"version\":1,\"display\":{\"theme\":\"Lime\"}}")]
        [TestCase("{\"version\":1,\"display\":{\"theme\":1}}")]
        [TestCase("{\"version\":1,\"display\":{\"theme\":null}}")]
        public void 正しくない内容はFormatException(string json)
        {
            Assert.Throws<FormatException>(() => SettingsSerializer.Deserialize(json));
        }
    }

    public sealed class SettingsStoreTests
    {
        string _directory;
        string _path;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "VRimeTests-" + Guid.NewGuid().ToString("N"));
            _path = Path.Combine(_directory, "sub", SettingsStore.FileName);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        [Test]
        public void ファイルがなければ既定値で通知なし()
        {
            SettingsLoadResult result = new SettingsStore(_path).Load();
            Assert.AreEqual(UserSettings.Default, result.Settings);
            Assert.IsNull(result.Notice);
            Assert.IsNull(result.Error);
        }

        [Test]
        public void 保存して読み込む()
        {
            var store = new SettingsStore(_path);
            UserSettings settings = UserSettings.Default.WithTriggerThreshold(0.8f);
            store.Save(settings);
            Assert.AreEqual(settings, store.Load().Settings);

            // 既存のファイルの置き換え。
            UserSettings changed = settings.WithStickThreshold(0.6f);
            store.Save(changed);
            Assert.AreEqual(changed, store.Load().Settings);
            Assert.IsFalse(File.Exists(_path + ".tmp"));
        }

        [Test]
        public void 正しくないファイルは別名に移して既定値と通知を返す()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.WriteAllText(_path, "{ broken");

            SettingsLoadResult result = new SettingsStore(_path).Load();
            Assert.AreEqual(UserSettings.Default, result.Settings);
            Assert.IsNotNull(result.Notice);
            Assert.IsInstanceOf<FormatException>(result.Error);
            Assert.IsFalse(File.Exists(_path));

            string[] moved = Directory.GetFiles(Path.GetDirectoryName(_path), "settings.invalid-*.json");
            Assert.AreEqual(1, moved.Length);
            Assert.AreEqual("{ broken", File.ReadAllText(moved[0]));
            StringAssert.Contains(Path.GetFileName(moved[0]), result.Notice);
        }

        [Test]
        public void 移した先の名前は重ならない()
        {
            string directory = Path.GetDirectoryName(_path);
            Directory.CreateDirectory(directory);
            for (int i = 0; i < 2; i++)
            {
                File.WriteAllText(_path, "{ broken " + i);
                new SettingsStore(_path).Load();
            }
            Assert.AreEqual(2, Directory.GetFiles(directory, "settings.invalid-*.json").Length);
        }

        [Test]
        public void 相対パスは例外()
        {
            Assert.Throws<ArgumentException>(() => new SettingsStore("settings.json"));
        }
    }

    /// <summary>
    /// シーンの設定画面と同じ大きさ（1280×872）の配置。左にカテゴリのタブを縦に並べ、右に選んでいるカテゴリのページを置く。
    /// ページはどれも同じ位置から行を並べるため、別のページの行どうしは重なる。
    /// 同意の画面はどれも同じ位置に置き、ボタンはページの行と重なる。
    /// 選択肢の一覧はボタンの下に開き、後ろの行と重なる。
    /// </summary>
    internal static class TestSettingsLayout
    {
        public static readonly Vector2 Size = new Vector2(1280f, 872f);
        public static readonly Rect Reset = new Rect(992f, 804f, 240f, 52f);
        public static readonly Rect Quit = new Rect(48f, 804f, 220f, 52f);
        public static readonly Rect ConsentAccept = new Rect(560f, 144f, 360f, 64f);
        public static readonly Rect ConsentCancel = new Rect(940f, 144f, 240f, 64f);
        public const float FirstRowTop = 144f;
        const float RowHeight = 72f;
        const float OptionHeight = 56f;

        public static SettingsLayout Create()
        {
            var pages = new List<SettingsPageLayout>();
            for (int i = 0; i < SettingsCatalog.Categories.Count; i++) pages.Add(Page(SettingsCatalog.Categories[i], i));
            return Create(pages.ToArray());
        }

        /// <summary>pages と、pages にある同意が必要な項目の同意の画面からなる配置。</summary>
        public static SettingsLayout Create(params SettingsPageLayout[] pages)
        {
            var consents = new List<SettingsConsentLayout>();
            foreach (SettingsPageLayout page in pages)
            {
                foreach (SettingsToggleLayout toggle in page.Toggles)
                {
                    if (toggle.Item is ToggleSettingItem stored && stored.RequiresConsentToEnable) consents.Add(Consent(stored));
                }
            }
            return new SettingsLayout(Size, pages, Reset, Quit, consents);
        }

        public static SettingsConsentLayout Consent(ToggleSettingItem item) => new SettingsConsentLayout(item, ConsentAccept, ConsentCancel);

        /// <summary>カテゴリの項目を、カテゴリの順に 1 行ずつ上から並べたページ。</summary>
        public static SettingsPageLayout Page(SettingsCategory category, int tabIndex)
        {
            var rows = new List<SettingsRowLayout>();
            var toggles = new List<SettingsToggleLayout>();
            var dropdowns = new List<SettingsDropdownLayout>();
            for (int i = 0; i < category.Items.Count; i++)
            {
                float top = FirstRowTop + RowHeight * i;
                switch (category.Items[i])
                {
                    case NumberSettingItem number:
                        rows.Add(Row(number, top));
                        break;
                    case SwitchSettingItem toggle:
                        toggles.Add(Toggle(toggle, top));
                        break;
                    case ChoiceSettingItem choice:
                        dropdowns.Add(Dropdown(choice, top));
                        break;
                    default:
                        throw new InvalidOperationException("未知の種類の項目です: " + category.Items[i].Key);
                }
            }
            return new SettingsPageLayout(category, Tab(tabIndex), rows, toggles, dropdowns);
        }

        public static Rect Tab(int index) => new Rect(48f, FirstRowTop + 80f * index, 220f, 64f);

        public static SettingsRowLayout Row(NumberSettingItem item, float top)
        {
            var slider = new Rect(680f, top, 440f, RowHeight);
            return new SettingsRowLayout(item,
                new Rect(600f, top + 4f, 64f, 64f),
                slider,
                new Rect(1136f, top + 4f, 64f, 64f),
                new Rect(slider.xMin + 18f, slider.center.y - 5f, slider.width - 36f, 10f));
        }

        public static SettingsToggleLayout Toggle(SwitchSettingItem item, float top) =>
            new SettingsToggleLayout(item, new Rect(300f, top, 900f, RowHeight));

        /// <summary>ボタンの下に、選択肢を縦に並べた一覧を開く行。</summary>
        public static SettingsDropdownLayout Dropdown(ChoiceSettingItem item, float top)
        {
            var options = new List<Rect>();
            for (int i = 0; i < item.Labels.Count; i++) options.Add(new Rect(600f, top + RowHeight + 4f + OptionHeight * i, 500f, OptionHeight));
            return new SettingsDropdownLayout(item, new Rect(600f, top + 4f, 500f, 64f), options);
        }

        /// <summary>数値の行の左（項目名の位置）。操作の対象がない。</summary>
        public static Vector2 LabelPoint(SettingsRowLayout row) => new Vector2(420f, row.Slider.center.y);
    }

    public sealed class SettingsLayoutTests
    {
        readonly SettingsLayout _layout = TestSettingsLayout.Create();

        static SettingsCategory Display => SettingsCatalog.FindCategory("display");
        static SettingsCategory Grip => SettingsCatalog.FindCategory("grip");
        static SettingsCategory Integration => SettingsCatalog.FindCategory("integration");
        static SettingsCategory Privacy => SettingsCatalog.FindCategory("privacy");

        [Test]
        public void 当たり判定()
        {
            SettingsRowLayout row = _layout.PageOf(Display).Rows[2];
            Assert.AreEqual(SettingsHit.Slider(row.Item), _layout.HitTest(row.Slider.center, Display, null, null));
            Assert.AreEqual(SettingsHit.Decrease(row.Item), _layout.HitTest(row.Decrease.center, Display, null, null));
            Assert.AreEqual(SettingsHit.Increase(row.Item), _layout.HitTest(row.Increase.center, Display, null, null));
            Assert.AreEqual(SettingsHit.Reset, _layout.HitTest(_layout.Reset.center, Display, null, null));
            Assert.AreEqual(SettingsHit.Quit, _layout.HitTest(_layout.Quit.center, Display, null, null));
            Assert.AreEqual(SettingsHit.None, _layout.HitTest(TestSettingsLayout.LabelPoint(row), Display, null, null));
            Assert.AreEqual(SettingsHit.None, _layout.HitTest(new Vector2(-10f, -10f), Display, null, null));

            SettingsToggleLayout toggle = _layout.PageOf(Integration).Toggles[1];
            Assert.AreEqual(SettingsHit.Toggle(toggle.Item), _layout.HitTest(toggle.Area.center, Integration, null, null));
        }

        [Test]
        public void タブはどのページを表示していても当たる()
        {
            foreach (SettingsPageLayout page in _layout.Pages)
            {
                foreach (SettingsCategory current in SettingsCatalog.Categories)
                    Assert.AreEqual(SettingsHit.Tab(page.Category), _layout.HitTest(page.Tab.center, current, null, null), page.Category.Key + " / " + current.Key);
            }
        }

        [Test]
        public void 表示しているページの対象だけに当たる()
        {
            // 別のページの対象どうしは同じ位置に重ねて置いてよい。
            SettingsRowLayout down = _layout.PageOf(Display).Rows[0];
            SettingsToggleLayout typing = _layout.PageOf(Integration).Toggles[1];
            Assert.IsTrue(down.Slider.Overlaps(typing.Area));
            Assert.AreEqual(SettingsHit.Slider(down.Item), _layout.HitTest(down.Slider.center, Display, null, null));
            Assert.AreEqual(SettingsHit.Toggle(typing.Item), _layout.HitTest(down.Slider.center, Integration, null, null));

            // grip のページは 3 行のため、display の最後の行の位置には対象がない。
            SettingsRowLayout width = _layout.PageOf(Display).Rows[4];
            Assert.AreEqual(SettingsHit.None, _layout.HitTest(width.Increase.center, Grip, null, null));
        }

        [Test]
        public void スライダーの位置は値の範囲の両端で0と1()
        {
            SettingsRowLayout row = _layout.PageOf(Display).Rows[0];
            Assert.AreEqual(0f, row.FractionAt(row.Track.xMin));
            Assert.AreEqual(1f, row.FractionAt(row.Track.xMax));
            Assert.AreEqual(0f, row.FractionAt(row.Slider.xMin));
            Assert.AreEqual(1f, row.FractionAt(row.Slider.xMax));
            Assert.AreEqual(0.5f, row.FractionAt(row.Track.center.x), 1e-6f);
        }

        [Test]
        public void 隣り合う行は接していても重ならない()
        {
            IReadOnlyList<SettingsRowLayout> rows = _layout.PageOf(Display).Rows;
            Assert.AreEqual(rows[0].Slider.yMax, rows[1].Slider.yMin);
            Assert.DoesNotThrow(() => TestSettingsLayout.Create());
        }

        [Test]
        public void 同じページの操作の対象が重なると例外()
        {
            var page = new SettingsPageLayout(Grip, TestSettingsLayout.Tab(0), new List<SettingsRowLayout>
            {
                TestSettingsLayout.Row(SettingsCatalog.FindNumber("triggerThreshold"), 144f),
                TestSettingsLayout.Row(SettingsCatalog.FindNumber("stickThreshold"), 184f),
                TestSettingsLayout.Row(SettingsCatalog.FindNumber("repeatDelaySeconds"), 400f),
            }, Array.Empty<SettingsToggleLayout>(), Array.Empty<SettingsDropdownLayout>());
            Assert.Throws<ArgumentException>(() => TestSettingsLayout.Create(page));
        }

        [Test]
        public void タブとページの操作の対象が重なると例外()
        {
            var page = new SettingsPageLayout(Integration, TestSettingsLayout.Tab(0), Array.Empty<SettingsRowLayout>(), new List<SettingsToggleLayout>
            {
                new SettingsToggleLayout(SettingsCatalog.FindToggle("focusOnStartup"), new Rect(200f, 144f, 900f, 72f)),
                TestSettingsLayout.Toggle(SettingsCatalog.FindToggle("showTypingText"), 216f),
                TestSettingsLayout.Toggle(SettingsCatalog.AutoLaunch, 288f),
            }, Array.Empty<SettingsDropdownLayout>());
            Assert.Throws<ArgumentException>(() => TestSettingsLayout.Create(page));
        }

        [Test]
        public void タブどうしが重なると例外()
        {
            SettingsPageLayout display = TestSettingsLayout.Page(Display, 0);
            var grip = new SettingsPageLayout(Grip, new Rect(48f, 180f, 220f, 64f), TestSettingsLayout.Page(Grip, 1).Rows,
                Array.Empty<SettingsToggleLayout>(), Array.Empty<SettingsDropdownLayout>());
            Assert.Throws<ArgumentException>(() => TestSettingsLayout.Create(display, grip));
        }

        [Test]
        public void 操作の対象が画面の外にあると例外()
        {
            var page = new SettingsPageLayout(Grip, TestSettingsLayout.Tab(0), new List<SettingsRowLayout>
            {
                TestSettingsLayout.Row(SettingsCatalog.FindNumber("triggerThreshold"), 144f),
                TestSettingsLayout.Row(SettingsCatalog.FindNumber("stickThreshold"), 216f),
                TestSettingsLayout.Row(SettingsCatalog.FindNumber("repeatDelaySeconds"), 820f),
            }, Array.Empty<SettingsToggleLayout>(), Array.Empty<SettingsDropdownLayout>());
            Assert.Throws<ArgumentException>(() => TestSettingsLayout.Create(page));
        }

        [Test]
        public void 操作の対象の大きさが正でないと例外()
        {
            var pages = new[] { TestSettingsLayout.Page(Display, 0) };
            Assert.Throws<ArgumentException>(() => new SettingsLayout(TestSettingsLayout.Size, pages, new Rect(992f, 804f, 0f, 52f),
                TestSettingsLayout.Quit, Array.Empty<SettingsConsentLayout>()));
            Assert.Throws<ArgumentException>(() => new SettingsLayout(TestSettingsLayout.Size, pages, TestSettingsLayout.Reset,
                new Rect(48f, 804f, 220f, 0f), Array.Empty<SettingsConsentLayout>()));
        }

        [Test]
        public void 終了がほかの操作の対象と重なると例外()
        {
            var pages = new[] { TestSettingsLayout.Page(Display, 0) };
            Assert.Throws<ArgumentException>(() => new SettingsLayout(TestSettingsLayout.Size, pages, TestSettingsLayout.Reset,
                new Rect(900f, 804f, 220f, 52f), Array.Empty<SettingsConsentLayout>()));
            Assert.Throws<ArgumentException>(() => new SettingsLayout(TestSettingsLayout.Size, pages, TestSettingsLayout.Reset,
                TestSettingsLayout.Tab(0), Array.Empty<SettingsConsentLayout>()));
        }

        [Test]
        public void 同意の画面を開いている間はその画面のボタンだけに当たる()
        {
            ToggleSettingItem item = SettingsCatalog.FindToggle("useGoogleConversion");
            SettingsConsentLayout consent = _layout.ConsentOf(item);
            Assert.AreEqual(SettingsHit.ConsentAccept, _layout.HitTest(consent.Accept.center, Privacy, item, null));
            Assert.AreEqual(SettingsHit.ConsentCancel, _layout.HitTest(consent.Cancel.center, Privacy, item, null));

            // ページの行・タブ・「既定に戻す」・「アプリを終了」は、同意の画面の後ろにあっても当たらない。
            SettingsToggleLayout row = _layout.PageOf(Privacy).Toggles[0];
            Assert.IsTrue(row.Area.Overlaps(consent.Accept));
            Assert.AreEqual(SettingsHit.Toggle(item), _layout.HitTest(consent.Accept.center, Privacy, null, null));
            Assert.AreEqual(SettingsHit.None, _layout.HitTest(new Vector2(row.Area.xMin + 10f, row.Area.center.y), Privacy, item, null));
            Assert.AreEqual(SettingsHit.None, _layout.HitTest(_layout.PageOf(Display).Tab.center, Privacy, item, null));
            Assert.AreEqual(SettingsHit.None, _layout.HitTest(_layout.Reset.center, Privacy, item, null));
            Assert.AreEqual(SettingsHit.None, _layout.HitTest(_layout.Quit.center, Privacy, item, null));
        }

        [Test]
        public void ドロップダウンを開いている間は選択肢だけに当たり_ほかの場所は閉じる対象になる()
        {
            ChoiceSettingItem theme = SettingsCatalog.FindChoice("theme");
            SettingsDropdownLayout dropdown = _layout.PageOf(Display).DropdownOf(theme);
            Assert.AreEqual(SettingsHit.Dropdown(theme), _layout.HitTest(dropdown.Button.center, Display, null, null));

            Assert.AreEqual(SettingsHit.Option(theme, 0), _layout.HitTest(dropdown.Options[0].center, Display, null, theme));
            Assert.AreEqual(SettingsHit.Option(theme, 1), _layout.HitTest(dropdown.Options[1].center, Display, null, theme));

            // 一覧の後ろの行・ボタン自身・タブ・「既定に戻す」は、開いている間は閉じる対象になる。
            SettingsRowLayout down = _layout.PageOf(Display).Rows[0];
            Assert.IsTrue(down.Slider.Overlaps(dropdown.Options[0]));
            Assert.AreEqual(SettingsHit.DropdownDismiss, _layout.HitTest(new Vector2(down.Slider.center.x, dropdown.Options[1].yMax + 4f), Display, null, theme));
            Assert.AreEqual(SettingsHit.DropdownDismiss, _layout.HitTest(dropdown.Button.center, Display, null, theme));
            Assert.AreEqual(SettingsHit.DropdownDismiss, _layout.HitTest(_layout.PageOf(Grip).Tab.center, Display, null, theme));
            Assert.AreEqual(SettingsHit.DropdownDismiss, _layout.HitTest(_layout.Reset.center, Display, null, theme));
        }

        [Test]
        public void 同意の画面と選択肢の一覧を同時に開く_または表示していないページの一覧を開くと例外()
        {
            ChoiceSettingItem theme = SettingsCatalog.FindChoice("theme");
            ToggleSettingItem google = SettingsCatalog.FindToggle("useGoogleConversion");
            Assert.Throws<ArgumentException>(() => _layout.HitTest(Vector2.zero, Privacy, google, theme));
            Assert.Throws<ArgumentException>(() => _layout.HitTest(Vector2.zero, Grip, null, theme));
        }

        [Test]
        public void 選択肢の数が項目と違う_選択肢どうしが重なる_または画面の外にあると例外()
        {
            ChoiceSettingItem theme = SettingsCatalog.FindChoice("theme");
            var button = new Rect(600f, 148f, 500f, 64f);
            Assert.Throws<ArgumentException>(() => new SettingsDropdownLayout(theme, button, new[] { new Rect(600f, 220f, 500f, 56f) }));

            SettingsPageLayout DisplayWith(params Rect[] options)
            {
                var rows = new List<SettingsRowLayout>();
                NumberSettingItem[] numbers = { SettingsCatalog.FindNumber("downMeters"), SettingsCatalog.FindNumber("forwardMeters"),
                    SettingsCatalog.FindNumber("rightMeters"), SettingsCatalog.FindNumber("tiltDegrees"), SettingsCatalog.FindNumber("widthMeters") };
                for (int i = 0; i < numbers.Length; i++) rows.Add(TestSettingsLayout.Row(numbers[i], 216f + 72f * i));
                return new SettingsPageLayout(Display, TestSettingsLayout.Tab(0), rows, Array.Empty<SettingsToggleLayout>(),
                    new[] { new SettingsDropdownLayout(theme, button, options) });
            }

            Assert.DoesNotThrow(() => TestSettingsLayout.Create(DisplayWith(new Rect(600f, 220f, 500f, 56f), new Rect(600f, 276f, 500f, 56f))));
            Assert.Throws<ArgumentException>(() => TestSettingsLayout.Create(DisplayWith(new Rect(600f, 220f, 500f, 56f), new Rect(600f, 250f, 500f, 56f))));
            Assert.Throws<ArgumentException>(() => TestSettingsLayout.Create(DisplayWith(new Rect(600f, 220f, 500f, 56f), new Rect(900f, 276f, 500f, 56f))));
        }

        [Test]
        public void ドロップダウンのボタンがページの対象と重なると例外()
        {
            ChoiceSettingItem theme = SettingsCatalog.FindChoice("theme");
            SettingsPageLayout page = TestSettingsLayout.Page(Display, 0);
            SettingsRowLayout down = page.Rows[0];
            var overlapping = new SettingsDropdownLayout(theme, down.Slider, page.Dropdowns[0].Options);
            var display = new SettingsPageLayout(Display, page.Tab, page.Rows, page.Toggles, new[] { overlapping });
            Assert.Throws<ArgumentException>(() => TestSettingsLayout.Create(display));
        }

        [Test]
        public void 同意の画面のボタンどうしが重なる_または画面の外にあると例外()
        {
            SettingsPageLayout page = TestSettingsLayout.Page(Privacy, 0);
            var item = (ToggleSettingItem)page.Toggles[0].Item;
            Assert.Throws<ArgumentException>(() => new SettingsLayout(TestSettingsLayout.Size, new[] { page }, TestSettingsLayout.Reset, TestSettingsLayout.Quit,
                new[] { new SettingsConsentLayout(item, new Rect(560f, 144f, 360f, 64f), new Rect(900f, 144f, 240f, 64f)) }));
            Assert.Throws<ArgumentException>(() => new SettingsLayout(TestSettingsLayout.Size, new[] { page }, TestSettingsLayout.Reset, TestSettingsLayout.Quit,
                new[] { new SettingsConsentLayout(item, TestSettingsLayout.ConsentAccept, new Rect(1100f, 144f, 240f, 64f)) }));
        }

        [Test]
        public void 同意が必要な項目の同意の画面の過不足は例外()
        {
            SettingsPageLayout privacy = TestSettingsLayout.Page(Privacy, 0);
            SettingsPageLayout grip = TestSettingsLayout.Page(Grip, 1);
            SettingsConsentLayout consent = TestSettingsLayout.Consent((ToggleSettingItem)privacy.Toggles[0].Item);
            Assert.Throws<ArgumentException>(() => new SettingsLayout(TestSettingsLayout.Size, new[] { privacy }, TestSettingsLayout.Reset, TestSettingsLayout.Quit,
                Array.Empty<SettingsConsentLayout>()));
            Assert.Throws<ArgumentException>(() => new SettingsLayout(TestSettingsLayout.Size, new[] { grip }, TestSettingsLayout.Reset, TestSettingsLayout.Quit,
                new[] { consent }));
            Assert.Throws<ArgumentException>(() => new SettingsLayout(TestSettingsLayout.Size, new[] { privacy }, TestSettingsLayout.Reset, TestSettingsLayout.Quit,
                new[] { consent, consent }));
        }

        [Test]
        public void 同意が必要でない項目の同意の画面は例外()
        {
            Assert.Throws<ArgumentException>(() => TestSettingsLayout.Consent(SettingsCatalog.FindToggle("focusOnStartup")));
            SettingsLayout layout = TestSettingsLayout.Create(TestSettingsLayout.Page(Integration, 0));
            Assert.Throws<ArgumentException>(() => layout.ConsentOf(SettingsCatalog.FindToggle("useGoogleConversion")));
        }

        [Test]
        public void ページがない_または同じカテゴリのページが重複すると例外()
        {
            Assert.Throws<ArgumentException>(() => TestSettingsLayout.Create(Array.Empty<SettingsPageLayout>()));
            Assert.Throws<ArgumentException>(() => TestSettingsLayout.Create(TestSettingsLayout.Page(Display, 0), TestSettingsLayout.Page(Display, 1)));
        }

        [Test]
        public void 同じ項目の行が重複すると例外()
        {
            NumberSettingItem trigger = SettingsCatalog.FindNumber("triggerThreshold");
            var rows = new List<SettingsRowLayout>(TestSettingsLayout.Page(Grip, 0).Rows) { TestSettingsLayout.Row(trigger, 400f) };
            Assert.Throws<ArgumentException>(() => new SettingsPageLayout(Grip, TestSettingsLayout.Tab(0), rows, Array.Empty<SettingsToggleLayout>(), Array.Empty<SettingsDropdownLayout>()));
        }

        [Test]
        public void ページにカテゴリの項目の行が欠けていると例外()
        {
            var rows = new List<SettingsRowLayout> { TestSettingsLayout.Row(SettingsCatalog.FindNumber("triggerThreshold"), 144f) };
            Assert.Throws<ArgumentException>(() => new SettingsPageLayout(Grip, TestSettingsLayout.Tab(0), rows, Array.Empty<SettingsToggleLayout>(), Array.Empty<SettingsDropdownLayout>()));
        }

        [Test]
        public void ほかのカテゴリの項目の行は例外()
        {
            var rows = new List<SettingsRowLayout>(TestSettingsLayout.Page(Grip, 0).Rows) { TestSettingsLayout.Row(SettingsCatalog.FindNumber("downMeters"), 400f) };
            Assert.Throws<ArgumentException>(() => new SettingsPageLayout(Grip, TestSettingsLayout.Tab(0), rows, Array.Empty<SettingsToggleLayout>(), Array.Empty<SettingsDropdownLayout>()));
        }

        [Test]
        public void 値の範囲の幅が正でないと例外()
        {
            NumberSettingItem item = SettingsCatalog.FindNumber("downMeters");
            Assert.Throws<ArgumentException>(() => new SettingsRowLayout(item,
                new Rect(600f, 148f, 64f, 64f), new Rect(680f, 144f, 440f, 72f), new Rect(1136f, 148f, 64f, 64f), new Rect(698f, 175f, 0f, 10f)));
        }

        [Test]
        public void 配置にない項目の行とカテゴリのページは例外()
        {
            SettingsLayout layout = TestSettingsLayout.Create(TestSettingsLayout.Page(Display, 0));
            Assert.Throws<ArgumentException>(() => layout.RowOf(SettingsCatalog.FindNumber("triggerThreshold")));
            Assert.Throws<ArgumentException>(() => layout.PageOf(Grip));
            Assert.Throws<ArgumentException>(() => layout.HitTest(Vector2.zero, Grip, null, null));
        }

        [Test]
        public void 当たり判定の結果から別の種類の項目を取ると例外()
        {
            SettingsHit toggle = SettingsHit.Toggle(SettingsCatalog.FindToggle("focusOnStartup"));
            SettingsHit slider = SettingsHit.Slider(SettingsCatalog.FindNumber("downMeters"));
            Assert.Throws<InvalidOperationException>(() => _ = toggle.NumberItem);
            Assert.Throws<InvalidOperationException>(() => _ = slider.ToggleItem);
            Assert.Throws<InvalidOperationException>(() => _ = SettingsHit.Reset.NumberItem);
            Assert.Throws<InvalidOperationException>(() => _ = slider.ChoiceItem);
            Assert.Throws<InvalidOperationException>(() => _ = SettingsHit.Dropdown(SettingsCatalog.FindChoice("theme")).OptionIndex);
        }

        [Test]
        public void 選択肢は位置で区別し_範囲外の位置は例外()
        {
            ChoiceSettingItem theme = SettingsCatalog.FindChoice("theme");
            Assert.AreEqual(SettingsHit.Option(theme, 1), SettingsHit.Option(theme, 1));
            Assert.AreNotEqual(SettingsHit.Option(theme, 0), SettingsHit.Option(theme, 1));
            Assert.AreEqual(1, SettingsHit.Option(theme, 1).OptionIndex);
            Assert.Throws<ArgumentOutOfRangeException>(() => SettingsHit.Option(theme, 2));
        }
    }

    public sealed class SettingsCatalogTests
    {
        [Test]
        public void 値の表示()
        {
            NumberSettingItem down = SettingsCatalog.FindNumber("downMeters");
            NumberSettingItem right = SettingsCatalog.FindNumber("rightMeters");
            NumberSettingItem tilt = SettingsCatalog.FindNumber("tiltDegrees");
            NumberSettingItem trigger = SettingsCatalog.FindNumber("triggerThreshold");
            NumberSettingItem repeat = SettingsCatalog.FindNumber("repeatDelaySeconds");
            Assert.AreEqual("26 cm", down.Format(0.26f));
            Assert.AreEqual("中央", right.Format(0f));
            Assert.AreEqual("右 12 cm", right.Format(0.12f));
            Assert.AreEqual("左 5 cm", right.Format(-0.05f));
            Assert.AreEqual("17°", tilt.Format(17f));
            Assert.AreEqual("70%", trigger.Format(0.7f));
            Assert.AreEqual("0.50 秒", repeat.Format(0.5f));
            Assert.AreEqual("0.35 秒", repeat.Format(0.35f));
        }

        [Test]
        public void 項目ごとに対応する値を読み書きする()
        {
            UserSettings s = UserSettings.Default;
            NumberSettingItem down = SettingsCatalog.FindNumber("downMeters");
            Assert.AreEqual(0.30f, down.Get(down.Set(s, 0.30f)));
            Assert.AreEqual(1.20f, SettingsCatalog.FindNumber("forwardMeters").Set(s, 1.20f).Placement.ForwardMeters);
            Assert.AreEqual(-0.10f, SettingsCatalog.FindNumber("rightMeters").Set(s, -0.10f).Placement.RightMeters);
            Assert.AreEqual(40f, SettingsCatalog.FindNumber("tiltDegrees").Set(s, 40f).Placement.TiltDegrees);
            Assert.AreEqual(0.60f, SettingsCatalog.FindNumber("widthMeters").Set(s, 0.60f).Placement.WidthMeters);
            Assert.AreEqual(0.50f, SettingsCatalog.FindNumber("triggerThreshold").Set(s, 0.50f).TriggerThreshold);
            Assert.AreEqual(0.65f, SettingsCatalog.FindNumber("stickThreshold").Set(s, 0.65f).StickThreshold);
            Assert.AreEqual(0.75f, SettingsCatalog.FindNumber("repeatDelaySeconds").Set(s, 0.75f).RepeatDelaySeconds);

            ToggleSettingItem focus = SettingsCatalog.FindToggle("focusOnStartup");
            ToggleSettingItem typing = SettingsCatalog.FindToggle("showTypingText");
            Assert.AreEqual(new VRChatSettings(false, true), focus.Set(s, false).VRChat);
            Assert.AreEqual(new VRChatSettings(true, false), typing.Set(s, false).VRChat);
            Assert.IsFalse(typing.Get(typing.Set(s, false)));

            ToggleSettingItem google = SettingsCatalog.FindToggle("useGoogleConversion");
            Assert.AreEqual(new PrivacySettings(true), google.Set(s, true).Privacy);
            Assert.AreEqual(s.VRChat, google.Set(s, true).VRChat);
        }

        [Test]
        public void テーマの選択肢と値の読み書き()
        {
            ChoiceSettingItem theme = SettingsCatalog.FindChoice("theme");
            CollectionAssert.AreEqual(new[] { "ライム", "ダーク" }, theme.Labels);
            Assert.AreEqual(0, theme.GetIndex(UserSettings.Default));
            UserSettings dark = theme.SetIndex(UserSettings.Default, 1);
            Assert.AreEqual(UserSettings.Default.WithTheme(ColorTheme.Dark), dark);
            Assert.AreEqual(1, theme.GetIndex(dark));
            Assert.AreEqual(UserSettings.Default, theme.ResetToDefault(dark));
            Assert.Throws<ArgumentOutOfRangeException>(() => theme.SetIndex(UserSettings.Default, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => theme.SetIndex(UserSettings.Default, -1));
        }

        [Test]
        public void 選択肢が2つ未満_または値か表示名が重複する項目は例外()
        {
            Func<UserSettings, ColorTheme> get = s => s.Theme;
            Func<UserSettings, ColorTheme, UserSettings> set = (s, v) => s.WithTheme(v);
            Assert.Throws<ArgumentException>(() => ChoiceSettingItem.Create("one", new[] { (ColorTheme.Dark, "ダーク") }, get, set));
            Assert.Throws<ArgumentException>(() => ChoiceSettingItem.Create("value", new[] { (ColorTheme.Dark, "ダーク"), (ColorTheme.Dark, "ライム") }, get, set));
            Assert.Throws<ArgumentException>(() => ChoiceSettingItem.Create("label", new[] { (ColorTheme.Dark, "ダーク"), (ColorTheme.Lime, "ダーク") }, get, set));
            Assert.Throws<ArgumentException>(() => ChoiceSettingItem.Create("empty", new[] { (ColorTheme.Dark, "ダーク"), (ColorTheme.Lime, "") }, get, set));
        }

        [Test]
        public void 選択肢にない値を読むと例外()
        {
            ChoiceSettingItem item = ChoiceSettingItem.Create("partial", new[] { (ColorTheme.Dark, "ダーク"), ((ColorTheme)99, "ほか") },
                s => s.Theme, (s, v) => s);
            Assert.Throws<InvalidOperationException>(() => item.GetIndex(UserSettings.Default.WithTheme(ColorTheme.Lime)));
        }

        [Test]
        public void スイッチは押すたびに切り替わる()
        {
            ToggleSettingItem focus = SettingsCatalog.FindToggle("focusOnStartup");
            UserSettings once = focus.Toggle(UserSettings.Default);
            Assert.IsFalse(once.VRChat.FocusOnStartup);
            Assert.AreEqual(UserSettings.Default, focus.Toggle(once));
        }

        [Test]
        public void 設定する値は刻みに揃える()
        {
            Assert.AreEqual(0.55f, SettingsCatalog.FindNumber("triggerThreshold").Set(UserSettings.Default, 0.56f).TriggerThreshold);
        }

        [Test]
        public void カテゴリの並びと項目()
        {
            CollectionAssert.AreEqual(new[] { "display", "grip", "integration", "privacy" }, Keys(SettingsCatalog.Categories, c => c.Key));
            CollectionAssert.AreEqual(new[] { "theme", "downMeters", "forwardMeters", "rightMeters", "tiltDegrees", "widthMeters" },
                Keys(SettingsCatalog.FindCategory("display").Items, i => i.Key));
            CollectionAssert.AreEqual(new[] { "triggerThreshold", "stickThreshold", "repeatDelaySeconds" },
                Keys(SettingsCatalog.FindCategory("grip").Items, i => i.Key));
            CollectionAssert.AreEqual(new[] { "focusOnStartup", "showTypingText", "autoLaunch" },
                Keys(SettingsCatalog.FindCategory("integration").Items, i => i.Key));
            CollectionAssert.AreEqual(new[] { "useGoogleConversion" },
                Keys(SettingsCatalog.FindCategory("privacy").Items, i => i.Key));
        }

        [Test]
        public void 位置の確認用のキーボードは表示位置のカテゴリだけで表示する()
        {
            foreach (SettingsCategory category in SettingsCatalog.Categories)
                Assert.AreEqual(category.Key == "display", category.ShowsKeyboardPreview, category.Key);
        }

        [Test]
        public void 同意が必要な項目はGoogleの変換だけ()
        {
            foreach (SettingItem item in SettingsCatalog.Items)
            {
                bool requiresConsent = item is ToggleSettingItem toggle && toggle.RequiresConsentToEnable;
                Assert.AreEqual(item.Key == "useGoogleConversion", requiresConsent, item.Key);
            }
        }

        [Test]
        public void 項目のキーは重複せずどれか1つのカテゴリに属する()
        {
            var keys = new HashSet<string>();
            foreach (SettingItem item in SettingsCatalog.Items) Assert.IsTrue(keys.Add(item.Key), item.Key);
            int count = 0;
            foreach (SettingsCategory category in SettingsCatalog.Categories) count += category.Items.Count;
            Assert.AreEqual(count, SettingsCatalog.Items.Count);
        }

        [Test]
        public void カテゴリを既定に戻すとそのカテゴリの項目だけが戻る()
        {
            UserSettings changed = UserSettings.Default
                .WithPlacement(KeyboardPlacement.Default.WithTilt(30f))
                .WithTheme(ColorTheme.Dark)
                .WithStickThreshold(0.8f)
                .WithRepeatDelaySeconds(0.7f)
                .WithVRChat(new VRChatSettings(false, false))
                .WithPrivacy(new PrivacySettings(true));

            UserSettings display = SettingsCatalog.FindCategory("display").ResetToDefault(changed);
            Assert.AreEqual(changed.WithPlacement(KeyboardPlacement.Default).WithTheme(ColorTheme.Lime), display);

            UserSettings grip = SettingsCatalog.FindCategory("grip").ResetToDefault(changed);
            Assert.AreEqual(UserSettings.Default.StickThreshold, grip.StickThreshold);
            Assert.AreEqual(UserSettings.Default.RepeatDelaySeconds, grip.RepeatDelaySeconds);
            Assert.AreEqual(changed.Placement, grip.Placement);
            Assert.AreEqual(changed.Theme, grip.Theme);
            Assert.AreEqual(changed.VRChat, grip.VRChat);
            Assert.AreEqual(changed.Privacy, grip.Privacy);

            UserSettings vrchat = SettingsCatalog.FindCategory("integration").ResetToDefault(changed);
            Assert.AreEqual(changed.WithVRChat(VRChatSettings.Default), vrchat);

            UserSettings privacy = SettingsCatalog.FindCategory("privacy").ResetToDefault(changed);
            Assert.AreEqual(changed.WithPrivacy(PrivacySettings.Default), privacy);

            UserSettings all = changed;
            foreach (SettingsCategory category in SettingsCatalog.Categories) all = category.ResetToDefault(all);
            Assert.AreEqual(UserSettings.Default, all);
        }

        [Test]
        public void ない項目_ない種類_ないカテゴリのキーは例外()
        {
            Assert.Throws<ArgumentException>(() => SettingsCatalog.Find("unknown"));
            Assert.Throws<ArgumentException>(() => SettingsCatalog.FindNumber("focusOnStartup"));
            Assert.Throws<ArgumentException>(() => SettingsCatalog.FindToggle("downMeters"));
            Assert.Throws<ArgumentException>(() => SettingsCatalog.FindToggle("autoLaunch"));
            Assert.Throws<ArgumentException>(() => SettingsCatalog.FindSwitch("downMeters"));
            Assert.AreSame(SettingsCatalog.AutoLaunch, SettingsCatalog.FindSwitch("autoLaunch"));
            Assert.Throws<ArgumentException>(() => SettingsCatalog.FindChoice("downMeters"));
            Assert.Throws<ArgumentException>(() => SettingsCatalog.FindCategory("unknown"));
        }

        [Test]
        public void 項目がない_または重複するカテゴリは例外()
        {
            SettingItem down = SettingsCatalog.Find("downMeters");
            Assert.Throws<ArgumentException>(() => new SettingsCategory("empty", Array.Empty<SettingItem>()));
            Assert.Throws<ArgumentException>(() => new SettingsCategory("duplicate", new[] { down, down }));
        }

        static List<string> Keys<T>(IReadOnlyList<T> values, Func<T, string> key)
        {
            var keys = new List<string>();
            foreach (T value in values) keys.Add(key(value));
            return keys;
        }
    }

    public sealed class SettingsFormTests
    {
        SettingsLayout _layout;
        SettingsForm _form;
        List<UserSettings> _changed;
        List<UserSettings> _committed;

        [SetUp]
        public void SetUp()
        {
            _layout = TestSettingsLayout.Create();
            _form = new SettingsForm(_layout, UserSettings.Default);
            _changed = new List<UserSettings>();
            _committed = new List<UserSettings>();
            _form.Changed += s => _changed.Add(s);
            _form.Committed += s => _committed.Add(s);
        }

        SettingsRowLayout Row(string key) => _layout.RowOf(SettingsCatalog.FindNumber(key));

        SettingsToggleLayout Toggle(string key)
        {
            SwitchSettingItem item = SettingsCatalog.FindSwitch(key);
            foreach (SettingsPageLayout page in _layout.Pages)
            {
                foreach (SettingsToggleLayout toggle in page.Toggles)
                {
                    if (toggle.Item == item) return toggle;
                }
            }
            throw new ArgumentException("配置にない項目です: " + key, nameof(key));
        }

        static void Click(SettingsForm form, Vector2 position)
        {
            form.PointerDown(position);
            form.PointerUp(position);
        }

        void SelectCategory(SettingsForm form, string key)
        {
            Click(form, _layout.PageOf(SettingsCatalog.FindCategory(key)).Tab.center);
            Assert.AreEqual(key, form.Category.Key);
        }

        [Test]
        public void 最初は先頭のカテゴリを表示する()
        {
            Assert.AreSame(_layout.Pages[0].Category, _form.Category);
        }

        [Test]
        public void タブを押して離すと表示するカテゴリが変わり確定しない()
        {
            SettingsCategory grip = SettingsCatalog.FindCategory("grip");
            Rect tab = _layout.PageOf(grip).Tab;
            _form.PointerDown(tab.center);
            Assert.AreEqual("display", _form.Category.Key, "押しただけでは変わらない");
            Assert.AreEqual(SettingsHit.Tab(grip), _form.Pressed);

            int version = _form.Version;
            _form.PointerUp(tab.center);
            Assert.AreSame(grip, _form.Category);
            Assert.Greater(_form.Version, version);
            Assert.AreEqual(0, _changed.Count);
            Assert.AreEqual(0, _committed.Count);
        }

        [Test]
        public void 表示していないページの項目は操作できない()
        {
            SelectCategory(_form, "grip");
            Click(_form, Row("widthMeters").Increase.center);
            Assert.AreEqual(UserSettings.Default, _form.Settings);
            Assert.AreEqual(0, _committed.Count);
        }

        [Test]
        public void 増やすボタンで刻み1つ分増えて確定する()
        {
            SettingsRowLayout row = Row("tiltDegrees");
            _form.PointerDown(row.Increase.center);
            Assert.AreEqual(0, _changed.Count, "押しただけでは変わらない");
            _form.PointerUp(row.Increase.center);

            Assert.AreEqual(18f, _form.Settings.Placement.TiltDegrees);
            Assert.AreEqual(1, _changed.Count);
            Assert.AreEqual(1, _committed.Count);
            Assert.AreEqual(_form.Settings, _committed[0]);
        }

        [Test]
        public void 減らすボタンで刻み1つ分減る()
        {
            SelectCategory(_form, "grip");
            Click(_form, Row("triggerThreshold").Decrease.center);
            Assert.AreEqual(0.65f, _form.Settings.TriggerThreshold);
        }

        [Test]
        public void ボタンの外で離すと働かない()
        {
            SettingsRowLayout row = Row("tiltDegrees");
            _form.PointerDown(row.Increase.center);
            _form.PointerUp(TestSettingsLayout.LabelPoint(row));
            Assert.AreEqual(UserSettings.Default, _form.Settings);
            Assert.AreEqual(0, _committed.Count);
        }

        [Test]
        public void 端の値でボタンを押しても確定しない()
        {
            var form = new SettingsForm(_layout, UserSettings.Default.WithTriggerThreshold(UserSettings.TriggerThresholdRange.Max));
            int committed = 0;
            form.Committed += _ => committed++;
            SelectCategory(form, "grip");
            Click(form, Row("triggerThreshold").Increase.center);
            Assert.AreEqual(0, committed);
        }

        [Test]
        public void スイッチを押して離すと切り替わって確定する()
        {
            SelectCategory(_form, "integration");
            SettingsToggleLayout toggle = Toggle("focusOnStartup");
            _form.PointerDown(toggle.Area.center);
            Assert.AreEqual(0, _changed.Count, "押しただけでは変わらない");
            Assert.AreEqual(SettingsHit.Toggle(toggle.Item), _form.Pressed);
            _form.PointerUp(toggle.Area.center);

            Assert.IsFalse(_form.Settings.VRChat.FocusOnStartup);
            Assert.IsTrue(_form.Settings.VRChat.ShowTypingText);
            Assert.AreEqual(1, _committed.Count);
            Assert.AreEqual(_form.Settings, _committed[0]);

            Click(_form, toggle.Area.center);
            Assert.AreEqual(UserSettings.Default, _form.Settings);
            Assert.AreEqual(2, _committed.Count);
        }

        [Test]
        public void 外が値を持つスイッチは値が渡されるまで利用できず_押しても何も求めない()
        {
            var requests = new List<(ExternalToggleSettingItem item, bool value)>();
            _form.ExternalChangeRequested += (item, value) => requests.Add((item, value));
            SelectCategory(_form, "integration");
            Assert.IsNull(_form.GetExternalValue(SettingsCatalog.AutoLaunch));
            Assert.IsNull(_form.IsOn(SettingsCatalog.AutoLaunch));

            Click(_form, Toggle("autoLaunch").Area.center);
            Assert.AreEqual(0, requests.Count);
            Assert.AreEqual(0, _changed.Count);
            Assert.AreEqual(0, _committed.Count);
        }

        [Test]
        public void 外が値を持つスイッチを押すと値を変えずに反対の値を求める()
        {
            var requests = new List<(ExternalToggleSettingItem item, bool value)>();
            _form.ExternalChangeRequested += (item, value) => requests.Add((item, value));
            int version = _form.Version;
            _form.SetExternalValue(SettingsCatalog.AutoLaunch, false);
            Assert.Greater(_form.Version, version);
            version = _form.Version;
            _form.SetExternalValue(SettingsCatalog.AutoLaunch, false);
            Assert.AreEqual(version, _form.Version, "同じ値では変わらない");

            SelectCategory(_form, "integration");
            Click(_form, Toggle("autoLaunch").Area.center);
            Assert.AreEqual(1, requests.Count);
            Assert.AreSame(SettingsCatalog.AutoLaunch, requests[0].item);
            Assert.IsTrue(requests[0].value);
            Assert.AreEqual(false, _form.GetExternalValue(SettingsCatalog.AutoLaunch), "読み直した値が渡されるまで変えない");
            Assert.AreEqual(0, _changed.Count);
            Assert.AreEqual(0, _committed.Count);

            _form.SetExternalValue(SettingsCatalog.AutoLaunch, true);
            Assert.AreEqual(true, _form.IsOn(SettingsCatalog.AutoLaunch));
        }

        [Test]
        public void 既定に戻すと外が値を持つスイッチは既定値と異なるときだけ既定値を求める()
        {
            var requests = new List<(ExternalToggleSettingItem item, bool value)>();
            _form.ExternalChangeRequested += (item, value) => requests.Add((item, value));
            SelectCategory(_form, "integration");

            _form.SetExternalValue(SettingsCatalog.AutoLaunch, SettingsCatalog.AutoLaunch.DefaultValue);
            Click(_form, _layout.Reset.center);
            Assert.AreEqual(0, requests.Count);

            _form.SetExternalValue(SettingsCatalog.AutoLaunch, !SettingsCatalog.AutoLaunch.DefaultValue);
            Click(_form, _layout.Reset.center);
            Assert.AreEqual(1, requests.Count);
            Assert.AreEqual(SettingsCatalog.AutoLaunch.DefaultValue, requests[0].value);

            // 表示していないカテゴリの「既定に戻す」では求めない。
            SelectCategory(_form, "grip");
            Click(_form, _layout.Reset.center);
            Assert.AreEqual(1, requests.Count);
        }

        [Test]
        public void 配置にない外の項目の値を読み書きすると例外()
        {
            var form = new SettingsForm(TestSettingsLayout.Create(TestSettingsLayout.Page(SettingsCatalog.FindCategory("grip"), 0)), UserSettings.Default);
            Assert.Throws<ArgumentException>(() => form.GetExternalValue(SettingsCatalog.AutoLaunch));
            Assert.Throws<ArgumentException>(() => form.SetExternalValue(SettingsCatalog.AutoLaunch, true));
        }

        [Test]
        public void スライダーはつまんでいる間随時変わり離したときに確定する()
        {
            SettingsRowLayout row = Row("rightMeters");
            float y = row.Slider.center.y;
            _form.PointerDown(new Vector2(row.Track.xMin, y));
            Assert.AreEqual(-0.50f, _form.Settings.Placement.RightMeters);
            Assert.AreSame(row.Item, _form.DraggingItem);

            // 行の外へ動かしても、つまんでいる間は追従する。
            _form.PointerMove(new Vector2(row.Track.xMax, y + 200f));
            Assert.AreEqual(0.50f, _form.Settings.Placement.RightMeters);
            Assert.AreEqual(2, _changed.Count);
            Assert.AreEqual(0, _committed.Count);

            _form.PointerUp(new Vector2(row.Track.xMax, y + 200f));
            Assert.IsNull(_form.DraggingItem);
            Assert.AreEqual(1, _committed.Count);
            Assert.AreEqual(0.50f, _committed[0].Placement.RightMeters);
        }

        [Test]
        public void スライダーを元の値に戻して離すと確定しない()
        {
            SettingsRowLayout row = Row("rightMeters");
            float y = row.Slider.center.y;
            float center = row.Track.center.x;
            _form.PointerDown(new Vector2(row.Track.xMin, y));
            _form.PointerMove(new Vector2(center, y));
            _form.PointerUp(new Vector2(center, y));
            Assert.AreEqual(UserSettings.Default, _form.Settings);
            Assert.AreEqual(0, _committed.Count);
        }

        [Test]
        public void つまんだままポインターが外れると確定する()
        {
            SettingsRowLayout row = Row("widthMeters");
            _form.PointerDown(new Vector2(row.Track.xMin, row.Slider.center.y));
            _form.PointerLeave();
            Assert.IsNull(_form.DraggingItem);
            Assert.AreEqual(1, _committed.Count);
            Assert.AreEqual(KeyboardPlacement.WidthRange.Min, _committed[0].Placement.WidthMeters);
        }

        [Test]
        public void 終了を押して離すと終了を求め_設定は確定しない()
        {
            int quit = 0;
            _form.QuitRequested += () => quit++;
            _form.PointerDown(_layout.Quit.center);
            Assert.AreEqual(SettingsHit.Quit, _form.Pressed);
            Assert.AreEqual(0, quit, "押しただけでは求めない");

            _form.PointerUp(_layout.Quit.center);
            Assert.AreEqual(1, quit);
            Assert.AreEqual(0, _changed.Count);
            Assert.AreEqual(0, _committed.Count);
        }

        [Test]
        public void 終了を押した後に外で離すと終了を求めない()
        {
            int quit = 0;
            _form.QuitRequested += () => quit++;
            _form.PointerDown(_layout.Quit.center);
            _form.PointerUp(_layout.Reset.center);
            _form.PointerDown(_layout.Quit.center);
            _form.PointerLeave();
            Assert.AreEqual(0, quit);
        }

        [Test]
        public void 既定に戻すと表示中のカテゴリの項目だけが戻る()
        {
            UserSettings initial = UserSettings.Default
                .WithPlacement(KeyboardPlacement.Default.WithTilt(30f))
                .WithStickThreshold(0.8f)
                .WithVRChat(new VRChatSettings(false, false));
            var form = new SettingsForm(_layout, initial);
            UserSettings committed = null;
            form.Committed += s => committed = s;

            SelectCategory(form, "grip");
            Click(form, _layout.Reset.center);
            UserSettings expected = initial.WithStickThreshold(UserSettings.Default.StickThreshold);
            Assert.AreEqual(expected, form.Settings);
            Assert.AreEqual(expected, committed);

            SelectCategory(form, "integration");
            Click(form, _layout.Reset.center);
            Assert.AreEqual(expected.WithVRChat(VRChatSettings.Default), form.Settings);
        }

        [Test]
        public void 指しているものと押しているものが変わると版が増える()
        {
            SettingsRowLayout row = Row("tiltDegrees");
            int version = _form.Version;
            _form.PointerMove(row.Increase.center);
            Assert.AreEqual(SettingsHit.Increase(row.Item), _form.Hover);
            Assert.Greater(_form.Version, version);

            version = _form.Version;
            _form.PointerMove(row.Increase.center);
            Assert.AreEqual(version, _form.Version, "同じものを指し続けても変わらない");

            _form.PointerDown(row.Increase.center);
            Assert.AreEqual(SettingsHit.Increase(row.Item), _form.Pressed);
            Assert.Greater(_form.Version, version);

            _form.PointerLeave();
            Assert.AreEqual(SettingsHit.None, _form.Hover);
            Assert.AreEqual(SettingsHit.None, _form.Pressed);
        }

        [Test]
        public void 同意が必要なスイッチをオフから押すと値を変えずに同意の画面を開く()
        {
            SelectCategory(_form, "privacy");
            SettingsToggleLayout toggle = Toggle("useGoogleConversion");
            int version = _form.Version;
            Click(_form, toggle.Area.center);

            Assert.AreSame(toggle.Item, _form.ConsentItem);
            Assert.Greater(_form.Version, version);
            Assert.AreEqual(SettingsHit.None, _form.Hover, "開いた直後は何も指していない");
            Assert.IsFalse(_form.Settings.Privacy.UseGoogleConversion);
            Assert.AreEqual(0, _changed.Count);
            Assert.AreEqual(0, _committed.Count);
        }

        [Test]
        public void 同意するとオンになって確定し同意の画面を閉じる()
        {
            SelectCategory(_form, "privacy");
            ToggleSettingItem item = SettingsCatalog.FindToggle("useGoogleConversion");
            Click(_form, Toggle(item.Key).Area.center);
            SettingsConsentLayout consent = _layout.ConsentOf(item);

            _form.PointerDown(consent.Accept.center);
            Assert.AreEqual(SettingsHit.ConsentAccept, _form.Pressed);
            Assert.IsFalse(_form.Settings.Privacy.UseGoogleConversion, "押しただけでは変わらない");
            _form.PointerUp(consent.Accept.center);

            Assert.IsNull(_form.ConsentItem);
            Assert.IsTrue(_form.Settings.Privacy.UseGoogleConversion);
            Assert.AreEqual(1, _committed.Count);
            Assert.AreEqual(_form.Settings, _committed[0]);
        }

        [Test]
        public void 同意を取り消すと何も変えずに同意の画面を閉じる()
        {
            SelectCategory(_form, "privacy");
            ToggleSettingItem item = SettingsCatalog.FindToggle("useGoogleConversion");
            Click(_form, Toggle(item.Key).Area.center);
            Click(_form, _layout.ConsentOf(item).Cancel.center);

            Assert.IsNull(_form.ConsentItem);
            Assert.AreEqual(UserSettings.Default, _form.Settings);
            Assert.AreEqual(0, _changed.Count);
            Assert.AreEqual(0, _committed.Count);
        }

        [Test]
        public void 同意の画面を開いている間はページとタブと既定に戻すを操作できない()
        {
            SelectCategory(_form, "privacy");
            ToggleSettingItem item = SettingsCatalog.FindToggle("useGoogleConversion");
            SettingsToggleLayout toggle = Toggle(item.Key);
            Click(_form, toggle.Area.center);

            Click(_form, new Vector2(toggle.Area.xMin + 10f, toggle.Area.center.y));
            Click(_form, _layout.PageOf(SettingsCatalog.FindCategory("display")).Tab.center);
            Click(_form, _layout.Reset.center);

            Assert.AreSame(item, _form.ConsentItem);
            Assert.AreEqual("privacy", _form.Category.Key);
            Assert.AreEqual(UserSettings.Default, _form.Settings);
            Assert.AreEqual(0, _committed.Count);
        }

        [Test]
        public void ポインターが外れても同意の画面は閉じない()
        {
            SelectCategory(_form, "privacy");
            ToggleSettingItem item = SettingsCatalog.FindToggle("useGoogleConversion");
            Click(_form, Toggle(item.Key).Area.center);
            _form.PointerDown(_layout.ConsentOf(item).Accept.center);
            _form.PointerLeave();

            Assert.AreSame(item, _form.ConsentItem);
            Assert.AreEqual(SettingsHit.None, _form.Pressed);
            Assert.IsFalse(_form.Settings.Privacy.UseGoogleConversion);
        }

        [Test]
        public void 同意が必要なスイッチもオフにするときと既定に戻すときは確認しない()
        {
            var form = new SettingsForm(_layout, UserSettings.Default.WithPrivacy(new PrivacySettings(true)));
            int committed = 0;
            form.Committed += _ => committed++;
            SelectCategory(form, "privacy");
            SettingsToggleLayout toggle = Toggle("useGoogleConversion");

            Click(form, toggle.Area.center);
            Assert.IsNull(form.ConsentItem);
            Assert.IsFalse(form.Settings.Privacy.UseGoogleConversion);
            Assert.AreEqual(1, committed);

            var enabled = new SettingsForm(_layout, UserSettings.Default.WithPrivacy(new PrivacySettings(true)));
            SelectCategory(enabled, "privacy");
            Click(enabled, _layout.Reset.center);
            Assert.IsNull(enabled.ConsentItem);
            Assert.AreEqual(UserSettings.Default, enabled.Settings);
        }

        SettingsDropdownLayout ThemeDropdown() => _layout.PageOf(SettingsCatalog.FindCategory("display")).DropdownOf(SettingsCatalog.FindChoice("theme"));

        [Test]
        public void ドロップダウンを押して離すと値を変えずに一覧を開く()
        {
            SettingsDropdownLayout dropdown = ThemeDropdown();
            _form.PointerDown(dropdown.Button.center);
            Assert.AreEqual(SettingsHit.Dropdown(dropdown.Item), _form.Pressed);
            Assert.IsNull(_form.OpenDropdown, "押しただけでは開かない");

            int version = _form.Version;
            _form.PointerUp(dropdown.Button.center);
            Assert.AreSame(dropdown.Item, _form.OpenDropdown);
            Assert.Greater(_form.Version, version);
            Assert.AreEqual(SettingsHit.None, _form.Hover, "開いた直後は何も指していない");
            Assert.AreEqual(0, _changed.Count);
            Assert.AreEqual(0, _committed.Count);
        }

        [Test]
        public void 選択肢を押して離すと値が変わって確定し一覧を閉じる()
        {
            SettingsDropdownLayout dropdown = ThemeDropdown();
            Click(_form, dropdown.Button.center);
            _form.PointerDown(dropdown.Options[1].center);
            Assert.AreEqual(SettingsHit.Option(dropdown.Item, 1), _form.Pressed);
            Assert.AreEqual(ColorTheme.Lime, _form.Settings.Theme, "押しただけでは変わらない");
            _form.PointerUp(dropdown.Options[1].center);

            Assert.IsNull(_form.OpenDropdown);
            Assert.AreEqual(ColorTheme.Dark, _form.Settings.Theme);
            Assert.AreEqual(1, _changed.Count);
            Assert.AreEqual(1, _committed.Count);
            Assert.AreEqual(_form.Settings, _committed[0]);
        }

        [Test]
        public void いまの値の選択肢を選ぶと一覧を閉じるだけで確定しない()
        {
            SettingsDropdownLayout dropdown = ThemeDropdown();
            Click(_form, dropdown.Button.center);
            Click(_form, dropdown.Options[0].center);
            Assert.IsNull(_form.OpenDropdown);
            Assert.AreEqual(UserSettings.Default, _form.Settings);
            Assert.AreEqual(0, _committed.Count);
        }

        [Test]
        public void 選択肢以外の場所を押して離すと何も変えずに一覧を閉じ_後ろの対象は働かない()
        {
            SettingsDropdownLayout dropdown = ThemeDropdown();
            Click(_form, dropdown.Button.center);
            Click(_form, _layout.Reset.center);
            Assert.IsNull(_form.OpenDropdown);

            Click(_form, dropdown.Button.center);
            Click(_form, _layout.PageOf(SettingsCatalog.FindCategory("grip")).Tab.center);
            Assert.IsNull(_form.OpenDropdown);
            Assert.AreEqual("display", _form.Category.Key);
            Assert.AreEqual(UserSettings.Default, _form.Settings);
            Assert.AreEqual(0, _changed.Count);
            Assert.AreEqual(0, _committed.Count);
        }

        [Test]
        public void 一覧を開いている間に選択肢の外で離すと選ばない()
        {
            SettingsDropdownLayout dropdown = ThemeDropdown();
            Click(_form, dropdown.Button.center);
            _form.PointerDown(dropdown.Options[1].center);
            _form.PointerUp(_layout.Reset.center);
            Assert.AreSame(dropdown.Item, _form.OpenDropdown);
            Assert.AreEqual(UserSettings.Default, _form.Settings);
        }

        [Test]
        public void ポインターが外れると一覧を閉じる()
        {
            SettingsDropdownLayout dropdown = ThemeDropdown();
            Click(_form, dropdown.Button.center);
            _form.PointerDown(dropdown.Options[1].center);
            _form.PointerLeave();
            Assert.IsNull(_form.OpenDropdown);
            Assert.AreEqual(SettingsHit.None, _form.Pressed);
            Assert.AreEqual(UserSettings.Default, _form.Settings);
        }

        [Test]
        public void 表示のカテゴリを既定に戻すとテーマもライムに戻る()
        {
            var form = new SettingsForm(_layout, UserSettings.Default.WithTheme(ColorTheme.Dark));
            Click(form, _layout.Reset.center);
            Assert.AreEqual(UserSettings.Default, form.Settings);
        }

        [Test]
        public void 通知を変えると版が増える()
        {
            int version = _form.Version;
            _form.SetNotice("通知");
            Assert.AreEqual("通知", _form.Notice);
            Assert.Greater(_form.Version, version);
            version = _form.Version;
            _form.SetNotice("通知");
            Assert.AreEqual(version, _form.Version);
        }
    }
}
