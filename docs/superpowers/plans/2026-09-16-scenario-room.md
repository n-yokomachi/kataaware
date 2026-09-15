# 場面 1 のシナリオ反映 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `docs/superpowers/specs/2026-09-16-scenario-design.md` の 4 節（場面 1 の本文と流れ）を仮の箱の自室に反映する。座った状態で始まりジャックを抜いて煙草を吸ってから立ち上がる導入、前提が済んでいないときの文、別の対象を済ませた後にだけ現れる対象、煙草の自動演出を土台に足し、文面を差し替える。

**Architecture:** 仕組みの追加は既存の層に沿って行う。場面データの型に `seat`、`hints`、`Daze.until` を足し、選択の純粋関数に「未達でも文があれば選べる」規則を足し、`Walker` に目線の高さと移動可否を持たせ、`Runtime` が座位からの立ち上がり・眩暈の保持と解放・未達の文・自動演出中の入力停止を扱う。煙の見た目は HTML の重ね表示、煙草の自動演出は場面固有のフックに置く。

**Tech Stack:** three.js 0.186、Vite 8、TypeScript 7、vitest 5（既存）。

**実行時の注意:**
- コミットメッセージにモデル名・ツール名を著者として入れない
- subagent のモデルは `opus` か `sonnet` で毎回明示する
- `HANDOFF.md` が無くても作らない。`docs/` は Task 5 で指定したファイル以外触らない
- 動作確認は `CLAUDE.md` の方法（`?nolock&debug` と `window.__step`）で行う

---

## ファイル構成

| パス | 変更 | 責務 |
|---|---|---|
| `src/data/types.ts` | 変更 | `Interactable.hints`、`Daze.until`、`WalkScene.seat` |
| `src/core/interact.ts` | 変更 | `unmetPrerequisite`、未達でも文があれば選べる規則 |
| `src/core/walk.ts` | 変更 | `Walker.canMove`、`Walker.eyeHeight` |
| `src/ui/overlay.ts`, `src/ui/overlay.css` | 変更 | 煙の重ね表示 |
| `src/scenes/runtime.ts` | 変更 | 座位と立ち上がり、眩暈の保持と解放、未達の文、入力停止、`onExamine` フック |
| `src/data/scenes/room.ts` | 変更 | 卓を椅子の横へ、対象の位置、任意の対象の文面 |
| `src/data/scenes/s01-room-intro.ts` | 変更 | 座位、眩暈の保持、必須の対象と文面 |
| `src/scenes/hooks/room-intro.ts` | 変更 | クレジット、最初の独白、煙草の自動演出 |
| `docs/superpowers/specs/2026-09-15-foundation-design.md` | 変更 | 4 節に新しいフィールドを追記 |
| `tests/interact.test.ts`, `tests/walk.test.ts`, `tests/scenes.test.ts` | 変更 | 新しい規則のテスト |

---

### Task 1: 型の追加と、未達でも文があれば選べる規則

**Files:**
- Modify: `src/data/types.ts`, `src/core/interact.ts`
- Test: `tests/interact.test.ts`

- [x] **Step 1: 型を足す**

`src/data/types.ts` の `Interactable` を次に置き換える:

```ts
export interface Interactable {
  id: string;
  position: Vec3;
  radius?: number; // 省略時 2
  required?: boolean;
  once?: boolean; // 省略時 true
  after?: string[]; // ここに挙げた id が済むまで選べない。ただし hints に文がある id については選べて、その文だけ出る
  hints?: Record<string, string[]>; // after の id ごとに、未達のときに調べると出す文。出しても済んだことにはならない
  label?: string;
  lines: string[];
}
```

`Daze` を次に置き換える:

```ts
export interface Daze {
  blur: number;
  wobble: number;
  duration: number; // 秒。値が 0 まで減る時間
  until?: string; // この id の対象を調べるまで最大のまま保ち、調べた後に duration で消す
}
```

`WalkScene` に 1 フィールド足す（`interactables` の後）:

```ts
  /** 座った状態で始める。standAfter の対象を調べると立ち上がり、移動できるようになる */
  seat?: { eyeHeight: number; standAfter: string };
```

