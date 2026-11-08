import type { CanduSnapshot } from "./protocol";
import { CUSTOM_PHYSICS_PACK_PREFIX } from "./customPhysics";
import "./gameUpdate.css";

interface UpdateSession {
  readonly snapshot?: CanduSnapshot;
  readonly isPending?: boolean;
  subscribe(listener: () => void): () => void;
  dispose(): void;
}

/** Checkpoint the current run before switching to a complete new build. */
export class GameUpdateView {
  readonly element = document.createElement("aside");
  private readonly version = document.createElement("span");
  private readonly message = document.createElement("p");
  private readonly button = document.createElement("button");
  private readonly progress = document.createElement("dialog");
  private waiting: ServiceWorker | null = null;
  private busy = false;
  private retry?: ReturnType<typeof setTimeout>;
  private readonly unsubscribe: () => void;
  private timer?: ReturnType<typeof setInterval>;
  private registration?: ServiceWorkerRegistration;
  private cleanRegistration = () => {};
  private build = "loading";

  constructor(private readonly session: UpdateSession, private readonly save: () => Promise<void | (() => void)>, private readonly reload: () => void = () => location.reload()) {
    this.element.className = "game-update";
    this.element.setAttribute("aria-label", "Game version and updates");
    this.message.setAttribute("role", "status"); this.message.hidden = true;
    this.button.type = "button"; this.button.textContent = "Retry update"; this.button.hidden = true;
    this.element.append(this.version, this.message, this.button); document.body.append(this.element);
    this.progress.className = "game-update-progress";
    this.progress.setAttribute("aria-label", "Saving run and updating game");
    this.progress.textContent = "Saving your run and preparing the game update…";
    this.progress.addEventListener("cancel", this.preventCancel);
    document.body.append(this.progress);
    this.button.addEventListener("click", this.apply);
    window.addEventListener("online", this.network);
    window.addEventListener("offline", this.network);
    let pending = session.isPending;
    this.unsubscribe = session.subscribe(() => {
      const readyToUpdate = pending && !session.isPending;
      pending = session.isPending;
      this.render();
      if (readyToUpdate) this.apply();
    });
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
        const previous = this.waiting;
        // A first install may briefly appear as waiting before it claims this page.
        this.waiting = navigator.serviceWorker.controller ? this.registration?.waiting ?? null : null;
        if (previous === this.waiting) return;
        this.message.hidden = !this.waiting;
        if (this.waiting) this.message.textContent = "A newer game is ready. Preparing to update automatically.";
        this.render();
        this.apply();
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
    const pack = snapshot?.dataPackId ?? "";
    const physics = pack.startsWith(CUSTOM_PHYSICS_PACK_PREFIX) ? "Custom physics" : pack.endsWith("xenon-v11")
      ? "Iodine–xenon calibration · v11" : pack.includes("diffusion-v10")
      ? "Reduced diffusion · Axial extrapolation · v10"
      : pack.includes("radial-circle-v9")
        ? "Circular radial boundary · Axial incoming current = 0 · v9"
        : pack.includes("axial-marshak-v8") ? "Axial incoming current = 0 · v8" : pack;
    const mode = snapshot?.pacingMode === "daily-turn" ? "Daily turns" : "Time based";
    const label = [`Build ${this.build}`, mode, physics].filter(Boolean).join(" · ");
    if (this.version.textContent !== label) this.version.textContent = label;
    this.version.title = pack;
    this.button.hidden = !this.waiting;
    this.button.disabled = this.busy || this.session.isPending || !navigator.onLine;
    if (this.waiting && this.session.isPending) this.message.textContent = "A newer game is ready. Return to the main screen to update.";
  }

  private readonly apply = (): void => { void this.update(); };
  private readonly network = (): void => { this.render(); this.apply(); };
  private readonly preventCancel = (event: Event): void => event.preventDefault();
  private async update(): Promise<void> {
    if (!this.waiting || this.busy || this.session.isPending || !navigator.onLine) return;
    if (this.retry) clearTimeout(this.retry);
    this.busy = true; this.render();
    let release: void | (() => void) = undefined;
    let reloading = false;
    try {
      const waiting = this.waiting;
      if (!await this.checkTabs(waiting) || this.session.isPending) return;
      // Block new input while the worker finishes its current command and the save commits.
      this.progress.showModal();
      release = await this.save();
      if (!await this.checkTabs(waiting)) return;
      this.message.textContent = "Run saved locally. Loading the new game…";
      if (!this.registration || !await this.registration.unregister()) throw new Error("Update could not be loaded. Your run is saved locally.");
      reloading = true;
      this.session.dispose();
      this.reload();
    } catch (error) { this.message.hidden = false; this.message.textContent = error instanceof Error ? error.message : "Update failed; the current game is preserved."; }
    finally { if (!reloading) release?.(); if (this.progress.open) this.progress.close(); this.busy = false; this.render(); }
  }

  private async checkTabs(worker: ServiceWorker): Promise<boolean> {
    const result = await new Promise<{ status: string; otherTabs?: number }>((resolve, reject) => {
      const channel = new MessageChannel();
      const timeout = setTimeout(() => {
        channel.port1.close();
        reject(new Error("The update check timed out. Try updating again."));
      }, 10_000);
      channel.port1.onmessage = event => {
        clearTimeout(timeout);
        channel.port1.close();
        resolve(event.data);
      };
      try {
        worker.postMessage({ type: "CANDU_ACTIVATE_UPDATE" }, [channel.port2]);
      } catch (error) {
        clearTimeout(timeout);
        channel.port1.close();
        channel.port2.close();
        reject(error);
      }
    });
    if (result.status === "other-tabs") {
      this.message.textContent = `Close ${result.otherTabs} other game tab(s) to continue the automatic update.`;
      this.retry = setTimeout(this.apply, 10_000);
      return false;
    }
    if (result.status !== "ready") throw new Error("The update could not be prepared.");
    return true;
  }

  destroy(): void {
    if (this.retry) clearTimeout(this.retry);
    if (this.timer) clearInterval(this.timer);
    this.cleanRegistration();
    window.removeEventListener("online", this.network);
    window.removeEventListener("offline", this.network);
    this.progress.removeEventListener("cancel", this.preventCancel); this.progress.remove();
    this.unsubscribe(); this.button.removeEventListener("click", this.apply); this.element.remove();
  }
}
