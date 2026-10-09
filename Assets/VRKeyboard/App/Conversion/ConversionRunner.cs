using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using VRKeyboard.Core;
using VRKeyboard.Core.Conversion;

namespace VRKeyboard.App.Conversion
{
    /// <summary>
    /// 変換の要求を <see cref="GoogleTransliterateClient"/> に渡し、結果を <see cref="KeyboardSession"/> へ返す。
    /// 通信は同時に 1件だけで、新しい要求・取り消し・破棄のときは前の通信を中止する。
    /// </summary>
    public sealed class ConversionRunner : IDisposable
    {
        readonly KeyboardSession _session;
        readonly GoogleTransliterateClient _client;
        CancellationTokenSource _current;

        public ConversionRunner(KeyboardSession session, GoogleTransliterateClient client)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        /// <summary>通信中かどうか。</summary>
        public bool IsRunning => _current != null;

        public void Start(int requestId, string reading)
        {
            if (string.IsNullOrEmpty(reading)) throw new ArgumentException("変換する読みが空です。", nameof(reading));
            Cancel();
            var source = new CancellationTokenSource();
            _current = source;
            Run(requestId, reading, source);
        }

        /// <summary>
        /// 応答待ちでなくなっていれば（閉じた・取り消した）通信を中止する。
        /// 中止しなくても Core が古い requestId の結果を捨てるが、通信を続ける理由がない。
        /// </summary>
        public void CancelUnlessWaiting()
        {
            if (_current != null && _session.Mode != KeyboardMode.WaitingConversion) Cancel();
        }

        public void Cancel()
        {
            _current?.Cancel();
        }

        public void Dispose()
        {
            Cancel();
        }

        // 呼び出し側は待たないため async void にしている。すべての例外はここで処理する。
        async void Run(int requestId, string reading, CancellationTokenSource source)
        {
            try
            {
                IReadOnlyList<ConversionSegment> segments = await _client.TransliterateAsync(reading, source.Token);
                if (!source.IsCancellationRequested) _session.CompleteConversion(requestId, segments);
            }
            catch (OperationCanceledException)
            {
                // 中止は正常な経路。結果は不要。
            }
            catch (ConversionFailedException e)
            {
                if (!source.IsCancellationRequested) _session.FailConversion(requestId, e.UserMessage);
            }
            catch (Exception e)
            {
                // 想定外の失敗。応答待ちのまま止まらないよう、代わりの候補で変換中にしてから、原因を記録する。
                Debug.LogException(e);
                if (!source.IsCancellationRequested) _session.FailConversion(requestId, "変換中に問題が発生しました");
            }
            finally
            {
                if (ReferenceEquals(_current, source)) _current = null;
                source.Dispose();
            }
        }
    }
}
