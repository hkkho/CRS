import { createClient, type SupabaseClient, type User } from '@supabase/supabase-js';
import { parseRunSave, type RunSave } from './runSave';

export interface CloudRun { id: string; revision: number; run: RunSave }
export interface LeaderboardEntry { display_name: string; score: number; seconds: number }

export class CloudPlayer {
  readonly client: SupabaseClient | null;
  user: User | null = null;
  constructor(url = import.meta.env.VITE_SUPABASE_URL, key = import.meta.env.VITE_SUPABASE_PUBLISHABLE_KEY) {
    try { this.client = url && key ? createClient(url, key, {
      auth: { flowType: 'pkce', detectSessionInUrl: true, persistSession: true, autoRefreshToken: true },
      global: { fetch: (input, init) => fetch(input, { ...init, signal: AbortSignal.timeout(20_000) }) },
    }) : null; } catch { this.client = null; }
  }
  async initialize(changed: () => void): Promise<() => void> {
    if (!this.client) return () => {};
    const { data, error } = await this.client.auth.getSession();
    if (error) throw error;
    this.user = data.session?.user ?? null;
    changed();
    const { data: subscription } = this.client.auth.onAuthStateChange((_event, session) => {
      this.user = session?.user ?? null;
      changed();
    });
    return () => subscription.subscription.unsubscribe();
  }
  async signIn(): Promise<void> {
    if (!this.client) throw new Error('Cloud login has not been configured for this deployment.');
    const { error } = await this.client.auth.signInWithOAuth({ provider: 'github', options: {
      redirectTo: new URL(import.meta.env.BASE_URL, location.origin).href,
    } });
    if (error) throw error;
  }
  async signOut(): Promise<void> {
    const result = await this.client?.auth.signOut({ scope: 'local' });
    if (result?.error) throw result.error;
    this.user = null;
  }
  async list(): Promise<RunSave[]> {
    if (!this.client || !this.user) return [];
    const user = this.user;
    const { data, error } = await this.client.from('player_runs').select('id,revision,run').order('updated_at', { ascending: false }).limit(100);
    if (error) throw error;
    return (data as CloudRun[]).map(row => {
      const run = parseRunSave(row.run);
      return { ...run, cloudRevision: row.revision, cloudOwnerId: user.id };
    });
  }
  async save(run: RunSave): Promise<void> {
    if (!this.client || !this.user) throw new Error('Sign in to save to the cloud.');
    const user = this.user;
    const { data, error } = await this.client.rpc('save_player_run', {
      payload: run, expected_revision: run.cloudOwnerId === this.user.id ? run.cloudRevision ?? 0 : 0,
    });
    if (error) throw error;
    run.cloudRevision = Number(data); run.cloudOwnerId = user.id;
  }
  async publish(run: RunSave): Promise<void> {
    if (!this.client || !this.user) throw new Error('Sign in to publish a score.');
    const { error } = await this.client!.rpc('publish_endless_score', { run_id: run.id });
    if (error) throw error;
  }
  async leaderboard(policy: string): Promise<LeaderboardEntry[]> {
    if (!this.client) return [];
    const { data, error } = await this.client.rpc('endless_leaderboard', { policy });
    if (error) throw error;
    return data as LeaderboardEntry[];
  }
}
