# モデルの出典

| 置き場 | 出典 | 許諾 |
|---|---|---|
| `kenney/` | Kenney Furniture Kit（https://kenney.nl/assets/furniture-kit） | Creative Commons CC0 1.0 |
| `quaternius/` | Quaternius Ultimate Modular Women（https://quaternius.com/packs/ultimatemodularwomen.html） | 下の注記を参照 |
| `quaternius/` | Quaternius Ultimate Modular Men（https://quaternius.com/packs/ultimatemodularcharacters.html） | 下の注記を参照 |
| `rocketbox/` | Microsoft Rocketbox Avatar Library（https://github.com/microsoft/Microsoft-Rocketbox） | MIT。下の注記を参照 |
| `generated/` | 自作。`Assets/Editor/ProcMesh.cs` で断面を張って作った物 | 本作の一部 |

Kenney の素材は CC0 なので表示の義務は無いが、どこから来た物かを残すために記録する。

取り込んだのは kit のうち自室で使う分だけで、glTF（`.glb`）の形式を選んだ。同じ kit には OBJ・FBX・DAE・STL も入っている。

この kit は 1 単位 = 0.5 メートルで作られている。実寸にするには 2 倍にする（ドアが高さ 2.02 メートル、机が 0.77 メートルになる）。原点は床に接する面にあり、z の負側へ伸びる。

| ファイル | 使い道 |
|---|---|
| `tableGlass.glb` | 端末を載せる机。脚のあいだに板が無い形 |
| `chairDesk.glb` | 机の前の椅子。段階 4-c で生成した固有の椅子に差し替える |
| `sideTableDrawers.glb` | 椅子の横の卓。明かりとメモリ架を載せる |
| `doorway.glb` | ドア |
| `bookcaseOpen.glb` | 隅の棚 |
| `books.glb` | 棚に並べる本。6 組 |
| `loungeSofa.glb` | 寝起きに使う革張りの長椅子。寝台の代わり |
| `pillow.glb` | 長椅子の枕 |
| `rugRectangle.glb` | 床の敷物 |
| `rugDoormat.glb` | ドアの前の敷物 |
| `lampSquareTable.glb` | 卓の上の明かり |
| `lampSquareCeiling.glb` | 天井の照明器具 |
| `kitchenFridgeLarge.glb` | 台所の冷蔵庫 |
| `kitchenCabinet.glb` | 台所の台 |
| `kitchenSink.glb` | 台所の流し |
| `kitchenMicrowave.glb` | 冷蔵庫の上の電子レンジ |
| `kitchenCoffeeMachine.glb` | 台の上の珈琲機 |
| `computerKeyboard.glb` | 机の鍵盤 |
| `computerMouse.glb` | 机の鼠 |
| `coatRackStanding.glb` | 入口脇の衣掛け |
| `cardboardBoxClosed.glb` / `cardboardBoxOpen.glb` | 積んである箱 |
| `pottedPlant.glb` | 机の脇の鉢 |

使わなくなった分は消してある。場面が参照していない `.glb` は置かない。

壁・天井・床のコンクリートは、この kit ではなく `Assets/Textures/Concrete.png` と `ConcreteFloor.png` で作った。継ぎ目が出ないよう 3x3 に並べてぼかしてから中央を切り出しており、壁には型枠の目地とセパレータの穴、床には打ち継ぎの目地だけを入れてある。作った手順は `docs/` ではなくこの記述に留める。

窓は kit の `wallWindow.glb` を使うのをやめた。壁の板ごと部屋の中へせり出してしまうため、壁を 4 枚に割って穴を空け、枠とガラスを箱で組んでいる。ドアは `doorway.glb` をそのまま使うが、枠が 0.227 メートル厚で壁の前に立ってしまうので、背面の壁を 3 枚に割り、枠より 0.01 メートル小さい穴へ手前の面を揃えて沈めてある。

長椅子の革は `Assets/Textures/Leather.png`（色の濃淡）と `LeatherNormal.png`（法線）で作った。詰め物のうねりと粒の 2 層から高さを作り、その傾きを符号付きで書き出したもの。`loungeSofa.glb` には接線が無く法線が効かないため、接線を付けた写しを `Assets/Models/generated/` に置いて場面ではそちらを使っている。

