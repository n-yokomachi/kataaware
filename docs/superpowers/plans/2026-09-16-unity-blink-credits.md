# 瞬きに乗せるクレジット Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `docs/superpowers/specs/2026-09-16-scenario-design.md` の 4.1 の 3 と、`docs/superpowers/specs/2026-09-15-game-design.md` の 7 節を実装する。クレジットとタイトルを場面の冒頭から外し、煙草を吸っているあいだの 3 回の瞬きに乗せる。瞼が上下から閉じているあいだにカードが出て、目を開けると消えている。

**Architecture:** 瞼は HUD の層として持ち、`HudView` は「どれだけ閉じているか」を 0 から 1 で受け取って形にするだけにする。閉じる・保つ・開くの間の取り方と、どのカードをいつ出すかは `RoomIntroDirector` が持つ。停止はこれまでどおり段ごとに掛け直し、演出の流れと別の時計にしない。カードの文面と 3 つの間は Inspector に出し、遊びながら詰められるようにする。

**Tech Stack:** Unity 6000.3.24f1、URP 17.3、TextMeshPro（com.unity.ugui 2.0）、MCP for Unity 10.0。

**変更前の姿:**
- `RoomIntroDirector`（`unity/Assets/Scripts/Flow/RoomIntroDirector.cs`）は場面の頭でクレジットとタイトルを出し、その間 6.2 秒ほど操作を止めていた。煙草を取ると 4 秒の煙を出して独白を言う
- `HudView`（`unity/Assets/Scripts/Hud/HudView.cs`）は字幕・印・中央の文字・暗転・煙を持つ。シーンの `Hud` の子は手前から順に `Smoke` `SubtitleBand` `Prompt` `Fade` `Center`
- `SceneFlow` は `Examined` で調べ終わりを知らせ、`Say` で字幕を積み、`Freeze` で操作を止める。`Freeze` は長い方を採る

**実行時の注意:**
- コミットメッセージにモデル名・ツール名を著者として入れない
- subagent のモデルは `opus` か `sonnet` で毎回明示する
- `HANDOFF.md` は作らない。`docs/` はこのファイルのチェックボックス以外触らない
- **カードの文面は承認済みのシナリオ文書から来ている。1 文字も変えない。**全角の空白・波ダッシュ・鉤括弧を半角に直さない。書いた後に必ず元と突き合わせる
- **シーンを変える前・再生する前に `EditorApplication.isPlaying` を読む**。再生中ならオーナーが遊んでいる可能性があるので勝手に止めず、親セッションに戻す
- C# を書いたら `mcp__UnityMCP__refresh_unity`（`mode: "force"`, `scope: "all"`, `compile: "request"`, `wait_for_ready: true`）→ `mcp__UnityMCP__read_console`（`types: ["error", "warning"]`）。`MCP-FOR-UNITY: [WebSocket]` の行は転送層の話で、このプロジェクトの誤りではない
- EditMode テストは `mcp__UnityMCP__run_tests`（`mode: "EditMode"`, `assembly_names: ["HalfAware.Tests.EditMode"]`）→ `mcp__UnityMCP__get_test_job`（`wait_timeout: 60`）。着手時点で 67 件通る。このプランはテストを増やさないので、どのタスクの後も 67 件のまま
- `execute_code` は C# 6 の範囲（`out var`・タプル・パターンマッチ・ローカル関数・文字列補間は使わない）。`HalfAware.*` と `UnityEngine.*` は名前空間つきなら直接書ける
- **YAML の中の日本語は `grep` で探せない**。Unity は `\uXXXX` で書き、長い文字列は折り返す。入ったかを確かめるときは `execute_code` で読み返す
- **フォントのアトラスをコミットに入れない**: `git status` に `unity/Assets/Fonts/NotoSansJP-Regular SDF.asset` が出たら `git checkout -- "unity/Assets/Fonts/NotoSansJP-Regular SDF.asset"` で戻す
- **MCP から再生するとき**は、実行中に `Application.runInBackground = true` を立て、確認の後に false へ戻す。カメラを向けたいときは `PlayerController.Pitch` に度を書く（カメラの変換を直接書いても毎フレーム上書きされる）

