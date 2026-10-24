import { isRunTerminal, type CanduSnapshot } from "./protocol";

/** Optional guidance follows real input and snapshots. It never sends a command. */
export class FirstShiftGuide {
  readonly element = document.createElement("section");
  private active = false;
  private selected = false;
  private startDay = 0;
  constructor(private readonly showBurnup: () => void, private readonly closed: () => void = () => {}) {
    this.element.className = "first-shift-guide";
    this.element.hidden = true;
    this.element.setAttribute("aria-label", "Optional first-shift guide");
    this.element.innerHTML = `<div><p class="studio-eyebrow" data-guide-step aria-live="polite"></p><h2></h2><p data-guide-copy></p></div><div class="guide-actions"><button type="button" data-guide="burnup">Show burnup map</button><button type="button" data-guide="skip">Skip guide</button></div>`;
    this.element.addEventListener("click", this.click);
  }
  start(snapshot: CanduSnapshot): void { this.active = true; this.selected = false; this.startDay = snapshot.completedDays ?? 0; }
  stop(): void { this.active = false; this.element.hidden = true; }
  inspect(): void { this.selected = true; }
  update(snapshot: CanduSnapshot, plan: readonly number[], pending: boolean, historical: boolean): void {
    this.element.hidden = !this.active;
    if (!this.active) return;
    const finished = (snapshot.completedDays ?? 0) > this.startDay || isRunTerminal(snapshot);
    const step = finished ? 3 : plan.length ? 2 : this.selected ? 1 : 0;
    const headings = ["Start with one channel.", "Read what will leave.", "Your sheet is a draft. You choose when to run.", "Read the result. Make your next plan."];
    const copy = [
      "Switch to burnup and select a channel on the core. Higher burnup means more-used fuel; it does not promise a safe move. Take your time.",
      "The workbench marks the outgoing positions OUT and retained positions KEEP. Each chosen channel uses eight fresh bundles when executed. Add a channel to fill the fuelling sheet.",
      "The sheet shows the execution order and fuel needed. You can remove a channel or clear the sheet without spending fuel. When ready, use Refuel & advance on the sheet.",
      "Open the Day report for fuel used, useful discharges and points. Inspect LZC, power and the changed core before choosing again. Survival and a challenge reward are separate outcomes.",
    ];
    this.element.dataset.step = String(step);
    this.element.querySelector("[data-guide-step]")!.textContent = `FIRST SHIFT / ${step + 1} OF 4 · OPTIONAL`;
    this.element.querySelector("h2")!.textContent = headings[step];
    this.element.querySelector("[data-guide-copy]")!.textContent = historical ? "You are viewing history. Return to live to continue planning." : pending ? "The shared simulation is calculating your day. Your submitted order is locked until the result returns." : copy[step];
    const burnup = this.element.querySelector<HTMLButtonElement>('[data-guide="burnup"]')!;
    burnup.hidden = step > 0; burnup.disabled = pending;
    this.element.querySelector('[data-guide="skip"]')!.textContent = finished ? "Finish guide" : "Skip guide";
  }
  private readonly click = (event: MouseEvent): void => {
    const action = (event.target as Element).closest<HTMLButtonElement>("[data-guide]")?.dataset.guide;
    if (action === "burnup") this.showBurnup();
    if (action === "skip") { this.active = false; this.element.hidden = true; this.closed(); }
  };
  destroy(): void { this.element.removeEventListener("click", this.click); this.element.remove(); }
}
