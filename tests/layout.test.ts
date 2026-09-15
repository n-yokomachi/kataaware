import { BoxGeometry, Group, Mesh, MeshStandardMaterial } from 'three';
import { describe, expect, it } from 'vitest';
import type { LayoutItem } from '../src/data/types';
import { colliderOf, itemFrom, placeItem } from '../src/scenes/layout';

/** 原点中心の 1m 立方体を子に持つグループ */
function cube(): Group {
  const g = new Group();
  g.add(new Mesh(new BoxGeometry(1, 1, 1), new MeshStandardMaterial()));
  return g;
}

describe('layout', () => {
  it('places an item and derives its world-space collider', () => {
    // 長さ 3 の直方体を Y で 90° 回すと、外接箱の x と z の幅が入れ替わる
    const root = new Group();
    root.add(new Mesh(new BoxGeometry(1, 1, 3), new MeshStandardMaterial()));
    const item: LayoutItem = { asset: 'x.glb', position: [2, 0, -3], rotationY: Math.PI / 2, scale: 2 };
    placeItem(root, item);
    const box = colliderOf(root);
    expect(box.min.map((v) => +v.toFixed(3))).toEqual([-1, -1, -4]);
    expect(box.max.map((v) => +v.toFixed(3))).toEqual([5, 1, -2]);
  });

  it('resets rotation to 0 and scale to 1 when the item omits them', () => {
    const root = cube();
    root.rotation.set(1, 1, 1);
    root.scale.set(3, 3, 3);
    placeItem(root, { asset: 'x.glb', position: [0, 0, 0] });
    expect(root.rotation.x).toBe(0);
    expect(root.rotation.y).toBe(0);
    expect(root.rotation.z).toBe(0);
    expect(root.scale.x).toBe(1);
    expect(colliderOf(root).max).toEqual([0.5, 0.5, 0.5]);
  });

  it('measures the collider in world space even under a moved parent', () => {
    const parent = new Group();
    parent.position.set(10, 0, 0);
    const root = cube();
    parent.add(root);
    placeItem(root, { asset: 'x.glb', position: [0, 0, 0] });
    expect(colliderOf(root).min[0]).toBeCloseTo(9.5);
    expect(colliderOf(root).max[0]).toBeCloseTo(10.5);
  });

  it('writes the transform back into an item, rounded to millimetres', () => {
    const root = cube();
    const item: LayoutItem = { asset: 'x.glb', position: [0, 0, 0], collider: false };
    placeItem(root, item);
    root.position.set(1.23456, 0, -2.5);
    root.rotation.y = 0.78539;
    expect(itemFrom(root, item)).toEqual({
      asset: 'x.glb',
      position: [1.235, 0, -2.5],
      rotationY: 0.785,
      scale: 1,
      collider: false,
    });
  });
});
