# Unity 移行 段階 2: 骨組み Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `docs/superpowers/specs/2026-09-16-unity-port-design.md` の 5 節・段階 2 を実装する。仮の箱の自室を Unity のシーンに組み、歩く・見回す・調べる・字幕・必須の完了までを C# で動かす。Three.js 版の `2026-09-15-foundation.md` に当たる到達点を Unity 上で作り直す。

**Architecture:** 判定（対象の選択、進行、字幕の待ち行列）は MonoBehaviour に依存しない純粋な C# クラスにして EditMode テストで確かめ、シーンとの接続（`PlayerController`、`Interactable`、`HudView`、`SceneFlow`）は MonoBehaviour に置く。シーンは MCP for Unity の `execute_code` で組み、以後の微調整はオーナーがエディタで行う。文面はこの段では `Interactable` に仮の文を直接持たせ、段階 3 で `RoomScript` に移す。

**Tech Stack:** Unity 6000.3.24f1、URP 17.3、Input System 1.20、TextMeshPro（com.unity.ugui 2.0）、Unity Test Framework 1.6（NUnit）、MCP for Unity 10.0。

**実行時の注意:**
- コミットメッセージにモデル名・ツール名を著者として入れない
- subagent のモデルは `opus` か `sonnet` で毎回明示する
- `HANDOFF.md` は作らない。`docs/` はこのファイルのチェックボックス以外触らない
- Unity エディタが `unity/` を開いて起動していることが前提。MCP の `UnityMCP`（47 tools）が使えなければ、親セッションに戻す
- C# ファイルを書いたら `mcp__UnityMCP__refresh_unity`（`mode: "force"`, `compile: "request"`, `wait_for_ready: true`）→ `mcp__UnityMCP__read_console`（`types: ["error"]`）でコンパイルエラーが 0 であることを確かめてから次へ進む
- EditMode テストは `mcp__UnityMCP__run_tests`（`mode: "EditMode"`, `assembly_names: ["HalfAware.Tests.EditMode"]`, `include_failed_tests: true`）で始め、返った `job_id` を `mcp__UnityMCP__get_test_job`（`wait_timeout: 60`, `include_failed_tests: true`）で待つ
- Unity は `Assets/` 以下の各ファイル・フォルダに `.meta` を作る。`refresh_unity` の後に `git add` し、`.meta` も一緒にコミットする。`unity/Library`、`unity/Temp`、`unity/Logs` はコミットしない
- `execute_code` に渡す C# は C# 6 の範囲で書く（`out var`、タプル、ローカル関数、パターンマッチは使わない）。`using` は書けないので、`UnityEngine` と `UnityEditor` 以外の型は名前空間つきで書く。`AssetDatabase.DeleteAsset` が安全確認に引っかかったら `safety_checks: false` で再実行する
- 座標: Three.js 試作（-Z が正面）の値は Z の符号を反転して使う。Unity は +Z が正面。左右はそのまま合う
- オーナーがエディタで並行して触ることがある。シーンを組む前に `mcp__UnityMCP__manage_scene`（`action: "get_hierarchy"`）で現状を見て、既にある物は上書きしない

---

## ファイル構成

| パス（`unity/` 以下） | 変更 | 責務 |
|---|---|---|
| `Assets/Scripts/HalfAware.asmdef` | 新規 | 実行時アセンブリ。Input System、TextMeshPro、uGUI を参照 |
| `Assets/Scripts/Hud/SubtitleQueue.cs` | 新規 | 字幕の待ち行列（純粋な C#） |
| `Assets/Scripts/Hud/HudView.cs` | 新規 | 字幕の黒帯、印、中央の文字の表示 |
| `Assets/Scripts/Interaction/IInteractable.cs` | 新規 | 調べる対象の読み取り口 |
| `Assets/Scripts/Interaction/InteractionPicker.cs` | 新規 | 距離と視線の角度、前提の判定（純粋な C#） |
| `Assets/Scripts/Interaction/Interactable.cs` | 新規 | シーン上の調べる対象（MonoBehaviour） |
| `Assets/Scripts/Flow/SceneProgress.cs` | 新規 | 済んだ対象の集合、必須の完了、調べたときの文（純粋な C#） |
| `Assets/Scripts/Flow/SceneFlow.cs` | 新規 | 毎フレームの進行。選択 → 印 → 調べる → 字幕 → 完了 |
| `Assets/Scripts/Player/PlayerController.cs` | 新規 | 歩く・見回す・カーソルのロック（CharacterController + Input System） |
| `Assets/Tests/EditMode/HalfAware.Tests.EditMode.asmdef` | 新規 | テストアセンブリ |
| `Assets/Tests/EditMode/SubtitleQueueTests.cs` | 新規 | 字幕の待ち行列のテスト |
| `Assets/Tests/EditMode/InteractionPickerTests.cs` | 新規 | 選択の規則のテスト（テスト用の対象 `FakeItem` を含む） |
| `Assets/Tests/EditMode/SceneProgressTests.cs` | 新規 | 進行の判定のテスト |
| `Assets/InputSystem_Actions.inputactions` | 変更 | `Interact` の Hold を外し、左クリックを追加 |
| `Assets/Fonts/NotoSansJP-Regular.otf`, `Assets/Fonts/NotoSansJP-Regular SDF.asset`, `Assets/Fonts/LICENSES.md` | 新規 | 日本語フォントと出典 |
| `Assets/TextMesh Pro/` | 新規 | TMP の必須リソース（取り込みで生成） |
| `Assets/Materials/Placeholder/*.mat` | 新規 | 仮の箱の色 |
| `Assets/Scenes/Room.unity` | 変更（`SampleScene.unity` を改名） | 自室 |
| `Assets/TutorialInfo/`, `Assets/Readme.asset` | 削除 | テンプレートの残骸 |
| `ProjectSettings/EditorBuildSettings.asset` | 変更 | ビルド対象に `Room` を登録 |

設計書 4 節の表との対応: `SceneFlow` のうち判定の部分を `SceneProgress` に切り出した（EditMode テストのため）。`freeze`、眩暈、立ち上がり、`SceneDirector`、`RoomIntroDirector`、`RoomScript`、`DazeVolume` は段階 3 以降。

---

### Task 1: アセンブリ定義と `SubtitleQueue`

テストの通り道（asmdef → テスト実行）を最小の部品で作る。

**Files:**
- Create: `unity/Assets/Scripts/HalfAware.asmdef`
- Create: `unity/Assets/Scripts/Hud/SubtitleQueue.cs`
- Create: `unity/Assets/Tests/EditMode/HalfAware.Tests.EditMode.asmdef`
- Test: `unity/Assets/Tests/EditMode/SubtitleQueueTests.cs`

- [ ] **Step 1: asmdef を 2 つ書く**

`unity/Assets/Scripts/HalfAware.asmdef`:

