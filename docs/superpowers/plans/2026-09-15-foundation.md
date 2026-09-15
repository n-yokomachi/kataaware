# 基盤の実装 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 文章も素材も仮のまま、場面 1（自室・導入）を「クリックで開始」からドアを出て「続く」の表示まで遊べる Three.js の土台を作る。場面 2 以降と、そのための仕組み（受け身の経路、車内、モンタージュ）はこの計画に含めない。

**Architecture:** 場面は TypeScript のデータ（`src/data/scenes/*`）で定義し、共通の `Runtime` が歩行の場面を動かす。当たり判定・調べる対象の選択・字幕・進行判定は描画から切り離した純粋関数にして vitest で先にテストを書く。描画は three.js の EffectComposer に眩暈パスと色味パスを載せ、UI は HTML の重ね表示で作る。見回しは Pointer Lock API のマウス移動量から自前で計算する。

**Tech Stack:** three.js、Vite、TypeScript、vitest。物理エンジン・ナビメッシュ・UI ライブラリは使わない。

**設計書との差分:** 設計書は見回しに PointerLockControls を挙げていたが、後で足す受け身の記憶シーンでは見回しを基準方向から一定角度に収める必要があり、歩行と受け身で見回しの仕組みを 1 つにするため、Pointer Lock API を直接使う。設計書 5 節の該当行は Task 11 で書き換える。

**実行時の注意:**
- 版は実装時点の最新安定版を `npm install` で入れ、`package.json` に記録された版をそのまま固定する
- コミットメッセージにモデル名・ツール名を著者として入れない
- タスクを subagent に委ねるときは、モデルを `opus` か `sonnet` で毎回明示する（`CLAUDE.md` の分担方針）
- 動作確認は in-app ブラウザで行う。`?nolock` を付けるとポインタロック無しでマウス移動量を拾う

---

## ファイル構成

| パス | 責務 |
|---|---|
| `index.html` | canvas と overlay の器 |
| `package.json`, `tsconfig.json`, `vite.config.ts` | ビルド・テスト設定 |
| `.claude/launch.json` | in-app ブラウザから開発サーバを起動する設定 |
| `src/main.ts` | 起動。開始画面 → 場面開始 → ループ |
| `src/data/types.ts` | 場面データの型 |
| `src/data/scenes/room.ts` | 自室の舞台。後の自室の場面でも共用する |
| `src/data/scenes/s01-room-intro.ts` | 場面 1 のデータ |
| `src/data/scenes/index.ts` | 場面の一覧と順序 |
| `src/core/progress.ts` | 必須の対象の完了判定（純粋関数） |
| `src/core/collide.ts` | 箱の判定と壁沿いの滑り（純粋関数） |
| `src/core/interact.ts` | 調べる対象の選択（純粋関数） |
| `src/core/walk.ts` | 見回しと歩行（純粋関数 + `Walker`） |
| `src/core/input.ts` | キー、ポインタロック、マウス移動量 |
| `src/core/app.ts` | レンダラ、カメラ、ループ |
| `src/core/scene-manager.ts` | 場面の切り替えと転換 |
| `src/scenes/runtime.ts` | 場面 1 つ分の実行（舞台の生成、更新、後始末） |
| `src/scenes/environment.ts` | 仮の箱と外部ファイルから舞台を組む |
| `src/scenes/hooks/room-intro.ts` | 導入のクレジットとタイトル |
| `src/scenes/index.ts` | 場面固有の演出の一覧 |
| `src/ui/overlay.ts`, `src/ui/overlay.css` | HTML の重ね表示 |
| `src/ui/subtitles.ts` | 字幕の待ち行列（純粋関数） |
| `src/fx/daze-pass.ts`, `src/fx/tone-pass.ts`, `src/fx/index.ts` | 後処理 |
| `tests/*.test.ts` | 純粋関数と場面データのテスト |

---

### Task 1: プロジェクトの土台

**Files:**
- Create: `package.json`, `tsconfig.json`, `vite.config.ts`, `index.html`, `src/main.ts`, `src/ui/overlay.css`, `.claude/launch.json`

- [x] **Step 1: 依存を入れる**

Run:
```bash
npm init -y >/dev/null && npm install three && npm install -D vite typescript vitest @types/three
```
Expected: `package.json` に `three` と devDependencies が記録される。

- [x] **Step 2: `package.json` を整える**

`npm init` が作った内容を次に置き換える。`dependencies` と `devDependencies` は Step 1 で入った値を残す。

```json
{
  "name": "kataaware",
  "private": true,
  "version": "0.0.0",
  "type": "module",
  "scripts": {
    "dev": "vite",
    "build": "tsc --noEmit && vite build",
    "preview": "vite preview",
    "test": "vitest run --passWithNoTests",
    "test:watch": "vitest"
  }
}
```

- [x] **Step 3: `tsconfig.json`**

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "ESNext",
    "moduleResolution": "bundler",
    "lib": ["ES2022", "DOM", "DOM.Iterable"],
    "types": ["vite/client"],
    "strict": true,
    "noEmit": true,
    "isolatedModules": true,
    "skipLibCheck": true
  },
  "include": ["src", "tests", "vite.config.ts"]
}
```

- [x] **Step 4: `vite.config.ts`**

```ts
/// <reference types="vitest/config" />
import { defineConfig } from 'vite';

export default defineConfig({
  base: './',
  build: { target: 'es2022' },
  test: { include: ['tests/**/*.test.ts'], environment: 'node' },
});
```

- [x] **Step 5: `index.html`**

```html
<!doctype html>
<html lang="ja">
  <head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>kataaware</title>
    <link rel="stylesheet" href="./src/ui/overlay.css" />
  </head>
  <body>
    <canvas id="view"></canvas>
    <div id="overlay"></div>
    <script type="module" src="./src/main.ts"></script>
  </body>
</html>
```

- [x] **Step 6: `src/ui/overlay.css`**

```css
html,
body {
  margin: 0;
  height: 100%;
  background: #000;
  overflow: hidden;
  font-family: "Noto Sans JP", "Hiragino Sans", "Yu Gothic", sans-serif;
  color: #eee;
}
#view {
  display: block;
  width: 100vw;
  height: 100vh;
}
#overlay {
  position: fixed;
  inset: 0;
  pointer-events: none;
}
#overlay > * {
  position: absolute;
}
#start,
#resume {
  inset: 0;
  background: #000;
  display: flex;
  align-items: center;
  justify-content: center;
  pointer-events: auto;
  cursor: pointer;
  font-size: 18px;
  letter-spacing: 0.2em;
}
#resume {
  background: rgba(0, 0, 0, 0.6);
}
#fade {
  inset: 0;
  background: #000;
  opacity: 0;
}
#subtitle {
  left: 0;
  right: 0;
  bottom: 8vh;
  padding: 0 10vw;
  text-align: center;
  font-size: 20px;
  line-height: 1.8;
  text-shadow: 0 0 6px #000;
}
#prompt {
  left: 50%;
  top: 50%;
  transform: translate(-50%, -50%);
  font-size: 14px;
  opacity: 0.9;
  white-space: nowrap;
}
#prompt::before {
  content: "";
  display: block;
  width: 6px;
  height: 6px;
  margin: 0 auto 8px;
  border-radius: 50%;
  background: #fff;
}
#center {
  left: 0;
  right: 0;
  top: 45%;
  text-align: center;
  font-size: 28px;
  letter-spacing: 0.3em;
  opacity: 0;
  transition: opacity 0.8s linear;
}
.hidden {
  display: none !important;
}
```

- [x] **Step 7: 仮の `src/main.ts`**

Task 11 で置き換える。ここでは黒い画面が出て、コンソールにエラーが無いことだけ確認する。

```ts
const canvas = document.getElementById('view') as HTMLCanvasElement;
canvas.getContext('webgl2');
console.log('kataaware: boot');
```

- [x] **Step 8: `.claude/launch.json`**

```json
{
  "version": "0.0.1",
  "configurations": [
    {
      "name": "dev",
      "runtimeExecutable": "npm",
      "runtimeArgs": ["run", "dev"],
      "port": 5173
    }
  ]
}
```

- [x] **Step 9: 起動とテスト実行を確認**

Run: `npm test`
Expected: テストファイル無しの表示で終了コード 0。

Run: `npx tsc --noEmit`
Expected: エラーなし。

Run: `npx vite build`
Expected: `dist/index.html` が生成される。

- [x] **Step 10: Commit**

```bash
git add package.json package-lock.json tsconfig.json vite.config.ts index.html src/main.ts src/ui/overlay.css .claude/launch.json
git commit -m "chore: scaffold vite + three + vitest project"
```

---

### Task 2: 場面データの型と進行判定

**Files:**
- Create: `src/data/types.ts`, `src/core/progress.ts`
- Test: `tests/progress.test.ts`

- [x] **Step 1: 型を書く**

`src/data/types.ts`:

```ts
export type Vec3 = [number, number, number];

