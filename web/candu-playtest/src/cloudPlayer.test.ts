import { beforeEach, expect, it, vi } from 'vitest';
import type { User } from '@supabase/supabase-js';
import type { RunSave } from './runSave';

const mocks = vi.hoisted(() => ({ rpc: vi.fn(), signInWithOAuth: vi.fn(), signOut: vi.fn(), createClient: vi.fn() }));
vi.mock('@supabase/supabase-js', () => ({ createClient: mocks.createClient }));
import { CloudPlayer } from './cloudPlayer';

beforeEach(() => {
  vi.clearAllMocks();
  mocks.createClient.mockReturnValue({ rpc: mocks.rpc, auth: { signInWithOAuth: mocks.signInWithOAuth, signOut: mocks.signOut } });
  mocks.rpc.mockResolvedValue({ data: 3, error: null });
  mocks.signInWithOAuth.mockResolvedValue({ error: null });
});
const run = () => ({ id: 'run', cloudOwnerId: 'alice', cloudRevision: 2 } as RunSave);
const player = () => { const result = new CloudPlayer('https://example.supabase.co', 'public-key'); result.user = { id: 'alice' } as User; return result; };

it('keeps guest mode usable without configuration or with an invalid URL', () => {
  expect(new CloudPlayer('', '').client).toBeNull();
  mocks.createClient.mockImplementationOnce(() => { throw new Error('bad config'); });
  expect(new CloudPlayer('bad', 'key').client).toBeNull();
});
it('uses PKCE and the current Pages base path without repository scopes', async () => {
  const cloud = player(); await cloud.signIn();
  expect(mocks.createClient.mock.calls[0][2].auth.flowType).toBe('pkce');
  expect(mocks.signInWithOAuth).toHaveBeenCalledWith({ provider: 'github', options: { redirectTo: new URL(import.meta.env.BASE_URL, location.origin).href } });
});
it('writes with the revision of this exact saved copy and updates it only on success', async () => {
  const cloud = player(); const save = run(); await cloud.save(save);
  expect(mocks.rpc).toHaveBeenCalledWith('save_player_run', { payload: save, expected_revision: 2 });
  expect(save.cloudRevision).toBe(3);
  mocks.rpc.mockResolvedValue({ error: new Error('conflict') });
  await expect(cloud.save(save)).rejects.toThrow('conflict'); expect(save.cloudRevision).toBe(3);
});
it('does not reuse another account revision or publish implicitly while saving', async () => {
  const cloud = player(); const save = run(); save.cloudOwnerId = 'bob'; await cloud.save(save);
  expect(mocks.rpc).toHaveBeenCalledTimes(1);
  expect(mocks.rpc.mock.calls[0][1].expected_revision).toBe(0);
  await cloud.publish(save);
  expect(mocks.rpc).toHaveBeenLastCalledWith('publish_endless_score', { run_id: save.id });
});

it('reports unreadable cloud rows while retaining valid saves and their revisions', async () => {
  const valid: RunSave = {
    id: '00000000-0000-4000-8000-000000000001', version: 1, savedAt: '2026-01-01T00:00:00Z',
    dataPackId: 'pack', scorePolicyId: 'score', seed: 1001, shiftId: 'free-practice', score: 0,
    seconds: 0, fuelConsumed: 0, ended: false, stateDigest: 'digest',
    commands: [{ command: { type: 'pause' }, accepted: true, responseMode: 'compact' }],
  };
  const order = vi.fn().mockReturnValue({ limit: vi.fn().mockResolvedValue({ data: [
    { id: 'bad-id', revision: 1, run: { version: 99 } },
    { id: valid.id, revision: 4, run: valid },
  ], error: null }) });
  mocks.createClient.mockReturnValueOnce({ from: vi.fn().mockReturnValue({ select: vi.fn().mockReturnValue({ order }) }) });

  await expect(player().list()).resolves.toEqual({
    runs: [{ ...valid, cloudRevision: 4, cloudOwnerId: 'alice' }],
    unreadableIds: ['bad-id'],
  });
});
