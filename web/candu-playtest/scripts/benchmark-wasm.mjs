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
const expectedCommitSha = options.expectedCommitSha ?? process.env.PLAYTEST_EXPECTED_COMMIT_SHA ?? null;
if (expectedCommitSha !== null && !/^[0-9a-f]{40}$/i.test(expectedCommitSha)) {
  throw new Error("--expected-commit-sha must be a 40-character hexadecimal Git commit SHA.");
}

const targetUrl = new URL(baseUrl);
const bridgeMode = parseBridgeMode(options.bridge ?? process.env.PLAYTEST_BENCHMARK_BRIDGE);
const useRichBridge = bridgeMode === "worker" ||
  (bridgeMode === "auto" && isLocalDevelopmentUrl(targetUrl));
const moduleUrl = new URL("/wasm/main.mjs", targetUrl.origin).toString();
const buildInfoUrl = new URL("/wasm/build-info.json", targetUrl.origin).toString();
const consoleErrors = [];
const pageErrors = [];
const observedPages = new WeakSet();
const browser = await chromium.launch({ headless: true });

// Keep the matrix small enough for routine CI runs while covering every
// direction/shift pair. Each representative gets both a paused and a live
// advance case, and every case is initialized from a fresh WASM session.
const MATRIX_CASES = [
  { advanceState: "paused", directionId: "toward-end-a", shiftCount: 4 },
  { advanceState: "live", directionId: "toward-end-b", shiftCount: 4 },
  { advanceState: "live", directionId: "toward-end-a", shiftCount: 8 },
  { advanceState: "paused", directionId: "toward-end-b", shiftCount: 8 },
];
const CASE_ADVANCE_WALL_MILLISECONDS = 1_000;
const REFUEL_CHANNEL_INDEX = 210;

