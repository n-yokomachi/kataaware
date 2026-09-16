import { describe, expect, it } from 'vitest';
import { Ps1Pass } from '../src/fx/ps1-pass';

describe('Ps1Pass', () => {
  it('stores the numbers and switches itself off at amount 0', () => {
    const pass = new Ps1Pass();
    pass.set(16, 0.5, 1);
    expect(pass.uniforms.levels.value).toBe(16);
    expect(pass.uniforms.dither.value).toBe(0.5);
    expect(pass.uniforms.amount.value).toBe(1);
    expect(pass.enabled).toBe(true);
    pass.set(16, 0.5, 0);
    expect(pass.enabled).toBe(false);
  });
});
