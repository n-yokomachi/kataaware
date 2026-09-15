import type { SceneDef } from '../types';
import { roomIntro } from './s01-room-intro';

export const SCENES: readonly SceneDef[] = [roomIntro];

export const FIRST_SCENE = SCENES[0].id;

export const sceneMap: ReadonlyMap<string, SceneDef> = new Map(SCENES.map((s) => [s.id, s]));
