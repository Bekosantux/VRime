![Banner](Images/Banners/VRime_Banner_White.png)

# VRime

VRime（ブイライム）は、SteamVR 上で動作する日本語入力用のキーボードオーバーレイアプリです。VR 内で、HMD を外さずに日本語の文章を入力・変換・送信できます。主に VRChat のテキストチャットでの利用を想定していますが、その他のVRやデスクトップアプリへの入力にも対応します。

## 主な機能

- グリップ入力: レーザーポインター、ジャイロを使わない独自の入力方式です。フリック入力の配置を踏襲しています。キーボードは母音と子音で左右に分かれており、それぞれのスティックの方向で決定します。3x3で足りない文字はグリップを押すことで現れます。
- 変換: Google CGI API for Japanese Input で変換します。設定画面での同意と有効化が必要です。
- 送信: フォーカス中のウィンドウが送信先になります。VRChat 向けに設計していますが、それ以外の VR アプリやデスクトップアプリにも対応しています。
- 自動起動: SteamVR と連動して起動できます。

<div><video controls src="https://github.com/user-attachments/assets/efb563c1-0170-41b5-9b82-f33b9ddfe80d" muted="false"></video></div>
練習中です…習熟度次第でもっと早くなると思います

## 使い方
1. SteamVR を起動後、`VRime.exe` を起動します。
2. 両手のグリップボタンを同時に2度押すとボードが開きます。（同じ操作で閉じる）
3. 左右のスティックで選択し、トリガーで文字を確定します。グリップを押すと「機能パネル」に切り替わり、足りない文字が選択できるようになります。
4. 母音ボード側機能パネルの「確定」または「変換」を選択します。

### おすすめ設定
- 「外部連携」から「SteamVRと同時に起動」を有効化
- 「個人情報」から「Google API」を有効化

詳細なガイドは、[ドキュメントを参照してください。](https://bekosantux.github.io/shop-document/vrime/how-to-use)

## 動作環境

| 項目 | 内容 |
|---|---|
| OS | Windows 10 / 11 |
| VR ランタイム | SteamVR |
| コントローラー | 動作確認済み: Meta Quest（Air Link / Steam Link / Virtual Desktop）。<br>未確認: Valve Index、HTC Vive ワンド、Pico、HTC Vive Focus 3、Steam Frame。既定のバインドは同梱していますが、実機での動作は確認していません。<br>詳細は [docs/SPEC.md「動作環境」](docs/SPEC.md#動作環境) を参照してください |
| ネットワーク | 変換に必要です |

## 外部サービスの利用

変換には Google CGI API for Japanese Input を使用し、「変換」を押したときに入力中の読み（ひらがな）を Google に送信します。変換は、設定画面で注意事項に同意してオンにすることで利用可能になります。

## 開発の始め方

1. Unity 6000.3.19f1 でプロジェクトを開きます。
2. シーン `Assets/VRime/Scenes/Main.unity` を開いて再生します。既定では、PC のキーボードで両手のコントローラーを模すため、VR 機器は不要です。SteamVR に接続して確かめるときは、`KeyboardApp` の `Editor Device` を `OpenVR` にします。
3. テストは Test Runner の EditMode で実行します。
4. ビルドはメニューの `VRime/Build/Development` または `VRime/Build/Release` で行います（出力先は `Builds/`）。

### ドキュメント
- 設計: [ARCHITECTURE.md](ARCHITECTURE.md)
- 作業の手順: [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md)
- 仕様: [docs/SPEC.md](docs/SPEC.md)
- 未実装の機能: [docs/ROADMAP.md](docs/ROADMAP.md)

## ライセンス

このリポジトリは [PolyForm Strict License 1.0.0](LICENSE) で公開しています。非商用の目的での利用は許可しますが、改変と再配布は許可しません。

同梱するサードパーティのコンポーネントには、それぞれのライセンスが適用されます。

| コンポーネント | ライセンス | ライセンス文 |
|---|---|---|
| OpenVR（Valve Corporation） | BSD 3条項ライセンス | [Assets/VRime/ThirdParty/OpenVR/LICENSE](Assets/VRime/ThirdParty/OpenVR/LICENSE) |
| Noto Sans JP | SIL Open Font License 1.1 | [Assets/VRime/Fonts/OFL.txt](Assets/VRime/Fonts/OFL.txt) |
