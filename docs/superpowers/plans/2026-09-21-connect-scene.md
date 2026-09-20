# 場面 3（自室・接続） 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 場面 2 の暗転から自室へ戻り、ソファの売上メモに今日のぶんを書き足し、椅子に座ってジャックを挿し、モニターに並んだクラック対象のリストを送りながら独白を聞き、「潜る」で場面を閉じる。

**Architecture:** 部屋の地形と小物は場面 1 の `Room.unity` を唯一の出処とし、`BuildConnect` がそれを開いて `Connect.unity` として組み直す。段の順は `Interactable.after` と、演出が終わってから対象を有効にすることの二つだけで決め、状態機械は増やさない。`SceneFlow` には手を入れない（場面 1・2・8 が乗っているため）。座る・挿す・画面が灯るといった時間のかかる演出は `ConnectDirector` が持ち、間合いの計算だけ MonoBehaviour の外へ出して EditMode テストで確かめる。

**Tech Stack:** Unity 6000.3.24f1 / URP 17.3 Forward+ / Input System / TextMeshPro / Unity Test Framework（EditMode）/ MCP for Unity

**設計書:** `docs/superpowers/specs/2026-09-16-scenario-design.md` 7 節

---

## この計画の前提

**必ず守ること。**

- コミットメッセージ・ドキュメントに「Co-Authored-By」「Claude」「Anthropic」などのモデル名・ツール名を著述者として書かない。例外なし
- カタカナで通じる技術用語を和語に訳さない。マテリアル / テクスチャ / メッシュ / レンダラー / アセット / エディタ / ポリゴン / ログ / ピン / パーティクル / アニメーション / コライダー / シーン / プレハブ。「材質」「描画」「資産」「編集器」「鋲」「目印」「控え」「記録」は使わない
- コードのコメントは日本語。既存ファイルの書きぶりに合わせる（なぜそうしたかを書き、何をしているかは書かない）
- **エディタを触る前に `EditorApplication.isPlaying` / `isCompiling` / `isUpdating` を直に見る。** 再生中は `refresh_unity` も `execute_menu_item` もシーンの書き換えも行わない
- `refresh_unity` はコンパイルが走らないことがある。そのときは `execute_code` で `UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.ForceSynchronousImport); UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();`
- Game ビューの大きさを変えない。確認用のカメラには `cam.enabled = false` と `HideFlags.HideAndDontSave` を付け、撮ったら `DestroyImmediate`
- **`Room.unity` を書き換えない。** `BuildConnect` は開いてすぐ `Connect.unity` として保存し、以降はそちらだけを触る。`Room.unity` を保存してしまうと場面 1 が壊れる
- **`unity/Assets/Fonts/NotoSansJP-Regular SDF.asset` と `unity/Assets/Materials/Alley/Buyer.mat` を commit に含めない。** この二つは本件と無関係な未コミットの変更
- 秒数・文章量はオーナーが決める。この計画の値は仮置き

**Unity の EditMode テストの流儀:**

未定義の型を参照するテストを書くと、テストアセンブリごとコンパイルエラーになる。これが「テストが落ちている」状態にあたる。`read_console` にコンパイルエラーが出ていることを確認してから実装に進む。実行は `run_tests`（`mode: "EditMode"`）→ `get_test_job`（`status` が `succeeded` になるまで）。

---

## 段の並び

コードでは 0 から数える。設計書 7.1 の番号とは対応させない。

| # | id | 印 | 開く条件 | 調べた後 |
|---|---|---|---|---|
| 0 | `note` | メモを見る | 初めから | 何もしない |
| 1 | `chair` | 椅子に座る | after: `note` | 座る演出（`ConnectDirector.Sitting`）。終わったら `jack` を有効にする |
| 2 | `jack` | ケーブルを繋ぐ | **初めは無効**。座り終えたら有効 | 挿す演出（`JackPlug`）。挿さった瞬間にモニターが灯る。演出が終わったら `monitor` を有効にする |
| 3 | `monitor` | リストを見る | **初めは無効** | 文（走査条件と候補）だけ |
| 4 | `list` | リストを送る | after: `monitor` | 画面の文字が流れ、独白が続く |
| 5 | `dive` | 潜る | after: `list` | 二択「潜る」。「はい」で完了 |

6 つとも必須。`SceneProgress.IsComplete` がすべて揃ったところで `SceneFlow` が暗転し、`nextScene` が空なので「（仮）続く」で止まる。

**`jack` と `monitor` を `after` ではなく有効・無効で開くのはなぜか。** `after` は「その id が済んだか」しか見ない。座る演出と挿す演出は調べた**後**に数秒かかるので、`after` だけだと演出の途中で次の対象が拾えてしまう。`ConnectDirector` が演出の終わりで `SetActive(true)` すれば、開く時刻が演出の終わりと一致する。`SceneFlow.Awake` は `FindObjectsInactive.Include` で対象を集めるので、無効で始めても必須の数え上げは狂わない。

---

## ファイル構成

| ファイル | 受け持ち |
|---|---|
| `unity/Assets/Scripts/Flow/ConnectIds.cs` | 場面 3 の対象の id と並び |
| `unity/Assets/Scripts/Player/PlugTimeline.cs` | ジャックを挿す一連の間合い（純粋） |
| `unity/Assets/Scripts/Player/JackPlug.cs` | 肘掛けのジャックを取って手首へ挿す |
| `unity/Assets/Scripts/Fx/ScreenBoot.cs` | モニターが灯るまでの明るさ（純粋） |
| `unity/Assets/Scripts/Fx/TerminalScreen.cs` | モニターの画面。消灯 → 起動 → 文字送り |
| `unity/Assets/Scripts/Flow/ConnectDirector.cs` | 段の進行を `SceneFlow` / `JackPlug` / `TerminalScreen` に繋ぐ |
| `unity/Assets/Editor/WriteConnectScript.cs` | 文面を `ConnectScript.asset` へ書き出す |
| `unity/Assets/Editor/BuildConnect.cs` | `Room.unity` から `Connect.unity` を組み直す |
| `unity/Assets/Data/ConnectScript.asset` | 文面（書き出しで作る） |
| `unity/Assets/Textures/TerminalRows.png` | 文字に見える帯。`tools/make-terminal.py` が作る |
| `unity/Assets/Scenes/Connect.unity` | シーン（組み立てで作る） |

テストは `unity/Assets/Tests/EditMode/` に置く。

**手を入れないもの:** `SceneFlow` / `SceneProgress` / `InteractionPicker` / `Interactable` / `RoomScript` / `Room.unity` / `RoomScript.asset`。

**既に済んでいるもの:** `unity/Assets/Audio/JackPlug.wav`（0.22 秒、頂点 −6dB）と `unity/Assets/Audio/LICENSES.md` への追記。

---

## Task 1: 対象の id

