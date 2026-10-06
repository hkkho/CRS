import { expect, it } from 'vitest';
import { rippleColor } from './powerReadings';

it('uses a fixed clipped 85%-115% ripple scale around a neutral 100%', () => {
  expect(rippleColor(1)).toBe('hsl(0 0% 90%)');
  expect(rippleColor(.85)).toBe('hsl(210 75% 35%)');
  expect(rippleColor(1.15)).toBe('hsl(0 80% 50%)');
  expect(rippleColor(.6)).toBe(rippleColor(.85));
  expect(rippleColor(1.5)).toBe(rippleColor(1.15));
  expect(rippleColor(.925)).not.toBe(rippleColor(.85));
  expect(rippleColor(1.075)).not.toBe(rippleColor(1.15));
});
