import { expect, it } from 'vitest';
import { readingRange, CHANNEL_PEAK_RANGE, ZONE_LEVEL_RANGE } from './displayScales';

it('reveals the benchmark variation without forcing zero or power limits into the view', () => {
  expect(readingRange([5.796388, 6.078733], CHANNEL_PEAK_RANGE)).toEqual([5.7, 6.2]);
  expect(readingRange([46.959, 51.131], ZONE_LEVEL_RANGE)).toEqual([45, 55]);
  const [low, high] = readingRange([1.000001, 1.000005], undefined, false, .00002);
  expect(high - low).toBeLessThan(.0001);
  expect(low).toBeLessThan(1.000001); expect(high).toBeGreaterThan(1.000005);
});

it('expands for outliers and keeps empty, constant and zero-based views finite', () => {
  const [low, high] = readingRange([5.1, 8], CHANNEL_PEAK_RANGE);
  expect(low).toBeLessThan(5.1); expect(high).toBeGreaterThan(8);
  expect(readingRange([], ZONE_LEVEL_RANGE)).toEqual([45, 55]);
  expect(readingRange([NaN, Infinity])).toEqual([0, 1]);
  const constant = readingRange([500, 500]);
  expect(constant[0]).toBeLessThan(500); expect(constant[1]).toBeGreaterThan(500);
  expect(readingRange([10, 20], undefined, true)[0]).toBe(0);
});
