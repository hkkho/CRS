import {
  CORE_BUNDLE_POSITION_COUNT,
  CORE_CHANNEL_COUNT,
  CORE_GRID_HEIGHT,
  CORE_GRID_WIDTH,
  type BridgeStatus,
  type BridgeModeId,
  type CanduCommand,
  type CanduCommandResponse,
  type CanduDispatchOptions,
  type CanduPlaytestBridge,
  type CanduPlaytestWasmExports,
  type CanduSnapshot,
  createUnavailableRrsSnapshot,
  findWasmExports,
  parseProtocolResponseWithSnapshot,
  parseProtocolResponse,
  parseProtocolSnapshot,
  ProtocolResyncRequiredError,
  PROTOCOL_VERSION,
  serializeProtocolCommand,
} from "./protocol";

const authoritativeWasmStatus: BridgeStatus = {
  source: "wasm",
  title: "BROWSER WASM",
  detail: "Authoritative ReactorSim.Game exports running in a dedicated worker",
  isWasmAvailable: true,
  capabilities: ["protocol snapshot", "command dispatch", "solver diagnostics"],
};

const loadingStatus: BridgeStatus = {
  source: "loading",
  title: "LOADING WASM",
  detail: "Attempting the authoritative browser bridge in a dedicated worker",
  isWasmAvailable: false,
  capabilities: ["bridge initialization pending"],
};

export const AUTHORITATIVE_WASM_UNAVAILABLE_MESSAGE = "Authoritative WASM bridge unavailable";

const unavailableStatus: BridgeStatus = {
  source: "unavailable",
  title: "AUTHORITATIVE WASM UNAVAILABLE",
  detail: AUTHORITATIVE_WASM_UNAVAILABLE_MESSAGE,
  isWasmAvailable: false,
  capabilities: [],
};

export interface CanduPlaytestBridgeLifecycle extends CanduPlaytestBridge {
  initializeMode: (mode: BridgeModeId) => Promise<CanduSnapshot>;
  subscribe: (listener: (status: BridgeStatus, snapshot: CanduSnapshot) => void) => () => void;
  getTransportMetrics?: () => readonly TransportMetric[];
  dispose?: () => void;
}

export interface TransportMetric {
  commandType: string;
  responseKind: "full" | "compact" | "error";
  wasmCallDurationMs: number;
  returnedUtf8PayloadBytes: number;
  jsonParseMaterializationDurationMs: number;
  coreReplacementIncluded: boolean;
}

const metricLimit = 256;

function nowMs(): number {
  return typeof performance === "undefined" ? Date.now() : performance.now();
}

function utf8ByteLength(value: string): number {
  return typeof TextEncoder === "undefined"
    ? value.length
    : new TextEncoder().encode(value).byteLength;
}

function recordMetric(
  metrics: TransportMetric[],
  metric: TransportMetric,
): void {
  metrics.push(metric);
  if (metrics.length > metricLimit) {
    metrics.splice(0, metrics.length - metricLimit);
  }
}

function responseKindOf(response: CanduCommandResponse): "full" | "compact" {
  return response.responseKind === "compact" ? "compact" : "full";
}

function coreReplacementIncluded(response: CanduCommandResponse): boolean {
  return response.coreReplacement !== undefined && response.coreReplacement !== null;
}

function resolveDispatchOptions(
  options: CanduDispatchOptions,
  lastSnapshot: CanduSnapshot | null,
): CanduDispatchOptions {
  if (
    options.responseMode !== "compact" ||
    options.baseSequence !== undefined ||
    lastSnapshot === null
  ) {
    return options;
  }

  // Compact commands issued by the session controller intentionally omit the
  // base while they wait in the serialized queue. Resolve it only when the
  // operation is about to be sent, after any preceding command has materialized
  // its authoritative response. An explicit base remains an explicit caller
  // assertion and must continue through unchanged for mismatch detection.
  return {
    ...options,
    baseSequence: lastSnapshot.sequence,
  };
}

