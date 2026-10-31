import "./gameLoading.css";

/** Entry gate: readiness never starts a run without the player's Start action. */
export class GameLoadingView {
  readonly element = document.createElement("dialog");
  private readonly heading = document.createElement("h2");
  private readonly detail = document.createElement("p");
  private readonly progress = document.createElement("progress");
  private readonly startButton = document.createElement("button");
  private readonly backButton = document.createElement("button");
  private ready = false;
  private finished = false;

  constructor(parent: HTMLElement, private readonly start: () => void, private readonly back: () => void) {
    this.element.className = "game-loading-dialog";
    this.element.setAttribute("aria-label", "Preparing game");
    this.heading.tabIndex = -1;
    this.detail.setAttribute("role", "status");
    this.progress.setAttribute("aria-label", "Loading game");
    this.startButton.type = "button";
    this.startButton.dataset.gameStart = "";
    this.startButton.className = "workspace-primary";
    this.startButton.textContent = "Start";
    this.startButton.hidden = true;
    this.startButton.addEventListener("click", this.onStart);
    const cancel = this.backButton;
    cancel.type = "button"; cancel.textContent = "Back";
    cancel.addEventListener("click", this.onBack);
    const actions = document.createElement("div");
    actions.className = "game-loading-actions"; actions.append(this.startButton, cancel);
    this.element.append(this.heading, this.detail, this.progress, actions);
    this.element.addEventListener("cancel", this.onCancel);
    parent.append(this.element);
    this.update(false);
    if (typeof this.element.showModal === "function") this.element.showModal();
    else this.element.setAttribute("open", "");
    this.heading.focus();
  }

  update(ready: boolean, error?: string): void {
    const becameReady = ready && !this.ready;
    this.ready = ready;
    this.element.setAttribute("aria-busy", String(!ready && !error));
    this.heading.textContent = error ? "Unable to load game" : ready ? "Your core is ready" : "Loading your game…";
    this.detail.textContent = error || (ready ? "Press Start when you're ready to play." : "Setting up the reactor and its initial power distribution.");
    this.progress.hidden = ready || !!error;
    this.startButton.hidden = !ready;
    this.startButton.disabled = !ready;
    if (becameReady) this.startButton.focus();
  }

  private readonly onStart = (): void => {
    if (!this.ready || this.finished) return;
    this.finished = true; this.destroy(); this.start();
  };
  private readonly onBack = (): void => {
    if (this.finished) return;
    this.finished = true; this.destroy(); this.back();
  };
  private readonly onCancel = (event: Event): void => { event.preventDefault(); this.onBack(); };
  destroy(): void {
    this.finished = true;
    this.element.removeEventListener("cancel", this.onCancel);
    this.startButton.removeEventListener("click", this.onStart);
    this.backButton.removeEventListener("click", this.onBack);
    if (this.element.open && typeof this.element.close === "function") this.element.close();
    this.element.remove();
  }
}