- [x] **Step 2: 失敗するテストを書く**

`tests/interact.test.ts` の `import` 行を次に置き換える:

```ts
import { selectInteractable, unmetPrerequisite } from '../src/core/interact';
```

`describe('selectInteractable', ...)` の末尾（`hides items whose prerequisites are not done` の後）に追加:

```ts
  it('keeps a gated item selectable when it has a hint for the unmet prerequisite', () => {
    const hinted: Interactable = { id: 'h', position: [0, 1.6, -1], after: ['x'], hints: { x: ['先に x'] }, lines: [] };
    expect(selectInteractable(cam, fwd, [hinted], new Set())?.id).toBe('h');
  });

  it('hides a gated item when the unmet prerequisite has no hint', () => {
    const partly: Interactable = { id: 'p', position: [0, 1.6, -1], after: ['x', 'y'], hints: { x: ['先に x'] }, lines: [] };
    expect(selectInteractable(cam, fwd, [partly], new Set(['x']))).toBeNull();
  });
```

ファイル末尾に追加:

```ts
describe('unmetPrerequisite', () => {
  it('returns the first prerequisite that is not done, or null', () => {
    const it2: Interactable = { id: 'd', position: [0, 0, 0], after: ['a', 'b'], lines: [] };
    expect(unmetPrerequisite(it2, new Set())).toBe('a');
    expect(unmetPrerequisite(it2, new Set(['a']))).toBe('b');
    expect(unmetPrerequisite(it2, new Set(['a', 'b']))).toBeNull();
    expect(unmetPrerequisite({ id: 'n', position: [0, 0, 0], lines: [] }, new Set())).toBeNull();
  });
});
```

- [x] **Step 3: 失敗を確認**

Run: `npx vitest run tests/interact.test.ts`
Expected: FAIL。`unmetPrerequisite` が無い。

- [x] **Step 4: 実装**

`src/core/interact.ts` に関数を足し、`selectInteractable` の `after` の判定を置き換える:

```ts
/** after のうち、まだ済んでいない最初の id。すべて済んでいれば null */
export function unmetPrerequisite(it: Interactable, done: ReadonlySet<string>): string | null {
  for (const id of it.after ?? []) {
    if (!done.has(id)) return id;
  }
  return null;
}
```

`selectInteractable` の中の

```ts
    if (it.after?.some((id) => !done.has(id))) continue;
```

を次に置き換える:

```ts
    const unmet = unmetPrerequisite(it, done);
    if (unmet !== null && !it.hints?.[unmet]) continue;
```

関数の doc コメントの末尾に「前提が未達でも、その id の hints があれば選べる」を足す。

- [x] **Step 5: 成功を確認**

Run: `npx vitest run tests/interact.test.ts`
Expected: PASS（11 tests）。

Run: `npx tsc --noEmit`
Expected: エラーなし。

- [x] **Step 6: Commit**

```bash
git add src/data/types.ts src/core/interact.ts tests/interact.test.ts
git commit -m "feat: hint lines for unmet prerequisites, seat and daze-hold fields"
```

---

### Task 2: Walker の目線の高さと移動可否

**Files:**
- Modify: `src/core/walk.ts`
- Test: `tests/walk.test.ts`

- [x] **Step 1: 失敗するテストを書く**

`tests/walk.test.ts` の `import` を次に置き換える:

```ts
import { PerspectiveCamera } from 'three';
import { describe, expect, it } from 'vitest';
import type { Input } from '../src/core/input';
import { EYE_HEIGHT, PITCH_LIMIT, WALK_SPEED, Walker, applyLook, walkDelta } from '../src/core/walk';
```

ファイル末尾に追加:

```ts
/** W だけ押されている入力の代わり */
const holdingW = { consumeMouse: () => ({ x: 0, y: 0 }), down: (code: string) => code === 'KeyW' } as unknown as Input;

describe('Walker', () => {
  it('walks when canMove is true', () => {
    const w = new Walker({ position: [0, 0, 0], yaw: 0 }, []);
    w.update(holdingW, 0.5);
    expect(w.state.feet[2]).toBeCloseTo(-WALK_SPEED * 0.5);
  });

  it('stays put when canMove is false but still looks around', () => {
    const w = new Walker({ position: [0, 0, 0], yaw: 0 }, []);
    w.canMove = false;
    const turning = { consumeMouse: () => ({ x: 100, y: 0 }), down: (code: string) => code === 'KeyW' } as unknown as Input;
    w.update(turning, 0.5);
    expect(w.state.feet).toEqual([0, 0, 0]);
    expect(w.state.yaw).toBeLessThan(0);
  });

  it('places the camera at its own eye height', () => {
    const w = new Walker({ position: [1, 0, 2], yaw: 0 }, []);
    const camera = new PerspectiveCamera();
    w.applyTo(camera);
    expect(camera.position.y).toBeCloseTo(EYE_HEIGHT);
    w.eyeHeight = 1.1;
    w.applyTo(camera);
    expect(camera.position.y).toBeCloseTo(1.1);
    expect(camera.position.x).toBeCloseTo(1);
    expect(camera.position.z).toBeCloseTo(2);
  });
});
```

- [x] **Step 2: 失敗を確認**

Run: `npx vitest run tests/walk.test.ts`
Expected: FAIL。`canMove` / `eyeHeight` が無い。

- [x] **Step 3: 実装**

`src/core/walk.ts` の `Walker` を次に置き換える:

```ts
export class Walker {
  state: WalkState;
  /** false の間は見回しだけできる（座っている、演出中など） */
  canMove = true;
  /** 足元からカメラまでの高さ。座位と立位で変える */
  eyeHeight = EYE_HEIGHT;

  constructor(
    spawn: { position: Vec3; yaw: number },
    private boxes: readonly AABB[],
  ) {
    this.state = { feet: [spawn.position[0], spawn.position[1], spawn.position[2]], yaw: spawn.yaw, pitch: 0 };
  }

  update(input: Input, dt: number): void {
    const m = input.consumeMouse();
    this.state = applyLook(this.state, m.x, m.y);
    if (!this.canMove) return;
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
    camera.position.set(x, y + this.eyeHeight, z);
    camera.rotation.order = 'YXZ';
    camera.rotation.set(this.state.pitch, this.state.yaw, 0);
  }
}
```

- [x] **Step 4: 成功を確認**

Run: `npx vitest run tests/walk.test.ts`
Expected: PASS（13 tests）。

Run: `npx tsc --noEmit`
Expected: エラーなし。

- [x] **Step 5: Commit**

```bash
git add src/core/walk.ts tests/walk.test.ts
git commit -m "feat: walker eye height and movement lock"
```

---

### Task 3: 煙の重ね表示

**Files:**
- Modify: `src/ui/overlay.ts`, `src/ui/overlay.css`

- [x] **Step 1: CSS**

`src/ui/overlay.css` の `.hidden` の前に追加:

```css
#smoke {
  left: 0;
  right: 0;
  bottom: 0;
  height: 60vh;
  opacity: 0;
  background: linear-gradient(to top, rgba(200, 200, 210, 0.35), rgba(200, 200, 210, 0));
  transition: opacity 1s linear;
}
#smoke.on {
  opacity: 1;
  animation: smoke-drift 4s ease-in-out infinite;
}
@keyframes smoke-drift {
  0% {
    transform: translateY(6vh);
  }
  50% {
    transform: translateY(-4vh);
  }
  100% {
    transform: translateY(6vh);
  }
}
```

- [x] **Step 2: Overlay**

`src/ui/overlay.ts` のフィールドに `private smoke: HTMLDivElement;` を足し、コンストラクタの生成順を次にする（煙は字幕より下、暗転より下）:

```ts
    // 後に追加したものほど手前に重なる。暗転は字幕と印を隠し、中央の文字は暗転の上に出す
    this.smoke = make('smoke');
    this.subtitle = make('subtitle');
    this.prompt = make('prompt');
    this.fade = make('fade');
    this.center = make('center');
    this.resume = make('resume', 'クリックで再開');
    this.start = make('start', 'クリックで開始');
```