---

## ファイル構成

| パス（`unity/` 以下） | 変更 | 責務 |
|---|---|---|
| `Assets/Scripts/Hud/HudView.cs` | 変更 | 瞼の層を足し、閉じ具合を 0〜1 で受ける |
| `Assets/Scripts/Flow/RoomIntroDirector.cs` | 変更 | 冒頭のクレジットを外し、煙草の演出に 3 回の瞬きとカードを入れる |
| `Assets/Scenes/Room.unity` | 変更 | 瞼の層 2 枚を `Hud` に足して繋ぎ、カードの文面と間を入れる |

---

### Task 1: `HudView` に瞼を足す

**Files:**
- Modify: `unity/Assets/Scripts/Hud/HudView.cs`

- [x] **Step 1: 項目と操作を足す**

`unity/Assets/Scripts/Hud/HudView.cs` の `[SerializeField] Image smokeLayer;` の行の直後に次を足す:

```csharp
        [Tooltip("上の瞼。画面の上から中央へ降りてくる黒い層")]
        [SerializeField] Image eyelidTop;
        [Tooltip("下の瞼。画面の下から中央へ上がってくる黒い層")]
        [SerializeField] Image eyelidBottom;
```

`Awake` の `SetSmoke(0f);` の直後に次を足す:

```csharp
            SetEyelids(0f);
```

`SetSmoke` メソッドの直前に次を足す:

```csharp
        /// <summary>
        /// 瞼の閉じ具合。0 で開ききり、1 で閉じきる。
        /// 上下の層の高さを画面の半分まで伸ばして中央で合わせるので、画面の大きさに依らない
        /// </summary>
        public void SetEyelids(float closed)
        {
            var t = Mathf.Clamp01(closed);
            Cover(eyelidTop, new Vector2(0f, 1f - 0.5f * t), new Vector2(1f, 1f), t);
            Cover(eyelidBottom, new Vector2(0f, 0f), new Vector2(1f, 0.5f * t), t);
        }

        static void Cover(Image layer, Vector2 anchorMin, Vector2 anchorMax, float shown)
        {
            if (layer == null) return;
            var rect = layer.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            layer.gameObject.SetActive(shown > 0f);
        }
```

暗転の層と同じく、瞼の層も繋がっていなければ何もしない。段階を追って足すため。

- [x] **Step 2: コンパイルとテストを確かめる**

`refresh_unity` → `read_console`（エラー・警告 0）→ `run_tests` → `get_test_job`。
Expected: 67 件 passed。

- [x] **Step 3: コミット**

```bash
cd /d/work/kataaware && git add unity/Assets/Scripts/Hud/HudView.cs && git commit -m "feat: add the eyelids to the HUD"
```

---

### Task 2: クレジットを瞬きへ移す

**Files:**
- Modify: `unity/Assets/Scripts/Flow/RoomIntroDirector.cs`

- [ ] **Step 1: 書き換える**

`unity/Assets/Scripts/Flow/RoomIntroDirector.cs` を全文次に置き換える:

```csharp
using System.Collections;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 場面 1 固有の演出。始まってすぐ最初の独白を流す。
    /// 煙草を取ったら煙を立てて操作を止め、そのあいだに 3 回の瞬きを挟む。
    /// 瞼が閉じているあいだにクレジットとタイトルのカードを出し、目を開けると消えている。
    /// 吸い終わりの独白で締める。SceneFlow とは Examined / Say / Freeze だけで繋ぐ
    /// </summary>
    public sealed class RoomIntroDirector : MonoBehaviour
    {
        /// <summary>瞼が閉じるのにかける秒数。開くより速い</summary>
        public const float BlinkCloseSeconds = 0.18f;
        /// <summary>閉じきって止まっている秒数。カードを読む時間</summary>
        public const float BlinkHoldSeconds = 1.3f;
        /// <summary>瞼が開くのにかける秒数</summary>
        public const float BlinkOpenSeconds = 0.25f;
        /// <summary>瞬きと瞬きのあいだ、目を開けている秒数</summary>
        public const float BlinkGapSeconds = 1f;
        /// <summary>煙草を取ってから最初の瞬きまでの秒数</summary>
        public const float LeadInSeconds = 0.8f;
        /// <summary>停止に足す余裕。停止が先に切れて、演出の途中で調べられるのを防ぐ</summary>
        public const float FreezeMargin = 0.25f;

        [SerializeField] SceneFlow flow;
        [SerializeField] HudView hud;
        [Tooltip("この id を調べたら煙草の演出を始める")]
        [SerializeField] string cigaretteId = "cigarette";
        [SerializeField] string[] firstLines = { "うぅ…今回は酔いが酷い…" };
        [Tooltip("瞬き 1 回につき 1 枚。空の要素は文字を出さずに閉じて開くだけ")]
        [SerializeField, TextArea] string[] blinkCards = new string[0];
        [SerializeField] string[] afterSmokeLines = { "煙草が切れた…買いに行くついでに今日のメモリも売っちゃおう" };

        bool smoking;

        /// <summary>煙草を取ってから吸い終わるまでの秒数。瞬きの回数から決まる</summary>
        public float SmokeSeconds
        {
            get
            {
                var blink = BlinkCloseSeconds + BlinkHoldSeconds + BlinkOpenSeconds + BlinkGapSeconds;
                return LeadInSeconds + blink * Mathf.Max(1, blinkCards.Length);
            }
        }

        void OnEnable()
        {
            if (flow == null || hud == null)
            {
                Debug.LogError("RoomIntroDirector: flow か hud が未接続", this);
                enabled = false;
                return;
            }
            flow.Examined += OnExamined;
        }

        void OnDisable()
        {
            if (flow != null) flow.Examined -= OnExamined;
            StopAllCoroutines();
            smoking = false;
            if (hud == null) return;
            hud.SetCenter(null);
            hud.SetEyelids(0f);
            hud.CancelSmoke();
        }

        /// <summary>最初の独白は場面の始めに 1 度だけ。切って入れ直してもやり直さない</summary>
        void Start()
        {
            if (enabled) flow.Say(firstLines);
        }

        void OnExamined(IInteractable item)
        {
            if (item.Id != cigaretteId || smoking) return;
            StartCoroutine(Smoke());
        }

        /// <summary>
        /// 煙草を取った後は吸い終わるまで自動で進む。停止は段ごとに掛け直し、
        /// 演出の流れと別の時計にしない。そうしないと停止が先に切れて、途中で調べられる
        /// </summary>
        IEnumerator Smoke()
        {
            smoking = true;
            hud.ShowSmoke(SmokeSeconds);
            flow.Freeze(LeadInSeconds + FreezeMargin);
            yield return new WaitForSeconds(LeadInSeconds);
            foreach (var card in blinkCards)
            {
                if (flow.Completed) yield break;
                flow.Freeze(BlinkCloseSeconds + BlinkHoldSeconds + BlinkOpenSeconds + BlinkGapSeconds + FreezeMargin);
                yield return Blink(card);
                yield return new WaitForSeconds(BlinkGapSeconds);
            }
            smoking = false;
            if (flow.Completed) yield break;
            flow.Say(afterSmokeLines);
        }

        /// <summary>瞼を閉じ、閉じきったあいだにカードを出し、また開く。閉じる方が速く、開く方が遅い</summary>
        IEnumerator Blink(string card)
        {
            for (var t = 0f; t < BlinkCloseSeconds; t += Time.deltaTime)
            {
                hud.SetEyelids(t / BlinkCloseSeconds);
                yield return null;
            }
            hud.SetEyelids(1f);
            if (!string.IsNullOrEmpty(card)) hud.SetCenter(card);
            yield return new WaitForSeconds(BlinkHoldSeconds);
            hud.SetCenter(null);
            for (var t = 0f; t < BlinkOpenSeconds; t += Time.deltaTime)
            {
                hud.SetEyelids(1f - t / BlinkOpenSeconds);
                yield return null;
            }
            hud.SetEyelids(0f);
        }
    }
}
```

