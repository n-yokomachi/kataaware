import { Box3, type Object3D } from 'three';
import type { AABB } from '../core/collide';
import type { LayoutItem } from '../data/types';

const round = (v: number): number => Math.round(v * 1000) / 1000;

/** 配置データどおりに置き、world 行列を更新する */
export function placeItem(root: Object3D, item: LayoutItem): void {
  root.position.set(item.position[0], item.position[1], item.position[2]);
  root.rotation.set(0, item.rotationY ?? 0, 0);
  const s = item.scale ?? 1;
  root.scale.set(s, s, s);
  root.updateMatrixWorld(true);
}

/** 置いた物の world 座標での外接箱 */
export function colliderOf(root: Object3D): AABB {
  root.updateWorldMatrix(true, false);
  const b = new Box3().setFromObject(root);
  return { min: [b.min.x, b.min.y, b.min.z], max: [b.max.x, b.max.y, b.max.z] };
}

/** 動かした後の位置・向き・大きさを配置データに書き戻す（?layout モードの書き出し用） */
export function itemFrom(root: Object3D, item: LayoutItem): LayoutItem {
  return {
    ...item,
    position: [round(root.position.x), round(root.position.y), round(root.position.z)],
    rotationY: round(root.rotation.y),
    scale: round(root.scale.x),
  };
}
