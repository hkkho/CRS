import {
  CORE_BUNDLE_POSITION_COUNT,
  CORE_CHANNEL_COUNT,
  CORE_GRID_HEIGHT,
  CORE_GRID_WIDTH,
  type BridgeStatus,
  type CanduCommand,
  type CanduCommandResponse,
  type CanduPlaytestBridge,
  type CanduPlaytestWasmExports,
  type CanduSnapshot,
  findWasmExports,
  parseProtocolResponse,
  parseProtocolSnapshot,
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
  initializeMode: (mode: "play" | "lab") => Promise<CanduSnapshot>;
  subscribe: (listener: (status: BridgeStatus, snapshot: CanduSnapshot) => void) => () => void;
}

export class WasmProtocolBridge implements CanduPlaytestBridge {
  readonly status = authoritativeWasmStatus;

  constructor(private readonly exports: CanduPlaytestWasmExports) {}

  async initialize(mode: "play" | "lab"): Promise<CanduSnapshot> {
    if (this.exports.initialize === undefined) {
      return this.getSnapshot();
    }

    const raw = await this.exports.initialize(JSON.stringify({ protocol: PROTOCOL_VERSION, mode }));
    return parseProtocolResponse(raw).snapshot;
  }

  getSnapshot(): CanduSnapshot {
    return parseProtocolSnapshot(this.exports.getSnapshotJson());
  }

  dispatch(command: CanduCommand): CanduCommandResponse | Promise<CanduCommandResponse> {
    const raw = this.exports.dispatchJson(serializeProtocolCommand(command));
    if (raw instanceof Promise) {
      return raw.then(parseProtocolResponse);
    }
    return parseProtocolResponse(raw);
  }
}

interface WorkerRequest {
  id: number;
  type: "initialize" | "get-snapshot" | "dispatch";
  mode?: "play" | "lab";
  commandJson?: string;
}

interface WorkerResultMessage {
  type: "result";
  id: number;
  resultJson: string;
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
  resolve: (value: string) => void;
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

  initialize(mode: "play" | "lab"): Promise<CanduSnapshot> {
    return this.enqueue(async () => {
      await this.ready;
      const raw = await this.request({ id: 0, type: "initialize", mode });
      const snapshot = parseProtocolResponse(raw).snapshot;
      this.lastSnapshot = snapshot;
      return snapshot;
    });
  }

  dispatch(command: CanduCommand): Promise<CanduCommandResponse> {
    return this.enqueue(async () => {
      await this.ready;
      const raw = await this.request({
        id: 0,
        type: "dispatch",
        commandJson: serializeProtocolCommand(command),
      });
      const response = parseProtocolResponse(raw);
      this.lastSnapshot = response.snapshot;
      return response;
    });
  }

  private enqueue<T>(operation: () => Promise<T>): Promise<T> {
    const result = this.operationQueue.then(operation, operation);
    this.operationQueue = result.catch(() => undefined);
    return result;
  }

  private request(request: WorkerRequest): Promise<string> {
    const id = this.nextRequestId++;
    return new Promise<string>((resolve, reject) => {
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
      pending.resolve(message.resultJson);
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
  private selectedMode: "play" | "lab" = "play";
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

  async dispatch(command: CanduCommand): Promise<CanduCommandResponse> {
    await this.settled;
    return await this.active.dispatch(command);
  }

  async initializeMode(mode: "play" | "lab"): Promise<CanduSnapshot> {
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
