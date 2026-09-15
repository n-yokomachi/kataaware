export interface SubtitleState {
  lines: readonly string[];
  index: number;
}

export const emptySubtitles: Readonly<SubtitleState> = { lines: [], index: 0 };

export function enqueue(s: SubtitleState, lines: readonly string[]): SubtitleState {
  if (lines.length === 0) return s;
  return { lines: [...s.lines, ...lines], index: s.index };
}

export function advance(s: SubtitleState): SubtitleState {
  if (s.index + 1 >= s.lines.length) return emptySubtitles;
  return { lines: s.lines, index: s.index + 1 };
}

export function current(s: SubtitleState): string | null {
  return s.index < s.lines.length ? s.lines[s.index] : null;
}
