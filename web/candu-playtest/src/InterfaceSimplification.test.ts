import { afterEach, expect, it, vi } from "vitest";
import { StudioView } from "./studio/StudioView";
import { ArcadeView } from "./ArcadeView";
import { createSnapshot, createShift } from "./testSnapshot";
import { ReactorHistory } from "./studio/ReactorHistory";
import type { SessionUpdate } from "./sessionController";

const cleanup: Array<() => void> = [];
afterEach(() => { cleanup.splice(0).forEach(f => f()); document.body.replaceChildren(); });
function setup(arcade = false) {
  const snapshot = createSnapshot(); snapshot.shift = createShift(); snapshot.freshBundlesAvailable = 128; snapshot.pacingMode = "daily-turn"; snapshot.completedDays = 0;
  const status = { source: "wasm" as const, isWasmAvailable: true, title: "Ready", detail: "Ready", capabilities: [] };
  let notify: (update: SessionUpdate) => void = () => {};
  const session = { snapshot, status, isPending: false, dailyPlan: [] as number[], history: new ReactorHistory(), dispatch: vi.fn(), startShift: vi.fn(), stopShift: vi.fn(),
    setDailyPlan: vi.fn((plan: readonly number[]) => { session.dailyPlan = [...plan]; }),
    subscribe: (callback: typeof notify) => { notify = callback; callback({ snapshot, status, pending: false, response: null, error: null }); return () => {}; } };
  const view = arcade ? new ArcadeView(session, document.body, vi.fn()) : new StudioView(session, document.body);
  cleanup.push(() => view.destroy());
  return { session, view, emit: () => notify({ snapshot, status, pending: session.isPending, response: null, error: null }) };
}
function element(selector: string) { return document.querySelector<HTMLElement>(selector.startsWith('[data-channel=') && document.querySelector('.reactor-studio') ? `.studio-map-wrap ${selector}` : selector)!; }
function click(selector: string) { element(selector).dispatchEvent(new MouseEvent("click", { bubbles: true, cancelable: true })); }
function double(selector: string) { element(selector).dispatchEvent(new MouseEvent("dblclick", { bubbles: true, cancelable: true })); }

it("inspects with click and drafts once per double-click without consuming time or fuel", () => {
  const { session } = setup(); const before = [session.snapshot.freshBundlesAvailable, session.snapshot.simulationTimeSeconds];
  click('[data-channel="0"]'); expect(session.dailyPlan).toEqual([]);
  double('[data-channel="0"]'); expect(session.dailyPlan).toEqual([0]);
  expect(document.querySelectorAll('[data-plan-list] li')).toHaveLength(1);
  double('[data-channel="0"]'); expect(session.dailyPlan).toEqual([]);
  expect(session.dispatch).not.toHaveBeenCalled();
  expect([session.snapshot.freshBundlesAvailable, session.snapshot.simulationTimeSeconds]).toEqual(before);
});

it("supports keyboard toggles, row inspection and × removal with stable focus", () => {
  const { session } = setup();
  element('[data-channel="0"]').dispatchEvent(new KeyboardEvent("keydown", { key: "Enter", bubbles: true, cancelable: true }));
  expect(session.dailyPlan).toEqual([0]);
  click('[data-channel="1"]'); double('[data-channel="1"]');
  click('.fuel-sheet-inspect[data-plan-channel="0"]');
  expect(element('[data-field="channel"]').textContent).toBe("A01");
  expect(session.dailyPlan).toEqual([0, 1]);
  element('.fuel-sheet-remove[data-plan-channel="0"]').focus(); click('.fuel-sheet-remove[data-plan-channel="0"]');
  expect(session.dailyPlan).toEqual([1]);
  expect(document.activeElement).toBe(element('.fuel-sheet-remove[data-plan-channel="1"]'));
});

it("blocks direct drafting when ineligible, over budget, pending, unavailable or terminal", () => {
  const { session, emit } = setup();
  session.snapshot.core.channels[0].canRefuel = false; double('[data-channel="0"]'); expect(session.dailyPlan).toEqual([]);
  session.snapshot.core.channels[0].canRefuel = true; session.snapshot.freshBundlesAvailable = 0; emit();
  double('[data-channel="0"]'); expect(session.dailyPlan).toEqual([]);
  session.snapshot.freshBundlesAvailable = 128; session.isPending = true; emit(); double('[data-channel="0"]');
  session.isPending = false; session.status.isWasmAvailable = false; emit(); double('[data-channel="0"]');
  session.status.isWasmAvailable = true; session.snapshot.runStatus = "completed"; emit(); double('[data-channel="0"]');
  expect(session.dailyPlan).toEqual([]); expect(session.dispatch).not.toHaveBeenCalled();
});

it("gives the sheet and tabbed inspection the side column with permanent history controls", () => {
  setup(); expect(document.querySelector('.studio-inspector')).toBeNull();
  expect(element('.studio-side-column').firstElementChild).toBe(element('.fuel-sheet'));
  expect(element('.studio-side-column').contains(element('.studio-zones'))).toBe(true);
  expect(element('.studio-inspection-dock').contains(element('[data-field="movement-plan"]'))).toBe(true);
  expect(document.querySelectorAll('[data-axial="power"]')).toHaveLength(1);
  expect(document.querySelector('[data-field="stock"]')).toBeNull();
  expect(element('.studio-history-navigation').tagName).toBe('DIV');
  expect(element('.studio-history-navigation').contains(element('[data-history="inspector"]'))).toBe(true);
  expect(document.querySelector('.reactor-locator')).toBeNull();
  expect(document.querySelector('.studio-lzc-label')).toBeNull();
});

it("keeps one Arcade sheet and its row identity through review, with direct pointer and keyboard drafting", () => {
  const { session } = setup(true);
  click('[data-channel="0"]'); double('[data-channel="0"]'); expect(session.dailyPlan).toEqual([0]);
  const row = element('[data-plan-list] li'); click('[data-action="commit"]');
  expect(element('[data-plan-list] li')).toBe(row); expect(document.querySelectorAll('[data-plan-list]')).toHaveLength(1);
  expect(document.querySelector('.arcade-plan')).toBeNull();
  expect((element('.fuel-sheet-remove') as HTMLButtonElement).disabled).toBe(true);
  expect(element('[data-action="clear"]').hidden).toBe(true);
  click('[data-action="back"]');
  element('[data-channel="0"]').dispatchEvent(new KeyboardEvent("keydown", { key: " ", bubbles: true, cancelable: true }));
  expect(session.dailyPlan).toEqual([]); expect(session.dispatch).not.toHaveBeenCalled();
});


it("switches the shared details dock with pointer and keyboard without dispatching commands", () => {
  const { session } = setup();
  click('[data-inspection-tab="fuel"]');
  expect(element('[data-inspection-panel="fuel"]').hidden).toBe(false);
  expect(element('[data-inspection-panel="zones"]').hidden).toBe(true);
  element('[data-inspection-tab="fuel"]').dispatchEvent(new KeyboardEvent("keydown", { key: "ArrowRight", bubbles: true, cancelable: true }));
  expect(element('[data-inspection-panel="profiles"]').hidden).toBe(false);
  expect(document.activeElement).toBe(element('[data-inspection-tab="profiles"]'));
  expect(session.dispatch).not.toHaveBeenCalled();
  expect(session.dailyPlan).toEqual([]);
});
