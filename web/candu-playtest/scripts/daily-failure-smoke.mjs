/** Separate early-ending acceptance case using only the normal daily UI. */
export async function verifyDailyFailure(browser, url) {
  const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
  const errors = []; page.on('pageerror', error => errors.push(error.message));
  try {
    await page.goto(url);
    await page.waitForFunction(() => document.querySelector('#status-mirror')?.textContent?.includes('live reactor online'), undefined, { timeout: 120_000 });
    await page.locator('.reactor-launcher [data-field="seed"]').fill('1001');
    await page.locator('.reactor-launcher [data-field="shift"]').selectOption('free-practice');
    await page.getByRole('button', { name: 'Use seed & objective', exact: true }).click();
    await page.waitForFunction(() => document.querySelector('.reactor-launcher')?.getAttribute('aria-busy') === 'false', undefined, { timeout: 120_000 });
    await page.getByRole('button', { name: 'Begin shift', exact: true }).click();
    await page.waitForFunction(() => document.querySelector('.reactor-studio')?.getAttribute('aria-busy') === 'false', undefined, { timeout: 120_000 });
    await page.locator('.reactor-studio').evaluate(studio => {
      const add = studio.querySelector('[data-action="refuel"]');
      for (const cell of studio.querySelectorAll('rect[data-channel]')) {
        cell.dispatchEvent(new MouseEvent('click', { bubbles: true })); add.click();
      }
    });
    if (await page.locator('[data-plan-list] > li').count() !== 380) throw new Error('Bulk plan was not retained.');
    await page.locator('[data-action="commit-day"]').click();
    const outcome = page.locator('.studio-day-dialog[data-phase="failure"]');
    await outcome.waitFor({ state: 'visible', timeout: 180_000 });
    if (await page.locator('[data-calculation-score]').isVisible()) throw new Error('Lost core displayed a success score.');
    const reason = await page.locator('[data-calculation-description]').textContent();
    if (process.env.PLAYTEST_CAPTURE_DIR) await page.screenshot({ path: `${process.env.PLAYTEST_CAPTURE_DIR}/daily-core-loss.png` });
    await outcome.getByRole('button', { name: 'Review the shift report', exact: true }).click();
    if (!await page.locator('[data-field="ending"]').isVisible()) throw new Error('Loss did not open the shift report.');
    if (errors.length) throw new Error(errors.join(' | '));
    return { outcome: 'failure', reason, successScoreHidden: true, shiftReport: 'visible', errors };
  } finally { await page.close(); }
}
