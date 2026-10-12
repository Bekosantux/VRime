using System;
using System.Collections.Generic;
using System.Globalization;

namespace VRKeyboard.App.Settings
{
    /// <summary>
    /// 設定画面の数値の項目。値の読み書きと表示の形式をまとめる。
    /// 画面の行（<see cref="View.SettingsRow"/>）とは <see cref="Key"/> で対応させる。項目名などの見た目はシーンに置く。
    /// </summary>
    public sealed class SettingItem
    {
        readonly Func<UserSettings, float> _get;
        readonly Func<UserSettings, float, UserSettings> _set;
        readonly Func<float, string> _format;

        public SettingItem(string key, SettingRange range, Func<UserSettings, float> get,
            Func<UserSettings, float, UserSettings> set, Func<float, string> format)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("キーが空です。", nameof(key));
            Key = key;
            Range = range;
            _get = get ?? throw new ArgumentNullException(nameof(get));
            _set = set ?? throw new ArgumentNullException(nameof(set));
            _format = format ?? throw new ArgumentNullException(nameof(format));
        }

        /// <summary>項目を識別するキー。設定ファイルの値の名前（SPEC 8.1）と同じ。</summary>
        public string Key { get; }

        public SettingRange Range { get; }

        public float Get(UserSettings settings) => _get(settings);

        /// <summary>value を刻みに揃えてから設定する。</summary>
        public UserSettings Set(UserSettings settings, float value) => _set(settings, Range.Snap(value));

        public string Format(float value) => _format(value);
    }

    /// <summary>設定画面に並べる項目（SPEC 8章）。</summary>
    public static class SettingsCatalog
    {
        public static readonly IReadOnlyList<SettingItem> Items = new[]
        {
            new SettingItem("downMeters", KeyboardPlacement.DownRange,
                s => s.Placement.DownMeters, (s, v) => s.WithPlacement(s.Placement.WithDown(v)), Centimeters),
            new SettingItem("forwardMeters", KeyboardPlacement.ForwardRange,
                s => s.Placement.ForwardMeters, (s, v) => s.WithPlacement(s.Placement.WithForward(v)), Centimeters),
            new SettingItem("rightMeters", KeyboardPlacement.RightRange,
                s => s.Placement.RightMeters, (s, v) => s.WithPlacement(s.Placement.WithRight(v)), Sideways),
            new SettingItem("tiltDegrees", KeyboardPlacement.TiltRange,
                s => s.Placement.TiltDegrees, (s, v) => s.WithPlacement(s.Placement.WithTilt(v)), Degrees),
            new SettingItem("widthMeters", KeyboardPlacement.WidthRange,
                s => s.Placement.WidthMeters, (s, v) => s.WithPlacement(s.Placement.WithWidth(v)), Centimeters),
            new SettingItem("triggerThreshold", UserSettings.TriggerThresholdRange,
                s => s.TriggerThreshold, (s, v) => s.WithTriggerThreshold(v), Percent),
            new SettingItem("stickThreshold", UserSettings.StickThresholdRange,
                s => s.StickThreshold, (s, v) => s.WithStickThreshold(v), Percent),
        };

        /// <summary>キーに対応する項目。ない場合は例外。</summary>
        public static SettingItem Find(string key)
        {
            foreach (SettingItem item in Items)
            {
                if (item.Key == key) return item;
            }
            throw new ArgumentException("設定の項目がありません: " + key, nameof(key));
        }

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
