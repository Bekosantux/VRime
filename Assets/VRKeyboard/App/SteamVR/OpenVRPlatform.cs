using System;
using System.IO;
using UnityEngine;
using Valve.VR;
using VRKeyboard.App.Input;
using VRKeyboard.App.Platform;
using VRKeyboard.App.View;

namespace VRKeyboard.App.SteamVR
{
    /// <summary>
    /// OpenVR（SteamVR）の環境。接続・入力・振動・オーバーレイをまとめ、
    /// キーボードの開閉に合わせて「オーバーレイの表示切り替え」と「入力の優先度の切り替え」を同時に行う。
    /// 作成に失敗したときは、それまでに作ったものを破棄して例外にする。
    /// </summary>
    public sealed class OpenVRPlatform : IKeyboardPlatform
    {
        /// <summary>アクションマニフェストの <c>StreamingAssets</c> 内の場所。</summary>
        public const string ActionManifestRelativePath = "SteamVR/actions.json";

        readonly OverlayRenderTarget _renderTarget;
        readonly OpenVRRuntime _runtime;
        readonly OpenVRControllerSource _controller;
        readonly OpenVRHapticOutput _haptics;
        readonly KeyboardOverlay _overlay;
        bool _disposed;
        bool _quitRequested;

        /// <param name="renderTarget">キーボードの描画先。描かれるたびに、そのテクスチャをオーバーレイへ渡す。</param>
        public OpenVRPlatform(OverlayRenderTarget renderTarget)
        {
            _renderTarget = renderTarget ?? throw new ArgumentNullException(nameof(renderTarget));

            string manifest = Path.Combine(Application.streamingAssetsPath, ActionManifestRelativePath);
            _runtime = OpenVRRuntime.Initialize(manifest);
            try
            {
                var actions = new OpenVRActions();
                _controller = new OpenVRControllerSource(actions);
                _haptics = new OpenVRHapticOutput(actions);
                _overlay = new KeyboardOverlay(renderTarget.Texture);
                _runtime.QuitRequested += OnRuntimeQuitRequested;
                _renderTarget.Rendered += OnRendered;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public IControllerSource Controller => _controller;
        public IHapticOutput Haptics => _haptics;
        public bool IsDashboardVisible { get; private set; }

        public event Action QuitRequested;

        public void BeginFrame()
        {
            ThrowIfDisposed();
            _runtime.PollEvents();
            if (_quitRequested) return;
            IsDashboardVisible = OpenVR.Overlay.IsDashboardVisible();
            _overlay.Update();
        }

        /// <summary>
        /// 開いたらオーバーレイを表示して入力を占有し、閉じたら隠して入力の占有をやめる。
        /// ダッシュボードによる一時停止・再開もここを通る（手動の開閉と同じ経路にするため。SPEC 4.4）。
        /// </summary>
        public void OnOpenChanged(bool isOpen)
        {
            ThrowIfDisposed();
            _controller.IsExclusive = isOpen;
            if (isOpen) _overlay.Show();
            else _overlay.Hide();
        }

        /// <summary>
        /// オーバーレイを破棄してから接続を閉じる。この順序でないと、オーバーレイの破棄に失敗する。
        /// 途中で失敗しても、接続は必ず閉じる。
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_renderTarget != null) _renderTarget.Rendered -= OnRendered;
            try
            {
                _overlay?.Dispose();
            }
            finally
            {
                _runtime?.Dispose();
            }
        }

        void OnRuntimeQuitRequested()
        {
            _quitRequested = true;
            QuitRequested?.Invoke();
        }

        void OnRendered()
        {
            _overlay.NotifyRendered();
        }

        void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(OpenVRPlatform));
        }
    }
}
