import type { BoxDef, Interactable, Vec3 } from '../types';

/** 6m 四方の自室。-Z 側の壁に机と端末、+Z 側の壁にドア */
export const ROOM_BOXES: BoxDef[] = [
  { position: [0, 1.5, -3], size: [6, 3, 0.2], color: 0x4a4a55 },
  { position: [0, 1.5, 3], size: [6, 3, 0.2], color: 0x4a4a55 },
  { position: [-3, 1.5, 0], size: [0.2, 3, 6], color: 0x4a4a55 },
  { position: [3, 1.5, 0], size: [0.2, 3, 6], color: 0x4a4a55 },
  { position: [0, 3.1, 0], size: [6, 0.2, 6], color: 0x2a2a30, collider: false }, // 天井
  { position: [0.8, 1.1, 2.88], size: [0.9, 2.2, 0.06], color: 0x6a4a3a, collider: false }, // ドア
  { position: [1.5, 0.4, -2.2], size: [1.6, 0.8, 0.8], color: 0x7a5a40 }, // 机
  { position: [1.5, 1.05, -2.5], size: [0.7, 0.45, 0.08], color: 0x111118 }, // 端末の画面
  { position: [-1.8, 0.4, -2.2], size: [0.9, 0.8, 0.5], color: 0x3a3a4a }, // メモリハブ
  { position: [-1.8, 0.35, 1.5], size: [1.2, 0.7, 0.8], color: 0x5a4a3a }, // 卓
];

export const ROOM_SPAWN = { position: [0, 0, 0.5] as Vec3, yaw: 0 };

export const TERMINAL_POS: Vec3 = [1.5, 1.05, -2.4];
export const HUB_POS: Vec3 = [-1.8, 0.9, -2.2];
export const DOOR_POS: Vec3 = [0.8, 1.2, 2.8];

export const ROOM_TONE = { color: 0xc8d0ff, amount: 0.25 };
export const ROOM_SKY = 0x0b0b12;

export const ROOM_OPTIONAL: Interactable[] = [
  { id: 'ashtray', position: [-1.8, 0.85, 1.5], label: '灰皿', lines: ['（仮）灰皿の煙草は、まだ煙を上げていた。'] },
  { id: 'cigarettes', position: [-1.2, 0.85, 1.6], label: '煙草の箱', lines: ['（仮）空だ。切らしていたのを思い出す。'] },
  { id: 'clipboard', position: [2.2, 0.9, -2.0], label: '紙ばさみ', lines: ['（仮）売り上げのメモ。数字と日付だけが並んでいる。'] },
];