**Files:**
- Create: `unity/Assets/Scripts/Flow/ConnectIds.cs`
- Test: `unity/Assets/Tests/EditMode/ConnectIdsTests.cs`

- [ ] **Step 1: 落ちるテストを書く**

`unity/Assets/Tests/EditMode/ConnectIdsTests.cs`:

```csharp
using NUnit.Framework;

namespace HalfAware.Tests
{
    public sealed class ConnectIdsTests
    {
        [Test]
        public void TheOrderHoldsEveryId()
        {
            CollectionAssert.AreEqual(
                new[] { "note", "chair", "jack", "monitor", "list", "dive" },
                ConnectIds.Order);
        }

        [Test]
        public void NoIdRepeats()
        {
            CollectionAssert.AllItemsAreUnique(ConnectIds.Order);
        }
    }
}
```

- [ ] **Step 2: 落ちることを確かめる**

`read_console` に `ConnectIds` が見つからないコンパイルエラーが出ていること。

- [ ] **Step 3: 実装**

`unity/Assets/Scripts/Flow/ConnectIds.cs`:

```csharp
namespace HalfAware
{
    /// <summary>
    /// 場面 3（自室・接続）の調べる対象。
    /// シーンと文面のアセットで同じ綴りを使うので、綴りはここ 1 箇所にまとめる
    /// </summary>
    public static class ConnectIds
    {
        /// <summary>ソファの売上メモ</summary>
        public const string Note = "note";
        /// <summary>椅子。調べると座る</summary>
        public const string Chair = "chair";
        /// <summary>肘掛けのジャック。調べると手首へ挿す</summary>
        public const string Jack = "jack";
        /// <summary>モニター。走査条件と候補が並ぶ</summary>
        public const string Monitor = "monitor";
        /// <summary>リストを送る。独白が続く</summary>
        public const string List = "list";
        /// <summary>潜る。二択で確かめて場面を閉じる</summary>
        public const string Dive = "dive";

        /// <summary>調べる順。組み立てはこの順に after を張る</summary>
        public static readonly string[] Order = { Note, Chair, Jack, Monitor, List, Dive };
    }
}
```

- [ ] **Step 4: テストが通ることを確かめる**

- [ ] **Step 5: commit**

```bash
git add unity/Assets/Scripts/Flow/ConnectIds.cs unity/Assets/Scripts/Flow/ConnectIds.cs.meta unity/Assets/Tests/EditMode/ConnectIdsTests.cs unity/Assets/Tests/EditMode/ConnectIdsTests.cs.meta
git commit -m "feat: name the six things she touches on the way back in"
```

---

## Task 2: ジャックを挿す間合い

**Files:**
- Create: `unity/Assets/Scripts/Player/PlugTimeline.cs`
- Test: `unity/Assets/Tests/EditMode/PlugTimelineTests.cs`

`PullTimeline`（抜く側）の裏返し。**抜く側には手を入れない。** 段の数も意味も違うので、同じ構造体に両方を持たせると片方を直したときにもう片方が動く。

段は次の通り。

| 段 | 秒 | 中身 |
|---|---|---|
| Look | 0.90 | 視線を肘掛けのジャックへ落とす |
| Reach | 0.70 | 左手を伸ばす |
| Grip | 0.35 | 掴む。ここからジャックは左手に付く |
| Carry | 0.85 | 右手首の前へ運ぶ |
| Push | 0.45 | 挿す。挿さった瞬間に音が鳴り、ジャックは手首へ移る |
| Settle | 0.60 | 挿さったところを見せたまま止める |
| Return | 0.75 | 手と視線を戻す |

合計 4.60 秒。

- [ ] **Step 1: 落ちるテストを書く**

`unity/Assets/Tests/EditMode/PlugTimelineTests.cs`:

```csharp
using NUnit.Framework;

namespace HalfAware.Tests
{
    public sealed class PlugTimelineTests
    {
        [Test]
        public void TheStagesRunInOrder()
        {
            Assert.Less(PlugTimeline.GripAt, PlugTimeline.CarryAt);
            Assert.Less(PlugTimeline.CarryAt, PlugTimeline.PushAt);
            Assert.Less(PlugTimeline.PushAt, PlugTimeline.InAt);
            Assert.Less(PlugTimeline.InAt, PlugTimeline.LetGoAt);
            Assert.Less(PlugTimeline.LetGoAt, PlugTimeline.Total);
        }

        [Test]
        public void EveryWeightStartsAndEndsAtNothing()
        {
            Assert.AreEqual(0f, PlugTimeline.Aim(0f), 1e-4f);
            Assert.AreEqual(0f, PlugTimeline.Reach(0f), 1e-4f);
            Assert.AreEqual(0f, PlugTimeline.Carry(0f), 1e-4f);
            Assert.AreEqual(0f, PlugTimeline.Push(0f), 1e-4f);
            Assert.AreEqual(0f, PlugTimeline.Aim(PlugTimeline.Total), 1e-4f);
            Assert.AreEqual(0f, PlugTimeline.Reach(PlugTimeline.Total), 1e-4f);
            Assert.AreEqual(0f, PlugTimeline.Carry(PlugTimeline.Total), 1e-4f);
            Assert.AreEqual(0f, PlugTimeline.Push(PlugTimeline.Total), 1e-4f);
        }

        [Test]
        public void EveryWeightIsFullWhileSheIsStillPushing()
        {
            var t = PlugTimeline.InAt + 0.1f;
            Assert.AreEqual(1f, PlugTimeline.Aim(t), 1e-3f);
            Assert.AreEqual(1f, PlugTimeline.Reach(t), 1e-3f);
            Assert.AreEqual(1f, PlugTimeline.Carry(t), 1e-3f);
            Assert.AreEqual(1f, PlugTimeline.Push(t), 1e-3f);
        }

        [Test]
        public void SheHoldsItFromTheGripUntilItIsIn()
        {
            Assert.IsFalse(PlugTimeline.Held(PlugTimeline.GripAt - 0.01f));
            Assert.IsTrue(PlugTimeline.Held(PlugTimeline.GripAt + 0.01f));
            Assert.IsTrue(PlugTimeline.Held(PlugTimeline.InAt - 0.01f));
            Assert.IsFalse(PlugTimeline.Held(PlugTimeline.InAt + 0.01f));
        }

        [Test]
        public void ItIsInOnlyAfterThePush()
        {
            Assert.IsFalse(PlugTimeline.In(PlugTimeline.InAt - 0.01f));
            Assert.IsTrue(PlugTimeline.In(PlugTimeline.InAt));
        }

        [Test]
        public void HerEyesLeaveItOnceSheLetsGo()
        {
            Assert.IsTrue(PlugTimeline.Follows(PlugTimeline.LetGoAt - 0.01f));
            Assert.IsFalse(PlugTimeline.Follows(PlugTimeline.LetGoAt));
        }

        [Test]
        public void ItIsDoneAtTheEnd()
        {
            Assert.IsFalse(PlugTimeline.Done(PlugTimeline.Total - 0.01f));
            Assert.IsTrue(PlugTimeline.Done(PlugTimeline.Total));
        }
    }
}
```

