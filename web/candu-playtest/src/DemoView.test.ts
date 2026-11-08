// @vitest-environment jsdom
import { afterEach, expect, it, vi } from "vitest";
import { createCanduPlaytestBridge, type CanduPlaytestBridgeLifecycle } from "./bridge";
import { DemoView } from "./DemoView";
import { createSnapshot } from "./testSnapshot";
import type { BridgeStatus, CanduCommandResponse, CanduSnapshot } from "./protocol";

vi.mock("./bridge", () => ({ createCanduPlaytestBridge: vi.fn() }));
let view: DemoView;
afterEach(() => { view?.destroy(); document.body.replaceChildren(); vi.useRealTimers(); });

it("waits through initialization and readiness until Start, then begins the clock", async () => {
  vi.useFakeTimers();
  const snapshot: CanduSnapshot = { ...createSnapshot(), pacingMode: "demo", simulationTimeSeconds: 0, completedDays: 0 };
  let notify: Parameters<CanduPlaytestBridgeLifecycle["subscribe"]>[0] = () => {};
  const status: BridgeStatus = { source: "loading", title: "Loading", detail: "Loading reactor", isWasmAvailable: false, capabilities: [] };
  const bridge: ReturnType<typeof createCanduPlaytestBridge> = {
    status, getSnapshot: () => snapshot, initializeMode: async () => snapshot, whenReady: async () => snapshot,
    subscribe: listener => { notify = listener; return () => {}; }, dispose: vi.fn(),
    dispatch: vi.fn(() => new Promise<CanduCommandResponse>(() => {})),
  };
  vi.mocked(createCanduPlaytestBridge).mockReturnValue(bridge);
  const leave = vi.fn();
  view = new DemoView(document.body, 1001, leave);
  const dialog = view.element.querySelector<HTMLDialogElement>(".game-loading-dialog")!;
  const start = dialog.querySelector<HTMLButtonElement>("[data-game-start]")!;
  expect(dialog.open).toBe(true); expect(start.hidden).toBe(true); expect(start.disabled).toBe(true);
  await vi.advanceTimersByTimeAsync(5000);
  expect(bridge.dispatch).not.toHaveBeenCalled();
  notify({ ...status, source: "wasm", isWasmAvailable: true }, snapshot);
  expect(start.hidden).toBe(false); expect(start.disabled).toBe(false);
  expect(document.activeElement).toBe(start);
  await vi.advanceTimersByTimeAsync(5000);
  expect(bridge.dispatch).not.toHaveBeenCalled();
  expect(view.element.querySelector("[data-day]")!.textContent).toBe("Day 0 · 00:00");
  start.click(); start.click();
  expect(view.element.querySelector("dialog[open]")).toBeNull();
  await vi.advanceTimersByTimeAsync(50);
  expect(bridge.dispatch).toHaveBeenCalledTimes(1);
  expect(bridge.dispatch).toHaveBeenCalledWith(expect.objectContaining({ type: "commit-day" }), { responseMode: "compact" });
  expect(leave).not.toHaveBeenCalled();
});