export interface BoxDef {
  position: Vec3; // 中心
  size: Vec3; // 全幅
  color?: number;
  emissive?: number;
  collider?: boolean; // 省略時 true
}

export type EnvironmentDef = { boxes: BoxDef[] } | { url: string };

export interface Interactable {
  id: string;
  position: Vec3;
  radius?: number; // 省略時 2
  required?: boolean;
  once?: boolean; // 省略時 true
  after?: string[]; // ここに挙げた id が済むまで選べない
  label?: string;
  lines: string[];
}

export interface Daze {
  blur: number;
  wobble: number;
  duration: number; // 秒。値が 0 まで減る時間
}

export type Transition = 'cut' | 'fade';

/** 歩いて調べる場面。受け身の記憶シーンなど他の種類は、その場面を追加する段で足す */
export interface WalkScene {
  id: string;
  kind: 'walk';
  next: string | null;
  transition: Transition; // この場面に入るときの転換
  tone: { color: number; amount: number };
  sky?: number;
  fog?: { color: number; near: number; far: number };
  environment: EnvironmentDef;
  dazeOnEnter?: Daze;
  onEnterLines?: string[];
  spawn: { position: Vec3; yaw: number };
  colliders?: BoxDef[];
  interactables: Interactable[];
}

export type SceneDef = WalkScene;
```

- [x] **Step 2: 失敗するテストを書く**

`tests/progress.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import { isComplete, requiredIds } from '../src/core/progress';

describe('progress', () => {
  it('collects required ids only', () => {
    expect(requiredIds([{ id: 'a', required: true }, { id: 'b' }, { id: 'c', required: true }])).toEqual(['a', 'c']);
  });

  it('is complete when every required id is done', () => {
    expect(isComplete(['a', 'c'], new Set(['a']))).toBe(false);
    expect(isComplete(['a', 'c'], new Set(['a', 'c', 'b']))).toBe(true);
    expect(isComplete([], new Set())).toBe(true);
  });
});
```

- [x] **Step 3: 失敗を確認**

Run: `npx vitest run tests/progress.test.ts`
Expected: FAIL。`Failed to resolve import "../src/core/progress"`。

- [x] **Step 4: 実装**

`src/core/progress.ts`:

```ts
export function requiredIds(items: readonly { id: string; required?: boolean }[]): string[] {
  return items.filter((i) => i.required).map((i) => i.id);
}

export function isComplete(required: readonly string[], done: ReadonlySet<string>): boolean {
  return required.every((id) => done.has(id));
}
```

- [x] **Step 5: 成功を確認**

Run: `npx vitest run tests/progress.test.ts`
Expected: PASS（2 tests）。

- [x] **Step 6: Commit**

```bash
git add src/data/types.ts src/core/progress.ts tests/progress.test.ts
git commit -m "feat: scene data types and progress check"
```

---

### Task 3: 当たり判定と壁沿いの滑り

**Files:**
- Create: `src/core/collide.ts`
- Test: `tests/collide.test.ts`

- [x] **Step 1: 失敗するテストを書く**

`tests/collide.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import { boxFromDef, intersects, moveWithSlide, type AABB } from '../src/core/collide';

// x = 1.9..2.1 に立つ壁
const wall = boxFromDef({ position: [2, 1.5, 0], size: [0.2, 3, 10] });

describe('collide', () => {
  it('builds an axis-aligned box from center and size', () => {
    expect(boxFromDef({ position: [0, 1, 0], size: [2, 2, 4] })).toEqual({ min: [-1, 0, -2], max: [1, 2, 2] });
  });

  it('detects overlap and non-overlap', () => {
    const a: AABB = { min: [0, 0, 0], max: [1, 1, 1] };
    expect(intersects(a, { min: [0.5, 0.5, 0.5], max: [2, 2, 2] })).toBe(true);
    expect(intersects(a, { min: [1, 0, 0], max: [2, 1, 1] })).toBe(false);
  });

  it('moves freely when nothing is in the way', () => {
    expect(moveWithSlide([0, 0, 0], [0.5, 0, -0.5], [wall])).toEqual([0.5, 0, -0.5]);
  });

  it('stops on the blocked axis and slides along the other', () => {
    // 半径 0.3 のプレイヤーが x=1.5 から +0.5 進むと壁に重なる
    expect(moveWithSlide([1.5, 0, 0], [0.5, 0, -0.5], [wall])).toEqual([1.5, 0, -0.5]);
  });
});
```

- [x] **Step 2: 失敗を確認**

Run: `npx vitest run tests/collide.test.ts`
Expected: FAIL。モジュール未解決。

- [x] **Step 3: 実装**

`src/core/collide.ts`:

```ts
import type { BoxDef, Vec3 } from '../data/types';

export interface AABB {
  min: Vec3;
  max: Vec3;
}

export const PLAYER_RADIUS = 0.3;
export const PLAYER_HEIGHT = 1.7;

export function boxFromDef(d: BoxDef): AABB {
  const [x, y, z] = d.position;
  const [w, h, l] = d.size;
  return { min: [x - w / 2, y - h / 2, z - l / 2], max: [x + w / 2, y + h / 2, z + l / 2] };
}

export function intersects(a: AABB, b: AABB): boolean {
  return (
    a.min[0] < b.max[0] && a.max[0] > b.min[0] &&
    a.min[1] < b.max[1] && a.max[1] > b.min[1] &&
    a.min[2] < b.max[2] && a.max[2] > b.min[2]
  );
}

export function playerBox(feet: Vec3, radius = PLAYER_RADIUS, height = PLAYER_HEIGHT): AABB {
  return {
    min: [feet[0] - radius, feet[1], feet[2] - radius],
    max: [feet[0] + radius, feet[1] + height, feet[2] + radius],
  };
}

function blocked(feet: Vec3, boxes: readonly AABB[]): boolean {
  const p = playerBox(feet);
  return boxes.some((b) => intersects(p, b));
}

/** X と Z を別々に試し、ぶつかった軸だけ止める */
export function moveWithSlide(feet: Vec3, delta: Vec3, boxes: readonly AABB[]): Vec3 {
  let x = feet[0];
  let z = feet[2];
  const y = feet[1];
  const tryX = x + delta[0];
  if (!blocked([tryX, y, z], boxes)) x = tryX;
  const tryZ = z + delta[2];
  if (!blocked([x, y, tryZ], boxes)) z = tryZ;
  return [x, y, z];
}
```

- [x] **Step 4: 成功を確認**

Run: `npx vitest run tests/collide.test.ts`
Expected: PASS（4 tests）。

- [x] **Step 5: Commit**

```bash
git add src/core/collide.ts tests/collide.test.ts
git commit -m "feat: AABB collision with axis sliding"
```

---

### Task 4: 字幕の待ち行列

**Files:**
- Create: `src/ui/subtitles.ts`
- Test: `tests/subtitles.test.ts`

- [x] **Step 1: 失敗するテストを書く**

`tests/subtitles.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import { advance, current, emptySubtitles, enqueue } from '../src/ui/subtitles';

