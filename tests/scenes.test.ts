import { describe, expect, it } from 'vitest';
import { FIRST_SCENE, SCENES, sceneMap } from '../src/data/scenes';

describe('scene data', () => {
  it('starts from the room', () => {
    expect(FIRST_SCENE).toBe('room-intro');
    expect(sceneMap.get(FIRST_SCENE)).toBeDefined();
  });

  it('links every next to an existing scene', () => {
    for (const s of SCENES) {
      if (s.next !== null) expect(sceneMap.has(s.next), `${s.id} -> ${s.next}`).toBe(true);
    }
  });

  it('has unique interactable ids per scene', () => {
    for (const s of SCENES) {
      expect(new Set(s.interactables.map((i) => i.id)).size).toBe(s.interactables.length);
    }
  });

  it('references only existing ids in after', () => {
    for (const s of SCENES) {
      const ids = new Set(s.interactables.map((i) => i.id));
      for (const i of s.interactables) {
        for (const a of i.after ?? []) expect(ids.has(a), `${s.id}: ${i.id} after ${a}`).toBe(true);
      }
    }
  });

  it('orders the room as chips, terminal, door', () => {
    const room = sceneMap.get('room-intro');
    const byId = new Map(room?.interactables.map((i) => [i.id, i]));
    expect(byId.get('chips')?.required).toBe(true);
    expect(byId.get('terminal')?.after).toEqual(['chips']);
    expect(byId.get('door')?.after).toEqual(['terminal']);
  });
});
