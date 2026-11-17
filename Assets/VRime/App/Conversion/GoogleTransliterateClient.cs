using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using VRime.Core.Conversion;

namespace VRime.App.Conversion
{
    /// <summary>
    /// Google CGI API for Japanese Input（SPEC 6.1）への変換リクエスト。メインスレッドから呼ぶ。
    /// URL の組み立てと応答の解析は Core の <see cref="TransliterateApi"/> が行い、ここは通信だけを担う。
    /// 失敗は <see cref="ConversionFailedException"/>、キャンセルは <see cref="OperationCanceledException"/>。
    /// 例外のメッセージに読みや URL を含めない（SPEC 6.2）。
    /// </summary>
    public sealed class GoogleTransliterateClient
    {
        /// <summary>SPEC 6.4 のタイムアウト。</summary>
        public const float DefaultTimeoutSeconds = 2f;

        readonly float _timeoutSeconds;

        public GoogleTransliterateClient(float timeoutSeconds = DefaultTimeoutSeconds)
        {
            if (!(timeoutSeconds > 0f))
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), timeoutSeconds, "0 より大きい必要があります。");
            _timeoutSeconds = timeoutSeconds;
        }

        public async Awaitable<IReadOnlyList<ConversionSegment>> TransliterateAsync(string reading, CancellationToken cancellationToken)
        {
            string url = TransliterateApi.BuildRequestUrl(reading);
            string body = await FetchAsync(url, cancellationToken);
            try
            {
                return TransliterateApi.ParseResponse(body);
            }
            catch (FormatException e)
            {
                throw new ConversionFailedException(ConversionFailureKind.InvalidResponse, "変換結果の形式が想定と異なります。", e);
            }
        }

        // 検証用に URL を差し替えて呼べるよう、URL の組み立てから分けてある。
        internal async Awaitable<string> FetchAsync(string url, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                // 制限時間は自前の時計で測る（Time は使わない。エディタの停止中や timeScale に左右されないため）。
                // request.timeout は自前の判定が止まった場合の保険で、こちらが先に働くよう長めにする。
                request.timeout = Mathf.CeilToInt(_timeoutSeconds) + 1;
                Stopwatch stopwatch = Stopwatch.StartNew();
                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                try
                {
                    while (!operation.isDone)
                    {
                        if (stopwatch.Elapsed.TotalSeconds >= _timeoutSeconds)
                            throw new ConversionFailedException(ConversionFailureKind.Timeout, "変換サーバーが制限時間内に応答しませんでした。");
                        await Awaitable.NextFrameAsync(cancellationToken);
                    }
                }
                finally
                {
                    if (!operation.isDone) request.Abort();
                }

                switch (request.result)
                {
                    case UnityWebRequest.Result.Success:
                        return request.downloadHandler.text;
                    case UnityWebRequest.Result.ConnectionError:
                        throw new ConversionFailedException(ConversionFailureKind.Connection, "変換サーバーに接続できませんでした。");
                    case UnityWebRequest.Result.ProtocolError:
                        throw new ConversionFailedException(ConversionFailureKind.HttpError, "変換サーバーが HTTP " + request.responseCode + " を返しました。");
                    case UnityWebRequest.Result.DataProcessingError:
                        throw new ConversionFailedException(ConversionFailureKind.InvalidResponse, "変換サーバーの応答を受信データとして処理できませんでした。");
                    default:
                        throw new InvalidOperationException("未知の通信結果: " + request.result);
                }
            }
        }
    }
}
