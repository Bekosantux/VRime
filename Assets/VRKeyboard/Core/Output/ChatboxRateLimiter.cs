using System;
using System.Collections.Generic;

namespace VRKeyboard.Core.Output
{
    /// <summary>
    /// VRChat のチャットボックスへの送信間隔の制御（SPEC 7.3）。
    /// 前回の送信から最小間隔以上空け、間隔内の送信はキューに入れて順に取り出す。
    /// 時刻は呼び出し側が渡す（秒）。
    /// </summary>
    public sealed class ChatboxRateLimiter
    {
        readonly double _minIntervalSeconds;
        readonly Queue<string> _queue = new Queue<string>();
        double _lastSentAt = double.NegativeInfinity;

        public ChatboxRateLimiter(double minIntervalSeconds = 1.0)
        {
            if (!(minIntervalSeconds >= 0.0))
                throw new ArgumentOutOfRangeException(nameof(minIntervalSeconds), minIntervalSeconds, "0 以上である必要があります。");
            _minIntervalSeconds = minIntervalSeconds;
        }

        public int PendingCount => _queue.Count;

        public void Enqueue(string message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            _queue.Enqueue(message);
        }

        /// <summary>
        /// 送信してよい時刻になっていれば、キューの先頭を取り出して true を返す（これが送信の時刻になる）。
        /// 毎フレーム呼ぶ。
        /// </summary>
        public bool TryDequeue(double nowSeconds, out string message)
        {
            if (_queue.Count == 0 || nowSeconds - _lastSentAt < _minIntervalSeconds)
            {
                message = null;
                return false;
            }
            message = _queue.Dequeue();
            _lastSentAt = nowSeconds;
            return true;
        }
    }
}
