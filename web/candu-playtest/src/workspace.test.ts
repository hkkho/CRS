import { afterEach, expect, it, vi } from "vitest";
import { LauncherView } from "./LauncherView";
import { DesignerControls } from "./DesignerControls";
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

it("exposes Designer actions and keeps inspector focus through authoritative updates", () => {
  const snapshot = createSnapshot();
  const actions = { selectChannel: vi.fn(), selectPosition: vi.fn(), toggleFuel: vi.fn(), toggleFace: vi.fn(), solve: vi.fn(), zones: vi.fn(), back: vi.fn() };
  const view = new DesignerControls(document.body, actions); cleanup.push(() => view.destroy());
  view.update(snapshot, 189, 0, false, true, "Ready");
  const channel = view.element.querySelector<HTMLSelectElement>('[data-field="channel"]')!;
  channel.focus(); channel.value = "190"; channel.dispatchEvent(new Event("change", { bubbles: true }));
  expect(actions.selectChannel).toHaveBeenCalledWith(190);
  view.update(snapshot, 190, 0, true, true, "Ready");
  expect(document.activeElement).toBe(channel);
  channel.value = "191"; channel.dispatchEvent(new Event("change", { bubbles: true }));
  expect(channel.value).toBe("190"); expect(actions.selectChannel).toHaveBeenCalledTimes(1);
  expect(view.element.querySelector<HTMLButtonElement>('[data-action="fuel"]')!.disabled).toBe(true);
  snapshot.core.channels[190]!.bundles[0]!.reflectiveFaces = ["north"];
  view.update(snapshot, 190, 0, false, true, "Accepted");
  expect(view.element.querySelector('[data-face="north"]')!.getAttribute("aria-pressed")).toBe("true");
  view.element.querySelector<HTMLButtonElement>('[data-face="north"]')!.click();
  expect(actions.toggleFace).toHaveBeenCalledWith("north");
  for (const [action, callback] of [["fuel", actions.toggleFuel], ["solve", actions.solve], ["zones", actions.zones], ["back", actions.back]] as const) {
    view.element.querySelector<HTMLButtonElement>(`[data-action="${action}"]`)!.click(); expect(callback).toHaveBeenCalledTimes(1);
  }
});
