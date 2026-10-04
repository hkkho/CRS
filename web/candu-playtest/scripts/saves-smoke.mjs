import { chromium } from 'playwright';
import { mkdir } from 'node:fs/promises';

const url = process.argv[2] ?? process.env.PLAYTEST_URL;
if (!url) throw new Error('Pass the production preview or Pages URL.');
const browser = await chromium.launch({ headless: true });
const context = await browser.newContext({ viewport: { width: 1280, height: 900 } });
const page = await context.newPage();
const errors = [];
page.on('pageerror', error => errors.push(error.message));
const check = (condition, message) => { if (!condition) throw new Error(message); };
const idle = () => page.waitForFunction(() => document.querySelector('.reactor-studio')?.getAttribute('aria-busy') === 'false', undefined, { timeout: 120_000 });
const panelIdle = () => page.waitForFunction(() => document.querySelector('.player-panel')?.getAttribute('aria-busy') === 'false', undefined, { timeout: 180_000 });
const localRuns = () => page.evaluate(() => new Promise((resolve, reject) => {
  const request = indexedDB.open('candu-player-v1', 1);
  request.onsuccess = () => {
    const get = request.result.transaction('runs').objectStore('runs').getAll();
    get.onsuccess = () => { resolve(get.result); request.result.close(); };
    get.onerror = () => reject(get.error);
  };
  request.onerror = () => reject(request.error);
}));
try {
  await page.goto(url);
  await page.waitForFunction(() => document.querySelector('#status-mirror')?.textContent?.includes('live reactor online'), undefined, { timeout: 120_000 });
  await page.locator('.reactor-launcher [data-field="seed"]').fill('2024');
  await page.getByRole('button', { name: 'Use seed & objective', exact: true }).click();
  await page.waitForFunction(() => document.querySelector('.reactor-launcher')?.getAttribute('aria-busy') === 'false' &&
    document.querySelector('.reactor-launcher [data-field="objective"]')?.textContent?.includes('2024'), undefined, { timeout: 120_000 });
  await page.locator('.player-toggle').click(); await panelIdle();
  check(await page.getByRole('button', { name: 'Continue without login', exact: true }).isVisible(), 'Guest option is missing.');
  await page.getByRole('button', { name: 'Continue without login', exact: true }).click();
  await page.getByRole('button', { name: 'Begin shift', exact: true }).click(); await idle();
  const studio = page.locator('.reactor-studio');
  if (!await studio.locator('details.studio-controls').evaluate(e => e.open)) await studio.locator('details.studio-controls > summary').click();
  await studio.locator('[data-action="step"]').click(); await idle();
  await studio.locator('[data-action="oldest"]').click();
  await studio.locator('[data-action="refuel"]').click(); await idle();
  const time = await studio.locator('[data-field="time"]').textContent();
  await page.locator('.player-toggle').click(); await panelIdle();
  await page.getByRole('button', { name: 'Save locally', exact: true }).click(); await panelIdle();
  check((await page.locator('[data-message]').textContent()).includes('Saved locally'), 'Local save failed.');
  const before = (await localRuns()).find(row => row.owner === 'guest').run;
  check(!before.ended && before.seed === 2024 && before.seconds > 0 && before.fuelConsumed === 8, 'Saved run lost seed, progress or fuel use.');
  await page.waitForFunction(() => !!navigator.serviceWorker.controller, undefined, { timeout: 120_000 });
  await context.setOffline(true);
  await page.reload();
  await page.waitForFunction(() => document.querySelector('#status-mirror')?.textContent?.includes('live reactor online'), undefined, { timeout: 120_000 });
  await page.locator('.player-toggle').click(); await panelIdle();
  await page.locator(`[data-run-id="${before.id}"][data-source="This browser"]`).getByRole('button', { name: 'Continue run' }).click();
  await page.waitForFunction(() => !document.querySelector('.player-panel')?.open, undefined, { timeout: 180_000 });
  await idle();
  check(await studio.locator('[data-field="time"]').textContent() === time, 'Restored simulation time changed.');
  check(await studio.locator('[data-field="pause"]').getAttribute('aria-label') === 'Resume simulation', 'Restored run was not paused.');
  const after = (await localRuns()).find(row => row.run.id === before.id).run;
  check(after.score === before.score && after.fuelConsumed === before.fuelConsumed, 'Restored stats changed.');
  await page.locator('.player-toggle').click(); await panelIdle();
  await page.setViewportSize({ width: 320, height: 740 });
  check(await page.locator('.player-panel').evaluate(e => e.scrollWidth <= e.clientWidth), 'Save panel overflows at 320px.');
  await mkdir('artifacts', { recursive: true });
  await page.screenshot({ path: 'artifacts/player-offline-320.png' });
  check(errors.length === 0, `Browser errors: ${errors.join(' | ')}`);
  console.log(JSON.stringify({ guest: 'passed', localSave: 'passed', offlineReload: 'passed', wasmReplayRestore: 'passed', paused: true, score: after.score, seconds: after.seconds, fuelConsumed: after.fuelConsumed, narrowLayout: 'passed' }, null, 2));
} finally { await browser.close(); }
