using System;
using System.Collections.Generic;

namespace VRime.Core.Output
{
    public enum ChatboxMessageKind
    {
        /// <summary>確定キーによる送信。通知音の設定に従う。</summary>
        Send,

        /// <summary>入力中の文字の表示。通知音を鳴らさない。空文字列は表示を消す。</summary>
        Preview,

        /// <summary>入力バッファが空のときの確定キーによる表示の消去。空文字列を通知音なしで出す。</summary>
        Clear,
    }

    public readonly struct ChatboxMessage
    {
        public ChatboxMessage(ChatboxMessageKind kind, string text)
        {
            Kind = kind;
            Text = text ?? throw new ArgumentNullException(nameof(text));
        }

        public ChatboxMessageKind Kind { get; }
        public string Text { get; }
    }

    /// <summary>
    /// VRChat のチャットボックスへ出すメッセージの順序と間隔の制御（docs/spec/output.md「VRChat」）。
    /// 送信と消去は順にすべて出し、入力中の表示は最新の 1 件だけを出す。送信と消去を優先し、すべてで最小間隔を共有する。
    /// 時刻は呼び出し側が渡す（秒）。
    /// </summary>
    public sealed class ChatboxOutbox
    {
        readonly double _minIntervalSeconds;
        readonly Queue<ChatboxMessage> _sends = new Queue<ChatboxMessage>();
        double _lastSentAt = double.NegativeInfinity;

        // 入力中の表示として出したい文字列と、出した文字列。null は表示なし。
        string _desiredPreview;
        string _shownPreview;

        public ChatboxOutbox(double minIntervalSeconds = 1.0)
        {
            if (!(minIntervalSeconds >= 0.0))
                throw new ArgumentOutOfRangeException(nameof(minIntervalSeconds), minIntervalSeconds, "0 以上である必要があります。");
            _minIntervalSeconds = minIntervalSeconds;
        }

        /// <summary>出していない送信と消去の件数。</summary>
        public int PendingSendCount => _sends.Count;

        /// <summary>出していない入力中の表示（消去を含む）があるか。</summary>
        public bool HasPendingPreview => _desiredPreview != _shownPreview;

        /// <summary>
        /// 送信を加える。送信した文字列が入力中の表示に置き換わるため、入力中の表示は出していないものも含めて取り下げる。
        /// </summary>
        public void EnqueueSend(string message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (message.Length == 0) throw new ArgumentException("表示の消去は EnqueueClear で指定します。", nameof(message));
            Enqueue(new ChatboxMessage(ChatboxMessageKind.Send, message));
        }

        /// <summary>
        /// 表示の消去を加える。送信と同じ順序で出す。消去が入力中の表示も消すため、入力中の表示は出していないものも含めて取り下げる。
        /// </summary>
        public void EnqueueClear()
        {
            Enqueue(new ChatboxMessage(ChatboxMessageKind.Clear, string.Empty));
        }

        void Enqueue(ChatboxMessage message)
        {
            _sends.Enqueue(message);
            _desiredPreview = null;
            _shownPreview = null;
        }

        /// <summary>
        /// 入力中の表示を指定する。null は表示なしで、表示中なら消す。毎フレーム呼んでよく、出したものと同じなら何も出さない。
        /// </summary>
        public void SetPreview(string text)
        {
            if (text != null && text.Length == 0)
                throw new ArgumentException("表示なしは null で指定します。", nameof(text));
            _desiredPreview = text;
        }

        /// <summary>
        /// 入力中の表示を出していれば、間隔を待たずに消すことにして true を返す（呼び出し側が直ちに消去を出す）。終了の前に使う。
        /// </summary>
        public bool TryClearPreviewNow()
        {
            _desiredPreview = null;
            if (_shownPreview == null) return false;
            _shownPreview = null;
            return true;
        }

        /// <summary>
        /// 出してよい時刻になっていれば、次のメッセージを取り出して true を返す（これが送信の時刻になる）。
        /// 毎フレーム呼ぶ。
        /// </summary>
        public bool TryDequeue(double nowSeconds, out ChatboxMessage message)
        {
            message = default;
            if (nowSeconds - _lastSentAt < _minIntervalSeconds) return false;
            if (_sends.Count > 0)
            {
                message = _sends.Dequeue();
            }
            else if (HasPendingPreview)
            {
                message = new ChatboxMessage(ChatboxMessageKind.Preview, _desiredPreview ?? string.Empty);
                _shownPreview = _desiredPreview;
            }
            else
            {
                return false;
            }
            _lastSentAt = nowSeconds;
            return true;
        }
    }
}
