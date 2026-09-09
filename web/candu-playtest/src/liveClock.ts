export const LIVE_CLOCK_WALL_TICK_MS = 100;
export const LIVE_CLOCK_TIMER_INTERVAL_MS = 100;
export const LIVE_CLOCK_MAX_DISPATCH_MS = 1_000;
export const LIVE_CLOCK_MAX_BACKLOG_MS = 60_000;

type TimerHandle = unknown;

export interface LiveClockSchedulerOptions {
  dispatch: (wallMilliseconds: number) => Promise<unknown> | unknown;
  canDispatch?: () => boolean;
  onDispatchError?: (error: unknown) => void;
  now?: () => number;
  setTimer?: (callback: () => void, delayMilliseconds: number) => TimerHandle;
  clearTimer?: (handle: TimerHandle) => void;
  timerIntervalMs?: number;
  wallTickMs?: number;
  maxDispatchMs?: number;
  maxBacklogMs?: number;
}

/**
 * Drives authoritative wall-time advances without treating timer callbacks as
 * elapsed time. The browser timer is only a wake-up hint; the monotonic clock
 * and the bounded backlog are the source of scheduling truth.
 */
export class LiveClockScheduler {
  private readonly dispatch: LiveClockSchedulerOptions["dispatch"];
  private readonly canDispatch: () => boolean;
  private readonly onDispatchError: (error: unknown) => void;
  private readonly now: () => number;
  private readonly setTimer: NonNullable<LiveClockSchedulerOptions["setTimer"]>;
  private readonly clearTimer: NonNullable<LiveClockSchedulerOptions["clearTimer"]>;
  private readonly timerIntervalMs: number;
  private readonly wallTickMs: number;
  private readonly maxDispatchMs: number;
  private readonly maxBacklogMs: number;

  private running = false;
  private visible = true;
  private disposed = false;
  private timerHandle: TimerHandle | null = null;
  private lastSampleMs: number | null = null;
  private backlogMs = 0;
  private dispatchInFlight = false;

  public constructor(options: LiveClockSchedulerOptions) {
    this.dispatch = options.dispatch;
    this.canDispatch = options.canDispatch ?? (() => true);
    this.onDispatchError = options.onDispatchError ?? (() => undefined);
    this.now = options.now ?? defaultMonotonicNow;
    this.setTimer = options.setTimer ?? ((callback, delayMilliseconds) => globalThis.setTimeout(callback, delayMilliseconds));
    this.clearTimer = options.clearTimer ?? ((handle) => globalThis.clearTimeout(handle as number));
    this.timerIntervalMs = positiveOption(options.timerIntervalMs, LIVE_CLOCK_TIMER_INTERVAL_MS, "timerIntervalMs");
    this.wallTickMs = positiveOption(options.wallTickMs, LIVE_CLOCK_WALL_TICK_MS, "wallTickMs");
    this.maxDispatchMs = positiveOption(options.maxDispatchMs, LIVE_CLOCK_MAX_DISPATCH_MS, "maxDispatchMs");
    this.maxBacklogMs = positiveOption(options.maxBacklogMs, LIVE_CLOCK_MAX_BACKLOG_MS, "maxBacklogMs");

    if (this.maxDispatchMs < this.wallTickMs) {
      throw new RangeError("maxDispatchMs must be at least one wall-time control tick.");
    }
    if (this.maxBacklogMs < this.maxDispatchMs) {
      throw new RangeError("maxBacklogMs must be at least one dispatch chunk.");
    }
  }

  public get pendingWallMilliseconds(): number {
    return this.backlogMs;
  }

  public get isDispatchInFlight(): boolean {
    return this.dispatchInFlight;
  }

  /**
   * Applies an authoritative playback lifecycle state. A changed active mode
   * rebases the wall clock but keeps already accrued work. Pausing clears the
   * pending wall-time work so paused time cannot be replayed on resume.
   */
  public setPlaybackState(running: boolean, lifecycleKey: string): void {
    if (this.disposed) {
      return;
    }

    const lifecycleChanged = this.lifecycleKey !== lifecycleKey;
    if (!lifecycleChanged && this.running === running) {
      if (running) {
        this.wake();
      }
      return;
    }

    if (this.running) {
      this.captureVisibleElapsed();
    }

    this.lifecycleKey = lifecycleKey;
    this.running = running;
    if (!running) {
      this.backlogMs = 0;
      this.lastSampleMs = null;
      this.clearScheduledTimer();
      return;
    }

    this.rebaseVisibleClock();
    this.wake();
  }

