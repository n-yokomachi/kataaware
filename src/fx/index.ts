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
