using System;

namespace VRKeyboard.App.Output
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

        /// <summary>送信の API が失敗した。</summary>
        SendFailed,
    }

    /// <summary>送信の結果。<see cref="Notice"/> は入力欄に出す短い日本語の通知で、送信内容を含まない。</summary>
    public readonly struct OutputResult
    {
        OutputResult(OutputFailure failure, string notice)
        {
            Failure = failure;
            Notice = notice ?? throw new ArgumentNullException(nameof(notice));
        }

        public bool Success => Failure == OutputFailure.None;
        public OutputFailure Failure { get; }
        public string Notice { get; }

        public static OutputResult Succeeded(string notice)
        {
            return new OutputResult(OutputFailure.None, notice);
        }

        public static OutputResult Failed(OutputFailure failure, string notice)
        {
            if (failure == OutputFailure.None) throw new ArgumentException("失敗の種類が None です。", nameof(failure));
            return new OutputResult(failure, notice);
        }
    }
}
