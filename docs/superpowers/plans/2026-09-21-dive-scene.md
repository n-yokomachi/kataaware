# 場面 4（潜る）・場面 5（小休止） 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 場面 3 の「潜る」から他人の記憶へ入り、人の脇に浮く板から人へ人へと渡り歩き、板の `切断` で自室へ戻って「次の記憶で今日は最後にしよう」までを作る。

**Architecture:** 記憶の一覧は `DiveRoster` アセットに持ち、渡り歩きの決まり（最初の一人、次を誰にするか、`切断` の大きさ）は `DiveChain` として MonoBehaviour の外に出して EditMode テストで確かめる。場所は一つのシーン `Dive.unity` に全部を無効のまま置き、`DiveDirector` が一つずつ有効にする。主の体の動きは記憶ごとの鍵打ち（`HostPath`）で、プレイヤーは首だけ動かす。自室への戻りは別シーン `Rest.unity` で、`Connect.unity` から組み直す。

**Tech Stack:** Unity 6000.3.24f1 / URP 17.3 Forward+ / Input System / TextMeshPro / Unity Test Framework（EditMode）/ MCP for Unity

**設計書:** `docs/superpowers/specs/2026-09-21-dive-scene-design.md`

---

## この計画の前提

**必ず守ること。**

- コミットメッセージ・ドキュメントに「Co-Authored-By」「Claude」「Anthropic」などのモデル名・ツール名を著述者として書かない。例外なし
- カタカナで通じる技術用語を和語に訳さない。マテリアル / テクスチャ / メッシュ / レンダラー / アセット / エディタ / ポリゴン / ログ / ピン / パーティクル / アニメーション / コライダー / シーン / プレハブ。「材質」「描画」「資産」「編集器」「鋲」「目印」「控え」「記録」は使わない
- コードのコメントは日本語。既存ファイルの書きぶりに合わせる（なぜそうしたかを書き、何をしているかは書かない）
- **エディタを触る前に `EditorApplication.isPlaying` / `isCompiling` / `isUpdating` を直に見る。** 再生中は `refresh_unity` も `execute_menu_item` もシーンの書き換えも行わない
- `refresh_unity` はコンパイルが走らないことがある。そのときは `execute_code` で `UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.ForceSynchronousImport); UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();`
- Game ビューの大きさを変えない。確認用のカメラには `cam.enabled = false` と `HideFlags.HideAndDontSave` を付け、撮ったら `DestroyImmediate`
- **`Room.unity` と `Connect.unity` を組み立て以外で保存しない。** `BuildRest` は `Connect.unity` を開いてすぐ `Rest.unity` として保存し、以降はそちらだけを触る
- **`unity/Assets/Fonts/NotoSansJP-Regular SDF.asset` と `unity/Assets/Materials/Alley/Buyer.mat` を commit に含めない**
- 秒数・色・位置はオーナーが決める。この計画の値は仮置き
- この環境のアセンブリは `HalfAware`（`unity/Assets/Scripts/HalfAware.asmdef`）。`Assembly-CSharp` ではない
- Unity の `Quad` は法線が -z。表を向けるつもりで 180 度回すと裏になって消える（場面 3 で踏んだ）

**Unity の EditMode テストの流儀:**

未定義の型を参照するテストを書くと、テストアセンブリごとコンパイルエラーになる。これが「テストが落ちている」状態にあたる。`read_console` にコンパイルエラーが出ていることを確認してから実装に進む。実行は `run_tests`（`mode: "EditMode"`）→ `get_test_job`（`status` が `succeeded` になるまで）。いまは 341 件が通っている。

---

## 先に片付けること

場面 3 の文面の列が氏名に変わっている（`WriteConnectScript.cs` は直したが、`ConnectScript.asset` は古い）。Unity が繋がったら最初に `HalfAware/Write the connect script` を走らせ、`Assets/Data/ConnectScript.asset` を commit する。

---

## ファイル構成

| ファイル | 受け持ち |
|---|---|
| `unity/Assets/Scripts/Flow/DiveIds.cs` | 場所の id と、列の並び |
| `unity/Assets/Scripts/Data/DiveEntry.cs` | 記憶一つ分の値と、一覧 `DiveRoster`（ScriptableObject） |
| `unity/Assets/Scripts/Flow/DiveChain.cs` | 渡り歩きの決まり（純粋）。最初・次・`切断` の大きさ |
| `unity/Assets/Scripts/Player/HostPath.cs` | 主の体の鍵打ちと補間（純粋） |
| `unity/Assets/Scripts/Fx/Take.cs` | 記憶一つ分のシーン側。場所・人・鍵打ち・声 |
| `unity/Assets/Scripts/Fx/HoloPanel.cs` | 人の脇の板 |
| `unity/Assets/Scripts/Fx/HostBody.cs` | 体の差を目線・ぼやけ・色味・音に当てる |
| `unity/Assets/Scripts/Fx/WindowStream.cs` | 電車の窓の外を流れる灯り。ST のずらしだけ |
| `unity/Assets/Scripts/Fx/Mover.cs` | 記憶の中で人や鳩を一直線に動かす |
| `unity/Assets/Scripts/Flow/DiveHandoff.cs` | 場面をまたいで渡す値（渡った人数） |
| `unity/Assets/Scripts/Flow/DiveDirector.cs` | 記憶の切り替え、板、眩暈、切断 |
| `unity/Assets/Scripts/Flow/RestDirector.cs` | 場面 5。眩暈が薄れ、煙草、一言、モニターで庭へ |
| `unity/Assets/Editor/WriteDiveRoster.cs` | 設計書 6 節を `DiveRoster.asset` へ書き出す |
| `unity/Assets/Editor/WriteRestScript.cs` | 場面 5 の文面 `RestScript.asset` |
| `unity/Assets/Editor/BuildDive.cs` | `Dive.unity` を組む。rig・画面・場所・人・鍵打ち |
| `unity/Assets/Editor/BuildDivePlaces.cs` | 五つの場所の形 |
| `unity/Assets/Editor/BuildDiveTakes.cs` | 十六の記憶の人・鍵打ち・向き |
| `unity/Assets/Editor/BuildRest.cs` | `Connect.unity` から `Rest.unity` を組む |
| `unity/Assets/Data/DiveRoster.asset` / `RestScript.asset` | 書き出しで作る |
| `unity/Assets/Scenes/Dive.unity` / `Rest.unity` | 組み立てで作る |

