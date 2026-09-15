import { Group, Mesh, MeshStandardMaterial, type Object3D } from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';

const ASSET_ROOT = './assets/';
const loader = new GLTFLoader();
const cache = new Map<string, Promise<Group>>();

/** 同じ glTF は 1 度だけ読む。path は public/assets/ 以下の相対パス */
export function loadAsset(path: string): Promise<Group> {
  let pending = cache.get(path);
  if (!pending) {
    pending = loader.loadAsync(ASSET_ROOT + path).then((gltf) => gltf.scene);
    cache.set(path, pending);
  }
  return pending;
}

/** 読み込んだ glTF の複製。形状と材質は共有し、tint があれば材質だけ複製して色を上書きする */
export function instantiate(source: Group, tint?: number): Object3D {
  const root = source.clone(true);
  if (tint === undefined) return root;
  root.traverse((o) => {
    if (!(o instanceof Mesh) || !(o.material instanceof MeshStandardMaterial)) return;
    const m = o.material.clone();
    m.color.set(tint);
    o.material = m;
    o.userData.ownedMaterial = true;
  });
  return root;
}
