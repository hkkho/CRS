import { describe, expect, it } from "vitest";
import {
  formatBundleBurnup,
  formatEffectiveK,
  formatPowerWatts,
  formatReactivity,
  formatSolveHealth,
  formatSolveResidual,
  getTiltLabel,
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
    expect(formatBundleBurnup(5.99)).toBe("6.0");
    expect(formatBundleBurnup(1250)).toBe("1.3k");
    expect(getTiltLabel(-0.000000001)).toBe("+0.00%");
    expect(getTiltLabel(-0.0513)).toBe("-5.13%");
    expect(formatReactivity(-0.0012)).toBe("-1.200 mk");
    expect(formatEffectiveK(1.0023456)).toBe("1.002346");
    expect(formatSolveResidual(0.0000123)).toBe("1.2e-5");
  });

  it("summarizes solve health without exposing verbose solver identity", () => {
    const snapshot = {
      physics: { solverIterationCount: 14, solverResidualRelativeInfinity: 0.00000042, solveState: "converged" },
      diagnostics: { convergence: { state: "converged", iterations: 12, residual: 0.2 } },
    } as CanduSnapshot;

    expect(formatSolveHealth(snapshot)).toBe("CONVERGED · 14 IT · RES 4.2e-7");
  });

  it("uses the same operating-envelope thresholds as the command deck", () => {
    const snapshot = {
      physics: { actualPowerFraction: 1, totalPowerWatts: 1 },
      targetPowerFraction: 1,
      axialTiltFraction: 0,
      rrsReserveFraction: 0.9,
    } as CanduSnapshot;

    expect(getOverallStatus(snapshot)).toBe("stable");
    expect(getOverallStatus({ ...snapshot, rrsReserveFraction: 0.55 })).toBe("attention");
    expect(getOverallStatus({ ...snapshot, axialTiltFraction: 0.06 })).toBe("watch");
  });
});
