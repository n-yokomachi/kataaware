# 場面 8（車内） 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 暗転明けに共用ガレージでオフロード車に乗り込み、以降は運転席の一人称のまま、5 つの景色の帯が暗転を挟んで移り変わる場面を作る。

**Architecture:** 車は原点に固定し、道のタイルを環にして手前へ送る（`DriveWorld`）。帯の進行は、きっかけの対象を調べて独白を送り切ることだけで進む状態機械（`BandClock`）が持ち、`DriveDirector` がそれを既存の `SceneFlow` / `HudView` に繋ぐ。判定に関わる部分はすべて MonoBehaviour の外に純粋な C# として出し、EditMode テストで確かめる。

**Tech Stack:** Unity 6000.3.24f1 / URP 17.3 Forward+ / Input System / TextMeshPro / Unity Test Framework（EditMode）/ MCP for Unity

**設計書:** `docs/superpowers/specs/2026-09-20-drive-scene-design.md`

---

## この計画の前提

**必ず守ること。**

- コミットメッセージ・ドキュメントに「Co-Authored-By」「Claude」「Anthropic」などのモデル名・ツール名を著述者として書かない。例外なし
- カタカナで通じる技術用語を和語に訳さない。マテリアル / テクスチャ / メッシュ / レンダラー / アセット / エディタ / ポリゴン / ログ / ピン / パーティクル / アニメーション / コライダー / シーン / プレハブ。「材質」「描画」「資産」「編集器」「鋲」「目印」「控え」「記録」は使わない。コメント・コミットメッセージ・UI 文言すべてに適用する
- コードのコメントは日本語。既存ファイルの書きぶりに合わせる（なぜそうしたかを書き、何をしているかは書かない）
- **エディタを触る前に必ず `mcpforunity://editor/state` を読む。** `is_playing` が true のあいだは `refresh_unity` も `execute_menu_item` もシーンの書き換えも行わない。再生中にこれをやるとドメインの再読み込みが走り、動いている MonoBehaviour の非直列化フィールドが消えて例外が毎フレーム出る
- `refresh_unity` は `refresh_triggered: false` を返してコンパイルが走らないことがある。そのときは `execute_code` で `UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.ForceSynchronousImport); UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();` を叩く
- Game ビューの大きさを変えない。確認用のカメラを作るときは `cam.enabled = false` と `HideFlags.HideAndDontSave` を付ける
- 秒数・文章量はオーナーが決める。この計画で入れるのは仮置きの値だけで、勝手に詰めない
- **帯はコードでは 0 から数える。** 設計書は読み物なので帯 1〜5 と書いてあるが、`Triggers[i]` も `Page(i)` も `Enter(i)` も 0 始まりで、設計書の「帯 1」がここでは 0 番にあたる。この計画の表もコメントもログの文言も、すべて 0 始まりで揃える

**Unity の EditMode テストの流儀:**

未定義の型を参照するテストを書くと、テストアセンブリごとコンパイルエラーになる。これが「テストが落ちている」状態にあたる。`read_console` にコンパイルエラーが出ていることを確認してから実装に進む。

テストの実行は MCP for Unity の `run_tests`（`mode: "EditMode"`）→ `get_test_job`（`job_id` を渡して `status` が `succeeded` になるまで）。

---

## ファイル構成

| ファイル | 受け持ち |
|---|---|
| `unity/Assets/Scripts/Data/DriveBand.cs` | 帯 1 つ分の値（名前・きっかけの id・速度・余韻・黒・フェード）と、帯の並び `DriveRoute` |
| `unity/Assets/Scripts/Flow/DriveIds.cs` | 場面 8 の対象と段の id |
| `unity/Assets/Scripts/Flow/BandClock.cs` | 帯の段取りの状態機械。走る → 独白 → 余韻 → 黒 → 明ける |
| `unity/Assets/Scripts/Fx/RoadRing.cs` | タイルの環の位置計算 |
| `unity/Assets/Scripts/Fx/DriveWorld.cs` | タイルと沿道を流す MonoBehaviour |
| `unity/Assets/Scripts/Flow/DriveDirector.cs` | 帯の進行を `SceneFlow` / `HudView` / `DriveWorld` に繋ぐ |
| `unity/Assets/Editor/WriteDriveScript.cs` | 文面を `DriveScript.asset` へ書き出す |
| `unity/Assets/Editor/BuildDrive.cs` | シーンを組み立てる |
| `unity/Assets/Editor/CheckDrive.cs` | 組み立ての最後に走る見直し |
| `unity/Assets/Data/DriveScript.asset` | 文面（書き出しで作る） |
| `unity/Assets/Scenes/Drive.unity` | シーン |

テストは `unity/Assets/Tests/EditMode/` に置く。

---

## Task 1: 帯の値と並び

**Files:**
- Create: `unity/Assets/Scripts/Data/DriveBand.cs`
- Test: `unity/Assets/Tests/EditMode/DriveRouteTests.cs`

- [ ] **Step 1: 落ちるテストを書く**

`unity/Assets/Tests/EditMode/DriveRouteTests.cs`:

```csharp
using NUnit.Framework;

namespace HalfAware.Tests
{
    public class DriveRouteTests
    {
        static DriveBand Band(string name, string trigger)
        {
            return new DriveBand
            {
                name = name,
                trigger = trigger,
                speed = 22f,
                rough = 1f,
                afterglow = 5f,
                black = 0.8f,
                fadeIn = 1.4f,
            };
        }

        [Test]
        public void AnEmptyRouteHasNoBands()
        {
            var route = new DriveRoute(null);
            Assert.AreEqual(0, route.Count);
            Assert.IsNull(route.At(0).trigger, "範囲の外は空の帯");
            Assert.IsFalse(route.IsLast(0), "帯がひとつも無いうちは最後にしない");
        }

        [Test]
        public void ItKeepsTheBandsInOrder()
        {
            var route = new DriveRoute(new[] { Band("夜", "a"), Band("朝", "b") });
            Assert.AreEqual(2, route.Count);
            Assert.AreEqual("夜", route.At(0).name);
            Assert.AreEqual("朝", route.At(1).name);
        }

        [Test]
        public void TheLastBandHasNothingAfterIt()
        {
            var route = new DriveRoute(new[] { Band("夜", "a"), Band("朝", "b") });
            Assert.IsFalse(route.IsLast(0));
            Assert.IsTrue(route.IsLast(1), "最後の帯の次は無い");
        }

        [Test]
        public void OutOfRangeIsTreatedAsTheLast()
        {
            var route = new DriveRoute(new[] { Band("夜", "a") });
            Assert.IsTrue(route.IsLast(0), "1 つしか無ければそれが最後");
            Assert.IsTrue(route.IsLast(9), "範囲を外れても落ちない");
            Assert.IsFalse(route.IsLast(-1), "まだどの帯にも入っていない");
        }

        [Test]
        public void ItFindsWhichBandATriggerBelongsTo()
        {
            var route = new DriveRoute(new[] { Band("夜", "a"), Band("朝", "b") });
            Assert.AreEqual(1, route.BandOf("b"));
            Assert.AreEqual(-1, route.BandOf("c"), "どの帯のきっかけでもない");
            Assert.AreEqual(-1, route.BandOf(null));
            Assert.AreEqual(-1, route.BandOf(""), "Inspector で空のままの id");
        }

        [Test]
        public void TheRouteDoesNotFollowLaterEditsToTheArray()
        {
            var source = new[] { Band("夜", "a") };
            var route = new DriveRoute(source);
            source[0].speed = 99f;
            Assert.AreEqual(22f, route.At(0).speed, 0.0001f, "渡された配列とは切り離して持つ");
        }
    }
}
```

- [ ] **Step 2: テストが落ちることを確かめる**

`run_tests`（EditMode）を走らせる前に `read_console` を見る。
Expected: `DriveBand`／`DriveRoute` が未定義でテストアセンブリがコンパイルエラー。

- [ ] **Step 3: 実装する**