export class WasmProtocolBridge implements CanduPlaytestBridge {
  readonly status = authoritativeWasmStatus;
  private readonly metrics: TransportMetric[] = [];
  private lastSnapshot: CanduSnapshot | null = null;

  constructor(private readonly exports: CanduPlaytestWasmExports) {}

  getTransportMetrics(): readonly TransportMetric[] {
    return this.metrics.slice();
  }

  async initialize(mode: BridgeModeId): Promise<CanduSnapshot> {
    if (this.exports.initialize === undefined) {
      return this.getSnapshot();
    }

    const callStarted = nowMs();
    const raw = await this.exports.initialize(JSON.stringify({ protocol: PROTOCOL_VERSION, mode }));
    const callDuration = nowMs() - callStarted;
    const parseStarted = nowMs();
    try {
      const response = parseProtocolResponse(raw);
      recordMetric(this.metrics, {
        commandType: "initialize",
        responseKind: responseKindOf(response),
        wasmCallDurationMs: callDuration,
        returnedUtf8PayloadBytes: utf8ByteLength(raw),
        jsonParseMaterializationDurationMs: nowMs() - parseStarted,
        coreReplacementIncluded: coreReplacementIncluded(response),
      });
      this.lastSnapshot = response.snapshot;
      return response.snapshot;
    } catch (error) {
      recordMetric(this.metrics, {
        commandType: "initialize",
        responseKind: "error",
        wasmCallDurationMs: callDuration,
        returnedUtf8PayloadBytes: utf8ByteLength(raw),
        jsonParseMaterializationDurationMs: nowMs() - parseStarted,
        coreReplacementIncluded: false,
      });
      throw error;
    }
  }

  getSnapshot(): CanduSnapshot {
    const callStarted = nowMs();
    const raw = this.exports.getSnapshotJson();
    const callDuration = nowMs() - callStarted;
    const parseStarted = nowMs();
    try {
      const snapshot = parseProtocolSnapshot(raw);
      recordMetric(this.metrics, {
        commandType: "snapshot",
        responseKind: "full",
        wasmCallDurationMs: callDuration,
        returnedUtf8PayloadBytes: utf8ByteLength(raw),
        jsonParseMaterializationDurationMs: nowMs() - parseStarted,
        coreReplacementIncluded: false,
      });
      this.lastSnapshot = snapshot;
      return snapshot;
    } catch (error) {
      recordMetric(this.metrics, {
        commandType: "snapshot",
        responseKind: "error",
        wasmCallDurationMs: callDuration,
        returnedUtf8PayloadBytes: utf8ByteLength(raw),
        jsonParseMaterializationDurationMs: nowMs() - parseStarted,
        coreReplacementIncluded: false,
      });
      throw error;
    }
  }

