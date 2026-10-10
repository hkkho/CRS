import { chromium } from "playwright";
import { mkdir } from "node:fs/promises";

const browser = await chromium.launch({ headless: true });
const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
const cdp = await page.context().newCDPSession(page);
const errors = [];
page.on("pageerror", error => errors.push(error.message));
page.on("console", message => { if (message.type() === "error") errors.push(message.text()); });
const check = (ok, message) => { if (!ok) throw new Error(message); };
const captures = process.env.PLAYTEST_CAPTURE_DIR;
async function capture(name) { if (captures) { await mkdir(captures, { recursive: true }); await page.screenshot({ path: `${captures}/${name}.png` }); } }
// Every interaction uses keyboard input. Locators only inspect the resulting state.
async function tabTo(locator) {
  for (let i = 0; i < 90; i++) {
    if (await locator.evaluate(element => element === document.activeElement)) return;
    await page.keyboard.press("Tab");
  }
  throw new Error("Keyboard could not reach: " + await locator.getAttribute("data-action"));
}
async function activate(locator) { await tabTo(locator); await page.keyboard.press("Enter"); }
async function idle(selector) { await page.waitForFunction(selector => document.querySelector(selector)?.getAttribute("aria-busy") === "false", selector, { timeout: 120000 }); }
async function noOverflow(locator, label) { check(await locator.evaluate(element => element.scrollWidth <= element.clientWidth + 1), `${label} overflows horizontally.`); }
async function zoomDesktop() {
  // Reset this CDP session before Playwright applies another viewport; otherwise
  // Chromium can treat a repeated override as unchanged in this session.
  await cdp.send("Emulation.clearDeviceMetricsOverride");
  await page.setViewportSize({ width: 640, height: 360 });
  await cdp.send("Emulation.setDeviceMetricsOverride", { width: 640, height: 360, deviceScaleFactor: 2, mobile: false, screenWidth: 1280, screenHeight: 720 });
  check(await page.evaluate(() => innerWidth === 640 && devicePixelRatio === 2), "200% zoom metrics were not applied.");
}
try {
  const legacyUrl = new URL(process.argv[2] ?? "http://127.0.0.1:4173"); legacyUrl.searchParams.set("pacing", "real-time");
  await page.goto(legacyUrl.toString());
  const launcher = page.locator(".reactor-launcher");
  const begin = launcher.locator('[data-action="begin"]');
  await page.waitForFunction(() => document.querySelector('.reactor-launcher [data-action="begin"]')?.getAttribute("aria-disabled") === "false", undefined, { timeout: 60000 });
  check(await page.locator("canvas").count() === 0, "Unexpected canvas on the native launcher.");
  await noOverflow(launcher, "Launcher at 1280px"); await capture("launcher-1280");

  // A 640px viewport models the available CSS width of 200% desktop zoom.
  await zoomDesktop();
  await noOverflow(launcher, "Launcher at 200% equivalent width"); await capture("launcher-zoom-width");
  await page.setViewportSize({ width: 320, height: 720 });
  await noOverflow(launcher, "Launcher at 320px"); await capture("launcher-320");
  await page.setViewportSize({ width: 1280, height: 720 });

  const seed = launcher.locator('[data-field="seed"]'); await tabTo(seed);
  await page.keyboard.press("Control+a"); await page.keyboard.type("42");
  const shift = launcher.locator('[data-field="shift"]'); await tabTo(shift);
  await page.keyboard.press("ArrowDown");
  check(await shift.inputValue() === "useful-fuel-day-v1", "Keyboard objective selection failed.");
  await activate(launcher.locator('[data-action="apply"]')); await idle(".reactor-launcher");
  check((await launcher.locator('[data-field="objective"]').textContent()).includes("seed 42"), "Seed choice was not applied.");
  await activate(begin);
  const studio = page.locator(".reactor-studio"); await studio.waitFor();
  check(await studio.locator('[data-action="designer"]').count() === 0, 'Removed designer is still offered.');
  await activate(studio.locator('[data-action="oldest"]'));
  await page.evaluate(() => {
    globalThis.accessibilityMessages = [];
    new MutationObserver(() => globalThis.accessibilityMessages.push(document.querySelector("#session-announcements").textContent))
      .observe(document.querySelector("#session-announcements"), { childList: true });
  });
  await activate(studio.locator('[data-action="refuel"]')); await idle(".reactor-studio");
  check((await studio.locator('[data-field="movement-result"]').textContent()).includes("Confirmed move #1"), "Keyboard refuelling failed.");

  check(await page.evaluate(() => globalThis.accessibilityMessages.filter(message => message.startsWith("Accepted:")).length) === 1, "Refuelling was announced more than once.");

  // Unrelated buttons keep their arrow behavior/focus; only channel controls move the map.
  const direction = studio.locator('[data-action="oldest"]'); await tabTo(direction);
  const before = await studio.locator('[data-field="channel"]').textContent(); await page.keyboard.press("ArrowRight");
  check(await direction.evaluate(element => element === document.activeElement) && await studio.locator('[data-field="channel"]').textContent() === before, "Arrow on watchlist button stole map focus.");
  await zoomDesktop(); await noOverflow(studio, "Studio at 200% equivalent width"); await capture("studio-zoom-width");
  await page.setViewportSize({ width: 320, height: 720 }); await noOverflow(studio, "Studio at 320px"); await capture("studio-320");
  await page.setViewportSize({ width: 1280, height: 720 });

  const playback = studio.locator('[data-action="pause"]'); await activate(playback); await idle(".reactor-studio");
  const clock = await studio.locator('[data-field="time"]').textContent();
  const quietMessage = await page.locator("#session-announcements").textContent();
  await page.waitForFunction(clock => document.querySelector('.reactor-studio [data-field="time"]')?.textContent !== clock, clock, { timeout: 60000 });
  await page.waitForFunction(() => /Observed [\d.]+ sim min\/s/.test(document.querySelector('[data-field="pace"]')?.textContent ?? ''), undefined, { timeout: 60000 });
  check(await page.locator("#session-announcements").textContent() === quietMessage, "Routine telemetry changed the live announcement.");
  await activate(playback); await idle(".reactor-studio");
  check((await studio.locator('[data-field="pace"]').textContent()).includes('Requested Paused · Observed —'), 'Pause retained stale achieved pace.');
  check(errors.length === 0, errors.join("\n"));
  console.log(JSON.stringify({ keyboardOnly: "launcher/seed/objective/inspection/refuel", layouts: [1280, 640, 320], zoomEmulation: "200%: 640x360 CSS pixels at DPR 2", focus: "preserved", duplicateAnnouncements: 0, telemetry: "quiet", browserErrors: errors.length }));
} catch (error) { await capture("accessibility-failure"); throw error; }
finally { await browser.close(); }
