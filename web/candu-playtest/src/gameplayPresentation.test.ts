import { describe, expect, it } from "vitest";
import { highestBurnupChannel, operationGuidance, refuelImpactText } from "./gameplayPresentation";
import type { CanduChannelSnapshot, CanduSnapshot } from "./protocol";

function channel(channelIndex: number, burnup: number, hasFuel = true): CanduChannelSnapshot {
  return { channelIndex, averageBurnupMwdPerKg: burnup, localPowerFraction: 0.8,
    localTiltFraction: -0.03, bundles: [{ hasFuel }] } as CanduChannelSnapshot;
}

function snapshot(channels: CanduChannelSnapshot[]): CanduSnapshot {
  return { core: { channels }, scoreTotal: 10, freshBundlesAvailable: 128,
    rrsReserveFraction: 0.84, rrs: { isGameOver: false }, physics: { actualPowerFraction: 1 } } as CanduSnapshot;
}

describe("gameplay presentation", () => {
  it("ranks burnup deterministically without reordering the snapshot or selecting empty cells", () => {
    const channels = [channel(9, 7), channel(1, 100, false), channel(3, 7)];
    expect(highestBurnupChannel(channels)).toBe(3);
    expect(channels.map(c => c.channelIndex)).toEqual([9, 1, 3]);
    const mixed = { ...channel(2, 200), bundles: [{ hasFuel: true }, { hasFuel: false }] } as CanduChannelSnapshot;
    expect(highestBurnupChannel([...channels, mixed])).toBe(3);
    expect(highestBurnupChannel([{ ...channel(2, 200), canRefuel: false }, ...channels])).toBe(3);
    expect(highestBurnupChannel([])).toBeNull();
    expect(highestBurnupChannel([channel(1, 100, false)])).toBeNull();
  });

  it("compares the requested channel and preserves signed response and score loss", () => {
    const before = snapshot([channel(3, 6), channel(9, 7)]);
    const after = snapshot([{ ...channel(3, 5), localPowerFraction: 0.9, localTiltFraction: 0.02 }]);
    after.freshBundlesAvailable = 124;
    after.scoreTotal = 4;
    const text = refuelImpactText(before, after, 3);
    expect(text).toContain("-6.0 SCORE");
    expect(text).toContain("80.0% → 90.0%");
    expect(text).toContain("-3.00% → +2.00%");
    expect(text).toContain("128 → 124 bundles");
    expect(text).toContain("100.0% → 100.0% (regulated)");
    expect(refuelImpactText(before, after, 9)).toContain("unavailable");
  });

  it("displays authoritative reward and cost without recomputing the policy", () => {
    const before = snapshot([channel(3, 6)]);
    const after = snapshot([channel(3, 5)]);
    after.lastRefuellingScore = { policyId: "test-policy", dischargeReward: 17, freshFuelCost: 6, netPoints: 11 };
    expect(refuelImpactText(before, after, 3)).toContain("+11.0 SCORE");
    expect(refuelImpactText(before, after, 3)).toContain("Discharge reward  +17.0");
    expect(refuelImpactText(before, after, 3)).toContain("Fresh fuel cost  −6.0");
  });

  it("explains exhausted inventory and terminal runs instead of silently disabling controls", () => {
    const state = snapshot([]);
    state.freshBundlesAvailable = 0;
    expect(operationGuidance(state)).toContain("Fuel budget spent");
    state.rrs.isGameOver = true;
    expect(operationGuidance(state)).toContain("Run complete");
  });
});
