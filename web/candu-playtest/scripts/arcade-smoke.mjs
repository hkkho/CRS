import { chromium } from 'playwright';
const browser = await chromium.launch({ headless: true });
const page = await browser.newPage({ viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true });
const errors = [];
page.on('pageerror', error => errors.push(error.message));
try {
  await page.goto(process.argv[2] ?? process.env.PLAYTEST_URL ?? 'http://localhost:4173/CRS/');
  await page.waitForFunction(() => document.querySelector('.reactor-launcher [data-action="arcade"]')?.getAttribute('aria-disabled') === 'false', undefined, { timeout: 180000 });
  await page.locator('[data-field="seed"]').fill('1001');
  await page.locator('[data-action="apply"]').click();
  await page.waitForFunction(() => document.querySelector('.reactor-launcher')?.getAttribute('aria-busy') === 'false', undefined, { timeout: 180000 });
  await page.locator('[data-action="arcade"]').tap();
  await page.locator('.reactor-arcade').waitFor({ timeout: 180000 });
  for (const width of [320, 390, 768, 1280]) {
    await page.setViewportSize({ width, height: 844 });
    if (!await page.evaluate(() => { const view = document.querySelector('.reactor-arcade'); return document.documentElement.scrollWidth <= innerWidth && view.scrollWidth <= view.clientWidth; })) throw new Error(`Overflow at ${width}px`);
    if (width === 1280 && process.env.ARCADE_SCREENSHOT) await page.screenshot({ path: `${process.env.ARCADE_SCREENSHOT}-desktop.png` });
  }
  await page.setViewportSize({ width: 390, height: 844 });
  if (await page.locator('.arcade-map [data-channel]').count() !== 380) throw new Error('Full core map is missing');
  await page.locator('.arcade-map [data-channel="210"]').tap();
  await page.locator('[data-action="zoom"]').tap();
  await page.locator('[data-map-mode]').selectOption('burnup');
  if (!(await page.locator('[data-legend]').innerText()).includes('BURNUP')) throw new Error('Map mode did not change');
  const zoomedCell = await page.locator('.arcade-map [data-channel="210"]').boundingBox();
  if (!zoomedCell || zoomedCell.width < 44) throw new Error('Zoom targets are too small for touch');
  await page.locator('.arcade-map [data-channel="211"]').tap();
  await page.locator('[data-action="toggle-channel"]').tap();
  if (await page.locator('.arcade-map [data-channel="211"]').getAttribute('data-planned') !== 'true') throw new Error('Map plan highlight missing');
  await page.locator('[data-action="zoom"]').tap();
  if (process.env.ARCADE_SCREENSHOT) await page.screenshot({ path: `${process.env.ARCADE_SCREENSHOT}-pick.png` });
  if (await page.locator('[data-remove]').count() !== 1) throw new Error('Touch selection failed');
  await page.locator('[data-action="commit"]').tap();
  if (await page.locator('[data-picker]').isVisible()) throw new Error('Review did not hide the picker');
  if (process.env.ARCADE_SCREENSHOT) await page.screenshot({ path: `${process.env.ARCADE_SCREENSHOT}-review.png` });
  await page.locator('[data-action="confirm"]').tap();
  await page.waitForFunction(() => document.querySelector('.reactor-arcade')?.getAttribute('aria-busy') === 'false', undefined, { timeout: 900000 });
  if (!await page.locator('.arcade-result').isVisible()) throw new Error('No authoritative challenge outcome');
  console.log(await page.locator('.arcade-result').innerText());
  if (process.env.ARCADE_SCREENSHOT) await page.screenshot({ path: process.env.ARCADE_SCREENSHOT, fullPage: true });
  await page.locator('[data-action="retry"]').tap();
  await page.waitForFunction(() => document.querySelector('.reactor-arcade')?.getAttribute('aria-busy') === 'false', undefined, { timeout: 180000 });
  if (await page.locator('.arcade-result').isVisible()) throw new Error('Retry did not reset the outcome');
  if (errors.length) throw new Error(errors.join('\n'));
  console.log('Arcade Pages-path smoke passed: core-map selection, touch zoom, power/burnup, 320/390/768/1280px, real WASM day, result, retry.');
} finally { await browser.close(); }
