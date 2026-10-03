import { chromium } from "playwright";
import { mkdir, writeFile } from "node:fs/promises";
import { dirname } from "node:path";

const url = process.argv[2] ?? "http://127.0.0.1:4173";
const output = process.argv[3];
const browser = await chromium.launch({ headless: true });
const samples = [];
try {
  for (const profile of ["normal", "constrained"]) for (let sample = 0; sample < 3; sample++) {
    const context = await browser.newContext({ viewport: { width: 1280, height: 720 } });
    const page = await context.newPage();
    const cdp = await context.newCDPSession(page);
    await cdp.send("Network.enable"); await cdp.send("Network.setCacheDisabled", { cacheDisabled: true });
    if (profile === "constrained") {
      await cdp.send("Emulation.setCPUThrottlingRate", { rate: 4 });
      await cdp.send("Network.emulateNetworkConditions", { offline: false, latency: 60, downloadThroughput: 1500000, uploadThroughput: 500000 });
    }
    const errors = [];
    page.on("pageerror", error => errors.push(error.message));
    let bytes = 0, jsBytes = 0, wasmRequestMs = null, encodedPageBytes = 0;
    const assets = [];
    const bodyReads = [];
    cdp.on("Network.loadingFinished", event => { encodedPageBytes += event.encodedDataLength; });
    const start = performance.now();
    context.on("request", request => { if (request.url().endsWith("/wasm/main.mjs")) wasmRequestMs = performance.now() - start; });
    context.on("response", response => { bodyReads.push((async () => {
      const headers = response.headers();
      const size = Number(headers["content-length"] ?? 0) > 0 && !headers["content-encoding"]
        ? Number(headers["content-length"]) : (await response.body()).length;
      bytes += size;
      if (/\.(m?js)(\?|$)/.test(response.url())) jsBytes += size;
      assets.push({ url: new URL(response.url()).pathname, bytes: size });
    })().catch(error => errors.push(`Asset measurement: ${error.message}`))); });
    await page.goto(url, { waitUntil: "domcontentloaded" });
    await page.locator(".reactor-launcher").waitFor();
    const shellMs = performance.now() - start;
    await page.waitForFunction(() => document.querySelector('.reactor-launcher [data-action="begin"]')?.getAttribute("aria-disabled") === "false", undefined, { timeout: 120000 });
    const readyMs = performance.now() - start;
    await Promise.all(bodyReads);
    if (errors.length) throw new Error(errors.join("\n"));
    samples.push({ profile, sample, shellMs, readyMs, wasmRequestMs,
      runtimeRequestToReadyMs: readyMs - (wasmRequestMs ?? 0), decodedResponseBodyBytes: bytes, javaScriptBodyBytes: jsBytes, encodedPageBytes, assets });
    await context.close();
  }
  const report = { conditions: "Cold contexts/cache disabled; constrained = page CDP 4x CPU, 60ms latency, 1.5MB/s download. Worker CPU throttling is not claimed. Body bytes are decoded; encoded page-target bytes exclude worker-target traffic.", samples };
  if (output) { await mkdir(dirname(output), { recursive: true }); await writeFile(output, JSON.stringify(report, null, 2)); }
  console.log(JSON.stringify({ ...report, samples: samples.map(({ assets, ...summary }) => summary) }));
} finally { await browser.close(); }