  async dispatch(command: CanduCommand, options: CanduDispatchOptions = {}): Promise<CanduCommandResponse> {
    const commandJson = serializeProtocolCommand(
      command,
      resolveDispatchOptions(options, this.lastSnapshot),
    );
    const callStarted = nowMs();
    const raw = await this.exports.dispatchJson(commandJson);
    const callDuration = nowMs() - callStarted;
    const parseStarted = nowMs();
    try {
      let response: CanduCommandResponse;
      try {
        response = parseProtocolResponse(raw, this.lastSnapshot ?? undefined);
      } catch (error) {
        if (!(error instanceof ProtocolResyncRequiredError)) {
          throw error;
        }
        const snapshotRaw = this.exports.getSnapshotJson();
        const snapshotParseStarted = nowMs();
        const snapshot = parseProtocolSnapshot(snapshotRaw);
        response = parseProtocolResponseWithSnapshot(raw, this.lastSnapshot ?? undefined, snapshot);
        recordMetric(this.metrics, {
          commandType: "snapshot",
          responseKind: "full",
          wasmCallDurationMs: 0,
          returnedUtf8PayloadBytes: utf8ByteLength(snapshotRaw),
          jsonParseMaterializationDurationMs: nowMs() - snapshotParseStarted,
          coreReplacementIncluded: false,
        });
      }
      // Direct callers can use compact mode only when they provide the base
      // snapshot through the bridge instance. The authoritative lifecycle
      // bridge supplies that state; direct bridges retain the last materialized
      // snapshot below through their normal dispatch path.
      this.lastSnapshot = response.snapshot;
      recordMetric(this.metrics, {
        commandType: command.type,
        responseKind: responseKindOf(response),
        wasmCallDurationMs: callDuration,
        returnedUtf8PayloadBytes: utf8ByteLength(raw),
        jsonParseMaterializationDurationMs: nowMs() - parseStarted,
        coreReplacementIncluded: coreReplacementIncluded(response),
      });
      return response;
    } catch (error) {
      recordMetric(this.metrics, {
        commandType: command.type,
        responseKind: "error",
        wasmCallDurationMs: callDuration,
        returnedUtf8PayloadBytes: utf8ByteLength(raw),
        jsonParseMaterializationDurationMs: nowMs() - parseStarted,
        coreReplacementIncluded: false,
      });
      throw error;
    }
  }
}

interface WorkerRequest {
  id: number;
  type: "initialize" | "get-snapshot" | "dispatch";
  mode?: BridgeModeId;
  commandJson?: string;
}

interface WorkerResultMessage {
  type: "result";
  id: number;
  resultJson: string;
  wasmCallDurationMs: number;
  returnedUtf8PayloadBytes: number;
}

interface WorkerErrorMessage {
  type: "error";
  id: number;
  error: string;
}

interface WorkerReadyMessage {
  type: "ready" | "load-error";
  error?: string;
}

type WorkerMessage = WorkerResultMessage | WorkerErrorMessage | WorkerReadyMessage;

export interface WorkerProtocolWorker {
  onmessage: ((event: MessageEvent) => void) | null;
  onmessageerror: ((event: MessageEvent) => void) | null;
  onerror: ((event: ErrorEvent) => void) | null;
  postMessage(message: unknown): void;
  terminate(): void;
}

export type WorkerProtocolWorkerFactory = () => WorkerProtocolWorker;

export interface WorkerProtocolBridgeOptions {
  createWorker?: WorkerProtocolWorkerFactory;
  onFatalError?: (error: Error) => void;
  startupTimeoutMs?: number;
  commandTimeoutMs?: number;
}

// Generous failure ceilings, not performance targets. See runtime-profile.md:
// measured 60x ticks are ~3.7s; allow cold loading and much slower devices.
export const WORKER_STARTUP_TIMEOUT_MS = 120_000;
export const WORKER_COMMAND_TIMEOUT_MS = 180_000;

interface PendingWorkerRequest {
  resolve: (value: WorkerResultMessage) => void;
  reject: (reason: unknown) => void;
  timer: ReturnType<typeof setTimeout>;
}

/**
 * Serializes calls into the .NET WASM runtime so the stateful bridge cannot be
 * re-entered by overlapping UI events or the live clock.
 */
export class WorkerProtocolBridge implements CanduPlaytestBridge {
  readonly status = authoritativeWasmStatus;
  readonly ready: Promise<void>;

  private readonly worker: WorkerProtocolWorker;
  private readonly onFatalError: (error: Error) => void;
  private readonly pending = new Map<number, PendingWorkerRequest>();
  private nextRequestId = 1;
  private operationQueue: Promise<unknown> = Promise.resolve();
  private lastSnapshot: CanduSnapshot | null = null;
  private resolveReady!: () => void;
  private rejectReady!: (reason: unknown) => void;
  private readySettled = false;
  private lifecycleState: "active" | "failed" | "disposed" = "active";
  private terminalError: Error | null = null;
  private workerTerminated = false;
  private readonly metrics: TransportMetric[] = [];
  private readonly startupTimeoutMs: number;
  private readonly commandTimeoutMs: number;
  private startupTimer: ReturnType<typeof setTimeout> | null = null;

