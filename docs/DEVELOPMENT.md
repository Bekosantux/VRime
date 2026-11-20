# 開発ガイド

複数のファイルにまたがる作業の手順と、守る規則。環境の準備と起動の方法は [README.md](../README.md#開発の始め方)、設計は [ARCHITECTURE.md](../ARCHITECTURE.md) を参照。

## シーンの uGUI

- `Assets/VRime/Scenes/Main.unity` を Build Settings の先頭に登録している。描画先は `KeyboardOverlay`（キャンバス 1024×1024）と `SettingsOverlay`（1280×872）の 2つ。
- キャンバスの大きさがテクスチャのピクセル数になるため、拡大率は 1 のままにする。キーボードは既定の幅 0.45m で、1px が約 0.44mm になる。
- 2つの描画先は互いに離して置く（エディターで重ならないようにするため。描画はレイヤーで分かれる）。
- 描画先の下のオブジェクトは、すべて描画先のレイヤー（`VRimeKeyboard` / `VRimeSettings`）に置く。部品を足すときもレイヤーを合わせる（違えば起動時の検証で例外）。
- 繰り返す部品は、`Assets/VRime/UI/Prefabs/` のプレハブのインスタンスとして置く。
  - キーボード: `GripPanelCell`・`GripPanel`（セル 9 個を入れ子に持ち、左右のパネルに使う）・`GripPanelChip`
  - 設定画面: `SettingsButton`・`SettingsRow`（［−］［＋］は `SettingsButton` の入れ子）・`SettingsToggleRow`・`SettingsTab`・`SettingsDropdownOption`
  - 構造・大きさ・フォントなど共通のものはプレハブで変え、位置・`_key`・項目名などの個別の値はインスタンスのオーバーライドに持つ。
  - プレハブの `_key` は空で、キーを設定しないインスタンスは起動時の検証で例外になる。プレハブの文字と色は仮のもの。
- 設定画面の階層:
  - タブ・「既定に戻す」（`SettingsView._reset`）・「アプリを終了」（`SettingsView._quit`）は、ページを隠しても見えるよう、ページの外（キャンバスの直下）に置く。
  - ページ（`SettingsPage`）どうしは同じ場所に重ね、`SettingsView._pages` に `SettingsCatalog` のカテゴリと同じ順で並べる。
  - 同意の画面（`SettingsConsentDialog`）はページの上に描くため、キャンバスの直下でページより後に置く。
  - 選択肢の一覧はほかの行の上に描くため、行の外の、ページの直下の末尾に置く。
- 配置・大きさ・項目名などのラベルはシーンで調整する。文字の内容は各ビューが書き換えるため、シーンの文字には仮の文字を入れてある。
- 文字のフォントは、2つの編集用のフォントアセットのどちらかにする。リッチテキストは入力欄の文字だけが使う。
- エディターで描いた文字のグリフが動的なアトラスの 2 枚目に入ると、TMP はその文字の子に `TMP SubMeshUI` を作る。シーンには保存しない（プレハブのインスタンスでは、子の追加のオーバーライドになる）。
- シーンを変えたら、EditMode テストの `MainSceneTests`（`KeyboardApp.ValidateScene()` を実行する）で確かめる。

## 色とスプライト

- 色をビューが設定しない部品（状態によらない色の部品）には、`ThemeColor` を付けて役割を選ぶ。色をビューが設定する部品には付けない（テーマを変えたときにビューの色を上書きするため）。
- `ThemeColorRole` はシーンに数値で保存されるため、値は末尾にだけ足す。
- プロジェクトの色空間は Linear のため、半透明の色は線形空間で混ざる。不透明度の低い明るい色は、ガンマ空間で混ぜる場合（デザインの CSS）より大きく明るくなる（白 6% の縁で約 2 倍）。下地が決まっている部品（盤面の縁・通知の札・候補の区切り）は、混ぜた後の色を不透明で指定する。
- キーボードの角丸と影は、`Assets/VRime/UI/Sprites/` の 9 スライスのスプライト（自作）を `Image`（Sliced）に使う。
  - スプライトは白（影も白）で作り、色は `Image.color` で付ける（テーマから入る）。
  - Pixels Per Unit 100・Full Rect で、キャンバスの `referencePixelsPerUnit` も 100 のため、スプライトの 1px がテクスチャの 1px になる。
  - `ui_round_32`（盤面）・`ui_ring_32`（盤面の縁）・`ui_round_20`（入力欄・候補のカード）・`ui_round_16`（セル・札の地）・`ui_round_12`（札・見出し・候補・帯・点灯の帯）・`ui_shadow_20`（浮いた部品の影。矩形は部品より各辺 24 大きく、下へ 6 ずらす）。
  - 境界より低い矩形（点灯の帯の高さ 6）では Unity が境界を縮めるため、両端が半円になる。
- エディターで別のテーマの見た目を確かめるときは、テーマのアセットのインスペクターの「シーンに反映」で塗り替える（Undo に記録し、保存はしない）。シーンだけを塗るため、プレハブのインスタンスでは塗った色がオーバーライドになる。
- `ThemeScenePainter` は、状態で変わる部品の色の対象を、ビューのシリアライズフィールドの名前で探す。ビューのフィールドや状態ごとの色を変えたら、合わせて直す。

## テーマを足す

次をすべて揃えて加える。

1. `ColorTheme` の値
2. テーマのアセット（`Assets/VRime/UI/Themes/KeyboardTheme_*.asset`）
3. 一覧（`ThemeCatalog.asset`）への登録。どの `ColorTheme` にもちょうど 1 つずつ
4. 設定の選択肢（`SettingsCatalog`）
5. 設定ファイルでの名前（`SettingsSerializer`）と、仕様書（[docs/spec/settings.md](spec/settings.md#設定ファイル)）

## 設定の項目・カテゴリを足す

- 項目:
  1. 設定ファイルに保存する項目は、値を `UserSettings` に加え、設定ファイルでの名前と読み書きを `SettingsSerializer` に加える。仕様書（[docs/spec/settings.md](spec/settings.md)）も更新する。
  2. `SettingsCatalog` に項目を加え、`UserSettings` の値を読み書きする関数を渡す。保存する項目のキーは、設定ファイルの項目名と同じにする。
  3. シーンの該当するページに、同じキーの行を置く。数値は `SettingsRow`、オン・オフは `SettingsToggleRow`（同名のプレハブを置き、`_key` と項目名を設定する）、選択肢は `SettingsDropdownRow`。
- カテゴリ: `SettingsPage` とタブ（`SettingsTab` のプレハブ）を置き、`SettingsView._pages` に `SettingsCatalog` と同じ順で加える。
- 同意が必要な項目: `SettingsConsentDialog` を置き、`SettingsView._consents` に項目の順で加える。
- 行やページの過不足は `SettingsLayout` の作成時に例外になる。`MainSceneTests` で確かめる。

## テスト

- テストは Test Runner の EditMode で実行する。
- Core のテスト（`Tests/EditMode/`）は、`KeyboardSession` に操作を与える `Rig`（`TestSupport.cs`）で、実際の操作と同じ入力列を再現する。
- App のテスト（`Tests/EditModeApp/`）は、Win32 と OpenVR を境界で差し替えて規則を確かめる。実際のクリップボードはテストで触らない。
- `ActionManifestTests` が、アクションマニフェストと `OpenVRActions.Names` の一致と、既定のバインドを検査する。
- `DocumentationTests` が、文書間のリンク（見出しのアンカーを含む）と、コードのコメントから文書への参照（`docs/spec/xxx.md「見出し」`）が解決することを検査する。

## ビルド

- ビルドはメニューの `VRime/Build/Development` と `VRime/Build/Release` で行う（`Assets/VRime/Editor/Build/PlayerBuilder`）。Build Profile（`Assets/Settings/Build Profiles/` の `Windows Development` と `Windows Release`）を指定してビルドし、出力先はプロファイルに保存されないため `PlayerBuilder` が決める。
- 出力先はプロジェクト直下の `Builds/Development/` と `Builds/Release/` で、`.gitignore` により管理対象外。ビルドの前に出力先を空にする。
- 2つのプロファイルの違いは Development Build の有無だけで、Player Settings は上書きしない。スクリプトのバックエンド（Mono）と Managed Stripping Level（Medium）はプロジェクト共通の設定にし、ストリッピングによる欠落を開発用のビルドでも再現させる。
- IL2CPP は、このプロジェクトでは `GameAssembly.dll` が Mono のランタイムと IL の合計より大きくなり、配布物が Mono より約 7MB（zip で）大きくなるため使っていない。
- `VRime/Build/Release` は、ビルドの後に `Builds/<製品名>-v<Version>-win64.zip` を作る。中身は製品名のフォルダーにまとめ、ライセンス文（`LICENSE` と、同梱するサードパーティのもの）を加え、配布してはいけないフォルダー（`*_BurstDebugInformation_DoNotShip` など）を除く。Version は Player Settings の Version（`bundleVersion`）。
- 実行には、`<名前>_Data/Plugins/x86_64/openvr_api.dll` と、StreamingAssets のファイル（`<名前>_Data/StreamingAssets/SteamVR/` のアクションマニフェスト・既定のバインド・`dashboard_icon.png`、`Windows/tray_icon.ico`）が必要。
- 開発用のツールが置く DLL（MCP for Unity が `Assets/Plugins/Roslyn/` に置く Roslyn。git の管理外）は、`EditorOnlyPlugins` がインポートのたびにエディター専用へ直す。ツールは全プラットフォーム向けの設定で置くため、そのままでは Player に約 12MB が入る。ビルドの前にも検査し、Player に含まれる設定ならビルドを止める。対象のフォルダーを増やすときは `EditorOnlyPlugins.Folders` に加える。
- `Assets/VRime/link.xml` で、自前のアセンブリ（`VRime.Core`・`VRime.App`）と `Valve.OpenVR`・`Newtonsoft.Json` をストリッピングから丸ごと保護する。リフレクションを使う処理や、ネイティブ側から呼ばれる処理を加えても、これらのアセンブリの中なら削られない。それ以外のアセンブリ（パッケージなど）でリフレクションを使うときは、ここに加える。
