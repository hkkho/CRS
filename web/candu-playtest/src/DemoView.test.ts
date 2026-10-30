// @vitest-environment jsdom
import { afterEach, expect, it, vi } from "vitest";
import { createCanduPlaytestBridge, type CanduPlaytestBridgeLifecycle } from "./bridge";
import { DemoView } from "./DemoView";
import { createSnapshot } from "./testSnapshot";
import type { CanduCommandResponse, CanduSnapshot } from "./protocol";

vi.mock("./bridge", () => ({ createCanduPlaytestBridge: vi.fn() }));
let view: DemoView;
afterEach(() => { view?.destroy(); document.body.replaceChildren(); vi.useRealTimers(); });

it("queues fuel without blocking the map, shows all zones and never advertises solving", async () => {
  vi.useFakeTimers();
  const snapshot: CanduSnapshot = { ...createSnapshot(), pacingMode: "demo", completedDays: 0 };
  snapshot.core.channels[210].powerWatts = 6_600_000;
  snapshot.core.channels[211].powerWatts = 7_050_000;
  snapshot.rrs.averageFillFraction = .14;
  snapshot.rrs.zones = Array.from({ length: 14 }, (_, logicalZoneId) => ({ logicalZoneId,
    fillFraction: logicalZoneId === 0 ? .1 : .2, referencePowerFraction: 1 / 14,
    targetPowerFraction: 1 / 14, measuredPowerFraction: 1 / 14, shapeError: 0,
  }));
  let finish: () => void = () => {};
  const bridge: CanduPlaytestBridgeLifecycle = {
    status: { source: "wasm", title: "", detail: "", isWasmAvailable: true, capabilities: [] },
    getSnapshot: () => snapshot, subscribe: () => () => {}, initializeMode: async () => snapshot, dispose: vi.fn(),
    dispatch: vi.fn(command => new Promise<CanduCommandResponse>(resolve => { finish = () => resolve({ protocol: "candu-playtest-v2", command, accepted: true, message: "", diagnostics: [], sequence: 1, stateDigest: "test", snapshot }); })),
  };
  vi.mocked(createCanduPlaytestBridge).mockReturnValue(bridge);
  view = new DemoView(document.body, 1001, vi.fn());
  const cell = view.element.querySelector<HTMLButtonElement>('[data-channel="210"]')!;
  const text = cell.textContent;
  expect(text).toBe("");
  expect(view.element.querySelector(".core-map-zoom")).toBeNull();
  expect(view.element.querySelectorAll(".demo-trends svg")).toHaveLength(2);
  cell.click(); cell.dispatchEvent(new MouseEvent("dblclick", { bubbles: true }));
  expect(view.element.querySelector('.demo-hud [data-power]')?.textContent).toBe("6600");
  expect(bridge.dispatch).not.toHaveBeenCalled();
  await vi.advanceTimersByTimeAsync(50);
  expect(bridge.dispatch).toHaveBeenCalledWith(expect.objectContaining({ type: "commit-day", channelIndices: [210] }), { responseMode: "compact" });
  expect(cell.disabled).toBe(false);
  expect(cell.textContent).toBe(text); // No fabricated power response while solving.
  expect(cell.dataset.planned).toBe("true");
  expect(view.element.querySelector("dialog")).toBeNull();
  expect(view.element.querySelector("[data-live]")?.textContent).toBe("Live power");
  expect(view.element.querySelectorAll("[data-zone]")).toHaveLength(14);
  expect(cell.dataset.demoAlert).toBe("flash");
  expect(view.element.querySelector<HTMLElement>('[data-channel="211"]')?.dataset.demoAlert).toBe("shake");
  expect(view.element.querySelector<HTMLElement>('[data-zone="0"]')?.dataset.demoAlert).toBe("shake");
  expect(view.element.querySelector<HTMLElement>('[data-zone="1"]')?.dataset.demoAlert).toBe("flash");
  expect(view.element.querySelector('[data-zone-risk]')?.textContent).toContain("4.0 percentage points");
  expect(view.element.querySelector("[data-zone-mean]")?.textContent).toContain("%");
  const other = view.element.querySelector<HTMLButtonElement>('[data-channel="211"]')!;
  other.click(); expect(other.getAttribute("aria-pressed")).toBe("true");
  expect(view.element.querySelector('.demo-hud [data-power]')?.textContent).toBe("7050");
  expect(view.element.querySelector('[data-selected-channel]')?.textContent).not.toBe("—");
  expect(cell.textContent).toBe(text);
  finish(); await vi.advanceTimersByTimeAsync(0);
  expect(cell.dataset.planned).toBe("true");
  // Endpoint completion is covered by DemoLoop; the view must keep the pending
  // cue until that endpoint, even after the worker has already returned.
  expect(view.element.querySelector('[data-message]')?.textContent).not.toContain("Power updated");
});