  constructor(options: WorkerProtocolBridgeOptions = {}) {
    this.startupTimeoutMs = timeoutOption(options.startupTimeoutMs, WORKER_STARTUP_TIMEOUT_MS);
    this.commandTimeoutMs = timeoutOption(options.commandTimeoutMs, WORKER_COMMAND_TIMEOUT_MS);
    this.onFatalError = options.onFatalError ?? (() => undefined);
    this.ready = new Promise<void>((resolve, reject) => {
      this.resolveReady = resolve;
      this.rejectReady = reject;
    });
    this.worker = (options.createWorker ?? createDefaultWorker)();
    this.worker.onmessage = (event: MessageEvent) => this.handleMessage(event.data);
    this.worker.onmessageerror = () => this.failReady(new Error("The browser WASM worker response could not be read."));
    this.worker.onerror = (event: ErrorEvent) => {
      this.failReady(new Error(event.message || "The browser WASM worker failed to load."));
    };
    this.startupTimer = setTimeout(() => this.failReady(new Error(
      "The reactor did not finish loading before the startup deadline.")), this.startupTimeoutMs);
  }

  getSnapshot(): CanduSnapshot {
    if (this.lastSnapshot === null) {
      throw new Error("The browser WASM bridge has not initialized yet.");
    }
    return this.lastSnapshot;
  }

  getTransportMetrics(): readonly TransportMetric[] {
    return this.metrics.slice();
  }

  dispose(): void {
    if (this.lifecycleState !== "active") {
      return;
    }

    const error = new Error("The browser WASM bridge was disposed.");
    this.lifecycleState = "disposed";
    this.terminalError = error;
    if (!this.readySettled) {
      this.readySettled = true;
      this.rejectReady(error);
    }
    this.rejectPending(error);
    this.terminateWorker();
  }

  initialize(mode: BridgeModeId): Promise<CanduSnapshot> {
    return this.enqueue(async () => {
      await this.ready;
      const result = await this.request({ id: 0, type: "initialize", mode });
      const parseStarted = nowMs();
      let snapshot: CanduSnapshot;
      let response: CanduCommandResponse;
      try {
        try {
          response = parseProtocolResponse(result.resultJson);
          snapshot = response.snapshot;
        } catch {
          // Older compatible hosts may expose only GetSnapshotJson and
          // DispatchJson. Treat that exact snapshot as a full initialization
          // result rather than inventing a browser-side state.
          snapshot = parseProtocolSnapshot(result.resultJson);
          response = {
            protocol: PROTOCOL_VERSION,
            accepted: true,
            sequence: snapshot.sequence,
            command: { type: "reset" },
            message: "Authoritative snapshot initialized.",
            diagnostics: [],
            snapshot,
          };
        }
      } catch (error) {
        recordMetric(this.metrics, {
          commandType: "initialize",
          responseKind: "error",
          wasmCallDurationMs: result.wasmCallDurationMs,
          returnedUtf8PayloadBytes: result.returnedUtf8PayloadBytes,
          jsonParseMaterializationDurationMs: nowMs() - parseStarted,
          coreReplacementIncluded: false,
        });
        throw error;
      }
      recordMetric(this.metrics, {
        commandType: "initialize",
        responseKind: responseKindOf(response),
        wasmCallDurationMs: result.wasmCallDurationMs,
        returnedUtf8PayloadBytes: result.returnedUtf8PayloadBytes,
        jsonParseMaterializationDurationMs: nowMs() - parseStarted,
        coreReplacementIncluded: coreReplacementIncluded(response),
      });
      this.lastSnapshot = snapshot;
      return snapshot;
    });
  }