メソッドを 1 つ足す（`cancelCenter` の後）:

```ts
  /** 画面下から煙を立ち上らせ、seconds 秒で消す */
  async showSmoke(seconds: number): Promise<void> {
    this.smoke.classList.add('on');
    await wait(seconds);
    this.smoke.classList.remove('on');
  }
```

- [x] **Step 3: 確認**

Run: `npx tsc --noEmit`
Expected: エラーなし。

- [x] **Step 4: Commit**

```bash
git add src/ui/overlay.ts src/ui/overlay.css
git commit -m "feat: smoke overlay"
```

---
### Task 4: Runtime に座位・眩暈の保持・未達の文・入力停止を足す

**Files:**
- Modify: `src/scenes/runtime.ts`

- [x] **Step 1: 実装**

`src/scenes/runtime.ts` を次に置き換える:

```ts
import { Vector3, type PerspectiveCamera, type Scene } from 'three';
import type { Input } from '../core/input';
import { selectInteractable, unmetPrerequisite } from '../core/interact';
import { isComplete, requiredIds } from '../core/progress';
import { EYE_HEIGHT, Walker } from '../core/walk';
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

/** 場面固有の演出。データで表せないものだけをここに書く。非同期の演出は onExit で自分から止める */
export interface Hooks {
  onEnter?(ctx: Ctx, rt: Runtime): void;
  onUpdate?(ctx: Ctx, rt: Runtime, dt: number): void;
  onExit?(ctx: Ctx, rt: Runtime): void;
  /** 対象を調べて済んだ直後 */
  onExamine?(ctx: Ctx, rt: Runtime, item: Interactable): void;
  /** 進行条件を満たした後、次の場面へ移る前に待つ演出 */
  onComplete?(ctx: Ctx, rt: Runtime): Promise<void>;
}

export type Step = 'continue' | 'complete';

/** 座位から立位へ目線を上げる秒数 */
const STAND_SECONDS = 0.6;

/** 歩いて調べる場面 1 つ分の実行 */
export class Runtime {
  env: BuiltEnvironment | null = null;
  time = 0;
  readonly done = new Set<string>();
  private subs: SubtitleState = emptySubtitles;
  private walker: Walker | null = null;
  private required: string[] = [];
  private forward = new Vector3();
  private frozenUntil = 0;
  private standT: number | null = null;
  private dazeReleased = false;

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

  /** seconds 秒のあいだ、調べる操作と進行を止める。見回しはできる */
  freeze(seconds: number): void {
    this.frozenUntil = this.time + seconds;
  }

  async enter(): Promise<void> {
    const { def, ctx } = this;
    this.env = await buildEnvironment(def);
    ctx.three.add(this.env.group);
    applyAtmosphere(ctx.three, def);
    ctx.fx.setTone(def.tone.color, def.tone.amount);
    const daze = def.dazeOnEnter;
    if (daze?.until) ctx.fx.setDaze(daze.blur, daze.wobble);
    else if (daze) ctx.fx.dazeDecay(daze.blur, daze.wobble, daze.duration);
    else ctx.fx.setDaze(0, 0);
    this.required = requiredIds(def.interactables);
    this.walker = new Walker(def.spawn, this.env.colliders);
    if (def.seat) {
      this.walker.canMove = false;
      this.walker.eyeHeight = def.seat.eyeHeight;
    }
    this.walker.applyTo(ctx.camera);
    if (def.onEnterLines) this.say(def.onEnterLines);
    this.hooks.onEnter?.(ctx, this);
  }

  /**
   * 字幕の表示中は E キーとクリックを字幕の送りにだけ使い、調べる操作は受け付けない。
   * freeze 中は調べる操作と進行を止める。
   * 必須の対象をすべて調べ、字幕も出ておらず、止まってもいなければ complete を返す。
   */
  update(dt: number): Step {
    const { ctx, def } = this;
    this.time += dt;
    const frozen = this.time < this.frozenUntil;
    let interact = !frozen && ctx.input.interactEdge();
    if (this.talking && interact) {
      this.subs = advance(this.subs);
      interact = false;
    }
    const walker = this.walker as Walker;
    walker.update(ctx.input, dt);
    walker.applyTo(ctx.camera);
    let selected: Interactable | null = null;
    if (!this.talking && !frozen) {
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
    if (selected && interact) this.examine(selected);
    this.releaseDaze();
    this.standUp(dt, frozen);
    ctx.overlay.setSubtitle(current(this.subs));
    this.hooks.onUpdate?.(ctx, this, dt);
    return isComplete(this.required, this.done) && !this.talking && !frozen ? 'complete' : 'continue';
  }

  /** 前提が未達なら、その id の文だけ出して済んだことにはしない */
  private examine(item: Interactable): void {
    const unmet = unmetPrerequisite(item, this.done);
    if (unmet !== null) {
      this.say(item.hints?.[unmet] ?? []);
      return;
    }
    this.say(item.lines);
    this.done.add(item.id);
    this.hooks.onExamine?.(this.ctx, this, item);
  }

  /** until の対象を調べたら、保っていた眩暈を消し始める */
  private releaseDaze(): void {
    const daze = this.def.dazeOnEnter;
    if (!daze?.until || this.dazeReleased || !this.done.has(daze.until)) return;
    this.dazeReleased = true;
    this.ctx.fx.dazeDecay(daze.blur, daze.wobble, daze.duration);
  }

  /** standAfter の対象を調べたら、止まっていない間に目線を上げて移動を許す */
  private standUp(dt: number, frozen: boolean): void {
    const seat = this.def.seat;
    const walker = this.walker as Walker;
    if (!seat || walker.canMove) return;
    if (this.standT === null) {
      if (frozen || !this.done.has(seat.standAfter)) return;
      this.standT = 0;
    }
    this.standT += dt;
    const k = Math.min(1, this.standT / STAND_SECONDS);
    walker.eyeHeight = seat.eyeHeight + (EYE_HEIGHT - seat.eyeHeight) * k;
    if (k >= 1) walker.canMove = true;
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

- [x] **Step 2: 確認**

Run: `npx tsc --noEmit`
Expected: エラーなし。

Run: `npm test`
Expected: 6 files、40 tests（Task 1 で +5、Task 2 で +3）。

- [x] **Step 3: Commit**

```bash
git add src/scenes/runtime.ts
git commit -m "feat: seated start, held daze, hint lines and input freeze in the runtime"
```

---

### Task 5: 自室の場面データ、文面、演出、テスト、通し確認

**Files:**
- Modify: `src/data/scenes/room.ts`, `src/data/scenes/s01-room-intro.ts`, `src/scenes/hooks/room-intro.ts`, `tests/scenes.test.ts`, `docs/superpowers/specs/2026-09-15-foundation-design.md`

- [x] **Step 1: 失敗するテストを書く**

`tests/scenes.test.ts` の `orders the room as chips, terminal, door` を次に置き換える:

```ts
  it('orders the room as jack, cigarette, chips, terminal, door', () => {
    const room = sceneMap.get('room-intro');
    const byId = new Map(room?.interactables.map((i) => [i.id, i]));
    expect(byId.get('jack')?.required).toBe(true);
    expect(byId.get('cigarette')?.after).toEqual(['jack']);
    expect(byId.get('chips')?.after).toEqual(['cigarette']);
    expect(byId.get('terminal')?.after).toEqual(['chips']);
    expect(byId.get('door')?.after).toEqual(['chips', 'terminal']);
    expect(room?.seat?.standAfter).toBe('cigarette');
    expect(room?.dazeOnEnter?.until).toBe('jack');
  });

  it('keeps hint keys inside after', () => {
    for (const s of SCENES) {
      for (const i of s.interactables) {
        for (const key of Object.keys(i.hints ?? {})) {
          expect(i.after ?? [], `${s.id}: ${i.id} hint ${key}`).toContain(key);
        }
      }
    }
  });