`unity/Assets/Scripts/Data/DriveBand.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace HalfAware
{
    /// <summary>
    /// 景色の帯 1 つ分。尺に関わる値はここにしか無い。
    /// 秒数はオーナーが実画面を見てから決めるので、組み立て側には仮置きしか入れない
    /// </summary>
    [Serializable]
    public struct DriveBand
    {
        /// <summary>帯の名前。ログと見直しで使う</summary>
        public string name;
        /// <summary>この帯の独白を始める対象の id。調べるまで帯は終わらない</summary>
        public string trigger;
        /// <summary>走る速さ。m/s。タイルの送りと揺れがこの一つから出る</summary>
        public float speed;
        /// <summary>路面の粗さ。1 が舗装、未舗装はもっと大きい。車体の揺れ幅に掛かる</summary>
        public float rough;
        /// <summary>独白を送り切ってから黒へ切り替わるまでの秒数。黙って走る</summary>
        public float afterglow;
        /// <summary>黒のまま置く秒数。仮眠にあたる切れ目だけ長く取る</summary>
        public float black;
        /// <summary>黒から次の帯へ浮かび上がる秒数</summary>
        public float fadeIn;
    }

    /// <summary>
    /// 帯の並び。順送りと、きっかけの id からの引き当てだけを持つ。
    /// 範囲の外を渡されても落ちない。組み立ての途中で帯が空のことがある
    /// </summary>
    public sealed class DriveRoute
    {
        readonly DriveBand[] bands;

        public DriveRoute(IReadOnlyList<DriveBand> from)
        {
            if (from == null) { bands = new DriveBand[0]; return; }
            bands = new DriveBand[from.Count];
            for (var i = 0; i < from.Count; i++) bands[i] = from[i];
        }

        public int Count { get { return bands.Length; } }

        /// <summary>i 番目の帯。範囲の外なら空の帯</summary>
        public DriveBand At(int i)
        {
            return i >= 0 && i < bands.Length ? bands[i] : new DriveBand();
        }

        /// <summary>
        /// i が最後の帯か。帯がひとつも無いうちは最後にしない。
        /// 組み立て途中の場面が、入った瞬間に閉じてしまうのを防ぐ（SceneProgress.IsComplete と同じ構え）。
        /// -1 は「まだどの帯にも入っていない」の意で、これも最後として扱わない
        /// </summary>
        public bool IsLast(int i)
        {
            return bands.Length > 0 && i >= bands.Length - 1;
        }

        /// <summary>そのきっかけの id を持つ帯。どの帯のものでもなければ -1</summary>
        public int BandOf(string trigger)
        {
            if (string.IsNullOrEmpty(trigger)) return -1;
            for (var i = 0; i < bands.Length; i++)
                if (bands[i].trigger == trigger) return i;
            return -1;
        }
    }
}
```

- [ ] **Step 4: テストが通ることを確かめる**

`run_tests`（EditMode）→ `get_test_job`。
Expected: `failed: 0`。既存の 261 件に 6 件足して 267 件。

- [ ] **Step 5: commit**

```bash
git add unity/Assets/Scripts/Data/DriveBand.cs unity/Assets/Scripts/Data/DriveBand.cs.meta unity/Assets/Tests/EditMode/DriveRouteTests.cs unity/Assets/Tests/EditMode/DriveRouteTests.cs.meta
git commit -m "feat: lay out the stretches of road I mean to drive"
```

---

## Task 2: 場面 8 の id

**Files:**
- Create: `unity/Assets/Scripts/Flow/DriveIds.cs`
- Test: `unity/Assets/Tests/EditMode/DriveIdsTests.cs`

- [ ] **Step 1: 落ちるテストを書く**

`unity/Assets/Tests/EditMode/DriveIdsTests.cs`:

```csharp
using NUnit.Framework;

namespace HalfAware.Tests
{
    public class DriveIdsTests
    {
        [Test]
        public void ThePagesAreNumbered()
        {
            Assert.AreEqual("drive.band0", DriveIds.Page(0));
            Assert.AreEqual("drive.band3", DriveIds.Page(3));
        }

        [Test]
        public void ItTellsAPageFromAnObject()
        {
            Assert.IsTrue(DriveIds.IsPage("drive.band0"));
            Assert.IsFalse(DriveIds.IsPage(DriveIds.Chips), "対象は段ではない");
            Assert.IsFalse(DriveIds.IsPage(null));
            foreach (var id in DriveIds.Triggers)
                Assert.IsFalse(DriveIds.IsPage(id), "きっかけの対象が段に見えている: " + id);
            foreach (var id in new[] { DriveIds.Door, DriveIds.Radio, DriveIds.Pocket, DriveIds.Fuel })
                Assert.IsFalse(DriveIds.IsPage(id), "対象が段に見えている: " + id);
        }

        [Test]
        public void TheTriggersAreAllDifferent()
        {
            var all = DriveIds.Triggers;
            Assert.AreEqual(5, all.Count, "帯は 5 つ");
            CollectionAssert.AllItemsAreUnique(all);
            Assert.AreEqual(DriveIds.Window, all[4], "最後の帯のきっかけは窓");
        }
    }
}
```

- [ ] **Step 2: テストが落ちることを確かめる**

Expected: `DriveIds` が未定義でコンパイルエラー。

- [ ] **Step 3: 実装する**

`unity/Assets/Scripts/Flow/DriveIds.cs`:

```csharp
using System.Collections.Generic;

namespace HalfAware
{
    /// <summary>
    /// 場面 8 の対象につける id。シーンを組む側（BuildDrive）と、
    /// 文面を書き出す側（WriteDriveScript）と、進行を動かす側（DriveDirector）で
    /// 同じ綴りを使う。文字列を三か所に散らすと、どれかを直し忘れて黙る。
    ///
    /// 帯はここでは 0 から数える。設計書が「帯 1」と呼ぶものが 0 番にあたる
    /// </summary>
    public static class DriveIds
    {
        /// <summary>ガレージ。運転席のドアを調べると乗り込む</summary>
        public const string Door = "garage.door";

        /// <summary>帯 0 のきっかけ。助手席のメモリーチップの束</summary>
        public const string Chips = "drive.chips";
        /// <summary>帯 1 のきっかけ。アクセスログの写し</summary>
        public const string Log = "drive.log";
        /// <summary>帯 2 のきっかけ。ルームミラー</summary>
        public const string Mirror = "drive.mirror";
        /// <summary>帯 3 のきっかけ。メーターの脇の写真立て</summary>
        public const string Photo = "drive.photo";
        /// <summary>帯 4 のきっかけ。窓を開けると場面 9 へ</summary>
        public const string Window = "drive.window";

        static readonly string[] triggers = { Chips, Log, Mirror, Photo, Window };

        /// <summary>
        /// 帯の順に並べたきっかけの id。BuildDrive と WriteDriveScript が同じ並びを使う。
        /// 中身を書き換えられないよう読み取りだけで渡す（MarketSale と同じ構え）
        /// </summary>
        public static IReadOnlyList<string> Triggers { get { return triggers; } }

        /// <summary>ラジオ。帯を問わず置く、読んでも帯が進まない対象</summary>
        public const string Radio = "drive.radio";
        /// <summary>上着のポケット。同じく帯は進まない</summary>
        public const string Pocket = "drive.pocket";
        /// <summary>給油計。同じく帯は進まない</summary>
        public const string Fuel = "drive.fuel";

        /// <summary>i 番目の帯で出す独白の段</summary>
        public static string Page(int i)
        {
            return "drive.band" + i;
        }

        /// <summary>独白の段か。段には調べる対象が紐づかない</summary>
        public static bool IsPage(string id)
        {
            return id != null && id.StartsWith("drive.band");
        }
    }
}
```

- [ ] **Step 4: テストが通ることを確かめる**

Expected: `failed: 0`。270 件。

- [ ] **Step 5: commit**

```bash
git add unity/Assets/Scripts/Flow/DriveIds.cs unity/Assets/Scripts/Flow/DriveIds.cs.meta unity/Assets/Tests/EditMode/DriveIdsTests.cs unity/Assets/Tests/EditMode/DriveIdsTests.cs.meta
git commit -m "feat: name what I can reach for from the driver's seat"
```

---

## Task 3: 帯の段取り

帯が「走る → 独白 → 余韻 → 黒 → 明ける」と進む状態機械。**ここが設計書 6 節の核**で、経過時間でも走行距離でも帯が終わらないことをここで担保する。

**Files:**
- Create: `unity/Assets/Scripts/Flow/BandClock.cs`
- Test: `unity/Assets/Tests/EditMode/BandClockTests.cs`

- [ ] **Step 1: 落ちるテストを書く**

`unity/Assets/Tests/EditMode/BandClockTests.cs`:

