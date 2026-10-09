/** Exercise the primary Studio view, history tabs and bundle poison in one live run. */
export async function verifyStudio(page) {
  const check = (condition, message) => { if (!condition) throw new Error(message); };
  const studio = page.locator('.reactor-studio');
  await studio.waitFor({ state: 'visible' });
  check(await studio.locator('[data-action="classic"]').count() === 0, 'Retired Tactical view is still offered.');
  check(await studio.locator('rect[data-channel]').count() === 380, 'Studio did not render 380 authoritative channels.');
  check(await studio.locator('[data-plan-adjuster]').count() === 21, 'Studio did not render the 21 authoritative inserted adjusters.');
  check(await studio.locator('[data-adjuster-column]').count() === 7, 'Face projection did not retain seven adjuster columns.');
  check(await studio.locator('[data-plan-lzc]').count() === 6, 'Plan did not separate the six localized LZC assemblies.');
  check(await studio.locator('[data-face-lzc]').count() === 7, 'End A face did not render seven LZC compartments.');
  await studio.locator('[data-action="lzc-plane"][data-plane="1"]').click();
  check((await studio.locator('[data-face-lzc]').evaluateAll(nodes => nodes.map(n => Number(n.dataset.faceLzc)))).every(id => id >= 7), 'End B plane did not switch to authoritative far-half compartments.');
  await studio.locator('[data-action="lzc-plane"][data-plane="0"]').click();
  check(await studio.locator('.studio-zone').count() === 14, 'Studio did not render 14 zone fills.');
  check(await studio.locator('.studio-metrics article').count() === 4, 'Studio still includes the redundant regulated-power box.');
  check(await studio.locator('[data-field="power"]').count() === 0, 'Redundant regulated-power field remains.');
  check(/^[A-HJ-W]\d{2}$/.test(await studio.locator('[data-field="channel"]').textContent()), 'Selected channel is not identified by its core name.');
  check((await studio.locator('[data-field="power-rating"]').textContent()).includes('MW thermal'), 'Studio power units are missing.');
  await studio.locator('[data-action="pause"]').click();
  await page.waitForFunction(() => document.querySelector('#status-mirror')?.textContent?.includes('Paused.'));
  await studio.locator('[data-tab="burnup"]').click();
  check((await studio.locator('#studio-history-panel').textContent()).includes('No discharged fuel yet'), 'Discharge chart invented a pre-refuelling reading.');
  await studio.locator('[data-tab="reactor"]').click();
  await studio.locator('[data-action="oldest"]').click();
  const chosen = await studio.locator('[data-field="channel"]').textContent();
  const burnupFill = await studio.locator('rect[tabindex="0"]').getAttribute('fill');
  await studio.locator('[data-map="power"]').click();
  check(await studio.locator('rect[tabindex="0"]').getAttribute('fill') !== burnupFill, 'Studio power map did not update.');
  await studio.locator('[data-map="burnup"]').click();
  check(await studio.locator('[data-movement="outgoing"]').count() === 8, 'Shared movement plan did not mark eight outgoing positions.');
  const direction = await studio.locator('[data-field="direction"]').textContent();
  await studio.locator('[data-action="refuel"]').click();
  await page.waitForFunction(() => document.querySelector('#status-mirror')?.textContent?.includes('Unlimited fresh fuel. 1 refuelling operations.'), undefined, { timeout: 120_000 });
  check((await studio.locator('[data-field="impact"]').textContent()).includes('Unlimited · 8 bundles used'), 'Studio fuel impact is missing.');
  check((await studio.locator('[data-field="impact"]').textContent()).includes('RMS ripple'), 'Authoritative ripple response is missing.');
  check((await studio.locator('[data-field="ripple-score"]').textContent()).includes('points/h'), 'Live ripple score rate is missing.');
  check((await studio.locator('[data-field="channel-reference"]').textContent()).includes('MW / reference'), 'Selected channel reference is missing.');
  check((await studio.locator('[data-field="channel-reference"]').textContent()).includes('21 inserted adjusters'), 'The channel reference still misrepresents the current adjuster model.');
  check((await studio.locator('[data-field="movement-result"]').textContent()).includes('Confirmed move #1'), 'Confirmed identity summary is missing.');
  check(await studio.locator('[data-axial]').count() === 4, 'Axial power/burnup/iodine/xenon graphs are missing.');
  const cleanPositions = await studio.locator('[data-confirmed="inserted"]').allTextContents();
  check(cleanPositions.length === 8, 'Refuel did not publish eight inserted bundles.');
  for (const label of cleanPositions) {
    const position = Number(label.replace('New ', '')) - 1;
    for (const metric of ['iodine', 'xenon']) {
      check((await studio.locator(`[data-axial="${metric}"] [data-axial-position="${position}"] title`).textContent()).includes(': 0.00 '), `Fresh bundle ${position + 1} has ${metric}.`);
    }
  }
  if (process.env.PLAYTEST_CAPTURE_DIR) {
    await studio.locator('[data-field="movement-result"]').scrollIntoViewIfNeeded();
    await page.screenshot({ path: `${process.env.PLAYTEST_CAPTURE_DIR}/studio-movement.png` });
    await studio.evaluate(element => { element.scrollTop = 0; });
  }
  await studio.locator('.studio-controls > summary').click();
  const clockBefore = await studio.locator('[data-field="time"]').textContent();
  await studio.locator('[data-action="step"]').click();
  await page.waitForFunction(clock => document.querySelector('.reactor-studio [data-field="time"]')?.textContent !== clock, clockBefore, { timeout: 120_000 });
  await studio.locator('[data-action="pause"]').click();
  await page.waitForFunction(() => document.querySelector('#status-mirror')?.textContent?.includes('Running.'));
  await studio.locator('[data-field="target-input"]').fill('95');
  await studio.locator('[data-action="target"]').click();
  await page.waitForFunction(() => document.querySelector('[data-field="power-target"]')?.textContent === '95.0%', undefined, { timeout: 120_000 });
  await studio.locator('[data-action="pause"]').click();
  await page.waitForFunction(() => document.querySelector('#status-mirror')?.textContent?.includes('Paused.'));
  for (const tab of ['power', 'burnup', 'zones', 'tilt', 'reactivity', 'xenon', 'fuel']) {
    await studio.locator(`[data-tab="${tab}"]`).click();
    check(await studio.locator('#studio-history-panel svg').count() > 0, `${tab} graphs are missing.`);
    check(!/NaN|Infinity/.test(await studio.locator('#studio-history-panel').innerHTML()), `${tab} graphs contain invalid numbers.`);
    if (tab === 'zones') {
      check(await studio.locator('[data-trace]').count() === 15, 'Zone histories should include Z1–Z14 and the core mean.');
      await studio.locator('[data-trace="0:0"]').click();
      check(await studio.locator('[data-trace="0:0"]').getAttribute('aria-pressed') === 'false', 'Zone trace toggle failed.');
    }
    if (tab === 'burnup') check(!(await studio.locator('#studio-history-panel').textContent()).includes('No discharged fuel yet'), 'Confirmed discharge history is missing.');
  }
  await studio.locator('[data-tab="power"]').click();
  const sampleCount = Number(await studio.locator('[data-history="inspector"]').getAttribute('max'));
  check(sampleCount >= 2, 'Advancing time did not record history.');
  await studio.locator('[data-history="window"]').selectOption('21600');
  await studio.locator('[data-history="inspector"]').fill('0');
  check((await studio.locator('[data-history="readout"]').textContent()).includes('HISTORY'), 'Sample inspection failed.');
  check(await studio.locator('[data-action="refuel"]').isDisabled(), 'Historical data allowed refuelling.');
  await studio.locator('[data-history="live"]').click();
  check((await studio.locator('[data-history="readout"]').textContent()).includes('latest'), 'Latest reading failed.');
  check(await studio.locator('[data-action="designer"]').count() === 0, 'Removed core designer is still offered.');
  check(await page.locator('canvas').count() === 0, 'Removed designer canvas is still loaded.');
  await studio.locator('[data-tab="reactor"]').click();
  check(await studio.locator('[data-field="channel"]').textContent() === chosen, 'History tabs lost the selected channel.');
  check(await studio.locator('[data-field="direction"]').textContent() === direction, 'History tabs lost refuel direction.');
  for (const width of [1600, 1280, 720]) {
    await page.setViewportSize({ width, height: width === 1600 ? 900 : 720 });
      check(await studio.evaluate(element => element.scrollWidth <= element.clientWidth), `Studio overflows at ${width}px.`);
      check(await studio.locator('[data-field="pace"]').evaluate(element => {
        const label = element.getBoundingClientRect(), card = element.closest('article').getBoundingClientRect();
        return label.left >= card.left && label.right <= card.right && label.bottom <= card.bottom;
      }), `Pace label escapes its clock card at ${width}px.`);
    if (process.env.PLAYTEST_CAPTURE_DIR) await page.screenshot({ path: `${process.env.PLAYTEST_CAPTURE_DIR}/studio-${width}.png` });
    await studio.locator('[data-tab="zones"]').click();
    check(await studio.evaluate(element => element.scrollWidth <= element.clientWidth), `Charts overflow at ${width}px.`);
    if (process.env.PLAYTEST_CAPTURE_DIR) await page.screenshot({ path: `${process.env.PLAYTEST_CAPTURE_DIR}/studio-trends-${width}.png` });
    await studio.locator('[data-tab="reactor"]').click();
  }
  await page.setViewportSize({ width: 1600, height: 900 });
  await studio.locator('[data-action="reset"]').click();
  await page.waitForFunction(() => document.querySelector('#status-mirror')?.textContent?.includes('Unlimited fresh fuel. 0 refuelling operations.'), undefined, { timeout: 120_000 });
  check((await studio.locator('[data-field="impact"]').textContent()).includes('first fuel move'), 'New shift retained old fuel impact.');
  await studio.locator('[data-action="pause"]').click();
  await page.waitForFunction(() => document.querySelector('#status-mirror')?.textContent?.includes('Paused.'));
  await studio.locator('[data-tab="burnup"]').click();
  check((await studio.locator('#studio-history-panel').textContent()).includes('No discharged fuel yet'), 'New shift retained old discharge readings.');
  check(Number(await studio.locator('[data-history="inspector"]').getAttribute('max')) < sampleCount, 'New shift retained old history samples.');
  await studio.locator('[data-tab="reactor"]').click();
  await studio.locator('[data-action="refuel"]').click();
  await page.waitForFunction(() => document.querySelector('#status-mirror')?.textContent?.includes('Unlimited fresh fuel. 1 refuelling operations.'), undefined, { timeout: 120_000 });
  check((await studio.locator('[data-field="impact"]').textContent()).includes('Unlimited · 8 bundles used'), 'First post-reset fuel move did not publish its impact.');
  return { channels: 380, zones: 14, historyTabs: 7, refuel: 'accepted', sessionPreserved: true, historyReset: true, desktopSizes: ['1600x900', '1280x720', '720x720'], freshBundlePoison: 'zero' };
}
