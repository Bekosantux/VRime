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
