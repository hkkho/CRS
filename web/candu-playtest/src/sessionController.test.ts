import { describe, expect, it } from "vitest";
import {
  createUnavailableRrsSnapshot,
  type BridgeStatus,
  type CanduCommand,
  type CanduCommandResponse,
  type CanduDispatchOptions,
  type CanduPlaytestBridge,
  type CanduSnapshot,
} from "./protocol";
import type { CanduPlaytestBridgeLifecycle } from "./bridge";
import { BridgeSessionController } from "./sessionController";

describe("bridge session controller", () => {
  it("starts and stops the authoritative live-clock lifecycle without owning simulation rules", async () => {
    const bridge = createSerializedFakeBridge();
    const clock = createClockHarness();
    const controller = new BridgeSessionController(bridge, clock.options);
    const updates: boolean[] = [];
    controller.subscribe((update) => updates.push(update.pending));

    expect(bridge.calls).toEqual([]);
    controller.startShift();
    clock.advanceBy(100);
    expect(bridge.calls.map((command) => command.type)).toEqual(["advance"]);

    bridge.resolveActive();
    await flushMicrotasks();
    expect(controller.status.isWasmAvailable).toBe(true);
    expect(updates.every((pending) => !pending)).toBe(true);

    controller.stopShift();
    clock.advanceBy(500);
    expect(bridge.calls.map((command) => command.type)).toEqual(["advance"]);
    controller.dispose();
  });

  it("runs a foreground command after the current advance and before any new clock advance", async () => {
    const bridge = createSerializedFakeBridge();
    const clock = createClockHarness();
    const controller = new BridgeSessionController(bridge, clock.options);
    controller.startShift();
    clock.advanceBy(100);
    expect(bridge.activeCommand?.type).toBe("advance");

    const pausePromise = controller.dispatch({ type: "pause" });
    clock.advanceBy(10_000);
    expect(bridge.calls.map((command) => command.type)).toEqual(["advance"]);

    bridge.resolveActive();
    await flushMicrotasks();
    expect(bridge.calls.map((command) => command.type)).toEqual(["advance", "pause"]);
    expect(bridge.activeCommand?.type).toBe("pause");

    bridge.resolveActive();
    await pausePromise;
    await flushMicrotasks();
    expect(controller.snapshot.isPaused).toBe(true);

    clock.advanceBy(10_000);
    expect(bridge.calls.map((command) => command.type)).toEqual(["advance", "pause"]);
    controller.dispose();
  });

  it("resolves a queued compact pause against the post-advance sequence", async () => {
    const bridge = createSerializedFakeBridge();
    const clock = createClockHarness();
    const controller = new BridgeSessionController(bridge, clock.options);
    controller.startShift();
    clock.advanceBy(100);

    const pausePromise = controller.dispatch({ type: "pause" });
    expect(bridge.activeCommand?.type).toBe("advance");

    bridge.resolveActive();
    await flushMicrotasks();

    expect(bridge.activeCommand?.type).toBe("pause");
    expect(bridge.activeCompactBaseSequence).toBe(1);
    bridge.resolveActive();
    const response = await pausePromise;

    expect(response.accepted).toBe(true);
    expect(response.baseSequence).toBe(1);
    expect(controller.snapshot.sequence).toBe(2);
    clock.advanceBy(10_000);
    expect(bridge.calls.map((command) => command.type)).toEqual(["advance", "pause"]);
    controller.dispose();
  });

  it("honors visibility loss and deactivation as fresh clock lifecycles", async () => {
    const bridge = createSerializedFakeBridge();
    const clock = createClockHarness();
    const controller = new BridgeSessionController(bridge, clock.options);
    controller.startShift();

    clock.advanceBy(99);
    controller.setVisible(false);
    clock.advanceBy(10_000);
    controller.setVisible(true);
    clock.advanceBy(99);
    expect(bridge.calls).toEqual([]);
    clock.advanceBy(1);
    expect(bridge.calls.map((command) => command.type)).toEqual(["advance"]);

    bridge.resolveActive();
    await flushMicrotasks();
    controller.stopShift();
    clock.advanceBy(10_000);
    expect(bridge.calls.map((command) => command.type)).toEqual(["advance"]);

    controller.startShift();
    clock.advanceBy(99);
    expect(bridge.calls.map((command) => command.type)).toEqual(["advance"]);
    clock.advanceBy(1);
    expect(bridge.calls.map((command) => command.type)).toEqual(["advance", "advance"]);

    bridge.resolveActive();
    await flushMicrotasks();
    controller.dispose();
  });

  it("drops the live-clock interval on disposal", () => {
    const bridge = createSerializedFakeBridge();
    const clock = createClockHarness();
    const controller = new BridgeSessionController(bridge, clock.options);
    controller.startShift();
    clock.advanceBy(99);
    controller.dispose();
    clock.advanceBy(10_000);

    expect(bridge.calls).toEqual([]);
  });

  it("surfaces command responses and errors as scene-friendly state", async () => {
    const bridge = createImmediateFakeBridge();
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

interface QueuedCommand {
  command: CanduCommand;
  options: CanduDispatchOptions;
  resolve: (response: CanduCommandResponse) => void;
  reject: (error: unknown) => void;
}

function createSerializedFakeBridge(): CanduPlaytestBridgeLifecycle & {
  calls: CanduCommand[];
  activeCommand: CanduCommand | null;
  activeCompactBaseSequence: number | null;
  resolveActive: () => void;
} {
  const status: BridgeStatus = {
    source: "wasm",
    title: "BROWSER WASM",
    detail: "test",
    isWasmAvailable: true,
    capabilities: [],
  };
  const listeners = new Set<(nextStatus: BridgeStatus, snapshot: CanduSnapshot) => void>();
  const calls: CanduCommand[] = [];
  const queued: QueuedCommand[] = [];
  let active: QueuedCommand | null = null;
  let activeCompactBaseSequence: number | null = null;
  let snapshot = createSnapshot();

  const pump = (): void => {
    if (active !== null || queued.length === 0) {
      return;
    }
    active = queued.shift() ?? null;
    if (active !== null) {
      calls.push(active.command);
      activeCompactBaseSequence = active.options.responseMode === "compact"
        ? active.options.baseSequence ?? snapshot.sequence
        : null;
    }
  };

  const dispatch = (
    command: CanduCommand,
    options: CanduDispatchOptions = {},
  ): Promise<CanduCommandResponse> => {
    return new Promise<CanduCommandResponse>((resolve, reject) => {
      queued.push({ command, options, resolve, reject });
      pump();
    });
  };

  const resolveActive = (): void => {
    if (active === null) {
      throw new Error("No serialized command is active.");
    }
    const current = active;
    active = null;
    activeCompactBaseSequence = null;
    snapshot = applyCommand(snapshot, current.command);
    current.resolve({
      ...createResponse(current.command, snapshot),
      ...(current.options.responseMode === "compact"
        ? {
            responseKind: "compact" as const,
            baseSequence: current.options.baseSequence ?? snapshot.sequence - 1,
          }
        : {}),
    });
    pump();
  };

  const bridge: CanduPlaytestBridgeLifecycle & {
    calls: CanduCommand[];
    activeCommand: CanduCommand | null;
    activeCompactBaseSequence: number | null;
    resolveActive: () => void;
  } = {
    status,
    calls,
    activeCommand: null,
    activeCompactBaseSequence: null,
    getSnapshot: () => snapshot,
    dispatch,
    initializeMode: async () => snapshot,
    subscribe: (listener) => {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
    resolveActive,
  };

  Object.defineProperty(bridge, "activeCommand", {
    enumerable: true,
    get: () => active?.command ?? null,
  });
  Object.defineProperty(bridge, "activeCompactBaseSequence", {
    enumerable: true,
    get: () => activeCompactBaseSequence,
  });
  void listeners;
  return bridge;
}

function createImmediateFakeBridge(): CanduPlaytestBridgeLifecycle {
  const status: BridgeStatus = {
    source: "wasm",
    title: "BROWSER WASM",
    detail: "test",
    isWasmAvailable: true,
    capabilities: [],
  };
  let snapshot = createSnapshot();
  return {
    status,
    getSnapshot: () => snapshot,
    dispatch: async (command) => {
      snapshot = applyCommand(snapshot, command);
      return createResponse(command, snapshot);
    },
    initializeMode: async () => snapshot,
    subscribe: () => () => undefined,
  };
}

function applyCommand(snapshot: CanduSnapshot, command: CanduCommand): CanduSnapshot {
  const playbackModeId = command.type === "pause"
    ? "pause"
    : command.type === "resume"
      ? "1x"
      : command.type === "set-playback-mode"
        ? command.modeId
        : snapshot.playbackModeId;
  return {
    ...snapshot,
    sequence: snapshot.sequence + 1,
    isPaused: playbackModeId === "pause",
    playbackModeId,
  };
}

function createResponse(command: CanduCommand, snapshot: CanduSnapshot): CanduCommandResponse {
  return {
    protocol: "candu-playtest-v1",
    accepted: true,
    sequence: snapshot.sequence,
    command,
    message: "accepted",
    diagnostics: [],
    snapshot,
  };
}

function createClockHarness() {
  let now = 0;
  let nextTimerId = 1;
  const timers = new Map<number, { dueAt: number; callback: () => void }>();
  const options = {
    now: () => now,
    setTimer: (callback: () => void, delayMilliseconds: number) => {
      const timerId = nextTimerId++;
      timers.set(timerId, { dueAt: now + delayMilliseconds, callback });
      return timerId;
    },
    clearTimer: (handle: unknown) => {
      timers.delete(handle as number);
    },
  };

  return {
    options,
    advanceBy(milliseconds: number) {
      const target = now + milliseconds;
      while (true) {
        const next = [...timers.entries()]
          .filter(([, entry]) => entry.dueAt <= target)
          .sort(([, left], [, right]) => left.dueAt - right.dueAt)[0];
        if (next === undefined) {
          break;
        }
        const [timerId, entry] = next;
        timers.delete(timerId);
        now = entry.dueAt;
        entry.callback();
      }
      now = target;
    },
  };
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
    rrs: createUnavailableRrsSnapshot(),
    core: { channelCount: 380, bundlePositionCount: 12, gridWidth: 22, gridHeight: 22, channels: [] },
    diagnostics: { convergence: { state: "converged", iterations: 1, residual: 0, relativePowerError: 0, lastSolveMilliseconds: 0, solverLabel: "test" }, checks: [] },
    lastEvent: null,
  };
}

async function flushMicrotasks(): Promise<void> {
  await Promise.resolve();
  await Promise.resolve();
  await Promise.resolve();
}