```csharp
using NUnit.Framework;

namespace HalfAware.Tests
{
    public class BandClockTests
    {
        static DriveBand Band()
        {
            return new DriveBand
            {
                name = "夜の高速",
                trigger = "drive.log",
                speed = 22f,
                rough = 1f,
                afterglow = 5f,
                black = 0.8f,
                fadeIn = 1.4f,
            };
        }

        /// <summary>seconds 秒ぶん、細かく刻んで進める</summary>
        static void Run(BandClock clock, DriveBand band, float seconds)
        {
            for (var t = 0f; t < seconds; t += 0.1f) clock.Tick(0.1f, band);
        }

        [Test]
        public void ItStartsByJustDriving()
        {
            var clock = new BandClock();
            Assert.AreEqual(DriveBeat.Running, clock.Beat);
        }

        [Test]
        public void TimeAloneNeverEndsTheBand()
        {
            var clock = new BandClock();
            Run(clock, Band(), 600f);
            Assert.AreEqual(DriveBeat.Running, clock.Beat, "調べるまで何時間でも走っていられる");
        }

        [Test]
        public void ExaminingTheTriggerStartsTheTalking()
        {
            var clock = new BandClock();
            clock.Trigger();
            Assert.AreEqual(DriveBeat.Talking, clock.Beat);
        }

        [Test]
        public void TalkingDoesNotTimeOut()
        {
            var clock = new BandClock();
            clock.Trigger();
            Run(clock, Band(), 600f);
            Assert.AreEqual(DriveBeat.Talking, clock.Beat, "読む速さはプレイヤー次第");
        }

        [Test]
        public void FinishingTheLinesOpensTheAfterglow()
        {
            var clock = new BandClock();
            clock.Trigger();
            clock.Spoken();
            Assert.AreEqual(DriveBeat.Afterglow, clock.Beat, "送り切ってもすぐには暗転しない");
        }

        [Test]
        public void TheAfterglowHoldsForItsSeconds()
        {
            var clock = new BandClock();
            var band = Band();
            clock.Trigger();
            clock.Spoken();
            Run(clock, band, 4.5f);
            Assert.AreEqual(DriveBeat.Afterglow, clock.Beat, "5 秒に満たないうちは黙って走ったまま");
            Run(clock, band, 1.0f);
            Assert.AreEqual(DriveBeat.Black, clock.Beat);
        }

        [Test]
        public void ItGoesBlackInOneStep()
        {
            var clock = new BandClock();
            var band = Band();
            clock.Trigger();
            clock.Spoken();
            Run(clock, band, 4.9f);
            Assert.AreEqual(0f, clock.Dark(band), 0.0001f, "余韻のあいだは一切暗くならない");
            Run(clock, band, 0.3f);
            Assert.AreEqual(DriveBeat.Black, clock.Beat);
            Assert.AreEqual(1f, clock.Dark(band), 0.0001f, "黒へは切り替えで入る。フェードアウトしない");
        }

        [Test]
        public void ItComesUpGradually()
        {
            var clock = new BandClock();
            var band = Band();
            clock.Trigger();
            clock.Spoken();
            Run(clock, band, 5.1f + 0.9f);          // 余韻 5 と黒 0.8 を越える
            Assert.AreEqual(DriveBeat.FadingIn, clock.Beat);
            var first = clock.Dark(band);
            Run(clock, band, 0.7f);
            var later = clock.Dark(band);
            Assert.Less(later, first, "少しずつ明るくなる");
            Assert.Greater(later, 0f, "まだ明け切っていない");
        }

        [Test]
        public void WhenItHasComeUpItIsDrivingAgain()
        {
            var clock = new BandClock();
            var band = Band();
            clock.Trigger();
            clock.Spoken();
            Run(clock, band, 5.1f + 0.9f + 1.5f);
            Assert.AreEqual(DriveBeat.Running, clock.Beat);
            Assert.AreEqual(0f, clock.Dark(band), 0.0001f);
        }

        [Test]
        public void ZeroSecondsDoesNotHang()
        {
            var clock = new BandClock();
            var band = new DriveBand { afterglow = 0f, black = 0f, fadeIn = 0f };
            clock.Trigger();
            clock.Spoken();
            clock.Tick(0.016f, band);
            Assert.AreEqual(DriveBeat.Running, clock.Beat, "全部 0 でも一巡して走りに戻る");
        }

        [Test]
        public void ItReportsWhenTheSceneryShouldBeSwapped()
        {
            var clock = new BandClock();
            var band = Band();
            clock.Trigger();
            clock.Spoken();
            Run(clock, band, 4.9f);
            Assert.IsFalse(clock.TakeSwap(), "まだ黒くない");
            Run(clock, band, 0.3f);
            Assert.IsTrue(clock.TakeSwap(), "黒へ入った一度だけ知らせる");
            Assert.IsFalse(clock.TakeSwap(), "二度は知らせない");
        }
    }
}
```

- [ ] **Step 2: テストが落ちることを確かめる**

Expected: `BandClock`／`DriveBeat` が未定義でコンパイルエラー。

- [ ] **Step 3: 実装する**

`unity/Assets/Scripts/Flow/BandClock.cs`:

```csharp
using UnityEngine;

namespace HalfAware
{
    /// <summary>帯の今の段取り</summary>
    public enum DriveBeat
    {
        /// <summary>黙って走っている。きっかけを調べるまでここから動かない</summary>
        Running,
        /// <summary>独白が出ている。送り切るまでここから動かない</summary>
        Talking,
        /// <summary>送り切ったあとの余韻。また黙って走る</summary>
        Afterglow,
        /// <summary>黒。裏で次の帯を並べる</summary>
        Black,
        /// <summary>黒から浮かび上がっている</summary>
        FadingIn,
    }

    /// <summary>
    /// 帯の段取り。走る → 独白 → 余韻 → 黒 → 明ける → 走る。
    ///
    /// 走りと独白には終わりの時間を持たせない。きっかけを調べるまで、
    /// そして独白を送り切るまで、いくら時間が経っても次へ行かない。
    /// 時間で動くのは余韻から先の三つだけで、そこが演出の長さにあたる。
    ///
    /// MonoBehaviour の外に出してあるのは、これを丸ごとテストで確かめたいため
    /// </summary>
    public sealed class BandClock
    {
        float since;
        bool swapped;

        public DriveBeat Beat { get; private set; }

        /// <summary>きっかけの対象を調べた。独白が始まる</summary>
        public void Trigger()
        {
            if (Beat != DriveBeat.Running) return;
            Beat = DriveBeat.Talking;
            since = 0f;
        }

        /// <summary>独白を送り切った。ここから余韻</summary>
        public void Spoken()
        {
            if (Beat != DriveBeat.Talking) return;
            Beat = DriveBeat.Afterglow;
            since = 0f;
        }

        /// <summary>
        /// 黒へ入った最初の一度だけ true を返す。呼んだら下ろす。
        /// 景色の入れ替えは黒のあいだにやりたいが、毎フレーム並べ直したくない
        /// </summary>
        public bool TakeSwap()
        {
            if (!swapped) return false;
            swapped = false;
            return true;
        }

        /// <summary>
        /// 時を進める。Running と Talking では何も起きない。
        /// 秒数が 0 でも詰まらないよう、1 回の Tick で先まで進めることがある
        /// </summary>
        public void Tick(float dt, DriveBand band)
        {
            if (Beat == DriveBeat.Running || Beat == DriveBeat.Talking) return;
            since += dt;
            // 秒数が 0 のときに 1 フレーム 1 段ずつ進むと、途中の段が見えてしまう。
            // 越えた分をそのまま次へ持ち越して、その場で最後まで進める
            for (var guard = 0; guard < 4; guard++)
            {
                if (Beat == DriveBeat.Afterglow)
                {
                    if (since < band.afterglow) return;
                    since -= band.afterglow;
                    Beat = DriveBeat.Black;
                    swapped = true;
                    continue;
                }
                if (Beat == DriveBeat.Black)
                {
                    if (since < band.black) return;
                    since -= band.black;
                    Beat = DriveBeat.FadingIn;
                    continue;
                }
                if (Beat == DriveBeat.FadingIn)
                {
                    if (since < band.fadeIn) return;
                    since = 0f;
                    Beat = DriveBeat.Running;
                    return;
                }
                return;
            }
        }

        /// <summary>
        /// 今どれだけ黒いか。0 で素通し、1 で真っ黒。
        /// 黒へは切り替えで入るので、Afterglow の 0 から Black の 1 へ一息に跳ぶ
        /// </summary>
        public float Dark(DriveBand band)
        {
            if (Beat == DriveBeat.Black) return 1f;
            if (Beat != DriveBeat.FadingIn) return 0f;
            if (band.fadeIn <= 0f) return 0f;
            return Mathf.Clamp01(1f - since / band.fadeIn);
        }

        /// <summary>頭から組み直す。帯を跨ぐたびに呼ぶ</summary>
        public void Reset()
        {
            Beat = DriveBeat.Running;
            since = 0f;
            swapped = false;
        }
    }
}
```

- [ ] **Step 4: テストが通ることを確かめる**

Expected: `failed: 0`。281 件。

- [ ] **Step 5: commit**

```bash
git add unity/Assets/Scripts/Flow/BandClock.cs unity/Assets/Scripts/Flow/BandClock.cs.meta unity/Assets/Tests/EditMode/BandClockTests.cs unity/Assets/Tests/EditMode/BandClockTests.cs.meta
git commit -m "feat: let the road wait for me, not the clock"
```

---

## Task 4: タイルの環

**Files:**
- Create: `unity/Assets/Scripts/Fx/RoadRing.cs`
- Test: `unity/Assets/Tests/EditMode/RoadRingTests.cs`

- [ ] **Step 1: 落ちるテストを書く**