  dispatch(command: CanduCommand, options: CanduDispatchOptions = {}): Promise<CanduCommandResponse> {
    return this.enqueue(async () => {
      await this.ready;
      const result = await this.request({
        id: 0,
        type: "dispatch",
        commandJson: serializeProtocolCommand(
          command,
          resolveDispatchOptions(options, this.lastSnapshot),
        ),
      });
      const parseStarted = nowMs();
      let response: CanduCommandResponse;
      try {
        response = parseProtocolResponse(result.resultJson, this.lastSnapshot ?? undefined);
      } catch (error) {
        if (!(error instanceof ProtocolResyncRequiredError)) {
          recordMetric(this.metrics, {
            commandType: command.type,
            responseKind: "error",
            wasmCallDurationMs: result.wasmCallDurationMs,
            returnedUtf8PayloadBytes: result.returnedUtf8PayloadBytes,
            jsonParseMaterializationDurationMs: nowMs() - parseStarted,
            coreReplacementIncluded: false,
          });
          throw error;
        }

        const snapshotResult = await this.request({ id: 0, type: "get-snapshot" });
        const snapshotParseStarted = nowMs();
        const snapshot = parseProtocolSnapshot(snapshotResult.resultJson);
        response = parseProtocolResponseWithSnapshot(
          result.resultJson,
          this.lastSnapshot ?? undefined,
          snapshot,
        );
        recordMetric(this.metrics, {
          commandType: "snapshot",
          responseKind: "full",
          wasmCallDurationMs: snapshotResult.wasmCallDurationMs,
          returnedUtf8PayloadBytes: snapshotResult.returnedUtf8PayloadBytes,
          jsonParseMaterializationDurationMs: nowMs() - snapshotParseStarted,
          coreReplacementIncluded: false,
        });
      }
      recordMetric(this.metrics, {
        commandType: command.type,
        responseKind: responseKindOf(response),
        wasmCallDurationMs: result.wasmCallDurationMs,
        returnedUtf8PayloadBytes: result.returnedUtf8PayloadBytes,
        jsonParseMaterializationDurationMs: nowMs() - parseStarted,
        coreReplacementIncluded: coreReplacementIncluded(response),
      });
      this.lastSnapshot = response.snapshot;
      return response;
    });
  }

  private enqueue<T>(operation: () => Promise<T>): Promise<T> {
    const result = this.operationQueue.then(operation, operation).catch((error: unknown) => {
      this.failReady(toError(error, "The authoritative response could not be processed."));
      throw error;
    });
    this.operationQueue = result.catch(() => undefined);
    return result;
  }

  private request(request: WorkerRequest): Promise<WorkerResultMessage> {
    if (this.terminalError !== null) {
      return Promise.reject(this.terminalError);
    }

    const id = this.nextRequestId++;
    return new Promise<WorkerResultMessage>((resolve, reject) => {
      if (this.terminalError !== null) {
        reject(this.terminalError);
        return;
      }

      const timeoutMs = request.type === "initialize" ? this.startupTimeoutMs : this.commandTimeoutMs;
      const timer = setTimeout(() => this.failReady(new Error(
        "The reactor stopped responding. The last order's result is unknown; it will not be retried.")), timeoutMs);
      this.pending.set(id, { resolve, reject, timer });
      try {
        this.worker.postMessage({ ...request, id });
      } catch (error) {
        const reason = toError(error, "The browser WASM worker could not accept a request.");
        this.failReady(reason);
        reject(this.terminalError ?? reason);
      }
    });
  }

