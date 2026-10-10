import { expect, it } from 'vitest';
import { powerColor, rippleColor } from './powerReadings';

it('spreads ordinary powers across the color scale and clips below the floor and above the limit', () => {
  expect(powerColor(4_500_000, 4_500_000, 7_300_000)).toBe('hsl(220 78% 55%)');
  expect(powerColor(0, 4_500_000, 7_300_000)).toBe(powerColor(4_500_000, 4_500_000, 7_300_000));
  expect(powerColor(5_900_000, 4_500_000, 7_300_000)).toBe('hsl(110 78% 55%)');
  expect(powerColor(7_300_000, 4_500_000, 7_300_000)).toBe('hsl(0 78% 55%)');
  expect(powerColor(8_000_000, 4_500_000, 7_300_000)).toBe(powerColor(7_300_000, 4_500_000, 7_300_000));
});

it('uses a fixed clipped 85%-115% ripple scale around a neutral 100%', () => {
  expect(rippleColor(1)).toBe('hsl(0 0% 90%)');
  expect(rippleColor(.85)).toBe('hsl(210 75% 35%)');
  expect(rippleColor(1.15)).toBe('hsl(0 80% 50%)');
  expect(rippleColor(.6)).toBe(rippleColor(.85));
  expect(rippleColor(1.5)).toBe(rippleColor(1.15));
  expect(rippleColor(.925)).not.toBe(rippleColor(.85));
  expect(rippleColor(1.075)).not.toBe(rippleColor(1.15));
});