```json
{
    "name": "HalfAware",
    "rootNamespace": "HalfAware",
    "references": [
        "Unity.InputSystem",
        "Unity.TextMeshPro",
        "UnityEngine.UI"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

`unity/Assets/Tests/EditMode/HalfAware.Tests.EditMode.asmdef`:

```json
{
    "name": "HalfAware.Tests.EditMode",
    "rootNamespace": "HalfAware.Tests",
    "references": [
        "HalfAware",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 2: 失敗するテストを書く**

`unity/Assets/Tests/EditMode/SubtitleQueueTests.cs`:

```csharp
using NUnit.Framework;

namespace HalfAware.Tests
{
    public class SubtitleQueueTests
    {
        [Test]
        public void ShowsTheFirstQueuedLine()
        {
            var q = new SubtitleQueue();
            q.Enqueue(new[] { "一行目", "二行目" });
            Assert.That(q.Current, Is.EqualTo("一行目"));
            Assert.That(q.IsTalking, Is.True);
        }

        [Test]
        public void AdvancesLineByLineAndEmptiesAtTheEnd()
        {
            var q = new SubtitleQueue();
            q.Enqueue(new[] { "一行目", "二行目" });
            q.Advance();
            Assert.That(q.Current, Is.EqualTo("二行目"));
            q.Advance();
            Assert.That(q.Current, Is.Null);
            Assert.That(q.IsTalking, Is.False);
        }

        [Test]
        public void AppendsLinesBehindTheOnesStillWaiting()
        {
            var q = new SubtitleQueue();
            q.Enqueue(new[] { "a" });
            q.Enqueue(new[] { "b" });
            q.Advance();
            Assert.That(q.Current, Is.EqualTo("b"));
        }

        [Test]
        public void IgnoresAnEmptyEnqueue()
        {
            var q = new SubtitleQueue();
            q.Enqueue(new string[0]);
            Assert.That(q.Current, Is.Null);
            Assert.That(q.IsTalking, Is.False);
        }

        [Test]
        public void ClearDropsEverything()
        {
            var q = new SubtitleQueue();
            q.Enqueue(new[] { "a", "b" });
            q.Clear();
            Assert.That(q.Current, Is.Null);
            q.Enqueue(new[] { "c" });
            Assert.That(q.Current, Is.EqualTo("c"));
        }
    }
}
```

- [ ] **Step 3: 失敗を確認**

`refresh_unity`（`compile: "request"`）→ `read_console`（`types: ["error"]`）。
Expected: `SubtitleQueue` が見つからないというコンパイルエラーが出る（テストアセンブリが組めないので、テストはまだ走らない）。

- [ ] **Step 4: 実装**

`unity/Assets/Scripts/Hud/SubtitleQueue.cs`:

```csharp
using System.Collections.Generic;

namespace HalfAware
{
    /// <summary>字幕の待ち行列。行を積み、E かクリックで 1 行ずつ送る。最後の行を送ると空になる</summary>
    public sealed class SubtitleQueue
    {
        readonly List<string> lines = new List<string>();
        int index;

        /// <summary>今出している行。無ければ null</summary>
        public string Current => index < lines.Count ? lines[index] : null;

        public bool IsTalking => Current != null;

        /// <summary>待っている行の後ろに積む。空なら何も変わらない</summary>
        public void Enqueue(IEnumerable<string> newLines)
        {
            lines.AddRange(newLines);
        }

        /// <summary>次の行へ。最後の行だったら空にする</summary>
        public void Advance()
        {
            if (index + 1 >= lines.Count)
            {
                Clear();
                return;
            }
            index++;
        }

        public void Clear()
        {
            lines.Clear();
            index = 0;
        }
    }
}
```

- [ ] **Step 5: テストが通ることを確認**

`refresh_unity` → `read_console`（エラー 0）→ `run_tests` → `get_test_job`。
Expected: `HalfAware.Tests.EditMode` の 5 件が passed、failed 0。

- [ ] **Step 6: コミット**

```bash
cd /d/work/kataaware && git add unity/Assets/Scripts unity/Assets/Scripts.meta unity/Assets/Tests unity/Assets/Tests.meta && git commit -m "feat: add the HalfAware assemblies and the subtitle queue with EditMode tests"
```

---

### Task 2: `IInteractable` と `InteractionPicker`

**Files:**
- Create: `unity/Assets/Scripts/Interaction/IInteractable.cs`
- Create: `unity/Assets/Scripts/Interaction/InteractionPicker.cs`
- Test: `unity/Assets/Tests/EditMode/InteractionPickerTests.cs`

- [ ] **Step 1: 読み取り口を書く**

`unity/Assets/Scripts/Interaction/IInteractable.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace HalfAware
{
    /// <summary>調べる対象の読み取り口。シーン上の Interactable と、テストの代用品が実装する</summary>
    public interface IInteractable
    {
        string Id { get; }
        Vector3 Position { get; }
        /// <summary>この距離以内で選べる</summary>
        float Radius { get; }
        bool Required { get; }
        /// <summary>true なら一度調べると選べなくなる</summary>
        bool Once { get; }
        /// <summary>ここに挙げた id が済むまで選べない。ただし HintFor に文がある id については選べて、その文だけ出る</summary>
        IReadOnlyList<string> After { get; }
        string Label { get; }
        IReadOnlyList<string> Lines { get; }
        /// <summary>After の id が未達のときに出す文。無ければ null</summary>
        IReadOnlyList<string> HintFor(string afterId);
    }
}
```

- [ ] **Step 2: 失敗するテストを書く**

`unity/Assets/Tests/EditMode/InteractionPickerTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    /// <summary>テスト用の調べる対象。必要な項目だけ設定する</summary>
    public sealed class FakeItem : IInteractable
    {
        public string Id { get; set; }
        public Vector3 Position { get; set; }
        public float Radius { get; set; } = InteractionPicker.DefaultRadius;
        public bool Required { get; set; }
        public bool Once { get; set; } = true;
        public IReadOnlyList<string> After { get; set; } = new string[0];
        public string Label { get; set; } = "調べる";
        public IReadOnlyList<string> Lines { get; set; } = new string[0];
        public Dictionary<string, string[]> Hints { get; } = new Dictionary<string, string[]>();

        public FakeItem(string id, Vector3 position)
        {
            Id = id;
            Position = position;
        }

        public IReadOnlyList<string> HintFor(string afterId)
        {
            string[] lines;
            return Hints.TryGetValue(afterId, out lines) ? lines : null;
        }
    }

    public class InteractionPickerTests
    {
        static readonly Vector3 Cam = new Vector3(0f, 1.6f, 0f);
        static readonly Vector3 Fwd = Vector3.forward;

        static FakeItem At(string id, float x, float z) => new FakeItem(id, new Vector3(x, 1.6f, z));

        static HashSet<string> Done(params string[] ids) => new HashSet<string>(ids);

        static IInteractable Pick(ICollection<string> done, params IInteractable[] items) =>
            InteractionPicker.Select(Cam, Fwd, items, done);

        [Test]
        public void PicksTheNearestItemInsideTheRadiusAndTheViewCone()
        {
            var picked = Pick(Done(), At("near", 0f, 1f), At("far", 0f, 1.8f), At("behind", 0f, -1f), At("out", 0f, 5f));
            Assert.That(picked.Id, Is.EqualTo("near"));
        }

        [Test]
        public void IgnoresItemsBehindTheCamera()
        {
            Assert.That(Pick(Done(), At("behind", 0f, -1f)), Is.Null);
        }

        [Test]
        public void IgnoresItemsOutsideTheRadius()
        {
            Assert.That(Pick(Done(), At("out", 0f, 5f)), Is.Null);
        }

        [Test]
        public void AcceptsItemsInsideTheViewConeAndRejectsItemsJustOutsideIt()
        {
            // MaxAngle は 0.7 rad（約 40°）。x=0.6, z=1 は約 31°、x=0.9, z=1 は約 42°
            var inside = At("in", 0.6f, 1f);
            var outside = At("out", 0.9f, 1f);
            Assert.That(Pick(Done(), inside).Id, Is.EqualTo("in"));
            Assert.That(Pick(Done(), outside), Is.Null);
            Assert.That(InteractionPicker.Select(Cam, Fwd, new[] { outside }, Done(), 0.8f).Id, Is.EqualTo("out"));
        }

        [Test]
        public void HonoursAPerItemRadius()
        {
            var wide = At("w", 0f, 5f);
            wide.Radius = 6f;
            Assert.That(Pick(Done(), wide).Id, Is.EqualTo("w"));
        }

        [Test]
        public void SkipsItemsAlreadyExaminedWhenOnceIsSet()
        {
            var picked = Pick(Done("near"), At("near", 0f, 1f), At("far", 0f, 1.8f));
            Assert.That(picked.Id, Is.EqualTo("far"));
        }

        [Test]
        public void KeepsRepeatableItemsSelectable()
        {
            var rep = At("r", 0f, 1f);
            rep.Once = false;
            Assert.That(Pick(Done("r"), rep).Id, Is.EqualTo("r"));
        }

        [Test]
        public void HidesItemsWhosePrerequisitesAreNotDone()
        {
            var gated = At("g", 0f, 1f);
            gated.After = new[] { "x" };
            Assert.That(Pick(Done(), gated), Is.Null);
            Assert.That(Pick(Done("x"), gated).Id, Is.EqualTo("g"));
        }

        [Test]
        public void KeepsAGatedItemSelectableWhenItHasAHintForTheUnmetPrerequisite()
        {
            var hinted = At("h", 0f, 1f);
            hinted.After = new[] { "x" };
            hinted.Hints["x"] = new[] { "先に x" };
            Assert.That(Pick(Done(), hinted).Id, Is.EqualTo("h"));
        }

        [Test]
        public void HidesAGatedItemWhenTheUnmetPrerequisiteHasNoHint()
        {
            var partly = At("p", 0f, 1f);
            partly.After = new[] { "x", "y" };
            partly.Hints["x"] = new[] { "先に x" };
            Assert.That(Pick(Done("x"), partly), Is.Null);
        }

        [Test]
        public void UsesTheHintOfTheFirstUnmetPrerequisiteOnly()
        {
            var later = At("l", 0f, 1f);
            later.After = new[] { "x", "y" };
            later.Hints["y"] = new[] { "先に y" };
            Assert.That(Pick(Done(), later), Is.Null);
            Assert.That(Pick(Done("x"), later).Id, Is.EqualTo("l"));
        }

        [Test]
        public void TreatsAnEmptyHintListAsNoHint()
        {
            var empty = At("e", 0f, 1f);
            empty.After = new[] { "x" };
            empty.Hints["x"] = new string[0];
            Assert.That(Pick(Done(), empty), Is.Null);
        }

        [Test]
        public void UnmetPrerequisiteReturnsTheFirstMissingIdOrNull()
        {
            var item = At("d", 0f, 0f);
            item.After = new[] { "a", "b" };
            Assert.That(InteractionPicker.UnmetPrerequisite(item, Done()), Is.EqualTo("a"));
            Assert.That(InteractionPicker.UnmetPrerequisite(item, Done("a")), Is.EqualTo("b"));
            Assert.That(InteractionPicker.UnmetPrerequisite(item, Done("a", "b")), Is.Null);
            Assert.That(InteractionPicker.UnmetPrerequisite(At("n", 0f, 0f), Done()), Is.Null);
        }
    }
}
```

- [ ] **Step 3: 失敗を確認**

`refresh_unity` → `read_console`（`types: ["error"]`）。
Expected: `InteractionPicker` が見つからないというコンパイルエラー。

- [ ] **Step 4: 実装**

`unity/Assets/Scripts/Interaction/InteractionPicker.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace HalfAware
{
    /// <summary>調べる対象の選択。距離と視線の角度、前提の判定だけを扱い、シーンには触れない</summary>
    public static class InteractionPicker
    {
        public const float DefaultRadius = 2f;
        /// <summary>ラジアン。視線からこの角度以内の対象だけ選ぶ</summary>
        public const float MaxAngle = 0.7f;

        /// <summary>After のうち、まだ済んでいない最初の id。すべて済んでいれば null</summary>
        public static string UnmetPrerequisite(IInteractable item, ICollection<string> done)
        {
            foreach (var id in item.After)
            {
                if (!done.Contains(id)) return id;
            }
            return null;
        }

        static bool HasHint(IInteractable item, string afterId)
        {
            var hint = item.HintFor(afterId);
            return hint != null && hint.Count > 0;
        }

        /// <summary>
        /// forward は正規化済みの視線方向。
        /// 距離が Radius 以内、視線からの角度が maxAngle 以内、前提が済んでいる対象のうち最も近いものを返す。
        /// 前提が未達でも、その id の文があれば選べる。該当が無ければ null
        /// </summary>
        public static IInteractable Select(
            Vector3 camPos,
            Vector3 forward,
            IEnumerable<IInteractable> items,
            ICollection<string> done,
            float maxAngle = MaxAngle)
        {
            IInteractable best = null;
            var bestDist = float.PositiveInfinity;
            foreach (var item in items)
            {
                if (item.Once && done.Contains(item.Id)) continue;
                var unmet = UnmetPrerequisite(item, done);
                if (unmet != null && !HasHint(item, unmet)) continue;
                var to = item.Position - camPos;
                var dist = to.magnitude;
                if (dist > item.Radius) continue;
                if (dist > 1e-6f)
                {
                    var cos = Vector3.Dot(to, forward) / dist;
                    if (Mathf.Acos(Mathf.Clamp(cos, -1f, 1f)) > maxAngle) continue;
                }
                if (dist < bestDist)
                {
                    best = item;
                    bestDist = dist;
                }
            }
            return best;
        }
    }
}
```

- [ ] **Step 5: テストが通ることを確認**

`refresh_unity` → `read_console`（エラー 0）→ `run_tests` → `get_test_job`。
Expected: 18 件 passed（Task 1 の 5 件 + 13 件）、failed 0。

- [ ] **Step 6: コミット**

```bash
cd /d/work/kataaware && git add unity/Assets/Scripts unity/Assets/Tests && git commit -m "feat: add the interaction picker with its selection rules"
```

---

### Task 3: `SceneProgress`

**Files:**
- Create: `unity/Assets/Scripts/Flow/SceneProgress.cs`
- Test: `unity/Assets/Tests/EditMode/SceneProgressTests.cs`

- [ ] **Step 1: 失敗するテストを書く**

`unity/Assets/Tests/EditMode/SceneProgressTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    public class SceneProgressTests
    {
        static FakeItem Item(string id, bool required = false)
        {
            var item = new FakeItem(id, Vector3.zero);
            item.Required = required;
            item.Lines = new[] { id + " の文" };
            return item;
        }

        [Test]
        public void CollectsRequiredIdsOnly()
        {
            var p = new SceneProgress(new[] { Item("a", true), Item("b"), Item("c", true) });
            Assert.That(p.Required, Is.EqualTo(new[] { "a", "c" }));
        }

        [Test]
        public void IsCompleteWhenEveryRequiredIdIsDone()
        {
            var a = Item("a", true);
            var c = Item("c", true);
            var p = new SceneProgress(new[] { a, Item("b"), c });
            Assert.That(p.IsComplete, Is.False);
            p.Examine(a);
            Assert.That(p.IsComplete, Is.False);
            p.Examine(c);
            Assert.That(p.IsComplete, Is.True);
        }

        [Test]
        public void IsCompleteWithoutRequiredItems()
        {
            Assert.That(new SceneProgress(new[] { Item("b") }).IsComplete, Is.True);
        }

        [Test]
        public void ExamineReturnsTheLinesAndMarksTheItemDone()
        {
            var a = Item("a");
            var p = new SceneProgress(new[] { a });
            Assert.That(p.Examine(a), Is.EqualTo(new[] { "a の文" }));
            Assert.That(p.Done, Does.Contain("a"));
        }

        [Test]
        public void ExamineWithAnUnmetPrerequisiteReturnsTheHintOnlyAndDoesNotMarkDone()
        {
            var door = Item("door");
            door.After = new[] { "terminal" };
            door.Hints["terminal"] = new[] { "先に端末" };
            var p = new SceneProgress(new[] { door });
            Assert.That(p.Examine(door), Is.EqualTo(new[] { "先に端末" }));
            Assert.That(p.Done, Does.Not.Contain("door"));
        }

        [Test]
        public void ExamineWithAnUnmetPrerequisiteAndNoHintReturnsNoLines()
        {
            var door = Item("door");
            door.After = new[] { "terminal" };
            var p = new SceneProgress(new[] { door });
            Assert.That(p.Examine(door), Is.Empty);
            Assert.That(p.Done, Does.Not.Contain("door"));
        }
    }
}
```

- [ ] **Step 2: 失敗を確認**

`refresh_unity` → `read_console`（`types: ["error"]`）。
Expected: `SceneProgress` が見つからないというコンパイルエラー。

- [ ] **Step 3: 実装**

`unity/Assets/Scripts/Flow/SceneProgress.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;

namespace HalfAware
{
    /// <summary>場面の進行の判定。済んだ対象の集合と必須の一覧を持ち、調べたときに出す文と完了を決める</summary>
    public sealed class SceneProgress
    {
        static readonly string[] NoLines = new string[0];
        readonly HashSet<string> done = new HashSet<string>();
        readonly List<string> required;

        public SceneProgress(IEnumerable<IInteractable> items)
        {
            required = items.Where(i => i.Required).Select(i => i.Id).ToList();
        }

        /// <summary>済んだ対象の id。InteractionPicker に渡す</summary>
        public ICollection<string> Done => done;

        public IReadOnlyList<string> Required => required;

        /// <summary>必須の対象をすべて調べたか</summary>
        public bool IsComplete => required.All(done.Contains);

        /// <summary>
        /// 調べたときに出す文を返す。前提が未達なら、その id の文だけ返して済んだことにはしない。
        /// 済ませた場合は Done に加える
        /// </summary>
        public IReadOnlyList<string> Examine(IInteractable item)
        {
            var unmet = InteractionPicker.UnmetPrerequisite(item, done);
            if (unmet != null) return item.HintFor(unmet) ?? NoLines;
            done.Add(item.Id);
            return item.Lines;
        }
    }
}
```

- [ ] **Step 4: テストが通ることを確認**

`refresh_unity` → `read_console`（エラー 0）→ `run_tests` → `get_test_job`。
Expected: 24 件 passed、failed 0。

- [ ] **Step 5: コミット**

```bash
cd /d/work/kataaware && git add unity/Assets/Scripts unity/Assets/Tests && git commit -m "feat: add the scene progress with the required-item completion"
```

---

### Task 4: `Interactable`、`PlayerController`、入力アセットの調整

MonoBehaviour なので単体テストは無い。コンパイルが通ることと、Task 8 の再生で確かめる。

**Files:**
- Create: `unity/Assets/Scripts/Interaction/Interactable.cs`
- Create: `unity/Assets/Scripts/Player/PlayerController.cs`
- Modify: `unity/Assets/InputSystem_Actions.inputactions`

- [ ] **Step 1: 入力アセットを調整する**

テンプレートの `Interact` には Hold（長押し）が付いているので外し、左クリックを追加する。リポジトリ直下で実行:

```bash
cd /d/work/kataaware && python - <<'EOF'
import json, uuid
path = 'unity/Assets/InputSystem_Actions.inputactions'
with open(path, encoding='utf-8') as f:
    data = json.load(f)
player = next(m for m in data['maps'] if m['name'] == 'Player')
interact = next(a for a in player['actions'] if a['name'] == 'Interact')
interact['interactions'] = ''
if not any(b['action'] == 'Interact' and b['path'] == '<Mouse>/leftButton' for b in player['bindings']):
    player['bindings'].append({
        'name': '',
        'id': str(uuid.uuid4()),
        'path': '<Mouse>/leftButton',
        'interactions': '',
        'processors': '',
        'groups': ';Keyboard&Mouse',
        'action': 'Interact',
        'isComposite': False,
        'isPartOfComposite': False,
    })
with open(path, 'w', encoding='utf-8', newline='\n') as f:
    json.dump(data, f, indent=4, ensure_ascii=False)
    f.write('\n')
print('ok')
EOF
```

確認: `git diff --stat unity/Assets/InputSystem_Actions.inputactions` に変更が出て、`grep -n '"interactions": "Hold"' unity/Assets/InputSystem_Actions.inputactions` が何も出さない。

- [ ] **Step 2: `Interactable` を書く**

`unity/Assets/Scripts/Interaction/Interactable.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HalfAware
{
    /// <summary>シーン上の調べる対象。位置はこの GameObject の位置。文面はこの段では仮の文を直接持ち、段階 3 で RoomScript に移す</summary>
    public sealed class Interactable : MonoBehaviour, IInteractable
    {
        [Serializable]
        public struct Hint
        {
            /// <summary>after に挙げた id</summary>
            public string after;
            /// <summary>その id が未達のときに出す文。出しても済んだことにはならない</summary>
            [TextArea] public string[] lines;
        }

        [SerializeField] string id;
        [SerializeField] float radius = InteractionPicker.DefaultRadius;
        [SerializeField] bool required;
        [SerializeField] bool once = true;
        [Tooltip("ここに挙げた id が済むまで選べない。hints に文がある id については選べて、その文だけ出る")]
        [SerializeField] string[] after = new string[0];
        [SerializeField] Hint[] hints = new Hint[0];
        [SerializeField] string label = "調べる";
        [SerializeField, TextArea] string[] lines = new string[0];

        public string Id => id;
        public Vector3 Position => transform.position;
        public float Radius => radius;
        public bool Required => required;
        public bool Once => once;
        public IReadOnlyList<string> After => after;
        public string Label => label;
        public IReadOnlyList<string> Lines => lines;

        public IReadOnlyList<string> HintFor(string afterId)
        {
            foreach (var hint in hints)
            {
                if (hint.after == afterId) return hint.lines;
            }
            return null;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = required ? new Color(1f, 0.6f, 0.2f) : new Color(0.6f, 0.8f, 1f);
            Gizmos.DrawWireSphere(transform.position, 0.1f);
        }
    }
}
```

- [ ] **Step 3: `PlayerController` を書く**

`unity/Assets/Scripts/Player/PlayerController.cs`:

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

namespace HalfAware
{
    /// <summary>
    /// 歩く・見回す。CharacterController と Input System の Player マップ（Move / Look / Interact）で動く。
    /// カーソルは画面のクリックでロックし、ロック中だけ見回しと移動と調べる操作を受け付ける。
    /// SceneFlow より先に Update が走るよう実行順を前に置く
    /// </summary>
    [DefaultExecutionOrder(-10)]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        public const float WalkSpeed = 2.6f;         // m/s
        public const float StandingEyeHeight = 1.6f;
        public const float PitchLimit = 80f;         // 度
        public const float LookSensitivity = 0.126f; // 度 / ピクセル。試作の 0.0022 rad/px と同じ

        [SerializeField] InputActionAsset actions;
        [SerializeField] Transform eye;

        CharacterController body;
        InputAction move;
        InputAction look;
        InputAction interact;
        float pitch;

        /// <summary>false の間は見回しだけできる（座っている、演出中など）</summary>
        public bool CanMove { get; set; } = true;

        /// <summary>足元からカメラまでの高さ。座位と立位で変える</summary>
        public float EyeHeight { get; set; } = StandingEyeHeight;

        /// <summary>カメラ。位置と向きの読み取りに使う</summary>
        public Transform Eye => eye;

        public static bool CursorLocked => Cursor.lockState == CursorLockMode.Locked;

        /// <summary>このフレームで調べる操作（E か左クリック）が押されたか。ロック中だけ true になる</summary>
        public bool InteractPressed { get; private set; }

        void Awake()
        {
            body = GetComponent<CharacterController>();
            var map = actions.FindActionMap("Player", true);
            move = map.FindAction("Move", true);
            look = map.FindAction("Look", true);
            interact = map.FindAction("Interact", true);
        }

        void OnEnable() => actions.FindActionMap("Player", true).Enable();

        void OnDisable() => actions.FindActionMap("Player", true).Disable();

        void Update()
        {
            InteractPressed = false;
            if (!CursorLocked)
            {
                // ロックするためのクリックは調べる操作に使わない
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) Lock();
                return;
            }
            InteractPressed = interact.WasPressedThisFrame();
            Look(look.ReadValue<Vector2>());
            eye.localPosition = new Vector3(0f, EyeHeight, 0f);
            if (CanMove) Walk(move.ReadValue<Vector2>());
        }

        static void Lock()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void Look(Vector2 delta)
        {
            transform.Rotate(0f, delta.x * LookSensitivity, 0f);
            pitch = Mathf.Clamp(pitch - delta.y * LookSensitivity, -PitchLimit, PitchLimit);
            eye.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        void Walk(Vector2 input)
        {
            var local = Vector3.ClampMagnitude(new Vector3(input.x, 0f, input.y), 1f);
            body.SimpleMove(transform.TransformDirection(local) * WalkSpeed);
        }
    }
}
```

- [ ] **Step 4: コンパイルを確認**

`refresh_unity` → `read_console`（`types: ["error", "warning"]`）。
Expected: エラー 0。`InputSystem_Actions.inputactions` の再取り込みで警告が出ないこと。

- [ ] **Step 5: テストが引き続き通ることを確認**

`run_tests` → `get_test_job`。
Expected: 24 件 passed。

- [ ] **Step 6: コミット**

```bash
cd /d/work/kataaware && git add unity/Assets/Scripts unity/Assets/InputSystem_Actions.inputactions && git commit -m "feat: add the interactable component and the player controller"
```

---

### Task 5: `HudView` と `SceneFlow`

**Files:**
- Create: `unity/Assets/Scripts/Hud/HudView.cs`
- Create: `unity/Assets/Scripts/Flow/SceneFlow.cs`

- [ ] **Step 1: `HudView` を書く**

`unity/Assets/Scripts/Hud/HudView.cs`:

```csharp
using TMPro;
using UnityEngine;

namespace HalfAware
{
    /// <summary>字幕（画面下の黒帯）、印（中央）、中央の文字。見せるだけで、何を出すかは SceneFlow が決める</summary>
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField] GameObject subtitleBand;
        [SerializeField] TMP_Text subtitleText;
        [SerializeField] TMP_Text promptText;
        [SerializeField] TMP_Text centerText;

        void Awake()
        {
            SetSubtitle(null);
            SetPrompt(null);
            SetCenter(null);
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
    }
}
```

- [ ] **Step 2: `SceneFlow` を書く**

`unity/Assets/Scripts/Flow/SceneFlow.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 歩いて調べる場面 1 つ分の進行。毎フレーム、対象の選択 → 印 → 調べる → 字幕 → 完了の判定の順に進める。
    /// 字幕の表示中は E とクリックを字幕の送りにだけ使い、調べる操作は受け付けない。
    /// 必須の対象をすべて調べ、字幕も出ていなければ完了。この段では中央に「（仮）続く」を出して止まる
    /// </summary>
    public sealed class SceneFlow : MonoBehaviour
    {
        public const string ToBeContinued = "（仮）続く";

        [SerializeField] PlayerController player;
        [SerializeField] HudView hud;
        [Tooltip("ラジアン。視線からこの角度以内の対象だけ選ぶ")]
        [SerializeField] float maxAngle = InteractionPicker.MaxAngle;

        readonly SubtitleQueue subtitles = new SubtitleQueue();
        List<IInteractable> items;
        SceneProgress progress;
        bool pendingInteract;

        public SceneProgress Progress => progress;
        public bool Completed { get; private set; }

        void Awake()
        {
            items = new List<IInteractable>(FindObjectsByType<Interactable>(FindObjectsSortMode.None));
            progress = new SceneProgress(items);
        }

        /// <summary>次のフレームで調べる操作を 1 回起こす。E キーの代わりに、再生中の動作確認から SendMessage で呼ぶ</summary>
        public void PressInteract() => pendingInteract = true;

        void Update()
        {
            if (Completed) return;
            var interact = player.InteractPressed || pendingInteract;
            pendingInteract = false;
            if (subtitles.IsTalking && interact)
            {
                subtitles.Advance();
                interact = false;
            }
            IInteractable selected = null;
            if (!subtitles.IsTalking)
            {
                var eye = player.Eye;
                selected = InteractionPicker.Select(eye.position, eye.forward, items, progress.Done, maxAngle);
            }
            hud.SetPrompt(selected != null ? "E  " + selected.Label : null);
            if (selected != null && interact) subtitles.Enqueue(progress.Examine(selected));
            hud.SetSubtitle(subtitles.Current);
            if (progress.IsComplete && !subtitles.IsTalking) Complete();
        }

        void Complete()
        {
            Completed = true;
            hud.SetPrompt(null);
            hud.SetSubtitle(null);
            hud.SetCenter(ToBeContinued);
        }
    }
}
```

- [ ] **Step 3: コンパイルとテストを確認**

`refresh_unity` → `read_console`（エラー 0）→ `run_tests` → `get_test_job`。
Expected: 24 件 passed。

- [ ] **Step 4: コミット**

```bash
cd /d/work/kataaware && git add unity/Assets/Scripts && git commit -m "feat: add the HUD view and the scene flow"
```

---

### Task 6: TextMeshPro の必須リソースと日本語フォント

字幕は日本語なので、TMP の既定フォント（LiberationSans）では出ない。Noto Sans JP（SIL Open Font License 1.1）を動的アトラスの TMP フォントにする。

**Files:**
- Create: `unity/Assets/TextMesh Pro/`（取り込みで生成）
- Create: `unity/Assets/Fonts/NotoSansJP-Regular.otf`
- Create: `unity/Assets/Fonts/NotoSansJP-Regular SDF.asset`
- Create: `unity/Assets/Fonts/LICENSES.md`

- [ ] **Step 1: TMP の必須リソースを取り込む**

メニューの「Import TMP Essential Resources」はダイアログを出すので、`mcp__UnityMCP__execute_code`（`action: "execute"`）で非対話に取り込む:

```csharp
var path = System.IO.Path.GetFullPath("Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage");
if (!System.IO.File.Exists(path)) return "not found: " + path;
AssetDatabase.ImportPackage(path, false);
AssetDatabase.Refresh();
return "imported " + path;
```

確認: `refresh_unity` の後、`ls "unity/Assets/TextMesh Pro/Resources"` に `TMP Settings.asset` がある。

- [ ] **Step 2: フォントを取得する（オーナーの許可が要る）**

親セッションがオーナーの許可を得てから行う。取得元と大きさ:

- `https://github.com/notofonts/noto-cjk/raw/main/Sans/SubsetOTF/JP/NotoSansJP-Regular.otf`（約 4.5 MB、OFL 1.1）

