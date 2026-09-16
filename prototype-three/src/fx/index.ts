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
export const PS1_DEFAULT = { levels: 32, dither: 1, amount: 1 } as const;

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