- [ ] **Step 2: 落ちることを確かめる**

- [ ] **Step 3: 実装**

`unity/Assets/Scripts/Player/PlugTimeline.cs`:

```csharp
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// ジャックを挿す一連の間。肘掛けのジャックへ視線を落とし、左手を伸ばして掴み、
    /// 右手首の前へ運んで挿し、挿さったところを見せてから手を戻す。
    /// 骨そのものは触らず、どの段でどれだけ曲げるかだけを持つ。
    ///
    /// <see cref="PullTimeline"/> の裏返しだが、段の数も意味も違うので別に持つ。
    /// 一つにまとめると、抜く側を詰めたときに挿す側が黙って動く
    /// </summary>
    public struct PlugTimeline
    {
        /// <summary>肘掛けのジャックへ視線を落とす</summary>
        public const float LookSeconds = 0.90f;
        /// <summary>左手を伸ばす</summary>
        public const float ReachSeconds = 0.70f;
        /// <summary>掴んで止まる</summary>
        public const float GripSeconds = 0.35f;
        /// <summary>右手首の前へ運ぶ</summary>
        public const float CarrySeconds = 0.85f;
        /// <summary>挿す</summary>
        public const float PushSeconds = 0.45f;
        /// <summary>挿さったところを見せたまま止める</summary>
        public const float SettleSeconds = 0.60f;
        /// <summary>手を戻す</summary>
        public const float ReturnSeconds = 0.75f;

        public static float GripAt { get { return LookSeconds + ReachSeconds; } }
        public static float CarryAt { get { return GripAt + GripSeconds; } }
        public static float PushAt { get { return CarryAt + CarrySeconds; } }
        /// <summary>挿さった瞬間。音が鳴り、ジャックは左手から手首へ移る</summary>
        public static float InAt { get { return PushAt + PushSeconds; } }
        public static float LetGoAt { get { return InAt + SettleSeconds; } }
        public static float Total { get { return LetGoAt + ReturnSeconds; } }

        /// <summary>
        /// 0 から 1 へ上がって、戻しの間に 0 へ下がる形。
        /// at から seconds かけて上がり、LetGoAt から ReturnSeconds かけて下がる
        /// </summary>
        static float Swell(float t, float at, float seconds)
        {
            if (t <= at) return 0f;
            if (t < at + seconds) return Mathf.SmoothStep(0f, 1f, (t - at) / seconds);
            if (t < LetGoAt) return 1f;
            if (t >= Total) return 0f;
            return Mathf.SmoothStep(1f, 0f, (t - LetGoAt) / ReturnSeconds);
        }

        /// <summary>視線をジャックへ寄せる強さ。0 で元の向き、1 でジャックの真正面</summary>
        public static float Aim(float t) { return Swell(t, 0f, LookSeconds); }

        /// <summary>左手を肘掛けへ伸ばす姿勢の強さ</summary>
        public static float Reach(float t) { return Swell(t, LookSeconds, ReachSeconds); }

        /// <summary>掴んだジャックを手首の前へ運ぶ姿勢の強さ</summary>
        public static float Carry(float t) { return Swell(t, CarryAt, CarrySeconds); }

        /// <summary>挿し込む姿勢の強さ</summary>
        public static float Push(float t) { return Swell(t, PushAt, PushSeconds); }

        /// <summary>左手がジャックを掴んでいる間。ここでジャックは左手について動く</summary>
        public static bool Held(float t) { return t >= GripAt && t < InAt; }

        /// <summary>挿さった後。音が鳴り、ジャックは手首につく</summary>
        public static bool In(float t) { return t >= InAt; }

        /// <summary>見せ終わって手を戻し始めた後</summary>
        public static bool LetGo(float t) { return t >= LetGoAt; }

        /// <summary>視線がまだジャックを追ってよいか。手を戻し始めたら追わない</summary>
        public static bool Follows(float t) { return !LetGo(t); }

        public static bool Done(float t) { return t >= Total; }
    }
}
```

- [ ] **Step 4: テストが通ることを確かめる**

- [ ] **Step 5: commit**

```bash
git add unity/Assets/Scripts/Player/PlugTimeline.cs unity/Assets/Scripts/Player/PlugTimeline.cs.meta unity/Assets/Tests/EditMode/PlugTimelineTests.cs unity/Assets/Tests/EditMode/PlugTimelineTests.cs.meta
git commit -m "feat: time the cable going back in"
```

---

## Task 3: ジャックを挿すしぐさ

**Files:**
- Create: `unity/Assets/Scripts/Player/JackPlug.cs`

**`JackPull` をそのまま写して裏返す。** 骨の曲げ（`SeatedPose.BoneTurn[]`）は Inspector で当てる値なので、組み立て（Task 8）で `JackPull` の値を段に読み替えて入れる。

`JackPull` との違いだけ挙げる。

- ジャックは始め `JackRest`（椅子の肘掛け）の子。`Grip()` で左手の `grip`（`JackHold`）へ移し、`Plug()` で右手首の `socket`（`Jack` が元いた `Wrist.R`）へ移す
- 音は `PlugTimeline.In` の頭で 1 度。クリップは `unity/Assets/Audio/JackPlug.wav`
- 視線は `PlugTimeline.Follows` の間だけジャックを追う。ジャックは掴めば左手について動くので、運んで挿すところまで画面に入る
- 挿し終わったら `Plugged` を立てる。`ConnectDirector` がこれを見てモニターを灯す

- [ ] **Step 1: 実装**

`unity/Assets/Scripts/Player/JackPlug.cs`:

