import { afterEach, expect, it, vi } from "vitest";
import { DemoLoop } from "./DemoLoop";
import type { CanduPlaytestBridgeLifecycle } from "./bridge";
import type { CanduCommand, CanduCommandResponse, CanduSnapshot } from "./protocol";
import type { RunSave } from "./runSave";
import { createSnapshot, createShift } from "./testSnapshot";
import { channelWatts } from "./studio/powerReadings";

let loop: DemoLoop;
afterEach(() => { loop?.destroy(); vi.useRealTimers(); });
function setup(createRecoveryBridge?: ConstructorParameters<typeof DemoLoop>[3]) {
  vi.useFakeTimers();
  let snapshot: CanduSnapshot = { ...createSnapshot(), pacingMode: "demo", scorePolicyId: "test-policy", completedDays: 0, simulationTimeSeconds: 0, shift: { ...createShift(), fuelConsumed: 0 }, ripple: undefined };
  snapshot.core.channels[211].flowDirection = "toward-end-b";
  const calls: CanduCommand[] = [];
  let complete: (accepted?: boolean, terminal?: boolean) => void = () => {};
  let notify: Parameters<CanduPlaytestBridgeLifecycle["subscribe"]>[0] = () => {};
  const bridge: CanduPlaytestBridgeLifecycle = {
    status: { source: "wasm", title: "Ready", detail: "", isWasmAvailable: true, capabilities: [] },
    getSnapshot: () => snapshot, initializeMode: async () => snapshot, subscribe: listener => { notify = listener; return () => {}; }, dispose: vi.fn(),
    dispatch: command => {
      calls.push(command);
      return new Promise<CanduCommandResponse>(resolve => {
        complete = (accepted = true, terminal = false) => {
          if (accepted && command.type === "commit-day") snapshot = { ...snapshot,
            completedDays: Math.floor((snapshot.simulationTimeSeconds + 43200) / 86400), simulationTimeSeconds: snapshot.simulationTimeSeconds + 43200,
            shift: snapshot.shift ? { ...snapshot.shift, fuelConsumed: snapshot.shift.fuelConsumed + command.channelIndices.length * 8 } : undefined,
            core: { ...snapshot.core, channels: snapshot.core.channels.map(c => ({ ...c, powerWatts: c.powerWatts + 100000 })) },
            rrs: { ...snapshot.rrs, averageFillFraction: snapshot.rrs.averageFillFraction + .1 },
            runStatus: terminal ? "ended" : "running", runEndReason: terminal ? "Channel power" : "",
          };
          if (accepted && command.type === "reset") snapshot = { ...createSnapshot(), pacingMode: "demo", completedDays: 0, simulationTimeSeconds: 0 };
          notify(bridge.status, snapshot); // A real bridge publishes before dispatch resolves.
          resolve({ protocol: "candu-playtest-v2", accepted, sequence: 1, command, message: "Rejected", diagnostics: [], stateDigest: "test", snapshot });
        };
      });
    },
  };
  loop = new DemoLoop(bridge, () => Date.now(), false, createRecoveryBridge);
  return { calls, bridge, complete: (accepted = true, terminal = false) => complete(accepted, terminal),
    fail: () => {
      Object.assign(bridge.status, { source: "unavailable", isWasmAvailable: false });
      notify(bridge.status, snapshot);
    } };
}

it("blocks playback and fuel input while a healthy bridge resets", async () => {
  const h = setup();
  const restart = loop.restart(1001);
  loop.togglePause();
  loop.fuel(211);
  await vi.advanceTimersByTimeAsync(100);
  expect(loop.running).toBe(false);
  expect(loop.pendingFuel).toEqual([]);
  expect(h.calls.map(command => command.type)).toEqual(["reset"]);
  h.complete();
  await restart;
  expect(loop.running).toBe(true);
});

