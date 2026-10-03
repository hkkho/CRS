import { SessionPresentation } from "./SessionPresentation";
import type { CanduCommandResponse } from "./protocol";
import { afterEach, expect, it, vi } from "vitest";
import { AppShell, type DesignerRuntime } from "./AppShell";
import { createSnapshot, createShift } from "./testSnapshot";
import { ReactorHistory } from "./studio/ReactorHistory";
import type { SessionUpdate } from "./sessionController";
import type { BridgeStatus } from "./protocol";
import type { StudioNavigation } from "./studio/StudioView";

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

it("loads Designer only on demand, reuses it and preserves one session/history/navigation", async () => {
  const shared = session();
  let returnToStudio = (_state: StudioNavigation) => {};
  const runtime: DesignerRuntime = { start: vi.fn((_state, onReturn) => { returnToStudio = onReturn; }), stop: vi.fn(), destroy: vi.fn() };
  const load = vi.fn().mockResolvedValue(runtime);
  shell = new AppShell(shared, document.body, load);
  expect(load).not.toHaveBeenCalled(); click('[data-action="begin"]');
  expect(load).not.toHaveBeenCalled();
  const history = shared.history;
  click('[data-action="designer"]');
  await vi.waitFor(() => expect(runtime.start).toHaveBeenCalledTimes(1));
  returnToStudio({ selectedChannelIndex: 210, selectedTab: "power", mapMode: "power" });
  expect(document.querySelector('[data-field="channel"]')!.textContent).toBe("Channel 210");
  expect(document.querySelector('[data-tab="power"]')!.getAttribute("aria-selected")).toBe("true");
  click('[data-action="designer"]'); await vi.waitFor(() => expect(runtime.start).toHaveBeenCalledTimes(2));
  expect(load).toHaveBeenCalledTimes(1); expect(shared.history).toBe(history);
  expect(shared.initializeMode).not.toHaveBeenCalled();
});

it("retains a playable Studio and retries a failed feature download without resetting", async () => {
  const shared = session();
  const runtime: DesignerRuntime = { start: vi.fn(), stop: vi.fn(), destroy: vi.fn() };
  const load = vi.fn().mockRejectedValueOnce(new Error("Download failed")).mockResolvedValue(runtime);
  shell = new AppShell(shared, document.body, load); click('[data-action="begin"]'); click('[data-action="designer"]');
  await vi.waitFor(() => expect(document.querySelector('.designer-loading')!.textContent).toContain("could not open"));
  expect(document.querySelector('.reactor-studio')).not.toBeNull();
  click('.designer-loading button'); await vi.waitFor(() => expect(runtime.start).toHaveBeenCalledTimes(1));
  expect(shared.initializeMode).not.toHaveBeenCalled(); expect(shared.dispatch).not.toHaveBeenCalled();
});

it('restores Studio navigation if the loaded Designer fails to start', async () => {
  const shared = session();
  const runtime: DesignerRuntime = { start: vi.fn(() => { throw new Error('Canvas failed'); }), stop: vi.fn(), destroy: vi.fn() };
  shell = new AppShell(shared, document.body, vi.fn().mockResolvedValue(runtime));
  click('[data-action="begin"]'); click('[data-tab="power"]'); click('[data-action="designer"]');
  await vi.waitFor(() => expect(document.querySelector('.designer-loading')!.textContent).toContain('could not open'));
  expect(document.querySelector('.reactor-studio')).not.toBeNull();
  expect(document.querySelector('[data-tab="power"]')!.getAttribute('aria-selected')).toBe('true');
  expect(runtime.destroy).toHaveBeenCalledTimes(1);
  expect(shared.initializeMode).not.toHaveBeenCalled();
});

it("repeated navigation retains drafts and response summaries with exactly one active subscriber", async () => {
  const base = session();
  const listeners = new Set<(update: SessionUpdate) => void>();
  const presentation = new SessionPresentation();
  const response = { command: { type: "commit-refuel", request: { channelIndex: 210 } }, accepted: true,
    message: "Four bundles refuelled.", snapshot: base.snapshot } as CanduCommandResponse;
  presentation.accept(response, base.snapshot);
  const impact = presentation.impactText;
  const shared = { ...base, presentation, subscribe: (listener: (update: SessionUpdate) => void) => {
    listeners.add(listener); listener({ snapshot: base.snapshot, status: base.status, pending: false, response: null, error: null });
    return () => { listeners.delete(listener); };
  } };
  let navigation: StudioNavigation = {};
  let returnToStudio = (_state: StudioNavigation) => {};
  const runtime: DesignerRuntime = { start: vi.fn((state, onReturn) => { navigation = state; returnToStudio = onReturn; }), stop: vi.fn(), destroy: vi.fn() };
  shell = new AppShell(shared, document.body, vi.fn().mockResolvedValue(runtime));
  click('[data-action="begin"]');
  click('[data-size="8"]'); click('[data-action="direction"]'); click('[data-map="power"]'); click('[data-tab="power"]');
  for (let cycle = 0; cycle < 5; cycle++) {
    expect(listeners.size).toBe(1);
    click('[data-action="designer"]');
    await vi.waitFor(() => expect(runtime.start).toHaveBeenCalledTimes(cycle + 1));
    expect(listeners.size).toBe(0);
    expect(navigation.refuelDraft?.shiftCount).toBe(8);
    expect(navigation.refuelDraft?.directionId).toBe(base.snapshot.core.channels[210].flowDirection === "toward-end-a" ? "toward-end-b" : "toward-end-a");
    expect(navigation.mapMode).toBe("power");
    returnToStudio(navigation);
    expect(listeners.size).toBe(1);
    expect(document.activeElement).toBe(document.querySelector('.reactor-studio'));
    expect(document.querySelector('[data-tab="power"]')!.getAttribute('aria-selected')).toBe('true');
    expect(document.querySelector('[data-field="impact"]')!.textContent).toBe(impact.replaceAll("\n", ""));
    expect(document.querySelector('[data-field="feedback"]')!.textContent).toBe("Four bundles refuelled.");
    expect(document.querySelector('[data-size="8"]')!.getAttribute('aria-pressed')).toBe('true');
  }
  expect(shared.history).toBe(base.history);
  expect(shared.dispatch).not.toHaveBeenCalled();
  shell.destroy(); shell = null;
  expect(listeners.size).toBe(0);
});
