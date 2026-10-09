using System;
using UnityEngine;
using VRKeyboard.App.Conversion;
using VRKeyboard.App.Input;
using VRKeyboard.App.Output;
using VRKeyboard.App.View;
using VRKeyboard.Core;
using VRKeyboard.Core.Input;

namespace VRKeyboard.App
{
    /// <summary>
    /// 起動の根。部品を組み立て、毎フレーム「入力を読む → Core を進める → 表示を更新する → 出力を進める」を行う。
    /// Core からの通知（<see cref="IKeyboardEffects"/>）を、振動・変換の通信・送信へ振り分ける。
    /// 入力元と振動の出力先は <see cref="SetDevices"/> で差し替えられる（既定は PC のキーボードによる開発用）。
    /// </summary>
    public sealed class KeyboardApp : MonoBehaviour, IKeyboardEffects
    {
        [Tooltip("Noto Sans JP Regular。Include Font Data を有効にしたフォント。")]
        [SerializeField] Font _regularFont;

        [Tooltip("Noto Sans JP Bold。Include Font Data を有効にしたフォント。")]
        [SerializeField] Font _boldFont;

        const int TargetFrameRate = 90;

        KeyboardSettings _settings;
        KeyboardSession _session;
        IControllerSource _source;
        IHapticOutput _haptics;
        KeyboardFonts _fonts;
        OverlayRenderTarget _overlay;
        KeyboardView _view;
        ConversionRunner _runner;
        VRChatChatbox _chatbox;
        TextOutputRouter _router;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        KeyboardPreview _preview;
#endif

        public KeyboardSession Session => _session;

        /// <summary>キーボードの描画先。OpenVR のオーバーレイのテクスチャとして渡す。</summary>
        public OverlayRenderTarget Overlay => _overlay;

        /// <summary>入力元と振動の出力先を差し替える（OpenVR 実機・検証用）。</summary>
        public void SetDevices(IControllerSource source, IHapticOutput haptics)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _haptics = haptics ?? throw new ArgumentNullException(nameof(haptics));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RecreatePreview();
#endif
        }

        void Awake()
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

            _source = new DebugKeyboardControllerSource();
            _haptics = new DebugHapticOutput();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RecreatePreview();
#endif
        }

        void Update()
        {
            // 時刻は Time.timeScale に影響されないものを使う。
            double now = Time.realtimeSinceStartupAsDouble;

            _session.Tick(_source.Read(), now);
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

        void RecreatePreview()
        {
            if (_session == null) return;
            _preview = new KeyboardPreview(_session, _overlay.Texture, _haptics as DebugHapticOutput);
        }
#endif

        void OnDestroy()
        {
            _runner?.Dispose();
            _chatbox?.Dispose();
            _overlay?.Dispose();
            _fonts?.Dispose();
        }

        void IKeyboardEffects.OnOpenChanged(bool isOpen)
        {
            // 表示の切り替えはフレームの描画に任せる（開いた時点で表示の版が変わる）。OpenVR のオーバーレイの表示切替はここで行う。
        }

        void IKeyboardEffects.OnHaptic(Hand hand, HapticKind kind)
        {
            _haptics.Play(hand, kind);
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
