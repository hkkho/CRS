/** Summarize opt-in worker timings without double-counting nested scopes. */
import { readFileSync } from "node:fs";

const [profilePath, normalPath] = process.argv.slice(2);
if (!profilePath) throw new Error("Pass instrumented JSON and optionally an unprofiled replay JSON.");
const load = path => JSON.parse(readFileSync(path, "utf8").replace(/^\uFEFF/, ""));
const run = load(profilePath);
if (run.measurementMode !== "production-worker-instrumented") throw new Error("Input is not an instrumented run.");
const summarize = samples => {
  const totals = new Map();
  for (const sample of samples) {
    for (const row of sample.profile ?? []) {
      const total = totals.get(row.name) ?? { name: row.name, calls: 0, inclusiveMs: 0, exclusiveMs: 0 };
      total.calls += row.calls; total.inclusiveMs += row.inclusiveMs; total.exclusiveMs += row.exclusiveMs;
      totals.set(row.name, total);
    }
  }
  const allExclusiveMs = [...totals.values()].reduce((sum, row) => sum + row.exclusiveMs, 0);
  return { requests: samples.length, meanWasmMs: samples.reduce((sum, row) => sum + row.wasmMs, 0) / samples.length,
    rows: [...totals.values()].sort((a, b) => b.exclusiveMs - a.exclusiveMs).map(row => ({
      name: row.name, callsPerRequest: row.calls / samples.length,
      inclusiveMsPerRequest: row.inclusiveMs / samples.length,
      exclusiveMsPerRequest: row.exclusiveMs / samples.length,
      exclusiveSharePercent: row.exclusiveMs / allExclusiveMs * 100,
    })) };
};
const hasSolve = sample => sample.profile?.some(row => row.name === "rrs-event");
const report = {
  format: "candu-worker-profile-summary-v1",
  live: summarize(run.samples),
  routineTicks: summarize(run.tickSamples.filter(sample => !hasSolve(sample))),
  solveTicks: summarize(run.tickSamples.filter(hasSolve)),
  refuel: summarize([run.refuel]),
  speeds: Object.fromEntries(["10x", "60x"].map(mode => [mode, summarize(run.speedSamples.filter(s => s.modeId === mode))])),
};
if (normalPath) {
  const normal = load(normalPath);
  report.replayMatches = ["finalStateDigest", "finalReplayDigest", "finalPoisonDigest", "finalTimeSeconds"]
    .every(key => run[key] !== undefined && run[key] === normal[key]);
  report.instrumentedP95Ms = run.wasmLatencyMs.p95;
  report.unprofiledP95Ms = normal.wasmLatencyMs.p95;
  if (!report.replayMatches) throw new Error("Instrumented and unprofiled replay outcomes differ.");
}
console.log(JSON.stringify(report, null, 2));
