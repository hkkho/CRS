import { afterEach, expect, it, vi } from "vitest";
import { FirstShiftGuide } from "./FirstShiftGuide";
import { createSnapshot } from "./testSnapshot";
import { StudioView } from "./studio/StudioView";
import { ReactorHistory } from "./studio/ReactorHistory";
import type { SessionUpdate } from "./sessionController";

afterEach(() => document.body.replaceChildren());
it("follows selection, drafting and an authoritative day result without sending commands", () => {
  const s = createSnapshot(); s.completedDays = 0; s.runStatus = "paused";
  const burnup = vi.fn(), closed = vi.fn();
  const guide = new FirstShiftGuide(burnup, closed); document.body.append(guide.element);
  guide.update(s, [], false, false); expect(guide.element.hidden).toBe(true);
  guide.start(s); guide.update(s, [], false, false); expect(guide.element.dataset.step).toBe("0");
  guide.element.querySelector<HTMLButtonElement>('[data-guide="burnup"]')!.click(); expect(burnup).toHaveBeenCalledOnce();
  guide.inspect(); guide.update(s, [], false, false); expect(guide.element.dataset.step).toBe("1");
  guide.update(s, [210], false, false); expect(guide.element.dataset.step).toBe("2");
  guide.update(s, [], false, false); expect(guide.element.dataset.step).toBe("1");
  s.completedDays = 1; guide.update(s, [], false, false); expect(guide.element.dataset.step).toBe("3");
  guide.element.querySelector<HTMLButtonElement>('[data-guide="skip"]')!.click();
  expect(guide.element.hidden).toBe(true); expect(closed).toHaveBeenCalledOnce(); guide.destroy();
});

it("keeps guided entry visible when subscribing receives the launch reset, then closes it on a later reset", () => {
  const snapshot = createSnapshot(); snapshot.pacingMode = "daily-turn";
  const status = { source: "wasm" as const, isWasmAvailable: true, title: "Ready", detail: "Ready", capabilities: [] };
  const response = { protocol: "candu-playtest-v2" as const, accepted: true, sequence: 1, command: { type: "reset" as const }, message: "Reset", diagnostics: [], stateDigest: "test", snapshot };
  let notify: (update: SessionUpdate) => void = () => {};
  const session = { snapshot, status, isPending: false, history: new ReactorHistory(), dispatch: vi.fn(),
    subscribe: (listener: typeof notify) => { notify = listener; listener({ snapshot, status, pending: false, response, error: null }); return () => {}; } };
  const view = new StudioView(session, document.body, { guided: true });
  expect(view.element.querySelector<HTMLElement>(".first-shift-guide")!.hidden).toBe(false);
  notify({ snapshot, status, pending: false, response: { ...response, sequence: 2 }, error: null });
  expect(view.element.querySelector<HTMLElement>(".first-shift-guide")!.hidden).toBe(true);
  view.destroy();
});
