# 舞台と小物のモデル 段階 1: 描画と配置の仕組み Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `docs/superpowers/specs/2026-09-16-assets-design.md` の 2 節と 3 節を実装する。PS1 風の後処理（低解像度の描画先、減色とディザ）、glTF の配置データと読み込み、`?layout` モードを足し、今の仮の箱を粗い描画で出しつつ、椅子 1 点を Kenney の glTF に置き換えて配置データの読み込みを確かめる。

**Architecture:** `Fx` の EffectComposer に低解像度で最近傍の描画先を渡し、減色とディザのパスを色味の後に置く。舞台の定義を `{ boxes?, layout?, url? }` に広げ、`layout` の各項目を GLTFLoader（キャッシュつき）で読んで複製し、`placeItem` で置き、外接箱を当たり判定にする。`?layout` モードは OrbitControls と TransformControls で物を動かし、`itemFrom` で値を書き出す。

**Tech Stack:** three.js 0.186（`three/addons` の GLTFLoader、OrbitControls、TransformControls）、Vite、TypeScript、vitest。

**実行時の注意:**
- コミットメッセージにモデル名・ツール名を著者として入れない
- subagent のモデルは `opus` か `sonnet` で毎回明示する
- 動作確認は `CLAUDE.md` の方法で行う。`?layout` はポインタロックを使わない
- Kenney Furniture Kit（CC0）の zip は数十 MB。取得先は https://kenney.nl/assets/furniture-kit のダウンロード（ページからリンクを取る）。取得できなければ https://poly.pizza/bundle/Furniture-Kit-NoG1sEUD1z から椅子だけを取る

---

## ファイル構成

| パス | 変更 | 責務 |
|---|---|---|
| `src/data/types.ts` | 変更 | `LayoutItem`、`EnvironmentDef` を `{ boxes?, layout?, url? }` に |
| `src/scenes/layout.ts` | 新規 | `placeItem`、`colliderOf`、`itemFrom`（three の数学だけ。DOM 非依存） |
| `src/scenes/assets.ts` | 新規 | GLTFLoader のキャッシュと複製 |
| `src/scenes/environment.ts` | 変更 | `layout` の読み込み、所有する材質と形状だけ破棄 |
| `src/fx/ps1-pass.ts` | 新規 | 減色とディザ |
| `src/fx/index.ts` | 変更 | 低解像度の描画先、PS1 パス、`setPs1` |
| `src/debug/layout-mode.ts` | 新規 | `?layout` モード |
| `src/main.ts` | 変更 | `?layout` の配線 |
| `src/data/scenes/room.ts` | 変更 | 椅子を配置データに |
| `public/assets/kenney/chair.glb`, `public/assets/LICENSES.md` | 新規 | 試験用の素材と出典 |
| `docs/superpowers/specs/2026-09-15-foundation-design.md` | 変更 | 4 節と 10 節に配置データを追記 |
| `tests/layout.test.ts` | 新規 | 配置と外接箱 |

---

### Task 1: 配置データの型と、置く・外接箱を取る・書き戻す

**Files:**
- Modify: `src/data/types.ts`, `src/scenes/environment.ts`
- Create: `src/scenes/layout.ts`
- Test: `tests/layout.test.ts`

- [ ] **Step 1: 型を足す**

`src/data/types.ts` の `export type EnvironmentDef = { boxes: BoxDef[] } | { url: string };` を次に置き換える:

```ts
/** 舞台に置く glTF 1 つ分。asset は public/assets/ 以下の相対パス */
export interface LayoutItem {
  asset: string;
  position: Vec3;
  rotationY?: number; // ラジアン。省略時 0
  scale?: number; // 省略時 1
  collider?: boolean; // 省略時 true
  tint?: number; // 材質の色を上書きする
}

/** 舞台の定義。仮の箱、配置データ、three.js editor の書き出しを混在できる */
export interface EnvironmentDef {
  boxes?: BoxDef[];
  layout?: LayoutItem[];
  url?: string;
}
```

