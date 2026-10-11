# アーキテクチャ

外部仕様は [docs/spec/SPEC.md](docs/spec/SPEC.md) を参照。本書は設計（クラス責務・ディレクトリ構成・依存の方向）を扱う。

## アセンブリと依存の方向

```
VRKeyboard.App  ──→  VRKeyboard.Core
      │
      └──────────→  Valve.OpenVR
```

| アセンブリ | 場所 | 責務 |
|---|---|---|
| `VRKeyboard.Core` | `Assets/VRKeyboard/Core/` | Unity・OpenVR・Win32 に依存しない純粋なロジック（`noEngineReferences`）。入力方式の状態機械、入力バッファ、変換候補の選択状態、OSC エンコードなど。EditMode テストで仕様を検証する |
| `VRKeyboard.App` | `Assets/VRKeyboard/App/` | MonoBehaviour、OpenVR、描画、通信、Win32 の呼び出し。Core の状態機械に入力を渡し、その結果を各部へ振り分ける |
| `Valve.OpenVR` | `Assets/VRKeyboard/ThirdParty/OpenVR/` | Valve 配布の `openvr_api.cs` をそのまま置く |

- Core は App を参照しない。Core から外部への作用（振動・変換の要求・送信）は、戻り値またはイベントとして App に返す。

## 外部由来のアセット

| 場所 | 内容 | 注意 |
|---|---|---|
| `Assets/VRKeyboard/ThirdParty/OpenVR/` | Valve 配布の `openvr_api.cs`・`LICENSE`、`Plugins/x86_64/openvr_api.dll` | Valve 配布のまま変更しない。asmdef は Editor と Windows 64bit に限定し、自動参照しない（`VRKeyboard.App` から明示参照する） |
| `Assets/StreamingAssets/SteamVR/` | SteamVR アクションマニフェスト（`actions.json`）とコントローラー別の既定バインディング（`bindings_*.json`）、ダッシュボードのタブのアイコン（`dashboard_icon.png`。自作） | ビルド後も実ファイルとして配置される必要があるため StreamingAssets に置く。SteamVR にはこのファイルのパスを渡す |
| `Assets/VRKeyboard/Fonts/` | Noto Sans JP（Regular・Bold の静的 OTF）と `OFL.txt` | TMP のフォントアセットは置かず、実行時に生成する（Play 中に atlas が書き換わり、アセットに差分が出るのを避けるため）。ライセンス文は再配布に必要なため同梱する |

## VRKeyboard.Core の構成

`Assets/VRKeyboard/Core/` のクラスと責務。テストは `Assets/VRKeyboard/Tests/EditMode/`（`VRKeyboard.Core.Tests`）にあり、`KeyboardSession` に操作を与える `Rig`（`TestSupport.cs`）で実際の操作と同じ入力列を再現する。

```
App ──Tick(ControllerFrame, 時刻)──→ KeyboardSession ──IKeyboardEffects──→ App
                                       │
        ┌──────────────────────────────┼───────────────────────┐
  DoubleChordDetector          GripInputMethod          ConversionController
  （開閉の判定）                 │   （入力中）            （変換中）
                               ├─ GripLayout（表のデータ）      └─ ConversionSession
                               ├─ StickClassifier                    （文節・候補の選択状態）
                               └─ InputBuffer ←── Text/*（CharacterModifier・CharacterWidth・Katakana）
```

