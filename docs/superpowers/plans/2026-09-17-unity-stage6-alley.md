# 場面 2（路地裏）Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `docs/superpowers/specs/2026-09-16-scenario-design.md` の 6 節を実装する。場面 1 のドアから暗転を挟まず路地裏へ切り替わり、通りを歩いてヤードへ入り、自分の露店のテーブルにチップを置くと買い手のやりとりが流れて場面 3 へ抜ける。

**Architecture:** 場面 1 と同じ仕組みを使い回す。`SceneFlow` に「必須を終えたら次に読むシーン」を持たせ、`（仮）続く` の代わりに切り替える。路地裏は新しいシーン `Alley.unity` として作り、Player・Hud・SceneFlow・Pins は Room から複製する。建物と露店は箱と `ProcMesh` で組み、ネオンは PIL で描いたテクスチャを自発光のマテリアルに貼る。人は遠景の板で置き、顔は作らない。

**Tech Stack:** Unity 6000.3.24f1、URP 17.3、TextMeshPro、MCP for Unity。

**実行時の注意:**

- **シーンを変える前・再生する前に `EditorApplication.isPlaying` を読む。** 再生中はオーナーが遊んでいる。再生中に加えたシーンの変更は再生を抜けた時点で消える
- 用語はカタカナのまま（`CLAUDE.md` の用語の節）
- コミットに著者としてモデル名・ツール名を入れない
- `execute_code` は C# 6 の範囲
- フォントのアトラス `unity/Assets/Fonts/NotoSansJP-Regular SDF.asset` はコミットに入れない
- 文面は 6 節から 1 文字も変えずに写す

---

## Task 1: 場面をまたぐ

**Files:**
- Modify: `unity/Assets/Scripts/Flow/SceneFlow.cs`
- Create: `unity/Assets/Scripts/Flow/SceneExit.cs`
- Test: `unity/Assets/Tests/EditMode/SceneExitTests.cs`

- [ ] **Step 1: 次に読むシーンの名前と、切り替えるかどうかの判定を純粋な形で書いてテストする**
- [ ] **Step 2: `SceneFlow.Complete` を、名前があれば `SceneManager.LoadScene`、無ければこれまでどおり `（仮）続く` に分ける**
- [ ] **Step 3: 暗転を挟まない。場面 1 の終わりだけ暗転する規則（ゲームデザイン 8 節）に従い、路地裏へは直接切り替える**
- [ ] **Step 4: テストを通してコミット**

## Task 2: 路地裏のシーンの器

**Files:**
- Create: `unity/Assets/Scenes/Alley.unity`
- Modify: `unity/Assets/Editor/BuildAlley.cs`（新規）

- [ ] **Step 1: Room を複製して部屋の中身を消し、Player・Hud・SceneFlow・Pins だけ残す**
- [ ] **Step 2: Build Settings に足す**
- [ ] **Step 3: 文面の入れ物 `AlleyScript.asset` を作り、6.2〜6.4 の文面を入れる**
- [ ] **Step 4: 何も無い床の上を歩けることを確かめてコミット**

## Task 3: 通りとヤード

**Files:**
- Modify: `unity/Assets/Editor/BuildAlley.cs`

- [ ] **Step 1: 一本道の形を決める。グレビル・ストリートの入口から小路、T 字のヤードまで**
- [ ] **Step 2: 建物の壁を箱で立て、路面を敷く**
- [ ] **Step 3: 露店（タープ・テーブル・椅子・看板）を組む**
- [ ] **Step 4: 歩いて端まで行けることを当たりで確かめてコミット**

## Task 4: ネオン

**Files:**
- Create: `unity/Assets/Textures/Neon*.png`
- Modify: `unity/Assets/Editor/BuildAlley.cs`

- [ ] **Step 1: 看板の絵を PIL で描く（6.3 の 3 つと、小路の表示板、自分の看板）**
- [ ] **Step 2: 自発光のマテリアルに貼り、通りに掛ける**
- [ ] **Step 3: 路面の濡れた反射を作る**
- [ ] **Step 4: 撮って確かめてコミット**

## Task 5: 人

**Files:**
- Modify: `unity/Assets/Editor/BuildAlley.cs`

- [ ] **Step 1: 遠景の人を置く。顔は作らない**
- [ ] **Step 2: 売り手は座らせ、買い手はゆっくり歩かせる**
- [ ] **Step 3: 撮って確かめてコミット**

## Task 6: 流れ

**Files:**
- Create: `unity/Assets/Scripts/Flow/AlleyDirector.cs`
- Test: `unity/Assets/Tests/EditMode/AlleyDirectorTests.cs`

- [ ] **Step 1: テーブルにチップを置いたら、買い手のやりとりを順に流す**
- [ ] **Step 2: 流れているあいだは調べる操作を止める**
- [ ] **Step 3: 終わったら場面 3 へ（今は `（仮）続く`）**
- [ ] **Step 4: 通しで確かめてコミット**