```bash
mkdir -p /d/work/kataaware/unity/Assets/Fonts && curl -L -o /d/work/kataaware/unity/Assets/Fonts/NotoSansJP-Regular.otf https://github.com/notofonts/noto-cjk/raw/main/Sans/SubsetOTF/JP/NotoSansJP-Regular.otf && ls -la /d/work/kataaware/unity/Assets/Fonts/
```

Expected: 4 MB 台のファイル。`file` か `head -c 4` で `OTTO` で始まることを確かめる（HTML が落ちてきていないこと）。404 のときは `https://fonts.google.com/noto/specimen/Noto+Sans+JP` の zip から `NotoSansJP-Regular.ttf` を取り、以下のパスをその名前に読み替える。

`unity/Assets/Fonts/LICENSES.md`:

```markdown
# フォントの出典

| ファイル | 出典 | 許諾 |
|---|---|---|
| `NotoSansJP-Regular.otf` | Noto Sans JP（notofonts/noto-cjk、Sans/SubsetOTF/JP） | SIL Open Font License 1.1 |
```

- [ ] **Step 3: TMP フォントアセットを作る**

`refresh_unity` でフォントを取り込んでから、`execute_code` で:

```csharp
var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansJP-Regular.otf");
if (font == null) return "font not imported";
var asset = TMPro.TMP_FontAsset.CreateFontAsset(font, 64, 6, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, TMPro.AtlasPopulationMode.Dynamic, true);
var path = "Assets/Fonts/NotoSansJP-Regular SDF.asset";
AssetDatabase.CreateAsset(asset, path);
asset.material.name = asset.name + " Material";
AssetDatabase.AddObjectToAsset(asset.material, asset);
asset.atlasTextures[0].name = asset.name + " Atlas";
AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
var settings = new SerializedObject(TMPro.TMP_Settings.instance);
settings.FindProperty("m_defaultFontAsset").objectReferenceValue = asset;
settings.ApplyModifiedPropertiesWithoutUndo();
AssetDatabase.SaveAssets();
AssetDatabase.Refresh();
return AssetDatabase.GetAssetPath(asset) + " / default=" + (TMPro.TMP_Settings.defaultFontAsset == asset);
```

