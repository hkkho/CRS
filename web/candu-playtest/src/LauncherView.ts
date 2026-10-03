import type { CanduCommand, ShiftId } from "./protocol";
import type { BridgeSessionController, SessionUpdate } from "./sessionController";
import "./workspace.css";

type LauncherSession = Pick<BridgeSessionController, "snapshot" | "status" | "isPending" | "subscribe" | "dispatch">;

/** Native launcher; starting a view never initializes another reactor. */
export class LauncherView {
  readonly element = document.createElement("main");
  private readonly unsubscribe: () => void;
  private lastSeed: number | undefined;
  private lastShift: string | undefined;
  private starting = false;

  constructor(private readonly session: LauncherSession, parent: HTMLElement, private readonly begin: () => void) {
    this.element.className = "reactor-launcher workspace-native";
    this.element.innerHTML = `<div class="launcher-shell">
      <p class="workspace-kicker">CANDU / ON-POWER REFUELLING</p>
      <h1>Keep the core productive.</h1>
      <p class="launcher-intro">Read the fuel. Choose a channel. Refuel at power and keep room in the regulating zones.</p>
      <div class="launcher-grid"><section><h2>Your next shift</h2>
        <p data-field="objective"></p><p data-field="connection"></p>
        <button data-action="begin" class="workspace-primary">Begin shift</button>
        <p>Use Tab to reach controls and Enter or Space to activate. Studio includes a keyboard channel map, fuel watchlist and history charts.</p>
      </section><section><h2>Choose a starting core</h2>
        <form><label>Core seed <input data-field="seed" type="number" min="0" max="4294967295" step="1" required></label>
        <label>Shift objective <select data-field="shift"><option value="free-practice">Free practice · 30 days</option><option value="useful-fuel-day-v1">One-day challenge</option></select></label>
        <button data-action="apply" type="submit">Use seed & objective</button></form>
        <button data-action="next">New aged core</button>
        <p>The same seed recreates the same aged fuel. Designer edits stay playable and mark the run as a modified sandbox.</p>
        <p data-field="error"></p>
      </section></div>
      <p class="workspace-kicker">380 CHANNELS · 12 BUNDLE POSITIONS · ONE LIVE REACTOR</p>
    </div>`;
    parent.append(this.element);
    this.element.addEventListener("click", this.onClick);
    this.element.querySelector("form")!.addEventListener("submit", this.onSubmit);
    this.unsubscribe = session.subscribe(update => this.render(update));
    this.field<HTMLButtonElement>("begin").focus({ preventScroll: true });
  }

  private field<T extends HTMLElement>(name: string): T {
    return this.element.querySelector(`[data-field="${name}"], [data-action="${name}"]`)! as T;
  }

  private render(update: SessionUpdate): void {
    const ready = update.status.isWasmAvailable && update.snapshot.core.channels.length === 380;
    this.element.setAttribute("aria-busy", String(update.pending));
    const wasDisabled = this.field<HTMLButtonElement>("begin").disabled;
    this.element.querySelectorAll<HTMLButtonElement>("button").forEach(button => {
      const disabled = !ready || update.pending || this.starting;
      button.setAttribute("aria-disabled", String(disabled));
      button.disabled = disabled && button !== document.activeElement;
    });
    this.field("connection").textContent = update.pending ? "Preparing your core…" : ready ? "Live reactor ready." : update.status.detail;
    if (wasDisabled && ready && !update.pending && (document.activeElement === document.body || document.activeElement?.tagName === "CANVAS")) this.field("begin").focus({ preventScroll: true });
    const shift = update.snapshot.shift;
    this.field("objective").textContent = shift ? `${shift.title} · seed ${shift.seed} · ${shift.fuelBudget} fresh bundles` : "Waiting for the live core.";
    // Preserve an unsubmitted seed/objective through pending/status emissions.
    if (shift && shift.seed !== this.lastSeed) { this.field<HTMLInputElement>("seed").value = String(shift.seed); this.lastSeed = shift.seed; }
    if (shift && shift.id !== this.lastShift) { this.field<HTMLSelectElement>("shift").value = shift.id; this.lastShift = shift.id; }
    if (update.error) this.field("error").textContent = update.error;
  }

  private async reset(command: CanduCommand): Promise<void> {
    if (this.session.isPending || !this.session.status.isWasmAvailable) return;
    this.field("error").textContent = "";
    try { const response = await this.session.dispatch(command); if (!response.accepted) this.field("error").textContent = response.message; }
    catch (error) { this.field("error").textContent = error instanceof Error ? error.message : String(error); }
  }

  private readonly onSubmit = (event: SubmitEvent): void => {
    event.preventDefault();
    const seed = this.field<HTMLInputElement>("seed");
    if (!seed.reportValidity()) return;
    void this.reset({ type: "reset", seed: Number(seed.value), shiftId: this.field<HTMLSelectElement>("shift").value as ShiftId });
  };

  private readonly onClick = (event: MouseEvent): void => {
    const action = (event.target as Element).closest("button")?.dataset.action;
    if ((event.target as Element).closest("button")?.getAttribute("aria-disabled") === "true") return;
    if (action === "begin" && !this.starting && !this.session.isPending && this.session.status.isWasmAvailable) {
      this.starting = true; this.begin();
    }
    if (action === "next") void this.reset({ type: "reset", seed: ((this.session.snapshot.shift?.seed ?? 1001) + 1) >>> 0,
      shiftId: this.field<HTMLSelectElement>("shift").value as ShiftId });
  };

  destroy(): void {
    this.unsubscribe(); this.element.removeEventListener("click", this.onClick);
    this.element.querySelector("form")!.removeEventListener("submit", this.onSubmit); this.element.remove();
  }
}
