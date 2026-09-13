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
const bridgeMode = parseBridgeMode(options.bridge ?? process.env.PLAYTEST_BENCHMARK_BRIDGE);
const useRichBridge = bridgeMode === "worker" ||
  (bridgeMode === "auto" && isLocalDevelopmentUrl(targetUrl));
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
  return page.evaluate(async ({ moduleUrl: wasmModuleUrl, useRichBridge: shouldUseRichBridge }) => {
    await import(/* @vite-ignore */ wasmModuleUrl);
    const api = globalThis.canduPlaytestWasm;
    if (api === undefined) {
      throw new Error("The browser WASM module did not expose canduPlaytestWasm.");
    }

    if (shouldUseRichBridge) {
      try {
        const { WasmProtocolBridge } = await import("/src/bridge.ts?benchmark=1");
        globalThis.__canduBenchmarkBridge = new WasmProtocolBridge(api);
        return "WasmProtocolBridge";
      } catch {
        // Keep local/dev runs usable when the source bridge is unavailable.
        // Production never attempts this import; it measures the exported
        // authoritative WASM calls directly below.
      }
    }

    globalThis.__canduBenchmarkBridge = null;
    globalThis.__canduBenchmarkApi = api;
    return "direct-json-parse";
  }, { moduleUrl, useRichBridge });
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
        commandType: value.command?.type ?? null,
        responseKind: value.responseKind ?? "full",
        stateDigest: value.stateDigest ?? null,
        replayDigest: value.replayDigest ?? null,
        requiresResync: value.requiresResync === true,
        snapshotPatchIncluded: value.snapshotPatch !== undefined && value.snapshotPatch !== null,
        coreReplacementIncluded: value.coreReplacement !== undefined && value.coreReplacement !== null,
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
      label: "commit-refuel",
      type: "commit-refuel",
      request: {
        channelIndex: 210,
        directionId: "toward-end-a",
        shiftCount: 4,
        fuelTypeId: "NAT-U-SYNTHETIC",
      },
    },
  ];
  let sequence = 0;
  let previousStateDigest = null;
  let previousReplayDigest = null;
  let refuelTransition = null;
  const steps = [];
  for (const entry of commands) {
    const { label, ...command } = entry;
    const beforeRefuel = command.type === "commit-refuel" ? await getSnapshot(page) : null;
    const result = await dispatch(page, { ...command, __baseSequence: sequence });
    const response = result.response;
    if (result.metric === null) {
      throw new Error(`${label} did not expose a transport metric.`);
    }
    if (result.metric.commandType !== command.type || response.commandType !== command.type) {
      throw new Error(`${label} was not acknowledged as the requested authoritative command.`);
    }
    if (response.responseKind !== "compact" || result.metric.responseKind !== "compact") {
      throw new Error(`${label} did not return the requested compact response.`);
    }
    if (response.snapshotPatchIncluded !== true) {
      throw new Error(`${label} did not return a compact snapshot patch.`);
    }
    if (response.requiresResync) {
      throw new Error(`${label} required a compact resync unexpectedly.`);
    }
    if (response.accepted !== true) {
      throw new Error(`${label} was rejected by the authoritative bridge.`);
    }
    if (response.sequence !== sequence + 1) {
      throw new Error(`${label} did not advance the authoritative sequence exactly once.`);
    }
    if (typeof response.stateDigest !== "string" || response.stateDigest.length === 0 ||
        typeof response.replayDigest !== "string" || response.replayDigest.length === 0) {
      throw new Error(`${label} did not return deterministic state and replay digests.`);
    }

    let stepRefuelTransition = null;
    if (command.type === "commit-refuel") {
      if (beforeRefuel === null) {
        throw new Error(`${label} did not capture the pre-commit authoritative snapshot.`);
      }
      if (response.coreReplacementIncluded !== true) {
        throw new Error(`${label} did not return the changed core in the compact response.`);
      }
      const afterRefuel = await getSnapshot(page);
      const beforeSemantic = semanticSnapshot(beforeRefuel);
      const afterSemantic = semanticSnapshot(afterRefuel);
      const beforeCore = canonicalJson(beforeRefuel.core);
      const afterCore = canonicalJson(afterRefuel.core);
      if (afterRefuel.refuellingOperationCount !== beforeRefuel.refuellingOperationCount + 1) {
        throw new Error(`${label} did not increment refuellingOperationCount exactly once.`);
      }
      if (afterRefuel.freshBundlesAvailable !==
          beforeRefuel.freshBundlesAvailable - command.request.shiftCount) {
        throw new Error(`${label} did not consume the requested fresh inventory.`);
      }
      if (beforeSemantic === afterSemantic || beforeCore === afterCore) {
        throw new Error(`${label} did not change authoritative refuelling state.`);
      }
      if (previousStateDigest === null || response.stateDigest === previousStateDigest ||
          previousReplayDigest === null || response.replayDigest === previousReplayDigest) {
        throw new Error(`${label} did not change the deterministic state and replay digests.`);
      }
      stepRefuelTransition = {
        before: {
          refuellingOperationCount: beforeRefuel.refuellingOperationCount,
          freshBundlesAvailable: beforeRefuel.freshBundlesAvailable,
        },
        after: {
          refuellingOperationCount: afterRefuel.refuellingOperationCount,
          freshBundlesAvailable: afterRefuel.freshBundlesAvailable,
        },
        refuellingOperationCountDelta:
          afterRefuel.refuellingOperationCount - beforeRefuel.refuellingOperationCount,
        freshBundlesAvailableDelta:
          afterRefuel.freshBundlesAvailable - beforeRefuel.freshBundlesAvailable,
        stateChanged: beforeSemantic !== afterSemantic,
        coreChanged: beforeCore !== afterCore,
        stateDigestChanged: response.stateDigest !== previousStateDigest,
        replayDigestChanged: response.replayDigest !== previousReplayDigest,
      };
      refuelTransition = stepRefuelTransition;
    }

    sequence = response.sequence;
    previousStateDigest = response.stateDigest;
    previousReplayDigest = response.replayDigest;
    steps.push({
      label,
      command,
      accepted: response.accepted,
      sequence: response.sequence,
      acknowledgedCommandType: response.commandType,
      responseKind: response.responseKind,
      stateDigest: response.stateDigest,
      replayDigest: response.replayDigest,
      snapshotPatchIncluded: response.snapshotPatchIncluded,
      coreReplacementIncluded: response.coreReplacementIncluded,
      refuelTransition: stepRefuelTransition,
      metric: result.metric,
    });
  }
  const snapshot = await getSnapshot(page);
  const semantic = semanticSnapshot(snapshot);
  return {
    steps,
    finalSequence: sequence,
    refuelTransition,
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

function assertDeterministicDigests(samples) {
  const baseline = samples[0]?.trace;
  if (baseline === undefined) return { sampleCount: 0, matched: true };

  for (let sampleIndex = 1; sampleIndex < samples.length; sampleIndex += 1) {
    const current = samples[sampleIndex].trace;
    if (current.finalSemanticSnapshotSha256 !== baseline.finalSemanticSnapshotSha256) {
      throw new Error(`Sample ${sampleIndex} produced a different final semantic snapshot digest.`);
    }
    if (current.steps.length !== baseline.steps.length) {
      throw new Error(`Sample ${sampleIndex} produced a different command trace length.`);
    }
    for (let stepIndex = 0; stepIndex < baseline.steps.length; stepIndex += 1) {
      for (const digestName of ["stateDigest", "replayDigest"]) {
        if (current.steps[stepIndex][digestName] !== baseline.steps[stepIndex][digestName]) {
          throw new Error(
            `Sample ${sampleIndex} step ${stepIndex} produced a different ${digestName}.`,
          );
        }
      }
    }
  }

  return {
    sampleCount: samples.length,
    matched: true,
    finalSemanticSnapshotSha256: baseline.finalSemanticSnapshotSha256,
  };
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

function parseBridgeMode(value) {
  const mode = String(value ?? "auto").toLowerCase();
  if (mode === "auto" || mode === "direct" || mode === "worker") return mode;
  throw new Error("--bridge must be auto, direct, or worker.");
}

function isLocalDevelopmentUrl(url) {
  return ["localhost", "127.0.0.1", "[::1]", "::1"].includes(url.hostname) &&
    url.port !== "4173";
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

  const deterministicDigestChecks = assertDeterministicDigests(samples);

  report = {
    label: options.label ?? "current",
    url: targetUrl.toString(),
    measurementMode,
    buildInfo,
    application,
    samples,
    commandSummary: summarize(samples),
    deterministicDigestChecks,
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
