using System;
using UnityEngine;
using Valve.VR;

namespace VRKeyboard.App.SteamVR
{
    /// <summary>
    /// キーボードを SteamVR のオーバーレイとして表示する。HMD に対する相対位置で置く頭部固定（位置は <see cref="OverlayPlacement"/>）。
    /// テクスチャは Unity の RenderTexture（Direct3D11）をそのまま渡す。渡す処理はレンダースレッドで行う（<see cref="OverlayTextureSubmitter"/>）。
    /// </summary>
    public sealed class KeyboardOverlay : IDisposable
    {
        /// <summary>オーバーレイのキー。接続ごとに一意。同じ接続の中で重複すると作成が失敗する。</summary>
        public const string OverlayKey = "vrkeyboard.keyboard";

        public const string FriendlyName = "VR Keyboard";

        OverlayTextureSubmitter _submitter;
        ulong _handle;
        bool _visible;

        public KeyboardOverlay(RenderTexture texture)
        {
            if (texture == null) throw new ArgumentNullException(nameof(texture));
            if (!texture.IsCreated()) throw new ArgumentException("RenderTexture が作成されていません。", nameof(texture));
            IntPtr nativeTexture = texture.GetNativeTexturePtr();
            if (nativeTexture == IntPtr.Zero) throw new InvalidOperationException("RenderTexture のネイティブハンドルを取得できません。");

            CVROverlay overlay = OpenVR.Overlay;
            OpenVRErrors.Check(overlay.CreateOverlay(OverlayKey, FriendlyName, ref _handle), "CreateOverlay(" + OverlayKey + ")");
            try
            {
                OpenVRErrors.Check(overlay.SetOverlayWidthInMeters(_handle, OverlayPlacement.WidthMeters), "SetOverlayWidthInMeters");

                // uGUI の出力は、アルファを乗算済みで RenderTexture に書き込まれる
                // （UI/Default・TextMeshPro のシェーダーはどちらも Blend One OneMinusSrcAlpha で、アルファ 0.5 の白は RGB 188・A 128 になる）。
                // SteamVR の既定は乗算済みでない前提のため、フラグで乗算済みであることを伝える。
                OpenVRErrors.Check(overlay.SetOverlayFlag(_handle, VROverlayFlags.IsPremultiplied, true), "SetOverlayFlag(IsPremultiplied)");

                // D3D11 の RenderTexture は上下が逆になるため、v を反転する（SPEC 3.3）。
                var bounds = new VRTextureBounds_t { uMin = 0f, uMax = 1f, vMin = 1f, vMax = 0f };
                OpenVRErrors.Check(overlay.SetOverlayTextureBounds(_handle, ref bounds), "SetOverlayTextureBounds");

                float[] m = OverlayPlacement.ComputeOverlayToHmd();
                var transform = new HmdMatrix34_t
                {
                    m0 = m[0], m1 = m[1], m2 = m[2], m3 = m[3],
                    m4 = m[4], m5 = m[5], m6 = m[6], m7 = m[7],
                    m8 = m[8], m9 = m[9], m10 = m[10], m11 = m[11],
                };
                OpenVRErrors.Check(
                    overlay.SetOverlayTransformTrackedDeviceRelative(_handle, OpenVR.k_unTrackedDeviceIndex_Hmd, ref transform),
                    "SetOverlayTransformTrackedDeviceRelative");

                _submitter = new OverlayTextureSubmitter(_handle, nativeTexture);
            }
            catch
            {
                overlay.DestroyOverlay(_handle);
                _handle = 0;
                throw;
            }
        }

        public bool IsVisible => _visible;

        public void Show()
        {
            ThrowIfDisposed();
            if (_visible) return;
            OpenVRErrors.Check(OpenVR.Overlay.ShowOverlay(_handle), "ShowOverlay");
            _visible = true;
        }

        public void Hide()
        {
            ThrowIfDisposed();
            if (!_visible) return;
            OpenVRErrors.Check(OpenVR.Overlay.HideOverlay(_handle), "HideOverlay");
            _visible = false;
        }

        /// <summary>
        /// テクスチャの内容が新しく描かれた。テクスチャを渡す処理をレンダースレッドに依頼する。
        /// 依頼は描画コマンドの後ろに積まれるため、渡されるテクスチャには描画が完了した内容が入っている。
        /// </summary>
        public void NotifyRendered()
        {
            ThrowIfDisposed();
            _submitter.Enqueue();
        }

        /// <summary>毎フレーム呼ぶ。レンダースレッドでテクスチャを渡す処理が失敗していれば、例外にして送出する。</summary>
        public void Update()
        {
            ThrowIfDisposed();
            _submitter.ThrowIfFailed();
        }

        /// <summary>オーバーレイを破棄する。<see cref="OpenVRRuntime"/> を閉じる前に呼ぶこと。</summary>
        public void Dispose()
        {
            if (_handle == 0) return;
            ulong handle = _handle;
            _handle = 0;
            _visible = false;
            // 先にレンダースレッドからの参照を断つ。これが戻った後はテクスチャを渡す処理がこのオーバーレイに触れないため、破棄してよい。
            _submitter?.Dispose();
            _submitter = null;
            OpenVRErrors.Check(OpenVR.Overlay.DestroyOverlay(handle), "DestroyOverlay");
        }

        void ThrowIfDisposed()
        {
            if (_handle == 0) throw new ObjectDisposedException(nameof(KeyboardOverlay));
        }
    }
}
