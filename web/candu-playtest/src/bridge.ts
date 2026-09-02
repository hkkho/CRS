import {
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
import { createSyntheticFixtureBridge } from "./fixture";

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

class HybridProtocolBridge implements CanduPlaytestBridgeLifecycle {
  private readonly fixture: CanduPlaytestBridge;
  private readonly compatibilityFixtureEnabled: boolean;
  private readonly listeners = new Set<(status: BridgeStatus, snapshot: CanduSnapshot) => void>();
  private readonly wasm: WorkerProtocolBridge | null;
  private readonly unavailable: CanduPlaytestBridge;
  private active: CanduPlaytestBridge;
  private activeStatus: BridgeStatus;
  private selectedMode: "play" | "lab" = "play";
  private readonly settled: Promise<void>;

  constructor() {
    this.fixture = createSyntheticFixtureBridge();
    this.compatibilityFixtureEnabled = isCompatibilityFixtureEnabled();
    this.unavailable = new UnavailableProtocolBridge(this.fixture.getSnapshot());
    this.active = this.compatibilityFixtureEnabled ? this.fixture : this.unavailable;
    this.activeStatus = loadingStatus;

    if (!this.compatibilityFixtureEnabled) {
      this.activeStatus = unavailableStatus;
    }

    try {
      this.wasm = new WorkerProtocolBridge();
    } catch {
      this.wasm = null;
    }

    if (this.wasm === null) {
      this.settled = Promise.resolve();
      if (this.compatibilityFixtureEnabled) {
        this.activeStatus = this.fixture.status;
      }
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
        if (this.compatibilityFixtureEnabled) {
          this.active = this.fixture;
          this.activeStatus = {
            ...this.fixture.status,
            detail: `Deterministic compatibility data · browser WASM unavailable (${reason})`,
          };
        } else {
          this.active = this.unavailable;
          this.activeStatus = {
            ...unavailableStatus,
            detail: `${AUTHORITATIVE_WASM_UNAVAILABLE_MESSAGE}. ${reason}`,
          };
        }
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

    if (this.active === this.unavailable) {
      throw new Error(AUTHORITATIVE_WASM_UNAVAILABLE_MESSAGE);
    }

    const reset = await this.fixture.dispatch({ type: "reset" });
    this.notify(reset.snapshot);
    return reset.snapshot;
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

function isCompatibilityFixtureEnabled(): boolean {
  if (import.meta.env.DEV) {
    return true;
  }

  if (typeof window === "undefined") {
    return false;
  }

  const parameters = new URLSearchParams(window.location.search);
  return parameters.get("compatibility") === "fixture" || parameters.get("debug") === "fixture";
}

export function createCanduPlaytestBridge(): CanduPlaytestBridgeLifecycle {
  return new HybridProtocolBridge();
}

export const protocolDescriptor = {
  version: PROTOCOL_VERSION,
  wasmGlobalNames: ["canduPlaytestWasm", "__canduPlaytestWasm", "CanduPlaytestWasm"],
  workerModule: "/src/wasmWorker.ts",
  stagedModule: "/wasm/main.mjs",
  injectedExports: findWasmExports,
};