- [ ] **Step 2: `environment.ts` を新しい型に合わせる（最小の変更）**

`src/scenes/environment.ts` の `buildEnvironment` の中の

```ts
  const env = def.environment;
  if ('boxes' in env) {
    for (const b of env.boxes) {
      group.add(boxMesh(b));
      if (b.collider !== false) colliders.push(boxFromDef(b));
    }
  } else {
    await loadFromUrl(env.url, group, colliders);
  }
```

を次に置き換える:

```ts
  const env = def.environment;
  for (const b of env.boxes ?? []) {
    group.add(boxMesh(b));
    if (b.collider !== false) colliders.push(boxFromDef(b));
  }
  if (env.url) await loadFromUrl(env.url, group, colliders);
```

Run: `npx tsc --noEmit` — Expected: エラーなし（`layout` はまだ読まない）。

- [ ] **Step 3: 失敗するテストを書く**

`tests/layout.test.ts`:

```ts
import { BoxGeometry, Group, Mesh, MeshStandardMaterial } from 'three';
import { describe, expect, it } from 'vitest';
import type { LayoutItem } from '../src/data/types';
import { colliderOf, itemFrom, placeItem } from '../src/scenes/layout';

/** 原点中心の 1m 立方体を子に持つグループ */
function cube(): Group {
  const g = new Group();
  g.add(new Mesh(new BoxGeometry(1, 1, 1), new MeshStandardMaterial()));
  return g;
}

describe('layout', () => {
  it('places an item and derives its world-space collider', () => {
    const root = cube();
    const item: LayoutItem = { asset: 'x.glb', position: [2, 0, -3], rotationY: Math.PI / 2, scale: 2 };
    placeItem(root, item);
    const box = colliderOf(root);
    expect(box.min.map((v) => +v.toFixed(3))).toEqual([1, -1, -4]);
    expect(box.max.map((v) => +v.toFixed(3))).toEqual([3, 1, -2]);
  });

  it('defaults rotation to 0 and scale to 1', () => {
    const root = cube();
    placeItem(root, { asset: 'x.glb', position: [0, 0, 0] });
    expect(root.rotation.y).toBe(0);
    expect(root.scale.x).toBe(1);
    expect(colliderOf(root).max).toEqual([0.5, 0.5, 0.5]);
  });

  it('writes the transform back into an item, rounded to millimetres', () => {
    const root = cube();
    const item: LayoutItem = { asset: 'x.glb', position: [0, 0, 0], collider: false };
    placeItem(root, item);
    root.position.set(1.23456, 0, -2.5);
    root.rotation.y = 0.78539;
    expect(itemFrom(root, item)).toEqual({
      asset: 'x.glb',
      position: [1.235, 0, -2.5],
      rotationY: 0.785,
      scale: 1,
      collider: false,
    });
  });
});
```

- [ ] **Step 4: 失敗を確認**

Run: `npx vitest run tests/layout.test.ts`
Expected: FAIL。`../src/scenes/layout` が無い。

- [ ] **Step 5: 実装**

`src/scenes/layout.ts`:

```ts
import { Box3, type Object3D } from 'three';
import type { AABB } from '../core/collide';
import type { LayoutItem } from '../data/types';

const round = (v: number): number => Math.round(v * 1000) / 1000;

/** 配置データどおりに置き、world 行列を更新する */
export function placeItem(root: Object3D, item: LayoutItem): void {
  root.position.set(item.position[0], item.position[1], item.position[2]);
  root.rotation.set(0, item.rotationY ?? 0, 0);
  const s = item.scale ?? 1;
  root.scale.set(s, s, s);
  root.updateMatrixWorld(true);
}

/** 置いた物の world 座標での外接箱 */
export function colliderOf(root: Object3D): AABB {
  const b = new Box3().setFromObject(root);
  return { min: [b.min.x, b.min.y, b.min.z], max: [b.max.x, b.max.y, b.max.z] };
}

/** 動かした後の位置・向き・大きさを配置データに書き戻す（?layout モードの書き出し用） */
export function itemFrom(root: Object3D, item: LayoutItem): LayoutItem {
  return {
    ...item,
    position: [round(root.position.x), round(root.position.y), round(root.position.z)],
    rotationY: round(root.rotation.y),
    scale: round(root.scale.x),
  };
}
```

