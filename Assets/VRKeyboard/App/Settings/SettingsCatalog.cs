using System;
using System.Collections.Generic;
using System.Globalization;

namespace VRKeyboard.App.Settings
{
    /// <summary>設定画面の数値の項目。値の読み書きと表示の形式をまとめる。</summary>
    public sealed class SettingItem
    {
        readonly Func<UserSettings, float> _get;
        readonly Func<UserSettings, float, UserSettings> _set;
        readonly Func<float, string> _format;

        public SettingItem(string label, SettingRange range, Func<UserSettings, float> get,
            Func<UserSettings, float, UserSettings> set, Func<float, string> format)
        {
            if (string.IsNullOrEmpty(label)) throw new ArgumentException("項目名が空です。", nameof(label));
            Label = label;
            Range = range;
            _get = get ?? throw new ArgumentNullException(nameof(get));
            _set = set ?? throw new ArgumentNullException(nameof(set));
            _format = format ?? throw new ArgumentNullException(nameof(format));
        }

        public string Label { get; }
        public SettingRange Range { get; }

        public float Get(UserSettings settings) => _get(settings);

        /// <summary>value を刻みに揃えてから設定する。</summary>
        public UserSettings Set(UserSettings settings, float value) => _set(settings, Range.Snap(value));

        public string Format(float value) => _format(value);
    }

    /// <summary>設定画面の見出しと、その下に並ぶ項目。</summary>
    public sealed class SettingGroup
    {
        public SettingGroup(string title, string note, IReadOnlyList<SettingItem> items)
        {
            if (string.IsNullOrEmpty(title)) throw new ArgumentException("見出しが空です。", nameof(title));
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (items.Count == 0) throw new ArgumentException("項目がありません。", nameof(items));
            Title = title;
            Note = note;
            Items = items;
        }

        public string Title { get; }

        /// <summary>見出しの横に添える説明。なければ null。</summary>
        public string Note { get; }

        public IReadOnlyList<SettingItem> Items { get; }
    }

    /// <summary>設定画面に並べる項目（SPEC 8章）。並びは画面の上からの順。</summary>
    public static class SettingsCatalog
    {
        public static readonly IReadOnlyList<SettingGroup> Groups = new[]
        {
            new SettingGroup("表示位置", "目の位置から見た、キーボード本体の中心の位置", new[]
            {
                new SettingItem("下への距離", KeyboardPlacement.DownRange,
                    s => s.Placement.DownMeters, (s, v) => s.WithPlacement(s.Placement.WithDown(v)), Centimeters),
                new SettingItem("前への距離", KeyboardPlacement.ForwardRange,
                    s => s.Placement.ForwardMeters, (s, v) => s.WithPlacement(s.Placement.WithForward(v)), Centimeters),
                new SettingItem("左右の位置", KeyboardPlacement.RightRange,
                    s => s.Placement.RightMeters, (s, v) => s.WithPlacement(s.Placement.WithRight(v)), Sideways),
                new SettingItem("傾き", KeyboardPlacement.TiltRange,
                    s => s.Placement.TiltDegrees, (s, v) => s.WithPlacement(s.Placement.WithTilt(v)), Degrees),
                new SettingItem("幅", KeyboardPlacement.WidthRange,
                    s => s.Placement.WidthMeters, (s, v) => s.WithPlacement(s.Placement.WithWidth(v)), Centimeters),
            }),
            new SettingGroup("グリップ入力", "引いた量・倒した量がこの値以上で反応します", new[]
            {
                new SettingItem("トリガーのしきい値", UserSettings.TriggerThresholdRange,
                    s => s.TriggerThreshold, (s, v) => s.WithTriggerThreshold(v), Percent),
                new SettingItem("スティックのしきい値", UserSettings.StickThresholdRange,
                    s => s.StickThreshold, (s, v) => s.WithStickThreshold(v), Percent),
            }),
        };

        static string Centimeters(float meters) => Round(meters * 100f) + " cm";

        static string Sideways(float meters)
        {
            int cm = Round(meters * 100f);
            if (cm == 0) return "中央";
            return (cm > 0 ? "右 " : "左 ") + Math.Abs(cm).ToString(CultureInfo.InvariantCulture) + " cm";
        }

        static string Degrees(float degrees) => Round(degrees) + "°";

        static string Percent(float ratio) => Round(ratio * 100f) + "%";

        static int Round(float value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }
}
