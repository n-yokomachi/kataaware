# Unity 移行 段階 3: 場面 1 の流れ Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `docs/superpowers/specs/2026-09-16-unity-port-design.md` の 5 節・段階 3 を実装する。`docs/superpowers/specs/2026-09-16-scenario-design.md` の 4 節に沿って、座位で始まりジャックを抜き、煙草を吸ってから立ち上がり、メモリハブ・端末・ドアと進んで暗転する流れを段階 2 の骨組みに載せる。文面は `RoomScript`（ScriptableObject）に移す。

**Architecture:** 判定と時間の計算（文面の検索、眩暈の保持と減衰、立ち上がりの補間）は MonoBehaviour に依存しない純粋な C# にして EditMode テストで確かめる。場面の進行は `SceneFlow` に集め、場面 1 固有の演出（クレジット、タイトル、煙草の自動進行）は `RoomIntroDirector` に分け、`SceneFlow.Examined` と `Say` / `Freeze` だけで繋ぐ。位置と規則はシーンに、文面はアセットに置き、どちらか一方だけを触れるようにする。

**Tech Stack:** Unity 6000.3.24f1、URP 17.3、Input System 1.20、TextMeshPro（com.unity.ugui 2.0）、Unity Test Framework 1.6（NUnit）、MCP for Unity 10.0。

**実行時の注意:**
- コミットメッセージにモデル名・ツール名を著者として入れない
- subagent のモデルは `opus` か `sonnet` で毎回明示する
- `HANDOFF.md` は作らない。`docs/` はこのファイルのチェックボックス以外触らない
- C# ファイルを書いたら `mcp__UnityMCP__refresh_unity`（`mode: "force"`, `scope: "all"`, `compile: "request"`, `wait_for_ready: true`）→ `mcp__UnityMCP__read_console`（`types: ["error", "warning"]`）で 0 件を確かめてから次へ進む
- EditMode テストは `mcp__UnityMCP__run_tests`（`mode: "EditMode"`, `assembly_names: ["HalfAware.Tests.EditMode"]`, `include_failed_tests: true`）→ `mcp__UnityMCP__get_test_job`（`wait_timeout: 60`, `include_failed_tests: true`）
- `execute_code` に渡す C# は C# 6 の範囲で書く（`out var`、タプル、ローカル関数、パターンマッチ、文字列補間は使わない）。`UnityEngine` と `UnityEditor` 以外の型は名前空間つきで書く。`AssetDatabase.DeleteAsset` などが安全確認に引っかかったら `safety_checks: false` で再実行し、報告に書く
- **フォントのアトラスをコミットに入れない**: `unity/Assets/Fonts/NotoSansJP-Regular SDF.asset` は動的アトラスなので、エディタで日本語を表示するたびにファイルが約 2 MB に膨らむ。ビルド時には破棄されるので履歴に入れる価値が無い。`git status` にこのファイルが出たら `git checkout -- "unity/Assets/Fonts/NotoSansJP-Regular SDF.asset"` で戻してからコミットする
- **MCP から再生するとき**は、エディタの窓が前面に無いと `Application.runInBackground` が false のままで `Update` が走らず、2 フレーム目で止まって見える。実行中に `Application.runInBackground = true` を立てれば進む。この代入は `PlayerSettings` 側にも書かれるので、確認の後に false へ戻し、`unity/ProjectSettings/ProjectSettings.asset` に差分が無いことを確かめる
- 座標: Unity は +Z が正面。試作（`prototype-three/src/data/scenes/room.ts`）の値は Z の符号を反転して使う
- **YAML の中の日本語は `grep` で探せない**: Unity はシーンにもアセットにも日本語を `\uXXXX` の逃がし表記で書き、長い文字列は字下げして折り返す。文面が入ったかを確かめるときは、文字列をそのまま探さず、`execute_code` で読み返すか、ファイル側なら折り返しを畳んで逃がし表記を戻してから照合する
- オーナーがエディタで並行して触ることがある。シーンを変える前に `mcp__UnityMCP__manage_scene`（`action: "get_hierarchy"`）で現状を見て、知らない物があれば親セッションに戻す
- 各タスクの終わりでシーンは通しで遊べる状態を保つ。新しい部品は既定値が段階 2 と同じ挙動になるようにしてある

---

## ファイル構成

| パス（`unity/` 以下） | 変更 | 責務 |
|---|---|---|
| `Assets/Scripts/Data/RoomScript.cs` | 新規 | 場面の文面（印、本文、前提が未達のときの文）を id で引く ScriptableObject |
| `Assets/Scripts/Data/ScriptEntry.cs` | 新規 | 文面 1 件分の型と、id での検索（純粋な C#） |
| `Assets/Data/RoomScript.asset` | 新規 | 場面 1 の文面 |
| `Assets/Scripts/Interaction/Interactable.cs` | 変更 | 文面を `RoomScript` から読む。位置と規則だけを持つ |
| `Assets/Scripts/Fx/Daze.cs` | 新規 | 眩暈の強さの保持と減衰（純粋な C#） |
| `Assets/Scripts/Fx/DazeVolume.cs` | 新規 | `Daze` をシーンに置く容れ物。段階 4 の後処理がここから数値を読む |
| `Assets/Scripts/Player/StandUp.cs` | 新規 | 座位から立位への目線の補間（純粋な C#） |
| `Assets/Scripts/Hud/HudView.cs` | 変更 | 暗転の層と煙の層を足す |
| `Assets/Scripts/Flow/SceneFlow.cs` | 変更 | 停止、座位と立ち上がり、眩暈の保持と解放、`Examined`、`Say`、暗転して「続く」 |
| `Assets/Scripts/Flow/RoomIntroDirector.cs` | 新規 | クレジットとタイトル、最初の独白、煙草の自動進行 |
| `Assets/Tests/EditMode/ScriptEntryTests.cs` | 新規 | 文面の検索のテスト |
| `Assets/Tests/EditMode/DazeTests.cs` | 新規 | 眩暈の保持と減衰のテスト |
| `Assets/Tests/EditMode/StandUpTests.cs` | 新規 | 立ち上がりのテスト |
| `Assets/Scenes/Room.unity` | 変更 | ジャック・煙草・煙草の箱の追加、椅子とジャックの仮の箱、HUD の層、演出の配線 |

設計書 4 節の表との対応: `SceneDirector`（場面の切り替え）は場面 2 を足す段まで作らない。`RoomScript` は設計書どおり文面だけを持ち、位置と前提はシーンに残す。

---

### Task 1: `ScriptEntry` と `RoomScript`

文面を id で引く仕組みを、検索だけ純粋な C# にして先に作る。この時点ではまだ誰も使わない。

**Files:**
- Create: `unity/Assets/Scripts/Data/ScriptEntry.cs`
- Create: `unity/Assets/Scripts/Data/RoomScript.cs`
- Test: `unity/Assets/Tests/EditMode/ScriptEntryTests.cs`

- [x] **Step 1: 失敗するテストを書く**

`unity/Assets/Tests/EditMode/ScriptEntryTests.cs`:

```csharp
using NUnit.Framework;

namespace HalfAware.Tests
{
    public class ScriptEntryTests
    {
        static ScriptEntry[] Entries()
        {
            var jack = new ScriptEntry
            {
                id = "jack",
                label = "インプラントジャックを抜く",
                lines = new[] { "大小の差こそあれ、他人の記憶を観た後はいつもこうだ" },
                hints = new ScriptHint[0],
            };
            var door = new ScriptEntry
            {
                id = "door",
                label = "ドア",
                lines = new[] { "タバコを買いに行くついでに、今日のチップを売ってしまおう" },
                hints = new[]
                {
                    new ScriptHint { after = "chips", lines = new[] { "テーブルからチップを取ってこよう" } },
                },
            };
            return new[] { jack, door };
        }

        [Test]
        public void FindsAnEntryById()
        {
            Assert.That(ScriptEntry.Find(Entries(), "door").label, Is.EqualTo("ドア"));
        }

        [Test]
        public void ReturnsAnEmptyEntryForAnUnknownId()
        {
            var found = ScriptEntry.Find(Entries(), "nothing");
            Assert.That(found.id, Is.Null);
            Assert.That(found.Label, Is.EqualTo(""));
            Assert.That(found.Lines, Is.Empty);
        }

        [Test]
        public void LabelFallsBackToTheIdWhenItIsBlank()
        {
            var entry = new ScriptEntry { id = "ashtray", label = "", lines = null, hints = null };
            Assert.That(entry.Label, Is.EqualTo("ashtray"));
        }

        [Test]
        public void LinesAreNeverNull()
        {
            var entry = new ScriptEntry { id = "a", label = "a", lines = null, hints = null };
            Assert.That(entry.Lines, Is.Not.Null);
            Assert.That(entry.Lines, Is.Empty);
        }

        [Test]
        public void FindsTheHintOfAPrerequisite()
        {
            var door = ScriptEntry.Find(Entries(), "door");
            Assert.That(door.HintFor("chips"), Is.EqualTo(new[] { "テーブルからチップを取ってこよう" }));
        }

        [Test]
        public void ReturnsNullForAPrerequisiteWithoutAHint()
        {
            var door = ScriptEntry.Find(Entries(), "door");
            Assert.That(door.HintFor("terminal"), Is.Null);
            Assert.That(ScriptEntry.Find(Entries(), "jack").HintFor("x"), Is.Null);
        }

        [Test]
        public void TellsAnEntryWithNoLinesApartFromAMissingOne()
        {
            // 煙草は文を持たず、吸い終わりの独白を演出の側が言う。「在るが空」を「無い」と取り違えない
            var found = ScriptEntry.Find(new[] { new ScriptEntry { id = "cigarette" } }, "cigarette");
            Assert.That(found.id, Is.Not.Null);
            Assert.That(found.Lines, Is.Empty);
            Assert.That(ScriptEntry.Find(new[] { new ScriptEntry { id = "cigarette" } }, "other").id, Is.Null);
        }

        [Test]
        public void TakesTheFirstEntryWhenAnIdIsRepeated()
        {
            var first = new ScriptEntry { id = "a", label = "先", lines = null, hints = null };
            var second = new ScriptEntry { id = "a", label = "後", lines = null, hints = null };
            Assert.That(ScriptEntry.Find(new[] { first, second }, "a").label, Is.EqualTo("先"));
        }
    }
}
```

