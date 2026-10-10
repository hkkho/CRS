const check = (condition, message) => { if (!condition) throw new Error(message); };

/** Normal product controls against the actual worker-hosted shared simulation. */
export async function verifyDailyTurns(page) {
  const studio = page.locator('.reactor-studio');
  const mirror = page.locator('#status-mirror');
  const idle = () => page.waitForFunction(() => document.querySelector('.reactor-studio')?.getAttribute('aria-busy') === 'false', undefined, { timeout: 900_000 });
  await idle();
  check(await mirror.getAttribute('data-pacing') === 'daily-turn', 'New run did not default to daily turns.');
  check(!await studio.locator('[data-action="pause"]').isVisible(), 'Daily run exposed live playback controls.');
  const before = await mirror.getAttribute('data-time-seconds');
  // Short deliberate observation window checks that the scheduler stays asleep.
  await page.waitForTimeout(300);
  check(await mirror.getAttribute('data-time-seconds') === before, 'Time advanced while choosing a plan.');
  for (const index of [210, 211]) {
    await studio.locator(`[data-channel="${index}"]`).first().click();
    await studio.locator('[data-action="refuel"]').click();
  }
  check(await studio.locator('[data-plan-list] li').count() === 2, 'Two-channel plan was not retained.');
  check(await mirror.getAttribute('data-fuel-consumed') === '0', 'Planning consumed fuel.');
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
