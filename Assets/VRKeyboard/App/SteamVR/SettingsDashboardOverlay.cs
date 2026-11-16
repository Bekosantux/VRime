using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using Valve.VR;
using VRKeyboard.App.Settings;

namespace VRKeyboard.App.SteamVR
{
    /// <summary>
    /// 設定画面を SteamVR のダッシュボードのタブ（ダッシュボードオーバーレイ）として表示する。
    /// 表示と非表示はダッシュボードが行う。レーザーによる操作はマウスのイベントとして届き、<see cref="ISettingsPointer"/> へ渡す。
    /// テクスチャは <see cref="KeyboardOverlay"/> と同じく、レンダースレッドで渡す。
    /// タブのアイコン（サムネイル）のオーバーレイは本体と一緒に OpenVR が破棄する。単独では破棄できない（<c>ThumbnailCantBeDestroyed</c>）。
    /// </summary>
    public sealed class SettingsDashboardOverlay : IDisposable
    {
        /// <summary>オーバーレイのキー。キーボードのオーバーレイのキーと重ならないこと。</summary>
        public const string OverlayKey = "vrkeyboard.settings";

        /// <summary>ダッシュボードのタブに表示する名前。</summary>
        public const string FriendlyName = AppInfo.DisplayName;

        /// <summary>タブのアイコンの <c>StreamingAssets</c> 内の場所。</summary>
        public const string ThumbnailRelativePath = "SteamVR/dashboard_icon.png";

        /// <summary>ダッシュボード上の幅。</summary>
        const float WidthMeters = 2.5f;

        static readonly uint EventSize = (uint)Marshal.SizeOf<VREvent_t>();

        readonly ISettingsPointer _pointer;
        OverlayTextureSubmitter _submitter;
        ulong _handle;

        public SettingsDashboardOverlay(RenderTexture texture, ISettingsPointer pointer)
        {
            IntPtr nativeTexture = OverlayTextures.GetNativeTexture(texture, nameof(texture));
            _pointer = pointer ?? throw new ArgumentNullException(nameof(pointer));

            string thumbnail = Path.Combine(Application.streamingAssetsPath, ThumbnailRelativePath);
            if (!File.Exists(thumbnail)) throw new FileNotFoundException("ダッシュボードのアイコンが見つかりません。", thumbnail);

            CVROverlay overlay = OpenVR.Overlay;
            ulong thumbnailHandle = 0;
            OpenVRErrors.Check(overlay.CreateDashboardOverlay(OverlayKey, FriendlyName, ref _handle, ref thumbnailHandle),
                "CreateDashboardOverlay(" + OverlayKey + ")");
            try
            {
                OpenVRErrors.Check(overlay.SetOverlayFromFile(thumbnailHandle, thumbnail), "SetOverlayFromFile");
                OpenVRErrors.Check(overlay.SetOverlayWidthInMeters(_handle, WidthMeters), "SetOverlayWidthInMeters");
                OverlayTextures.Configure(_handle);

                // マウスの座標はテクスチャのピクセルで受け取る。
                OpenVRErrors.Check(overlay.SetOverlayInputMethod(_handle, VROverlayInputMethod.Mouse), "SetOverlayInputMethod");
                var mouseScale = new HmdVector2_t { v0 = texture.width, v1 = texture.height };
                OpenVRErrors.Check(overlay.SetOverlayMouseScale(_handle, ref mouseScale), "SetOverlayMouseScale");

                _submitter = new OverlayTextureSubmitter(_handle, nativeTexture);
            }
            catch
            {
                // 元の例外を優先するため、破棄の失敗は無視する。
                overlay.DestroyOverlay(_handle);
                _handle = 0;
                throw;
            }
        }

        /// <summary>設定画面のタブが表示されているか。<see cref="Update"/> で更新される。</summary>
        public bool IsVisible { get; private set; }

        /// <summary>テクスチャの内容が新しく描かれた。テクスチャを渡す処理をレンダースレッドに依頼する。</summary>
        public void NotifyRendered()
        {
            ThrowIfDisposed();
            _submitter.Enqueue();
        }

        /// <summary>
        /// 毎フレーム呼ぶ。テクスチャを渡す処理の失敗を確認し、レーザーの操作を <see cref="ISettingsPointer"/> へ渡し、表示の状態を読む。
        /// </summary>
        public void Update()
        {
            ThrowIfDisposed();
            _submitter.ThrowIfFailed();

            CVROverlay overlay = OpenVR.Overlay;
            var ev = new VREvent_t();
            while (overlay.PollNextOverlayEvent(_handle, ref ev, EventSize))
            {
                switch ((EVREventType)ev.eventType)
                {
                    case EVREventType.VREvent_MouseMove:
                        _pointer.PointerMove(OverlayTextures.ToTexturePosition(ev.data.mouse));
                        break;
                    case EVREventType.VREvent_MouseButtonDown:
                        if (ev.data.mouse.button == (uint)EVRMouseButton.Left) _pointer.PointerDown(OverlayTextures.ToTexturePosition(ev.data.mouse));
                        break;
                    case EVREventType.VREvent_MouseButtonUp:
                        if (ev.data.mouse.button == (uint)EVRMouseButton.Left) _pointer.PointerUp(OverlayTextures.ToTexturePosition(ev.data.mouse));
                        break;
                    case EVREventType.VREvent_FocusLeave:
                    case EVREventType.VREvent_OverlayHidden:
                    case EVREventType.VREvent_DashboardDeactivated:
                        _pointer.PointerLeave();
                        break;
                }
            }

            IsVisible = overlay.IsOverlayVisible(_handle);
        }

        /// <summary>オーバーレイを破棄する。<see cref="OpenVRRuntime"/> を閉じる前に呼ぶこと。</summary>
        public void Dispose()
        {
            if (_handle == 0) return;
            ulong handle = _handle;
            _handle = 0;
            IsVisible = false;
            // 先にレンダースレッドからの参照を断つ（KeyboardOverlay.Dispose と同じ理由）。
            _submitter?.Dispose();
            _submitter = null;
            OpenVRErrors.Check(OpenVR.Overlay.DestroyOverlay(handle), "DestroyOverlay");
        }

        void ThrowIfDisposed()
        {
            if (_handle == 0) throw new ObjectDisposedException(nameof(SettingsDashboardOverlay));
        }
    }
}
