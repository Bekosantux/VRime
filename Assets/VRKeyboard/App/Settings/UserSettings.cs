using System;
using VRKeyboard.Core;

namespace VRKeyboard.App.Settings
{
    /// <summary>
    /// 設定画面で変更し、設定ファイルに保存する値（SPEC 8章）。値は生成時に範囲を検査し、範囲外は例外にする。
    /// キーボードの動作に関わる値は <see cref="ToKeyboardSettings"/> で Core の設定にする。
    /// </summary>
    public sealed class UserSettings
    {
        public static readonly SettingRange TriggerThresholdRange = new SettingRange(0.25f, 0.95f, 0.05f);
        public static readonly SettingRange StickThresholdRange = new SettingRange(0.25f, 0.95f, 0.05f);

        /// <summary>しきい値の既定は Core の既定値に揃える。</summary>
        public static readonly UserSettings Default = CreateDefault();

        public UserSettings(KeyboardPlacement placement, float triggerThreshold, float stickThreshold)
        {
            Placement = placement ?? throw new ArgumentNullException(nameof(placement));
            if (!TriggerThresholdRange.Contains(triggerThreshold))
                throw new ArgumentOutOfRangeException(nameof(triggerThreshold), triggerThreshold,
                    TriggerThresholdRange.Min + "〜" + TriggerThresholdRange.Max + " である必要があります。");
            if (!StickThresholdRange.Contains(stickThreshold))
                throw new ArgumentOutOfRangeException(nameof(stickThreshold), stickThreshold,
                    StickThresholdRange.Min + "〜" + StickThresholdRange.Max + " である必要があります。");
            TriggerThreshold = triggerThreshold;
            StickThreshold = stickThreshold;
        }

        public KeyboardPlacement Placement { get; }

        /// <summary>トリガーの引き量がこの値以上で押したとみなす（<see cref="KeyboardSettings.TriggerPressThreshold"/>）。</summary>
        public float TriggerThreshold { get; }

        /// <summary>スティックの傾きがこの値以上で方向を決める（<see cref="KeyboardSettings.StickEnterThreshold"/>）。</summary>
        public float StickThreshold { get; }

        public UserSettings WithPlacement(KeyboardPlacement value) => new UserSettings(value, TriggerThreshold, StickThreshold);
        public UserSettings WithTriggerThreshold(float value) => new UserSettings(Placement, value, StickThreshold);
        public UserSettings WithStickThreshold(float value) => new UserSettings(Placement, TriggerThreshold, value);

        /// <summary>Core の設定にする。この設定で扱わない値（子音の手など）は Core の既定値。</summary>
        public KeyboardSettings ToKeyboardSettings()
        {
            return new KeyboardSettings(stickEnterThreshold: StickThreshold, triggerPressThreshold: TriggerThreshold);
        }

        public bool Equals(UserSettings other)
        {
            return other != null
                && Placement.Equals(other.Placement)
                && TriggerThreshold == other.TriggerThreshold
                && StickThreshold == other.StickThreshold;
        }

        public override bool Equals(object obj) => Equals(obj as UserSettings);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Placement.GetHashCode();
                hash = hash * 31 + TriggerThreshold.GetHashCode();
                hash = hash * 31 + StickThreshold.GetHashCode();
                return hash;
            }
        }

        static UserSettings CreateDefault()
        {
            var core = new KeyboardSettings();
            return new UserSettings(KeyboardPlacement.Default, core.TriggerPressThreshold, core.StickEnterThreshold);
        }
    }
}