**手を入れるもの:** `SceneMenu.cs`（一覧に四つ加える）、`PlayScene.cs`（二つ加える）、`BuildConnect.cs`（`nextScene` を `Dive` に）、`TerminalScreen.cs`（灯った状態で始める印）。

**手を入れないもの:** `SceneFlow` / `PlayerController` / `HudView` / `DazeVolume` / `Room.unity`。

---

## Task 1: 場所の id と列

**Files:**
- Create: `unity/Assets/Scripts/Flow/DiveIds.cs`
- Test: `unity/Assets/Tests/EditMode/DiveIdsTests.cs`

- [ ] **Step 1: 落ちるテストを書く**

```csharp
using NUnit.Framework;

namespace HalfAware.Tests
{
    public sealed class DiveIdsTests
    {
        [Test]
        public void ThereAreFivePlaces()
        {
            CollectionAssert.AreEqual(
                new[] { "estate", "park", "train", "kitchen", "classroom" },
                DiveIds.Places);
        }

        [Test]
        public void TheListIsTheSixRowsOnTheMonitorInOrder()
        {
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 4, 5, 7 }, DiveIds.Listed);
        }
    }
}
```

- [ ] **Step 2: 落ちることを確かめる**
- [ ] **Step 3: 実装**

```csharp
namespace HalfAware
{
    /// <summary>
    /// 場面 4 の場所と、場面 3 のモニターの列。
    /// 列の並びは `WriteConnectScript` の monitor の三ページ目と同じ順で、
    /// 端末が次を選ぶときはこの順に辿る
    /// </summary>
    public static class DiveIds
    {
        public const string Estate = "estate";
        public const string Park = "park";
        public const string Train = "train";
        public const string Kitchen = "kitchen";
        public const string Classroom = "classroom";

        public static readonly string[] Places = { Estate, Park, Train, Kitchen, Classroom };

        /// <summary>列に載っている記憶の、一覧での番号（0 始まり）。メイ・ハンナ・アルベルト・エミリー・マーク・リー</summary>
        public static readonly int[] Listed = { 0, 1, 2, 4, 5, 7 };
    }
}
```

- [ ] **Step 4: テストが通ることを確かめる**
- [ ] **Step 5: commit** `feat: name the five places she will stand in`

---

## Task 2: 記憶一つ分の値と一覧

**Files:**
- Create: `unity/Assets/Scripts/Data/DiveEntry.cs`
- Test: `unity/Assets/Tests/EditMode/DiveEntryTests.cs`

```csharp
using System;
using UnityEngine;

namespace HalfAware
{
    /// <summary>ぼやけ方。老眼は近くが、近視は遠くがぼやける</summary>
    public enum Blur { Sharp, Near, Far }

    /// <summary>記憶の中で見える人ひとり。板が出る相手と、板から飛ぶ先</summary>
    [Serializable]
    public struct Seen
    {
        [Tooltip("Take の下の GameObject の名前")]
        public string name;
        [Tooltip("飛び先。一覧での番号（0 始まり）")]
        public int target;
    }

    /// <summary>記憶一つ分の値。場所と人の形はシーン（Take）が持ち、ここは数と文字だけ</summary>
    [Serializable]
    public struct DiveEntry
    {
        [Tooltip("右上と板に出す行。場面 3 の列と同じ書式")]
        public string row;
        [Tooltip("場所の id。DiveIds.Places のどれか")]
        public string place;
        [Tooltip("秒")]
        public float length;
        [Tooltip("目の高さ。m")]
        public float eyeHeight;
        public Blur blur;
        [Tooltip("ぼやけの強さ。0〜1")]
        public float blurAmount;
        [Tooltip("体の動きの速さ。基準 1")]
        public float speed;
        [Tooltip("色味。Volume の Color Filter に入れる")]
        public Color tint;
        [Tooltip("耳の詰まり。0 で素、1 で低域だけ")]
        public float muffle;
        public bool heartbeat;
        public Seen[] seen;
    }

    /// <summary>場面 4 の記憶の一覧。設計書 6 節。書き出し（WriteDiveRoster）で作る</summary>
    [CreateAssetMenu(fileName = "DiveRoster", menuName = "HalfAware/Dive Roster")]
    public sealed class DiveRoster : ScriptableObject
    {
        [SerializeField] DiveEntry[] entries = new DiveEntry[0];

        public int Count { get { return entries.Length; } }

        public DiveEntry this[int i] { get { return entries[i]; } }

        /// <summary>一覧の中で閉じているか。飛び先がすべて一覧の中を指しているか</summary>
        public static bool Closed(DiveEntry[] all)
        {
            for (var i = 0; i < all.Length; i++)
            {
                var seen = all[i].seen;
                if (seen == null) continue;
                for (var k = 0; k < seen.Length; k++)
                    if (seen[k].target < 0 || seen[k].target >= all.Length) return false;
            }
            return true;
        }
    }
}
```

テストは `Closed` の真偽（飛び先が範囲内／範囲外）の 2 件。

