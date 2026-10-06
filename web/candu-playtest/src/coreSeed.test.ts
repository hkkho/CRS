import { afterEach, expect, it, vi } from 'vitest';
import { randomCoreSeed } from './coreSeed';

afterEach(() => vi.restoreAllMocks());

it('uses cryptographic uint32 draws and redraws the current seed', () => {
  const draws = [42, 4294967295, 0];
  const entropy = vi.spyOn(globalThis.crypto, 'getRandomValues').mockImplementation(array => {
    (array as Uint32Array)[0] = draws.shift()!;
    return array;
  });
  expect(randomCoreSeed(42)).toBe(4294967295);
  expect(randomCoreSeed()).toBe(0);
  expect(entropy).toHaveBeenCalledTimes(3);
});
