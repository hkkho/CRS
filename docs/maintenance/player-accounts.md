# Player accounts, saves and endless scores

GitHub Pages still hosts the complete game and authoritative WASM simulation.
GitHub login is optional. Supabase provides OAuth and PostgreSQL storage; the
browser contains only a public project URL and publishable key. Reactor rules
remain in Core/Game, with no server or TypeScript replacement simulator.

## Player workflow

- Begin shift without an account, or open **Saves · Stats · Login** and choose
  **Sign in with GitHub**. Signing in preserves the current run locally before
  redirecting; continue that guest save after returning.
- Local saves use IndexedDB and autosave every 30 seconds while playing.
  **Save locally** is the explicit checkpoint before closing the page. Storage
  denial or quota failure is reported rather than treated as successful saving.
- Signed-in players use **Save to cloud** to copy progress and stats to their
  private account. Active-run cloud checkpoints are explicit; ended-run stats
  also attempt a cloud save automatically when signed in and online. Loss of
  internet does not block simulation or local saves. Reconnect and save to sync.
- **Continue run** restores a live save paused. Ended/completed runs retain
  stats but have no continuation action. An ended local/cloud copy suppresses
  continuation of older listed copies with the same run ID. A device offline
  cannot know about an ending recorded only on another device.
- The panel lists local saves and the latest 100 cloud saves. Stats deduplicate
  listed runs by ID; the endless best is restricted to the current score policy.
- **Publish score** explicitly shares an ended endless result and the account's
  GitHub username. The leaderboard displays the top 50 players, one best score
  per player per scoring policy. Scores are **player-submitted**, not verified
  by replay on a trusted server. A modified browser can fabricate a submission;
  this is a casual board, not an anti-cheat competitive service.
- Account saves are separated locally by Supabase user ID. Guest saves remain
  accessible to other users of that browser. Signing out does not erase saves;
  use browser site-data deletion to remove local data.

## Offline installation and updates

Production builds generate a service worker with an immutable cache of that
build's HTML, JavaScript, CSS and complete WASM assets. The panel reports
**Offline game download ready** after installation. Initial installation needs
internet and available browser storage; merely opening a URL once is not enough.
Local development and research builds do not register an offline worker.

The worker only handles the app's own static assets and entry navigation,
never Supabase, GitHub, tokens or API responses. A new build waits for old app
tabs to close before activating, keeping JS and WASM versions together. Close
all game tabs and reopen to receive an update. Browser eviction or clearing site
data removes offline assets and local saves.

## Save format and compatibility

`runSave.ts` owns a versioned journal of exact bridge commands, accepted flags,
response modes, simulation pack and scoring policy IDs, and the last shared
state digest. Rejected responses are retained because they affect bridge
sequence/digest history. Accepted resets start a new run ID and journal with
an explicit seed/objective. No simulation steps are merged or approximated.

Restoration uses a separate worker and replays through the same C# bridge.
Acceptance outcomes and the final state digest must match before the controller
adopts the worker. It then pauses the restored run. Failure preserves the old
worker and save. History graphs restart on continuation; reactor state, fuel,
poison, score and accumulated stats come from the restored simulation.

Replaying a long run can be slow, proportional to original simulation work;
the panel reports commands completed. Saves are limited to 100,000 commands
and 12 MB. Limits or incompatible physics changes produce a visible error,
never a partially restored reactor. Native checkpoints would be a future Game/
Core feature, not a frontend reconstruction of physical state.

Cloud saves use optimistic revisions attached to the particular saved copy.
Refreshing the list does not silently grant a stale local copy the revision of
a newer cloud copy. Concurrent updates, backward time, changed seed/objective,
and attempts to revive a cloud-ended run are rejected. Continue the cloud copy
after a conflict. Saves are not automatically merged across devices.

## Supabase and GitHub Pages setup

1. Create/reuse a Supabase project for CRS and apply
   `supabase/migrations/202610040001_player_runs.sql` with the SQL editor or CLI.
2. Create a GitHub OAuth App with homepage `https://hkkho.github.io/CRS/` and
   callback `https://<project-ref>.supabase.co/auth/v1/callback`. Enable the GitHub
   provider in Supabase and enter the OAuth client ID and secret **there only**.
3. Set Supabase Auth Site URL to `https://hkkho.github.io/CRS/` and allow that
   exact redirect URL. Add localhost URLs only for development. Browser auth
   uses PKCE and returns to the Pages base path; no server callback route or
   repository access scope is needed.
4. Set GitHub repository **Actions variables** `VITE_SUPABASE_URL` and
   `VITE_SUPABASE_PUBLISHABLE_KEY`. The Pages build reads those public values.
   For local builds, copy `web/candu-playtest/.env.example` to `.env.local` and
   fill the same values. Never put a service-role key, Supabase secret key,
   database password, or GitHub client secret in frontend configuration.
5. Deploy through the existing Pages pipeline. Without configuration the game
   remains usable as guest and clearly says cloud login is not configured.
6. Test a real GitHub sign-in, redirect back to `/CRS/`, cloud save, sign-out,
   and continuation from a second browser. Publish an ended endless score only
   when intentionally sharing that test account's result.

The private table uses row-level security for reads and restricted write RPCs.
Public queries return leaderboard names and scores, never account IDs or save
payloads. User deletion cascades to runs and published scores. Apply Supabase
project quotas/rate limits appropriate to traffic before a broad public launch.

References: [GitHub OAuth setup](https://supabase.com/docs/guides/auth/social-login/auth-github),
[Supabase row-level security](https://supabase.com/docs/guides/database/postgres/row-level-security).

## Verification

`tools/Test-Browser.ps1` covers controller/save tests, PostgreSQL ownership,
conflict and leaderboard tests using embedded PostgreSQL (PGlite), the existing
bridge suite, and production build. `npm run smoke:saves -- <URL>` verifies the
actual WASM game, local save, offline reload, verified restore, paused state,
score/fuel preservation and 320px layout. It runs on the Pages subpath in CI.
Live OAuth/project configuration is a separate hosted integration check.