  private handleMessage(value: unknown): void {
    if (this.lifecycleState !== "active") return;
    if (!isWorkerMessage(value)) {
      this.failReady(new Error("The browser WASM worker sent a malformed response."));
      return;
    }
    const message = value;
    if (message.type === "ready") {
      if (!this.readySettled) {
        this.readySettled = true;
        this.clearStartupTimer();
        this.resolveReady();
      }
      return;
    }

    if (message.type === "load-error") {
      this.failReady(new Error(message.error ?? "The browser WASM module could not be loaded."));
      return;
    }

    if (message.type !== "result" && message.type !== "error") {
      return;
    }

    const pending = this.pending.get(message.id);
    if (pending === undefined) {
      return;
    }
    this.pending.delete(message.id);
    clearTimeout(pending.timer);
    if (message.type === "error") {
      // An exception may occur after state committed. Do not let the clock retry.
      this.failReady(new Error(message.error));
      pending.reject(new Error(message.error));
    } else {
      pending.resolve(message);
    }
  }

  private failReady(error: Error): void {
    if (this.lifecycleState !== "active") {
      return;
    }

    this.lifecycleState = "failed";
    this.terminalError = error;
    if (!this.readySettled) {
      this.readySettled = true;
      this.rejectReady(error);
    }

    this.rejectPending(error);
    this.terminateWorker();
    try {
      this.onFatalError(error);
    } catch {
      // Fatal-state cleanup must not depend on subscriber behavior.
    }
  }

  private rejectPending(error: Error): void {
    for (const pending of this.pending.values()) {
      clearTimeout(pending.timer);
      pending.reject(error);
    }
    this.pending.clear();
  }

  private terminateWorker(): void {
    this.clearStartupTimer();
    if (this.workerTerminated) {
      return;
    }

    this.workerTerminated = true;
    this.worker.onmessage = null;
    this.worker.onmessageerror = null;
    this.worker.onerror = null;
    try {
      this.worker.terminate();
    } catch {
      // The bridge is already terminal even if a host-specific terminate call fails.
    }
  }

  private clearStartupTimer(): void {
    if (this.startupTimer !== null) clearTimeout(this.startupTimer);
    this.startupTimer = null;
  }
}

function timeoutOption(value: number | undefined, fallback: number): number {
  const timeout = value ?? fallback;
  if (!Number.isFinite(timeout) || timeout <= 0 || timeout > 2_147_483_647) {
    throw new RangeError("Worker timeout must be a positive finite timer duration.");
  }
  return timeout;
}

function isWorkerMessage(value: unknown): value is WorkerMessage {
  if (typeof value !== "object" || value === null) return false;
  const message = value as Record<string, unknown>;
  if (message.type === "ready") return true;
  if (message.type === "load-error") return message.error === undefined || typeof message.error === "string";
  if (!Number.isSafeInteger(message.id) || (message.id as number) <= 0) return false;
  if (message.type === "error") return typeof message.error === "string";
  return message.type === "result" && typeof message.resultJson === "string" &&
    typeof message.wasmCallDurationMs === "number" && Number.isFinite(message.wasmCallDurationMs) && message.wasmCallDurationMs >= 0 &&
    typeof message.returnedUtf8PayloadBytes === "number" && Number.isSafeInteger(message.returnedUtf8PayloadBytes) && message.returnedUtf8PayloadBytes >= 0;
}

function createDefaultWorker(): WorkerProtocolWorker {
  return new Worker(new URL("./wasmWorker.ts", import.meta.url), { type: "module" });
}

function toError(error: unknown, fallback: string): Error {
  return error instanceof Error ? error : new Error(error === undefined ? fallback : String(error));
}

class AuthoritativeProtocolBridge implements CanduPlaytestBridgeLifecycle {
  private readonly listeners = new Set<(status: BridgeStatus, snapshot: CanduSnapshot) => void>();
  private readonly wasm: WorkerProtocolBridge | null;
  private readonly unavailable: CanduPlaytestBridge;
  private active: CanduPlaytestBridge;
  private activeStatus: BridgeStatus;
  private selectedMode: BridgeModeId = "play";
  private readonly settled: Promise<void>;
  private failedClosed = false;
  private disposed = false;