describe('subtitles', () => {
  it('shows the first queued line', () => {
    const s = enqueue(emptySubtitles, ['一行目', '二行目']);
    expect(current(s)).toBe('一行目');
  });

  it('advances line by line and empties at the end', () => {
    let s = enqueue(emptySubtitles, ['一行目', '二行目']);
    s = advance(s);
    expect(current(s)).toBe('二行目');
    s = advance(s);
    expect(current(s)).toBeNull();
    expect(s).toEqual(emptySubtitles);
  });

  it('appends lines behind the ones still waiting', () => {
    let s = enqueue(emptySubtitles, ['a']);
    s = enqueue(s, ['b']);
    s = advance(s);
    expect(current(s)).toBe('b');
  });

  it('ignores an empty enqueue', () => {
    expect(enqueue(emptySubtitles, [])).toBe(emptySubtitles);
  });
});
```

- [x] **Step 2: 失敗を確認**

Run: `npx vitest run tests/subtitles.test.ts`
Expected: FAIL。モジュール未解決。

- [x] **Step 3: 実装**

`src/ui/subtitles.ts`:

```ts
export interface SubtitleState {
  lines: readonly string[];
  index: number;
}

export const emptySubtitles: SubtitleState = { lines: [], index: 0 };

export function enqueue(s: SubtitleState, lines: readonly string[]): SubtitleState {
  if (lines.length === 0) return s;
  return { lines: [...s.lines, ...lines], index: s.index };
}

export function advance(s: SubtitleState): SubtitleState {
  if (s.index + 1 >= s.lines.length) return emptySubtitles;
  return { lines: s.lines, index: s.index + 1 };
}

export function current(s: SubtitleState): string | null {
  return s.index < s.lines.length ? s.lines[s.index] : null;
}
```

- [x] **Step 4: 成功を確認**

Run: `npx vitest run tests/subtitles.test.ts`
Expected: PASS（4 tests）。

- [x] **Step 5: Commit**

```bash
git add src/ui/subtitles.ts tests/subtitles.test.ts
git commit -m "feat: subtitle queue"
```

---

### Task 5: 調べる対象の選択

**Files:**
- Create: `src/core/interact.ts`
- Test: `tests/interact.test.ts`

- [x] **Step 1: 失敗するテストを書く**

`tests/interact.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import { selectInteractable } from '../src/core/interact';
import type { Interactable, Vec3 } from '../src/data/types';

const cam: Vec3 = [0, 1.6, 0];
const fwd: Vec3 = [0, 0, -1];
const items: Interactable[] = [
  { id: 'near', position: [0, 1.6, -1], lines: ['a'] },
  { id: 'far', position: [0, 1.6, -1.8], lines: ['b'] },
  { id: 'behind', position: [0, 1.6, 1], lines: ['c'] },
  { id: 'out', position: [0, 1.6, -5], lines: ['d'] },
];

describe('selectInteractable', () => {
  it('picks the nearest item inside the radius and the view cone', () => {
    expect(selectInteractable(cam, fwd, items, new Set())?.id).toBe('near');
  });

  it('ignores items behind the camera and outside the radius', () => {
    expect(selectInteractable(cam, fwd, [items[2], items[3]], new Set())).toBeNull();
  });

  it('skips items already examined when once is set', () => {
    expect(selectInteractable(cam, fwd, items, new Set(['near']))?.id).toBe('far');
  });

  it('keeps repeatable items selectable', () => {
    const rep: Interactable = { id: 'r', position: [0, 1.6, -1], once: false, lines: [] };
    expect(selectInteractable(cam, fwd, [rep], new Set(['r']))?.id).toBe('r');
  });

  it('hides items whose prerequisites are not done', () => {
    const gated: Interactable = { id: 'g', position: [0, 1.6, -1], after: ['x'], lines: [] };
    expect(selectInteractable(cam, fwd, [gated], new Set())).toBeNull();
    expect(selectInteractable(cam, fwd, [gated], new Set(['x']))?.id).toBe('g');
  });
});
```

- [x] **Step 2: 失敗を確認**

Run: `npx vitest run tests/interact.test.ts`
Expected: FAIL。モジュール未解決。

- [x] **Step 3: 実装**

`src/core/interact.ts`:

```ts
import type { Interactable, Vec3 } from '../data/types';

export const DEFAULT_RADIUS = 2;
export const MAX_ANGLE = 0.7; // ラジアン。視線からこの角度以内の対象だけ選ぶ

/**
 * forward は正規化済みの視線方向。
 * 距離が radius 以内、視線からの角度が maxAngle 以内、前提が済んでいる対象のうち最も近いものを返す。
 */
export function selectInteractable(
  camPos: Vec3,
  forward: Vec3,
  items: readonly Interactable[],
  done: ReadonlySet<string>,
  maxAngle = MAX_ANGLE,
): Interactable | null {
  let best: Interactable | null = null;
  let bestDist = Infinity;
  for (const it of items) {
    if ((it.once ?? true) && done.has(it.id)) continue;
    if (it.after?.some((id) => !done.has(id))) continue;
    const dx = it.position[0] - camPos[0];
    const dy = it.position[1] - camPos[1];
    const dz = it.position[2] - camPos[2];
    const dist = Math.hypot(dx, dy, dz);
    if (dist > (it.radius ?? DEFAULT_RADIUS)) continue;
    if (dist > 1e-6) {
      const cos = (dx * forward[0] + dy * forward[1] + dz * forward[2]) / dist;
      if (Math.acos(Math.min(1, Math.max(-1, cos))) > maxAngle) continue;
    }
    if (dist < bestDist) {
      best = it;
      bestDist = dist;
    }
  }
  return best;
}
```

- [x] **Step 4: 成功を確認**

Run: `npx vitest run tests/interact.test.ts`
Expected: PASS（5 tests）。

- [x] **Step 5: Commit**

```bash
git add src/core/interact.ts tests/interact.test.ts
git commit -m "feat: interactable selection by distance and view cone"
```

---
### Task 6: 入力と歩行

**Files:**
- Create: `src/core/input.ts`, `src/core/walk.ts`
- Test: `tests/walk.test.ts`

- [x] **Step 1: 失敗するテストを書く**

`tests/walk.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import { PITCH_LIMIT, WALK_SPEED, applyLook, walkDelta } from '../src/core/walk';

describe('walkDelta', () => {
  it('walks toward -Z when yaw is 0', () => {
    const d = walkDelta(0, 1, 0, 1);
    expect(d[0]).toBeCloseTo(0);
    expect(d[2]).toBeCloseTo(-WALK_SPEED);
  });

  it('strafes toward +X when yaw is 0', () => {
    const d = walkDelta(0, 0, 1, 1);
    expect(d[0]).toBeCloseTo(WALK_SPEED);
    expect(d[2]).toBeCloseTo(0);
  });

  it('normalises diagonal movement', () => {
    const d = walkDelta(0, 1, 1, 1);
    expect(Math.hypot(d[0], d[2])).toBeCloseTo(WALK_SPEED);
  });

  it('turns with yaw', () => {
    // yaw = +90° は左を向く。前進は -X
    const d = walkDelta(Math.PI / 2, 1, 0, 1);
    expect(d[0]).toBeCloseTo(-WALK_SPEED);
    expect(d[2]).toBeCloseTo(0);
  });

  it('stays still without input', () => {
    expect(walkDelta(0, 0, 0, 1)).toEqual([0, 0, 0]);
  });
});

describe('applyLook', () => {
  it('turns right when the mouse moves right', () => {
    const s = applyLook({ feet: [0, 0, 0], yaw: 0, pitch: 0 }, 100, 0);
    expect(s.yaw).toBeLessThan(0);
  });

  it('clamps pitch', () => {
    const up = applyLook({ feet: [0, 0, 0], yaw: 0, pitch: 0 }, 0, -100000);
    expect(up.pitch).toBeCloseTo(PITCH_LIMIT);
    const down = applyLook({ feet: [0, 0, 0], yaw: 0, pitch: 0 }, 0, 100000);
    expect(down.pitch).toBeCloseTo(-PITCH_LIMIT);
  });
});
```

- [x] **Step 2: 失敗を確認**

Run: `npx vitest run tests/walk.test.ts`
Expected: FAIL。モジュール未解決。

- [x] **Step 3: `src/core/input.ts`**

```ts
export interface MouseDelta {
  x: number;
  y: number;
}

/**
 * キー状態、ポインタロック、マウス移動量をまとめる。
 * requireLock が false のときはロック無しでもマウス移動量を拾う（動作確認用 ?nolock）。
 */
export class Input {
  locked = false;
  private keys = new Set<string>();
  private edges = new Set<string>();
  private mouse: MouseDelta = { x: 0, y: 0 };
  private clickEdge = false;

