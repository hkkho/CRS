import { afterEach, describe, expect, it, vi } from "vitest";
import { StudioView } from "./StudioView";
import { createUnavailableRrsSnapshot, type BridgeStatus, type CanduChannelSnapshot, type CanduCommand, type CanduCommandResponse, type CanduSnapshot } from "../protocol";
import type { SessionUpdate } from "../sessionController";

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
  rrs.zones = Array.from({ length: 14 }, (_, logicalZoneId) => ({ logicalZoneId, fillFraction: 0.58,
    referencePowerFraction: 0, targetPowerFraction: 0, measuredPowerFraction: 0, shapeError: 0 }));
  const snapshot = { sequence: 0, simulationTimeSeconds: 0, scoreTotal: 0, isPaused: true,
    freshBundlesAvailable: 128, refuellingOperationCount: 0, rrsReserveFraction: 0.84,
    targetPowerFraction: 1, axialTiltFraction: 0, playbackModeId: "pause", rrs,
    physics: { actualPowerFraction: 1, targetPowerWatts: 1000, referencePowerWatts: 1000 }, core: { channels: [channel(210, 10, 6), channel(211, 11, 8)] } } as CanduSnapshot;
  const status: BridgeStatus = { source: "wasm", title: "Ready", detail: "Ready", isWasmAvailable: true, capabilities: [] };
  const session = {
    snapshot, status, isPending: false,
    subscribe: (callback: (update: SessionUpdate) => void) => {
      listener = callback; callback({ snapshot: session.snapshot, status, pending: session.isPending, response: null, error: null });
      return () => { listener = null; };
    },
    dispatch: vi.fn(async (command: CanduCommand) => ({ command, accepted: true }) as CanduCommandResponse),
  };
  const navigate = vi.fn();
  const view = new StudioView(session, document.body, navigate);
  views.push(view);
  const emit = (response: CanduCommandResponse | null = null) => listener?.({ snapshot: session.snapshot, status, pending: session.isPending, response, error: null });
  const button = (action: string) => view.element.querySelector<HTMLButtonElement>(`button[data-action="${action}"]`)!;
  return { session, view, emit, button, navigate };
}

describe("Reactor Studio live interface", () => {
  it("displays authoritative electrical and thermal power with explicit units", () => {
    const { session, view, emit } = harness();
    session.snapshot.physics.electricalPowerWatts = 650_000_000;
    session.snapshot.physics.totalPowerWatts = 2_064_000_000;
    emit();
    expect(view.element.querySelector('[data-field="power-rating"]')?.textContent)
      .toBe("650 MW electric · 2064 MW thermal");
  });

  it("uses the shared command contract and transfers channel and draft when switching views", () => {
    const { session, button, view, navigate } = harness();
    button("oldest").click();
    view.element.querySelector<HTMLButtonElement>('[data-size="8"]')!.click();
    button("direction").click();
    button("refuel").click();
    expect(session.dispatch).toHaveBeenCalledWith({ type: "commit-refuel", request: {
      channelIndex: 211, shiftCount: 8, directionId: "toward-end-a", fuelTypeId: "NAT-U-SYNTHETIC",
    } });
    button("classic").click();
    expect(navigate).toHaveBeenCalledWith("OperationsScene", { selectedChannelIndex: 211,
      refuelDraft: { channelIndex: 211, shiftCount: 8, directionId: "toward-end-a", fuelTypeId: "NAT-U-SYNTHETIC" } });
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
    const circle = view.element.querySelector<SVGCircleElement>('[data-channel="210"]')!;
    circle.focus();
    session.snapshot = { ...session.snapshot, sequence: 1, freshBundlesAvailable: 124, scoreTotal: 12,
      core: { ...session.snapshot.core, channels: [{ ...session.snapshot.core.channels[0], localTiltFraction: -0.03 }, session.snapshot.core.channels[1]] } };
    const response = { sequence: 1, accepted: true, message: "Channel refuelled.", snapshot: session.snapshot,
      command: { type: "commit-refuel", request: { channelIndex: 210, shiftCount: 4, directionId: "toward-end-b", fuelTypeId: "NAT-U-SYNTHETIC" } } } as CanduCommandResponse;
    emit(response);
    const impact = view.element.querySelector('[data-field="impact"]')!.textContent;
    expect(impact).toContain("128 → 124"); expect(impact).toContain("+0.00% → -3.00%");
    emit(response);
    expect(view.element.querySelector('[data-field="impact"]')!.textContent).toBe(impact);
    expect(document.activeElement).toBe(circle);
    circle.dispatchEvent(new KeyboardEvent("keydown", { key: "ArrowRight", bubbles: true }));
    expect(document.activeElement?.getAttribute("data-channel")).toBe("211");
    expect(view.element.querySelector('[data-field="channel"]')!.textContent).toBe("Channel 211");
  });

  it("keeps range keyboard editing separate from gameplay shortcuts and dispatches controls", () => {
    const { view, session, button, emit } = harness();
    const input = view.element.querySelector<HTMLInputElement>("input")!;
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

  it("displays the accepted solver target rather than the fixed legacy target", () => {
    const { session, view, emit } = harness();
    session.snapshot.physics.targetPowerWatts = 950;
    session.snapshot.physics.actualPowerFraction = 0.95;
    emit();
    expect(view.element.querySelector('[data-field="power-target"]')!.textContent).toBe("95.0%");
    expect(view.element.querySelector('[data-field="status"]')!.textContent).toBe("stable");
  });
});
