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
            float stickEnterThreshold = 0.5f,
            float stickExitThreshold = 0.3f,
            double chordWindowSeconds = 0.20,
            double doubleChordWindowSeconds = 0.60,
            int maxLength = 144)
        {
            if (consonantHand != Hand.Left && consonantHand != Hand.Right)
                throw new ArgumentOutOfRangeException(nameof(consonantHand), consonantHand, null);
            // 傾きは 0..1 で表されるため、1 を超える enter は到達できない。
            if (!(stickEnterThreshold > 0f && stickEnterThreshold <= 1f))
                throw new ArgumentOutOfRangeException(nameof(stickEnterThreshold), stickEnterThreshold, "0 を超え 1 以下である必要があります。");
            if (!(stickExitThreshold > 0f && stickExitThreshold < stickEnterThreshold))
                throw new ArgumentOutOfRangeException(nameof(stickExitThreshold), stickExitThreshold, "0 を超え enter 未満である必要があります。");
            if (!(chordWindowSeconds > 0.0))
                throw new ArgumentOutOfRangeException(nameof(chordWindowSeconds), chordWindowSeconds, "正の値である必要があります。");
            if (!(doubleChordWindowSeconds > 0.0))
                throw new ArgumentOutOfRangeException(nameof(doubleChordWindowSeconds), doubleChordWindowSeconds, "正の値である必要があります。");
            if (maxLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxLength), maxLength, "正の値である必要があります。");

            ConsonantHand = consonantHand;
            StickEnterThreshold = stickEnterThreshold;
            StickExitThreshold = stickExitThreshold;
            ChordWindowSeconds = chordWindowSeconds;
            DoubleChordWindowSeconds = doubleChordWindowSeconds;
            MaxLength = maxLength;
        }

        /// <summary>子音パネルを担当する手。母音パネルは反対の手。</summary>
        public Hand ConsonantHand { get; }

        public Hand VowelHand => ConsonantHand.Opposite();

        /// <summary>スティックの傾きがこの値以上で方向を決める。</summary>
        public float StickEnterThreshold { get; }

        /// <summary>スティックの傾きがこの値未満で中央に戻る。</summary>
        public float StickExitThreshold { get; }

        /// <summary>左右のグリップの押下時刻の差がこの秒数以内なら同時押しとみなす。</summary>
        public double ChordWindowSeconds { get; }

        /// <summary>同時押しをこの秒数以内に2回行うと開閉する。</summary>
        public double DoubleChordWindowSeconds { get; }

        /// <summary>入力バッファの最大長（UTF-16 単位）。</summary>
        public int MaxLength { get; }
    }
}
