import { Raycaster, Vector2, type Object3D, type PerspectiveCamera, type Scene, type WebGLRenderer } from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { TransformControls } from 'three/addons/controls/TransformControls.js';
import type { LayoutItem } from '../data/types';
import { itemFrom } from '../scenes/layout';

/** 配置データで置いた物の根元（userData.layoutItem を持つ祖先） */
function layoutRootOf(o: Object3D): Object3D | null {
  let cur: Object3D | null = o;
  while (cur) {
    if (cur.userData.layoutItem) return cur;
    cur = cur.parent;
  }
  return null;
}

/**
 * ?layout モード。ゲームは止め、カメラは OrbitControls で回す。
 * 配置した物をクリックで選び、T で移動、R で Y 回転、Esc で選択解除。動かすたびに配置データをコンソールに書き出す。
 */
export function startLayoutMode(renderer: WebGLRenderer, scene: Scene, camera: PerspectiveCamera): void {
  const orbit = new OrbitControls(camera, renderer.domElement);
  orbit.target.set(0, 1, 0);
  orbit.update();

  const transform = new TransformControls(camera, renderer.domElement);
  scene.add(transform.getHelper());
  transform.addEventListener('dragging-changed', (e) => {
    const dragging = Boolean(e.value);
    orbit.enabled = !dragging;
  });
  transform.addEventListener('objectChange', () => {
    const o = transform.object;
    if (!o) return;
    console.log(JSON.stringify(itemFrom(o, o.userData.layoutItem as LayoutItem)));
  });

  const ray = new Raycaster();
  const ndc = new Vector2();
  renderer.domElement.addEventListener('pointerdown', (e) => {
    if (transform.dragging) return;
    ndc.set((e.clientX / window.innerWidth) * 2 - 1, -(e.clientY / window.innerHeight) * 2 + 1);
    ray.setFromCamera(ndc, camera);
    const hit = ray.intersectObjects(scene.children, true).find((h) => layoutRootOf(h.object) !== null);
    const root = hit ? layoutRootOf(hit.object) : null;
    if (root) transform.attach(root);
    else transform.detach();
  });

  window.addEventListener('keydown', (e) => {
    if (e.code === 'KeyT') {
      transform.setMode('translate');
      transform.showX = transform.showY = transform.showZ = true;
    }
    if (e.code === 'KeyR') {
      transform.setMode('rotate');
      transform.showX = transform.showZ = false;
      transform.showY = true;
    }
    if (e.code === 'Escape') transform.detach();
  });

  console.log('layout mode: click an object, T=translate, R=rotate(Y), Esc=deselect');
}