## Quaternius の許諾について

表記が 2 つ存在する。**パックに同梱されている `License.txt` は CC0 1.0 Universal（パブリックドメインの献呈）**と書いてある。一方、サイトの現行の許諾ページは Quaternius Asset License (QAL) v1.0 に変わっている。

両者に共通するのは、表記不要・商用可・地域の制限なし・完成した作品への同梱は明示的に可、という点。異なるのは QAL だけが「素材そのものを単体の素材集・素材ファイル・雛形として抽出・再梱包・再配布すること」を禁じていること。

本作は game の一部として同梱するので、どちらの読み方でも問題ない。CC0 の献呈は撤回できないため、同梱の License.txt を伴って配られた版は CC0 として扱える。

ただし**この repo を公開する場合は、生の `.fbx` を置いたままにしない**こと。QAL の側で読むと、素材ファイルそのものの再配布に当たりうる。

## 取り込みの注意

人体は **FBX で取り込む**。`.gltf` は glTFast が骨入りの mesh で `SortAndNormalizeBoneWeightsJob` のジョブ安全性の例外を出して失敗する。Kenney の家具は骨が無いので `.glb` のままで通っている。

## 通りの人

`W_*.fbx` が Ultimate Modular Women、`M_*.fbx` が Ultimate Modular Men。どちらも CC0。

街に居そうな身なりだけを選んで、男女 6 体ずつ置いてある。群衆は一様に選ぶので
半々に散る（`HalfAware/Build the alley` の記録に内訳が出る）。
男性パックは `Individual Characters/FBX/` から取った。King・Spacesuit・Swat・
Beach・Farmer はこの街に合わないので入れていない。

**骨組みは男女で同じ**（85〜86 本、`UpperLeg.L` などの名前も揃っている）ので、
`BuildAlley.Pose` はどちらにもそのまま効く。

配布の模型は歩いている途中の姿勢で入っていて、素のままだと足首が前後に 0.29 m ずれる。
立ち姿はそのぶん腿を寄せて揃えている（`BuildAlley.Stride`）。
**骨を identity へ戻してはいけない。** mesh はこの姿勢に合わせて皮を張ってあるので、
大きくずらすと体が裂ける。

## Microsoft Rocketbox について

主人公と片割れの顔と主人公の体は、Microsoft Rocketbox Avatar Library の `Female_Adult_14`（一覧の番号で「女大 14」）から、髪は `Female_Adult_08`（女大 08）から作った。片割れの体は `Female_Adult_11`（女大 11）の体を、膝から下は `Female_Party_02`（一覧の女大 19）の素足とサンダルを借りた。候補として `Female_Adult_03`（女大 03）と `Female_Adult_02`（女大 02）の体も入れてある（体だけを借りる人は、頭のテクスチャを落としていない）。

- 出どころ: https://github.com/microsoft/Microsoft-Rocketbox の `Assets/Avatars/Adults/Female_Adult_14/`、`Assets/Avatars/Adults/Female_Adult_08/`、`Assets/Avatars/Adults/Female_Adult_03/`、`Assets/Avatars/Adults/Female_Adult_02/`、`Assets/Avatars/Adults/Female_Adult_11/`、`Assets/Avatars/Adults/Female_Party_02/`（どれも 2026-09-23 に落とした）
- 許諾: MIT License（同じリポジトリの `LICENSE.md`）。配るものには下の著作権の表示と許諾の文を添える