  /**
   * Visibility transitions explicitly stop accumulation while hidden and
   * rebase on the first visible timestamp, excluding the hidden interval.
   */
  public setVisible(visible: boolean): void {
    if (this.disposed || this.visible === visible) {
      return;
    }

    if (!visible) {
      if (this.running) {
        this.captureVisibleElapsed();
      }
      this.visible = false;
      this.lastSampleMs = null;
      this.clearScheduledTimer();
      return;
    }

    this.visible = true;
    this.rebaseVisibleClock();
    this.wake();
  }

  /**
   * Samples elapsed visible time and retries scheduling. App commands call
   * this when foreground work finishes so retained time is not delayed until
   * the next browser timer callback.
   */
  public wake(): void {
    if (this.disposed || !this.running || !this.visible) {
      return;
    }

    this.captureVisibleElapsed();
    this.pump();
    this.ensureScheduledTimer();
  }

  public dispose(): void {
    if (this.disposed) {
      return;
    }
    this.disposed = true;
    this.running = false;
    this.lastSampleMs = null;
    this.clearScheduledTimer();
  }

  private lifecycleKey: string | null = null;

  private handleTimer = (): void => {
    this.timerHandle = null;
    this.wake();
  };

  private ensureScheduledTimer(): void {
    if (this.timerHandle !== null || this.disposed || !this.running || !this.visible) {
      return;
    }
    this.timerHandle = this.setTimer(this.handleTimer, this.timerIntervalMs);
  }

  private clearScheduledTimer(): void {
    if (this.timerHandle === null) {
      return;
    }
    this.clearTimer(this.timerHandle);
    this.timerHandle = null;
  }

  private rebaseVisibleClock(): void {
    this.lastSampleMs = this.visible ? readFiniteNow(this.now) : null;
  }

  private captureVisibleElapsed(): void {
    if (!this.running || !this.visible) {
      return;
    }

    const currentMs = readFiniteNow(this.now);
    if (this.lastSampleMs === null) {
      this.lastSampleMs = currentMs;
      return;
    }

    const elapsedMs = Math.max(0, currentMs - this.lastSampleMs);
    this.lastSampleMs = currentMs;
    this.backlogMs = Math.min(this.maxBacklogMs, this.backlogMs + elapsedMs);
  }

  private pump(): void {
    if (this.disposed || !this.running || !this.visible || this.dispatchInFlight || !this.canDispatch()) {
      return;
    }

    const alignedAvailableMs = Math.floor(this.backlogMs / this.wallTickMs) * this.wallTickMs;
    if (alignedAvailableMs < this.wallTickMs) {
      return;
    }

    const chunkMs = Math.min(alignedAvailableMs, this.maxDispatchMs);
    this.backlogMs -= chunkMs;
    this.dispatchInFlight = true;

    let operation: Promise<unknown> | unknown;
    try {
      operation = this.dispatch(chunkMs);
    } catch (error) {
      this.finishFailedDispatch(chunkMs, error);
      return;
    }

    Promise.resolve(operation).then(
      () => this.finishSuccessfulDispatch(),
      (error: unknown) => this.finishFailedDispatch(chunkMs, error),
    );
  }

  private finishSuccessfulDispatch(): void {
    this.dispatchInFlight = false;
    if (!this.disposed) {
      this.wake();
    }
  }

  private finishFailedDispatch(chunkMs: number, error: unknown): void {
    this.dispatchInFlight = false;
    this.backlogMs = Math.min(this.maxBacklogMs, this.backlogMs + chunkMs);
    try {
      this.onDispatchError(error);
    } catch {
      // Error reporting must not disable future clock retries.
    }
    if (!this.disposed) {
      // The existing timer supplies a bounded retry cadence. Do not spin on a
      // synchronously failing bridge and starve the rest of the UI thread.
      this.ensureScheduledTimer();
    }
  }
}

function positiveOption(value: number | undefined, fallback: number, name: string): number {
  const resolved = value ?? fallback;
  if (!Number.isFinite(resolved) || resolved <= 0) {
    throw new RangeError(`${name} must be a positive finite number.`);
  }
  return resolved;
}

function readFiniteNow(now: () => number): number {
  const value = now();
  return Number.isFinite(value) ? value : 0;
}

function defaultMonotonicNow(): number {
  if (typeof performance !== "undefined" && typeof performance.now === "function") {
    return performance.now();
  }
  return Date.now();
}
