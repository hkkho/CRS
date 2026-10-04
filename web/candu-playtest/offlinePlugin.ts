import { createHash } from 'node:crypto';
import { readdir, readFile, writeFile } from 'node:fs/promises';
import { join, relative } from 'node:path';
import type { Plugin, ResolvedConfig } from 'vite';

/** Cache one complete, immutable app/WASM build. Never intercept cloud/auth requests. */
export function offlinePlugin(): Plugin {
  let config: ResolvedConfig;
  return {
    name: 'candu-offline', apply: 'build',
    configResolved(value) { config = value; },
    async closeBundle() {
      const root = config.build.outDir;
      const paths: string[] = [];
      async function walk(directory: string): Promise<void> {
        for (const entry of await readdir(directory, { withFileTypes: true })) {
          const path = join(directory, entry.name);
          if (entry.isDirectory()) await walk(path);
          else if (!/\.(map|br|gz)$/.test(entry.name) && entry.name !== 'sw.js' && !entry.name.startsWith('.')) paths.push(path);
        }
      }
      await walk(root); paths.sort();
      const hash = createHash('sha256');
      for (const path of paths) { hash.update(relative(root, path)); hash.update(await readFile(path)); }
      const urls = paths.map(path => `${config.base}${relative(root, path).replaceAll('\\', '/')}`);
      const prefix = `candu-offline-${config.base}-`;
      await writeFile(join(root, 'sw.js'), `
const CACHE = ${JSON.stringify(prefix + hash.digest('hex').slice(0, 20))};
const PREFIX = ${JSON.stringify(prefix)};
const ASSETS = ${JSON.stringify(urls)};
const INDEX = ${JSON.stringify(config.base + 'index.html')};
const BASE = ${JSON.stringify(config.base)};
self.addEventListener('install', event => event.waitUntil((async () => {
  const cache = await caches.open(CACHE);
  try {
    for (let i = 0; i < ASSETS.length; i += 12) await cache.addAll(ASSETS.slice(i, i + 12));
  } catch (error) { await caches.delete(CACHE); throw error; }
})()));
self.addEventListener('activate', event => event.waitUntil((async () => {
  for (const name of await caches.keys()) if (name.startsWith(PREFIX) && name !== CACHE) await caches.delete(name);
  await self.clients.claim();
})()));
self.addEventListener('fetch', event => {
  const url = new URL(event.request.url);
  if (event.request.method !== 'GET' || url.origin !== self.location.origin) return;
  const navigation = event.request.mode === 'navigate' && (url.pathname === BASE || url.pathname === INDEX);
  if (!navigation && !ASSETS.includes(url.pathname)) return;
  event.respondWith((async () => {
    const cache = await caches.open(CACHE);
    return await cache.match(navigation ? INDEX : url.pathname) || fetch(event.request);
  })());
});
`);
    },
  };
}