```

- [x] **Step 2: 失敗を確認**

Run: `npx vitest run tests/scenes.test.ts`
Expected: FAIL（`jack` が無い）。

- [x] **Step 3: `src/data/scenes/room.ts`**

```ts
import type { BoxDef, Interactable, Vec3 } from '../types';

/** 6m 四方の自室。-Z 側の壁に机と端末、机の前に椅子、椅子の右に小さな卓、+Z 側の壁にドア */
export const ROOM_BOXES: BoxDef[] = [
  { position: [0, 1.5, -3], size: [6, 3, 0.2], color: 0x4a4a55 },
  { position: [0, 1.5, 3], size: [6, 3, 0.2], color: 0x4a4a55 },
  { position: [-3, 1.5, 0], size: [0.2, 3, 6], color: 0x4a4a55 },
  { position: [3, 1.5, 0], size: [0.2, 3, 6], color: 0x4a4a55 },
  { position: [0, 3.1, 0], size: [6, 0.2, 6], color: 0x2a2a30, collider: false }, // 天井
  { position: [0.8, 1.1, 2.88], size: [0.9, 2.2, 0.06], color: 0x6a4a3a, collider: false }, // ドア
  { position: [1.5, 0.4, -2.2], size: [1.6, 0.8, 0.8], color: 0x7a5a40 }, // 机
  { position: [1.5, 1.05, -2.5], size: [0.7, 0.45, 0.08], color: 0x111118 }, // 端末の画面
  { position: [1.5, 0.25, -1.2], size: [0.5, 0.5, 0.5], color: 0x3a3040, collider: false }, // 椅子（座る位置）
  { position: [2.5, 0.35, -1.0], size: [0.6, 0.7, 0.6], color: 0x5a4a3a }, // 卓。灰皿と煙草
  { position: [-1.8, 0.4, -2.2], size: [0.9, 0.8, 0.5], color: 0x3a3a4a }, // メモリハブ
];