- [x] **Step 2: 失敗を確認**

`refresh_unity` → `read_console`（`types: ["error"]`）。
Expected: `ScriptEntry` と `ScriptHint` が見つからないというコンパイルエラー。

- [x] **Step 3: 型と検索を書く**

`unity/Assets/Scripts/Data/ScriptEntry.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace HalfAware
{
    /// <summary>前提が未達のときに出す文。出しても済んだことにはならない</summary>
    [Serializable]
    public struct ScriptHint
    {
        /// <summary>対象の after に挙げた id</summary>
        public string after;
        public string[] lines;
    }

    /// <summary>調べる対象 1 つ分の文面。位置と前提はシーンの Interactable が持つ</summary>
    [Serializable]
    public struct ScriptEntry
    {
        static readonly string[] NoLines = new string[0];

        public string id;
        /// <summary>印に出す短い文。空なら id を出す</summary>
        public string label;
        public string[] lines;
        public ScriptHint[] hints;

        public string Label => string.IsNullOrEmpty(label) ? (id ?? "") : label;

        public IReadOnlyList<string> Lines => lines ?? NoLines;

        /// <summary>afterId が未達のときに出す文。無ければ null</summary>
        public IReadOnlyList<string> HintFor(string afterId)
        {
            if (hints == null) return null;
            foreach (var hint in hints)
            {
                if (hint.after == afterId) return hint.lines;
            }
            return null;
        }

        /// <summary>id で引く。見つからなければ id が null の空の項目を返す</summary>
        public static ScriptEntry Find(IReadOnlyList<ScriptEntry> entries, string id)
        {
            if (entries != null)
            {
                for (var i = 0; i < entries.Count; i++)
                {
                    if (entries[i].id == id) return entries[i];
                }
            }
            return new ScriptEntry();
        }
    }
}
```

`unity/Assets/Scripts/Data/RoomScript.cs`:

```csharp
using UnityEngine;

namespace HalfAware
{
    /// <summary>場面 1 つ分の文面。シナリオの文をコードとシーンから分けて持つ</summary>
    [CreateAssetMenu(fileName = "RoomScript", menuName = "HalfAware/Room Script")]
    public sealed class RoomScript : ScriptableObject
    {
        [SerializeField] ScriptEntry[] entries = new ScriptEntry[0];

        /// <summary>id で引く。見つからなければ id が null の空の項目を返す</summary>
        public ScriptEntry Find(string id)
        {
            return ScriptEntry.Find(entries, id);
        }

        /// <summary>この文面が持つ id の一覧。シーンとの食い違いを調べるのに使う</summary>
        public string[] Ids()
        {
            var ids = new string[entries.Length];
            for (var i = 0; i < entries.Length; i++) ids[i] = entries[i].id;
            return ids;
        }
    }
}
```

`ScriptEntry` の `lines` と `ScriptHint` の `lines` は Inspector で 1 行ずつ編集する。複数行の入力欄にはしない（字幕は 1 行ずつ送るため、1 要素 = 1 行）。

- [x] **Step 4: テストが通ることを確認**

`refresh_unity` → `read_console`（エラー・警告 0）→ `run_tests` → `get_test_job`。
Expected: 35 件 passed（段階 2 の 27 件 + 8 件）、failed 0。

- [x] **Step 5: コミット**

```bash
cd /d/work/kataaware && git add unity/Assets/Scripts unity/Assets/Tests && git commit -m "feat: add the room script asset type and its lookup by id"
```

---

### Task 2: 場面 1 の文面のアセット

`docs/superpowers/specs/2026-09-16-scenario-design.md` の 4 節の文面を `RoomScript` のアセットに入れる。まだ誰も参照しないので、シーンの動きは変わらない。

**Files:**
- Create: `unity/Assets/Data/RoomScript.asset`

- [x] **Step 1: アセットを作る**

`execute_code`:

```csharp
if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
var path = "Assets/Data/RoomScript.asset";
var asset = AssetDatabase.LoadAssetAtPath<HalfAware.RoomScript>(path);
if (asset == null)
{
    asset = ScriptableObject.CreateInstance<HalfAware.RoomScript>();
    AssetDatabase.CreateAsset(asset, path);
}
var so = new SerializedObject(asset);
var entries = so.FindProperty("entries");
entries.arraySize = 8;
System.Action<int, string, string, string[]> put = (index, id, label, lines) => {
    var e = entries.GetArrayElementAtIndex(index);
    e.FindPropertyRelative("id").stringValue = id;
    e.FindPropertyRelative("label").stringValue = label;
    var l = e.FindPropertyRelative("lines");
    l.arraySize = lines.Length;
    for (int i = 0; i < lines.Length; i++) l.GetArrayElementAtIndex(i).stringValue = lines[i];
    e.FindPropertyRelative("hints").arraySize = 0;
};
System.Action<int, int, string, string[]> hint = (index, slot, after, lines) => {
    var h = entries.GetArrayElementAtIndex(index).FindPropertyRelative("hints");
    if (h.arraySize <= slot) h.arraySize = slot + 1;
    var item = h.GetArrayElementAtIndex(slot);
    item.FindPropertyRelative("after").stringValue = after;
    var l = item.FindPropertyRelative("lines");
    l.arraySize = lines.Length;
    for (int i = 0; i < lines.Length; i++) l.GetArrayElementAtIndex(i).stringValue = lines[i];
};

put(0, "jack", "インプラントジャックを抜く", new[] {
    "大小の差こそあれ、他人の記憶を観た後はいつもこうだ" });
// 煙草は取った後の自動演出が終わってから文が出る（RoomIntroDirector が言う）ので、ここは空
put(1, "cigarette", "煙草を取る", new string[0]);
put(2, "chips", "チップを抜く", new[] {
    "メモリが6枚。今日の分だ",
    "08/15 #1 男 41 『ディエゴ』 3分40秒",
    "08/15 #2 女 23 『ミア』 2分05秒",
    "08/15 #3 男 8 『ゆうと』 1分50秒",
    "08/15 #4 女 35 『阿明』 4分00秒",
    "08/15 #5 男 19 『リアム』 1分30秒",
    "08/15 #6 女 52 『マチルド』 3分55秒" });
put(3, "terminal", "端末", new[] {
    "世間ではホロコンソールが人気だが私はもっぱら物理モニターを使っている",
    "というのもほら、",
    "こうして反射で自分の顔が見られるからだ",
    "黒い髪に琥珀色の目。左目の下のほくろ",
    "滅多にないことだが、潜り込んだ他人の記憶が薬などでトリップしていると",
    "私まで影響を受ける",
    "抜け出した後の自己同定のために、鏡を見ることは大切だ",
    "スリープを解除すると、今日の記憶走査条件が表示される",
    "条件　名前を呼ばれた時刻",
    "対象　防壁なし　距離 ランダム　期間 2156年3月2日～2156年3月3日",
    "候補の簡易抽出　56,232,318" });
put(4, "door", "ドア", new[] {
    "タバコを買いに行くついでに、今日のチップを売ってしまおう" });
hint(4, 0, "chips", new[] { "テーブルからチップを取ってこよう" });
hint(4, 1, "terminal", new[] { "（仮）端末を確かめてからだ" });
put(5, "ashtray", "灰皿", new[] {
    "吸い殻がたまっている" });
put(6, "cigarette-box", "煙草の箱", new[] {
    "『双鶴（シュアンフー）』という名前の中国産煙草の箱。今はカラだ" });
put(7, "clipboard", "紙ばさみ", new[] {
    "売り上げのメモだ",
    "2166/08/13 5枚　2166/08/14 4枚　2166/08/15 ―" });

so.ApplyModifiedPropertiesWithoutUndo();
EditorUtility.SetDirty(asset);
AssetDatabase.SaveAssets();
AssetDatabase.Refresh();
return string.Join(",", asset.Ids());
```

Expected: 戻り値 `jack,cigarette,chips,terminal,door,ashtray,cigarette-box,clipboard`。`read_console` でエラー 0。

`hint(4, 1, ...)` の「（仮）」は、シナリオ設計書 6 節で「ドアを、チップを取った後・端末を確かめる前に調べたときの文」が未決のまま残っているため。文が決まるまでこの印を外さない。

- [x] **Step 2: 中身を読み返す**

`execute_code`:

```csharp
var asset = AssetDatabase.LoadAssetAtPath<HalfAware.RoomScript>("Assets/Data/RoomScript.asset");
if (asset == null) return "asset missing";
var report = "";
foreach (var id in asset.Ids())
{
    var e = asset.Find(id);
    report += e.id + " label=" + e.Label + " lines=" + e.Lines.Count + " hints=" + (e.hints == null ? 0 : e.hints.Length) + "\n";
}
var door = asset.Find("door");
report += "door hint(chips)=" + (door.HintFor("chips") != null ? door.HintFor("chips")[0] : "null") + "\n";
report += "door hint(terminal)=" + (door.HintFor("terminal") != null ? door.HintFor("terminal")[0] : "null") + "\n";
report += "terminal line 10=" + asset.Find("terminal").Lines[9];
return report;
```

Expected: `jack` は文 1 件、`cigarette` は 0 件、`chips` は 7 件、`terminal` は 11 件で `hints=0`、`door` は文 1 件で `hints=2`、`ashtray` と `cigarette-box` は 1 件、`clipboard` は 2 件。ドアの 2 つの文が読め、端末の 10 行目が `対象　防壁なし　距離 ランダム　期間 2156年3月2日～2156年3月3日` であること（全角の空白が潰れていないことの確認）。

- [x] **Step 3: コミット**

```bash
cd /d/work/kataaware && git add unity/Assets/Data unity/Assets/Data.meta && git commit -m "feat: add the scene 1 script with the scenario text"
```

---

### Task 3: `Interactable` が文面のアセットから読む

対象は位置と規則だけを持ち、印と文は `RoomScript` から引く。シーン上の 5 つの対象にアセットを繋ぎ、埋め込みの文面を落とす。

**Files:**
- Modify: `unity/Assets/Scripts/Interaction/Interactable.cs`
- Modify: `unity/Assets/Scenes/Room.unity`

- [x] **Step 1: `Interactable` を書き換える**

`unity/Assets/Scripts/Interaction/Interactable.cs` を全文次に置き換える:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// シーン上の調べる対象。位置はこの GameObject の位置。
    /// 印と文は RoomScript から id で引く。位置と規則はシーン、文面はアセットと分けてある
    /// </summary>
    public sealed class Interactable : MonoBehaviour, IInteractable
    {
        static readonly string[] NoLines = new string[0];

        [SerializeField] string id;
        [Tooltip("印と文を引く文面のアセット")]
        [SerializeField] RoomScript script;
        [SerializeField] float radius = InteractionPicker.DefaultRadius;
        [SerializeField] bool required;
        [SerializeField] bool once = true;
        [Tooltip("ここに挙げた id が済むまで選べない。文面に hint がある id については選べて、その文だけ出る")]
        [SerializeField] string[] after = new string[0];

        public string Id => id;
        public Vector3 Position => transform.position;
        public float Radius => radius;
        public bool Required => required;
        public bool Once => once;
        public IReadOnlyList<string> After => after;
        /// <summary>文面が引けないときは id を出す。印が空欄になって気づけないのを避ける</summary>
        public string Label
        {
            get
            {
                if (script == null) return id;
                var entry = script.Find(id);
                return entry.id != null ? entry.Label : id;
            }
        }

        public IReadOnlyList<string> Lines => script != null ? script.Find(id).Lines : NoLines;

        public IReadOnlyList<string> HintFor(string afterId)
        {
            return script != null ? script.Find(id).HintFor(afterId) : null;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = required ? new Color(1f, 0.6f, 0.2f) : new Color(0.6f, 0.8f, 1f);
            Gizmos.DrawWireSphere(transform.position, 0.1f);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (string.IsNullOrEmpty(id)) Debug.LogWarning("Interactable に id がない: " + name, this);
            else if (script == null) Debug.LogWarning("Interactable に文面のアセットがない: " + name, this);
            else if (script.Find(id).id == null) Debug.LogWarning("文面に id が無い: " + id, this);
        }
#endif
    }
}
```

`Label` は選ばれた対象 1 つにつき 1 フレーム 1 回、`Lines` と `HintFor` は調べたときだけ引かれる。文面は 8 件なので線形の検索で足りる。

- [x] **Step 2: コンパイルを確認**

`refresh_unity` → `read_console`（`types: ["error"]`）。
Expected: エラー 0。文面のアセットをまだ繋いでいないので、`OnValidate` の警告「Interactable に文面のアセットがない」がシーンの対象 5 件それぞれに 1 回出る。これは Step 3 で消える。

- [x] **Step 3: シーンの対象に文面のアセットを繋ぐ**

`execute_code`:

```csharp
var script = AssetDatabase.LoadAssetAtPath<HalfAware.RoomScript>("Assets/Data/RoomScript.asset");
if (script == null) return "script asset missing";
var items = UnityEngine.Object.FindObjectsByType(System.Type.GetType("HalfAware.Interactable, HalfAware"), FindObjectsSortMode.InstanceID);
var report = "";
foreach (var it in items)
{
    var so = new SerializedObject(it);
    so.FindProperty("script").objectReferenceValue = script;
    so.ApplyModifiedPropertiesWithoutUndo();
    var comp = it as Component;
    var read = comp as HalfAware.IInteractable;
    report += read.Id + " label=" + read.Label + " lines=" + read.Lines.Count + "\n";
}
var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return report + "saved " + items.Length;
```

Expected: 5 件それぞれに文面から引いた印と件数が出る（`chips label=チップを抜く lines=7`、`terminal label=端末 lines=11`、`door label=ドア lines=1`、`clipboard label=紙ばさみ lines=2`、`ashtray label=灰皿 lines=1`）、最後に `saved 5`。`read_console` でエラー 0。

- [x] **Step 4: シーンから古い文面が消えたことを確かめる**

```bash
cd /d/work/kataaware && echo "label keys: $(grep -cE '^  label: ' unity/Assets/Scenes/Room.unity)"; echo "lines keys: $(grep -cE '^  lines:' unity/Assets/Scenes/Room.unity)"; echo "hints keys: $(grep -cE '^  hints:' unity/Assets/Scenes/Room.unity)"; echo "script refs: $(grep -c 041d521c5151e274996d894e143d3cb2 unity/Assets/Scenes/Room.unity)"
```

Expected: `label` / `lines` / `hints` の 3 つが 0（埋め込みの文面が落ちた。この作業の前はそれぞれ 5）、`script refs` が 5（5 つの対象それぞれに文面のアセットへの参照が入った）。`041d52...` は `unity/Assets/Data/RoomScript.asset.meta` の `guid`。

- [x] **Step 5: テストとコミット**

`run_tests` → `get_test_job`。Expected: 35 件 passed。

```bash
cd /d/work/kataaware && git status --short && git add unity/Assets/Scripts/Interaction/Interactable.cs unity/Assets/Scenes/Room.unity && git commit -m "feat: read the label and the lines of an interactable from the room script"
```

`git status` に `unity/Assets/Fonts/NotoSansJP-Regular SDF.asset` が出ていたら、コミット前に `git checkout -- "unity/Assets/Fonts/NotoSansJP-Regular SDF.asset"` で戻す。

---

### Task 4: `Daze` と `DazeVolume`

眩暈の強さを持ち、対象を調べるまで保ち、その後は時間で 0 まで減らす。段階 3 では数値だけで、画面の見え方は段階 4 で繋ぐ。

**Files:**
- Create: `unity/Assets/Scripts/Fx/Daze.cs`
- Create: `unity/Assets/Scripts/Fx/DazeVolume.cs`
- Test: `unity/Assets/Tests/EditMode/DazeTests.cs`

- [x] **Step 1: 失敗するテストを書く**

`unity/Assets/Tests/EditMode/DazeTests.cs`:

```csharp
using NUnit.Framework;

namespace HalfAware.Tests
{
    public class DazeTests
    {
        [Test]
        public void StartsClear()
        {
            var daze = new Daze();
            Assert.That(daze.Blur, Is.EqualTo(0f));
            Assert.That(daze.Wobble, Is.EqualTo(0f));
            Assert.That(daze.IsClear, Is.True);
        }

        [Test]
        public void HoldKeepsTheStrengthAcrossTicks()
        {
            var daze = new Daze();
            daze.Hold(1f, 0.5f);
            daze.Tick(10f);
            Assert.That(daze.Blur, Is.EqualTo(1f));
            Assert.That(daze.Wobble, Is.EqualTo(0.5f));
            Assert.That(daze.IsClear, Is.False);
        }

        [Test]
        public void DecayFallsLinearlyToZero()
        {
            var daze = new Daze();
            daze.Decay(1f, 0.4f, 4f);
            Assert.That(daze.Blur, Is.EqualTo(1f));
            daze.Tick(1f);
            Assert.That(daze.Blur, Is.EqualTo(0.75f).Within(1e-4f));
            Assert.That(daze.Wobble, Is.EqualTo(0.3f).Within(1e-4f));
            daze.Tick(2f);
            Assert.That(daze.Blur, Is.EqualTo(0.25f).Within(1e-4f));
            daze.Tick(1f);
            Assert.That(daze.IsClear, Is.True);
        }

        [Test]
        public void DecayStaysAtZeroAfterTheEnd()
        {
            var daze = new Daze();
            daze.Decay(1f, 1f, 2f);
            daze.Tick(5f);
            daze.Tick(5f);
            Assert.That(daze.Blur, Is.EqualTo(0f));
            Assert.That(daze.Wobble, Is.EqualTo(0f));
        }

        [Test]
        public void DecayWithoutTimeClearsAtOnce()
        {
            var daze = new Daze();
            daze.Hold(1f, 1f);
            daze.Decay(1f, 1f, 0f);
            Assert.That(daze.IsClear, Is.True);
        }