`unity/Assets/Tests/EditMode/RoadRingTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    public class RoadRingTests
    {
        const int Tiles = 8;
        const float Length = 20f;
        const float Behind = -30f;

        [Test]
        public void TilesSitOneLengthApart()
        {
            var zs = new float[Tiles];
            for (var i = 0; i < Tiles; i++) zs[i] = RoadRing.Slot(i, Tiles, Length, 0f, Behind);
            System.Array.Sort(zs);
            for (var i = 1; i < Tiles; i++)
                Assert.AreEqual(Length, zs[i] - zs[i - 1], 0.0001f, "隙間も重なりも無い");
        }

        [Test]
        public void TheWholeRingCoversTheSpan()
        {
            var zs = new float[Tiles];
            for (var i = 0; i < Tiles; i++) zs[i] = RoadRing.Slot(i, Tiles, Length, 7.3f, Behind);
            System.Array.Sort(zs);
            Assert.GreaterOrEqual(zs[0], Behind, "後ろの端より手前には来ない");
            Assert.Less(zs[Tiles - 1], Behind + Tiles * Length, "前の端は越えない");
        }

        [Test]
        public void DrivingOneTileLengthBringsTheRingBack()
        {
            for (var i = 0; i < Tiles; i++)
            {
                var start = RoadRing.Slot(i, Tiles, Length, 0f, Behind);
                var later = RoadRing.Slot(i, Tiles, Length, Tiles * Length, Behind);
                Assert.AreEqual(start, later, 0.0001f, "一周すると並びが元に戻る");
            }
        }

        [Test]
        public void TilesMoveTowardsTheCar()
        {
            var before = RoadRing.Slot(3, Tiles, Length, 0f, Behind);
            var after = RoadRing.Slot(3, Tiles, Length, 2f, Behind);
            Assert.Less(after, before, "走ると手前へ寄ってくる");
        }

        [Test]
        public void GoingBackwardsDoesNotBreakIt()
        {
            var z = RoadRing.Slot(2, Tiles, Length, -45f, Behind);
            Assert.GreaterOrEqual(z, Behind);
            Assert.Less(z, Behind + Tiles * Length);
        }

        [Test]
        public void ARingOfNothingIsHarmless()
        {
            Assert.AreEqual(Behind, RoadRing.Slot(0, 0, Length, 5f, Behind), 0.0001f);
            Assert.AreEqual(Behind, RoadRing.Slot(0, Tiles, 0f, 5f, Behind), 0.0001f);
        }

        [Test]
        public void ItCountsHowManyTilesReachAhead()
        {
            // 前 140 と後ろ 30 で 170 m。1 枚 20 m なら 9 枚
            Assert.AreEqual(9, RoadRing.Needed(140f, Length, Behind));
            Assert.GreaterOrEqual(RoadRing.Needed(140f, Length, Behind) * Length, 140f - Behind,
                "環の長さが見える範囲を覆っていないと、前の端に穴が空く");
        }

        [Test]
        public void AShortRoadStillNeedsTwoTiles()
        {
            Assert.AreEqual(2, RoadRing.Needed(1f, 100f, -1f), "1 枚だと送った瞬間に消える");
            Assert.AreEqual(0, RoadRing.Needed(140f, 0f, Behind), "長さが 0 なら敷きようがない");
        }
    }
}
```

- [ ] **Step 2: テストが落ちることを確かめる**

Expected: `RoadRing` が未定義でコンパイルエラー。

- [ ] **Step 3: 実装する**

`unity/Assets/Scripts/Fx/RoadRing.cs`:

```csharp
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 道のタイルを環にして流す計算。
    ///
    /// 車は原点に置いたままで、道の方を手前（-z）へ送る。後ろへ抜けたタイルは
    /// 環の一番先へ回すので、何分走っても座標は原点の周りに留まる。
    /// 実寸で何 km も敷くと座標が大きくなり、揺れや影が粗くなるため。
    ///
    /// MonoBehaviour の外に出してあるのは、隙間や重なりをテストで確かめたいため
    /// </summary>
    public static class RoadRing
    {
        /// <summary>z を [behind, behind + span) に畳む</summary>
        public static float Wrap(float z, float span, float behind)
        {
            if (span <= 0f) return behind;
            var t = (z - behind) % span;
            if (t < 0f) t += span;
            return behind + t;
        }

        /// <summary>
        /// i 枚目のタイルの z。travelled は走った距離。
        /// behind は車の後ろのどこまでタイルを残すか（負の値）
        /// </summary>
        public static float Slot(int i, int n, float length, float travelled, float behind)
        {
            if (n <= 0 || length <= 0f) return behind;
            return Wrap(i * length - travelled, n * length, behind);
        }

        /// <summary>
        /// 前方 ahead まで途切れず敷くのに要る枚数。
        /// 環の長さが前後の見える範囲を覆えばよい
        /// </summary>
        public static int Needed(float ahead, float length, float behind)
        {
            if (length <= 0f) return 0;
            return Mathf.Max(2, Mathf.CeilToInt((ahead - behind) / length));
        }
    }
}
```

- [ ] **Step 4: テストが通ることを確かめる**

Expected: `failed: 0`。289 件。

落ちたら実装を直す。**テストの期待値を実装に寄せて黙らせない。**

- [ ] **Step 5: commit**

```bash
git add unity/Assets/Scripts/Fx/RoadRing.cs unity/Assets/Scripts/Fx/RoadRing.cs.meta unity/Assets/Tests/EditMode/RoadRingTests.cs unity/Assets/Tests/EditMode/RoadRingTests.cs.meta
git commit -m "feat: loop the road so I never leave the origin"
```

---

## Task 5: 世界を流す

タイルと沿道を実際に動かす MonoBehaviour。計算は Task 4 の `RoadRing` に任せ、ここは Transform を動かすだけにする。

**Files:**
- Create: `unity/Assets/Scripts/Fx/DriveWorld.cs`

- [ ] **Step 1: 実装する**

`unity/Assets/Scripts/Fx/DriveWorld.cs`:

```csharp
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 道と沿道を流す。車は原点に置いたままで、世界の方を手前へ送る。
    ///
    /// タイルは組み立てのときに作って渡してもらう。ここでは位置だけ動かす。
    /// 帯を跨ぐときは Dress を呼んで、沿道の並びを今の帯のものに入れ替える
    /// </summary>
    public sealed class DriveWorld : MonoBehaviour
    {
        [Tooltip("道のタイル。環にして流す。順番は問わない")]
        [SerializeField] Transform[] tiles = new Transform[0];
        [Tooltip("タイル 1 枚の長さ。m")]
        [SerializeField] float tileLength = 20f;
        [Tooltip("車の後ろのどこまで残すか。負の値")]
        [SerializeField] float behind = -30f;
        [Tooltip("帯ごとの沿道。今の帯のものだけ出す")]
        [SerializeField] Transform[] roadsides = new Transform[0];

        /// <summary>走る速さ。m/s。0 で止まる</summary>
        public float Speed { get; set; }

        /// <summary>走り出したか。乗り込むまでは動かさない</summary>
        public bool Rolling { get; set; }

        /// <summary>走った距離。揺れと音がこれを見る</summary>
        public float Travelled { get; private set; }

        void Update()
        {
            if (!Rolling) return;
            Travelled += Speed * Time.deltaTime;
            Place();
        }

        /// <summary>今の走行距離でタイルを並べ直す</summary>
        public void Place()
        {
            for (var i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] == null) continue;
                var z = RoadRing.Slot(i, tiles.Length, tileLength, Travelled, behind);
                var at = tiles[i].localPosition;
                tiles[i].localPosition = new Vector3(at.x, at.y, z);
            }
        }

        /// <summary>which 番目の帯の沿道だけ出す。-1 でどれも出さない</summary>
        public void Dress(int which)
        {
            for (var i = 0; i < roadsides.Length; i++)
                if (roadsides[i] != null) roadsides[i].gameObject.SetActive(i == which);
        }

        /// <summary>頭から走り直す。帯を跨ぐときに距離を戻すと、沿道の並びも頭から出る</summary>
        public void Rewind()
        {
            Travelled = 0f;
            Place();
        }
    }
}
```

- [ ] **Step 2: コンパイルを通す**

`mcpforunity://editor/state` を読み、`is_playing` が false かつ `phase` が `idle` であることを確かめてから
`execute_code` で `AssetDatabase.Refresh(ForceSynchronousImport)` と `CompilationPipeline.RequestScriptCompilation()`。
`read_console`（types: error）。
Expected: 0 件。

- [ ] **Step 3: テストを走らせて崩れていないことを確かめる**

Expected: `failed: 0`。289 件のまま。

- [ ] **Step 4: commit**

```bash
git add unity/Assets/Scripts/Fx/DriveWorld.cs unity/Assets/Scripts/Fx/DriveWorld.cs.meta
git commit -m "feat: hold the car still and send the world past it"
```

---

## Task 6: 文面を書き出す

**Files:**
- Create: `unity/Assets/Editor/WriteDriveScript.cs`
- Create（生成物）: `unity/Assets/Data/DriveScript.asset`
- Test: `unity/Assets/Tests/EditMode/DriveScriptAssetTests.cs`