- [ ] **Step 1〜5**: 上のとおり TDD で。commit `feat: hold one memory's worth of numbers`

---

## Task 3: 渡り歩きの決まり

**Files:**
- Create: `unity/Assets/Scripts/Flow/DiveChain.cs`
- Test: `unity/Assets/Tests/EditMode/DiveChainTests.cs`

`DiveChain` は一覧の人数と列の並びを受け取り、いま誰か・次は誰か・`切断` がどれだけ大きいかを返す。無作為は外から `System.Random` を渡す（テストで固定できるように）。

- [ ] **Step 1: 落ちるテストを書く**

```csharp
using System;
using NUnit.Framework;

namespace HalfAware.Tests
{
    public sealed class DiveChainTests
    {
        static DiveChain Fresh() { return new DiveChain(16, new[] { 0, 1, 2, 4, 5, 7 }, 8, new Random(1)); }

        [Test]
        public void SheStartsWithTheTopOfTheList()
        {
            var chain = Fresh();
            Assert.AreEqual(0, chain.Current);
            Assert.AreEqual(0, chain.Hops);
        }

        [Test]
        public void HoppingGoesWhereSheChose()
        {
            var chain = Fresh();
            chain.Hop(9);
            Assert.AreEqual(9, chain.Current);
            Assert.AreEqual(1, chain.Hops);
        }

        [Test]
        public void WhenSheDoesNotChooseTheTerminalWalksTheList()
        {
            var chain = Fresh();
            chain.Next();
            Assert.AreEqual(1, chain.Current);
            chain.Next();
            Assert.AreEqual(2, chain.Current);
        }

        [Test]
        public void TheTerminalSkipsWhereSheHasAlreadyBeen()
        {
            var chain = Fresh();
            chain.Hop(1);
            chain.Next();
            Assert.AreEqual(2, chain.Current);
        }

        [Test]
        public void OnceTheListIsSpentTheTerminalDrawsFromTheRest()
        {
            var chain = Fresh();
            for (var i = 0; i < 5; i++) chain.Next();
            chain.Next();
            Assert.IsFalse(Array.IndexOf(new[] { 0, 1, 2, 4, 5, 7 }, chain.Current) >= 0);
        }

        [Test]
        public void SheCanGoBackToSomeoneByChoice()
        {
            var chain = Fresh();
            chain.Hop(1);
            chain.Hop(0);
            Assert.AreEqual(0, chain.Current);
        }

        [Test]
        public void CutIsSmallAndDeadUntilTheEighthHop()
        {
            var chain = Fresh();
            Assert.IsFalse(chain.CanCut);
            Assert.AreEqual(0.4f, chain.CutSize, 1e-4f);
            for (var i = 0; i < 7; i++) chain.Hop(i + 1);
            Assert.IsFalse(chain.CanCut);
            Assert.Less(chain.CutSize, 1f);
            chain.Hop(9);
            Assert.IsTrue(chain.CanCut);
            Assert.AreEqual(1f, chain.CutSize, 1e-4f);
        }

        [Test]
        public void CutOnlyGrows()
        {
            var chain = Fresh();
            var last = chain.CutSize;
            for (var i = 0; i < 12; i++)
            {
                chain.Next();
                Assert.GreaterOrEqual(chain.CutSize, last);
                last = chain.CutSize;
            }
        }
    }
}
```

- [ ] **Step 2: 落ちることを確かめる**
- [ ] **Step 3: 実装**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 渡り歩きの決まり。いま誰の記憶か、次は誰か、`切断` がどれだけ大きいか。
    ///
    /// 最初の一人は列の一番上。プレイヤーが板で選べばそこへ、選ばずに尽きれば
    /// 端末が列の順に、列が尽きれば一覧から無作為に選ぶ。端末は一度潜った人を飛ばすが、
    /// プレイヤーは板で戻ってよい。
    ///
    /// `切断` は初め四割の大きさで押せず、人を渡るごとに大きくなって、
    /// threshold 人で `潜る` と同じ大きさになり、そこから押せる
    /// </summary>
    public sealed class DiveChain
    {
        /// <summary>`切断` の初めの大きさ。`潜る` を 1 として</summary>
        public const float CutStart = 0.4f;

        readonly int count;
        readonly int[] listed;
        readonly int threshold;
        readonly Random random;
        readonly HashSet<int> visited = new HashSet<int>();

        public int Current { get; private set; }
        public int Hops { get; private set; }

        public DiveChain(int count, int[] listed, int threshold, Random random)
        {
            this.count = count;
            this.listed = listed;
            this.threshold = Mathf.Max(1, threshold);
            this.random = random;
            Current = listed.Length > 0 ? listed[0] : 0;
            visited.Add(Current);
        }

        public bool CanCut { get { return Hops >= threshold; } }

        public float CutSize { get { return Mathf.Lerp(CutStart, 1f, Mathf.Clamp01((float)Hops / threshold)); } }

        /// <summary>板で選んだ先へ</summary>
        public void Hop(int target)
        {
            Current = Mathf.Clamp(target, 0, count - 1);
            visited.Add(Current);
            Hops++;
        }

        /// <summary>尽きたので端末が選ぶ</summary>
        public void Next()
        {
            for (var i = 0; i < listed.Length; i++)
                if (!visited.Contains(listed[i])) { Hop(listed[i]); return; }
            var rest = new List<int>();
            for (var i = 0; i < count; i++) if (!visited.Contains(i)) rest.Add(i);
            if (rest.Count == 0) { Hop(random.Next(count)); return; }
            Hop(rest[random.Next(rest.Count)]);
        }
    }
}
```

- [ ] **Step 4: テストが通ることを確かめる**
- [ ] **Step 5: commit** `feat: decide who is next, and when she may stop`

---

## Task 4: 主の体の鍵打ち

**Files:**
- Create: `unity/Assets/Scripts/Player/HostPath.cs`
- Test: `unity/Assets/Tests/EditMode/HostPathTests.cs`

主の体は鍵打ち（時刻・位置・向き・目の高さ）の並びで持ち、間は滑らかに補間する。プレイヤーの首の向きは `PlayerController.HeadYaw` としてこの上に乗る。

```csharp
using System;
using UnityEngine;

