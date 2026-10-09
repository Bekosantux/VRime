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
| | `IKeyboardEffects` | Core から App への通知（開閉・振動・変換の要求・送信の要求）。Core は結果をここへ出すだけで、通信や描画は知らない |
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
