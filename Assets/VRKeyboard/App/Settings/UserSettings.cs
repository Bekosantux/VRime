using System;
using System.Globalization;
using VRKeyboard.Core;

namespace VRKeyboard.App.Settings
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

        /// <summary>しきい値と長押しの時間の既定は Core の既定値に揃える。</summary>
        public static readonly UserSettings Default = CreateDefault();

        public UserSettings(KeyboardPlacement placement, float triggerThreshold, float stickThreshold, float repeatDelaySeconds,
            VRChatSettings vrchat)
        {
            Placement = placement ?? throw new ArgumentNullException(nameof(placement));
            VRChat = vrchat ?? throw new ArgumentNullException(nameof(vrchat));
            if (!TriggerThresholdRange.Contains(triggerThreshold))
                throw new ArgumentOutOfRangeException(nameof(triggerThreshold), triggerThreshold,
                    TriggerThresholdRange.Min + "〜" + TriggerThresholdRange.Max + " である必要があります。");
            if (!StickThresholdRange.Contains(stickThreshold))
                throw new ArgumentOutOfRangeException(nameof(stickThreshold), stickThreshold,
                    StickThresholdRange.Min + "〜" + StickThresholdRange.Max + " である必要があります。");
            if (!RepeatDelaySecondsRange.Contains(repeatDelaySeconds))
                throw new ArgumentOutOfRangeException(nameof(repeatDelaySeconds), repeatDelaySeconds,
                    RepeatDelaySecondsRange.Min + "〜" + RepeatDelaySecondsRange.Max + " である必要があります。");
            TriggerThreshold = triggerThreshold;
            StickThreshold = stickThreshold;
            RepeatDelaySeconds = repeatDelaySeconds;
        }

        public KeyboardPlacement Placement { get; }

        /// <summary>トリガーの引き量がこの値以上で押したとみなす（<see cref="KeyboardSettings.TriggerThreshold"/>）。</summary>
        public float TriggerThreshold { get; }

        /// <summary>スティックの傾きがこの値以上で方向を決める（<see cref="KeyboardSettings.StickThreshold"/>）。</summary>
        public float StickThreshold { get; }

        /// <summary>トリガーを引いたままこの秒数が経つと連続入力を始める（<see cref="KeyboardSettings.RepeatDelaySeconds"/>）。</summary>
        public float RepeatDelaySeconds { get; }

        public VRChatSettings VRChat { get; }

        public UserSettings WithPlacement(KeyboardPlacement value) => new UserSettings(value, TriggerThreshold, StickThreshold, RepeatDelaySeconds, VRChat);
        public UserSettings WithTriggerThreshold(float value) => new UserSettings(Placement, value, StickThreshold, RepeatDelaySeconds, VRChat);
        public UserSettings WithStickThreshold(float value) => new UserSettings(Placement, TriggerThreshold, value, RepeatDelaySeconds, VRChat);
        public UserSettings WithRepeatDelaySeconds(float value) => new UserSettings(Placement, TriggerThreshold, StickThreshold, value, VRChat);
        public UserSettings WithVRChat(VRChatSettings value) => new UserSettings(Placement, TriggerThreshold, StickThreshold, RepeatDelaySeconds, value);

        /// <summary>Core の設定にする。この設定で扱わない値（子音の手など）は Core の既定値。</summary>
        public KeyboardSettings ToKeyboardSettings()
        {
            return new KeyboardSettings(
                stickThreshold: StickThreshold,
                triggerThreshold: TriggerThreshold,
                repeatDelaySeconds: ToDouble(RepeatDelaySeconds));
        }

        public bool Equals(UserSettings other)
        {
            return other != null
                && Placement.Equals(other.Placement)
                && TriggerThreshold == other.TriggerThreshold
                && StickThreshold == other.StickThreshold
                && RepeatDelaySeconds == other.RepeatDelaySeconds
                && VRChat.Equals(other.VRChat);
        }

        public override bool Equals(object obj) => Equals(obj as UserSettings);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Placement.GetHashCode();
                hash = hash * 31 + TriggerThreshold.GetHashCode();
                hash = hash * 31 + StickThreshold.GetHashCode();
                hash = hash * 31 + RepeatDelaySeconds.GetHashCode();
                hash = hash * 31 + VRChat.GetHashCode();
                return hash;
            }
        }

        static UserSettings CreateDefault()
        {
            var core = new KeyboardSettings();
            return new UserSettings(KeyboardPlacement.Default, core.TriggerThreshold, core.StickThreshold, (float)core.RepeatDelaySeconds,
                VRChatSettings.Default);
        }

        // float を double に広げると 0.35 が 0.34999999403953552 になるため、float として最短の 10進表記を経由する。
        static double ToDouble(float value)
        {
            return double.Parse(value.ToString("R", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }
    }
}