- [ ] **Step 6: 成功を確認**

Run: `npx vitest run tests/layout.test.ts`
Expected: PASS（3 tests）。

Run: `npx tsc --noEmit`
Expected: エラーなし。

- [ ] **Step 7: Commit**

```bash
git add src/data/types.ts src/scenes/environment.ts src/scenes/layout.ts tests/layout.test.ts
git commit -m "feat: layout item type, placement and collider helpers"
```

---

### Task 2: PS1 風の後処理

**Files:**
- Create: `src/fx/ps1-pass.ts`
- Modify: `src/fx/index.ts`

- [ ] **Step 1: `src/fx/ps1-pass.ts`**

```ts
import { ShaderPass } from 'three/addons/postprocessing/ShaderPass.js';

const VERTEX = /* glsl */ `
  varying vec2 vUv;
  void main() {
    vUv = uv;
    gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
  }`;

const Ps1Shader = {
  name: 'Ps1Shader',
  uniforms: {
    tDiffuse: { value: null },
    levels: { value: 32 },
    dither: { value: 1 },
    amount: { value: 1 },
  },
  vertexShader: VERTEX,
  fragmentShader: /* glsl */ `
    uniform sampler2D tDiffuse;
    uniform float levels;
    uniform float dither;
    uniform float amount;
    varying vec2 vUv;
    // 4x4 の規則的なディザ。gl_FragCoord は低解像度の描画先の画素座標
    float bayer2(vec2 a) {
      a = floor(a);
      return fract(a.x / 2.0 + a.y * a.y * 0.75);
    }
    float bayer4(vec2 a) {
      return bayer2(0.5 * a) * 0.25 + bayer2(a);
    }
    void main() {
      vec4 c = texture2D(tDiffuse, vUv);
      float t = bayer4(gl_FragCoord.xy) - 0.5;
      vec3 q = floor(c.rgb * levels + dither * t + 0.5) / levels;
      gl_FragColor = vec4(mix(c.rgb, q, amount), c.a);
    }`,
};

/** 減色（levels 段階）とディザ（dither 0..1）。amount 0 で素通し */
export class Ps1Pass extends ShaderPass {
  constructor() {
    super(Ps1Shader);
  }

  set(levels: number, dither: number, amount: number): void {
    this.uniforms.levels.value = levels;
    this.uniforms.dither.value = dither;
    this.uniforms.amount.value = amount;
    this.enabled = amount > 0;
  }
}
```

- [ ] **Step 2: `src/fx/index.ts` を置き換える**

