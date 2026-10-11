using System;
using VRKeyboard.Core.Input;

namespace VRKeyboard.Core
{
    /// <summary>
    /// キーボードの動作設定。値は生成時に検証し、不正なら例外を投げる。
    /// スティックとトリガーのしきい値は、判定する量だけを持ち、戻りの判定はそこから <see cref="ReleaseMargin"/> を引いた値にする。
    /// </summary>
    public sealed class KeyboardSettings
    {
        /// <summary>しきい値と、戻りの判定（中央に戻る・離した）との差。差の間は直前の状態を保つ（チャタリングの防止）。</summary>
        public const float ReleaseMargin = 0.2f;

        public KeyboardSettings(
            Hand consonantHand = Hand.Right,
            float stickEnterThreshold = 0.5f,
            float triggerPressThreshold = 0.7f,
            double chordWindowSeconds = 0.20,
            double doubleChordWindowSeconds = 0.60,
            int maxLength = 144)
        {
            if (consonantHand != Hand.Left && consonantHand != Hand.Right)
                throw new ArgumentOutOfRangeException(nameof(consonantHand), consonantHand, null);
            ValidateThreshold(stickEnterThreshold, nameof(stickEnterThreshold));
            ValidateThreshold(triggerPressThreshold, nameof(triggerPressThreshold));
            if (!(chordWindowSeconds > 0.0))
                throw new ArgumentOutOfRangeException(nameof(chordWindowSeconds), chordWindowSeconds, "正の値である必要があります。");
            if (!(doubleChordWindowSeconds > 0.0))
                throw new ArgumentOutOfRangeException(nameof(doubleChordWindowSeconds), doubleChordWindowSeconds, "正の値である必要があります。");
            if (maxLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxLength), maxLength, "正の値である必要があります。");

            ConsonantHand = consonantHand;
            StickEnterThreshold = stickEnterThreshold;
            TriggerPressThreshold = triggerPressThreshold;
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
        public float StickExitThreshold => StickEnterThreshold - ReleaseMargin;

        /// <summary>トリガーの引き量がこの値以上で押したとみなす。</summary>
        public float TriggerPressThreshold { get; }

        /// <summary>押している間、トリガーの引き量がこの値未満になると離したとみなす。</summary>
        public float TriggerReleaseThreshold => TriggerPressThreshold - ReleaseMargin;

        /// <summary>左右のグリップの押下時刻の差がこの秒数以内なら同時押しとみなす。</summary>
        public double ChordWindowSeconds { get; }

        /// <summary>同時押しをこの秒数以内に2回行うと開閉する。</summary>
        public double DoubleChordWindowSeconds { get; }

        /// <summary>入力バッファの最大長（UTF-16 単位）。</summary>
        public int MaxLength { get; }

        // 傾き・引き量は 0..1 で表されるため、1 を超える値には到達できない。戻りの判定が 0 以下だと中央・離した状態に戻れない。
        static void ValidateThreshold(float value, string name)
        {
            if (!(value > ReleaseMargin && value <= 1f))
                throw new ArgumentOutOfRangeException(name, value, ReleaseMargin + " を超え 1 以下である必要があります。");
        }
    }
}
