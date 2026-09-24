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

function assertAsset(response, label, { rejectHtml = false, contentTypeIncludes } = {}) {
  if (!response.ok()) throw new Error(`${label} returned HTTP ${response.status()}.`);
  const contentType = response.headers()["content-type"] ?? "";
  if (rejectHtml && contentType.toLowerCase().includes("text/html")) throw new Error(`${label} returned HTML instead of an asset (${contentType}).`);
  if (contentTypeIncludes && !contentType.toLowerCase().includes(contentTypeIncludes)) throw new Error(`${label} returned unexpected content type: ${contentType}.`);
}

async function getAssetWithRetry(page, pathname, label, options = {}) {
  const deadline = Date.now() + 60_000;
  let lastError;
  while (Date.now() < deadline) {
    const response = await page.request.get(absolutePath(pathname));
    try {
      assertAsset(response, label, options);
      return response;
    } catch (error) {
      lastError = error;
    }
    await new Promise((resolve) => setTimeout(resolve, 2_000));
  }
  throw new Error(`${lastError?.message ?? `${label} was unavailable`} (after 60s of retries)`);
}

const browser = await chromium.launch({ headless: true });
const page = await browser.newPage({ viewport: { width: 1600, height: 900 } });
const consoleErrors = [];
const pageErrors = [];
page.on("console", (message) => { if (message.type() === "error") consoleErrors.push(message.text()); });
page.on("pageerror", (error) => pageErrors.push(error.message));

try {
  const mainResponse = await getAssetWithRetry(page, "/wasm/main.mjs", "/wasm/main.mjs", { rejectHtml: true });
  if ((await mainResponse.text()).trimStart().startsWith("<")) throw new Error("/wasm/main.mjs returned markup instead of JavaScript.");
  if (wasmPath) await getAssetWithRetry(page, wasmPath, wasmPath, { rejectHtml: true, contentTypeIncludes: "application/wasm" });

  await page.goto(targetUrl.toString(), { waitUntil: "networkidle" });
  await page.locator("canvas").waitFor({ state: "attached", timeout: 10_000 });
  await page.waitForFunction(() => document.querySelector("#status-mirror")?.textContent?.includes("live reactor online"), undefined, { timeout: 60_000 });
  const mirror = await page.locator("#status-mirror").textContent();
  if (!mirror?.includes("380 channels")) throw new Error(`The browser did not report the full play snapshot: ${mirror}`);

  if (consoleErrors.length > 0 || pageErrors.length > 0) throw new Error(`Browser errors detected: ${[...consoleErrors, ...pageErrors].join(" | ")}`);
  const viewport = await page.locator("canvas").evaluate((canvas) => ({ width: canvas.width, height: canvas.height }));
  if (viewport.width !== 1600 || viewport.height !== 900) throw new Error(`Unexpected Phaser game viewport: ${viewport.width} × ${viewport.height}.`);
  const capture = (x, y, width, height) => page.screenshot({ clip: { x, y, width, height } });
  const expectChanged = (before, after, label) => {
    if (before.equals(after)) throw new Error(`${label} did not respond to a mouse click.`);
  };

  await page.mouse.click(300, 820); // Begin Shift
  await page.waitForTimeout(200);
  const refuelBefore = await capture(1240, 740, 315, 79);
  await page.mouse.click(1518, 757); // 8 Bundles
  await page.mouse.move(50, 850);
  const refuelAfter = await capture(1240, 740, 315, 79);
  expectChanged(refuelBefore, refuelAfter, "Refuel size");

  const modalBefore = await capture(400, 220, 220, 100);
  await page.mouse.click(1496, 800); // Control
  const modalAfter = await capture(400, 220, 220, 100);
  expectChanged(modalBefore, modalAfter, "Control dialog");
  const targetBefore = await capture(800, 335, 205, 35);
  await page.mouse.click(752, 450); // Increase power target
  const targetAfter = await capture(800, 335, 205, 35);
  expectChanged(targetBefore, targetAfter, "Power target");

  await page.mouse.click(200, 200); // Close dialog outside its frame
  const designerBefore = await capture(1090, 250, 150, 100);
  await page.mouse.click(1285, 22); // Core Designer
  await page.waitForTimeout(200);
  const designerAfter = await capture(1090, 250, 150, 100);
  expectChanged(designerBefore, designerAfter, "Core Designer");

  if (consoleErrors.length > 0 || pageErrors.length > 0) throw new Error(`Browser errors detected: ${[...consoleErrors, ...pageErrors].join(" | ")}`);
  console.log(JSON.stringify({ status: "ok", url: targetUrl.toString(), bridge: "authoritative-csharp-wasm", viewport, mirror, mouseControls: "ok" }));
} finally {
  await browser.close();
}
