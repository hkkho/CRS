// @vitest-environment node
import { afterEach, expect, test } from 'vitest';
import { mkdtemp, mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { runInNewContext } from 'node:vm';
import { offlinePlugin } from '../offlinePlugin.ts';

const directories = [];
afterEach(async () => {
  await Promise.all(directories.splice(0).map(path => rm(path, { recursive: true, force: true })));
});

test('an offline update installs current HTML with its matching script despite stale HTTP cache', async () => {
  const root = await mkdtemp(join(tmpdir(), 'candu-offline-update-'));
  directories.push(root);
  await mkdir(join(root, 'assets'));
  const currentHtml = '<script src="/CRS/assets/current.js"></script>';
  await writeFile(join(root, 'index.html'), currentHtml);
  await writeFile(join(root, 'assets/current.js'), 'current build');
  const plugin = offlinePlugin();
  plugin.configResolved({ base: '/CRS/', build: { outDir: root } });
  await plugin.closeBundle();

  const handlers = new Map();
  const installed = new Map();
  const network = new Map([
    ['/CRS/index.html', currentHtml],
    ['/CRS/assets/current.js', 'current build'],
  ]);
  class ScopedRequest extends Request {
    constructor(url, options) { super(new URL(url, 'https://example.test'), options); }
  }
  runInNewContext(await readFile(join(root, 'sw.js'), 'utf8'), {
    Request: ScopedRequest,
    self: { addEventListener: (name, handler) => handlers.set(name, handler) },
    caches: {
      open: async () => ({
        addAll: async requests => {
          for (const input of requests) {
            const request = typeof input === 'string' ? new ScopedRequest(input) : input;
            const path = new URL(request.url).pathname;
            // The old HTTP-cached HTML references an asset removed by deployment.
            installed.set(path, path.endsWith('index.html') && request.cache !== 'reload'
              ? '<script src="/CRS/assets/removed.js"></script>' : network.get(path));
          }
        },
      }),
      delete: async () => true,
    },
  });
  let completion;
  handlers.get('install')({ waitUntil: promise => { completion = promise; } });
  await completion;
  expect(installed.get('/CRS/index.html')).toBe(currentHtml);
  expect(installed.get('/CRS/assets/current.js')).toBe('current build');
});

test('a waiting update refuses to replace another open game tab', async () => {
  const root = await mkdtemp(join(tmpdir(), 'candu-offline-tabs-')); directories.push(root);
  await writeFile(join(root, 'index.html'), 'current');
  const plugin = offlinePlugin(); plugin.configResolved({ base: '/CRS/', build: { outDir: root } }); await plugin.closeBundle();
  const handlers = new Map(), replies = [];
  let windows = [{ url: 'https://example.test/CRS/' }, { url: 'https://example.test/CRS/?seed=2' }], activated = 0;
  runInNewContext(await readFile(join(root, 'sw.js'), 'utf8'), {
    URL,
    self: { addEventListener: (name, handler) => handlers.set(name, handler), clients: { matchAll: async () => windows }, skipWaiting: async () => { activated++; } },
  });
  const send = async () => {
    let completion;
    handlers.get('message')({ data: { type: 'CANDU_ACTIVATE_UPDATE' }, ports: [{ postMessage: value => replies.push(value) }], waitUntil: promise => { completion = promise; } });
    await completion;
  };
  await send(); expect(activated).toBe(0); expect(replies[0].status).toBe('other-tabs'); expect(replies[0].otherTabs).toBe(1);
  windows = [windows[0]]; await send(); expect(activated).toBe(0); expect(replies[1].status).toBe('ready');
});
