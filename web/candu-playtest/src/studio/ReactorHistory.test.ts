import { describe, expect, it } from "vitest";
import { ReactorHistory, HISTORY_CAPACITY, HISTORY_SNAPSHOT_CAPACITY } from "./ReactorHistory";
import { createUnavailableRrsSnapshot, type CanduSnapshot } from "../protocol";

function snapshot(time = 0, operations = 0): CanduSnapshot {
  const rrs = createUnavailableRrsSnapshot();
  rrs.zones = Array.from({ length: 14 }, (_, logicalZoneId) => ({ logicalZoneId,
    fillFraction: logicalZoneId / 20, referencePowerFraction: 0, targetPowerFraction: 0, measuredPowerFraction: 0, shapeError: 0 }));
  return { source: "wasm", simulationTimeSeconds: time, refuellingOperationCount: operations,
    freshBundlesAvailable: 128, scoreTotal: 10, axialTiltFraction: -.01, rrsReserveFraction: .5, rrs,
    physics: { isAuthoritative: true, totalPowerWatts: 2e9, electricalPowerWatts: 650e6, effectiveK: 1.001, reactivity: .001 },
    core: { channels: [{ powerWatts: 6e6, localTiltFraction: -.03, bundles: [
      { hasFuel: true, powerWatts: 800e3, currentBurnupMwdPerKg: 7 },
      { hasFuel: true, powerWatts: 600e3, currentBurnupMwdPerKg: 5 },
      { hasFuel: false, powerWatts: 99e6, currentBurnupMwdPerKg: 99 },
    ] }] } } as CanduSnapshot;
}

describe("session observation history", () => {
  it("records shared poison measurements and preserves unavailable values as null", () => {
    const history = new ReactorHistory();
    const reading = snapshot();
    reading.xenon = { hasCoupling: true, meanI135NumberDensityM3: 3e20,
      meanXe135NumberDensityM3: 2e20 } as CanduSnapshot["xenon"];
    reading.rrs.zones[0].meanXe135NumberDensityM3 = 4e20;
    history.record(reading);
    expect(history.samples[0]).toMatchObject({ meanIodine: 3, meanXenon: 2 });
    expect(history.samples[0].zoneXenon.slice(0, 2)).toEqual([4, null]);
    history.record(snapshot(1800));
    expect(history.samples[1]).toMatchObject({ meanIodine: null, meanXenon: null });
  });
  it("records physical power peaks, fourteen zone fills, signed tilt and authoritative discharge data", () => {
    const history = new ReactorHistory();
    history.record(snapshot());
    expect(history.samples[0]).toMatchObject({ maxChannelMw: 6, maxBundleKw: 800, thermalMw: 2000,
      electricalMw: 650, meanFuelBurnup: 6, maximumFuelBurnup: 7, axialTiltPercent: -1, maxLocalTiltPercent: 3,
      lastDischargeBurnup: null, maximumDischargeBurnup: null });
    expect(history.samples[0].zoneFills).toHaveLength(14);
    expect(history.samples[0].zoneFills[13]).toBe(65);
    history.record({ ...snapshot(1800, 1), lastDischargedMaximumBurnupMwdPerKg: 8.5, maximumDischargedBurnupMwdPerKg: 8.5 });
    history.record({ ...snapshot(3600, 2), lastDischargedMaximumBurnupMwdPerKg: 2, maximumDischargedBurnupMwdPerKg: 8.5 });
    expect(history.samples.at(-1)).toMatchObject({ lastDischargeBurnup: 2, maximumDischargeBurnup: 8.5 });
  });
  it("ignores duplicate notifications and fixtures, retains instantaneous changes, and resets on a new run", () => {
    const history = new ReactorHistory();
    history.record({ ...snapshot(), source: "synthetic-fixture" }); expect(history.samples).toHaveLength(0);
    history.record(snapshot(3600)); history.record(snapshot(3600)); expect(history.samples).toHaveLength(1);
    history.record({ ...snapshot(3600), scoreTotal: 20 }); expect(history.samples).toHaveLength(2);
    history.record(snapshot(0)); expect(history.samples).toHaveLength(1);
    history.clear(); expect(history.samples).toHaveLength(0);
  });
  it("bounds retained observations without altering the latest measurement", () => {
    const history = new ReactorHistory();
    for (let time = 0; time <= HISTORY_CAPACITY; time++) history.record(snapshot(time));
    expect(history.samples).toHaveLength(HISTORY_CAPACITY);
    expect(history.samples[0].timeSeconds).toBe(1);
    expect(history.samples.at(-1)!.timeSeconds).toBe(HISTORY_CAPACITY);
    expect(history.snapshotAt(history.inspectableSamples.at(-1)!)!.simulationTimeSeconds).toBe(HISTORY_CAPACITY);
    expect(history.snapshotAt(history.samples[0])).toBeUndefined();
  });
  it("retains half-hour states and the latest frame, with a bounded full-state archive", () => {
    const history = new ReactorHistory();
    for (let i = 0; i <= HISTORY_SNAPSHOT_CAPACITY; i++) history.record(snapshot(i * 1800));
    expect(history.inspectableSamples).toHaveLength(HISTORY_SNAPSHOT_CAPACITY);
    history.record(snapshot(HISTORY_SNAPSHOT_CAPACITY * 1800 + 1));
    const transient = history.inspectableSamples.at(-1)!;
    history.pinObservation(transient);
    history.record(snapshot(HISTORY_SNAPSHOT_CAPACITY * 1800 + 2));
    expect(history.snapshotAt(transient)).toBeDefined();
    expect(history.inspectableSamples).toHaveLength(HISTORY_SNAPSHOT_CAPACITY);
  });
});