        [Test]
        public void HoldAfterDecayStopsTheFall()
        {
            var daze = new Daze();
            daze.Decay(1f, 1f, 4f);
            daze.Tick(2f);
            daze.Hold(1f, 1f);
            daze.Tick(10f);
            Assert.That(daze.Blur, Is.EqualTo(1f));
        }

        [Test]
        public void ClearStopsEverything()
        {
            var daze = new Daze();
            daze.Decay(1f, 1f, 4f);
            daze.Clear();
            Assert.That(daze.IsClear, Is.True);
            daze.Tick(1f);
            Assert.That(daze.IsClear, Is.True);
        }
    }
}
```

- [x] **Step 2: 失敗を確認**

`refresh_unity` → `read_console`（`types: ["error"]`）。
Expected: `Daze` が見つからないというコンパイルエラー。

- [x] **Step 3: 実装**

`unity/Assets/Scripts/Fx/Daze.cs`:

```csharp
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 眩暈の強さ。ぼかしと輪郭の揺れを 0〜1 で持つ。
    /// Hold で保ち、Decay で時間をかけて 0 にする。見え方は段階 4 の後処理が受け持つ
    /// </summary>
    public sealed class Daze
    {
        float fromBlur;
        float fromWobble;
        float total;
        float left;

        public float Blur { get; private set; }
        public float Wobble { get; private set; }

        public bool IsClear => Blur <= 0f && Wobble <= 0f;

        /// <summary>この強さのまま保つ。Tick では減らない</summary>
        public void Hold(float blur, float wobble)
        {
            total = 0f;
            left = 0f;
            Blur = blur;
            Wobble = wobble;
        }

        /// <summary>この強さから seconds 秒かけて 0 にする。seconds が 0 以下なら即 0</summary>
        public void Decay(float blur, float wobble, float seconds)
        {
            if (seconds <= 0f)
            {
                Clear();
                return;
            }
            fromBlur = blur;
            fromWobble = wobble;
            total = seconds;
            left = seconds;
            Blur = blur;
            Wobble = wobble;
        }

        public void Clear()
        {
            Hold(0f, 0f);
        }

        public void Tick(float dt)
        {
            if (total <= 0f) return;
            left = Mathf.Max(0f, left - dt);
            var k = left / total;
            Blur = fromBlur * k;
            Wobble = fromWobble * k;
            if (left <= 0f) total = 0f;
        }
    }
}
```

`unity/Assets/Scripts/Fx/DazeVolume.cs`:

```csharp
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 眩暈の強さをシーンに置く。SceneFlow が Hold と Decay を呼び、段階 4 の後処理がここから数値を読む。
    /// SceneFlow より後に Update が走るよう実行順を後ろに置く
    /// </summary>
    [DefaultExecutionOrder(10)]
    public sealed class DazeVolume : MonoBehaviour
    {
        readonly Daze daze = new Daze();

        public float Blur => daze.Blur;
        public float Wobble => daze.Wobble;
        public bool IsClear => daze.IsClear;

        public void Hold(float blur, float wobble) => daze.Hold(blur, wobble);

        public void Decay(float blur, float wobble, float seconds) => daze.Decay(blur, wobble, seconds);

        public void Clear() => daze.Clear();

        void Update() => daze.Tick(Time.deltaTime);
    }
}
```

- [x] **Step 4: テストが通ることを確認**

`refresh_unity` → `read_console`（エラー・警告 0）→ `run_tests` → `get_test_job`。
Expected: 42 件 passed（35 + 7 件）、failed 0。

- [x] **Step 5: コミット**

```bash
cd /d/work/kataaware && git add unity/Assets/Scripts unity/Assets/Tests && git commit -m "feat: add the daze strength with its hold and decay"
```

---

### Task 5: `StandUp`

座位から立位へ目線を上げる。合図が済むまで座ったまま、止まっている間は立ち上がり始めない。

**Files:**
- Create: `unity/Assets/Scripts/Player/StandUp.cs`
- Test: `unity/Assets/Tests/EditMode/StandUpTests.cs`

- [x] **Step 1: 失敗するテストを書く**

`unity/Assets/Tests/EditMode/StandUpTests.cs`:

```csharp
using NUnit.Framework;

namespace HalfAware.Tests
{
    public class StandUpTests
    {
        static StandUp Seated() => new StandUp(1.1f, 1.6f, 0.6f);

        [Test]
        public void StartsSeated()
        {
            var s = Seated();
            Assert.That(s.EyeHeight, Is.EqualTo(1.1f));
            Assert.That(s.Standing, Is.False);
        }

        [Test]
        public void StaysSeatedUntilReleased()
        {
            var s = Seated();
            s.Tick(1f, false, false);
            Assert.That(s.EyeHeight, Is.EqualTo(1.1f));
            Assert.That(s.Standing, Is.False);
        }

        [Test]
        public void DoesNotStartWhileFrozen()
        {
            var s = Seated();
            s.Tick(1f, true, true);
            Assert.That(s.EyeHeight, Is.EqualTo(1.1f));
            Assert.That(s.Standing, Is.False);
        }

        [Test]
        public void RisesOverTheGivenSeconds()
        {
            var s = Seated();
            s.Tick(0.3f, true, false);
            Assert.That(s.EyeHeight, Is.EqualTo(1.35f).Within(1e-4f));
            Assert.That(s.Standing, Is.False);
            s.Tick(0.3f, true, false);
            Assert.That(s.EyeHeight, Is.EqualTo(1.6f).Within(1e-4f));
            Assert.That(s.Standing, Is.True);
        }

        [Test]
        public void KeepsRisingOnceStartedEvenIfFrozen()
        {
            var s = Seated();
            s.Tick(0.3f, true, false);
            s.Tick(0.3f, true, true);
            Assert.That(s.Standing, Is.True);
            Assert.That(s.EyeHeight, Is.EqualTo(1.6f).Within(1e-4f));
        }

        [Test]
        public void DoesNotOvershoot()
        {
            var s = Seated();
            s.Tick(10f, true, false);
            Assert.That(s.EyeHeight, Is.EqualTo(1.6f));
            s.Tick(10f, true, false);
            Assert.That(s.EyeHeight, Is.EqualTo(1.6f));
        }

