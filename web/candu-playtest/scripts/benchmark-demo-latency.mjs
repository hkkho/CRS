import { chromium } from "playwright";
import { build } from "esbuild";

const url = process.argv[2];
if (!url) throw new Error("Pass the URL of a production build or preview.");
const blocks = Number(process.argv[3] ?? 3);
if (!Number.isInteger(blocks) || blocks < 1 || blocks > 3) {
  throw new Error("Pass one to three sample blocks per seed.");
}

// One excluded empty half-day, then one sample of each batch size per block.
const batches = [[], [], [210], [45, 324], [], [105], [75, 270], [], [180], [25, 352]]
  .slice(0, 1 + 3 * blocks);
const seeds = [1001, 1002, 1003];
const verifyLegacy = process.argv.includes("--verify-legacy");
const browser = await chromium.launch({ headless: true });
try {
  const page = await browser.newPage();
  const errors = [];
  page.on("pageerror", error => errors.push(error.message));
  page.on("console", message => {
    if (message.type() === "error") errors.push(message.text());
  });
  await page.goto(url, { waitUntil: "domcontentloaded" });
  const buildInfoUrl = new URL("wasm/build-info.json", page.url());
  const buildInfo = await (await page.request.get(buildInfoUrl.href)).json();
  if (verifyLegacy) {
    const parser = await build({
      stdin: { contents: "export { canonicalJson, parseProtocolResponse } from './src/protocol';", resolveDir: process.cwd() },
      bundle: true, write: false, format: "iife", globalName: "demoWireProtocol",
    });
    await page.addScriptTag({ content: parser.outputFiles[0].text });
  }
  const runs = await page.evaluate(async ({ seeds, batches, verifyLegacy }) => {
    const entry = document.querySelector('script[type="module"][src]').src;
    const source = await (await fetch(entry)).text();
    const workerAsset = source.match(/wasmWorker-[A-Za-z0-9_-]+\.js/)?.[0];
    if (!workerAsset) throw new Error("Production worker asset was not found.");

    const runs = [];
    for (const seed of seeds) {
      const checkThisSeed = verifyLegacy && seed === seeds[0];
      const compact = await run(seed, true, checkThisSeed);
      if (checkThisSeed) {
        const legacy = await run(seed, false, true);
        for (const [index, sample] of compact.samples.entries()) {
          const original = legacy.samples[index];
          if (sample.snapshotHash !== original.snapshotHash || sample.stateDigest !== original.stateDigest ||
              sample.replayDigest !== original.replayDigest) throw new Error(`Wire parity failed for seed ${seed}, step ${index + 1}.`);
          sample.legacyBytes = original.bytes;
        }
      }
      runs.push(compact);
    }
    return runs;

    async function run(seed, demoReadings, materialize) {
      const worker = new Worker(new URL(workerAsset, entry), { type: "module" });
      let id = 0;
      function exchange(request) {
        return new Promise((resolve, reject) => {
          const timer = setTimeout(() => reject(new Error("Worker timed out.")), 120_000);
          const finish = (value, error) => {
            clearTimeout(timer);
            worker.onmessage = null;
            worker.onerror = null;
            if (error) reject(error);
            else resolve(value);
          };
          worker.onerror = error => finish(null, new Error(error.message));
          worker.onmessage = ({ data }) => {
            if (data.type === "load-error" || data.type === "error") {
              finish(null, new Error(data.error));
            } else if (request ? data.type === "result" && data.id === id : data.type === "ready") {
              finish(data);
            }
          };
          if (request) worker.postMessage({ ...request, id: ++id });
        });
      }
      try {
        await exchange();
        const initial = await exchange({ type: "initialize", mode: "play", seed, pacingMode: "demo" });
        const initialResult = JSON.parse(initial.resultJson);
        if (!initialResult.accepted || initialResult.snapshot.pacingMode !== "demo" ||
            initialResult.snapshot.core.channelCount !== 380 ||
            initialResult.snapshot.core.bundlePositionCount !== 12) {
          throw new Error(`Seed ${seed} did not initialize the expected core.`);
        }
        let materialized = materialize ? demoWireProtocol.parseProtocolResponse(initial.resultJson).snapshot : undefined;
        let { sequence } = initialResult;
        let { completedDays, simulationTimeSeconds } = initialResult.snapshot;
        let fuelConsumed = initialResult.snapshot.shift.fuelConsumed;
        const samples = [];
        for (const [index, channels] of batches.entries()) {
          const start = performance.now();
          const response = await exchange({
            type: "dispatch",
            commandJson: JSON.stringify({
              protocol: "candu-playtest-v2",
              type: "command",
              responseMode: "compact",
              ...(demoReadings ? { demoReadings: true } : {}),
              baseSequence: sequence,
              payload: { type: "commit-day", expectedCompletedDays: completedDays, channelIndices: channels },
            }),
          });
          const result = JSON.parse(response.resultJson);
          const elapsedMs = performance.now() - start;
          if (demoReadings && (!result.demoReadings || result.coreReplacement))
            throw new Error(`Seed ${seed}, step ${index + 1} did not use the demo readings encoding.`);
          let snapshotHash;
          if (materialized) {
            materialized = demoWireProtocol.parseProtocolResponse(response.resultJson, materialized).snapshot;
            const bytes = new TextEncoder().encode(demoWireProtocol.canonicalJson(materialized));
            snapshotHash = Array.from(new Uint8Array(await crypto.subtle.digest("SHA-256", bytes)), byte => byte.toString(16).padStart(2, "0")).join("");
          }
          const endpoint = result.snapshotPatch;
          if (!result.accepted || !["paused", "running"].includes(endpoint?.runStatus) ||
              result.sequence !== sequence + 1 ||
              endpoint.simulationTimeSeconds !== simulationTimeSeconds + 43_200 ||
              endpoint.shift.fuelConsumed !== fuelConsumed + 8 * channels.length ||
              !result.stateDigest?.startsWith("sha256:") ||
              !Number.isFinite(endpoint.physics.solverResidualRelativeInfinity)) {
            throw new Error(`Seed ${seed}, command ${index + 1}, status ${endpoint?.runStatus}: ${endpoint?.runEndReason || result.message}`);
          }
          ({ completedDays, simulationTimeSeconds } = endpoint);
          sequence = result.sequence;
          fuelConsumed = endpoint.shift.fuelConsumed;
          samples.push({
            channels, warmup: index === 0, elapsedMs, workerMs: response.wasmCallDurationMs,
            bytes: response.returnedUtf8PayloadBytes, simulationTimeSeconds, stateDigest: result.stateDigest,
            replayDigest: result.replayDigest, snapshotHash,
            endpointOuterIterations: endpoint.physics.solverIterationCount,
            endpointResidual: endpoint.physics.solverResidualRelativeInfinity,
            endpointCandidateSolves: endpoint.rrs.candidateSolveCount,
          });
        }
        return { seed, dataPackId: initialResult.snapshot.dataPackId, samples };
      } finally {
        worker.terminate();
      }
    }
  }, { seeds, batches, verifyLegacy });
  if (errors.length) throw new Error(errors.join(" | "));
  console.log(JSON.stringify({ url, chromium: browser.version(), buildInfo, verifiedLegacyParity: verifyLegacy, runs }, null, 2));
} finally {
  await browser.close();
}
