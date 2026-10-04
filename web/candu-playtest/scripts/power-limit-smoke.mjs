import { chromium } from "playwright";
import { mkdir, writeFile } from "node:fs/promises";

// Verify a playable full-power start, map scales, synchronized history,
// and a deliberate first-tick power-limit ending in the actual WASM runtime.
const url = process.argv[2];
if (!url) throw new Error("Pass the playtest URL.");
const browser = await chromium.launch({ headless: true });
const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
const errors = [];
page.on("pageerror", error => errors.push(error.message));
page.on("console", message => { if (message.type() === "error") errors.push(message.text()); });
const check = (condition, message) => { if (!condition) throw new Error(message); };
try {
  await page.goto(url, { waitUntil: "networkidle" });
  await page.waitForFunction(() => document.querySelector("#status-mirror")?.textContent?.includes("live reactor online"), undefined, { timeout: 120_000 });
  const launcherReason = await page.locator('.reactor-launcher [data-field="connection"]').textContent();
  check(!launcherReason.includes("ended"), "Rebalanced starting core is already terminal.");
  check((await page.locator(".reactor-launcher").textContent()).includes("935 kW"), "Bundle limit was not explained.");
  await page.getByRole("button", { name: "Begin shift", exact: true }).click();
  const studio = page.locator(".reactor-studio");
  check(await studio.locator('[data-field="ending"]').isHidden(), "Rebalanced start is not playable.");
  await studio.locator('[data-action="pause"]').click();
  await page.waitForFunction(() => document.querySelector("#status-mirror")?.textContent?.includes("Paused."));
  for (const [mode, legend] of [["power", "7300 kW / channel"], ["ripple", "100% reference"], ["bundle-power", "935 kW / hottest bundle"]]) {
    await studio.locator(`[data-map="${mode}"]`).click();
    check((await studio.locator('[data-field="legend"]').textContent()).includes(legend), `${mode} legend is incorrect.`);
  }
  check(await studio.locator('[data-power-limit="935"]').count() === 1, "Bundle profile lacks its power limit.");
  await studio.locator('summary').filter({ hasText: 'Power & time controls' }).click();
  await studio.locator('[data-action="step"]').click();
  await page.waitForFunction(() => !document.querySelector('.reactor-studio')?.getAttribute('aria-busy')?.includes('true'), undefined, { timeout: 120_000 });
  const liveTime = await studio.locator('[data-field="time"]').textContent();
  await studio.locator('[data-history="inspector"]').fill("0");
  check((await studio.locator('[data-history="readout"]').textContent()).includes("HISTORY"), "Historical view was not labelled.");
  check(await studio.locator('[data-action="refuel"]').isDisabled(), "Historical refuelling is enabled.");
  check(await studio.locator('[data-field="time"]').textContent() !== liveTime, "Historical inspector retained live time.");
  await studio.locator('[data-history="live"]').click();
  check(await studio.locator('[data-field="time"]').textContent() === liveTime, "Return to live did not restore current data.");
  const capture = process.env.PLAYTEST_CAPTURE_DIR;
  if (capture) {
    await mkdir(capture, { recursive: true });
    await page.screenshot({ path: `${capture}/power-maps-live.png`, fullPage: true });
  }
  await studio.locator('[data-field="target-input"]').fill("120");
  await studio.locator('[data-action="pause"]').click();
  await page.waitForFunction(() => !document.querySelector('.reactor-studio [data-action="target"]')?.disabled);
  await studio.locator('[data-action="target"]').click();
  await studio.locator('[data-field="ending"]').waitFor({ state: "visible" });
  const reason = await studio.locator('[data-field="ending-reason"]').textContent();
  check(reason === "Channel power exceeds 7,300 kW", "Studio did not display the authoritative ending reason.");
  check(await studio.locator('[data-action="refuel"]').isDisabled(), "Terminal refuelling remained enabled.");
  const time = await studio.locator('[data-field="time"]').textContent();
  const score = await studio.locator('[data-field="score"]').textContent();
  const fuel = await studio.locator('[data-field="stock"]').textContent();
  if (capture) {
    await mkdir(capture, { recursive: true });
    await page.screenshot({ path: `${capture}/power-limit-ending.png` });
    await writeFile(`${capture}/power-limit-ending.json`, JSON.stringify({ launcherReason, reason, time, score, fuel, errors }, null, 2));
  }
  check(errors.length === 0, "Browser errors: " + errors.join(" | "));
  console.log(JSON.stringify({ status: "ok", startingProfileBelowLimits: true, powerMaps: 3, synchronizedHistory: true, reason, time, score, fuel, errors }));
} finally { await browser.close(); }
