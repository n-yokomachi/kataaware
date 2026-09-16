import { describe, expect, it } from 'vitest';
import { isComplete, requiredIds } from '../src/core/progress';

describe('progress', () => {
  it('collects required ids only', () => {
    expect(requiredIds([{ id: 'a', required: true }, { id: 'b' }, { id: 'c', required: true }])).toEqual(['a', 'c']);
  });

  it('is complete when every required id is done', () => {
    expect(isComplete(['a', 'c'], new Set(['a']))).toBe(false);
    expect(isComplete(['a', 'c'], new Set(['a', 'c', 'b']))).toBe(true);
    expect(isComplete([], new Set())).toBe(true);
  });
});
