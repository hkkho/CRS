const check = (condition, message) => { if (!condition) throw new Error(message); };

/** Normal product controls against the actual worker-hosted shared simulation. */
export async function verifyDailyTurns(page) {
  const studio = page.locator('.reactor-studio');
  const mirror = page.locator('#status-mirror');
  const idle = () => page.waitForFunction(() => document.querySelector('.reactor-studio')?.getAttribute('aria-busy') === 'false', undefined, { timeout: 900_000 });
  await idle();
  check((await studio.locator('[data-field="legend"]').textContent()).includes('7100 kW / channel'), 'Shared channel limit is not 7100 kW.');
  const initialZones = await studio.locator('.studio-zone > span').allTextContents();
  check(new Set(initialZones).size > 1, 'Aged-core zones were not settled against the fixed reference.');
  check(await studio.locator('.reactor-locator, .studio-lzc-label').count() === 0, 'Decorative rails or device text remain on the map.');
  check(await studio.locator('.studio-history-navigation').evaluate(e => e.tagName === 'DIV'), 'History inspection is still collapsed behind a disclosure.');
  await studio.locator('[data-inspection-tab="profiles"]').click();
  check((await studio.locator('[data-axial="power"]').textContent()).includes('900 kW limit'), 'Shared bundle limit is not 900 kW.');
  await studio.locator('[data-inspection-tab="zones"]').click();
  check(await mirror.getAttribute('data-pacing') === 'daily-turn', 'New run did not default to daily turns.');
  check(!await studio.locator('[data-action="pause"]').isVisible(), 'Daily run exposed live playback controls.');
  const before = await mirror.getAttribute('data-time-seconds');
  // Short deliberate observation window checks that the scheduler stays asleep.
  await page.waitForTimeout(2000);
  check(await mirror.getAttribute('data-time-seconds') === before, 'Time advanced while choosing a plan.');
  const target = index => studio.locator(`.studio-map-wrap [data-channel="${index}"]`);
  await target(210).click();
  check(await studio.locator('[data-plan-list] li').count() === 0, 'Inspecting a channel changed the draft.');
  await target(210).dblclick();
  // Inspection must not move the board between the two clicks of a fuel order.
  await target(211).scrollIntoViewIfNeeded();
  const beforeInspect = await target(211).boundingBox();
  const beforeScroll = await studio.evaluate(e => e.scrollTop);
  await target(211).click();
  const afterInspect = await target(211).boundingBox();
  const afterScroll = await studio.evaluate(e => e.scrollTop);
  // Focus scrolling and selection outlines may change viewport bounds. The
  // channel's center in workspace coordinates must remain fixed.
  check(beforeInspect && afterInspect && Math.abs(beforeInspect.y + beforeInspect.height / 2 + beforeScroll - afterInspect.y - afterInspect.height / 2 - afterScroll) < 1,
    `Channel inspection moved the input target: ${JSON.stringify({ beforeInspect, afterInspect, beforeScroll, afterScroll })}`);
  await target(211).dblclick();
  check(await studio.locator('[data-plan-list] li').count() === 2, 'Two-channel plan was not retained.');
  await target(210).dblclick();
  check(await studio.locator('[data-plan-list] li').count() === 1, 'Double-click did not remove exactly one order.');
  await target(210).dblclick();
  await studio.locator('.fuel-sheet-inspect[data-plan-channel="211"]').click();
  check(await target(211).getAttribute('aria-pressed') === 'true', 'Sheet row did not inspect its core channel.');
  check(await mirror.getAttribute('data-fuel-consumed') === '0', 'Planning consumed fuel.');
  if (process.env.PLAYTEST_CAPTURE_DIR) {
    await studio.locator('[data-field="map"]').press('Escape');
    await page.screenshot({ path: `${process.env.PLAYTEST_CAPTURE_DIR}/studio-fuel-plan.png` });
  }
  const durations = [];
  for (let day = 1; day <= 2; day++) {
    const started = Date.now();
    await studio.locator('[data-action="commit-day"]').click();
    const progressTimer = process.env.PLAYTEST_PROGRESS ? setInterval(() => studio.locator('[data-day-pending]').textContent().then(text => console.log(`Day ${day}: ${text}`)).catch(() => undefined), 15_000) : null;
    try { await idle(); } finally { if (progressTimer) clearInterval(progressTimer); }
    durations.push(Date.now() - started);
    const outcome = studio.locator('.studio-day-dialog');
    await outcome.waitFor({ state: 'visible' });
    check(await outcome.getAttribute('data-phase') === 'success', 'Surviving day did not flash success.');
    check((await outcome.locator('[data-calculation-score]').textContent()).includes('Total score'), 'Success score is missing.');
    if (process.env.PLAYTEST_CAPTURE_DIR) await page.screenshot({ path: `${process.env.PLAYTEST_CAPTURE_DIR}/day-${day}-success.png` });
    await outcome.getByRole('button', { name: 'Continue', exact: true }).click();
    check(await mirror.getAttribute('data-time-seconds') === String(day * 86400), `Day ${day} did not advance exactly one day.`);
    check(await mirror.getAttribute('data-completed-days') === String(day), `Day ${day} count is incorrect.`);
    await page.waitForTimeout(2000);
    check(await mirror.getAttribute('data-time-seconds') === String(day * 86400), 'Time kept ticking after completing a turn.');
    check(await mirror.getAttribute('data-fuel-consumed') === '16', 'Daily fuel accounting differs from two eight-bundle moves.');
    check(await studio.locator('[data-plan-list] li').count() === 0, 'Accepted day did not clear the plan.');
    check(await studio.locator('[data-day-summary]').textContent() !== '', 'Daily result is missing.');
    if (day === 1) check(await studio.locator('[data-day-moves] > li').count() === 2, 'Daily report lost a fuel movement.');
  }
  await page.setViewportSize({ width: 320, height: 740 });
  check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'Daily view overflows at 320px.');
  await page.setViewportSize({ width: 1280, height: 900 });
  return { default: 'daily-turn', multiChannelDay: 'passed', emptyDay: 'passed', frozenPlanning: 'passed', narrowLayout: 'passed', durationsMs: durations };
}
