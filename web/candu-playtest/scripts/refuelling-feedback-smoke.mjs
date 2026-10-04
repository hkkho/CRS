import { chromium } from "playwright";
import { mkdir } from "node:fs/promises";

// Focused real-WASM acceptance path for the refuelling feedback slices.
const browser = await chromium.launch({ headless: true });
const page = await browser.newPage({ viewport: { width: 1600, height: 900 } });
const errors = [];
page.on("pageerror", error => errors.push(error.message));
page.on("console", message => { if (message.type() === "error") errors.push(message.text()); });
const check = (condition, message) => { if (!condition) throw new Error(message); };
const idle = () => page.waitForFunction(() => document.querySelector(".reactor-studio")?.getAttribute("aria-busy") === "false", undefined, { timeout: 120000 });
async function returnToStudio() {
  const deadline = Date.now() + 120000;
  while (await page.locator(".reactor-studio").count() === 0) {
    if (Date.now() > deadline) throw new Error("Designer edit did not finish.");
    await page.keyboard.press("Escape");
    await page.waitForTimeout(200);
  }
}
try {
  await page.goto(process.argv[2] ?? "http://127.0.0.1:4173");
  await page.waitForFunction(() => document.querySelector("#status-mirror")?.textContent?.includes("live reactor online"), undefined, { timeout: 60000 });
  await page.getByRole("button", { name: "Begin shift", exact: true }).click();
  const studio = page.locator(".reactor-studio"); await studio.waitFor();
  await studio.locator('[data-action="pause"]').click(); await idle();
  const highest = studio.locator('[data-action="oldest"]');
  check((await highest.textContent()).includes("Highest burnup"), "Candidate name still implies fuel residence age.");
  await highest.click();
  check((await studio.locator('[data-field="order-note"]').textContent()).includes("fill room"), "Relevant zone headroom is missing.");
  const originalKind = await studio.getAttribute("data-run-kind");
  await studio.locator('[data-action="designer"]').click(); await studio.waitFor({ state: "detached" });
  await returnToStudio();
  check(await studio.getAttribute("data-run-kind") === originalKind, "Inspection modified the run.");
  const channel = (await studio.locator('[data-field="channel"]').textContent()).replace("Channel ", "");
  await studio.locator('[data-action="designer"]').click();
  await studio.waitFor({ state: "detached" });
  await page.keyboard.press("f");
  await page.waitForFunction(() => document.querySelector("#status-mirror")?.textContent?.includes("configuration committed."), undefined, { timeout: 120000 });
  await returnToStudio();
  await page.waitForFunction(() => document.querySelector('[data-field="order-note"]')?.textContent?.includes("nonfuel"), undefined, { timeout: 120000 });
  check(await studio.getAttribute("data-run-kind") === "modified-sandbox", "Accepted edit did not flag sandbox provenance.");
  check(await studio.locator('[data-action="refuel"]').isDisabled(), "Nonfuel channel remains executable.");
  check(await studio.locator(`[data-field="watchlist"] [data-channel="${Number(channel)}"]`).count() === 0, "Ineligible channel remains a candidate.");
  await studio.locator('[data-action="designer"]').click(); await studio.waitFor({ state: "detached" });
  await page.keyboard.press("f");
  await page.waitForFunction(() => document.querySelector("#status-mirror")?.textContent?.includes("configuration committed."), undefined, { timeout: 120000 });
  await returnToStudio();
  await page.waitForFunction(() => document.querySelector('[data-action="refuel"]')?.disabled === false, undefined, { timeout: 120000 });
  check(await studio.getAttribute("data-run-kind") === "modified-sandbox", "Restoring geometry erased the modification reason.");
  check(await studio.locator('[data-movement="outgoing"]').count() === 8, "Shared position plan is missing.");
  await studio.locator('[data-action="refuel"]').click(); await idle();
  check((await studio.locator('[data-field="movement-result"]').textContent()).includes("Confirmed move #1"), "Confirmed movement is missing.");
  check(await studio.locator('[data-field="zone-note"]').getAttribute("data-decision") !== "unavailable", "Controller decision is missing.");
  check((await studio.locator('[data-field="zone-note"]').textContent()).includes("least headroom"), "Limiting-zone explanation is missing.");
  check(await studio.locator('[data-limiting="true"]').count() === 1, "Limiting zone is not highlighted.");
  for (const width of [1600, 720]) {
    await page.setViewportSize({ width, height: 900 });
    check(await studio.evaluate(el => el.scrollWidth <= el.clientWidth), `Feedback overflows at ${width}px.`);
    await studio.locator('[data-field="movement-result"]').scrollIntoViewIfNeeded();
    if (process.env.PLAYTEST_CAPTURE_DIR) {
      await mkdir(process.env.PLAYTEST_CAPTURE_DIR, { recursive: true });
      await page.screenshot({ path: `${process.env.PLAYTEST_CAPTURE_DIR}/refuelling-feedback-${width}.png` });
    }
  }
  check(errors.length === 0, errors.join(" | "));
  console.log(JSON.stringify({ status: "ok", eligibility: "rejected and restored", movement: "confirmed", widths: [1600, 720], browserErrors: errors }));
} catch (error) {
  console.error(await page.locator("#status-mirror").textContent());
  if (process.env.PLAYTEST_CAPTURE_DIR) {
    await mkdir(process.env.PLAYTEST_CAPTURE_DIR, { recursive: true });
    await page.screenshot({ path: `${process.env.PLAYTEST_CAPTURE_DIR}/feedback-failure.png` });
  }
  throw error;
} finally { await browser.close(); }
