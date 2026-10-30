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
