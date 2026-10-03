import { chromium } from 'playwright';
import { build } from 'esbuild';
import { writeFile } from 'node:fs/promises';
const compiled = await build({ stdin: { contents: `import { StudioView } from './src/studio/StudioView'; import { ReactorHistory } from './src/studio/ReactorHistory'; window.renderFixture = { StudioView, ReactorHistory };`, resolveDir: process.cwd() }, bundle: true, write: false, loader: { '.css': 'empty' }, format: 'iife' });
const browser = await chromium.launch({ headless: true });
try {
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  await page.goto(process.argv[2] ?? 'http://127.0.0.1:4173');
  await page.waitForFunction(() => document.querySelector('[data-action="begin"]')?.getAttribute('aria-disabled') === 'false');
  await page.addScriptTag({ content: compiled.outputFiles[0].text });
  const cdp = await page.context().newCDPSession(page);
  const trace = [];
  cdp.on('Tracing.dataCollected', event => trace.push(...event.value));
  await cdp.send('Tracing.start', { categories: 'devtools.timeline,blink.user_timing', transferMode: 'ReportEvents' });
  const result = await page.evaluate(async () => {
    // The production WASM supplies the 380-channel fixture; history timestamps
    // below are synthetic presentation stress data, never simulation fallback.
    await import('/wasm/main.mjs');
    const start = performance.now();
    const raw = await window.canduPlaytestWasm.initialize(JSON.stringify({ protocol: 'candu-playtest-v2', mode: 'play' }));
    const wasmMs = performance.now() - start, parseStart = performance.now();
    const snapshot = JSON.parse(raw).snapshot;
    const parseMs = performance.now() - parseStart;
    const { StudioView, ReactorHistory } = window.renderFixture;
    document.querySelector('#game-root').replaceChildren();
    const history = new ReactorHistory();
    for (let i = 0; i < 4096; i++) history.record({ ...snapshot, simulationTimeSeconds: i * 60 });
    let listener;
    const status = { source: 'wasm', title: 'Ready', detail: 'Ready', isWasmAvailable: true, capabilities: [] };
    const session = { snapshot, history, status, isPending: false, dispatch: async () => {}, subscribe: callback => { listener = callback; callback({ snapshot, status, pending: false, response: null, error: null }); return () => {}; } };
    const view = new StudioView(session, document.querySelector('#game-root'), () => {});
    let lastSnapshot = snapshot;
    const emit = () => { const changeKind = session.snapshot === lastSnapshot ? 'status' : 'snapshot'; lastSnapshot = session.snapshot; listener({ snapshot: session.snapshot, status, pending: session.isPending, response: null, error: null, changeKind }); };
    const rows = [];
    for (const kind of ['pending', 'compact', 'selection', 'history']) {
      performance.mark(`render-profile-${kind}-start`);
      view.element.querySelector(`[data-tab="${kind === 'history' ? 'zones' : 'reactor'}"]`).click();
      const times = [], frames = [], mutations = [];
      const observer = new MutationObserver(() => {}); observer.observe(view.element, { attributes: true, childList: true, characterData: true, subtree: true });
      for (let i = 0; i < 30; i++) {
        observer.takeRecords();
        const before = performance.now();
        if (kind === 'pending') { session.isPending = !session.isPending; emit(); }
        if (kind === 'compact') { session.snapshot = { ...session.snapshot, scoreTotal: i, simulationTimeSeconds: 300000 + i }; emit(); }
        if (kind === 'selection') view.element.querySelector(`rect[data-channel="${i % 2 ? 210 : 211}"]`).dispatchEvent(new MouseEvent('click', { bubbles: true }));
        if (kind === 'history') { session.snapshot = { ...session.snapshot, simulationTimeSeconds: 400000 + i }; history.record(session.snapshot); emit(); }
        times.push(performance.now() - before); mutations.push(observer.takeRecords().length);
        // Double rAF is an end-to-frame opportunity, not isolated GPU paint time.
        await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
        frames.push(performance.now() - before);
      }
      observer.disconnect();
      const stats = values => { const sorted = [...values].sort((a,b) => a-b); return { p50: sorted[15], p95: sorted[28] }; };
      rows.push({ kind, renderMs: stats(times), frameOpportunityMs: stats(frames), mutations: stats(mutations) });
      performance.mark(`render-profile-${kind}-end`);
    }
    view.destroy();
    return { fixture: 'Production WASM 380 channels; synthetic 4096-sample presentation history', channels: snapshot.core.channels.length, history: history.samples.length, wasmCallMs: wasmMs, parseMs, payloadBytes: new TextEncoder().encode(raw).length, rows };
  });
  const complete = new Promise(resolve => cdp.once('Tracing.tracingComplete', resolve));
  await cdp.send('Tracing.end'); await complete;
  for (const row of result.rows) {
    const start = trace.find(event => event.name === `render-profile-${row.kind}-start`)?.ts;
    const end = trace.find(event => event.name === `render-profile-${row.kind}-end`)?.ts;
    if (start == null || end == null) throw new Error('Missing rendering trace markers.');
    const sum = name => trace.filter(event => event.name === name && event.ph === 'X' && event.ts >= start && event.ts <= end).reduce((total, event) => total + (event.dur ?? 0) / 1000, 0);
    row.cpuTimelineTotalsMs = { paint: sum('Paint'), layout: sum('Layout'), style: sum('UpdateLayoutTree'), updates: 30 };
  }
  if (process.argv[3]) await writeFile(process.argv[3], JSON.stringify(result, null, 2));
  console.log(JSON.stringify(result));
} finally { await browser.close(); }
