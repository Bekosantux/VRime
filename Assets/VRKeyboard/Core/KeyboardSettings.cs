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
            int maxLength = 144)
        {
            if (consonantHand != Hand.Left && consonantHand != Hand.Right)
                throw new ArgumentOutOfRangeException(nameof(consonantHand), consonantHand, null);
            ValidateThreshold(stickThreshold, nameof(stickThreshold));
            ValidateThreshold(triggerThreshold, nameof(triggerThreshold));
            if (!(chordWindowSeconds > 0.0))
                throw new ArgumentOutOfRangeException(nameof(chordWindowSeconds), chordWindowSeconds, "正の値である必要があります。");
            if (!(doubleChordWindowSeconds > 0.0))
                throw new ArgumentOutOfRangeException(nameof(doubleChordWindowSeconds), doubleChordWindowSeconds, "正の値である必要があります。");
            if (maxLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxLength), maxLength, "正の値である必要があります。");

            ConsonantHand = consonantHand;
            StickThreshold = stickThreshold;
            TriggerThreshold = triggerThreshold;
            ChordWindowSeconds = chordWindowSeconds;
            DoubleChordWindowSeconds = doubleChordWindowSeconds;
            MaxLength = maxLength;
        }

        /// <summary>子音パネルを担当する手。母音パネルは反対の手。</summary>
        public Hand ConsonantHand { get; }

        public Hand VowelHand => ConsonantHand.Opposite();

        /// <summary>スティックの傾きがこの値以上で方向を決め、未満なら中央とみなす。</summary>
        public float StickThreshold { get; }

        /// <summary>トリガーの引き量がこの値以上で押したとみなし、未満なら離したとみなす。</summary>
        public float TriggerThreshold { get; }

        /// <summary>左右のグリップの押下時刻の差がこの秒数以内なら同時押しとみなす。</summary>
        public double ChordWindowSeconds { get; }

        /// <summary>同時押しをこの秒数以内に2回行うと開閉する。</summary>
        public double DoubleChordWindowSeconds { get; }

        /// <summary>入力バッファの最大長（UTF-16 単位）。</summary>
        public int MaxLength { get; }

        // 傾き・引き量は 0..1 で表されるため、1 を超える値には到達できない。0 以下だと、触れていなくても倒した・押したとみなす。
        static void ValidateThreshold(float value, string name)
        {
            if (!(value > 0f && value <= 1f))
                throw new ArgumentOutOfRangeException(name, value, "0 を超え 1 以下である必要があります。");
        }
    }
}
