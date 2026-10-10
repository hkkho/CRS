import { describe, expect, it } from "vitest";
import {
  formatEffectiveK,
  formatLiquidZoneRegion,
  getTiltLabel,
  getOverallStatus,
} from "./visuals";
import type { CanduSnapshot } from "./protocol";

describe("Studio display helpers", () => {
  it("formats physical readings", () => {
    expect(getTiltLabel(-0.000000001)).toBe("+0.00%");
    expect(getTiltLabel(-0.0513)).toBe("-5.13%");
    expect(formatEffectiveK(1.0023456)).toBe("1.002346");
    expect(formatLiquidZoneRegion(0)).toBe("Z1 · End A lower left");
    expect(formatLiquidZoneRegion(4)).toBe("Z5 · End A upper centre");
    expect(formatLiquidZoneRegion(13)).toBe("Z14 · End B upper right");
  });

  it("uses the same operating-envelope thresholds as the command deck", () => {
    const snapshot = {
      physics: { actualPowerFraction: 1, totalPowerWatts: 1 },
      targetPowerFraction: 1,
      axialTiltFraction: 0,
      rrs: { averageFillFraction: 0.5 },
    } as CanduSnapshot;

    expect(getOverallStatus(snapshot)).toBe("stable");
    expect(getOverallStatus({ ...snapshot, rrs: { ...snapshot.rrs, averageFillFraction: 0.15 } })).toBe("attention");
    expect(getOverallStatus({ ...snapshot, axialTiltFraction: 0.06 })).toBe("watch");
  });
});
