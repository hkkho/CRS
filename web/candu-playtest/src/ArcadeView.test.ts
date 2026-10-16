import { afterEach, expect, it, vi } from "vitest";
import { ArcadeView } from "./ArcadeView";
import { createSnapshot, createShift } from "./testSnapshot";
import { ReactorHistory } from "./studio/ReactorHistory";
import type { SessionUpdate } from "./sessionController";

let view: ArcadeView;
afterEach(() => { view?.destroy(); document.body.replaceChildren(); });
function setup() {
  const snapshot = createSnapshot(); snapshot.shift = createShift(); snapshot.pacingMode = "daily-turn"; snapshot.completedDays = 0;
  const status = { source: "wasm" as const, isWasmAvailable: true, title: "Ready", detail: "Ready", capabilities: [] };
  let notify: (update: SessionUpdate) => void = () => {};
  const unsubscribe = vi.fn();
  const session = { snapshot, status, isPending: false, history: new ReactorHistory(), startShift: vi.fn(), stopShift: vi.fn(), setDailyPlan: vi.fn(),
    dispatch: vi.fn().mockResolvedValue({ accepted: false, message: "Rejected plan" }),
    subscribe: (listener: typeof notify) => { notify = listener; listener({ snapshot, status, pending: false, response: null, error: null }); return unsubscribe; } };
  view = new ArcadeView(session, document.body, vi.fn());
  return { session, snapshot, unsubscribe, emit: () => notify({ snapshot, status, pending: false, response: null, error: null }) };
}
function button(selector: string) { return document.querySelector<HTMLButtonElement>(selector)!; }

it("inspects any map channel, explicitly adds it, and submits the shared daily command", async () => {
  const { session } = setup();
  button('[data-channel="0"]').click();
  expect(session.setDailyPlan).not.toHaveBeenCalled();
  expect(document.querySelectorAll('.arcade-map [data-channel]').length).toBe(380);
  button('[data-action="toggle-channel"]').click();
  expect(button('[data-channel="0"]').getAttribute("aria-pressed")).toBe("true");
  expect(button('[data-channel="0"]').dataset.planned).toBe("true");
  button('[data-channel="379"]').click();
  expect(button('[data-action="toggle-channel"]').disabled).toBe(true);
  button('[data-channel="0"]').click();
  button('[data-action="commit"]').click();
  expect(session.dispatch).not.toHaveBeenCalled();
  expect((document.querySelector('[data-picker]') as HTMLElement).hidden).toBe(true);
  button('[data-action="back"]').click();
  expect(button('[data-channel="0"]').getAttribute("aria-pressed")).toBe("true");
  button('[data-action="commit"]').click();
  button('[data-action="confirm"]').click();
  expect(session.dispatch).toHaveBeenCalledWith({ type: "commit-day", expectedCompletedDays: 0, channelIndices: [0] });
  await vi.waitFor(() => expect(button('[data-action="commit"]').disabled).toBe(false));
  expect(button('[data-channel="0"]').getAttribute("aria-pressed")).toBe("true");
  expect(document.querySelector('[data-message]')!.textContent).toBe("Rejected plan");
});

it("uses the published outcome rather than treating a surviving missed goal as a win", () => {
  const { snapshot, emit } = setup();
  snapshot.runStatus = "completed"; snapshot.shift!.outcome = "missed";
  emit();
  expect(document.querySelector('[data-outcome]')!.textContent).toContain("Goal missed");
  expect(button('[data-action="commit"]').disabled).toBe(true);
  expect((document.querySelector('[data-picker]') as HTMLElement).hidden).toBe(true);
  expect((document.querySelector('footer') as HTMLElement).hidden).toBe(true);
  snapshot.shift!.outcome = "success"; snapshot.shift!.rewardEarned = true; emit();
  expect(document.querySelector('[data-outcome]')!.textContent).toBe("Challenge cleared!");
});

it("lets players inspect an ineligible channel but blocks adding it", () => {
  const { snapshot, emit, session } = setup();
  snapshot.core.channels[379].canRefuel = false;
  snapshot.core.channels[379].refuellingIneligibilityReason = "No fuel in this channel.";
  emit();
  button('[data-channel="379"]').click();
  expect(button('[data-channel="379"]').getAttribute("aria-pressed")).toBe("true");
  expect(document.querySelector('[data-eligibility]')!.textContent).toBe("No fuel in this channel.");
  expect(button('[data-action="toggle-channel"]').disabled).toBe(true);
  expect(session.setDailyPlan).not.toHaveBeenCalled();
});

it("reviews a no-fuel day explicitly and keeps focus when removing a pick", () => {
  const { session } = setup();
  button('[data-channel="0"]').click();
  button('[data-action="toggle-channel"]').click();
  button('[data-action="commit"]').click();
  button('[data-remove="0"]').click();
  expect(document.activeElement).toBe(button('[data-action="back"]'));
  expect(document.querySelector('[data-plan]')!.textContent).toContain("No fuel selected");
  expect(session.dispatch).not.toHaveBeenCalled();
  button('[data-action="confirm"]').click();
  expect(session.dispatch).toHaveBeenCalledWith({ type: "commit-day", expectedCompletedDays: 0, channelIndices: [] });
});

it("retries the exact seed and objective and releases its subscription", () => {
  const { session, unsubscribe } = setup();
  button('[data-action="retry"]').click();
  expect(session.dispatch).toHaveBeenCalledWith({ type: "reset", seed: 42, shiftId: "useful-fuel-day-v1" });
  view.destroy(); expect(unsubscribe).toHaveBeenCalledOnce();
});