```ts
import { HalfFloatType, NearestFilter, WebGLRenderTarget, type PerspectiveCamera, type Scene, type WebGLRenderer } from 'three';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { OutputPass } from 'three/addons/postprocessing/OutputPass.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { DazePass } from './daze-pass';
import { Ps1Pass } from './ps1-pass';
import { TonePass } from './tone-pass';

/** 3D の描画解像度。画面に対する比。字幕などの HTML には影響しない */
export const RENDER_SCALE = 1 / 3;
/** 減色の段階数、ディザの強さ、効き具合の既定値 */
export const PS1_DEFAULT = { levels: 32, dither: 1, amount: 1 };

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
  private ps1 = new Ps1Pass();
  private decay: Decay | null = null;

  constructor(renderer: WebGLRenderer, scene: Scene, camera: PerspectiveCamera) {
    // 低解像度で描き、最近傍で拡大する。大きさは resize で決まる
    const target = new WebGLRenderTarget(1, 1, {
      minFilter: NearestFilter,
      magFilter: NearestFilter,
      type: HalfFloatType,
    });
    this.composer = new EffectComposer(renderer, target);
    this.composer.setPixelRatio(1);
    this.composer.addPass(new RenderPass(scene, camera));
    this.composer.addPass(this.daze);
    this.composer.addPass(this.tone);
    this.composer.addPass(this.ps1);
    this.composer.addPass(new OutputPass());
    this.setPs1(PS1_DEFAULT.levels, PS1_DEFAULT.dither, PS1_DEFAULT.amount);
  }

  setDaze(blur: number, wobble: number): void {
    this.decay = null;
    this.daze.set(blur, wobble);
  }

  /** 指定の強さから seconds 秒かけて 0 まで減らす。seconds が 0 以下なら眩暈を出さない */
  dazeDecay(blur: number, wobble: number, seconds: number): void {
    if (seconds <= 0) {
      this.setDaze(0, 0);
      return;
    }
    this.daze.set(blur, wobble);
    this.decay = { blur, wobble, total: seconds, left: seconds };
  }

  setTone(color: number, amount: number): void {
    this.tone.set(color, amount);
  }

  setPs1(levels: number, dither: number, amount: number): void {
    this.ps1.set(levels, dither, amount);
  }

  update(dt: number): void {
    this.daze.tick(dt);
    if (!this.decay) return;
    this.decay.left = Math.max(0, this.decay.left - dt);
    const k = this.decay.left / this.decay.total;
    this.daze.set(this.decay.blur * k, this.decay.wobble * k);
    if (k === 0) this.decay = null;
  }

  /** 画面の大きさから低解像度の描画先の大きさを決める */
  resize(w: number, h: number): void {
    this.composer.setSize(Math.max(1, Math.round(w * RENDER_SCALE)), Math.max(1, Math.round(h * RENDER_SCALE)));
  }

  render(): void {
    this.composer.render();
  }
}
```

- [ ] **Step 3: 型とビルドを確認**

Run: `npx tsc --noEmit` — Expected: エラーなし。
Run: `npx vite build` — Expected: 成功。
Run: `npm test` — Expected: 7 files、46 tests（Task 1 で +3）。

- [ ] **Step 4: 目視（controller が行う）**

`?nolock&debug` で開き、`#view` を取り出して見る。3D が粗い画素で描かれ、ディザの模様が乗り、字幕は鮮明なこと。眩暈のぼかしが低解像度の上でも成立すること。

- [ ] **Step 5: Commit**

```bash
git add src/fx/ps1-pass.ts src/fx/index.ts
git commit -m "feat: low-resolution render target with posterize and dither pass"
```

---

### Task 3: glTF の読み込みと配置、椅子の差し替え

**Files:**
- Create: `src/scenes/assets.ts`, `public/assets/kenney/chair.glb`, `public/assets/LICENSES.md`
- Modify: `src/scenes/environment.ts`, `src/data/scenes/room.ts`

- [ ] **Step 1: 素材を取る**

Kenney Furniture Kit の zip を取得し、椅子の glTF（zip 内の `Models/GLTF format/` にある。名前は `chair.glb` か `chair.gltf` 系。`.gltf` の場合は同名の `.bin` とテクスチャも一緒に置き、パスは `.gltf` を指す）を `public/assets/kenney/` にコピーする。zip そのものはリポジトリに入れない。

`public/assets/LICENSES.md`:

```markdown
# 素材の出典

| パス | 出典 | 許諾 |
|---|---|---|
| `kenney/` | Kenney Furniture Kit https://kenney.nl/assets/furniture-kit | CC0 1.0 |
```

- [ ] **Step 2: `src/scenes/assets.ts`**