| 種別 | クラス | 責務 |
|---|---|---|
| 状態機械 | `KeyboardSession` | 全体の状態機械。開閉・ダッシュボードによる一時停止・モード（入力 / 応答待ち / 変換中）を持ち、操作を `GripInputMethod` と `ConversionController` に振り分ける。しきい値の変更は `ApplySettings` で次のフレームから反映する（子音の手・最大長・同時押しの判定時間の変更は `NotSupportedException`）。グリップの押下エッジは常に追跡し、開いた直後・変換の開始・取り消しの直後に握っていたグリップは離すまで無視する。変換結果は `CompleteConversion` / `FailConversion` で受け取り、リクエスト ID が古いものは捨てる |
| | `IKeyboardEffects` | Core から App への通知（開閉・振動・変換の要求・送信の要求）。Core は結果をここへ出すだけで、通信や描画は知らない。送信の要求（`OnSendRequested`）だけは成否を `bool` で同期的に返し、`true` のときだけ Core が入力バッファを空にする（失敗時は内容を残して `Invalid` の振動を出す） |
| | `KeyboardSettings` | 設定値（子音の手・スティックとトリガーのしきい値・同時押しの窓・最大長）。戻り・離したとみなすしきい値は、設定値から `ReleaseMargin`（0.2）を引いて導く。不正な値は例外 |
| 入力 | `ControllerFrame` / `HandState` / `StickValue` | 1フレーム分の両手の状態（スティック・トリガーの引き量 0〜1・グリップ） |
| | `EdgeTracker` / `FrameEdges` | 前フレームとの差から押下エッジを出す。トリガーは引き量をしきい値で判定する（押している間は離したとみなすしきい値で判定するヒステリシス） |
| 開閉 | `DoubleChordDetector` | 両手グリップの2回続けての同時押しを検出する（時刻は引数で受け取り、時計に依存しない） |
| グリップ入力 | `GripInputMethod` | セル・方向の選択、機能ページ、トリガーによる入力と直前の文字の切り替え（手ごとの役割は SPEC 5.2）。バッファを更新し、変換・確定は要求（`GripRequest`）として返す。ページは言語と数字ページの有無から導出する |
| | `GripLayout` / `GripCell` | SPEC 5.2 の表をデータとして持つ |
| | `StickClassifier` / `StickDirectionTracker` | スティックの 8方向（3×3 のセル）・4方向への分類（ヒステリシス付き）と、中央から倒した瞬間の検出。候補欄のスティック操作でも使う |
| | `GripViewState` | 描画用のスナップショット（ラベル・無効・点灯・選択位置と、通常のページに重ねる機能ページのヒント）。App はこれを描くだけにする |
| テキスト | `InputBuffer` | 確定済みと未確定の文字列とそれぞれのカーソル、上限の検査。操作の結果は `EditResult` で返す |
| | `CharacterModifier` / `CharacterWidth` / `Katakana` | 直前の文字の切り替え（かなの小゛゜の巡回・英字の大文字/小文字）・全角化・ひらがな→カタカナの変換表 |
| 変換 | `TransliterateApi` | Google CGI API のリクエスト URL の組み立てと応答 JSON の解析（通信は App が行う） |
| | `ConversionSession` / `ConversionSegment` | 文節ごとの候補と選択状態。失敗時の代わりの候補（`CreateFallback`）も作る |
| | `ConversionController` | 変換中のスティック・トリガー・グリップ操作を、移動・確定・取り消しに変換する |
| 出力 | `OscCodec` / `ChatboxRateLimiter` | OSC メッセージのバイト列化（送信のみ）と、チャットボックスの送信間隔の制御 |

- 表示用の状態は `ViewVersion`（セッション全体）と各クラスの `Version` で変化を検出できる。App は変化したときだけ描き直す。
- 時刻・スティックの傾き・押下状態はすべて引数で受け取るため、Core のテストでは時間の経過を決め打ちで再現できる。

## VRKeyboard.App の構成

`Assets/VRKeyboard/App/`。asmdef は Editor と Windows 64bit に限定する。テストは `Assets/VRKeyboard/Tests/EditModeApp/`（`VRKeyboard.App.Tests`）にある。

