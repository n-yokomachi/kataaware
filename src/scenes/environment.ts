import {
  Box3,
  BoxGeometry,
  Color,
  DirectionalLight,
  Fog,
  Group,
  HemisphereLight,
  Mesh,
  MeshStandardMaterial,
  Object3D,
  ObjectLoader,
  PlaneGeometry,
  Scene,
} from 'three';
import { boxFromDef, type AABB } from '../core/collide';
import type { BoxDef, Interactable, SceneDef } from '../data/types';

export interface BuiltEnvironment {
  group: Group;
  colliders: AABB[];
  dispose(): void;
}

const FLOOR_SIZE = 400;
/** 外部ファイルの中で、この接頭辞の名前を持つメッシュは当たり判定に含めない */
const NO_COLLIDE_PREFIX = 'nc_';

function boxMesh(b: BoxDef): Mesh {
  const mat = new MeshStandardMaterial({ color: b.color ?? 0x8a8a92, roughness: 0.9 });
  if (b.emissive !== undefined) {
    mat.emissive = new Color(b.emissive);
    mat.emissiveIntensity = 1;
  }
  const mesh = new Mesh(new BoxGeometry(b.size[0], b.size[1], b.size[2]), mat);
  mesh.position.set(b.position[0], b.position[1], b.position[2]);
  return mesh;
}

/** 仮の箱の段では、調べる対象の位置に光る小さな箱を置いて見つけやすくする */
function markerMesh(it: Interactable): Mesh {
  const mesh = new Mesh(
    new BoxGeometry(0.25, 0.25, 0.25),
    new MeshStandardMaterial({ color: 0xffdd55, emissive: 0xff9900, emissiveIntensity: 0.7 }),
  );
  mesh.position.set(it.position[0], it.position[1], it.position[2]);
  mesh.name = `marker:${it.id}`;
  return mesh;
}

function addLights(group: Group): void {
  group.add(new HemisphereLight(0xffffff, 0x404040, 1.2));
  const sun = new DirectionalLight(0xffffff, 1.0);
  sun.position.set(5, 10, 3);
  group.add(sun);
}

function addFloor(group: Group): void {
  const floor = new Mesh(
    new PlaneGeometry(FLOOR_SIZE, FLOOR_SIZE),
    new MeshStandardMaterial({ color: 0x2e2e34, roughness: 1 }),
  );
  floor.rotation.x = -Math.PI / 2;
  group.add(floor);
}

/** three.js editor が書き出した JSON を読み、メッシュの外接箱を当たり判定にする */
async function loadFromUrl(url: string, group: Group, colliders: AABB[]): Promise<void> {
  const root = (await new ObjectLoader().loadAsync(url)) as Object3D;
  group.add(root);
  root.updateMatrixWorld(true);
  root.traverse((o) => {
    if (!(o instanceof Mesh) || o.name.startsWith(NO_COLLIDE_PREFIX)) return;
    const b = new Box3().setFromObject(o);
    colliders.push({ min: [b.min.x, b.min.y, b.min.z], max: [b.max.x, b.max.y, b.max.z] });
  });
}

export async function buildEnvironment(def: SceneDef): Promise<BuiltEnvironment> {
  const group = new Group();
  const colliders: AABB[] = [];
  addFloor(group);
  addLights(group);
  const env = def.environment;
  for (const b of env.boxes ?? []) {
    group.add(boxMesh(b));
    if (b.collider !== false) colliders.push(boxFromDef(b));
  }
  if (env.url) await loadFromUrl(env.url, group, colliders);
  for (const c of def.colliders ?? []) colliders.push(boxFromDef(c));
  for (const it of def.interactables) group.add(markerMesh(it));
  return {
    group,
    colliders,
    dispose() {
      group.traverse((o) => {
        if (!(o instanceof Mesh)) return;
        o.geometry.dispose();
        const m = o.material;
        if (Array.isArray(m)) m.forEach((x) => x.dispose());
        else m.dispose();
      });
    },
  };
}

export function applyAtmosphere(scene: Scene, def: SceneDef): void {
  scene.background = new Color(def.sky ?? 0x101014);
  scene.fog = def.fog ? new Fog(def.fog.color, def.fog.near, def.fog.far) : null;
}
