import { createHash } from "node:crypto";
import { chromium } from "playwright";

const options = parseOptions(process.argv.slice(2));
const baseUrl = options.url ?? process.env.PLAYTEST_URL;
if (!baseUrl) {
  throw new Error("Pass a playtest URL as the first argument or set PLAYTEST_URL.");
}

const warmSamples = Number(options.warmSamples ?? 3);
if (!Number.isInteger(warmSamples) || warmSamples < 0 || warmSamples > 10) {
  throw new Error("--warm-samples must be an integer from 0 through 10.");
}

const targetUrl = new URL(baseUrl);
const moduleUrl = new URL("/wasm/main.mjs", targetUrl.origin).toString();
const buildInfoUrl = new URL("/wasm/build-info.json", targetUrl.origin).toString();
const consoleErrors = [];
const pageErrors = [];
const browser = await chromium.launch({ headless: true });

function observe(page) {
  page.on("console", (message) => {
    if (message.type() === "error") consoleErrors.push(message.text());
  });
  page.on("pageerror", (error) => pageErrors.push(error.message));
}

async function checkApplication(page) {
  observe(page);
  await page.goto(targetUrl.toString(), { waitUntil: "domcontentloaded" });
  await page.locator("canvas").waitFor({ state: "attached", timeout: 10_000 });
  await page.waitForFunction(
    () => document.querySelector("#status-mirror")?.textContent?.includes("play mode online"),
    undefined,
    { timeout: 60_000 },
  );
  const mirror = await page.locator("#status-mirror").textContent();
  if (!mirror?.includes("380 channels")) {
    throw new Error(`The browser did not report the full play snapshot: ${mirror}`);
  }
  const viewport = await page.locator("canvas").evaluate((canvas) => ({
    width: canvas.width,
    height: canvas.height,
  }));
  if (viewport.width !== 1600 || viewport.height !== 900) {
    throw new Error(`Unexpected Phaser game viewport: ${viewport.width} × ${viewport.height}.`);
  }
  return { mirror, viewport };
}

async function loadBenchmarkBridge(page) {
  observe(page);
  await checkApplication(page);
  return page.evaluate(async ({ moduleUrl: wasmModuleUrl }) => {
    await import(/* @vite-ignore */ wasmModuleUrl);
    const api = globalThis.canduPlaytestWasm;
    if (api === undefined) {
      throw new Error("The browser WASM module did not expose canduPlaytestWasm.");
    }

    try {
      const { WasmProtocolBridge } = await import("/src/bridge.ts?benchmark=1");
      globalThis.__canduBenchmarkBridge = new WasmProtocolBridge(api);
      return "WasmProtocolBridge";
    } catch {
      globalThis.__canduBenchmarkBridge = null;
      globalThis.__canduBenchmarkApi = api;
      return "direct-json-parse";
    }
  }, { moduleUrl });
}

async function initialize(page) {
  return page.evaluate(async () => {
    const bridge = globalThis.__canduBenchmarkBridge;
    if (bridge !== null && bridge !== undefined) {
      const snapshot = await bridge.initialize("play");
      const metric = bridge.getTransportMetrics().at(-1);
      return {
        snapshotSequence: snapshot.sequence,
        metric: metric === undefined ? null : { ...metric },
      };
    }

    const api = globalThis.__canduBenchmarkApi;
    const started = performance.now();
    const raw = await api.initialize(JSON.stringify({ protocol: "candu-playtest-v1", mode: "play" }));
    const wasmCallDurationMs = performance.now() - started;
    const parseStarted = performance.now();
    const response = JSON.parse(raw);
    const jsonParseMaterializationDurationMs = performance.now() - parseStarted;
    return {
      snapshotSequence: response.snapshot?.sequence ?? response.sequence,
      stateDigest: response.stateDigest ?? null,
      replayDigest: response.replayDigest ?? null,
      metric: {
        commandType: "initialize",
        responseKind: response.responseKind === "compact" ? "compact" : "full",
        wasmCallDurationMs,
        returnedUtf8PayloadBytes: new TextEncoder().encode(raw).byteLength,
        jsonParseMaterializationDurationMs,
        coreReplacementIncluded: response.coreReplacement !== undefined && response.coreReplacement !== null,
      },
    };
  });
}

