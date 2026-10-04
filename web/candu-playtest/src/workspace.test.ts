import { afterEach, expect, it, vi } from "vitest";
import { LauncherView } from "./LauncherView";
import { createSnapshot, createShift } from "./testSnapshot";
import type { BridgeStatus, CanduCommandResponse } from "./protocol";
import type { SessionUpdate } from "./sessionController";

const cleanup: Array<() => void> = [];
afterEach(() => { cleanup.splice(0).forEach(destroy => destroy()); document.body.replaceChildren(); });

it("launches once, preserves seed drafts during notifications and sends the selected seed/objective", () => {
  const snapshot = createSnapshot(); snapshot.shift = createShift();
  const status: BridgeStatus = { source: "wasm", isWasmAvailable: true, title: "Ready", detail: "Ready", capabilities: [] };
  let listener: (update: SessionUpdate) => void = () => {};
  const session = { snapshot, status, isPending: false,
    dispatch: vi.fn().mockResolvedValue({ accepted: true } as CanduCommandResponse),
    subscribe: (callback: typeof listener) => { listener = callback; callback({ snapshot, status, pending: false, response: null, error: null }); return vi.fn(); } };
  const begin = vi.fn();
  const view = new LauncherView(session, document.body, begin); cleanup.push(() => view.destroy());
  const seed = view.element.querySelector<HTMLInputElement>('input')!;
  seed.value = "42";
  listener({ snapshot, status, pending: true, response: null, error: null });
  expect(seed.value).toBe("42");
  expect(view.element.querySelector<HTMLButtonElement>('[data-action="begin"]')!.getAttribute("aria-disabled")).toBe("true");
  listener({ snapshot, status, pending: false, response: null, error: null });
  view.element.querySelector<HTMLSelectElement>('select')!.value = "useful-fuel-day-v1";
  view.element.querySelector('form')!.dispatchEvent(new SubmitEvent("submit", { cancelable: true }));
  expect(session.dispatch).toHaveBeenCalledWith({ type: "reset", seed: 42, shiftId: "useful-fuel-day-v1" });
  const button = view.element.querySelector<HTMLButtonElement>('[data-action="begin"]')!;
  button.click(); button.click(); expect(begin).toHaveBeenCalledTimes(1);
});

it("keeps launch unavailable without the authoritative core and rejects an invalid seed locally", () => {
  const snapshot = createSnapshot(); snapshot.shift = createShift();
  const dispatch = vi.fn();
  const session = { snapshot, isPending: false, status: { isWasmAvailable: false } as BridgeStatus, dispatch,
    subscribe: (listener: (update: SessionUpdate) => void) => { listener({ snapshot, status: session.status, pending: false, response: null, error: null }); return vi.fn(); } };
  const begin = vi.fn();
  const view = new LauncherView(session, document.body, begin); cleanup.push(() => view.destroy());
  view.element.querySelector<HTMLButtonElement>('[data-action="begin"]')!.click();
  expect(begin).not.toHaveBeenCalled();
  view.element.querySelector<HTMLInputElement>('input')!.value = "-1";
  view.element.querySelector('form')!.dispatchEvent(new SubmitEvent("submit", { cancelable: true }));
  expect(dispatch).not.toHaveBeenCalled();
});