  constructor(options: Omit<WorkerProtocolBridgeOptions, "onFatalError"> = {}) {
    // Keep a shape-compatible snapshot available while the authoritative
    // module loads. It is never an active bridge or a fallback data source.
    const placeholderSnapshot = createUnavailableSnapshot();
    this.unavailable = new UnavailableProtocolBridge(placeholderSnapshot);
    this.active = this.unavailable;
    this.activeStatus = loadingStatus;

    try {
      this.wasm = new WorkerProtocolBridge({
        ...options,
        onFatalError: (error) => this.transitionToUnavailable(error),
      });
    } catch {
      this.wasm = null;
    }

    if (this.wasm === null) {
      this.settled = Promise.resolve();
      this.failedClosed = true;
      this.activeStatus = unavailableStatus;
      return;
    }

    this.settled = this.wasm.ready
      .then(() => this.wasm!.initialize(this.selectedMode))
      .then((snapshot) => {
        if (this.disposed || this.failedClosed) {
          return;
        }
        this.active = this.wasm!;
        this.activeStatus = this.wasm!.status;
        this.notify(snapshot);
      })
      .catch((error: unknown) => {
        this.transitionToUnavailable(error);
      });
  }

  get status(): BridgeStatus {
    return this.activeStatus;
  }

  getSnapshot(): CanduSnapshot {
    return this.active.getSnapshot();
  }

  async dispatch(command: CanduCommand, options: CanduDispatchOptions = {}): Promise<CanduCommandResponse> {
    await this.settled;
    return await this.active.dispatch(command, options);
  }

  getTransportMetrics(): readonly TransportMetric[] {
    return this.wasm?.getTransportMetrics() ?? [];
  }

  dispose(): void {
    if (this.disposed) {
      return;
    }

    this.disposed = true;
    this.active = this.unavailable;
    this.activeStatus = unavailableStatus;
    this.listeners.clear();
    this.wasm?.dispose();
  }

  async initializeMode(mode: BridgeModeId): Promise<CanduSnapshot> {
    this.selectedMode = mode;
    await this.settled;
    if (this.active === this.wasm && this.wasm !== null) {
      const snapshot = await this.wasm.initialize(mode);
      this.notify(snapshot);
      return snapshot;
    }

    throw new Error(AUTHORITATIVE_WASM_UNAVAILABLE_MESSAGE);
  }

  subscribe(listener: (status: BridgeStatus, snapshot: CanduSnapshot) => void): () => void {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  private transitionToUnavailable(error: unknown): void {
    if (this.disposed || this.failedClosed) {
      return;
    }

    this.failedClosed = true;
    const reason = error instanceof Error ? error.message : "module unavailable";
    this.active = this.unavailable;
    this.activeStatus = {
      ...unavailableStatus,
      detail: `${AUTHORITATIVE_WASM_UNAVAILABLE_MESSAGE}. ${reason}`,
    };
    this.notify(this.active.getSnapshot());
  }

  private notify(snapshot: CanduSnapshot): void {
    for (const listener of this.listeners) {
      listener(this.activeStatus, snapshot);
    }
  }
}

class UnavailableProtocolBridge implements CanduPlaytestBridge {
  readonly status = unavailableStatus;

  constructor(private readonly snapshot: CanduSnapshot) {}

  getSnapshot(): CanduSnapshot {
    return this.snapshot;
  }

