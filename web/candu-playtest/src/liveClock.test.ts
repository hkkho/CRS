import { describe, expect, it } from "vitest";
import { LiveClockScheduler } from "./liveClock";

describe("LiveClockScheduler", () => {
  it("retains visible elapsed time while an advance is in flight and drains it afterward", async () => {
    const resolvers: Array<() => void> = [];
    const dispatched: number[] = [];
    let activeDispatches = 0;
    let maximumActiveDispatches = 0;
    const harness = createHarness((wallMilliseconds) => {
      dispatched.push(wallMilliseconds);
      activeDispatches += 1;
      maximumActiveDispatches = Math.max(maximumActiveDispatches, activeDispatches);
      return new Promise<void>((resolve) => {
        resolvers.push(() => {
          activeDispatches -= 1;
          resolve();
        });
      });
    });

    harness.scheduler.setPlaybackState(true, "1x");
    harness.advanceBy(550);

    expect(dispatched).toEqual([100]);
    expect(harness.scheduler.pendingWallMilliseconds).toBe(400);
    expect(maximumActiveDispatches).toBe(1);

    resolvers.shift()?.();
    await flushMicrotasks();

    expect(dispatched).toEqual([100, 400]);
    expect(maximumActiveDispatches).toBe(1);
    expect(harness.scheduler.pendingWallMilliseconds).toBe(50);

    resolvers.shift()?.();
    await flushMicrotasks();
    expect(harness.scheduler.isDispatchInFlight).toBe(false);
  });

  it("aligns commands to 100 ms control ticks while retaining the sub-tick remainder", async () => {
    const dispatched: number[] = [];
    const harness = createHarness(async (wallMilliseconds) => {
      dispatched.push(wallMilliseconds);
    });
    harness.scheduler.setPlaybackState(true, "1x");

    harness.advanceBy(250);
    await flushMicrotasks();

    expect(dispatched).toEqual([100, 100]);
    expect(harness.scheduler.pendingWallMilliseconds).toBe(50);

    harness.advanceBy(50);
    await flushMicrotasks();
    expect(dispatched).toEqual([100, 100, 100]);
  });

  it("retains elapsed time while a foreground command temporarily blocks dispatch", async () => {
    const dispatched: number[] = [];
    let foregroundCommandPending = true;
    const harness = createHarness(
      async (wallMilliseconds) => {
        dispatched.push(wallMilliseconds);
      },
      undefined,
      { canDispatch: () => !foregroundCommandPending },
    );
    harness.scheduler.setPlaybackState(true, "1x");

    harness.advanceBy(500);
    expect(dispatched).toEqual([]);
    expect(harness.scheduler.pendingWallMilliseconds).toBe(500);

    foregroundCommandPending = false;
    harness.scheduler.wake();
    await flushMicrotasks();
    expect(dispatched).toEqual([500]);
  });

  it("does not accrue paused or hidden time and rebases when playback resumes", async () => {
    const dispatched: number[] = [];
    const harness = createHarness(async (wallMilliseconds) => {
      dispatched.push(wallMilliseconds);
    });
    harness.scheduler.setPlaybackState(true, "1x");

    harness.advanceBy(100);
    await flushMicrotasks();
    harness.scheduler.setPlaybackState(false, "pause");
    harness.advanceBy(5_000);
    expect(dispatched).toEqual([100]);

    harness.scheduler.setPlaybackState(true, "1x");
    harness.advanceBy(99);
    expect(dispatched).toEqual([100]);
    harness.advanceBy(1);
    await flushMicrotasks();
    expect(dispatched).toEqual([100, 100]);

    harness.scheduler.setVisible(false);
    harness.advanceBy(5_000);
    harness.scheduler.setVisible(true);
    expect(dispatched).toEqual([100, 100]);
    harness.advanceBy(99);
    expect(dispatched).toEqual([100, 100]);
    harness.advanceBy(1);
    await flushMicrotasks();
    expect(dispatched).toEqual([100, 100, 100]);
  });

  it("requeues a failed chunk and continues retrying on a later timer", async () => {
    const dispatched: number[] = [];
    const errors: unknown[] = [];
    let shouldFail = true;
    const harness = createHarness(
      (wallMilliseconds) => {
        dispatched.push(wallMilliseconds);
        return shouldFail ? Promise.reject(new Error("temporary worker failure")) : Promise.resolve();
      },
      (error) => errors.push(error),
    );
    harness.scheduler.setPlaybackState(true, "1x");

    harness.advanceBy(100);
    await flushMicrotasks();
    expect(dispatched).toEqual([100]);
    expect(errors).toHaveLength(1);
    expect(harness.scheduler.pendingWallMilliseconds).toBe(100);

    shouldFail = false;
    harness.advanceBy(100);
    await flushMicrotasks();
    expect(dispatched).toEqual([100, 200]);
    expect(harness.scheduler.pendingWallMilliseconds).toBe(0);

    harness.advanceBy(100);
    await flushMicrotasks();
    expect(dispatched).toEqual([100, 200, 100]);
  });

  it("caps catch-up backlog and bounds each dispatch chunk", async () => {
    const resolvers: Array<() => void> = [];
    const dispatched: number[] = [];
    const harness = createHarness(
      (wallMilliseconds) => {
        dispatched.push(wallMilliseconds);
        return new Promise<void>((resolve) => resolvers.push(resolve));
      },
      undefined,
      { maxDispatchMs: 200, maxBacklogMs: 500 },
    );
    harness.scheduler.setPlaybackState(true, "1x");

    harness.advanceBy(2_000);
    expect(dispatched).toEqual([100]);
    expect(harness.scheduler.pendingWallMilliseconds).toBe(500);

    resolvers.shift()?.();
    await flushMicrotasks();
    expect(dispatched).toEqual([100, 200]);
    expect(harness.scheduler.pendingWallMilliseconds).toBe(300);
    expect(dispatched.every((value) => value <= 200 && value % 100 === 0)).toBe(true);
  });
});

interface TimerEntry {
  dueAt: number;
  callback: () => void;
}

function createHarness(
  dispatch: (wallMilliseconds: number) => Promise<unknown> | unknown,
  onDispatchError?: (error: unknown) => void,
  options: { canDispatch?: () => boolean; maxDispatchMs?: number; maxBacklogMs?: number } = {},
) {
  let now = 0;
  let nextTimerId = 1;
  const timers = new Map<number, TimerEntry>();
  const scheduler = new LiveClockScheduler({
    dispatch,
    canDispatch: options.canDispatch,
    onDispatchError,
    now: () => now,
    setTimer: (callback, delayMilliseconds) => {
      const timerId = nextTimerId++;
      timers.set(timerId, { dueAt: now + delayMilliseconds, callback });
      return timerId;
    },
    clearTimer: (handle) => {
      timers.delete(handle as number);
    },
    ...options,
  });

  return {
    scheduler,
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

async function flushMicrotasks(): Promise<void> {
  await Promise.resolve();
  await Promise.resolve();
}
