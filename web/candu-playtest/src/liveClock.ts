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
  /**
   * Retained for source compatibility with the previous scheduler. The live
   * clock now deliberately coalesces to one control quantum instead of
   * retaining a catch-up backlog.
   */
  maxDispatchMs?: number;
  /** @deprecated Pending time is always capped at one wall-time quantum. */
  maxBacklogMs?: number;
}

/**
 * Drives authoritative wall-time advances from a monotonic clock.
 *
 * The scheduler has two important invariants:
 *
 * - only one advance operation can be in flight;
 * - pending wall time is coalesced to at most one control quantum.
 *
 * A slow WASM call therefore loses wall time that elapsed while it was
 * executing instead of replaying stale catch-up work. Simulation time remains
 * whatever the authoritative response reports.
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

  private running = false;
  private visible = true;
  private disposed = false;
  private timerHandle: TimerHandle | null = null;
  private lastSampleMs: number | null = null;
  private pendingWallMs = 0;
  private dispatchInFlight = false;
  private foregroundSuppressionCount = 0;
  private lifecycleKey: string | null = null;

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

    if (this.maxDispatchMs < this.wallTickMs) {
      throw new RangeError("maxDispatchMs must be at least one wall-time control tick.");
    }

    // Validate the legacy option when supplied, but do not use it to permit a
    // second pending quantum. Keeping this validation makes accidental invalid
    // callers fail in the same place as before while preserving the new cap.
    if (options.maxBacklogMs !== undefined) {
      positiveOption(options.maxBacklogMs, LIVE_CLOCK_MAX_BACKLOG_MS, "maxBacklogMs");
    }
  }

  /** Pending wall time is always in [0, wallTickMs]. */
  public get pendingWallMilliseconds(): number {
    return this.pendingWallMs;
  }

  public get isDispatchInFlight(): boolean {
    return this.dispatchInFlight;
  }

  /**
   * Applies the current authoritative playback/lifecycle state.
   *
   * Any transition into a stopped state discards pending clock time. A
   * transition into a running state starts a fresh monotonic interval; it does
   * not replay time spent paused, hidden, inactive, or in a foreground command.
   */
  public setPlaybackState(running: boolean, lifecycleKey: string): void {
    if (this.disposed) {
      return;
    }

    const lifecycleChanged = this.lifecycleKey !== lifecycleKey;
    const runningChanged = this.running !== running;
    this.lifecycleKey = lifecycleKey;

    if (!running) {
      this.running = false;
      this.discardPendingTime();
      this.clearScheduledTimer();
      return;
    }

    this.running = true;
    if (runningChanged || lifecycleChanged || this.lastSampleMs === null) {
      // Playback changes are foreground state changes. Start a new interval
      // instead of allowing time from the previous state to become a tick.
      this.pendingWallMs = 0;
      this.rebaseVisibleClock();
    }

    this.wake();
  }

  /**
   * Suppresses the next clock advance for a foreground command.
   *
   * The caller invokes this before submitting pause/resume, control-target,
   * preview, commit, or other interactive commands. It clears any pending
   * clock quantum and prevents a timer callback from submitting work while the
   * command waits behind the currently executing serialized WASM operation.
   * Calls are counted so multiple foreground commands remain safe.
   */
  public suppressNextAdvance(): void {
    if (this.disposed) {
      return;
    }

    this.foregroundSuppressionCount += 1;
    this.pendingWallMs = 0;
    this.rebaseVisibleClock();
    this.clearScheduledTimer();
  }

  /**
   * Releases one foreground-command suppression token. The final release
   * rebases the clock and arms one fresh timer interval if playback is still
   * active. No wall time from the command's wait is replayed.
   */
  public releaseForegroundCommand(): void {
    if (this.disposed || this.foregroundSuppressionCount === 0) {
      return;
    }

    this.foregroundSuppressionCount -= 1;
    if (this.foregroundSuppressionCount !== 0) {
      return;
    }

    this.pendingWallMs = 0;
    this.rebaseVisibleClock();
    this.wake();
  }

  /**
   * Visibility transitions discard pending wall time and exclude the hidden
   * interval. An already executing WASM call is allowed to finish.
   */
  public setVisible(visible: boolean): void {
    if (this.disposed || this.visible === visible) {
      return;
    }

    this.visible = visible;
    this.discardPendingTime();
    this.clearScheduledTimer();

    if (visible) {
      this.rebaseVisibleClock();
      this.wake();
    }
  }

  /**
   * Samples visible elapsed time and attempts one dispatch. Timer callbacks
   * are only wake-up hints; they never accumulate time while an authoritative
   * advance is executing.
   */
  public wake(): void {
    if (this.disposed || !this.running || !this.visible || this.dispatchInFlight) {
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
    this.foregroundSuppressionCount = 0;
    this.discardPendingTime();
    this.clearScheduledTimer();
  }

  private handleTimer = (): void => {
    this.timerHandle = null;
    this.wake();
  };

  private ensureScheduledTimer(): void {
    if (
      this.timerHandle !== null ||
      this.disposed ||
      !this.running ||
      !this.visible ||
      this.dispatchInFlight ||
      this.foregroundSuppressionCount !== 0 ||
      !this.canDispatch()
    ) {
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

  private discardPendingTime(): void {
    this.pendingWallMs = 0;
    this.lastSampleMs = null;
  }

  private captureVisibleElapsed(): void {
    if (!this.running || !this.visible || this.dispatchInFlight || this.foregroundSuppressionCount !== 0) {
      return;
    }

    const currentMs = readFiniteNow(this.now);
    if (this.lastSampleMs === null) {
      this.lastSampleMs = currentMs;
      return;
    }

    const elapsedMs = Math.max(0, currentMs - this.lastSampleMs);
    this.lastSampleMs = currentMs;
    this.pendingWallMs = Math.min(this.wallTickMs, this.pendingWallMs + elapsedMs);
  }

  private pump(): void {
    if (
      this.disposed ||
      !this.running ||
      !this.visible ||
      this.dispatchInFlight ||
      this.foregroundSuppressionCount !== 0 ||
      !this.canDispatch() ||
      this.pendingWallMs < this.wallTickMs
    ) {
      return;
    }

    // Keep every normal live-clock request deterministic. maxDispatchMs is a
    // compatibility bound, but it cannot turn one scheduler tick into a
    // catch-up batch.
    const chunkMs = Math.min(this.wallTickMs, this.maxDispatchMs);
    this.pendingWallMs = 0;
    this.clearScheduledTimer();
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
    if (this.disposed) {
      return;
    }

    // The operation may have taken seconds. That interval is intentionally
    // excluded from wall-time accounting; the next timer starts a fresh one.
    this.rebaseVisibleClock();
    if (this.running && this.visible && this.foregroundSuppressionCount === 0) {
      this.ensureScheduledTimer();
    }
  }

  private finishFailedDispatch(chunkMs: number, error: unknown): void {
    this.dispatchInFlight = false;
    // Retry exactly one lost control quantum, never the elapsed duration of
    // the failed operation or a larger accumulated backlog.
    this.pendingWallMs = Math.min(this.wallTickMs, Math.max(this.pendingWallMs, chunkMs));
    this.rebaseVisibleClock();
    try {
      this.onDispatchError(error);
    } catch {
      // Error reporting must not disable future clock retries.
    }
    if (!this.disposed) {
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
