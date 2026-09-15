import { PerspectiveCamera } from 'three';
import { describe, expect, it } from 'vitest';
import type { Input } from '../src/core/input';
import { EYE_HEIGHT, PITCH_LIMIT, WALK_SPEED, Walker, applyLook, walkDelta } from '../src/core/walk';

describe('walkDelta', () => {
  it('walks toward -Z when yaw is 0', () => {
    const d = walkDelta(0, 1, 0, 1);
    expect(d[0]).toBeCloseTo(0);
    expect(d[2]).toBeCloseTo(-WALK_SPEED);
  });

  it('strafes toward +X when yaw is 0', () => {
    const d = walkDelta(0, 0, 1, 1);
    expect(d[0]).toBeCloseTo(WALK_SPEED);
    expect(d[2]).toBeCloseTo(0);
  });

  it('normalises diagonal movement', () => {
    const d = walkDelta(0, 1, 1, 1);
    expect(Math.hypot(d[0], d[2])).toBeCloseTo(WALK_SPEED);
  });

  it('turns with yaw', () => {
    // yaw = +90° は左を向く。前進は -X
    const d = walkDelta(Math.PI / 2, 1, 0, 1);
    expect(d[0]).toBeCloseTo(-WALK_SPEED);
    expect(d[2]).toBeCloseTo(0);
  });

  it('stays still without input', () => {
    expect(walkDelta(0, 0, 0, 1)).toEqual([0, 0, 0]);
  });

  it('scales with dt and speed', () => {
    const d = walkDelta(0, 1, 0, 0.5, 4);
    expect(d[2]).toBeCloseTo(-2);
  });

  it('walks backward and strafes left', () => {
    const back = walkDelta(0, -1, 0, 1);
    expect(back[2]).toBeCloseTo(WALK_SPEED);
    const left = walkDelta(0, 0, -1, 1);
    expect(left[0]).toBeCloseTo(-WALK_SPEED);
  });
});

describe('applyLook', () => {
  it('turns right when the mouse moves right', () => {
    const s = applyLook({ feet: [0, 0, 0], yaw: 0, pitch: 0 }, 100, 0);
    expect(s.yaw).toBeLessThan(0);
  });

  it('turns by the mouse delta times the sensitivity', () => {
    const s = applyLook({ feet: [0, 0, 0], yaw: 0, pitch: 0 }, 100, 0, 0.01);
    expect(s.yaw).toBeCloseTo(-1);
  });

  it('clamps pitch', () => {
    const up = applyLook({ feet: [0, 0, 0], yaw: 0, pitch: 0 }, 0, -100000);
    expect(up.pitch).toBeCloseTo(PITCH_LIMIT);
    const down = applyLook({ feet: [0, 0, 0], yaw: 0, pitch: 0 }, 0, 100000);
    expect(down.pitch).toBeCloseTo(-PITCH_LIMIT);
  });
});

/** W だけ押されている入力の代わり */
const holdingW = { consumeMouse: () => ({ x: 0, y: 0 }), down: (code: string) => code === 'KeyW' } as unknown as Input;

describe('Walker', () => {
  it('walks when canMove is true', () => {
    const w = new Walker({ position: [0, 0, 0], yaw: 0 }, []);
    w.update(holdingW, 0.5);
    expect(w.state.feet[2]).toBeCloseTo(-WALK_SPEED * 0.5);
  });

  it('stays put when canMove is false but still looks around', () => {
    const w = new Walker({ position: [0, 0, 0], yaw: 0 }, []);
    w.canMove = false;
    const turning = { consumeMouse: () => ({ x: 100, y: 0 }), down: (code: string) => code === 'KeyW' } as unknown as Input;
    w.update(turning, 0.5);
    expect(w.state.feet).toEqual([0, 0, 0]);
    expect(w.state.yaw).toBeLessThan(0);
  });

  it('places the camera at its own eye height', () => {
    const w = new Walker({ position: [1, 0, 2], yaw: 0 }, []);
    const camera = new PerspectiveCamera();
    w.applyTo(camera);
    expect(camera.position.y).toBeCloseTo(EYE_HEIGHT);
    w.eyeHeight = 1.1;
    w.applyTo(camera);
    expect(camera.position.y).toBeCloseTo(1.1);
    expect(camera.position.x).toBeCloseTo(1);
    expect(camera.position.z).toBeCloseTo(2);
  });
});
