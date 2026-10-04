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
  await page.goto(process.argv[2] ?? "http://127.0.0.1:4173");
  const launcher = page.locator(".reactor-launcher");
  const begin = launcher.locator('[data-action="begin"]');
  await page.waitForFunction(() => document.querySelector('.reactor-launcher [data-action="begin"]')?.getAttribute("aria-disabled") === "false", undefined, { timeout: 60000 });
  check(await page.locator("canvas").count() === 0, "Designer canvas was eagerly loaded on the launcher.");
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
  const designer = studio.locator('[data-action="designer"]'); await activate(designer);
  const controls = page.locator(".designer-controls"); await controls.waitFor({ timeout: 60000 });
  check(await page.locator("canvas").getAttribute("aria-hidden") === "true", "Designer canvas is exposed as duplicate controls.");
  const channel = controls.locator('[data-field="channel"]'); await tabTo(channel);
  const oldChannel = await channel.inputValue(); await page.keyboard.press("ArrowDown");
  check(await channel.inputValue() !== oldChannel, "Designer channel selection failed.");
  const selectedChannel = await channel.inputValue();
  const position = controls.locator('[data-field="position"]'); await tabTo(position); await page.keyboard.press("End");
  check(await position.inputValue() === "11" && await channel.inputValue() === selectedChannel, "Position arrows escaped the native selector.");
  check((await controls.locator('[data-field="reading"]').textContent()).includes("Position 12"), "Selected cell has no readable measurements.");
  await noOverflow(controls, "Designer at 1280px"); await capture("designer-1280");
  await zoomDesktop(); await noOverflow(controls, "Designer at 200% equivalent width"); await capture("designer-zoom-width");
  await page.setViewportSize({ width: 320, height: 720 }); await noOverflow(controls, "Designer at 320px"); await capture("designer-320");
  await page.setViewportSize({ width: 1280, height: 720 });

  await page.evaluate(() => {
    globalThis.accessibilityMessages = [];
    new MutationObserver(() => globalThis.accessibilityMessages.push(document.querySelector("#session-announcements").textContent))
      .observe(document.querySelector("#session-announcements"), { childList: true });
  });
  const fuel = controls.locator('[data-action="fuel"]');
  await activate(fuel); await idle(".designer-controls");
  check((await fuel.textContent()).includes("Set fuel"), "Keyboard fuel edit was not committed.");
  check(await fuel.evaluate(element => element === document.activeElement), "Solving lost inspector focus.");
  check(await page.evaluate(() => globalThis.accessibilityMessages.filter(message => message.startsWith("Accepted:")).length) === 1, "Fuel edit was announced more than once.");
  await activate(fuel); await idle(".designer-controls");
  const face = controls.locator('[data-face="north"]');
  await activate(face); await idle(".designer-controls"); check(await face.getAttribute("aria-pressed") === "true", "Reflective edit failed.");
  await activate(face); await idle(".designer-controls");
  await activate(controls.locator('[data-action="solve"]')); await idle(".designer-controls");

  await activate(controls.locator('[data-action="zones"]'));
  const dialog = page.locator("dialog.zone-layout-editor"); await dialog.waitFor();
  check(await dialog.locator("#session-announcements").count() === 1, "Live announcements are outside the modal accessible tree.");
  check(await dialog.locator('rect[tabindex="0"]').count() === 1, "Zone map forces hundreds of Tab stops.");
  const zoneChannel = dialog.locator('[data-field="channel"]'); await tabTo(zoneChannel); await page.keyboard.press("ArrowDown");
  await activate(dialog.locator('[data-action="paint"]'));
  check((await dialog.locator('[data-field="message"]').textContent()).includes("Draft changed"), "Keyboard zone draft editing failed.");
  await activate(dialog.locator('[data-action="undo"]'));
  await activate(dialog.locator('[data-action="apply"]'));
  await page.waitForFunction(() => document.querySelector('.zone-layout-editor [data-field="message"]')?.textContent?.startsWith("Applied:"), undefined, { timeout: 120000 });
  await page.setViewportSize({ width: 320, height: 720 }); await noOverflow(dialog, "Zone editor at 320px"); await capture("zones-320");
  await page.setViewportSize({ width: 1280, height: 720 });
  await page.keyboard.press("Escape"); await dialog.waitFor({ state: "detached" });
  check(await controls.locator('[data-action="zones"]').evaluate(element => element === document.activeElement), "Closing geometry did not restore focus.");
  await activate(controls.locator('[data-action="back"]')); await studio.waitFor();
  check(await studio.getAttribute("data-run-kind") === "modified-sandbox", "Designer edits lost run provenance.");
  await activate(studio.locator('[data-action="oldest"]'));
  await activate(studio.locator('[data-action="refuel"]')); await idle(".reactor-studio");
  check((await studio.locator('[data-field="movement-result"]').textContent()).includes("Confirmed move #1"), "Keyboard refuelling failed.");

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
  console.log(JSON.stringify({ keyboardOnly: "launcher/seed/objective/inspection/fuel/faces/solve/zones/refuel", layouts: [1280, 640, 320], zoomEmulation: "200%: 640x360 CSS pixels at DPR 2", focus: "preserved", duplicateAnnouncements: 0, telemetry: "quiet", browserErrors: errors.length }));
} catch (error) { await capture("accessibility-failure"); throw error; }
finally { await browser.close(); }