Expected: 戻り値が `Assets/Fonts/NotoSansJP-Regular SDF.asset / default=True`。`read_console` でエラー 0。
うまくいかない場合は Window > TextMeshPro > Font Asset Creator で、Source Font = `NotoSansJP-Regular.otf`、Sampling Point Size = 64、Padding = 6、Atlas Resolution = 1024×1024、Render Mode = SDFAA、Character Set = ASCII で生成して同じパスに保存し、Inspector の Generation Settings で Atlas Population Mode を Dynamic にする。その場合はオーナーに手順を伝えて任せる。

- [ ] **Step 4: コミット**

```bash
cd /d/work/kataaware && git add "unity/Assets/TextMesh Pro" "unity/Assets/TextMesh Pro.meta" unity/Assets/Fonts unity/Assets/Fonts.meta && git commit -m "feat: import the TextMeshPro resources and add Noto Sans JP as the subtitle font"
```

---

### Task 7: 自室のシーン

`SampleScene` を `Room` に改名し、仮の箱・プレイヤー・調べる対象・HUD・進行を `execute_code` で組む。数値は試作 `prototype-three/src/data/scenes/room.ts` の Z を反転したもの。

**Files:**
- Modify: `unity/Assets/Scenes/SampleScene.unity` → `unity/Assets/Scenes/Room.unity`
- Create: `unity/Assets/Materials/Placeholder/*.mat`
- Delete: `unity/Assets/TutorialInfo/`, `unity/Assets/Readme.asset`
- Modify: `unity/ProjectSettings/EditorBuildSettings.asset`