it("shows setup failure and lets Escape return without starting", () => {
  const status: BridgeStatus = { source: "unavailable", title: "Unavailable", detail: "Reactor could not load", isWasmAvailable: false, capabilities: [] };
  const bridge: ReturnType<typeof createCanduPlaytestBridge> = {
    status, getSnapshot: createSnapshot, initializeMode: async () => createSnapshot(), whenReady: async () => createSnapshot(),
    subscribe: listener => { listener(status, createSnapshot()); return () => {}; }, dispose: vi.fn(), dispatch: vi.fn(),
  };
  vi.mocked(createCanduPlaytestBridge).mockReturnValue(bridge);
  const leave = vi.fn(); view = new DemoView(document.body, 1001, leave);
  const dialog = view.element.querySelector<HTMLDialogElement>(".game-loading-dialog")!;
  expect(dialog.textContent).toContain("Reactor could not load");
  expect(dialog.querySelector<HTMLButtonElement>("[data-game-start]")!.hidden).toBe(true);
  dialog.dispatchEvent(new Event("cancel", { cancelable: true }));
  expect(leave).toHaveBeenCalledTimes(1); expect(bridge.dispatch).not.toHaveBeenCalled();
});

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
  const bridge: ReturnType<typeof createCanduPlaytestBridge> = {
    status: { source: "wasm", title: "", detail: "", isWasmAvailable: true, capabilities: [] },
    getSnapshot: () => snapshot, subscribe: () => () => {}, initializeMode: async () => snapshot, whenReady: async () => snapshot, dispose: vi.fn(),
    dispatch: vi.fn(command => new Promise<CanduCommandResponse>(resolve => { finish = () => resolve({ protocol: "candu-playtest-v2", command, accepted: true, message: "", diagnostics: [], sequence: 1, stateDigest: "test", snapshot }); })),
  };
  vi.mocked(createCanduPlaytestBridge).mockReturnValue(bridge);
  view = new DemoView(document.body, 1001, vi.fn());
  expect(view.element.querySelector(".game-loading-dialog[open]")).not.toBeNull();
  await vi.advanceTimersByTimeAsync(10_000);
  expect(bridge.dispatch).not.toHaveBeenCalled();
  expect(view.element.dataset.running).toBe("false");
  view.element.querySelector<HTMLButtonElement>("[data-game-start]")!.click();
  expect(view.element.dataset.running).toBe("true");
  const cell = view.element.querySelector<HTMLButtonElement>('[data-channel="210"]')!;
  const text = cell.textContent;
  expect(text).toBe("");
  expect(view.element.querySelector(".core-map-zoom")).toBeNull();
  expect(view.element.textContent).not.toContain("DEMO");
  expect(view.element.textContent).not.toContain("1 day / 5 sec");
  expect(view.element.querySelector("[data-day]")?.textContent).toMatch(/Day \d+ · \d\d:\d\d/);
  expect(view.element.querySelectorAll(".demo-trends svg")).toHaveLength(2);
  cell.click(); cell.dispatchEvent(new MouseEvent("dblclick", { bubbles: true }));
  expect(view.element.querySelector('.demo-hud [data-power]')?.textContent).toBe("6600");
  const second = view.element.querySelector<HTMLButtonElement>('[data-channel="211"]')!;
  second.dispatchEvent(new MouseEvent("dblclick", { bubbles: true }));
  expect(second.dataset.planned).toBe("true");
  expect(bridge.dispatch).not.toHaveBeenCalled();
  await vi.advanceTimersByTimeAsync(50);
  expect(bridge.dispatch).toHaveBeenCalledWith(expect.objectContaining({ type: "commit-day", channelIndices: [210, 211] }), { responseMode: "compact" });
  expect(cell.disabled).toBe(false);
  expect(cell.textContent).toBe(text); // No fabricated power response while solving.
  expect(cell.dataset.planned).toBe("true");
  expect(view.element.querySelector("[data-fuel-queue]")?.textContent).toBe("0 waiting · 2 submitted");
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
  expect(cell.dataset.planned).toBe("true"); // Remains queued until the displayed endpoint.
  expect(second.dataset.planned).toBe("true");
  expect(view.element.querySelector('[data-message]')?.textContent).not.toContain("Power updated");
  const tabs = [...view.element.querySelectorAll<HTMLButtonElement>('[role="tab"]')];
  expect(tabs.map(tab => tab.textContent)).toEqual(["Power", "Ripple", "Burnup", "Inlet zones"]);
  const panel = view.element.querySelector('[role="tabpanel"]')!;
  for (const value of ["ripple", "burnup", "inlet-zones", "power"]) {
    const tab = tabs.find(tab => tab.dataset.mapTab === value)!;
    tab.click();
    expect(tabs.filter(tab => tab.getAttribute("aria-selected") === "true")).toEqual([tab]);
    expect(tabs.filter(tab => tab.tabIndex === 0)).toEqual([tab]);
    expect(panel.getAttribute("aria-labelledby")).toBe(tab.id);
    expect(view.element.querySelector('.arcade-map')?.getAttribute('data-map-mode')).toBe(value);
    expect(view.element.querySelector('[data-legend]')?.textContent).toContain(value === "power" ? "Channel power (kW)" : value === "inlet-zones" ? "Inlet zone fill (%)" : value === "ripple" ? "Ripple" : "Average channel burnup");
  }
  tabs[0].focus();
  for (const [key, index] of [["ArrowRight", 1], ["End", 3], ["ArrowRight", 0], ["ArrowLeft", 3], ["Home", 0]] as const) {
    document.activeElement!.dispatchEvent(new KeyboardEvent("keydown", { key, bubbles: true }));
    expect(document.activeElement).toBe(tabs[index]);
    expect(tabs[index].getAttribute("aria-selected")).toBe("true");
    expect(panel.getAttribute("aria-labelledby")).toBe(tabs[index].id);
  }
});

it("routes standard controller presses through the same map, fuel, and pause controls", async () => {
  vi.useFakeTimers();
  const hasFocus = vi.spyOn(document, "hasFocus").mockReturnValue(true);
  const buttons = Array.from({ length: 16 }, () => ({ pressed: false, touched: false, value: 0 }));
  vi.stubGlobal("navigator", { getGamepads: () => [
    { index: 0, connected: true, mapping: "standard", buttons },
  ] });
  try {
    const snapshot: CanduSnapshot = { ...createSnapshot(), pacingMode: "demo" };
    vi.mocked(createCanduPlaytestBridge).mockReturnValue({
      status: { source: "wasm", title: "", detail: "", isWasmAvailable: true, capabilities: [] },
      getSnapshot: () => snapshot, subscribe: () => () => {}, initializeMode: async () => snapshot, whenReady: async () => snapshot,
      dispatch: vi.fn(() => new Promise<CanduCommandResponse>(() => {})),
    });
    view = new DemoView(document.body, 1001, vi.fn());
    await vi.advanceTimersByTimeAsync(40);
    buttons[9].pressed = true;
    await vi.advanceTimersByTimeAsync(40);
    expect(view.element.dataset.running).toBe("true");
    expect(view.element.querySelector("[data-fuel-queue]")?.textContent).toBe("Queue empty");
    view.element.querySelector<HTMLButtonElement>('[data-channel="210"]')!.click();
    buttons[9].pressed = false;
    buttons[15].pressed = true;
    buttons[5].pressed = true;
    await vi.advanceTimersByTimeAsync(40);
    expect(view.element.querySelector('[data-channel="211"]')?.getAttribute("aria-pressed")).toBe("true");
    expect(view.element.dataset.mapMode).toBe("ripple");
    buttons[0].pressed = true;
    await vi.advanceTimersByTimeAsync(40);
    expect(view.element.querySelector('[data-channel="211"]')?.getAttribute("data-planned")).toBe("true");
    buttons[9].pressed = true;
    await vi.advanceTimersByTimeAsync(120);
    expect(view.element.dataset.running).toBe("false");
  } finally {
    vi.unstubAllGlobals();
    hasFocus.mockRestore();
  }
});