/** 端末の前の椅子に座った状態で始める */
export const ROOM_SPAWN = { position: [1.5, 0, -1.2] as Vec3, yaw: 0 };
export const SEAT_EYE_HEIGHT = 1.1;

export const JACK_POS: Vec3 = [1.75, 0.75, -1.5];
export const CIGARETTE_POS: Vec3 = [2.5, 0.85, -1.0];
export const CIGARETTE_BOX_POS: Vec3 = [2.4, 0.85, -0.85];
export const ASHTRAY_POS: Vec3 = [2.65, 0.85, -1.2];
export const HUB_POS: Vec3 = [-1.8, 0.9, -2.2];
export const TERMINAL_POS: Vec3 = [1.5, 1.05, -2.4];
export const CLIPBOARD_POS: Vec3 = [2.2, 0.9, -2.0];
export const DOOR_POS: Vec3 = [0.8, 1.2, 2.8];

export const ROOM_TONE = { color: 0xc8d0ff, amount: 0.25 };
export const ROOM_SKY = 0x0b0b12;

export const ROOM_OPTIONAL: Interactable[] = [
  { id: 'ashtray', position: ASHTRAY_POS, label: '灰皿', lines: ['吸い殻がたまっている'] },
  {
    id: 'cigarette-box',
    position: CIGARETTE_BOX_POS,
    after: ['cigarette'],
    label: '煙草の箱',
    lines: ['『双鶴（シュアンフー）』という名前の中国産煙草の箱。今はカラだ'],
  },
  {
    id: 'clipboard',
    position: CLIPBOARD_POS,
    label: '紙ばさみ',
    lines: ['売り上げのメモだ', '2166/08/13 5枚　2166/08/14 4枚　2166/08/15 ―'],
  },
];
```

- [x] **Step 4: `src/data/scenes/s01-room-intro.ts`**

```ts
import type { WalkScene } from '../types';
import {
  CIGARETTE_POS,
  DOOR_POS,
  HUB_POS,
  JACK_POS,
  ROOM_BOXES,
  ROOM_OPTIONAL,
  ROOM_SKY,
  ROOM_SPAWN,
  ROOM_TONE,
  SEAT_EYE_HEIGHT,
  TERMINAL_POS,
} from './room';

