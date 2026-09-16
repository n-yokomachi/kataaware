# Unity 移行 段階 4-b: 家具への差し替え Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `docs/superpowers/specs/2026-09-16-assets-design.md` の 6 節・段階 2 を実装する。自室の一般的な物（机、椅子、卓、ドア）を仮の箱から Kenney のモデルに差し替え、部屋が住んでいる場所に見えるだけの物（棚、敷物、卓上の明かり）を足す。固有物（端末、メモリハブ、前腕とジャック）は箱のまま残し、段階 4-c で生成した物に差し替える。

**Architecture:** 差し替えは 1 つずつ行い、そのたびに当たり判定を付け直す。モデルは当たり判定を持たないので、置いた後の外接箱から `BoxCollider` を作る。調べる対象（`Interactables` の子）はモデルとは別の物なので触らず、卓の高さが変わる 3 つだけ高さを合わせ直す。位置はオーナーが後から動かす前提なので、仮の箱と同じ場所に置いてから調整する。

**Tech Stack:** Unity 6000.3.24f1、URP 17.3、glTFast 6.13（`.glb` の取り込み）、MCP for Unity 10.0。

**先に確かめてあること（親セッションが実機で確認済み）:**
- Kenney Furniture Kit（CC0）から 7 点を `unity/Assets/Models/kenney/` に取り込み済み。出典は `unity/Assets/Models/LICENSES.md`。コミット `6da5147`
- **この kit は 1 単位 = 0.5 メートル。実寸にするには倍率 2 を掛ける。** 原点は床に接する面にあり、モデルは z の負側へ伸びる
- 倍率 2 のときの実寸（幅 x 高さ x 奥行、メートル）。`中心 z` は原点から見た奥行方向の中心

| ファイル | 実寸 | 中心 z |
|---|---|---|
| `desk` | 1.47 x 0.77 x 0.78 | -0.37 |
| `chairDesk` | 0.67 x 1.22 x 0.63 | -0.31 |
| `cabinetBedDrawer` | 0.53 x 0.53 x 0.43 | -0.19 |
| `doorway` | 0.97 x 2.02 x 0.23 | -0.09 |
| `bookcaseOpen` | 0.80 x 1.76 x 0.50 | -0.25 |
| `rugRectangle` | 3.14 x 0.02 x 1.84 | -0.92 |
| `lampSquareTable` | 0.24 x 0.58 x 0.24 | -0.12 |

- 材質は glTFast が `Shader Graphs/glTF-pbrMetallicRoughness` で作る。URP で描ける
- 部屋は 6 メートル四方。+Z 側の壁に机、-Z 側の壁にドア。床は y=0

**実行時の注意:**
- コミットメッセージにモデル名・ツール名を著者として入れない
- subagent のモデルは `opus` か `sonnet` で毎回明示する
- `HANDOFF.md` は作らない。`docs/` はこのファイルのチェックボックス以外触らない
- **シーンを変える前・再生する前に `EditorApplication.isPlaying` を読む**。再生中ならオーナーが遊んでいる可能性があるので勝手に止めず、親セッションに戻す
- **`Interactables` の子には触らない**（Task 3 で高さを直す 3 つを除く）。調べる対象の id・前提・文面は段階 3 で固めたもの
- `execute_code` は C# 6 の範囲（`out var`・タプル・パターンマッチ・ローカル関数・文字列補間は使わない）。`HalfAware.*` と `UnityEngine.*` は名前空間つきなら直接書ける
- EditMode テストは `mcp__UnityMCP__run_tests`（`mode: "EditMode"`, `assembly_names: ["HalfAware.Tests.EditMode"]`）→ `get_test_job`。着手時点で 67 件通る。このプランはコードを変えないので、どのタスクの後も 67 件のまま
- **フォントのアトラスをコミットに入れない**: `git status` に `unity/Assets/Fonts/NotoSansJP-Regular SDF.asset` が出たら `git checkout -- "unity/Assets/Fonts/NotoSansJP-Regular SDF.asset"` で戻す
- **MCP から再生するとき**は、実行中に `Application.runInBackground = true` を立て、確認の後に false へ戻す。カメラを向けたいときは `PlayerController.Pitch`（正が下向き）と `Yaw` に度を書く