        [Test]
        public void RisesAtOnceWithoutTime()
        {
            var s = new StandUp(1.1f, 1.6f, 0f);
            s.Tick(0.016f, true, false);
            Assert.That(s.EyeHeight, Is.EqualTo(1.6f));
            Assert.That(s.Standing, Is.True);
        }
    }
}
```

- [x] **Step 2: 失敗を確認**

`refresh_unity` → `read_console`（`types: ["error"]`）。
Expected: `StandUp` が見つからないというコンパイルエラー。

- [x] **Step 3: 実装**

`unity/Assets/Scripts/Player/StandUp.cs`:

```csharp
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 座位から立位への目線の移り変わり。released が立ったら上がり始め、seconds かけて立位の高さになる。
    /// 止まっている間は上がり始めない。一度始まったら止まらない
    /// </summary>
    public sealed class StandUp
    {
        readonly float seat;
        readonly float standing;
        readonly float seconds;
        /// <summary>立ち上がり始めてからの秒数。負のあいだはまだ始まっていない</summary>
        float elapsed = -1f;

        public StandUp(float seatEyeHeight, float standingEyeHeight, float seconds)
        {
            seat = seatEyeHeight;
            standing = standingEyeHeight;
            this.seconds = seconds;
            EyeHeight = seatEyeHeight;
        }

        public float EyeHeight { get; private set; }

        /// <summary>立ち終わったか。true になったら移動を許してよい</summary>
        public bool Standing { get; private set; }

        public void Tick(float dt, bool released, bool frozen)
        {
            if (Standing) return;
            if (elapsed < 0f)
            {
                if (frozen || !released) return;
                elapsed = 0f;
            }
            elapsed += dt;
            var k = seconds <= 0f ? 1f : Mathf.Min(1f, elapsed / seconds);
            EyeHeight = seat + (standing - seat) * k;
            if (k >= 1f)
            {
                EyeHeight = standing;
                Standing = true;
            }
        }
    }
}
```

- [x] **Step 4: テストが通ることを確認**

`refresh_unity` → `read_console`（エラー・警告 0）→ `run_tests` → `get_test_job`。
Expected: 49 件 passed（42 + 7 件）、failed 0。

- [x] **Step 5: コミット**

```bash
cd /d/work/kataaware && git add unity/Assets/Scripts unity/Assets/Tests && git commit -m "feat: add the rise from the seat to standing"
```

---

### Task 6: `HudView` に暗転と煙を足す

**Files:**
- Modify: `unity/Assets/Scripts/Hud/HudView.cs`

- [x] **Step 1: 書き換える**

`unity/Assets/Scripts/Hud/HudView.cs` を全文次に置き換える:

```csharp
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HalfAware
{
    /// <summary>
    /// 字幕（画面下の黒帯）、印（中央）、中央の文字、暗転、煙。
    /// 見せるだけで、何をいつ出すかは SceneFlow と場面固有の演出が決める。
    /// 暗転と煙の層は、繋がっていなければ何もしない（段階を追って足すため）
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        /// <summary>煙が消えるのにかける秒数</summary>
        public const float SmokeFadeSeconds = 1f;

        [SerializeField] GameObject subtitleBand;
        [SerializeField] TMP_Text subtitleText;
        [SerializeField] TMP_Text promptText;
        [SerializeField] TMP_Text centerText;
        [Tooltip("画面全体の黒い層。暗転に使う")]
        [SerializeField] Image fadeLayer;
        [Tooltip("画面下から立ち上る煙。見た目は段階 4 で作り込む")]
        [SerializeField] Image smokeLayer;

        Coroutine smoking;

        void Awake()
        {
            SetSubtitle(null);
            SetPrompt(null);
            SetCenter(null);
            SetFade(0f);
            SetSmoke(0f);
        }

        /// <summary>null で黒帯ごと隠す</summary>
        public void SetSubtitle(string text)
        {
            subtitleBand.SetActive(text != null);
            subtitleText.text = text ?? string.Empty;
        }

        /// <summary>null で隠す</summary>
        public void SetPrompt(string text)
        {
            promptText.gameObject.SetActive(text != null);
            promptText.text = text ?? string.Empty;
        }

        /// <summary>null で隠す</summary>
        public void SetCenter(string text)
        {
            centerText.gameObject.SetActive(text != null);
            centerText.text = text ?? string.Empty;
        }

        /// <summary>黒い層の濃さ。0 で透明、1 で真っ黒</summary>
        public void SetFade(float alpha)
        {
            SetAlpha(fadeLayer, alpha);
        }

        /// <summary>seconds 秒かけて黒い層の濃さを変える</summary>
        public IEnumerator FadeTo(float alpha, float seconds)
        {
            if (fadeLayer == null || seconds <= 0f)
            {
                SetFade(alpha);
                yield break;
            }
            var from = fadeLayer.color.a;
            for (var t = 0f; t < seconds; t += Time.deltaTime)
            {
                SetFade(Mathf.Lerp(from, alpha, t / seconds));
                yield return null;
            }
            SetFade(alpha);
        }

        /// <summary>煙を seconds 秒立ち上らせ、その後 SmokeFadeSeconds 秒かけて消す</summary>
        public void ShowSmoke(float seconds)
        {
            CancelSmoke();
            if (smokeLayer == null) return;
            smoking = StartCoroutine(Smoke(seconds));
        }

        /// <summary>進行中の煙を止めて消す。場面が終わるときに呼ぶ</summary>
        public void CancelSmoke()
        {
            if (smoking != null)
            {
                StopCoroutine(smoking);
                smoking = null;
            }
            SetSmoke(0f);
        }

        IEnumerator Smoke(float seconds)
        {
            for (var t = 0f; t < SmokeFadeSeconds; t += Time.deltaTime)
            {
                SetSmoke(t / SmokeFadeSeconds);
                yield return null;
            }
            SetSmoke(1f);
            yield return new WaitForSeconds(Mathf.Max(0f, seconds - SmokeFadeSeconds));
            for (var t = 0f; t < SmokeFadeSeconds; t += Time.deltaTime)
            {
                SetSmoke(1f - t / SmokeFadeSeconds);
                yield return null;
            }
            SetSmoke(0f);
            smoking = null;
        }

        void SetSmoke(float alpha)
        {
            SetAlpha(smokeLayer, alpha);
        }

        static void SetAlpha(Image layer, float alpha)
        {
            if (layer == null) return;
            var color = layer.color;
            color.a = alpha;
            layer.color = color;
            layer.gameObject.SetActive(alpha > 0f);
        }
    }
}
```

- [x] **Step 2: コンパイルとテストを確認**

`refresh_unity` → `read_console`（エラー・警告 0）→ `run_tests` → `get_test_job`。
Expected: 49 件 passed。

- [x] **Step 3: コミット**

```bash
cd /d/work/kataaware && git add unity/Assets/Scripts/Hud/HudView.cs && git commit -m "feat: add the fade layer and the smoke layer to the HUD"
```

---

### Task 7: `SceneFlow` に停止・座位・眩暈・暗転を足す

**Files:**
- Modify: `unity/Assets/Scripts/Flow/SceneFlow.cs`

- [x] **Step 1: 書き換える**

`unity/Assets/Scripts/Flow/SceneFlow.cs` を全文次に置き換える:

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 歩いて調べる場面 1 つ分の進行。毎フレーム、対象の選択 → 印 → 調べる → 字幕 → 完了の判定の順に進める。
    /// 字幕の表示中は E とクリックを字幕の送りにだけ使い、調べる操作は受け付けない。
    /// 止まっている間は調べる操作も進行も止まり、見回しだけできる。
    /// 必須の対象をすべて調べ、字幕も出ておらず、止まってもいなければ暗転して「続く」を出す
    /// </summary>
    public sealed class SceneFlow : MonoBehaviour
    {
        public const string ToBeContinued = "（仮）続く";
        /// <summary>暗転にかける秒数</summary>
        public const float FadeSeconds = 1.5f;
        /// <summary>座位から立位へ目線を上げる秒数</summary>
        public const float StandSeconds = 0.6f;

        [SerializeField] PlayerController player;
        [SerializeField] HudView hud;
        [Tooltip("眩暈の強さの容れ物。無ければ眩暈を使わない")]
        [SerializeField] DazeVolume daze;
        [Tooltip("ラジアン。視線からこの角度以内の対象だけ選ぶ")]
        [SerializeField] float maxAngle = InteractionPicker.MaxAngle;

        [Header("座って始める")]
        [Tooltip("座っているときの目線の高さ")]
        [SerializeField] float seatEyeHeight = 1.1f;
        [Tooltip("この id を調べると立ち上がって移動できるようになる。空なら最初から立っている")]
        [SerializeField] string standAfter = "";

        [Header("入ったときの眩暈")]
        [SerializeField] float dazeBlur = 1f;
        [SerializeField] float dazeWobble = 1f;
        [Tooltip("秒。解放してからこの時間で 0 になる")]
        [SerializeField] float dazeSeconds = 5f;
        [Tooltip("この id を調べるまで眩暈を保つ。空なら入った時点から消え始める")]
        [SerializeField] string dazeUntil = "";

        readonly SubtitleQueue subtitles = new SubtitleQueue();
        List<IInteractable> items;
        SceneProgress progress;
        StandUp standUp;
        bool pendingInteract;
        float frozenUntil;
        bool dazeReleased;

        /// <summary>対象を調べて済んだ直後。前提が未達で文だけ出たときは呼ばない</summary>
        public event Action<IInteractable> Examined;

        public SceneProgress Progress => progress;
        public bool Completed { get; private set; }

        /// <summary>調べる操作と進行が止まっているか。見回しは止めない</summary>
        public bool Frozen => Time.time < frozenUntil;

        void Awake()
        {
            if (player == null || hud == null)
            {
                Debug.LogError("SceneFlow: player か hud が未接続", this);
                enabled = false;
                return;
            }
            items = new List<IInteractable>(FindObjectsByType<Interactable>(FindObjectsSortMode.InstanceID));
            if (items.Count == 0) Debug.LogWarning("SceneFlow: 調べる対象が 1 つも見つからない", this);
            progress = new SceneProgress(items);
            if (standAfter.Length > 0)
            {
                standUp = new StandUp(seatEyeHeight, PlayerController.StandingEyeHeight, StandSeconds);
                player.CanMove = false;
                player.EyeHeight = seatEyeHeight;
            }
            if (daze != null && dazeUntil.Length > 0) daze.Hold(dazeBlur, dazeWobble);
        }

        /// <summary>次のフレームで調べる操作を 1 回起こす。E キーの代わりに、再生中の動作確認から SendMessage で呼ぶ</summary>
        public void PressInteract() => pendingInteract = true;

        /// <summary>字幕を積む。場面固有の演出から呼ぶ</summary>
        public void Say(IReadOnlyList<string> lines) => subtitles.Enqueue(lines);

        /// <summary>seconds 秒のあいだ、調べる操作と進行を止める。すでに止まっているときは長い方を採る</summary>
        public void Freeze(float seconds) => frozenUntil = Mathf.Max(frozenUntil, Time.time + seconds);

        void Update()
        {
            if (Completed) return;
            var frozen = Frozen;
            var interact = (player.InteractPressed || pendingInteract) && !frozen;
            pendingInteract = false;
            if (subtitles.IsTalking && interact)
            {
                subtitles.Advance();
                interact = false;
            }
            IInteractable selected = null;
            if (!subtitles.IsTalking && !frozen)
            {
                var eye = player.Eye;
                selected = InteractionPicker.Select(eye.position, eye.forward, items, progress.Done, maxAngle);
            }
            hud.SetPrompt(selected != null ? "E  " + selected.Label : null);
            if (selected != null && interact)
            {
                subtitles.Enqueue(progress.Examine(selected));
                if (progress.Done.Contains(selected.Id) && Examined != null) Examined(selected);
            }
            // 調べた先の演出が Freeze を呼ぶので、止まっているかは調べた後に見直す
            var frozenNow = Frozen;
            ReleaseDaze();
            Stand(frozenNow);
            hud.SetSubtitle(subtitles.Current);
            if (progress.IsComplete && !subtitles.IsTalking && !frozenNow) StartCoroutine(Complete());
        }

        /// <summary>dazeUntil の対象を調べたら、保っていた眩暈を消し始める</summary>
        void ReleaseDaze()
        {
            if (daze == null || dazeReleased) return;
            if (dazeUntil.Length > 0 && !progress.Done.Contains(dazeUntil)) return;
            dazeReleased = true;
            daze.Decay(dazeBlur, dazeWobble, dazeSeconds);
        }

        /// <summary>standAfter の対象を調べたら、止まっていない間に目線を上げて移動を許す</summary>
        void Stand(bool frozen)
        {
            if (standUp == null) return;
            standUp.Tick(Time.deltaTime, progress.Done.Contains(standAfter), frozen);
            player.EyeHeight = standUp.EyeHeight;
            player.CanMove = standUp.Standing;
        }

        IEnumerator Complete()
        {
            Completed = true;
            hud.SetPrompt(null);
            hud.SetSubtitle(null);
            hud.CancelSmoke();
            yield return hud.FadeTo(1f, FadeSeconds);
            hud.SetCenter(ToBeContinued);
        }
    }
}
```

