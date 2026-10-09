using System;

namespace VRKeyboard.Core.Control
{
    /// <summary>
    /// 両手グリップの「2回続けての同時押し」を検出する（SPEC 4.2）。
    /// 同時押し = 後から押した側の押下時に、もう一方が押されたままで、押下時刻の差が chordWindow 以内。
    /// </summary>
    public sealed class DoubleChordDetector
    {
        readonly double _chordWindow;
        readonly double _doubleWindow;
        bool _leftHeld;
        bool _rightHeld;
        double _leftDownAt = double.NegativeInfinity;
        double _rightDownAt = double.NegativeInfinity;
        double _lastChordAt = double.NegativeInfinity;

        public DoubleChordDetector(double chordWindowSeconds, double doubleChordWindowSeconds)
        {
            if (!(chordWindowSeconds > 0.0))
                throw new ArgumentOutOfRangeException(nameof(chordWindowSeconds), chordWindowSeconds, "正の値である必要があります。");
            if (!(doubleChordWindowSeconds > 0.0))
                throw new ArgumentOutOfRangeException(nameof(doubleChordWindowSeconds), doubleChordWindowSeconds, "正の値である必要があります。");
            _chordWindow = chordWindowSeconds;
            _doubleWindow = doubleChordWindowSeconds;
        }

        /// <summary>毎フレーム呼ぶ。2回目の同時押しが成立したフレームで true を返す。</summary>
        public bool Update(bool leftGrip, bool rightGrip, double timeSeconds)
        {
            bool chord = false;
            if (rightGrip && !_rightHeld)
            {
                _rightDownAt = timeSeconds;
                if (leftGrip && timeSeconds - _leftDownAt <= _chordWindow) chord = true;
            }
            if (leftGrip && !_leftHeld)
            {
                _leftDownAt = timeSeconds;
                if (rightGrip && timeSeconds - _rightDownAt <= _chordWindow) chord = true;
            }
            _leftHeld = leftGrip;
            _rightHeld = rightGrip;

            if (!chord) return false;

            if (timeSeconds - _lastChordAt <= _doubleWindow)
            {
                _lastChordAt = double.NegativeInfinity;
                return true;
            }
            _lastChordAt = timeSeconds;
            return false;
        }
    }
}
