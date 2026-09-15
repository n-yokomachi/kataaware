import { roomIntroHooks } from './hooks/room-intro';
import type { Hooks } from './runtime';

export const hooks: Readonly<Record<string, Hooks>> = {
  'room-intro': roomIntroHooks(),
};
