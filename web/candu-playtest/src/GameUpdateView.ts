import type { BridgeSessionController } from "./sessionController";
import "./gameUpdate.css";

/** Keep a complete JS/WASM build together and let the player choose when to reload. */
export class GameUpdateView {
  readonly element = document.createElement("aside");
  private readonly version = document.createElement("span");
  private readonly message = document.createElement("p");
  private readonly button = document.createElement("button");
  private readonly progress = document.createElement("dialog");
  private waiting: ServiceWorker | null = null;
  private busy = false;
  private readonly unsubscribe: () => void;
  private timer?: ReturnType<typeof setInterval>;
  private registration?: ServiceWorkerRegistration;
  private cleanRegistration = () => {};
  private build = "loading";

  constructor(private readonly session: BridgeSessionController, private readonly save: () => Promise<void>, private readonly reload: () => void = () => location.reload()) {
    this.element.className = "game-update";
    this.element.setAttribute("aria-label", "Game version and updates");
    this.message.setAttribute("role", "status"); this.message.hidden = true;
    this.button.type = "button"; this.button.textContent = "Save locally & update"; this.button.hidden = true;
    this.element.append(this.version, this.message, this.button); document.body.append(this.element);
    this.progress.className = "game-update-progress";
    this.progress.setAttribute("aria-label", "Saving run and updating game");
    this.progress.textContent = "Saving your run and preparing the game update…";
    this.progress.addEventListener("cancel", this.preventCancel);
    document.body.append(this.progress);
    this.button.addEventListener("click", this.apply);
    window.addEventListener("online", this.network);
    window.addEventListener("offline", this.network);
    this.unsubscribe = session.subscribe(() => this.render());
  }

  async initialize(base: string, registerOffline = true): Promise<void> {
    try {
      const response = await fetch(`${base}wasm/build-info.json`);
      if (response.ok) { const info = await response.json(); this.build = String(info.gitCommitSha ?? "unknown").slice(0, 7); }
    } catch { this.build = "offline"; }
    this.render();
    if (!registerOffline || !("serviceWorker" in navigator)) return;
    try {
      this.registration = await navigator.serviceWorker.register(`${base}sw.js`, { scope: base, updateViaCache: "none" });
      const inspect = () => {
        this.waiting = this.registration?.waiting ?? null;
        this.message.hidden = !this.waiting;
        if (this.waiting) { this.message.hidden = false; this.message.textContent = "A newer game is ready. Save locally before updating. Older physics saves may require a new run."; }
        this.render();
      };
      const watch = () => {
        const installing = this.registration?.installing;
        installing?.addEventListener("statechange", () => {
          if (installing.state === "installed" || installing.state === "activated") inspect();
          if (installing.state === "redundant" && !this.registration?.active) window.dispatchEvent(new Event("candu-offline-unavailable"));
        });
      };
      this.registration.addEventListener("updatefound", watch); watch(); inspect();
      navigator.serviceWorker.addEventListener("controllerchange", inspect);
      this.cleanRegistration = () => {
        this.registration?.removeEventListener("updatefound", watch);
        navigator.serviceWorker.removeEventListener("controllerchange", inspect);
      };
      this.timer = setInterval(() => { void this.registration?.update().catch(() => undefined); }, 60_000);
      void navigator.serviceWorker.ready.then(() => window.dispatchEvent(new Event("candu-offline-ready")));
    } catch { window.dispatchEvent(new Event("candu-offline-unavailable")); }
  }

  private render(): void {
    const snapshot = this.session.snapshot;
    const physics = snapshot.dataPackId.includes("axial-marshak-v8") ? "Axial incoming current = 0 · v8" : snapshot.dataPackId;
    this.version.textContent = `Build ${this.build} · ${snapshot.pacingMode === "daily-turn" ? "Daily turns" : "Update required"} · ${physics}`;
    this.version.title = `${snapshot.dataPackId} · Time stays frozen until you advance one day.`;
    this.button.hidden = !this.waiting;
    this.button.disabled = this.busy || this.session.isPending || !navigator.onLine;
  }

  private readonly apply = (): void => { void this.update(); };
  private readonly network = (): void => this.render();
  private readonly preventCancel = (event: Event): void => event.preventDefault();
  private async update(): Promise<void> {
    if (!this.waiting || this.busy || this.session.isPending || !navigator.onLine) return;
    this.busy = true; this.render();
    try {
      // Prevent new fuel orders while the checkpoint and reload are in flight.
      this.progress.showModal();
      await this.save();
      const result = await new Promise<{ status: string; otherTabs?: number }>((resolve, reject) => {
        const channel = new MessageChannel();
        const timeout = setTimeout(() => { channel.port1.close(); reject(new Error("Update not available yet. Your run is saved; close other game tabs and reopen the game.")); }, 10_000);
        channel.port1.onmessage = event => { clearTimeout(timeout); channel.port1.close(); resolve(event.data); };
        this.waiting!.postMessage({ type: "CANDU_ACTIVATE_UPDATE" }, [channel.port2]);
      });
      if (result.status === "other-tabs") {
        this.message.textContent = `Run saved locally. Close ${result.otherTabs} other game tab(s), then try updating again.`;
        return;
      }
      if (result.status !== "ready") throw new Error("Update could not be loaded. Your run is saved locally.");
      this.message.textContent = "Run saved locally. Loading the new game…";
      if (!this.registration || !await this.registration.unregister()) throw new Error("Update could not be loaded. Your run is saved locally.");
      this.session.dispose();
      this.reload();
    } catch (error) { this.message.hidden = false; this.message.textContent = error instanceof Error ? error.message : "Update failed; the current game is preserved."; }
    finally { this.progress.close(); this.busy = false; this.render(); }
  }

  destroy(): void {
    if (this.timer) clearInterval(this.timer);
    this.cleanRegistration();
    window.removeEventListener("online", this.network);
    window.removeEventListener("offline", this.network);
    this.progress.removeEventListener("cancel", this.preventCancel); this.progress.remove();
    this.unsubscribe(); this.button.removeEventListener("click", this.apply); this.element.remove();
  }
}
