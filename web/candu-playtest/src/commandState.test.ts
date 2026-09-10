import { describe, expect, it } from "vitest";
import {
  adjustTarget,
  canConfirmRefuel,
  createRefuelDraft,
  formatRefuelDirection,
  previewMatchesDraft,
  toggleRefuelDirection,
  toggleShiftCount,
  toRefuelRequest,
} from "./commandState";

describe("Phaser command window state", () => {
  const channel = { channelIndex: 210, flowDirection: "toward-end-b" as const };

  it("creates a playable order from the selected channel", () => {
    const draft = createRefuelDraft(channel);
    expect(toRefuelRequest(draft)).toEqual({
      channelIndex: 210,
      directionId: "toward-end-b",
      shiftCount: 4,
      fuelTypeId: "NAT-U-SYNTHETIC",
    });
    expect(formatRefuelDirection(draft.directionId)).toContain("END A");
    expect(toggleRefuelDirection(draft.directionId)).toBe("toward-end-a");
    expect(toggleShiftCount(draft.shiftCount)).toBe(8);
  });

  it("only enables confirmation for the current authoritative preview", () => {
    const draft = createRefuelDraft(channel);
    const preview = {
      request: toRefuelRequest(draft),
      dischargeBurnupMwdPerKg: 5_200,
      localPowerDeltaFraction: 0.01,
      localTiltDeltaFraction: -0.01,
      predictedReactivityDelta: 0.0001,
      projectedPowerFraction: 1,
      projectedTiltFraction: 0,
      projectedScoreDelta: 2,
      insertedBundleIds: [],
      dischargedBundleIds: [],
    };

    expect(previewMatchesDraft(preview, draft)).toBe(true);
    expect(canConfirmRefuel(preview, draft, 8, false)).toBe(true);
    expect(canConfirmRefuel(preview, { ...draft, shiftCount: 8 }, 8, false)).toBe(false);
    expect(canConfirmRefuel(preview, draft, 2, false)).toBe(false);
    expect(canConfirmRefuel(preview, draft, 8, true)).toBe(false);
  });

  it("keeps target nudges inside the operator envelope", () => {
    expect(adjustTarget(1.2, 0.05, 0.8, 1.2)).toBe(1.2);
    expect(adjustTarget(0.8, -0.05, 0.8, 1.2)).toBe(0.8);
    expect(adjustTarget(0.995, 0.005, 0.8, 1.2)).toBe(1);
  });
});
