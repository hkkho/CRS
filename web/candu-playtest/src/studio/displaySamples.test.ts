import { expect, it } from 'vitest';
import { displaySamples } from './displaySamples';
it('retains endpoints, narrow extrema, missing gaps and equal-time discontinuities', () => {
  const points = Array.from({ length: 4096 }, (_, i) => ({ time: i, value: 1 as number | null }));
  points[1001].value = 100; points[2001].value = -100;
  points[2501].value = null; points[3001].time = points[3000].time; points[3001].value = 4;
  const result = displaySamples(points, p => p.value, p => p.time, false);
  expect(result.length).toBeLessThan(900);
  for (const index of [0, 4095, 1001, 2001, 2500, 2501, 2502, 3000, 3001]) expect(result).toContain(points[index]);
  expect(points).toHaveLength(4096);
});
it('keeps both sides of every step change, including same-time changes', () => {
  const points = Array.from({ length: 4096 }, (_, i) => ({ time: i, value: i < 1234 ? 10 : i < 2345 ? 20 : 5 }));
  const result = displaySamples(points, p => p.value, p => p.time, true);
  for (const index of [1233, 1234, 2344, 2345]) expect(result).toContain(points[index]);
});
