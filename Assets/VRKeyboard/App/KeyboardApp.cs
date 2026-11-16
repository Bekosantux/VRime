using System;
using System.IO;
using System.Net.Sockets;
using TMPro;
using UnityEngine;
using VRKeyboard.App.Conversion;
using VRKeyboard.App.Native;
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
    /// 前面のウィンドウ（<see cref="ForegroundWindowMonitor"/>）を毎フレーム調べ、送信先の表示・文字数の上限・チャットボックスへの入力中の表示を合わせる。
    /// キーボードと設定画面の uGUI はシーンに置き、起動時に検証してから組み立てる（<see cref="ValidateScene"/>）。
    /// ビルドでは、組み立てが終わった後にタスクトレイにアイコンを置き、Unity のウィンドウを隠す。
    /// 隠したウィンドウは、トレイの「設定を開く」でデスクトップの設定ウィンドウとして表示する（<see cref="DesktopSettingsWindow"/>）。
    /// 終了は SteamVR の終了・設定画面の「アプリを終了」・トレイの「終了」で行う。
    /// </summary>
    public sealed class KeyboardApp : MonoBehaviour, IKeyboardEffects
    {
        [Tooltip("Noto Sans JP Regular の編集用のフォントアセット（Dynamic）。シーンの文字はこれか Bold を使い、起動時に実行用のアセットへ差し替える。")]
        [SerializeField] TMP_FontAsset _regularFont;

        [Tooltip("Noto Sans JP Bold の編集用のフォントアセット（Dynamic）。")]
        [SerializeField] TMP_FontAsset _boldFont;

        [Tooltip("状態によって切り替わる色と文字の大きさ。")]
        [SerializeField] KeyboardTheme _theme;

        [Tooltip("キーボードの描画先。")]
        [SerializeField] OverlayRenderTarget _keyboardTarget;

        [Tooltip("キーボードの表示。_keyboardTarget のキャンバスと同じオブジェクトに置く。")]
        [SerializeField] KeyboardView _keyboardView;

        [Tooltip("設定画面の描画先。")]
        [SerializeField] OverlayRenderTarget _settingsTarget;

        [Tooltip("設定画面の表示。_settingsTarget のキャンバスと同じオブジェクトに置く。")]
        [SerializeField] SettingsView _settingsView;

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

        /// <summary>タスクトレイのアイコンの <c>StreamingAssets</c> 内の場所。</summary>
        const string TrayIconRelativePath = "Windows/tray_icon.ico";

        KeyboardSession _session;
        IKeyboardPlatform _platform;
        KeyboardFonts _fonts;
        bool _keyboardShown;
        SettingsStore _settingsStore;
        SettingsForm _settingsForm;
        string _loadNotice;
        ConversionRunner _runner;
        ForegroundWindowMonitor _monitor;
        VRChatChatbox _chatbox;
        DesktopTextSender _desktop;
        TextOutputRouter _router;
        bool _quitting;

        // ビルドでだけ作る。エディターでは null のため、未代入の警告（CS0649）になるので抑止する。
#pragma warning disable CS0649
        TrayIcon _tray;
        DesktopSettingsWindow _desktopSettings;
#pragma warning restore CS0649

#if UNITY_EDITOR
        KeyboardPreview _preview;
#endif

        public KeyboardSession Session => _session;

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

        /// <summary>
        /// シーンの構成を検証する。足りない参照・誤った設定は、どこが誤っているかを示す例外にする。シーンの状態は変えない。
        /// </summary>
        public void ValidateScene()
        {
            SceneChecks.Require(_theme, this, nameof(_theme));
            SceneChecks.Require(_keyboardTarget, this, nameof(_keyboardTarget));
            SceneChecks.Require(_keyboardView, this, nameof(_keyboardView));
            SceneChecks.Require(_settingsTarget, this, nameof(_settingsTarget));
            SceneChecks.Require(_settingsView, this, nameof(_settingsView));
            KeyboardFonts.ValidateEditFont(_regularFont, SceneChecks.PathOf(transform) + " の " + nameof(_regularFont));
            KeyboardFonts.ValidateEditFont(_boldFont, SceneChecks.PathOf(transform) + " の " + nameof(_boldFont));
            _theme.Validate();

            _keyboardTarget.Validate(KeyboardLayer);
            _settingsTarget.Validate(SettingsLayer);
            RequireOnCanvas(_keyboardView, _keyboardTarget, nameof(_keyboardView));
            RequireOnCanvas(_settingsView, _settingsTarget, nameof(_settingsView));

            _keyboardView.Validate();
            _settingsView.CreateLayout(SettingsCatalog.Categories);
            KeyboardFonts.ValidateTexts(_keyboardTarget.transform, _regularFont, _boldFont);
            KeyboardFonts.ValidateTexts(_settingsTarget.transform, _regularFont, _boldFont);
        }

        void RequireOnCanvas(Component view, OverlayRenderTarget target, string field)
        {
            if (view.transform != target.Canvas.transform)
                throw new InvalidOperationException(SceneChecks.PathOf(transform) + " の " + field + " は " +
                    SceneChecks.PathOf(target.Canvas.transform) + " に置く必要があります（" + SceneChecks.PathOf(view.transform) + " に置かれています）。");
        }

        void Initialize()
        {
            ValidateScene();

            // 非表示のウィンドウでも入力を取りこぼさないよう、VSync を切って一定のフレームレートで回す（SPEC 3.3）。
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFrameRate;
            // OnGUI では GUILayout を使わない。使わなければ、レイアウトのためのイベントを省ける。
            useGUILayout = false;

            _settingsStore = new SettingsStore(SettingsStore.DefaultPath);
            SettingsLoadResult loaded = _settingsStore.Load();
            if (loaded.Error != null)
                Debug.LogWarning("設定ファイルの内容が正しくないため、既定の設定で起動します: " + loaded.Error.Message);
            _loadNotice = loaded.Notice;

            KeyboardSettings settings = loaded.Settings.ToKeyboardSettings();
            _session = new KeyboardSession(settings, this);

            // 文字を描く前にフォントを差し替える。編集用のアセットのアトラスに、実行中の文字が追加されないようにするため。
            _fonts = new KeyboardFonts(_regularFont, _boldFont);
            _fonts.Apply(_keyboardTarget.transform);
            _fonts.Apply(_settingsTarget.transform);

            _keyboardTarget.Initialize(KeyboardLayer);
            _settingsTarget.Initialize(SettingsLayer);
            _monitor = ForegroundWindowMonitor.CreateForSystem();
            _keyboardView.Initialize(_session, _monitor, settings.ConsonantHand, _theme);

            _settingsForm = new SettingsForm(_settingsView.CreateLayout(SettingsCatalog.Categories), loaded.Settings);
            _settingsForm.SetNotice(_loadNotice);
            _settingsForm.Changed += OnSettingsChanged;
            _settingsForm.Committed += OnSettingsCommitted;
            _settingsView.Initialize(_settingsForm, _theme);
            _runner = new ConversionRunner(_session, new GoogleTransliterateClient());

            var appSettings = new AppSettings();
            _chatbox = new VRChatChatbox(appSettings);
            // 時刻は Update と同じく Time.timeScale に影響されないものを使う。
            _desktop = new DesktopTextSender(() => Time.realtimeSinceStartupAsDouble);
            _router = TextOutputRouter.Create(appSettings, _chatbox, _desktop, _monitor);

            // 環境の作成は、描画先が揃ったあとに行う。OpenVR の初期化に失敗した場合は例外にする（別の環境へは切り替えない）。
            _platform = CreatePlatform();
            _platform.QuitRequested += OnQuitRequested;
#if UNITY_EDITOR
            var debugPlatform = _platform as DebugKeyboardPlatform;
            _preview = new KeyboardPreview(_session, _platform, _keyboardTarget.Texture, _settingsTarget.Texture,
                debugPlatform?.DebugHaptics, debugPlatform != null ? _settingsForm : null);
#endif

            _settingsForm.QuitRequested += OnQuitRequested;

#if !UNITY_EDITOR
            // ウィンドウを隠した後も設定を開けて終了できるよう、隠す（Start）前にトレイのアイコンを置く（SPEC 9）。
            _desktopSettings = new DesktopSettingsWindow(_settingsTarget.Texture, _settingsForm, AppInfo.DisplayName + " の設定");
            _tray = new TrayIcon(Path.Combine(Application.streamingAssetsPath, TrayIconRelativePath), AppInfo.DisplayName);
            Application.wantsToQuit += OnWantsToQuit;
#endif
        }

        // Unity は最初のシーンの Awake の後、Start の前にプレイヤーのウィンドウを表示する。Awake で隠しても表示し直されるため、
        // VRChat を前面にする処理とウィンドウを隠す処理は Start で行う（SPEC 3.1）。
        // Awake が例外で終わったときは enabled が false になり Start は呼ばれないため、ウィンドウが残り、異常に気付ける。
        void Start()
        {
            // 開発用のキーボードの入力は前面のウィンドウにしか届かないため、その環境では前面を移さない。
            if (_settingsForm.Settings.VRChat.FocusOnStartup && !_platform.RequiresOwnWindowFocus) FocusVRChat();

#if !UNITY_EDITOR
            // VRChat を前面にするより後に行う。前面のウィンドウを持つ間に切り替えれば、Windows に拒まれないため。
            PlayerWindow.Hide();
#endif
        }

        static void FocusVRChat()
        {
            VRChatFocusResult result = VRChatWindow.Focus();
            switch (result)
            {
                case VRChatFocusResult.Focused:
                case VRChatFocusResult.NotFound:
                    break;
                case VRChatFocusResult.Refused:
                    Debug.LogWarning("VRChat を前面にできませんでした: " + result);
                    break;
                default:
                    throw new InvalidOperationException("未知の結果: " + result);
            }
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
            var overlayPlacement = new OverlayPlacement(_keyboardView.BoardCenterOffset);
            return new OpenVRPlatform(_keyboardTarget, _settingsTarget, _settingsForm, overlayPlacement, _settingsForm.Settings.Placement);
        }

        void Update()
        {
            // 時刻は Time.timeScale に影響されないものを使う。
            double now = Time.realtimeSinceStartupAsDouble;

            _platform.BeginFrame();
            // 終了を求められたあとは、環境の API を呼ばない。
            if (_quitting) return;
            TakeTrayCommands();
            if (_quitting) return;

            // 送信先は、このフレームの入力を処理する前に決める。送信と文字数の上限が同じ送信先に従うようにするため。
            _monitor.Update();
            _session.SetLengthLimit(_monitor.Target.Kind == OutputTargetKind.VRChat ? VRChatChatbox.MaxLength : (int?)null);

            // ダッシュボードが入力を持っている間は、手動で閉じるのと同じ処理でキーボードを閉じる（SPEC 4.4）。
            _session.SetDashboardVisible(_platform.IsDashboardHoldingInput);

            _session.Tick(_platform.Controller.Read(), now);
            _runner.CancelUnlessWaiting();

            // 位置の確認用のキーボードは、設定画面（ダッシュボードのタブかデスクトップの設定ウィンドウ）で表示位置のカテゴリを選んでいる間だけ表示する。
            bool settingsVisible = _platform.IsSettingsVisible || (_desktopSettings != null && _desktopSettings.IsVisible);
            bool placementPreview = settingsVisible && _settingsForm.Category.ShowsKeyboardPreview;
            _platform.SetPlacementPreviewVisible(placementPreview);

            // キーボードは、開いている間と位置の確認用に表示している間だけ描く。
            // 隠れている間に表示の版が変わっていることがあるため、表示し始めたときは必ず描く。
            bool keyboardShown = _session.IsOpen || placementPreview;
            if (keyboardShown && (_keyboardView.RefreshIfChanged() || !_keyboardShown)) _keyboardTarget.Render();
            _keyboardShown = keyboardShown;
            if (_settingsView.RefreshIfChanged()) _settingsTarget.Render();

            UpdateChatbox(now);
            UpdateDesktop();
        }

        void TakeTrayCommands()
        {
            if (_tray == null) return;
            while (_tray.TryTakeCommand(out TrayCommand command))
            {
                switch (command)
                {
                    case TrayCommand.OpenSettings:
                        _desktopSettings.Show();
                        break;
                    case TrayCommand.Quit:
                        OnQuitRequested();
                        return;
                    default:
                        throw new InvalidOperationException("未知のトレイの操作: " + command);
                }
            }
        }

        void UpdateChatbox(double now)
        {
            // 入力中であることの表示は、送信先が VRChat の間だけ送る（SPEC 7.3）。送信先が変われば次のフレームで取り下げる。
            bool typing = _session.IsOpen && _session.Length > 0 && _monitor.Target.Kind == OutputTargetKind.VRChat;
            _chatbox.SetTyping(typing);
            bool showText = typing && _settingsForm.Settings.VRChat.ShowTypingText;
            _chatbox.SetPreview(showText ? _session.Text : null);
            _chatbox.Update(now);
        }

        void UpdateDesktop()
        {
            if (_desktop.Update() == ClipboardRestoreAction.GaveUp)
                Debug.LogWarning("クリップボードを開けなかったため、送信前の内容を戻せませんでした。");
        }

        void OnGUI()
        {
#if UNITY_EDITOR
            _preview?.Draw();
#else
            _desktopSettings?.Draw();
#endif
        }

        void OnDestroy()
        {
            _runner?.Dispose();
            ClearChatboxPreview();
            _chatbox?.Dispose();
            RestoreClipboard();
            _desktop?.Dispose();
            // オーバーレイ（OpenVR 側）が描画先のテクスチャを参照しているため、先に環境を閉じる。
            if (_platform != null) _platform.QuitRequested -= OnQuitRequested;
            _platform?.Dispose();
            // シーンの終了時は、描画先のオブジェクトが先に破棄されていることがある。テクスチャはオブジェクトとは別に破棄する。
            if (_keyboardTarget is not null) _keyboardTarget.Release();
            if (_settingsTarget is not null) _settingsTarget.Release();
            _fonts?.Dispose();
            if (_tray != null) Application.wantsToQuit -= OnWantsToQuit;
            // 失敗すると例外になるため、他の後始末を終えた後に行う。
            _tray?.Dispose();
        }

        // 終了の前に、チャットボックスに入力途中の文字を残さない。送れなくても終了は続ける。
        void ClearChatboxPreview()
        {
            if (_chatbox == null) return;
            try
            {
                _chatbox.ClearPreviewNow();
            }
            catch (SocketException e)
            {
                Debug.LogException(e);
            }
        }

        // 送信の直後に終了しても、クリップボードを送信前の内容に戻す。戻せなくても終了は続ける。
        void RestoreClipboard()
        {
            if (_desktop == null) return;
            try
            {
                if (!_desktop.RestoreNow())
                    Debug.LogWarning("クリップボードを開けなかったため、送信前の内容を戻せませんでした。");
            }
            catch (System.ComponentModel.Win32Exception e)
            {
                Debug.LogException(e);
            }
        }

        // 設定ウィンドウを表示している間は、ウィンドウを閉じる操作（×ボタン・Alt+F4）で終了せず、設定ウィンドウを隠す。
        // アプリ自身が求めた終了と、設定ウィンドウを隠している間の終了の要求は妨げない。
        bool OnWantsToQuit()
        {
            if (_quitting || !_desktopSettings.IsVisible) return true;
            _desktopSettings.Hide();
            return false;
        }

        // 環境（SteamVR の終了）・設定画面の「アプリを終了」・トレイの「終了」から呼ばれる。
        void OnQuitRequested()
        {
            if (_quitting) return;
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
            // 成功は通知しない（入力バッファが空になることで分かる）。
            if (!result.Success) _session.SetNotice(result.Notice);
            return result.Success;
        }
    }
}
