import type { BridgeSessionController } from './sessionController';
import { isRunTerminal } from './protocol';
import { CloudPlayer } from './cloudPlayer';
import { PlayerStore } from './playerStore';
import type { RunSave } from './runSave';
import './player.css';

/** Account/storage UI is independent of the reactor and never gates guest play. */
export class PlayerPanel {
  private readonly toggle = document.createElement('button');
  private readonly dialog = document.createElement('dialog');
  private readonly store = new PlayerStore();
  private readonly cloud = new CloudPlayer();
  private readonly unsubscribe: () => void;
  private unsubscribeAuth = () => {};
  private busy = false;
  private disposed = false;
  private lastSaved = 0;
  private lastTerminal = '';
  private offlineReady = false;
  private offlineUnavailable = false;
  private readonly timer: ReturnType<typeof setInterval>;

  constructor(private readonly session: BridgeSessionController, private readonly continued: () => void) {
    this.toggle.className = 'player-toggle'; this.toggle.textContent = 'Saves · Stats · Login';
    this.toggle.setAttribute('aria-haspopup', 'dialog');
    this.dialog.className = 'player-panel'; this.dialog.setAttribute('aria-labelledby', 'player-title');
    this.dialog.innerHTML = `<h2 id="player-title">Your runs</h2>
      <p data-account></p><p data-network></p>
      <div class="player-actions"><button data-action="login">Sign in with GitHub</button>
      <button data-action="logout">Sign out</button><button data-action="guest">Continue without login</button>
      <button data-action="close">Back to game</button></div>
      <p>Guest play needs no account. Local saves stay in this browser; GitHub login enables private cloud saves and stats on other devices.</p>
      <p>Active runs restore paused by replaying the shared simulation. Long runs can take several minutes. Ended runs keep stats but cannot be continued.</p>
      <div class="player-actions"><button data-action="save">Save locally</button><button data-action="cloud">Save to cloud</button>
      <button data-action="refresh">Refresh saves & stats</button></div>
      <p data-message role="status" aria-live="polite"></p>
      <h3>Stats for listed saves</h3><p data-stats></p>
      <h3>Saved runs</h3><div data-runs></div>
      <h3>Endless leaderboard</h3><p>Player-submitted scores, not server-verified. Publishing shares your GitHub name, score and run duration. One best score per player, per scoring policy.</p>
      <button data-action="publish">Publish this ended endless run</button>
      <button data-action="leaderboard">Refresh leaderboard</button><ol data-leaderboard></ol>`;
    document.body.append(this.toggle, this.dialog);
    this.toggle.addEventListener('click', this.open);
    this.dialog.addEventListener('click', this.onClick);
    window.addEventListener('online', this.network); window.addEventListener('offline', this.network);
    window.addEventListener('candu-offline-ready', this.offline);
    window.addEventListener('candu-offline-unavailable', this.offlineFailed);
    this.unsubscribe = session.subscribe(update => {
      this.render();
      if (update.response && isRunTerminal(update.snapshot) && this.lastTerminal !== session.journal.id && !this.busy) {
        void this.task(async () => {
          const run = await this.persist();
          if (this.cloud.user && navigator.onLine) await this.sync(run);
          await this.refresh();
        });
      }
    });
    this.timer = setInterval(() => {
      if (!this.busy && !session.isPending && session.snapshot.simulationTimeSeconds > 0 &&
        (isRunTerminal(session.snapshot) ? this.lastTerminal !== session.journal.id : Date.now() - this.lastSaved > 30_000)) {
        void this.task(async () => {
          const run = await this.persist();
          if (run.ended && this.cloud.user && navigator.onLine) await this.sync(run);
        });
      }
    }, 30_000);
    void this.cloud.initialize(() => this.render()).then(unsubscribe => {
      if (this.disposed) unsubscribe(); else this.unsubscribeAuth = unsubscribe;
    }).catch(error => this.message(`Login unavailable: ${errorText(error)} Guest play still works.`));
    this.render();
  }

