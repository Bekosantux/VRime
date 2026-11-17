# アーキテクチャ

外部仕様は [docs/SPEC.md](docs/SPEC.md) を参照。本書は設計（クラス責務・ディレクトリ構成・依存の方向）を扱う。

## アセンブリと依存の方向

```
VRime.App  ──→  VRime.Core
      │
      └──────────→  Valve.OpenVR
```

| アセンブリ | 場所 | 責務 |
|---|---|---|
| `VRime.Core` | `Assets/VRime/Core/` | Unity・OpenVR・Win32 に依存しない純粋なロジック（`noEngineReferences`）。入力方式の状態機械、入力バッファ、変換候補の選択状態、OSC エンコードなど。EditMode テストで仕様を検証する |
| `VRime.App` | `Assets/VRime/App/` | MonoBehaviour、OpenVR、描画、通信、Win32 の呼び出し。Core の状態機械に入力を渡し、その結果を各部へ振り分ける |
| `Valve.OpenVR` | `Assets/VRime/ThirdParty/OpenVR/` | Valve 配布の `openvr_api.cs` をそのまま置く |

- Core は App を参照しない。Core から外部への作用（振動・変換の要求・送信）は、戻り値またはイベントとして App に返す。

## 外部由来のアセット

| 場所 | 内容 | 注意 |
|---|---|---|
| `Assets/VRime/ThirdParty/OpenVR/` | Valve 配布の `openvr_api.cs`・`LICENSE`、`Plugins/x86_64/openvr_api.dll` | Valve 配布のまま変更しない。asmdef は Editor と Windows 64bit に限定し、自動参照しない（`VRime.App` から明示参照する） |
| `Assets/StreamingAssets/SteamVR/` | SteamVR アクションマニフェスト（`actions.json`）とコントローラー別の既定バインディング（`bindings_*.json`）、ダッシュボードのタブのアイコン（`dashboard_icon.png`。256px） | ビルド後も実ファイルとして配置される必要があるため StreamingAssets に置く。SteamVR にはこのファイルのパスを渡す。既定のバインドは、グリップを展開・収納とパネルの切り替えの 2つのエントリーに割り当てる（1つのエントリーは 1つのアクションにしか出力できないため） |
| `Assets/StreamingAssets/Windows/tray_icon.ico` | タスクトレイのアイコン。16・20・24・32・40・48px（`SM_CXSMICON` の表示倍率 100〜300% の大きさ） | Win32 の `LoadImage` にファイルのパスを渡すため StreamingAssets に置く（小さいアイコンの大きさに合うものが選ばれる）。16〜32px は線を太く描き直した版で、SVG を縮小したものとは異なる |
| `Assets/VRime/Icon/vrime-icon-1024.png` | exe のアイコン（Player Settings の Default Icon） | Unity が各サイズに縮小する |
| `Images/Icons/` | アイコンの原版（`vrime-icon.svg`）と 1024px の PNG | 上の 3つのアイコンはここから作る。色や形を変えたら 3つとも作り直す |
| `Assets/VRime/Fonts/` | Noto Sans JP（Regular・Bold の静的 OTF）と `OFL.txt`、編集用の TMP フォントアセット（`NotoSansJP-* SDF.asset`） | ライセンス文は再配布に必要なため同梱する。OTF は Include Font Data を有効にしておく（実行用のフォントアセットの元になる）。編集用のフォントアセットは Dynamic・atlas 512・Clear Dynamic Data On Build。エディターでシーンを開くと表示した文字が atlas に追加され、アセットに差分が出る（Play 中は実行用のアセットに差し替えるため出ない） |

## VRime.Core の構成

