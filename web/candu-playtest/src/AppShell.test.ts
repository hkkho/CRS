import { afterEach, expect, it, vi } from "vitest";
import { AppShell } from "./AppShell";
import { createSnapshot, createShift } from "./testSnapshot";
import { ReactorHistory } from "./studio/ReactorHistory";
import type { SessionUpdate } from "./sessionController";
import type { BridgeStatus } from "./protocol";

let shell: AppShell | null = null;
afterEach(() => { shell?.destroy(); shell = null; document.body.replaceChildren(); });
function session() {
  const snapshot = createSnapshot(); snapshot.shift = createShift();
  const status: BridgeStatus = { source: "wasm", isWasmAvailable: true, title: "Ready", detail: "Ready", capabilities: [] };
  const history = new ReactorHistory(); history.record(snapshot);
  return { snapshot, status, history, isPending: false, dispatch: vi.fn(), initializeMode: vi.fn(), startShift: vi.fn(), stopShift: vi.fn(),
    subscribe: (listener: (update: SessionUpdate) => void) => { listener({ snapshot, status, pending: false, response: null, error: null }); return vi.fn(); } };
}
function click(selector: string) { document.querySelector<HTMLButtonElement>(selector)!.click(); }

it("starts Arcade only after the shared free-practice reset is accepted", async () => {
  const shared = session();
  shared.dispatch.mockResolvedValue({ accepted: false, message: "Core unavailable" });
  shell = new AppShell(shared, document.body);
  document.querySelector<HTMLSelectElement>('[data-presentation]')!.value = "arcade";
  click('[data-action="begin"]');
  await vi.waitFor(() => expect(document.querySelector('[data-field="error"]')!.textContent).toBe("Core unavailable"));
  expect(document.querySelector('.reactor-arcade')).toBeNull();
  shared.dispatch.mockResolvedValue({ accepted: true });
  click('[data-action="begin"]');
  await vi.waitFor(() => expect(document.querySelector('.reactor-arcade')).not.toBeNull());
  expect(shared.dispatch).toHaveBeenLastCalledWith({ type: "reset", seed: 42, shiftId: "free-practice" });
});

it("opens the native Studio over the existing session and cleans up its subscriber", () => {
  const base = session();
  base.snapshot.shift = { ...base.snapshot.shift!, id: "free-practice", isEndless: true, unlimitedFreshFuel: true };
  const listeners = new Set<(update: SessionUpdate) => void>();
  const shared = { ...base, subscribe: (listener: (update: SessionUpdate) => void) => {
    listeners.add(listener);
    listener({ snapshot: base.snapshot, status: base.status, pending: false, response: null, error: null });
    return () => { listeners.delete(listener); };
  } };
  shell = new AppShell(shared, document.body);
  expect(document.querySelector('.reactor-launcher h1')!.textContent).toBe("CRS");
  expect(listeners.size).toBe(1);
  click('[data-action="begin"]');
  expect(document.querySelector('.reactor-studio')).not.toBeNull();
  expect(document.querySelector('.studio-brand h1')!.textContent).toBe("CRS");
  expect((document.querySelector('.reactor-studio') as HTMLElement).dataset.mapMode).toBe("power");
  expect(document.querySelector('[data-map="power"]')!.getAttribute("aria-pressed")).toBe("true");
  expect(document.querySelector('[data-action="designer"]')).toBeNull();
  expect(document.querySelector('canvas')).toBeNull();
  expect(listeners.size).toBe(1);
  expect(shared.startShift).toHaveBeenCalledTimes(1);
  expect(shared.initializeMode).not.toHaveBeenCalled();
  expect(shared.history).toBe(base.history);
  shell.destroy(); shell = null;
  expect(shared.stopShift).toHaveBeenCalledTimes(1);
  expect(listeners.size).toBe(0);
});