  constructor(private target: HTMLElement, readonly requireLock: boolean) {
    window.addEventListener('keydown', (e) => {
      if (e.repeat) return;
      this.keys.add(e.code);
      this.edges.add(e.code);
    });
    window.addEventListener('keyup', (e) => this.keys.delete(e.code));
    window.addEventListener('mousemove', (e) => {
      if (this.locked || !this.requireLock) {
        this.mouse.x += e.movementX;
        this.mouse.y += e.movementY;
      }
    });
    window.addEventListener('mousedown', (e) => {
      if (e.button === 0 && e.target === this.target) this.clickEdge = true;
    });
    document.addEventListener('pointerlockchange', () => {
      this.locked = document.pointerLockElement === this.target;
    });
    window.addEventListener('blur', () => this.keys.clear());
  }

  requestLock(): void {
    if (this.requireLock && document.pointerLockElement !== this.target) this.target.requestPointerLock();
  }

  down(code: string): boolean {
    return this.keys.has(code);
  }

  justPressed(code: string): boolean {
    return this.edges.has(code);
  }

  /** E キーか canvas 上の左クリックがこのフレームで押されたか */
  interactEdge(): boolean {
    return this.edges.has('KeyE') || this.clickEdge;
  }

  consumeMouse(): MouseDelta {
    const m = { ...this.mouse };
    this.mouse = { x: 0, y: 0 };
    return m;
  }

  /** 毎フレームの最後に呼ぶ */
  endFrame(): void {
    this.edges.clear();
    this.clickEdge = false;
  }
}
```

- [x] **Step 4: `src/core/walk.ts`**

```ts
import type { PerspectiveCamera } from 'three';
import type { Vec3 } from '../data/types';
import { moveWithSlide, type AABB } from './collide';
import type { Input } from './input';

export const LOOK_SENS = 0.0022; // ラジアン / ピクセル
export const WALK_SPEED = 2.6; // m/s
export const EYE_HEIGHT = 1.6;
export const PITCH_LIMIT = 1.4;

export interface WalkState {
  feet: Vec3;
  yaw: number;
  pitch: number;
}

export function applyLook(s: WalkState, dx: number, dy: number, sens = LOOK_SENS): WalkState {
  const pitch = Math.min(PITCH_LIMIT, Math.max(-PITCH_LIMIT, s.pitch - dy * sens));
  return { feet: s.feet, yaw: s.yaw - dx * sens, pitch };
}

/** yaw 0 は -Z 向き。fwd は前(+)後(-)、strafe は右(+)左(-)。斜めは正規化する */
export function walkDelta(yaw: number, fwd: number, strafe: number, dt: number, speed = WALK_SPEED): Vec3 {
  const len = Math.hypot(fwd, strafe);
  if (len === 0) return [0, 0, 0];
  const f = fwd / len;
  const r = strafe / len;
  const x = (-Math.sin(yaw) * f + Math.cos(yaw) * r) * speed * dt;
  const z = (-Math.cos(yaw) * f - Math.sin(yaw) * r) * speed * dt;
  return [x, 0, z];
}

export class Walker {
  state: WalkState;

  constructor(
    spawn: { position: Vec3; yaw: number },
    private boxes: readonly AABB[],
  ) {
    this.state = { feet: [spawn.position[0], spawn.position[1], spawn.position[2]], yaw: spawn.yaw, pitch: 0 };
  }

  update(input: Input, dt: number): void {
    const m = input.consumeMouse();
    this.state = applyLook(this.state, m.x, m.y);
    const fwd = (input.down('KeyW') ? 1 : 0) - (input.down('KeyS') ? 1 : 0);
    const strafe = (input.down('KeyD') ? 1 : 0) - (input.down('KeyA') ? 1 : 0);
    if (fwd === 0 && strafe === 0) return;
    this.state = {
      ...this.state,
      feet: moveWithSlide(this.state.feet, walkDelta(this.state.yaw, fwd, strafe, dt), this.boxes),
    };
  }

  applyTo(camera: PerspectiveCamera): void {
    const [x, y, z] = this.state.feet;
    camera.position.set(x, y + EYE_HEIGHT, z);
    camera.rotation.order = 'YXZ';
    camera.rotation.set(this.state.pitch, this.state.yaw, 0);
  }
}
```

- [x] **Step 5: 成功を確認**

Run: `npx vitest run tests/walk.test.ts`
Expected: PASS（7 tests）。

Run: `npx tsc --noEmit`
Expected: エラーなし。

- [x] **Step 6: Commit**

```bash
git add src/core/input.ts src/core/walk.ts tests/walk.test.ts
git commit -m "feat: input handling and first-person walker"
```

---

### Task 7: 舞台の生成

**Files:**
- Create: `src/scenes/environment.ts`

- [x] **Step 1: 実装**

`src/scenes/environment.ts`:

```ts
import {
  Box3,
  BoxGeometry,
  Color,
  DirectionalLight,
  Fog,
  Group,
  HemisphereLight,
  Mesh,
  MeshStandardMaterial,
  Object3D,
  ObjectLoader,
  PlaneGeometry,
  Scene,
} from 'three';
import { boxFromDef, type AABB } from '../core/collide';
import type { BoxDef, Interactable, SceneDef } from '../data/types';

export interface BuiltEnvironment {
  group: Group;
  colliders: AABB[];
  dispose(): void;
}

const FLOOR_SIZE = 400;
/** 外部ファイルの中で、この接頭辞の名前を持つメッシュは当たり判定に含めない */
const NO_COLLIDE_PREFIX = 'nc_';

function boxMesh(b: BoxDef): Mesh {
  const mat = new MeshStandardMaterial({ color: b.color ?? 0x8a8a92, roughness: 0.9 });
  if (b.emissive !== undefined) {
    mat.emissive = new Color(b.emissive);
    mat.emissiveIntensity = 1;
  }
  const mesh = new Mesh(new BoxGeometry(b.size[0], b.size[1], b.size[2]), mat);
  mesh.position.set(b.position[0], b.position[1], b.position[2]);
  return mesh;
}

/** 仮の箱の段では、調べる対象の位置に光る小さな箱を置いて見つけやすくする */
function markerMesh(it: Interactable): Mesh {
  const mesh = new Mesh(
    new BoxGeometry(0.25, 0.25, 0.25),
    new MeshStandardMaterial({ color: 0xffdd55, emissive: 0xff9900, emissiveIntensity: 0.7 }),
  );
  mesh.position.set(it.position[0], it.position[1], it.position[2]);
  mesh.name = `marker:${it.id}`;
  return mesh;
}

function addLights(group: Group): void {
  group.add(new HemisphereLight(0xffffff, 0x404040, 1.2));
  const sun = new DirectionalLight(0xffffff, 1.0);
  sun.position.set(5, 10, 3);
  group.add(sun);
}

function addFloor(group: Group): void {
  const floor = new Mesh(
    new PlaneGeometry(FLOOR_SIZE, FLOOR_SIZE),
    new MeshStandardMaterial({ color: 0x2e2e34, roughness: 1 }),
  );
  floor.rotation.x = -Math.PI / 2;
  group.add(floor);
}

/** three.js editor が書き出した JSON を読み、メッシュの外接箱を当たり判定にする */
async function loadFromUrl(url: string, group: Group, colliders: AABB[]): Promise<void> {
  const root = (await new ObjectLoader().loadAsync(url)) as Object3D;
  group.add(root);
  root.updateMatrixWorld(true);
  root.traverse((o) => {
    if (!(o instanceof Mesh) || o.name.startsWith(NO_COLLIDE_PREFIX)) return;
    const b = new Box3().setFromObject(o);
    colliders.push({ min: [b.min.x, b.min.y, b.min.z], max: [b.max.x, b.max.y, b.max.z] });
  });
}

export async function buildEnvironment(def: SceneDef): Promise<BuiltEnvironment> {
  const group = new Group();
  const colliders: AABB[] = [];
  addFloor(group);
  addLights(group);
  const env = def.environment;
  if ('boxes' in env) {
    for (const b of env.boxes) {
      group.add(boxMesh(b));
      if (b.collider !== false) colliders.push(boxFromDef(b));
    }
  } else {
    await loadFromUrl(env.url, group, colliders);
  }
  for (const c of def.colliders ?? []) colliders.push(boxFromDef(c));
  for (const it of def.interactables) group.add(markerMesh(it));
  return {
    group,
    colliders,
    dispose() {
      group.traverse((o) => {
        if (!(o instanceof Mesh)) return;
        o.geometry.dispose();
        const m = o.material;
        if (Array.isArray(m)) m.forEach((x) => x.dispose());
        else m.dispose();
      });
    },
  };
}

