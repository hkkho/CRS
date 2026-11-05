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
  let annotatedPassages = 0;
  let explainedGroups = 0;
  for (const route of ['01-startup/', '02-browser-transport/', '03-daily-turn/', '04-core-physics/', '05-presentation/', '06-saves-lifecycle/']) {
    await page.goto(new URL(route, docsUrl).href);
    const passages = page.locator('.annotated-code');
    const count = await passages.count();
    if (count < 2) throw new Error(`Missing annotated source passages: ${route}`);
    annotatedPassages += count;
    const groups = page.locator('.annotated-step');
    explainedGroups += await groups.count();
    const paired = await groups.evaluateAll(rows => rows.every(row =>
      row.querySelector('.source-line') && row.querySelector('.annotated-explanation p:not(.source-range)')?.textContent.trim()));
    if (!paired) throw new Error(`Source group lacks commentary: ${route}`);
    const sourceLink = await passages.first().locator('.annotated-heading a').getAttribute('href');
    if (!sourceLink?.match(/\/blob\/[^/]+\/.+#L\d+-L\d+$/)) throw new Error(`Missing source line anchors: ${route}`);
  }
  await page.goto(new URL('01-startup/', docsUrl).href);
  for (const width of [1280, 640, 320]) {
    await page.setViewportSize({ width, height: 900 });
    const layout = await page.locator('.annotated-step').first().evaluate(row => {
      const source = row.querySelector('.annotated-source').getBoundingClientRect();
      const explanation = row.querySelector('.annotated-explanation').getBoundingClientRect();
      return { sideBySide: Math.abs(source.top - explanation.top) < 1,
        stacked: explanation.top >= source.bottom - 1,
        overflow: document.documentElement.scrollWidth > innerWidth + 1 };
    });
    if (layout.overflow || (width === 1280 ? !layout.sideBySide : !layout.stacked))
      throw new Error(`Annotated source layout failed at ${width}px: ${JSON.stringify(layout)}`);
    if (process.env.PLAYTEST_CAPTURE_DIR) {
      await mkdir(process.env.PLAYTEST_CAPTURE_DIR, { recursive: true });
      await page.locator('#maints-constructs-the-browser-run').scrollIntoViewIfNeeded();
      await page.screenshot({ path: `${process.env.PLAYTEST_CAPTURE_DIR}/annotated-source-${width}.png` });
    }
  }
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.goto(docsUrl);
  const search = page.getByRole('textbox', { name: 'Search', exact: true });
  await search.fill('refuelling');
  await page.locator('.md-search-result__list a').first().waitFor();

  // The shared manual remains published as a documentation route.
  await page.goto(new URL('manual/', docsUrl).href);
  await page.getByRole('heading', { name: 'Player manual', exact: false, level: 1 }).waitFor();
  const gameLink = page.getByRole('link', { name: 'Return to the game', exact: true });
  const gamePath = new URL(gameUrl).pathname;
  if (new URL(await gameLink.getAttribute('href')).pathname !== gamePath)
    throw new Error('The player manual does not link back to the deployed game path.');
  if (errors.length) throw new Error(errors.join(' | '));
  const result = { status: 'passed', gameUrl, docsUrl, buildInfo: info, routes: 6, annotatedPassages, explainedGroups, annotationWidths: [1280, 640, 320], search: 'passed', manual: 'published-route', errors };
  if (process.env.PLAYTEST_CAPTURE_DIR) {
    await mkdir(process.env.PLAYTEST_CAPTURE_DIR, { recursive: true });
    await writeFile(`${process.env.PLAYTEST_CAPTURE_DIR}/documentation.json`, JSON.stringify(result, null, 2));
  }
  console.log(JSON.stringify(result));
} finally { await browser.close(); }
