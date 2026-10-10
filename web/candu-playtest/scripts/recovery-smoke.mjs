import { mkdir } from "node:fs/promises";
import { join } from "node:path";

// Exercise the actual recovery action without adding fault controls to the app.
export async function verifyRecovery(browser, url) {
  const context = await browser.newContext();
  const page = await context.newPage();
  try {
    await context.route("**/wasm/main.mjs", (route) => route.abort());
    await page.goto(url);
    const recovery = page.locator(".bridge-recovery");
    await recovery.waitFor({ state: "visible", timeout: 30_000 });
    if (!(await recovery.textContent()).includes("Current progress will be lost")) {
      throw new Error("Recovery must explain progress loss before reload.");
    }
    if (process.env.PLAYTEST_CAPTURE_DIR) {
      await mkdir(process.env.PLAYTEST_CAPTURE_DIR, { recursive: true });
      await page.screenshot({ path: join(process.env.PLAYTEST_CAPTURE_DIR, "recovery.png") });
    }
    await context.unroute("**/wasm/main.mjs");
    await Promise.all([
      page.waitForEvent("domcontentloaded"),
      recovery.getByRole("button", { name: "Reload and start a new shift" }).click(),
    ]);
    await page.waitForFunction(() => document.querySelector("#status-mirror")?.textContent?.includes("live reactor online"), undefined, { timeout: 120_000 });
    if (await recovery.isVisible()) throw new Error("Recovery remained visible after a successful reload.");
    const pacing = await page.locator("#status-mirror").getAttribute("data-pacing");
    const expectedPacing = new URL(url).searchParams.get("pacing") === "real-time" ? "real-time" : "daily-turn";
    if (pacing !== expectedPacing) throw new Error("Reload changed the selected pacing mode.");
    return { status: "passed", action: "reload after unavailable WASM", pacing };
  } finally {
    await context.close();
  }
}
