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
const timeout = 180_000;
const game = page.locator('.reactor-demo');
try {
  await page.goto(url);
  console.log("Opening the first build.");
  await page.locator('.launcher-setup summary').click();
  await page.locator('[data-field=seed]').fill('1001');
  await page.locator('[data-action=demo]').click();
  await page.locator('[data-game-start]').click({ timeout });
  await page.waitForFunction(() => !!navigator.serviceWorker.controller, undefined, { timeout });
  await page.locator('.reactor-demo [data-channel="210"]').dblclick();
  await page.waitForFunction(() => document.querySelector('.reactor-demo')?.dataset.fuelConsumed === '8', undefined, { timeout });
  console.log('First build has confirmed a fuel move.');
  const second = await context.newPage();
  await second.goto(url);
  served = next;
  await page.evaluate(async () => { const registration = await navigator.serviceWorker.getRegistration(); await registration.update(); });
  await page.getByText(/Close 1 other game tab/).waitFor({ state: 'visible', timeout });
  check(await page.locator('html').getAttribute('data-upgrade-fixture') === null, 'Updated while another tab was open.');
  await page.bringToFront();
  await game.evaluate(element => {
    element.querySelector('[data-channel="211"]').dispatchEvent(new MouseEvent('dblclick', { bubbles: true }));
    element.querySelector('[data-action=pause]').click();
  });
  check((await game.locator('[data-fuel-queue]').textContent()).startsWith('1 waiting ·'), 'Expected a waiting fuel order.');
  await page.waitForFunction(() => document.querySelector('.reactor-demo')?.dataset.solving === 'false', undefined, { timeout });
  const before = Number(await game.getAttribute('data-time-seconds'));
  await second.close();
  console.log("The other tab closed. Waiting for automatic reload and restore.");

  await page.waitForFunction(() => document.documentElement.dataset.upgradeFixture === 'candidate', undefined, { timeout });
  await page.getByText(/Build 7777777/).waitFor({ state: 'visible', timeout });
  await page.locator('[data-game-start]').waitFor({ state: 'visible', timeout });
  check(await game.getAttribute('data-running') === 'false', 'Playback resumed before Start.');
  check(Number(await game.getAttribute('data-time-seconds')) >= before, 'Restored simulation time went backwards.');
  check(await game.getAttribute('data-fuel-consumed') === '8', 'Confirmed fuel move did not survive the update.');
  check(await game.locator('[data-fuel-queue]').textContent() === '1 waiting · 0 submitted', 'Waiting order did not survive the update.');
  const result = { status: 'passed', automaticUpdate: true, otherTabProtected: true, updatedBuild: candidateSha, restoredFuel: 8, restoredWaitingOrders: 1 };
  await writeFile(join(root, 'result.json'), JSON.stringify(result, null, 2)); console.log(JSON.stringify(result));
} catch (error) {
  await page.screenshot({ path: join(root, 'failure.png'), fullPage: true, timeout: 5000 }).catch(() => {});
  console.log(await page.locator('.game-update').textContent().catch(() => 'No version view'));
  console.log(await page.evaluate(async () => {
    const registration = await navigator.serviceWorker?.getRegistration();
    const entries = [];
    for (const key of await (globalThis.caches?.keys() ?? [])) {
      const cache = await caches.open(key), response = await cache.match('/CRS/wasm/build-info.json');
      entries.push({ key, build: response ? (await response.json()).gitCommitSha : null });
    }
    return { active: registration?.active?.state, waiting: registration?.waiting?.state, controller: navigator.serviceWorker?.controller?.state, entries };
  }));
  throw error;
} finally { await browser.close(); await new Promise(resolve => server.close(resolve)); }
