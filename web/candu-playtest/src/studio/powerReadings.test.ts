import { expect, it } from "vitest";
import { bandColors, burnupColor, powerBand, powerColor, rippleColor } from "./powerReadings";
it("uses discrete power bands at the floor, 90% and 97% boundaries", () => {
  expect(powerColor(4_500_000, 4_500_000, 7_100_000)).toBe(bandColors.low);
  expect(powerColor(4_500_001, 4_500_000, 7_100_000)).toBe(bandColors.moderate);
  expect(powerColor(6_389_999, 4_500_000, 7_100_000)).toBe(bandColors.normal);
  expect(powerColor(6_390_000, 4_500_000, 7_100_000)).toBe(bandColors.high);
  expect(powerBand(6_887_000, 4_500_000, 7_100_000)).toBe("extreme");
  expect(powerBand(6_886_999, 4_500_000, 7_100_000)).toBe("high");
  expect(powerBand(8_000_000, 4_500_000, 7_100_000)).toBe("extreme");
  expect(powerBand(873_000, 400_000, 900_000)).toBe("extreme");
  expect(powerBand(NaN, 400_000, 900_000)).toBe("unavailable");
});
it("uses a neutral ripple band and discrete burnup bands", () => {
  expect(rippleColor(.89)).toBe(bandColors.low);
  expect(rippleColor(.9)).not.toBe(bandColors.low);
  expect(rippleColor(.95)).toBe(bandColors.normal);
  expect(rippleColor(1.05)).toBe(bandColors.normal);
  expect(rippleColor(1.051)).toBe(bandColors.high);
  expect(rippleColor(1.15)).toBe(bandColors.extreme);
  expect(new Set([0, 2, 4, 6, 8].map(burnupColor)).size).toBe(5);
  expect(burnupColor(5.99)).toBe(burnupColor(4));
});
