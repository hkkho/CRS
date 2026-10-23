import { chromium } from 'playwright';
import { mkdir, writeFile } from 'node:fs/promises';

const gameUrl = process.argv[2] ?? process.env.PLAYTEST_URL;
if (!gameUrl) throw new Error('Pass the production preview or Pages game URL.');
const docsUrl = new URL('docs/', gameUrl).href;
const browser = await chromium.launch({ headless: true });
const page = await browser.newPage();
const errors = [];
page.on('pageerror', error => errors.push(error.message));
try {
  const metadata = await page.request.get(new URL('build-info.json', docsUrl).href);
  if (!metadata.ok()) throw new Error(`Documentation metadata unavailable: ${metadata.status()}`);
  const info = await metadata.json();
  if (process.env.PLAYTEST_EXPECTED_COMMIT_SHA && info.sourceRevision !== process.env.PLAYTEST_EXPECTED_COMMIT_SHA)
    throw new Error('Documentation and game do not describe the same source commit.');

  for (const [route, heading] of [
    ['', 'CRS documentation'],
    ['manual/', 'Player manual'],
    ['manual/start/', 'One day at a time'],
    ['theory/', 'Reactor theory'],
    ['theory/numerical-model/', 'The spatial solver and daily approximation'],
    ['01-startup/', '1. Entry points and startup'],
  ]) {
    await page.goto(new URL(route, docsUrl).href);
    await page.getByRole('heading', { name: heading, exact: false, level: 1 }).waitFor();
    const localAssets = await page.locator('link[rel="stylesheet"]').evaluateAll(links => links.map(link => link.href));
    for (const asset of localAssets) {
      if (new URL(asset).origin !== new URL(docsUrl).origin) continue;
      if (!(await page.request.get(asset)).ok()) throw new Error(`Documentation stylesheet unavailable: ${asset}`);
    }
  }
  await page.goto(docsUrl);
  const search = page.getByRole('textbox', { name: 'Search', exact: true });
  await search.fill('refuelling');
  await page.locator('.md-search-result__list a').first().waitFor();

  // The game link keeps the current reactor in its original tab; F1 stays local.
  await page.goto(gameUrl);
  await page.getByRole('button', { name: 'Simulator manual', exact: true }).click();
  const manual = page.locator('dialog.simulator-manual[open]');
  await manual.getByRole('heading', { name: 'One day at a time', exact: true }).waitFor();
  const library = manual.getByRole('link', { name: /Manual, theory & code library/ });
  if (await library.getAttribute('href') !== `${new URL(gameUrl).pathname}docs/manual/`)
    throw new Error('In-game manual does not point at the documentation subpath.');
  if (await library.getAttribute('target') !== '_blank') throw new Error('Documentation navigation would replace the current run.');
  await page.keyboard.press('Escape');
  await manual.waitFor({ state: 'hidden' });
  if (errors.length) throw new Error(errors.join(' | '));
  const result = { status: 'passed', gameUrl, docsUrl, buildInfo: info, routes: 6, search: 'passed', manual: 'shared-markdown', errors };
  if (process.env.PLAYTEST_CAPTURE_DIR) {
    await mkdir(process.env.PLAYTEST_CAPTURE_DIR, { recursive: true });
    await writeFile(`${process.env.PLAYTEST_CAPTURE_DIR}/documentation.json`, JSON.stringify(result, null, 2));
  }
  console.log(JSON.stringify(result));
} finally { await browser.close(); }