`standAfter` と `dazeUntil` の既定は空なので、シーンを繋ぐ前は段階 2 と同じ動き（立って始まり、眩暈なし）のまま。

- [x] **Step 2: コンパイルとテストを確認**

`refresh_unity` → `read_console`（エラー・警告 0）→ `run_tests` → `get_test_job`。
Expected: 49 件 passed。

- [x] **Step 3: コミット**

```bash
cd /d/work/kataaware && git add unity/Assets/Scripts/Flow/SceneFlow.cs && git commit -m "feat: add the freeze, the seat, the daze and the fade to the scene flow"
```

---

### Task 8: `RoomIntroDirector`

**Files:**
- Create: `unity/Assets/Scripts/Flow/RoomIntroDirector.cs`

- [x] **Step 1: 書く**

`unity/Assets/Scripts/Flow/RoomIntroDirector.cs`:

```csharp
using System.Collections;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 場面 1 固有の演出。眩暈の中でクレジットとタイトルを順に出し、その間は調べる操作を止める。
    /// 終わったら最初の独白を流す。煙草を取ったら煙を立てて数秒止め、吸い終わりの独白を流す。
    /// SceneFlow とは Examined / Say / Freeze だけで繋ぐ
    /// </summary>
    public sealed class RoomIntroDirector : MonoBehaviour
    {
        /// <summary>クレジット 1 枚を見せている秒数</summary>
        public const float CreditSeconds = 2f;
        /// <summary>タイトルを見せている秒数</summary>
        public const float TitleSeconds = 2.5f;
        /// <summary>導入のあいだ調べる操作を止める秒数。クレジットとタイトルの合計より長くする</summary>
        public const float IntroSeconds = 6.2f;
        /// <summary>煙草を取ってから吸い終わるまでの秒数</summary>
        public const float SmokeSeconds = 4f;

        [SerializeField] SceneFlow flow;
        [SerializeField] HudView hud;
        [Tooltip("この id を調べたら煙草の演出を始める")]
        [SerializeField] string cigaretteId = "cigarette";
        [SerializeField] string[] credits = { "制作 〔名義〕" };
        [SerializeField] string titleCard = "HALF AWARE";
        [SerializeField] string[] firstLines = { "うぅ…今回は酔いが酷い…" };
        [SerializeField] string[] afterSmokeLines = { "煙草が切れた…買いに行くついでに今日のメモリも売っちゃおう" };

        void OnEnable()
        {
            if (flow == null || hud == null)
            {
                Debug.LogError("RoomIntroDirector: flow か hud が未接続", this);
                enabled = false;
                return;
            }
            flow.Examined += OnExamined;
            StartCoroutine(Intro());
        }

        void OnDisable()
        {
            if (flow != null) flow.Examined -= OnExamined;
            StopAllCoroutines();
            if (hud == null) return;
            hud.SetCenter(null);
            hud.CancelSmoke();
        }

        IEnumerator Intro()
        {
            flow.Freeze(IntroSeconds);
            var shown = 0f;
            foreach (var line in credits)
            {
                hud.SetCenter(line);
                yield return new WaitForSeconds(CreditSeconds);
                shown += CreditSeconds;
            }
            hud.SetCenter(titleCard);
            yield return new WaitForSeconds(TitleSeconds);
            shown += TitleSeconds;
            hud.SetCenter(null);
            yield return new WaitForSeconds(Mathf.Max(0f, IntroSeconds - shown));
            flow.Say(firstLines);
        }

        void OnExamined(IInteractable item)
        {
            if (item.Id != cigaretteId) return;
            StartCoroutine(Smoke());
        }

        /// <summary>煙草を取った後は吸い終わるまで自動で進む。その間は調べる操作を止める</summary>
        IEnumerator Smoke()
        {
            flow.Freeze(SmokeSeconds);
            hud.ShowSmoke(SmokeSeconds);
            yield return new WaitForSeconds(SmokeSeconds);
            flow.Say(afterSmokeLines);
        }
    }
}
```

クレジットの名義はオーナーが決めるまで `〔名義〕` のまま（シナリオ設計書 4.1）。

- [x] **Step 2: コンパイルとテストを確認**

`refresh_unity` → `read_console`（エラー・警告 0）→ `run_tests` → `get_test_job`。
Expected: 49 件 passed。

- [x] **Step 3: コミット**

```bash
cd /d/work/kataaware && git add unity/Assets/Scripts/Flow && git commit -m "feat: add the credits, the title and the cigarette sequence of scene 1"
```

---

### Task 9: シーンに対象と層と演出を足す

**Files:**
- Modify: `unity/Assets/Scenes/Room.unity`

- [ ] **Step 1: 現状を見る**

`manage_scene`（`action: "get_hierarchy"`, `max_depth: 3`）。
Expected: `Directional Light`、`Global Volume`、`Room`（11 個の子）、`RoomLight`、`Player`（子に `Main Camera`）、`Interactables`（5 個の子）、`Hud`（3 個の子）、`SceneFlow`。知らない物があれば親セッションに戻す。

- [ ] **Step 2: 椅子とジャックの仮の箱、対象 3 つを足し、前提を繋ぎ直す**

`execute_code`:

```csharp
var script = AssetDatabase.LoadAssetAtPath<HalfAware.RoomScript>("Assets/Data/RoomScript.asset");
if (script == null) return "script asset missing";
var room = GameObject.Find("Room");
var itemsRoot = GameObject.Find("Interactables");
if (room == null || itemsRoot == null) return "Room or Interactables not found";
var shader = Shader.Find("Universal Render Pipeline/Lit");
System.Func<string, Color, Material> material = (name, color) => {
    var path = "Assets/Materials/Placeholder/" + name + ".mat";
    var m = AssetDatabase.LoadAssetAtPath<Material>(path);
    if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
    m.SetColor("_BaseColor", color);
    EditorUtility.SetDirty(m);
    return m;
};
var chairMat = material("Chair", new Color(0.26f, 0.26f, 0.30f));
var jackMat = material("Jack", new Color(0.55f, 0.45f, 0.40f));

// 椅子（座る位置なので当たり判定は付けない）と、前腕とジャックの仮の箱
System.Action<string, Vector3, Vector3, Material> box = (name, pos, size, mat) => {
    var existing = room.transform.Find(name);
    if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
    go.name = name;
    go.transform.SetParent(room.transform, false);
    go.transform.localPosition = pos;
    go.transform.localScale = size;
    go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    UnityEngine.Object.DestroyImmediate(go.GetComponent<BoxCollider>());
};
box("Chair", new Vector3(1.5f, 0.45f, 1.5f), new Vector3(0.6f, 0.9f, 0.6f), chairMat);
box("Jack", new Vector3(1.75f, 0.75f, 1.5f), new Vector3(0.28f, 0.1f, 0.1f), jackMat);

// 対象。位置は試作の Z を反転したもの
var itemType = System.Type.GetType("HalfAware.Interactable, HalfAware");
System.Action<string, Vector3, bool, string[], float> item = (id, pos, required, after, radius) => {
    var found = itemsRoot.transform.Find("Interactable_" + id);
    var go = found != null ? found.gameObject : new GameObject("Interactable_" + id);
    if (found == null) go.transform.SetParent(itemsRoot.transform, false);
    go.transform.localPosition = pos;
    var comp = go.GetComponent(itemType);
    if (comp == null) comp = go.AddComponent(itemType);
    var so = new SerializedObject(comp);
    so.FindProperty("id").stringValue = id;
    so.FindProperty("script").objectReferenceValue = script;
    so.FindProperty("required").boolValue = required;
    so.FindProperty("radius").floatValue = radius;
    so.FindProperty("once").boolValue = true;
    var a = so.FindProperty("after");
    a.arraySize = after.Length;
    for (int i = 0; i < after.Length; i++) a.GetArrayElementAtIndex(i).stringValue = after[i];
    so.ApplyModifiedPropertiesWithoutUndo();
};
// 座った目線（高さ 1.1）から水平に見ても視線の角度に入らず、下を向くと入る位置
item("jack", new Vector3(1.75f, 0.75f, 1.5f), true, new string[0], 1.2f);
item("cigarette", new Vector3(2.5f, 0.85f, 1.0f), true, new[] { "jack" }, 2f);
item("chips", new Vector3(-1.8f, 0.9f, 2.2f), true, new[] { "cigarette" }, 2f);
item("terminal", new Vector3(1.5f, 1.05f, 2.4f), true, new[] { "chips" }, 2f);
item("door", new Vector3(0.8f, 1.2f, -2.8f), true, new[] { "chips", "terminal" }, 2f);
item("cigarette-box", new Vector3(2.4f, 0.85f, 0.85f), false, new[] { "cigarette" }, 2f);
item("ashtray", new Vector3(2.65f, 0.85f, 1.2f), false, new string[0], 2f);
item("clipboard", new Vector3(2.2f, 0.9f, 2.0f), false, new string[0], 2f);

AssetDatabase.SaveAssets();
var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return "interactables=" + itemsRoot.transform.childCount + " roomChildren=" + room.transform.childCount;
```