- [ ] **Step 1: 現状を見る**

`manage_scene`（`action: "get_active"`）で `SampleScene.unity` が開いていることと、`manage_scene`（`action: "get_hierarchy"`）で `Main Camera`、`Directional Light`、`Global Volume` の 3 つだけであることを確かめる。他に物があればオーナーが置いたものなので、親セッションに戻す。

- [ ] **Step 2: 改名とテンプレートの残骸の削除**

`manage_scene`（`action: "save"`）の後、`execute_code`:

```csharp
var moved = AssetDatabase.MoveAsset("Assets/Scenes/SampleScene.unity", "Assets/Scenes/Room.unity");
if (!string.IsNullOrEmpty(moved)) return "move failed: " + moved;
AssetDatabase.DeleteAsset("Assets/TutorialInfo");
AssetDatabase.DeleteAsset("Assets/Readme.asset");
AssetDatabase.SaveAssets();
AssetDatabase.Refresh();
UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Room.unity");
EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Room.unity", true) };
return UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path;
```

Expected: 戻り値 `Assets/Scenes/Room.unity`。`read_console` でエラー 0（`Readme` 関連のエラーが出たら `refresh_unity` を 1 回挟んで再確認）。

- [ ] **Step 3: 仮の箱、明かり、プレイヤー、調べる対象を組む**

