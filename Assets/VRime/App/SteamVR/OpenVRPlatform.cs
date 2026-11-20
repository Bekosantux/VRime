using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Valve.VR;
using VRime.App.Input;
using VRime.App.Platform;
using VRime.App.Settings;
using VRime.App.View;

namespace VRime.App.SteamVR
{
    /// <summary>
    /// OpenVR（SteamVR）の環境。接続・入力・振動・オーバーレイをまとめ、
    /// キーボードの開閉に合わせて「オーバーレイの表示切り替え」と「入力の優先度の切り替え」を同時に行う。
    /// 設定画面はダッシュボードのタブとして置き、そのタブの表示中はキーボードのオーバーレイも表示する（入力は占有しない）。
    /// 指定されたときは、本アプリを SteamVR に登録し（<see cref="OpenVRApplicationManifest"/>）、自動で起動するかを読み書きできるようにする。
    /// 作成に失敗したときは、それまでに作ったものを破棄して例外にする。
    /// </summary>
    public sealed class OpenVRPlatform : IKeyboardPlatform
    {
        /// <summary>アクションマニフェストの <c>StreamingAssets</c> 内の場所。</summary>
        public const string ActionManifestRelativePath = "SteamVR/actions.json";

        readonly OverlayRenderTarget _renderTarget;
        readonly OverlayRenderTarget _settingsRenderTarget;
        readonly OpenVRRuntime _runtime;
        readonly OpenVRControllerSource _controller;
        readonly OpenVRHapticOutput _haptics;
        readonly KeyboardOverlay _overlay;
        readonly SettingsDashboardOverlay _settingsOverlay;
        bool _isOpen;
        bool _placementPreview;
        bool _disposed;
        bool _quitRequested;