it("retries on a fresh seeded bridge without replaying queued fuel", async () => {
  const replacementSnapshot = { ...createSnapshot(), pacingMode: "demo" as const, shift: { ...createShift(), seed: 1001 } };
  const factory = vi.fn();
  const h = setup(factory);
  const replacement = { ...h.bridge, dispatch: vi.fn(), dispose: vi.fn(), whenReady: async () => replacementSnapshot };
  factory.mockReturnValue(replacement);
  loop.fuel(211);
  h.fail();

  await loop.restart(1001);

  expect(factory).toHaveBeenCalledWith(1001);
  expect(h.bridge.dispose).toHaveBeenCalledOnce();
  expect(h.calls).toEqual([]);
  expect(replacement.dispatch).not.toHaveBeenCalled();
  expect(loop.snapshot).toBe(replacementSnapshot);
  expect(loop.pendingFuel).toEqual([]);
  expect(loop.running).toBe(true);
});

it("buffers solved endpoints, blends for 7.5 seconds by default, and never leaks subscription results", async () => {
  const h = setup(), start = loop.snapshot;
  await vi.advanceTimersByTimeAsync(50);
  h.complete(); await vi.advanceTimersByTimeAsync(0);
  expect(loop.snapshot).toBe(start);
  await vi.advanceTimersByTimeAsync(50); // Start playback, compute one endpoint ahead.
  expect(h.calls).toHaveLength(2);
  h.complete(); await vi.advanceTimersByTimeAsync(3750);
  expect(loop.snapshot.completedDays).toBe(0);
  expect(channelWatts(loop.snapshot, loop.snapshot.core.channels[0])).toBeCloseTo(start.core.channels[0].powerWatts + 50000);
  expect(loop.snapshot.rrs.averageFillFraction).toBeCloseTo(start.rrs.averageFillFraction + .05);
  expect(loop.playbackDay).toBeCloseTo(.25);
  expect(h.calls).toHaveLength(2);
  await vi.advanceTimersByTimeAsync(3750);
  expect(loop.snapshot.completedDays).toBe(0);
  expect(h.calls[2]).toEqual({ type: "commit-day", expectedCompletedDays: 1, channelIndices: [] });
});

it.each([["slow", 10_000], ["fast", 5_000]] as const)("plays a half-day at %s speed in %i ms", async (speed, duration) => {
  const h = setup();
  loop.toggleSpeed(speed);
  await vi.advanceTimersByTimeAsync(50);
  h.complete(); await vi.advanceTimersByTimeAsync(50);
  expect(h.calls).toHaveLength(2); // Solve ahead at every playback speed.
  h.complete(); await vi.advanceTimersByTimeAsync(duration - 50);
  expect(loop.snapshot.simulationTimeSeconds).toBe(0);
  await vi.advanceTimersByTimeAsync(50);
  expect(loop.snapshot.simulationTimeSeconds).toBe(43_200);
  expect(h.calls).toHaveLength(3);
});

it("preserves the displayed position when switching speed while paused", async () => {
  const h = setup();
  await vi.advanceTimersByTimeAsync(50);
  h.complete(); await vi.advanceTimersByTimeAsync(50 + 3750);
  loop.togglePause();
  loop.toggleSpeed("fast");
  await vi.advanceTimersByTimeAsync(5000);
  expect(loop.playbackDay).toBe(.25);
  loop.togglePause();
  await vi.advanceTimersByTimeAsync(1250);
  expect(loop.playbackDay).toBe(.375);
  loop.toggleSpeed("fast");
  expect(loop.speed).toBe("normal");
  expect(loop.playbackDay).toBe(.375);
});