export function applyAtmosphere(scene: Scene, def: SceneDef): void {
  scene.background = new Color(def.sky ?? 0x101014);
  scene.fog = def.fog ? new Fog(def.fog.color, def.fog.near, def.fog.far) : null;
}
```

- [x] **Step 2: 型を確認**

Run: `npx tsc --noEmit`
Expected: エラーなし。

- [x] **Step 3: Commit**

```bash
git add src/scenes/environment.ts
git commit -m "feat: build gray-box environments with colliders"
```

---

### Task 8: 後処理とアプリの骨組み

**Files:**
- Create: `src/fx/daze-pass.ts`, `src/fx/tone-pass.ts`, `src/fx/index.ts`, `src/core/app.ts`
- Modify: `src/main.ts`（動作確認用に一時的に書き換える）

- [x] **Step 1: `src/fx/daze-pass.ts`**

```ts
import { ShaderPass } from 'three/addons/postprocessing/ShaderPass.js';

const VERTEX = /* glsl */ `
  varying vec2 vUv;
  void main() {
    vUv = uv;
    gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
  }`;

const DazeShader = {
  name: 'DazeShader',
  uniforms: {
    tDiffuse: { value: null },
    blur: { value: 0 },
    wobble: { value: 0 },
    time: { value: 0 },
  },
  vertexShader: VERTEX,
  fragmentShader: /* glsl */ `
    uniform sampler2D tDiffuse;
    uniform float blur;
    uniform float wobble;
    uniform float time;
    varying vec2 vUv;
    void main() {
      vec2 uv = vUv;
      uv.x += wobble * 0.03 * sin(uv.y * 14.0 + time * 2.5);
      uv.y += wobble * 0.02 * sin(uv.x * 11.0 + time * 1.9);
      float r = blur * 0.012;
      vec4 c = vec4(0.0);
      for (int i = -2; i <= 2; i++) {
        for (int j = -2; j <= 2; j++) {
          c += texture2D(tDiffuse, uv + vec2(float(i), float(j)) * r);
        }
      }
      gl_FragColor = c / 25.0;
    }`,
};

/** ぼかし（blur 0..1）と輪郭の揺れ（wobble 0..1）を数値で持つ後処理 */
export class DazePass extends ShaderPass {
  constructor() {
    super(DazeShader);
  }

  set(blur: number, wobble: number): void {
    this.uniforms.blur.value = blur;
    this.uniforms.wobble.value = wobble;
  }

  tick(dt: number): void {
    this.uniforms.time.value += dt;
  }
}
```

- [x] **Step 2: `src/fx/tone-pass.ts`**

```ts
import { Color } from 'three';
import { ShaderPass } from 'three/addons/postprocessing/ShaderPass.js';

const VERTEX = /* glsl */ `
  varying vec2 vUv;
  void main() {
    vUv = uv;
    gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
  }`;

const ToneShader = {
  name: 'ToneShader',
  uniforms: {
    tDiffuse: { value: null },
    color: { value: new Color(1, 1, 1) },
    amount: { value: 0 },
  },
  vertexShader: VERTEX,
  fragmentShader: /* glsl */ `
    uniform sampler2D tDiffuse;
    uniform vec3 color;
    uniform float amount;
    varying vec2 vUv;
    void main() {
      vec4 c = texture2D(tDiffuse, vUv);
      vec3 tinted = c.rgb * color;
      gl_FragColor = vec4(mix(c.rgb, tinted, amount), c.a);
    }`,
};

/** 指定色を掛け合わせた色味へ amount (0..1) だけ寄せる後処理 */
export class TonePass extends ShaderPass {
  constructor() {
    super(ToneShader);
  }

  set(color: number, amount: number): void {
    (this.uniforms.color.value as Color).set(color);
    this.uniforms.amount.value = amount;
  }
}
```

- [x] **Step 3: `src/fx/index.ts`**

```ts
import type { PerspectiveCamera, Scene, WebGLRenderer } from 'three';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { OutputPass } from 'three/addons/postprocessing/OutputPass.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { DazePass } from './daze-pass';
import { TonePass } from './tone-pass';

interface Decay {
  blur: number;
  wobble: number;
  total: number;
  left: number;
}

export class Fx {
  private composer: EffectComposer;
  private daze = new DazePass();
  private tone = new TonePass();
  private decay: Decay | null = null;

  constructor(renderer: WebGLRenderer, scene: Scene, camera: PerspectiveCamera) {
    this.composer = new EffectComposer(renderer);
    this.composer.addPass(new RenderPass(scene, camera));
    this.composer.addPass(this.daze);
    this.composer.addPass(this.tone);
    this.composer.addPass(new OutputPass());
  }

  setDaze(blur: number, wobble: number): void {
    this.decay = null;
    this.daze.set(blur, wobble);
  }

  /** 指定の強さから seconds 秒かけて 0 まで減らす */
  dazeDecay(blur: number, wobble: number, seconds: number): void {
    this.daze.set(blur, wobble);
    this.decay = { blur, wobble, total: seconds, left: seconds };
  }

  setTone(color: number, amount: number): void {
    this.tone.set(color, amount);
  }

  update(dt: number): void {
    this.daze.tick(dt);
    if (!this.decay) return;
    this.decay.left = Math.max(0, this.decay.left - dt);
    const k = this.decay.left / this.decay.total;
    this.daze.set(this.decay.blur * k, this.decay.wobble * k);
    if (k === 0) this.decay = null;
  }

  resize(w: number, h: number): void {
    this.composer.setSize(w, h);
  }

  render(): void {
    this.composer.render();
  }
}
```

- [x] **Step 4: `src/core/app.ts`**

```ts
import { Clock, PerspectiveCamera, Scene, WebGLRenderer } from 'three';
import { Fx } from '../fx';

export class App {
  readonly renderer: WebGLRenderer;
  readonly scene = new Scene();
  readonly camera: PerspectiveCamera;
  readonly fx: Fx;
  private clock = new Clock();

  constructor(canvas: HTMLCanvasElement) {
    this.renderer = new WebGLRenderer({ canvas, antialias: true });
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.camera = new PerspectiveCamera(70, 1, 0.05, 300);
    this.fx = new Fx(this.renderer, this.scene, this.camera);
    const resize = (): void => {
      const w = window.innerWidth;
      const h = window.innerHeight;
      this.renderer.setSize(w, h, false);
      this.camera.aspect = w / h;
      this.camera.updateProjectionMatrix();
      this.fx.resize(w, h);
    };
    window.addEventListener('resize', resize);
    resize();
  }

  /** 毎フレーム update(dt) → 後処理の更新 → 描画 */
  run(update: (dt: number) => void): void {
    const frame = (): void => {
      const dt = Math.min(this.clock.getDelta(), 0.1);
      update(dt);
      this.fx.update(dt);
      this.fx.render();
      requestAnimationFrame(frame);
    };
    requestAnimationFrame(frame);
  }
}
```

- [x] **Step 5: 動作確認用の `src/main.ts`（一時）**

仮の箱の自室を出し、眩暈が 6 秒で消えることと色味が付くことを目で確認する。Task 11 で置き換える。

```ts
import { App } from './core/app';
import { applyAtmosphere, buildEnvironment } from './scenes/environment';
import type { WalkScene } from './data/types';

