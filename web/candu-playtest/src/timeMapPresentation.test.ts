import { expect, it } from "vitest";
import { timeMapCell } from "./timeMapPresentation";
import { createSnapshot } from "./testSnapshot";
import { ArcadeMap } from "./ArcadeMap";

it("reads ripple from the shared reference ratios independently of raw channel power", () => {
  const snapshot = createSnapshot(), channel = snapshot.core.channels[0];
  snapshot.ripple = { referenceId: "test", dataPackVersion: "test", coefficientBindingDigestHex: "test", referenceThermalPowerWatts: 1, referenceChannelPowerWatts: [6_000_000], channelRippleFractions: [1.12], rmsDeviationFraction: .12, pointsPerHour: 0 };
  channel.powerWatts = 1;
  expect(timeMapCell(snapshot, channel, "ripple").reading).toBe("Ripple 112.0% of reference");
  expect(timeMapCell(snapshot, channel, "power").reading).toBe("6720 kW");
});

it("uses published paired regions and updates both end fills without inventing missing geometry", () => {
  const snapshot = createSnapshot(), channel = snapshot.core.channels[0];
  channel.bundles.forEach(b => { b.logicalZoneId = b.position < 6 ? 2 : 9; b.absorberZoneId = 0; });
  snapshot.rrs.zones = [2, 9].map((logicalZoneId, i) => ({ logicalZoneId, fillFraction: i ? .8 : .2, referencePowerFraction: 0, targetPowerFraction: 0, measuredPowerFraction: 0, shapeError: 0 }));
  const pair = timeMapCell(snapshot, channel, "paired-zones");
  expect(pair.reading).toBe("End A Z3: 20.0% filled · End B Z10: 80.0% filled");
  expect(pair.background).toContain("linear-gradient");
  snapshot.rrs.zones[1].fillFraction = .6;
  expect(timeMapCell(snapshot, channel, "paired-zones").background).not.toBe(pair.background);
  channel.bundles.forEach(b => { delete b.logicalZoneId; });
  expect(timeMapCell(snapshot, channel, "paired-zones").reading).toBe("End A fill unavailable · End B fill unavailable");
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