it("submits the entire waiting queue in one step and keeps the markers until its displayed endpoint", async () => {
  const h = setup();
  await vi.advanceTimersByTimeAsync(50); h.complete(); await vi.advanceTimersByTimeAsync(50);
  h.complete(); await vi.advanceTimersByTimeAsync(0); // Active and buffered endpoints exist.
  loop.fuel(210); loop.fuel(211); loop.fuel(210);
  await vi.advanceTimersByTimeAsync(7450);
  expect(h.calls).toHaveLength(2); // Never mutate an endpoint already solved.
  expect(loop.pendingFuel).toEqual([210, 211]);
  await vi.advanceTimersByTimeAsync(50);
  expect(h.calls[2]).toEqual({ type: "commit-day", expectedCompletedDays: 1, channelIndices: [210, 211] });
  expect(loop.waitingFuel.size).toBe(0);
  loop.fuel(210); // A submitted channel is still pending, not a new request.
  h.complete(); await vi.advanceTimersByTimeAsync(7500);
  expect(loop.snapshot.shift?.fuelConsumed).toBe(0);
  expect(loop.pendingFuel).toEqual([210, 211]);
  await vi.advanceTimersByTimeAsync(7450);
  expect(loop.snapshot.shift?.fuelConsumed).toBe(0);
  await vi.advanceTimersByTimeAsync(50);
  expect(loop.snapshot.shift?.fuelConsumed).toBe(16);
  expect(loop.pendingFuel).toEqual([]);
  expect(h.calls.every(c => c.type === "commit-day")).toBe(true);
});

it("batches input arriving during a solve and separates only input arriving after submission", async () => {
  const h = setup();
  await vi.advanceTimersByTimeAsync(50);
  loop.fuel(210); loop.fuel(211);
  expect(h.calls).toHaveLength(1);
  h.complete(); await vi.advanceTimersByTimeAsync(50);
  expect(h.calls[1]).toEqual({ type: "commit-day", expectedCompletedDays: 0, channelIndices: [210, 211] });
  loop.fuel(212); loop.fuel(213);
  h.complete(); await vi.advanceTimersByTimeAsync(7500);
  expect(h.calls[2]).toEqual({ type: "commit-day", expectedCompletedDays: 1, channelIndices: [212, 213] });
  expect(loop.pendingFuel).toEqual([210, 211, 212, 213]);
});

it("deduplicates and bounds each waiting batch to eight channels", async () => {
  const h = setup();
  for (let i = 0; i < 20; i++) { loop.fuel(i); loop.fuel(i); }
  expect(loop.waitingFuel.size).toBe(8);
  expect(loop.pendingFuel).toHaveLength(8);
  expect(loop.message).toContain("queue is full");
  await vi.advanceTimersByTimeAsync(50);
  expect(h.calls[0]).toMatchObject({ type: "commit-day", channelIndices: [0, 1, 2, 3, 4, 5, 6, 7] });
});

it("holds at a slow endpoint without extrapolating or replaying missed days", async () => {
  const h = setup(); loop.toggleSpeed("fast"); await vi.advanceTimersByTimeAsync(50);
  await vi.advanceTimersByTimeAsync(60000); expect(h.calls).toHaveLength(1);
  h.complete(); await vi.advanceTimersByTimeAsync(50);
  await vi.advanceTimersByTimeAsync(60000);
  expect(h.calls).toHaveLength(2); expect(loop.snapshot.completedDays).toBe(0);
  expect(loop.playbackDay).toBe(.5); // Holding an endpoint must not add another day to the chart.
  const held = loop.snapshot;
  await vi.advanceTimersByTimeAsync(5000); expect(loop.snapshot).toBe(held);
  h.complete(); await vi.advanceTimersByTimeAsync(50);
  expect(loop.snapshot).toBe(held); expect(h.calls).toHaveLength(3);
});

it("freezes playback and retains queued input on pause/hidden tabs, then resets cleanly", async () => {
  const h = setup(); await vi.advanceTimersByTimeAsync(50); h.complete(); await vi.advanceTimersByTimeAsync(1050);
  loop.fuel(210); loop.togglePause(); const frozen = loop.snapshot;
  h.complete(); await vi.advanceTimersByTimeAsync(60000);
  expect(loop.snapshot).toBe(frozen); expect(loop.waitingFuel.has(210)).toBe(true);
  loop.togglePause(); loop.setVisible(false); await vi.advanceTimersByTimeAsync(60000);
  expect(loop.snapshot).toBe(frozen);
  loop.setVisible(true); await vi.advanceTimersByTimeAsync(50);
  expect(loop.snapshot).not.toBe(frozen); expect(loop.dayProgress).toBeCloseTo(.14);
  const reset = loop.restart(1001); h.complete(); await reset;
  expect(loop.pendingFuel).toHaveLength(0); expect(loop.snapshot.completedDays).toBe(0);
  expect(loop.dayProgress).toBe(0); expect(loop.running).toBe(true);
  loop.destroy(); expect(h.bridge.dispose).toHaveBeenCalled();
});

