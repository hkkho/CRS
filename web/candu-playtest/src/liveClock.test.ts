import { describe, expect, it } from "vitest";
import { LiveClockScheduler } from "./liveClock";

describe("LiveClockScheduler", () => {
  it("allows one advance in flight and coalesces pending time to one quantum", async () => {
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
    harness.advanceBy(100);
    harness.advanceBy(10_000);
    harness.scheduler.wake();

    expect(dispatched).toEqual([100]);
    expect(harness.scheduler.pendingWallMilliseconds).toBe(0);
    expect(harness.scheduler.isDispatchInFlight).toBe(true);
    expect(maximumActiveDispatches).toBe(1);

    resolvers.shift()?.();
    await flushMicrotasks();

    expect(dispatched).toEqual([100]);
    expect(harness.scheduler.pendingWallMilliseconds).toBe(0);
    expect(harness.scheduler.isDispatchInFlight).toBe(false);

    harness.advanceBy(100);
    expect(dispatched).toEqual([100, 100]);
    expect(maximumActiveDispatches).toBe(1);
    harness.scheduler.dispose();
  });

  it("caps blocked wall time at one 100 ms pending quantum", () => {
    const dispatched: number[] = [];
    let canDispatch = false;
    const harness = createHarness(
      (wallMilliseconds) => {
        dispatched.push(wallMilliseconds);
      },
      { canDispatch: () => canDispatch },
    );
    harness.scheduler.setPlaybackState(true, "1x");

    harness.advanceBy(10_000);
    harness.scheduler.wake();

    expect(dispatched).toEqual([]);
    expect(harness.scheduler.pendingWallMilliseconds).toBe(100);

    canDispatch = true;
    harness.scheduler.wake();
    expect(dispatched).toEqual([100]);
    expect(harness.scheduler.pendingWallMilliseconds).toBe(0);
    harness.scheduler.dispose();
  });

  it("does not create catch-up advances after a ten-second blocked call", async () => {
    const resolvers: Array<() => void> = [];
    const dispatched: number[] = [];
    const harness = createHarness((wallMilliseconds) => {
      dispatched.push(wallMilliseconds);
      return new Promise<void>((resolve) => resolvers.push(resolve));
    });
    harness.scheduler.setPlaybackState(true, "1x");

    harness.advanceBy(100);
    harness.advanceBy(10_000);
    expect(dispatched).toEqual([100]);
    expect(harness.scheduler.pendingWallMilliseconds).toBe(0);

    resolvers.shift()?.();
    await flushMicrotasks();
    expect(dispatched).toEqual([100]);

    // Completion rebases at the current monotonic timestamp. Exactly one new
    // quantum becomes eligible after the next fresh timer interval.
    harness.advanceBy(99);
    expect(dispatched).toEqual([100]);
    harness.advanceBy(1);
    expect(dispatched).toEqual([100, 100]);
    harness.scheduler.dispose();
  });

  it("suppresses the next clock advance for a foreground operation", async () => {
    const resolvers: Array<() => void> = [];
    const dispatched: number[] = [];
    const harness = createHarness((wallMilliseconds) => {
      dispatched.push(wallMilliseconds);
      return new Promise<void>((resolve) => resolvers.push(resolve));
    });
    harness.scheduler.setPlaybackState(true, "1x");

    harness.advanceBy(100);
    harness.scheduler.suppressNextAdvance();
    harness.advanceBy(10_000);

    expect(dispatched).toEqual([100]);
    expect(harness.scheduler.pendingWallMilliseconds).toBe(0);

    resolvers.shift()?.();
    await flushMicrotasks();
    expect(dispatched).toEqual([100]);

    harness.scheduler.releaseForegroundCommand();
    harness.advanceBy(99);
    expect(dispatched).toEqual([100]);
    harness.advanceBy(1);
    expect(dispatched).toEqual([100, 100]);
    harness.scheduler.dispose();
  });

  it("discards pending time when paused and starts a fresh interval on resume", () => {
    let canDispatch = false;
    const dispatched: number[] = [];
    const harness = createHarness(
      (wallMilliseconds) => dispatched.push(wallMilliseconds),
      { canDispatch: () => canDispatch },
    );
    harness.scheduler.setPlaybackState(true, "1x");
    harness.advanceBy(5_000);
    harness.scheduler.wake();
    expect(harness.scheduler.pendingWallMilliseconds).toBe(100);

    harness.scheduler.setPlaybackState(false, "pause");
    expect(harness.scheduler.pendingWallMilliseconds).toBe(0);
    harness.advanceBy(5_000);

    canDispatch = true;
    harness.scheduler.setPlaybackState(true, "1x");
    harness.advanceBy(99);
    expect(dispatched).toEqual([]);
    harness.advanceBy(1);
    expect(dispatched).toEqual([100]);
    harness.scheduler.dispose();
  });

  it("discards pending time on visibility loss and excludes the hidden interval", () => {
    let canDispatch = false;
    const dispatched: number[] = [];
    const harness = createHarness(
      (wallMilliseconds) => dispatched.push(wallMilliseconds),
      { canDispatch: () => canDispatch },
    );
    harness.scheduler.setPlaybackState(true, "1x");
    harness.advanceBy(5_000);
    harness.scheduler.wake();
    expect(harness.scheduler.pendingWallMilliseconds).toBe(100);

    harness.scheduler.setVisible(false);
    expect(harness.scheduler.pendingWallMilliseconds).toBe(0);
    harness.advanceBy(5_000);

    canDispatch = true;
    harness.scheduler.setVisible(true);
    harness.advanceBy(99);
    expect(dispatched).toEqual([]);
    harness.advanceBy(1);
    expect(dispatched).toEqual([100]);
    harness.scheduler.dispose();
  });

  it("discards pending time on disposal and never dispatches afterward", () => {
    let canDispatch = false;
    const dispatched: number[] = [];
    const harness = createHarness(
      (wallMilliseconds) => dispatched.push(wallMilliseconds),
      { canDispatch: () => canDispatch },
    );
    harness.scheduler.setPlaybackState(true, "1x");
    harness.advanceBy(5_000);
    harness.scheduler.wake();
    expect(harness.scheduler.pendingWallMilliseconds).toBe(100);

    harness.scheduler.dispose();
    expect(harness.scheduler.pendingWallMilliseconds).toBe(0);
    canDispatch = true;
    harness.advanceBy(5_000);
    harness.scheduler.wake();
    harness.scheduler.setPlaybackState(true, "1x");

    expect(dispatched).toEqual([]);
  });

  it("retries exactly one failed quantum on a later timer", async () => {
    const dispatched: number[] = [];
    const errors: unknown[] = [];
    let shouldFail = true;
    const harness = createHarness(
      (wallMilliseconds) => {
        dispatched.push(wallMilliseconds);
        return shouldFail ? Promise.reject(new Error("temporary worker failure")) : Promise.resolve();
      },
      { onDispatchError: (error) => errors.push(error) },
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
    expect(dispatched).toEqual([100, 100]);
    expect(harness.scheduler.pendingWallMilliseconds).toBe(0);
    harness.scheduler.dispose();
  });
});

interface TimerEntry {
  dueAt: number;
  callback: () => void;
}

function createHarness(
  dispatch: (wallMilliseconds: number) => Promise<unknown> | unknown,
  options: {
    canDispatch?: () => boolean;
    onDispatchError?: (error: unknown) => void;
  } = {},
) {
  let now = 0;
  let nextTimerId = 1;
  const timers = new Map<number, TimerEntry>();
  const scheduler = new LiveClockScheduler({
    dispatch,
    canDispatch: options.canDispatch,
    onDispatchError: options.onDispatchError,
    now: () => now,
    setTimer: (callback, delayMilliseconds) => {
      const timerId = nextTimerId++;
      timers.set(timerId, { dueAt: now + delayMilliseconds, callback });
      return timerId;
    },
    clearTimer: (handle) => {
      timers.delete(handle as number);
    },
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
