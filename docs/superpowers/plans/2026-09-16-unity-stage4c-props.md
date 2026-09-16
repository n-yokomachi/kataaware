# 段階 4-c 固有物の生成と差し替え 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 自室の固有物を Hunyuan3D で生成して仮置きと差し替え、前腕とジャックを主観視点に出す。

**Architecture:** 前腕はカメラの子として付け、下を向いたときだけ出す。判定は純粋なクラスに切り出して EditMode テストで押さえ、MonoBehaviour は表示の切り替えだけを持つ。生成物は `Assets/Models/generated/` に `.glb` で置き、場面では Kenney の仮置きと同じ「測ってから据える」手順で入れ替える。

**Tech Stack:** Unity 6000.3.24f1 / URP 17.3 / glTFast 6.13 / Unity Test Framework 1.6、Hunyuan3D 2.1（形状生成のみ）、gltf-transform（三角形の削減）

---

## Hunyuan3D は使えない（ライセンスの territory 条項）

オーナーの承認を得て導入に着手したが、リポジトリを取得して LICENSE を読んだ段階で止めた。**Tencent Hunyuan 3D 2.1 の許諾は欧州連合・英国・韓国を「Territory」から除いており、生成物（Output）についても Territory の外での使用・複製・改変・配布・表示を禁じている**（第 2 条 l 項の定義、第 5 条 c 項）。

この作品は itch.io での公開を前提にしている（Unity 移植の設計書 5 節の段 5）。web で公開すれば英国と欧州連合からも見えるため、生成物を同梱した配布は許諾の外に出る。舞台が倫敦であることとは無関係に、配布先の問題として成立しない。

取得したリポジトリ（315 MB）は作業ツリーの外へ退けてあり、重みは取得していない。

**代わりの案:**

1. **Tripo か Meshy**（設計書 3 節が挙げた代替）。MCP for Unity の `generate_model` が両方に対応していて、文章からの生成にも対応する。鍵が未設定なので、オーナーが editor の secure store に API キーを入れる必要がある。有料
2. **CC0 の素材パック**。体と椅子は Quaternius や KayKit に合う物がある見込み。小物は自分で箱から組む
3. **許諾の緩い別のモデル**。重みの許諾まで含めて確かめる必要がある

小物（灰皿、煙草の箱、煙草 1 本）は箱と円柱で組んで既に入れた。モニター、筐体、カーテンと同じやり方で、生成器の決着を待たずに進められた分。

## 前提と、オーナーに確認が要ること

- 素材の設計書 4 節が「大きなダウンロードの前にオーナーに確認する」と定めている。**タスク 2 以降は、その確認が取れるまで着手しない**
- 確認する内容: Hunyuan3D 2.1 の重みと PyTorch 一式で数 GB の取得と導入が要る。置き場所はこの端末（RTX 4070 12GB / RAM 32GB）。形状生成のみでテクスチャ生成は使わない
- 確認が取れない場合の代わり: 固有物だけ Tripo（web サービス）で生成する。設計書 3 節に代替として明記済み

## 設計書から変わったこと

- 素材設計書は固有物に「端末（物理モニターと薄いキーボード）」を挙げているが、段 4-b でオーナーの指示によりモニター 5 枚のアーム構成を箱で組み、鍵盤と鼠は Kenney の物を置いた。**端末は生成の対象から外す**
- 「メモリハブ」は今、脇机の上の仮の箱。生成して差し替える
- 「椅子」は Kenney の `chairDesk` が仮置き

## 生成する物

| 物 | 今の状態 | 要点 |
|---|---|---|
| 椅子 | `chairDesk`（Kenney） | 神経端末に繋いで潜る椅子。リクライニングし、土台が重く、ケーブルの受け口がある |
| メモリハブ | 仮の箱 | 16 枚挿しのラック。前面にスロットが並ぶ箱 |
| 灰皿 | 実体なし（調べる対象だけ） | 吸い殻が乗った皿 |
| 煙草の箱 | 実体なし（調べる対象だけ） | 紙巻き煙草の紙箱。『双鶴』の意匠は入れない |
| 主観視点の体 | 仮の箱の前腕 `Forearm` | 首から下。座位と立位。前腕にジャックが刺さった状態と抜いた状態 |

## ファイル構成

- 作成: `unity/Assets/Scripts/Player/ForearmView.cs` — 前腕を出すかどうかの判定だけを持つ純粋なクラス
- 作成: `unity/Assets/Scripts/Player/Forearm.cs` — 判定を受けて表示を切り替える MonoBehaviour
- 作成: `unity/Assets/Tests/EditMode/ForearmViewTests.cs`
- 変更: `unity/Assets/Scenes/Room.unity` — `Player/Eye` の下に `Forearm`、`Interactable_jack` をその子へ移す
- 作成: `unity/Assets/Models/generated/*.glb` — 生成物
- 変更: `unity/Assets/Models/LICENSES.md` — 生成物の出典を記録