`Assets/VRime/Core/` のクラスと責務。テストは `Assets/VRime/Tests/EditMode/`（`VRime.Core.Tests`）にあり、`KeyboardSession` に操作を与える `Rig`（`TestSupport.cs`）で実際の操作と同じ入力列を再現する。

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
| 状態機械 | `KeyboardSession` | 全体の状態機械。開閉・ダッシュボードによる一時停止・モード（入力 / 応答待ち / 変換中）を持ち、操作を `GripInputMethod` と `ConversionController` に振り分ける。しきい値・連続入力の時間・変換を使えるかの変更は `ApplySettings` で次のフレームから反映する（子音の手・同時押しの判定時間の変更は `NotSupportedException`）。文字数の上限は送信先に合わせて App が `SetLengthLimit` で毎フレーム渡し、上限を超えている間は確定キーで送信を要求しない。パネルの切り替えの押下エッジは常に追跡し、開いた直後・変換の開始・取り消しの直後に押していたパネルの切り替えは離すまで無視する。両手の展開・収納を押している間はトリガーを受け付けない。変換結果は `CompleteConversion` / `FailConversion` で受け取り、リクエスト ID が古いものは捨てる |
| | `IKeyboardEffects` | Core から App への通知（開閉・振動・変換の要求・送信の要求）。Core は結果をここへ出すだけで、通信や描画の処理は持たない。送信の要求（`OnSendRequested`）だけは成否を `bool` で同期的に返し、`true` のときだけ Core が入力バッファを空にする（失敗時は内容を残して `Invalid` の振動を出す） |
| | `KeyboardSettings` | 設定値（子音の手・スティックとトリガーのしきい値・連続入力の開始までの時間と間隔・同時押しの窓・変換を使えるか）。変換を使えない間、`GripInputMethod` は「変換」を無効のセルにし、押されても要求を出さずに `Invalid` の振動を返す。しきい値は 1 つで、倒した・押したと中央に戻った・離したの両方の判定に使う。不正な値は例外 |
| 入力 | `ControllerFrame` / `HandState` / `StickValue` | 1フレーム分の両手の状態。SteamVR のアクション（SPEC 5.6）ごとに、キーの選択（スティック）・キーの入力（トリガーの引き量 0〜1）・展開・収納・パネルの切り替えを持つ。物理的なボタンの情報は持たない（既定のバインドではグリップが展開・収納とパネルの切り替えの両方を押す） |
| | `EdgeTracker` / `FrameEdges` | 前フレームとの差から、トリガーとパネルの切り替えの押下エッジを出す。トリガーは引き量がしきい値以上なら押したとみなす |
| | `KeyRepeater` | 長押しによる連続入力の時刻管理（どの手のトリガーか・次に繰り返す時刻）。1回の `Poll` で繰り返すのは1回までで、遅れた分は捨てる |
| 開閉 | `DoubleChordDetector` | 両手の展開・収納の2回続けての同時押しを検出する（時刻は引数で受け取り、時計に依存しない） |
| グリップ入力 | `GripInputMethod` | セル・方向の選択、機能ページ、トリガーによる入力と直前の文字の切り替え（手ごとの役割は SPEC 5.2）、長押しによる連続入力。連続入力は `KeyRepeater` で時刻を管理し、繰り返すときは同じセルをもう一度押したものとして処理する。止める条件（選択の変化・別のトリガー・引き量がしきい値未満・`Reset`）はここで判定する。バッファを更新し、変換・確定は要求（`GripRequest`）として返す。ページは言語と数字ページの有無から導出する |
| | `GripLayout` / `GripCell` | SPEC 5.2 の表をデータとして持つ |
| | `StickClassifier` / `StickDirectionTracker` | スティックの 8方向（3×3 のセル）・4方向への分類（倒し量がしきい値未満なら中央。直前の状態によらない）と、中央から倒した瞬間の検出。候補欄のスティック操作でも使う |
| | `GripViewState` | 描画用のスナップショット（ラベル・無効・点灯・選択位置と、通常のページに重ねる機能ページのヒント）。App はこれを描くだけにする |
| テキスト | `InputBuffer` | 確定済みと未確定の文字列とそれぞれのカーソル、長さの上限の検査。上限は途中で変えられ（null は上限なし）、変えても内容は切り詰めないため、上限を超えた状態がありうる（`IsOverLengthLimit`）。操作の結果は `EditResult` で返す |
| | `CharacterModifier` / `CharacterWidth` / `Katakana` | 直前の文字の切り替え（かなの小゛゜の巡回・英字の大文字/小文字）・全角化・ひらがな→カタカナの変換表 |
| 変換 | `TransliterateApi` | Google CGI API のリクエスト URL の組み立てと応答 JSON の解析（通信は App が行う） |
| | `ConversionSession` / `ConversionSegment` | 文節ごとの候補と選択状態。失敗時の代わりの候補（`CreateFallback`）も作る |
| | `ConversionController` | 変換中のスティック・トリガー・グリップ操作を、移動・確定・取り消しに変換する |
| 出力 | `OscCodec` / `ChatboxOutbox` | OSC メッセージのバイト列化（送信のみ）と、チャットボックスへ出すメッセージの順序と間隔の制御。送信は順にすべて、入力中の文字は最新の 1 件だけを出し、送信を優先して間隔を共有する。送信を積むと、入力中の文字は出したものも含めて取り下げる（送信した文字列に置き換わるため） |

- 表示用の状態は `ViewVersion`（セッション全体）と各クラスの `Version` で変化を検出できる。App は変化したときだけ描き直す。
- 時刻・スティックの傾き・押下状態はすべて引数で受け取るため、Core のテストでは時間の経過を決め打ちで再現できる。

## VRime.App の構成

`Assets/VRime/App/`。asmdef は Editor と Windows 64bit に限定する。テストは `Assets/VRime/Tests/EditModeApp/`（`VRime.App.Tests`）にある。