Expected: 戻り値 `interactables=8 roomChildren=13`。`read_console` でエラー 0（`Interactable に id がない` の警告は `AddComponent` の直後に 1 回出るが、`id` はその後に書かれるので問題ない。Step 5 の読み返しで確かめる）。

- [ ] **Step 3: HUD に暗転と煙の層を足す**

重なりの順は、後に足したものほど手前。煙 → 黒帯 → 印 → 暗転 → 中央の文字（暗転は字幕と印を隠し、中央の文字は暗転の上に出す）。既にある 3 つは `SubtitleBand` → `Prompt` → `Center` の順に並んでいるので、煙を先頭へ、暗転を `Center` の直前へ入れる。

`execute_code`:

```csharp
var hudGo = GameObject.Find("Hud");
if (hudGo == null) return "Hud not found";
System.Func<string, Color, UnityEngine.UI.Image> layer = (name, color) => {
    var found = hudGo.transform.Find(name);
    var go = found != null ? found.gameObject : new GameObject(name, typeof(RectTransform));
    if (found == null) go.transform.SetParent(hudGo.transform, false);
    var image = go.GetComponent<UnityEngine.UI.Image>();
    if (image == null) image = go.AddComponent<UnityEngine.UI.Image>();
    image.color = color;
    image.raycastTarget = false;
    var rect = go.GetComponent<RectTransform>();
    rect.anchorMin = Vector2.zero;
    rect.anchorMax = Vector2.one;
    rect.offsetMin = Vector2.zero;
    rect.offsetMax = Vector2.zero;
    return image;
};
// 煙は画面下半分だけ（見た目は段階 4 で作り込む）
var smoke = layer("Smoke", new Color(0.75f, 0.75f, 0.72f, 0f));
smoke.rectTransform.anchorMin = new Vector2(0f, 0f);
smoke.rectTransform.anchorMax = new Vector2(1f, 0.55f);
smoke.transform.SetAsFirstSibling();
var fade = layer("Fade", new Color(0f, 0f, 0f, 0f));
fade.transform.SetSiblingIndex(hudGo.transform.childCount - 1);
var center = hudGo.transform.Find("Center");
if (center != null) center.SetAsLastSibling();

var hud = hudGo.GetComponent(System.Type.GetType("HalfAware.HudView, HalfAware"));
var hudSo = new SerializedObject(hud);
hudSo.FindProperty("fadeLayer").objectReferenceValue = fade;
hudSo.FindProperty("smokeLayer").objectReferenceValue = smoke;
hudSo.ApplyModifiedPropertiesWithoutUndo();

var order = "";
for (int i = 0; i < hudGo.transform.childCount; i++) order += hudGo.transform.GetChild(i).name + " ";
var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return order.Trim();
```

Expected: 戻り値 `Smoke SubtitleBand Prompt Fade Center`。`read_console` でエラー 0。

- [ ] **Step 4: 眩暈の容れ物と演出を置き、`SceneFlow` を繋ぐ**

`execute_code`:

```csharp
var flowGo = GameObject.Find("SceneFlow");
var hudGo = GameObject.Find("Hud");
if (flowGo == null || hudGo == null) return "SceneFlow or Hud not found";
var dazeType = System.Type.GetType("HalfAware.DazeVolume, HalfAware");
var daze = flowGo.GetComponent(dazeType);
if (daze == null) daze = flowGo.AddComponent(dazeType);

var flowType = System.Type.GetType("HalfAware.SceneFlow, HalfAware");
var flow = flowGo.GetComponent(flowType);
var so = new SerializedObject(flow);
so.FindProperty("daze").objectReferenceValue = daze;
so.FindProperty("seatEyeHeight").floatValue = 1.1f;
so.FindProperty("standAfter").stringValue = "cigarette";
so.FindProperty("dazeBlur").floatValue = 1f;
so.FindProperty("dazeWobble").floatValue = 1f;
so.FindProperty("dazeSeconds").floatValue = 5f;
so.FindProperty("dazeUntil").stringValue = "jack";
so.ApplyModifiedPropertiesWithoutUndo();

var directorType = System.Type.GetType("HalfAware.RoomIntroDirector, HalfAware");
var director = flowGo.GetComponent(directorType);
if (director == null) director = flowGo.AddComponent(directorType);
var dso = new SerializedObject(director);
dso.FindProperty("flow").objectReferenceValue = flow;
dso.FindProperty("hud").objectReferenceValue = hudGo.GetComponent(System.Type.GetType("HalfAware.HudView, HalfAware"));
dso.ApplyModifiedPropertiesWithoutUndo();

var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return "wired";
```

Expected: 戻り値 `wired`。`read_console` でエラー 0。

- [ ] **Step 5: 全部の配線を読み返す**

`execute_code`:

```csharp
var flowGo = GameObject.Find("SceneFlow");
var so = new SerializedObject(flowGo.GetComponent(System.Type.GetType("HalfAware.SceneFlow, HalfAware")));
var dso = new SerializedObject(flowGo.GetComponent(System.Type.GetType("HalfAware.RoomIntroDirector, HalfAware")));
var hudGo = GameObject.Find("Hud");
var hso = new SerializedObject(hudGo.GetComponent(System.Type.GetType("HalfAware.HudView, HalfAware")));
var report = "flow: player=" + (so.FindProperty("player").objectReferenceValue != null)
    + " hud=" + (so.FindProperty("hud").objectReferenceValue != null)
    + " daze=" + (so.FindProperty("daze").objectReferenceValue != null)
    + " standAfter=" + so.FindProperty("standAfter").stringValue
    + " seatEye=" + so.FindProperty("seatEyeHeight").floatValue
    + " dazeUntil=" + so.FindProperty("dazeUntil").stringValue
    + "\ndirector: flow=" + (dso.FindProperty("flow").objectReferenceValue != null)
    + " hud=" + (dso.FindProperty("hud").objectReferenceValue != null)
    + " cigaretteId=" + dso.FindProperty("cigaretteId").stringValue
    + " credits=" + dso.FindProperty("credits").GetArrayElementAtIndex(0).stringValue
    + " title=" + dso.FindProperty("titleCard").stringValue
    + "\nhud: fade=" + (hso.FindProperty("fadeLayer").objectReferenceValue != null)
    + " smoke=" + (hso.FindProperty("smokeLayer").objectReferenceValue != null);
var items = UnityEngine.Object.FindObjectsByType(System.Type.GetType("HalfAware.Interactable, HalfAware"), FindObjectsSortMode.InstanceID);
report += "\ninteractables=" + items.Length;
foreach (var it in items)
{
    var read = it as HalfAware.IInteractable;
    var iso = new SerializedObject(it);
    var after = "";
    var a = iso.FindProperty("after");
    for (int i = 0; i < a.arraySize; i++) after += a.GetArrayElementAtIndex(i).stringValue + ";";
    report += "\n  " + read.Id + " required=" + read.Required + " radius=" + read.Radius
        + " after=[" + after + "] label=" + read.Label + " lines=" + read.Lines.Count;
}
return report;
```

Expected: すべての参照が `True`、`standAfter=cigarette`、`seatEye=1.1`、`dazeUntil=jack`、`cigaretteId=cigarette`、`credits=制作 〔名義〕`、`title=HALF AWARE`、`interactables=8`。各対象は次のとおり:

| id | required | radius | after | lines |
|---|---|---|---|---|
| jack | True | 1.2 | （空） | 1 |
| cigarette | True | 2 | jack | 0 |
| chips | True | 2 | cigarette | 7 |
| terminal | True | 2 | chips | 11 |
| door | True | 2 | chips;terminal | 1 |
| cigarette-box | False | 2 | cigarette | 1 |
| ashtray | False | 2 | （空） | 1 |
| clipboard | False | 2 | （空） | 2 |

食い違いがあれば止めて報告する。

- [ ] **Step 6: テストとコミット**

`run_tests` → `get_test_job`。Expected: 49 件 passed。

```bash
cd /d/work/kataaware && git status --short && git add unity/Assets/Scenes/Room.unity unity/Assets/Materials && git commit -m "feat: place the jack, the cigarette and the chair, and wire the scene 1 sequence"
```

`git status` に `unity/Assets/Fonts/NotoSansJP-Regular SDF.asset` が出ていたら、コミット前に `git checkout -- "unity/Assets/Fonts/NotoSansJP-Regular SDF.asset"` で戻す。

---

### Task 10: 再生での確認

場面 1 を頭から通し、導入・座位・ジャック・煙草・立ち上がり・必須 3 つ・暗転までを確かめる。操作感と見た目の最終判断はオーナー。

**Files:** なし（直すものが出たら該当タスクのファイル）

- [ ] **Step 1: 再生して導入を見る**

`read_console`（`action: "clear"`）→ `manage_editor`（`action: "play"`）。
再生に入ったら、`execute_code` で `Application.runInBackground = true;` を立てる（エディタが前面に無いと `Update` が走らないため）。
`read_console`（`types: ["error", "warning"]`）。Expected: エラー 0。

クレジットを読む:

```csharp
var center = GameObject.Find("Hud").transform.Find("Center");
var flow = GameObject.Find("SceneFlow").GetComponent(System.Type.GetType("HalfAware.SceneFlow, HalfAware")) as HalfAware.SceneFlow;
var daze = GameObject.Find("SceneFlow").GetComponent(System.Type.GetType("HalfAware.DazeVolume, HalfAware")) as HalfAware.DazeVolume;
var player = GameObject.Find("Player").GetComponent(System.Type.GetType("HalfAware.PlayerController, HalfAware")) as HalfAware.PlayerController;
return "t=" + Time.time.ToString("0.0")
    + " center=[" + center.GetComponent<TMPro.TextMeshProUGUI>().text + "]"
    + " frozen=" + flow.Frozen
    + " blur=" + daze.Blur.ToString("0.00")
    + " canMove=" + player.CanMove
    + " eye=" + player.EyeHeight.ToString("0.00");
```

