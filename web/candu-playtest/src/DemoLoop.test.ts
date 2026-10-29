import { afterEach, expect, it, vi } from "vitest";
import { DemoLoop } from "./DemoLoop";
import type { CanduPlaytestBridgeLifecycle } from "./bridge";
import type { CanduCommand, CanduCommandResponse, CanduSnapshot } from "./protocol";
import { createSnapshot } from "./testSnapshot";

let loop: DemoLoop;
afterEach(() => { loop?.destroy(); vi.useRealTimers(); });
function setup() {
  vi.useFakeTimers();
  let snapshot: CanduSnapshot = { ...createSnapshot(), pacingMode: "demo", completedDays: 0, simulationTimeSeconds: 0 };
  const calls: CanduCommand[] = [];
  let complete: (accepted?: boolean) => void = () => {};
  const bridge: CanduPlaytestBridgeLifecycle = {
    status: { source: "wasm", title: "Ready", detail: "", isWasmAvailable: true, capabilities: [] },
    getSnapshot: () => snapshot, initializeMode: async () => snapshot, subscribe: () => () => {}, dispose: vi.fn(),
    dispatch: command => {
      calls.push(command);
      return new Promise<CanduCommandResponse>(resolve => {
        complete = (accepted = true) => {
          if (accepted && command.type === "commit-day") snapshot = { ...snapshot, completedDays: (snapshot.completedDays ?? 0) + 1, simulationTimeSeconds: snapshot.simulationTimeSeconds + 86400 };
          resolve({ protocol: "candu-playtest-v2", accepted, sequence: 1, command, message: "Rejected", diagnostics: [], stateDigest: "test", snapshot });
        };
      });
    },
  };
  loop = new DemoLoop(bridge, () => Date.now());
  return { calls, bridge, complete: (accepted = true) => complete(accepted) };
}

it("advances every five seconds and never overlaps or replays slow solves", async () => {
  const h = setup();
  await vi.advanceTimersByTimeAsync(4999); expect(h.calls).toHaveLength(0);
  await vi.advanceTimersByTimeAsync(1); expect(h.calls[0]).toEqual({ type: "commit-day", expectedCompletedDays: 0, channelIndices: [] });
  await vi.advanceTimersByTimeAsync(60000); expect(h.calls).toHaveLength(1);
  h.complete(); await vi.advanceTimersByTimeAsync(100);
  expect(h.calls).toHaveLength(2);
  h.complete(); await vi.advanceTimersByTimeAsync(4900); expect(h.calls).toHaveLength(2);
  await vi.advanceTimersByTimeAsync(100); expect(h.calls).toHaveLength(3);
});

it("accepts immediate fuel without time advance, deduplicates pending clicks and bounds the queue", async () => {
  const h = setup(); loop.fuel(210); loop.fuel(210);
  expect(h.calls).toHaveLength(1); expect(h.calls[0].type).toBe("commit-refuel");
  expect(loop.snapshot.simulationTimeSeconds).toBe(0);
  for (let i = 0; i < 20; i++) loop.fuel(i);
  expect(loop.waitingFuel.size).toBe(8);
  await vi.advanceTimersByTimeAsync(6000); expect(h.calls).toHaveLength(1);
  h.complete(); await vi.advanceTimersByTimeAsync(100);
  expect(h.calls[1].type).toBe("commit-day"); // Clock cannot starve behind fuel.
});

it("pauses on rejection and discards hidden/paused time and waiting input", async () => {
  const h = setup(); loop.fuel(210); loop.fuel(211);
  h.complete(false); await vi.advanceTimersByTimeAsync(100);
  expect(loop.running).toBe(false); expect(loop.waitingFuel.size).toBe(0);
  loop.togglePause(); loop.setVisible(false);
  await vi.advanceTimersByTimeAsync(60000); expect(h.calls).toHaveLength(1);
  loop.setVisible(true); await vi.advanceTimersByTimeAsync(4900); expect(h.calls).toHaveLength(1);
  await vi.advanceTimersByTimeAsync(100); expect(h.calls).toHaveLength(2);
  loop.togglePause(); h.complete(); await vi.advanceTimersByTimeAsync(60000); expect(h.calls).toHaveLength(2);
  loop.destroy(); expect(h.bridge.dispose).toHaveBeenCalled();
});