        /// <param name="renderTarget">キーボードの描画先。描画されるたびに、そのテクスチャをオーバーレイへ渡す。</param>
        /// <param name="settingsRenderTarget">設定画面の描画先。描画されるたびに、そのテクスチャをダッシュボードのタブへ渡す。</param>
        /// <param name="settingsPointer">設定画面へのレーザーの操作の渡し先。</param>
        /// <param name="overlayPlacement">キーボードの位置からオーバーレイの変換を求めるもの。</param>
        /// <param name="placement">キーボードのオーバーレイの最初の位置。</param>
        /// <param name="registerApplication">
        /// 本アプリを SteamVR に登録するか。登録には、このプロセスの exe のパスを書く。エディターではエディター自体が登録されるため false にする。
        /// </param>
        public OpenVRPlatform(OverlayRenderTarget renderTarget, OverlayRenderTarget settingsRenderTarget,
            ISettingsPointer settingsPointer, OverlayPlacement overlayPlacement, KeyboardPlacement placement, bool registerApplication)
        {
            _renderTarget = renderTarget != null ? renderTarget : throw new ArgumentNullException(nameof(renderTarget));
            _settingsRenderTarget = settingsRenderTarget != null ? settingsRenderTarget : throw new ArgumentNullException(nameof(settingsRenderTarget));
            if (settingsPointer == null) throw new ArgumentNullException(nameof(settingsPointer));
            if (overlayPlacement == null) throw new ArgumentNullException(nameof(overlayPlacement));
            if (placement == null) throw new ArgumentNullException(nameof(placement));

            string actionManifest = Path.Combine(Application.streamingAssetsPath, ActionManifestRelativePath);
            string applicationManifest = registerApplication ? WriteApplicationManifest(actionManifest) : null;
            _runtime = OpenVRRuntime.Initialize(actionManifest, applicationManifest);
            try
            {
                var actions = new OpenVRActions();
                _controller = new OpenVRControllerSource(actions);
                _haptics = new OpenVRHapticOutput(actions);
                _overlay = new KeyboardOverlay(renderTarget.Texture, overlayPlacement, placement);
                _settingsOverlay = new SettingsDashboardOverlay(settingsRenderTarget.Texture, settingsPointer);
                _runtime.QuitRequested += OnRuntimeQuitRequested;
                _renderTarget.Rendered += OnRendered;
                _settingsRenderTarget.Rendered += OnSettingsRendered;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public IControllerSource Controller => _controller;
        public IHapticOutput Haptics => _haptics;
        public bool IsDashboardHoldingInput { get; private set; }
        public bool IsSettingsVisible { get; private set; }

        /// <summary>コントローラーの入力は SteamVR から読むため、前面のウィンドウによらない。</summary>
        public bool RequiresOwnWindowFocus => false;

        public bool? AutoLaunch
        {
            get
            {
                ThrowIfDisposed();
                return _runtime.IsApplicationRegistered ? _runtime.GetAutoLaunch() : (bool?)null;
            }
        }

        public void SetAutoLaunch(bool enabled)
        {
            ThrowIfDisposed();
            if (!_runtime.IsApplicationRegistered)
                throw new NotSupportedException("SteamVR に登録していないため、自動で起動するかを変えられません。");
            _runtime.SetAutoLaunch(enabled);
        }

        public event Action QuitRequested;

        public void BeginFrame()
        {
            ThrowIfDisposed();
            _runtime.PollEvents();
            if (_quitRequested) return;
            // ダッシュボードを閉じても、入力がアプリへ戻るのは数フレーム後になる。その間に入力の占有を始めると、
            // 占有が有効にならず、開き直すまでゲームにも入力が渡り続ける。入力が戻るまでは一時停止を続ける。
            IsDashboardHoldingInput = OpenVR.Overlay.IsDashboardVisible() || !OpenVR.System.IsInputAvailable();
            _overlay.Update();
            _settingsOverlay.Update();
            IsSettingsVisible = _settingsOverlay.IsVisible;
        }

        /// <summary>
        /// 開いたらオーバーレイを表示して入力を占有し、閉じたら隠して入力の占有をやめる（位置の確認用に表示している間は隠さない）。
        /// ダッシュボードによる一時停止・再開もここを通る（手動の開閉と同じ経路にするため。ARCHITECTURE.md「入力の横取り」）。
        /// </summary>
        public void OnOpenChanged(bool isOpen)
        {
            ThrowIfDisposed();
            _isOpen = isOpen;
            _controller.IsExclusive = isOpen;
            UpdateOverlayVisibility();
        }

        public void SetPlacementPreviewVisible(bool visible)
        {
            ThrowIfDisposed();
            _placementPreview = visible;
            UpdateOverlayVisibility();
        }

        public void SetPlacement(KeyboardPlacement placement)
        {
            ThrowIfDisposed();
            _overlay.SetPlacement(placement);
        }

        /// <summary>
        /// オーバーレイを破棄してから接続を閉じる。この順序でないと、オーバーレイの破棄に失敗する。
        /// 途中で失敗しても、接続は必ず閉じる。
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            // 描画先はコンストラクタの最初に設定済み。シーンの終了で破棄されていても、イベントの解除はできる。
            _renderTarget.Rendered -= OnRendered;
            _settingsRenderTarget.Rendered -= OnSettingsRendered;
            try
            {
                try
                {
                    _settingsOverlay?.Dispose();
                }
                finally
                {
                    _overlay?.Dispose();
                }
            }
            finally
            {
                _runtime?.Dispose();
            }
        }

        // マニフェストを書き出し、そのパスを返す。
        static string WriteApplicationManifest(string actionManifest)
        {
            string binary = Process.GetCurrentProcess().MainModule.FileName;
            string image = Path.Combine(Application.streamingAssetsPath, SettingsDashboardOverlay.ThumbnailRelativePath);
            string path = OpenVRApplicationManifest.DefaultPath;
            // streamingAssetsPath の区切りは「/」のため、Windows の区切りに揃える。
            OpenVRApplicationManifest.Write(path, OpenVRApplicationManifest.Create(binary, Path.GetFullPath(actionManifest), Path.GetFullPath(image)));
            return path;
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

        void OnSettingsRendered()
        {
            _settingsOverlay.NotifyRendered();
        }

        void UpdateOverlayVisibility()
        {
            if (_isOpen || _placementPreview) _overlay.Show();
            else _overlay.Hide();
        }

        void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(OpenVRPlatform));
        }
    }
}