---

## ファイル構成

| パス（`unity/` 以下） | 変更 | 責務 |
|---|---|---|
| `Assets/Scenes/Room.unity` | 変更 | 仮の箱をモデルに差し替え、当たり判定を付け直し、物を足す |

コードも素材も変えない。触るのはシーン 1 つだけ。

---

### Task 1: 机・椅子・ドアを差し替える

**Files:**
- Modify: `unity/Assets/Scenes/Room.unity`

- [ ] **Step 1: 現状を見る**

`execute_code` で `return "isPlaying=" + EditorApplication.isPlaying;`。`True` なら止めて親セッションに戻す。

`mcp__UnityMCP__manage_scene`（`action: "get_hierarchy"`, `max_depth: 2`）で `Room` の子が 13 個（`Floor` `Ceiling` `Wall_Front` `Wall_Back` `Wall_Left` `Wall_Right` `Door` `Desk` `Screen` `SideTable` `MemoryHub` `Chair` `Jack`）であることを確かめる。

- [ ] **Step 2: 差し替える**

`execute_code`:

```csharp
if (EditorApplication.isPlaying) return "in play mode";
var room = GameObject.Find("Room");
if (room == null) return "Room not found";
System.Func<string, string, Vector3, float, bool, string> place = (oldName, model, pos, yaw, collide) => {
    var src = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/kenney/" + model + ".glb");
    if (src == null) return oldName + ":model missing";
    var existing = room.transform.Find(oldName);
    if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
    var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
    go.name = oldName;
    go.transform.SetParent(room.transform, false);
    go.transform.localPosition = pos;
    go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
    go.transform.localScale = Vector3.one * 2f;  // kit は 1 単位 = 0.5 メートル
    if (!collide) return oldName + ":ok(no collider)";
    // モデルは当たり判定を持たないので、置いた後の外接箱から作る
    var renderers = go.GetComponentsInChildren<Renderer>();
    if (renderers.Length == 0) return oldName + ":no renderer";
    var b = renderers[0].bounds;
    for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
    var box = go.AddComponent<BoxCollider>();
    box.center = go.transform.InverseTransformPoint(b.center);
    box.size = new Vector3(b.size.x / go.transform.lossyScale.x, b.size.y / go.transform.lossyScale.y, b.size.z / go.transform.lossyScale.z);
    return oldName + ":ok " + b.size.x.ToString("0.00") + "x" + b.size.y.ToString("0.00") + "x" + b.size.z.ToString("0.00");
};
// 机は +Z 側の壁を背にする。モデルは z の負側へ伸びるので、原点を壁際に置いて手前へ伸ばす
var a = place("Desk", "desk", new Vector3(1.5f, 0f, 2.75f), 0f, true);
// 椅子は机に向かう。背が -Z 側、座面が机の方を向く
var b = place("Chair", "chairDesk", new Vector3(1.5f, 0f, 1.75f), 0f, false);
// ドアは -Z 側の壁。壁の内側に貼り付ける
var c = place("Door", "doorway", new Vector3(0.8f, 0f, -2.88f), 180f, false);
var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return a + " / " + b + " / " + c + " / roomChildren=" + room.transform.childCount;
```

Expected: 3 つとも `ok`、`roomChildren=13`（差し替えなので数は変わらない）。`read_console` でエラー 0。

- [ ] **Step 3: 置いた結果を読み返す**

`execute_code`:

```csharp
var room = GameObject.Find("Room").transform;
var report = "";
string[] names = new string[] { "Desk", "Chair", "Door" };
foreach (var n in names)
{
    var t = room.Find(n);
    if (t == null) { report += n + ": missing\n"; continue; }
    var renderers = t.GetComponentsInChildren<Renderer>();
    var b = renderers[0].bounds;
    for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
    var box = t.GetComponent<BoxCollider>();
    report += n + ": pos=" + t.localPosition + " yaw=" + t.localEulerAngles.y.ToString("0")
        + " world x=" + b.min.x.ToString("0.00") + ".." + b.max.x.ToString("0.00")
        + " y=" + b.min.y.ToString("0.00") + ".." + b.max.y.ToString("0.00")
        + " z=" + b.min.z.ToString("0.00") + ".." + b.max.z.ToString("0.00")
        + " collider=" + (box != null) + "\n";
}
return report.Trim();
```

