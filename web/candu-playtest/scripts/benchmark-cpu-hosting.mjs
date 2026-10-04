import { chromium } from "playwright";
import { readFile } from "node:fs/promises";
import { isDeepStrictEqual } from "node:util";

const [url, prefix = "/cpu-threaded-main/", countText = "8", referencePath] = process.argv.slice(2);
if (!url) throw new Error("Pass preview URL, runtime prefix, sample count, and optional serial JSON reference.");
const sampleCount = Number(countText);
if (!Number.isInteger(sampleCount) || sampleCount < 2 || sampleCount > 20) throw new Error("Use 2–20 samples.");
const browser = await chromium.launch({ headless: true });
const errors = [];
try {
  const page = await browser.newPage();
  page.on("pageerror", error => errors.push(error.message));
  page.on("console", message => {
    if (message.type() === "error") errors.push(message.text());
    if (message.text().startsWith("cpu-host:")) process.stderr.write(message.text() + "\n");
  });
  // Empty same-origin diagnostic page: the product's worker host is not started.
  const diagnosticUrl = new URL("/cpu-host-experiment", url).href;
  await page.route(diagnosticUrl, route => route.fulfill({ contentType: "text/html",
    headers: { "Cross-Origin-Opener-Policy": "same-origin", "Cross-Origin-Embedder-Policy": "require-corp" },
    body: "<!doctype html><title>CPU hosting experiment</title><p>Background simulation hosting test</p>" }));
  await page.goto(diagnosticUrl);
  const runtimeUrl = new URL(prefix, url).href;
  const buildInfo = JSON.parse((await (await page.request.get(new URL("build-info.json", runtimeUrl).href)).text()).replace(/^\uFEFF/, ""));
  const result = await page.evaluate(async ({ runtimeUrl, sampleCount }) => {
    if (!crossOriginIsolated) throw new Error("Threaded test requires cross-origin isolation.");
    await import(new URL("main.mjs", runtimeUrl).href);
    const api = globalThis.canduPlaytestWasm;
    const hostInfo = api.getCpuHostInfo ? JSON.parse(await api.getCpuHostInfo()) : null;
    console.info("cpu-host: runtime ready", JSON.stringify(hostInfo));
    if (hostInfo && hostInfo.partitions > 1 && hostInfo.commandThreadId === hostInfo.entryThreadId)
      throw new Error("Threaded command ran on the JS entry thread.");
    let beatCount = 0, maxBeatGapMs = 0, lastBeat = performance.now();
    const heartbeat = setInterval(() => {
      const now = performance.now(); maxBeatGapMs = Math.max(maxBeatGapMs, now - lastBeat);
      lastBeat = now; beatCount++;
    }, 16);
    const timed = async action => {
      const started = performance.now();
      let timer;
      const value = JSON.parse(await Promise.race([action(), new Promise((_, reject) => {
        timer = setTimeout(() => reject(new Error("Host command timed out.")), 120_000);
      })]).finally(() => clearTimeout(timer)));
      if (!value.accepted) throw new Error(value.message ?? "Request rejected.");
      return { value, wallMs: performance.now() - started };
    };
    try {
      const initial = await timed(() => api.initialize(JSON.stringify({ protocol: "candu-playtest-v2", mode: "play" })));
      console.info("cpu-host: session initialized", initial.wallMs);
      let sequence = initial.value.sequence, finalStateDigest, finalReplayDigest;
      const dispatch = async payload => {
        const response = await timed(() => api.dispatchJson(JSON.stringify({ protocol: "candu-playtest-v2",
          type: "command", responseMode: "compact", baseSequence: sequence, payload })));
        sequence = response.value.sequence;
        finalStateDigest = response.value.stateDigest; finalReplayDigest = response.value.replayDigest;
        return response;
      };
      await dispatch({ type: "pause" });
      const refuel = await dispatch({ type: "commit-refuel", request: { channelIndex: 210,
        directionId: "toward-end-b", shiftCount: 8, fuelTypeId: "NAT-U-SYNTHETIC" } });
      console.info("cpu-host: refuel accepted", refuel.wallMs);
      await dispatch({ type: "resume" });
      await dispatch({ type: "queue-power-target", targetFraction: .95 });
      const samples = [];
      for (let n = 0; n < sampleCount; n++) {
        const response = await dispatch({ type: "advance", wallMilliseconds: 1000 });
        const patch = response.value.snapshotPatch;
        samples.push({ wallMs: response.wallMs, timeSeconds: patch.simulationTimeSeconds,
          poisonDigest: patch.xenon.stateDigestHex, coupledPoisonDigest: patch.xenon.coupledStateDigestHex,
          zoneFills: patch.rrs.zones.map(zone => zone.fillFraction) });
      }
      const ticks = [];
      for (let n = 0; n < 50; n++) ticks.push((await dispatch({ type: "advance", wallMilliseconds: 100 })).wallMs);
      for (const modeId of ["10x", "60x"]) {
        await dispatch({ type: "set-playback-mode", modeId });
        for (let n = 0; n < 2; n++) await dispatch({ type: "advance", wallMilliseconds: 100 });
      }
      const snapshot = JSON.parse(await api.getSnapshotJson());
      const before = await api.getSnapshotJson();
      const [queuedBefore, rejected, queuedAfter] = await Promise.all([
        api.getSnapshotJson(),
        api.dispatchJson(JSON.stringify({ protocol: "candu-playtest-v2", type: "command",
          baseSequence: sequence, payload: { type: "unsupported-host-test-command" } })),
        api.getSnapshotJson(),
      ]);
      const rejection = JSON.parse(rejected);
      // Dispatched rejections are audited: sequence/replay advance, physical
      // state does not. Full and compact reply digests also use different formats.
      if (rejection.accepted || rejection.sequence !== sequence + 1 || rejection.replayDigest === finalReplayDigest)
        throw new Error("Queued rejection did not follow the bridge audit contract.");
      const prior = JSON.parse(before);
      for (const returned of [queuedBefore, queuedAfter]) {
        const current = JSON.parse(returned);
        for (const key of ["simulationTimeSeconds", "core", "rrs", "xenon", "physics",
          "freshBundlesAvailable", "refuellingOperationCount", "scoreTotal", "isPaused"])
          if (JSON.stringify(prior[key]) !== JSON.stringify(current[key]))
            throw new Error(`Queued rejection changed physical snapshot field: ${key}`);
      }
      // Permit the heartbeat immediately after the last command to run before stopping.
      await new Promise(resolve => setTimeout(resolve, 32));
      return { hostInfo, queuedRejectionAtomic: true, postGuardSequence: rejection.sequence,
        postGuardReplayDigest: rejection.replayDigest,
        initialMs: initial.wallMs, refuelMs: refuel.wallMs, samples, ticks,
        heartbeat: { beatCount, maxBeatGapMs }, finalTimeSeconds: snapshot.simulationTimeSeconds,
        finalStateDigest, finalReplayDigest, finalPoisonDigest: snapshot.xenon.stateDigestHex,
        finalZoneFills: snapshot.rrs.zones.map(zone => zone.fillFraction) };
    } finally { clearInterval(heartbeat); }
  }, { runtimeUrl, sampleCount });
  if (errors.length) throw new Error(errors.join(" | "));
  let referenceMatched = null;
  if (referencePath) {
    const reference = JSON.parse((await readFile(referencePath, "utf8")).replace(/^\uFEFF/, ""));
    for (const key of ["finalTimeSeconds", "finalStateDigest", "finalReplayDigest", "finalPoisonDigest", "finalZoneFills"])
      if (!isDeepStrictEqual(reference[key], result[key])) throw new Error(`Serial reference differs: ${key}`);
    if (!isDeepStrictEqual(reference.samples.map(({ timeSeconds, poisonDigest, coupledPoisonDigest, zoneFills }) =>
      ({ timeSeconds, poisonDigest, coupledPoisonDigest, zoneFills })), result.samples.map(({ wallMs, ...state }) => state)))
      throw new Error("Sampled poison/zone states differ from the serial worker reference.");
    referenceMatched = true;
  }
  const stats = values => {
    const sorted = [...values].sort((a, b) => a - b);
    return { median: sorted[Math.floor(sorted.length / 2)], p95: sorted[Math.ceil(sorted.length * .95) - 1] };
  };
  console.log(JSON.stringify({ format: "candu-cpu-main-host-benchmark-v1", url, runtimeUrl, buildInfo,
    referenceMatched, wallLatencyMs: stats(result.samples.map(s => s.wallMs)), tickLatencyMs: stats(result.ticks),
    consoleErrors: errors, ...result }, null, 2));
} finally { await browser.close(); }
