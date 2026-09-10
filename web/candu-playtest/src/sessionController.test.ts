import { describe, expect, it } from "vitest";
import type {
  BridgeStatus,
  CanduCommand,
  CanduCommandResponse,
  CanduPlaytestBridge,
  CanduSnapshot,
} from "./protocol";
import type { CanduPlaytestBridgeLifecycle } from "./bridge";
import { BridgeSessionController } from "./sessionController";

describe("bridge session controller", () => {
  it("starts and stops the authoritative live-clock lifecycle without owning simulation rules", async () => {
    const bridge = createFakeBridge();
    let now = 0;
    const timers: Array<() => void> = [];
    const controller = new BridgeSessionController(bridge, {
      now: () => now,
      setTimer: (callback) => {
        timers.push(callback);
        return callback;
      },
      clearTimer: () => undefined,
    });
    const updates: boolean[] = [];
    controller.subscribe((update) => updates.push(update.pending));

    expect(bridge.advanceCalls).toEqual([]);
    controller.startShift();
    now = 100;
    controller.setVisible(true);
    timers.shift()?.();
    await Promise.resolve();

    expect(bridge.advanceCalls).toEqual([100]);
    expect(controller.status.isWasmAvailable).toBe(true);
    expect(updates.every((pending) => !pending)).toBe(true);

    controller.stopShift();
    now += 500;
    timers.shift()?.();
    await Promise.resolve();
    expect(bridge.advanceCalls).toEqual([100]);
    controller.dispose();
  });

  it("surfaces command responses and errors as scene-friendly state", async () => {
    const bridge = createFakeBridge();
    const controller = new BridgeSessionController(bridge);
    const responses: string[] = [];
    controller.subscribe((update) => {
      if (update.response !== null) responses.push(update.response.message);
    });

    const response = await controller.dispatch({ type: "pause" });
    expect(response.accepted).toBe(true);
    expect(controller.snapshot.sequence).toBe(1);
    expect(responses).toContain("accepted");
    controller.dispose();
  });
});

function createFakeBridge(): CanduPlaytestBridgeLifecycle & { advanceCalls: number[] } {
  const status: BridgeStatus = {
    source: "wasm",
    title: "BROWSER WASM",
    detail: "test",
    isWasmAvailable: true,
    capabilities: [],
  };
  const listeners = new Set<(nextStatus: BridgeStatus, snapshot: CanduSnapshot) => void>();
  const advanceCalls: number[] = [];
  let snapshot = createSnapshot();
  const dispatch = async (command: CanduCommand): Promise<CanduCommandResponse> => {
    if (command.type === "advance") {
      advanceCalls.push(command.wallMilliseconds);
    }
    snapshot = { ...snapshot, sequence: snapshot.sequence + 1, isPaused: command.type === "pause" ? true : snapshot.isPaused };
    return {
      protocol: "candu-playtest-v1",
      accepted: true,
      sequence: snapshot.sequence,
      command,
      message: "accepted",
      diagnostics: [],
      snapshot,
      preview: null,
    };
  };
  const bridge: CanduPlaytestBridge & CanduPlaytestBridgeLifecycle & { advanceCalls: number[] } = {
    status,
    advanceCalls,
    getSnapshot: () => snapshot,
    dispatch,
    initializeMode: async () => snapshot,
    subscribe: (listener) => {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
  };
  void listeners;
  return bridge;
}

function createSnapshot(): CanduSnapshot {
  return {
    protocol: "candu-playtest-v1",
    source: "wasm",
    sequence: 0,
    scenarioId: "test",
    dataPackId: "test",
    simulationTimeSeconds: 0,
    wallElapsedSeconds: 0,
    normalizedPowerFraction: 1,
    targetPowerFraction: 1,
    absoluteTiltFraction: 0,
    targetTiltFraction: 0,
    controlMarginFraction: 1,
    deviceAvailableFraction: 1,
    pendingActionCount: 0,
    scoreTotal: 0,
    scoreDelta: 0,
    isPaused: false,
    playbackModeId: "1x",
    freshBundlesAvailable: 8,
    refuellingOperationCount: 0,
    lastRefuelledChannel: -1,
    lastRefuellingDirectionId: null,
    lastRefuellingShiftCount: 0,
    physics: {} as CanduSnapshot["physics"],
    xenon: {} as CanduSnapshot["xenon"],
    core: { channelCount: 380, bundlePositionCount: 12, gridWidth: 22, gridHeight: 22, channels: [] },
    diagnostics: { convergence: { state: "converged", iterations: 1, residual: 0, relativePowerError: 0, lastSolveMilliseconds: 0, solverLabel: "test" }, checks: [] },
    lastEvent: null,
  };
}