Expected: 机は y が 0 から 0.77 ほど、z が 2 前後から 2.75 まで（壁は z=2.9 が内側）。ドアは y が 0 から 2.02 ほど。壁（±2.9）や床（0）を突き抜けていないこと。突き抜けていたら止めて報告する。

- [ ] **Step 4: コミット**

```bash
cd /d/work/kataaware && git status --short && git add unity/Assets/Scenes/Room.unity && git commit -m "feat: put real furniture where the desk, the chair and the door were"
```

---

### Task 2: 卓を差し替え、部屋に物を足す

**Files:**
- Modify: `unity/Assets/Scenes/Room.unity`

- [ ] **Step 1: 卓を差し替え、棚・敷物・明かりを足す**

`execute_code`。`place` は Task 1 と同じ書き方を使う:

```csharp
if (EditorApplication.isPlaying) return "in play mode";
var room = GameObject.Find("Room");
if (room == null) return "Room not found";
System.Func<string, string, Vector3, float, bool, string> place = (name, model, pos, yaw, collide) => {
    var src = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/kenney/" + model + ".glb");
    if (src == null) return name + ":model missing";
    var existing = room.transform.Find(name);
    if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
    var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
    go.name = name;
    go.transform.SetParent(room.transform, false);
    go.transform.localPosition = pos;
    go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
    go.transform.localScale = Vector3.one * 2f;
    if (!collide) return name + ":ok(no collider)";
    var renderers = go.GetComponentsInChildren<Renderer>();
    if (renderers.Length == 0) return name + ":no renderer";
    var b = renderers[0].bounds;
    for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
    var box = go.AddComponent<BoxCollider>();
    box.center = go.transform.InverseTransformPoint(b.center);
    box.size = new Vector3(b.size.x / go.transform.lossyScale.x, b.size.y / go.transform.lossyScale.y, b.size.z / go.transform.lossyScale.z);
    return name + ":ok";
};
// 椅子の右の卓。灰皿と煙草を載せる。高さ 0.53
var a = place("SideTable", "cabinetBedDrawer", new Vector3(2.5f, 0f, 1.2f), 0f, true);
// 壁際の棚。-X 側の壁を背にする
var b = place("Bookcase", "bookcaseOpen", new Vector3(-2.85f, 0f, 0.5f), 90f, true);
// 床の敷物。部屋の中ほど。当たり判定は要らない
var c = place("Rug", "rugRectangle", new Vector3(-1.4f, 0.01f, 1.2f), 0f, false);
// 卓の上の明かり。卓の天板が 0.53 なのでその上に置く
var d = place("Lamp", "lampSquareTable", new Vector3(2.62f, 0.53f, 1.1f), 0f, false);
var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return a + " / " + b + " / " + c + " / " + d + " / roomChildren=" + room.transform.childCount;
```

Expected: 4 つとも `ok`、`roomChildren=16`（卓の差し替え 1 つ、追加 3 つ）。`read_console` でエラー 0。

- [ ] **Step 2: 置いた結果を読み返す**

Task 1 Step 3 と同じ読み返しを `SideTable` `Bookcase` `Rug` `Lamp` に対して行う。
Expected: 棚が -X 側の壁（x=-2.9）を突き抜けていないこと。敷物が床（y=0）に沈んでいないこと。明かりが卓の上に載っていること。

- [ ] **Step 3: コミット**

```bash
cd /d/work/kataaware && git add unity/Assets/Scenes/Room.unity && git commit -m "feat: give the room a side table, a bookcase, a rug and a lamp"
```

---

### Task 3: 卓の上の物の高さを合わせる

仮の箱の卓は高さ 0.7 で、その上の 3 つ（灰皿、煙草、煙草の箱）は y=0.85 に置いてあった。新しい卓は 0.53 なので、載っているように見える高さへ下げる。

**Files:**
- Modify: `unity/Assets/Scenes/Room.unity`

- [ ] **Step 1: 下げる**

`execute_code`:

```csharp
if (EditorApplication.isPlaying) return "in play mode";
var itemsRoot = GameObject.Find("Interactables");
if (itemsRoot == null) return "Interactables not found";
var report = "";
// 卓の天板は 0.53。その少し上に載せる
string[] onTable = new string[] { "ashtray", "cigarette", "cigarette-box" };
foreach (var id in onTable)
{
    var t = itemsRoot.transform.Find("Interactable_" + id);
    if (t == null) { report += id + ":missing\n"; continue; }
    var p = t.localPosition;
    var before = p.y;
    p.y = 0.58f;
    t.localPosition = p;
    report += id + ": y " + before.ToString("0.00") + " -> " + p.y.ToString("0.00") + "\n";
}
var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return report.Trim();
```

Expected: 3 つとも `0.85 -> 0.58`。

- [ ] **Step 2: 座ったまま届くか確かめる**

`execute_code`。座った目の位置（1.5, 1.15, 1.2）から、卓の上の 3 つと机の上の物が、距離 2 メートル以内かつ視線の角度 0.7 ラジアン以内に入るかを計算する:

```csharp
var eye = new Vector3(1.5f, 1.15f, 1.2f);
var itemsRoot = GameObject.Find("Interactables").transform;
var report = "座った目から見た角度と距離\n";
foreach (Transform t in itemsRoot)
{
    var to = t.position - eye;
    var dist = to.magnitude;
    // 水平に見たときの角度と、真下を向いたときの角度を両方出す
    var flat = Mathf.Acos(Mathf.Clamp(Vector3.Dot(to.normalized, new Vector3(to.x, 0f, to.z).normalized), -1f, 1f)) * Mathf.Rad2Deg;
    report += "  " + t.name.Replace("Interactable_", "").PadRight(16)
        + "距離 " + dist.ToString("0.00") + "m  水平からの下がり " + flat.ToString("0") + "度"
        + (dist <= 2f ? "" : "  ← 2m を超える") + "\n";
}
return report.Trim();
```

Expected: `jack` は下がりが 40 度より大きい（下を向かないと選べない、設計どおり）。`cigarette` `ashtray` `cigarette-box` は距離 2 メートル以内で、下がりが 40 度より小さい（座ったまま選べる）。`chips` `door` は座位からは遠くてよい。40 度を超えるものが `jack` 以外にあれば報告する。

- [ ] **Step 3: コミット**

```bash
cd /d/work/kataaware && git add unity/Assets/Scenes/Room.unity && git commit -m "fix: sit the ashtray and the cigarettes on the new table"
```

---

### Task 4: 再生での確認

**Files:** なし

- [ ] **Step 1: 再生して見る**

`read_console`（`action: "clear"`）→ `isPlaying` が `False` を確かめる → `manage_editor`（`action: "play"`）→ `Application.runInBackground = true;` → `read_console`（`types: ["error", "warning"]`）。
Expected: エラー 0。

- [ ] **Step 2: 座った視界を撮る**

`Temp/furniture-01-seated.png`。座り始めの視界で、机と端末が正面に見えること。

- [ ] **Step 3: 部屋を見回して撮る**

`PlayerController.Yaw` を 0 / 90 / 180 / 270 と回して 4 枚撮る（`Temp/furniture-02-yaw0.png` など）。棚・敷物・ドア・卓が入るようにする。
見るところ:
- 物が床に沈んだり浮いたりしていないか
- 壁や床を突き抜けていないか
- 机と椅子の向きが噛み合っているか（椅子が机に向かっているか）
- ドアが壁の位置と揃っているか
- 明かりが卓の上に載っているか

- [ ] **Step 4: 当たり判定を確かめる**

`execute_code` でプレイヤーを立たせ、机・卓・棚へ向かって歩かせて、すり抜けないことを見る:

```csharp
var player = GameObject.Find("Player");
var pc = player.GetComponent<HalfAware.PlayerController>();
pc.CanMove = true;
var cc = player.GetComponent<CharacterController>();
cc.enabled = false;
player.transform.position = new Vector3(1.5f, 0.05f, 0.5f);
cc.enabled = true;
pc.EyeHeight = 1.6f;
// 机へ向かって押し込む
for (int i = 0; i < 60; i++) cc.SimpleMove(Vector3.forward * 2.6f);
return "机へ歩いた後の位置 z=" + player.transform.position.z.ToString("0.00");
```