function observe(page) {
  if (observedPages.has(page)) return;
  observedPages.add(page);
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
        const recordingApi = {
          getCapabilities: api.getCapabilities?.bind(api),
          getSnapshotJson: api.getSnapshotJson.bind(api),
          initialize: async (requestJson) => {
            const raw = await api.initialize(requestJson);
            globalThis.__canduBenchmarkLastRawResponse = raw;
            return raw;
          },
          dispatchJson: async (commandJson) => {
            const raw = await api.dispatchJson(commandJson);
            globalThis.__canduBenchmarkLastRawResponse = raw;
            return raw;
          },
        };
        globalThis.__canduBenchmarkLastRawResponse = null;
        globalThis.__canduBenchmarkBridge = new WasmProtocolBridge(recordingApi);
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

async function installPageHelpers(page) {
  await page.evaluate(() => {
    function snapshotMeta(snapshot) {
      return {
        protocol: snapshot.protocol,
        source: snapshot.source,
        scenarioId: snapshot.scenarioId,
        dataPackId: snapshot.dataPackId,
        coreChannelCount: snapshot.core?.channelCount ?? null,
        bundlePositionCount: snapshot.core?.bundlePositionCount ?? null,
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

    async function sha256Hex(value) {
      if (globalThis.crypto?.subtle === undefined) return null;
      const digest = await globalThis.crypto.subtle.digest("SHA-256", new TextEncoder().encode(value));
      return Array.from(new Uint8Array(digest), (byte) => byte.toString(16).padStart(2, "0")).join("");
    }

    function compactSnapshotSummary(snapshot, selectedChannelIndex) {
      const channel = snapshot.core.channels.find((item) => item.channelIndex === selectedChannelIndex);
      if (channel === undefined) {
        throw new Error(`The authoritative snapshot did not contain channel ${selectedChannelIndex}.`);
      }
      return {
        protocol: snapshot.protocol,
        source: snapshot.source,
        sequence: snapshot.sequence,
        scenarioId: snapshot.scenarioId,
        dataPackId: snapshot.dataPackId,
        simulationTimeSeconds: snapshot.simulationTimeSeconds,
        wallElapsedSeconds: snapshot.wallElapsedSeconds,
        isPaused: snapshot.isPaused,
        playbackModeId: snapshot.playbackModeId,
        normalizedPowerFraction: snapshot.normalizedPowerFraction,
        targetPowerFraction: snapshot.targetPowerFraction,
        absoluteTiltFraction: snapshot.absoluteTiltFraction,
        targetTiltFraction: snapshot.targetTiltFraction,
        controlMarginFraction: snapshot.controlMarginFraction,
        scoreTotal: snapshot.scoreTotal,
        scoreDelta: snapshot.scoreDelta,
        freshBundlesAvailable: snapshot.freshBundlesAvailable,
        refuellingOperationCount: snapshot.refuellingOperationCount,
        lastRefuelledChannel: snapshot.lastRefuelledChannel,
        lastRefuellingDirectionId: snapshot.lastRefuellingDirectionId,
        lastRefuellingShiftCount: snapshot.lastRefuellingShiftCount,
        physics: {
          sourceId: snapshot.physics.sourceId,
          formulationId: snapshot.physics.formulationId,
          shapeMethodId: snapshot.physics.shapeMethodId,
          amplitudeMethodId: snapshot.physics.amplitudeMethodId,
          reactivityMethodId: snapshot.physics.reactivityMethodId,
          solveState: snapshot.physics.solveState,
          bindingVersion: snapshot.physics.bindingVersion,
          actualPowerFraction: snapshot.physics.actualPowerFraction,
          totalPowerWatts: snapshot.physics.totalPowerWatts,
          effectiveK: snapshot.physics.effectiveK,
          reactivity: snapshot.physics.reactivity,
          staticReactivity: snapshot.physics.staticReactivity,
          weightedPerturbationReactivity: snapshot.physics.weightedPerturbationReactivity,
          coreReactivity: snapshot.physics.coreReactivity,
          compensatedNetReactivity: snapshot.physics.compensatedNetReactivity,
          solverIdentity: snapshot.physics.solverIdentity,
          solverIterationCount: snapshot.physics.solverIterationCount,
          solverResidualRelativeInfinity: snapshot.physics.solverResidualRelativeInfinity,
          cadenceIdentity: snapshot.physics.cadenceIdentity,
        },
        rrs: {
          controllerIdentity: snapshot.rrs.controllerIdentity,
          mappingIdentity: snapshot.rrs.mappingIdentity,
          overlayIdentity: snapshot.rrs.overlayIdentity,
          stateDigestHex: snapshot.rrs.stateDigestHex,
          simulationTimeSeconds: snapshot.rrs.simulationTimeSeconds,
          nodeCount: snapshot.rrs.nodeCount,
          averageFillFraction: snapshot.rrs.averageFillFraction,
          minimumFillFraction: snapshot.rrs.minimumFillFraction,
          maximumFillFraction: snapshot.rrs.maximumFillFraction,
          measuredPowerWatts: snapshot.rrs.measuredPowerWatts,
          targetPowerWatts: snapshot.rrs.targetPowerWatts,
          powerErrorWatts: snapshot.rrs.powerErrorWatts,
          coreReactivity: snapshot.rrs.coreReactivity,
          compensatedNetReactivity: snapshot.rrs.compensatedNetReactivity,
          appliedFillCommand: snapshot.rrs.appliedFillCommand,
          controlledBaselineWeightedResidual: snapshot.rrs.controlledBaselineWeightedResidual,
          combinedWeightedResidual: snapshot.rrs.combinedWeightedResidual,
          candidateSolveCount: snapshot.rrs.candidateSolveCount,
          verificationSolveCount: snapshot.rrs.verificationSolveCount,
          correctionSolveCount: snapshot.rrs.correctionSolveCount,
          correctionApplied: snapshot.rrs.correctionApplied,
          lowExhaustion: snapshot.rrs.lowExhaustion,
          highExhaustion: snapshot.rrs.highExhaustion,
          isGameOver: snapshot.rrs.isGameOver,
          gameOverReason: snapshot.rrs.gameOverReason,
          cadenceIdentity: snapshot.rrs.cadenceIdentity,
          zones: snapshot.rrs.zones.map((zone) => ({
            logicalZoneId: zone.logicalZoneId,
            fillFraction: zone.fillFraction,
            targetPowerFraction: zone.targetPowerFraction,
            measuredPowerFraction: zone.measuredPowerFraction,
            shapeError: zone.shapeError,
          })),
        },
        channel: {
          channelIndex: channel.channelIndex,
          gridColumn: channel.gridColumn,
          gridRow: channel.gridRow,
          flowDirection: channel.flowDirection,
          averageBurnupMwdPerKg: channel.averageBurnupMwdPerKg,
          powerWatts: channel.powerWatts,
          localPowerFraction: channel.localPowerFraction,
          localTiltFraction: channel.localTiltFraction,
          bundles: channel.bundles.map((bundle) => ({
            position: bundle.position,
            bundleId: bundle.bundleId,
            fuelTypeId: bundle.fuelTypeId,
            currentBurnupMwdPerKg: bundle.currentBurnupMwdPerKg,
            powerWatts: bundle.powerWatts,
            localPowerFraction: bundle.localPowerFraction,
            insertedAtSeconds: bundle.insertedAtSeconds,
            stateVersion: bundle.stateVersion,
            isFresh: bundle.isFresh,
          })),
        },
      };
    }

    function lastMetric(metrics, commandType) {
      for (let index = metrics.length - 1; index >= 0; index -= 1) {
        if (metrics[index]?.commandType === commandType) return metrics[index];
      }
      return null;
    }

    globalThis.__canduBenchmarkHelpers = {
      compactSnapshotSummary,
      lastMetric,
      semanticSnapshot,
      sha256Hex,
      snapshotMeta,
    };
  });
}

async function initialize(page) {
  return page.evaluate(async () => {
    const helpers = globalThis.__canduBenchmarkHelpers;
    if (helpers === undefined) throw new Error("Benchmark page helpers were not installed.");
    const bridge = globalThis.__canduBenchmarkBridge;
    if (bridge !== null && bridge !== undefined) {
      const snapshot = await bridge.initialize("play");
      const raw = globalThis.__canduBenchmarkLastRawResponse;
      const response = raw === null || raw === undefined ? null : JSON.parse(raw);
      const metric = helpers.lastMetric(bridge.getTransportMetrics(), "initialize");
      return {
        snapshotSequence: snapshot.sequence,
        snapshotMeta: helpers.snapshotMeta(snapshot),
        stateDigest: response?.stateDigest ?? null,
        replayDigest: response?.replayDigest ?? null,
        metric: metric === null ? null : { ...metric },
      };
    }

    const api = globalThis.__canduBenchmarkApi;
    const started = performance.now();
    const raw = await api.initialize(JSON.stringify({ protocol: "candu-playtest-v1", mode: "play" }));
    const wasmCallDurationMs = performance.now() - started;
    const parseStarted = performance.now();
    const response = JSON.parse(raw);
    const jsonParseMaterializationDurationMs = performance.now() - parseStarted;
    const snapshot = response.snapshot ?? response;
    return {
      snapshotSequence: snapshot.sequence ?? response.sequence,
      snapshotMeta: helpers.snapshotMeta(snapshot),
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
    const helpers = globalThis.__canduBenchmarkHelpers;

    function serializeResponse(value) {
      return {
        accepted: value.accepted,
        sequence: value.sequence,
        commandType: value.command?.type ?? commandValue.type,
        responseKind: value.responseKind ?? "full",
        message: value.message ?? "",
        baseSequence: value.baseSequence ?? null,
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
      const metric = helpers.lastMetric(bridge.getTransportMetrics(), commandValue.type);
      return {
        response: serializeResponse(response),
        metric: metric === null ? null : { ...metric },
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
        commandType: commandValue.type,
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
    if (bridge !== null && bridge !== undefined) return bridge.getSnapshot();

    const api = globalThis.__canduBenchmarkApi;
    return JSON.parse(await api.getSnapshotJson());
  });
}

async function captureSnapshot(page, channelIndex, stateDigest, replayDigest) {
  const capture = await page.evaluate(async ({ selectedChannelIndex, currentStateDigest, currentReplayDigest }) => {
    const helpers = globalThis.__canduBenchmarkHelpers;
    const bridge = globalThis.__canduBenchmarkBridge;
    const snapshot = bridge !== null && bridge !== undefined
      ? bridge.getSnapshot()
      : JSON.parse(await globalThis.__canduBenchmarkApi.getSnapshotJson());
    const semantic = helpers.semanticSnapshot(snapshot);
    const semanticSha256 = await helpers.sha256Hex(semantic);
    return {
      summary: helpers.compactSnapshotSummary(snapshot, selectedChannelIndex),
      semanticSha256,
      semanticJson: semanticSha256 === null ? semantic : undefined,
      semanticBytes: new TextEncoder().encode(semantic).byteLength,
      snapshotSequence: snapshot.sequence,
      stateDigest: currentStateDigest,
      replayDigest: currentReplayDigest,
    };
  }, { selectedChannelIndex: channelIndex, currentStateDigest: stateDigest, currentReplayDigest: replayDigest });
  if (capture.semanticSha256 === null) {
    capture.semanticSha256 = sha256(capture.semanticJson);
    delete capture.semanticJson;
  }
  return capture;
}

function requireAuthoritativeInitialization(initialization, label) {
  if (initialization.snapshotSequence !== 0) {
    throw new Error(`${label} did not start at authoritative sequence zero.`);
  }
  if (initialization.metric === null) {
    throw new Error(`${label} did not expose an initialization transport metric.`);
  }
  requireMetric(initialization.metric, `${label} initialization`);
  if (typeof initialization.stateDigest !== "string" || initialization.stateDigest.length === 0 ||
      typeof initialization.replayDigest !== "string" || initialization.replayDigest.length === 0) {
    throw new Error(`${label} did not return initialization state and replay digests.`);
  }
  const meta = initialization.snapshotMeta;
  if (meta?.source !== "wasm" || meta.coreChannelCount !== 380 || meta.bundlePositionCount !== 12) {
    throw new Error(`${label} did not initialize the authoritative 380-channel WASM snapshot.`);
  }
}

function selectRepresentativeChannels(snapshot) {
  if (snapshot.source !== "wasm" || snapshot.core?.channelCount !== 380 ||
      !Array.isArray(snapshot.core.channels) || snapshot.core.channels.length !== 380) {
    throw new Error("The benchmark requires an authoritative WASM snapshot with 380 channels.");
  }

  const target = snapshot.core.channels.find((channel) => channel.channelIndex === REFUEL_CHANNEL_INDEX);
  if (target === undefined) {
    throw new Error(`The authoritative snapshot did not contain channel ${REFUEL_CHANNEL_INDEX}.`);
  }

  const centerX = (snapshot.core.gridWidth - 1) / 2;
  const centerY = (snapshot.core.gridHeight - 1) / 2;
  const distanceFromCenter = (channel) =>
    (channel.gridColumn - centerX) ** 2 + (channel.gridRow - centerY) ** 2;
  const candidates = snapshot.core.channels.filter((channel) => channel.channelIndex !== REFUEL_CHANNEL_INDEX);
  const central = [...candidates].sort((left, right) =>
    distanceFromCenter(left) - distanceFromCenter(right) || left.channelIndex - right.channelIndex)[0];
  const peripheral = [...candidates].sort((left, right) =>
    distanceFromCenter(right) - distanceFromCenter(left) || left.channelIndex - right.channelIndex)[0];
  if (central === undefined || peripheral === undefined || central.channelIndex === peripheral.channelIndex) {
    throw new Error("The authoritative core did not provide distinct central and peripheral channels.");
  }

  return [
    representative("channel-210", target),
    representative("central", central),
    representative("peripheral", peripheral),
  ];
}

function representative(role, channel) {
  return {
    role,
    channelIndex: channel.channelIndex,
    gridColumn: channel.gridColumn,
    gridRow: channel.gridRow,
    flowDirection: channel.flowDirection,
  };
}

function createMatrixCases(representatives) {
  return representatives.flatMap((channel) => MATRIX_CASES.map((entry) => ({
    caseId: `${channel.role}-ch${channel.channelIndex}-${entry.advanceState}-${directionShortName(entry.directionId)}-${entry.shiftCount}`,
    channel,
    advanceState: entry.advanceState,
    directionId: entry.directionId,
    shiftCount: entry.shiftCount,
  })));
}

async function runCase(page, definition, initialization) {
  requireAuthoritativeInitialization(initialization, `${definition.caseId} initialization`);
  let sequence = initialization.snapshotSequence;
  let stateDigest = initialization.stateDigest;
  let replayDigest = initialization.replayDigest;
  const preparation = [];
  const preparationCommands = definition.advanceState === "paused"
    ? [
        { label: "pause", type: "pause" },
        { label: "advance-while-paused", type: "advance", wallMilliseconds: CASE_ADVANCE_WALL_MILLISECONDS },
      ]
    : [
        { label: "resume", type: "resume" },
        { label: "advance-while-live", type: "advance", wallMilliseconds: CASE_ADVANCE_WALL_MILLISECONDS },
      ];

  for (const entry of preparationCommands) {
    const { label, ...command } = entry;
    const result = await dispatch(page, { ...command, __baseSequence: sequence });
    assertCommandResult(result, command.type, sequence, label);
    sequence = result.response.sequence;
    stateDigest = result.response.stateDigest;
    replayDigest = result.response.replayDigest;
    preparation.push({
      label,
      command,
      response: result.response,
      metric: requireMetric(result.metric, `${definition.caseId} ${label}`),
    });
  }

  const before = await captureSnapshot(page, definition.channel.channelIndex, stateDigest, replayDigest);
  if (before.snapshotSequence !== sequence) {
    throw new Error(`${definition.caseId} before snapshot sequence did not match the command trace.`);
  }
  if (before.summary.isPaused !== (definition.advanceState === "paused")) {
    throw new Error(`${definition.caseId} did not reach its requested ${definition.advanceState} state.`);
  }
  if (definition.advanceState === "paused" && before.summary.simulationTimeSeconds !== 0) {
    throw new Error(`${definition.caseId} advanced simulation time while paused.`);
  }
  if (definition.advanceState === "live" && before.summary.simulationTimeSeconds <= 0) {
    throw new Error(`${definition.caseId} did not advance simulation time while live.`);
  }

  const command = {
    type: "commit-refuel",
    request: {
      channelIndex: definition.channel.channelIndex,
      directionId: definition.directionId,
      shiftCount: definition.shiftCount,
      fuelTypeId: "NAT-U-SYNTHETIC",
    },
  };
  const result = await dispatch(page, { ...command, __baseSequence: sequence });
  assertCommandResult(result, command.type, sequence, definition.caseId);
  if (result.response.coreReplacementIncluded !== true) {
    throw new Error(`${definition.caseId} did not return the changed core in its compact response.`);
  }
  const after = await captureSnapshot(
    page,
    definition.channel.channelIndex,
    result.response.stateDigest,
    result.response.replayDigest,
  );
  if (after.snapshotSequence !== result.response.sequence) {
    throw new Error(`${definition.caseId} after snapshot sequence did not match the compact response.`);
  }
  if (after.summary.freshBundlesAvailable !==
      before.summary.freshBundlesAvailable - definition.shiftCount) {
    throw new Error(`${definition.caseId} did not consume the requested fresh inventory.`);
  }
  if (after.summary.refuellingOperationCount !== before.summary.refuellingOperationCount + 1) {
    throw new Error(`${definition.caseId} did not increment refuellingOperationCount exactly once.`);
  }
  if (after.semanticSha256 === before.semanticSha256) {
    throw new Error(`${definition.caseId} did not change the authoritative semantic snapshot.`);
  }
  if (result.response.stateDigest === stateDigest || result.response.replayDigest === replayDigest) {
    throw new Error(`${definition.caseId} did not change the deterministic state and replay digests.`);
  }

  const metric = requireMetric(result.metric, definition.caseId);
  return {
    caseId: definition.caseId,
    channel: definition.channel,
    advanceState: definition.advanceState,
    directionId: definition.directionId,
    shiftCount: definition.shiftCount,
    fuelTypeId: command.request.fuelTypeId,
    initialization: compactInitialization(initialization),
    preparation,
    before,
    after,
    response: result.response,
    metric,
    wasmCallDurationMs: metric.wasmCallDurationMs,
    jsonParseMaterializationDurationMs: metric.jsonParseMaterializationDurationMs,
    returnedUtf8PayloadBytes: metric.returnedUtf8PayloadBytes,
  };
}

function assertCommandResult(result, commandType, previousSequence, label) {
  if (result.metric === null) {
    throw new Error(`${label} did not expose a transport metric.`);
  }
  if (result.metric.commandType !== commandType || result.response.commandType !== commandType) {
    throw new Error(`${label} was not acknowledged as the requested authoritative command.`);
  }
  if (result.response.responseKind !== "compact" || result.metric.responseKind !== "compact") {
    throw new Error(`${label} did not return the requested compact response.`);
  }
  if (result.response.snapshotPatchIncluded !== true || result.response.requiresResync) {
    throw new Error(`${label} did not return a compact snapshot patch without resync.`);
  }
  if (result.response.accepted !== true) {
    throw new Error(`${label} was rejected by the authoritative bridge: ${result.response.message}`);
  }
  if (result.response.sequence !== previousSequence + 1) {
    throw new Error(`${label} did not advance the authoritative sequence exactly once.`);
  }
  if (typeof result.response.stateDigest !== "string" || result.response.stateDigest.length === 0 ||
      typeof result.response.replayDigest !== "string" || result.response.replayDigest.length === 0) {
    throw new Error(`${label} did not return deterministic state and replay digests.`);
  }
}

function requireMetric(metric, label) {
  if (metric === null ||
      !Number.isFinite(metric.wasmCallDurationMs) ||
      !Number.isFinite(metric.jsonParseMaterializationDurationMs) ||
      !Number.isInteger(metric.returnedUtf8PayloadBytes)) {
    throw new Error(`${label} did not expose complete WASM, JSON parse, and UTF-8 metrics.`);
  }
  return metric;
}

function compactInitialization(initialization) {
  return {
    snapshotSequence: initialization.snapshotSequence,
    snapshotMeta: initialization.snapshotMeta,
    stateDigest: initialization.stateDigest,
    replayDigest: initialization.replayDigest,
    metric: initialization.metric,
  };
}

function summarizeInitialization(initializations) {
  return {
    sampleCount: initializations.length,
    wasmCallDurationMs: initializations.map((item) => item.metric.wasmCallDurationMs),
    jsonParseMaterializationDurationMs: initializations.map((item) => item.metric.jsonParseMaterializationDurationMs),
    returnedUtf8PayloadBytes: initializations.map((item) => item.metric.returnedUtf8PayloadBytes),
    responseKinds: initializations.map((item) => item.metric.responseKind),
    stateDigests: initializations.map((item) => item.stateDigest),
    replayDigests: initializations.map((item) => item.replayDigest),
  };
}

function summarizeRows(samples) {
  const rows = samples.flatMap((sample) => sample.rows);
  return {
    sampleCount: samples.length,
    rowCount: rows.length,
    wasmCallDurationMs: rows.map((row) => row.wasmCallDurationMs),
    jsonParseMaterializationDurationMs: rows.map((row) => row.jsonParseMaterializationDurationMs),
    returnedUtf8PayloadBytes: rows.map((row) => row.returnedUtf8PayloadBytes),
    responseKinds: rows.map((row) => row.response.responseKind),
    acceptedRows: rows.filter((row) => row.response.accepted).length,
  };
}

function matrixFingerprint(rows) {
  return rows.map((row) => ({
    caseId: row.caseId,
    before: {
      semanticSha256: row.before.semanticSha256,
      stateDigest: row.before.stateDigest,
      replayDigest: row.before.replayDigest,
    },
    after: {
      semanticSha256: row.after.semanticSha256,
      stateDigest: row.after.stateDigest,
      replayDigest: row.after.replayDigest,
    },
    response: {
      accepted: row.response.accepted,
      sequence: row.response.sequence,
      stateDigest: row.response.stateDigest,
      replayDigest: row.response.replayDigest,
    },
  }));
}

function matrixDigest(rows) {
  return sha256(canonicalJson(matrixFingerprint(rows)));
}

function assertDeterministicDigests(samples) {
  const baseline = samples[0]?.rows ?? [];
  const baselineFingerprint = canonicalJson(matrixFingerprint(baseline));
  for (let sampleIndex = 1; sampleIndex < samples.length; sampleIndex += 1) {
    const current = samples[sampleIndex].rows;
    if (canonicalJson(matrixFingerprint(current)) !== baselineFingerprint) {
      throw new Error(`Sample ${sampleIndex} produced a different deterministic refuelling matrix.`);
    }
  }

  return {
    sampleCount: samples.length,
    rowCount: baseline.length,
    matched: true,
    matrixSha256: sha256(baselineFingerprint),
  };
}

function representativeCoverage(representatives) {
  return {
    channelIndices: representatives.map((channel) => channel.channelIndex),
    roles: representatives.map((channel) => channel.role),
    directions: ["toward-end-a", "toward-end-b"],
    shiftCounts: [4, 8],
    advanceStates: ["paused", "live"],
    directionShiftPairs: MATRIX_CASES.map((entry) => ({
      directionId: entry.directionId,
      shiftCount: entry.shiftCount,
    })),
    casesPerRepresentative: MATRIX_CASES.length,
    caseCount: representatives.length * MATRIX_CASES.length,
  };
}

function directionShortName(directionId) {
  return directionId === "toward-end-a" ? "end-a" : "end-b";
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
let appContext;
let benchmarkContext;
try {
  const buildInfoResponse = await fetch(buildInfoUrl);
  const buildInfo = buildInfoResponse.ok ? await buildInfoResponse.json() : null;
  if (expectedCommitSha !== null && buildInfo?.gitCommitSha !== expectedCommitSha) {
    throw new Error(
      `Deployed bridge commit ${buildInfo?.gitCommitSha ?? "unavailable"} does not match expected source ${expectedCommitSha}.`,
    );
  }
  appContext = await browser.newContext({ viewport: { width: 1600, height: 900 } });
  const appPage = await appContext.newPage();
  const application = await checkApplication(appPage);

  benchmarkContext = await browser.newContext({ viewport: { width: 1600, height: 900 } });
  const benchmarkPage = await benchmarkContext.newPage();
  const measurementMode = await loadBenchmarkBridge(benchmarkPage);
  await installPageHelpers(benchmarkPage);

  // The first initialization both measures cold startup and supplies the
  // authoritative topology used to choose central/peripheral representatives.
  const coldInitialization = await initialize(benchmarkPage);
  requireAuthoritativeInitialization(coldInitialization, "cold initialization");
  const coldSnapshot = await getSnapshot(benchmarkPage);
  const representatives = selectRepresentativeChannels(coldSnapshot);
  const matrixCases = createMatrixCases(representatives);
  const samples = [];
  const warmInitializations = [];

  for (let sampleIndex = 0; sampleIndex <= warmSamples; sampleIndex += 1) {
    const rows = [];
    for (let caseIndex = 0; caseIndex < matrixCases.length; caseIndex += 1) {
      let initialization;
      if (sampleIndex === 0 && caseIndex === 0) {
        initialization = coldInitialization;
      } else {
        initialization = await initialize(benchmarkPage);
        requireAuthoritativeInitialization(initialization, `${matrixCases[caseIndex].caseId} initialization`);
        warmInitializations.push(initialization);
      }
      rows.push(await runCase(benchmarkPage, matrixCases[caseIndex], initialization));
    }
    samples.push({
      sample: sampleIndex,
      kind: sampleIndex === 0 ? "cold+warm" : "warm",
      rows,
      matrixSha256: matrixDigest(rows),
    });
  }

  const deterministicDigestChecks = assertDeterministicDigests(samples);
  report = {
    format: "candu-playtest-reproduction-matrix-v1",
    label: options.label ?? "refuelling-matrix",
    url: targetUrl.toString(),
    measurementMode,
    bridgeAuthority: "authoritative-csharp-wasm",
    buildInfo,
    provenance: {
      expectedCommitSha,
      deployedCommitSha: buildInfo?.gitCommitSha ?? null,
      sourceCommitMatched: expectedCommitSha === null ? null : true,
    },
    application,
    coverage: representativeCoverage(representatives),
    representatives,
    initialization: {
      cold: compactInitialization(coldInitialization),
      warm: summarizeInitialization(warmInitializations),
    },
    samples,
    commandSummary: summarizeRows(samples),
    deterministicDigestChecks,
    consoleErrors,
    pageErrors,
  };
} finally {
  await appContext?.close();
  await benchmarkContext?.close();
  await browser.close();
}

if (consoleErrors.length > 0 || pageErrors.length > 0) {
  throw new Error(`Browser errors detected: ${[...consoleErrors, ...pageErrors].join(" | ")}`);
}
console.log(JSON.stringify(report, null, 2));
