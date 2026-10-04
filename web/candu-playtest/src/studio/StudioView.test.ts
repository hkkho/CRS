import { afterEach, describe, expect, it, vi } from "vitest";
import { StudioView } from "./StudioView";
import { createUnavailableRrsSnapshot, type BridgeStatus, type CanduChannelSnapshot, type CanduCommand, type CanduCommandResponse, type CanduSnapshot } from "../protocol";
import type { SessionUpdate } from "../sessionController";
import { ReactorHistory } from "./ReactorHistory";

import { createShift } from "../testSnapshot";

const views: StudioView[] = [];
afterEach(() => { views.splice(0).forEach(view => view.destroy()); document.body.replaceChildren(); });

function channel(index: number, column: number, burnup: number): CanduChannelSnapshot {
  return { channelIndex: index, gridColumn: column, gridRow: 10, flowDirection: "toward-end-b",
    localPowerFraction: 1, localTiltFraction: 0, averageBurnupMwdPerKg: burnup,
    bundles: Array.from({ length: 12 }, (_, position) => ({ position, hasFuel: true, isFresh: false,
      powerWatts: 1000, currentBurnupMwdPerKg: burnup })) } as CanduChannelSnapshot;
}

function harness() {
  let listener: ((update: SessionUpdate) => void) | null = null;
  const rrs = createUnavailableRrsSnapshot();
  rrs.averageFillFraction = 0.58;
  rrs.averageFillFraction = 0.58;
  rrs.zones = Array.from({ length: 14 }, (_, logicalZoneId) => ({ logicalZoneId, fillFraction: 0.58,
    referencePowerFraction: 0, targetPowerFraction: 0, measuredPowerFraction: 0, shapeError: 0 }));
  const snapshot = { sequence: 0, simulationTimeSeconds: 0, scoreTotal: 0, isPaused: true,
    freshBundlesAvailable: 128, refuellingOperationCount: 0, rrsReserveFraction: 0.84,
    targetPowerFraction: 1, axialTiltFraction: 0, playbackModeId: "pause", rrs,
    source: "wasm", physics: { isAuthoritative: true, effectiveK: 1, totalPowerWatts: 1000,
      reactivity: 0, actualPowerFraction: 1, targetPowerWatts: 1000, referencePowerWatts: 1000 },
    core: { channels: [channel(210, 10, 6), { ...channel(211, 11, 8), flowDirection: "toward-end-a" }] } } as CanduSnapshot;
  const status: BridgeStatus = { source: "wasm", title: "Ready", detail: "Ready", isWasmAvailable: true, capabilities: [] };
  const session = {
    snapshot, status, isPending: false, history: new ReactorHistory(),
    subscribe: (callback: (update: SessionUpdate) => void) => {
      listener = callback; session.history.record(session.snapshot);
      callback({ snapshot: session.snapshot, status, pending: session.isPending, response: null, error: null });
      return () => { listener = null; };
    },
    dispatch: vi.fn(async (command: CanduCommand) => ({ command, accepted: true }) as CanduCommandResponse),
  };
  const navigate = vi.fn();
  const view = new StudioView(session, document.body, navigate);
  views.push(view);
  const emit = (response: CanduCommandResponse | null = null, changeKind?: SessionUpdate['changeKind'], pace?: SessionUpdate['pace']) => {
    session.history.record(session.snapshot);
    listener?.({ snapshot: session.snapshot, status, pending: session.isPending, response, error: null, changeKind, pace });
  };
  const button = (action: string) => view.element.querySelector<HTMLButtonElement>(`button[data-action="${action}"]`)!;
  return { session, view, emit, button, navigate };
}

