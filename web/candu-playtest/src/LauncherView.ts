import { randomCoreSeed } from "./coreSeed";
import type { CanduCommand, ShiftId } from "./protocol";
import type { BridgeSessionController, SessionUpdate } from "./sessionController";
import "./workspace.css";
import { SimulatorManual } from "./SimulatorManual";

type LauncherSession = Pick<BridgeSessionController, "snapshot" | "status" | "isPending" | "subscribe" | "dispatch">;

/** Native launcher; starting a view never initializes another reactor. */
export class LauncherView {
  readonly element = document.createElement("main");
  private readonly unsubscribe: () => void;
  private lastSeed: number | undefined;
  private lastShift: string | undefined;
  private starting = false;
  private readonly manual: SimulatorManual;

  constructor(private readonly session: LauncherSession, parent: HTMLElement, private readonly begin: (guided?: boolean) => void, private readonly arcade?: () => void) {
    this.element.className = "reactor-launcher workspace-native";
    this.element.innerHTML = `<div class="launcher-shell">
      <header class="launcher-masthead"><span class="observatory-wordmark" aria-hidden="true">C<span>06</span></span><p class="workspace-kicker">CRS / THE REACTOR OBSERVATORY</p><span class="launcher-edition">ON-POWER REFUELLING</span></header>
      <div class="launcher-hero"><div class="launcher-story"><p class="workspace-kicker">A SMALL DECISION. A CHANGING CORE.</p>
      <h1>CRS</h1><p class="launcher-headline">Find your<br><em>balance.</em></p>
      <p class="launcher-intro">Read the core. Renew the fuel. See what tomorrow brings.</p>
      <p class="launcher-description">A thoughtful reactor game, one day at a time. Shape a smoother power distribution with every fuel plan.</p>
      <div class="launcher-actions"><button data-action="begin" class="workspace-primary">Begin shift</button><button data-action="arcade">Play Arcade · phone friendly</button></div>
      <p class="launcher-mode-note">Studio for deep inspection. Arcade for a focused one-day challenge.</p><button class="launcher-guide" data-action="guided">First shift · guided</button><p class="launcher-mode-note">Optional introduction · seed 1001 · unlimited practice fuel</p></div>
      <figure class="launcher-art" aria-label="Illustration of a reactor vessel and its fuel channels"><svg viewBox="0 0 600 580" role="img"><title>Reactor vessel study</title><defs><radialGradient id="observatory-glow"><stop stop-color="#244f4a"/><stop offset="1" stop-color="#0b171c"/></radialGradient><linearGradient id="observatory-metal" x2="1" y2="1"><stop stop-color="#79b4a8"/><stop offset=".45" stop-color="#243e41"/><stop offset="1" stop-color="#0c1b22"/></linearGradient></defs>
      <g fill="none" stroke="#395653"><circle cx="300" cy="278" r="245" stroke-dasharray="1 9"/><circle cx="300" cy="278" r="223"/><path d="M300 11v50M300 495v48M28 278h55M517 278h55"/></g>
      <ellipse cx="324" cy="305" rx="203" ry="205" fill="#050d12"/><circle cx="300" cy="278" r="205" fill="url(#observatory-metal)" stroke="#74a497"/><circle cx="300" cy="278" r="187" fill="url(#observatory-glow)" stroke="#466c64"/>
      <g>${Array.from({ length: 17 }, (_, row) => Array.from({ length: 17 }, (_, col) => {
        const x = (col - 8) * 19, y = (row - 8) * 19;
        if (x * x + y * y > 164 * 164) return "";
        const warm = (col + row * 3) % 11 < 3;
        return `<circle cx="${300 + x}" cy="${278 + y}" r="6.5" fill="${warm ? "#bfa16b" : "#467e72"}" stroke="${warm ? "#e8cd92" : "#80b5a1"}" stroke-width="1.2"/><circle cx="${300 + x}" cy="${278 + y}" r="2" fill="#0c1c21"/>`;
      }).join("")).join("")}</g><g fill="none" stroke="#d1b47a"><path d="M417 149l57-43h64M182 408l-54 53H67"/><circle cx="417" cy="149" r="10"/><circle cx="182" cy="408" r="10"/></g><g fill="#a8bcb5" font-family="monospace" font-size="10" letter-spacing="2"><text x="450" y="93">CANDU / 06</text><text x="50" y="482">FUEL CHANNELS</text><text x="300" y="563" text-anchor="middle">VESSEL STUDY / ON-POWER REFUELLING</text></g></svg><figcaption><span>380 channels</span><span>12 bundle positions</span><span>One evolving core</span></figcaption></figure></div>
      <ol class="observatory-loop" aria-label="The daily loop"><li><span>01</span><div><strong>Read the core</strong><p>Look for used fuel and watch the power shape.</p></div></li><li><span>02</span><div><strong>Make your plan</strong><p>Choose channels. Inspect what leaves.</p></div></li><li><span>03</span><div><strong>Meet tomorrow</strong><p>Advance a day. Read the result. Refine.</p></div></li></ol>
      <section class="launcher-goals" aria-label="Multi-day goals"><div><p class="workspace-kicker">MAKE A RUN OF IT</p><h2>A rhythm. Then a whole week.</h2><p>One stock of fuel, carried across every day. Same reactor rules. A longer decision.</p></div><button data-action="goal" data-goal="three-day-rhythm-v1"><span>03 DAYS / 128 BUNDLES</span><strong>Three-day rhythm</strong><small>Complete three days. Find your fuelling rhythm.</small><b>Start this run →</b></button><button data-action="goal" data-goal="seven-day-stewardship-v1"><span>07 DAYS / 128 BUNDLES</span><strong>Seven-day stewardship</strong><small>Complete the week. Recover 48 useful bundles at ≥6 MWd/kg.</small><b>Start this run →</b></button></section>
      <div class="launcher-grid"><section><p class="workspace-kicker">YOUR NEXT SHIFT</p><h2>Ready when you are.</h2>
        <p data-field="objective"></p><p data-field="connection" role="status"></p>
        <button type="button" data-manual-open>Simulator manual <span aria-hidden="true">↗</span></button>
      </section><section><p class="workspace-kicker">A DIFFERENT START</p><h2>Choose a starting core</h2>
        <form><label>Core seed <input data-field="seed" type="number" min="0" max="4294967295" step="1" required></label>
        <label>Shift objective <select data-field="shift"><option value="free-practice">Free practice · Endless</option><option value="useful-fuel-day-v1">One-day challenge</option><option value="three-day-rhythm-v1">Three-day rhythm</option><option value="seven-day-stewardship-v1">Seven-day stewardship</option></select></label>
        <button data-action="apply" type="submit">Use seed & objective</button></form>
        <button data-action="next">New aged core</button>
        <p data-field="error"></p>
      </section></div>
      <p class="launcher-footnote">Planning is untimed. Every core has a different story.</p>
    </div>`;
    parent.append(this.element);
    this.manual = new SimulatorManual(this.element);
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
      if (button.hasAttribute("data-manual-open") || button.closest(".simulator-manual")) return;
      const disabled = !ready || update.pending || this.starting;
      button.setAttribute("aria-disabled", String(disabled));
      button.disabled = disabled && button !== document.activeElement;
    });
    this.field("connection").textContent = update.pending ? "Preparing your core…" : ready
      ? update.snapshot.runStatus === "ended" ? `Starting core has ended: ${update.snapshot.runEndReason}` : "Live reactor ready."
      : update.status.detail;
    if (wasDisabled && ready && !update.pending && (document.activeElement === document.body || document.activeElement?.tagName === "CANVAS")) this.field("begin").focus({ preventScroll: true });
    const shift = update.snapshot.shift;
    this.field("objective").textContent = shift ? `${shift.title} · seed ${shift.seed} · ${shift.unlimitedFreshFuel ? "Unlimited fresh fuel" : `${shift.fuelBudget} fresh bundles`}` : "Waiting for the live core.";
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
    if (action === "guided") { void this.startGoal("free-practice", true); return; }
    if (action === "goal") { void this.startGoal((event.target as Element).closest<HTMLButtonElement>("button")!.dataset.goal as ShiftId); return; }
    if (action === "begin" && !this.starting && !this.session.isPending && this.session.status.isWasmAvailable) {
      this.starting = true; this.begin();
    }
    if (action === "next") void this.reset({ type: "reset", seed: randomCoreSeed(this.session.snapshot.shift?.seed),
      shiftId: this.field<HTMLSelectElement>("shift").value as ShiftId });
    if (action === "arcade" && this.arcade && !this.starting && !this.session.isPending) {
      this.starting = true;
      void this.session.dispatch({ type: "reset", seed: this.session.snapshot.shift?.seed ?? randomCoreSeed(), shiftId: "useful-fuel-day-v1" })
        .then(response => { if (response.accepted) this.arcade?.(); else { this.starting = false; this.field("error").textContent = response.message; } })
        .catch(error => { this.starting = false; this.field("error").textContent = String(error); })
        .finally(() => {
          if (this.element.isConnected && !this.starting) this.render({ snapshot: this.session.snapshot, status: this.session.status,
            pending: this.session.isPending, response: null, error: null });
        });
    }
  };

  private async startGoal(shiftId: ShiftId, guided = false): Promise<void> {
    if (this.starting || this.session.isPending || !this.session.status.isWasmAvailable) return;
    this.starting = true;
    try {
      const response = await this.session.dispatch({ type: "reset", seed: guided ? 1001 : this.session.snapshot.shift?.seed ?? randomCoreSeed(), shiftId });
      if (response.accepted) this.begin(guided);
      else { this.starting = false; this.field("error").textContent = response.message; }
    } catch (error) { this.starting = false; this.field("error").textContent = String(error); }
    finally { if (this.element.isConnected && !this.starting) this.render({ snapshot: this.session.snapshot, status: this.session.status, pending: this.session.isPending, response: null, error: null }); }
  }

  destroy(): void {
    this.manual.destroy();
    this.unsubscribe(); this.element.removeEventListener("click", this.onClick);
    this.element.querySelector("form")!.removeEventListener("submit", this.onSubmit); this.element.remove();
  }
}
