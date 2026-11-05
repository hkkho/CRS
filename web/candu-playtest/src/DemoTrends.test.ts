// @vitest-environment jsdom
import { expect, it } from "vitest";
import { DemoTrends } from "./DemoTrends";
import { createSnapshot } from "./testSnapshot";

it("keeps bounded displayed history, changes channel traces, and resets on timeline rewind", () => {
  const trends = new DemoTrends(), snapshot = createSnapshot();
  snapshot.core.channels[0].powerWatts = 5_100_000;
  snapshot.core.channels[1].powerWatts = 6_200_000;
  trends.update(snapshot, 0, 0);
  trends.update(snapshot, 0, 0); // Paused/held frames do not grow history.
  expect(trends.element.dataset.samples).toBe("1");
  trends.update(snapshot, .5, 0);
  const previousPath = trends.element.querySelector('[data-chart=power] .trend-line:last-of-type')?.getAttribute("d");
  trends.update(snapshot, .5, 1);
  expect(trends.element.dataset.samples).toBe("2");
  expect(trends.element.querySelector('[data-power-trend-reading]')?.textContent).toContain("6200");
  expect(trends.element.querySelector('[data-chart=power] .trend-line:last-of-type')?.getAttribute("d")).not.toBe(previousPath);
  for (let i = 1; i <= 260; i++) trends.update(snapshot, i, 1);
  expect(trends.element.dataset.samples).toBe("241");
  trends.update(snapshot, 0, 1);
  expect(trends.element.dataset.samples).toBe("1");
  expect(trends.element.querySelectorAll('[data-chart=zones] .trend-limit')).toHaveLength(2);
  trends.update({ ...snapshot, runStatus: "ended", rrs: { ...snapshot.rrs, averageFillFraction: .09 } }, 0, 1);
  expect(trends.element.dataset.samples).toBe("1");
  expect(trends.element.querySelector('[data-zone-trend-reading]')?.textContent).toContain("9.0%");
});

it("switches both selected zone histories immediately, retaining the core average", () => {
  const snapshot = createSnapshot();
  for (const [index, pair] of [[0, [0, 7]], [1, [2, 9]]] as const) {
    snapshot.core.channels[index].bundles = snapshot.core.channels[index].bundles.map((bundle, i) =>
      ({ ...bundle, logicalZoneId: pair[i < 6 ? 0 : 1] }));
  }
  snapshot.rrs.averageFillFraction = .5;
  snapshot.rrs.zones = [9, 7, 2, 0].map(logicalZoneId => ({
    logicalZoneId, fillFraction: (30 + logicalZoneId) / 100, referencePowerFraction: 0,
    targetPowerFraction: 0, measuredPowerFraction: 0, shapeError: 0,
  }));
  const trends = new DemoTrends();
  const zoneChart = trends.element.querySelector('[data-chart="zones"]')!;
  const paths = () => [...zoneChart.querySelectorAll(".trend-line")].map(path => path.getAttribute("d"));
  trends.update(snapshot, 0, 0);
  snapshot.rrs.zones[0].fillFraction = .6;
  trends.update(snapshot, .05, 0);
  const original = paths();
  expect(original).toHaveLength(3);
  expect(trends.element.querySelector("[data-trend-end-b]")?.textContent).toBe("End B · Z8");

  trends.update(snapshot, .06, 1);
  const switched = paths();
  expect(trends.element.dataset.samples).toBe("2");
  expect(switched[0]).toBe(original[0]);
  expect(switched[1]).not.toBe(original[1]);
  expect(switched[2]).toBe("M38.0,95.1 L48.5,68.2");
  expect(trends.element.querySelector("[data-trend-end-a]")?.textContent).toBe("End A · Z3");
  expect(trends.element.querySelector("[data-trend-end-b]")?.textContent).toBe("End B · Z10");
  expect(trends.element.querySelector("[data-zone-trend-reading]")?.textContent).toContain("End A: 32.0% · End B: 60.0%");

  trends.update(snapshot, .06, 2);
  expect(paths()).toEqual([original[0]]);
  expect(trends.element.querySelector("[data-zone-trend-reading]")?.textContent).toContain("End A: — · End B: —");
});
