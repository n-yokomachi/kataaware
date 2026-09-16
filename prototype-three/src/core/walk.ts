import type { PerspectiveCamera } from 'three';
import type { Vec3 } from '../data/types';
import { moveWithSlide, type AABB } from './collide';
import type { Input } from './input';

export const LOOK_SENS = 0.0022; // ラジアン / ピクセル
export const WALK_SPEED = 2.6; // m/s
export const EYE_HEIGHT = 1.6;
export const PITCH_LIMIT = 1.4;

export interface WalkState {
  feet: Vec3;
  yaw: number;
  pitch: number;
}

export function applyLook(s: WalkState, dx: number, dy: number, sens = LOOK_SENS): WalkState {
  const pitch = Math.min(PITCH_LIMIT, Math.max(-PITCH_LIMIT, s.pitch - dy * sens));
  return { feet: s.feet, yaw: s.yaw - dx * sens, pitch };
}

/** yaw 0 は -Z 向き。fwd は前(+)後(-)、strafe は右(+)左(-)。斜めは正規化する */
export function walkDelta(yaw: number, fwd: number, strafe: number, dt: number, speed = WALK_SPEED): Vec3 {
  const len = Math.hypot(fwd, strafe);
  if (len === 0) return [0, 0, 0];
  const f = fwd / len;
  const r = strafe / len;
  const x = (-Math.sin(yaw) * f + Math.cos(yaw) * r) * speed * dt;
  const z = (-Math.cos(yaw) * f - Math.sin(yaw) * r) * speed * dt;
  return [x, 0, z];
}

export class Walker {
  state: WalkState;
  /** false の間は見回しだけできる（座っている、演出中など） */
  canMove = true;
  /** 足元からカメラまでの高さ。座位と立位で変える */
  eyeHeight = EYE_HEIGHT;

  constructor(
    spawn: { position: Vec3; yaw: number },
    private boxes: readonly AABB[],
  ) {
    this.state = { feet: [spawn.position[0], spawn.position[1], spawn.position[2]], yaw: spawn.yaw, pitch: 0 };
  }

  update(input: Input, dt: number): void {
    const m = input.consumeMouse();
    this.state = applyLook(this.state, m.x, m.y);
    if (!this.canMove) return;
    const fwd = (input.down('KeyW') ? 1 : 0) - (input.down('KeyS') ? 1 : 0);
    const strafe = (input.down('KeyD') ? 1 : 0) - (input.down('KeyA') ? 1 : 0);
    if (fwd === 0 && strafe === 0) return;
    this.state = {
      ...this.state,
      feet: moveWithSlide(this.state.feet, walkDelta(this.state.yaw, fwd, strafe, dt), this.boxes),
    };
  }

  applyTo(camera: PerspectiveCamera): void {
    const [x, y, z] = this.state.feet;
    camera.position.set(x, y + this.eyeHeight, z);
    camera.rotation.order = 'YXZ';
    camera.rotation.set(this.state.pitch, this.state.yaw, 0);
  }
}
