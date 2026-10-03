import type { BridgeStatus } from "./protocol";

/** A native recovery action available on the launcher and every game view. */
export class BridgeRecoveryView {
  readonly element = document.createElement("section");
  private readonly detail = document.createElement("p");
  private readonly button = document.createElement("button");

  constructor(parent: HTMLElement, reload: () => void = () => window.location.reload()) {
    this.element.className = "bridge-recovery";
    this.element.hidden = true;
    this.element.setAttribute("role", "alert");
    const title = document.createElement("h2");
    title.textContent = "Reactor connection lost";
    const explanation = document.createElement("p");
    explanation.textContent = "Reload to start a new shift. Current progress will be lost. The last order will not be retried.";
    this.button.textContent = "Reload and start a new shift";
    this.button.addEventListener("click", reload);
    this.element.append(title, this.detail, explanation, this.button);
    parent.append(this.element);
  }

  update(status: BridgeStatus): void {
    const wasHidden = this.element.hidden;
    this.element.hidden = status.source !== "unavailable";
    this.detail.textContent = status.detail;
    if (wasHidden && !this.element.hidden) this.button.focus();
  }
}
