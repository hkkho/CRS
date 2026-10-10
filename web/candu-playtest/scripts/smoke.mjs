import { mkdir, writeFile } from "node:fs/promises";
import { join } from "node:path";
import { verifyRecovery } from "./recovery-smoke.mjs";
import { chromium } from "playwright";
import { verifyShift } from "./shift-smoke.mjs";
import { verifyDailyTurns } from "./daily-turn-smoke.mjs";
import { verifyDailyFailure } from "./daily-failure-smoke.mjs";
import { verifyStudio } from "./studio-smoke.mjs";

const baseUrl = process.argv[2] ?? process.env.PLAYTEST_URL;
const wasmPath = process.argv[3] ?? process.env.PLAYTEST_WASM_PATH;

if (!baseUrl) {
  throw new Error("Pass the deployed playtest URL as the first argument or set PLAYTEST_URL.");
}

const targetUrl = new URL(baseUrl);
targetUrl.pathname = `${targetUrl.pathname.replace(/\/$/, "")}/`;

function absolutePath(pathname) {
  return new URL(pathname.replace(/^\//, ""), targetUrl).toString();
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
  await page.locator(".reactor-launcher").waitFor({ state: "attached", timeout: 10_000 });
  await page.waitForFunction(() => document.querySelector("#status-mirror")?.textContent?.includes("live reactor online"), undefined, { timeout: 60_000 });
  const mirror = await page.locator("#status-mirror").textContent();
  if (!mirror?.includes("380 channels")) throw new Error(`The browser did not report the full play snapshot: ${mirror}`);

  if (consoleErrors.length > 0 || pageErrors.length > 0) throw new Error(`Browser errors detected: ${[...consoleErrors, ...pageErrors].join(" | ")}`);
  const viewport = await page.locator("#game-root").evaluate(root => ({ width: root.clientWidth, height: root.clientHeight }));
  const seedInput = page.locator('.reactor-launcher [data-field="seed"]');
  const seedBefore = await seedInput.inputValue();
  await page.locator('.reactor-launcher [data-action="next"]').click();
  await page.waitForFunction(() => document.querySelector("#status-mirror")?.textContent?.includes("Browser playtest run reset."), undefined, { timeout: 60_000 });
  if (await seedInput.inputValue() === seedBefore) throw new Error("New aged core did not advance the displayed seed.");

  await seedInput.fill("1001");
  await page.getByRole("button", { name: "Use seed & objective", exact: true }).click();
  await page.waitForFunction(() => document.querySelector('.reactor-launcher')?.getAttribute('aria-busy') === 'false' && document.querySelector('.reactor-launcher [data-field="seed"]')?.value === '1001');
  await page.getByRole("button", { name: "Begin shift", exact: true }).click();
  await page.locator(".reactor-studio").waitFor({ state: "visible" });
  const daily = await verifyDailyTurns(page);
  // Retain legacy pacing smoke as an explicit developer comparison.
  const legacyUrl = new URL(targetUrl); legacyUrl.searchParams.set("pacing", "real-time");
  await page.goto(legacyUrl.toString(), { waitUntil: "networkidle" });
  await page.waitForFunction(() => document.querySelector("#status-mirror")?.textContent?.includes("live reactor online"), undefined, { timeout: 60_000 });
  await page.getByRole("button", { name: "Begin shift", exact: true }).click();
  const studio = await verifyStudio(page);
  const shift = await verifyShift(page);

  if (consoleErrors.length > 0 || pageErrors.length > 0) throw new Error(`Browser errors detected: ${[...consoleErrors, ...pageErrors].join(" | ")}`);
  await verifyRecovery(browser, targetUrl.toString());
  const dailyFailure = await verifyDailyFailure(browser, targetUrl.toString());
  console.log(JSON.stringify({ status: "ok", url: targetUrl.toString(), bridge: "authoritative-csharp-wasm", viewport, mirror, mouseControls: "ok", daily, dailyFailure, studio, shift }));
} finally {
  if (process.env.PLAYTEST_CAPTURE_DIR) {
    const directory = process.env.PLAYTEST_CAPTURE_DIR;
    await mkdir(directory, { recursive: true });
    await page.screenshot({ path: join(directory, "smoke-final.png"), fullPage: true }).catch(() => undefined);
    await writeFile(join(directory, "browser-errors.json"), JSON.stringify({ consoleErrors, pageErrors }, null, 2));
  }
  await browser.close();
}