namespace HalfAware
{
    /// <summary>鍵打ち一つ。at 秒にここにいて、こちらを向いて、この高さで見ている</summary>
    [Serializable]
    public struct HostKey
    {
        public float at;
        public Vector3 position;
        public float yaw;
        public float pitch;
        public float eyeHeight;
    }

    /// <summary>
    /// 主の体の道筋。鍵打ちの間を SmoothStep で繋ぐ。
    /// 記憶の速さ（DiveEntry.speed）は時刻の側に掛けるので、ここは倍率を知らない
    /// </summary>
    public static class HostPath
    {
        public static HostKey At(HostKey[] keys, float t)
        {
            if (keys == null || keys.Length == 0) return new HostKey();
            if (t <= keys[0].at) return keys[0];
            for (var i = 1; i < keys.Length; i++)
            {
                if (t > keys[i].at) continue;
                var a = keys[i - 1];
                var b = keys[i];
                var span = b.at - a.at;
                var k = span <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, (t - a.at) / span);
                return new HostKey
                {
                    at = t,
                    position = Vector3.Lerp(a.position, b.position, k),
                    yaw = Mathf.LerpAngle(a.yaw, b.yaw, k),
                    pitch = Mathf.Lerp(a.pitch, b.pitch, k),
                    eyeHeight = Mathf.Lerp(a.eyeHeight, b.eyeHeight, k),
                };
            }
            return keys[keys.Length - 1];
        }

