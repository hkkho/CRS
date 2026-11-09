import { chromium } from "playwright";

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
  const runs = await page.evaluate(async ({ seeds, batches }) => {
    const entry = document.querySelector('script[type="module"][src]').src;
    const source = await (await fetch(entry)).text();
    const workerAsset = source.match(/wasmWorker-[A-Za-z0-9_-]+\.js/)?.[0];
    if (!workerAsset) throw new Error("Production worker asset was not found.");

    const runs = [];
    for (const seed of seeds) {
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
              baseSequence: sequence,
              payload: { type: "commit-day", expectedCompletedDays: completedDays, channelIndices: channels },
            }),
          });
          const result = JSON.parse(response.resultJson);
          const elapsedMs = performance.now() - start;
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
            endpointOuterIterations: endpoint.physics.solverIterationCount,
            endpointResidual: endpoint.physics.solverResidualRelativeInfinity,
            endpointCandidateSolves: endpoint.rrs.candidateSolveCount,
          });
        }
        runs.push({ seed, dataPackId: initialResult.snapshot.dataPackId, samples });
      } finally {
        worker.terminate();
      }
    }
    return runs;
  }, { seeds, batches });
  if (errors.length) throw new Error(errors.join(" | "));
  console.log(JSON.stringify({ url, chromium: browser.version(), buildInfo, runs }, null, 2));
} finally {
  await browser.close();
}
