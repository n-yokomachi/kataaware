import type { BoxDef, Interactable, Vec3 } from '../types';

/** 6m 四方の自室。-Z 側の壁に机と端末、机の前に椅子、椅子の右に小さな卓、+Z 側の壁にドア */
export const ROOM_BOXES: BoxDef[] = [
  { position: [0, 1.5, -3], size: [6, 3, 0.2], color: 0x4a4a55 },
  { position: [0, 1.5, 3], size: [6, 3, 0.2], color: 0x4a4a55 },
  { position: [-3, 1.5, 0], size: [0.2, 3, 6], color: 0x4a4a55 },
  { position: [3, 1.5, 0], size: [0.2, 3, 6], color: 0x4a4a55 },
  { position: [0, 3.1, 0], size: [6, 0.2, 6], color: 0x2a2a30, collider: false }, // 天井
  { position: [0.8, 1.1, 2.88], size: [0.9, 2.2, 0.06], color: 0x6a4a3a, collider: false }, // ドア
  { position: [1.5, 0.4, -2.2], size: [1.6, 0.8, 0.8], color: 0x7a5a40 }, // 机
  { position: [1.5, 1.05, -2.5], size: [0.7, 0.45, 0.08], color: 0x111118 }, // 端末の画面
  { position: [1.5, 0.25, -1.2], size: [0.5, 0.5, 0.5], color: 0x3a3040, collider: false }, // 椅子（座る位置）
  { position: [2.5, 0.35, -1.0], size: [0.6, 0.7, 0.6], color: 0x5a4a3a }, // 卓。灰皿と煙草
  { position: [-1.8, 0.4, -2.2], size: [0.9, 0.8, 0.5], color: 0x3a3a4a }, // メモリハブ
];

/** 端末の前の椅子に座った状態で始める */
export const ROOM_SPAWN = { position: [1.5, 0, -1.2] as Vec3, yaw: 0 };
export const SEAT_EYE_HEIGHT = 1.1;

export const JACK_POS: Vec3 = [1.75, 0.75, -1.5];
export const CIGARETTE_POS: Vec3 = [2.5, 0.85, -1.0];
export const CIGARETTE_BOX_POS: Vec3 = [2.4, 0.85, -0.85];
export const ASHTRAY_POS: Vec3 = [2.65, 0.85, -1.2];
export const HUB_POS: Vec3 = [-1.8, 0.9, -2.2];
export const TERMINAL_POS: Vec3 = [1.5, 1.05, -2.4];
export const CLIPBOARD_POS: Vec3 = [2.2, 0.9, -2.0];
export const DOOR_POS: Vec3 = [0.8, 1.2, 2.8];

export const ROOM_TONE = { color: 0xc8d0ff, amount: 0.25 };
export const ROOM_SKY = 0x0b0b12;

export const ROOM_OPTIONAL: Interactable[] = [
  { id: 'ashtray', position: ASHTRAY_POS, label: '灰皿', lines: ['吸い殻がたまっている'] },
  {
    id: 'cigarette-box',
    position: CIGARETTE_BOX_POS,
    after: ['cigarette'],
    label: '煙草の箱',
    lines: ['『双鶴（シュアンフー）』という名前の中国産煙草の箱。今はカラだ'],
  },
  {
    id: 'clipboard',
    position: CLIPBOARD_POS,
    label: '紙ばさみ',
    lines: ['売り上げのメモだ', '2166/08/13 5枚　2166/08/14 4枚　2166/08/15 ―'],
  },
];
