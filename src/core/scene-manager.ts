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
      this.advance().catch((err: unknown) => {
        console.error(`scene transition failed after ${this.current?.def.id ?? 'end'}`, err);
      });
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
