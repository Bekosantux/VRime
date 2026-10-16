using System;

namespace VRKeyboard.Core.Input
{
    /// <summary>
    /// 長押しによる連続入力の時刻を管理する。押した時刻から遅延の後に最初の連続入力、以降は間隔ごとに連続入力する。
    /// 間隔より長いフレームが挟まっても、1回の <see cref="Poll"/> で連続入力するのは1回まで（遅れを取り戻すためにまとめて入力しない）。
    /// </summary>
    public sealed class KeyRepeater
    {
        Hand _hand;
        double _nextAt;

        public bool IsActive { get; private set; }

        /// <summary>連続入力しているトリガーの手。</summary>
        public Hand Hand
        {
            get
            {
                if (!IsActive) throw new InvalidOperationException("連続入力していません。");
                return _hand;
            }
        }

        public void Start(Hand hand, double pressedAtSeconds, double delaySeconds)
        {
            if (hand != Hand.Left && hand != Hand.Right)
                throw new ArgumentOutOfRangeException(nameof(hand), hand, null);
            if (!(delaySeconds > 0.0))
                throw new ArgumentOutOfRangeException(nameof(delaySeconds), delaySeconds, "正の値である必要があります。");
            _hand = hand;
            _nextAt = pressedAtSeconds + delaySeconds;
            IsActive = true;
        }

        public void Stop()
        {
            IsActive = false;
        }

        /// <summary>毎フレーム呼ぶ。連続入力する時刻に達していれば true を返し、次の時刻を進める。</summary>
        public bool Poll(double nowSeconds, double intervalSeconds)
        {
            if (!(intervalSeconds > 0.0))
                throw new ArgumentOutOfRangeException(nameof(intervalSeconds), intervalSeconds, "正の値である必要があります。");
            if (!IsActive || nowSeconds < _nextAt) return false;
            _nextAt += intervalSeconds;
            if (_nextAt <= nowSeconds) _nextAt = nowSeconds + intervalSeconds;
            return true;
        }
    }
}