参考にする既存ファイル: `unity/Assets/Editor/WriteAlleyScript.cs`、`unity/Assets/Tests/EditMode/RoomScriptAssetTests.cs`

- [ ] **Step 1: 書き出しを実装する**

`unity/Assets/Editor/WriteDriveScript.cs`:

```csharp
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 8 の文面を書き出す。原作の車中の独白をここに持ち、
    /// `Assets/Data/DriveScript.asset` へ流し込む。
    ///
    /// 文面をアセットへ置くのは、位置と規則（シーン）と文（アセット）を分けるため。
    /// ただし手で YAML を書くと日本語が読めなくなるので、元はこのファイルに置いて
    /// メニューから書き出す。**台詞を直すならここを直して走らせ直す。**
    ///
    /// 独白の本文と行数はオーナーが決める。ここに入れてあるのは原作からの抜きで、
    /// 帯ごとの分け方も含めて差し替えてよい
    /// </summary>
    public static class WriteDriveScript
    {
        public const string Path = "Assets/Data/DriveScript.asset";

        /// <summary>帯ごとの独白。DriveIds.Triggers と同じ並び</summary>
        public static readonly string[][] BandPages =
        {
            new[]
            {
                "ナーヴ・ターミナルのローカルストレージにデータを保存した。日は暮れていた",
                "自動運転に任せて、私は腕組みをしながら考える",
            },
            new[]
            {
                "思えばこの時のために、私は幾人もの思い出を盗み見てきた",
                "誰かの記憶の中に私がいれば、きっとその人は私のことを知っている",
            },
            new[]
            {
                "思い出を持たない私の代わりに、私のことを記憶してくれている人がいる。そんな人を私はずっと探していた",
                "だが、この時が思わぬ形で訪れたことに、私はまだ動揺していた",
            },
            new[]
            {
                "今向かっている先にいる人が私のことを知っている確証はない",
                "かと言ってこの巡りあわせを逃す手もない。ただ私と同じ感覚を持っているという点だけが今は手掛かりなのだ",
            },
            new[]
            {
                "この匂いを覚えている。あの記憶の中で嗅いだ匂いだ",
            },
        };

        /// <summary>きっかけの対象そのものの文。段の前に出る</summary>
        public static readonly string[] TriggerLines =
        {
            "助手席に、売れ残りのメモリーチップの束が転がっている",
            "アクセスログの写し。エディンバラから少し離れた田舎町の名がある",
            "ルームミラー。角度が悪くて、自分の顔は映らない",
            "メーターの脇に、誰のものとも知れない古い写真立てが挟んである",
            "窓を開けて、大きく息を吸い込んだ",
        };

        /// <summary>きっかけの対象の印に出す文</summary>
        public static readonly string[] TriggerLabels =
        {
            "チップの束を見る",
            "アクセスログを見る",
            "ルームミラーを見る",
            "写真立てを見る",
            "窓を開ける",
        };

        [MenuItem("HalfAware/Write the drive script", false, 220)]
        public static void Menu()
        {
            Write();
        }

        public static void Write()
        {
            var entries = new List<ScriptEntry>();

            // ガレージ。運転席のドアを調べると乗り込む
            entries.Add(new ScriptEntry
            {
                id = DriveIds.Door,
                label = "車に乗り込む",
                lines = new[] { "ボロのオフロード車。ドアの立て付けは相変わらず悪い" },
                hints = new ScriptHint[0],
            });

            // 帯ごとのきっかけ。対象の文のあとに、DriveDirector が段を積む
            for (var i = 0; i < DriveIds.Triggers.Count; i++)
            {
                entries.Add(new ScriptEntry
                {
                    id = DriveIds.Triggers[i],
                    label = TriggerLabels[i],
                    lines = new[] { TriggerLines[i] },
                    hints = new ScriptHint[0],
                });
                entries.Add(new ScriptEntry
                {
                    id = DriveIds.Page(i),
                    label = "",
                    lines = BandPages[i],
                    hints = new ScriptHint[0],
                });
            }

            // 帯を問わず置く、読んでも帯が進まない対象
            entries.Add(new ScriptEntry
            {
                id = DriveIds.Radio,
                label = "ラジオをつける",
                lines = new[] { "雑音しか拾わない。この辺りはもう中継塔の外だ" },
                hints = new ScriptHint[0],
            });
            entries.Add(new ScriptEntry
            {
                id = DriveIds.Pocket,
                label = "上着のポケットを探る",
                lines = new[] { "煙草の箱。買い手から押し付けられたやつだ" },
                hints = new ScriptHint[0],
            });
            entries.Add(new ScriptEntry
            {
                id = DriveIds.Fuel,
                label = "給油計を見る",
                lines = new[] { "半分を切っている。町に着くまでは保つ" },
                hints = new ScriptHint[0],
            });

            var asset = AssetDatabase.LoadAssetAtPath<RoomScript>(Path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<RoomScript>();
                AssetDatabase.CreateAsset(asset, Path);
            }
            var so = new SerializedObject(asset);
            var list = so.FindProperty("entries");
            list.arraySize = entries.Count;
            for (var i = 0; i < entries.Count; i++) Fill(list.GetArrayElementAtIndex(i), entries[i]);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log(string.Format("場面 8 の文面を書き出した。{0} 項目 → {1}", entries.Count, Path));
        }

        static void Fill(SerializedProperty at, ScriptEntry entry)
        {
            at.FindPropertyRelative("id").stringValue = entry.id;
            at.FindPropertyRelative("label").stringValue = entry.label ?? "";
            var lines = at.FindPropertyRelative("lines");
            lines.arraySize = entry.lines == null ? 0 : entry.lines.Length;
            for (var i = 0; i < lines.arraySize; i++)
                lines.GetArrayElementAtIndex(i).stringValue = entry.lines[i];
            at.FindPropertyRelative("hints").arraySize = 0;
            at.FindPropertyRelative("choice").FindPropertyRelative("question").stringValue = "";
            at.FindPropertyRelative("choice").FindPropertyRelative("afterYes").arraySize = 0;
        }
    }
}
```

`WriteAlleyScript.cs` の `Fill` と同じ書き方になっているか照らし合わせ、違っていれば既存に合わせる（既存が正）。

- [ ] **Step 2: コンパイルして書き出す**

`mcpforunity://editor/state` を確かめてからコンパイル。`read_console`（types: error）が 0 件であることを確かめ、
`execute_menu_item`（`HalfAware/Write the drive script`）。
Expected: ログに「場面 8 の文面を書き出した。19 項目 → Assets/Data/DriveScript.asset」。

- [ ] **Step 3: アセットのテストを書く**

`unity/Assets/Tests/EditMode/DriveScriptAssetTests.cs`:

```csharp
using NUnit.Framework;
using UnityEditor;

namespace HalfAware.Tests
{
    public class DriveScriptAssetTests
    {
        const string Path = "Assets/Data/DriveScript.asset";

        static RoomScript Load()
        {
            var asset = AssetDatabase.LoadAssetAtPath<RoomScript>(Path);
            Assert.IsNotNull(asset, "文面のアセットが無い。HalfAware/Write the drive script を走らせる");
            return asset;
        }

        [Test]
        public void EveryTriggerHasItsOwnLines()
        {
            var script = Load();
            foreach (var id in DriveIds.Triggers)
            {
                var entry = script.Find(id);
                Assert.AreEqual(id, entry.id, id + " が文面に無い");
                Assert.Greater(entry.Lines.Count, 0, id + " に文が無い");
                Assert.IsNotEmpty(entry.Label, id + " に印の文が無い");
            }
        }

        [Test]
        public void EveryBandHasItsPage()
        {
            var script = Load();
            for (var i = 0; i < DriveIds.Triggers.Count; i++)
            {
                var page = script.Find(DriveIds.Page(i));
                Assert.AreEqual(DriveIds.Page(i), page.id, i + " 帯の段が文面に無い");
                Assert.Greater(page.Lines.Count, 0, i + " 帯の段に文が無い");
            }
        }

        [Test]
        public void TheGarageDoorIsThere()
        {
            var entry = Load().Find(DriveIds.Door);
            Assert.AreEqual(DriveIds.Door, entry.id);
            Assert.IsNotEmpty(entry.Label);
        }

        [Test]
        public void NoIdIsUsedTwice()
        {
            var ids = Load().Ids();
            CollectionAssert.AllItemsAreUnique(ids);
        }
    }
}
```

- [ ] **Step 4: テストが通ることを確かめる**

Expected: `failed: 0`。293 件。

- [ ] **Step 5: commit**

```bash
git add unity/Assets/Editor/WriteDriveScript.cs unity/Assets/Editor/WriteDriveScript.cs.meta unity/Assets/Data/DriveScript.asset unity/Assets/Data/DriveScript.asset.meta unity/Assets/Tests/EditMode/DriveScriptAssetTests.cs unity/Assets/Tests/EditMode/DriveScriptAssetTests.cs.meta
git commit -m "feat: write down what I think about on the way north"
```