describe("Reactor Studio live interface", () => {
  it("shows average LZC level and global tilt limits, with blue low power and red high power", () => {
    const { session, view, emit } = harness();
    session.snapshot.core.channels[0].localPowerFraction = 0.4;
    session.snapshot.core.channels[1].localPowerFraction = 2.5;
    view.element.querySelector<HTMLButtonElement>('[data-map="power"]')!.click();
    expect(view.element.querySelector('rect[data-channel="210"]')!.getAttribute("fill")).toBe("rgb(37, 99, 235)");
    expect(view.element.querySelector('rect[data-channel="211"]')!.getAttribute("fill")).toBe("rgb(220, 38, 38)");
    expect(view.element.querySelector('[data-field="reserve"]')!.textContent).toBe("58.0%");
    expect(view.element.textContent).toContain("LZC AVERAGE LEVEL");
    expect(view.element.textContent).toContain("Limit ±20%");
    expect(view.element.querySelector('[data-action="size"]')).toBeNull();
    expect(view.element.querySelector('[data-action="direction"]')).toBeNull();
    session.snapshot.rrs.averageFillFraction = .91; emit();
    expect(view.element.querySelector('[data-field="status"]')!.textContent).toBe("attention");
  });

  it("keeps the refuel label and clock text stable while calculation status toggles", () => {
    const { session, view, emit, button } = harness();
    emit(null, "status", { requested: "1×", simulatedMinutesPerSecond: 3, solving: false });
    const label = button("refuel").textContent;
    const pace = view.element.querySelector('[data-field="pace"]')!.textContent;
    session.isPending = true; emit(null, "status", { requested: "1×", simulatedMinutesPerSecond: 3, solving: true });
    expect(button("refuel").textContent).toBe(label);
    expect(view.element.querySelector('[data-field="pace"]')!.textContent).toBe(pace);
    expect(view.element.querySelector('[data-field="pace"]')!.textContent).not.toContain("Solving");
    expect(button("refuel").disabled).toBe(true);
  });

  it('leaves map and axial nodes intact on status updates and patches changed readings', () => {
    const { session, view, emit, button } = harness();
    const bundle = view.element.querySelector('[data-axial="power"]')!;
    const map = view.element.querySelector('rect[data-channel="210"]')!;
    button('refuel').focus();
    const observer = new MutationObserver(() => {}); observer.observe(map, { subtree: true, attributes: true, childList: true });
    session.isPending = true; emit(null, 'status');
    expect(view.element.querySelector('[data-axial="power"]')).toBe(bundle);
    expect(observer.takeRecords()).toHaveLength(0);
    expect(document.activeElement).toBe(button('refuel'));
    session.isPending = false;
    session.snapshot.core.channels[0].bundles[0].powerWatts = 2000;
    emit();
    expect(view.element.querySelector('[data-axial="power"]')).toBe(bundle);
    expect(bundle.textContent).toContain('2.00');
    observer.disconnect();
  });
  it("keeps arrows on unrelated buttons from selecting a channel or stealing focus", () => {
    const { view, button } = harness();
    const selected = view.element.querySelector('[data-field="channel"]')!.textContent;
    button("oldest").focus();
    button("oldest").dispatchEvent(new KeyboardEvent("keydown", { key: "ArrowRight", bubbles: true, cancelable: true }));
    expect(document.activeElement).toBe(button("oldest"));
    expect(view.element.querySelector('[data-field="channel"]')!.textContent).toBe(selected);
  });
  it("labels modified results and excludes a standard reward while preserving reset", () => {
    const { session, view, emit, button } = harness();
    session.snapshot.provenance = { kind: "modified-sandbox", label: "Modified sandbox", isModified: true,
      eligibleForStandardChallenge: false, reasons: ["Zone geometry edited"] };
    session.snapshot.shift = { ...createShift(), outcome: "success", rewardEarned: false };
    session.snapshot.runStatus = "completed";
    emit();
    expect(view.element.querySelector('[data-field="provenance"]')!.textContent).toContain("Zone geometry edited");
    expect(view.element.querySelector('[data-field="ending-title"]')!.textContent).toContain("Sandbox");
    expect(view.element.querySelector('[data-field="ending-reward"]')!.textContent).toContain("excluded");
    expect(button("retry").disabled).toBe(false);
    session.snapshot.provenance = { kind: "standard-challenge", label: "Standard challenge", isModified: false,
      eligibleForStandardChallenge: true, reasons: [] };
    session.snapshot.runStatus = "paused"; emit();
    expect(view.element.querySelector('[data-field="provenance"]')!.textContent).toBe("Standard challenge");
  });

  it("uses published controller reasons even when fills do not move", () => {
    const { session, view, emit } = harness();
    session.snapshot.rrs.decisionCode = "retained-best";
    session.snapshot.rrs.decisionExplanation = "Retained previous fills; no better admissible move was accepted.";
    session.snapshot.rrs.limitingZoneId = 3;
    emit();
    const note = view.element.querySelector('[data-field="zone-note"]')!;
    expect(note.textContent).toContain("no better admissible move");
    expect(note.textContent).toContain("Z4 has least headroom: 58.0% drain / 42.0% fill room");
    expect(view.element.querySelectorAll('[data-limiting="true"]')).toHaveLength(1);
    session.snapshot.rrs.decisionCode = "exhausted-empty";
    session.snapshot.rrs.decisionExplanation = "Regulating reserve exhausted: all zones are empty.";
    emit(); expect(note.textContent).toContain("exhausted");
    expect(note.getAttribute("data-decision")).toBe("exhausted-empty");
  });

  it("excludes ineligible candidates and explains a selected nonfuel channel before dispatch", () => {
    const { session, view, emit, button } = harness();
    session.snapshot.core.channels[1].canRefuel = false;
    session.snapshot.core.channels[1].refuellingIneligibilityReason = "Restore every cell to fuel in Core Designer.";
    emit(); button("oldest").click();
    expect(view.element.querySelector('[data-field="channel"]')!.textContent).toBe("Channel 210");
    view.element.querySelector<SVGRectElement>('rect[data-channel="211"]')!.dispatchEvent(new MouseEvent("click", { bubbles: true }));
    expect(button("refuel").disabled).toBe(true);
    expect(view.element.querySelector('[data-field="order-note"]')!.textContent).toContain("Restore every cell");
    button("refuel").click(); expect(session.dispatch).not.toHaveBeenCalled();
  });

  it("renders authoritative movement plans and keeps confirmed identities on navigation", () => {
    const { session, view, emit } = harness();
    const plan = { directionId: "toward-end-b" as const, shiftCount: 8 as const, incomingEnd: "A" as const, outgoingEnd: "B" as const,
      insertedPositions: [0,1,2,3,4,5,6,7], dischargedPositions: [4,5,6,7,8,9,10,11], retainedFromPositions: [0,1,2,3], retainedToPositions: [8,9,10,11] };
    session.snapshot.refuellingPlans = [plan];
    session.snapshot.lastFuelMovement = { operationId: 1, channelIndex: 210, plan,
      score: { policyId: "test", dischargeReward: 12, freshFuelCost: 6, netPoints: 6 },
      bundles: [{ bundleId: "old-identity", beforePosition: 8, afterPosition: null, burnupMwdPerKg: 8 },
        { bundleId: "fresh-identity", beforePosition: null, afterPosition: 0, burnupMwdPerKg: 0 }] };
    const animate = vi.fn();
    const original = HTMLElement.prototype.animate;
    HTMLElement.prototype.animate = animate;
    const response = { sequence: 1, accepted: true, message: "Moved", snapshot: session.snapshot,
      command: { type: "commit-refuel", request: { channelIndex: 210, shiftCount: 8, directionId: "toward-end-b", fuelTypeId: "NAT-U-SYNTHETIC" } } } as CanduCommandResponse;
    emit({ ...response, accepted: false });
    expect(animate).not.toHaveBeenCalled();
    emit({ ...response, sequence: 2 });
    expect(animate).toHaveBeenCalledTimes(2);
    emit({ ...response, sequence: 3 });
    expect(animate).toHaveBeenCalledTimes(2);
    HTMLElement.prototype.animate = original;
    expect(view.element.querySelector('[data-field="movement-plan"]')!.textContent).toContain("positions 5, 6, 7, 8, 9, 10, 11, 12 leave End B");
    expect(view.element.querySelectorAll('[data-movement="outgoing"]')).toHaveLength(8);
    const outgoing = view.element.querySelector('[data-movement="outgoing"]');
    session.snapshot.core.channels[0].bundles[4].currentBurnupMwdPerKg += .1;
    emit();
    expect(view.element.querySelector('[data-movement="outgoing"]')).toBe(outgoing);
    expect(view.element.querySelector('[data-field="movement-result"]')!.textContent).toContain("old-identity: position 9 → discharged (8.00");
    const details = view.element.querySelector<HTMLDetailsElement>('[data-field="movement-result"] details')!;
    details.open = true;
    emit(null, 'status');
    expect(view.element.querySelector('[data-field="movement-result"] details')).toBe(details);
    expect(details.open).toBe(true);
    view.destroy();
    const returned = new StudioView(session, document.body, vi.fn()); views.push(returned);
    expect(returned.element.querySelector('[data-field="movement-result"]')!.textContent).toContain("Confirmed move #1");
  });

  it("shows supplied objective progress and cumulative report, then retries with the same or next seed", () => {
    const { session, view, emit, button } = harness();
    session.snapshot.shift = createShift();
    emit();
    expect(view.element.querySelector('[data-field="objective-progress"]')!.textContent).toBe("3 / 8 useful bundles");
    expect(view.element.querySelector('[data-field="remaining"]')!.textContent).toContain("20h 00m remaining");
    expect(view.element.querySelector('[data-field="remaining"]')!.textContent).toContain("128 / 128");
    expect(view.element.querySelector<HTMLElement>('[data-field="ending"]')!.hidden).toBe(true);
    session.snapshot.runStatus = "completed";
    session.snapshot.shift.outcome = "missed";
    session.snapshot.runEndReason = "Challenge day completed";
    emit();
    expect(view.element.querySelector('[data-field="ending-title"]')!.textContent).toBe("Objective missed");
    expect(view.element.querySelector('[data-field="energy"]')!.textContent).toContain("2,600 MWh electric (estimate)");
    expect(view.element.querySelector('[data-field="score-components"]')!.textContent).toBe("Operating 400.0 + discharge 36.0 − fuel cost 12.0");
    expect(document.activeElement).toBe(view.element.querySelector('[data-field="ending-title"]'));
    button("retry").click();
    expect(session.dispatch).toHaveBeenLastCalledWith({ type: "reset" });
    button("new-seed").click();
    expect(session.dispatch).toHaveBeenLastCalledWith({ type: "reset", seed: 43 });
    session.snapshot.runStatus = "running";
    session.snapshot.shift = createShift();
    emit();
    expect(view.element.querySelector<HTMLElement>('[data-field="ending"]')!.hidden).toBe(true);
    button("challenge").click();
    expect(session.dispatch).toHaveBeenLastCalledWith({ type: "reset", shiftId: "free-practice" });
  });

  it("announces the earned badge supplied by the shared game", () => {
    const { session, view, emit } = harness();
    session.snapshot.shift = { ...createShift(), outcome: "success", rewardEarned: true };
    session.snapshot.runStatus = "completed";
    emit();
    expect(view.element.querySelector('[data-field="ending-reward"]')!.textContent).toBe("Earned: Efficient refueller badge");
  });

  it("shows horizon completion and leaves only reset and inspection available", () => {
    const { session, view, emit, button } = harness();
    session.snapshot.runStatus = "completed";
    session.snapshot.runEndReason = "Practice horizon completed";
    emit();
    for (const action of ["refuel", "pause", "step", "target"]) expect(button(action).disabled).toBe(true);
    expect(button("reset").disabled).toBe(false);
    expect(view.element.textContent).toContain("Practice horizon completed");
  });
  it("clears feedback on a sequence-reset and handles the first new fuel move once", () => {
    const { session, view, emit } = harness();
    const move = (sequence: number, stock: number) => {
      session.snapshot = { ...session.snapshot, sequence, freshBundlesAvailable: stock };
      return { sequence, accepted: true, message: "Fuel moved.", snapshot: session.snapshot,
        command: { type: "commit-refuel", request: { channelIndex: 210, shiftCount: 8,
          directionId: "toward-end-b", fuelTypeId: "NAT-U-SYNTHETIC" } } } as CanduCommandResponse;
    };
    emit(move(20, 120));
    session.snapshot = { ...session.snapshot, sequence: 1, freshBundlesAvailable: 128 };
    const reset = { sequence: 1, accepted: true, snapshot: session.snapshot,
      command: { type: "reset" } } as CanduCommandResponse;
    emit(reset);
    expect(view.element.querySelector('[data-field="impact"]')!.textContent).toContain("first fuel move");
    expect(view.element.dataset.result).toBeUndefined();
    const response = move(2, 120);
    emit(response);
    const impact = view.element.querySelector('[data-field="impact"]')!.textContent;
    expect(impact).toContain("128 → 120");
    emit(response);
    expect(view.element.querySelector('[data-field="impact"]')!.textContent).toBe(impact);
  });
  it("uses square channel cells and plots axial measurements in physical position order", () => {
    const { session, view, emit } = harness();
    const bundles = session.snapshot.core.channels[0].bundles.map(bundle => ({ ...bundle,
      powerWatts: (bundle.position + 1) * 1000, currentBurnupMwdPerKg: bundle.position * .5 }));
    bundles[5].hasFuel = false;
    session.snapshot.core.channels[0].bundles = bundles.reverse();
    emit();
    expect(view.element.querySelectorAll("rect[data-channel]")).toHaveLength(2);
    expect(view.element.querySelectorAll("circle[data-channel]")).toHaveLength(0);
    const power = view.element.querySelector('[data-axial="power"]')!;
    expect(power.querySelector("desc")!.textContent).toContain("1: 1.00; 2: 2.00");
    expect(power.querySelector("desc")!.textContent).toContain("6: empty/unavailable");
    expect(power.querySelector(".axial-line")!.getAttribute("d")!.match(/M/g)).toHaveLength(2);
    expect(view.element.querySelector('[data-axial="burnup"] desc')!.textContent).toContain("12: 5.50");
    expect(view.element.querySelectorAll('[data-axial-position]')).toHaveLength(22);
  });
  it("displays authoritative electrical and thermal power with explicit units", () => {
    const { session, view, emit } = harness();
    session.snapshot.physics.electricalPowerWatts = 650_000_000;
    session.snapshot.physics.totalPowerWatts = 2_064_000_000;
    emit();
    expect(view.element.querySelector('[data-field="power-rating"]')?.textContent)
      .toBe("650 MW electric · 2064 MW thermal");
  });

  it("uses Studio exclusively and transfers channel and draft to Core Designer", () => {
    const { session, button, view, navigate } = harness();
    button("oldest").click();
    button("oldest").click();
    button("refuel").click();
    expect(session.dispatch).toHaveBeenCalledWith({ type: "commit-refuel", request: {
      channelIndex: 211, shiftCount: 8, directionId: "toward-end-a", fuelTypeId: "NAT-U-SYNTHETIC",
    } });
    expect(view.element.querySelector('[data-action="classic"]')).toBeNull();
    button("designer").click();
    expect(navigate).toHaveBeenCalledWith("CoreDesignerScene", { selectedChannelIndex: 211,
      refuelDraft: { channelIndex: 211, shiftCount: 8, directionId: "toward-end-a", fuelTypeId: "NAT-U-SYNTHETIC" },
      selectedTab: "reactor", mapMode: "burnup" });
  });

  it("switches accessible history tabs, draws measurements and keeps tab focus on updates", () => {
    const { view, session, emit } = harness();
    const powerTab = view.element.querySelector<HTMLButtonElement>('[data-tab="power"]')!;
    powerTab.click(); powerTab.focus();
    expect(view.element.querySelector<HTMLElement>('[data-field="reactor-panel"]')!.hidden).toBe(true);
    expect(view.element.textContent).toContain("Maximum channel power");
    session.snapshot = { ...session.snapshot, sequence: 1, simulationTimeSeconds: 3600 };
    emit();
    expect(document.activeElement).toBe(powerTab);
    expect(view.element.querySelector('[data-history="note"]')!.textContent).toContain("2 samples");
    powerTab.dispatchEvent(new KeyboardEvent("keydown", { key: "ArrowRight", bubbles: true }));
    expect(document.activeElement?.getAttribute("data-tab")).toBe("burnup");
    expect(view.element.textContent).toContain("No discharged fuel yet");
    view.element.querySelector<HTMLButtonElement>('[data-tab="zones"]')!.click();
    expect(view.element.querySelectorAll("[data-trace]")).toHaveLength(15);
    const trace = view.element.querySelector<HTMLButtonElement>('[data-trace="0:0"]')!;
    trace.click(); expect(trace.getAttribute("aria-pressed")).toBe("false");
    expect(view.element.querySelector(".studio-trend-plot")!.innerHTML).not.toMatch(/NaN|Infinity/);
    view.element.querySelector<HTMLButtonElement>('[data-tab="reactor"]')!.click();
    expect(view.element.querySelector<HTMLElement>('[data-field="reactor-panel"]')!.hidden).toBe(false);
    view.element.querySelector<HTMLButtonElement>('[data-tab="xenon"]')!.click();
    expect(view.element.textContent).toContain("Core iodine and xenon");
    expect(view.element.textContent).toContain("Xenon by measured zone");
    expect(view.element.querySelector(".studio-trend-plot")!.innerHTML).not.toMatch(/NaN|Infinity/);
  });

  it("blocks unavailable, pending, exhausted and terminal commands with visible explanations", () => {
    const { session, emit, button, view } = harness();
    session.isPending = true; emit(); button("refuel").click();
    expect(session.dispatch).not.toHaveBeenCalled();
    expect(view.element.textContent).toContain("Reactor is solving your order");
    session.isPending = false; session.snapshot.freshBundlesAvailable = 3; emit();
    expect(button("refuel").disabled).toBe(true);
    expect(view.element.textContent).toContain("Not enough fresh fuel");
    session.snapshot.freshBundlesAvailable = 128; session.snapshot.rrs.isGameOver = true; emit();
    expect(button("refuel").disabled).toBe(true);
    expect(button("reset").disabled).toBe(false);
    session.status.isWasmAvailable = false; emit();
    expect(button("reset").disabled).toBe(true);
  });

  it("shows accepted before/after impact once and retains focus through clock updates", () => {
    const { session, view, emit } = harness();
    const circle = view.element.querySelector<SVGRectElement>('[data-channel="210"]')!;
    circle.focus();
    session.snapshot = { ...session.snapshot, sequence: 1, freshBundlesAvailable: 120, scoreTotal: 12,
      core: { ...session.snapshot.core, channels: [{ ...session.snapshot.core.channels[0], localTiltFraction: -0.03 }, session.snapshot.core.channels[1]] } };
    const response = { sequence: 1, accepted: true, message: "Channel refuelled.", snapshot: session.snapshot,
      command: { type: "commit-refuel", request: { channelIndex: 210, shiftCount: 8, directionId: "toward-end-b", fuelTypeId: "NAT-U-SYNTHETIC" } } } as CanduCommandResponse;
    emit(response);
    const impact = view.element.querySelector('[data-field="impact"]')!.textContent;
    expect(impact).toContain("128 → 120"); expect(impact).toContain("+0.00% → -3.00%");
    emit(response);
    expect(view.element.querySelector('[data-field="impact"]')!.textContent).toBe(impact);
    expect(document.activeElement).toBe(circle);
    circle.dispatchEvent(new KeyboardEvent("keydown", { key: "ArrowRight", bubbles: true }));
    expect(document.activeElement?.getAttribute("data-channel")).toBe("211");
    expect(view.element.querySelector('[data-field="channel"]')!.textContent).toBe("Channel 211");
  });

  it("keeps range keyboard editing separate from gameplay shortcuts and dispatches controls", () => {
    const { view, session, button, emit } = harness();
    const input = view.element.querySelector<HTMLInputElement>('[data-field="target-input"]')!;
    input.value = "95"; input.dispatchEvent(new Event("input"));
    input.dispatchEvent(new KeyboardEvent("keydown", { key: "r", bubbles: true }));
    expect(session.dispatch).not.toHaveBeenCalled();
    expect(button("target").disabled).toBe(true);
    session.snapshot.isPaused = false; emit();
    button("target").click();
    expect(session.dispatch).toHaveBeenCalledWith({ type: "queue-power-target", targetFraction: 0.95 });
    session.snapshot.isPaused = true; emit();
    button("step").click();
    expect(session.dispatch).toHaveBeenCalledWith({ type: "step", simulationSeconds: 3600 });
    button("pause").click();
    expect(session.dispatch).toHaveBeenCalledWith({ type: "resume" });
  });

  it("displays the authoritative applied target", () => {
    const { session, view, emit } = harness();
    session.snapshot.targetPowerFraction = 0.95;
    session.snapshot.physics.targetPowerWatts = 950;
    session.snapshot.physics.actualPowerFraction = 0.95;
    emit();
    expect(view.element.querySelector('[data-field="power-target"]')!.textContent).toBe("95.0%");
    expect(view.element.querySelector('[data-field="status"]')!.textContent).toBe("stable");
  });
});
