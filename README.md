# HALF AWARE ／ かたあはれ

短時間で終わる一人称 3D ウェブゲーム。自作短編『かたあはれ』の着想を元にしたオリジナルシナリオ。

2166 年のロンドン。記憶をチップから抜き取って売る女の、ある夕方の話。

## 中身

| | |
|---|---|
| `unity/` | Unity プロジェクト本体（Unity 6 LTS + URP + WebGL） |
| `docs/` | 設計文書・実装計画・参考資料 |
| `prototype-three/` | 移行前の Three.js 試作。参照用 |

場面は 2 つ。

- **自室** `Assets/Scenes/Room.unity` — ロンドンの安宿。煙草と記憶の抜き取り
- **路地裏** `Assets/Scenes/Alley.unity` — グレビル・ストリートとブリーディング・ハート・ヤード

## 開いたら、まず組み直す

路地裏の mesh は手で置いたものではなく、`Assets/Editor/BuildAlley.cs` が組み立てて焼いたもの。
焼いた結果は 62 MB あるので repo には置いていない。**clone した直後は路地裏が空なので、一度組み直すこと。**

1. Unity で `Assets/Scenes/Alley.unity` を開く
2. メニューの **`HalfAware` ▸ `Build the alley`**

通りと中庭、出店、人、雨、看板まで一式が出来る。数十秒かかる。

## メニュー

| | |
|---|---|
| `HalfAware/Scenes` | 場面の一覧。タブとして開いておける。開く・開いて再生 |
| `HalfAware/Build the alley` | 路地裏を組み直す |
| `HalfAware/Bake the font` | 暗転のカードに使う明朝を焼き直す。カードの文を変えたら走らせる |
| `HalfAware/Shoot the street backdrop` | 通りの突き当たりに貼る書き割りを撮り直す |

自室の小物にもそれぞれ組み立てのメニューがある（`Fill the ashtray`、`Build the implant jack` ほか）。

## 遊び方

- 移動 `WASD`、見回し マウス、調べる `E`
- `Shift` を押している間は走る
- `Tab` でログと場面の一覧。数字で場面を移れる

## 確かめ方

純粋な C# と薄い MonoBehaviour に分けてあるので、進行・時刻表・姿勢の計算は
EditMode のテストで確かめられる。Unity の Test Runner から EditMode を走らせる。

## 素材の出典

- 音 — `unity/Assets/Audio/LICENSES.md`
- フォント — `unity/Assets/Fonts/LICENSES.md`

**音の一部は許諾が未記入のまま。公開前に埋めること。**
