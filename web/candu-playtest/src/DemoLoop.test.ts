import { afterEach, expect, it, vi } from "vitest";
import { DemoLoop } from "./DemoLoop";
import type { CanduPlaytestBridgeLifecycle } from "./bridge";
import type { CanduCommand, CanduCommandResponse, CanduSnapshot } from "./protocol";
import { createSnapshot } from "./testSnapshot";
import { channelWatts } from "./studio/powerReadings";

let loop: DemoLoop;
afterEach(() => { loop?.destroy(); vi.useRealTimers(); });
function setup() {
  vi.useFakeTimers();
  let snapshot: CanduSnapshot = { ...createSnapshot(), pacingMode: "demo", completedDays: 0, simulationTimeSeconds: 0, ripple: undefined };
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
            core: { ...snapshot.core, channels: snapshot.core.channels.map(c => ({ ...c, powerWatts: c.powerWatts + 100000 })) },
            rrs: { ...snapshot.rrs, averageFillFraction: snapshot.rrs.averageFillFraction + .1 },
            runStatus: terminal ? "ended" : "running", runEndReason: terminal ? "Channel power" : "",
          };
          if (accepted && command.type === "commit-refuel") snapshot = { ...snapshot, shift: snapshot.shift ? { ...snapshot.shift, fuelConsumed: snapshot.shift.fuelConsumed + 8 } : undefined };
          if (accepted && command.type === "reset") snapshot = { ...createSnapshot(), pacingMode: "demo", completedDays: 0, simulationTimeSeconds: 0 };
          notify(bridge.status, snapshot); // A real bridge publishes before dispatch resolves.
          resolve({ protocol: "candu-playtest-v2", accepted, sequence: 1, command, message: "Rejected", diagnostics: [], stateDigest: "test", snapshot });
        };
      });
    },
  };
  loop = new DemoLoop(bridge, () => Date.now());
  return { calls, bridge, complete: (accepted = true, terminal = false) => complete(accepted, terminal) };
}

it("buffers solved endpoints, blends for five seconds, and never leaks subscription results", async () => {
  const h = setup(), start = loop.snapshot;
  await vi.advanceTimersByTimeAsync(50);
  h.complete(); await vi.advanceTimersByTimeAsync(0);
  expect(loop.snapshot).toBe(start);
  await vi.advanceTimersByTimeAsync(50); // Start playback, compute one endpoint ahead.
  expect(h.calls).toHaveLength(2);
  h.complete(); await vi.advanceTimersByTimeAsync(2500);
  expect(loop.snapshot.completedDays).toBe(0);
  expect(channelWatts(loop.snapshot, loop.snapshot.core.channels[0])).toBeCloseTo(start.core.channels[0].powerWatts + 50000);
  expect(loop.snapshot.rrs.averageFillFraction).toBeCloseTo(start.rrs.averageFillFraction + .05);
  expect(loop.playbackDay).toBeCloseTo(.25);
  expect(h.calls).toHaveLength(2);
  await vi.advanceTimersByTimeAsync(2500);
  expect(loop.snapshot.completedDays).toBe(0);
  expect(h.calls[2]).toEqual({ type: "commit-day", expectedCompletedDays: 1, channelIndices: [] });
});

it("prioritizes immediate fuel even with a buffered time step, deduplicates and bounds input", async () => {
  const h = setup();
  await vi.advanceTimersByTimeAsync(50); h.complete(); await vi.advanceTimersByTimeAsync(50);
  h.complete(); await vi.advanceTimersByTimeAsync(0); // Both active and buffered endpoints exist.
  loop.fuel(210); loop.fuel(210);
  await vi.advanceTimersByTimeAsync(50);
  expect(h.calls[2]).toEqual({ type: "commit-refuel", request: { channelIndex: 210, directionId: "toward-end-a", shiftCount: 8, fuelTypeId: "NAT-U-SYNTHETIC" } });
  for (let i = 0; i < 20; i++) loop.fuel(i);
  expect(loop.waitingFuel.size).toBe(8);
  loop.fuel(210);
  h.complete(); await vi.advanceTimersByTimeAsync(50);
  expect(h.calls[3].type).toBe("commit-refuel");
  expect(h.calls[3]).toMatchObject({ request: { channelIndex: 0, directionId: "toward-end-a" } });
  expect(loop.pendingFuel).toContain(210);
  expect(h.calls.filter(c => c.type === "commit-day")).toHaveLength(2);
});

it("starts queued fuel immediately after the in-flight calculation, before another time step", async () => {
  const h = setup();
  await vi.advanceTimersByTimeAsync(50);
  loop.fuel(210); loop.fuel(211);
  expect(h.calls).toHaveLength(1);
  h.complete(); await vi.advanceTimersByTimeAsync(0);
  expect(h.calls[1]).toMatchObject({ type: "commit-refuel", request: { channelIndex: 210 } });
  h.complete(); await vi.advanceTimersByTimeAsync(0);
  expect(h.calls[2]).toMatchObject({ type: "commit-refuel", request: { channelIndex: 211 } });
  h.complete(); await vi.advanceTimersByTimeAsync(0);
  expect(h.calls).toHaveLength(3);
  expect(h.calls.filter(c => c.type === "commit-day")).toHaveLength(1);
});

it("holds at a slow endpoint without extrapolating or replaying missed days", async () => {
  const h = setup(); await vi.advanceTimersByTimeAsync(50);
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
  expect(loop.snapshot).not.toBe(frozen); expect(loop.dayProgress).toBeCloseTo(.21);
  h.complete(); await vi.advanceTimersByTimeAsync(0); // Finish priority fuel before retry.
  const reset = loop.restart(1001); h.complete(); await reset;
  expect(loop.pendingFuel).toHaveLength(0); expect(loop.snapshot.completedDays).toBe(0);
  expect(loop.dayProgress).toBe(0); expect(loop.running).toBe(true);
  loop.destroy(); expect(h.bridge.dispose).toHaveBeenCalled();
});

it("reveals terminal state only at the displayed endpoint and stops future commands", async () => {
  const h = setup(); await vi.advanceTimersByTimeAsync(50); h.complete(true, true);
  await vi.advanceTimersByTimeAsync(50);
  expect(loop.snapshot.runStatus).not.toBe("ended"); expect(loop.running).toBe(true);
  await vi.advanceTimersByTimeAsync(5000);
  expect(loop.snapshot.runStatus).toBe("ended"); expect(loop.running).toBe(false);
  await vi.advanceTimersByTimeAsync(60000); expect(h.calls).toHaveLength(1);
});

it("rejects invalid fuel without claiming delivery or stopping time", async () => {
  const h = setup(); loop.fuel(210); await vi.advanceTimersByTimeAsync(50); h.complete(false);
  await vi.advanceTimersByTimeAsync(100);
  expect(loop.running).toBe(true); expect(loop.message).toBe("Rejected");
  expect(loop.snapshot.completedDays).toBe(0); expect(loop.pendingFuel).not.toContain(210);
});