export const roomIntro: WalkScene = {
  id: 'room-intro',
  kind: 'walk',
  next: null,
  transition: 'fade',
  tone: ROOM_TONE,
  sky: ROOM_SKY,
  environment: { boxes: ROOM_BOXES },
  spawn: ROOM_SPAWN,
  seat: { eyeHeight: SEAT_EYE_HEIGHT, standAfter: 'cigarette' },
  dazeOnEnter: { blur: 1, wobble: 1, duration: 5, until: 'jack' },
  interactables: [
    {
      id: 'jack',
      position: JACK_POS,
      radius: 1.2,
      required: true,
      label: 'インプラントジャックを抜く',
      lines: ['大小の差こそあれ、他人の記憶を観た後はいつもこうだ'],
    },
    { id: 'cigarette', position: CIGARETTE_POS, required: true, after: ['jack'], label: '煙草を取る', lines: [] },
    {
      id: 'chips',
      position: HUB_POS,
      required: true,
      after: ['cigarette'],
      label: 'チップを抜く',
      lines: [
        'メモリが6枚。今日の分だ',
        '08/15 #1 男 41 『ディエゴ』 3分40秒',
        '08/15 #2 女 23 『ミア』 2分05秒',
        '08/15 #3 男 8 『ゆうと』 1分50秒',
        '08/15 #4 女 35 『阿明』 4分00秒',
        '08/15 #5 男 19 『リアム』 1分30秒',
        '08/15 #6 女 52 『マチルド』 3分55秒',
      ],
    },
    {
      id: 'terminal',
      position: TERMINAL_POS,
      required: true,
      after: ['chips'],
      label: '端末',
      lines: [
        '世間ではホロコンソールが人気だが私はもっぱら物理モニターを使っている',
        'というのもほら、',
        'こうして反射で自分の顔が見られるからだ',
        '黒い髪に琥珀色の目。左目の下のほくろ',
        '滅多にないことだが、潜り込んだ他人の記憶が薬などでトリップしていると',
        '私まで影響を受ける',
        '抜け出した後の自己同定のために、鏡を見ることは大切だ',
        'スリープを解除すると、今日の記憶走査条件が表示される',
        '条件　名前を呼ばれた時刻',
        '対象　防壁なし　距離 ランダム　期間 2156年3月2日～2156年3月3日',
        '候補の簡易抽出　56,232,318',
      ],
    },
    {
      id: 'door',
      position: DOOR_POS,
      required: true,
      after: ['chips', 'terminal'],
      hints: { chips: ['テーブルからチップを取ってこよう'], terminal: ['（仮）端末を確かめてからだ'] },
      label: 'ドア',
      lines: ['タバコを買いに行くついでに、今日のチップを売ってしまおう'],
    },
    ...ROOM_OPTIONAL,
  ],
};
```

- [x] **Step 5: `src/scenes/hooks/room-intro.ts`**

```ts
import type { Hooks } from '../runtime';

export const CREDITS = ['制作 〔名義〕'];
export const TITLE_CARD = 'HALF AWARE';
export const FIRST_LINE = 'うぅ…今回は酔いが酷い…';
export const AFTER_SMOKE_LINE = '煙草が切れた…買いに行くついでに今日のメモリも売っちゃおう';
/** 煙草を取ってから吸い終わるまでの秒数 */
export const SMOKE_SECONDS = 4;

/**
 * 眩暈の中でクレジットとタイトルを順に出し、消えたら最初の独白を流す。
 * 煙草を取ったら、煙を立てて数秒止め、吸い終わりの独白を流す。
 * 場面が終わっていたら途中でやめる。
 */
