using VRKeyboard.Core.Input;

namespace VRKeyboard.Core
{
    /// <summary>Core から App への通知。Core の状態遷移の中で同期的に呼ばれる。</summary>
    public interface IKeyboardEffects
    {
        /// <summary>
        /// 開閉が変わった。App は入力の優先度とオーバーレイの表示を切り替える。
        /// ダッシュボードによる一時停止・再開でも呼ばれる。
        /// </summary>
        void OnOpenChanged(bool isOpen);

        void OnHaptic(Hand hand, HapticKind kind);

        /// <summary>変換の要求。結果は同じ requestId で CompleteConversion / FailConversion に返す。</summary>
        void OnConversionRequested(int requestId, string reading);

        /// <summary>入力バッファの内容の送信の要求。</summary>
        void OnSendRequested(string text);
    }
}
