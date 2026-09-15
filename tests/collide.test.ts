import { describe, expect, it } from 'vitest';
import { boxFromDef, intersects, moveWithSlide, type AABB } from '../src/core/collide';

// x = 1.9..2.1 に立つ壁
const wall = boxFromDef({ position: [2, 1.5, 0], size: [0.2, 3, 10] });

describe('collide', () => {
  it('builds an axis-aligned box from center and size', () => {
    expect(boxFromDef({ position: [0, 1, 0], size: [2, 2, 4] })).toEqual({ min: [-1, 0, -2], max: [1, 2, 2] });
  });

  it('detects overlap and non-overlap', () => {
    const a: AABB = { min: [0, 0, 0], max: [1, 1, 1] };
    expect(intersects(a, { min: [0.5, 0.5, 0.5], max: [2, 2, 2] })).toBe(true);
    expect(intersects(a, { min: [1, 0, 0], max: [2, 1, 1] })).toBe(false);
  });

  it('moves freely when nothing is in the way', () => {
    expect(moveWithSlide([0, 0, 0], [0.5, 0, -0.5], [wall])).toEqual([0.5, 0, -0.5]);
  });

  it('stops on the blocked axis and slides along the other', () => {
    // 半径 0.3 のプレイヤーが x=1.5 から +0.5 進むと壁に重なる
    expect(moveWithSlide([1.5, 0, 0], [0.5, 0, -0.5], [wall])).toEqual([1.5, 0, -0.5]);
  });
});