| 場所 | クラス | 責務 |
|---|---|---|
| 直下 | `KeyboardApp` | 起動の根（MonoBehaviour）。部品を組み立て、毎フレーム「環境のイベント（`BeginFrame`）→ トレイの操作（`TrayIcon.TryTakeCommand`）→ 前面のウィンドウの監視と文字数の上限 → ダッシュボードの状態を渡す → 入力を読む → `Tick` → 変換の中止判定 → 位置の確認用の表示（`SetPlacementPreviewVisible`。設定タブかデスクトップの設定ウィンドウの表示中で、選んでいるカテゴリが `ShowsKeyboardPreview` のとき）→ 表示の更新 → `/chatbox/typing` と入力中の文字の表示（どちらも送信先が VRChat の間だけ）→ チャットボックスの送信」を行う。送信は失敗したときだけ通知を入力欄に出す。ビルドでは組み立て（`Awake`）の最後に `TrayIcon` と `DesktopSettingsWindow` を作る。`Start` で、設定に従って `VRChatWindow.Focus` を呼び、ビルドでは `PlayerWindow.Hide` でウィンドウを隠す（Unity は最初のシーンの `Awake` の後、`Start` の前にウィンドウを表示するため、`Awake` で隠しても表示し直される。`Awake` が例外で終わると `Start` は呼ばれず、ウィンドウが残る。前面を持っているうちに VRChat へ切り替えるため、`Focus` より後に行う。隠した後も操作できるよう、トレイのアイコンは `Awake` で先に置く）。`Application.wantsToQuit` で、設定ウィンドウの表示中にウィンドウを閉じる操作を取り消して設定ウィンドウを隠す（`_quitting` が立っていれば通す）。終了は `IKeyboardPlatform.QuitRequested`・`SettingsForm.QuitRequested`・`TrayCommand.Quit` で受け、終了時はチャットボックスの入力中の文字を消す。`IKeyboardEffects` を実装し、振動・変換の要求・送信の要求を各部へ振り分ける。入力元・振動・オーバーレイの表示・終了通知は `IKeyboardPlatform` に任せ、`KeyboardApp` に OpenVR の分岐を持たせない。編集用のフォントアセット・`KeyboardTheme`・2つの描画先とビューはシリアライズフィールドで受け取り、起動時に `ValidateScene()` でシーンの構成を検証する（誤りは場所を示す例外） |
| `Input/` | `IControllerSource` / `IHapticOutput` | 入力元（1フレーム分の `ControllerFrame` を返す）と振動の出力先の境界。OpenVR 実装はこの境界に差す |
| | `DebugKeyboardControllerSource` / `DebugHapticOutput` | PC のキーボード（旧 Input Manager）で両手を模す開発用の入力元と、直近の振動を保持して表示に渡す出力先。**キー割り当ては `DebugKeyboardControllerSource.Read` が唯一の定義**（画面への表示は `KeyboardPreview`）。グリップのキーは既定のバインドと同じく、展開・収納とパネルの切り替えの両方を押す |
| `Platform/` | `IKeyboardPlatform` | 実行環境の境界。`Controller`・`Haptics`・`IsDashboardHoldingInput`・`IsSettingsVisible`・`QuitRequested`、毎フレームの `BeginFrame()`（環境のイベントの処理）、開閉の通知 `OnOpenChanged(bool)`、位置の確認用にキーボードを表示するか `SetPlacementPreviewVisible(bool)`、キーボードの位置の変更 `SetPlacement`、入力を読むのに本アプリのウィンドウが前面である必要があるか（`RequiresOwnWindowFocus`。true の環境では起動時に VRChat を前面にしない）を持つ。実装は `OpenVRPlatform` と `DebugKeyboardPlatform` |
| | `DebugKeyboardPlatform` | `DebugKeyboardControllerSource` と `DebugHapticOutput` をまとめた開発用の環境。Tab キーで「ダッシュボードの設定タブを表示している状態」（`IsDashboardHoldingInput` と `IsSettingsVisible` が両方 true）を切り替える。終了通知・開閉の通知・位置の確認用の表示・位置の変更は何もしない。`KeyboardPreview` へ振動の表示用に `DebugHaptics` を渡す |
| `SteamVR/` | `OpenVRPlatform` | OpenVR の環境。接続・入力・振動・オーバーレイ（キーボードと設定タブ）を作る。キーボードのオーバーレイは「開いている、または位置の確認用に表示する（`SetPlacementPreviewVisible`）」間に表示し、入力の占有（優先度）は開いている間だけにする。`IsDashboardHoldingInput` はダッシュボードの表示中に加え、閉じた後に `IsInputAvailable` が true に戻るまでの間も true にする（入力が戻る前に優先度を上げると占有が有効にならないため。SPEC 4.4）。作成の途中で失敗したら、それまでのものを破棄して例外にする。名前空間と場所を `OpenVR` にしないのは、`Valve.VR.OpenVR` 型を隠さないため |
| | `OpenVRRuntime` | `VRApplication_Overlay` での初期化（SteamVR を起動しない）、アクションマニフェストの登録、毎フレームのイベント処理。`VREvent_Quit` は `AcknowledgeQuit_Exiting` を呼んで `QuitRequested` を出す。破棄で `VR_Shutdown`。プロセス内で同時に 1接続だけ許す |
| | `OpenVRActions` | アクションセットと各アクション（展開・収納・パネルの切り替え・キーの選択・キーの入力・振動）の名前とハンドルの取得。無効なハンドルは例外。名前の一覧（`Names`）はマニフェストの `actions` と過不足なく一致させ、`ActionManifestTests` が既定のバインドとあわせて検査する |
| | `OpenVRControllerSource` | `IControllerSource` の OpenVR 実装。`UpdateActionState` でアクションセット 1つ（優先度は閉じている間 0、開いている間 `k_nActionSetOverlayGlobalPriorityMin`）を有効にして読む。`IsExclusive` を開いている間に下げることはしない。非アクティブ（`NoData`）は中立値、それ以外のエラーは例外 |
| | `OpenVRHapticOutput` | `IHapticOutput` の OpenVR 実装。種類ごとの値は `GetPattern`（値は SPEC 5.1） |
| | `KeyboardOverlay` | オーバーレイの作成・幅と HMD 相対の配置（`SetPlacement` で表示中も変えられる）・表示と破棄。テクスチャは `Rendered` ごとに `OverlayTextureSubmitter` へ渡す依頼を出す。毎フレームの `Update` でレンダースレッドでの失敗を確認する。破棄では、先に `OverlayTextureSubmitter` を解除してから `DestroyOverlay` を呼ぶ。色空間は Gamma（RenderTexture は sRGB 形式で、バイト列が符号化済み） |
| | `OverlayTextureSubmitter` | `SetOverlayTexture` を Unity のレンダースレッドで呼ぶ。`SetOverlayTexture` は呼び出したスレッドで、テクスチャの D3D11 デバイスのイミディエイトコンテキストを使ってコピーする。このコンテキストは Unity のレンダースレッドも使うため、メインスレッドから呼ぶと同時使用になり、GPU ドライバー内でクラッシュした。`GL.IssuePluginEvent` で描画コマンドの直後に実行を依頼するため、渡すテクスチャには描画が完了した内容が入る。コールバックは静的な登録簿（id → インスタンス）を引き、未登録なら何もしない。`Dispose` は同じロックの下で登録を解除するため、戻った後は `DestroyOverlay` と `VR_Shutdown` を安全に呼べる。コールバックは例外を出さず、最初の失敗を記録して、メインスレッドの `ThrowIfFailed` で送出する |
| | `SettingsDashboardOverlay` | 設定画面のダッシュボードオーバーレイ（タブ）とアイコン。入力方式はマウスで、マウスの座標の範囲をテクスチャのピクセル数にする。毎フレームの `Update` でオーバーレイのイベントを読み、マウスの移動・左ボタンを `ISettingsPointer` へ渡す（座標は `OverlayTextures.ToTexturePosition` で設定画面の座標にする）。フォーカスを失う・隠れる・ダッシュボードが閉じるときは `PointerLeave` を呼ぶ。表示の状態は `IsOverlayVisible` で読む。テクスチャは `KeyboardOverlay` と同じく `OverlayTextureSubmitter` で渡す |
| | `OverlayTextures` | RenderTexture をオーバーレイに渡すための共通の処理（ネイティブテクスチャの取得、乗算済みアルファ、D3D11 の上下反転のためのテクスチャの範囲 `vMin=1, vMax=0`、マウスの座標の変換）。マウスの座標はテクスチャの範囲を通して求められるため、v を反転したオーバーレイでは上が 0 の座標で届き、そのまま設定画面の座標になる |
| | `OverlayPlacement` | `KeyboardPlacement`（SPEC 4.1）を 3×4 の行列にする。基準は盤面の中心（テクスチャの中心ではない）で、そのずれは `KeyboardView.BoardCenterOffset`（シーンの盤面の位置から求める）を受け取る |
| | `OpenVRErrors` | `EVRInputError` / `EVROverlayError` の検査。失敗は操作名とエラー名を含む例外にする |
| `View/` | `KeyboardView` | キーボードの表示の根（キャンバスに置く）。`RefreshIfChanged()` で `ViewVersion` か `ForegroundWindowMonitor.Version` が変わったときだけ各部を更新する。`BoardCenterOffset` は盤面（`_board`）の中心のテクスチャの中心からのずれ |
| | `GripPanelView` / `GripPanelCell` / `GripPanelChip` / `GripPanelCaption` | グリップのパネル（左右 3×3 のセル・見出し・ページ・全半角・大小の札）。子音を担当する手に応じて、左右のどちらを子音側にするかを決める。見出し（`GripPanelCaption`）は手の名前と役割を別の文字に持ち、機能ページの間は役割の色を変える。2つの文字の並びと寄せはシーンの `HorizontalLayoutGroup` で決める |
| | `InputAreaView` | 入力欄（確定・未確定・キャレット）と状態行（通知・送信先・文字数）。キャレットは文字の配置から位置と高さを決め、行が溢れたら文字をずらす。状態行は表示を更新するたびにコードで並べる（通知の札は文字に合わせた幅で、通知が空なら隠す。続けて「送信先」のラベルと名前、右端に文字数）。通知と名前は残りの幅に収まるよう `TextFitting` で切る |
| | `CandidateAreaView` / `CandidateColumn` / `CandidateLabel` | 候補欄（応答待ち・文節と候補・失敗の帯）。文節の列と候補の行はシーンの雛形を複製し、雛形は起動時に非表示にする。文節の並び（`_row`）は左下を基準にし、下端を入力欄の直上にそろえる（候補の数で上端が動く）。シーンでの位置がずらしていないときの位置で、左の余白と同じ幅を右にも空ける。失敗の帯は配置を確定させた後、文節の並びの上に置く。列のカードの地と影は `LayoutElement.ignoreLayout` の子として列の先頭に置く（子は親より後に描かれるため、影を列自身の `Image` にはできない）。見出しと候補の間の区切りは候補を表示している列だけに出す |
| | `TextFitting` | 1行の文字を幅に収め、収まらなければ切って省略記号を付ける（候補欄・失敗の帯・通知・送信先の名前で使う） |
| | `SettingsView` / `SettingsPage` / `SettingsTab` | 設定画面（キャンバスに置く）。`SettingsView` はページを `SettingsCatalog.Categories` と同じ順に持ち、`CreateLayout` でシーンの矩形から `SettingsLayout` を作る。`SettingsPage` は `_key` でカテゴリと対応し、行を子に持つ。選んでいるページだけを `SetActive(true)` にし、ページどうしは同じ場所に重ねる。タブはページを隠しても見えるよう、ページの外（キャンバスの直下）に置く。`SettingsForm.Version` が変わったときだけ更新する。操作は持たない |
| | `SettingsConsentDialog` | 同意が必要な項目の同意の画面（注意事項の文章・同意と取り消しの `SettingsButton`・背景）。`_key` で項目と対応し、`SettingsForm.ConsentItem` がその項目の間だけ表示する。ページの上に描くため、キャンバスの直下でページより後に置く |
| | `SettingsRow` / `SettingsToggleRow` / `SettingsButton` | 数値の項目の行（［−］・スライダー・［＋］・値）と、オン・オフの項目の行（スイッチ・値。行の矩形全体が当たり判定）。「既定に戻す」（`SettingsView._reset`）と「アプリを終了」（`SettingsView._quit`）は `SettingsButton` で、キャンバスの直下に置く。`_key` で `SettingsCatalog` の項目と対応させる |
| | `OverlayRenderTarget` | 描画先の根（MonoBehaviour）。子に無効な正投影のカメラと World Space のキャンバスを持つ。`Initialize` でキャンバスと同じピクセル数の透明な RenderTexture を作り、カメラをキャンバスの正面に合わせる。`Render()` を呼んだときだけ 1回描き、`Rendered` イベントで通知する。テクスチャの破棄（`Release`）はオーバーレイの破棄の後に `KeyboardApp` が呼ぶ |
| | `KeyboardFonts` | 編集用のフォントアセットの元のフォントから、実行用の Dynamic SDF フォントアセット（atlas 2048・multi atlas）を作り、描画先の下のすべての文字のフォントを差し替える（保存しない）。最初の描画より前に差し替えるため、編集用のアセットは書き換わらない |
| | `KeyboardTheme` | 状態によって切り替わる色と文字の大きさ（ScriptableObject。`Assets/VRime/UI/KeyboardTheme.asset`）。状態によらない見た目はシーンに置く |
| | `UiRects` / `SceneChecks` | 左上を原点・下向きを正とする矩形の配置と読み取り（当たり判定と盤面の位置に使う）。シーンの参照の検証（どのオブジェクトのどの欄かを示す例外） |
| | `KeyboardPreview` | エディター専用。メインカメラを使わず OnGUI で、テクスチャ・振動・操作キーを表示する。設定画面の表示中は設定画面のテクスチャを表示し、開発用の環境ではマウスの操作を `SettingsMouseInput` で `ISettingsPointer` へ渡す。ビルドではプレイヤーのウィンドウを隠し、表示するときは設定ウィンドウとして使うため、開発ビルドでも作らない |
| | `DesktopSettingsWindow` | ビルド専用のデスクトップの設定ウィンドウ。`PlayerWindow` でプレイヤーのウィンドウを表示・非表示にし、表示中は OnGUI で設定画面のテクスチャを描いて、`SettingsMouseInput` でマウスの操作を `SettingsForm` へ渡す（ダッシュボードのレーザーと同じ入口）。大きさとタイトルは最初に開いたときだけ決める（`Screen.SetResolution`） |
| | `SettingsMouseInput` | OnGUI のマウスの左ボタンの操作を設定画面のテクスチャの座標に変換し、`ISettingsPointer` へ渡す。ボタンを押していない間の移動は描画のイベントで渡す |
| `Settings/` | `UserSettings` / `KeyboardPlacement` / `VRChatSettings` / `PrivacySettings` / `SettingRange` | 設定画面で変える値（表示位置・2つのしきい値・長押しの時間・VRChat 関連の2つのオン・オフ・変換に Google API を使うか）。不変で、範囲外は例外。`SettingRange` は範囲と刻みを持ち、刻みに丸める。しきい値と長押しの時間の既定値は Core の `KeyboardSettings` の既定値から取る。`ToKeyboardSettings` で Core の設定にする |
| | `SettingsSerializer` / `SettingsStore` / `SettingsLoadResult` | 設定ファイルの JSON（形式は SPEC 8.1）への変換と読み書き。内容が正しくないファイルは別名に移し、既定値と通知を返す。読み書きの IO の失敗は例外のまま上げる。保存は一時ファイルに書いてから置き換える |
| | `SettingsCatalog` / `SettingsCategory` / `SettingItem` | 設定画面のカテゴリ（タブ）と項目の定義。項目は数値（`NumberSettingItem`。範囲・表示の形式）とオン・オフ（`ToggleSettingItem`。オンにする前に同意が必要か `RequiresConsentToEnable`）。カテゴリは、表示中に位置の確認用のキーボードを出すか（`ShowsKeyboardPreview`）を持つ。キーは設定ファイルの項目名と同じ。項目を足すときは、ここに加え、シーンの該当するページに同じキーの `SettingsRow` / `SettingsToggleRow` を置く。カテゴリを足すときは、`SettingsPage` とタブを置き、`SettingsView._pages` に同じ順で加える。同意が必要な項目を足すときは、`SettingsConsentDialog` を置き、`SettingsView._consents` に項目の順で加える |
| | `SettingsLayout` / `SettingsPageLayout` / `SettingsRowLayout` / `SettingsToggleLayout` / `SettingsConsentLayout` / `SettingsHit` | 設定画面の操作の対象の配置（テクスチャのピクセル、左上が原点）と当たり判定。シーンの矩形から作り（`SettingsView.CreateLayout`）、表示と操作（`SettingsForm`）が同じ配置を使う。当たり判定は、タブ・「既定に戻す」・「アプリを終了」に加え、選んでいるページの対象だけを使う。同意の画面を開いている間は、その画面のボタンだけを使う（ページの上に重ねるため、ページの対象と重なってよい）。同時に表示される対象どうしの重なり・画面外・大きさが正でないもの・カテゴリの項目の行の過不足・同意が必要な項目と同意の画面の過不足は例外 |
| | `SettingsForm` / `ISettingsPointer` | 設定画面の状態（値・表示しているカテゴリ・開いている同意の画面・指している対象・つまんでいるスライダー・押しているボタン・通知・版）とポインターの操作。同意が必要なスイッチをオフから押すと、値を変えずに同意の画面を開き（`ConsentItem`）、同意で値をオンにして確定、取り消しで閉じるだけにする。ボタン・スイッチ・タブは押したときと離したときの対象が同じなら実行する。タブは表示を切り替えるだけで値を確定しない。「アプリを終了」は確認せずに `QuitRequested` を出す。値が変わるたびに `Changed`、スライダーを離す・ボタンを実行するなどで値が確定し、前回の確定から変わっていれば `Committed` を出す |
| `Conversion/` | `ConversionRunner` | 変換の要求を `GoogleTransliterateClient` に渡し、結果を `CompleteConversion` / `FailConversion` で返す。通信は同時に 1件で、新しい要求・応答待ちの終了・破棄のときは前の通信を中止する |
| | `GoogleTransliterateClient` | `TransliterateApi` の URL に `UnityWebRequest` で GET し、文節の一覧を返す（`Awaitable`・メインスレッド専用）。制限時間は 2秒で、自前の時計で測る。ログや例外に読みを含めない |
| | `ConversionFailedException` | 失敗の種類（接続・タイムアウト・HTTP エラー・応答の形式）と、入力欄に出す短い日本語（`UserMessage`）を持つ。キャンセルは `OperationCanceledException` |
| `Output/` | `ForegroundWindowMonitor` / `OutputTarget` | 前面のウィンドウを毎フレーム調べ、送信先（なし・本アプリ自身・VRChat・デスクトップ）を保つ。ウィンドウの詳細（`ForegroundWindowInfo`）はハンドルかプロセスが変わったときだけ調べ直す。送信・状態行の表示・文字数の上限・入力中の文字の表示が同じ送信先に従うよう、`KeyboardApp` はフレームの入力の処理の前に `Update` を呼ぶ。取得元は `IForegroundWindowSource` で差し替えられる |
| | `TextOutputRouter` | `OutputTarget` に従って送る。前面なし・自プロセスは送らず、VRChat は `VRChatChatbox`、それ以外は `DesktopTextSender`。結果は `OutputResult`（成否・失敗の種類・失敗の通知文）で返す。成功には通知文がなく、読むと例外 |
| | `VRChatChatbox` | OSC（UDP）の送信。`Send`（送信）と `SetPreview`（入力中の文字。144文字を超える分は切る）は `ChatboxOutbox` に予約するだけで、毎フレームの `Update(now)` で間隔を守って送る。`ClearPreviewNow` は終了前に間隔を待たずに入力中の文字を消す。`SetTyping` は値が変わったときだけ送る。接続しないソケットを使う（接続済みだと宛先未起動の ICMP が次の送信の例外になる） |
| | `DesktopTextSender` | クリップボードに文字列を置き、`SendInput` で Ctrl+V（設定により続けて Enter）を送る。送信前に `ProcessElevation` で対象の昇格状態を調べ、届かない相手には送らない。元の内容は退避し、毎フレームの `Update` で戻す時刻・書き換えの有無を `ClipboardRestoreSchedule` に問い合わせて戻す。終了前に `RestoreNow` で時刻を待たずに戻す |
| | `ClipboardRestoreSchedule` | 退避した内容を戻す時刻と、戻すかどうかの判定（Win32 に依存しない）。書き換えの検出は、送信で書き込んだ直後のクリップボードの通番との比較。続けて送るときは、通番が変わっていなければ退避を引き継ぐ |
| | `ClipboardAccess` / `ClipboardSnapshot` | クリップボードの開閉・送信用の文字列の書き込み・履歴に残さない印の付与と、内容の全形式の退避・復元。所有者なしで開くと空にした後の `SetClipboardData` が失敗するため、メッセージ専用のウィンドウを所有者にする |
| | `ProcessElevation` | トークンの昇格状態の照会 |
| | `ForegroundWindowInfo` | 前面ウィンドウのハンドル・プロセス ID・プロセス名・表示名・タイトル。タイトルは内容を含みうるため、ログに出さない |
| | `ProcessImage` | プロセスの実行ファイルのパスと名前を、制限付きの照会権限（`PROCESS_QUERY_LIMITED_INFORMATION`）で調べる。Mono の `System.Diagnostics.Process` は VRChat（保護されたプロセス）の名前を読めないため、プロセス名はすべてこれで調べる |
| | `VRChatWindow` | 所有者のない表示中のトップレベルウィンドウを列挙し、プロセス名が VRChat のもの（判定は `OutputTarget.IsVRChatProcess`）を前面にする。Windows が前面の切り替えを拒んだときは、Alt キーを押している間に切り替えを試す（Alt の押下中は切り替えが許されるため） |
| | `AppSettings` | App 層の設定値（通知音・Enter・OSC の宛先）。不正な値は例外。設定画面と設定ファイルにはまだ含めていない |
| `Native/` | `Win32` | Win32 API の宣言（`INPUT` は x64 の 40 バイト、`NOTIFYICONDATA` は 976 バイト前提） |
| | `PlayerWindow` | Unity のプレイヤーのウィンドウ（自プロセスのクラス名 `UnityWndClass` のトップレベルウィンドウ）を隠す（`SW_HIDE`）・表示して前面にする・タイトルを変える。ちょうど 1 つ見つからない、隠した後も表示されている、表示した後も表示されていない場合は例外。エディターでは呼ばない |
| | `TrayIcon` / `TrayCommand` | タスクトレイのアイコンとメニュー。専用のスレッドで、表示しないトップレベルのウィンドウとメッセージループを持つ（メニューは閉じるまでスレッドを止めるため、メインスレッドでは出さない。メッセージ専用のウィンドウには `TaskbarCreated` が届かないため、トップレベルにする）。選ばれた操作とスレッドで起きた失敗はキューに積み、メインスレッドの `TryTakeCommand` が取り出す（失敗は 1 件ずつ例外にする）。終了は独自のメッセージで依頼し、外部からの `WM_CLOSE` では閉じない。メニューを開いている間に終了を依頼されたら `EndMenu` で閉じてから破棄する。ウィンドウのクラスを登録するため、プロセスに同時に 1 つだけ |