---

### タスク 1: 前腕を下向きのときだけ出す（生成を待たずに実装できる）

**Files:**
- Create: `unity/Assets/Scripts/Player/ForearmView.cs`
- Create: `unity/Assets/Scripts/Player/Forearm.cs`
- Create: `unity/Assets/Tests/EditMode/ForearmViewTests.cs`
- Modify: `unity/Assets/Scenes/Room.unity`

背景: 主観視点に手は描かない方針だが、前腕とジャックはその例外（素材設計書 5 節）。ピッチが一定より下を向いたときだけ出す。境目でちらつかないよう、出る角度と消える角度をずらす。`PlayerController.Pitch` は正が下向き。

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using NUnit.Framework;
using HalfAware;

namespace HalfAware.Tests
{
    public class ForearmViewTests
    {
        [Test]
        public void HiddenWhenLookingAhead()
        {
            Assert.IsFalse(ForearmView.ShouldShow(0f, false, 35f, 8f));
        }

        [Test]
        public void ShowsOnceThePitchPassesTheThreshold()
        {
            Assert.IsTrue(ForearmView.ShouldShow(36f, false, 35f, 8f));
        }

        [Test]
        public void StaysUpUntilWellBackAboveTheThreshold()
        {
            // 出ている間は、しきい値から hysteresis ぶん戻るまで消えない
            Assert.IsTrue(ForearmView.ShouldShow(30f, true, 35f, 8f));
            Assert.IsFalse(ForearmView.ShouldShow(26f, true, 35f, 8f));
        }

        [Test]
        public void DoesNotFlickerRightAtTheThreshold()
        {
            var shown = false;
            for (var i = 0; i < 10; i++)
            {
                shown = ForearmView.ShouldShow(35f + (i % 2 == 0 ? 0.01f : -0.01f), shown, 35f, 8f);
            }
            Assert.IsFalse(shown, "しきい値をまたぐ揺れでは出ない");
        }
    }
}
```

- [ ] **Step 2: 落ちることを確かめる**

`run_tests`（`mode: "EditMode"`）。Expected: コンパイルに失敗し `ForearmView` が見つからない。

- [ ] **Step 3: 判定を書く**

```csharp
using UnityEngine;

namespace HalfAware
{
    /// <summary>前腕を出すかどうか。ピッチは正が下向き。境目でちらつかないよう戻りをずらす</summary>
    public static class ForearmView
    {
        /// <summary>出る角度。度</summary>
        public const float DefaultShowBelow = 35f;
        /// <summary>出ている間は、この角度ぶん戻るまで消えない。度</summary>
        public const float DefaultHysteresis = 8f;