Expected: 机の手前で止まる（z が 2 を大きく超えない）。すり抜けたら当たり判定が効いていないので報告する。

- [ ] **Step 5: 止めて後始末**

`Application.runInBackground = false;` → `manage_editor`（`action: "stop"`）→ `read_console`（`types: ["error"]`）。

```bash
cd /d/work/kataaware && git status --short && grep -n "runInBackground" unity/ProjectSettings/ProjectSettings.asset
```

Expected: エラー 0、`runInBackground: 0`、作業ツリーはきれい。`Room.unity` に差分が出ていたら `git checkout` で戻す。

- [ ] **Step 6: オーナーに手元の確認を頼む**

（親セッションが行う。あなたは実施しない）

---

## 段階 4-c に持ち越すこと

- 固有物の生成（椅子、メモリハブ、端末、灰皿、煙草の箱、前腕とジャック）。Hunyuan3D をこの端末に導入する話になるので、着手前にオーナーに確認する
- 前腕とジャックをカメラの子にして、下を向いたときだけ出す仕組み。今の `Jack` は部屋に固定された箱なので、そのとき `Interactable_jack` も一緒に動かす必要がある
- 端末の黒い画面に映る顔の反射。片割れの顔のモデルが要るので固有物の生成より後
- 位置の詰め。この段では仮の箱と同じ場所に置いただけなので、オーナーが実画面を見て動かす。`Room` の子を動かせばよく、調べる対象は `Interactables` の子で別に動かす
- 頂点の揺れ（PS1 の位置精度の粗さ）を入れるかどうか。家具が入ってから試して決める
- 部屋の壁と床は仮の箱のまま。Kenney の壁材に替えるかは、家具が入った見え方を見てから決める

---

## 締めた後に直したこと

計画を実行した後、オーナーが実画面を見て出た指摘を順に直した。以下は記録であり、上のタスクの記述は実行時のまま残してある。

- 本棚を上下逆にしていた。`bookcaseOpen` は素の姿勢で既に正立（y が 0 から 1.76）なのに、逆さに見えるという指摘へ 180 度の捻りを足してしまい、かえって逆さにしていた。捻りを外し、開いた面（local +Z）が部屋を向く向き（y 90 度）に据え直した。**見た目の指摘を受けたら、まず素の姿勢を測ってから直す**
- 本がどの棚板にも載っていなかった。棚板の上面は床から 0.26 / 0.74 / 1.22 / 1.70 の 4 枚で、本は 0.95 に置かれていて宙に浮いていた。頂点の y を集計して棚板の高さを割り出し、下から 3 枚に 2 組ずつ、計 6 組を載せた
- 机のある壁（`Wall_Front`）に窓を足した。机と背の高い鉢で塞がっていない左寄り（x が -2.30 から -0.30）に `wallWindow` を置き、レールと左右に寄せた襞つきのカーテンを付けた。カーテンは箱を 5 枚ずつ並べ、z を交互にずらして襞に見せている
- 窓のガラスが不透明な金属になっていた。`wallWindow` は renderer が 2 つ（壁の板と窓）で材質の枠が 3 つと 2 つあり、既にある窓も含めて全部の枠に金属を当てていたため、ガラスまで金属になっていた。ガラス用に `NightGlass.mat`（紫の自発光）を作って当てた
- 窓の板が壁から白く浮いていた。板側の 3 枠を部屋の壁と同じ材質にして馴染ませ、枠の細部だけ `SteelDark` を残した
- `NightOutside` の板は壁の箱（厚み 0.2）の中に埋まっていて、最初から一度も見えていなかった。ガラスが光るようになったので 2 枚とも外した
- ベッドとその上掛け、敷物、流しとその隣の台を黒に落とした（`ClothDark.mat` を新設、硬い面は `PanelDark`）。併せて、ラジオ・段ボール・鉢・脇机に残っていた木の材質も暗い板に替えた

作業の進め方で反省がもう一つ。撮影の subagent を走らせたまま同じ場面を直そうとして、また止める羽目になった。**場面を触る作業と撮影は重ねない**。
