import { chromium } from 'playwright';
import { mkdir } from 'node:fs/promises';

const browser = await chromium.launch({ headless: true });
const page = await browser.newPage({ viewport: { width: 1440, height: 1000 }, reducedMotion: 'reduce' });
const errors = [];
page.on('pageerror', error => errors.push(error.message));
page.on('console', message => { if (message.type() === 'error') errors.push(message.text()); });
const waitOptions = { timeout: 180000 };
try {
  await page.goto(process.argv[2] ?? process.env.PLAYTEST_URL ?? 'http://127.0.0.1:4173/CRS/');
  await page.locator('[data-action=demo]').waitFor(waitOptions);
  if (errors.length) throw new Error(errors.join('\n'));
  console.log('Launcher ready.');
  await page.locator('.launcher-setup details > summary').click();
  await page.locator('[data-field=seed]').fill('1001');
  if (await page.getByRole('button', { name: 'Turn based', exact: true }).count()) throw new Error('Turn based is still selectable');
  if ((await page.locator('[data-action=demo]').innerText()) !== 'Start game') throw new Error('Start game choice missing');
  if (await page.locator('.launcher-actions button').first().getAttribute('data-action') !== 'demo') throw new Error('Start game is not first');
  await page.locator('[data-action=demo]').click();
  await page.locator('.game-loading-dialog').waitFor();
  await mkdir('artifacts/demo', { recursive: true });
  await page.screenshot({ path: 'artifacts/demo/loading.png', fullPage: true });
  await page.locator('[data-game-start]').waitFor({ state: 'visible', ...waitOptions });
  if (await page.locator('.reactor-demo').getAttribute('data-running') !== 'false') throw new Error('Game started before Start');
  await page.screenshot({ path: 'artifacts/demo/ready.png', fullPage: true });
  await page.locator('[data-game-start]').click();
  await page.locator('.reactor-demo .arcade-map [data-channel="210"]').waitFor(waitOptions);
  const slow = page.getByRole('button', { name: '0.5× speed (10 seconds per half-day)' });
  const fast = page.getByRole('button', { name: '1.5× speed (5 seconds per half-day)' });
  await slow.click();
  if (await slow.getAttribute('aria-pressed') !== 'true') throw new Error('Slow speed was not selected');
  await slow.click();
  if (await slow.getAttribute('aria-pressed') !== 'false') throw new Error('Normal speed was not restored');
  await fast.click();
  if (await fast.getAttribute('aria-pressed') !== 'true') throw new Error('Fast speed was not selected');
  console.log('Time based core ready.');
  for (const mode of ['ripple', 'burnup', 'inlet-zones', 'power']) {
    const tab = page.locator(`[role=tab][data-map-tab="${mode}"]`);
    await tab.click();
    if (await tab.getAttribute('aria-selected') !== 'true' ||
        await page.locator('.arcade-map').getAttribute('data-map-mode') !== mode) throw new Error(`Map did not change to ${mode}`);
    if (mode === 'inlet-zones') {
      const channel = page.locator('.arcade-map [data-channel="210"]');
      const neighbor = page.locator('.arcade-map [data-channel="211"]');
      const top = page.locator('.arcade-map [data-channel="0"]');
      if (!(await channel.getAttribute('aria-label')).includes('Inlet End B · Z14') ||
          !(await neighbor.getAttribute('aria-label')).includes('Inlet End A · Z7') ||
          !(await top.getAttribute('aria-label')).includes('Inlet End A · Z3')) {
        throw new Error('Inlet zones do not match alternating flow and face numbering');
      }
      if ((await channel.evaluate(cell => getComputedStyle(cell).backgroundImage)) !== 'none' ||
          (await channel.evaluate(cell => cell.style.backgroundColor)) === (await neighbor.evaluate(cell => cell.style.backgroundColor))) {
        throw new Error('Inlet zones are missing their alternating full-cell colors');
      }
      await channel.click();
      if (!(await page.locator('[data-selected-zone]').innerText()).includes('Inlet End B · Z14') ||
          await page.locator('[data-trend-end-a]').innerText() !== 'End A · Z7' ||
          await page.locator('[data-trend-end-b]').innerText() !== 'End B · Z14' ||
          await page.locator('[data-chart=zones] .trend-line').count() !== 3) {
        throw new Error('Zone chart does not show the selected channel pair');
      }
      await page.screenshot({ path: 'artifacts/demo/inlet-map.png', fullPage: true });
    }
  }
  if (await page.locator('.reactor-demo h1').innerText() !== 'Reactor Simulator') throw new Error('Play-screen title is incorrect');
  if ((await page.locator('.reactor-demo header').innerText()).includes('DEMO')) throw new Error('Old demo label');
  if ((await page.locator('.demo-hud').innerText()).includes('sec')) throw new Error('Speed value is still visible');
  if (!/^Ripple [0-9.]+ .* Tilt [0-9.]+ pts\/h$/.test(await page.locator('[data-score-rate]').innerText())) throw new Error('Operating score components missing');
  if (!(await page.locator('[data-day]').innerText()).includes(':')) throw new Error('Simulation clock missing');
  if (await page.locator('.demo-zone-grid [data-zone]').count() !== 14) throw new Error('Missing liquid zones');
  if (await page.locator('.reactor-demo .core-map-zoom').count()) throw new Error('Demo retained zoom controls');
  if ((await page.locator('.reactor-demo .arcade-map [data-channel="210"]').innerText()) !== '') throw new Error('Demo channel still contains a power label');
  if (await page.locator('.demo-trends svg').count() !== 2) throw new Error('Missing live plots');
  if (!await page.evaluate(() => document.querySelector('.reactor-demo [data-map]').clientWidth >= document.querySelector('.demo-core').clientWidth * .98)) throw new Error('Core map does not fill its available panel');
  if (!await page.evaluate(() => document.querySelector('.demo-selected-hud').getBoundingClientRect().height < 100)) throw new Error('Selected channel header is wrapping excessively');
  // Queue several channels early in playback, while its successor is solving or buffered.
  await page.waitForFunction(() => {
    const clock = document.querySelector('.demo-clock');
    return clock?.value > .05 && clock.value < .3;
  }, undefined, waitOptions);
  const initialDay = Number(await page.locator('.reactor-demo').getAttribute('data-days'));
  const start = Date.now();
  await page.locator('.reactor-demo .arcade-map [data-channel="210"]').dblclick();
  await page.locator('.reactor-demo .arcade-map [data-channel="211"]').dblclick();
  if (await page.locator('.reactor-demo [data-planned=true]').count() !== 2) throw new Error('Both queued outlines must remain visible');
  const queuedStyle = await page.locator('.reactor-demo [data-channel="210"]').evaluate(cell => ({ ring: getComputedStyle(cell).boxShadow, dot: getComputedStyle(cell, '::after').display }));
  if (!queuedStyle.ring.includes('inset') || !queuedStyle.ring.includes('3px') || queuedStyle.dot !== 'none') throw new Error('Queued channel is missing its full inset outline');
  await page.screenshot({ path: 'artifacts/demo/queued-batch.png', fullPage: true });
  const fuelReadings = [];
  // Selection remains interactive while a shared solver operation is in flight.
  await page.locator('.reactor-demo .arcade-map [data-channel="211"]').click();
  if (await page.locator('.reactor-demo [data-channel="211"]').getAttribute('aria-pressed') !== 'true') throw new Error('Map blocked during fuel solve');
  if (await page.locator('.reactor-demo [data-selected-channel]').innerText() === '—') throw new Error('Selected channel label is missing');
  if (await page.locator('dialog[open]').count()) throw new Error('Demo opened a calculation screen');
  if ((await page.locator('.reactor-demo [data-live]').innerText()) !== 'Live power') throw new Error('Solver status leaked into live play');
  // Startup and the first shared solve may exceed a playback interval on non-AOT builds.
  await page.locator('.reactor-demo .arcade-map [data-channel="210"]').click();
  await page.waitForFunction(() => {
    const progress = document.querySelector('.demo-clock');
    return progress && progress.value > .05 && progress.value < .6;
  }, undefined, waitOptions);
  const powerFrames = new Set();
  for (let i = 0; i < 30; i++) {
    powerFrames.add(await page.locator('.demo-hud [data-power]').innerText());
    fuelReadings.push(Number(await page.locator('.reactor-demo').getAttribute('data-fuel-consumed')));
    await page.waitForTimeout(150);
  }
  if (powerFrames.size < 3) throw new Error('Power display did not interpolate intermediate readings');
  if (Number(await page.locator('.demo-trends').getAttribute('data-samples')) < 3) throw new Error('Live plots did not accumulate displayed history');
  await page.waitForFunction(() => Number(document.querySelector('.reactor-demo')?.getAttribute('data-fuel-consumed')) > 0, undefined, waitOptions);
  fuelReadings.push(Number(await page.locator('.reactor-demo').getAttribute('data-fuel-consumed')));
  if (fuelReadings.some(value => value !== 0 && value !== 16)) throw new Error(`Fuel batch was split across displayed steps: ${fuelReadings}`);
  if (fuelReadings.at(-1) !== 16) throw new Error('Both channels were not fuelled together');
  if (!/Burnup [+-][0-9.]+ .* Zones \+[0-9.]+ pts/.test(await page.locator('[data-fuel-receipt]').innerText())) throw new Error('Fuel receipt score components missing');
  await page.locator('[data-fuel-zone-response] summary').click();
  if (!/Z14 [\d.]+% → [\d.]+% → [\d.]+%/.test(await page.locator('[data-fuel-zone-values]').innerText())) throw new Error('Fuel receipt zone response missing');
  if (await page.locator('.reactor-demo [data-planned=true]').count()) throw new Error('Delivered batch retained queue outlines');
  const fuelMs = Date.now() - start;
  console.log(`Fuel response: ${fuelMs}ms.`);
  await page.waitForFunction(day => Number(document.querySelector('.reactor-demo')?.getAttribute('data-days')) > day, initialDay, waitOptions);
  const dayMs = Date.now() - start;
  const cadenceDay = Number(await page.locator('.reactor-demo').getAttribute('data-days'));
  await page.waitForFunction(day => Number(document.querySelector('.reactor-demo')?.getAttribute('data-days')) > day, cadenceDay, waitOptions);
  const dayIntervalMs = Date.now() - start - dayMs;
  await page.getByRole('button', { name: 'Pause', exact: true }).click();
  await page.getByRole('button', { name: 'Resume', exact: true }).waitFor();
  if (await fast.getAttribute('aria-pressed') !== 'true') throw new Error('Pause changed the selected speed');
  await page.waitForFunction(() => document.querySelector('.reactor-demo')?.getAttribute('data-solving') === 'false', undefined, waitOptions);
  const pausedDay = await page.locator('.reactor-demo').getAttribute('data-days');
  const pausedTime = await page.locator('.reactor-demo').getAttribute('data-time-seconds');
  const fuel = page.locator('[data-action=fuel]');
  const cell = page.locator('.arcade-map [data-channel="210"]');
  await cell.click();
  await fuel.click();
  if (await cell.getAttribute('data-fuel-state') !== 'waiting' || await fuel.innerText() !== 'Remove order') throw new Error('Cannot queue fuel while paused');
  await cell.press('Enter');
  if (await cell.getAttribute('data-planned') !== 'false' || await fuel.innerText() !== 'Queue fuel') throw new Error('Cannot remove a waiting order by keyboard');
  await cell.dblclick();
  await fuel.click();
  if (await cell.getAttribute('data-planned') !== 'false') throw new Error('Cannot remove a waiting order using the fuel button');
  await page.waitForTimeout(5500);
  if (await page.locator('.reactor-demo').getAttribute('data-days') !== pausedDay ||
      await page.locator('.reactor-demo').getAttribute('data-time-seconds') !== pausedTime) throw new Error('Paused demo advanced');
  await mkdir('artifacts/demo', { recursive: true });
  await page.locator('.reactor-demo').evaluate(element => { element.scrollTop = 0; });
  await page.screenshot({ path: 'artifacts/demo/desktop.png', fullPage: true });
  for (const width of [1440, 320, 390, 768]) {
    await page.setViewportSize({ width, height: 844 });
    if (!await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth && document.querySelector('.reactor-demo').scrollWidth <= innerWidth)) throw new Error(`Overflow at ${width}px`);
    const layout = await page.evaluate(() => {
      const [map, readout, trends] = [...document.querySelector('.demo-workspace').children].map(panel => panel.getBoundingClientRect());
      const scale = document.querySelector('[data-scale]');
      const expectedColumns = innerWidth >= 960 ? readout.left >= map.right && trends.left === readout.left : readout.top >= map.bottom;
      return expectedColumns && trends.top >= readout.bottom && readout.height > 0 && trends.height > 0 &&
        scale.clientWidth >= map.width * .98 &&
        getComputedStyle(scale, '::before').backgroundImage.startsWith('linear-gradient');
    });
    if (!layout) throw new Error(`Workspace panels or full-width heat bar missing at ${width}px`);
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
  if (errors.length) throw new Error(errors.join('\n'));
  console.log(JSON.stringify({ result: 'Demo Pages-path smoke passed', fuelMs, firstDayMs: dayMs, dayIntervalMs, distinctPowerFrames: powerFrames.size, zones: 14, batchedChannels: 2, queueOutline: true, viewports: [1440, 768, 390, 320], keyboard: true, pause: true, retry: true }));
} catch (error) {
  await mkdir('artifacts/demo', { recursive: true });
  await page.screenshot({ path: 'artifacts/demo/failure.png', fullPage: true });
  console.error(await page.locator('body').innerText(), errors);
  throw error;
} finally { await browser.close(); }
