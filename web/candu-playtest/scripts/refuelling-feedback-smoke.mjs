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
  check(await studio.locator('[data-action="designer"]').count() === 0, 'Removed designer is still offered.');
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
  console.log(JSON.stringify({ status: "ok", designer: "removed", movement: "confirmed", widths: [1600, 720], browserErrors: errors }));
} catch (error) {
  console.error(await page.locator("#status-mirror").textContent());
  if (process.env.PLAYTEST_CAPTURE_DIR) {
    await mkdir(process.env.PLAYTEST_CAPTURE_DIR, { recursive: true });
    await page.screenshot({ path: `${process.env.PLAYTEST_CAPTURE_DIR}/feedback-failure.png` });
  }
  throw error;
} finally { await browser.close(); }