| 場所 | クラス | 責務 |
|---|---|---|
| 直下 | `KeyboardApp` | 起動の根（MonoBehaviour）。部品を組み立て、毎フレーム「環境のイベント（`BeginFrame`）→ ダッシュボードの状態を渡す → 入力を読む → `Tick` → 変換の中止判定 → 表示の更新 → `/chatbox/typing` → チャットボックスの送信」を行う。`IKeyboardEffects` を実装し、振動・変換の要求・送信の要求を各部へ振り分ける。入力元・振動・オーバーレイの表示・終了通知は `IKeyboardPlatform` に任せ、`KeyboardApp` に OpenVR の分岐を持たせない。フォント（Regular・Bold）はシリアライズフィールドで受け取り、未設定は例外 |
| `Input/` | `IControllerSource` / `IHapticOutput` | 入力元（1フレーム分の `ControllerFrame` を返す）と振動の出力先の境界。OpenVR 実装はこの境界に差す |
| | `DebugKeyboardControllerSource` / `DebugHapticOutput` | PC のキーボード（旧 Input Manager）で両手を模す開発用の入力元と、直近の振動を保持して表示に渡す出力先。**キー割り当ては `DebugKeyboardControllerSource.Read` が唯一の定義**（画面への表示は `KeyboardPreview`） |
| `Platform/` | `IKeyboardPlatform` | 実行環境の境界。`Controller`・`Haptics`・`IsDashboardVisible`・`IsSettingsVisible`・`QuitRequested`、毎フレームの `BeginFrame()`（環境のイベントの処理）、開閉の通知 `OnOpenChanged(bool)`、キーボードの位置の変更 `SetPlacement` を持つ。実装は `OpenVRPlatform` と `DebugKeyboardPlatform` |
| | `DebugKeyboardPlatform` | `DebugKeyboardControllerSource` と `DebugHapticOutput` をまとめた開発用の環境。Tab キーで「ダッシュボードの設定タブを表示している状態」（`IsDashboardVisible` と `IsSettingsVisible` が両方 true）を切り替える。終了通知・開閉の通知・位置の変更は何もしない。`KeyboardPreview` へ振動の表示用に `DebugHaptics` を渡す |
| `SteamVR/` | `OpenVRPlatform` | OpenVR の環境。接続・入力・振動・オーバーレイ（キーボードと設定タブ）を作る。キーボードのオーバーレイは「開いている、または設定タブの表示中」に表示し、入力の占有（優先度）は開いている間だけにする。作成の途中で失敗したら、それまでのものを破棄して例外にする。名前空間と場所を `OpenVR` にしないのは、`Valve.VR.OpenVR` 型を隠さないため |
| | `OpenVRRuntime` | `VRApplication_Overlay` での初期化（SteamVR を起動しない）、アクションマニフェストの登録、毎フレームのイベント処理。`VREvent_Quit` は `AcknowledgeQuit_Exiting` を呼んで `QuitRequested` を出す。破棄で `VR_Shutdown`。プロセス内で同時に 1接続だけ許す |
| | `OpenVRActions` | アクションセットと各アクション（スティック・トリガー・グリップ・振動）のハンドルの取得。無効なハンドルは例外 |
| | `OpenVRControllerSource` | `IControllerSource` の OpenVR 実装。`UpdateActionState` でアクションセット 1つ（優先度は閉じている間 0、開いている間 `k_nActionSetOverlayGlobalPriorityMin`）を有効にして読む。`IsExclusive` を開いている間に下げることはしない。非アクティブ（`NoData`）は中立値、それ以外のエラーは例外 |
| | `OpenVRHapticOutput` | `IHapticOutput` の OpenVR 実装。種類ごとの値は `GetPattern`（値は SPEC 5.1） |
| | `KeyboardOverlay` | オーバーレイの作成・幅と HMD 相対の配置（`SetPlacement` で表示中も変えられる）・表示と破棄。テクスチャは `Rendered` ごとに `OverlayTextureSubmitter` へ渡す依頼を出す。毎フレームの `Update` でレンダースレッドでの失敗を確認する。破棄では、先に `OverlayTextureSubmitter` を解除してから `DestroyOverlay` を呼ぶ。色空間は Gamma（RenderTexture は sRGB 形式で、バイト列が符号化済み） |
| | `OverlayTextureSubmitter` | `SetOverlayTexture` を Unity のレンダースレッドで呼ぶ。`SetOverlayTexture` は呼び出したスレッドで、テクスチャの D3D11 デバイスのイミディエイトコンテキストを使ってコピーする。このコンテキストは Unity のレンダースレッドも使うため、メインスレッドから呼ぶと同時使用になり、GPU ドライバー内でクラッシュした。`GL.IssuePluginEvent` で描画コマンドの直後に実行を依頼するため、渡すテクスチャには描画が完了した内容が入る。コールバックは静的な登録簿（id → インスタンス）を引き、未登録なら何もしない。`Dispose` は同じロックの下で登録を解除するため、戻った後は `DestroyOverlay` と `VR_Shutdown` を安全に呼べる。コールバックは例外を出さず、最初の失敗を記録して、メインスレッドの `ThrowIfFailed` で送出する |
| | `SettingsDashboardOverlay` | 設定画面のダッシュボードオーバーレイ（タブ）とアイコン。入力方式はマウスで、マウスの座標の範囲をテクスチャのピクセル数にする。毎フレームの `Update` でオーバーレイのイベントを読み、マウスの移動・左ボタンを `ISettingsPointer` へ渡す（OpenVR の座標は左下が原点のため上下を反転する）。フォーカスを失う・隠れる・ダッシュボードが閉じるときは `PointerLeave` を呼ぶ。表示の状態は `IsOverlayVisible` で読む。テクスチャは `KeyboardOverlay` と同じく `OverlayTextureSubmitter` で渡す |
| | `OverlayTextures` | RenderTexture をオーバーレイに渡すための共通の処理（ネイティブテクスチャの取得、乗算済みアルファ、D3D11 の上下反転のためのテクスチャの範囲 `vMin=1, vMax=0`） |
| | `OverlayPlacement` | `KeyboardPlacement`（SPEC 4.1）を、`KeyboardTheme` の寸法から 3×4 の行列にする。基準は盤面の中心（テクスチャの中心ではない） |
| | `OpenVRErrors` | `EVRInputError` / `EVROverlayError` の検査。失敗は操作名とエラー名を含む例外にする |
| `View/` | `KeyboardView` | 表示の根。盤面・入力欄・グリップパネル・候補欄を組み立て、`RefreshIfChanged()` で `ViewVersion` が変わったときだけ各部を更新する |
| | `GripPanelView` / `InputAreaView` / `CandidateAreaView` | それぞれグリップのパネル（母音側・子音側、機能ページのヒント、ページ・全半角・大小のチップ）、入力欄（確定・未確定・カーソル・残り文字数・通知）、候補欄（応答待ち・文節と候補・失敗の理由）。いずれも `KeyboardSession` の状態を読むだけで、操作は持たない |
| | `SettingsView` | 設定画面の uGUI。`SettingsLayout` の配置で組み立て、`SettingsForm.Version` が変わったときだけ更新する。操作は持たない |
| | `OverlayRenderTarget` | 専用の無効なカメラと透明な RenderTexture、Screen Space - Camera のキャンバス。名前・大きさ・レイヤーは描画先ごとに受け取る。`Render()` を呼んだときだけ 1回描き、`Rendered` イベントで通知する。`Texture` が OpenVR のオーバーレイへ渡すもの |
| | `KeyboardFonts` | 日本語フォント（Regular・Bold）から TMP の動的フォントアセットを実行時に生成する（アセットとして保存しない）。atlas は満杯になると増える（multi atlas） |
| | `KeyboardTheme` / `UiBuilder` | 寸法と色の定数（キーボードのテクスチャは 1024×1024、設定画面の文字の大きさと色を含む）と、uGUI をコードで組み立てる補助 |
| | `KeyboardPreview` | エディターと開発ビルド専用。メインカメラを使わず OnGUI で、テクスチャ・振動・操作キーを表示する。設定画面の表示中は設定画面のテクスチャを表示し、開発用の環境ではマウスの操作を `ISettingsPointer` へ渡す |
| `Settings/` | `UserSettings` / `KeyboardPlacement` / `SettingRange` | 設定画面で変える値（表示位置と2つのしきい値）。不変で、範囲外は例外。`SettingRange` は範囲と刻みを持ち、刻みに丸める。しきい値の既定値は Core の `KeyboardSettings` の既定値から取る。`ToKeyboardSettings` で Core の設定にする |
| | `SettingsSerializer` / `SettingsStore` / `SettingsLoadResult` | 設定ファイルの JSON（形式は SPEC 8.1）への変換と読み書き。内容が正しくないファイルは別名に移し、既定値と通知を返す。読み書きの IO の失敗は例外のまま上げる。保存は一時ファイルに書いてから置き換える |
| | `SettingsCatalog` / `SettingGroup` / `SettingItem` | 画面に並べる項目の定義（見出し・注記・項目名・範囲・値の読み書き・表示の形式）。項目を足すときはここに加える |
| | `SettingsLayout` / `SettingsGroupLayout` / `SettingsRowLayout` / `SettingsHit` | 設定画面の配置（テクスチャ 1280×800 のピクセル、左上が原点）と当たり判定。表示（`SettingsView`）と操作（`SettingsForm`）が同じ配置を使う。項目が下端に収まらなければ例外 |
| | `SettingsForm` / `ISettingsPointer` | 設定画面の状態（値・指している対象・つまんでいるスライダー・押しているボタン・通知・版）とポインターの操作。ボタンは押したときと離したときの対象が同じなら実行する。値が変わるたびに `Changed`、スライダーを離す・ボタンを実行するなどで値が確定し、前回の確定から変わっていれば `Committed` を出す |
| `Conversion/` | `ConversionRunner` | 変換の要求を `GoogleTransliterateClient` に渡し、結果を `CompleteConversion` / `FailConversion` で返す。通信は同時に 1件で、新しい要求・応答待ちの終了・破棄のときは前の通信を中止する |
| | `GoogleTransliterateClient` | `TransliterateApi` の URL に `UnityWebRequest` で GET し、文節の一覧を返す（`Awaitable`・メインスレッド専用）。制限時間は 2秒で、自前の時計で測る。ログや例外に読みを含めない |
| | `ConversionFailedException` | 失敗の種類（接続・タイムアウト・HTTP エラー・応答の形式）と、入力欄に出す短い日本語（`UserMessage`）を持つ。キャンセルは `OperationCanceledException` |
| `Output/` | `TextOutputRouter` | 送信時点の前面ウィンドウで出力先を決める。前面なし・自プロセスは送らず、VRChat は `VRChatChatbox`、それ以外は `DesktopTextSender`。結果は `OutputResult`（成否・失敗の種類・通知文）で返す |
| | `VRChatChatbox` | OSC（UDP）の送信。`Send` で `ChatboxRateLimiter` のキューに積み、毎フレームの `Update(now)` で間隔を守って送る。`SetTyping` は値が変わったときだけ送る。接続しないソケットを使う（接続済みだと宛先未起動の ICMP が次の送信の例外になる） |
| | `DesktopTextSender` | `SendInput` + `KEYEVENTF_UNICODE`。送信前に `ProcessElevation` で対象の昇格状態を調べ、届かない相手には送らない |
| | `ProcessElevation` / `Win32` | トークンの昇格状態の照会と、Win32 API の宣言（`INPUT` は x64 の 40 バイト前提） |
| | `ForegroundWindowInfo` | 前面ウィンドウのハンドル・プロセス ID・プロセス名・表示名・タイトル。タイトルは内容を含みうるため、ログに出さない |
| | `AppSettings` | App 層の設定値（通知音・Enter・OSC の宛先）。不正な値は例外。設定画面と設定ファイルにはまだ含めていない |