---

## Task 7: 帯の進行を繋ぐ

**Files:**
- Create: `unity/Assets/Scripts/Flow/DriveDirector.cs`

参考にする既存ファイル: `unity/Assets/Scripts/Flow/AlleyDirector.cs`

- [ ] **Step 1: 実装する**

`unity/Assets/Scripts/Flow/DriveDirector.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 場面 8 の演出。ガレージで乗り込ませ、以降は帯を順に送る。
    ///
    /// 帯が終わる条件は、その帯のきっかけの対象を調べて独白を送り切ったことだけ。
    /// 経過時間でも走行距離でも進まないので、調べなければいつまでも走っていられる。
    /// 段取りそのものは BandClock が持っていて、ここはそれを
    /// SceneFlow・HudView・DriveWorld に繋ぐだけにしてある。
    ///
    /// 黒へは切り替えで入り、明けるときだけフェードする。
    /// 場面 1 のドアを閉める暗転と同じ扱い
    /// </summary>
    [DefaultExecutionOrder(-5)]
    public sealed class DriveDirector : MonoBehaviour
    {
        [SerializeField] SceneFlow flow;
        [SerializeField] HudView hud;
        [SerializeField] PlayerController player;
        [Tooltip("帯の段を引く文面のアセット")]
        [SerializeField] RoomScript script;
        [SerializeField] DriveWorld world;

        [Header("ガレージ")]
        [Tooltip("乗り込んだら伏せる。ガレージの建物ごと")]
        [SerializeField] GameObject garage;
        [Tooltip("運転席。乗り込んだらここへ立たせる")]
        [SerializeField] Transform seat;

        [Header("帯")]
        [Tooltip("景色の帯。DriveIds.Triggers と同じ並びにする")]
        [SerializeField] DriveBand[] bands = new DriveBand[0];
        [Tooltip("きっかけの対象。帯と同じ並び。その帯に入るまで伏せておく")]
        [SerializeField] GameObject[] triggers = new GameObject[0];

        DriveRoute route;
        readonly BandClock clock = new BandClock();
        int band = -1;
        bool aboard;

        /// <summary>
        /// 今の帯の値。DriveRoute は並びの問い合わせにだけ使い、秒数はここから直に読む。
        /// 秒数はオーナーが再生しながら Inspector で決めるので、
        /// Awake で写し取ると触っても効かなくなる
        /// </summary>
        DriveBand Now
        {
            get { return band >= 0 && band < bands.Length ? bands[band] : new DriveBand(); }
        }

        void Awake()
        {
            if (flow == null || hud == null || player == null || world == null)
            {
                Debug.LogError("DriveDirector: flow か hud か player か world が未接続", this);
                enabled = false;
                return;
            }
            route = new DriveRoute(bands);
            world.Rolling = false;
            world.Dress(-1);
            ShowTrigger(-1);
            flow.Examined += Examined;
        }

        void OnDestroy()
        {
            if (flow != null) flow.Examined -= Examined;
        }

        void Update()
        {
            if (!aboard) return;

            // 独白を送り切ったか。積んだ字幕が尽きたところで余韻へ移る
            if (clock.Beat == DriveBeat.Talking && !flow.Talking) clock.Spoken();

            clock.Tick(Time.deltaTime, Now);
            hud.SetFade(clock.Dark(Now));

            // 黒へ入った一度だけ、次の帯を並べる
            if (clock.TakeSwap()) Enter(band + 1);

            // 黒と明けのあいだは操作を止める。字幕は積んでいないので送りは関わらない
            if (clock.Beat == DriveBeat.Black || clock.Beat == DriveBeat.FadingIn) flow.Freeze(0.25f);

            // 最後の帯の独白まで済んだら、場面を閉じてよい
            flow.Held = !(route.IsLast(band) && clock.Beat == DriveBeat.Afterglow);
        }

        void Examined(IInteractable item)
        {
            if (item == null) return;
            if (item.Id == DriveIds.Door) { Board(); return; }
            if (!aboard || band < 0) return;
            // その帯のきっかけでなければ、対象そのものの文だけで終わる。
            // band を先に弾いておくのは、BandOf の「見つからない」も -1 で返るため。
            // 両方 -1 のまま比べると、どの対象を調べても通ってしまう
            if (route.BandOf(item.Id) != band) return;
            clock.Trigger();
            if (script == null) { Debug.LogWarning("DriveDirector: 文面が未接続", this); return; }
            var page = script.Find(DriveIds.Page(band));
            if (page.id == null) { Debug.LogWarning("DriveDirector: 段が文面に無い: " + DriveIds.Page(band), this); return; }
            flow.Say(page.Lines);
        }

        /// <summary>乗り込む。ガレージを伏せ、運転席に据えて走り出す</summary>
        void Board()
        {
            if (aboard) return;
            aboard = true;
            if (garage != null) garage.SetActive(false);
            if (seat != null)
            {
                player.transform.position = seat.position;
                player.Yaw = seat.eulerAngles.y;
                player.Pitch = 0f;
            }
            player.CanMove = false;
            world.Rolling = true;
            Enter(0);
        }

        /// <summary>which 番目の帯に入る。黒のあいだに呼ぶ</summary>
        void Enter(int which)
        {
            if (which < 0 || which >= route.Count)
            {
                // 最後の帯の先は無い。走らせたまま場面が閉じるのを待つ
                world.Rolling = false;
                return;
            }
            band = which;
            clock.Reset();
            world.Speed = Now.speed;
            world.Rough = Now.rough;
            world.Dress(band);
            world.Rewind();
            ShowTrigger(band);
        }

        /// <summary>which 番目の帯のきっかけだけ出す。-1 でどれも出さない</summary>
        void ShowTrigger(int which)
        {
            for (var i = 0; i < triggers.Length; i++)
                if (triggers[i] != null) triggers[i].SetActive(i == which);
        }
    }
}
```

- [ ] **Step 2: コンパイルを通す**

`mcpforunity://editor/state` を確かめてからコンパイル。`read_console`（types: error）。
Expected: 0 件。`PlayerController` に `CanMove` / `Yaw` / `Pitch` が無ければ `AlleyDirector.cs` の `Seat()` の使い方に合わせて直す（既存が正）。

- [ ] **Step 3: テストを走らせて崩れていないことを確かめる**

Expected: `failed: 0`。293 件のまま。

- [ ] **Step 4: commit**

```bash
git add unity/Assets/Scripts/Flow/DriveDirector.cs unity/Assets/Scripts/Flow/DriveDirector.cs.meta
git commit -m "feat: drive north one thought at a time"
```

---

## Task 8: シーンを組み立てる — 道と車内

ここから見た目。**まず動く形を作り、オーナーが実画面を見てから詰める。**

**Files:**
- Create: `unity/Assets/Editor/BuildDrive.cs`
- Create（生成物）: `unity/Assets/Scenes/Drive.unity`

参考にする既存ファイル: `unity/Assets/Editor/BuildAlley.cs`（組み立ての骨格）、`unity/Assets/Editor/AlleyMesh.cs`（`Bank` の `Box` / `FaceY` / `Quad`）

- [ ] **Step 1: シーンと骨格を作る**

`BuildDrive.cs` に `[MenuItem("HalfAware/Build the drive", false, 230)]` を置き、次の順に組む。

```
Prune → Scene → Car → Road → Roadsides → Garage → Items → Wire → CheckDrive.Run
```

寸法は次のとおり。単位は m。

| もの | 値 |
|---|---|
| タイル 1 枚の長さ | 20 |
| 前方に敷く距離 | 140 |
| 後ろに残す距離 | -30 |
| タイルの枚数 | `RoadRing.Needed(140f, 20f, -30f)` |
| 道幅（舗装） | 7.0（帯 0〜3） |
| 道幅（未舗装） | 4.6（帯 4） |
| 路肩 | 片側 1.2 |

道のタイルは `Bank` の `FaceY` で 1 枚ずつ作る。マテリアルは帯ごとに差し替えず、タイルは 1 種のみにして、色味は Volume（Color Adjustments）で寄せる。

- [ ] **Step 2: 車内を組む**

運転席の視点は `seat` の位置 `(0, 1.18, 0)`、向き `yaw 0`。原点は車の中心。

