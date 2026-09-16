# モデルの出典

| 置き場 | 出典 | 許諾 |
|---|---|---|
| `kenney/` | Kenney Furniture Kit（https://kenney.nl/assets/furniture-kit） | Creative Commons CC0 1.0 |
| `quaternius/` | Quaternius Ultimate Modular Women（https://quaternius.com/packs/ultimatemodularwomen.html） | 下の注記を参照 |
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