`credits` と `titleCard` と `IntroSeconds` は無くなる。カードは `blinkCards` にまとめ、何枚出すかで煙草の長さが決まる。

- [ ] **Step 2: コンパイルとテストを確かめる**

`refresh_unity` → `read_console`（エラー・警告 0）→ `run_tests` → `get_test_job`。
Expected: 67 件 passed。

- [ ] **Step 3: コミット**

```bash
cd /d/work/kataaware && git add unity/Assets/Scripts/Flow/RoomIntroDirector.cs && git commit -m "feat: put the credits in the blinks while the cigarette burns"
```

---

### Task 3: シーンに瞼を足してカードを入れる

**Files:**
- Modify: `unity/Assets/Scenes/Room.unity`

- [ ] **Step 1: 現状を見る**

`execute_code` で `return "isPlaying=" + EditorApplication.isPlaying;`。`True` なら止めて親セッションに戻す。

`mcp__UnityMCP__manage_scene`（`action: "get_hierarchy"`, `max_depth: 2`）で `Hud` の子が `Smoke` `SubtitleBand` `Prompt` `Fade` `Center` の 5 つであることを確かめる。

- [ ] **Step 2: 瞼の層を足して繋ぐ**

重なりの順は、後に足したものほど手前。瞼は暗転より手前、中央の文字より奥に置く。カードは瞼の上に出したいため。

`execute_code`:

```csharp
if (EditorApplication.isPlaying) return "in play mode";
var hudGo = GameObject.Find("Hud");
if (hudGo == null) return "Hud not found";
System.Func<string, UnityEngine.UI.Image> lid = (name) => {
    var found = hudGo.transform.Find(name);
    var go = found != null ? found.gameObject : new GameObject(name, typeof(RectTransform));
    if (found == null) go.transform.SetParent(hudGo.transform, false);
    var image = go.GetComponent<UnityEngine.UI.Image>();
    if (image == null) image = go.AddComponent<UnityEngine.UI.Image>();
    image.color = Color.black;
    image.raycastTarget = false;
    return image;
};
var top = lid("EyelidTop");
var bottom = lid("EyelidBottom");
var center = hudGo.transform.Find("Center");
top.transform.SetAsLastSibling();
bottom.transform.SetAsLastSibling();
if (center != null) center.SetAsLastSibling();

var hud = hudGo.GetComponent(System.Type.GetType("HalfAware.HudView, HalfAware"));
var so = new SerializedObject(hud);
so.FindProperty("eyelidTop").objectReferenceValue = top;
so.FindProperty("eyelidBottom").objectReferenceValue = bottom;
so.ApplyModifiedPropertiesWithoutUndo();

var order = "";
for (int i = 0; i < hudGo.transform.childCount; i++) order += hudGo.transform.GetChild(i).name + " ";
var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return order.Trim();
```

Expected: 戻り値 `Smoke SubtitleBand Prompt Fade EyelidTop EyelidBottom Center`。`read_console` でエラー 0。

- [ ] **Step 3: カードの文面を入れる**

3 枚目の「倫敦」にふりがなを添える。TextMeshPro にルビの機能は無いので、小さくした「ロンドン」を上へずらして置き、そのぶん横へ戻して重ねる。ふりがな 4 文字を半分の大きさにすると幅は 2 文字ぶんになり、「倫敦」の 2 文字と同じ幅で揃う。

`execute_code`:

```csharp
if (EditorApplication.isPlaying) return "in play mode";
var flowGo = GameObject.Find("SceneFlow");
if (flowGo == null) return "SceneFlow not found";
var director = flowGo.GetComponent(System.Type.GetType("HalfAware.RoomIntroDirector, HalfAware"));
if (director == null) return "RoomIntroDirector not found";
var so = new SerializedObject(director);
var cards = so.FindProperty("blinkCards");
string[] text = new string[] {
    "制作 : yoko",
    "<size=64>HALF AWARE</size>\n<size=32>かたあはれ</size>",
    "<size=30>2166年8月15日 18時35分　<voffset=0.75em><size=15>ロンドン</size></voffset><space=-2em>倫敦</size>"
};
cards.arraySize = text.Length;
for (int i = 0; i < text.Length; i++) cards.GetArrayElementAtIndex(i).stringValue = text[i];
so.ApplyModifiedPropertiesWithoutUndo();
var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
var check = new SerializedObject(flowGo.GetComponent(System.Type.GetType("HalfAware.RoomIntroDirector, HalfAware")));
var report = "cards=" + check.FindProperty("blinkCards").arraySize;
for (int i = 0; i < check.FindProperty("blinkCards").arraySize; i++)
    report += "\n  [" + i + "] " + check.FindProperty("blinkCards").GetArrayElementAtIndex(i).stringValue;
var d = flowGo.GetComponent<HalfAware.RoomIntroDirector>();
report += "\nsmokeSeconds=" + d.SmokeSeconds.ToString("0.00");
return report;
```

Expected: `cards=3`、3 枚の文面がそのまま読め、`smokeSeconds=8.99`。中央の文字の基準の大きさは 40 なので、タイトルは 64、日付は 30、ふりがなは 15 になる。

- [ ] **Step 4: 文面が壊れていないか確かめる**

Step 3 の戻り値を、シナリオ設計書 4.1 の 3 と 1 文字ずつ突き合わせる。`制作 : yoko`、`HALF AWARE`、`かたあはれ`、`2166年8月15日 18時35分　倫敦`、ふりがなの `ロンドン`。全角の空白（年月日と時刻のあいだではなく、`18時35分` と `倫敦` のあいだ）が潰れていないこと。

- [ ] **Step 5: コミット**

```bash
cd /d/work/kataaware && git status --short && git add unity/Assets/Scenes/Room.unity && git commit -m "feat: put the eyelids and the three cards in the room"
```

---

### Task 4: 再生での確認

**Files:** なし

- [ ] **Step 1: 再生に入る**

`read_console`（`action: "clear"`）→ `EditorApplication.isPlaying` が `False` を確かめる → `manage_editor`（`action: "play"`）→ `execute_code` で `Application.runInBackground = true;` → `read_console`（`types: ["error", "warning"]`）。
Expected: エラー 0。

- [ ] **Step 2: 冒頭にカードが出ないことを見る**

```csharp
var center = GameObject.Find("Hud").transform.Find("Center");
var band = GameObject.Find("Hud").transform.Find("SubtitleBand");
var flow = GameObject.Find("SceneFlow").GetComponent<HalfAware.SceneFlow>();
return "t=" + Time.time.ToString("0.0")
    + " center=[" + center.GetComponent<TMPro.TextMeshProUGUI>().text + "]"
    + " frozen=" + flow.Frozen
    + " band=" + band.gameObject.activeSelf
    + " [" + band.Find("Subtitle").GetComponent<TMPro.TextMeshProUGUI>().text + "]";
```

Expected: `center=[]`、`frozen=False`、字幕が `[うぅ…今回は酔いが酷い…]`。冒頭でカードが出ず、操作も止まっていないこと。

- [ ] **Step 3: ジャックを抜いて煙草まで進める**

字幕を `SendMessage("PressInteract")` で送り切る。カメラを下へ向けるには `PlayerController.Pitch` に書く:

```csharp
var pc = GameObject.Find("Player").GetComponent<HalfAware.PlayerController>();
pc.Pitch = -55f;
return "pitch=" + pc.Pitch;
```

