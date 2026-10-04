import { chromium } from 'playwright';
import { mkdir, readFile, writeFile } from 'node:fs/promises';

// Replay an authoritative native playtest's orders through deployed WASM at
// both speeds. No horizon/inventory override is available in this browser path.
const url = process.argv[2] ?? 'https://hkkho.github.io/CRS/';
const replayPath = process.argv[3] ?? '../../tmp/longrun-reserve-pair.json';
const output = process.argv[4] ?? '../../tmp/longrun-browser';
const replay = JSON.parse(await readFile(replayPath, 'utf8'));
const moves = Array.isArray(replay) ? replay : replay.moves;
const seed = Array.isArray(replay) ? 1001 : replay.seed;
const days = Array.isArray(replay) ? 30 : replay.days;
if (!Array.isArray(moves) || !Number.isSafeInteger(seed)) throw new Error('Invalid replay report.');
if (!Array.isArray(replay) && replay.inventoryOverridden) throw new Error('Extra-fuel experiments cannot be replayed through normal browser commands.');
await mkdir(output, { recursive: true });
const browser = await chromium.launch({ headless: true });
const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
const errors = [];
page.on('pageerror', error => errors.push(error.message));
page.on('console', message => { if (message.type() === 'error') errors.push(message.text()); });
try {
  await page.goto(url, { waitUntil: 'networkidle' });
  await page.waitForFunction(() => document.querySelector('#status-mirror')?.textContent?.includes('live reactor online'), undefined, { timeout: 120000 });
  await page.getByRole('button', { name: 'Begin shift', exact: true }).click();
  await page.locator('.reactor-studio [data-action="pause"]').click();
  await page.waitForFunction(() => document.querySelector('#status-mirror')?.textContent?.includes('Paused.'), undefined, { timeout: 120000 });
  await page.screenshot({ path: `${output}/studio-start.png`, fullPage: true });
  const buildInfo = await page.evaluate(async base => (await fetch(new URL('wasm/build-info.json', base))).json(), url);
  await page.evaluate(async base => {
    await import(new URL('wasm/main.mjs', base).href);
    globalThis.longRunApi = globalThis.canduPlaytestWasm;
    globalThis.longRunDispatch = async payload => {
      const result = JSON.parse(await globalThis.longRunApi.dispatchJson(JSON.stringify({ protocol: 'candu-playtest-v2', type: 'command', payload })));
      if (!result.accepted) throw new Error(JSON.stringify(result.diagnostics ?? result));
      return result.snapshot;
    };
    globalThis.longRunRows = {};
  }, url);
  const runs = [];
  for (const speed of ['10x', '1x']) {
    await page.evaluate(async ({ speed, seed }) => {
      const response = JSON.parse(await globalThis.longRunApi.initialize(JSON.stringify({ protocol: 'candu-playtest-v2', mode: 'play', seed })));
      globalThis.longRunSnapshot = response.snapshot ?? response;
      await globalThis.longRunDispatch({ type: 'set-playback-mode', modeId: speed });
      globalThis.longRunRows[speed] = [];
    }, { speed, seed });
    let moveIndex = 0, result;
    // Batch wall-time commands up to the next order/day boundary. Game still
    // evaluates every normal control tick and scheduled half-hour solve.
    for (let tick = 0; tick < days * 48; tick++) {
      result = await page.evaluate(async ({ speed, moves, moveIndex, days }) => {
        let s = globalThis.longRunSnapshot;
        while (moveIndex < moves.length && Math.abs(s.simulationTimeSeconds - moves[moveIndex].day * 86400) < .001) {
          const move = moves[moveIndex++];
          s = await globalThis.longRunDispatch({ type: 'commit-refuel', channelIndex: move.channel, directionId: move.direction, shiftCount: 8, fuelTypeId: 'NAT-U-SYNTHETIC' });
        }
        const nextOrder = moveIndex < moves.length ? moves[moveIndex].day * 86400 : days * 86400;
        const nextDay = (Math.floor(s.simulationTimeSeconds / 86400) + 1) * 86400;
        const simulationSeconds = Math.min(nextOrder, nextDay) - s.simulationTimeSeconds;
        if (!['ended', 'completed'].includes(s.runStatus)) s = await globalThis.longRunDispatch({ type: 'advance', wallMilliseconds: Math.round(simulationSeconds / (speed === '10x' ? 18 : 1.8)) });
        globalThis.longRunSnapshot = s;
        const row = {
          time: s.simulationTimeSeconds, status: s.runStatus, reason: s.runEndReason,
          lzc: s.rrs.averageFillFraction, tilt: s.axialTiltFraction,
          score: s.scoreTotal, fuel: s.freshBundlesAvailable, operations: s.refuellingOperationCount,
          isEndless: s.shift.isEndless, unlimitedFreshFuel: s.shift.unlimitedFreshFuel, fuelConsumed: s.shift.fuelConsumed,
          thermalMwh: s.shift.thermalEnergyMwh, zones: s.rrs.zones.map(z => z.fillFraction),
          powers: s.core.channels.map(c => c.powerWatts),
          bundlePowers: s.core.channels.flatMap(c => c.bundles.map(b => b.powerWatts)),
          burnup: s.core.channels.flatMap(c => c.bundles.map(b => b.currentBurnupMwdPerKg))
        };
        globalThis.longRunRows[speed].push(row);
        return { moveIndex, day: row.time / 86400, gameOver: ['ended', 'completed'].includes(s.runStatus), status: row.status, reason: row.reason, lzc: row.lzc, fuel: row.fuel, score: row.score };
      }, { speed, moves, moveIndex, days });
      moveIndex = result.moveIndex;
      console.log(JSON.stringify({ speed, ...result }));
      if (result.gameOver || result.day >= days) break;
    }
    runs.push({ speed, ...result });
    if (moveIndex !== moves.length) throw new Error(`Replay ended before all orders were applied at ${speed}.`);
  }
  const comparison = await page.evaluate(() => {
    const a = globalThis.longRunRows['10x'], b = globalThis.longRunRows['1x'];
    const drift = {}, mismatches = [];
    const difference = (key, x, y) => { drift[key] = Math.max(drift[key] ?? 0, Math.abs(x - y)); };
    if (a.length !== b.length) mismatches.push('Different trajectory lengths');
    for (let i = 0; i < Math.min(a.length, b.length); i++) {
      for (const key of ['time', 'lzc', 'tilt', 'score', 'thermalMwh']) difference(key, a[i][key], b[i][key]);
      for (const key of ['zones', 'powers', 'bundlePowers', 'burnup'])
        for (let j = 0; j < a[i][key].length; j++) difference(key, a[i][key][j], b[i][key][j]);
      for (const key of ['status', 'reason', 'fuel', 'operations'])
        if (a[i][key] !== b[i][key]) mismatches.push(`Row ${i}: ${key}`);
    }
    return { checkpoints: a.length, maximumDifferences: drift, mismatches, final10x: a.at(-1), final1x: b.at(-1) };
  });
  await writeFile(`${output}/report.json`, JSON.stringify({ url, buildInfo, replayPath, seed, runs, comparison, errors }, null, 2));
  console.log(JSON.stringify({ runs, checkpoints: comparison.checkpoints, maximumDifferences: comparison.maximumDifferences, mismatches: comparison.mismatches, errors }));
  const d = comparison.maximumDifferences;
  if (comparison.mismatches.length || errors.length || d.time !== 0 || d.powers > .01 ||
      d.bundlePowers > .01 || d.burnup > 1e-8 || d.lzc > 1e-8 || d.score > 1e-6 || d.thermalMwh > 1e-6)
    throw new Error('Browser speed comparison failed; inspect report.');
} finally { await browser.close(); }
