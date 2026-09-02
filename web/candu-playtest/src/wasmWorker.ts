import {
  findWasmExports,
  PROTOCOL_VERSION,
  type CanduPlaytestWasmExports,
} from "./protocol";

type WorkerRequest =
  | { id: number; type: "initialize"; mode: "play" | "lab" }
  | { id: number; type: "get-snapshot" }
  | { id: number; type: "dispatch"; commandJson: string };

type WorkerResponse =
  | { type: "ready" }
  | { type: "load-error"; error: string }
  | { type: "result"; id: number; resultJson: string }
  | { type: "error"; id: number; error: string };

interface WorkerScope {
  addEventListener: (type: "message", listener: (event: MessageEvent<WorkerRequest>) => void) => void;
  postMessage: (message: WorkerResponse) => void;
  location: Location;
}

const scope = globalThis as unknown as WorkerScope;
let exportsPromise: Promise<CanduPlaytestWasmExports>;
let requestQueue = Promise.resolve();

scope.addEventListener("message", (event) => {
  requestQueue = requestQueue.then(async () => {
    try {
      const api = await getWasmExports();
      const resultJson = await dispatchRequest(api, event.data);
      scope.postMessage({ type: "result", id: event.data.id, resultJson });
    } catch (error) {
      scope.postMessage({ type: "error", id: event.data.id, error: formatError(error) });
    }
  });
});

exportsPromise = loadWasm();

async function loadWasm(): Promise<CanduPlaytestWasmExports> {
  try {
    const moduleUrl = new URL("/wasm/main.mjs", scope.location.href).href;
    await import(/* @vite-ignore */ moduleUrl);
    const wasmExports = findWasmExports();
    if (wasmExports === null) {
      throw new Error("The browser WASM module loaded without canduPlaytestWasm exports.");
    }
    scope.postMessage({ type: "ready" });
    return wasmExports;
  } catch (error) {
    scope.postMessage({ type: "load-error", error: formatError(error) });
    throw error;
  }
}

async function getWasmExports(): Promise<CanduPlaytestWasmExports> {
  return exportsPromise;
}

async function dispatchRequest(api: CanduPlaytestWasmExports, request: WorkerRequest): Promise<string> {
  switch (request.type) {
    case "initialize":
      if (api.initialize === undefined) {
        return api.getSnapshotJson();
      }
      return normalizeJson(await api.initialize(JSON.stringify({ protocol: PROTOCOL_VERSION, mode: request.mode })));
    case "get-snapshot":
      return normalizeJson(await api.getSnapshotJson());
    case "dispatch":
      return normalizeJson(await api.dispatchJson(request.commandJson));
  }
}

function normalizeJson(value: string | unknown): string {
  if (typeof value === "string") {
    return value;
  }
  const serialized = JSON.stringify(value);
  if (serialized === undefined) {
    throw new Error("The browser WASM bridge returned a non-serializable result.");
  }
  return serialized;
}

function formatError(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}
