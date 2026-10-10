import { chromium } from "playwright";
import { mkdir, writeFile } from "node:fs/promises";

const browser = await chromium.launch({ headless: true });
const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
await page.emulateMedia({ reducedMotion: "reduce" });
const cdp = await page.context().newCDPSession(page);
const errors = [];
page.on("pageerror", error => errors.push(error.message));
page.on("console", message => { if (message.type() === "error") errors.push(message.text()); });
const check = (ok, message) => { if (!ok) throw new Error(message); };
const captures = process.env.PLAYTEST_CAPTURE_DIR;
async function capture(name) { if (captures) { await mkdir(captures, { recursive: true }); await page.screenshot({ path: `${captures}/${name}.png` }); } }
// Every game interaction uses keyboard input. Locators inspect the resulting state.
async function tabTo(locator) {
  for (let i = 0; i < 90; i++) {
    if (await locator.evaluate(element => element === document.activeElement)) return;
    await page.keyboard.press("Tab");
  }
  throw new Error("Keyboard could not reach: " + await locator.getAttribute("data-action"));
}
async function activate(locator) { await tabTo(locator); await page.keyboard.press("Enter"); }
async function idle(selector) { await page.waitForFunction(selector => document.querySelector(selector)?.getAttribute("aria-busy") === "false", selector, { timeout: 900_000 }); }
async function noOverflow(locator, label) { check(await locator.evaluate(element => element.scrollWidth <= element.clientWidth + 1), `${label} overflows horizontally.`); }
async function zoomDesktop() {
  await cdp.send("Emulation.clearDeviceMetricsOverride");
  await page.setViewportSize({ width: 640, height: 360 });
  await cdp.send("Emulation.setDeviceMetricsOverride", { width: 640, height: 360, deviceScaleFactor: 2, mobile: false, screenWidth: 1280, screenHeight: 720 });
  check(await page.evaluate(() => innerWidth === 640 && devicePixelRatio === 2), "200% zoom metrics were not applied.");
}
async function selectChannel(studio, index) {
  const target = studio.locator(`rect[data-channel="${index}"]`);
  const location = await target.evaluate(e => ({ x: Number(e.getAttribute("x")), y: Number(e.getAttribute("y")) }));
  await tabTo(studio.locator('rect[data-channel][tabindex="0"]'));
  for (let i = 0; i < 50; i++) {
    const selected = await studio.locator('rect[data-channel][tabindex="0"]').evaluate(e => ({ index: Number(e.getAttribute("data-channel")), x: Number(e.getAttribute("x")), y: Number(e.getAttribute("y")) }));
    if (selected.index === index) return;
    await page.keyboard.press(selected.y < location.y ? "ArrowDown" : selected.y > location.y ? "ArrowUp" : selected.x < location.x ? "ArrowRight" : "ArrowLeft");
  }
  throw new Error(`Keyboard could not select channel ${index}.`);
}
try {
  await page.goto(process.argv[2] ?? process.env.PLAYTEST_URL ?? "http://127.0.0.1:4173");
  const launcher = page.locator(".reactor-launcher");
  const begin = launcher.locator('[data-action="begin"]');
  await page.waitForFunction(() => document.querySelector('.reactor-launcher [data-action="begin"]')?.getAttribute("aria-disabled") === "false", undefined, { timeout: 120_000 });
  check(await page.locator("#status-mirror").getAttribute("data-pacing") === "daily-turn", "Accessibility acceptance must use default daily turns.");
  check(await page.locator("canvas").count() === 0, "Unexpected canvas on the native launcher.");
  await noOverflow(launcher, "Launcher at 1280px"); await capture("launcher-1280");
  await zoomDesktop(); await noOverflow(launcher, "Launcher at 200% equivalent width"); await capture("launcher-zoom-width");
  await page.setViewportSize({ width: 320, height: 720 }); await noOverflow(launcher, "Launcher at 320px"); await capture("launcher-320");
  await page.setViewportSize({ width: 1280, height: 720 });

  const seed = launcher.locator('[data-field="seed"]'); await tabTo(seed);
  await page.keyboard.press("Control+a"); await page.keyboard.type("1001");
  const shift = launcher.locator('[data-field="shift"]'); await tabTo(shift);
  await page.keyboard.press("ArrowDown");
  check(await shift.inputValue() === "useful-fuel-day-v1", "Keyboard objective selection failed.");
  await page.keyboard.press("ArrowUp");
  check(await shift.inputValue() === "free-practice", "Keyboard objective selection did not return to practice.");
  await activate(launcher.locator('[data-action="apply"]')); await idle(".reactor-launcher");
  check((await launcher.locator('[data-field="objective"]').textContent()).includes("seed 1001"), "Seed choice was not applied.");
  await activate(begin);
  const studio = page.locator(".reactor-studio"); await studio.waitFor(); await idle(".reactor-studio");
  check(!await studio.locator('[data-action="pause"]').isVisible(), "Daily Studio exposed real-time controls.");
  await activate(studio.locator('[data-action="oldest"]'));
  const before = await studio.locator('[data-field="channel"]').textContent();
  await page.keyboard.press("ArrowRight");
  check(await studio.locator('[data-action="oldest"]').evaluate(e => e === document.activeElement) && await studio.locator('[data-field="channel"]').textContent() === before, "Arrow on the watchlist button stole map focus.");
  await selectChannel(studio, 210);
  await page.evaluate(() => {
    globalThis.accessibilityMessages = [];
    new MutationObserver(() => globalThis.accessibilityMessages.push(document.querySelector("#session-announcements").textContent))
      .observe(document.querySelector("#session-announcements"), { childList: true });
  });
  await activate(studio.locator('[data-action="refuel"]'));
  check(await studio.locator('[data-plan-list] > li').count() === 1, "Keyboard did not add the channel to today's plan.");
  check(await page.locator("#status-mirror").getAttribute("data-fuel-consumed") === "0", "Planning consumed fuel.");
  check(await page.evaluate(() => globalThis.accessibilityMessages.filter(message => message.startsWith("Accepted:")).length) === 0, "Draft planning was announced as an executed order.");
  await zoomDesktop(); await noOverflow(studio, "Studio at 200% equivalent width"); await capture("studio-zoom-width");
  await page.setViewportSize({ width: 320, height: 720 }); await noOverflow(studio, "Studio at 320px"); await capture("studio-320");
  await page.setViewportSize({ width: 1280, height: 720 });

  const commit = studio.locator('[data-action="commit-day"]'); await activate(commit);
  const outcome = studio.locator(".studio-day-dialog"); await outcome.waitFor({ state: "visible" });
  check(await outcome.evaluate(e => e.contains(document.activeElement)), "Calculation dialog did not receive keyboard focus.");
  check(await outcome.evaluate(e => getComputedStyle(e).animationName === "none"), "Reduced motion did not disable the result animation.");
  await idle(".reactor-studio");
  check(await outcome.getAttribute("data-phase") === "success", "Keyboard day did not finish with success.");
  check((await outcome.locator('[data-calculation-score]').textContent()).includes("Total score"), "Success score is missing.");
  await capture("daily-keyboard-success");
  await activate(outcome.getByRole("button", { name: "Continue", exact: true }));
  check(await commit.evaluate(e => e === document.activeElement), "Closing the result did not restore day-button focus.");
  check(await page.locator("#status-mirror").getAttribute("data-time-seconds") === "86400", "Keyboard order did not advance one day.");
  check(await page.locator("#status-mirror").getAttribute("data-fuel-consumed") === "8", "Keyboard day used the wrong fuel count.");
  check(await studio.locator('[data-plan-list] > li').count() === 0, "Completed day did not clear the plan.");
  check(await page.evaluate(() => globalThis.accessibilityMessages.filter(message => message.startsWith("Accepted:")).length) === 1, "Completed day was not announced exactly once.");
  const quietMessage = await page.locator("#session-announcements").textContent();
  await page.waitForTimeout(300);
  check(await page.locator("#session-announcements").textContent() === quietMessage, "Frozen planning changed the live announcement.");
  check(errors.length === 0, errors.join(" | "));
  const result = { status: "passed", pacing: "daily-turn", keyboardOnly: "launcher/seed/objective/map/plan/day/result", layouts: [1280, 640, 320], zoomEmulation: "200%: 640x360 CSS pixels at DPR 2", reducedMotion: "respected", focus: "dialog and return preserved", duplicateAnnouncements: 0, browserErrors: errors };
  if (captures) await writeFile(`${captures}/accessibility-acceptance.json`, JSON.stringify(result, null, 2));
  console.log(JSON.stringify(result));
} catch (error) { await capture("accessibility-failure"); throw error; }
finally { await browser.close(); }
