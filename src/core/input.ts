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
    if (!this.requireLock || document.pointerLockElement === this.target) return;
    const result = this.target.requestPointerLock() as unknown;
    if (result instanceof Promise) result.catch(() => undefined);
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

  /** 毎フレームの最後に呼ぶ。消費されなかったマウス移動量も捨て、転換中の動きが次のフレームで一気に反映されないようにする */
  endFrame(): void {
    this.edges.clear();
    this.clickEdge = false;
    this.mouse = { x: 0, y: 0 };
  }
}
