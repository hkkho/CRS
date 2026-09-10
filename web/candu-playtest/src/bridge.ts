import {
  CORE_BUNDLE_POSITION_COUNT,
  CORE_CHANNEL_COUNT,
  CORE_GRID_HEIGHT,
  CORE_GRID_WIDTH,
  type BridgeStatus,
  type CanduCommand,
  type CanduCommandResponse,
  type CanduDispatchOptions,
  type CanduPlaytestBridge,
  type CanduPlaytestWasmExports,
  type CanduSnapshot,
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
  initializeMode: (mode: "play") => Promise<CanduSnapshot>;
  subscribe: (listener: (status: BridgeStatus, snapshot: CanduSnapshot) => void) => () => void;
  getTransportMetrics?: () => readonly TransportMetric[];
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

  async initialize(mode: "play"): Promise<CanduSnapshot> {
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
  mode?: "play";
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

interface PendingWorkerRequest {
  resolve: (value: WorkerResultMessage) => void;
  reject: (reason: unknown) => void;
}

/**
 * Serializes calls into the .NET WASM runtime so the stateful bridge cannot be
 * re-entered by overlapping UI events or the live clock.
 */
export class WorkerProtocolBridge implements CanduPlaytestBridge {
  readonly status = authoritativeWasmStatus;
  readonly ready: Promise<void>;

  private readonly worker: Worker;
  private readonly pending = new Map<number, PendingWorkerRequest>();
  private nextRequestId = 1;
  private operationQueue: Promise<unknown> = Promise.resolve();
  private lastSnapshot: CanduSnapshot | null = null;
  private resolveReady!: () => void;
  private rejectReady!: (reason: unknown) => void;
  private readySettled = false;
  private readonly metrics: TransportMetric[] = [];

  constructor() {
    this.ready = new Promise<void>((resolve, reject) => {
      this.resolveReady = resolve;
      this.rejectReady = reject;
    });
    this.worker = new Worker(new URL("./wasmWorker.ts", import.meta.url), { type: "module" });
    this.worker.onmessage = (event: MessageEvent<WorkerMessage>) => this.handleMessage(event.data);
    this.worker.onerror = (event: ErrorEvent) => {
      this.failReady(new Error(event.message || "The browser WASM worker failed to load."));
    };
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

  initialize(mode: "play"): Promise<CanduSnapshot> {
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
            preview: null,
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
    const result = this.operationQueue.then(operation, operation);
    this.operationQueue = result.catch(() => undefined);
    return result;
  }

  private request(request: WorkerRequest): Promise<WorkerResultMessage> {
    const id = this.nextRequestId++;
    return new Promise<WorkerResultMessage>((resolve, reject) => {
      this.pending.set(id, { resolve, reject });
      this.worker.postMessage({ ...request, id });
    });
  }

  private handleMessage(message: WorkerMessage): void {
    if (message.type === "ready") {
      if (!this.readySettled) {
        this.readySettled = true;
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
    if (message.type === "error") {
      pending.reject(new Error(message.error));
    } else {
      pending.resolve(message);
    }
  }

  private failReady(error: Error): void {
    if (!this.readySettled) {
      this.readySettled = true;
      this.rejectReady(error);
    }

    for (const pending of this.pending.values()) {
      pending.reject(error);
    }
    this.pending.clear();
  }
}

class AuthoritativeProtocolBridge implements CanduPlaytestBridgeLifecycle {
  private readonly listeners = new Set<(status: BridgeStatus, snapshot: CanduSnapshot) => void>();
  private readonly wasm: WorkerProtocolBridge | null;
  private readonly unavailable: CanduPlaytestBridge;
  private active: CanduPlaytestBridge;
  private activeStatus: BridgeStatus;
  private selectedMode: "play" = "play";
  private readonly settled: Promise<void>;

  constructor() {
    // Keep a shape-compatible snapshot available while the authoritative
    // module loads. It is never an active bridge or a fallback data source.
    const placeholderSnapshot = createUnavailableSnapshot();
    this.unavailable = new UnavailableProtocolBridge(placeholderSnapshot);
    this.active = this.unavailable;
    this.activeStatus = loadingStatus;

    try {
      this.wasm = new WorkerProtocolBridge();
    } catch {
      this.wasm = null;
    }

    if (this.wasm === null) {
      this.settled = Promise.resolve();
      this.activeStatus = unavailableStatus;
      return;
    }

    this.settled = this.wasm.ready
      .then(() => this.wasm!.initialize(this.selectedMode))
      .then((snapshot) => {
        this.active = this.wasm!;
        this.activeStatus = this.wasm!.status;
        this.notify(snapshot);
      })
      .catch((error: unknown) => {
        const reason = error instanceof Error ? error.message : "module unavailable";
        this.active = this.unavailable;
        this.activeStatus = {
          ...unavailableStatus,
          detail: `${AUTHORITATIVE_WASM_UNAVAILABLE_MESSAGE}. ${reason}`,
        };
        this.notify(this.active.getSnapshot());
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

  async initializeMode(mode: "play"): Promise<CanduSnapshot> {
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
    absoluteTiltFraction: 0,
    targetTiltFraction: 0,
    controlMarginFraction: 0,
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
      staticReactivity: 0,
      staticReactivityMethodId: "unavailable",
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

export function createCanduPlaytestBridge(): CanduPlaytestBridgeLifecycle {
  return new AuthoritativeProtocolBridge();
}

export const protocolDescriptor = {
  version: PROTOCOL_VERSION,
  wasmGlobalNames: ["canduPlaytestWasm", "__canduPlaytestWasm", "CanduPlaytestWasm"],
  workerModule: "/src/wasmWorker.ts",
  stagedModule: "/wasm/main.mjs",
  injectedExports: findWasmExports,
};