- 出力の `TextOutputRouter` はウィンドウの取得・チャットボックス・デスクトップ送信を差し替えられるため、振り分けの規則は Win32 なしでテストできる。

**描画の流れと構成**

- 描画は `KeyboardApp.Update` が `KeyboardView.RefreshIfChanged()` で `ViewVersion` の変化を検出したときだけ `OverlayRenderTarget.Render()` を呼ぶ。キーボードは開いている間と設定画面の表示中だけ描き、表示し始めたフレームでは版が変わっていなくても描く。設定画面は `SettingsView.RefreshIfChanged()` が true のときに描く。
- `Render()` は `Canvas.ForceUpdateCanvases()` でレイアウトと TMP のメッシュを確定してから、`RenderPipeline.SubmitRenderRequest`（URP の `SingleCameraRequest`）で無効なカメラを 1回描く。`Camera.Render()` と違い、出力先を要求に含められ、対応していないパイプラインでは例外にできる。カメラの `targetTexture` は常に設定しておく（Screen Space - Camera のキャンバスの大きさがテクスチャのピクセルに一致するため）。
- 描画先ごとのレイヤー `VRKeyboardOverlay`（キーボード）と `VRKeyboardSettings`（設定画面）を Tags and Layers に登録している。各カメラは自分のレイヤーだけを映す（同じ位置にある別の描画先の uGUI を映さないため）。TMP の `Mobile/Distance Field` シェーダーは Always Included Shaders に登録している（ビルドで除外されないため）。
- TMP の `Ellipsis` は、レイアウトで幅が決まる文字では描かれなくなるため、候補欄は文字列を自前で切って省略記号を付ける。
- シーン `Assets/VRKeyboard/Scenes/Main.unity` は `KeyboardApp` を持つ 1つのオブジェクトだけで構成し、フォントを設定する。Build Settings の先頭に登録している。