- 出力の `TextOutputRouter` は送信先・チャットボックス・デスクトップ送信を、`ForegroundWindowMonitor` はウィンドウの取得元を差し替えられるため、振り分けと監視の規則は Win32 なしでテストできる。クリップボードの復元の規則も `ClipboardRestoreSchedule` に分けてテストする（実際のクリップボードはテストで触らない）。

**描画の流れと構成**

- 描画は `KeyboardApp.Update` が `KeyboardView.RefreshIfChanged()` で `ViewVersion` の変化を検出したときだけ `OverlayRenderTarget.Render()` を呼ぶ。キーボードは開いている間と位置の確認用に表示している間（設定タブか設定ウィンドウで「表示位置」のカテゴリを選んでいる間）だけ描き、表示し始めたフレームでは版が変わっていなくても描く。設定画面は `SettingsView.RefreshIfChanged()` が true のときに描く。
- `Render()` は `Canvas.ForceUpdateCanvases()` でレイアウトと TMP のメッシュを確定してから、`RenderPipeline.SubmitRenderRequest`（URP の `SingleCameraRequest`）で無効なカメラを 1回描く。`Camera.Render()` と違い、出力先を要求に含められ、対応していないパイプラインでは例外にできる。
- 描画先ごとのレイヤー `VRimeKeyboard`（キーボード）と `VRimeSettings`（設定画面）を Tags and Layers に登録している。各カメラは自分のレイヤーだけを映す（同じ位置にある別の描画先の uGUI を映さないため）。TMP の `Mobile/Distance Field` シェーダーは Always Included Shaders に登録している（ビルドで除外されないため）。
- TMP の `Ellipsis` は、レイアウトで幅が決まる文字では描かれなくなるため、幅に収める文字は `TextFitting` で切って省略記号を付ける。
- シーン `Assets/VRime/Scenes/Main.unity` を Build Settings の先頭に登録している。

