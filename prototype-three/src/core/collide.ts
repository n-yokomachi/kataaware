import type { BoxDef, Vec3 } from '../data/types';

export interface AABB {
  min: Vec3;
  max: Vec3;
}

export const PLAYER_RADIUS = 0.3;
export const PLAYER_HEIGHT = 1.7;

export function boxFromDef(d: BoxDef): AABB {
  const [x, y, z] = d.position;
  const [w, h, l] = d.size;
  return { min: [x - w / 2, y - h / 2, z - l / 2], max: [x + w / 2, y + h / 2, z + l / 2] };
}

export function intersects(a: AABB, b: AABB): boolean {
  return (
    a.min[0] < b.max[0] && a.max[0] > b.min[0] &&
    a.min[1] < b.max[1] && a.max[1] > b.min[1] &&
    a.min[2] < b.max[2] && a.max[2] > b.min[2]
  );
}

export function playerBox(feet: Vec3, radius = PLAYER_RADIUS, height = PLAYER_HEIGHT): AABB {
  return {
    min: [feet[0] - radius, feet[1], feet[2] - radius],
    max: [feet[0] + radius, feet[1] + height, feet[2] + radius],
  };
}

function blocked(feet: Vec3, boxes: readonly AABB[]): boolean {
  const p = playerBox(feet);
  return boxes.some((b) => intersects(p, b));
}

/** X と Z を別々に試し、ぶつかった軸だけ止める */
export function moveWithSlide(feet: Vec3, delta: Vec3, boxes: readonly AABB[]): Vec3 {
  let x = feet[0];
  let z = feet[2];
  const y = feet[1];
  const tryX = x + delta[0];
  if (!blocked([tryX, y, z], boxes)) x = tryX;
  const tryZ = z + delta[2];
  if (!blocked([x, y, tryZ], boxes)) z = tryZ;
  return [x, y, z];
}