        public static bool ShouldShow(float pitch, bool showing, float showBelow, float hysteresis)
        {
            if (showing) return pitch > showBelow - hysteresis;
            return pitch > showBelow;
        }
    }
}
```

- [ ] **Step 4: 通ることを確かめる**

`run_tests`（`mode: "EditMode"`）。Expected: 4 件とも通る。他の 67 件も通ったまま。

- [ ] **Step 5: 表示を切り替える MonoBehaviour を書く**

```csharp
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// カメラの子に付ける前腕。下を向いたときだけ出す。
    /// ジャックを抜いたら、ケーブルつきの腕からケーブルなしの腕へ替える
    /// </summary>
    [DefaultExecutionOrder(-5)]
    public sealed class Forearm : MonoBehaviour
    {
        [SerializeField] PlayerController player;
        [SerializeField] SceneFlow flow;
        [Tooltip("ジャックが刺さっている前腕")]
        [SerializeField] GameObject withJack;
        [Tooltip("抜いた後の前腕")]
        [SerializeField] GameObject withoutJack;
        [Tooltip("抜く操作の対象。腕が出ていないあいだは選べないようにする")]
        [SerializeField] GameObject jackItem;
        [Tooltip("この角度より下を向くと出る。度。正が下向き")]
        [SerializeField] float showBelow = ForearmView.DefaultShowBelow;
        [Tooltip("出ている間は、この角度ぶん戻るまで消えない。度")]
        [SerializeField] float hysteresis = ForearmView.DefaultHysteresis;

        bool showing;
        bool pulled;

        void OnEnable()
        {
            if (flow != null) flow.Examined += OnExamined;
        }

        void OnDisable()
        {
            if (flow != null) flow.Examined -= OnExamined;
        }

        void OnExamined(IInteractable item)
        {
            if (item != null && item.Id == "jack") pulled = true;
        }

        void LateUpdate()
        {
            if (player == null) return;
            showing = ForearmView.ShouldShow(player.Pitch, showing, showBelow, hysteresis);
            if (withJack != null) withJack.SetActive(showing && !pulled);
            if (withoutJack != null) withoutJack.SetActive(showing && pulled);
            // 腕が見えていないあいだはジャックを選べない。天井を向いたまま抜けるのを防ぐ
            if (jackItem != null) jackItem.SetActive(showing && !pulled);
        }
    }
}
```

- [ ] **Step 6: 場面に組み込む**

`execute_code` で、再生していないことを確かめてから:

```csharp
var player = GameObject.Find("Player").transform;
var eye = player.Find("Eye") ?? player.GetComponentInChildren<Camera>().transform;
var arm = new GameObject("Forearm");
arm.transform.SetParent(eye, false);
arm.transform.localPosition = new Vector3(0.18f, -0.28f, 0.42f);
arm.transform.localRotation = Quaternion.Euler(18f, -12f, 0f);
```

仮の形として、`withJack` は前腕の箱（0.09 x 0.09 x 0.30）＋ジャックの小箱（0.03 x 0.03 x 0.05）、`withoutJack` は前腕の箱だけ。材質は `Assets/Materials/Room/PanelDark.mat` を当てる。生成物ができたら差し替える。

`Interactable_jack` を `Forearm` の子へ移し、`localPosition` を手首の位置に合わせる。位置はカメラ基準になるので、部屋の座標からは外れる。

- [ ] **Step 7: 実機で確かめる**

再生して、正面では腕が出ず、下を向くと出ること、35 度あたりで往復させてもちらつかないことを `ScreenCapture` で確かめる。`Interactable_jack` が腕の出ていないときに選ばれないことを `InteractionPicker.Select` で確かめる。

- [ ] **Step 8: commit**

```bash
cd /d/work/kataaware && git checkout -- "unity/Assets/Fonts/NotoSansJP-Regular SDF.asset"; git add -A unity/Assets && git commit -m "feat: show the forearm only when he looks down"
```

---

### タスク 2: Hunyuan3D の導入（**オーナーの確認が要る**）

**Files:**
- Create: `tools/hunyuan/README.md` — 導入手順と動かし方の記録

- [ ] **Step 1: 取得するものと容量をオーナーに示して確認を取る**

確認が取れるまで以降のステップに進まない。取れない場合はタスク 7（Tripo による代替）へ移る。

- [ ] **Step 2: 環境を作る**

Python の仮想環境を `tools/hunyuan/.venv` に作り、CUDA 版の PyTorch と Hunyuan3D 2.1 の形状生成に要る依存だけを入れる。テクスチャ生成の依存は入れない。`.gitignore` に `tools/hunyuan/.venv` と重みの置き場を足す。

- [ ] **Step 3: 一番小さい入力で 1 つ出して、通ることを確かめる**

「a simple cube ashtray」程度の文章で 1 つ生成し、GLB が出ることと、生成にかかる時間と VRAM の使用量を記録する。

- [ ] **Step 4: 手順を README に残して commit**

---

### タスク 3〜6: 固有物の生成と差し替え（タスク 2 の後）

物ごとに同じ手順を踏む。1 物 1 タスクとして扱い、1 つ終えるごとに commit する。

対象と順番: 灰皿 → 煙草の箱 → メモリハブ → 椅子 → 体（タスク 8）

各物の手順:

- [ ] **Step 1: 文章を書いて生成する**

上の表の「要点」を英語の文章にして入力する。テクスチャ生成は使わない。

- [ ] **Step 2: 三角形を 3,000 以下に減らす**

`gltf-transform simplify` を使う。減らした後の三角形数を記録する。

- [ ] **Step 3: `Assets/Models/generated/` に入れて取り込む**

`refresh_unity` の後、`execute_code` で寸法を測る。生成物は縮尺が決まっていないので、**必ず測ってから実寸に合わせる**。Kenney の物のような「1 単位 = 0.5 メートル」の約束は無い。

- [ ] **Step 4: 仮置きと入れ替える**

「原点を 0 に置く → 外接箱を測る → 足元の中心へ寄せる」の手順で据える。段 4-b で使った `seat` の小道具と同じ。材質は `Assets/Materials/Room/` の物から部位ごとに当てる。テクスチャは使わない。

- [ ] **Step 5: 調べる対象の位置を合わせる**

物の実体ができた分、`Interactables` の子の位置を実体に寄せる。座った位置からの距離の順（煙草 < 箱 < 灰皿）は崩さない。崩れたら段 4-b の締めの確認と同じ判定を回して確かめる。

- [ ] **Step 6: 撮って確かめて commit**

---

### タスク 7: 代替経路（タスク 2 の確認が取れない場合のみ）

Tripo の web サービスで同じ 6 点を生成し、GLB をダウンロードして同じ手順で取り込む。ローカル導入が不要な代わり、生成物の許諾を `LICENSES.md` に必ず記録する。

---


---

### タスク 8: 主観視点の体（腕だけをやめて、首から下を出す）

タスク 1 で前腕だけを出す仕組みを作ったが、オーナーが実画面を見て「腕だけ見えても変」と判断した。素材設計書 5 節を体を出す方針に書き換えてある。

**要点:**

- **直方体を組んだ棒人間にはしない**。人体の形をした低ポリゴンの物を使う
- 第一候補は CC0 の人体パック。Quaternius の Ultimate Modular Men など。ダウンロードの前に出典と許諾を確かめ、`LICENSES.md` に記録する。合う物が無ければ Hunyuan3D で生成する
- 首から下だけ出す。頭はカメラの位置にあるので描かない。髪や首が視界に入らないところまで切る
- 姿勢が 2 つ要る。座位（開始から煙草を吸い終わるまで）と立位。骨を入れて 2 姿勢を作るか、姿勢ごとの形を 2 つ持つかは、選んだ素材を見て決める
- 体はカメラではなく `Player` に付ける。カメラに付けると、見回すたびに胴ごと回ってしまう
- 体が常に見えるようになるので、`ForearmView` の扱いを見直す。体の表示の条件としては要らなくなるが、**ジャックを選べる条件としては残す**。残さないと、天井を向いたままジャックを抜けてしまう
- `SceneFlow` の立ち上がり（`StandUp`）に合わせて姿勢を切り替える。目線の高さだけでなく体の姿勢も変わる
- 体が近すぎてカメラの手前でちぎれないか、`nearClipPlane`（今 0.05）との兼ね合いを実画面で確かめる

**締めの確認:**

- 正面を向いたとき視界に体が入りすぎない
- うつむくと胴と脚が見える
- 座位から立位への切り替えが、目線の高さの変化と噛み合っている
- ジャックが、腕の見えていないときに選べない
- EditMode テストが全件通る

## 締めの確認

- EditMode テストが全件通る
- 調べる対象の選択が意図した順のまま（段 4-b の締めの確認と同じ判定を回す）
- 床の通行判定が繋がったまま
- 前腕が正面では出ず、下を向くと出る
- コンソールにエラー・警告なし
- `LICENSES.md` に生成物の出典と、生成に使った文章が残っている

## この段に含めないこと

- 端末の黒い画面に映る顔の反射（片割れの顔のモデルが要るため、段 5 より後）
- 音
- 頂点の揺れ

---

## 素材を選び直したときに判ったこと（記録）

**主人公は女性。** 最初に Quaternius の Ultimate Modular **Men** から選んでしまった。Ultimate Modular Women に同じ構成（11 体、4 部位、24 動作）がある。

**顔は要る。** 「頭を消して首から下だけ」で済むと考えたのは誤り。顔は 2 度映る。

1. 端末の黒い画面への反射。文面が顔の特徴を名指ししている（「黒い髪に琥珀色の目。左目の下のほくろ」）
2. 最後の場面での片割れとの邂逅

したがって選ぶ体は、**顔がその 3 つの特徴を担えること**が条件になる。Quaternius の顔は目・髪・眉が別々の材質になっているので、琥珀色の目と黒い髪は材質を差し替えるだけで済む。ほくろは小さな面を足す。

**様式の懸念。** オーナーが Quaternius の見本を見て「カートゥーン過ぎる」と判断した。ただし宣伝用の絵は原寸で、この作品は 1/3 解像度・32 階調・ディザで描く。写実的な顔ほど潰れ、様式化された顔の方が形が残る可能性がある。**実際の描画設定で顔を出して判断する**こと。より写実寄りの候補は Universal Base Characters だが、こちらは下着姿の素体で服が無い。

**glTFast が骨入りの mesh を取り込めない。** `SortAndNormalizeBoneWeightsJob` でジョブ安全性の例外が出て取り込みが失敗する。Kenney の物は骨が無いので通っていた。**人体は FBX を使う**。Unity 標準の取り込みなら骨も動作も通り、Humanoid の avatar も作れるので、座り姿勢の retarget にも有利。

**許諾の表記が 2 つある。** パックに同梱の `License.txt` は CC0 1.0 と書いてあるが、サイトの現行表記は Quaternius Asset License v1.0 に変わっている。どちらも表記不要・商用可・地域制限なしで、game に同梱するのは明示的に許されている。QAL だけが「素材そのものを単体の素材集として再配布すること」を禁じる。この repo は remote を持たない手元だけのものなので今は触れないが、**公開する場合は生の素材を repo に入れない**こと。CC0 の献呈は撤回できないため、同梱の License.txt を落とした時点の版は CC0 として扱える。
