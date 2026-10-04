import { SessionPresentation } from "./SessionPresentation";
import { isRunTerminal } from "./protocol";
import {
  AUTHORITATIVE_WASM_UNAVAILABLE_MESSAGE,
  createCanduPlaytestBridge,
  type CanduPlaytestBridgeLifecycle,
} from "./bridge";
import {
  LiveClockScheduler,
  type LiveClockSchedulerOptions,
} from "./liveClock";
import type {
  BridgeModeId,
  BridgeStatus,
  CanduCommand,
  CanduCommandResponse,
  CanduDispatchOptions,
  CanduSnapshot,
} from "./protocol";
import { ReactorHistory } from "./studio/ReactorHistory";
import { ObservedPace, type PaceReading } from './observedPace';
import { RunJournal, parseRunSave, type RunSave } from './runSave';

export interface SessionUpdate {
  pace?: PaceReading;
  changeKind?: "snapshot" | "status";
  status: BridgeStatus;
  snapshot: CanduSnapshot;
  pending: boolean;
  response: CanduCommandResponse | null;
  error: string | null;
}

export type SessionListener = (update: SessionUpdate) => void;

/**
 * Owns the browser bridge boundary and the wall-clock pump. Native views only
 * render snapshots and send protocol commands through this controller.
 */
export class BridgeSessionController {
  public readonly presentation = new SessionPresentation();
  public readonly history = new ReactorHistory();
  private bridge: CanduPlaytestBridgeLifecycle;
  public readonly journal = new RunJournal();
  private readonly scheduler: LiveClockScheduler;
  private readonly listeners = new Set<SessionListener>();
  private unsubscribeBridge: () => void;
  private statusValue: BridgeStatus;
  private snapshotValue: CanduSnapshot;
  private pendingCount = 0;
  private foregroundPendingCount = 0;
  private modePending = false;
  private modeValue: BridgeModeId;
  private active = false;
  private disposed = false;
  private lastError: string | null = null;
  private lastResponse: CanduCommandResponse | null = null;
  private lastEmittedSnapshot: CanduSnapshot | null = null;
  private readonly observedPace = new ObservedPace();
  private readonly now: () => number;
  private visible = true;
  private restoring = false;

  public constructor(
    bridge: CanduPlaytestBridgeLifecycle = createCanduPlaytestBridge(),
    schedulerOptions: Pick<LiveClockSchedulerOptions, "now" | "setTimer" | "clearTimer"> = {},
  ) {
    this.bridge = bridge;
    this.now = schedulerOptions.now ?? (() => performance.now());
    this.statusValue = bridge.status;
    this.snapshotValue = bridge.getSnapshot();
    this.history.record(this.snapshotValue);
    // Play is the only live session mode.
    this.modeValue = "play";
    this.unsubscribeBridge = bridge.subscribe((status, snapshot) => {
      this.statusValue = status;
      this.snapshotValue = snapshot;
      this.emit();
    });

    this.scheduler = new LiveClockScheduler({
      ...schedulerOptions,
      dispatch: (wallMilliseconds) => this.dispatch({ type: "advance", wallMilliseconds }),
      canDispatch: () => this.canAdvance(),
      onDispatchError: (error) => {
        this.lastError = formatError(error);
        this.emit();
      },
    });
    this.syncScheduler();
  }

  public get status(): BridgeStatus {
    return this.statusValue;
  }

  public get snapshot(): CanduSnapshot {
    return this.snapshotValue;
  }

  public get isPending(): boolean {
    return this.foregroundPendingCount > 0 || this.modePending;
  }

  public get mode(): BridgeModeId {
    return this.modeValue;
  }

  public get error(): string | null {
    return this.lastError;
  }

  public get lastResponseValue(): CanduCommandResponse | null {
    return this.lastResponse;
  }

  public subscribe(listener: SessionListener): () => void {
    this.listeners.add(listener);
    listener(this.createUpdate());
    return () => this.listeners.delete(listener);
  }