  private owner(): string { return this.cloud.user?.id ?? 'guest'; }
  private message(value: string): void { this.dialog.querySelector('[data-message]')!.textContent = value; }
  private readonly network = (): void => this.render();
  private readonly offline = (): void => { this.offlineReady = true; this.render(); };
  private readonly offlineFailed = (): void => { this.offlineUnavailable = true; this.render(); };
  private render(): void {
    this.dialog.querySelector('[data-account]')!.textContent = this.cloud.user
      ? `Signed in as ${this.cloud.user.user_metadata.user_name ?? 'GitHub player'}.`
      : this.cloud.client ? 'Playing as guest.' : 'Playing as guest. Cloud login is not configured on this deployment.';
    this.dialog.querySelector('[data-network]')!.textContent = navigator.onLine
      ? `Local autosave every 30 seconds of play; use Save to cloud before leaving to sync progress and stats. ${this.offlineReady ? 'Offline game download ready.' : this.offlineUnavailable || !import.meta.env.PROD ? 'Offline reload is unavailable in this browser or build.' : 'Keep this page open while the offline game downloads.'}`
      : 'Offline. Play and local saves remain available; cloud actions need a connection.';
    for (const button of this.dialog.querySelectorAll<HTMLButtonElement>('button')) {
      const action = button.dataset.action;
      button.disabled = this.busy || (['save', 'cloud', 'publish'].includes(action ?? '') &&
        (!this.session.status.isWasmAvailable || this.session.isPending));
      if (['login', 'cloud', 'publish', 'leaderboard'].includes(action ?? ''))
        button.disabled ||= !this.cloud.client || !navigator.onLine;
      if (['cloud', 'publish', 'logout'].includes(action ?? '')) button.disabled ||= !this.cloud.user;
      if (action === 'publish') button.disabled ||= !isRunTerminal(this.session.snapshot) ||
        !this.session.snapshot.shift?.isEndless || !!this.session.snapshot.provenance?.isModified;
      if (action === 'login') button.hidden = !!this.cloud.user;
      if (action === 'logout' || action === 'guest') button.hidden = action === 'logout' ? !this.cloud.user : !!this.cloud.user;
    }
    this.dialog.setAttribute('aria-busy', String(this.busy));
  }

  private async task(work: () => Promise<void>): Promise<void> {
    if (this.busy || this.disposed) return;
    this.busy = true; this.render();
    try { await work(); }
    catch (error) { this.message(errorText(error)); }
    finally { this.busy = false; this.render(); }
  }

  private async pause(): Promise<void> {
    if (this.session.status.isWasmAvailable && !isRunTerminal(this.session.snapshot)) {
      const result = await this.session.dispatch({ type: 'pause' });
      if (!result.accepted) throw new Error(result.message);
    }
  }

  private async persist(): Promise<RunSave> {
    const run = this.session.captureRun();
    await this.store.put(this.owner(), run); this.lastSaved = Date.now();
    if (run.ended) this.lastTerminal = run.id;
    this.message(`Saved locally at ${new Date(run.savedAt).toLocaleTimeString()}.`);
    return run;
  }

  private async sync(run: RunSave, publish = false): Promise<void> {
    const owner = this.owner();
    await this.cloud.save(run);
    this.session.journal.markSynced(run);
    await this.store.put(owner, run);
    if (publish) await this.cloud.publish(run);
  }

  private readonly open = (): void => {
    this.dialog.showModal();
    void this.task(async () => { await this.pause(); await this.refresh(); });
  };

