import { chromium } from 'playwright';
import { build } from 'esbuild';
import { readdir, writeFile } from 'node:fs/promises';
const base = process.argv[2] ?? 'http://127.0.0.1:4173';
const worker = (await readdir('dist/assets')).find(name => /^wasmWorker-.*\.js$/.test(name));
if (!worker) throw new Error('Build the production frontend first.');
const bundle = await build({ stdin: { contents: `import { WorkerProtocolBridge } from './src/bridge'; import { BridgeSessionController } from './src/sessionController'; import { createRefuelDraft } from './src/commandState'; window.paceFixture = { WorkerProtocolBridge, BridgeSessionController, createRefuelDraft };`, resolveDir: process.cwd() }, bundle: true, write: false, format: 'iife', define: { 'import.meta.url': JSON.stringify(base) } });
const browser = await chromium.launch({ headless: true });
try {
  const page = await browser.newPage();
  const errors = []; page.on('pageerror', error => errors.push(error.message));
  await page.goto(base);
  await page.waitForFunction(() => document.querySelector('[data-action="begin"]')?.getAttribute('aria-disabled') === 'false');
  await page.addScriptTag({ content: bundle.outputFiles[0].text });
  const report = await page.evaluate(async workerUrl => {
    const { WorkerProtocolBridge, BridgeSessionController, createRefuelDraft } = window.paceFixture;
    const bridge = new WorkerProtocolBridge({ createWorker: () => new Worker(workerUrl, { type: 'module' }) });
    const loadingStart = performance.now(); await bridge.ready;
    const moduleReadyMs = performance.now() - loadingStart;
    const measurements = [];
    const measure = async (label, command, full = false) => {
      const start = performance.now();
      let response;
      try { response = await bridge.dispatch(command, { responseMode: full ? 'full' : 'compact' }); }
      catch (error) { throw new Error(`${label}: ${error.message}`); }
      if (!response.accepted) throw new Error(`${label}: ${response.message}`);
      const totalMs = performance.now() - start;
      const metric = bridge.getTransportMetrics().at(-1);
      measurements.push({ label, accepted: response.accepted, totalMs, ...metric,
        queueTransferEncodingMs: Math.max(0, totalMs - metric.wasmCallDurationMs - metric.jsonParseMaterializationDurationMs),
        publishedSolveDiagnosticMs: response.snapshot.diagnostics?.convergence?.lastSolveMilliseconds ?? null });
      return response;
    };
    const initStart = performance.now(); await bridge.initialize('play');
    const initializationMs = performance.now() - initStart;
    const initializationMetric = bridge.getTransportMetrics().at(-1);
    for (const modeId of ['1x', '10x', '60x']) {
      await measure(`request-${modeId}`, { type: 'set-playback-mode', modeId });
      for (let i = 0; i < (modeId === '1x' ? 20 : 6); i++) await measure(`tick-${modeId}`, { type: 'advance', wallMilliseconds: 100 });
      await measure(`pause-${modeId}`, { type: 'pause' });
    }
    for (let i = 0; i < 3; i++) {
      await measure('refuel', { type: 'commit-refuel', request: { ...createRefuelDraft(bridge.getSnapshot().core.channels.find(c => c.channelIndex === 210)), directionId: i % 2 ? 'toward-end-a' : 'toward-end-b', shiftCount: 4 } });
      await measure('shape-solve', { type: 'solve' }, true);
    }
    // Exercise actual bounded scheduler and foreground queue against this worker.
    const lifecycle = { get status() { return bridge.status; }, getSnapshot: () => bridge.getSnapshot(), dispatch: bridge.dispatch.bind(bridge), initializeMode: bridge.initialize.bind(bridge), subscribe: () => () => {}, dispose: () => bridge.dispose() };
    await bridge.dispatch({ type: 'set-playback-mode', modeId: '60x' });
    const controller = new BridgeSessionController(lifecycle);
    let latest, tickStarted, observedAfterTick = null;
    const started = new Promise(resolve => { tickStarted = resolve; });
    controller.subscribe(update => { latest = update; if (update.response?.accepted && update.response.command.type === 'advance') observedAfterTick = update.pace.simulatedMinutesPerSecond; if (update.pace?.solving) tickStarted(); });
    controller.startShift(); await started;
    // Queue pause while a clock tick is solving. No subsequent tick may overtake it.
    const pauseStart = performance.now(); await controller.dispatch({ type: 'pause' });
    const queuedPauseMs = performance.now() - pauseStart;
    if (observedAfterTick === null || observedAfterTick <= 0) throw new Error('No achieved progression measured through solver wait.');
    const paused = controller.snapshot.isPaused;
    const sequence = controller.snapshot.sequence;
    await new Promise(resolve => setTimeout(resolve, 400));
    if (!paused || sequence !== controller.snapshot.sequence || latest.pace.simulatedMinutesPerSecond !== null) throw new Error('Foreground pause/backpressure failed.');
    const refuelStart = performance.now();
    const refuel = await controller.dispatch({ type: 'commit-refuel', request: createRefuelDraft(controller.snapshot.core.channels.find(c => c.channelIndex === 211)) });
    const foregroundRefuelMs = performance.now() - refuelStart;
    controller.dispose();
    return { moduleReadyMs, initializationMs, initializationMetric, measurements, foreground: { queuedPauseMs, paused, noExtraTick: true, observedAfterTick, foregroundRefuelMs, refuelAccepted: refuel.accepted } };
  }, new URL(`/assets/${worker}`, base).href);
  if (errors.length) throw new Error(errors.join('\n'));
  if (process.argv[3]) await writeFile(process.argv[3], JSON.stringify(report, null, 2));
  console.log(JSON.stringify(report));
} finally { await browser.close(); }