**シーンの uGUI**

- `Main.unity` は `KeyboardApp` と、2つの描画先 `KeyboardOverlay`（キャンバス 1024×1024）・`SettingsOverlay`（1280×872）で構成する。キャンバスの大きさがテクスチャのピクセル数になり、拡大率は 1 のままにする。2つの描画先は互いに離して置く（エディターで重ならないようにするため。描画はレイヤーで分かれる）。
- 描画先の下のオブジェクトはすべて描画先のレイヤーに置く。部品を足すときもレイヤーを合わせる（違えば起動時の検証で例外）。
- 配置・大きさ・固定の色・項目名などのラベルはシーンで調整する。状態で変わる色と大きさは `KeyboardTheme`、文字の内容は各ビューが書き換える。シーンの文字には仮の文字を入れてある。
- キーボードの角丸と影は `Assets/VRime/UI/Sprites/` の 9 スライスのスプライト（自作）を `Image`（Sliced）に使う。スプライトは白（影も白）で作り、色は `Image.color` で付ける（状態で変わる色は `KeyboardTheme` から入る）。Pixels Per Unit 100・Full Rect で、キャンバスの `referencePixelsPerUnit` も 100 のため、スプライトの 1px がテクスチャの 1px になる。`ui_round_32`（盤面）・`ui_ring_32`（盤面の縁）・`ui_round_20`（入力欄・候補のカード）・`ui_round_16`（セル・札の地）・`ui_round_12`（札・見出し・候補・帯・点灯の帯）・`ui_shadow_20`（浮いた部品の影。矩形は部品より各辺 24 大きく、下へ 6 ずらす）。境界より低い矩形（点灯の帯の高さ 6）では Unity が境界を縮めるため、両端が半円になる。
- プロジェクトの色空間は Linear のため、半透明の色は線形空間で混ざる。不透明度の低い明るい色はガンマ空間で混ぜる場合（デザインの CSS）より大きく明るくなる（白 6% の縁で約 2 倍）。下地が決まっている部品（盤面の縁・通知の札・候補の区切り）は、混ぜた後の色を不透明で指定する。
- 文字のフォントは2つの編集用のフォントアセットのどちらかにする。リッチテキストは入力欄の文字だけが使う。
- `KeyboardApp.ValidateScene()` は EditMode テスト（`MainSceneTests`）でも実行する。シーンを変えたらテストで確かめる。