        public static float Length(HostKey[] keys)
        {
            return keys == null || keys.Length == 0 ? 0f : keys[keys.Length - 1].at;
        }
    }
}
```

テスト: 端で端の鍵打ちを返す／真ん中で真ん中／向きは短い方へ回る（350 → 10 が 0 を通る）／`Length` が最後の at。

- [ ] **Step 1〜5**: TDD で。commit `feat: keyframe the body she is borrowing`

---

## Task 5: 一覧の書き出し

**Files:**
- Create: `unity/Assets/Editor/WriteDiveRoster.cs`
- Create: `unity/Assets/Data/DiveRoster.asset`（書き出しで）
- Test: `unity/Assets/Tests/EditMode/DiveRosterAssetTests.cs`

`WriteDriveScript` と同じ作り。`HalfAware/Write the dive roster`（`false, 249`）。設計書 6 節の 16 人を、行・場所・長さ・体・見える人の順に書く。**行の文字は設計書と一字も違えない。** 見える人の `name` は Task 8 の `Take` の子の名前と一致させる（下の表）。

| # | row | place | length | eye | blur | speed | tint（仮） | seen |
|---|---|---|---|---|---|---|---|---|
| 0 | 女　6　『メイ』　2156/03/02 07:14 | estate | 60 | 1.00 | Sharp 0 | 1.5 | (1.00, 0.98, 0.92) | Mother → 1 |
| 1 | 女　34　『ハンナ』　2156/03/02 09:02 | estate | 30 | 1.55 | Sharp 0 | 1.0 | (0.94, 0.92, 0.88) | Daughter → 0, Neighbour → 8 |
| 2 | 男　78　『アルベルト』　2156/03/02 15:47 | park | 60 | 1.75 | Near 0.7 | 0.6 | (0.98, 0.92, 0.78) | Granddaughter → 3, Wife → 9 |
| 3 | 女　7　『ソフィア』　2156/03/02 15:47 | park | 30 | 1.05 | Sharp 0 | 1.4 | (1.00, 0.96, 0.84) | Grandfather → 2, Grandmother → 9 |
| 4 | 女　19　『エミリー』　2156/03/02 18:20 | train | 30 | 1.58 | Sharp 0 | 1.1 | (0.92, 0.96, 1.00) | Junior → 11, Passenger → 5 |
| 5 | 男　52　『マーク』　2156/03/03 06:55 | kitchen | 35 | 1.72 | Far 0.6 | 0.9 | (0.88, 0.94, 1.00) | Wife → 6, Son → 12 |
| 6 | 女　49　『リンダ』　2156/03/03 06:56 | kitchen | 30 | 1.58 | Sharp 0 | 1.0 | (0.90, 0.95, 1.00) | Husband → 5, Son → 12 |
| 7 | 男　41　『リー』　2156/03/03 11:38 | classroom | 35 | 1.70 | Sharp 0 | 1.0 | (1.00, 1.00, 0.96) | Pupil → 13, Sleeper → 14 |
| 8 | 男　66　『ジョルジョ』　2156/03/02 09:03 | estate | 25 | 1.65 | Near 0.6 | 0.7 | (0.92, 0.90, 0.86) | Wife → 15, Mother → 1 |
| 9 | 女　72　『ローザ』　2156/03/02 15:50 | park | 30 | 1.50 | Near 0.6 | 0.6 | (0.98, 0.94, 0.82) | Toddler → 10, Husband → 2, Granddaughter → 3 |
| 10 | 男　3　『ルーカス』　2156/03/02 15:50 | park | 25 | 0.90 | Sharp 0 | 1.6 | (1.00, 0.98, 0.86) | Grandmother → 9, Grandfather → 2 |
| 11 | 女　18　『プリヤ』　2156/03/02 18:20 | train | 25 | 1.55 | Sharp 0 | 1.1 | (0.92, 0.96, 1.00) | Senior → 4 |
| 12 | 男　15　『ダニエル』　2156/03/03 06:56 | kitchen | 25 | 1.65 | Sharp 0 | 1.2 | (0.90, 0.95, 1.00) | Mother → 6, Father → 5 |
| 13 | 女　16　『アイシャ』　2156/03/03 11:38 | classroom | 25 | 1.20 | Sharp 0 | 1.1 | (1.00, 1.00, 0.96) | Teacher → 7, Neighbour → 14 |
| 14 | 男　16　『マテオ』　2156/03/03 11:39 | classroom | 25 | 1.20 | Far 0.7 | 0.8 | (1.00, 1.00, 0.92) | Neighbour → 13, Teacher → 7 |
| 15 | 女　63　『エレナ』　2156/03/02 09:03 | estate | 25 | 1.15 | Near 0.6 | 0.7 | (0.92, 0.90, 0.86) | Husband → 8 |

`muffle` は老眼の人（2・8・9・15）を 0.4、他は 0。`heartbeat` は 0・3・10（子ども）を true。

- [ ] **Step 1: 落ちるテストを書く**（`Assets/Data/DiveRoster.asset` が在る／16 人／`DiveRoster.Closed` が真／`DiveIds.Listed` の 6 人の row が場面 3 の `ConnectScript.asset` の monitor 三ページ目の各行と一致する／同じ place の対が同じ place を指す）
- [ ] **Step 2: 落ちることを確かめる**
- [ ] **Step 3: `WriteDiveRoster` を書き、メニューを走らせてアセットを作る**
- [ ] **Step 4: テストが通ることを確かめる**
- [ ] **Step 5: commit** `feat: write down the sixteen she will borrow`

---

## Task 6: 板と体

**Files:**
- Create: `unity/Assets/Scripts/Fx/HoloPanel.cs`
- Create: `unity/Assets/Scripts/Fx/HostBody.cs`

### HoloPanel

世界に浮く板。`Quad` 一枚（緑・半透明・縁だけ線のマテリアル）と、その上に TextMeshPro（3D の `TextMeshPro`、`TMP_Text`）二行。

- `Show(Transform beside, string row, string targetRow)`: beside の肩の脇（`beside.position + beside.right * 0.35f + Vector3.up * 1.4f`、仮）に置き、毎フレーム主の目の方を向く（`transform.forward = (transform.position - eye.position).normalized` の反対。**Quad の法線は -z なので、向けるのは -forward**）。一行目に row、二行目に `E 潜る　　　切断`
- `Hide()`
- `Grow(float size)`: 二行目の `切断` の大きさ。`size` は 0.4〜1。1 未満は灰色、1 で緑
- `Select(int index)`: 0 で `潜る`、1 で `切断` に `▶` を付ける（`Choice.Cursor` と同じ印）。`Grow` が 1 未満のときは呼んでも `潜る` のまま
- 出るまでと消えるまでの半秒は `DiveDirector` が数える。板は言われたとおりに出るだけ

TextMeshPro の 3D 文字は `Assets/Fonts/NotoSansJP-Regular SDF.asset` を使う（場面の字幕と同じ）。**このアセットの差分を commit に含めないこと**（前からある無関係な変更）。

### HostBody

記憶ごとの体の差を当てる。

- `Apply(DiveEntry entry)`:
  - `player.EyeHeight = entry.eyeHeight`（鍵打ちの eyeHeight が毎フレーム上書きするので、ここは初期値）
  - ぼやけ: シーンの `Volume` の `DepthOfField`（Gaussian）。`Near` なら `gaussianStart = 0.2, gaussianEnd = 1.2`、`Far` なら `gaussianStart = 3, gaussianEnd = 8`、`Sharp` なら無効。強さは `gaussianMaxRadius = blurAmount * 1.5`
  - 色味: 同じ `Volume` の `ColorAdjustments.colorFilter = entry.tint`
  - 耳: `AudioListener` に付けた `AudioLowPassFilter.cutoffFrequency = Mathf.Lerp(22000, 900, entry.muffle)`
  - 心音: `heartbeat` なら `AudioSource`（ループ）を鳴らす。クリップが無ければ黙って何もしない
- `Clear()`: 全部を素に戻す

Volume は `BuildDive` が置く（`Global Volume` に `DepthOfField` と `ColorAdjustments` を override で持つ profile）。**場面 8 の空の Volume と同じ作り方**（`BuildDriveLand` を見る）。

- [ ] **Step 1: 実装**
- [ ] **Step 2: コンパイルが通ることを確かめる**
- [ ] **Step 3: commit** `feat: a panel beside the stranger, and a body to borrow`

---

## Task 7: 記憶一つ分のシーン側

**Files:**
- Create: `unity/Assets/Scripts/Fx/Take.cs`

`Take` は記憶一つ分の GameObject に付く。子に人（`Seen.name` と同じ名前）を持ち、鍵打ちと声を持つ。

```csharp
public sealed class Take : MonoBehaviour
{
    [SerializeField] int entry;                 // 一覧での番号
    [SerializeField] HostKey[] keys;            // 主の体
    [SerializeField] AudioClip call;            // 呼ぶ声。無くても動く
    [SerializeField] Transform[] people;        // 板の出る相手。名前が Seen.name
    public int Entry { get { return entry; } }
    public HostKey[] Keys { get { return keys; } }
    public AudioClip Call { get { return call; } }
    public Transform Person(string name) { ... }
}
```

人は `BuildAlley.BakeOne` で焼いた立ち姿（`quaternius` の模型、pose 0〜2）。動く人は鍵打ちではなく、`Take` の子の `Mover`（開始位置・終了位置・秒）で一直線に動かす。**顔は見せない**ので、背中を向ける・帽子の影・逆光になる向きで置く。

- [ ] **Step 1: 実装**（`Take` と、簡単な `Mover`）
- [ ] **Step 2: コンパイルが通ることを確かめる**
- [ ] **Step 3: commit** `feat: one memory's worth of people and path`

