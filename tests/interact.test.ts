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

  it('ignores items behind the camera', () => {
    expect(selectInteractable(cam, fwd, [items[2]], new Set())).toBeNull();
  });

  it('ignores items outside the radius', () => {
    expect(selectInteractable(cam, fwd, [items[3]], new Set())).toBeNull();
  });

  it('accepts items inside the view cone and rejects items just outside it', () => {
    // MAX_ANGLE は 0.7 rad（約 40°）。x=0.6, z=-1 は約 31°、x=0.9, z=-1 は約 42°
    const inside: Interactable = { id: 'in', position: [0.6, 1.6, -1], lines: [] };
    const outside: Interactable = { id: 'out', position: [0.9, 1.6, -1], lines: [] };
    expect(selectInteractable(cam, fwd, [inside], new Set())?.id).toBe('in');
    expect(selectInteractable(cam, fwd, [outside], new Set())).toBeNull();
    expect(selectInteractable(cam, fwd, [outside], new Set(), 0.8)?.id).toBe('out');
  });

  it('honours a per-item radius', () => {
    const wide: Interactable = { id: 'w', position: [0, 1.6, -5], radius: 6, lines: [] };
    expect(selectInteractable(cam, fwd, [wide], new Set())?.id).toBe('w');
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
