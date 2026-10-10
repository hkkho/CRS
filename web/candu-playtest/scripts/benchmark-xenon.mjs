/** Production worker benchmark: live poison history, transport latency and cadence. */
import { chromium } from "playwright";

const url = process.argv[2] ?? process.env.PLAYTEST_URL;
if (!url) throw new Error("Pass the production-preview/deployment URL.");
const campaign72Hours = process.argv.includes("--campaign-72h");
const sampleCount = campaign72Hours ? 144 : Number(process.argv[3] ?? 20);
const profiling = process.argv.includes("--profile");
if (!Number.isInteger(sampleCount) || sampleCount < 2 || (!campaign72Hours && sampleCount > 100))
  throw new Error("Sample count must be an integer from 2 to 100.");
const browser = await chromium.launch({ headless: true });
const errors = [];
try {
  const page = await browser.newPage();
  page.on("pageerror", error => errors.push(error.message));
  page.on("console", message => { if (message.type() === "error") errors.push(message.text()); });
  await page.goto(url, { waitUntil: "domcontentloaded" });
  await page.waitForFunction(() => document.querySelector("#status-mirror")?.textContent?.includes("live reactor online"),
    undefined, { timeout: 120_000 });
  const buildInfo = JSON.parse((await (await page.request.get(new URL("wasm/build-info.json", page.url()).href)).text()).replace(/^\uFEFF/, ""));
  const result = await page.evaluate(async ({ sampleCount, profiling, campaign72Hours }) => {
    const app = document.querySelector('script[type="module"][src]');
    const scriptUrl = new URL(app.getAttribute("src"), location.href);
    const source = await (await fetch(scriptUrl)).text();
    const match = source.match(/wasmWorker-[A-Za-z0-9_-]+\.js/);
    if (!match) throw new Error("Production worker asset was not found in the application bundle.");
    const worker = new Worker(new URL(match[0], scriptUrl), { type: "module" });
    let id = 0;
    const pending = new Map();
    let resolveReady, rejectReady;
    const ready = new Promise((resolve, reject) => { resolveReady = resolve; rejectReady = reject; });
    worker.onmessage = ({ data }) => {
      if (data.type === "ready") return resolveReady();
      if (data.type === "load-error") return rejectReady(new Error(data.error));
      const item = pending.get(data.id);
      if (!item) return;
      pending.delete(data.id); clearTimeout(item.timer);
      if (data.type === "error") item.reject(new Error(data.error)); else item.resolve(data);
    };
    worker.onerror = error => rejectReady(new Error(error.message));
    const request = payload => new Promise((resolve, reject) => {
      const key = ++id;
      const timer = setTimeout(() => { pending.delete(key); reject(new Error("Worker benchmark request timed out.")); }, 120_000);
      pending.set(key, { resolve, reject, timer }); worker.postMessage({ ...payload, id: key });
    });
    try {
      await ready;
      const initial = JSON.parse((await request({ type: "initialize", mode: "play", pacingMode: "real-time" })).resultJson);
      if (!initial.accepted || !initial.snapshot.xenon.hasCoupling) throw new Error("Live poison coupling is unavailable.");
      let sequence = initial.sequence;
      let finalStateDigest, finalReplayDigest;
      const dispatch = async payload => {
        const start = performance.now();
        const response = await request({ type: "dispatch", profile: profiling, commandJson: JSON.stringify({
          protocol: "candu-playtest-v2", type: "command", responseMode: "compact", baseSequence: sequence, payload,
        }) });
        const parseStart = performance.now();
        const envelope = JSON.parse(response.resultJson);
        const value = profiling ? JSON.parse(envelope.resultJson) : envelope;
        const parseMs = performance.now() - parseStart;
        if (!value.accepted) throw new Error(`${payload.type}: ${value.message}`);
        sequence = value.sequence;
        finalStateDigest = value.stateDigest; finalReplayDigest = value.replayDigest;
        return { value, wallMs: performance.now() - start, wasmMs: response.wasmCallDurationMs,
          parseMs, bytes: response.returnedUtf8PayloadBytes, profile: envelope.profile };
      };
      if (campaign72Hours) await dispatch({ type: "reset", seed: 1001 });
      await dispatch({ type: "pause" });
      const refuel = campaign72Hours ? null : await dispatch({ type: "commit-refuel", request: { channelIndex: 210, directionId: "toward-end-b",
        shiftCount: 8, fuelTypeId: "NAT-U-SYNTHETIC" } });
      await dispatch({ type: "resume" });
      if (!campaign72Hours) await dispatch({ type: "queue-power-target", targetFraction: 0.95 });
      const samples = [], campaignRefuels = [];
      for (let n = 0; n < sampleCount; n++) {
        const sample = await dispatch({ type: "advance", wallMilliseconds: 1000 });
        const patch = sample.value.snapshotPatch;
        samples.push({ index: n, wallMs: sample.wallMs, wasmMs: sample.wasmMs, parseMs: sample.parseMs,
          bytes: sample.bytes, profile: sample.profile, timeSeconds: patch.simulationTimeSeconds,
          meanIodine: patch.xenon.meanI135NumberDensityM3, meanXenon: patch.xenon.meanXe135NumberDensityM3,
          poisonDigest: patch.xenon.stateDigestHex, coupledPoisonDigest: patch.xenon.coupledStateDigestHex,
          coupledTimeSeconds: patch.xenon.coupledSimulationTimeSeconds,
          reactivity: patch.rrs.compensatedNetReactivity,
          maxShapeError: Math.max(...patch.rrs.zones.map(zone => Math.abs(zone.shapeError))),
          zoneFills: patch.rrs.zones.map(zone => zone.fillFraction),
          zoneXenon: patch.rrs.zones.map(zone => zone.meanXe135NumberDensityM3) });
        if (campaign72Hours && patch.simulationTimeSeconds === 14 * 3600) {
          for (const [channelIndex, directionId] of [[75, "toward-end-a"], [324, "toward-end-b"]]) {
            const operation = await dispatch({ type: "commit-refuel", request: { channelIndex, directionId,
              shiftCount: 8, fuelTypeId: "NAT-U-SYNTHETIC" } });
            const state = operation.value.snapshotPatch;
            if (state.simulationTimeSeconds !== patch.simulationTimeSeconds) throw new Error("Refuel advanced the clock.");
            campaignRefuels.push({ channelIndex, directionId, wallMs: operation.wallMs, wasmMs: operation.wasmMs,
              reactivity: state.rrs.compensatedNetReactivity,
              maxShapeError: Math.max(...state.rrs.zones.map(zone => Math.abs(zone.shapeError))) });
          }
        }
      }
      const tickSamples = [], tickBatchServiceMs = [];
      for (let batch = 0; batch < (campaign72Hours ? 0 : 5); batch++) {
        const started = performance.now();
        for (let tick = 0; tick < 10; tick++) {
          const sample = await dispatch({ type: "advance", wallMilliseconds: 100 });
          tickSamples.push({ batch, tick, wallMs: sample.wallMs, wasmMs: sample.wasmMs,
            profile: sample.profile,
            timeSeconds: sample.value.snapshotPatch.simulationTimeSeconds });
        }
        tickBatchServiceMs.push(performance.now() - started);
      }
      const speedSamples = [];
      for (const modeId of (campaign72Hours ? [] : ["10x", "60x"])) {
        await dispatch({ type: "set-playback-mode", modeId });
        for (let n = 0; n < 2; n++) {
          const sample = await dispatch({ type: "advance", wallMilliseconds: 100 });
          speedSamples.push({ modeId, wallMilliseconds: 100, wallMs: sample.wallMs, wasmMs: sample.wasmMs,
            profile: sample.profile,
            timeSeconds: sample.value.snapshotPatch.simulationTimeSeconds });
        }
      }
      const snapshot = JSON.parse((await request({ type: "get-snapshot" })).resultJson);
      if (campaign72Hours && (snapshot.simulationTimeSeconds !== 72 * 3600 || campaignRefuels.length !== 2))
        throw new Error("72-hour fuelling campaign did not complete.");
      if (campaign72Hours && (snapshot.freshBundlesAvailable !== (snapshot.shift?.unlimitedFreshFuel ? 0 : 112) || snapshot.refuellingOperationCount !== 2 || snapshot.shift.fuelConsumed !== 16))
        throw new Error("72-hour campaign fuel accounting differs from two eight-bundle operations.");
      if (!samples.every(sample => Number.isFinite(sample.meanXenon) && sample.meanXenon >= 0))
        throw new Error("Invalid xenon measurement.");
      if (samples.at(-1).poisonDigest === samples[0].poisonDigest) throw new Error("Poison state did not evolve.");
      if (!samples.at(-1).zoneFills.some((fill, n) => Math.abs(fill - samples[0].zoneFills[n]) > 1e-8))
        throw new Error("Zones did not respond during poison evolution.");
      return { campaign72Hours, campaignSeed: campaign72Hours ? 1001 : null, campaignRefuels,
        refuel: refuel ? { wallMs: refuel.wallMs, wasmMs: refuel.wasmMs, profile: refuel.profile } : null,
        samples, tickSamples, tickBatchServiceMs, speedSamples, finalTimeSeconds: snapshot.simulationTimeSeconds,
        finalFreshBundlesAvailable: snapshot.freshBundlesAvailable,
        finalStateDigest, finalReplayDigest,
        finalPoisonDigest: snapshot.xenon.stateDigestHex, finalZoneFills: snapshot.rrs.zones.map(zone => zone.fillFraction) };
    } finally { worker.terminate(); }
  }, { sampleCount, profiling, campaign72Hours });
  const stats = values => {
    const sorted = [...values].sort((a, b) => a - b);
    if (!sorted.length) return { count: 0, median: null, p95: null, maximum: null };
    return { count: sorted.length, median: sorted[Math.floor(sorted.length / 2)],
      p95: sorted[Math.ceil(sorted.length * .95) - 1], maximum: sorted.at(-1) };
  };
  if (errors.length) throw new Error(errors.join(" | "));
  console.log(JSON.stringify({ format: "candu-xenon-worker-benchmark-v1", url, buildInfo,
    measurementMode: profiling ? "production-worker-instrumented" : "production-worker-roundtrip", simulationSecondsPerWallSecond: 1800,
    wallLatencyMs: stats(result.samples.map(s => s.wallMs)), wasmLatencyMs: stats(result.samples.map(s => s.wasmMs)),
    proposed250MsGatePassed: stats(result.samples.map(s => s.wallMs)).p95 < 250,
    keepsUpWithOneSecondRequests: stats(result.samples.map(s => s.wallMs)).p95 < 1000,
    uiTickLatencyMs: stats(result.tickSamples.map(s => s.wallMs)),
    tenUiTicksServiceMs: stats(result.tickBatchServiceMs),
    uiTicksMeet100MsBudget: campaign72Hours ? null : stats(result.tickSamples.map(s => s.wallMs)).p95 < 100,
    consoleErrors: errors, ...result }, null, 2));
} finally { await browser.close(); }