  private async refresh(): Promise<void> {
    const owner = this.owner();
    const local = await this.store.list(owner);
    const guest = owner === 'guest' ? [] : await this.store.list('guest');
    let remote: RunSave[] = [];
    if (this.cloud.user && navigator.onLine) {
      try { remote = await this.cloud.list(); }
      catch (error) { this.message(`Local saves loaded. Cloud unavailable: ${errorText(error)}`); }
    }
    const list = this.dialog.querySelector('[data-runs]')!; list.replaceChildren();
    const entries = [...local.map(run => ({ run, label: 'This browser' })),
      ...guest.map(run => ({ run, label: 'Guest save in this browser' })),
      ...remote.map(run => ({ run, label: 'Cloud' }))];
    if (!entries.length) list.textContent = 'No saved runs yet.';
    for (const { run, label } of entries) {
      const row = document.createElement('p');
      row.dataset.runId = run.id; row.dataset.source = label;
      row.textContent = `${label} · ${run.shiftId === 'free-practice' ? 'Endless' : 'One-day challenge'} · seed ${run.seed} · ${run.score.toFixed(2)} points · ${(run.seconds / 86400).toFixed(2)} days · ${new Date(run.savedAt).toLocaleString()} · ${run.ended ? 'Ended' : 'Active'} `;
      // Any newer ended copy is a tombstone: do not offer the earlier live save.
      const dead = entries.some(e => e.run.id === run.id && e.run.ended);
      if (!run.ended && !dead) {
        const button = document.createElement('button'); button.textContent = 'Continue run';
        button.addEventListener('click', () => void this.task(async () => {
          await this.pause();
          // Preserve the current run before replacing it.
          await this.persist();
          await this.session.restoreRun(run, (done, total) => this.message(`Restoring ${done} / ${total} commands…`));
          await this.persist(); this.continued(); this.dialog.close();
        }));
        row.append(button);
      }
      if (run.ended && run.shiftId === 'free-practice' && this.cloud.user) {
        const publish = document.createElement('button'); publish.textContent = 'Publish score';
        publish.addEventListener('click', () => void this.task(async () => {
          await this.sync(run, true); this.message('Endless score published.'); await this.leaderboard(); await this.refresh();
        }));
        row.append(publish);
      }
      list.append(row);
    }
    const unique = new Map<string, RunSave>();
    for (const run of [...local, ...remote]) {
      const previous = unique.get(run.id);
      if (!previous || run.ended || run.seconds > previous.seconds) unique.set(run.id, run);
    }
    const runs = [...unique.values()];
    this.dialog.querySelector('[data-stats]')!.textContent = `${runs.length} saved runs · ${runs.filter(r => r.ended).length} ended · ${(runs.reduce((sum, r) => sum + r.seconds, 0) / 86400).toFixed(2)} total days · ${runs.reduce((sum, r) => sum + r.fuelConsumed, 0)} bundles consumed. Endless best: ${Math.max(0, ...runs.filter(r => r.shiftId === 'free-practice' && r.scorePolicyId === this.session.snapshot.scorePolicyId).map(r => r.score)).toFixed(2)} points.`;
  }

  private async leaderboard(): Promise<void> {
    const entries = await this.cloud.leaderboard(this.session.snapshot.scorePolicyId ?? '');
    const list = this.dialog.querySelector('[data-leaderboard]')!; list.replaceChildren();
    for (const entry of entries) {
      const item = document.createElement('li');
      item.textContent = `${entry.display_name} — ${Number(entry.score).toFixed(2)} points · ${(Number(entry.seconds) / 86400).toFixed(2)} days`;
      list.append(item);
    }
    if (!entries.length) this.message('No scores published for this scoring policy yet.');
  }

  private readonly onClick = (event: MouseEvent): void => {
    const action = (event.target as Element).closest<HTMLButtonElement>('button')?.dataset.action;
    if (!action || this.busy) return;
    if (action === 'close' || action === 'guest') { this.dialog.close(); return; }
    void this.task(async () => {
      if (action === 'login') {
        if (this.session.status.isWasmAvailable) { await this.pause(); await this.persist(); }
        await this.cloud.signIn();
      }
      if (action === 'logout') { await this.cloud.signOut(); await this.refresh(); }
      if (action === 'save' || action === 'cloud' || action === 'publish') {
        await this.pause(); const run = await this.persist();
        if (action === 'cloud') { await this.sync(run); this.message('Progress and stats saved to your private cloud account.'); }
        if (action === 'publish') { await this.sync(run, true); this.message('Endless score published.'); await this.leaderboard(); }
        await this.refresh();
      }
      if (action === 'refresh') await this.refresh();
      if (action === 'leaderboard') await this.leaderboard();
    });
  };

  destroy(): void {
    this.disposed = true; clearInterval(this.timer); this.unsubscribe(); this.unsubscribeAuth();
    window.removeEventListener('online', this.network); window.removeEventListener('offline', this.network);
    window.removeEventListener('candu-offline-ready', this.offline);
    window.removeEventListener('candu-offline-unavailable', this.offlineFailed);
    this.toggle.remove(); this.dialog.remove();
  }
}
function errorText(error: unknown): string {
  return error instanceof Error ? error.message : typeof error === 'object' && error && 'message' in error ? String(error.message) : String(error);
}
