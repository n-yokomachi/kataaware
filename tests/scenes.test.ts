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

  it('orders the room as jack, cigarette, chips, terminal, door', () => {
    const room = sceneMap.get('room-intro');
    const byId = new Map(room?.interactables.map((i) => [i.id, i]));
    expect(byId.get('jack')?.required).toBe(true);
    expect(byId.get('cigarette')?.after).toEqual(['jack']);
    expect(byId.get('chips')?.after).toEqual(['cigarette']);
    expect(byId.get('terminal')?.after).toEqual(['chips']);
    expect(byId.get('door')?.after).toEqual(['chips', 'terminal']);
    expect(room?.seat?.standAfter).toBe('cigarette');
    expect(room?.dazeOnEnter?.until).toBe('jack');
  });

  it('keeps hint keys inside after', () => {
    for (const s of SCENES) {
      for (const i of s.interactables) {
        for (const key of Object.keys(i.hints ?? {})) {
          expect(i.after ?? [], `${s.id}: ${i.id} hint ${key}`).toContain(key);
        }
      }
    }
  });

  it('keeps every required interactable reachable through its prerequisites', () => {
    for (const s of SCENES) {
      const byId = new Map(s.interactables.map((i) => [i.id, i]));
      const done = new Set<string>();
      let progressed = true;
      while (progressed) {
        progressed = false;
        for (const i of s.interactables) {
          if (done.has(i.id)) continue;
          if ((i.after ?? []).every((a) => done.has(a))) {
            done.add(i.id);
            progressed = true;
          }
        }
      }
      for (const i of s.interactables) {
        if (i.required) expect(done.has(i.id), `${s.id}: ${i.id} is unreachable`).toBe(true);
      }
      expect(byId.size).toBe(s.interactables.length);
    }
  });
});
