using System;
using System.Globalization;
using VRime.Core;

namespace VRime.App.Settings
{
    /// <summary>
    /// 設定画面で変更し、設定ファイルに保存する値（SPEC 8章）。値は生成時に範囲を検査し、範囲外は例外にする。
    /// キーボードの動作に関わる値は <see cref="ToKeyboardSettings"/> で Core の設定にする。
    /// </summary>
    public sealed class UserSettings
    {
        public static readonly SettingRange TriggerThresholdRange = new SettingRange(0.05f, 0.95f, 0.05f);
        public static readonly SettingRange StickThresholdRange = new SettingRange(0.05f, 0.95f, 0.05f);
        public static readonly SettingRange RepeatDelaySecondsRange = new SettingRange(0.20f, 1.00f, 0.05f);

        /// <summary>しきい値と長押しの時間の既定は Core の既定値に揃える。テーマの既定はダーク。</summary>
        public static readonly UserSettings Default = CreateDefault();

        public UserSettings(KeyboardPlacement placement, ColorTheme theme, float triggerThreshold, float stickThreshold, float repeatDelaySeconds,
            VRChatSettings vrchat, PrivacySettings privacy)
        {
            Placement = placement ?? throw new ArgumentNullException(nameof(placement));
            if (!Enum.IsDefined(typeof(ColorTheme), theme))
                throw new ArgumentOutOfRangeException(nameof(theme), theme, "未知のテーマです。");
            VRChat = vrchat ?? throw new ArgumentNullException(nameof(vrchat));
            Privacy = privacy ?? throw new ArgumentNullException(nameof(privacy));
            if (!TriggerThresholdRange.Contains(triggerThreshold))
                throw new ArgumentOutOfRangeException(nameof(triggerThreshold), triggerThreshold,
                    TriggerThresholdRange.Min + "〜" + TriggerThresholdRange.Max + " である必要があります。");
            if (!StickThresholdRange.Contains(stickThreshold))
                throw new ArgumentOutOfRangeException(nameof(stickThreshold), stickThreshold,
                    StickThresholdRange.Min + "〜" + StickThresholdRange.Max + " である必要があります。");
            if (!RepeatDelaySecondsRange.Contains(repeatDelaySeconds))
                throw new ArgumentOutOfRangeException(nameof(repeatDelaySeconds), repeatDelaySeconds,
                    RepeatDelaySecondsRange.Min + "〜" + RepeatDelaySecondsRange.Max + " である必要があります。");
            Theme = theme;
            TriggerThreshold = triggerThreshold;
            StickThreshold = stickThreshold;
            RepeatDelaySeconds = repeatDelaySeconds;
        }

        public KeyboardPlacement Placement { get; }

        /// <summary>キーボードと設定画面の色のテーマ。</summary>
        public ColorTheme Theme { get; }

        /// <summary>トリガーの引き量がこの値以上で押したとみなす（<see cref="KeyboardSettings.TriggerThreshold"/>）。</summary>
        public float TriggerThreshold { get; }

        /// <summary>スティックの傾きがこの値以上で方向を決める（<see cref="KeyboardSettings.StickThreshold"/>）。</summary>
        public float StickThreshold { get; }

        /// <summary>トリガーを引いたままこの秒数が経つと連続入力を始める（<see cref="KeyboardSettings.RepeatDelaySeconds"/>）。</summary>
        public float RepeatDelaySeconds { get; }

        public VRChatSettings VRChat { get; }

        public PrivacySettings Privacy { get; }

        public UserSettings WithPlacement(KeyboardPlacement value) => new UserSettings(value, Theme, TriggerThreshold, StickThreshold, RepeatDelaySeconds, VRChat, Privacy);
        public UserSettings WithTheme(ColorTheme value) => new UserSettings(Placement, value, TriggerThreshold, StickThreshold, RepeatDelaySeconds, VRChat, Privacy);
        public UserSettings WithTriggerThreshold(float value) => new UserSettings(Placement, Theme, value, StickThreshold, RepeatDelaySeconds, VRChat, Privacy);
        public UserSettings WithStickThreshold(float value) => new UserSettings(Placement, Theme, TriggerThreshold, value, RepeatDelaySeconds, VRChat, Privacy);
        public UserSettings WithRepeatDelaySeconds(float value) => new UserSettings(Placement, Theme, TriggerThreshold, StickThreshold, value, VRChat, Privacy);
        public UserSettings WithVRChat(VRChatSettings value) => new UserSettings(Placement, Theme, TriggerThreshold, StickThreshold, RepeatDelaySeconds, value, Privacy);
        public UserSettings WithPrivacy(PrivacySettings value) => new UserSettings(Placement, Theme, TriggerThreshold, StickThreshold, RepeatDelaySeconds, VRChat, value);

        /// <summary>Core の設定にする。この設定で扱わない値（子音の手など）は Core の既定値。</summary>
        public KeyboardSettings ToKeyboardSettings()
        {
            return new KeyboardSettings(
                stickThreshold: StickThreshold,
                triggerThreshold: TriggerThreshold,
                repeatDelaySeconds: ToDouble(RepeatDelaySeconds),
                conversionEnabled: Privacy.UseGoogleConversion);
        }

        public bool Equals(UserSettings other)
        {
            return other != null
                && Placement.Equals(other.Placement)
                && Theme == other.Theme
                && TriggerThreshold == other.TriggerThreshold
                && StickThreshold == other.StickThreshold
                && RepeatDelaySeconds == other.RepeatDelaySeconds
                && VRChat.Equals(other.VRChat)
                && Privacy.Equals(other.Privacy);
        }

        public override bool Equals(object obj) => Equals(obj as UserSettings);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Placement.GetHashCode();
                hash = hash * 31 + (int)Theme;
                hash = hash * 31 + TriggerThreshold.GetHashCode();
                hash = hash * 31 + StickThreshold.GetHashCode();
                hash = hash * 31 + RepeatDelaySeconds.GetHashCode();
                hash = hash * 31 + VRChat.GetHashCode();
                hash = hash * 31 + Privacy.GetHashCode();
                return hash;
            }
        }

        static UserSettings CreateDefault()
        {
            var core = new KeyboardSettings();
            return new UserSettings(KeyboardPlacement.Default, ColorTheme.Dark, core.TriggerThreshold, core.StickThreshold, (float)core.RepeatDelaySeconds,
                VRChatSettings.Default, PrivacySettings.Default);
        }

        // float を double に広げると 0.35 が 0.34999999403953552 になるため、float として最短の 10進表記を経由する。
        static double ToDouble(float value)
        {
            return double.Parse(value.ToString("R", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }
    }
}