it("reveals terminal state only at the displayed endpoint and stops future commands", async () => {
  const h = setup(); await vi.advanceTimersByTimeAsync(50); h.complete(true, true);
  await vi.advanceTimersByTimeAsync(50);
  expect(loop.snapshot.runStatus).not.toBe("ended"); expect(loop.running).toBe(true);
  await vi.advanceTimersByTimeAsync(7500);
  expect(loop.snapshot.runStatus).toBe("ended"); expect(loop.running).toBe(false);
  await vi.advanceTimersByTimeAsync(60000); expect(h.calls).toHaveLength(1);
});

it("stops on a rejected batch without claiming any fuel delivery", async () => {
  const h = setup(); loop.fuel(210); await vi.advanceTimersByTimeAsync(50); h.complete(false);
  await vi.advanceTimersByTimeAsync(100);
  expect(loop.running).toBe(false); expect(loop.message).toBe("Rejected");
  expect(h.calls).toHaveLength(1);
  expect(loop.snapshot.completedDays).toBe(0); expect(loop.pendingFuel).not.toContain(210);
});

it("finishes the in-flight step and checkpoints its confirmed endpoint plus waiting fuel", async () => {
  const h = setup();
  loop.fuel(210);
  await vi.advanceTimersByTimeAsync(50);
  loop.fuel(211);
  const write = vi.fn();
  const checkpoint = loop.checkpoint(write);
  loop.fuel(212);
  expect(write).not.toHaveBeenCalled();
  h.complete();
  const release = await checkpoint;
  expect(write).toHaveBeenCalledWith(loop.snapshot.shift!.seed, expect.objectContaining({
    pacingMode: "demo", seconds: 43_200, fuelConsumed: 8, draftChannels: [211], stateDigest: "test",
    commands: [{ command: { type: "commit-day", expectedCompletedDays: 0, channelIndices: [210] }, accepted: true, responseMode: "compact" }],
  }));
  await vi.advanceTimersByTimeAsync(200);
  expect(h.calls).toHaveLength(1);
  release();
  await vi.advanceTimersByTimeAsync(50);
  expect(h.calls[1]).toMatchObject({ channelIndices: [211] });
});

it("checkpoints a fuel order queued before the first time step", async () => {
  const h = setup();
  loop.fuel(210);
  const write = vi.fn();
  const checkpoint = loop.checkpoint(write);
  expect(h.calls).toEqual([{ type: "pause" }]);
  h.complete();
  (await checkpoint)();
  expect(write).toHaveBeenCalledWith(loop.snapshot.shift!.seed, expect.objectContaining({
    seconds: 0, draftChannels: [210], stateDigest: "test",
  }));
});

it.each([
  ["dataPackId", "v9", "different reactor model"],
  ["scorePolicyId", "practice-channel-ripple-v3", "different scoring policy"],
] as const)("retains a checkpoint with an incompatible %s", async (key, value, reason) => {
  const h = setup();
  loop.fuel(210);
  const write = vi.fn<(seed: number, save?: RunSave) => Promise<void>>();
  const checkpoint = loop.checkpoint(write);
  h.complete();
  (await checkpoint)();
  const oldSave = { ...write.mock.calls[0][1]!, [key]: value };
  const initial = h.bridge.getSnapshot();
  const bridge = { ...h.bridge, getSnapshot: () => initial, whenReady: async () => initial, dispatch: vi.fn() };
  loop.destroy();
  loop = new DemoLoop(bridge, () => Date.now(), true, undefined, Promise.resolve(oldSave));
  await vi.advanceTimersByTimeAsync(0);
  expect(loop.message).toContain(reason);
  expect(loop.message).toContain("checkpoint is still stored");
  expect(bridge.dispatch).not.toHaveBeenCalled();
});
