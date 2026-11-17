using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using Valve.VR;

namespace VRime.App.SteamVR
{
    /// <summary>
    /// オーバーレイのテクスチャを、Unity のレンダースレッドで SteamVR に渡す。
    /// <c>SetOverlayTexture</c> は、呼び出したスレッドの上で、テクスチャの Direct3D11 デバイスのイミディエイトコンテキストを使ってコピーする。
    /// イミディエイトコンテキストは複数のスレッドから同時に使えず、Unity はこれをレンダースレッドで使っている。
    /// そのため、メインスレッドから呼ぶとコンテキストを同時に使うことになり、GPU ドライバー内でクラッシュする。
    /// 渡す処理は <see cref="GL.IssuePluginEvent(IntPtr, int)"/> でレンダースレッドへ依頼する。
    /// 依頼は、それまでに積まれた描画コマンドの直後に実行されるため、渡されるテクスチャには必ず描画が完了した内容が入っている。
    /// レンダースレッドでの失敗は例外にできないため記録し、<see cref="ThrowIfFailed"/> でメインスレッドから送出する。
    /// </summary>
    public sealed class OverlayTextureSubmitter : IDisposable
    {
        delegate void RenderEventCallback(int eventId);

        // ネイティブから呼ばれ続けるため、デリゲートはガベージコレクションされないよう静的フィールドに保持する。
        // 初期化は記述順に行われるため、s_callback を s_callbackPtr より前に置くこと。
        static readonly RenderEventCallback s_callback = OnRenderEvent;
        static readonly IntPtr s_callbackPtr = Marshal.GetFunctionPointerForDelegate(s_callback);

        // 登録簿とその内部の状態（各インスタンスの失敗の記録を含む）の保護に使う。
        static readonly object s_lock = new object();
        static readonly Dictionary<int, OverlayTextureSubmitter> s_registry = new Dictionary<int, OverlayTextureSubmitter>();
        static int s_nextId;

        readonly ulong _handle;
        readonly IntPtr _nativeTexture;
        readonly int _id;
        bool _disposed;

        // レンダースレッドで最初に起きた失敗。s_lock の下で読み書きする。
        EVROverlayError _failedError = EVROverlayError.None;
        Exception _failedException;

        // 失敗が記録されたか。毎フレームの確認でロックを取らないためのもの
        // （ロックを取ると、レンダースレッドが SetOverlayTexture を実行している間メインスレッドが待たされる）。
        volatile bool _hasFailed;

        public OverlayTextureSubmitter(ulong overlayHandle, IntPtr nativeTexture)
        {
            if (overlayHandle == 0) throw new ArgumentException("オーバーレイのハンドルが不正です。", nameof(overlayHandle));
            if (nativeTexture == IntPtr.Zero) throw new ArgumentException("テクスチャのネイティブハンドルが不正です。", nameof(nativeTexture));
            _handle = overlayHandle;
            _nativeTexture = nativeTexture;
            lock (s_lock)
            {
                _id = ++s_nextId;
                s_registry.Add(_id, this);
            }
        }

        /// <summary>
        /// テクスチャを渡す処理をレンダースレッドに依頼する。メインスレッドから呼ぶ。
        /// 呼び出しの時点までに積まれた描画コマンドの完了後に実行される。
        /// </summary>
        public void Enqueue()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(OverlayTextureSubmitter));
            GL.IssuePluginEvent(s_callbackPtr, _id);
        }

        /// <summary>レンダースレッドで失敗が記録されていれば、それを例外にして送出する。メインスレッドから呼ぶ。</summary>
        public void ThrowIfFailed()
        {
            if (!_hasFailed) return;
            EVROverlayError error;
            Exception captured;
            lock (s_lock)
            {
                error = _failedError;
                captured = _failedException;
            }
            if (error != EVROverlayError.None) OpenVRErrors.Check(error, "SetOverlayTexture");
            if (captured != null) throw new InvalidOperationException("SetOverlayTexture に失敗しました。", captured);
        }

        /// <summary>
        /// 登録を解除する。冪等。
        /// 戻った後は、依頼済みのイベントが残っていても、コールバックがこのオーバーレイに触れることはない
        /// （コールバックは登録の確認から呼び出しの完了までロックを保持し、解除も同じロックの下で行うため）。
        /// これにより、続く <c>DestroyOverlay</c> と <c>OpenVR.Shutdown</c> を安全に実行できる。
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            lock (s_lock)
            {
                s_registry.Remove(_id);
            }
        }

        // レンダースレッドから呼ばれる。ここから例外が出るとプロセスが異常終了するため、必ず捕捉する。
        // Unity の API は呼ばない（レンダースレッドからは使えない）。
        [AOT.MonoPInvokeCallback(typeof(RenderEventCallback))]
        static void OnRenderEvent(int eventId)
        {
            try
            {
                // 解除と同時に DestroyOverlay が行われないよう、SetOverlayTexture の完了までロックを保持する。
                lock (s_lock)
                {
                    if (!s_registry.TryGetValue(eventId, out OverlayTextureSubmitter submitter)) return;
                    submitter.Submit();
                }
            }
            catch
            {
                // ロックの取得や登録簿の参照が失敗する状況は想定されない。記録する先もないため、握りつぶすしかない。
            }
        }

        // s_lock を保持して呼ぶこと。
        void Submit()
        {
            try
            {
                // 色空間: RenderTexture はリニア色空間のプロジェクトでは sRGB 形式（R8G8B8A8_SRGB）で、
                // 書き込まれたバイト列はすでにガンマ（sRGB）符号化されている。ディスプレイにそのまま出せる値のため Gamma を指定する。
                // Linear はバイト列がリニア値のときの指定で、ここで指定すると符号化済みの値をさらに符号化して白っぽくなる。
                var texture = new Texture_t
                {
                    handle = _nativeTexture,
                    eType = ETextureType.DirectX,
                    eColorSpace = EColorSpace.Gamma,
                };
                EVROverlayError error = OpenVR.Overlay.SetOverlayTexture(_handle, ref texture);
                if (error != EVROverlayError.None) RecordFailure(error, null);
            }
            catch (Exception e)
            {
                RecordFailure(EVROverlayError.None, e);
            }
        }

        // 最初の失敗だけを残す。s_lock を保持して呼ぶこと。
        void RecordFailure(EVROverlayError error, Exception exception)
        {
            if (_hasFailed) return;
            _failedError = error;
            _failedException = exception;
            _hasFailed = true;
        }
    }
}
