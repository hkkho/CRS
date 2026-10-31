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
  check(await page.title() === 'CRS' && await launcher.locator('.launcher-story h1').textContent() === 'CRS', 'Game title is not CRS.');
  const begin = launcher.locator('[data-action="begin"]');
  await page.waitForFunction(() => document.querySelector('.reactor-launcher [data-action="begin"]')?.getAttribute("aria-disabled") === "false", undefined, { timeout: 120_000 });
  check(await page.locator("#status-mirror").getAttribute("data-pacing") === "daily-turn", "Accessibility acceptance must use default daily turns.");
  check(await page.locator("canvas").count() === 0, "Unexpected canvas on the native launcher.");
  await noOverflow(launcher, "Launcher at 1280px"); await capture("launcher-1280");
  await zoomDesktop(); await noOverflow(launcher, "Launcher at 200% equivalent width"); await capture("launcher-zoom-width");
  await page.setViewportSize({ width: 320, height: 720 }); await noOverflow(launcher, "Launcher at 320px"); await capture("launcher-320");
  await page.setViewportSize({ width: 1280, height: 720 });

  await activate(launcher.locator('[data-manual-open]'));
  const launcherManual = launcher.locator('.simulator-manual');
  check(await launcherManual.isVisible(), 'Launcher manual did not open.');
  await page.keyboard.press('Escape');
  check(await launcher.locator('[data-manual-open]').evaluate(e => e === document.activeElement), 'Launcher manual did not restore focus.');

  await activate(launcher.locator(".launcher-setup details > summary"));
  const seed = launcher.locator('[data-field="seed"]'); await tabTo(seed);
  await page.keyboard.press("Control+a"); await page.keyboard.type("1001");
  check(await launcher.locator("[data-goal]").count() === 1, "Removed challenges are still selectable.");
  await activate(launcher.locator('[data-goal="free-practice"]'));
  await activate(begin);
  await page.locator("[data-game-start]").waitFor({ state: "visible", timeout: 180000 });
  await activate(page.locator("[data-game-start]"));
  const studio = page.locator(".reactor-studio"); await studio.waitFor(); await idle(".reactor-studio");
  check(await studio.locator('.studio-brand h1').textContent() === 'CRS', 'Gameplay title is not CRS.');
  check(await studio.getAttribute('data-map-mode') === 'power', 'Initial core map is not channel power.');
  check(!await studio.locator('[data-action="pause"]').isVisible(), "Daily Studio exposed real-time controls.");
  await activate(studio.locator('.studio-watchlist > summary'));
  await activate(studio.locator('[data-action="oldest"]'));
  const before = await studio.locator('[data-field="channel"]').textContent();
  await page.keyboard.press("ArrowRight");
  check(await studio.locator('.studio-watchlist > summary').evaluate(e => e === document.activeElement) && await studio.locator('[data-field="channel"]').textContent() === before, "Arrow on the fuel menu stole map focus.");
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
  await activate(studio.locator('[data-plan-review] > summary'));
  check(await studio.locator('[data-plan-list] > li').first().isVisible(), 'Plan review did not reveal outgoing fuel.');
  await activate(studio.locator('[data-plan-review] > summary'));
  await page.setViewportSize({ width: 1280, height: 1000 });
  await studio.evaluate(e => { e.scrollTop = 0; });
  const mapBounds = await studio.locator('[data-field="map"]').boundingBox();
  const coreCard = await studio.locator('.studio-core').boundingBox();
  const detailsCard = await studio.locator('.studio-inspection-dock').boundingBox();
  check(Math.abs(coreCard.y + coreCard.height - detailsCard.y - detailsCard.height) < 1, 'Desktop core and details bottoms do not align.');
  await capture('studio-zones-1280');
  await tabTo(studio.locator('[data-inspection-tab="zones"]'));
  await page.keyboard.press('End');
  check(await studio.locator('[data-inspection-tab="profiles"]').getAttribute('aria-selected') === 'true', 'Keyboard did not select Profiles in the details dock.');
  const axialBounds = await studio.locator('[data-axial="power"]').boundingBox();
  check(mapBounds.width >= 580 && axialBounds.width >= 260, 'Core or channel graphs did not expand at desktop width.');
  check(mapBounds.y < 450, 'Stacked controls pushed the core below the desktop workspace.');
  check(await studio.locator('[data-action="commit-day"]').isVisible(), 'Sheet action is not available.');
  await capture('studio-1280');
  for (const width of [1100, 1024, 900]) {
    await page.setViewportSize({ width, height: 900 });
    await noOverflow(studio, `Studio at ${width}px`);
    await noOverflow(studio.locator('.studio-inspection-dock'), `Details at ${width}px`);
    await noOverflow(studio.locator('.studio-inspection-tabs'), `Details tabs at ${width}px`);
    const core = await studio.locator('.studio-core').boundingBox();
    const sheet = await studio.locator('.studio-daily-plan').boundingBox();
    const dock = await studio.locator('.studio-inspection-dock').boundingBox();
    check(width > 1050 ? Math.abs(core.y + core.height - dock.y - dock.height) < 1 : sheet.y >= core.y + core.height && dock.y >= sheet.y + sheet.height,
      `Intermediate layout squeezed the sheet or details at ${width}px.`);
    await studio.evaluate(e => { e.scrollTop = 0; }); await capture(`studio-${width}`);
  }
  await page.setViewportSize({ width: 1280, height: 720 });
  await activate(studio.locator('.studio-map-menu > summary'));
  await activate(studio.locator('[data-map="power"]'));
  check(await studio.locator('.studio-map-menu').evaluate(e => !e.open), 'Choosing a map left its menu open.');
  check(await studio.locator('.studio-map-menu > summary').evaluate(e => e === document.activeElement), 'Map menu did not restore focus.');
  await activate(studio.locator('.studio-map-menu > summary'));
  await activate(studio.locator('[data-map="burnup"]'));
  await activate(studio.locator('.studio-game-menu > summary'));
  await activate(studio.locator('[data-manual-open]'));
  const manual = studio.locator('.simulator-manual');
  check(await manual.isVisible(), 'Studio manual did not open.');
  await page.keyboard.press('r');
  check(await studio.locator('[data-plan-list] > li').count() === 1, 'Manual shortcut changed the fuel plan.');
  check(await page.locator('#status-mirror').getAttribute('data-time-seconds') === '0', 'Manual advanced the reactor.');
  await activate(manual.locator('[data-manual-chapter="physics"]'));
  await capture('manual-1280');
  // Native modal dialog must keep keyboard focus inside the manual.
  for (let i = 0; i < 12; i++) {
    await page.keyboard.press('Tab');
    check(await manual.evaluate(e => e.contains(document.activeElement)), 'Manual leaked keyboard focus to game controls.');
  }
  await page.setViewportSize({ width: 320, height: 720 });
  await noOverflow(manual, 'Manual at 320px'); await capture('manual-320');
  await page.keyboard.press('Escape');
  check(await studio.locator('[data-manual-open]').evaluate(e => e === document.activeElement), 'Studio manual did not restore focus.');
  await page.keyboard.press('Escape');
  check(await studio.locator('.studio-game-menu > summary').evaluate(e => e === document.activeElement), 'Game menu did not restore focus.');
  await page.setViewportSize({ width: 1280, height: 720 });
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
  await activate(studio.locator('.studio-view-menu > summary'));
  await tabTo(studio.locator('[data-tab="reactor"]'));
  await page.keyboard.press('ArrowRight'); await page.keyboard.press('Enter');
  check(await studio.locator('.studio-view-menu').evaluate(e => !e.open), 'Choosing a graph left the view menu open.');
  check((await studio.locator('.studio-trend-plot svg').first().boundingBox()).width > 1100, 'History plot did not use the available desktop width.');
  await studio.evaluate(e => { e.scrollTop = 0; }); await capture('power-1280');
  await page.setViewportSize({ width: 320, height: 720 }); await noOverflow(studio, 'Graphs at 320px'); await capture('power-320');
  await page.setViewportSize({ width: 1280, height: 720 });
  check(await page.evaluate(() => globalThis.accessibilityMessages.filter(message => message.startsWith("Accepted:")).length) === 1, "Completed day was not announced exactly once.");
  const quietMessage = await page.locator("#session-announcements").textContent();
  await page.waitForTimeout(300);
  check(await page.locator("#session-announcements").textContent() === quietMessage, "Frozen planning changed the live announcement.");
  check(errors.length === 0, errors.join(" | "));
  const result = { status: "passed", pacing: "daily-turn", keyboardOnly: "launcher/menus/manual/seed/objective/map/plan/day/result/graphs", desktopSizes: { core: mapBounds.width, channelGraph: axialBounds.width }, layouts: [1280, 1100, 1024, 900, 640, 320], zoomEmulation: "200%: 640x360 CSS pixels at DPR 2", reducedMotion: "respected", manual: "focus trapped, chapters reachable, draft preserved", focus: "menus, dialog and return preserved", duplicateAnnouncements: 0, browserErrors: errors };
  if (captures) await writeFile(`${captures}/accessibility-acceptance.json`, JSON.stringify(result, null, 2));
  console.log(JSON.stringify(result));
} catch (error) { await capture("accessibility-failure"); throw error; }
finally { await browser.close(); }
