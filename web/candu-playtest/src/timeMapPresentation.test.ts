import { expect, it } from "vitest";
import { timeMapCell } from "./timeMapPresentation";
import { createSnapshot } from "./testSnapshot";
import { ArcadeMap } from "./ArcadeMap";
import type { CanduSnapshot } from "./protocol";

const plans: CanduSnapshot["refuellingPlans"] = [
  { directionId: "toward-end-a", shiftCount: 8, incomingEnd: "B", outgoingEnd: "A", insertedPositions: [4,5,6,7,8,9,10,11],
    dischargedPositions: [0,1,2,3,4,5,6,7], retainedFromPositions: [8,9,10,11], retainedToPositions: [0,1,2,3] },
  { directionId: "toward-end-b", shiftCount: 8, incomingEnd: "A", outgoingEnd: "B", insertedPositions: [0,1,2,3,4,5,6,7],
    dischargedPositions: [4,5,6,7,8,9,10,11], retainedFromPositions: [0,1,2,3], retainedToPositions: [8,9,10,11] },
];

it("reads ripple from the shared reference ratios independently of raw channel power", () => {
  const snapshot = createSnapshot(), channel = snapshot.core.channels[0];
  snapshot.ripple = { referenceId: "test", dataPackVersion: "test", coefficientBindingDigestHex: "test", referenceThermalPowerWatts: 1, referenceChannelPowerWatts: [6_000_000], channelRippleFractions: [1.12], rmsDeviationFraction: .12, pointsPerHour: 0 };
  channel.powerWatts = 1;
  expect(timeMapCell(snapshot, channel, "ripple").reading).toBe("Ripple 112.0% of reference");
  expect(timeMapCell(snapshot, channel, "power").reading).toBe("6720 kW");
});

it("uses the published inlet and maps calibrated zone IDs to face-view labels", () => {
  const snapshot = createSnapshot(), channel = snapshot.core.channels[0];
  channel.bundles.forEach(b => { b.logicalZoneId = b.position < 6 ? 2 : 9; b.absorberZoneId = 0; });
  snapshot.refuellingPlans = plans;
  snapshot.rrs.zones = [2, 9].map((logicalZoneId, i) => ({ logicalZoneId, fillFraction: i ? .8 : .2, referencePowerFraction: 0, targetPowerFraction: 0, measuredPowerFraction: 0, shapeError: 0 }));
  const b = timeMapCell(snapshot, channel, "inlet-zones");
  expect(b.reading).toBe("Inlet End B · Z12 · 80.0% filled");
  expect(b.background).toMatch(/^rgb\(/);
  snapshot.rrs.zones[0].fillFraction = .3;
  expect(timeMapCell(snapshot, channel, "inlet-zones")).toEqual(b);
  channel.flowDirection = "toward-end-b";
  const a = timeMapCell(snapshot, channel, "inlet-zones");
  expect(a.reading).toBe("Inlet End A · Z5 · 30.0% filled");
  expect(a.background).not.toBe(b.background);
  channel.bundles.forEach(b => { delete b.logicalZoneId; });
  expect(timeMapCell(snapshot, channel, "inlet-zones").reading).toBe("Inlet zone unavailable");
});

it("changes map readings and colours while preserving fuelling and clearing power animations", () => {
  const snapshot = createSnapshot(); snapshot.core.channels[0].powerWatts = 7_050_000;
  snapshot.core.channels[0].averageBurnupMwdPerKg = 7.5;
  let fuel = -1;
  const map = new ArcadeMap(snapshot, () => {}, index => { fuel = index; }, true);
  document.body.append(map.element);
  const cell = map.element.querySelector<HTMLButtonElement>('[data-channel="0"]')!;
  map.update(snapshot, 0, [], "power", false);
  expect(cell.dataset.demoAlert).toBe("shake");
  const powerColor = cell.style.background;
  map.update(snapshot, 0, [], "burnup", false);
  expect(cell.style.background).not.toBe(powerColor);
  expect(cell.getAttribute("aria-label")).toContain("7.50 MWd/kg");
  expect(cell.dataset.demoAlert).toBe("normal");
  cell.focus();
  expect(document.querySelector('[role="tooltip"]')!.textContent).toContain("7.50 MWd/kg");
  map.update(snapshot, 0, [], "ripple", false);
  expect(cell.getAttribute("aria-label")).toContain("Ripple unavailable");
  cell.dispatchEvent(new MouseEvent("dblclick", { bubbles: true })); expect(fuel).toBe(0);
  map.destroy(); map.element.remove();
});

it("distinguishes nearby readings inside the benchmarked operating bands", () => {
  const snapshot = createSnapshot(), channel = snapshot.core.channels[0];
  const shade = (mode: "power" | "ripple" | "burnup" | "inlet-zones") => timeMapCell(snapshot, channel, mode).background;

  snapshot.ripple = undefined;
  channel.powerWatts = 4_400_000;
  const lowerPower = shade("power");
  channel.powerWatts = 4_450_000;
  expect(shade("power")).not.toBe(lowerPower);

  snapshot.ripple = { referenceId: "test", dataPackVersion: "test", coefficientBindingDigestHex: "test", referenceThermalPowerWatts: 1, referenceChannelPowerWatts: [5_500_000], channelRippleFractions: [.998], rmsDeviationFraction: .002, pointsPerHour: 0 };
  const belowReference = shade("ripple");
  snapshot.ripple.channelRippleFractions[0] = 1.002;
  expect(shade("ripple")).not.toBe(belowReference);

  channel.averageBurnupMwdPerKg = 3.2;
  const lowerBurnup = shade("burnup");
  channel.averageBurnupMwdPerKg = 3.3;
  expect(shade("burnup")).not.toBe(lowerBurnup);

  channel.bundles.forEach(b => { b.logicalZoneId = b.position < 6 ? 2 : 9; });
  snapshot.refuellingPlans = plans;
  snapshot.rrs.zones = [2, 9].map(logicalZoneId => ({ logicalZoneId, fillFraction: .5, referencePowerFraction: 0, targetPowerFraction: 0, measuredPowerFraction: 0, shapeError: 0 }));
  const halfFull = shade("inlet-zones");
  snapshot.rrs.zones[1].fillFraction = .505;
  expect(shade("inlet-zones")).not.toBe(halfFull);
});
