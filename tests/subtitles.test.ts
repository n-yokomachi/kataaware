import { describe, expect, it } from 'vitest';
import { advance, current, emptySubtitles, enqueue } from '../src/ui/subtitles';

describe('subtitles', () => {
  it('shows the first queued line', () => {
    const s = enqueue(emptySubtitles, ['一行目', '二行目']);
    expect(current(s)).toBe('一行目');
  });

  it('advances line by line and empties at the end', () => {
    let s = enqueue(emptySubtitles, ['一行目', '二行目']);
    s = advance(s);
    expect(current(s)).toBe('二行目');
    s = advance(s);
    expect(current(s)).toBeNull();
    expect(s).toEqual(emptySubtitles);
  });

  it('appends lines behind the ones still waiting', () => {
    let s = enqueue(emptySubtitles, ['a']);
    s = enqueue(s, ['b']);
    s = advance(s);
    expect(current(s)).toBe('b');
  });

  it('ignores an empty enqueue', () => {
    expect(enqueue(emptySubtitles, [])).toBe(emptySubtitles);
  });
});
