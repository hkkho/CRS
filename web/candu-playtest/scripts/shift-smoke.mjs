import { verifyZoneLayout } from "./zone-layout-smoke.mjs";

/** Complete a short challenge using only visible controls and shared Game results. */
export async function verifyShift(page) {
  const check = (condition, message) => { if (!condition) throw new Error(message); };
  const studio = page.locator('.reactor-studio');
  const idle = () => page.waitForFunction(() => document.querySelector('.reactor-studio')?.getAttribute('aria-busy') === 'false', undefined, { timeout: 120_000 });
  await studio.locator('[data-action="challenge"]').click();
  await page.waitForFunction(() => document.querySelector('[data-field="objective-title"]')?.textContent?.includes('One useful fuel day'), undefined, { timeout: 120_000 });
  await idle();
  check(await studio.locator('[data-field="pause"]').getAttribute('aria-label') === 'Resume simulation', 'Challenge should start paused for inspection.');
  const seed = (await studio.locator('[data-field="objective-title"]').textContent()).split('SEED ')[1];
  check((await studio.locator('[data-field="objective"]').textContent()).includes('6 MWd/kg'), 'Useful discharge threshold is missing.');
  await studio.locator('[data-tab="reactor"]').click();
  await studio.locator('[data-size="8"]').click();
  let useful = 0;
  for (let move = 0; move < 4 && useful < 8; move++) {
    await studio.locator('[data-action="oldest"]').click();
    await studio.locator('[data-size="8"]').click();
    // The authored aged snapshot retains older fuel at the opposite end to the default draft.
    await studio.locator('[data-action="direction"]').click();
    const beforeStock = await studio.locator('[data-field="stock"]').textContent();
    await studio.locator('[data-action="refuel"]').click();
    await page.waitForFunction(before => document.querySelector('[data-field="stock"]')?.textContent !== before, beforeStock, { timeout: 120_000 });
    await idle();
    useful = Number.parseInt(await studio.locator('[data-field="objective-progress"]').textContent(), 10);
  }
  check(useful >= 8, 'Four aged-fuel moves did not satisfy the useful-discharge objective.');
  await studio.locator('details.studio-controls > summary').click();
  const advanceDay = async () => {
    for (let hour = 0; hour < 24 && await studio.locator('[data-field="ending"]').isHidden(); hour++) {
      const beforeTime = await studio.locator('[data-field="time"]').textContent();
      await studio.locator('[data-action="step"]').click();
      await page.waitForFunction(before => document.querySelector('[data-field="time"]')?.textContent !== before, beforeTime, { timeout: 120_000 });
      await idle();
    }
  };
  await advanceDay();
  check(await studio.locator('[data-field="ending"]').isVisible(), 'Challenge did not end after 24 hours.');
  check((await studio.locator('[data-field="ending-reward"]').textContent()).includes('Earned:'), 'Completed challenge did not earn its badge.');
  check((await studio.locator('[data-field="energy"]').textContent()).includes('MWh electric (estimate)'), 'Energy report is missing units.');
  check(await studio.locator('[data-action="step"]').isDisabled(), 'Completed shift permits further time steps.');
  for (const width of [1600, 720]) {
    await page.setViewportSize({ width, height: 900 });
    check(await studio.evaluate(element => element.scrollWidth <= element.clientWidth), `Shift report overflows at ${width}px.`);
    await studio.evaluate(element => { element.scrollTop = 0; });
    if (process.env.PLAYTEST_CAPTURE_DIR) await page.screenshot({ path: `${process.env.PLAYTEST_CAPTURE_DIR}/shift-report-${width}.png` });
  }
  await studio.locator('[data-action="retry"]').click();
  await page.waitForFunction(() => document.querySelector('[data-field="ending"]')?.hidden === true && document.querySelector('[data-field="stock"]')?.textContent === '128', undefined, { timeout: 120_000 });
  await idle();
  check((await studio.locator('[data-field="objective-title"]').textContent()).endsWith(`SEED ${seed}`), 'Retry changed the seed.');
  check((await studio.locator('[data-field="objective-progress"]').textContent()).startsWith('0 / 8'), 'Retry retained useful discharge.');
  // An accepted geometry edit keeps the playable objective, but excludes standard rewards.
  await page.setViewportSize({ width: 1600, height: 900 });
  await studio.locator('[data-action="designer"]').click();
  await studio.waitFor({ state: "detached" });
  await verifyZoneLayout(page);
  await page.keyboard.press("Escape");
  await studio.waitFor();
  check(await studio.getAttribute("data-run-kind") === "modified-sandbox", "Geometry edit did not mark the run modified.");
  check((await studio.locator('[data-field="provenance"]').textContent()).includes("Zone geometry edited"), "Modification reason is missing.");
  useful = 0;
  for (let move = 0; move < 4 && useful < 8; move++) {
    await studio.locator('[data-action="oldest"]').click();
    await studio.locator('[data-size="8"]').click();
    await studio.locator('[data-action="direction"]').click();
    await studio.locator('[data-action="refuel"]').click(); await idle();
    useful = Number.parseInt(await studio.locator('[data-field="objective-progress"]').textContent(), 10);
  }
  check(useful >= 8, "Modified objective playthrough did not discharge useful fuel.");
  await studio.locator('details.studio-controls > summary').click();
  await advanceDay();
  check((await studio.locator('[data-field="ending-title"]').textContent()).includes("Sandbox"), "Modified report looks standard.");
  check((await studio.locator('[data-field="ending-provenance"]').textContent()).includes("Excluded from standard challenge"), "Modified report exclusion is missing.");
  check(!(await studio.locator('[data-field="ending-reward"]').textContent()).includes("Earned:"), "Modified run earned a standard badge.");
  if (process.env.PLAYTEST_CAPTURE_DIR) {
    await studio.evaluate(element => { element.scrollTop = 0; });
    await page.screenshot({ path: `${process.env.PLAYTEST_CAPTURE_DIR}/sandbox-report-1600.png` });
    await page.setViewportSize({ width: 720, height: 900 });
    check(await studio.evaluate(element => element.scrollWidth <= element.clientWidth), "Sandbox report overflows at 720px.");
    await page.screenshot({ path: `${process.env.PLAYTEST_CAPTURE_DIR}/sandbox-report-720.png` });
  }
  await studio.locator('[data-action="retry"]').click(); await idle();
  check(await studio.getAttribute("data-run-kind") === "standard-challenge", "Retry retained sandbox provenance.");
  // A third day without refuelling must end with the objective missed.
  await advanceDay();
  check((await studio.locator('[data-field="ending-title"]').textContent()) === 'Objective missed', 'Empty retry incorrectly met the challenge.');
  check(!(await studio.locator('[data-field="ending-reward"]').textContent()).includes('Earned:'), 'Empty retry awarded a badge.');
  await studio.locator('[data-action="new-seed"]').click();
  await page.waitForFunction(nextSeed => document.querySelector('[data-field="objective-title"]')?.textContent?.endsWith(`SEED ${nextSeed}`), (Number(seed) + 1) >>> 0, { timeout: 120_000 });
  await idle();
  await studio.locator('[data-action="challenge"]').click();
  await page.waitForFunction(() => document.querySelector('[data-field="objective-title"]')?.textContent?.includes('Free practice'), undefined, { timeout: 120_000 });
  return { challenge: 'completed', usefulBundles: useful, badge: 'earned', modifiedChallenge: 'sandbox/no standard badge', emptyRetry: 'missed', seededRetries: 'ok', practiceReturn: 'ok' };
}