`execute_code`:

```csharp
var shader = Shader.Find("Universal Render Pipeline/Lit");
if (shader == null) return "URP Lit shader not found";
if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
if (!AssetDatabase.IsValidFolder("Assets/Materials/Placeholder")) AssetDatabase.CreateFolder("Assets/Materials", "Placeholder");
System.Func<string, Color, Material> material = (name, color) => {
    var path = "Assets/Materials/Placeholder/" + name + ".mat";
    var m = AssetDatabase.LoadAssetAtPath<Material>(path);
    if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
    m.SetColor("_BaseColor", color);
    EditorUtility.SetDirty(m);
    return m;
};
var wall = material("Wall", new Color(0.29f, 0.29f, 0.33f));
var ceiling = material("Ceiling", new Color(0.16f, 0.16f, 0.19f));
var floor = material("Floor", new Color(0.20f, 0.20f, 0.23f));
var door = material("Door", new Color(0.42f, 0.29f, 0.23f));
var desk = material("Desk", new Color(0.48f, 0.35f, 0.25f));
var screen = material("Screen", new Color(0.07f, 0.07f, 0.09f));
var table = material("SideTable", new Color(0.35f, 0.29f, 0.23f));
var hubMat = material("MemoryHub", new Color(0.23f, 0.23f, 0.29f));

// 6m 四方の自室。+Z 側の壁に机と端末、机の前が座る位置、その右に小さな卓、-Z 側の壁にドア
var room = new GameObject("Room");
System.Action<string, Vector3, Vector3, Material, bool> box = (name, pos, size, mat, collider) => {
    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
    go.name = name;
    go.transform.SetParent(room.transform, false);
    go.transform.localPosition = pos;
    go.transform.localScale = size;
    go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<BoxCollider>());
};
box("Floor", new Vector3(0f, -0.05f, 0f), new Vector3(6f, 0.1f, 6f), floor, true);
box("Ceiling", new Vector3(0f, 3.1f, 0f), new Vector3(6f, 0.2f, 6f), ceiling, false);
box("Wall_Front", new Vector3(0f, 1.5f, 3f), new Vector3(6f, 3f, 0.2f), wall, true);
box("Wall_Back", new Vector3(0f, 1.5f, -3f), new Vector3(6f, 3f, 0.2f), wall, true);
box("Wall_Left", new Vector3(-3f, 1.5f, 0f), new Vector3(0.2f, 3f, 6f), wall, true);
box("Wall_Right", new Vector3(3f, 1.5f, 0f), new Vector3(0.2f, 3f, 6f), wall, true);
box("Door", new Vector3(0.8f, 1.1f, -2.88f), new Vector3(0.9f, 2.2f, 0.06f), door, false);
box("Desk", new Vector3(1.5f, 0.4f, 2.2f), new Vector3(1.6f, 0.8f, 0.8f), desk, true);
box("Screen", new Vector3(1.5f, 1.05f, 2.5f), new Vector3(0.7f, 0.45f, 0.08f), screen, true);
box("SideTable", new Vector3(2.5f, 0.35f, 1.0f), new Vector3(0.6f, 0.7f, 0.6f), table, true);
box("MemoryHub", new Vector3(-1.8f, 0.4f, 2.2f), new Vector3(0.9f, 0.8f, 0.5f), hubMat, true);
// 天井は上からの光を遮らない（照明の調整は段階 4）
room.transform.Find("Ceiling").GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

// 部屋の中の明かり
var lightGo = new GameObject("RoomLight");
lightGo.transform.position = new Vector3(0f, 2.7f, 0f);
var light = lightGo.AddComponent<Light>();
light.type = LightType.Point;
light.range = 10f;
light.intensity = 3f;

// プレイヤー。端末の前に立ち、+Z（机）を向く。Main Camera を目の位置に付け替える
var player = new GameObject("Player");
player.transform.position = new Vector3(1.5f, 0.05f, 1.2f);
var cc = player.AddComponent<CharacterController>();
cc.height = 1.7f;
cc.radius = 0.3f;
cc.center = new Vector3(0f, 0.85f, 0f);
var cam = GameObject.Find("Main Camera");
if (cam == null) return "Main Camera not found";
cam.transform.SetParent(player.transform, false);
cam.transform.localPosition = new Vector3(0f, 1.6f, 0f);
cam.transform.localRotation = Quaternion.identity;
var camera = cam.GetComponent<Camera>();
camera.fieldOfView = 70f;
camera.nearClipPlane = 0.05f;
var pc = player.AddComponent(System.Type.GetType("HalfAware.PlayerController, HalfAware"));
var pcSo = new SerializedObject(pc);
pcSo.FindProperty("actions").objectReferenceValue = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>("Assets/InputSystem_Actions.inputactions");
pcSo.FindProperty("eye").objectReferenceValue = cam.transform;
pcSo.ApplyModifiedPropertiesWithoutUndo();

// 調べる対象。名前は Interactable_<id>。文面は仮
var itemsRoot = new GameObject("Interactables");
var itemType = System.Type.GetType("HalfAware.Interactable, HalfAware");
System.Action<string, Vector3, bool, string[], string, string[]> item = (id, pos, required, after, label, lines) => {
    var go = new GameObject("Interactable_" + id);
    go.transform.SetParent(itemsRoot.transform, false);
    go.transform.localPosition = pos;
    var so = new SerializedObject(go.AddComponent(itemType));
    so.FindProperty("id").stringValue = id;
    so.FindProperty("required").boolValue = required;
    so.FindProperty("label").stringValue = label;
    var a = so.FindProperty("after");
    a.arraySize = after.Length;
    for (int i = 0; i < after.Length; i++) a.GetArrayElementAtIndex(i).stringValue = after[i];
    var l = so.FindProperty("lines");
    l.arraySize = lines.Length;
    for (int i = 0; i < lines.Length; i++) l.GetArrayElementAtIndex(i).stringValue = lines[i];
    so.ApplyModifiedPropertiesWithoutUndo();
};
item("chips", new Vector3(-1.8f, 0.9f, 2.2f), true, new string[0], "チップを抜く", new[] { "（仮）メモリハブからチップを抜いた" });
item("terminal", new Vector3(1.5f, 1.05f, 2.4f), true, new[] { "chips" }, "端末", new[] { "（仮）端末を調べた", "（仮）字幕の 2 行目" });
item("door", new Vector3(0.8f, 1.2f, -2.8f), true, new[] { "terminal" }, "ドア", new[] { "（仮）ドアから出る" });
item("clipboard", new Vector3(2.2f, 0.9f, 2.0f), false, new string[0], "紙ばさみ", new[] { "（仮）紙ばさみ" });
item("ashtray", new Vector3(2.65f, 0.85f, 1.2f), false, new string[0], "灰皿", new[] { "（仮）灰皿" });

AssetDatabase.SaveAssets();
var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return "room built, " + itemsRoot.transform.childCount + " interactables";
```