```ts
import { Group, Mesh, MeshStandardMaterial, type Object3D } from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';

const ASSET_ROOT = './assets/';
const loader = new GLTFLoader();
const cache = new Map<string, Promise<Group>>();

/** 同じ glTF は 1 度だけ読む。path は public/assets/ 以下の相対パス */
export function loadAsset(path: string): Promise<Group> {
  let pending = cache.get(path);
  if (!pending) {
    pending = loader.loadAsync(ASSET_ROOT + path).then((gltf) => gltf.scene);
    cache.set(path, pending);
  }
  return pending;
}

/** 読み込んだ glTF の複製。形状と材質は共有し、tint があれば材質だけ複製して色を上書きする */
export function instantiate(source: Group, tint?: number): Object3D {
  const root = source.clone(true);
  if (tint === undefined) return root;
  root.traverse((o) => {
    if (!(o instanceof Mesh) || !(o.material instanceof MeshStandardMaterial)) return;
    const m = o.material.clone();
    m.color.set(tint);
    o.material = m;
    o.userData.ownedMaterial = true;
  });
  return root;
}
```

- [ ] **Step 3: `src/scenes/environment.ts` に配置データの読み込みを足す**

import に追加:

```ts
import { instantiate, loadAsset } from './assets';
import { colliderOf, placeItem } from './layout';
```

`boxMesh`、`markerMesh`、`addFloor` で作る `Mesh` に、それぞれ `mesh.userData.owned = true;`（`addFloor` は `floor.userData.owned = true;`）を足す。舞台が自分で作った形状と材質だけを破棄するため。

`buildEnvironment` の `if (env.url) await loadFromUrl(env.url, group, colliders);` の直後に追加:

```ts
  for (const item of env.layout ?? []) {
    const root = instantiate(await loadAsset(item.asset), item.tint);
    root.userData.layoutItem = item;
    placeItem(root, item);
    group.add(root);
    if (item.collider !== false) colliders.push(colliderOf(root));
  }
```

`dispose` を次に置き換える。舞台が自分で作った形状（`owned`）と、tint で複製した材質（`ownedMaterial`）だけを破棄し、キャッシュが持つ glTF の形状と材質は破棄しない:

```ts
    dispose() {
      group.traverse((o) => {
        if (!(o instanceof Mesh)) return;
        if (o.userData.owned) o.geometry.dispose();
        if (o.userData.owned || o.userData.ownedMaterial) {
          const m = o.material;
          if (Array.isArray(m)) m.forEach((x) => x.dispose());
          else m.dispose();
        }
      });
    },
```

- [ ] **Step 4: 椅子を配置データにする**

`src/data/scenes/room.ts` の `ROOM_BOXES` から椅子の行

```ts
  { position: [1.5, 0.25, -1.2], size: [0.5, 0.5, 0.5], color: 0x3a3040, collider: false }, // 椅子（座る位置）
```

を削除し、ファイル末尾に追加:

```ts
import type { LayoutItem } from '../types';

/** 配置データ。段階 1 は椅子だけ。座る位置なので当たり判定は付けない */
export const ROOM_LAYOUT: LayoutItem[] = [
  { asset: 'kenney/chair.glb', position: [1.5, 0, -1.2], rotationY: 0, collider: false },
];
```

（`import` はファイル先頭の既存の import にまとめる。`asset` は Step 1 で置いた実際のファイル名に合わせる。）

`src/data/scenes/s01-room-intro.ts` の `environment: { boxes: ROOM_BOXES },` を `environment: { boxes: ROOM_BOXES, layout: ROOM_LAYOUT },` にし、`ROOM_LAYOUT` を import に足す。

- [ ] **Step 5: 確認**

Run: `npx tsc --noEmit` — Expected: エラーなし。
Run: `npm test` — Expected: 7 files、46 tests。
Run: `npx vite build` — Expected: 成功。`dist/assets/kenney/chair.glb` が出力に含まれる。