```csharp
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 肘掛けに置いたジャックを左手で取り、右手首へ挿す。座位の姿勢の上に曲げを重ねて動かす。
    /// まず肘掛けへ視線を落として置いてあるところを見せ、掴んで手首の前へ運び、挿す。
    /// 視線はジャックを追うので、挿さる瞬間も画面から外れない。
    /// 挿している間は調べる操作も見回しも止める。
    ///
    /// <see cref="JackPull"/>（場面 1 で抜く側）の裏返し
    /// </summary>
    [DefaultExecutionOrder(25)]
    public sealed class JackPlug : MonoBehaviour
    {
        [SerializeField] SceneFlow flow;
        [Tooltip("座位の姿勢。ここへ曲げを重ねる")]
        [SerializeField] SeatedPose pose;
        [Tooltip("この id を調べたら挿し始める")]
        [SerializeField] string id = ConnectIds.Jack;
        [Tooltip("肘掛けに置いてあるジャック")]
        [SerializeField] Transform jack;
        [Tooltip("掴んでいる間ジャックを預ける置き所。左手の JackHold")]
        [SerializeField] Transform grip;
        [Tooltip("挿さったジャックの行き先。右の手首")]
        [SerializeField] Transform socket;
        [Tooltip("挿さる音。無くても動く")]
        [SerializeField] AudioSource source;
        [SerializeField] AudioClip plug;
        [Tooltip("肘掛けのジャックへ視線を落とす曲げ")]
        [SerializeField] SeatedPose.BoneTurn[] look = new SeatedPose.BoneTurn[0];
        [Tooltip("左手を肘掛けへ伸ばす曲げ")]
        [SerializeField] SeatedPose.BoneTurn[] reach = new SeatedPose.BoneTurn[0];
        [Tooltip("掴んだジャックを右手首の前へ運ぶ曲げ")]
        [SerializeField] SeatedPose.BoneTurn[] carry = new SeatedPose.BoneTurn[0];
        [Tooltip("挿し込む曲げ")]
        [SerializeField] SeatedPose.BoneTurn[] push = new SeatedPose.BoneTurn[0];

        SeatedPose.BoneTurn[] scratch;
        float elapsed = -1f;
        float fromYaw;
        float fromPitch;
        float aimYaw;
        float aimPitch;
        bool aimed;
        bool held;
        bool seated;
        bool sounded;
        bool tookLook;

        /// <summary>挿している最中か</summary>
        public bool Plugging { get { return elapsed >= 0f && !PlugTimeline.Done(elapsed); } }

        /// <summary>もう手首に挿さっているか。モニターを灯す合図に使う</summary>
        public bool Plugged { get { return elapsed >= 0f && PlugTimeline.In(elapsed); } }

        /// <summary>しぐさが終わったか</summary>
        public bool Done { get; private set; }

        void OnEnable()
        {
            if (flow != null) flow.Examined += OnExamined;
            scratch = new SeatedPose.BoneTurn[look.Length + reach.Length + carry.Length + push.Length];
        }

        void OnDisable()
        {
            if (flow != null) flow.Examined -= OnExamined;
            Release();
        }

        void OnExamined(IInteractable item)
        {
            if (item == null || item.Id != id || elapsed >= 0f) return;
            elapsed = 0f;
            var player = flow != null ? flow.Player : null;
            if (player != null)
            {
                fromYaw = player.Yaw;
                fromPitch = player.Pitch;
                // 挿し終わるまでは見回しも受け付けない。視線はこちらで運ぶ
                player.CanLook = false;
                tookLook = true;
            }
            if (flow != null) flow.Freeze(PlugTimeline.Total);
        }

        void Update()
        {
            if (elapsed < 0f || Done) return;
            elapsed += Time.deltaTime;
            Apply(elapsed);
            Aim(elapsed);
            if (PlugTimeline.Held(elapsed) && !held) Grip();
            if (PlugTimeline.In(elapsed) && !seated) Seat();
            if (PlugTimeline.In(elapsed) && !sounded) Sound();
            if (!PlugTimeline.Done(elapsed)) return;
            Done = true;
            if (pose != null) { pose.Extra = null; pose.ExtraWeight = 0f; }
            Release();
        }

        /// <summary>段ごとの曲げを重みつきで 1 本にまとめ、座位の上へ重ねる</summary>
        void Apply(float t)
        {
            if (pose == null) return;
            var n = 0;
            Blend(look, PlugTimeline.Aim(t), ref n);
            Blend(reach, PlugTimeline.Reach(t), ref n);
            Blend(carry, PlugTimeline.Carry(t), ref n);
            Blend(push, PlugTimeline.Push(t), ref n);
            pose.Extra = scratch;
            pose.ExtraWeight = 1f;
        }

        void Blend(SeatedPose.BoneTurn[] turns, float weight, ref int n)
        {
            for (var i = 0; i < turns.Length; i++)
            {
                var turn = turns[i];
                turn.degrees *= weight;
                scratch[n++] = turn;
            }
        }

        /// <summary>視線をジャックへ寄せる。掴めば左手について動くので、挿さる瞬間も画面に入る</summary>
        void Aim(float t)
        {
            var player = flow != null ? flow.Player : null;
            if (player == null || player.Eye == null) return;
            if (PlugTimeline.Follows(t) && jack != null)
            {
                var to = jack.position - player.Eye.position;
                if (to.sqrMagnitude > 1e-6f)
                {
                    to.Normalize();
                    aimYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                    aimPitch = -Mathf.Asin(Mathf.Clamp(to.y, -1f, 1f)) * Mathf.Rad2Deg;
                    aimed = true;
                }
            }
            if (!aimed) return;
            var k = PlugTimeline.Aim(t);
            player.Yaw = Mathf.LerpAngle(fromYaw, aimYaw, k);
            player.Pitch = Mathf.Lerp(fromPitch, aimPitch, k);
        }

        /// <summary>掴んだ。ここからジャックは左手について動く</summary>
        void Grip()
        {
            held = true;
            if (jack == null || grip == null) return;
            jack.SetParent(grip, false);
            jack.localPosition = Vector3.zero;
            jack.localRotation = Quaternion.identity;
            var s = grip.lossyScale.x;
            jack.localScale = Vector3.one / (Mathf.Approximately(s, 0f) ? 1f : s);
        }

        /// <summary>挿さった。ジャックは手首のものになる</summary>
        void Seat()
        {
            seated = true;
            if (jack == null || socket == null) return;
            jack.SetParent(socket, false);
            jack.localPosition = Vector3.zero;
            jack.localRotation = Quaternion.identity;
            jack.localScale = Vector3.one;
        }

        /// <summary>挿さる音。挿さった頭で鳴らす</summary>
        void Sound()
        {
            sounded = true;
            if (source == null || plug == null) return;
            source.PlayOneShot(plug);
        }

        /// <summary>見回しを返す。途中で切られても視線が固まったままにならないよう、ここへ集めてある</summary>
        void Release()
        {
            if (!tookLook) return;
            tookLook = false;
            var player = flow != null ? flow.Player : null;
            if (player != null) player.CanLook = true;
        }
    }
}
```

- [ ] **Step 2: コンパイルが通ることを確かめる**

`read_console` にエラーが無いこと。`Seat()` が `jack.localPosition = Vector3.zero` で手首の元の位置に戻ることは、組み立て（Task 8）で `socket` に `Wrist.R` を当ててから実機で見る。

- [ ] **Step 3: commit**

```bash
git add unity/Assets/Scripts/Player/JackPlug.cs unity/Assets/Scripts/Player/JackPlug.cs.meta
git commit -m "feat: take the cable off the armrest and push it into her wrist"
```

---

## Task 4: モニターが灯るまで

**Files:**
- Create: `unity/Assets/Scripts/Fx/ScreenBoot.cs`
- Test: `unity/Assets/Tests/EditMode/ScreenBootTests.cs`

