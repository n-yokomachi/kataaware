import { PerspectiveCamera, Scene, Timer, WebGLRenderer } from 'three';
import { Fx } from '../fx';

const MAX_DT = 0.1;

export class App {
  readonly renderer: WebGLRenderer;
  readonly scene = new Scene();
  readonly camera: PerspectiveCamera;
  readonly fx: Fx;
  private timer = new Timer();
  private update: (dt: number) => void = () => undefined;

  constructor(canvas: HTMLCanvasElement) {
    this.renderer = new WebGLRenderer({ canvas, antialias: true });
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.camera = new PerspectiveCamera(70, 1, 0.05, 300);
    this.fx = new Fx(this.renderer, this.scene, this.camera);
    window.addEventListener('resize', () => this.resize());
    this.resize();
  }

  /** 非表示のタブでは幅や高さが 0 になることがあるので、そのときは何もしない */
  private resize(): void {
    const w = window.innerWidth;
    const h = window.innerHeight;
    if (w === 0 || h === 0) return;
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.renderer.setSize(w, h, false);
    this.camera.aspect = w / h;
    this.camera.updateProjectionMatrix();
    this.fx.resize(w, h);
  }

  /** 1 フレーム分。update(dt) → 後処理の更新 → 描画 */
  step(dt: number): void {
    this.update(dt);
    this.fx.update(dt);
    this.fx.render();
  }

  /** requestAnimationFrame で step を回す。dt は MAX_DT で頭打ち */
  run(update: (dt: number) => void): void {
    this.update = update;
    const frame = (): void => {
      this.timer.update();
      this.step(Math.min(this.timer.getDelta(), MAX_DT));
      requestAnimationFrame(frame);
    };
    requestAnimationFrame(frame);
  }
}
