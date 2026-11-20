using System;

namespace VRime.App.Output
{
    public enum ClipboardRestoreAction
    {
        /// <summary>何もしない（復元待ちがない、または時刻がまだ来ていない）。</summary>
        None,

        /// <summary>退避した内容を戻す。戻せたら <see cref="ClipboardRestoreSchedule{T}.Complete"/> を呼ぶ。戻せなければ次の呼び出しで再び返る。</summary>
        Restore,

        /// <summary>送信で置いた内容が別のアプリに書き換えられたため、戻さずに退避を捨てた。</summary>
        Discarded,

        /// <summary>期限までに戻せなかったため、退避を捨てた。</summary>
        GaveUp,
    }

    /// <summary>
    /// 送信のためにクリップボードへ置いた内容を、退避しておいた元の内容へ戻す時期を決める（docs/spec/output.md「クリップボードの扱い」）。Win32 に依存しない。
    /// 書き換えの検出にはクリップボードのシーケンス番号（GetClipboardSequenceNumber）を使う。送信で書き込んだ直後のシーケンス番号から変わっていれば、
    /// 利用者や別のアプリが新しい内容を置いたとみなし、戻さない。
    /// </summary>
    /// <typeparam name="T">退避した内容。</typeparam>
    public sealed class ClipboardRestoreSchedule<T> where T : class
    {
        readonly double _delaySeconds;
        readonly double _giveUpSeconds;

        T _snapshot;
        uint _writtenSequence;
        double _dueTime;

        /// <param name="delaySeconds">書き込んでから戻すまでの時間。送信先が貼り付けを処理し終えるのを待つ。</param>
        /// <param name="giveUpSeconds">戻す時刻を過ぎてから、戻せないまま諦めるまでの時間。</param>
        public ClipboardRestoreSchedule(double delaySeconds, double giveUpSeconds)
        {
            if (!(delaySeconds > 0)) throw new ArgumentOutOfRangeException(nameof(delaySeconds), delaySeconds, "正の値である必要があります。");
            if (!(giveUpSeconds > 0)) throw new ArgumentOutOfRangeException(nameof(giveUpSeconds), giveUpSeconds, "正の値である必要があります。");
            _delaySeconds = delaySeconds;
            _giveUpSeconds = giveUpSeconds;
        }

        public bool HasPending => _snapshot != null;

        /// <summary>
        /// 送信の前に、引き継ぐべき退避を取り出す。復元待ちがあり、シーケンス番号が書き込んだときのままなら、その退避を返す
        /// （続けて送ったときに、送信で置いた内容ではなく利用者の元の内容を退避し続けるため）。
        /// シーケンス番号が変わっていれば、復元待ちを捨てて false を返す（呼び出し側は現在の内容を再度退避する）。
        /// </summary>
        public bool TryTakePending(uint currentSequence, out T snapshot)
        {
            snapshot = _snapshot;
            bool carried = _snapshot != null && currentSequence == _writtenSequence;
            Clear();
            if (!carried) snapshot = null;
            return carried;
        }

        /// <summary>送信の内容を書き込んだ後に呼ぶ。<paramref name="writtenSequence"/> は書き込んだ直後のシーケンス番号。</summary>
        public void Arm(T snapshot, uint writtenSequence, double now)
        {
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            _writtenSequence = writtenSequence;
            _dueTime = now + _delaySeconds;
        }

        /// <summary>毎フレーム呼び、行うべきことを返す。<see cref="ClipboardRestoreAction.Restore"/> のときは <see cref="Pending"/> を戻す。</summary>
        public ClipboardRestoreAction Poll(double now, uint currentSequence)
        {
            if (_snapshot == null) return ClipboardRestoreAction.None;
            if (currentSequence != _writtenSequence)
            {
                Clear();
                return ClipboardRestoreAction.Discarded;
            }
            if (now < _dueTime) return ClipboardRestoreAction.None;
            if (now >= _dueTime + _giveUpSeconds)
            {
                Clear();
                return ClipboardRestoreAction.GaveUp;
            }
            return ClipboardRestoreAction.Restore;
        }

        /// <summary>戻す内容。復元待ちがないときは例外。</summary>
        public T Pending => _snapshot ?? throw new InvalidOperationException("復元待ちの内容がありません。");

        /// <summary>シーケンス番号が書き込んだときのままか（時刻を問わず、いま戻してよいか）。復元待ちがないときは false。</summary>
        public bool IsUnchanged(uint currentSequence)
        {
            return _snapshot != null && currentSequence == _writtenSequence;
        }

        /// <summary>戻し終えた、または戻すのをやめたときに呼ぶ。</summary>
        public void Complete()
        {
            if (_snapshot == null) throw new InvalidOperationException("復元待ちの内容がありません。");
            Clear();
        }

        void Clear()
        {
            _snapshot = null;
            _writtenSequence = 0;
            _dueTime = 0;
        }
    }
}
