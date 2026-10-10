import { chromium } from 'playwright';
import { mkdir, writeFile } from 'node:fs/promises';
import { verifyDailyTurns } from './daily-turn-smoke.mjs';
import { verifyDailyFailure } from './daily-failure-smoke.mjs';
import { verifyRecovery } from './recovery-smoke.mjs';

const url = process.argv[2] ?? process.env.PLAYTEST_URL;
if (!url) throw new Error('Pass the production preview or Pages URL.');
if (process.env.PLAYTEST_CAPTURE_DIR) await mkdir(process.env.PLAYTEST_CAPTURE_DIR, { recursive: true });
const browser = await chromium.launch({ headless: true });
const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
const errors = [];
page.on('pageerror', error => errors.push(error.message));
page.on('console', message => { if (message.type() === 'error') errors.push(message.text()); });
try {
  const buildUrl = new URL('wasm/build-info.json', url);
  buildUrl.searchParams.set('release-check', Date.now().toString());
  const buildResponse = await page.request.get(buildUrl.href);
  if (!buildResponse.ok()) throw new Error(`Build metadata unavailable: ${buildResponse.status()}`);
  const buildInfo = await buildResponse.json();
  const expectedCommit = process.env.PLAYTEST_EXPECTED_COMMIT_SHA;
  if (expectedCommit && buildInfo.gitCommitSha !== expectedCommit) {
    throw new Error(`Expected commit ${expectedCommit}; received ${buildInfo.gitCommitSha}`);
  }
  await page.goto(url);
  await page.waitForFunction(() => document.querySelector('#status-mirror')?.textContent?.includes('live reactor online'), undefined, { timeout: 120_000 });
  await page.locator('.reactor-launcher [data-field="seed"]').fill('1001');
  await page.locator('.reactor-launcher [data-field="shift"]').selectOption('free-practice');
  await page.getByRole('button', { name: 'Use seed & objective', exact: true }).click();
  await page.waitForFunction(() => document.querySelector('.reactor-launcher')?.getAttribute('aria-busy') === 'false', undefined, { timeout: 120_000 });
  await page.getByRole('button', { name: 'Begin shift', exact: true }).click();
  const daily = await verifyDailyTurns(page);
  const loss = await verifyDailyFailure(browser, url);
  const recovery = await verifyRecovery(browser, url);
  if (errors.length) throw new Error(errors.join(' | '));
  const result = { status: 'passed', url, bridge: 'authoritative-csharp-wasm', buildInfo, daily, loss, recovery, errors };
  if (process.env.PLAYTEST_CAPTURE_DIR) await writeFile(`${process.env.PLAYTEST_CAPTURE_DIR}/daily-acceptance.json`, JSON.stringify(result, null, 2));
  console.log(JSON.stringify(result));
} finally { await browser.close(); }