async function dispatch(page, command) {
  return page.evaluate(async (payload) => {
    const commandValue = { ...payload };
    const baseSequence = commandValue.__baseSequence;
    delete commandValue.__baseSequence;

    function serializeResponse(value) {
      return {
        accepted: value.accepted,
        sequence: value.sequence,
        responseKind: value.responseKind ?? "full",
        stateDigest: value.stateDigest ?? null,
        replayDigest: value.replayDigest ?? null,
        requiresResync: value.requiresResync === true,
        coreReplacementIncluded: value.coreReplacement !== undefined && value.coreReplacement !== null,
        hasPreview: value.preview !== undefined && value.preview !== null,
        snapshotSequence: value.snapshot?.sequence ?? null,
      };
    }

    const bridge = globalThis.__canduBenchmarkBridge;
    if (bridge !== null && bridge !== undefined) {
      const response = await bridge.dispatch(commandValue, { responseMode: "compact", baseSequence });
      const metric = bridge.getTransportMetrics().at(-1);
      return {
        response: serializeResponse(response),
        metric: metric === undefined ? null : { ...metric },
      };
    }

    const api = globalThis.__canduBenchmarkApi;
    const commandJson = JSON.stringify({
      protocol: "candu-playtest-v1",
      type: "command",
      responseMode: "compact",
      baseSequence,
      payload: commandValue,
    });
    const started = performance.now();
    const raw = await api.dispatchJson(commandJson);
    const wasmCallDurationMs = performance.now() - started;
    const parseStarted = performance.now();
    const response = JSON.parse(raw);
    const jsonParseMaterializationDurationMs = performance.now() - parseStarted;
    return {
      response: serializeResponse(response),
      metric: {
        commandType: payload.type,
        responseKind: response.responseKind === "compact" ? "compact" : "full",
        wasmCallDurationMs,
        returnedUtf8PayloadBytes: new TextEncoder().encode(raw).byteLength,
        jsonParseMaterializationDurationMs,
        coreReplacementIncluded: response.coreReplacement !== undefined && response.coreReplacement !== null,
      },
    };

  }, { ...command, __baseSequence: command.__baseSequence });
}

async function getSnapshot(page) {
  return page.evaluate(async () => {
    const bridge = globalThis.__canduBenchmarkBridge;
    if (bridge !== null && bridge !== undefined) {
      return bridge.getSnapshot();
    }

    const api = globalThis.__canduBenchmarkApi;
    const raw = await api.getSnapshotJson();
    return JSON.parse(raw);
  });
}

async function runTrace(page) {
  const commands = [
    { label: "pause", type: "pause" },
    { label: "resume", type: "resume" },
    { label: "advance-100-ms", type: "advance", wallMilliseconds: 100 },
    { label: "advance-1000-ms", type: "advance", wallMilliseconds: 1000 },
    {
      label: "preview-refuel",
      type: "preview-refuel",
      request: {
        channelIndex: 210,
        directionId: "toward-end-a",
        shiftCount: 4,
        fuelTypeId: "NAT-U-SYNTHETIC",
      },
    },
  ];
  let sequence = 0;
  const steps = [];
  for (const entry of commands) {
    const { label, ...command } = entry;
    const result = await dispatch(page, { ...command, __baseSequence: sequence });
    const response = result.response;
    if (result.metric === null) {
      throw new Error(`${label} did not expose a transport metric.`);
    }
    if (response.requiresResync) {
      throw new Error(`${label} required a compact resync unexpectedly.`);
    }
    if (response.accepted !== true) {
      throw new Error(`${label} was rejected by the authoritative bridge.`);
    }
    sequence = response.sequence;
    steps.push({
      label,
      command,
      accepted: response.accepted,
      sequence: response.sequence,
      responseKind: response.responseKind,
      stateDigest: response.stateDigest,
      replayDigest: response.replayDigest,
      coreReplacementIncluded: response.coreReplacementIncluded,
      hasPreview: response.hasPreview,
      metric: result.metric,
    });
  }
  const snapshot = await getSnapshot(page);
  const semantic = semanticSnapshot(snapshot);
  return {
    steps,
    finalSequence: sequence,
    finalSemanticSnapshotBytes: new TextEncoder().encode(semantic).byteLength,
    finalSemanticSnapshotSha256: sha256(semantic),
  };
}