export function roomIntroHooks(): Hooks {
  let alive = false;
  return {
    onEnter(ctx, rt) {
      alive = true;
      void (async () => {
        for (const line of CREDITS) {
          if (!alive) return;
          await ctx.overlay.showCenter(line, 2);
        }
        if (!alive) return;
        await ctx.overlay.showCenter(TITLE_CARD, 2.5);
        if (!alive) return;
        rt.say([FIRST_LINE]);
      })();
    },
    onExamine(ctx, rt, item) {
      if (item.id !== 'cigarette') return;
      rt.freeze(SMOKE_SECONDS);
      void (async () => {
        await ctx.overlay.showSmoke(SMOKE_SECONDS);
        if (alive) rt.say([AFTER_SMOKE_LINE]);
      })();
    },
    onExit(ctx) {
      alive = false;
      ctx.overlay.cancelCenter();
    },
  };
}
```

- [x] **Step 6: 設計書の 4 節に追記**

`docs/superpowers/specs/2026-09-15-foundation-design.md` の 4 節の行

`- `walk`: `spawn`（位置と向き）、`colliders`（見えない壁。省略時は舞台の物から自動生成）、`interactables`（`id`、位置、半径、`required`、`after`（先に済ませる対象）、字幕の行、`once`）、`onEnter` の字幕、`dazeOnEnter`（入ったときの眩暈）`

の末尾に次を足す:

`、`seat`（座った状態で始め、`standAfter` の対象を調べると立ち上がる）。`interactables` の `hints` は `after` の id ごとの未達時の文で、文がある id は未達でも選べて文だけ出る。`dazeOnEnter.until` はその対象を調べるまで眩暈を保つ`

- [x] **Step 7: テストと型と ビルド**

Run: `npm test`
Expected: 6 files、41 tests。

Run: `npx tsc --noEmit`
Expected: エラーなし。

Run: `npm run build`
Expected: 成功。

- [x] **Step 8: 通しの動作確認（controller が行う）**

`CLAUDE.md` の方法で `http://localhost:5173/?nolock&debug` を開き、次を確かめる。

1. 開始直後は座位（カメラの高さ 1.1）で、W を押しても動かない。ぼやけと揺れは消えない
2. 下を向くと「E  インプラントジャックを抜く」が出る。E で独白が出て、そこから眩暈が引いていく
3. 右を向くと卓の上に「E  煙草を取る」。E で煙が立ち、4 秒は E も印も効かない。4 秒後に「煙草が切れた…」が出て、送ると目線が 1.6 まで上がり、歩けるようになる
4. 煙草の箱は吸った後にだけ「E  煙草の箱」が出る
5. チップを取る前にドアを調べると「テーブルからチップを取ってこよう」だけが出て、済んだ扱いにならない
6. メモリハブ → 端末 → ドアの順で文が出て、暗転して「（仮）続く」
7. コンソールにエラーが無い

- [x] **Step 9: Commit**

```bash
git add src/data/scenes/room.ts src/data/scenes/s01-room-intro.ts src/scenes/hooks/room-intro.ts tests/scenes.test.ts docs/superpowers/specs/2026-09-15-foundation-design.md
git commit -m "feat: room intro follows the scenario: seated start, jack, cigarette, hints"
```

---

## 計画の自己確認

- シナリオ設計書 4.1 の流れ（座位 → ジャック → 煙草 → 立ち上がり → メモリハブ → 端末 → ドア）: Task 4 の `seat` / `freeze` / `releaseDaze` / `standUp`、Task 5 のデータ
- 4.2 と 4.3 の文面: Task 5 のデータとフック。端末の独白は 7 行に分割済み。ドアの `terminal` 側の文は仮（オーナーの文が無いため）
- 5 節の「前提が済んでいないときの文」: Task 1 の `hints` と Task 4 の `examine`
- 5 節の「別の対象を済ませた後にだけ現れる」: `after` に文が無ければ隠れる（既存の規則のまま）
- 5 節の煙草の自動演出: Task 3 の煙と Task 5 のフック。効果音は音の段
- 5 節のジャックの前腕: 素材の段。この段では対象の位置だけ
- 型の一貫性: `Walker.canMove` / `eyeHeight`（Task 2）を Task 4 が使う。`unmetPrerequisite`（Task 1）を Task 4 が使う。`showSmoke`（Task 3）を Task 5 が使う。`Hooks.onExamine`（Task 4）を Task 5 が使う