| 部位 | 中心 | 大きさ | マテリアル |
|---|---|---|---|
| ダッシュボード | (0, 0.92, 0.72) | (1.72, 0.26, 0.42) | `CarTrim` |
| メーター | (-0.38, 1.02, 0.60) | (0.34, 0.14, 0.03) | `CarGlass` |
| ハンドル（輪） | (-0.38, 1.02, 0.44) | 外径 0.36・太さ 0.035、x 軸まわりに 68 度倒す | `CarTrim` |
| フロントガラス | (0, 1.32, 0.86) | (1.66, 0.62, 0.02)、上を 22 度後ろへ倒す | `CarGlass`（透過） |
| ドアの内張り（左） | (-0.86, 0.86, 0.10) | (0.08, 0.72, 1.30) | `CarTrim` |
| ドアの内張り（右） | (0.86, 0.86, 0.10) | (0.08, 0.72, 1.30) | `CarTrim` |
| 助手席 | (0.42, 0.62, -0.06) | (0.52, 0.10, 0.52) | `CarSeat` |
| 助手席の背 | (0.42, 0.94, 0.22) | (0.52, 0.54, 0.10) | `CarSeat` |
| 天井 | (0, 1.52, 0.10) | (1.72, 0.06, 1.60) | `CarTrim` |
| ルームミラー | (0, 1.44, 0.74) | (0.28, 0.08, 0.02) | `CarGlass` |

マテリアルの色（URP/Lit、`Smoothness` は括弧内）:

- `CarTrim` `(0.085, 0.082, 0.090)`（0.18）
- `CarSeat` `(0.115, 0.098, 0.090)`（0.10）
- `CarGlass` `(0.55, 0.60, 0.66)`（0.85）、`Surface Type` を Transparent、`_BaseColor` の a を 0.12

- [ ] **Step 3: 沿道を帯ごとに作る**

`Roadsides/Band0`〜`Band4` の 5 つの入れ物を作り、それぞれの下に沿道の物を並べる。**沿道もタイルと同じ環に乗せる**ので、各帯の物はタイルの子にする。

| 帯 | 沿道に並べるもの |
|---|---|
| 0 | 高架の脚（(±6.5, 0〜6.0)、40 m ごと）、ネオンの板（(±5.4, 3.2)、25 m ごと）、濡れた路面の照り返し |
| 1 | 街灯（(±4.6, 0〜7.2)、35 m ごと）、対向車のヘッドライトの点（x -2.4、逆向きに流す） |
| 2 | 木立（(±5.8, 0〜5.0)、12 m ごとにばらけさせる） |
| 3 | 石垣（(±4.4, 0〜0.9)、連続）、牧草地の面 |
| 4 | 小麦（(±3.4 より外、0〜1.1)、2.5 m ごと）、土の轍 |

形はすべて `Bank.Box` の組み合わせで済ませる。細部はオーナーが実画面を見てから詰めるので、ここでは輪郭だけ作る。

- [ ] **Step 4: 組み立ててコンパイルを通す**

`mcpforunity://editor/state` を確かめてから `execute_menu_item`（`HalfAware/Build the drive`）。
`read_console`（types: error, warning）。
Expected: エラー 0 件。ログに組み立ての報告が出る。

- [ ] **Step 5: commit**

```bash
git add unity/Assets/Editor/BuildDrive.cs unity/Assets/Editor/BuildDrive.cs.meta unity/Assets/Scenes/Drive.unity unity/Assets/Scenes/Drive.unity.meta unity/Assets/Materials/Drive
git commit -m "feat: build the car and the road it runs on"
```

---

## Task 9: ガレージと調べる対象

**Files:**
- Modify: `unity/Assets/Editor/BuildDrive.cs`

- [ ] **Step 1: ガレージを組む**

車は原点、ガレージはその周り。プレイヤーは隅に立ち、歩いて運転席のドアまで来る。

| もの | 値 |
|---|---|
| 床 | (0, 0, -2.0)、14 × 16 |
| 天井 | 高さ 2.9 |
| 柱 | 四隅と、長辺の中ほどに 1 本ずつ。0.35 角 |
| シャッター | (0, 0〜2.6, 6.0)、幅 4.2 |
| 立ち位置 | (-4.2, 0, -7.0)、yaw 28 度。ここから車まで約 8 m |
| 照明 | 天井に 2 本、`(±3.0, 2.8, -2.0)`、色 `(0.62, 0.66, 0.72)` |

ガレージは `Garage` という一つの入れ物にまとめ、`DriveDirector.garage` へ繋ぐ。乗り込んだ時点で丸ごと伏せる。

- [ ] **Step 2: 調べる対象を立てる**

`Items` の下に `Interactable` を置く。判定点は物の少し手前。

| id | 位置 | radius | required | 伏せるか |
|---|---|---|---|---|
| `garage.door` | (-0.92, 1.05, 0.10) | 1.6 | true | 出したまま |
| `drive.chips` | (0.42, 0.78, -0.02) | 1.4 | false | 帯 0 で出す |
| `drive.log` | (0.42, 0.80, 0.24) | 1.4 | false | 帯 1 で出す |
| `drive.mirror` | (0, 1.42, 0.72) | 1.4 | false | 帯 2 で出す |
| `drive.photo` | (-0.16, 1.00, 0.62) | 1.4 | false | 帯 3 で出す |
| `drive.window` | (-0.84, 1.00, 0.10) | 1.4 | **true** | 帯 4 で出す |
| `drive.radio` | (-0.02, 0.96, 0.70) | 1.4 | false | 出したまま |
| `drive.pocket` | (0.10, 0.70, -0.30) | 1.4 | false | 出したまま |
| `drive.fuel` | (-0.46, 1.02, 0.58) | 1.4 | false | 出したまま |

すべて `once: true`、`script` は `Assets/Data/DriveScript.asset`。

`garage.door` と `drive.window` を required にしておくと、`SceneFlow` の必須の判定が乗り込みと窓の両方を見る。`drive.window` は帯 4 に入るまで伏せてあるので、それまで場面は閉じない。

- [ ] **Step 3: 帯の値を入れる**

`DriveDirector.bands` に 5 つ。**すべて仮置き。オーナーが実画面を見てから決める。**

| 帯 | name | trigger | speed | rough | afterglow | black | fadeIn |
|---|---|---|---|---|---|---|---|
| 0 | 倫敦の外れ | `drive.chips` | 16 | 1.0 | 5.0 | 0.8 | 1.4 |
| 1 | 夜の高速 | `drive.log` | 28 | 1.0 | 5.0 | 0.8 | 1.4 |
| 2 | 深夜の幹線 | `drive.mirror` | 24 | 1.0 | 5.0 | **3.5** | **2.6** |
| 3 | 明け方の丘陵 | `drive.photo` | 20 | 1.6 | 5.0 | 0.8 | 1.6 |
| 4 | 朝靄の未舗装路 | `drive.window` | 11 | 4.5 | 5.0 | 0.8 | 1.4 |

帯 2 の `black` と `fadeIn` が長いのが仮眠にあたる。

- [ ] **Step 4: 組み立てて確かめる**

`execute_menu_item`（`HalfAware/Build the drive`）。
`read_console`（types: error, warning）。
Expected: エラー 0 件。

- [ ] **Step 5: commit**

```bash
git add unity/Assets/Editor/BuildDrive.cs unity/Assets/Scenes/Drive.unity
git commit -m "feat: park the car in the garage and give me things to look at"
```

---

## Task 10: 視点・腕・揺れ

設計書 4 節のうち、まだ繋いでいないもの。**車内は座ったまま動かないので、ここが無いと画面が完全に固まって見える。**

**Files:**
- Modify: `unity/Assets/Editor/BuildDrive.cs`
- Modify: `unity/Assets/Scripts/Flow/DriveDirector.cs`

- [ ] **Step 1: 視線移動を繋ぐ**

`unity/Assets/Scripts/Player/HeadTurn.cs` と `EyeSway.cs` を読み、`Alley.unity` でプレイヤーにどう付いているかを `execute_code` で調べる。

```csharp
var p = UnityEngine.Object.FindFirstObjectByType<HalfAware.PlayerController>();
var report = "";
foreach (var c in p.GetComponentsInChildren<UnityEngine.MonoBehaviour>()) report += c.GetType().Name + " on " + c.name + "
";
return report;
```

同じ並びを `BuildDrive` のプレイヤーにも作る。車内では移動しないので `PlayerController.CanMove` は乗り込んだ時点で false になるが、**見回しは効いたままにする**（`AlleyDirector.Seat()` と同じで、`CanMove` は移動だけを止める）。乗り込んだあと見回せることを Task 12 の通しで確かめる。

- [ ] **Step 2: 車体の揺れを繋ぐ**

`unity/Assets/Scripts/Player/Sway.cs` を読み、揺れの強さを外から入れられるか確かめる。入れられなければ `DriveWorld` に次を足す。

```csharp
[Header("揺れ")]
[Tooltip("揺れの幅。m。舗装はごく小さく、未舗装は粗く")]
[SerializeField] float shake = 0.004f;
[Tooltip("揺れの速さ。走る速さに掛ける")]
[SerializeField] float shakeRate = 0.35f;
[Tooltip("揺らす対象。運転席のカメラ")]
[SerializeField] Transform shaken;

Vector3 rest;

/// <summary>路面の粗さ。1 が舗装、未舗装はもっと大きい。帯ごとに入れる</summary>
public float Rough { get; set; }
```

