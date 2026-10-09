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
| `Assets/StreamingAssets/SteamVR/` | SteamVR アクションマニフェスト（`actions.json`）とコントローラー別の既定バインディング（`bindings_*.json`） | ビルド後も実ファイルとして配置される必要があるため StreamingAssets に置く。SteamVR にはこのファイルのパスを渡す |
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
                               └─ InputBuffer ←── Text/*（KanaModifier・CharacterWidth・Katakana）
```

| 種別 | クラス | 責務 |
|---|---|---|
| 状態機械 | `KeyboardSession` | 全体の状態機械。開閉・ダッシュボードによる一時停止・モード（入力 / 応答待ち / 変換中）を持ち、操作を `GripInputMethod` と `ConversionController` に振り分ける。グリップの押下エッジは常に追跡し、開いた直後・変換の開始・取り消しの直後に握っていたグリップは離すまで無視する。変換結果は `CompleteConversion` / `FailConversion` で受け取り、リクエスト ID が古いものは捨てる |
| | `IKeyboardEffects` | Core から App への通知（開閉・振動・変換の要求・送信の要求）。Core は結果をここへ出すだけで、通信や描画は知らない。送信の要求（`OnSendRequested`）だけは成否を `bool` で同期的に返し、`true` のときだけ Core が入力バッファを空にする（失敗時は内容を残して `Invalid` の振動を出す） |
| | `KeyboardSettings` | 設定値（子音の手・スティックの閾値・同時押しの窓・最大長）。不正な値は例外 |
| 入力 | `ControllerFrame` / `HandState` / `StickValue` | 1フレーム分の両手の状態（スティック・トリガー・グリップ） |
| | `EdgeTracker` / `FrameEdges` | 前フレームとの差から押下エッジを出す |
| 開閉 | `DoubleChordDetector` | 両手グリップの2回続けての同時押しを検出する（時刻は引数で受け取り、時計に依存しない） |
| グリップ入力 | `GripInputMethod` | セル・方向の選択、機能ページ、トリガーによる入力。バッファを更新し、変換・確定は要求（`GripRequest`）として返す。ページは言語と数字ページの有無から導出する |
| | `GripLayout` / `GripCell` | SPEC 5.2 の表をデータとして持つ |
| | `StickClassifier` / `StickDirectionTracker` | スティックの 8方向（3×3 のセル）・4方向への分類（ヒステリシス付き）と、中央から倒した瞬間の検出。候補欄のスティック操作でも使う |
| | `GripViewState` | 描画用のスナップショット（ラベル・無効・点灯・選択位置）。App はこれを描くだけにする |
| テキスト | `InputBuffer` | 確定済みと未確定の文字列とそれぞれのカーソル、上限の検査。操作の結果は `EditResult` で返す |
| | `KanaModifier` / `CharacterWidth` / `Katakana` | 小゛゜の巡回・全角化・ひらがな→カタカナの変換表 |
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
| `Platform/` | `IKeyboardPlatform` | 実行環境の境界。`Controller`・`Haptics`・`IsDashboardVisible`・`QuitRequested`、毎フレームの `BeginFrame()`（環境のイベントの処理）、開閉の通知 `OnOpenChanged(bool)` を持つ。実装は `OpenVRPlatform` と `DebugKeyboardPlatform` |
| | `DebugKeyboardPlatform` | `DebugKeyboardControllerSource` と `DebugHapticOutput` をまとめた開発用の環境。ダッシュボードは常に非表示で、終了通知・開閉の通知は何もしない。`KeyboardPreview` へ振動の表示用に `DebugHaptics` を渡す |
| `SteamVR/` | `OpenVRPlatform` | OpenVR の環境。接続・入力・振動・オーバーレイを作り、開閉に合わせて「入力の占有（優先度）」と「オーバーレイの表示・非表示」を同時に切り替える。作成の途中で失敗したら、それまでのものを破棄して例外にする。名前空間と場所を `OpenVR` にしないのは、`Valve.VR.OpenVR` 型を隠さないため |
| | `OpenVRRuntime` | `VRApplication_Overlay` での初期化（SteamVR を起動しない）、アクションマニフェストの登録、毎フレームのイベント処理。`VREvent_Quit` は `AcknowledgeQuit_Exiting` を呼んで `QuitRequested` を出す。破棄で `VR_Shutdown`。プロセス内で同時に 1接続だけ許す |
| | `OpenVRActions` | アクションセットと各アクション（スティック・トリガー・グリップ・振動）のハンドルの取得。無効なハンドルは例外 |
| | `OpenVRControllerSource` | `IControllerSource` の OpenVR 実装。`UpdateActionState` でアクションセット 1つ（優先度は閉じている間 0、開いている間 `k_nActionSetOverlayGlobalPriorityMin`）を有効にして読む。`IsExclusive` を開いている間に下げることはしない。非アクティブ（`NoData`）は中立値、それ以外のエラーは例外 |
| | `OpenVRHapticOutput` | `IHapticOutput` の OpenVR 実装。種類ごとの値は `GetPattern`（値は SPEC 5.1） |
| | `KeyboardOverlay` | オーバーレイの作成・幅・乗算済みアルファ・テクスチャの範囲（`vMin=1, vMax=0`）・HMD 相対の配置・表示と破棄。テクスチャは `Rendered` ごとに渡し、続く 2フレームも渡し直す（Unity の描画はレンダースレッドで遅れて完了するため）。色空間は Gamma（RenderTexture は sRGB 形式で、バイト列が符号化済み） |
| | `OverlayPlacement` | SPEC 4.1 の位置を、`KeyboardTheme` の寸法から 3×4 の行列にする。基準は盤面の中心（テクスチャの中心ではない） |
| | `OpenVRErrors` | `EVRInputError` / `EVROverlayError` の検査。失敗は操作名とエラー名を含む例外にする |
| `View/` | `KeyboardView` | 表示の根。盤面・入力欄・グリップパネル・候補欄を組み立て、`RefreshIfChanged()` で `ViewVersion` が変わったときだけ各部を更新する |
| | `GripPanelView` / `InputAreaView` / `CandidateAreaView` | それぞれグリップのパネル（母音側・子音側、ページ・全半角・大小のチップ）、入力欄（確定・未確定・カーソル・残り文字数・通知）、候補欄（応答待ち・文節と候補・失敗の理由）。いずれも `KeyboardSession` の状態を読むだけで、操作は持たない |
| | `OverlayRenderTarget` | 専用の無効なカメラと透明な RenderTexture、Screen Space - Camera のキャンバス。`Render()` を呼んだときだけ 1回描き、`Rendered` イベントで通知する。`Texture` が OpenVR のオーバーレイへ渡すもの |
| | `KeyboardFonts` | 日本語フォント（Regular・Bold）から TMP の動的フォントアセットを実行時に生成する（アセットとして保存しない）。atlas は満杯になると増える（multi atlas） |
| | `KeyboardTheme` / `UiBuilder` | 寸法と色の定数（テクスチャは 1024×1024）と、uGUI をコードで組み立てる補助 |
| | `KeyboardPreview` | エディターと開発ビルド専用。メインカメラを使わず OnGUI で、テクスチャ・振動・操作キーを表示する |
| `Conversion/` | `ConversionRunner` | 変換の要求を `GoogleTransliterateClient` に渡し、結果を `CompleteConversion` / `FailConversion` で返す。通信は同時に 1件で、新しい要求・応答待ちの終了・破棄のときは前の通信を中止する |
| | `GoogleTransliterateClient` | `TransliterateApi` の URL に `UnityWebRequest` で GET し、文節の一覧を返す（`Awaitable`・メインスレッド専用）。制限時間は 2秒で、自前の時計で測る。ログや例外に読みを含めない |
| | `ConversionFailedException` | 失敗の種類（接続・タイムアウト・HTTP エラー・応答の形式）と、入力欄に出す短い日本語（`UserMessage`）を持つ。キャンセルは `OperationCanceledException` |
| `Output/` | `TextOutputRouter` | 送信時点の前面ウィンドウで出力先を決める。前面なし・自プロセスは送らず、VRChat は `VRChatChatbox`、それ以外は `DesktopTextSender`。結果は `OutputResult`（成否・失敗の種類・通知文）で返す |
| | `VRChatChatbox` | OSC（UDP）の送信。`Send` で `ChatboxRateLimiter` のキューに積み、毎フレームの `Update(now)` で間隔を守って送る。`SetTyping` は値が変わったときだけ送る。接続しないソケットを使う（接続済みだと宛先未起動の ICMP が次の送信の例外になる） |
| | `DesktopTextSender` | `SendInput` + `KEYEVENTF_UNICODE`。送信前に `ProcessElevation` で対象の昇格状態を調べ、届かない相手には送らない |
| | `ProcessElevation` / `Win32` | トークンの昇格状態の照会と、Win32 API の宣言（`INPUT` は x64 の 40 バイト前提） |
| | `ForegroundWindowInfo` | 前面ウィンドウのハンドル・プロセス ID・プロセス名・表示名・タイトル。タイトルは内容を含みうるため、ログに出さない |
| | `AppSettings` | App 層の設定値（通知音・Enter・OSC の宛先）。不正な値は例外 |

- 出力の `TextOutputRouter` はウィンドウの取得・チャットボックス・デスクトップ送信を差し替えられるため、振り分けの規則は Win32 なしでテストできる。

**描画の流れと構成**

- 描画は `KeyboardApp.Update` が `KeyboardView.RefreshIfChanged()` で `ViewVersion` の変化を検出したときだけ `OverlayRenderTarget.Render()` を呼ぶ。閉じている間は描かない（開いた時点で版が変わるため、そのとき描かれる）。
- `Render()` は `Canvas.ForceUpdateCanvases()` でレイアウトと TMP のメッシュを確定してから、`RenderPipeline.SubmitRenderRequest`（URP の `SingleCameraRequest`）で無効なカメラを 1回描く。`Camera.Render()` と違い、出力先を要求に含められ、対応していないパイプラインでは例外にできる。カメラの `targetTexture` は常に設定しておく（Screen Space - Camera のキャンバスの大きさがテクスチャのピクセルに一致するため）。
- オーバーレイ専用のレイヤー `VRKeyboardOverlay` を Tags and Layers に登録している。カメラはこのレイヤーだけを映す。TMP の `Mobile/Distance Field` シェーダーは Always Included Shaders に登録している（ビルドで除外されないため）。
- TMP の `Ellipsis` は、レイアウトで幅が決まる文字では描かれなくなるため、候補欄は文字列を自前で切って省略記号を付ける。
- シーン `Assets/VRKeyboard/Scenes/Main.unity` は `KeyboardApp` を持つ 1つのオブジェクトだけで構成し、フォントを設定する。Build Settings の先頭に登録している。

**入力元の選択と開閉の流れ**

- 環境の選択は `KeyboardApp.CreatePlatform` だけで行う。ビルドは常に `OpenVRPlatform`。エディターは `_editorDevice`（`DebugKeyboard` が既定 / `OpenVR`）に従う。OpenVR の初期化に失敗したときは例外にし、`Awake` で `enabled = false` にして毎フレームの例外を防ぐ（別の環境へ切り替えない）。
- 開閉は Core の `IKeyboardEffects.OnOpenChanged` → `KeyboardApp` → `IKeyboardPlatform.OnOpenChanged` と流れ、OpenVR では `OpenVRControllerSource.IsExclusive` とオーバーレイの `Show` / `Hide` が同時に切り替わる。ダッシュボードの表示は、毎フレーム `IsDashboardVisible` を `KeyboardSession.SetDashboardVisible` へ渡し、手動で閉じるのと同じ経路で閉じる（SPEC 4.4）。
- 毎フレームの順序は `BeginFrame`（イベント処理。終了を求められたら以降の API を呼ばない）→ `SetDashboardVisible` → `Read` → `Tick` → 描画（`Rendered` でテクスチャをオーバーレイへ渡す）。
- エディターで再生を繰り返しても接続できるよう、終了時はオーバーレイを破棄してから `VR_Shutdown` を呼ぶ。`OpenVR.Init` が管理側のインターフェースを作り直すため、次の再生で再初期化できる。ハンドルとマニフェストは接続ごとに取得・登録し直す。

**ビルド**

- Windows 64bit の出力先はプロジェクト直下の `Builds/` で、`.gitignore` により管理対象外。`openvr_api.dll` が `<名前>_Data/Plugins/x86_64/` に、アクションマニフェストが `<名前>_Data/StreamingAssets/SteamVR/` に入る。
