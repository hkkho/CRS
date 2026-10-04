import { chromium } from 'playwright';
import { mkdir } from 'node:fs/promises';
const browser = await chromium.launch({ headless: true });
try {
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  const errors = []; page.on('pageerror', error => errors.push(error.message));
  await page.goto(process.argv[2] ?? 'http://127.0.0.1:4173');
  await page.waitForFunction(() => document.querySelector('[data-action="begin"]')?.getAttribute('aria-disabled') === 'false');
  await page.locator('[data-action="begin"]').click();
  const studio = page.locator('.reactor-studio');
  await studio.locator('[data-speed="60x"]').click();
  await page.waitForFunction(() => /Observed [\d.]+ sim min\/s/.test(document.querySelector('[data-field="pace"]')?.textContent ?? ''), undefined, { timeout: 60000 });
  if ((await studio.locator('[data-field="pace"]').textContent()).includes('Solving')) throw new Error('Clock calculation text is flashing.');
  if (!await studio.locator('[data-action="pause"]').isEnabled()) throw new Error('Clock solving blocked foreground pause.');
  const quiet = await page.locator('#session-announcements').textContent();
  await page.waitForTimeout(1000);
  if (quiet !== await page.locator('#session-announcements').textContent()) throw new Error('Pace telemetry announced repeatedly.');
  await studio.locator('[data-action="pause"]').click();
  await page.waitForFunction(() => document.querySelector('[data-field="pace"]')?.textContent?.includes('Requested Paused · Observed —'));
  for (const width of [320, 640, 720, 1280, 1600]) {
    await page.setViewportSize({ width, height: 720 });
    if (!await studio.locator('[data-field="pace"]').evaluate(element => {
      const label = element.getBoundingClientRect(), card = element.closest('article').getBoundingClientRect();
      return label.left >= card.left && label.right <= card.right && label.bottom <= card.bottom;
    })) throw new Error(`Pace label escapes its clock card at ${width}px.`);
    if (!await studio.evaluate(element => element.scrollWidth <= element.clientWidth)) throw new Error(`Studio overflow at ${width}px.`);
    const captures = process.env.PLAYTEST_CAPTURE_DIR;
    if (captures) { await mkdir(captures, { recursive: true }); await page.screenshot({ path: `${captures}/pace-${width}.png` }); }
  }
  if (errors.length) throw new Error(errors.join('\n'));
  console.log(JSON.stringify({ pace: 'observed through real 60x solving; reset on pause', foregroundPause: 'enabled during tick', announcements: 'quiet', layouts: [320,640,720,1280,1600], browserErrors: 0 }));
} finally { await browser.close(); }