目視（controller）: 開始位置の足元に椅子が見え、椅子の大きさが人の座る高さ（座面が 0.4〜0.5m）に近いこと。大きく違えば `scale` で合わせ、値を `ROOM_LAYOUT` に書く。

- [ ] **Step 6: Commit**

```bash
git add src/scenes/assets.ts src/scenes/environment.ts src/data/scenes/room.ts src/data/scenes/s01-room-intro.ts public/assets/kenney public/assets/LICENSES.md
git commit -m "feat: load layout items from glTF and place the chair from Kenney Furniture Kit"
```

---

### Task 4: `?layout` モード

**Files:**
- Create: `src/debug/layout-mode.ts`
- Modify: `src/main.ts`

- [ ] **Step 1: `src/debug/layout-mode.ts`**

```ts
import { Raycaster, Vector2, type Object3D, type PerspectiveCamera, type Scene, type WebGLRenderer } from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { TransformControls } from 'three/addons/controls/TransformControls.js';
import type { LayoutItem } from '../data/types';
import { itemFrom } from '../scenes/layout';

/** 配置データで置いた物の根元（userData.layoutItem を持つ祖先） */
function layoutRootOf(o: Object3D): Object3D | null {
  let cur: Object3D | null = o;
  while (cur) {
    if (cur.userData.layoutItem) return cur;
    cur = cur.parent;
  }
  return null;
}

/**
 * ?layout モード。ゲームは止め、カメラは OrbitControls で回す。
 * 配置した物をクリックで選び、T で移動、R で Y 回転、Esc で選択解除。動かすたびに配置データをコンソールに書き出す。
 */
export function startLayoutMode(renderer: WebGLRenderer, scene: Scene, camera: PerspectiveCamera): void {
  const orbit = new OrbitControls(camera, renderer.domElement);
  orbit.target.set(0, 1, 0);
  orbit.update();

  const transform = new TransformControls(camera, renderer.domElement);
  scene.add(transform.getHelper());
  transform.addEventListener('dragging-changed', (e) => {
    orbit.enabled = !e.value;
  });
  transform.addEventListener('objectChange', () => {
    const o = transform.object;
    if (!o) return;
    console.log(JSON.stringify(itemFrom(o, o.userData.layoutItem as LayoutItem)));
  });

  const ray = new Raycaster();
  const ndc = new Vector2();
  renderer.domElement.addEventListener('pointerdown', (e) => {
    if (transform.dragging) return;
    ndc.set((e.clientX / window.innerWidth) * 2 - 1, -(e.clientY / window.innerHeight) * 2 + 1);
    ray.setFromCamera(ndc, camera);
    const hit = ray.intersectObjects(scene.children, true).find((h) => layoutRootOf(h.object) !== null);
    const root = hit ? layoutRootOf(hit.object) : null;
    if (root) transform.attach(root);
    else transform.detach();
  });

  window.addEventListener('keydown', (e) => {
    if (e.code === 'KeyT') {
      transform.setMode('translate');
      transform.showX = transform.showY = transform.showZ = true;
    }
    if (e.code === 'KeyR') {
      transform.setMode('rotate');
      transform.showX = transform.showZ = false;
      transform.showY = true;
    }
    if (e.code === 'Escape') transform.detach();
  });

  console.log('layout mode: click an object, T=translate, R=rotate(Y), Esc=deselect');
}
```

- [ ] **Step 2: `src/main.ts` の配線**

import に `import { startLayoutMode } from './debug/layout-mode';` を足す。

`const input = new Input(canvas, !params.has('nolock'));` を

```ts
  const layoutMode = params.has('layout');
  const input = new Input(canvas, !params.has('nolock') && !layoutMode);
```

に、`app.run` の呼び出しを

