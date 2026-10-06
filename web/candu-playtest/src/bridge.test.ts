import { createSnapshot } from "./testSnapshot";
import { afterEach, describe, expect, it, vi } from "vitest";
import {
  createCanduPlaytestBridge,
  WorkerProtocolBridge,
  type WorkerProtocolWorker,
} from "./bridge";

afterEach(() => { vi.useRealTimers(); vi.restoreAllMocks(); });

describe("WorkerProtocolBridge lifecycle", () => {
  it("sends a cryptographic seed for new worker cores and preserves explicit replay seeds", async () => {
    vi.spyOn(globalThis.crypto, 'getRandomValues').mockImplementation(array => { (array as Uint32Array)[0] = 3456789012; return array; });
    const harness = createWorkerHarness();
    const bridge = new WorkerProtocolBridge({ createWorker: () => harness.worker });
    harness.emitReady();
    for (const explicit of [undefined, 1001]) {
      const pending = bridge.initialize('play', explicit);
      await flushMicrotasks();
      const request = harness.postedMessages.at(-1) as { id: number; seed: number };
      expect(request.seed).toBe(explicit ?? 3456789012);
      harness.worker.onmessage?.({ data: { type: 'result', id: request.id, wasmCallDurationMs: 0,
        returnedUtf8PayloadBytes: 1, resultJson: JSON.stringify(createSnapshot()) } } as MessageEvent);
      await pending;
    }
    bridge.dispose();
  });
  it("fails startup closed when the worker never becomes ready", async () => {
    vi.useFakeTimers();
    const harness = createWorkerHarness();
    const bridge = createCanduPlaytestBridge({ createWorker: () => harness.worker, startupTimeoutMs: 50 });
    await vi.advanceTimersByTimeAsync(50);
    expect(bridge.status.source).toBe("unavailable");
    expect(bridge.status.detail).toContain("startup deadline");
    expect(harness.terminateCalls).toBe(1);
    expect(vi.getTimerCount()).toBe(0);
  });

  it("times out initialization after ready and notifies recovery", async () => {
    vi.useFakeTimers();
    const harness = createWorkerHarness();
    const bridge = createCanduPlaytestBridge({ createWorker: () => harness.worker, startupTimeoutMs: 50 });
    harness.emitReady();
    await vi.advanceTimersByTimeAsync(50);
    expect(bridge.status.source).toBe("unavailable");
    expect(harness.terminateCalls).toBe(1);
    expect(vi.getTimerCount()).toBe(0);
  });

  it("rejects active and queued work on timeout without retrying or accepting late replies", async () => {
    vi.useFakeTimers();
    const harness = createWorkerHarness();
    const fatal = vi.fn();
    const bridge = new WorkerProtocolBridge({ createWorker: () => harness.worker, commandTimeoutMs: 50, onFatalError: fatal });
    harness.emitReady();
    const lateHandler = harness.worker.onmessage!;
    const first = bridge.dispatch({ type: "pause" });
    const second = bridge.dispatch({ type: "resume" });
    const settled = Promise.allSettled([first, second]);
    await vi.advanceTimersByTimeAsync(50);
    expect((await settled).map(result => result.status)).toEqual(["rejected", "rejected"]);
    expect(harness.postedMessages).toHaveLength(1);
    lateHandler({ data: { type: "ready" } } as MessageEvent);
    await expect(bridge.dispatch({ type: "pause" })).rejects.toThrow("will not be retried");
    expect(fatal).toHaveBeenCalledOnce();
    expect(harness.terminateCalls).toBe(1);
    expect(vi.getTimerCount()).toBe(0);
  });

  it.each([null, { type: "result", id: 1, resultJson: 42 }])("fails closed for a malformed message %j", async data => {
    const harness = createWorkerHarness();
    const bridge = new WorkerProtocolBridge({ createWorker: () => harness.worker });
    harness.emitReady();
    const pending = bridge.dispatch({ type: "pause" });
    await flushMicrotasks();
    harness.worker.onmessage?.({ data } as MessageEvent);
    await expect(pending).rejects.toThrow("malformed");
    expect(harness.terminateCalls).toBe(1);
  });

  it("fails closed when a result contains invalid protocol JSON", async () => {
    const harness = createWorkerHarness();
    const bridge = new WorkerProtocolBridge({ createWorker: () => harness.worker });
    harness.emitReady();
    const pending = bridge.dispatch({ type: "pause" });
    await flushMicrotasks();
    harness.worker.onmessage?.({ data: { type: "result", id: 1, resultJson: "{",
      wasmCallDurationMs: 1, returnedUtf8PayloadBytes: 1 } } as MessageEvent);
    await expect(pending).rejects.toThrow();
    await expect(bridge.dispatch({ type: "pause" })).rejects.toThrow();
    expect(harness.postedMessages).toHaveLength(1);
    expect(harness.terminateCalls).toBe(1);
  });

  it("handles message deserialization failure", async () => {
    const harness = createWorkerHarness();
    const bridge = new WorkerProtocolBridge({ createWorker: () => harness.worker });
    harness.emitReady();
    const pending = bridge.dispatch({ type: "pause" });
    await flushMicrotasks();
    harness.worker.onmessageerror?.({} as MessageEvent);
    await expect(pending).rejects.toThrow("could not be read");
    expect(harness.terminateCalls).toBe(1);
  });

  it("allows a slow successful request and clears its watchdog", async () => {
    vi.useFakeTimers();
    const snapshot = createSnapshot();
    const harness = createWorkerHarness();
    const bridge = new WorkerProtocolBridge({ createWorker: () => harness.worker, commandTimeoutMs: 100 });
    harness.emitReady();
    const pending = bridge.dispatch({ type: "pause" });
    await vi.advanceTimersByTimeAsync(99);
    const request = harness.postedMessages[0] as { id: number };
    harness.worker.onmessage?.({ data: { type: "result", id: request.id, wasmCallDurationMs: 99,
      returnedUtf8PayloadBytes: 1, resultJson: JSON.stringify({ protocol: "candu-playtest-v2", accepted: true,
        sequence: 0, command: { type: "pause" }, message: "Paused", diagnostics: [], snapshot }) } } as MessageEvent);
    expect((await pending).accepted).toBe(true);
    await vi.advanceTimersByTimeAsync(100);
    expect(harness.terminateCalls).toBe(0);
    expect(vi.getTimerCount()).toBe(0);
    bridge.dispose();
  });
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
    vi.useFakeTimers();
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
    expect(vi.getTimerCount()).toBe(0);

    expect(harness.postedMessages).toHaveLength(postedCount);
    expect(harness.terminateCalls).toBe(1);
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
    onmessageerror: null,
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
