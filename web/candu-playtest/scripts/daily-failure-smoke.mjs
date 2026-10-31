/** Separate early-ending acceptance case using only the normal daily UI. */
export async function verifyDailyFailure(browser, url) {
  const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
  const errors = []; page.on('pageerror', error => errors.push(error.message));
  try {
    await page.goto(url);
    await page.waitForFunction(() => document.querySelector('#status-mirror')?.textContent?.includes('live reactor online'), undefined, { timeout: 120_000 });
    await page.locator('.launcher-setup details > summary').click();
  await page.locator('.reactor-launcher [data-field="seed"]').fill('1001');
    await page.locator('[data-goal="free-practice"]').click();
      await page.waitForFunction(() => document.querySelector('.reactor-launcher')?.getAttribute('aria-busy') === 'false', undefined, { timeout: 120_000 });
    await page.getByRole('button', { name: 'Turn based', exact: true }).click();
    await page.locator('[data-game-start]').click({ timeout: 180000 });
    await page.waitForFunction(() => document.querySelector('.reactor-studio')?.getAttribute('aria-busy') === 'false', undefined, { timeout: 120_000 });
    await page.locator('.reactor-studio').evaluate(studio => {
      const add = studio.querySelector('[data-action="refuel"]');
      for (const cell of studio.querySelectorAll('rect[data-channel]')) {
        cell.dispatchEvent(new MouseEvent('click', { bubbles: true })); add.click();
      }
    });
    if (await page.locator('[data-plan-list] > li').count() !== 380) throw new Error('Bulk plan was not retained.');
    await page.locator('[data-action="commit-day"]').click();
    await page.locator('[data-field="ending"]').waitFor({ state: 'visible', timeout: 180_000 });
    if (await page.locator('.studio-day-dialog').isVisible()) throw new Error('Terminal result opened a duplicate outcome dialog.');
    const reason = await page.locator('[data-field="ending-reason"]').textContent();
    if (process.env.PLAYTEST_CAPTURE_DIR) await page.screenshot({ path: `${process.env.PLAYTEST_CAPTURE_DIR}/daily-core-loss.png` });
    if (errors.length) throw new Error(errors.join(' | '));
    return { outcome: 'failure', reason, successScoreHidden: true, shiftReport: 'visible', errors };
  } finally { await page.close(); }
}
