import { expect, it } from 'vitest';
import { ObservedPace } from './observedPace';
it('includes slow solving time and measures published progression without requested-rate assumptions', () => {
  const pace = new ObservedPace();
  expect(pace.observe('1x', 100, 0)).toBeNull();
  expect(pace.observe('1x', 280, 2000)).toBe(1.5);
  expect(pace.observe('1x', 280, 4000)).toBe(.75);
});
it('resets on pause, speed, visibility/lifecycle change and backwards clocks or seeded reset', () => {
  const pace = new ObservedPace();
  pace.observe('1x', 0, 0); expect(pace.observe('1x', 60, 1000)).toBe(1);
  expect(pace.observe(null, 60, 2000)).toBeNull();
  expect(pace.observe('1x', 60, 30000)).toBeNull();
  expect(pace.observe('10x', 60, 31000)).toBeNull();
  expect(pace.observe('60x', 60, 31000)).toBeNull();
  expect(pace.observe('10x', 0, 32000)).toBeNull();
  expect(pace.observe('10x', 0, 10)).toBeNull();
});
it('bounds retained measurement work during long runs', () => {
  const pace = new ObservedPace();
  for (let i = 0; i < 10000; i++) pace.observe('1x', i * 60, i * 1000);
  expect(pace.observe('1x', 600000, 10000000)).toBe(1);
});
