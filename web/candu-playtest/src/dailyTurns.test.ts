// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { BridgeSessionController } from "./sessionController";
import { StudioView } from "./studio/StudioView";
import { createSnapshot, createShift } from "./testSnapshot";
import { isProtocolSnapshot, type CanduCommand, type CanduCommandResponse, type CanduSnapshot } from "./protocol";
import type { CanduPlaytestBridgeLifecycle } from "./bridge";
import { parseRunSave } from "./runSave";

const disposables: (() => void)[] = [];
afterEach(() => { disposables.splice(0).reverse().forEach(fn => fn()); document.body.replaceChildren(); });
function harness() {
  let snapshot: CanduSnapshot = { ...createSnapshot(), shift: { ...createShift(), unlimitedFreshFuel: true, fuelBudget: 0 },
    pacingMode: "daily-turn", completedDays: 0, isPaused: true, scorePolicyId: "test" };
  snapshot.core.channels.forEach(c => { c.canRefuel = true; });
  const bridge: CanduPlaytestBridgeLifecycle = {
    status: { source: "wasm", title: "Ready", detail: "Ready", isWasmAvailable: true, capabilities: [] },
    getSnapshot: () => snapshot, initializeMode: vi.fn(async () => snapshot), subscribe: () => () => {},
    dispatch: vi.fn(async command => {
      snapshot = { ...snapshot, sequence: snapshot.sequence + 1 };
      if (command.type === "commit-day") snapshot = { ...snapshot, completedDays: 1, simulationTimeSeconds: 86400 };
      return { protocol: "candu-playtest-v2", accepted: true, sequence: snapshot.sequence, command, message: "Day complete", diagnostics: [], stateDigest: "digest", snapshot } as CanduCommandResponse;
    }),
  };
  const timer = vi.fn();
  const session = new BridgeSessionController(bridge, { setTimer: timer, clearTimer: vi.fn(), now: () => 0 });
  disposables.push(() => session.dispose());
  return { session, bridge, timer };
}

describe("daily browser decisions", () => {
  it("never schedules time and can save a frozen first-day draft", async () => {
    const { session, timer } = harness(); session.startShift();
    await vi.waitFor(() => expect(session.isPending).toBe(false));
    session.setDailyPlan([211, 210]); session.setVisible(false); session.setVisible(true);
    expect(timer).not.toHaveBeenCalled();
    const save = session.captureRun();
    expect(save.version).toBe(2); expect(save.pacingMode).toBe("daily-turn"); expect(save.seconds).toBe(0);
    expect(save.draftChannels).toEqual([210, 211]); expect(parseRunSave(save)).toEqual(save);
    expect(() => parseRunSave({ ...save, draftChannels: [380] })).toThrow();
  });

  it("edits a multi-channel plan without dispatching and submits one day", async () => {
    const { session, bridge } = harness();
    const view = new StudioView(session, document.body); disposables.push(() => view.destroy());
    const button = (action: string) => view.element.querySelector<HTMLButtonElement>(`[data-action="${action}"]`)!;
    button("refuel").click();
    view.element.querySelector<HTMLElement>('[data-channel="211"]')!.dispatchEvent(new MouseEvent("click", { bubbles: true }));
    button("refuel").click();
    expect(session.dailyPlan).toEqual([210, 211]); expect(bridge.dispatch).not.toHaveBeenCalled();
    expect(view.element.querySelector('[data-channel="210"]')!.getAttribute("data-planned")).toBe("true");
    expect(button("refuel").textContent).toContain("Remove");
    button("commit-day").click(); button("commit-day").click();
    await vi.waitFor(() => expect(session.isPending).toBe(false));
    expect(bridge.dispatch).toHaveBeenCalledTimes(1);
    expect(bridge.dispatch).toHaveBeenCalledWith({ type: "commit-day", expectedCompletedDays: 0, channelIndices: [210, 211] }, expect.anything());
    expect(session.dailyPlan).toEqual([]);
    expect(button("commit-day").textContent).toBe("Advance 1 day without refuelling");
    expect(view.element.querySelector('[data-field="time"]')!.textContent).toBe("Day 2");
  });

  it("keeps a rejected draft and prevents repeat requests while pending", async () => {
    const { session, bridge } = harness(); session.setDailyPlan([210]);
    let finish!: (r: CanduCommandResponse) => void;
    vi.mocked(bridge.dispatch).mockImplementation(() => new Promise(resolve => { finish = resolve; }));
    const command: CanduCommand = { type: "commit-day", expectedCompletedDays: 0, channelIndices: [210] };
    const pending = session.dispatch(command);
    await expect(session.dispatch(command)).rejects.toThrow("current day");
    session.setDailyPlan([]); expect(session.dailyPlan).toEqual([210]);
    finish({ protocol: "candu-playtest-v2", accepted: false, sequence: 1, command, message: "Rejected", diagnostics: [], snapshot: session.snapshot });
    await pending; expect(session.dailyPlan).toEqual([210]);
  });

  it("restores daily drafts into a separate worker and keeps the scheduler asleep", async () => {
    const { session, timer } = harness(); await session.dispatch({ type: "pause" }); session.setDailyPlan([210]);
    const saved = session.captureRun(); const candidate = harness().bridge;
    await session.restoreRun(saved, undefined, () => candidate);
    expect(candidate.initializeMode).toHaveBeenCalledWith("play", "daily-turn", saved.seed);
    expect(session.dailyPlan).toEqual([210]); session.startShift(); expect(timer).not.toHaveBeenCalled();
    await session.restoreRun({ ...saved, version: 1, pacingMode: undefined, draftChannels: undefined }, undefined, () => candidate);
    expect(candidate.initializeMode).toHaveBeenLastCalledWith("play", "real-time", saved.seed);
  });

  it("rejects invalid daily snapshot contracts", () => {
    const { session } = harness();
    expect(isProtocolSnapshot(session.snapshot)).toBe(true);
    expect(isProtocolSnapshot({ ...session.snapshot, isPaused: false })).toBe(false);
    expect(isProtocolSnapshot({ ...session.snapshot, completedDays: -1 })).toBe(false);
    expect(isProtocolSnapshot({ ...session.snapshot, lastDayResult: { movements: [] } })).toBe(false);
  });

  it("rejects daily saves from another integration model before creating a worker", async () => {
    const { session } = harness(); await session.dispatch({ type: "pause" });
    const save = session.captureRun(), factory = vi.fn(() => harness().bridge);
    await expect(session.restoreRun({ ...save, dailyIntegrationId: "old-three-minute-model" }, undefined, factory)).rejects.toThrow("time-step model");
    expect(factory).not.toHaveBeenCalled();
  });
});
