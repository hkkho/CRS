import { chromium } from 'playwright';
import { setTimeout as delay } from 'node:timers/promises';

const gameUrl = new URL(process.env.PLAYTEST_URL);
const expectedCommit = process.env.PLAYTEST_EXPECTED_COMMIT_SHA;
if (!/^[a-f0-9]{40}$/i.test(expectedCommit ?? '')) {
  throw new Error('Set PLAYTEST_EXPECTED_COMMIT_SHA to the full release commit.');
}

const browser = await chromium.launch({ headless: true });
try {
  const page = await browser.newPage();
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));

  async function waitForPublishedCommit(path, key) {
    const url = new URL(path, gameUrl);
    const deadline = Date.now() + 45_000;
    let lastError;
    do {
      try {
        const response = await page.request.get(url.href, {
          headers: { 'Cache-Control': 'no-cache' }, timeout: 5_000,
        });
        if (!response.ok()) throw new Error(`${path} returned HTTP ${response.status()}`);
        const metadata = await response.json();
        if (metadata[key] === expectedCommit) return;
        lastError = new Error(`${path} reports ${metadata[key]}, expected ${expectedCommit}`);
      } catch (error) {
        lastError = error;
      }
      if (Date.now() < deadline) await delay(2_000);
    } while (Date.now() < deadline);
    throw lastError;
  }

  await Promise.all([
    waitForPublishedCommit('wasm/build-info.json', 'gitCommitSha'),
    waitForPublishedCommit('docs/build-info.json', 'sourceRevision'),
  ]);

  await page.goto(new URL('docs/manual/', gameUrl).href);
  await page.getByRole('heading', { name: 'Player manual', level: 1 }).waitFor();
  await page.goto(gameUrl.href);
  await page.locator('.launcher-setup details > summary').click();
  await page.locator('[data-field=seed]').fill('1001');
  await page.locator('[data-action=demo]').click();
  await page.locator('[data-game-start]').click({ timeout: 180_000 });
  await page.locator('.game-update').getByText(`Build ${expectedCommit.slice(0, 7)}`, { exact: false }).waitFor();
  await page.waitForFunction(() => {
    const elapsed = Number(document.querySelector('.reactor-demo')?.dataset.timeSeconds);
    return elapsed >= 43_200;
  }, undefined, { timeout: 180_000 });

  if (errors.length) throw new Error(errors.join('\n'));
  console.log(`Verified Pages release ${expectedCommit}, player manual, and the first gameplay step.`);
} finally {
  await browser.close();
}
