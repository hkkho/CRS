/** Exercise the primary Studio view, history tabs and Designer in one live run. */
export async function verifyStudio(page, verifyGeometry) {
  const check = (condition, message) => { if (!condition) throw new Error(message); };
  const studio = page.locator('.reactor-studio');
  await studio.waitFor({ state: 'visible' });
  check(await studio.locator('[data-action="classic"]').count() === 0, 'Retired Tactical view is still offered.');
  check(await studio.locator('rect[data-channel]').count() === 380, 'Studio did not render 380 authoritative channels.');
  check(await studio.locator('.studio-zone').count() === 14, 'Studio did not render 14 zone fills.');
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
  await page.waitForFunction(() => document.querySelector('#status-mirror')?.textContent?.includes('120 fresh bundles. 1 refuelling operations.'), undefined, { timeout: 120_000 });
  check((await studio.locator('[data-field="impact"]').textContent()).includes('128 → 120'), 'Studio fuel impact is missing.');
  check((await studio.locator('[data-field="impact"]').textContent()).includes('Fresh fuel cost  −12.0'), 'Authoritative score breakdown is missing.');
  check((await studio.locator('[data-field="movement-result"]').textContent()).includes('Confirmed move #1'), 'Confirmed identity summary is missing.');
  check(await studio.locator('[data-axial]').count() === 2, 'Axial power/burnup graphs are missing.');
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
  check((await studio.locator('[data-history="readout"]').textContent()).includes('inspecting'), 'Sample inspection failed.');
  await studio.locator('[data-history="live"]').click();
  check((await studio.locator('[data-history="readout"]').textContent()).includes('latest'), 'Latest reading failed.');
  const retainedImpact = await studio.locator('[data-field="impact"]').textContent();
  const retainedFeedback = await studio.locator('[data-field="feedback"]').textContent();
  await studio.locator('[data-action="designer"]').click();
  await studio.waitFor({ state: 'detached' });
    const canvas = page.locator('.designer-stage canvas');
    await canvas.waitFor({ state: 'visible' });
    check(await canvas.evaluate(node => node.width === 1600 && node.height === 900), 'Designer canvas dimensions changed.');
  await page.waitForTimeout(200);
  const zoneLayout = verifyGeometry ? await verifyGeometry() : null;
  await page.keyboard.press('Escape');
  await studio.waitFor({ state: 'visible' });
  check(await studio.locator('[data-field="impact"]').textContent() === retainedImpact, 'Designer return lost the fuel impact.');
  check(await studio.locator('[data-field="feedback"]').textContent() === retainedFeedback, 'Designer return lost the response feedback.');
  for (let cycle = 0; cycle < 3; cycle++) {
    await studio.locator('[data-action="designer"]').click();
    await studio.waitFor({ state: 'detached' });
    await page.locator('.designer-stage canvas').waitFor({ state: 'visible' });
    await page.locator('.designer-controls').waitFor({ state: 'visible' });
    await page.keyboard.press('Escape');
    await studio.waitFor({ state: 'visible' });
    check(await studio.locator('[data-tab="power"]').getAttribute('aria-selected') === 'true', 'Repeated navigation lost the selected tab.');
    check(await studio.locator('[data-field="refuel"]').textContent() === 'Refuel 8 bundles →', 'Repeated navigation lost the eight-bundle order.');
    check(await studio.locator('[data-field="direction"]').textContent() === direction, 'Repeated navigation lost the draft direction.');
    check(await studio.locator('[data-field="impact"]').textContent() === retainedImpact, 'Repeated navigation lost the impact summary.');
    check(await studio.evaluate(element => element === document.activeElement), 'Return did not focus Studio.');
  }
  check((await studio.locator('[data-field="movement-result"]').textContent()).includes('Confirmed move #1'), 'Designer return lost the movement summary.');
  if (verifyGeometry) check(await studio.getAttribute('data-run-kind') === 'modified-sandbox', 'Accepted geometry edit is presented as a standard run.');
  check(await studio.locator('[data-tab="power"]').getAttribute('aria-selected') === 'true', 'Designer return lost the history tab.');
  check(Number(await studio.locator('[data-history="inspector"]').getAttribute('max')) >= sampleCount, 'Designer return lost the history.');
  await studio.locator('[data-tab="reactor"]').click();
  check(await studio.locator('[data-field="channel"]').textContent() === chosen, 'Designer return lost the selected channel.');
  check(await studio.locator('[data-field="direction"]').textContent() === direction, 'Designer return lost refuel direction.');
  check(await studio.locator('[data-field="refuel"]').textContent() === 'Refuel 8 bundles →', 'Designer return lost the eight-bundle order.');
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
  await page.waitForFunction(() => document.querySelector('#status-mirror')?.textContent?.includes('128 fresh bundles. 0 refuelling operations.'), undefined, { timeout: 120_000 });
  check((await studio.locator('[data-field="impact"]').textContent()).includes('first fuel move'), 'New shift retained old fuel impact.');
  await studio.locator('[data-action="pause"]').click();
  await page.waitForFunction(() => document.querySelector('#status-mirror')?.textContent?.includes('Paused.'));
  await studio.locator('[data-tab="burnup"]').click();
  check((await studio.locator('#studio-history-panel').textContent()).includes('No discharged fuel yet'), 'New shift retained old discharge readings.');
  check(Number(await studio.locator('[data-history="inspector"]').getAttribute('max')) < sampleCount, 'New shift retained old history samples.');
  await studio.locator('[data-tab="reactor"]').click();
  await studio.locator('[data-action="refuel"]').click();
  await page.waitForFunction(() => document.querySelector('#status-mirror')?.textContent?.includes('120 fresh bundles. 1 refuelling operations.'), undefined, { timeout: 120_000 });
  check((await studio.locator('[data-field="impact"]').textContent()).includes('128 → 120'), 'First post-reset fuel move did not publish its impact.');
  return { channels: 380, zones: 14, historyTabs: 7, refuel: 'accepted', sessionPreserved: true, historyReset: true, desktopSizes: ['1600x900', '1280x720', '720x720'], zoneLayout };
}
