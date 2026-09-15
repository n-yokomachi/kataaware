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