const canvas = document.getElementById('view') as HTMLCanvasElement;
const app = new App(canvas);
const def: WalkScene = {
  id: 'smoke',
  kind: 'walk',
  next: null,
  transition: 'cut',
  tone: { color: 0xc8d0ff, amount: 0.3 },
  sky: 0x0b0b12,
  environment: {
    boxes: [
      { position: [0, 1.5, -3], size: [6, 3, 0.2], color: 0x4a4a55 },
      { position: [1.5, 0.4, -2.2], size: [1.6, 0.8, 0.8], color: 0x7a5a40 },
    ],
  },
  spawn: { position: [0, 0, 0.5], yaw: 0 },
  interactables: [{ id: 'x', position: [1.5, 1, -2.4], lines: [] }],
};
const env = await buildEnvironment(def);
app.scene.add(env.group);
applyAtmosphere(app.scene, def);
app.camera.position.set(0, 1.6, 0.5);
app.fx.setTone(def.tone.color, def.tone.amount);
app.fx.dazeDecay(1, 1, 6);
app.run(() => {});
```

- [x] **Step 6: ブラウザで確認**

`preview_start` で `dev` を起動し、in-app ブラウザで開く。
Expected: 暗い部屋に壁と机の箱が見え、最初はぼやけて揺れ、6 秒で鮮明になる。コンソールにエラーが無い。

Run: `npx tsc --noEmit`
Expected: エラーなし。

- [x] **Step 7: Commit**

```bash
git add src/fx/daze-pass.ts src/fx/tone-pass.ts src/fx/index.ts src/core/app.ts src/main.ts
git commit -m "feat: renderer with daze and tone post-processing"
```

---

### Task 9: HTML の重ね表示

**Files:**
- Create: `src/ui/overlay.ts`

- [x] **Step 1: 実装**

`src/ui/overlay.ts`:

```ts
const wait = (seconds: number): Promise<void> => new Promise((r) => setTimeout(r, seconds * 1000));

/** 字幕、印、中央の文字、暗転、開始と再開の画面 */
export class Overlay {
  private start: HTMLDivElement;
  private resume: HTMLDivElement;
  private fade: HTMLDivElement;
  private subtitle: HTMLDivElement;
  private prompt: HTMLDivElement;
  private center: HTMLDivElement;

  constructor(root: HTMLElement) {
    const make = (id: string, text = ''): HTMLDivElement => {
      const el = document.createElement('div');
      el.id = id;
      el.textContent = text;
      root.appendChild(el);
      return el;
    };
    // 後に追加したものほど手前に重なる
    this.fade = make('fade');
    this.subtitle = make('subtitle');
    this.prompt = make('prompt');
    this.center = make('center');
    this.resume = make('resume', 'クリックで再開');
    this.start = make('start', 'クリックで開始');
    this.resume.classList.add('hidden');
    this.setPrompt(null);
  }

  waitForStart(): Promise<void> {
    return new Promise((resolve) => {
      this.start.addEventListener(
        'click',
        (e) => {
          e.stopPropagation();
          this.start.classList.add('hidden');
          resolve();
        },
        { once: true },
      );
    });
  }

  showResume(show: boolean): void {
    this.resume.classList.toggle('hidden', !show);
  }

  onResume(cb: () => void): void {
    this.resume.addEventListener('click', (e) => {
      e.stopPropagation();
      cb();
    });
  }

  setSubtitle(text: string | null): void {
    this.subtitle.textContent = text ?? '';
  }

  setPrompt(text: string | null): void {
    this.prompt.textContent = text ?? '';
    this.prompt.classList.toggle('hidden', text === null);
  }

  /** 黒い層の不透明度を seconds 秒かけて変える。0 なら即時 */
  fadeTo(opacity: number, seconds: number): Promise<void> {
    this.fade.style.transition = seconds > 0 ? `opacity ${seconds}s linear` : 'none';
    this.fade.style.opacity = String(opacity);
    return wait(seconds);
  }

  /** 中央に文字を出し、seconds 秒見せてから消す */
  async showCenter(text: string, seconds: number): Promise<void> {
    this.center.textContent = text;
    this.center.style.opacity = '1';
    await wait(seconds);
    this.center.style.opacity = '0';
    await wait(0.8);
  }

  /** 中央に文字を出したままにする（結末用） */
  holdCenter(text: string): void {
    this.center.textContent = text;
    this.center.style.opacity = '1';
  }
}
```

- [x] **Step 2: 型を確認**

Run: `npx tsc --noEmit`
Expected: エラーなし。

- [x] **Step 3: Commit**

```bash
git add src/ui/overlay.ts
git commit -m "feat: DOM overlay for subtitles, prompts and transitions"
```

---
### Task 10: 場面の実行と切り替え

**Files:**
- Create: `src/scenes/runtime.ts`, `src/core/scene-manager.ts`

- [x] **Step 1: `src/scenes/runtime.ts`**

```ts
import { Vector3, type PerspectiveCamera, type Scene } from 'three';
import type { Input } from '../core/input';
import { selectInteractable } from '../core/interact';
import { isComplete, requiredIds } from '../core/progress';
import { Walker } from '../core/walk';
import type { Interactable, SceneDef } from '../data/types';
import type { Fx } from '../fx';
import type { Overlay } from '../ui/overlay';
import { advance, current, emptySubtitles, enqueue, type SubtitleState } from '../ui/subtitles';
import { applyAtmosphere, buildEnvironment, type BuiltEnvironment } from './environment';

export interface Ctx {
  three: Scene;
  camera: PerspectiveCamera;
  input: Input;
  overlay: Overlay;
  fx: Fx;
}

/** 場面固有の演出。データで表せないものだけをここに書く */
export interface Hooks {
  onEnter?(ctx: Ctx, rt: Runtime): void;
  onUpdate?(ctx: Ctx, rt: Runtime, dt: number): void;
  onExit?(ctx: Ctx, rt: Runtime): void;
  /** 進行条件を満たした後、次の場面へ移る前に待つ演出 */
  onComplete?(ctx: Ctx, rt: Runtime): Promise<void>;
}

export type Step = 'continue' | 'complete';

/** 歩いて調べる場面 1 つ分の実行 */
export class Runtime {
  env: BuiltEnvironment | null = null;
  time = 0;
  readonly done = new Set<string>();
  private subs: SubtitleState = emptySubtitles;
  private walker: Walker | null = null;
  private required: string[] = [];
  private forward = new Vector3();

  constructor(
    readonly def: SceneDef,
    private ctx: Ctx,
    readonly hooks: Hooks,
  ) {}

  say(lines: readonly string[]): void {
    this.subs = enqueue(this.subs, lines);
  }

  get talking(): boolean {
    return current(this.subs) !== null;
  }

  async enter(): Promise<void> {
    const { def, ctx } = this;
    this.env = await buildEnvironment(def);
    ctx.three.add(this.env.group);
    applyAtmosphere(ctx.three, def);
    ctx.fx.setTone(def.tone.color, def.tone.amount);
    if (def.dazeOnEnter) ctx.fx.dazeDecay(def.dazeOnEnter.blur, def.dazeOnEnter.wobble, def.dazeOnEnter.duration);
    else ctx.fx.setDaze(0, 0);
    this.required = requiredIds(def.interactables);
    this.walker = new Walker(def.spawn, this.env.colliders);
    this.walker.applyTo(ctx.camera);
    if (def.onEnterLines) this.say(def.onEnterLines);
    this.hooks.onEnter?.(ctx, this);
  }

  /**
   * 字幕の表示中は E キーとクリックを字幕の送りにだけ使い、調べる操作は受け付けない。
   * 必須の対象をすべて調べ、字幕も出ていなければ complete を返す。
   */
  update(dt: number): Step {
    const { ctx, def } = this;
    this.time += dt;
    let interact = ctx.input.interactEdge();
    if (this.talking && interact) {
      this.subs = advance(this.subs);
      interact = false;
    }
    const walker = this.walker as Walker;
    walker.update(ctx.input, dt);
    walker.applyTo(ctx.camera);
    let selected: Interactable | null = null;
    if (!this.talking) {
      ctx.camera.getWorldDirection(this.forward);
      const p = ctx.camera.position;
      selected = selectInteractable(
        [p.x, p.y, p.z],
        [this.forward.x, this.forward.y, this.forward.z],
        def.interactables,
        this.done,
      );
    }
    ctx.overlay.setPrompt(selected ? `E  ${selected.label ?? '調べる'}` : null);
    if (selected && interact) {
      this.say(selected.lines);
      this.done.add(selected.id);
    }
    ctx.overlay.setSubtitle(current(this.subs));
    this.hooks.onUpdate?.(ctx, this, dt);
    return isComplete(this.required, this.done) && !this.talking ? 'complete' : 'continue';
  }

