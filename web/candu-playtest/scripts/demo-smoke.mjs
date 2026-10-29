import { chromium } from 'playwright';
import { mkdir } from 'node:fs/promises';

const browser = await chromium.launch({ headless: true });
const page = await browser.newPage({ viewport: { width: 1440, height: 1000 }, reducedMotion: 'reduce' });
const errors = [];
page.on('pageerror', error => errors.push(error.message));
page.on('console', message => { if (message.type() === 'error') errors.push(message.text()); });
const waitOptions = { timeout: 180000 };
try {
  await page.goto(process.argv[2] ?? 'http://127.0.0.1:4175/CRS/');
  await page.waitForFunction(() => document.querySelector('[data-action=demo]')?.getAttribute('aria-disabled') === 'false' || document.querySelector('.bridge-recovery:not([hidden])'), undefined, waitOptions);
  if (errors.length) throw new Error(errors.join('\n'));
  console.log('Launcher ready.');
  await page.locator('.launcher-setup details > summary').click();
  await page.locator('[data-field=seed]').fill('1001');
  await page.locator('[data-action=demo]').click();
  await page.locator('.reactor-demo .arcade-map [data-channel="210"]').waitFor(waitOptions);
  console.log('Demo core ready.');
  const initialDay = Number(await page.locator('.reactor-demo').getAttribute('data-days'));
  const start = Date.now();
  await page.locator('.reactor-demo .arcade-map [data-channel="210"]').dblclick();
  // Selection remains interactive while a shared solver operation is in flight.
  await page.locator('.reactor-demo .arcade-map [data-channel="211"]').click();
  if (await page.locator('.reactor-demo [data-channel="211"]').getAttribute('aria-pressed') !== 'true') throw new Error('Map blocked during fuel solve');
  if (await page.locator('.reactor-demo [data-selected-channel]').innerText() === '—') throw new Error('Selected channel label is missing');
  if (await page.locator('dialog[open]').count()) throw new Error('Demo opened a calculation screen');
  await page.waitForFunction(() => Number(document.querySelector('.reactor-demo')?.getAttribute('data-fuel-consumed')) === 8, undefined, waitOptions);
  const fuelMs = Date.now() - start;
  console.log(`Fuel response: ${fuelMs}ms.`);
  await page.waitForFunction(day => Number(document.querySelector('.reactor-demo')?.getAttribute('data-days')) > day, initialDay, waitOptions);
  const dayMs = Date.now() - start;
  await page.waitForFunction(day => Number(document.querySelector('.reactor-demo')?.getAttribute('data-days')) > day + 1, initialDay, waitOptions);
  const dayIntervalMs = Date.now() - start - dayMs;
  await page.locator('.reactor-demo [data-action=pause]').click();
  await page.waitForFunction(() => document.querySelector('.reactor-demo')?.getAttribute('data-solving') === 'false', undefined, waitOptions);
  const pausedDay = await page.locator('.reactor-demo').getAttribute('data-days');
  await page.waitForTimeout(5500);
  if (await page.locator('.reactor-demo').getAttribute('data-days') !== pausedDay) throw new Error('Paused demo advanced');
  await mkdir('artifacts/demo', { recursive: true });
  await page.screenshot({ path: 'artifacts/demo/desktop.png', fullPage: true });
  for (const width of [320, 390, 768]) {
    await page.setViewportSize({ width, height: 844 });
    if (!await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth && document.querySelector('.reactor-demo').scrollWidth <= innerWidth)) throw new Error(`Overflow at ${width}px`);
  }
  await page.setViewportSize({ width: 390, height: 844 });
  await page.locator('.reactor-demo').evaluate(element => { element.scrollTop = 0; });
  await page.screenshot({ path: 'artifacts/demo/mobile.png', fullPage: true });
  await page.locator('.demo-readout').scrollIntoViewIfNeeded();
  await page.screenshot({ path: 'artifacts/demo/mobile-controls.png', fullPage: true });
  await page.locator('.reactor-demo [data-action=retry]').click();
  await page.waitForFunction(() => document.querySelector('.reactor-demo')?.getAttribute('data-days') === '0' && document.querySelector('.reactor-demo')?.getAttribute('data-running') === 'true', undefined, waitOptions);
  if (await page.locator('.reactor-demo').getAttribute('data-fuel-consumed') !== '0') throw new Error('Retry retained fuel consumption');
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.locator('.reactor-demo [data-channel="210"]').focus();
  await page.keyboard.press('Enter');
  await page.waitForFunction(() => document.querySelector('.reactor-demo')?.getAttribute('data-fuel-consumed') === '8', undefined, waitOptions);
  await page.locator('.reactor-demo [data-action=leave]').click();
  if (!await page.locator('.reactor-launcher').isVisible()) throw new Error('Return to launcher failed');
  if (await page.locator('#status-mirror').getAttribute('data-completed-days') !== '0') throw new Error('Demo changed the daily run');
  if (errors.length) throw new Error(errors.join('\n'));
  console.log(JSON.stringify({ result: 'Demo Pages-path smoke passed', fuelMs, firstDayMs: dayMs, dayIntervalMs, viewports: [1440, 768, 390, 320], keyboard: true, pause: true, retry: true, preservedDailyRun: true }));
} catch (error) {
  await mkdir('artifacts/demo', { recursive: true });
  await page.screenshot({ path: 'artifacts/demo/failure.png', fullPage: true });
  console.error(await page.locator('body').innerText(), errors);
  throw error;
} finally { await browser.close(); }