  dispatch(): Promise<CanduCommandResponse> {
    return Promise.reject(new Error(AUTHORITATIVE_WASM_UNAVAILABLE_MESSAGE));
  }
}

function createUnavailableSnapshot(): CanduSnapshot {
  return {
    protocol: PROTOCOL_VERSION,
    source: "wasm",
    sequence: 0,
    scenarioId: "unavailable",
    dataPackId: "unavailable",
    simulationTimeSeconds: 0,
    wallElapsedSeconds: 0,
    normalizedPowerFraction: 0,
    targetPowerFraction: 0,
    axialTiltFraction: 0,
    rrsReserveFraction: 0,
    deviceAvailableFraction: 0,
    pendingActionCount: 0,
    scoreTotal: 0,
    scoreDelta: 0,
    isPaused: true,
    playbackModeId: "pause",
    freshBundlesAvailable: 0,
    refuellingOperationCount: 0,
    lastRefuelledChannel: -1,
    lastRefuellingDirectionId: null,
    lastRefuellingShiftCount: 0,
    physics: {
      sourceId: "unavailable",
      formulationId: "unavailable",
      shapeMethodId: "unavailable",
      amplitudeMethodId: "unavailable",
      reactivityMethodId: "unavailable",
      solveState: "unavailable",
      isAuthoritative: false,
      bindingVersion: 0,
      referencePowerWatts: 1,
      powerAmplitude: 0,
      actualPowerFraction: 0,
      targetPowerWatts: 0,
      totalPowerWatts: 0,
      meanChannelPowerWatts: 0,
      meanBundlePowerWatts: 0,
      effectiveK: 1,
      reactivity: 0,
      weightedPerturbationReactivity: 0,
      reactivityNumerator: 0,
      reactivityDenominator: 0,
      reactivityIdentity: "unavailable",
      reactivityBindingDigestHex: "",
      coreReactivity: 0,
      compensatedNetReactivity: 0,
      compensationState: 0,
      compensationCommand: 0,
      compensationLowerBound: -0.25,
      compensationUpperBound: 0.25,
      compensationSaturated: false,
      compensationResponseTimeSeconds: 4,
      cadenceIdentity: "deterministic-regulated-steady-state-long-step-v1",
      powerBalanceRelativeError: 0,
      solverIdentity: "unavailable",
      solverIterationCount: 0,
      solverResidualRelativeInfinity: 0,
    },
    xenon: {
      stateIdentity: "unavailable",
      stateDigestHex: "",
      stateVersion: 0,
      simulationTimeSeconds: 0,
      nodeCount: 0,
      couplingIdentity: "unavailable",
      hasCoupling: false,
      baseCoefficientDigestHex: "",
      dynamicXenonDigestHex: "",
      effectiveCoefficientDigestHex: "",
      meanI135NumberDensityM3: 0,
      maxI135NumberDensityM3: 0,
      meanXe135NumberDensityM3: 0,
      maxXe135NumberDensityM3: 0,
      meanDynamicAbsorptionGroup1PerM: 0,
      maxDynamicAbsorptionGroup1PerM: 0,
      meanDynamicAbsorptionGroup2PerM: 0,
      maxDynamicAbsorptionGroup2PerM: 0,
      selectedChannelIndex: -1,
      selectedChannel: null,
    },
    rrs: createUnavailableRrsSnapshot(),
    core: {
      channelCount: CORE_CHANNEL_COUNT,
      bundlePositionCount: CORE_BUNDLE_POSITION_COUNT,
      gridWidth: CORE_GRID_WIDTH,
      gridHeight: CORE_GRID_HEIGHT,
      channels: [],
    },
    diagnostics: {
      convergence: {
        state: "unavailable",
        iterations: 0,
        residual: 0,
        relativePowerError: 0,
        lastSolveMilliseconds: 0,
        solverLabel: "unavailable",
      },
      checks: [],
    },
    lastEvent: null,
  };
}

export function createCanduPlaytestBridge(
  options: Omit<WorkerProtocolBridgeOptions, "onFatalError"> = {},
): CanduPlaytestBridgeLifecycle {
  return new AuthoritativeProtocolBridge(options);
}

export const protocolDescriptor = {
  version: PROTOCOL_VERSION,
  wasmGlobalNames: ["canduPlaytestWasm", "__canduPlaytestWasm", "CanduPlaytestWasm"],
  workerModule: "/src/wasmWorker.ts",
  stagedModule: "/wasm/main.mjs",
  injectedExports: findWasmExports,
};
