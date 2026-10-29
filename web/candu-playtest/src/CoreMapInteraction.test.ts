import { afterEach, expect, it } from "vitest";
import { CoreMapTooltip, CoreMapZoom, exitBurnup } from "./CoreMapInteraction";
import { createSnapshot } from "./testSnapshot";

afterEach(() => document.body.replaceChildren());
it("previews only the published outgoing positions and refuses incomplete readings", () => {
  const snapshot = createSnapshot(), channel = snapshot.core.channels[0];
  channel.bundles.forEach((bundle, i) => bundle.currentBurnupMwdPerKg = i);
  snapshot.refuellingPlans = [ { directionId: "toward-end-a", shiftCount: 8, incomingEnd: "B", outgoingEnd: "A", insertedPositions: [], dischargedPositions: [0,1,2,3,4,5,6,7], retainedFromPositions: [], retainedToPositions: [] },
    { directionId: "toward-end-b", shiftCount: 8, incomingEnd: "A", outgoingEnd: "B", insertedPositions: [], dischargedPositions: [4,5,6,7,8,9,10,11], retainedFromPositions: [], retainedToPositions: [] } ];
  expect(exitBurnup(snapshot, channel)).toBe(3.5);
  channel.flowDirection = "toward-end-b";
  expect(exitBurnup(snapshot, channel)).toBe(7.5);
  channel.bundles[11].hasFuel = false;
  expect(exitBurnup(snapshot, channel)).toBeNull();
  snapshot.refuellingPlans = [];
  expect(exitBurnup(snapshot, channel)).toBeNull();
});
it("shows accessible hover/focus readings, refreshes them, dismisses on Escape and cleans up", () => {
  const map = document.createElement("div"); map.innerHTML = '<button data-channel="0">Channel</button>';
  document.body.append(map);
  const tooltip = new CoreMapTooltip(map), snapshot = createSnapshot(); tooltip.update(snapshot);
  const button = map.querySelector("button")!;
  button.dispatchEvent(new MouseEvent("pointerover", { bubbles: true }));
  const tip = document.querySelector<HTMLElement>('[role="tooltip"]')!;
  expect(tip.hidden).toBe(false); expect(button.getAttribute("aria-describedby")).toBe(tip.id);
  expect(tip.textContent).toContain("Exit burnup unavailable");
  snapshot.core.channels[0].powerWatts = 6500000; tooltip.update(snapshot);
  expect(tip.textContent).toContain("6500 kW");
  document.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape" }));
  expect(tip.hidden).toBe(true); expect(button.hasAttribute("aria-describedby")).toBe(false);
  button.focus(); expect(tip.hidden).toBe(false);
  tooltip.destroy(); expect(document.querySelector('[role="tooltip"]')).toBeNull();
});
it("bounds zoom, preserves selection, and returns to fit without reactor commands", () => {
  const viewport = document.createElement("div"), canvas = document.createElement("div");
  canvas.innerHTML = '<button data-channel="0" aria-pressed="true">Channel</button>';
  viewport.append(canvas); document.body.append(viewport);
  const zoom = new CoreMapZoom(viewport, canvas);
  const plus = document.querySelector<HTMLButtonElement>('[data-zoom="in"]')!;
  plus.click(); expect(canvas.style.width).toBe("150%");
  plus.click(); plus.click(); plus.click(); expect(canvas.style.width).toBe("400%"); expect(plus.disabled).toBe(true);
  document.querySelector<HTMLButtonElement>('[data-zoom="out"]')!.click(); expect(canvas.style.width).toBe("300%");
  document.querySelector<HTMLButtonElement>('[data-zoom="fit"]')!.click(); expect(canvas.style.width).toBe("100%");
  expect(canvas.querySelector('[aria-pressed="true"]')).not.toBeNull();
  zoom.destroy(); expect(document.querySelector('.core-map-zoom')).toBeNull();
});
