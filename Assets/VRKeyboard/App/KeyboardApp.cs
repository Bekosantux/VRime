using System;
using UnityEngine;
using VRKeyboard.App.Conversion;
using VRKeyboard.App.Output;
using VRKeyboard.App.Platform;
using VRKeyboard.App.SteamVR;
using VRKeyboard.App.View;
using VRKeyboard.Core;
using VRKeyboard.Core.Input;

namespace VRKeyboard.App
{
    /// <summary>エディタで使う入力元。ビルドでは常に <see cref="OpenVR"/>。</summary>
    public enum EditorDevice
    {
        /// <summary>PC のキーボードで両手を模す。VR 機器なしで開発できる。</summary>
        DebugKeyboard,

        /// <summary>SteamVR に接続する。SteamVR が起動していないと、起動時に例外になる。</summary>
        OpenVR,
    }

    /// <summary>
    /// 起動の根。部品を組み立て、毎フレーム「環境のイベント → 入力を読む → Core を進める → 表示を更新する → 出力を進める」を行う。
    /// Core からの通知（<see cref="IKeyboardEffects"/>）を、振動・変換の通信・送信・環境の切り替えへ振り分ける。
    /// 実行環境（OpenVR / 開発用のキーボード）は <see cref="IKeyboardPlatform"/> の差し替えで切り替える。
    /// </summary>
    public sealed class KeyboardApp : MonoBehaviour, IKeyboardEffects
    {
        [Tooltip("Noto Sans JP Regular。Include Font Data を有効にしたフォント。")]
        [SerializeField] Font _regularFont;

        [Tooltip("Noto Sans JP Bold。Include Font Data を有効にしたフォント。")]
        [SerializeField] Font _boldFont;

        // 値はエディタでしか読まないが、シリアライズの構造をエディタとビルドで揃えるためフィールドは常に残す。
        // そのためビルドでは未使用の警告（CS0414）になるので抑止する。
#pragma warning disable CS0414
        [Tooltip("エディタでの入力元（開発用）。ビルドでは常に OpenVR。")]
        [SerializeField] EditorDevice _editorDevice = EditorDevice.DebugKeyboard;
#pragma warning restore CS0414

        const int TargetFrameRate = 90;

        KeyboardSettings _settings;
        KeyboardSession _session;
        IKeyboardPlatform _platform;
        KeyboardFonts _fonts;
        OverlayRenderTarget _overlay;
        KeyboardView _view;
        ConversionRunner _runner;
        VRChatChatbox _chatbox;
        TextOutputRouter _router;
        bool _quitting;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        KeyboardPreview _preview;
#endif

        public KeyboardSession Session => _session;

        /// <summary>キーボードの描画先。OpenVR のオーバーレイのテクスチャとして渡す。</summary>
        public OverlayRenderTarget Overlay => _overlay;

        void Awake()
        {
            try
            {
                Initialize();
            }
            catch
            {
                // 組み立てに失敗した状態で Update が回り続け、同じ例外が毎フレーム出るのを防ぐ。
                enabled = false;
                throw;
            }
        }

        void Initialize()
        {
            if (_regularFont == null) throw new InvalidOperationException("Regular フォントが設定されていません。");
            if (_boldFont == null) throw new InvalidOperationException("Bold フォントが設定されていません。");

            // 非表示のウィンドウでも入力を取りこぼさないよう、VSync を切って一定のフレームレートで回す（SPEC 3.3）。
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFrameRate;

            _settings = new KeyboardSettings();
            _session = new KeyboardSession(_settings, this);
            _fonts = new KeyboardFonts(_regularFont, _boldFont);
            _overlay = new OverlayRenderTarget(KeyboardTheme.Width, KeyboardTheme.Height);
            _view = new KeyboardView(_session, _settings, _fonts, _overlay.Canvas.transform);
            _runner = new ConversionRunner(_session, new GoogleTransliterateClient());

            var appSettings = new AppSettings();
            _chatbox = new VRChatChatbox(appSettings);
            _router = TextOutputRouter.Create(appSettings, _chatbox, new DesktopTextSender());

            // 環境の作成は、描画先が揃ったあとに行う。OpenVR の初期化に失敗した場合は例外にする（別の環境へは切り替えない）。
            _platform = CreatePlatform();
            _platform.QuitRequested += OnQuitRequested;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _preview = new KeyboardPreview(_session, _overlay.Texture, (_platform as DebugKeyboardPlatform)?.DebugHaptics);
#endif
        }

        IKeyboardPlatform CreatePlatform()
        {
#if UNITY_EDITOR
            switch (_editorDevice)
            {
                case EditorDevice.DebugKeyboard: return new DebugKeyboardPlatform();
                case EditorDevice.OpenVR: return new OpenVRPlatform(_overlay);
                default: throw new InvalidOperationException("未知の入力元: " + _editorDevice);
            }
#else
            return new OpenVRPlatform(_overlay);
#endif
        }

        void Update()
        {
            // 時刻は Time.timeScale に影響されないものを使う。
            double now = Time.realtimeSinceStartupAsDouble;

            _platform.BeginFrame();
            // 終了を求められたあとは、環境の API を呼ばない。
            if (_quitting) return;

            // ダッシュボードの表示中は、手動で閉じるのと同じ処理でキーボードを閉じる（SPEC 4.4）。
            _session.SetDashboardVisible(_platform.IsDashboardVisible);

            _session.Tick(_platform.Controller.Read(), now);
            _runner.CancelUnlessWaiting();

            // 閉じている間は描かない。開いた時点で表示の版が変わるため、そのとき描かれる。
            if (_session.IsOpen && _view.RefreshIfChanged()) _overlay.Render();

            // 送信先は送信の時点まで決まらないため、入力中の表示は出力先を問わず送る（SPEC 7.3）。
            _chatbox.SetTyping(_session.IsOpen && (_session.Committed.Length > 0 || _session.Composition.Length > 0));
            _chatbox.Update(now);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void OnGUI()
        {
            _preview?.Draw();
        }
#endif

        void OnDestroy()
        {
            _runner?.Dispose();
            _chatbox?.Dispose();
            // オーバーレイ（OpenVR 側）が描画先のテクスチャを参照しているため、先に環境を閉じる。
            if (_platform != null) _platform.QuitRequested -= OnQuitRequested;
            _platform?.Dispose();
            _overlay?.Dispose();
            _fonts?.Dispose();
        }

        void OnQuitRequested()
        {
            _quitting = true;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void IKeyboardEffects.OnOpenChanged(bool isOpen)
        {
            // 表示の描き直しはフレームの描画に任せる（開いた時点で表示の版が変わる）。
            // オーバーレイの表示切り替えと入力の優先度は、環境が行う。
            _platform.OnOpenChanged(isOpen);
        }

        void IKeyboardEffects.OnHaptic(Hand hand, HapticKind kind)
        {
            _platform.Haptics.Play(hand, kind);
        }

        void IKeyboardEffects.OnConversionRequested(int requestId, string reading)
        {
            _runner.Start(requestId, reading);
        }

        bool IKeyboardEffects.OnSendRequested(string text)
        {
            OutputResult result = _router.Send(text);
            _session.SetNotice(result.Notice);
            return result.Success;
        }
    }
}
