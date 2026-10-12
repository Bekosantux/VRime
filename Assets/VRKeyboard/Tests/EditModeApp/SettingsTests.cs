using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using VRKeyboard.App.Settings;
using VRKeyboard.Core;

namespace VRKeyboard.App.Tests
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
        public void 既定のしきい値はCoreの既定値と同じ()
        {
            var core = new KeyboardSettings();
            Assert.AreEqual(core.TriggerPressThreshold, UserSettings.Default.TriggerThreshold);
            Assert.AreEqual(core.StickEnterThreshold, UserSettings.Default.StickThreshold);
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
            Assert.AreEqual(0.6f, core.TriggerPressThreshold);
            Assert.AreEqual(0.4f, core.StickEnterThreshold);
        }

        [Test]
        public void すべての範囲のしきい値をCoreが受け付ける()
        {
            // 離した判定のしきい値（-0.2）が 0 より大きくなるよう、範囲の下限は Core の制約を満たす。
            Assert.DoesNotThrow(() => UserSettings.Default.WithTriggerThreshold(UserSettings.TriggerThresholdRange.Min)
                .WithStickThreshold(UserSettings.StickThresholdRange.Min).ToKeyboardSettings());
            Assert.DoesNotThrow(() => UserSettings.Default.WithTriggerThreshold(UserSettings.TriggerThresholdRange.Max)
                .WithStickThreshold(UserSettings.StickThresholdRange.Max).ToKeyboardSettings());
        }

        [Test]
        public void 範囲外の値は例外()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => UserSettings.Default.WithTriggerThreshold(0.2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => UserSettings.Default.WithStickThreshold(1f));
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
        }
    }

    public sealed class SettingsSerializerTests
    {
        [Test]
        public void 書いて読むと同じ値に戻る()
        {
            UserSettings settings = UserSettings.Default
                .WithPlacement(new KeyboardPlacement(0.31f, 0.72f, -0.12f, 25f, 0.52f))
                .WithTriggerThreshold(0.55f)
                .WithStickThreshold(0.35f);
            Assert.AreEqual(settings, SettingsSerializer.Deserialize(SettingsSerializer.Serialize(settings)));
        }

        [Test]
        public void 値は短い10進表記で書く()
        {
            string json = SettingsSerializer.Serialize(UserSettings.Default);
            StringAssert.Contains("\"downMeters\": 0.26", json);
            StringAssert.DoesNotContain("0.2599", json);
            StringAssert.Contains("\"version\": 1", json);
        }

        [Test]
        public void 欠けている項目は既定値()
        {
            UserSettings settings = SettingsSerializer.Deserialize("{\"version\":1,\"display\":{\"tiltDegrees\":30}}");
            Assert.AreEqual(KeyboardPlacement.Default.WithTilt(30f), settings.Placement);
            Assert.AreEqual(UserSettings.Default.TriggerThreshold, settings.TriggerThreshold);

            Assert.AreEqual(UserSettings.Default, SettingsSerializer.Deserialize("{\"version\":1}"));
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
        [TestCase("{\"version\":1,\"grip\":{\"triggerThreshold\":0.1}}")]
        [TestCase("{\"version\":1,\"grip\":{\"stickThreshold\":null}}")]
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
            _directory = Path.Combine(Path.GetTempPath(), "VRKeyboardTests-" + Guid.NewGuid().ToString("N"));
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

    /// <summary>シーンの設定画面と同じ寸法の配置（1280×800。行の高さ 72・［−］・スライダー・［＋］を横に並べる）。</summary>
    internal static class TestSettingsLayout
    {
        public static readonly Vector2 Size = new Vector2(1280f, 800f);
        public static readonly Rect Reset = new Rect(992f, 732f, 240f, 52f);
        const float FirstRowTop = 144f;
        const float RowHeight = 72f;

        public static SettingsLayout Create()
        {
            var rows = new List<SettingsRowLayout>();
            for (int i = 0; i < SettingsCatalog.Items.Count; i++) rows.Add(Row(SettingsCatalog.Items[i], FirstRowTop + RowHeight * i));
            return new SettingsLayout(Size, rows, Reset);
        }

        public static SettingsRowLayout Row(SettingItem item, float top)
        {
            var slider = new Rect(440f, top, 520f, RowHeight);
            return new SettingsRowLayout(item,
                new Rect(360f, top + 4f, 64f, 64f),
                slider,
                new Rect(976f, top + 4f, 64f, 64f),
                new Rect(slider.xMin + 18f, slider.center.y - 5f, slider.width - 36f, 10f));
        }

        /// <summary>行の左（項目名の位置）。操作の対象がない。</summary>
        public static Vector2 LabelPoint(SettingsRowLayout row) => new Vector2(200f, row.Slider.center.y);
    }

    public sealed class SettingsLayoutTests
    {
        readonly SettingsLayout _layout = TestSettingsLayout.Create();

        [Test]
        public void 当たり判定()
        {
            SettingsRowLayout row = _layout.Rows[2];
            Assert.AreEqual(new SettingsHit(SettingsTarget.Slider, row.Item), _layout.HitTest(row.Slider.center));
            Assert.AreEqual(new SettingsHit(SettingsTarget.Decrease, row.Item), _layout.HitTest(row.Decrease.center));
            Assert.AreEqual(new SettingsHit(SettingsTarget.Increase, row.Item), _layout.HitTest(row.Increase.center));
            Assert.AreEqual(new SettingsHit(SettingsTarget.Reset, null), _layout.HitTest(_layout.Reset.center));
            Assert.AreEqual(SettingsHit.None, _layout.HitTest(TestSettingsLayout.LabelPoint(row)));
            Assert.AreEqual(SettingsHit.None, _layout.HitTest(new Vector2(-10f, -10f)));
        }

        [Test]
        public void スライダーの位置は値の範囲の両端で0と1()
        {
            SettingsRowLayout row = _layout.Rows[0];
            Assert.AreEqual(0f, row.FractionAt(row.Track.xMin));
            Assert.AreEqual(1f, row.FractionAt(row.Track.xMax));
            Assert.AreEqual(0f, row.FractionAt(row.Slider.xMin));
            Assert.AreEqual(1f, row.FractionAt(row.Slider.xMax));
            Assert.AreEqual(0.5f, row.FractionAt(row.Track.center.x), 1e-6f);
        }

        [Test]
        public void 隣り合う行は接していても重ならない()
        {
            Assert.AreEqual(_layout.Rows[0].Slider.yMax, _layout.Rows[1].Slider.yMin);
            Assert.DoesNotThrow(() => TestSettingsLayout.Create());
        }

        [Test]
        public void 操作の対象が重なると例外()
        {
            var rows = new List<SettingsRowLayout>
            {
                TestSettingsLayout.Row(SettingsCatalog.Items[0], 100f),
                TestSettingsLayout.Row(SettingsCatalog.Items[1], 140f),
            };
            Assert.Throws<ArgumentException>(() => new SettingsLayout(TestSettingsLayout.Size, rows, TestSettingsLayout.Reset));
        }

        [Test]
        public void 操作の対象が画面の外にあると例外()
        {
            var rows = new List<SettingsRowLayout> { TestSettingsLayout.Row(SettingsCatalog.Items[0], 760f) };
            Assert.Throws<ArgumentException>(() => new SettingsLayout(TestSettingsLayout.Size, rows, TestSettingsLayout.Reset));
        }

        [Test]
        public void 操作の対象の大きさが正でないと例外()
        {
            var rows = new List<SettingsRowLayout> { TestSettingsLayout.Row(SettingsCatalog.Items[0], 100f) };
            Assert.Throws<ArgumentException>(() => new SettingsLayout(TestSettingsLayout.Size, rows, new Rect(992f, 732f, 0f, 52f)));
        }

        [Test]
        public void 同じ項目の行が重複すると例外()
        {
            var rows = new List<SettingsRowLayout>
            {
                TestSettingsLayout.Row(SettingsCatalog.Items[0], 100f),
                TestSettingsLayout.Row(SettingsCatalog.Items[0], 300f),
            };
            Assert.Throws<ArgumentException>(() => new SettingsLayout(TestSettingsLayout.Size, rows, TestSettingsLayout.Reset));
        }

        [Test]
        public void 値の範囲の幅が正でないと例外()
        {
            SettingItem item = SettingsCatalog.Items[0];
            Assert.Throws<ArgumentException>(() => new SettingsRowLayout(item,
                new Rect(360f, 104f, 64f, 64f), new Rect(440f, 100f, 520f, 72f), new Rect(976f, 104f, 64f, 64f), new Rect(458f, 131f, 0f, 10f)));
        }

        [Test]
        public void 配置にない項目の行は例外()
        {
            var rows = new List<SettingsRowLayout> { TestSettingsLayout.Row(SettingsCatalog.Items[0], 100f) };
            var layout = new SettingsLayout(TestSettingsLayout.Size, rows, TestSettingsLayout.Reset);
            Assert.Throws<ArgumentException>(() => layout.RowOf(SettingsCatalog.Items[1]));
        }
    }

    public sealed class SettingsCatalogTests
    {
        [Test]
        public void 値の表示()
        {
            SettingItem down = SettingsCatalog.Find("downMeters");
            SettingItem right = SettingsCatalog.Find("rightMeters");
            SettingItem tilt = SettingsCatalog.Find("tiltDegrees");
            SettingItem trigger = SettingsCatalog.Find("triggerThreshold");
            Assert.AreEqual("26 cm", down.Format(0.26f));
            Assert.AreEqual("中央", right.Format(0f));
            Assert.AreEqual("右 12 cm", right.Format(0.12f));
            Assert.AreEqual("左 5 cm", right.Format(-0.05f));
            Assert.AreEqual("17°", tilt.Format(17f));
            Assert.AreEqual("70%", trigger.Format(0.7f));
        }

        [Test]
        public void 項目ごとに対応する値を読み書きする()
        {
            UserSettings s = UserSettings.Default;
            Assert.AreEqual(0.30f, SettingsCatalog.Find("downMeters").Get(SettingsCatalog.Find("downMeters").Set(s, 0.30f)));
            Assert.AreEqual(1.20f, SettingsCatalog.Find("forwardMeters").Set(s, 1.20f).Placement.ForwardMeters);
            Assert.AreEqual(-0.10f, SettingsCatalog.Find("rightMeters").Set(s, -0.10f).Placement.RightMeters);
            Assert.AreEqual(40f, SettingsCatalog.Find("tiltDegrees").Set(s, 40f).Placement.TiltDegrees);
            Assert.AreEqual(0.60f, SettingsCatalog.Find("widthMeters").Set(s, 0.60f).Placement.WidthMeters);
            Assert.AreEqual(0.50f, SettingsCatalog.Find("triggerThreshold").Set(s, 0.50f).TriggerThreshold);
            Assert.AreEqual(0.65f, SettingsCatalog.Find("stickThreshold").Set(s, 0.65f).StickThreshold);
        }

        [Test]
        public void 設定する値は刻みに揃える()
        {
            Assert.AreEqual(0.55f, SettingsCatalog.Find("triggerThreshold").Set(UserSettings.Default, 0.56f).TriggerThreshold);
        }

        [Test]
        public void 項目のキーは重複しない()
        {
            var keys = new HashSet<string>();
            foreach (SettingItem item in SettingsCatalog.Items) Assert.IsTrue(keys.Add(item.Key), item.Key);
        }

        [Test]
        public void ない項目のキーは例外()
        {
            Assert.Throws<ArgumentException>(() => SettingsCatalog.Find("unknown"));
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

        SettingsRowLayout Row(string key) => _layout.RowOf(SettingsCatalog.Find(key));

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
            SettingsRowLayout row = Row("triggerThreshold");
            _form.PointerDown(row.Decrease.center);
            _form.PointerUp(row.Decrease.center);
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
            SettingsRowLayout row = Row("triggerThreshold");
            form.PointerDown(row.Increase.center);
            form.PointerUp(row.Increase.center);
            Assert.AreEqual(0, committed);
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
        public void 既定に戻す()
        {
            var form = new SettingsForm(_layout, UserSettings.Default.WithStickThreshold(0.8f));
            UserSettings committed = null;
            form.Committed += s => committed = s;
            form.PointerDown(_layout.Reset.center);
            form.PointerUp(_layout.Reset.center);
            Assert.AreEqual(UserSettings.Default, form.Settings);
            Assert.AreEqual(UserSettings.Default, committed);
        }

        [Test]
        public void 指しているものと押しているものが変わると版が増える()
        {
            SettingsRowLayout row = Row("tiltDegrees");
            int version = _form.Version;
            _form.PointerMove(row.Increase.center);
            Assert.AreEqual(new SettingsHit(SettingsTarget.Increase, row.Item), _form.Hover);
            Assert.Greater(_form.Version, version);

            version = _form.Version;
            _form.PointerMove(row.Increase.center);
            Assert.AreEqual(version, _form.Version, "同じものを指し続けても変わらない");

            _form.PointerDown(row.Increase.center);
            Assert.AreEqual(new SettingsHit(SettingsTarget.Increase, row.Item), _form.Pressed);
            Assert.Greater(_form.Version, version);

            _form.PointerLeave();
            Assert.AreEqual(SettingsHit.None, _form.Hover);
            Assert.AreEqual(SettingsHit.None, _form.Pressed);
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
