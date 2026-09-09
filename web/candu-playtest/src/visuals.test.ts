import { describe, expect, it } from "vitest";
import {
  formatPowerWatts,
  formatReactivity,
  getFlowArrow,
  getFlowDirectionLabel,
  getHeatColor,
  getOverallStatus,
} from "./visuals";
import type { CanduSnapshot } from "./protocol";

describe("tactical playtest display helpers", () => {
  it("keeps the heat scale ordered from cool navy to hot red", () => {
    expect(getHeatColor(0.68)).toBe("rgb(20, 42, 59)");
    expect(getHeatColor(1.26)).toBe("rgb(248, 92, 105)");
    expect(getHeatColor(0.96)).not.toBe(getHeatColor(1.12));
  });

  it("labels fuelling direction and physical display units", () => {
    expect(getFlowArrow("toward-end-b")).toBe("→");
    expect(getFlowDirectionLabel("toward-end-a")).toBe("END B → END A");
    expect(formatPowerWatts(2.5e6)).toBe("2.5 MW");
    expect(formatReactivity(-0.0012)).toBe("-1.200 mk");
  });

  it("uses the same operating-envelope thresholds as the command deck", () => {
    const snapshot = {
      physics: { actualPowerFraction: 1, totalPowerWatts: 1 },
      targetPowerFraction: 1,
      absoluteTiltFraction: 0,
      targetTiltFraction: 0,
      controlMarginFraction: 0.9,
    } as CanduSnapshot;

    expect(getOverallStatus(snapshot)).toBe("stable");
    expect(getOverallStatus({ ...snapshot, controlMarginFraction: 0.55 })).toBe("attention");
    expect(getOverallStatus({ ...snapshot, absoluteTiltFraction: 0.06 })).toBe("watch");
  });
});
