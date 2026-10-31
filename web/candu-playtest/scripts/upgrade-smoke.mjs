import { chromium } from 'playwright';
import { createServer } from 'node:http';
import { cp, mkdir, readFile, writeFile } from 'node:fs/promises';
import { resolve, extname, join } from 'node:path';
import { offlinePlugin } from '../offlinePlugin.ts';

// Two real production app versions, sharing unchanged authoritative physics.
// The candidate has new HTML/JS identities and unchanged authoritative physics.
const root = resolve(process.argv[2] ?? '../../tmp/game-upgrade');
const first = join(root, 'first'), next = join(root, 'next');
await mkdir(root, { recursive: true });
await cp('dist-pages', first, { recursive: true }); await cp('dist-pages', next, { recursive: true });
const candidateSha = '7'.repeat(40);
const info = JSON.parse(await readFile(join(next, 'wasm/build-info.json'), 'utf8'));
info.gitCommitSha = candidateSha; await writeFile(join(next, 'wasm/build-info.json'), JSON.stringify(info));
const html = await readFile(join(next, 'index.html'), 'utf8');
const script = html.match(/<script[^>]*src="([^"]+)"/)[1];
const candidateScript = script.replace(/\.js$/, '-upgrade.js');
await writeFile(join(next, candidateScript.replace(/^\/CRS\//, '')),
  await readFile(join(next, script.replace(/^\/CRS\//, '')), 'utf8') + '\ndocument.documentElement.dataset.upgradeFixture = "candidate";\n');
await writeFile(join(next, 'index.html'), html.replace(script, candidateScript));
const plugin = offlinePlugin(); plugin.configResolved({ base: '/CRS/', build: { outDir: next } }); await plugin.closeBundle();
let served = first;
const server = createServer(async (request, response) => {
  try {
    const pathname = decodeURIComponent(new URL(request.url, 'http://localhost').pathname);
    const file = resolve(served, pathname.replace(/^\/CRS\//, '') || 'index.html');
    if (!file.startsWith(served + '/') && !file.startsWith(served + '\\')) throw new Error('Invalid path');
    const body = await readFile(file);
    const types = { '.html': 'text/html', '.js': 'text/javascript', '.mjs': 'text/javascript', '.css': 'text/css', '.json': 'application/json', '.wasm': 'application/wasm' };
    response.writeHead(200, { 'Content-Type': types[extname(file)] ?? 'application/octet-stream', 'Cache-Control': 'no-cache' }); response.end(body);
  } catch { response.writeHead(404); response.end(); }
});
await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
const url = `http://127.0.0.1:${server.address().port}/CRS/`;
const browser = await chromium.launch({ headless: true });
const context = await browser.newContext(), page = await context.newPage();
const check = (value, message) => { if (!value) throw new Error(message); };
const idle = () => page.waitForFunction(() => document.querySelector('.reactor-studio')?.getAttribute('aria-busy') === 'false', undefined, { timeout: 900_000 });
try {
  await page.goto(`${url}?pacing=real-time`);
  console.log('Loading first production build');
  await page.waitForFunction(() => document.querySelector('#status-mirror')?.getAttribute('data-pacing') === 'daily-turn' && document.querySelector('#status-mirror')?.textContent?.includes('live reactor online'), undefined, { timeout: 120_000 });
  await page.waitForFunction(() => !!navigator.serviceWorker.controller, undefined, { timeout: 120_000 });
  await page.getByRole('button', { name: 'Turn based', exact: true }).click();
  await page.locator('[data-game-start]').click({ timeout: 180000 }); await idle();
  await page.locator('[data-action="commit-day"]').click(); await idle();
  await page.locator('.studio-day-dialog').getByRole('button', { name: 'Continue', exact: true }).click();
  await page.locator('[data-action="refuel"]').click();
  const second = await context.newPage(); await second.goto(url);
  console.log('Installing candidate while two game tabs remain open');
  served = next;
  await page.evaluate(async () => { const registration = await navigator.serviceWorker.getRegistration(); await registration.update(); });
  const update = page.getByRole('button', { name: 'Save locally & update', exact: true });
  await update.waitFor({ state: 'visible', timeout: 120_000 });
  await update.click();
  await page.getByText(/Close 1 other game tab/).waitFor({ state: 'visible', timeout: 30_000 });
  console.log('Other tab protected and current run saved');
  check(await page.locator('#status-mirror').getAttribute('data-time-seconds') === '86400', 'Blocked update changed the current day.');
  await second.close();
  await Promise.all([page.waitForEvent('domcontentloaded'), update.click()]);
  console.log('Reloading candidate');
  await page.waitForFunction(() => document.querySelector('.game-update')?.textContent?.includes('Build 7777777'), undefined, { timeout: 120_000 });
  check(await page.locator('html').getAttribute('data-upgrade-fixture') === 'candidate', 'The update did not load the new HTML and JavaScript.');
  await page.waitForFunction(() => document.querySelector('#status-mirror')?.textContent?.includes('live reactor online'), undefined, { timeout: 120_000 });
  await page.locator('.player-toggle').click();
  await page.waitForFunction(() => document.querySelector('.player-panel')?.getAttribute('aria-busy') === 'false');
  await page.locator('[data-source="This browser"] button').first().waitFor({ state: 'visible', timeout: 120_000 });
  await page.locator('[data-source="This browser"] button').first().click();
  await page.waitForFunction(() => document.querySelector('.player-panel')?.getAttribute('aria-busy') === 'false', undefined, { timeout: 120_000 });
  check(!await page.locator('.player-panel').evaluate(element => element.open), `Restore failed: ${await page.locator('[data-message]').textContent()}`);
  await idle();
  check(await page.locator('#status-mirror').getAttribute('data-time-seconds') === '86400', 'Saved day did not survive the update.');
  check(await page.locator('[data-plan-list] > li').count() === 1, 'Draft did not survive the update.');
  const result = { status: 'passed', updatePrompt: true, otherTabProtected: true, newFrontendLoaded: true, updatedBuild: candidateSha, savedDay: 1, restoredDraft: 1 };
  await writeFile(join(root, 'result.json'), JSON.stringify(result, null, 2)); console.log(JSON.stringify(result));
} catch (error) {
  await page.screenshot({ path: join(root, 'failure.png'), fullPage: true });
  console.log(await page.locator('.game-update').textContent().catch(() => 'No version view'));
  console.log(await page.evaluate(async () => {
    const registration = await navigator.serviceWorker.getRegistration();
    const entries = [];
    for (const key of await caches.keys()) {
      const cache = await caches.open(key), response = await cache.match('/CRS/wasm/build-info.json');
      entries.push({ key, build: response ? (await response.json()).gitCommitSha : null });
    }
    return { active: registration?.active?.state, waiting: registration?.waiting?.state, controller: navigator.serviceWorker.controller?.state, entries };
  }));
  throw error;
} finally { await browser.close(); await new Promise(resolve => server.close(resolve)); }
