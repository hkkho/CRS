import { chromium } from "playwright";

const baseUrl = process.argv[2] ?? process.env.PLAYTEST_URL;
const wasmPath = process.argv[3] ?? process.env.PLAYTEST_WASM_PATH;

if (!baseUrl) {
  throw new Error("Pass the deployed playtest URL as the first argument or set PLAYTEST_URL.");
}

const targetUrl = new URL(baseUrl);
targetUrl.pathname = targetUrl.pathname.replace(/\/$/, "");

function absolutePath(pathname) {
  return new URL(pathname.replace(/^\//, ""), `${targetUrl.origin}/`).toString();
}

async function assertAsset(response, label, { rejectHtml = false, contentTypeIncludes } = {}) {
  if (!response.ok()) {
    throw new Error(`${label} returned HTTP ${response.status()}.`);
  }

  const contentType = response.headers()["content-type"] ?? "";
  if (rejectHtml && contentType.toLowerCase().includes("text/html")) {
    throw new Error(`${label} returned HTML instead of an asset (${contentType}).`);
  }
  if (contentTypeIncludes && !contentType.toLowerCase().includes(contentTypeIncludes)) {
    throw new Error(`${label} returned unexpected content type: ${contentType}.`);
  }
  return contentType;
}

const browser = await chromium.launch({ headless: true });
const page = await browser.newPage();
const consoleErrors = [];
const pageErrors = [];
page.on("console", (message) => {
  if (message.type() === "error") {
    consoleErrors.push(message.text());
  }
});
page.on("pageerror", (error) => pageErrors.push(error.message));

try {
  const mainResponse = await page.request.get(absolutePath("/wasm/main.mjs"));
  await assertAsset(mainResponse, "/wasm/main.mjs", { rejectHtml: true });
  const mainText = await mainResponse.text();
  if (mainText.trimStart().startsWith("<")) {
    throw new Error("/wasm/main.mjs returned markup instead of JavaScript.");
  }

  if (wasmPath) {
    const wasmResponse = await page.request.get(absolutePath(wasmPath));
    await assertAsset(wasmResponse, wasmPath, {
      rejectHtml: true,
      contentTypeIncludes: "application/wasm",
    });
  }

  const missingResponse = await page.request.get(absolutePath("/wasm/__candu_missing_asset__.mjs"));
  if (missingResponse.status() !== 404) {
    throw new Error(`Missing /wasm asset returned HTTP ${missingResponse.status()} instead of 404.`);
  }

  await page.goto(targetUrl.toString(), { waitUntil: "networkidle" });
  await page.locator('[data-authoritative-bridge="active"]').waitFor({ state: "visible", timeout: 60000 });
  const bridgeSource = await page.locator("[data-bridge-source]").getAttribute("data-bridge-source");
  const bridgeText = await page.locator("[data-bridge-source]").innerText();
  if (bridgeSource !== "wasm" || !bridgeText.includes("BROWSER WASM")) {
    throw new Error(`The browser did not report the authoritative bridge as active (source=${bridgeSource}).`);
  }

  const deterministicCheck = await page.evaluate(async () => {
    const protocol = "candu-playtest-v1";
    const command = JSON.stringify({
      protocol,
      payload: { type: "step", simulationSeconds: 3600 },
    });
    const root = globalThis;
    if (!root.canduPlaytestWasm) {
      await import("/wasm/main.mjs");
    }
    const api = root.canduPlaytestWasm;
    if (!api) {
      throw new Error("The browser WASM module did not expose canduPlaytestWasm.");
    }

    const parse = (value) => typeof value === "string" ? JSON.parse(value) : value;
    const firstInit = parse(await api.initialize(JSON.stringify({ protocol, mode: "play" })));
    const firstAction = parse(await api.dispatchJson(command));
    const replayInit = parse(await api.initialize(JSON.stringify({ protocol, mode: "play" })));
    const replayAction = parse(await api.dispatchJson(command));

    if (!firstInit.snapshot || !firstAction.snapshot || !replayInit.snapshot || !replayAction.snapshot) {
      throw new Error("The browser WASM bridge returned an incomplete deterministic response.");
    }
    if (firstInit.snapshot.source !== "wasm" || firstAction.snapshot.source !== "wasm") {
      throw new Error("The direct browser bridge check did not return WASM snapshots.");
    }
    if (!firstAction.accepted || !replayAction.accepted) {
      throw new Error("The deterministic bridge action was rejected.");
    }
    if (firstAction.stateDigest !== replayAction.stateDigest) {
      throw new Error("The deterministic bridge replay produced a different state digest.");
    }

    return {
      source: firstAction.snapshot.source,
      action: firstAction.command?.type ?? "step",
      stateDigest: firstAction.stateDigest,
    };
  });

  if (consoleErrors.length > 0 || pageErrors.length > 0) {
    throw new Error(`Browser errors detected: ${[...consoleErrors, ...pageErrors].join(" | ")}`);
  }

  console.log(JSON.stringify({
    status: "ok",
    url: targetUrl.toString(),
    bridge: "authoritative-csharp-wasm",
    deterministicCheck,
  }));
} finally {
  await browser.close();
}
