import { chromium } from 'playwright';
import { mkdir, writeFile } from 'node:fs/promises';
import { verifyDailyTurns } from './daily-turn-smoke.mjs';

const url = process.argv[2] ?? process.env.PLAYTEST_URL;
if (!url) throw new Error('Pass the production preview or Pages URL.');
const capture = process.env.PLAYTEST_CAPTURE_DIR ?? 'artifacts/display-scales';
await mkdir(capture, { recursive: true });
const check = (condition, message) => { if (!condition) throw new Error(message); };
const browser = await chromium.launch({ headless: true });
const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
const errors = [];
page.on('pageerror', error => errors.push(error.message));
page.on('console', message => { if (message.type() === 'error') errors.push(message.text()); });
try {
  await page.goto(url);
  await page.waitForFunction(() => document.querySelector('#status-mirror')?.textContent?.includes('live reactor online'), undefined, { timeout: 120_000 });
  await page.locator('.launcher-setup details > summary').click();
  await page.locator('.reactor-launcher [data-field="seed"]').fill('1001');
  await page.locator('[data-goal="free-practice"]').click();
  await page.waitForFunction(() => document.querySelector('.reactor-launcher')?.getAttribute('aria-busy') === 'false', undefined, { timeout: 120_000 });
  await page.getByRole('button', { name: 'Turn based', exact: true }).click();
  await page.locator('[data-game-start]').click({ timeout: 180000 });
  const daily = await verifyDailyTurns(page);
  const studio = page.locator('.reactor-studio');
  await studio.getByRole('button', { name: 'Zoom in', exact: true }).click();
  check(await studio.locator('.core-map-zoom output').textContent() === '150%', 'Map zoom did not advance.');
  await studio.locator('rect[data-channel="210"]').hover();
  const tooltip = page.getByRole('tooltip');
  await tooltip.waitFor({ state: 'visible' });
  check((await tooltip.textContent()).includes('Exit burnup'), 'Channel tooltip lost discharge preview.');
  check(!(await tooltip.textContent()).includes('exit burnup: unavailable'), 'Real WASM exit preview is unavailable.');
  await page.screenshot({ path: `${capture}/zoom-tooltip.png`, fullPage: true });
  await page.keyboard.press('Escape');
  check(!await tooltip.isVisible(), 'Escape did not dismiss the tooltip.');
  await studio.getByRole('button', { name: 'Fit core', exact: true }).click();
  await page.screenshot({ path: `${capture}/studio-map.png`, fullPage: true });
  const palettes = {};
  for (const [mode, floor] of [['power', '4500'], ['bundle-power', '400']]) {
    await studio.locator('.studio-map-menu > summary').click();
    await studio.locator(`[data-map="${mode}"]`).click();
    check((await studio.locator('[data-field="legend"]').textContent()).includes(`Dark ≤${floor}`), `${mode} color floor is missing.`);
    palettes[mode] = await studio.locator('rect[data-channel]').evaluateAll(cells => new Set(cells.map(cell => cell.getAttribute('fill'))).size);
    const colors = await studio.locator('rect[data-channel]').evaluateAll(cells => [...new Set(cells.map(cell => cell.getAttribute('fill')))]);
    check(colors.every(color => ['#263346', '#98602f', '#f5a623', '#db3535', '#ff6b6b'].includes(color)), `${mode} map must use the discrete value bands.`);
    await studio.locator('[data-field="map"]').screenshot({ path: `${capture}/${mode}-map.png` });
  }
  const ranges = {};
  await studio.locator('[data-channel="210"]').first().click();
  await studio.locator('[data-inspection-tab="profiles"]').click();
  const axialPowers = await studio.locator('[data-axial="power"] [data-axial-position] title')
    .evaluateAll(titles => titles.map(title => Number(title.textContent.split(': ')[1].split(' ')[0])));
  check(axialPowers.length === 12, 'Selected channel lost its twelve bundle power readings.');
  const peak = Math.max(...axialPowers);
  check(axialPowers[0] < peak * .3 && axialPowers[11] < peak * .3,
    'Axial end powers do not show the v8 zero-incoming-current boundary response.');
  check((await page.locator('.game-update').textContent()).includes('Axial incoming current = 0'), 'Loaded physics version is missing.');
  await studio.locator('[data-axial="power"]').screenshot({ path: `${capture}/axial-power.png` });
  for (const tab of ['power', 'zones', 'reactivity']) {
    await studio.locator('.studio-view-menu > summary').click();
    await studio.locator(`[data-tab="${tab}"]`).click();
    const plots = studio.locator('.studio-trend-plot svg');
    ranges[tab] = await plots.evaluateAll(elements => elements.map(svg => ({ title: svg.querySelector('title').textContent,
      low: Number(svg.dataset.yMin), high: Number(svg.dataset.yMax), limits: [...svg.querySelectorAll('[data-chart-limit]')].map(label => label.textContent) })));
    if (tab === 'power') {
      check(ranges[tab][0].low > 5 && ranges[tab][0].high < 7.5,
        `Channel trend did not zoom to ordinary v9 power: ${JSON.stringify(ranges[tab][0])}`);
      check(ranges[tab][1].low >= 700 && ranges[tab][1].high <= 900, 'Bundle trend did not zoom to ordinary v9 power.');
      check(ranges[tab][0].limits[0].includes('7.10 MW thermal'), 'Channel limit disappeared from the graph.');
    }
    if (tab === 'zones') check(ranges[tab][0].low <= 45 && ranges[tab][0].high >= 55 && ranges[tab][0].high - ranges[tab][0].low < 35, `Zone graph lost its useful benchmark view: ${JSON.stringify(ranges[tab][0])}`);
    if (tab === 'reactivity') check(ranges[tab][0].high - ranges[tab][0].low < .0001, 'Keff variation is flattened.');
    for (let i = 0; i < ranges[tab].length; i++) await studio.locator('.studio-trend').nth(i).screenshot({ path: `${capture}/${tab}-graph-${i}.png` });
    await page.setViewportSize({ width: 320, height: 740 });
    check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `${tab} graphs overflow at 320px.`);
    await page.screenshot({ path: `${capture}/${tab}-320.png`, fullPage: true });
    await page.setViewportSize({ width: 1280, height: 900 });
  }
  check(errors.length === 0, errors.join(' | '));
  const result = { status: 'passed', url, seed: 1001, daily, palettes, ranges, axialPowers, narrowLayout: 'passed', errors };
  await writeFile(`${capture}/acceptance.json`, JSON.stringify(result, null, 2));
  console.log(JSON.stringify(result));
} finally { await browser.close(); }