**入力元の選択と開閉の流れ**

- 環境の選択は `KeyboardApp.CreatePlatform` だけで行う。ビルドは常に `OpenVRPlatform`。エディターは `_editorDevice`（`DebugKeyboard` が既定 / `OpenVR`）に従う。OpenVR の初期化に失敗したときは例外にし、`Awake` で `enabled = false` にして毎フレームの例外を防ぐ（別の環境へ切り替えない）。
- 開閉は Core の `IKeyboardEffects.OnOpenChanged` → `KeyboardApp` → `IKeyboardPlatform.OnOpenChanged` と流れ、OpenVR では `OpenVRControllerSource.IsExclusive` とオーバーレイの `Show` / `Hide` が切り替わる（設定タブの表示中は閉じていてもオーバーレイを表示したままにする）。ダッシュボードの表示は、毎フレーム `IsDashboardVisible` を `KeyboardSession.SetDashboardVisible` へ渡し、手動で閉じるのと同じ経路で閉じる（SPEC 4.4）。
- 毎フレームの順序は `BeginFrame`（イベント処理と設定タブの操作。終了を求められたら以降の API を呼ばない）→ `SetDashboardVisible` → `Read` → `Tick` → 描画（`Rendered` でテクスチャをオーバーレイへ渡す）。
- エディターで再生を繰り返しても接続できるよう、終了時はオーバーレイを破棄してから `VR_Shutdown` を呼ぶ。`OpenVR.Init` が管理側のインターフェースを作り直すため、次の再生で再初期化できる。ハンドルとマニフェストは接続ごとに取得・登録し直す。

**設定の流れ**

- 起動時に `SettingsStore.Load` で `%AppData%\VRKeyboard\settings.json` を読み、`UserSettings` から Core の `KeyboardSettings` とオーバーレイの最初の位置を作る。
- 設定タブのレーザー操作は `SettingsDashboardOverlay.Update`（`BeginFrame` の中）→ `SettingsForm` と流れる。開発用の環境では `KeyboardPreview` のマウス操作が同じ `ISettingsPointer` に入る。
- `SettingsForm.Changed` で `KeyboardSession.ApplySettings` と `IKeyboardPlatform.SetPlacement` を呼び、`Committed` で保存する。保存の失敗（`IOException` / `UnauthorizedAccessException`）は `SettingsForm.SetNotice` で画面に出し、次に保存できたら読み込み時の通知に戻す。

**ビルド**

- Windows 64bit の出力先はプロジェクト直下の `Builds/` で、`.gitignore` により管理対象外。`openvr_api.dll` が `<名前>_Data/Plugins/x86_64/` に、アクションマニフェストが `<名前>_Data/StreamingAssets/SteamVR/` に入る。
