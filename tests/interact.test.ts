import { describe, expect, it } from 'vitest';
import { selectInteractable } from '../src/core/interact';
import type { Interactable, Vec3 } from '../src/data/types';

const cam: Vec3 = [0, 1.6, 0];
const fwd: Vec3 = [0, 0, -1];
const items: Interactable[] = [
  { id: 'near', position: [0, 1.6, -1], lines: ['a'] },
  { id: 'far', position: [0, 1.6, -1.8], lines: ['b'] },
  { id: 'behind', position: [0, 1.6, 1], lines: ['c'] },
  { id: 'out', position: [0, 1.6, -5], lines: ['d'] },
];

describe('selectInteractable', () => {
  it('picks the nearest item inside the radius and the view cone', () => {
    expect(selectInteractable(cam, fwd, items, new Set())?.id).toBe('near');
  });

  it('ignores items behind the camera and outside the radius', () => {
    expect(selectInteractable(cam, fwd, [items[2], items[3]], new Set())).toBeNull();
  });

  it('skips items already examined when once is set', () => {
    expect(selectInteractable(cam, fwd, items, new Set(['near']))?.id).toBe('far');
  });

  it('keeps repeatable items selectable', () => {
    const rep: Interactable = { id: 'r', position: [0, 1.6, -1], once: false, lines: [] };
    expect(selectInteractable(cam, fwd, [rep], new Set(['r']))?.id).toBe('r');
  });

  it('hides items whose prerequisites are not done', () => {
    const gated: Interactable = { id: 'g', position: [0, 1.6, -1], after: ['x'], lines: [] };
    expect(selectInteractable(cam, fwd, [gated], new Set())).toBeNull();
    expect(selectInteractable(cam, fwd, [gated], new Set(['x']))?.id).toBe('g');
  });
});
