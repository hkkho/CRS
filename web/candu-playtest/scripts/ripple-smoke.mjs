import { chromium } from 'playwright';
import { mkdir, writeFile } from 'node:fs/promises';

const url = process.argv[2];
if (!url) throw new Error('Pass the playtest URL.');
const browser = await chromium.launch({ headless: true });
const page = await browser.newPage({ viewport: { width: 1600, height: 1000 } });
const errors = [];
page.on('pageerror', error => errors.push(error.message));
page.on('console', message => { if (message.type() === 'error') errors.push(message.text()); });
const check = (condition, message) => { if (!condition) throw new Error(message); };
try {
  await page.goto(url, { waitUntil: 'networkidle' });
  await page.waitForFunction(() => document.querySelector('#status-mirror')?.textContent?.includes('live reactor online'), undefined, { timeout: 120_000 });
  await page.getByRole('button', { name: 'Begin shift', exact: true }).click();
  const studio = page.locator('.reactor-studio');
  const reference = studio.locator('[data-field="channel-reference"]');
  const rate = studio.locator('[data-field="ripple-score"]');
  const before = await reference.textContent();
  const read = text => {
    const matches = text.match(/Channel power ([\d.]+) MW \/ reference ([\d.]+) MW · ripple ([\d.]+)%/);
    check(matches, 'Reference readings are missing.');
    const [actual, target, ripple] = matches.slice(1).map(Number);
    check(Math.abs(actual / target * 100 - ripple) < 0.15, 'Displayed watts disagree with the authoritative ripple ratio.');
    return { actual, target, ripple };
  };
  const initial = read(before);
  await studio.locator('.studio-controls > summary').click();
  await studio.locator('[data-field="target-input"]').fill('95');
  await studio.locator('[data-action="target"]').click();
  await page.waitForFunction(() => document.querySelector('[data-field="power-target"]')?.textContent === '95.0%', undefined, { timeout: 120_000 });
  await studio.locator('[data-action="pause"]').click();
  await page.waitForFunction(() => document.querySelector('#status-mirror')?.textContent?.includes('Paused.'), undefined, { timeout: 120_000 });
  const derated = read(await reference.textContent());
  check(initial.target === derated.target, 'Applying power target rebased the reference.');
  check((await rate.textContent()).includes('points/h'), 'Ripple score rate is missing.');
  const score = await studio.locator('[data-field="score"]').textContent();
  await studio.locator('[data-action="refuel"]').click();
  await page.waitForFunction(() => document.querySelector('#status-mirror')?.textContent?.includes('120 fresh bundles. 1 refuelling operations.'), undefined, { timeout: 120_000 });
  check(await studio.locator('[data-field="score"]').textContent() === score, 'Paused refuelling awarded instant points.');
  const refuelled = read(await reference.textContent());
  check(refuelled.target === initial.target, 'Refuelling rebased the reference.');
  check(errors.length === 0, 'Browser errors: ' + errors.join(' | '));
  if (process.env.PLAYTEST_CAPTURE_DIR) {
    await mkdir(process.env.PLAYTEST_CAPTURE_DIR, { recursive: true });
    await studio.locator('[data-field="channel-reference"]').scrollIntoViewIfNeeded();
    await page.screenshot({ path: `${process.env.PLAYTEST_CAPTURE_DIR}/ripple-reference.png` });
    await writeFile(`${process.env.PLAYTEST_CAPTURE_DIR}/ripple-readings.json`, JSON.stringify({ initial, derated, refuelled, rate: await rate.textContent(), score, errors }, null, 2));
  }
  console.log(JSON.stringify({ status: 'ok', initial, derated, refuelled, fixedReference: true, noInstantPoints: true, errors }));
} finally { await browser.close(); }