function semanticSnapshot(snapshot) {
  const copy = JSON.parse(JSON.stringify(snapshot));
  if (copy.diagnostics?.convergence !== undefined) {
    delete copy.diagnostics.convergence.lastSolveMilliseconds;
  }
  return canonicalJson(copy);
}

function canonicalJson(value) {
  if (Array.isArray(value)) return `[${value.map(canonicalJson).join(",")}]`;
  if (value !== null && typeof value === "object") {
    return `{${Object.keys(value).sort().map((key) => `${JSON.stringify(key)}:${canonicalJson(value[key])}`).join(",")}}`;
  }
  return JSON.stringify(value);
}

function sha256(value) {
  return createHash("sha256").update(value).digest("hex");
}

function summarize(samples) {
  const byLabel = {};
  for (const sample of samples) {
    for (const step of sample.trace.steps) {
      const values = byLabel[step.label] ??= [];
      values.push(step.metric);
    }
  }
  return Object.fromEntries(Object.entries(byLabel).map(([label, metrics]) => [label, {
    wasmCallDurationMs: metrics.map((metric) => metric.wasmCallDurationMs),
    jsonParseMaterializationDurationMs: metrics.map((metric) => metric.jsonParseMaterializationDurationMs),
    returnedUtf8PayloadBytes: metrics.map((metric) => metric.returnedUtf8PayloadBytes),
    responseKinds: metrics.map((metric) => metric.responseKind),
  }]));
}

function parseOptions(argumentsList) {
  const result = {};
  const positional = [];
  for (let index = 0; index < argumentsList.length; index += 1) {
    const value = argumentsList[index];
    if (value.startsWith("--") && value.includes("=")) {
      const separator = value.indexOf("=");
      result[toOptionName(value.slice(2, separator))] = value.slice(separator + 1);
    } else if (value.startsWith("--")) {
      const key = toOptionName(value.slice(2));
      result[key] = argumentsList[index + 1] ?? "true";
      index += 1;
    } else {
      positional.push(value);
    }
  }
  if (result.url === undefined && positional.length > 0) result.url = positional[0];
  return result;
}

function toOptionName(value) {
  return value.replace(/-([a-z])/g, (_, letter) => letter.toUpperCase());
}

let report;
try {
  const buildInfoResponse = await fetch(buildInfoUrl);
  const buildInfo = buildInfoResponse.ok ? await buildInfoResponse.json() : null;
  const appContext = await browser.newContext({ viewport: { width: 1600, height: 900 } });
  const appPage = await appContext.newPage();
  const application = await checkApplication(appPage);

  const benchmarkContext = await browser.newContext({ viewport: { width: 1600, height: 900 } });
  const benchmarkPage = await benchmarkContext.newPage();
  const measurementMode = await loadBenchmarkBridge(benchmarkPage);
  const samples = [];

  const coldInitialization = await initialize(benchmarkPage);
  const coldTrace = await runTrace(benchmarkPage);
  samples.push({ kind: "cold", initialization: coldInitialization, trace: coldTrace });

  for (let index = 0; index < warmSamples; index += 1) {
    const warmInitialization = await initialize(benchmarkPage);
    const warmTrace = await runTrace(benchmarkPage);
    samples.push({ kind: "warm", sample: index + 1, initialization: warmInitialization, trace: warmTrace });
  }

  report = {
    label: options.label ?? "current",
    url: targetUrl.toString(),
    measurementMode,
    buildInfo,
    application,
    samples,
    commandSummary: summarize(samples),
    consoleErrors,
    pageErrors,
  };
  await appContext.close();
  await benchmarkContext.close();
} finally {
  await browser.close();
}

if (consoleErrors.length > 0 || pageErrors.length > 0) {
  throw new Error(`Browser errors detected: ${[...consoleErrors, ...pageErrors].join(" | ")}`);
}
console.log(JSON.stringify(report, null, 2));