印が `E  インプラントジャックを抜く` になったら `PressInteract` で抜き、字幕を送り切る。次にカメラを戻して卓の方を向く:

```csharp
var pc = GameObject.Find("Player").GetComponent<HalfAware.PlayerController>();
pc.Pitch = 0f;
var player = GameObject.Find("Player");
var cc = player.GetComponent<CharacterController>();
cc.enabled = false;
player.transform.rotation = Quaternion.LookRotation(new Vector3(1f, 0f, -0.3f));
cc.enabled = true;
return "facing the side table";
```

印が `E  煙草を取る` になったら `PressInteract`。

- [ ] **Step 4: 3 回の瞬きとカードを撮る**

煙草を取った直後から、瞼とカードを繰り返し読む:

```csharp
var hudGo = GameObject.Find("Hud");
var top = hudGo.transform.Find("EyelidTop");
var center = hudGo.transform.Find("Center");
var rect = top.GetComponent<RectTransform>();
var flow = GameObject.Find("SceneFlow").GetComponent<HalfAware.SceneFlow>();
return "t=" + Time.time.ToString("0.0")
    + " lidShown=" + top.gameObject.activeSelf
    + " closed=" + ((1f - rect.anchorMin.y) * 2f).ToString("0.00")
    + " center=[" + center.GetComponent<TMPro.TextMeshProUGUI>().text + "]"
    + " frozen=" + flow.Frozen;
```

`closed` が 0 から 1 へ動き、1 のときにカードが出ていること。3 枚とも順に出ること。1 往復が遅いので `Time.timeScale = 0.2f` に落として観察し、終わったら 1 に戻す。

瞼が閉じきってカードが出ている瞬間に、3 枚それぞれ写真を撮る。`Temp/blink-01-credit.png`、`Temp/blink-02-title.png`、`Temp/blink-03-place.png`。Read で見て、次を確かめる:
- 画面の上下から黒い帯が中央で合わさっていること
- カードの文字が読めること。1 枚目は制作の行、2 枚目はタイトルが日本語題より大きいこと、3 枚目は日付と場所で、「倫敦」の上に小さく「ロンドン」が乗り、位置が「倫敦」の真上に来ていること
- 文字化けや豆腐（□）が無いこと

ふりがなの位置がずれていたら、直さずに実際の見え方を報告する。`<space=-2em>` の数値で合わせるため、親セッションが判断する。

- [ ] **Step 5: 吸い終わりまで通す**

3 回目の瞬きが明けた後、字幕が `[煙草が切れた…買いに行くついでに今日のメモリも売っちゃおう]` になり、`frozen=False` に戻り、その後 `PlayerController.CanMove` が `True`、`EyeHeight` が `1.60` になること。

- [ ] **Step 6: 止めて後始末**

`Time.timeScale = 1f; Application.runInBackground = false;` → `manage_editor`（`action: "stop"`）→ `read_console`（`types: ["error"]`）。

```bash
cd /d/work/kataaware && git status --short && grep -n "runInBackground" unity/ProjectSettings/ProjectSettings.asset
```

Expected: エラー 0、`runInBackground: 0`、作業ツリーはきれい。

- [ ] **Step 7: オーナーに手元の確認を頼む**

（親セッションが行う。あなたは実施しない）

---

## 持ち越すこと

- 瞼の縁を丸くするか。今は真っ直ぐな帯で、本物の瞼のような曲線ではない。実際に見てから決める
- 瞬きに合わせた音（瞼の音、煙草を吸う音）。音の段で入れる
- カードの出入りをぼかすか。今は瞼が閉じきった瞬間に出て、開く直前に消える
- 終わりのクレジット。素材の出典とライセンスの置き場が要る。書き出しの段までに決める
- クレジットの名義は `制作 : yoko` で確定。3 枚目の日付はチップのラベルと紙ばさみのメモから作中の当日として定めたもの