  public startShift(): void {
    this.active = true;
    this.syncScheduler();
    this.emit();
  }

  public stopShift(): void {
    this.active = false;
    this.syncScheduler();
    this.emit();
  }

  public async initializeMode(mode: BridgeModeId): Promise<CanduSnapshot> {
    if (this.restoring) throw new Error('Please wait until the saved run finishes restoring.');
    if (!this.statusValue.isWasmAvailable) {
      throw new Error(
        this.statusValue.source === "loading"
          ? "The authoritative bridge is still loading."
          : AUTHORITATIVE_WASM_UNAVAILABLE_MESSAGE,
      );
    }

    this.active = false;
    this.modePending = true;
    this.presentation.clear();
    this.lastError = null;
    this.lastResponse = null;
    this.syncScheduler();
    this.emit();
    try {
      const snapshot = await this.bridge.initializeMode(mode);
      this.journal.clear();
      this.history.clear();
      this.modeValue = mode;
      this.snapshotValue = snapshot;
      this.emit();
      return snapshot;
    } catch (error) {
      this.lastError = formatError(error);
      this.emit();
      throw error;
    } finally {
      this.modePending = false;
      this.syncScheduler();
      this.emit();
    }
  }

  public setVisible(visible: boolean): void {
    this.visible = visible;
    this.scheduler.setVisible(visible);
    this.emit();
  }

  public async dispatch(
    command: CanduCommand,
    options?: CanduDispatchOptions,
  ): Promise<CanduCommandResponse> {
    if (this.restoring) throw new Error('Please wait until the saved run finishes restoring.');
    if (!this.statusValue.isWasmAvailable) {
      throw new Error(
        this.statusValue.source === "loading"
          ? "The authoritative bridge is still loading."
          : AUTHORITATIVE_WASM_UNAVAILABLE_MESSAGE,
      );
    }

    const isClockTick = command.type === "advance";
    this.pendingCount += 1;
    if (!isClockTick) {
      this.foregroundPendingCount += 1;
      this.scheduler.suppressNextAdvance();
    }
    this.lastError = null;
    this.emit();
    try {
      const responseOptions = options ?? (isEngineeringCommand(command)
        ? { responseMode: "full" as const }
        : { responseMode: "compact" as const });
      const previous = this.snapshotValue;
      const response = await this.bridge.dispatch(command, responseOptions);
      this.journal.record(command, response, responseOptions.responseMode ?? 'full');
      this.presentation.accept(response, previous);
      if (command.type === "reset" && response.accepted) this.history.clear();
      this.lastResponse = response;
      this.snapshotValue = response.snapshot;
      this.emit(response);
      return response;
    } catch (error) {
      this.lastError = formatError(error);
      this.emit();
      throw error;
    } finally {
      this.pendingCount = Math.max(0, this.pendingCount - 1);
      if (!isClockTick) {
        this.foregroundPendingCount = Math.max(0, this.foregroundPendingCount - 1);
        this.scheduler.releaseForegroundCommand();
      }
      this.syncScheduler();
      this.emit();
    }
  }

  public dispose(): void {
    if (this.disposed) {
      return;
    }

    this.disposed = true;
    this.active = false;
    this.unsubscribeBridge();
    this.scheduler.dispose();
    this.bridge.dispose?.();
    this.listeners.clear();
  }

  public captureRun(): RunSave { return this.journal.capture(this.snapshotValue); }

