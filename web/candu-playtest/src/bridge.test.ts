import { describe, expect, it } from "vitest";
import {
  createCanduPlaytestBridge,
  WorkerProtocolBridge,
  type WorkerProtocolWorker,
} from "./bridge";

describe("WorkerProtocolBridge lifecycle", () => {
  it("rejects an in-flight request after a fatal worker error", async () => {
    const harness = createWorkerHarness();
    const bridge = new WorkerProtocolBridge({
      createWorker: () => harness.worker,
    });

    harness.emitReady();
    const pending = bridge.dispatch({ type: "pause" });
    await flushMicrotasks();

    expect(harness.postedMessages).toHaveLength(1);
    harness.emitError("worker crashed");

    await expect(pending).rejects.toThrow("worker crashed");
    expect(harness.terminateCalls).toBe(1);
  });

  it("rejects subsequent requests without posting after a fatal worker error", async () => {
    const harness = createWorkerHarness();
    const bridge = new WorkerProtocolBridge({
      createWorker: () => harness.worker,
    });

    harness.emitReady();
    const pending = bridge.dispatch({ type: "pause" });
    await flushMicrotasks();
    harness.emitError("worker crashed");
    await expect(pending).rejects.toThrow("worker crashed");

    const postedCount = harness.postedMessages.length;
    await expect(bridge.dispatch({ type: "resume" })).rejects.toThrow("worker crashed");

    expect(harness.postedMessages).toHaveLength(postedCount);
    expect(harness.terminateCalls).toBe(1);
  });

  it("terminates the worker and rejects pending work on disposal", async () => {
    const harness = createWorkerHarness();
    const bridge = new WorkerProtocolBridge({
      createWorker: () => harness.worker,
    });

    harness.emitReady();
    const pending = bridge.dispatch({ type: "pause" });
    await flushMicrotasks();
    expect(harness.postedMessages).toHaveLength(1);

    bridge.dispose();

    await expect(pending).rejects.toThrow("disposed");
    const postedCount = harness.postedMessages.length;
    await expect(bridge.dispatch({ type: "resume" })).rejects.toThrow("disposed");
    bridge.dispose();

    expect(harness.postedMessages).toHaveLength(postedCount);
    expect(harness.terminateCalls).toBe(1);
  });

  it("sends Lab mode through the worker initialize transport", async () => {
    const harness = createWorkerHarness();
    const bridge = new WorkerProtocolBridge({
      createWorker: () => harness.worker,
    });

    harness.emitReady();
    const pending = bridge.initialize("lab");
    await flushMicrotasks();

    expect(harness.postedMessages).toContainEqual(expect.objectContaining({
      type: "initialize",
      mode: "lab",
    }));

    bridge.dispose();
    await expect(pending).rejects.toThrow("disposed");
  });

  it("fails the authoritative lifecycle closed and notifies on a fatal worker error", async () => {
    const harness = createWorkerHarness();
    const bridge = createCanduPlaytestBridge({
      createWorker: () => harness.worker,
    });
    const updates: Array<{ source: string; available: boolean; scenarioId: string }> = [];
    bridge.subscribe((status, snapshot) => {
      updates.push({
        source: status.source,
        available: status.isWasmAvailable,
        scenarioId: snapshot.scenarioId,
      });
    });

    harness.emitReady();
    await flushMicrotasks();
    harness.emitError("worker crashed");

    expect(bridge.status.source).toBe("unavailable");
    expect(bridge.status.isWasmAvailable).toBe(false);
    expect(updates).toEqual([{
      source: "unavailable",
      available: false,
      scenarioId: "unavailable",
    }]);
    await expect(bridge.dispatch({ type: "pause" })).rejects.toThrow(
      "Authoritative WASM bridge unavailable",
    );

    bridge.dispose?.();
  });
});

function createWorkerHarness() {
  const postedMessages: unknown[] = [];
  let terminateCalls = 0;
  const worker: WorkerProtocolWorker = {
    onmessage: null,
    onerror: null,
    postMessage(message: unknown): void {
      postedMessages.push(message);
    },
    terminate(): void {
      terminateCalls += 1;
    },
  };

  return {
    worker,
    postedMessages,
    get terminateCalls(): number {
      return terminateCalls;
    },
    emitReady(): void {
      worker.onmessage?.({ data: { type: "ready" } } as MessageEvent);
    },
    emitError(message: string): void {
      worker.onerror?.({ message } as unknown as ErrorEvent);
    },
  };
}

async function flushMicrotasks(): Promise<void> {
  await Promise.resolve();
  await Promise.resolve();
  await Promise.resolve();
}
