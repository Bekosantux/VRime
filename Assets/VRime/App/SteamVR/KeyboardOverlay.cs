using System;
using UnityEngine;
using Valve.VR;
using VRime.App.Settings;

namespace VRime.App.SteamVR
{
    /// <summary>
    /// キーボードを SteamVR のオーバーレイとして表示する。HMD に対する相対位置で置く頭部固定（位置は <see cref="OverlayPlacement"/>。設定画面で変えるとすぐに反映する）。
    /// テクスチャは Unity の RenderTexture（Direct3D11）をそのまま渡す。渡す処理はレンダースレッドで行う（<see cref="OverlayTextureSubmitter"/>）。
    /// </summary>
    public sealed class KeyboardOverlay : IDisposable
    {
        /// <summary>オーバーレイのキー。接続ごとに一意。同じ接続の中で重複すると作成が失敗する。</summary>
        public const string OverlayKey = "vrime.keyboard";

        public const string FriendlyName = AppInfo.DisplayName;

        readonly OverlayPlacement _placement;
        OverlayTextureSubmitter _submitter;
        ulong _handle;
        bool _visible;

        /// <param name="overlayPlacement">位置からオーバーレイの変換を求めるもの（盤面の配置を持つ）。</param>
        /// <param name="placement">最初の位置。</param>
        public KeyboardOverlay(RenderTexture texture, OverlayPlacement overlayPlacement, KeyboardPlacement placement)
        {
            IntPtr nativeTexture = OverlayTextures.GetNativeTexture(texture, nameof(texture));
            _placement = overlayPlacement ?? throw new ArgumentNullException(nameof(overlayPlacement));
            if (placement == null) throw new ArgumentNullException(nameof(placement));

            CVROverlay overlay = OpenVR.Overlay;
            OpenVRErrors.Check(overlay.CreateOverlay(OverlayKey, FriendlyName, ref _handle), "CreateOverlay(" + OverlayKey + ")");
            try
            {
                OverlayTextures.Configure(_handle);
                ApplyPlacement(placement);
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

        /// <summary>位置・傾き・大きさを変える。表示中でもそのまま反映される。</summary>
        public void SetPlacement(KeyboardPlacement placement)
        {
            ThrowIfDisposed();
            if (placement == null) throw new ArgumentNullException(nameof(placement));
            ApplyPlacement(placement);
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

        void ApplyPlacement(KeyboardPlacement placement)
        {
            CVROverlay overlay = OpenVR.Overlay;
            OpenVRErrors.Check(overlay.SetOverlayWidthInMeters(_handle, placement.WidthMeters), "SetOverlayWidthInMeters");

            float[] m = _placement.ComputeOverlayToHmd(placement);
            var transform = new HmdMatrix34_t
            {
                m0 = m[0], m1 = m[1], m2 = m[2], m3 = m[3],
                m4 = m[4], m5 = m[5], m6 = m[6], m7 = m[7],
                m8 = m[8], m9 = m[9], m10 = m[10], m11 = m[11],
            };
            OpenVRErrors.Check(
                overlay.SetOverlayTransformTrackedDeviceRelative(_handle, OpenVR.k_unTrackedDeviceIndex_Hmd, ref transform),
                "SetOverlayTransformTrackedDeviceRelative");
        }

        void ThrowIfDisposed()
        {
            if (_handle == 0) throw new ObjectDisposedException(nameof(KeyboardOverlay));
        }
    }
}