Expected: 戻り値 `room built, 5 interactables`。`read_console` でエラー 0。`manage_scene`（`get_hierarchy`）に `Room`（11 個の子）、`RoomLight`、`Player`（子に `Main Camera`）、`Interactables`（5 個の子）が出る。

- [ ] **Step 4: HUD と進行を組む**

`execute_code`:

```csharp
var jp = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Fonts/NotoSansJP-Regular SDF.asset");
if (jp == null) return "font asset missing";
var canvasGo = new GameObject("Hud", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
var scaler = canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>();
scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
scaler.referenceResolution = new Vector2(1280f, 720f);
scaler.matchWidthOrHeight = 0.5f;

System.Func<string, Transform, TMPro.TextMeshProUGUI> text = (name, parent) => {
    var go = new GameObject(name, typeof(RectTransform));
    go.transform.SetParent(parent, false);
    var t = go.AddComponent<TMPro.TextMeshProUGUI>();
    t.font = jp;
    t.color = Color.white;
    t.alignment = TMPro.TextAlignmentOptions.Center;
    return t;
};

// 黒帯。画面下、幅いっぱい、高さ 120、下端から 40 上
var band = new GameObject("SubtitleBand", typeof(RectTransform));
band.transform.SetParent(canvasGo.transform, false);
band.AddComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.75f);
var bandRect = band.GetComponent<RectTransform>();
bandRect.anchorMin = new Vector2(0f, 0f);
bandRect.anchorMax = new Vector2(1f, 0f);
bandRect.pivot = new Vector2(0.5f, 0f);
bandRect.anchoredPosition = new Vector2(0f, 40f);
bandRect.sizeDelta = new Vector2(0f, 120f);
var subtitle = text("Subtitle", band.transform);
subtitle.rectTransform.anchorMin = Vector2.zero;
subtitle.rectTransform.anchorMax = Vector2.one;
subtitle.rectTransform.offsetMin = new Vector2(80f, 12f);
subtitle.rectTransform.offsetMax = new Vector2(-80f, -12f);
subtitle.fontSize = 28f;

// 印。中央のやや下
var prompt = text("Prompt", canvasGo.transform);
prompt.rectTransform.anchoredPosition = new Vector2(0f, -40f);
prompt.rectTransform.sizeDelta = new Vector2(800f, 40f);
prompt.fontSize = 22f;

// 中央の文字
var center = text("Center", canvasGo.transform);
center.rectTransform.sizeDelta = new Vector2(1000f, 80f);
center.fontSize = 40f;

var hud = canvasGo.AddComponent(System.Type.GetType("HalfAware.HudView, HalfAware"));
var hudSo = new SerializedObject(hud);
hudSo.FindProperty("subtitleBand").objectReferenceValue = band;
hudSo.FindProperty("subtitleText").objectReferenceValue = subtitle;
hudSo.FindProperty("promptText").objectReferenceValue = prompt;
hudSo.FindProperty("centerText").objectReferenceValue = center;
hudSo.ApplyModifiedPropertiesWithoutUndo();

var flowGo = new GameObject("SceneFlow");
var flow = flowGo.AddComponent(System.Type.GetType("HalfAware.SceneFlow, HalfAware"));
var flowSo = new SerializedObject(flow);
var playerType = System.Type.GetType("HalfAware.PlayerController, HalfAware");
flowSo.FindProperty("player").objectReferenceValue = GameObject.Find("Player").GetComponent(playerType);
flowSo.FindProperty("hud").objectReferenceValue = hud;
flowSo.ApplyModifiedPropertiesWithoutUndo();

var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return "hud and flow built";
```

