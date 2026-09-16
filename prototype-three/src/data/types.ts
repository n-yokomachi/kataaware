export type Vec3 = [number, number, number];

export interface BoxDef {
  position: Vec3; // 中心
  size: Vec3; // 全幅
  color?: number;
  emissive?: number;
  collider?: boolean; // 省略時 true
}

/** 舞台に置く glTF 1 つ分。asset は public/assets/ 以下の相対パス */
export interface LayoutItem {
  asset: string;
  position: Vec3;
  rotationY?: number; // ラジアン。省略時 0
  scale?: number; // 省略時 1
  collider?: boolean; // 省略時 true
  tint?: number; // 材質の色を上書きする
}

/** 舞台の定義。仮の箱、配置データ、three.js editor の書き出しを混在できる */
export interface EnvironmentDef {
  boxes?: BoxDef[];
  layout?: LayoutItem[];
  url?: string;
}

export interface Interactable {
  id: string;
  position: Vec3;
  radius?: number; // 省略時 2
  required?: boolean;
  once?: boolean; // 省略時 true
  after?: string[]; // ここに挙げた id が済むまで選べない。ただし hints に文がある id については選べて、その文だけ出る
  hints?: Record<string, string[]>; // after の id ごとに、未達のときに調べると出す文。出しても済んだことにはならない
  label?: string;
  lines: string[];
}

export interface Daze {
  blur: number;
  wobble: number;
  duration: number; // 秒。値が 0 まで減る時間
  until?: string; // この id の対象を調べるまで最大のまま保ち、調べた後に duration で消す
}

export type Transition = 'cut' | 'fade';

/** 歩いて調べる場面。受け身の記憶シーンなど他の種類は、その場面を追加する段で足す */
export interface WalkScene {
  id: string;
  kind: 'walk';
  next: string | null;
  transition: Transition; // この場面に入るときの転換
  tone: { color: number; amount: number };
  sky?: number;
  fog?: { color: number; near: number; far: number };
  environment: EnvironmentDef;
  dazeOnEnter?: Daze;
  onEnterLines?: string[];
  spawn: { position: Vec3; yaw: number };
  colliders?: BoxDef[];
  interactables: Interactable[];
  /** 座った状態で始める。standAfter の対象を調べると立ち上がり、移動できるようになる */
  seat?: { eyeHeight: number; standAfter: string };
}

export type SceneDef = WalkScene;
