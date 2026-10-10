// @vitest-environment node
import { readFile, readdir } from 'node:fs/promises';
import { PGlite } from '@electric-sql/pglite';
import { beforeAll, afterAll, expect, it } from 'vitest';

const alice = '11111111-1111-4111-8111-111111111111';
const bob = '22222222-2222-4222-8222-222222222222';
const runId = '33333333-3333-4333-8333-333333333333';
const db = new PGlite();
beforeAll(async () => {
  await db.exec(`
    create role anon; create role authenticated;
    create schema auth;
    create table auth.users (id uuid primary key);
    create table auth.identities (user_id uuid, provider text, identity_data jsonb);
    create function auth.uid() returns uuid language sql stable as
      $$ select nullif(current_setting('request.jwt.claim.sub', true), '')::uuid $$;
    grant usage on schema auth to anon, authenticated;
    insert into auth.users values ('${alice}'), ('${bob}');
    insert into auth.identities values ('${alice}', 'github', '{"user_name":"alice"}'), ('${bob}', 'github', '{"user_name":"bob"}');
  `);
  const migrations = new URL('../../../supabase/migrations/', import.meta.url);
  for (const name of (await readdir(migrations)).filter(n => n.endsWith('.sql')).sort())
    await db.exec(await readFile(new URL(name, migrations), 'utf8'));
}, 30_000);
afterAll(async () => { await db.close(); });

const payload = (overrides = {}) => ({ version: 1, id: runId, seed: 1001, shiftId: 'free-practice',
  dataPackId: 'test-pack', scorePolicyId: 'test-policy', score: 1, seconds: 3600,
  fuelConsumed: 8, ended: false, commands: [{ command: { type: 'pause' }, accepted: true, responseMode: 'compact' }], stateDigest: 'test', ...overrides });
async function asUser(uid, role = 'authenticated') {
  await db.exec(`reset role; set request.jwt.claim.sub = '${uid ?? ''}'; set role ${role};`);
}
const save = (run, revision) => db.query('select public.save_player_run($1::jsonb, $2::integer) as revision', [JSON.stringify(run), revision]);

it('isolates private saves, rejects anonymous writes and direct mutations', async () => {
  await asUser(null, 'anon');
  await expect(save(payload(), 0)).rejects.toThrow();
  await expect(db.query('select * from public.player_runs')).rejects.toThrow();
  await asUser(alice);
  expect((await save(payload(), 0)).rows[0].revision).toBe(1);
  expect((await db.query('select * from public.player_runs')).rows).toHaveLength(1);
  await expect(db.query('delete from public.player_runs')).rejects.toThrow();
  await asUser(bob);
  expect((await db.query('select * from public.player_runs')).rows).toHaveLength(0);
  await expect(db.query('select public.publish_endless_score($1)', [runId])).rejects.toThrow();
});

it('rejects stale devices, backward progress, changed objectives and resurrection', async () => {
  await asUser(alice);
  await expect(save(payload({ seconds: 4000 }), 0)).rejects.toThrow('another device');
  await expect(save(payload({ seconds: 1 }), 1)).rejects.toThrow('older');
  await expect(save(payload({ seed: 1002 }), 1)).rejects.toThrow('seed');
  expect((await save(payload({ ended: true }), 1)).rows[0].revision).toBe(2);
  await expect(save(payload(), 2)).rejects.toThrow('Ended');
});

it('publishes only owned ended endless runs; exposes a best score without save or user IDs', async () => {
  await asUser(alice);
  await db.query('select public.publish_endless_score($1)', [runId]);
  await save(payload({ score: 0.5, ended: true }), 2);
  await db.query('select public.publish_endless_score($1)', [runId]);
  await asUser(bob);
  await save(payload({ id: '44444444-4444-4444-8444-444444444444' }), 0);
  await expect(db.query('select public.publish_endless_score($1)', ['44444444-4444-4444-8444-444444444444'])).rejects.toThrow('ended endless');
  await asUser(null, 'anon');
  expect((await db.query("select * from public.endless_leaderboard('test-policy')")).rows)
    .toEqual([{ display_name: 'alice', score: 1, seconds: 3600 }]);
  expect((await db.query("select * from public.endless_leaderboard('other-policy')")).rows).toEqual([]);
  await expect(db.query('select * from public.endless_scores')).rejects.toThrow();
});

it('rejects malformed and nonfinite stat payloads', async () => {
  await asUser(bob);
  for (const change of [{ commands: {} }, { commands: [] }, { score: 'NaN' }, { score: 'Infinity' }, { seconds: -1 }, { ended: null }]) {
    await expect(save(payload(change), 0)).rejects.toThrow();
  }
});

it('accepts daily v2 saves, rejects invalid pacing and keeps pacing immutable', async () => {
  await asUser(alice);
  const daily = payload({ id: '44444444-4444-4444-8444-444444444444', version: 2, pacingMode: 'daily-turn', draftChannels: [210, 211],
    commands: [{ command: { type: 'commit-day', expectedCompletedDays: 0, channelIndices: [] }, accepted: true, responseMode: 'compact' }] });
  await expect(save({ ...daily, pacingMode: 'unknown' }, 0)).rejects.toThrow('daily save');
  await expect(save({ ...daily, draftChannels: null }, 0)).rejects.toThrow('daily save');
  expect((await save(daily, 0)).rows[0].revision).toBe(1);
  await expect(save({ ...daily, pacingMode: 'real-time' }, 1)).rejects.toThrow('cannot change');
  expect((await save({ ...daily, seconds: 86400, draftChannels: [] }, 1)).rows[0].revision).toBe(2);
});
