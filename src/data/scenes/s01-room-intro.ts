import type { WalkScene } from '../types';
import { DOOR_POS, HUB_POS, ROOM_BOXES, ROOM_OPTIONAL, ROOM_SKY, ROOM_SPAWN, ROOM_TONE, TERMINAL_POS } from './room';

export const roomIntro: WalkScene = {
  id: 'room-intro',
  kind: 'walk',
  next: null,
  transition: 'fade',
  tone: ROOM_TONE,
  sky: ROOM_SKY,
  environment: { boxes: ROOM_BOXES },
  spawn: ROOM_SPAWN,
  dazeOnEnter: { blur: 1, wobble: 1, duration: 8 },
  interactables: [
    { id: 'chips', position: HUB_POS, required: true, label: 'チップを抜く', lines: ['（仮）六枚のチップを抜き取った。'] },
    {
      id: 'terminal',
      position: TERMINAL_POS,
      required: true,
      after: ['chips'],
      label: '端末',
      lines: ['（仮）電源の落ちた黒い画面に、自分の顔が映っている。', '（仮）起動すると、顔は消えた。'],
    },
    {
      id: 'door',
      position: DOOR_POS,
      required: true,
      after: ['terminal'],
      label: 'ドア',
      lines: ['（仮）チップをポケットに入れて、家を出る。'],
    },
    ...ROOM_OPTIONAL,
  ],
};
