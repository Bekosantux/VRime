using VRime.Core.Input;

namespace VRime.Core
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

        /// <summary>
        /// 入力バッファの内容の送信の要求。送信を同期的に試み、成否を返す。
        /// true のときだけ Core は入力バッファを空にする。false のときは内容を残し、振動で失敗を知らせる。
        /// 失敗の理由の表示は App が行う（Core が受け取るのは成否だけで、理由は受け取らない）。
        /// </summary>
        bool OnSendRequested(string text);

        /// <summary>
        /// 入力バッファが空のときの確定キーによる、送信先の表示の消去の要求（docs/spec/output.md「入力バッファ」）。
        /// 表示を消せる送信先なら消去を受け付けて true を返し、それ以外は何もせずに false を返す。
        /// false のとき Core は振動で知らせる。App は通知を出さない（送信の失敗ではないため）。
        /// </summary>
        bool OnClearRequested();
    }
}