---

## Task 8: 場所と記憶の組み立て

**Files:**
- Create: `unity/Assets/Editor/BuildDive.cs`
- Create: `unity/Assets/Editor/BuildDivePlaces.cs`
- Create: `unity/Assets/Editor/BuildDiveTakes.cs`
- Create: `unity/Assets/Scenes/Dive.unity`（組み立てで）

`HalfAware/Build the dive`（`false, 250`）。`BuildDrive` の作りをなぞる。

### BuildDive（骨）

1. 再生中なら戻る。`Dive.unity` が無ければ新しいシーンを作って保存
2. `Rig()`: `BuildDrive.Rig` と同じ Player + Main Camera（`AudioListener` と `AudioLowPassFilter`）+ `PlayerController`。**`CharacterController` は付けない**（体は鍵打ちで動かす。当たりに押し出されると鍵打ちからずれる）。`CanMove = false`
3. `Screen()`: `BuildDrive.Screen` と同じ HUD に、右上の `Caption`（`TMP_Text`、端末の緑 `(0.32, 0.80, 0.46)`、22pt、右寄せ、anchor (1,1)、余白 24px）を加える
4. `Volume()`: `Global Volume` に DoF と ColorAdjustments
5. `Places()`: 五つの場所を `Places/<id>` に無効で置く（`BuildDivePlaces`）
6. `Takes()`: 十六の記憶を `Takes/<番号>` に無効で置く（`BuildDiveTakes`）
7. `Panel()`: `HoloPanel` を一つ、無効で置く
8. `Wire()`: `DiveDirector` に player・hud・caption・daze・volume・body・panel・roster・places・takes を繋ぐ
9. `EditorBuildSettings.scenes` に `Dive.unity` と `Rest.unity` が無ければ登録する（`SceneManager.LoadScene` は登録が要る）
10. 保存。見直し: 一覧の place がすべて `Places` にあること、各 `Take` の `people` が `Seen.name` と一致すること、鍵打ちの最後の at が `length` と一致すること

### BuildDivePlaces（五つの場所）

`Bank`（`AlleyMesh.cs`）で箱を組む。**暗がりと色味で誤魔化せる最小の箱。** 天井と壁は暗いマテリアル、床だけ少し明るく。灯りは一つ。

| id | 中身（仮） |
|---|---|
| estate | 外階段一本（幅 1.2 m、三階ぶん、踊り場二つ）、踊り場にドア二つ（隣同士）、手すり、部屋の中（畳は無し、床と壁、テレビの光の四角） |
| park | 地面（10×10）、ベンチ二つ、池の縁（弧の低い壁）、遠くの木（板）、門の柱二本、鳩（小さな箱を 8 つ。飛び立つのは `Mover` で上へ） |
| train | 車両一両ぶん（長さ 8 m）、座席二列、吊り革（棒に掛かった輪）、窓は外を流れる灯り（`Screenwater` ではなく、窓に貼った長い帯のテクスチャを `TerminalScreen` と同じ ST のずらしで流す。専用の小さな `WindowStream` を書く） |
| kitchen | 場面 3 の部屋の台所と玄関を使い回す（`Connect.unity` の `Room/KitchenSink` / `KitchenCabinet` / `Door` / `Doormat` を複製して置く）。二階への階段は箱で作る |
| classroom | 机の列（4×3）、黒板、窓（白い板）、教壇 |

### BuildDiveTakes（十六の記憶）

記憶ごとに、場所・人・鍵打ちを C# の表で持つ。鍵打ちは設計書 6 節の出来事を秒に割ったもの。**ここは長くなる。** 一つだけ完全に書いておくので、残りは同じ形で書く。

```csharp
// 0. 女 6『メイ』 estate 60 秒
// 階段の下（0, 0, 0）→ 三階のドアの前（0, 8.4, -6）→ 降りる
static readonly HostKey[] Mei =
{
    K(0f,   0f, 0.0f, 0.0f,  180f,  40f, 0.55f),  // しゃがんで靴紐。下を見ている
    K(3f,   0f, 0.0f, 0.0f,    0f,  10f, 1.00f),  // 立って上を向く
    K(9f,   0f, 2.8f, -2.0f,   0f,   5f, 1.00f),  // 一つ目の踊り場
    K(15f,  0f, 5.6f, -4.0f,   0f,   5f, 1.00f),  // 二つ目
    K(21f,  0f, 8.4f, -6.0f,   0f,  15f, 1.00f),  // 三階、ドアの前。母の脚
    K(23f,  0f, 8.4f, -6.0f,   0f,  60f, 1.50f),  // 抱き上げられる。上を向く
    K(26f,  0f, 8.4f, -6.0f,  90f,  20f, 1.50f),  // 回る。空、壁
    K(29f,  0f, 8.4f, -6.0f,   0f, -10f, 1.00f),  // 降ろされる
    K(33f,  0f, 8.4f, -5.2f, 180f,   0f, 1.00f),  // 背中を押されて向きが変わる
    K(45f,  0f, 5.6f, -4.0f, 180f,   0f, 1.00f),  // 二つ目の踊り場
    K(47f,  0f, 5.6f, -4.0f,   0f, -20f, 1.00f),  // 振り返る。戸口の母は逆光
    K(60f,  0f, 0.0f,  1.0f, 180f,   0f, 1.00f),  // 下まで降りて走り出す
};
// 人: Mother（W_Casual、pose 0、三階のドアの前で背を向けて立つ。23〜29 秒は目の前に来て、33 秒からドアの中へ）
```