Expected: 戻り値 `hud and flow built`。`read_console` でエラー 0。

- [ ] **Step 5: 保存されたことを確認してコミット**

```bash
cd /d/work/kataaware && git status --short unity/ && grep -c "Interactable_" unity/Assets/Scenes/Room.unity
```

Expected: `Room.unity`（新規）、`SampleScene.unity`（削除）、`Materials/`、`TutorialInfo/` と `Readme.asset`（削除）、`EditorBuildSettings.asset` が出て、grep が 5。

```bash
cd /d/work/kataaware && git add -A unity/Assets unity/ProjectSettings/EditorBuildSettings.asset && git commit -m "feat: build the placeholder room scene with the player, the interactables and the HUD"
```

---

### Task 8: 再生での確認

MCP で再生し、進行を外から起こして字幕と印と完了を確かめる。操作感と見た目の最終判断はオーナー。

**Files:** なし（直すものが出たら該当タスクのファイル）

- [ ] **Step 1: 再生してエラーを見る**

`manage_editor`（`action: "play"`）→ `read_console`（`types: ["error", "warning"]`）。
Expected: エラー 0。`InputActionAsset` や `TMP` の警告が出たら内容を記録して親セッションに報告する。

- [ ] **Step 2: 最初の画面を撮る**

`execute_code`:

```csharp
ScreenCapture.CaptureScreenshot("Temp/stage2-01-start.png");
return "queued";
```

数秒後に `unity/Temp/stage2-01-start.png` を Read で見る。
Expected: 正面に机と黒い画面の箱、右手に卓、部屋の壁。画面下に黒帯は出ていない（字幕なし）。

- [ ] **Step 3: 前提が済むまでドアが選べないことを見る**

`execute_code` でドアの前に立たせる（CharacterController を一度切ってから位置を変える）:

```csharp
var player = GameObject.Find("Player");
var cc = player.GetComponent<CharacterController>();
cc.enabled = false;
player.transform.position = new Vector3(0.8f, 0.05f, -1.6f);
player.transform.rotation = Quaternion.LookRotation(Vector3.back);
cc.enabled = true;
return player.transform.position.ToString();
```

次の `execute_code` で印を読む:

```csharp
var prompt = GameObject.Find("Hud").transform.Find("Prompt");
return prompt.gameObject.activeSelf + " | " + prompt.GetComponent<TMPro.TextMeshProUGUI>().text;
```

Expected: `False | `（ドアは `terminal` が未達で、文も無いので選べない）。

- [ ] **Step 4: メモリハブ → 端末 → ドアの順に調べる**

メモリハブの前へ:

```csharp
var player = GameObject.Find("Player");
var cc = player.GetComponent<CharacterController>();
cc.enabled = false;
player.transform.position = new Vector3(-1.8f, 0.05f, 0.9f);
player.transform.rotation = Quaternion.LookRotation(Vector3.forward);
cc.enabled = true;
return "at hub";
```

印を読む（Step 3 の読み取りと同じコード）。Expected: `True | E  チップを抜く`。

調べる:

```csharp
GameObject.Find("SceneFlow").SendMessage("PressInteract");
return "pressed";
```

字幕を読む:

```csharp
var band = GameObject.Find("Hud").transform.Find("SubtitleBand");
return band.gameObject.activeSelf + " | " + band.Find("Subtitle").GetComponent<TMPro.TextMeshProUGUI>().text;
```

Expected: `True | （仮）メモリハブからチップを抜いた`。ここで `ScreenCapture.CaptureScreenshot("Temp/stage2-02-subtitle.png")` を撮り、黒帯に日本語が出ていることを Read で見る（文字が □ なら Task 6 のフォントが効いていない）。

もう一度 `PressInteract`（字幕を送る）→ 字幕を読む。Expected: `False | `。

端末の前へ（開始位置と同じ）:

```csharp
var player = GameObject.Find("Player");
var cc = player.GetComponent<CharacterController>();
cc.enabled = false;
player.transform.position = new Vector3(1.5f, 0.05f, 1.2f);
player.transform.rotation = Quaternion.LookRotation(Vector3.forward);
cc.enabled = true;
return "at terminal";
```

印を読む。Expected: `True | E  端末`（紙ばさみは視線の角度の外なので選ばれない）。
`PressInteract` → 字幕 `True | （仮）端末を調べた` → `PressInteract` → 字幕 `True | （仮）字幕の 2 行目` → `PressInteract` → 字幕 `False | `。

ドアの前へ（Step 3 と同じ位置）→ 印を読む。Expected: `True | E  ドア`。
`PressInteract` → 字幕 `True | （仮）ドアから出る` → `PressInteract`。

中央の文字を読む:

```csharp
var center = GameObject.Find("Hud").transform.Find("Center");
return center.gameObject.activeSelf + " | " + center.GetComponent<TMPro.TextMeshProUGUI>().text;
```

Expected: `True | （仮）続く`。`ScreenCapture.CaptureScreenshot("Temp/stage2-03-end.png")` を撮って Read で見る。

- [ ] **Step 5: 停止してコンソールを見る**

`manage_editor`（`action: "stop"`）→ `read_console`（`types: ["error"]`）。
Expected: エラー 0。再生中に変えた位置はシーンに残らない（`git status` で `Room.unity` に差分が無い）。差分が出ていたら `git checkout unity/Assets/Scenes/Room.unity` で戻す。

- [ ] **Step 6: オーナーに手元の確認を頼む**

親セッションから次を伝える:
- Game ビューをクリックするとカーソルがロックされ、WASD で歩き、マウスで見回せる。Esc で外れる
- 対象に近づいて視線を向けると印が出て、E か左クリックで調べる。字幕は E かクリックで送る
- メモリハブ → 端末 → ドアの順で必須が終わると「（仮）続く」が出る
- 見た目（明るさ、視野角 70°、感度、歩く速さ 2.6 m/s）の違和感はオーナーが伝え、数値は該当スクリプトの定数か Inspector で直す

---

## 段階 3 に持ち越すこと

- 座位で開始、立ち上がり、目線の高さの補間（`PlayerController.CanMove` と `EyeHeight` は用意済み）
- ジャック、煙草と煙、眩暈の保持と解放、`freeze`、クレジットとタイトル（`RoomIntroDirector`、`DazeVolume`）
- 前提が未達のときの文（`Interactable.hints` は用意済み。シーンの値は未設定）
- 文面を `RoomScript`（ScriptableObject）に移し、`Interactable.lines` を id からの参照に変える
- 暗転と「続く」（`HudView` に黒い層を足す）
- WebGL では `Cursor.lockState` の変更がクリックのイベント内でしか効かないことがある。段階 5 で実機確認し、必要なら `PlayerController.Lock` を Input System のコールバックに移す
- WebGL の容量: Noto Sans JP は約 4.5 MB。段階 5 で必要なら使う文字だけに絞る