消えている画面が、二度瞬いてから明るさを上げきるまでの形。純粋な計算だけ。

- [ ] **Step 1: 落ちるテストを書く**

`unity/Assets/Tests/EditMode/ScreenBootTests.cs`:

```csharp
using NUnit.Framework;

namespace HalfAware.Tests
{
    public sealed class ScreenBootTests
    {
        [Test]
        public void TheScreenIsDarkBeforeItStarts()
        {
            Assert.AreEqual(0f, ScreenBoot.Level(0f), 1e-4f);
            Assert.AreEqual(0f, ScreenBoot.Level(-1f), 1e-4f);
        }

        [Test]
        public void TheScreenIsFullAtTheEnd()
        {
            Assert.AreEqual(1f, ScreenBoot.Level(ScreenBoot.Total), 1e-4f);
            Assert.AreEqual(1f, ScreenBoot.Level(ScreenBoot.Total + 5f), 1e-4f);
        }

        [Test]
        public void TheLevelNeverLeavesNoughtToOne()
        {
            for (var t = 0f; t <= ScreenBoot.Total + 0.5f; t += 0.01f)
            {
                var v = ScreenBoot.Level(t);
                Assert.GreaterOrEqual(v, 0f);
                Assert.LessOrEqual(v, 1f);
            }
        }

        [Test]
        public void ItBlinksTwiceBeforeItSettles()
        {
            // 瞬きの間は暗いところへ何度も落ちる。落ちた回数で数える
            var dips = 0;
            var lit = false;
            for (var t = 0f; t < ScreenBoot.FlickerSeconds; t += 0.005f)
            {
                var on = ScreenBoot.Level(t) > 0.5f;
                if (on && !lit) dips++;
                lit = on;
            }
            Assert.AreEqual(2, dips);
        }

        [Test]
        public void ItRisesWithoutFallingOnceTheFlickerIsOver()
        {
            var last = ScreenBoot.Level(ScreenBoot.FlickerSeconds);
            for (var t = ScreenBoot.FlickerSeconds; t <= ScreenBoot.Total; t += 0.01f)
            {
                var now = ScreenBoot.Level(t);
                Assert.GreaterOrEqual(now, last - 1e-4f);
                last = now;
            }
        }
    }
}
```

- [ ] **Step 2: 落ちることを確かめる**

- [ ] **Step 3: 実装**

`unity/Assets/Scripts/Fx/ScreenBoot.cs`:

```csharp
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 消えている画面が灯るまでの明るさ。
    ///
    /// **一息に明るくしない。** 一息に上げると、画面が点いたのではなく
    /// マテリアルの色が変わっただけに見える。古い物理モニターらしく二度瞬かせてから上げる
    /// </summary>
    public struct ScreenBoot
    {
        /// <summary>瞬いている間</summary>
        public const float FlickerSeconds = 0.42f;
        /// <summary>瞬きの後、明るさを上げきるまで</summary>
        public const float RiseSeconds = 0.85f;

        public static float Total { get { return FlickerSeconds + RiseSeconds; } }

        /// <summary>瞬き 1 回ぶんの長さ</summary>
        const float Blink = FlickerSeconds * 0.5f;

        /// <summary>
        /// t 秒での明るさ。0 が消灯、1 が点きっぱなし。
        /// 瞬きの間は前半だけ点く矩形を 2 つ並べ、そのあとは滑らかに上げる
        /// </summary>
        public static float Level(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= Total) return 1f;
            if (t < FlickerSeconds)
            {
                // 前半で点き、後半で落ちる。これを 2 回
                var into = t - Blink * Mathf.Floor(t / Blink);
                return into < Blink * 0.45f ? 0.8f : 0f;
            }
            return Mathf.SmoothStep(0f, 1f, (t - FlickerSeconds) / RiseSeconds);
        }

        /// <summary>もう点いているか</summary>
        public static bool Lit(float t) { return t >= Total; }
    }
}
```

- [ ] **Step 4: テストが通ることを確かめる**

`ItBlinksTwiceBeforeItSettles` が 2 にならなければ `Blink` の割合を直す。**テストの期待値を実装に合わせない。** 二度瞬くことが決めごと。

- [ ] **Step 5: commit**

```bash
git add unity/Assets/Scripts/Fx/ScreenBoot.cs unity/Assets/Scripts/Fx/ScreenBoot.cs.meta unity/Assets/Tests/EditMode/ScreenBootTests.cs unity/Assets/Tests/EditMode/ScreenBootTests.cs.meta
git commit -m "feat: let the monitor blink twice before it comes up"
```

---

## Task 5: 画面に流す帯のテクスチャ

**Files:**
- Create: `tools/make-terminal.py`
- Create: `unity/Assets/Textures/TerminalRows.png`

**読めなくてよい。** 設計書 7.1 が「文字として読める必要はない」と断っている。427×240 の出力で、モニターの面は横 100 px ほどにしかならないので、字を並べても潰れる。行ごとに長さの違う明るい帯を置いて、文字の行に見せる。

仕様:

- 256×256、RGBA。`unity/Assets/Textures/` の他の PNG と同じ扱い（Point フィルタ、圧縮なし）
- 背景は透明（0,0,0,0）。帯だけ不透明の白。色はマテリアル側で付ける
- 1 行の高さ 6 px、行間 4 px。上から 25 行
- 行ごとに、左端から 2〜3 個の帯をランダムな長さ（12〜90 px）と隙間（4〜10 px）で置く。**行の 3 割は空にする**。全部埋めると段落に見えず、砂に見える
- 縦に繋いでも切れ目が出ないよう、上下の端は行間の真ん中で切る
- 種は固定（`random.Random(30301)`）。走らせ直すたびに絵が変わると、差分がノイズになる

- [ ] **Step 1: `tools/make-terminal.py` を書いて走らせる**

`tools/make-neon.py` と同じ書きぶりに合わせる（`Pillow` を使い、`unity/Assets/Textures/` へ直に書く）。

- [ ] **Step 2: 出来た PNG を Unity に取り込ませ、取り込みの設定を合わせる**

`execute_code` で `TextureImporter` に当てる。他の `Assets/Textures/*.png` と同じ設定（`filterMode = Point`、`mipmapEnabled = false`、`wrapMode = Repeat`、`textureCompression = Uncompressed`、`alphaIsTransparency = true`）。**`wrapMode` は Repeat。** 流すのに UV をずらすので、Clamp だと端で止まる。

- [ ] **Step 3: commit**

```bash
git add tools/make-terminal.py unity/Assets/Textures/TerminalRows.png unity/Assets/Textures/TerminalRows.png.meta
git commit -m "draw rows that read as text without being text"
```

---

## Task 6: モニターの画面

**Files:**
- Create: `unity/Assets/Scripts/Fx/TerminalScreen.cs`

