using System;

namespace VRime.App.Conversion
{
    public enum ConversionFailureKind
    {
        /// <summary>接続できない（DNS の解決失敗・接続拒否・ネットワーク切断）。</summary>
        Connection,

        /// <summary>制限時間内に応答がなかった（docs/spec/conversion.md「エラー時の扱い」）。</summary>
        Timeout,

        /// <summary>HTTP のエラーステータスが返った。</summary>
        HttpError,

        /// <summary>応答を読み取れなかった。</summary>
        InvalidResponse,
    }

    /// <summary>
    /// 変換の通信に失敗した。<see cref="UserMessage"/> は入力欄の通知にそのまま出せる短い文言。
    /// <see cref="Exception.Message"/> は開発用で、入力した読みを含めない。
    /// </summary>
    public sealed class ConversionFailedException : Exception
    {
        public ConversionFailedException(ConversionFailureKind kind, string developerMessage, Exception inner = null)
            : base(developerMessage, inner)
        {
            Kind = kind;
        }

        public ConversionFailureKind Kind { get; }

        public string UserMessage
        {
            get
            {
                switch (Kind)
                {
                    case ConversionFailureKind.Connection: return "変換サーバーに接続できません";
                    case ConversionFailureKind.Timeout: return "変換サーバーから応答がありません";
                    case ConversionFailureKind.HttpError: return "変換サーバーがエラーを返しました";
                    case ConversionFailureKind.InvalidResponse: return "変換結果を読み取れませんでした";
                    default: throw new InvalidOperationException("未知の種類: " + Kind);
                }
            }
        }
    }
}
