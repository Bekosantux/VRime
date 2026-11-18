using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VRime.App.Settings
{
    /// <summary>
    /// <see cref="UserSettings"/> と設定ファイルの JSON の相互変換（SPEC 8章）。
    /// 項目が欠けている場合はその項目だけ既定値にする。形式・型・範囲・版が正しくない場合は <see cref="FormatException"/>。
    /// </summary>
    public static class SettingsSerializer
    {
        public const int CurrentVersion = 1;

        const string VersionKey = "version";
        const string DisplayKey = "display";
        const string DownKey = "downMeters";
        const string ForwardKey = "forwardMeters";
        const string RightKey = "rightMeters";
        const string TiltKey = "tiltDegrees";
        const string WidthKey = "widthMeters";
        const string ThemeKey = "theme";
        const string DarkTheme = "dark";
        const string LimeTheme = "lime";
        const string GripKey = "grip";
        const string TriggerKey = "triggerThreshold";
        const string StickKey = "stickThreshold";
        const string RepeatDelayKey = "repeatDelaySeconds";
        const string VRChatKey = "vrchat";
        const string FocusOnStartupKey = "focusOnStartup";
        const string ShowTypingTextKey = "showTypingText";
        const string PrivacyKey = "privacy";
        const string UseGoogleConversionKey = "useGoogleConversion";

        public static string Serialize(UserSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            KeyboardPlacement p = settings.Placement;
            var root = new JObject
            {
                [VersionKey] = CurrentVersion,
                [DisplayKey] = new JObject
                {
                    [DownKey] = Number(p.DownMeters),
                    [ForwardKey] = Number(p.ForwardMeters),
                    [RightKey] = Number(p.RightMeters),
                    [TiltKey] = Number(p.TiltDegrees),
                    [WidthKey] = Number(p.WidthMeters),
                    [ThemeKey] = ThemeName(settings.Theme),
                },
                [GripKey] = new JObject
                {
                    [TriggerKey] = Number(settings.TriggerThreshold),
                    [StickKey] = Number(settings.StickThreshold),
                    [RepeatDelayKey] = Number(settings.RepeatDelaySeconds),
                },
                [VRChatKey] = new JObject
                {
                    [FocusOnStartupKey] = settings.VRChat.FocusOnStartup,
                    [ShowTypingTextKey] = settings.VRChat.ShowTypingText,
                },
                [PrivacyKey] = new JObject
                {
                    [UseGoogleConversionKey] = settings.Privacy.UseGoogleConversion,
                },
            };
            return root.ToString(Formatting.Indented);
        }

        public static UserSettings Deserialize(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));

            JToken token;
            try
            {
                token = JToken.Parse(json);
            }
            catch (JsonException e)
            {
                throw new FormatException("JSON として読み取れません。", e);
            }
            if (!(token is JObject root)) throw new FormatException("JSON のオブジェクトではありません。");

            JToken version = root[VersionKey];
            if (version == null) throw new FormatException("\"" + VersionKey + "\" がありません。");
            if (version.Type != JTokenType.Integer || (long)version != CurrentVersion)
                throw new FormatException("対応していない版です: " + version.ToString(Formatting.None));

            UserSettings defaults = UserSettings.Default;
            KeyboardPlacement dp = defaults.Placement;
            JObject display = OptionalObject(root, DisplayKey);
            JObject grip = OptionalObject(root, GripKey);
            JObject vrchat = OptionalObject(root, VRChatKey);
            JObject privacy = OptionalObject(root, PrivacyKey);
            var vrchatSettings = new VRChatSettings(
                OptionalBool(vrchat, FocusOnStartupKey, defaults.VRChat.FocusOnStartup),
                OptionalBool(vrchat, ShowTypingTextKey, defaults.VRChat.ShowTypingText));
            var privacySettings = new PrivacySettings(
                OptionalBool(privacy, UseGoogleConversionKey, defaults.Privacy.UseGoogleConversion));
            string themeName = OptionalString(display, ThemeKey);
            ColorTheme theme = themeName == null ? defaults.Theme : ParseTheme(themeName);

            try
            {
                var placement = new KeyboardPlacement(
                    OptionalNumber(display, DownKey, dp.DownMeters),
                    OptionalNumber(display, ForwardKey, dp.ForwardMeters),
                    OptionalNumber(display, RightKey, dp.RightMeters),
                    OptionalNumber(display, TiltKey, dp.TiltDegrees),
                    OptionalNumber(display, WidthKey, dp.WidthMeters));
                return new UserSettings(
                    placement,
                    theme,
                    OptionalNumber(grip, TriggerKey, defaults.TriggerThreshold),
                    OptionalNumber(grip, StickKey, defaults.StickThreshold),
                    OptionalNumber(grip, RepeatDelayKey, defaults.RepeatDelaySeconds),
                    vrchatSettings,
                    privacySettings);
            }
            catch (ArgumentOutOfRangeException e)
            {
                throw new FormatException("範囲外の値があります: " + e.ParamName + " = "
                    + Convert.ToString(e.ActualValue, CultureInfo.InvariantCulture), e);
            }
        }

        // float を double に広げると 0.26 が 0.25999999046325684 になるため、float として最短の 10進表記を経由する。
        static JValue Number(float value)
        {
            return new JValue(double.Parse(value.ToString("R", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture));
        }

        // 欠けていれば null。オブジェクト以外は不正。
        static JObject OptionalObject(JObject parent, string key)
        {
            JToken token = parent[key];
            if (token == null) return null;
            if (!(token is JObject obj)) throw new FormatException("\"" + key + "\" がオブジェクトではありません。");
            return obj;
        }

        // 親または項目が欠けていれば既定値。数値以外は不正。
        static float OptionalNumber(JObject parent, string key, float defaultValue)
        {
            JToken token = parent?[key];
            if (token == null) return defaultValue;
            if (token.Type != JTokenType.Float && token.Type != JTokenType.Integer)
                throw new FormatException("\"" + key + "\" が数値ではありません。");
            double value = (double)token;
            if (double.IsNaN(value) || double.IsInfinity(value) || Math.Abs(value) > float.MaxValue)
                throw new FormatException("\"" + key + "\" が有限の数値ではありません。");
            return (float)value;
        }

        // 親または項目が欠けていれば null。文字列以外は不正。
        static string OptionalString(JObject parent, string key)
        {
            JToken token = parent?[key];
            if (token == null) return null;
            if (token.Type != JTokenType.String) throw new FormatException("\"" + key + "\" が文字列ではありません。");
            return (string)token;
        }

        static string ThemeName(ColorTheme theme)
        {
            switch (theme)
            {
                case ColorTheme.Dark: return DarkTheme;
                case ColorTheme.Lime: return LimeTheme;
                default: throw new ArgumentOutOfRangeException(nameof(theme), theme, "未知のテーマです。");
            }
        }

        static ColorTheme ParseTheme(string name)
        {
            switch (name)
            {
                case DarkTheme: return ColorTheme.Dark;
                case LimeTheme: return ColorTheme.Lime;
                default: throw new FormatException("\"" + ThemeKey + "\" が未知のテーマです: " + name);
            }
        }

        // 親または項目が欠けていれば既定値。真偽値以外は不正。
        static bool OptionalBool(JObject parent, string key, bool defaultValue)
        {
            JToken token = parent?[key];
            if (token == null) return defaultValue;
            if (token.Type != JTokenType.Boolean) throw new FormatException("\"" + key + "\" が真偽値ではありません。");
            return (bool)token;
        }
    }
}