`Monitors/Monitor2` の面のマテリアルを持ち、消灯 → 起動 → 文字送りを受け持つ。

- 対象は `Monitor2` の子の面のレンダラー。マテリアルはこの場面のために複製したものを当てる（`Room.unity` と共有すると場面 1 の画面まで灯る）
- `Boot()` で `ScreenBoot` の明るさを `_BaseColor` の明るさと `_EmissionColor` に当てる
- `Scroll(bool)` で UV の縦方向のずれを毎フレーム進める。速さは `scrollSpeed`（UV/秒、仮 0.22）
- 灯る前は帯のテクスチャを出さない（明るさ 0 なので黒く見える）

- [ ] **Step 1: 実装**

`unity/Assets/Scripts/Fx/TerminalScreen.cs`:

```csharp
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// モニターの画面。消灯から起動して、文字に見える帯を流す。
    ///
    /// **面のマテリアルはこの場面のために複製したものを当てる。**
    /// 場面 1 と共有すると、こちらで灯した画面が向こうでも灯る
    /// </summary>
    public sealed class TerminalScreen : MonoBehaviour
    {
        [Tooltip("画面の面。ここのマテリアルを触る")]
        [SerializeField] Renderer face;
        [Tooltip("消えているときの色")]
        [SerializeField] Color off = new Color(0.035f, 0.040f, 0.045f);
        [Tooltip("点いているときの色")]
        [SerializeField] Color glow = new Color(0.32f, 0.80f, 0.46f);
        [Tooltip("文字を流す速さ。UV/秒")]
        [SerializeField] float scrollSpeed = 0.22f;

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");

        MaterialPropertyBlock block;
        float booted = -1f;
        float offset;
        bool scrolling;

        /// <summary>もう灯っているか</summary>
        public bool Lit { get { return booted >= 0f && ScreenBoot.Lit(booted); } }

        void Awake()
        {
            block = new MaterialPropertyBlock();
            Paint(0f);
        }

        /// <summary>灯す。二度瞬いてから明るさが上がる</summary>
        public void Boot()
        {
            if (booted >= 0f) return;
            booted = 0f;
        }

        /// <summary>文字を流すか</summary>
        public void Scroll(bool on) { scrolling = on; }

        void Update()
        {
            if (booted >= 0f && !ScreenBoot.Lit(booted)) booted += Time.deltaTime;
            if (scrolling) offset += scrollSpeed * Time.deltaTime;
            Paint(booted < 0f ? 0f : ScreenBoot.Level(booted));
        }

        void Paint(float level)
        {
            if (face == null) return;
            face.GetPropertyBlock(block);
            var lit = Color.Lerp(off, glow, level);
            block.SetColor(BaseColor, lit);
            block.SetColor(Emission, lit * level);
            // 流すのは縦だけ。横へずらすと行が切れて読めない絵になる
            block.SetVector(BaseMap + 2, new Vector4(1f, 1f, 0f, -offset));
            face.SetPropertyBlock(block);
        }
    }
}
```

**`BaseMap + 2` は使わない。** `MaterialPropertyBlock` で UV のずれを渡すときは `_BaseMap_ST` の id を別に取る。実装では次のように書くこと:

```csharp
static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");
...
block.SetVector(BaseMapST, new Vector4(1f, 1f, 0f, -offset));
```

- [ ] **Step 2: コンパイルが通ることを確かめる**

- [ ] **Step 3: commit**

```bash
git add unity/Assets/Scripts/Fx/TerminalScreen.cs unity/Assets/Scripts/Fx/TerminalScreen.cs.meta
git commit -m "feat: give the monitor something to show"
```

---

## Task 7: 文面

**Files:**
- Create: `unity/Assets/Editor/WriteConnectScript.cs`
- Create: `unity/Assets/Data/ConnectScript.asset`
- Test: `unity/Assets/Tests/EditMode/ConnectScriptAssetTests.cs`

`WriteDriveScript` と同じ作り。`HalfAware/Write the connect script` で `ConnectScript.asset` を作り直す。

**文は設計書 7.2 の写し。** 勝手に足さない・削らない。

### `note`（ソファのメモ）

設計書に文が無いので、場面 1 の `clipboard` の文に今日のぶんを足した形で置く。**仮置き。オーナーが直す。**

- 印: 「メモを見る」
- 文:
  - 「売り上げのメモだ。今日のぶんがまだ空いている」
  - 「2166/08/13　5枚\n2166/08/14　4枚\n2166/08/15　―」
  - 「6枚。書き足しておく」
  - 「2166/08/13　5枚\n2166/08/14　4枚\n2166/08/15　6枚」

**枚数は `MarketSale` から出す。** 文字で「6枚」と書くと、露店の売れ行きを直したときにここだけ取り残される。

```csharp
var sold = MarketSale.Chips - MarketSale.Left(MarketSale.Count - 1);
```

### `chair`

- 印: 「椅子に座る」
- 文: 「椅子に座る」（1 行。座る演出がすぐ続くので短く）

### `jack`

- 印: 「ケーブルを繋ぐ」
- 文: 「手首のジャックにケーブルを挿す。冷たい感触が皮膚の下に侵入してくる」

### `monitor`

- 印: 「リストを見る」
- 文:
  - 「モニターにクラック対象の個人情報リストが並んでいる」
  - 「条件　名前を呼ばれた時刻\n対象　防壁なし　距離 ランダム　期間 2156年3月2日～2156年3月3日\n候補の簡易抽出　56,232,318」
  - 「女　6　『メイ』　2156/03/02 07:14\n女　34　『ママ』　2156/03/02 09:02\n男　78　『おじいちゃん』　2156/03/02 15:47\n女　19　『先輩』　2156/03/02 18:20\n男　52　『あなた』　2156/03/03 06:55\n男　41　『先生』　2156/03/03 11:38」

### `list`

- 印: 「リストを送る」
- 文（設計書 7.2 の「リストを送る」の箇条書きをそのまま。順も変えない）:
  - 「思い出の価値がどれほどのものか、私には分からない」
  - 「それが、他人の思い出を数えきれないほど盗み見てきて感覚が麻痺したからなのか、それとも私自身の思い出がなにもないからなのかはもまた、私には分からない」
  - 「私にはある日を境に記憶がない」
  - 「気づいたときにはこの｜倫敦《ロンドン》の路地に突っ立っていた。まるで突然大人の姿でこの世に生まれてきたみたいなのに、言葉もナーヴ・ターミナルも使い方は分かっているのだからおかしな感じがしたものだった」
  - 「その瞬間の驚きや嘆かわしさや言い表しようのない疼きを表そうにも、赤子のように泣きわめくこともできず、なんとか言葉にしようとしてそれができないのだからもどかしかった」
  - 「だからたとえ食っていくだけの金が稼げても、私は他人の思い出を除いて回るのをやめられない。毎日大半の時間はホロ・コンソールを眺めながら過ごした。毎日数十人の頭を覗き見た」
  - 「どこかに自分の痕跡を探して。誰かの記憶に自分の姿を探して」