`K(at, x, y, z, yaw, pitch, eyeHeight)` は `HostKey` を作る短い関数。位置は場所のローカル。

人の置き方（全部に共通）:
- `BuildAlley.BakeOne(take, name, model, at, yaw, pose, Vector3.one, BuildAlley.BuyerMat())`
- 模型は `W_Casual` / `W_Formal` / `M_Casual` / `M_Worker` / `M_Suit` / `W_Suit` から。子どもは `Vector3.one * 0.6`、老人は `0.95`
- 動く人には `Mover` を付ける

- [ ] **Step 1: `BuildDive.cs` の骨を書き、空の場所で組めることを確かめる**
- [ ] **Step 2: `BuildDivePlaces.cs` に五つの場所を書き、組んで、確認用のカメラで各場所を一枚ずつ撮る**
- [ ] **Step 3: `BuildDiveTakes.cs` に十六の記憶を書く。まず 0・1・2・3 の四つ（団地と公園）を書いて組み、鍵打ちの位置が場所の中に収まっていることを測る**
- [ ] **Step 4: 残りの十二を書く**
- [ ] **Step 5: 組み立ての見直しに警告が無いこと**
- [ ] **Step 6: commit（場所と記憶で分けてよい）** `build the five places` / `lay the sixteen paths`

---

## Task 9: 段の進行

**Files:**
- Create: `unity/Assets/Scripts/Flow/DiveHandoff.cs`
- Create: `unity/Assets/Scripts/Flow/DiveDirector.cs`

### DiveHandoff

```csharp
/// <summary>場面をまたいで渡す値。Rest は渡った人数で眩暈の濃さを決める</summary>
public static class DiveHandoff
{
    public static int Hops;
    public static bool FromDive;
}
```

### DiveDirector

- `Start`: 黒から明ける（`hud.SetFade(1)` → `FadeTo(0, 0.6)`）。`chain = new DiveChain(roster.Count, DiveIds.Listed, cutAfter, new System.Random())`。`Play(chain.Current)`
- `Play(int i)`:
  - 前の `Take` と場所を無効に。`roster[i].place` の場所と `Takes/i` を有効に
  - `body.Apply(roster[i])`、`caption.text = roster[i].row`
  - 主の体: `take.Keys` を `roster[i].speed` 倍の速さで再生。向きは `player.transform.rotation = Quaternion.Euler(key.pitch, placeYaw + key.yaw, 0f)`、目の高さは `player.EyeHeight = key.eyeHeight`
  - **位置は、体を傾けたぶんを打ち消して置く。** 目はカメラとして体の子の `(0, EyeHeight, eyeLead)` にあり、`PlayerController` が毎フレームそこへ置き直す。体を x 回りに傾けると目もその弧を動くので、傾き 40 度・目の高さ 1.6・lead 0.22 では目が前へ 0.98 m、下へ 0.52 m ずれる。`BuildDiveTakes` の鍵打ちは**足元の位置**として書かれていて、目は真上にある前提なので、そのままだと壁を抜けたり相手を通り越したりする。次のように解く:

```csharp
var lead = player.Eye.localPosition.z;             // Start で一度だけ読む。EyeOffset が 0 のうちに
var foot = place.TransformPoint(key.position);
var spin = Quaternion.Euler(key.pitch, placeYaw + key.yaw, 0f);
var flat = Quaternion.Euler(0f, placeYaw + key.yaw, 0f);
var offset = new Vector3(0f, key.eyeHeight, lead);
// 傾けない体での目の座を守り、そこへ傾けた体を合わせる
player.transform.position = foot + flat * offset - spin * offset;
player.transform.rotation = spin;
player.EyeHeight = key.eyeHeight;
```

  - **首の向きはこの上に乗る**: 場面の頭で `player.HeadYawLimit = 90` を一度掛けておくと、`PlayerController` はマウスの向きを首（`HeadYaw`）と `Pitch` に入れ、体の `transform.rotation` には触らない。主の上下の傾きを体に入れるのは、`Pitch` が一つしか無くプレイヤーの分と主の分を分けられないため。`player.Yaw` の set は使わない（首が制限されていると首を回してしまう）
  - **`Mover` は自分の時計を持たない。** `take.GetComponentsInChildren<Mover>()` を毎フレーム `Play(t)` で進める。呼ばないと 43 個が始まりの位置に止まったままになる
  - `take.Call` があれば 0.3 秒後に鳴らす
  - 尽きたら（`t >= length`）`chain.Next()` → `Play`
- 板: 毎フレーム、`take.People` のうち目の中央に近い一人（目からの向きとの角 12 度以内、最も近い）を探す。半秒続いたら `panel.Show(person, roster[i].row, roster[target].row)`。外れて半秒で `Hide`。`panel.Grow(chain.CutSize)`
- 入力: 板が出ているとき、`player.ChoiceStep` で `Select`（`chain.CanCut` のときだけ）。`player.InteractPressed` で、選んでいるのが `潜る` なら `chain.Hop(target)` → `Play`、`切断` なら `Cut()`
- 眩暈: `Play` のたびに `daze.Hold(step, step)`、`step = Mathf.Clamp01((float)chain.Hops / cutAfter) * dazeMax`（`dazeMax` 仮 0.8）
- `Cut()`: `DiveHandoff.Hops = chain.Hops; DiveHandoff.FromDive = true;` 画面が裂ける（**仮**: `hud.FadeTo(1, 0.4)`。裂けるシェーダーは後回し）→ `SceneManager.LoadScene("Rest")`
- `OnDisable`: `body.Clear()`、`player.CanLook = true`

**`E 次へ` は無い。** 板が出ていないときの E は何もしない。

