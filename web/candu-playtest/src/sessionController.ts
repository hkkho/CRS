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

export interface SessionUpdate {
  status: BridgeStatus;
  snapshot: CanduSnapshot;
  pending: boolean;
  response: CanduCommandResponse | null;
  error: string | null;
}

export type SessionListener = (update: SessionUpdate) => void;

/**
 * Owns the browser bridge boundary and the wall-clock pump. Phaser scenes only
 * render snapshots and send protocol commands through this controller.
 */
export class BridgeSessionController {
  private readonly bridge: CanduPlaytestBridgeLifecycle;
  private readonly scheduler: LiveClockScheduler;
  private readonly listeners = new Set<SessionListener>();
  private readonly unsubscribeBridge: () => void;
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

  public constructor(
    bridge: CanduPlaytestBridgeLifecycle = createCanduPlaytestBridge(),
    schedulerOptions: Pick<LiveClockSchedulerOptions, "now" | "setTimer" | "clearTimer"> = {},
  ) {
    this.bridge = bridge;
    this.statusValue = bridge.status;
    this.snapshotValue = bridge.getSnapshot();
    // Play is the only live session mode. The designer fixture is carried as
    // an optional projection on the play snapshot, so its presence must not
    // gate the wall-clock pump or turn a scene transition into a mode switch.
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
  }

  public stopShift(): void {
    this.active = false;
    this.syncScheduler();
  }

  public async initializeMode(mode: BridgeModeId): Promise<CanduSnapshot> {
    if (!this.statusValue.isWasmAvailable) {
      throw new Error(
        this.statusValue.source === "loading"
          ? "The authoritative bridge is still loading."
          : AUTHORITATIVE_WASM_UNAVAILABLE_MESSAGE,
      );
    }

    this.active = false;
    this.modePending = true;
    this.lastError = null;
    this.lastResponse = null;
    this.syncScheduler();
    this.emit();
    try {
      const snapshot = await this.bridge.initializeMode(mode);
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
    this.scheduler.setVisible(visible);
  }

  public async dispatch(
    command: CanduCommand,
    options?: CanduDispatchOptions,
  ): Promise<CanduCommandResponse> {
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
      const response = await this.bridge.dispatch(command, responseOptions);
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

  private canAdvance(): boolean {
    return this.active &&
      this.statusValue.isWasmAvailable &&
      this.modeValue === "play" &&
      this.pendingCount === 0 &&
      !this.snapshotValue.isPaused &&
      this.snapshotValue.playbackModeId !== "pause" &&
      !this.snapshotValue.rrs.isGameOver;
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
      status: this.statusValue,
      snapshot: this.snapshotValue,
      pending: this.foregroundPendingCount > 0 || this.modePending,
      response,
      error: this.lastError,
    };
  }

  private emit(response: CanduCommandResponse | null = null): void {
    const update = this.createUpdate(response ?? this.lastResponse);
    for (const listener of this.listeners) {
      listener(update);
    }
  }
}

function isEngineeringCommand(command: CanduCommand): boolean {
  return command.type === "configure-cell" ||
    command.type === "solve" ||
    command.type === "reset-lab" ||
    command.type === "lab-refuel" ||
    command.type === "reset";
}

function formatError(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}