```
MIT License

Copyright (c) 2020 Microsoft

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

落とした元のファイルは `unity/RawAssets/rocketbox/`（人ごとのフォルダ）に置き、リポジトリに入れていない（`Assets` の外で、`unity/.gitignore` で外してある）。

| 元のファイル | リポジトリに入れた物 |
|---|---|
| `Female_Adult_14.fbx`（0.6 MB） | `rocketbox/Female_Adult_14/Female_Adult_14.fbx`。手を加えずに写した |
| `f017_head_color.tga`（2048、顔・頭皮の髪・首と胸・口の中・目の玉） | `rocketbox/Female_Adult_14/f017_head_color.png`（512 に縮めた写し） |
| `f017_body_color.tga`（2048、服・手・靴） | `rocketbox/Female_Adult_14/f017_body_color.png`（512 に縮めた写し） |
| `f017_opacity_color.tga`（2048、髪の房とまつ毛の色と透け） | `rocketbox/Female_Adult_14/f017_opacity_color.png`（512 に縮めた写し） |
| `Female_Adult_08.fbx`（0.6 MB） | `rocketbox/Female_Adult_08/Female_Adult_08.fbx`。手を加えずに写した |
| `f008_head_color.tga`（2048） | `rocketbox/Female_Adult_08/f008_head_color.png`（512 に縮めた写し） |
| `f008_body_color.tga`（2048） | `rocketbox/Female_Adult_08/f008_body_color.png`（512 に縮めた写し） |
| `f008_opacity_color.tga`（2048） | `rocketbox/Female_Adult_08/f008_opacity_color.png`（512 に縮めた写し） |
| `Female_Adult_03.fbx`（0.8 MB） | `rocketbox/Female_Adult_03/Female_Adult_03.fbx`。手を加えずに写した |
| `f003_body_color.tga`（2048、服・腕と肩・手・靴） | `rocketbox/Female_Adult_03/f003_body_color.png`（512 に縮めた写し） |
| `f003_opacity_color.tga`（2048） | `rocketbox/Female_Adult_03/f003_opacity_color.png`（512 に縮めた写し） |
| `Female_Adult_02.fbx`（0.6 MB） | `rocketbox/Female_Adult_02/Female_Adult_02.fbx`。手を加えずに写した |
| `f002_body_color.tga`（2048） | `rocketbox/Female_Adult_02/f002_body_color.png`（512 に縮めた写し） |
| `f002_opacity_color.tga`（1024） | `rocketbox/Female_Adult_02/f002_opacity_color.png`（512 に縮めた写し） |
| `Female_Adult_11.fbx`（0.6 MB） | `rocketbox/Female_Adult_11/Female_Adult_11.fbx`。手を加えずに写した |
| `f011_body_color.tga`（2048） | `rocketbox/Female_Adult_11/f011_body_color.png`（512 に縮めた写し） |
| `f011_opacity_color.tga`（2048） | `rocketbox/Female_Adult_11/f011_opacity_color.png`（512 に縮めた写し） |
| `Female_Party_02.fbx`（0.7 MB） | `rocketbox/Female_Party_02/Female_Party_02.fbx`。手を加えずに写した |
| `f022_body_color.tga`（2048） | `rocketbox/Female_Party_02/f022_body_color.png`（512 に縮めた写し） |
| `f022_opacity_color.tga`（2048） | `rocketbox/Female_Party_02/f022_opacity_color.png`（512 に縮めた写し） |

- 縮めた写しは `Assets/Editor/Rocketbox/RocketboxTextures.cs`（メニューの HalfAware/Rocketbox/Shrink the textures。人は `RocketboxPerson.cs` に並べてある）で、元の TGA から作り直せる。線形の光で 4×4 を平均し、透けのある絵は α で重みを付けた
- 人ごとの `Painted/` は、縮めた写しに組み立ての手順（`Assets/Editor/Rocketbox/RocketboxPaint.cs`）で手を入れた物。髪を黒に、目の下に黒子、顔に控えめな手入れ。女大 14 はカーディガンも黒にした。女大 08 の服と、どちらの目も元の色のまま。片割れの候補の女大 03（`Face14_Hair08_Body03/Painted/`）はキャミソールとパンツを白に、女大 02（`Face14_Hair08_Body02/Painted/`）はスカートをベージュにした。片割れ（`Face14_Hair08_Body11_Legs22/Painted/`）は女大 11 のワンピースを生成りにし、女大 19 の脚の肌を頭の肌に揃えた
- 法線と光沢のテクスチャ、付いてくる動き（3ds Max の形式で Unity では読めない）は落としていない
- 取り込みの設定は `Assets/Editor/Rocketbox/RocketboxImport.cs` が `Assets/Models/rocketbox/` の下にだけ掛ける。Rocketbox に同梱の `FixRocketboxMaxImport.cs` はプロジェクトの全部の取り込みに掛かり、Quaternius の模型を取り込むたびに例外を出すので入れていない