- [ ] **Step 1: 実装**
- [ ] **Step 2: 組み立てを走らせ直して繋ぐ**
- [ ] **Step 3: 再生して、メイ → ハンナ → ジョルジョ → エレナ と板で渡り、8 人目で `切断` が緑になり、押すと `Rest` へ行こうとする（まだ無いのでエラーになるが、ログにその名が出る）ことを確かめる。板を押さずに放っておくと列の順に進むことも確かめる**
- [ ] **Step 4: commit** `feat: walk her from head to head until she cuts`

---

## Task 10: 場面 5（小休止）

**Files:**
- Create: `unity/Assets/Scripts/Flow/RestDirector.cs`
- Create: `unity/Assets/Editor/WriteRestScript.cs` / `unity/Assets/Data/RestScript.asset`
- Create: `unity/Assets/Editor/BuildRest.cs` / `unity/Assets/Scenes/Rest.unity`
- Modify: `unity/Assets/Scripts/Fx/TerminalScreen.cs`（`startLit` を加える）

### RestScript

id は一つ。`dive`: 印「潜る」、文なし、二択なし。

### BuildRest

`HalfAware/Build the rest`（`false, 251`）。`BuildConnect.Rename` と同じ手で `Connect.unity` を開いてすぐ `Rest.unity` として保存し、以降はそちらだけを触る。

- `ConnectDirector` を外し、`RestDirector` を付ける
- `Interactables` の子を全部消し、`Interactable_dive` を `ScreenAt (1.50, 1.10, 2.42)` に置く（`RestScript.asset`、半径 2、必須）
- プレイヤーを座席（`BuildConnect.SeatAt`、目 `SeatEyeHeight`）に据え、`CanMove = false`、`HeadYawLimit = 90`。`Chair/Blocker` は無効
- `Jack` を `JackSocket` の子に戻す（挿さったまま）。`JackPlug` を外す
- `TerminalScreen.startLit = true`（灯ったまま）。`TerminalScreen` に `[SerializeField] bool startLit` を加え、`Awake` で真なら `booted = ScreenBoot.Total` にする
- `SceneFlow`: `nextScene = ""`（庭はまだ無い。「（仮）続く」で止まる）、`standAfter = ""`、`daze` は繋いだまま（眩暈をここで使う）
- 煙草: `Connect.unity` に `Cigarette`（`Player` か煙草の物体）と `SmokePuffs`（`Main Camera` の子）が残っているか先に確かめる。`BuildConnect.Strip` が落としていれば、`BuildProps` の `HalfAware/Build the cigarette smoke` と同じ手順で `BuildRest` の中で作る。それを `RestDirector` に繋ぐ

### RestDirector

- `Start`: `daze.Decay(DiveHandoff.FromDive ? 0.8f : 0.4f, 同じ, 6f)`。`flow.Freeze(SmokeBeats.Total(1) + 1f)`。`cigarette.Light(1)`。吸い終わったら `flow.Say(new[] { "次の記憶で今日は最後にしよう" })`。`Interactable_dive` は初め無効で、独白を出したら有効にする
- `flow.Examined` で `dive` → 何もしない（`SceneFlow` が必須を済ませて閉じる。`nextScene` が空なので「（仮）続く」）

- [ ] **Step 1: `WriteRestScript` と `RestScript.asset`**
- [ ] **Step 2: `TerminalScreen.startLit`**
- [ ] **Step 3: `RestDirector`**
- [ ] **Step 4: `BuildRest`。組んで、`Connect.unity` と `Room.unity` に差分が出ていないこと**
- [ ] **Step 5: 再生。眩暈が薄れ、煙草、一言、モニターを調べて「（仮）続く」**
- [ ] **Step 6: commit** `feat: back in the chair, one more and then bed`

---

## Task 11: 繋ぎ

**Files:**
- Modify: `unity/Assets/Editor/BuildConnect.cs`（`nextScene = "Dive"`）
- Modify: `unity/Assets/Scripts/Flow/SceneMenu.cs`
- Modify: `unity/Assets/Editor/PlayScene.cs`

- `BuildConnect.Flow` の `nextScene` を `"Dive"` に。組み立て直して `Connect.unity` を commit
- `SceneMenu.Titles` / `Scenes` に `自室・接続 / Connect`、`潜る / Dive`、`小休止 / Rest`、`車内 / Drive` を加える（テスト `SceneMenuTests` があれば合わせる）
- `PlayScene` の一覧に `潜る`（場面 4）と `小休止`（場面 5）を加える

- [ ] **Step 1〜3**: 直して、`Connect` から `Dive` へ、`Dive` から `Rest` へ、`Rest` から「（仮）続く」まで一度通す
- [ ] **Step 4: commit** `wire the room to the dive and the dive back to the chair`

---

## 最後に

- [ ] EditMode テストを全部走らせ、落ちが無いこと
- [ ] `Room.unity` / `Connect.unity` に組み立て以外の差分が出ていないこと
- [ ] `Fonts/NotoSansJP-Regular SDF.asset` と `Alley/Buyer.mat` を commit に含めていないこと
- [ ] オーナーに見てもらう。板の位置・秒数・色味・場所の形はすべてここから詰める

## 決めていないこと

- 呼ぶ声の音源（英語の合成音声をぼかす）。クリップが無い間は黙って進む
- 眩暈の漂い（`EyeSway`）は `Dive.unity` に置いていない。`DazeVolume` のぼやけと二重像だけが効く。漂いも要るなら `BuildDive.Rig` で `EyeSway` を加える
- 場所ごとの環境音、心音のクリップ
- 切断の「裂ける」見え方。いまはフェードで代える
- 板の置き場（肩の脇）と、出る・消える半秒
- `切断` が同じ大きさになる人数（8）
- 庭（場面 6）が無いので、`Rest` は「（仮）続く」で止まる
