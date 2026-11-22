# アーキテクチャ

複数のクラスにまたがる構造と、設計上の判断・制約を扱う。クラスごとの責務は各クラスのドキュメントコメントに書く。外部仕様は [docs/SPEC.md](docs/SPEC.md)、作業の手順と規則は [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) を参照。

## 開発環境

| 項目 | 内容 |
|---|---|
| Unity | 6000.3.19f1（Unity 6.3 LTS） |
| レンダリングパイプライン | URP。Render Graph で描画する。URP Global Settings の互換モード（Compatibility Mode）はオフ（Unity 6.3 では、オンのままだとビルドが失敗する） |
| UI | uGUI ＋ TextMeshPro（Noto Sans JP, Dynamic SDF） |
| C# | 9 |
| 追加のパッケージ | `com.unity.nuget.newtonsoft-json`: Google API の応答は入れ子の配列であるため、`JsonUtility` では読めない |

- **XR 関連のパッケージ（XR Plug-in Management、OpenVR XR Plugin、SteamVR Unity Plugin）は導入禁止。** 導入すると Unity がシーンアプリとして起動し、VRChat の描画を奪う。Valve 配布の `openvr_api.cs` を直接使い、`VRApplication_Overlay` で初期化する。

### Player Settings

| 項目 | 設定 | 理由 |
|---|---|---|
| Graphics APIs | Direct3D11 のみ | `GetNativeTexturePtr` を `ETextureType.DirectX` でそのまま `SetOverlayTexture` に渡すため |
| Color Space | Linear（URP 既定） | オーバーレイには RenderTexture を sRGB のバイト列として渡す（[描画](#描画)） |
| Run In Background | オン | ウィンドウを隠しても動く |
| フレームレート | `KeyboardApp` が `vSyncCount = 0`・`targetFrameRate = 90` にする | 隠したウィンドウがモニターの VSync に同期せず、スティック入力を取り逃さないようにする |
| スクリプトバックエンド | Mono。Managed Stripping Level は Medium | [DEVELOPMENT.md](docs/DEVELOPMENT.md#ビルド) を参照 |
| スプラッシュ画面 | オフ | ウィンドウを隠すまでの時間を短くする |
| 解像度 | ウィンドウモード・640×480 | 起動処理中にウィンドウが画面を覆わないようにする |
| Default Icon | `Assets/VRime/Icon/vrime-icon-1024.png` | exe のアイコン。トレイとダッシュボードのアイコンは StreamingAssets のファイルを使用 |

## アセンブリと依存の方向

```
VRime.Editor  ──→  VRime.App  ──→  VRime.Core
                         │
                         └──────────→  Valve.OpenVR
```

| アセンブリ | 場所 | 責務 |
|---|---|---|
| `VRime.Core` | `Assets/VRime/Core/` | Unity・OpenVR・Win32 に依存しない純粋なロジック（`noEngineReferences`）。入力方式の状態機械、入力バッファ、変換候補の選択状態、OSC エンコードなど |
| `VRime.App` | `Assets/VRime/App/` | MonoBehaviour、OpenVR、描画、通信、Win32 の呼び出し。Core の状態機械に入力を渡し、その結果を各部へ振り分ける。asmdef は Editor と Windows 64bit に限定する |
| `Valve.OpenVR` | `Assets/VRime/ThirdParty/OpenVR/` | Valve 配布の `openvr_api.cs` をそのまま置く |
| `VRime.Editor` | `Assets/VRime/Editor/` | エディター専用の開発用ツールとビルド |

- Core は App を参照しない。Core から外部への作用（開閉・振動・変換の要求・送信と表示の消去の要求）は、`IKeyboardEffects` で App に通知
- Core は時刻・スティックの傾き・押下状態をすべて引数で受け取り、時計や入力の API に依存しない。そのため、テストでは時間の経過と操作の列を固定の値で再現できる
- App は Win32 や OpenVR を呼び出す処理を境界（`IKeyboardPlatform`・`IControllerSource`・`IForegroundWindowSource` など）の背後に隠し、振り分けや判定の規則を、境界を差し替えてテストできるようにする

## ディレクトリ

`Assets/VRime/` の下の構成。

| 場所 | 内容 |
|---|---|
| `Core/` | `KeyboardSession`（全体の状態機械）・`IKeyboardEffects`（App への通知）・`KeyboardSettings`（動作設定） |
| `Core/Input/` | 1フレーム分の両手の状態・押下エッジ・長押しの時刻管理 |
| `Core/Control/` | 開閉の同時押しの検出 |
| `Core/Grip/` | グリップ入力の状態機械・文字の配置の表・スティックの分類・描画用の状態 |
| `Core/Text/` | 入力バッファ・直前の文字の切り替え・全角化・カタカナ |
| `Core/Conversion/` | Google API の URL の組み立てと応答の解析・候補の選択状態・変換中の操作 |
| `Core/Output/` | OSC のエンコード・チャットボックスへ送る順序と間隔 |
| `App/` | `KeyboardApp`（コンポジションルート）・`AppInfo`（表示名・データの保存先） |
| `App/Platform/` | 実行環境の境界 `IKeyboardPlatform` と、PC のキーボードによる開発用の環境 |
| `App/Input/` | 入力元・振動の出力先の境界と、開発用の実装 |
| `App/SteamVR/` | OpenVR の環境・接続・アプリの登録・アクション・オーバーレイ・テクスチャの受け渡し。名前空間と場所を `OpenVR` にしないのは、`Valve.VR.OpenVR` 型を隠さないため |
| `App/View/` | 描画先・キーボードと設定画面のビュー・色のテーマ・デスクトップの設定ウィンドウ・エディターの確認表示 |
| `App/Settings/` | 設定値・設定ファイル・設定画面の項目の定義・状態と当たり判定 |
| `App/Conversion/` | 変換の通信 |
| `App/Output/` | 前面のウィンドウの監視・送信の振り分け・OSC の送信・クリップボード |
| `App/Native/` | Win32 の宣言・メッセージボックス・プレイヤーのウィンドウ・多重起動の防止・タスクトレイ |
| `Editor/` | テーマのシーンへの反映（`KeyboardThemeEditor`・`ThemeScenePainter`）、`Build/` にビルド |
| `Tests/EditMode/` | Core のテスト（`VRime.Core.Tests`） |
| `Tests/EditModeApp/` | App のテスト（`VRime.App.Tests`） |
| `UI/` | プレハブ・スプライト・テーマのアセット |
| `Scenes/Main.unity` | 唯一のシーン。`KeyboardApp` と、2つの描画先（キーボード・設定画面）を置く |

## 外部由来のアセット

| 場所 | 内容 | 注意 |
|---|---|---|
| `Assets/VRime/ThirdParty/OpenVR/` | Valve 配布の `openvr_api.cs`・`LICENSE`、`Plugins/x86_64/openvr_api.dll` | Valve 配布のまま変更しない。asmdef は Editor と Windows 64bit に限定し、自動参照しない（`VRime.App` から明示参照する） |
| `Assets/StreamingAssets/SteamVR/` | SteamVR アクションマニフェスト（`actions.json`）とコントローラー別の既定バインディング（`bindings_*.json`）、ダッシュボードのタブのアイコン（`dashboard_icon.png`。256px） | ビルド後も実ファイルとして配置される必要があるため StreamingAssets に置く。SteamVR にはこのファイルのパスを渡す。既定のバインドは、グリップを展開・収納とパネルの切り替えの 2つのエントリーに割り当てる（1つのエントリーは 1つのアクションにしか出力できないため） |
| `Assets/StreamingAssets/Windows/tray_icon.ico` | タスクトレイのアイコン。16・20・24・32・40・48px（`SM_CXSMICON` の表示倍率 100〜300% の大きさ） | Win32 の `LoadImage` にファイルのパスを渡すため StreamingAssets に置く（小さいアイコンの大きさに合うものが選ばれる）。16〜32px は線を太くして再作成したもので、SVG を縮小したものとは異なる |
| `Assets/VRime/Icon/vrime-icon-1024.png` | exe のアイコン（Player Settings の Default Icon） | Unity が各サイズに縮小する |
| `Images/Icons/` | アイコンの原版（`vrime-icon.svg`）と 1024px の PNG | 上の 3つのアイコンはここから作る。色や形を変えたら 3つとも再作成する |
| `Assets/VRime/Fonts/` | Noto Sans JP（Regular・Bold の静的 OTF）と `OFL.txt`、編集用の TMP フォントアセット（`NotoSansJP-* SDF.asset`） | ライセンス文は再配布に必要なため同梱する。OTF は Include Font Data を有効にしておく（実行用のフォントアセットの元になる）。編集用のフォントアセットは Dynamic・atlas 512・Clear Dynamic Data On Build。エディターでシーンを開くと表示した文字が atlas に追加され、アセットに差分が出る（Play 中は実行用のアセットに差し替えるため出ない） |

## Core の構造

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

- `KeyboardSession` が `KeyboardMode`（`Input` / `WaitingConversion` / `Converting`）を持ち、入力中は `GripInputMethod`、変換中は `ConversionController` に操作を振り分ける。
- 変換・送信のようにバッファだけでは完結しない操作は、`GripInputMethod` が要求（`GripRequest`）として返し、`KeyboardSession` が `IKeyboardEffects` で App に依頼する。変換の結果は、リクエスト ID を付けて `CompleteConversion` / `FailConversion` で戻る
- 表示用の状態は、`KeyboardSession.ViewVersion` と各クラスの `Version` で変化を検出できる。App は変化したときだけ再描画する。描画用の状態（`GripViewState`）は Core が作り、App はそれを描画するだけ

## 起動・毎フレーム・終了

`KeyboardApp` がコンポジションルートの MonoBehaviour で、部品を組み立てて毎フレームの処理を順に呼ぶ。`IKeyboardEffects` を実装し、Core からの通知を各部へ振り分ける。

### 実行環境の選択

- 環境の選択は `KeyboardApp.CreatePlatform` だけで行う。ビルドは常に `OpenVRPlatform`。エディターは `_editorDevice`（`DebugKeyboard` が既定 / `OpenVR`）に従う
- OpenVR の初期化に失敗したときは例外にし、別の環境へは切り替えない
- 入力元・振動・オーバーレイの表示・終了通知は `IKeyboardPlatform` に任せ、`KeyboardApp` に OpenVR の分岐を持たせない

### 起動

- `Awake` と `Start` は `RunStartupStep` を通し、例外で終われば `enabled = false` にする（毎フレームの例外を防ぐ）。ビルドではその例外をログに残して `ErrorDialog` で示し、閉じたら終了する。エディターでは例外をそのまま送出する
- `Awake` の順序:
  1. ビルドでは `SingleInstanceLock` を取得する。取得できなければ（起動済みのプロセスがあれば）何も作らずに終了する。
  2. 設定を読み（`SettingsStore.Load`）、Core の設定・オーバーレイの最初の位置・表示のテーマ（`ActiveTheme`）を作る。
  3. シーンの構成を `ValidateScene()` で検証し（誤りは場所を示す例外）、環境・ビュー・出力などの部品を組み立てる。
  4. ビルドでは `TrayIcon` と `DesktopSettingsWindow` を作る。ウィンドウを隠した後も設定を開けて終了できるよう、トレイのアイコンは隠す前に置く。
- `Start`: 設定に従って `VRChatWindow.Focus` を呼び、ビルドでは `PlayerWindow.Hide` でウィンドウを隠す。
  - Unity は最初のシーンの `Awake` の後、`Start` の前にウィンドウを表示するため、`Awake` で隠しても再表示される。
  - 前面を持っているうちに VRChat へ切り替えるため、隠すのは `Focus` より後に行う。

### 毎フレーム

`KeyboardApp.Update` は次の順で処理する。送信・状態行の表示・文字数の上限・入力中の文字の表示が同じ送信先に従うよう、前面のウィンドウの監視は入力の処理より前に行う。

1. `IKeyboardPlatform.BeginFrame()`: 環境のイベントの処理と、設定タブの操作。終了を求められたら、以降の API を呼ばない。
2. `TrayIcon.TryTakeCommand`: トレイの操作。
3. `ForegroundWindowMonitor.Update(KeyboardSession.IsPasteMode)`・`KeyboardSession.SetPasteModeAvailable`・`KeyboardSession.SetLengthLimit`: 送信先、「貼付」キーを使えるか、文字数の上限。「貼付」キーによる貼り付けモードの切り替えは、次のフレームの送信先に反映する。
4. `KeyboardSession.SetDashboardVisible(IsDashboardHoldingInput)`: ダッシュボードによる一時停止。
5. `IControllerSource.Read` → `KeyboardSession.Tick`。
6. 変換の中止の判定。
7. `SetPlacementPreviewVisible`: 設定タブかデスクトップの設定ウィンドウの表示中で、選んでいるカテゴリが `ShowsKeyboardPreview` のとき、位置の確認用にキーボードを表示する。
8. 表示の更新（[描画](#描画)）。
9. `/chatbox/typing` と入力中の文字の表示。どちらも送信先が VRChat（チャットボックス）の間だけ送り、送信先が変われば（貼り付けモードへの切り替えを含む）次のフレームで取り下げる。
10. チャットボックスの送信（`VRChatChatbox.Update`）。送信は失敗したときだけ通知を入力欄に出す。

- 値を SteamVR が持つ設定項目（自動で起動するか）は、環境を作った直後と設定画面の表示中の毎フレームに `IKeyboardPlatform.AutoLaunch` を読んで `SettingsForm.SetExternalValue` で渡す。`SettingsForm.ExternalChangeRequested` を受けたら `SetAutoLaunch` で変えてから読み直す。

### 開閉

- 開閉は Core の `IKeyboardEffects.OnOpenChanged` → `KeyboardApp` → `IKeyboardPlatform.OnOpenChanged` と流れ、OpenVR では `OpenVRControllerSource.IsExclusive` とオーバーレイの `Show` / `Hide` が切り替わる。位置の確認用に表示している間は、閉じていてもオーバーレイを表示したままにする。
- ダッシュボードによる一時停止は、手動で閉じるのと同じ経路で閉じる（[入力の横取り](#入力の横取り)の制約を守るため）。

### 終了

- 終了は `IKeyboardPlatform.QuitRequested`・`SettingsForm.QuitRequested`・`TrayCommand.Quit` で受ける。終了時はチャットボックスの入力中の文字を消し（`VRChatChatbox.ClearPreviewNow`）、クリップボードを戻す（`DesktopTextSender.RestoreNow`）。
- `Application.wantsToQuit` で、設定ウィンドウの表示中にウィンドウを閉じる操作を取り消し、設定ウィンドウを隠す（`_quitting` が立っていれば通す）。
- オーバーレイを破棄してから `VR_Shutdown` を呼び、その後に描画先のテクスチャを解放する（`OverlayRenderTarget.Release`）。`OpenVR.Init` が管理側のインターフェースを再作成するため、エディターで再生を繰り返しても再初期化できる。ハンドルとマニフェストは接続ごとに再取得・再登録する。

## 入力の横取り

キーボードを開いている間にゲームへコントローラー入力を渡さないための制御。次の制約は、守らないと実機で入力が壊れる。

- アクションセットは **1つだけ** 使う。閉じている間は優先度 `0`（ゲームと入力を共有し、開閉の操作だけを監視する）、開いている間は `k_nActionSetOverlayGlobalPriorityMin`（ゲームへの入力を止める）。アクションセットはアプリ側で固定のため、利用者がバインドを変えてもこの制約は守られる。
- **同じボタンを、優先度の違う2つのアクションセットに割り当てない。** 低い方が「押されたまま」の古い値を返し続けた。
- **開いたまま優先度だけを下げない**（`OpenVRControllerSource.IsExclusive` を開いている間に false にしない）。再起動するまで入力が取得できなくなった。そのため、ダッシュボードによる一時停止も手動で閉じるのと同じ経路で閉じる。
- ダッシュボードが閉じてから入力がアプリへ戻るのは、数フレーム後（`IVRSystem::IsInputAvailable` が true に戻ったとき）になる。それより前に優先度を上げると優先度が有効にならず、開き直すまでゲームにも入力が渡り続けた。そのため `OpenVRPlatform.IsDashboardHoldingInput` は、入力が戻るまで true を返し続ける。

## 描画

- キーボードと設定画面は、それぞれの描画先（`OverlayRenderTarget`）で、uGUI を専用のカメラで RenderTexture に描画し、OpenVR のオーバーレイに渡す。
- 描画するのは表示が変わったときだけ。`KeyboardView.RefreshIfChanged()` が `ViewVersion` などの変化を検出したときに `OverlayRenderTarget.Render()` を呼ぶ。キーボードは開いている間と位置の確認用に表示している間だけ描画し、表示し始めたフレームではバージョンが変わっていなくても描画する。設定画面は `SettingsView.RefreshIfChanged()` が true のときに描画する。
- `Render()` は `Canvas.ForceUpdateCanvases()` でレイアウトと TMP のメッシュを確定してから、`RenderPipeline.SubmitRenderRequest`（URP の `SingleCameraRequest`）で、常に無効にしてあるカメラを 1回描画する。`Camera.Render()` と違い、出力先を要求に含められ、対応していないパイプラインでは例外にできる。
- 背景を透明にするため、カメラは Solid Color (0,0,0,0) でクリアし、ポストプロセス・HDR・MSAA を無効にする。
- 描画先ごとの Unity のレイヤー `VRimeKeyboard`（キーボード）と `VRimeSettings`（設定画面）を Tags and Layers に登録し、各カメラは自分のレイヤーだけを描画する（同じ位置にある別の描画先の uGUI を描画しないため）。
- TMP の `Mobile/Distance Field` シェーダーを Always Included Shaders に登録している（ビルドで除外されないため）。
- 文字は起動時に、実行用の Dynamic SDF フォントアセットへ差し替える（`KeyboardFonts`）。最初の描画より前に差し替えるため、編集用のアセットは書き換わらない。
- TMP の `Ellipsis` は、レイアウトで幅が決まる文字では描画されなくなるため、幅に収める文字は `TextFitting` で切り詰めて省略記号を付ける。

### オーバーレイへの受け渡し

- `SetOverlayTexture` は Unity のレンダースレッドで呼ぶ（`OverlayTextureSubmitter`）。呼び出したスレッドで D3D11 のイミディエイトコンテキストを使ってコピーするため、メインスレッドから呼ぶとレンダースレッドとの同時使用になり、GPU ドライバー内でクラッシュした。
- D3D11 の RenderTexture は上下が逆になるため、`SetOverlayTextureBounds` で `vMin=1, vMax=0` を指定する（`OverlayTextures`）。ダッシュボードのマウスの座標もこの範囲を通して求められるため、上が 0・下向きが正で届き、そのまま設定画面の座標になる。
- 色空間は Gamma を指定する。リニア色空間のプロジェクトの RenderTexture は sRGB 形式で、バイト列が符号化済みのため。
- キーボードの位置の基準はボードの中心で、テクスチャの中心とのずれはシーンのボードの配置から求める（`KeyboardView.BoardCenterOffset` → `OverlayPlacement`）。

## 色のテーマ

- 色はすべてテーマのアセット（`KeyboardTheme`。一覧は `ThemeCatalog`）から入る。シーンに保存した色は編集用の見本で、描画するときに上書きされる。
- 状態で変わる色（選択・押下・無効など）はビューが設定し、状態によらない色は uGUI に付けた `ThemeColor` が役割（`ThemeColorRole`）に従って設定する。
- ビューは表示中のテーマ（`ActiveTheme`）を共有し、`Version` が変われば再描画する。`KeyboardApp` は設定のテーマが変わったときに差し替える。

## 設定

- 設定画面の状態と操作は `SettingsForm` が持ち、表示（`SettingsView`）は状態を描画するだけにする。操作の対象の配置（`SettingsLayout`）はシーンの uGUI の矩形から作り、表示と当たり判定が同じ配置を使う。
- ポインターの入力は 3つの入口から同じ `ISettingsPointer` に入る。
  - ダッシュボードのタブのレーザー: `SettingsDashboardOverlay.Update`（`BeginFrame` の中）
  - デスクトップの設定ウィンドウのマウス（ビルド）: `DesktopSettingsWindow`（`OnGUI` の中）
  - エディターの確認表示のマウス（開発用の環境）: `KeyboardPreview`（`OnGUI` の中）
- `SettingsForm.Changed` で `KeyboardSession.ApplySettings` と `IKeyboardPlatform.SetPlacement` を呼び（入力中の文字の表示は毎フレーム `SettingsForm.Settings` を読む）、`Committed` で保存する。
- 保存の失敗（`IOException` / `UnauthorizedAccessException`）は `SettingsForm.SetNotice` で画面に出し、次に保存できたら読み込み時の通知に戻す。
- データの保存先（`%AppData%\VRime`）は `AppInfo.DataDirectory` で揃える。