```ts
  app.run((dt) => {
    if (!layoutMode && (!input.requireLock || input.locked)) manager.update(dt);
    input.endFrame();
  });
```

に置き換える。`await manager.start(FIRST_SCENE);` の直後に追加:

```ts
  if (layoutMode) {
    // 配置の確認用。眩暈と減色を切り、ゲームの進行を止めてカメラを自由にする
    app.fx.setDaze(0, 0);
    app.fx.setPs1(PS1_DEFAULT.levels, PS1_DEFAULT.dither, 0);
    overlay.cancelCenter();
    startLayoutMode(app.renderer, app.scene, app.camera);
  }
```

`PS1_DEFAULT` を `./fx` から import する。

- [ ] **Step 3: 確認**

Run: `npx tsc --noEmit` — Expected: エラーなし。
Run: `npx vite build` — Expected: 成功。

目視（controller）: `http://localhost:5173/?layout` を開き、開始クリック後にカメラがドラッグで回り、椅子をクリックすると操作の矢印が出て、動かすとコンソールに `{"asset":"kenney/chair.glb",...}` が出ること。通常の URL では今までどおり遊べること。

- [ ] **Step 4: Commit**

```bash
git add src/debug/layout-mode.ts src/main.ts
git commit -m "feat: layout mode for placing assets in the browser"
```

---

### Task 5: 設計書の追記と通し確認

**Files:**
- Modify: `docs/superpowers/specs/2026-09-15-foundation-design.md`

- [ ] **Step 1: 4 節の共通フィールドの行を更新**

`- 共通: `id`、`kind`、`next`、`transition`（`cut` | `fade`）、`tone`（色味）、`environment`（仮の箱の一覧。後の段で glTF や書き出した場面ファイルに置き換える）` の末尾に次を足す:

`。`environment` は `boxes`（仮の箱）、`layout`（glTF の配置データ。`asset`、位置、Y 回転、大きさ、当たり判定の有無、色の上書き）、`url`（three.js editor の書き出し）を混在できる`

- [ ] **Step 2: 10 節を更新**

10 節の 2 つ目の項目を次に置き換える:

`- 素材の段では、一般的な物は CC0 の glTF、固有物は AI 生成の glTF を `public/assets/` に置き、配置データ（`layout`）で置く。配置の微調整は `?layout` モードで行い、書き出した値を配置データに写す。麦畑のような大量の複製は、その場面を足す段で決める。3D は画面の 3 分の 1 の解像度で描いて最近傍で拡大し、減色とディザを後処理で載せる（`2026-09-16-assets-design.md`）`

- [ ] **Step 3: 通し確認（controller）**

`?nolock&debug` で場面 1 を最初から最後まで通し、粗い描画のまま、座位・ジャック・煙草・立ち上がり・メモリハブ・端末・ドア・「続く」が動くこと。コンソールにエラーが無いこと。

- [ ] **Step 4: Commit**

```bash
git add docs/superpowers/specs/2026-09-15-foundation-design.md
git commit -m "docs: record layout data and low-resolution rendering in the foundation spec"
```

---

## 計画の自己確認

- 設計書 2 節（PS1 風の描画）: Task 2。頂点の揺れは設計書どおり後で試す
- 設計書 3 節（配置データと読み込み、`?layout`、素材の置き場と出典）: Task 1、3、4
- 設計書 6 節の段階 1 の到達点「仮の箱を粗い描画で出し、配置データの読み込みは 1 つの試験用 glTF で確かめる」: Task 2 と Task 3 の椅子
- 型の一貫性: `LayoutItem`（Task 1）を `layout.ts`、`assets.ts`、`environment.ts`、`layout-mode.ts`、`room.ts` が使う。`itemFrom`（Task 1）を Task 4 が使う。`PS1_DEFAULT`（Task 2）を Task 4 が使う。`userData.owned` / `ownedMaterial` は Task 3 の `environment.ts` と `assets.ts` で対になる