  exit(): void {
    const { ctx } = this;
    this.hooks.onExit?.(ctx, this);
    if (this.env) {
      ctx.three.remove(this.env.group);
      this.env.dispose();
      this.env = null;
    }
    ctx.overlay.setSubtitle(null);
    ctx.overlay.setPrompt(null);
  }
}
```

- [x] **Step 2: `src/core/scene-manager.ts`**

```ts
import type { SceneDef, Transition } from '../data/types';
import { Runtime, type Ctx, type Hooks } from '../scenes/runtime';

/** 現在の場面を 1 つ持ち、完了したら次の場面へ切り替える。次が無ければ onEnd を呼ぶ */
export class SceneManager {
  private current: Runtime | null = null;
  private busy = false;

  constructor(
    private ctx: Ctx,
    private defs: ReadonlyMap<string, SceneDef>,
    private hooks: Readonly<Record<string, Hooks>>,
    private onEnd: () => Promise<void>,
  ) {}

  async start(id: string): Promise<void> {
    this.busy = true;
    await this.enter(id, this.lookup(id).transition);
    this.busy = false;
  }

  update(dt: number): void {
    if (!this.current || this.busy) return;
    if (this.current.update(dt) === 'complete') {
      this.busy = true;
      void this.advance();
    }
  }

  private lookup(id: string): SceneDef {
    const def = this.defs.get(id);
    if (!def) throw new Error(`unknown scene: ${id}`);
    return def;
  }

  private async enter(id: string, transition: Transition): Promise<void> {
    const def = this.lookup(id);
    if (transition === 'fade') await this.ctx.overlay.fadeTo(1, 0.8);
    this.current?.exit();
    this.current = new Runtime(def, this.ctx, this.hooks[id] ?? {});
    await this.current.enter();
    if (transition === 'fade') await this.ctx.overlay.fadeTo(0, 0.8);
  }

  private async advance(): Promise<void> {
    const cur = this.current as Runtime;
    await cur.hooks.onComplete?.(this.ctx, cur);
    const next = cur.def.next;
    if (next === null) {
      cur.exit();
      this.current = null;
      await this.onEnd();
      return;
    }
    await this.enter(next, this.lookup(next).transition);
    this.busy = false;
  }
}
```

- [x] **Step 3: 型を確認**

Run: `npx tsc --noEmit`
Expected: エラーなし。

- [x] **Step 4: Commit**

```bash
git add src/scenes/runtime.ts src/core/scene-manager.ts
git commit -m "feat: scene runtime and manager with cut and fade transitions"
```

---

### Task 11: 自室の場面、配線、通しの確認、ビルド

**Files:**
- Create: `src/data/scenes/room.ts`, `src/data/scenes/s01-room-intro.ts`, `src/data/scenes/index.ts`, `src/scenes/hooks/room-intro.ts`, `src/scenes/index.ts`
- Modify: `src/main.ts`（Task 8 の一時版を置き換える）、`docs/superpowers/specs/2026-09-15-foundation-design.md`
- Test: `tests/scenes.test.ts`

- [x] **Step 1: 失敗するテストを書く**

場面データが壊れていないことを確かめる。

`tests/scenes.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import { FIRST_SCENE, SCENES, sceneMap } from '../src/data/scenes';

describe('scene data', () => {
  it('starts from the room', () => {
    expect(FIRST_SCENE).toBe('room-intro');
    expect(sceneMap.get(FIRST_SCENE)).toBeDefined();
  });

  it('links every next to an existing scene', () => {
    for (const s of SCENES) {
      if (s.next !== null) expect(sceneMap.has(s.next), `${s.id} -> ${s.next}`).toBe(true);
    }
  });

  it('has unique interactable ids per scene', () => {
    for (const s of SCENES) {
      expect(new Set(s.interactables.map((i) => i.id)).size).toBe(s.interactables.length);
    }
  });

  it('references only existing ids in after', () => {
    for (const s of SCENES) {
      const ids = new Set(s.interactables.map((i) => i.id));
      for (const i of s.interactables) {
        for (const a of i.after ?? []) expect(ids.has(a), `${s.id}: ${i.id} after ${a}`).toBe(true);
      }
    }
  });

  it('orders the room as chips, terminal, door', () => {
    const room = sceneMap.get('room-intro');
    const byId = new Map(room?.interactables.map((i) => [i.id, i]));
    expect(byId.get('chips')?.required).toBe(true);
    expect(byId.get('terminal')?.after).toEqual(['chips']);
    expect(byId.get('door')?.after).toEqual(['terminal']);
  });
});
```

- [x] **Step 2: 失敗を確認**

Run: `npx vitest run tests/scenes.test.ts`
Expected: FAIL。`../src/data/scenes` が未解決。

- [x] **Step 3: `src/data/scenes/room.ts`**

```ts
import type { BoxDef, Interactable, Vec3 } from '../types';

/** 6m 四方の自室。-Z 側の壁に机と端末、+Z 側の壁にドア */
export const ROOM_BOXES: BoxDef[] = [
  { position: [0, 1.5, -3], size: [6, 3, 0.2], color: 0x4a4a55 },
  { position: [0, 1.5, 3], size: [6, 3, 0.2], color: 0x4a4a55 },
  { position: [-3, 1.5, 0], size: [0.2, 3, 6], color: 0x4a4a55 },
  { position: [3, 1.5, 0], size: [0.2, 3, 6], color: 0x4a4a55 },
  { position: [0, 3.1, 0], size: [6, 0.2, 6], color: 0x2a2a30, collider: false }, // 天井
  { position: [0.8, 1.1, 2.88], size: [0.9, 2.2, 0.06], color: 0x6a4a3a, collider: false }, // ドア
  { position: [1.5, 0.4, -2.2], size: [1.6, 0.8, 0.8], color: 0x7a5a40 }, // 机
  { position: [1.5, 1.05, -2.5], size: [0.7, 0.45, 0.08], color: 0x111118 }, // 端末の画面
  { position: [-1.8, 0.4, -2.2], size: [0.9, 0.8, 0.5], color: 0x3a3a4a }, // メモリハブ
  { position: [-1.8, 0.35, 1.5], size: [1.2, 0.7, 0.8], color: 0x5a4a3a }, // 卓
];

export const ROOM_SPAWN = { position: [0, 0, 0.5] as Vec3, yaw: 0 };

export const TERMINAL_POS: Vec3 = [1.5, 1.05, -2.4];
export const HUB_POS: Vec3 = [-1.8, 0.9, -2.2];
export const DOOR_POS: Vec3 = [0.8, 1.2, 2.8];

export const ROOM_TONE = { color: 0xc8d0ff, amount: 0.25 };
export const ROOM_SKY = 0x0b0b12;

export const ROOM_OPTIONAL: Interactable[] = [
  { id: 'ashtray', position: [-1.8, 0.85, 1.5], label: '灰皿', lines: ['（仮）灰皿の煙草は、まだ煙を上げていた。'] },
  { id: 'cigarettes', position: [-1.2, 0.85, 1.6], label: '煙草の箱', lines: ['（仮）空だ。切らしていたのを思い出す。'] },
  { id: 'clipboard', position: [2.2, 0.9, -2.0], label: '紙ばさみ', lines: ['（仮）売り上げのメモ。数字と日付だけが並んでいる。'] },
];
```

- [x] **Step 4: `src/data/scenes/s01-room-intro.ts`**

```ts
import type { WalkScene } from '../types';
import { DOOR_POS, HUB_POS, ROOM_BOXES, ROOM_OPTIONAL, ROOM_SKY, ROOM_SPAWN, ROOM_TONE, TERMINAL_POS } from './room';

