using System;
using System.IO;
using UnityEngine;
using VRKeyboard.App.Conversion;
using VRKeyboard.App.Output;
using VRKeyboard.App.Platform;
using VRKeyboard.App.Settings;
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
    /// 設定画面で変えた値は、キーボードの動作と表示位置へ随時反映し、確定したときに保存する。
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

        // 描画先ごとのレイヤー。Project Settings の Tags and Layers に登録されている必要がある。
        const string KeyboardLayer = "VRKeyboardOverlay";
        const string SettingsLayer = "VRKeyboardSettings";

        KeyboardSession _session;
        IKeyboardPlatform _platform;
        KeyboardFonts _fonts;
        OverlayRenderTarget _overlay;
        KeyboardView _view;
        bool _keyboardShown;
        SettingsStore _settingsStore;
        SettingsForm _settingsForm;
        OverlayRenderTarget _settingsOverlay;
        SettingsView _settingsView;
        string _loadNotice;
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

            _settingsStore = new SettingsStore(SettingsStore.DefaultPath);
            SettingsLoadResult loaded = _settingsStore.Load();
            if (loaded.Error != null)
                Debug.LogWarning("設定ファイルの内容が正しくないため、既定の設定で起動します: " + loaded.Error.Message);
            _loadNotice = loaded.Notice;

            KeyboardSettings settings = loaded.Settings.ToKeyboardSettings();
            _session = new KeyboardSession(settings, this);
            _fonts = new KeyboardFonts(_regularFont, _boldFont);
            _overlay = new OverlayRenderTarget("VRKeyboard Overlay", KeyboardTheme.Width, KeyboardTheme.Height, KeyboardLayer);
            _view = new KeyboardView(_session, settings, _fonts, _overlay.Canvas.transform);

            _settingsForm = new SettingsForm(new SettingsLayout(SettingsCatalog.Groups), loaded.Settings);
            _settingsForm.SetNotice(_loadNotice);
            _settingsForm.Changed += OnSettingsChanged;
            _settingsForm.Committed += OnSettingsCommitted;
            _settingsOverlay = new OverlayRenderTarget("VRKeyboard Settings", SettingsLayout.Width, SettingsLayout.Height, SettingsLayer);
            _settingsView = new SettingsView(_settingsForm, _fonts, _settingsOverlay.Canvas.transform);
            _runner = new ConversionRunner(_session, new GoogleTransliterateClient());

            var appSettings = new AppSettings();
            _chatbox = new VRChatChatbox(appSettings);
            _router = TextOutputRouter.Create(appSettings, _chatbox, new DesktopTextSender());

            // 環境の作成は、描画先が揃ったあとに行う。OpenVR の初期化に失敗した場合は例外にする（別の環境へは切り替えない）。
            _platform = CreatePlatform();
            _platform.QuitRequested += OnQuitRequested;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var debugPlatform = _platform as DebugKeyboardPlatform;
            _preview = new KeyboardPreview(_session, _platform, _overlay.Texture, _settingsOverlay.Texture,
                debugPlatform?.DebugHaptics, debugPlatform != null ? _settingsForm : null);
#endif
        }

        IKeyboardPlatform CreatePlatform()
        {
#if UNITY_EDITOR
            switch (_editorDevice)
            {
                case EditorDevice.DebugKeyboard: return new DebugKeyboardPlatform();
                case EditorDevice.OpenVR: return CreateOpenVRPlatform();
                default: throw new InvalidOperationException("未知の入力元: " + _editorDevice);
            }
#else
            return CreateOpenVRPlatform();
#endif
        }

        OpenVRPlatform CreateOpenVRPlatform()
        {
            return new OpenVRPlatform(_overlay, _settingsOverlay, _settingsForm, _settingsForm.Settings.Placement);
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

            // キーボードは、開いている間と設定画面の表示中（位置の確認用）だけ描く。
            // 隠れている間に表示の版が変わっていることがあるため、表示し始めたときは必ず描く。
            bool keyboardShown = _session.IsOpen || _platform.IsSettingsVisible;
            if (keyboardShown && (_view.RefreshIfChanged() || !_keyboardShown)) _overlay.Render();
            _keyboardShown = keyboardShown;
            if (_settingsView.RefreshIfChanged()) _settingsOverlay.Render();

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
            _settingsOverlay?.Dispose();
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

        // 設定画面の操作中（確定前）も随時反映する。値を変えながら、キーボードの位置と反応を確かめられるようにするため。
        void OnSettingsChanged(UserSettings settings)
        {
            _session.ApplySettings(settings.ToKeyboardSettings());
            _platform.SetPlacement(settings.Placement);
        }

        // 保存の失敗ではアプリを止めない。設定画面に通知し、次に保存できたときに通知を戻す。
        void OnSettingsCommitted(UserSettings settings)
        {
            try
            {
                _settingsStore.Save(settings);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogException(e);
                _settingsForm.SetNotice("設定を保存できませんでした: " + e.Message);
                return;
            }
            _settingsForm.SetNotice(_loadNotice);
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