**入力元の選択と開閉の流れ**

- 環境の選択は `KeyboardApp.CreatePlatform` だけで行う。ビルドは常に `OpenVRPlatform`。エディターは `_editorDevice`（`DebugKeyboard` が既定 / `OpenVR`）に従う。OpenVR の初期化に失敗したときは例外にし、`Awake` で `enabled = false` にして毎フレームの例外を防ぐ（別の環境へ切り替えない）。
- 開閉は Core の `IKeyboardEffects.OnOpenChanged` → `KeyboardApp` → `IKeyboardPlatform.OnOpenChanged` と流れ、OpenVR では `OpenVRControllerSource.IsExclusive` とオーバーレイの `Show` / `Hide` が切り替わる（位置の確認用に表示している間は閉じていてもオーバーレイを表示したままにする）。ダッシュボードの状態は毎フレーム `IsDashboardHoldingInput` を `KeyboardSession.SetDashboardVisible` へ渡し、手動で閉じるのと同じ経路で閉じる（SPEC 4.4）。
- 毎フレームの順序は `BeginFrame`（イベント処理と設定タブの操作。終了を求められたら以降の API を呼ばない）→ `ForegroundWindowMonitor.Update` と `SetLengthLimit` → `SetDashboardVisible` → `Read` → `Tick` → 描画（`Rendered` でテクスチャをオーバーレイへ渡す）。
- エディターで再生を繰り返しても接続できるよう、終了時はオーバーレイを破棄してから `VR_Shutdown` を呼ぶ。`OpenVR.Init` が管理側のインターフェースを作り直すため、次の再生で再初期化できる。ハンドルとマニフェストは接続ごとに取得・登録し直す。