export const roomIntro: WalkScene = {
  id: 'room-intro',
  kind: 'walk',
  next: null,
  transition: 'fade',
  tone: ROOM_TONE,
  sky: ROOM_SKY,
  environment: { boxes: ROOM_BOXES },
  spawn: ROOM_SPAWN,
  dazeOnEnter: { blur: 1, wobble: 1, duration: 8 },
  interactables: [
    { id: 'chips', position: HUB_POS, required: true, label: 'チップを抜く', lines: ['（仮）六枚のチップを抜き取った。'] },
    {
      id: 'terminal',
      position: TERMINAL_POS,
      required: true,
      after: ['chips'],
      label: '端末',
      lines: ['（仮）電源の落ちた黒い画面に、自分の顔が映っている。', '（仮）起動すると、顔は消えた。'],
    },
    {
      id: 'door',
      position: DOOR_POS,
      required: true,
      after: ['terminal'],
      label: 'ドア',
      lines: ['（仮）チップをポケットに入れて、家を出る。'],
    },
    ...ROOM_OPTIONAL,
  ],
};
```

- [x] **Step 5: `src/data/scenes/index.ts`**

```ts
import type { SceneDef } from '../types';
import { roomIntro } from './s01-room-intro';

export const SCENES: readonly SceneDef[] = [roomIntro];

export const FIRST_SCENE = SCENES[0].id;

export const sceneMap: ReadonlyMap<string, SceneDef> = new Map(SCENES.map((s) => [s.id, s]));
```

- [x] **Step 6: `src/scenes/hooks/room-intro.ts`**

```ts
import type { Hooks } from '../runtime';

export const CREDITS = ['（仮）クレジット 1', '（仮）クレジット 2'];
export const TITLE_CARD = '（仮）タイトル';

/** 眩暈が消えるまでの間にクレジットとタイトルを順に出し、消えたら最初の独白を流す */
export function roomIntroHooks(): Hooks {
  return {
    onEnter(ctx, rt) {
      void (async () => {
        for (const line of CREDITS) await ctx.overlay.showCenter(line, 2);
        await ctx.overlay.showCenter(TITLE_CARD, 2.5);
        rt.say(['（仮）他人の記憶を観た後は、いつもこうなる。']);
      })();
    },
  };
}
```

- [x] **Step 7: `src/scenes/index.ts`**

```ts
import { roomIntroHooks } from './hooks/room-intro';
import type { Hooks } from './runtime';

export const hooks: Readonly<Record<string, Hooks>> = {
  'room-intro': roomIntroHooks(),
};
```

- [x] **Step 8: `src/main.ts` を置き換える**

```ts
import { App } from './core/app';
import { Input } from './core/input';
import { SceneManager } from './core/scene-manager';
import { FIRST_SCENE, sceneMap } from './data/scenes';
import { hooks } from './scenes';
import type { Ctx } from './scenes/runtime';
import { Overlay } from './ui/overlay';

const TO_BE_CONTINUED = '（仮）続く';

async function main(): Promise<void> {
  const params = new URLSearchParams(location.search);
  const canvas = document.getElementById('view') as HTMLCanvasElement;
  const overlay = new Overlay(document.getElementById('overlay') as HTMLElement);
  const app = new App(canvas);
  const input = new Input(canvas, !params.has('nolock'));
  const ctx: Ctx = { three: app.scene, camera: app.camera, input, overlay, fx: app.fx };
  const manager = new SceneManager(ctx, sceneMap, hooks, async () => {
    await overlay.fadeTo(1, 1.5);
    overlay.holdCenter(TO_BE_CONTINUED);
  });

  overlay.onResume(() => {
    overlay.showResume(false);
    input.requestLock();
  });
  document.addEventListener('pointerlockchange', () => {
    if (input.requireLock && document.pointerLockElement !== canvas) overlay.showResume(true);
  });

  await overlay.fadeTo(1, 0);
  await overlay.waitForStart();
  input.requestLock();
  input.endFrame();
  await manager.start(FIRST_SCENE);
  app.run((dt) => {
    manager.update(dt);
    input.endFrame();
  });
}

void main();
```

- [x] **Step 9: テストと型を確認**

Run: `npm test`
Expected: PASS。全ファイル（progress, collide, subtitles, interact, walk, scenes）。

Run: `npx tsc --noEmit`
Expected: エラーなし。

- [x] **Step 10: 通しの動作確認**

`preview_start` で `dev` を起動し、in-app ブラウザで `http://localhost:5173/?nolock` を開く。in-app ブラウザではポインタロックが取れない場合があるため `?nolock` を付ける。操作は `javascript_tool` からキーイベントを送って行う。

歩く（W を押し続ける）:
```js
window.dispatchEvent(new KeyboardEvent('keydown', { code: 'KeyW' }));
```
止まる:
```js
window.dispatchEvent(new KeyboardEvent('keyup', { code: 'KeyW' }));
```
右を向く（100px ぶん）:
```js
window.dispatchEvent(new MouseEvent('mousemove', { movementX: 100, movementY: 0 }));
```
調べる・字幕を送る:
```js
window.dispatchEvent(new KeyboardEvent('keydown', { code: 'KeyE' }));
window.dispatchEvent(new KeyboardEvent('keyup', { code: 'KeyE' }));
```

確認する項目。それぞれスクリーンショットで見る。
1. 「クリックで開始」→ クリックで黒から自室へフェード。ぼやけと揺れの中でクレジット 2 枚とタイトルが順に出て、8 秒で視界が戻り、最初の独白が出る
2. WASD で歩ける。壁と机で止まり、斜めに当たると壁沿いに滑る
3. メモリハブに近づいて向くと印「E  チップを抜く」が出て、E で字幕が出て、E で消える
4. 端末はチップの後にだけ印が出る。ドアは端末の後にだけ印が出る
5. 灰皿・煙草の箱・紙ばさみでも字幕が出て、二度目は印が出ない
6. ドアを調べて字幕を送ると暗転して「（仮）続く」が出る
7. どの段階でもコンソールにエラーが出ない
8. `?nolock` を外して開き、クリックでポインタロックが取れるか確認する。in-app ブラウザで取れなければ、その旨を報告に残す（通常のブラウザで確認する）
   - 結果: in-app ブラウザはペインが非表示だと描画ループもポインタロックも動かないため未確認。通常のブラウザでの確認が残っている。1〜7 は `?nolock&debug` と `window.__step` で確認済み

- [x] **Step 11: ビルドの確認**

Run: `npm run build`
Expected: `dist/` に `index.html` と `assets/` が出る。

Run: `npx vite preview --port 4173`
in-app ブラウザで `http://localhost:4173/?nolock` を開き、開始画面から自室に入れることを確認する。

- [x] **Step 12: 設計書の差分を反映**

`docs/superpowers/specs/2026-09-15-foundation-design.md` の 5 節の行
「見回しは three.js の PointerLockControls。移動は WASD。走る操作は入れない」を
「見回しは Pointer Lock API のマウス移動量から自前で計算する。後で足す受け身の記憶シーンでも同じ仕組みを使う。移動は WASD。走る操作は入れない」に書き換える。

- [x] **Step 13: Commit**

```bash
git add src/data/scenes/room.ts src/data/scenes/s01-room-intro.ts src/data/scenes/index.ts src/scenes/hooks/room-intro.ts src/scenes/index.ts src/main.ts tests/scenes.test.ts docs/superpowers/specs/2026-09-15-foundation-design.md
git commit -m "feat: room intro scene playable in gray-box"
```

---

## 計画の自己確認

- 設計書 1 節の完了条件: Task 11 Step 10（通し）、Step 9（テスト）、Step 11（ビルド）
- 2 節の技術構成とディレクトリ: Task 1、各 Task のファイル配置
- 3 節の実行時の構造: Task 8（App）、Task 10（Runtime, SceneManager）。`walk` のみ
- 4 節の場面データ: Task 2（型）、Task 11（自室のデータ）
- 5 節の歩行と当たり判定: Task 3、Task 6。PointerLockControls の差分は Task 11 Step 12 で設計書に反映
- 6 節の受け身: この計画では実装しない（設計書も後続の段の設計案と明記済み）
- 7 節の調べる操作: Task 5、Task 10 の `update`
- 8 節の UI: Task 9、Task 4。字幕表示中は調べる操作を受け付けない点は Task 10 の `update` で実装。導入のクレジットとタイトルは Task 11 のフック。結末の「続く」は Task 11 の `main.ts`
- 9 節の後処理: Task 8
- 10 節の舞台の作り方: Task 7。`url` の分岐は素材の段で外部ファイルを用意して確認する
- 11 節の検証: 各 Task のテストと Task 11
- 12 節のビルド: Task 1 の `base: './'`、Task 11 Step 11
- 13 節の除外: 場面 2 以降、音、素材、片割れの顔は扱わない