  /** Restore in a separate WASM worker; failures leave the current reactor untouched. */
  public async restoreRun(input: unknown, progress: (done: number, total: number) => void = () => {},
    factory: () => CanduPlaytestBridgeLifecycle = createCanduPlaytestBridge): Promise<void> {
    const save = parseRunSave(input);
    if (save.ended) throw new Error('Ended runs cannot be continued.');
    if (save.dataPackId !== this.snapshotValue.dataPackId || save.scorePolicyId !== this.snapshotValue.scorePolicyId)
      throw new Error('This save uses a different simulation or scoring version.');
    if (this.pendingCount || this.restoring) throw new Error('Wait for the current command to finish.');
    this.restoring = true; this.modePending = true; this.syncScheduler(); this.emit();
    let candidate: CanduPlaytestBridgeLifecycle | null = null;
    try {
      candidate = factory();
      await candidate.initializeMode('play');
      let result: CanduCommandResponse | null = null;
      for (let i = 0; i < save.commands.length; i++) {
        if (this.disposed) throw new Error('Restore cancelled.');
        const record = save.commands[i];
        result = await candidate.dispatch(record.command, { responseMode: record.responseMode });
        if (result.accepted !== record.accepted) throw new Error('The saved run is incompatible with this simulation.');
        progress(i + 1, save.commands.length);
      }
      if (!result || result.stateDigest !== save.stateDigest || isRunTerminal(result.snapshot))
        throw new Error('Saved state verification failed. The current run was preserved.');
      const paused = await candidate.dispatch({ type: 'pause' }, { responseMode: 'compact' });
      if (!paused.accepted) throw new Error('Could not pause the restored run.');
      this.unsubscribeBridge(); this.bridge.dispose?.(); this.bridge = candidate; candidate = null;
      this.unsubscribeBridge = this.bridge.subscribe((status, snapshot) => {
        this.statusValue = status; this.snapshotValue = snapshot; this.emit();
      });
      this.statusValue = this.bridge.status; this.snapshotValue = paused.snapshot;
      this.journal.adopt(save); this.journal.record({ type: 'pause' }, paused, 'compact');
      this.lastResponse = paused; this.lastError = null; this.presentation.clear(); this.history.clear();
    } finally {
      candidate?.dispose?.(); this.restoring = false; this.modePending = false; this.syncScheduler(); this.emit();
    }
  }

  private canAdvance(): boolean {
    return !this.restoring && this.isPlaybackActive() && this.pendingCount === 0;
  }

  private isPlaybackActive(): boolean {
    return this.active &&
      this.statusValue.isWasmAvailable &&
      this.modeValue === "play" &&
      !this.snapshotValue.isPaused &&
      this.snapshotValue.playbackModeId !== "pause" &&
      !isRunTerminal(this.snapshotValue);
  }

  private syncScheduler(): void {
    const isRunning = this.canAdvance();
    const lifecycleKey = `${this.active ? "active" : "title"}:${this.snapshotValue.playbackModeId}:${this.snapshotValue.isPaused ? "paused" : "running"}`;
    this.scheduler.setPlaybackState(isRunning, lifecycleKey);
    if (isRunning) {
      this.scheduler.wake();
    }
  }

  private createUpdate(response: CanduCommandResponse | null = this.lastResponse): SessionUpdate {
    return {
      pace: {
        requested: this.snapshotValue.isPaused ? 'Paused' : this.snapshotValue.playbackModeId.replace('x', '×'),
        simulatedMinutesPerSecond: this.observedPace.observe(this.isPlaybackActive() && this.visible ? this.snapshotValue.playbackModeId : null,
          this.snapshotValue.simulationTimeSeconds, this.now()),
        solving: this.pendingCount > 0,
      },
      status: this.statusValue,
      snapshot: this.snapshotValue,
      pending: this.foregroundPendingCount > 0 || this.modePending,
      response,
      error: this.lastError,
    };
  }

  private emit(response: CanduCommandResponse | null = null): void {
    this.history.record(this.snapshotValue);
    const update = this.createUpdate(response ?? this.lastResponse);
    update.changeKind = this.snapshotValue === this.lastEmittedSnapshot ? "status" : "snapshot";
    this.lastEmittedSnapshot = this.snapshotValue;
    for (const listener of this.listeners) {
      listener(update);
    }
  }
}

function isEngineeringCommand(command: CanduCommand): boolean {
  return command.type === "configure-cell" ||
    command.type === "configure-zone-layout" ||
    command.type === "solve" ||
    command.type === "reset";
}

function formatError(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}