**ルビは青空文庫の記法で書く**（`｜倫敦《ロンドン》`）。`SubtitleBox` が折り返してから `Ruby.Expand` で書式に直すので、文面に書式のタグを入れてはいけない。

**「しばらく間を開けて」は文ではなく間。** 5 つ目（「…もどかしかった」）を読み終えたところで `ConnectDirector` が送りを止める（Task 9）。文面に空行を入れて間にしない。空行は空の字幕ページとして出てしまう。

### `dive`

- 印: 「潜る」
- 文: なし（印を見て二択へ）
- 二択: 質問「潜る」、`afterYes` はなし

- [ ] **Step 1: 落ちるテストを書く**

`unity/Assets/Tests/EditMode/ConnectScriptAssetTests.cs`。`RoomScriptAssetTests` / `DriveScriptAssetTests` の書きぶりに合わせ、次を確かめる:

- `Assets/Data/ConnectScript.asset` が在る
- `ConnectIds.Order` の 6 つがすべて文面にあり、余計な id が無い
- `dive` だけが二択を持つ
- `list` の 4 つ目の行にルビの記法（`Ruby.Head`）が入っている
- `note` の最後の行が `MarketSale` から出した枚数と合う
- どの行も `ListFormat` の 1 ページに収まる（既存のテストに同じ確かめ方があるのでそれに倣う）

- [ ] **Step 2: 落ちることを確かめる**

- [ ] **Step 3: `WriteConnectScript` を書き、`HalfAware/Write the connect script` を走らせて `ConnectScript.asset` を作る**

`WriteDriveScript` を写す。アセットが無ければ `ScriptableObject.CreateInstance<RoomScript>()` で作って `AssetDatabase.CreateAsset`。

- [ ] **Step 4: テストが通ることを確かめる**

- [ ] **Step 5: commit**

```bash
git add unity/Assets/Editor/WriteConnectScript.cs unity/Assets/Editor/WriteConnectScript.cs.meta unity/Assets/Data/ConnectScript.asset unity/Assets/Data/ConnectScript.asset.meta unity/Assets/Tests/EditMode/ConnectScriptAssetTests.cs unity/Assets/Tests/EditMode/ConnectScriptAssetTests.cs.meta
git commit -m "feat: write what she reads on the way back in"
```

---

## Task 8: 場面の組み立て

**Files:**
- Create: `unity/Assets/Editor/BuildConnect.cs`
- Create: `unity/Assets/Scenes/Connect.unity`（組み立てで出来る）

`HalfAware/Build the connect scene`（`false, 240`）。

**手順は必ずこの順で。**

1. `EditorApplication.isPlaying` なら何もせず警告して戻る
2. `EditorSceneManager.OpenScene("Assets/Scenes/Room.unity", OpenSceneMode.Single)`
3. **すぐに** `EditorSceneManager.SaveScene(scene, "Assets/Scenes/Connect.unity")`。以降の書き換えは `Connect.unity` に載る
4. 詰め直す（下記）
5. `EditorSceneManager.SaveScene(scene)`
6. 見直しを走らせ、`Debug.Log` で一行まとめを出す（`BuildDrive` と同じ形）

### 詰め直し

**場面 1 のものを落とす:**

- `RoomIntroDirector` の付いた GameObject からそのコンポーネントを外す
- `Interactables` の子（`Interactable_*`）を全部消す。`Jack` の子の `Interactable_jack` も消す
- `MemoryHub` の `Chip0` / `Chip1` / `Chip2` / `Chip4` / `Chip8` / `Chip11` と対の `Label*` を消す。場面 2 で売り切れている

**`SceneFlow`:**

| 項目 | 値 | なぜ |
|---|---|---|
| `standAfter` | `""` | 空だと `Awake` が座位の仕度を丸ごと飛ばす。立って始まる |
| `nextScene` | `""` | 場面 4 はまだ無い。「（仮）続く」で止める |
| `openingCard` | `""` | 場面 2 の暗転から続くので見出しを挟まない |
| `dazeUntil` | `""` | 眩暈は場面 1 の目覚めのもの |
| `cutToBlack` | `false` | |
| `exitSound` | なし | 部屋を出ないので扉の音は鳴らない |

**プレイヤー:**

- `Player` の位置 `(0.80, 0.05, -2.45)`、向き `Yaw = 0`（戸口の内側から部屋の奥を向く）
- `EyeHeight = PlayerController.StandingEyeHeight`、`CanMove = true`
- `SeatedPose.Seated = false`
- `Chair/Blocker` を有効にする（歩き回るので椅子に当たる）

**ジャック:**

- `Jack` を `Chair/JackRest` の子にし、`localPosition`・`localRotation` を 0、`localScale` を 1 にする
- `Cable` の繋ぎ先（`CableEnd`）は `Jack` の子のままなので触らない。肘掛けの差込口から垂れた形になる

**調べる対象:**

`Interactables` の下に 6 つ作る。文面は `Assets/Data/ConnectScript.asset`。

| id | 位置 | 半径 | 必須 | after | 初めから有効 |
|---|---|---|---|---|---|
| `note` | `(-2.40, 0.67, 0.19)` | 2.0 | ○ | なし | ○ |
| `chair` | `(1.50, 0.75, 1.20)` | 2.0 | ○ | `note` | ○ |
| `jack` | `Jack` の子、原点 | 1.2 | ○ | なし | **×** |
| `monitor` | `(1.50, 1.10, 2.42)` | 2.0 | ○ | なし | **×** |
| `list` | `(1.50, 1.10, 2.42)` | 2.0 | ○ | `monitor` | ○ |
| `dive` | `(1.50, 1.10, 2.42)` | 2.0 | ○ | `list` | ○ |

`monitor` / `list` / `dive` は同じ点。順に開くので同時に選べることはない。

**モニター:**

- `Monitors/Monitor2` の面のレンダラーに当たっているマテリアルを複製し、`Assets/Materials/Connect/TerminalFace.mat` として保存して当て直す
- そのマテリアルの `_BaseMap` に `Assets/Textures/TerminalRows.png` を当てる
- `TerminalScreen` を `Monitors/Monitor2` に付け、`face` にその面を繋ぐ

**新しい繋ぎもの:**