`Update` の末尾で、走った距離を位相にして上下と左右へ振る。時間ではなく距離で振るのは、速く走るほど細かく揺れてほしいため。

```csharp
if (shaken == null) return;
if (rest == Vector3.zero) rest = shaken.localPosition;
var phase = Travelled * shakeRate;
var amount = shake * Mathf.Max(0f, Rough);
shaken.localPosition = rest + new Vector3(
    Mathf.Sin(phase * 2.3f) * amount * 0.6f,
    Mathf.Sin(phase * 3.7f) * amount,
    0f);
```

`Rough` は帯ごとの値で、`DriveBand.rough` として Task 1 で定義済み。
`DriveDirector.Enter` での受け渡しも Task 7 で済んでいる（`world.Rough = Now.rough;`）ので、
ここでやるのは `DriveWorld` 側に `Rough` と揺れを足すところまで。

- [ ] **Step 3: 前腕を出す**

`unity/Assets/Scripts/Player/Forearm.cs` と `ForearmView.cs` を読む。場面 1 でどう置いているかを `BuildProps.cs` か `BuildAlley.cs` で調べ、同じ作りで運転席に置く。

- 帯 0〜3: 腕組み。原作「自動運転に任せて私は腕組みをしながら考える」。胸の前、`(0, 1.02, 0.28)` あたり
- 帯 4: ハンドルに手を乗せる。`(-0.38, 1.00, 0.46)` あたり、ハンドルの輪に沿わせる

どちらも描画だけで、操作には繋がらない。`DriveDirector` に足す。

```csharp
[Header("腕")]
[Tooltip("腕組みの腕。手動運転の帯だけ伏せる")]
[SerializeField] GameObject folded;
[Tooltip("ハンドルに乗せた手。手動運転の帯だけ出す")]
[SerializeField] GameObject onWheel;
[Tooltip("手動で運転する帯。0 から数える")]
[SerializeField] int drivenBand = 4;
```

`Enter` の末尾に足す。

```csharp
// 最後の帯は原作どおり手動運転。ただし描画だけで、入力は受け付けない
var driving = band == drivenBand;
if (folded != null) folded.SetActive(!driving);
if (onWheel != null) onWheel.SetActive(driving);
```

- [ ] **Step 4: 組み立ててコンパイルを通す**

`mcpforunity://editor/state` を確かめてからコンパイル → `execute_menu_item`（`HalfAware/Build the drive`）。
`read_console`（types: error）。
Expected: 0 件。

- [ ] **Step 5: テストを走らせる**

Expected: `failed: 0`。293 件のまま。テストは足していない。

- [ ] **Step 6: commit**

```bash
git add unity/Assets/Scripts/Fx/DriveWorld.cs unity/Assets/Scripts/Flow/DriveDirector.cs unity/Assets/Editor/BuildDrive.cs unity/Assets/Scenes/Drive.unity
git commit -m "feat: let me look around, feel the road, and fold my arms"
```

---

## Task 11: 見直し

組み立てのたびに機械で見る。**目で気づくまで放っておかない。**

**Files:**
- Create: `unity/Assets/Editor/CheckDrive.cs`
- Modify: `unity/Assets/Editor/BuildDrive.cs`（最後に `CheckDrive.Run` を呼ぶ）

参考にする既存ファイル: `unity/Assets/Editor/CheckAlley.cs`

- [ ] **Step 1: 実装する**

`[MenuItem("HalfAware/Check the drive", false, 235)]` と `public static void Run(Transform root)` を置く。見るのは次の 6 つで、それぞれ問題の数を返す。

1. **タイルの環** — `RoadRing.Slot` で全枚数の z を出し、並べ替えて隣との差が 1 枚の長さと一致するか。ずれていたら「タイルの環に隙間がある: 差 N m」
2. **沿道が道に出ていないか** — 各帯の沿道の物の mesh 頂点を車の向きへ直し、`|x| < 道幅の半分` に入る頂点があれば「沿道の物が道に出ている: 名前」
3. **ピンが埋まっていないか** — `Physics.OverlapSphere(it.Position + up * 0.17f, 0.12f)` と `ClosestPoint` で包含を見る。`CheckAlley.Pins` と同じ
4. **id の食い違い** — シーンの `Interactable` の id と `DriveScript` の id を突き合わせる。`DriveIds.IsPage` は対象を持たないので飛ばす。`CheckAlley.Ids` と同じ
5. **帯ときっかけの対応** — `DriveDirector.bands` の `trigger` が `DriveIds.Triggers` と同じ並びか、どの帯にもちょうど一つ割り当たっているか。欠けていたら「〈帯の名前〉にきっかけが無い」、重複していたら「きっかけが二つの帯で使われている: id」。警告に数字でなく `DriveBand.name` を出すのは、オーナーが 1 始まりの設計書を横に置いて読むため。`name` の説明にも「ログと見直しで使う」と書いてある
6. **きっかけの対象がシーンにあるか** — `bands[i].trigger` と同じ id の `Interactable` が `triggers[i]` の下にあるか

最後に `bad == 0` なら `Debug.Log("見直し: 気になるところは無し")`、そうでなければ `Debug.LogWarning("見直し: 気になるところ " + bad + " 件。上を参照")`。

`BuildDrive` の最後に `CheckDrive.Run(root)` を足す。

- [ ] **Step 2: 組み立てて見直しを走らせる**

`execute_menu_item`（`HalfAware/Build the drive`）。
Expected: ログの末尾に「見直し: 気になるところは無し」。警告が出たら**それは本物の欠陥**なので、`BuildDrive` の側を直す。見直しの側を緩めて黙らせない。

- [ ] **Step 3: commit**

```bash
git add unity/Assets/Editor/CheckDrive.cs unity/Assets/Editor/CheckDrive.cs.meta unity/Assets/Editor/BuildDrive.cs unity/Assets/Scenes/Drive.unity
git commit -m "feat: let the machine look over the road before I do"
```

---

## Task 12: 通しで動かす

**Files:**
- Modify: `unity/Assets/Scenes/Drive.unity`
- Modify: `unity/Assets/Editor/PlayScene.cs`（場面の一覧に Drive を足す）

- [ ] **Step 1: 場面の一覧に足す**

`PlayScene.cs` を読み、`Room` と `Alley` を並べている箇所に `Drive` を同じ書き方で足す。Build Settings のシーン一覧にも `Assets/Scenes/Drive.unity` を足す（`manage_build` の `action: "scenes"`）。

- [ ] **Step 2: 再生して通す**

`manage_editor`（`action: "play"`）。**再生中は `refresh_unity` も `execute_menu_item` もシーンの書き換えも行わない。**

`execute_code` で反射を使い、次を順に確かめる。

```csharp
var d = UnityEngine.Object.FindFirstObjectByType<HalfAware.DriveDirector>();
var w = UnityEngine.Object.FindFirstObjectByType<HalfAware.DriveWorld>();
var f = typeof(HalfAware.DriveDirector).GetField("band",
    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
return "帯 " + f.GetValue(d) + " / 走行 " + w.Travelled.ToString("F1") + " / 流れている " + w.Rolling;
```

確かめること:

1. 乗り込む前は `Rolling` が false で `Travelled` が 0
2. 乗り込んだあと `Rolling` が true になり、`Travelled` が増える
3. きっかけを調べずに 30 秒置いても帯が 0 のまま変わらない
4. きっかけを調べて独白を送り切ると、余韻のあいだ `Travelled` が増え続け、そのあと帯が 1 に変わる

`manage_editor`（`action: "stop"`）で止める。

- [ ] **Step 3: テストを全部走らせる**

`run_tests`（EditMode）→ `get_test_job`。
Expected: `failed: 0`。293 件。

- [ ] **Step 4: 組み立て直して保存する**

再生が止まっていることを `mcpforunity://editor/state` で確かめてから
`execute_menu_item`（`HalfAware/Build the drive`）→ `manage_scene`（`action: "save"`）。
Expected: 「見直し: 気になるところは無し」。

- [ ] **Step 5: commit**

```bash
git add unity/Assets/Editor/PlayScene.cs unity/Assets/Scenes/Drive.unity unity/ProjectSettings/EditorBuildSettings.asset
git commit -m "feat: put the drive in the running order"
```

---

## 済んだあと、オーナーに渡すもの

実装が終わったら次を報告する。**勝手に詰めない。**

1. `DriveDirector.bands` の 5 行の値（`afterglow` / `black` / `fadeIn`）はすべて仮置きであること
2. 各帯の独白の行数と本文（`WriteDriveScript.BandPages`）も仮置きで、原作からの抜きであること
3. 帯ごとのきっかけの割り当て（チップ / ログ / ミラー / 写真立て / 窓）も仮の割り当てであること
4. 沿道の物は輪郭だけで、密度も形も詰めていないこと

`docs/superpowers/specs/2026-09-20-drive-scene-design.md` 9 節に挙げた「後続に委ねる事項」はこの計画では手を付けない。
