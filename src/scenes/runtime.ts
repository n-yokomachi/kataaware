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
