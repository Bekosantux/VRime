using System;

namespace VRime.App.Output
{
    public enum OutputFailure
    {
        None,

        /// <summary>前面のウィンドウがない。</summary>
        NoWindow,

        /// <summary>前面が本アプリ自身のプロセス（エディタを含む）。</summary>
        OwnWindow,

        /// <summary>管理者権限のウィンドウへは Windows の仕様で送れない。</summary>
        ElevatedWindow,

        /// <summary>他のアプリがクリップボードを開いたままのため、デスクトップへ貼り付けられない。</summary>
        ClipboardBusy,

        /// <summary>送信の API が失敗した。</summary>
        SendFailed,
    }

    /// <summary>
    /// 送信の結果。失敗のときだけ、入力欄に出す短い日本語の通知（<see cref="Notice"/>）を持つ。通知は送信内容を含まない。
    /// 成功は通知しない。
    /// </summary>
    public readonly struct OutputResult
    {
        public static readonly OutputResult Succeeded = default;

        readonly string _notice;

        OutputResult(OutputFailure failure, string notice)
        {
            Failure = failure;
            _notice = notice;
        }

        public bool Success => Failure == OutputFailure.None;
        public OutputFailure Failure { get; }

        /// <summary>失敗の通知。成功の結果では例外。</summary>
        public string Notice => Success
            ? throw new InvalidOperationException("成功の結果には通知がありません。")
            : _notice;

        public static OutputResult Failed(OutputFailure failure, string notice)
        {
            if (failure == OutputFailure.None) throw new ArgumentException("失敗の種類が None です。", nameof(failure));
            if (string.IsNullOrEmpty(notice)) throw new ArgumentException("通知が空です。", nameof(notice));
            return new OutputResult(failure, notice);
        }
    }
}