Expected: 2 秒あたりで `center=[制作 〔名義〕]`、その後 `center=[HALF AWARE]`。どちらのときも `frozen=True`、`blur=1.00`、`canMove=False`、`eye=1.10`。`ScreenCapture.CaptureScreenshot("Temp/stage3-01-title.png")` を撮り、`D:\work\kataaware\unity\Temp\stage3-01-title.png` を Read で見る。

- [ ] **Step 2: 導入が明けて最初の独白が出ることを見る**

6.2 秒を過ぎてから字幕を読む:

```csharp
var band = GameObject.Find("Hud").transform.Find("SubtitleBand");
var flow = GameObject.Find("SceneFlow").GetComponent(System.Type.GetType("HalfAware.SceneFlow, HalfAware")) as HalfAware.SceneFlow;
return "t=" + Time.time.ToString("0.0") + " frozen=" + flow.Frozen
    + " band=" + band.gameObject.activeSelf
    + " [" + band.Find("Subtitle").GetComponent<TMPro.TextMeshProUGUI>().text + "]";
```

Expected: `frozen=False`、`band=True`、`[うぅ…今回は酔いが酷い…]`。
`SendMessage("PressInteract")` で送ってから字幕を読む。Expected: `band=False`。

- [ ] **Step 3: 座ったままではジャックが選べず、下を向くと選べることを見る**

印を読む:

```csharp
var prompt = GameObject.Find("Hud").transform.Find("Prompt");
return prompt.gameObject.activeSelf + " | " + prompt.GetComponent<TMPro.TextMeshProUGUI>().text;
```

Expected: `False | `（座った目線から水平に見るとジャックは視線の角度の外）。

カメラを下に向ける:

```csharp
var eye = GameObject.Find("Main Camera").transform;
eye.localRotation = Quaternion.Euler(55f, 0f, 0f);
return eye.localEulerAngles.ToString();
```

印を読む。Expected: `True | E  インプラントジャックを抜く`。

- [ ] **Step 4: ジャックを抜くと眩暈が引き始めることを見る**

`SendMessage("PressInteract")` → 字幕を読む。Expected: `True | [大小の差こそあれ、他人の記憶を観た後はいつもこうだ]`。
`PressInteract` で送る → 眩暈を読む:

```csharp
var daze = GameObject.Find("SceneFlow").GetComponent(System.Type.GetType("HalfAware.DazeVolume, HalfAware")) as HalfAware.DazeVolume;
return "blur=" + daze.Blur.ToString("0.00") + " wobble=" + daze.Wobble.ToString("0.00") + " clear=" + daze.IsClear;
```

Expected: `blur` が 1.00 より小さくなっている。数秒おいてもう一度読むとさらに小さく、5 秒経つと `clear=True`。

- [ ] **Step 5: 煙草を取ると自動で進み、立ち上がることを見る**

カメラを水平に戻して卓の方を向く:

```csharp
var eye = GameObject.Find("Main Camera").transform;
eye.localRotation = Quaternion.identity;
var player = GameObject.Find("Player");
var cc = player.GetComponent<CharacterController>();
cc.enabled = false;
player.transform.rotation = Quaternion.LookRotation(new Vector3(1f, 0f, -0.3f));
cc.enabled = true;
return "facing the side table";
```

印を読む。Expected: `True | E  煙草を取る`。選べないときは `player.transform.rotation` の向きを変えて、卓（2.5, 0.85, 1.0）が視線の角度に入る位置に調整し、使った値を報告する。

`SendMessage("PressInteract")` → すぐに読む:

```csharp
var flow = GameObject.Find("SceneFlow").GetComponent(System.Type.GetType("HalfAware.SceneFlow, HalfAware")) as HalfAware.SceneFlow;
var smoke = GameObject.Find("Hud").transform.Find("Smoke");
var player = GameObject.Find("Player").GetComponent(System.Type.GetType("HalfAware.PlayerController, HalfAware")) as HalfAware.PlayerController;
return "frozen=" + flow.Frozen + " smoke=" + smoke.gameObject.activeSelf
    + " alpha=" + smoke.GetComponent<UnityEngine.UI.Image>().color.a.ToString("0.00")
    + " canMove=" + player.CanMove + " eye=" + player.EyeHeight.ToString("0.00");
```

Expected: `frozen=True`、`smoke=True` で `alpha` が 0 より大きい、`canMove=False`、`eye=1.10`（止まっている間は立ち上がらない）。ここで `ScreenCapture.CaptureScreenshot("Temp/stage3-02-smoke.png")` を撮って Read で見る。

4 秒を過ぎてから読む。Expected: 字幕が `[煙草が切れた…買いに行くついでに今日のメモリも売っちゃおう]`、`frozen=False`、`smoke=False`。さらに 1 秒ほど後に `canMove=True`、`eye=1.60`。

- [ ] **Step 6: メモリハブ → 端末 → ドアを通して暗転まで見る**

字幕が残っていれば `PressInteract` で送り切る。以降は段階 2 の確認と同じ要領で、`CharacterController` を切って位置を変えてから戻す。

メモリハブの前（-1.8, 0.05, 0.9）で +Z を向く → 印 `True | E  チップを抜く` → `PressInteract` を 7 回（1 行目から 7 行目まで）＋ 1 回（送り切り）。
端末の前（1.5, 0.05, 1.2）で +Z を向く → 印 `True | E  端末` → `PressInteract` を 11 回 ＋ 1 回。
ドアの前（0.8, 0.05, -1.6）で -Z を向く → 印 `True | E  ドア` → `PressInteract` → 字幕 `[タバコを買いに行くついでに、今日のチップを売ってしまおう]` → `PressInteract`。

途中、`cigarette-box` を調べずに進んでよい（任意の対象）。

暗転を読む:

```csharp
var hudGo = GameObject.Find("Hud");
var fade = hudGo.transform.Find("Fade").GetComponent<UnityEngine.UI.Image>();
var center = hudGo.transform.Find("Center");
var flow = GameObject.Find("SceneFlow").GetComponent(System.Type.GetType("HalfAware.SceneFlow, HalfAware")) as HalfAware.SceneFlow;
return "completed=" + flow.Completed + " fade=" + fade.color.a.ToString("0.00")
    + " center=" + center.gameObject.activeSelf + " [" + center.GetComponent<TMPro.TextMeshProUGUI>().text + "]";
```

Expected: 1.5 秒かけて `fade` が 1.00 になり、その後 `center=True` で `[（仮）続く]`。`ScreenCapture.CaptureScreenshot("Temp/stage3-03-end.png")` を撮って Read で見る。真っ黒な画面に「（仮）続く」だけが見えること。

- [ ] **Step 7: 停止して後始末**

`execute_code` で `Application.runInBackground = false;` に戻す → `manage_editor`（`action: "stop"`）→ `read_console`（`types: ["error"]`）。
Expected: エラー 0。

```bash
cd /d/work/kataaware && git status --short && grep -n "runInBackground" unity/ProjectSettings/ProjectSettings.asset
```

Expected: `Room.unity` に差分が無く、`runInBackground: 0`。`unity/Assets/Fonts/NotoSansJP-Regular SDF.asset` が出ていたら `git checkout --` で戻す。差分が残る場合は該当ファイルを報告する。

- [ ] **Step 8: オーナーに手元の確認を頼む**

親セッションから次を伝える:
- 再生してクリックすると、座った視界でクレジットとタイトルが出て、明けると最初の独白が出る
- 下を向くとジャックの仮の箱が見え、調べると眩暈が引き始める（段階 3 では数値だけで、画面はまだぼやけない）
- 右手の卓の煙草を取ると 4 秒の自動演出が入り、吸い終わると独白が出て立ち上がれる
- メモリハブ → 端末 → ドアの順に調べると暗転して「続く」が出る
- ドアをチップより先に調べると「テーブルからチップを取ってこよう」、チップの後・端末の前だと「（仮）端末を確かめてからだ」が出る。後者の文はシナリオ設計書 6 節で未決のまま
- 卓・机・椅子の位置は Hierarchy の `Room` の子を動かせば変えられる。調べる対象の位置は `Interactables` の子で、文面は `Assets/Data/RoomScript.asset`。位置と文面は別々に直せる
- クレジットの名義（`SceneFlow` の `RoomIntroDirector` の `credits`）は未定のまま

---

## 段階 4 に持ち越すこと

- PS1 風の描画（Render Scale 1/3 と Point 拡大、減色とディザ）と、`DazeVolume` の数値を読む Full Screen Pass。眩暈が画面に出るのはここから
- 色味（Volume の Color Adjustments）
- 煙の見た目の作り込み（今は画面下の半透明な層）
- Kenney の物の取り込みと配置、固有物（椅子、メモリハブ、端末、灰皿、煙草の箱、前腕とジャック）の生成
- 前腕とジャックを下を向いたときだけ出す仕組み。今はただの箱が置いてあるだけ
- 端末の黒い画面に映る顔の反射
- 音（効果音、英語の合成音声のサンプリング、BGM）
- クレジットとタイトルの出入り（今は切り替わるだけ）
- クレジットの名義と、ドアの「端末を確かめてから」の文を確定する
