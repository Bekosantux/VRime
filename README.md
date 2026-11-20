![Banner](Images/Banners/VRime_Banner_White.png)

# VRime

VRime（ブイライム）は、SteamVR 上で動作する日本語入力用のキーボードオーバーレイアプリです。VR 内で、HMD を外さずに日本語の文章を入力・変換・送信できます。主に VRChat のテキストチャットでの利用を想定していますが、その他のVRやデスクトップアプリへの入力にも対応します。

現在は開発中で、配布用のビルドはまだ公開していません。

## 主な機能

- グリップ入力: レーザーポインター、ジャイロを使わない独自の入力方式です。フリック入力の配置を踏襲しており、子音パネルのスティックで 3×3 のセルから1つを選び、母音パネルのスティックの方向で文字を選びます。グリップボタンを握っている間は、パネルが機能ページに切り替わります。
- 開閉: 両手のグリップを2回続けて同時押しすると開き、開いているときに同じ操作で閉じます。
- 変換: Google CGI API for Japanese Input で変換し、キーボードの上に開く候補欄から候補を選びます。
- 送信: フォーカス中のウィンドウが送信先になります。VRChat には OSC で直接チャットボックスに送り、それ以外のデスクトップのアプリには、文字列をクリップボードに一時的にコピーして Ctrl+V で貼り付けます。
- 設定: SteamVR ダッシュボードにこのアプリのタブを追加し、そこで設定します。システムトレイのアイコンからは、デスクトップの設定ウィンドウを開けます。
- 自動起動: SteamVR にアプリとして登録し、SteamVR の起動と同時に起動できます（設定画面で切り替えます。既定はオフ）。

## 動作環境

| 項目 | 内容 |
|---|---|
| OS | Windows 10 / 11 |
| VR ランタイム | SteamVR |
| コントローラー | 既定のバインドを同梱: Valve Index、Meta Quest（Link / Air Link / Virtual Desktop 等）、HTC Vive ワンド（トラックパッドをスティックとして使用）、Pico、HTC Vive Focus 3、Steam Frame。そのほかのコントローラーは、SteamVR のバインド設定画面で割り当てれば使えるはずです |
| ネットワーク | 変換に必要です |

## 外部サービスの利用

変換には Google CGI API for Japanese Input を使用し、「変換」を押したときに入力中の読み（ひらがな）を Google に送信します。変換は、設定画面で注意事項に同意してオンにしたときだけ使えます。

## 開発の始め方

1. Unity 6000.3.19f1 でプロジェクトを開きます。
2. シーン `Assets/VRime/Scenes/Main.unity` を開いて再生します。既定では、PC のキーボードで両手のコントローラーを模すため、VR 機器は不要です。操作キーは Game ビューに表示されます。SteamVR に接続して確かめるときは、`KeyboardApp` の `Editor Device` を `OpenVR` にします。
3. テストは Test Runner の EditMode で実行します。
4. ビルドはメニューの `VRime/Build/Development` または `VRime/Build/Release` で行います（出力先は `Builds/`）。

設計は [ARCHITECTURE.md](ARCHITECTURE.md)、作業の手順は [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md)、仕様は [docs/SPEC.md](docs/SPEC.md)、未実装の機能は [docs/ROADMAP.md](docs/ROADMAP.md) を参照してください。

## ライセンス

このリポジトリは [PolyForm Strict License 1.0.0](LICENSE) で公開しています。非商用の目的での利用は許可しますが、改変と再配布は許可しません。

同梱するサードパーティのコンポーネントには、それぞれのライセンスが適用されます。

| コンポーネント | ライセンス | ライセンス文 |
|---|---|---|
| OpenVR（Valve Corporation） | BSD 3条項ライセンス | [Assets/VRime/ThirdParty/OpenVR/LICENSE](Assets/VRime/ThirdParty/OpenVR/LICENSE) |
| Noto Sans JP | SIL Open Font License 1.1 | [Assets/VRime/Fonts/OFL.txt](Assets/VRime/Fonts/OFL.txt) |