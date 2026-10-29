// @vitest-environment jsdom
import { afterEach, expect, it, vi } from "vitest";
import { createCanduPlaytestBridge, type CanduPlaytestBridgeLifecycle } from "./bridge";
import { DemoView } from "./DemoView";
import { createSnapshot } from "./testSnapshot";
import type { CanduCommandResponse, CanduSnapshot } from "./protocol";

vi.mock("./bridge", () => ({ createCanduPlaytestBridge: vi.fn() }));
let view: DemoView;
afterEach(() => { view?.destroy(); document.body.replaceChildren(); vi.useRealTimers(); });

it("keeps the power map interactive and last solved values visible while fuelling", async () => {
  vi.useFakeTimers();
  const snapshot: CanduSnapshot = { ...createSnapshot(), pacingMode: "demo", completedDays: 0 };
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
  cell.click(); cell.dispatchEvent(new MouseEvent("dblclick", { bubbles: true }));
  expect(bridge.dispatch).toHaveBeenCalledWith(expect.objectContaining({ type: "commit-refuel", request: expect.objectContaining({ channelIndex: 210 }) }), { responseMode: "compact" });
  expect(cell.disabled).toBe(false);
  expect(cell.textContent).toBe(text); // No fabricated power response while solving.
  expect(cell.dataset.planned).toBe("true");
  expect(view.element.querySelector("dialog")).toBeNull();
  const other = view.element.querySelector<HTMLButtonElement>('[data-channel="211"]')!;
  other.click(); expect(other.getAttribute("aria-pressed")).toBe("true");
  expect(view.element.querySelector('[data-selected-channel]')?.textContent).not.toBe("—");
  expect(cell.textContent).toBe(text);
  finish(); await vi.advanceTimersByTimeAsync(0);
  expect(cell.dataset.planned).toBe("false");
  expect(view.element.querySelector('[data-message]')?.textContent).toContain("Fuel delivered");
});