**設定の流れ**

- 起動時に `SettingsStore.Load` で `%AppData%\VRime\settings.json` を読み、`UserSettings` から Core の `KeyboardSettings` とオーバーレイの最初の位置を作る。
- 設定タブのレーザー操作は `SettingsDashboardOverlay.Update`（`BeginFrame` の中）→ `SettingsForm` と流れる。ビルドでは `DesktopSettingsWindow`、エディターの開発用の環境では `KeyboardPreview` のマウス操作が同じ `ISettingsPointer` に入る（どちらも `OnGUI` の中）。
- `SettingsForm.Changed` で `KeyboardSession.ApplySettings` と `IKeyboardPlatform.SetPlacement` を呼び（入力中の文字の表示は毎フレーム `SettingsForm.Settings` を読む）、`Committed` で保存する。保存の失敗（`IOException` / `UnauthorizedAccessException`）は `SettingsForm.SetNotice` で画面に出し、次に保存できたら読み込み時の通知に戻す。

**ビルド**

- Windows 64bit の出力先はプロジェクト直下の `Builds/` で、`.gitignore` により管理対象外。`openvr_api.dll` が `<名前>_Data/Plugins/x86_64/` に、アクションマニフェストが `<名前>_Data/StreamingAssets/SteamVR/` に入る。
