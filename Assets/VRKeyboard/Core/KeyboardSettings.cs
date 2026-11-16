using System;
using VRKeyboard.Core.Input;

namespace VRKeyboard.Core
{
    /// <summary>
    /// キーボードの動作設定。値は生成時に検証し、不正なら例外を投げる。
    /// </summary>
    public sealed class KeyboardSettings
    {
        public KeyboardSettings(
            Hand consonantHand = Hand.Right,
            float stickThreshold = 0.5f,
            float triggerThreshold = 0.7f,
            double chordWindowSeconds = 0.20,
            double doubleChordWindowSeconds = 0.60,
            double repeatDelaySeconds = 0.50,
            double repeatIntervalSeconds = 0.10,
            bool conversionEnabled = true)
        {
            if (consonantHand != Hand.Left && consonantHand != Hand.Right)
                throw new ArgumentOutOfRangeException(nameof(consonantHand), consonantHand, null);
            ValidateThreshold(stickThreshold, nameof(stickThreshold));
            ValidateThreshold(triggerThreshold, nameof(triggerThreshold));
            if (!(chordWindowSeconds > 0.0))
                throw new ArgumentOutOfRangeException(nameof(chordWindowSeconds), chordWindowSeconds, "正の値である必要があります。");
            if (!(doubleChordWindowSeconds > 0.0))
                throw new ArgumentOutOfRangeException(nameof(doubleChordWindowSeconds), doubleChordWindowSeconds, "正の値である必要があります。");
            ValidateSeconds(repeatDelaySeconds, nameof(repeatDelaySeconds));
            ValidateSeconds(repeatIntervalSeconds, nameof(repeatIntervalSeconds));

            ConsonantHand = consonantHand;
            StickThreshold = stickThreshold;
            TriggerThreshold = triggerThreshold;
            ChordWindowSeconds = chordWindowSeconds;
            DoubleChordWindowSeconds = doubleChordWindowSeconds;
            RepeatDelaySeconds = repeatDelaySeconds;
            RepeatIntervalSeconds = repeatIntervalSeconds;
            ConversionEnabled = conversionEnabled;
        }

        /// <summary>子音パネルを担当する手。母音パネルは反対の手。</summary>
        public Hand ConsonantHand { get; }

        public Hand VowelHand => ConsonantHand.Opposite();

        /// <summary>スティックの傾きがこの値以上で方向を決め、未満なら中央とみなす。</summary>
        public float StickThreshold { get; }

        /// <summary>トリガーの引き量がこの値以上で押したとみなし、未満なら離したとみなす。</summary>
        public float TriggerThreshold { get; }

        /// <summary>左右の展開・収納のボタンの押下時刻の差がこの秒数以内なら同時押しとみなす。</summary>
        public double ChordWindowSeconds { get; }

        /// <summary>同時押しをこの秒数以内に2回行うと開閉する。</summary>
        public double DoubleChordWindowSeconds { get; }

        /// <summary>トリガーを引いたままこの秒数が経つと、連続入力を始める（長押し時間）。</summary>
        public double RepeatDelaySeconds { get; }

        /// <summary>連続入力の2回目以降の間隔。</summary>
        public double RepeatIntervalSeconds { get; }

        /// <summary>変換を使えるか。false の間、「変換」は無効のセルになり、押すと無効の振動を返す（変換の要求を出さない）。</summary>
        public bool ConversionEnabled { get; }

        // 傾き・引き量は 0..1 で表されるため、1 を超える値には到達できない。0 以下だと、触れていなくても倒した・押したとみなす。
        static void ValidateThreshold(float value, string name)
        {
            if (!(value > 0f && value <= 1f))
                throw new ArgumentOutOfRangeException(name, value, "0 を超え 1 以下である必要があります。");
        }

        static void ValidateSeconds(double value, string name)
        {
            if (!(value > 0.0) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(name, value, "正の有限の値である必要があります。");
        }
    }
}
