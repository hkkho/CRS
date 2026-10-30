import { expect, it } from "vitest";
import { demoPowerAlert, demoPowerColor, interpolateDemo, zoneAlert } from "./demoPresentation";
import { createSnapshot } from "./testSnapshot";
import { channelWatts } from "./studio/powerReadings";

it("uses fixed 5000–6500 kW colours and strict flashing/shaking thresholds", () => {
  expect(demoPowerColor(0)).toBe(demoPowerColor(5_000_000));
  expect(demoPowerColor(8_000_000)).toBe(demoPowerColor(6_500_000));
  expect(new Set([5000, 5300, 5600, 5900, 6200, 6500].map(k => demoPowerColor(k * 1000))).size).toBe(6);
  expect(demoPowerAlert(6_500_000)).toBe("normal");
  expect(demoPowerAlert(6_500_001)).toBe("flash");
  expect(demoPowerAlert(7_000_000)).toBe("flash");
  expect(demoPowerAlert(7_000_001)).toBe("shake");
  expect([.05, .2, .5, .8, .95].map(zoneAlert)).toEqual(["shake", "flash", "normal", "flash", "shake"]);
});

it("blends published power ratios without mutating endpoints or committing discrete outcomes", () => {
  const from = createSnapshot();
  const to = { ...from, completedDays: 1, runStatus: "ended" as const,
    ripple: from.ripple && { ...from.ripple, channelRippleFractions: from.ripple.channelRippleFractions.map(r => r * 1.1) },
    core: { ...from.core, channels: from.core.channels.map(c => ({ ...c, powerWatts: c.powerWatts * 1.1 })) },
  };
  const original = JSON.stringify(from), halfway = interpolateDemo(from, to, .5);
  expect(channelWatts(halfway, halfway.core.channels[0])).toBeCloseTo(channelWatts(from, from.core.channels[0]) * 1.05);
  expect(halfway.completedDays).toBe(from.completedDays); expect(halfway.runStatus).toBe(from.runStatus);
  expect(JSON.stringify(from)).toBe(original);
  expect(interpolateDemo(from, to, 1)).toBe(to);
});