- `SceneFlow` の GameObject に `ConnectDirector` を付け、`flow` / `hud` / `screen` / `plug` / 6 つの対象を繋ぐ
- `Player/Protagonist` に `JackPlug` を付け、`flow` / `pose` / `jack`（`Jack`）/ `grip`（`Wrist.L/JackHold`）/ `socket`（`Wrist.R`）/ `source`（`Main Camera/Voice` の `AudioSource`）/ `plug`（`Assets/Audio/JackPlug.wav`）を繋ぐ
- `JackPlug` の 4 つの曲げは、`Room.unity` の `JackPull` に入っている値を読み替えて入れる。`look` は `JackPull.look` の符号を変えたもの、`reach` はそのまま、`carry` は `JackPull.show` を裏返したもの、`push` は `JackPull.lift` を裏返したもの。**まず値をそのまま写して組み、絵を見てから詰める。** 秒数も曲げもオーナーが決める

### 見直し

`BuildDrive` の最後と同じく、組み立ての最後で次を測って `Debug.LogWarning` を出す。

- 6 つの id がシーンと文面の両方にあるか
- `note` / `chair` から届く位置に立てるか（対象の半径の中に、床の上の立てる点があるか）
- `jack` の判定点が `JackRest` の上にあるか
- `Monitor2` の面のマテリアルが `Room.unity` と共有になっていないか

- [ ] **Step 1: `BuildConnect.cs` を書く**
- [ ] **Step 2: `HalfAware/Build the connect scene` を走らせ、`read_console` に警告が無いことを確かめる**
- [ ] **Step 3: 運転席と同じ要領で、確認用のカメラを 3 枚撮る（戸口から／ソファの前／座ってモニターを見たところ）**
- [ ] **Step 4: commit**

```bash
git add unity/Assets/Editor/BuildConnect.cs unity/Assets/Editor/BuildConnect.cs.meta unity/Assets/Scenes/Connect.unity unity/Assets/Scenes/Connect.unity.meta unity/Assets/Materials/Connect
git commit -m "build the room again, the evening she comes back"
```

---

## Task 9: 段の進行

**Files:**
- Create: `unity/Assets/Scripts/Flow/ConnectDirector.cs`

`SceneFlow.Examined` を受けて、演出と対象の開閉を並べるだけ。状態は「いまどの段か」しか持たない。

**受け持ち:**

1. `note` を調べた → 何もしない（`chair` は `after` で開く）
2. `chair` を調べた → `Sitting()` を走らせる
   - `flow.Freeze` を段ごとに掛け直す（`RoomIntroDirector.Smoke` と同じ。一つの長い停止にすると先に切れて途中で調べられる）
   - `player.CanMove = false`、`player.CanLook = false`
   - 目を今の位置から座席の目（`(1.50, 0.05, 1.20)` + 目線 `seatEyeHeight` 1.1）へ、`sitSeconds`（仮 1.4 秒）かけて滑らせる。向きはモニターの方（`Yaw = 0`）へ、`Pitch` は 0 へ
   - `Chair/Blocker` を無効にする
   - `SeatedPose.Seated = true`
   - 座り終えたら `player.HeadYawLimit = HeadTurn.DefaultLimit`、`player.CanLook = true`
   - `jack` の対象を有効にする
3. `jack` を調べた → `JackPlug` が動く。`ConnectDirector` は `plug.Plugged` が立ったフレームで `screen.Boot()`、`plug.Done` が立ったフレームで `monitor` を有効にする
4. `monitor` を調べた → 何もしない（`list` は `after` で開く）
5. `list` を調べた → `screen.Scroll(true)`。`flow.CurrentLine` が `pauseAfterLine`（既定は「…もどかしかった」で終わる行）と一致したフレームで `flow.Freeze(pauseSeconds)`（仮 2.0 秒）。**一度だけ。** 毎フレーム掛け直すと送りが戻らない
6. `dive` を調べて「はい」→ `screen.Scroll(false)`。`SceneProgress` が完了するので `SceneFlow` が閉じる

**注意:**

- `OnDisable` で `player.CanLook = true` に戻し、`StopAllCoroutines`。途中で切られたときに視線が固まったままにならないように（`RoomIntroDirector` と同じ作り）
- `Sitting()` の `finally` でも見回しを返す
- 対象の開閉は `GameObject.SetActive`。`Interactable.Active` は `isActiveAndEnabled` を見ている

- [ ] **Step 1: 実装**
- [ ] **Step 2: コンパイルが通ることを確かめる**
- [ ] **Step 3: 組み立てを走らせ直して繋ぎ直す**
- [ ] **Step 4: 再生して頭から終わりまで一度通す。** `read_console` に例外が無いこと。6 つを順に調べて「（仮）続く」まで行けること
- [ ] **Step 5: commit**

```bash
git add unity/Assets/Scripts/Flow/ConnectDirector.cs unity/Assets/Scripts/Flow/ConnectDirector.cs.meta unity/Assets/Scenes/Connect.unity
git commit -m "feat: walk her from the note to the dive"
```

---

## Task 10: 場面の一覧に足す

**Files:**
- Modify: `unity/Assets/Editor/PlayScene.cs`

いまの一覧は自室と路地裏の 2 つだけで、場面 8 も入っていない。3 つ足す。

```csharp
new Entry { title = "自室", path = "Assets/Scenes/Room.unity", note = "場面 1。ロンドンの安宿。煙草と記憶の抜き取り" },
new Entry { title = "路地裏", path = "Assets/Scenes/Alley.unity", note = "場面 2。グレビル・ストリートとブリーディング・ハート・ヤード" },
new Entry { title = "自室・接続", path = "Assets/Scenes/Connect.unity", note = "場面 3。売上を書き足し、ジャックを繋いで潜る" },
new Entry { title = "車内", path = "Assets/Scenes/Drive.unity", note = "場面 8。ガレージから乗り込み、3 つの景色を抜ける" },
```

- [ ] **Step 1: 直す**
- [ ] **Step 2: `HalfAware/Scenes` を開いて 4 つ並ぶことを確かめる**
- [ ] **Step 3: commit**

```bash
git add unity/Assets/Editor/PlayScene.cs
git commit -m "list the drive and the reconnection with the other scenes"
```

---

## 最後に

- [ ] EditMode テストを全部走らせ、落ちが無いこと
- [ ] `HalfAware/Build the connect scene` を走らせ直し、`read_console` に警告が無いこと
- [ ] `Room.unity` に差分が出ていないこと（`git status` で確かめる。出ていたら組み立てが場面 1 を踏んでいる）
- [ ] `unity/Assets/Fonts/NotoSansJP-Regular SDF.asset` と `unity/Assets/Materials/Alley/Buyer.mat` を commit に含めていないこと
- [ ] オーナーに見てもらう。秒数・文章・立ち位置・画面の色はすべてここから詰める

## 決めていないこと

- **メモの文**。設計書に無いので場面 1 の写しで置いてある
- **椅子を引く音**。座る演出に音が無い。要るかはオーナーが決める
- **`list` の間の長さ**（仮 2.0 秒）と、どの行の後に入れるか
- **場面 4 へ渡すときの見出し**。いまは「（仮）続く」で止まる
