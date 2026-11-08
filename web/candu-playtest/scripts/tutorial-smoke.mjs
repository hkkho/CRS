import assert from 'node:assert/strict';
import { mkdir } from 'node:fs/promises';
import { chromium } from 'playwright';

const browser = await chromium.launch({ headless: true });
const page = await browser.newPage({ viewport: { width: 1440, height: 1000 }, reducedMotion: 'reduce' });
const captures = process.env.PLAYTEST_CAPTURE_DIR ?? 'artifacts/tutorial';
const errors = [];
page.on('pageerror', error => errors.push(error.message));
const tutorial = page.locator('.demo-tutorial');
const view = page.locator('.reactor-tutorial');
const heading = tutorial.getByRole('heading', { level: 2 });
const button = name => tutorial.getByRole('button', { name, exact: true });
const advance = button('Advance 12 hours');
const queue = button('Queue fuel');
const map = page.locator('.arcade-map');
const cell = index => map.locator(`[data-channel="${index}"]`);
const selected = () => map.locator('[aria-pressed=true]').getAttribute('data-channel');
const caption = tutorial.locator('.demo-tutorial-kicker');
const cards = tutorial.locator('.demo-tutorial-examples');
const readings = tutorial.locator('.demo-tutorial-readings').first();
const wait = { timeout: 180_000 };

async function checkLayout(selector, widths, name) {
  for (const width of widths) {
    await page.setViewportSize({ width, height: width < 700 ? 844 : 1000 });
    const root = page.locator(selector);
    assert.ok(await root.evaluate(element => element.scrollWidth <= element.clientWidth), `${name} overflow at ${width}px`);
    await root.evaluate(element => { element.scrollTop = 0; });
    await page.screenshot({ path: `${captures}/${name}-${width}.png` });
  }
}

async function refuel(index) {
  assert.equal(await selected(), String(index));
  await queue.click();
  assert.equal(await map.locator('[data-planned=true]').count(), 1);
  assert.equal(await view.getAttribute('data-time-seconds'), '0');
  await advance.click();
  await button('See result').waitFor(wait);
  await button('See result').click();
  assert.match(await heading.innerText(), /solved result/);
  assert.equal(await view.getAttribute('data-time-seconds'), '43200');
  assert.equal(await view.getAttribute('data-fuel-consumed'), '8');
  assert.equal(await map.locator('[data-planned=true]').count(), 0);
  assert.match(await readings.innerText(), /8 bundles delivered.*MWd\/kg outgoing.*Step score/);
  return readings.innerText();
}

try {
  await mkdir(captures, { recursive: true });
  await page.goto(process.argv[2] ?? process.env.PLAYTEST_URL ?? 'http://127.0.0.1:4173/CRS/');
  const title = 'Candu Refuelling Simulator (CRS)';
  const launcher = page.locator('.crs-launcher');
  const start = launcher.getByRole('button', { name: 'Start game', exact: true });
  const guide = launcher.getByRole('button', { name: 'Tutorial', exact: true });
  await start.waitFor(wait);
  assert.equal(await page.title(), title);
  assert.equal(await launcher.getByRole('heading', { level: 1 }).innerText(), title);
  assert.equal(await launcher.locator('.launcher-subtitle').innerText(), 'A Stylized Simulator demo made by ChatGPT and Gemini.');
  assert.equal(await launcher.getByRole('link', { name: /report an issue/i }).getAttribute('href'), 'https://github.com/hkkho/CRS/issues');
  assert.ok((await start.boundingBox()).y < (await guide.boundingBox()).y);
  await checkLayout('.crs-launcher', [320, 1440], 'launcher');
  await launcher.locator('summary').click();
  await launcher.locator('[data-field=seed]').fill('42');
  await guide.click();
  await page.locator('[data-game-start]').click(wait);
  assert.equal(await caption.innerText(), 'LESSON 1 / 8 · SEED 1001');
  assert.ok(await heading.evaluate(element => element === document.activeElement));
  assert.equal(await view.getAttribute('data-running'), 'false');
  await cell(210).focus();
  await page.keyboard.press('Enter');
  assert.equal(await map.locator('[data-planned=true]').count(), 0);
  for (const mode of ['ripple', 'burnup', 'inlet-zones', 'power', 'power', 'burnup']) {
    await button('Next lesson').click();
    assert.equal(await map.getAttribute('data-map-mode'), mode);
    assert.notEqual(await readings.innerText(), '');
  }
  assert.match(await heading.innerText(), /Compare two channels/);
  await checkLayout('.reactor-tutorial', [320, 390, 1440], 'practice');
  const startingCards = await cards.innerText();
  const inspect = cards.getByRole('button');
  assert.equal(await inspect.count(), 2);
  assert.ok(await advance.isDisabled());
  await cell(210).focus();
  await page.keyboard.press('ArrowRight');
  assert.equal(await selected(), '211');
  await cell(212).click();
  assert.ok(await queue.isDisabled());
  await cell(212).press('Enter');
  assert.equal(await map.locator('[data-planned=true]').count(), 0);
  await inspect.first().click();
  await queue.click();
  await inspect.last().click();
  assert.ok(await queue.isDisabled());
  await button('Remove order').click();
  assert.ok(await advance.isDisabled());
  await inspect.first().click();
  const first = await refuel(210);
  assert.match(first, /Z14 34\.0% → 42\.0% → 34\.0%/);
  await page.screenshot({ path: `${captures}/result.png` });
  await button('Try the other channel').click();
  await advance.waitFor(wait);
  assert.equal(await caption.innerText(), 'LESSON 7 / 8 · SEED 1001');
  assert.equal(await cards.innerText(), startingCards);
  assert.equal(await view.getAttribute('data-fuel-consumed'), '0');
  assert.notEqual(await refuel(211), first);
  assert.equal(await tutorial.locator('.demo-tutorial-readings').last().innerText(), `Previous choice · ${first}`);
  await button('Finish tutorial').click();
  assert.ok(await guide.evaluate(element => element === document.activeElement));
  assert.equal(await launcher.locator('[data-field=seed]').inputValue(), '42');
  assert.deepEqual(errors, []);
  console.log('Verified the fixed-seed tutorial, both fuel choices, keyboard gates and narrow layouts.');
} catch (error) {
  await page.screenshot({ path: `${captures}/tutorial-failure.png` });
  console.error(await page.locator('body').innerText(), errors);
  throw error;
} finally {
  await browser.close();
}
