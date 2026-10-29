import { afterEach, expect, it, vi } from "vitest";
import { LauncherView } from "./LauncherView";
import { createSnapshot, createShift } from "./testSnapshot";
import type { BridgeStatus, CanduCommandResponse } from "./protocol";
import type { SessionUpdate } from "./sessionController";

const cleanup: Array<() => void> = [];
afterEach(() => { cleanup.splice(0).forEach(destroy => destroy()); document.body.replaceChildren(); });

it.each(["free-practice", "guided"])("launches %s only after a successful shared reset", async choice => {
  const snapshot = createSnapshot(); snapshot.shift = createShift();
  const status: BridgeStatus = { source: "wasm", isWasmAvailable: true, title: "Ready", detail: "Ready", capabilities: [] };
  const dispatch = vi.fn().mockResolvedValueOnce({ accepted: false, message: "Reset rejected" }).mockResolvedValue({ accepted: true });
  const session = { snapshot, status, isPending: false, dispatch,
    subscribe: (callback: (update: SessionUpdate) => void) => { callback({ snapshot, status, pending: false, response: null, error: null }); return vi.fn(); } };
  const begin = vi.fn(); const view = new LauncherView(session, document.body, begin); cleanup.push(() => view.destroy());
  if (choice === "guided") view.element.querySelector<HTMLInputElement>("[data-guided]")!.checked = true;
  else view.element.querySelector<HTMLButtonElement>(`[data-goal="${choice}"]`)!.click();
  expect(dispatch).not.toHaveBeenCalled();
  const button = view.element.querySelector<HTMLButtonElement>('[data-action="begin"]')!;
  button.click(); await vi.waitFor(() => expect(view.element.querySelector('[data-field="error"]')!.textContent).toBe("Reset rejected"));
  expect(begin).not.toHaveBeenCalled(); button.click();
  await vi.waitFor(() => expect(begin).toHaveBeenCalledWith(choice === "guided"));
  expect(dispatch).toHaveBeenLastCalledWith({ type: "reset", seed: choice === "guided" ? 1001 : 42, shiftId: "free-practice" });
});

it("launches once, preserves seed drafts during notifications and sends the selected seed/objective", async () => {
  const snapshot = createSnapshot(); snapshot.shift = createShift();
  const status: BridgeStatus = { source: "wasm", isWasmAvailable: true, title: "Ready", detail: "Ready", capabilities: [] };
  let listener: (update: SessionUpdate) => void = () => {};
  const session = { snapshot, status, isPending: false,
    dispatch: vi.fn().mockResolvedValue({ accepted: true } as CanduCommandResponse),
    subscribe: (callback: typeof listener) => { listener = callback; callback({ snapshot, status, pending: false, response: null, error: null }); return vi.fn(); } };
  const begin = vi.fn();
  const view = new LauncherView(session, document.body, begin); cleanup.push(() => view.destroy());
  const seed = view.element.querySelector<HTMLInputElement>('[data-field=seed]')!;
  seed.value = "42";
  listener({ snapshot, status, pending: true, response: null, error: null });
  expect(seed.value).toBe("42");
  expect(view.element.querySelector<HTMLButtonElement>('[data-action="begin"]')!.getAttribute("aria-disabled")).toBe("true");
  listener({ snapshot, status, pending: false, response: null, error: null });
  view.element.querySelector<HTMLButtonElement>('[data-goal="free-practice"]')!.click();
  const button = view.element.querySelector<HTMLButtonElement>('[data-action="begin"]')!;
  button.click(); button.click();
  await vi.waitFor(() => expect(begin).toHaveBeenCalledTimes(1));
  expect(session.dispatch).toHaveBeenCalledWith({ type: "reset", seed: 42, shiftId: "free-practice" });

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
  view.element.querySelector<HTMLInputElement>('[data-field=seed]')!.value = "-1";
  view.element.querySelector<HTMLButtonElement>('[data-action="begin"]')!.click();
  expect(dispatch).not.toHaveBeenCalled();
});
