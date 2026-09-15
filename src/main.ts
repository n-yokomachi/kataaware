import { App } from './core/app';
import { Input } from './core/input';
import { SceneManager } from './core/scene-manager';
import { FIRST_SCENE, sceneMap } from './data/scenes';
import { startLayoutMode } from './debug/layout-mode';
import { PS1_DEFAULT } from './fx';
import { hooks } from './scenes';
import type { Ctx } from './scenes/runtime';
import { Overlay } from './ui/overlay';

const TO_BE_CONTINUED = '（仮）続く';

async function main(): Promise<void> {
  const params = new URLSearchParams(location.search);
  const canvas = document.getElementById('view') as HTMLCanvasElement;
  const overlay = new Overlay(document.getElementById('overlay') as HTMLElement);
  const app = new App(canvas);
  const layoutMode = params.has('layout');
  const input = new Input(canvas, !params.has('nolock') && !layoutMode);
  const ctx: Ctx = { three: app.scene, camera: app.camera, input, overlay, fx: app.fx };
  let ended = false;
  const manager = new SceneManager(ctx, sceneMap, hooks, async () => {
    ended = true;
    await overlay.fadeTo(1, 1.5);
    overlay.holdCenter(TO_BE_CONTINUED);
  });

  // ポインタロックが外れている間は場面を止め、「クリックで再開」を出す
  const onLockLost = (): void => {
    if (!ended && input.requireLock && document.pointerLockElement !== canvas) overlay.showResume(true);
  };
  overlay.onResume(() => {
    overlay.showResume(false);
    input.requestLock();
  });
  document.addEventListener('pointerlockchange', onLockLost);
  document.addEventListener('pointerlockerror', onLockLost);

  await overlay.fadeTo(1, 0);
  await overlay.waitForStart();
  input.requestLock();
  input.endFrame();
  // 場面の暗転明けを描画するため、最初の場面を開始する前にループを回し始める
  app.run((dt) => {
    if (!layoutMode && (!input.requireLock || input.locked)) manager.update(dt);
    input.endFrame();
  });
  if (params.has('debug')) {
    // 非表示のタブでは requestAnimationFrame が止まるため、確認用に手動で進められるようにする。
    // ポインタロックが要る設定では場面が止まるので、?nolock と併用する
    (window as unknown as { __step: (dt: number, n?: number) => void }).__step = (dt, n = 1) => {
      for (let i = 0; i < n; i++) app.step(dt);
    };
    (window as unknown as { __camera: typeof app.camera }).__camera = app.camera;
  }
  await manager.start(FIRST_SCENE);
  if (layoutMode) {
    // 配置の確認用。眩暈と減色を切り、ゲームの進行を止めてカメラを自由にする
    app.fx.setDaze(0, 0);
    app.fx.setPs1(PS1_DEFAULT.levels, PS1_DEFAULT.dither, 0);
    overlay.cancelCenter();
    startLayoutMode(app.renderer, app.scene, app.camera);
  }
}

void main();
