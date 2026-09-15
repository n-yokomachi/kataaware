import { App } from './core/app';
import { applyAtmosphere, buildEnvironment } from './scenes/environment';
import type { WalkScene } from './data/types';

const canvas = document.getElementById('view') as HTMLCanvasElement;
const app = new App(canvas);
const def: WalkScene = {
  id: 'smoke',
  kind: 'walk',
  next: null,
  transition: 'cut',
  tone: { color: 0xc8d0ff, amount: 0.3 },
  sky: 0x0b0b12,
  environment: {
    boxes: [
      { position: [0, 1.5, -3], size: [6, 3, 0.2], color: 0x4a4a55 },
      { position: [1.5, 0.4, -2.2], size: [1.6, 0.8, 0.8], color: 0x7a5a40 },
    ],
  },
  spawn: { position: [0, 0, 0.5], yaw: 0 },
  interactables: [{ id: 'x', position: [1.5, 1, -2.4], lines: [] }],
};
const env = await buildEnvironment(def);
app.scene.add(env.group);
applyAtmosphere(app.scene, def);
app.camera.position.set(0, 1.6, 0.5);
app.fx.setTone(def.tone.color, def.tone.amount);
app.fx.dazeDecay(1, 1, 6);
app.run(() => {});
if (new URLSearchParams(location.search).has('debug')) {
  // 非表示のタブでは requestAnimationFrame が止まるため、確認用に手動で進められるようにする
  (window as unknown as { __step: (dt: number, n?: number) => void }).__step = (dt, n = 1) => {
    for (let i = 0; i < n; i++) app.step(dt);
  };
}
