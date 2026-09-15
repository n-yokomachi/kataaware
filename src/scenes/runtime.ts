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
