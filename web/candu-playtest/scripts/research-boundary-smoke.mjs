import { chromium } from "playwright";

const [defaultUrl, researchUrl] = process.argv.slice(2);
if (!defaultUrl || !researchUrl) throw new Error("Pass default and research preview URLs.");
const browser = await chromium.launch({ headless: true });
try {
  for (const [url, expectedResearch] of [[defaultUrl, false], [researchUrl, true]]) {
    const page = await browser.newPage();
    await page.goto(url, { waitUntil: "domcontentloaded" });
    const result = await page.evaluate(async () => {
      const source = `
        try {
          await import(${JSON.stringify(new URL("/wasm/main.mjs", location.href).href)});
          const api = globalThis.canduPlaytestWasm;
          api.initialize(JSON.stringify({ protocol: "candu-playtest-v2", mode: "play" }));
          const before = api.getSnapshotJson();
          const research = typeof api.getGpuPrototypeFixtureJson === "function";
          let fixture;
          if (research) fixture = JSON.parse(api.getGpuPrototypeFixtureJson(JSON.stringify({
            protocol: "candu-spatial-gpu-prototype-v1", group: 1, iterations: 0
          })));
          postMessage({ research, profiling: typeof api.dispatchProfileJson === "function",
            unchanged: before === api.getSnapshotJson(), nodeCount: fixture?.nodeCount,
            fixtureKeys: fixture ? Object.keys(fixture) : [] });
        } catch (error) { postMessage({ error: String(error?.stack ?? error) }); }
      `;
      const blob = URL.createObjectURL(new Blob([source], { type: "text/javascript" }));
      const worker = new Worker(blob, { type: "module" });
      try {
        return await new Promise((resolve, reject) => {
          const timer = setTimeout(() => reject(new Error("WASM boundary check timed out")), 120_000);
          worker.onmessage = ({ data }) => { clearTimeout(timer); resolve(data); };
          worker.onerror = error => { clearTimeout(timer); reject(new Error(error.message)); };
        });
      } finally { worker.terminate(); URL.revokeObjectURL(blob); }
    });
    if (result.error) throw new Error(result.error);
    if (result.research !== expectedResearch || result.profiling || !result.unchanged)
      throw new Error(`Unexpected runtime boundary: ${JSON.stringify(result)}`);
    if (expectedResearch && result.nodeCount !== 4560)
      throw new Error(`Unexpected research fixture: ${JSON.stringify(result)}`);
    console.log(JSON.stringify({ url, ...result }));
    await page.close();
  }
} finally { await browser.close(); }
