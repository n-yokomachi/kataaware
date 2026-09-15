import type { Interactable, Vec3 } from '../data/types';

export const DEFAULT_RADIUS = 2;
export const MAX_ANGLE = 0.7; // ラジアン。視線からこの角度以内の対象だけ選ぶ

/** after のうち、まだ済んでいない最初の id。すべて済んでいれば null */
export function unmetPrerequisite(it: Interactable, done: ReadonlySet<string>): string | null {
  for (const id of it.after ?? []) {
    if (!done.has(id)) return id;
  }
  return null;
}

/**
 * forward は正規化済みの視線方向。
 * 距離が radius 以内、視線からの角度が maxAngle 以内、前提が済んでいる対象のうち最も近いものを返す。
 * 前提が未達でも、その id の hints があれば選べる。
 */
export function selectInteractable(
  camPos: Vec3,
  forward: Vec3,
  items: readonly Interactable[],
  done: ReadonlySet<string>,
  maxAngle = MAX_ANGLE,
): Interactable | null {
  let best: Interactable | null = null;
  let bestDist = Infinity;
  for (const it of items) {
    if ((it.once ?? true) && done.has(it.id)) continue;
    const unmet = unmetPrerequisite(it, done);
    if (unmet !== null && !it.hints?.[unmet]?.length) continue;
    const dx = it.position[0] - camPos[0];
    const dy = it.position[1] - camPos[1];
    const dz = it.position[2] - camPos[2];
    const dist = Math.hypot(dx, dy, dz);
    if (dist > (it.radius ?? DEFAULT_RADIUS)) continue;
    if (dist > 1e-6) {
      const cos = (dx * forward[0] + dy * forward[1] + dz * forward[2]) / dist;
      if (Math.acos(Math.min(1, Math.max(-1, cos))) > maxAngle) continue;
    }
    if (dist < bestDist) {
      best = it;
      bestDist = dist;
    }
  }
  return best;
}
