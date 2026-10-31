import { reactorIllustration } from "./ReactorIllustration";
import { randomCoreSeed } from "./coreSeed";
import type { BridgeSessionController, SessionUpdate } from "./sessionController";
import "./workspace.css";
import { SimulatorManual } from "./SimulatorManual";
import { GameLoadingView } from "./GameLoadingView";

type LauncherSession = Pick<BridgeSessionController, "snapshot" | "status" | "isPending" | "subscribe" | "dispatch">;

/** Native launcher; starting a view never initializes another reactor. */
export class LauncherView {
  readonly element = document.createElement("main");
  private readonly unsubscribe: () => void;
  private lastSeed: number | undefined;
  private starting = false;
  private readonly manual: SimulatorManual;
  private loading?: GameLoadingView;

  constructor(private readonly session: LauncherSession, parent: HTMLElement, private readonly begin: (guided?: boolean) => void, private readonly arcade?: () => void, private readonly demo?: (seed: number) => void) {
    this.element.className = "reactor-launcher workspace-native";
    this.element.innerHTML = `<div class="launcher-shell">
      <header class="launcher-masthead"><span class="observatory-wordmark" aria-hidden="true">C<span>06</span></span><p class="workspace-kicker">CRS / A REACTOR STRATEGY GAME</p><span class="launcher-edition">PLAN. REFUEL. REPEAT.</span></header>
      <div class="launcher-hero"><div class="launcher-story"><p class="workspace-kicker">TAKE THE NEXT SHIFT.</p>
      <h1>CRS</h1><p class="launcher-headline">YOUR CORE.<br><em>YOUR CALL.</em></p><p class="launcher-tagline">Keep the balance. Make every bundle count.</p>
      <p class="launcher-intro">Pick your channels, build a fuel plan, and keep the core in balance. Choose turn based planning or keep time flowing with time based play.</p>
      <p class="launcher-description">A thoughtful reactor game, one day at a time. Shape a smoother power distribution with every fuel plan.</p>
      <div class="launcher-setup"><label>Presentation <select data-presentation><option value="studio">Studio · full strategy view</option><option value="arcade">Arcade · touch view</option></select></label><label><input type="checkbox" data-guided> Show me the ropes · guided practice</label><details><summary>Starting core</summary><label>Core seed<input data-field="seed" type="number" min="0" max="4294967295" step="1" required></label><button data-action="next">Random seed</button></details><p data-field="objective" hidden></p><p data-field="error" role="status"></p></div><div class="launcher-actions"><button data-action="begin" class="workspace-primary">Turn based <span aria-hidden="true">↗</span></button><button type="button" data-manual-open>Manual</button></div></div>
      <figure class="launcher-art"><div class="launcher-art-label"><span>REACTOR 06</span><span data-field="connection" role="status"></span></div>${reactorIllustration()}<figcaption><span><strong>380</strong> channels</span><span><strong>12</strong> bundle positions</span><span><strong>01</strong> day at a time</span></figcaption></figure></div>
      <ol class="observatory-loop" aria-label="The daily loop"><li><span>01</span><div><strong>Read the core</strong><p>Look for used fuel and watch the power shape.</p></div></li><li><span>02</span><div><strong>Make your plan</strong><p>Choose channels. Inspect what leaves.</p></div></li><li><span>03</span><div><strong>Meet tomorrow</strong><p>Advance a day. Read the result. Refine.</p></div></li></ol>
      <section class="launcher-goals" aria-label="Choose run objective"><div><p class="workspace-kicker">CHOOSE YOUR RUN</p><h2>Set your rhythm.</h2></div>${[
        ["free-practice", "ENDLESS / UNLIMITED FUEL", "Free practice", "Learn the rhythm. Unlimited fuel in either mode."],
      ].map(([id, label, title, copy]) => `<button data-action="goal" data-goal="${id}" aria-pressed="false"><span>${label}</span><strong>${title}</strong><small>${copy}</small></button>`).join("")}</section>
      <p class="launcher-footnote">DAILY STRATEGY OR LIVE POWER. FIND YOUR RHYTHM.</p>
    </div>`;
    const goals = this.element.querySelector(".launcher-goals")!;
    this.element.querySelector(".launcher-setup")!.before(goals);
    this.element.querySelector(".launcher-setup")!.before(this.element.querySelector(".launcher-actions")!);
    this.element.querySelector(".observatory-loop")!.remove();
    this.element.querySelector(".launcher-description")!.remove();
    parent.append(this.element);
    if (demo) {
      const button = document.createElement("button");
      button.type = "button"; button.dataset.action = "demo"; button.className = "workspace-primary";
      button.textContent = "Time based";
      this.element.querySelector(".launcher-actions")!.prepend(button);
    }
    this.manual = new SimulatorManual(this.element);
    this.element.addEventListener("click", this.onClick);
    this.element.addEventListener("change", this.onSelectionChange);

    this.unsubscribe = session.subscribe(update => this.render(update));
    this.field<HTMLButtonElement>(demo ? "demo" : "begin").focus({ preventScroll: true });
  }

  private field<T extends HTMLElement>(name: string): T {
    return this.element.querySelector(`[data-field="${name}"], [data-action="${name}"]`)! as T;
  }

  private render(update: SessionUpdate): void {
    const ready = update.status.isWasmAvailable && update.snapshot.core.channels.length === 380;
    this.element.setAttribute("aria-busy", String(update.pending));
    const wasDisabled = this.field<HTMLButtonElement>("begin").disabled;
    this.element.querySelectorAll<HTMLButtonElement>("button").forEach(button => {
      if (button.hasAttribute("data-manual-open") || button.closest(".simulator-manual, .game-loading-dialog")) return;
      const disabled = !ready || update.pending || this.starting;
      button.setAttribute("aria-disabled", String(disabled));
      button.disabled = disabled && button !== document.activeElement;
    });
    this.field("connection").textContent = update.pending ? "Preparing your core…" : ready
      ? update.snapshot.runStatus === "ended" ? `Starting core has ended: ${update.snapshot.runEndReason}` : "Your core is ready."
      : update.status.detail;
    if (wasDisabled && ready && !update.pending && (document.activeElement === document.body || document.activeElement?.tagName === "CANVAS")) this.field("begin").focus({ preventScroll: true });
    const shift = update.snapshot.shift;
    this.field("objective").textContent = shift ? `${shift.title} · seed ${shift.seed} · ${shift.unlimitedFreshFuel ? "Unlimited fresh fuel" : `${shift.fuelBudget} fresh bundles`}` : "Waiting for the live core.";
    // Preserve an unsubmitted seed/objective through pending/status emissions.
    if (shift && shift.seed !== this.lastSeed) { this.field<HTMLInputElement>("seed").value = String(shift.seed); this.lastSeed = shift.seed; }
    this.renderSelection();
    if (update.error) this.field("error").textContent = update.error;
  }

  private renderSelection(): void {
    const guided = this.element.querySelector<HTMLInputElement>("[data-guided]")!.checked;
    const goal = "free-practice";
    this.element.querySelectorAll<HTMLButtonElement>("[data-goal]").forEach(b => b.setAttribute("aria-pressed", String(b.dataset.goal === goal)));
    const label = this.element.querySelector(`[data-goal="${goal}"] strong`)?.textContent ?? goal;
    this.field("objective").textContent = guided ? "Guided practice · seed 1001 · unlimited fuel" : `Selected: ${label}`;
  }

  private readonly onSelectionChange = (): void => { this.renderSelection(); };

  private readonly onClick = (event: MouseEvent): void => {
    const button = (event.target as Element).closest<HTMLButtonElement>("button");
    if (!button || button.disabled || button.getAttribute("aria-disabled") === "true") return;
    if (button.dataset.action === "goal") {
      this.element.querySelector<HTMLSelectElement>("[data-presentation]")!.value = "studio";
      this.element.querySelector<HTMLInputElement>("[data-guided]")!.checked = false;
      this.renderSelection();
    }
    if (button.dataset.action === "next") this.field<HTMLInputElement>("seed").value = String(randomCoreSeed(Number(this.field<HTMLInputElement>("seed").value)));
    if (button.dataset.action === "begin") void this.start();
    if (button.dataset.action === "demo") {
      const seed = this.field<HTMLInputElement>("seed");
      if (seed.reportValidity()) this.demo?.(Number(seed.value));
    }
  };

  private async start(): Promise<void> {
    if (this.starting || this.session.isPending || !this.session.status.isWasmAvailable) return;
    const guided = this.element.querySelector<HTMLInputElement>("[data-guided]")!.checked;
    const arcade = !guided && this.element.querySelector<HTMLSelectElement>("[data-presentation]")!.value === "arcade";
    const seed = this.field<HTMLInputElement>("seed"); if (!guided && !seed.reportValidity()) return;
    const shiftId = "free-practice";
    const startSeed = guided ? 1001 : Number(seed.value);
    this.starting = true; this.field("error").textContent = "";
    const loading = this.loading = new GameLoadingView(this.element, () => {
      this.loading = undefined;
      if (arcade && this.arcade) this.arcade(); else this.begin(guided);
    }, () => {
      this.loading = undefined; this.starting = false;
      this.render({ snapshot: this.session.snapshot, status: this.session.status, pending: this.session.isPending, response: null, error: null });
      this.field<HTMLButtonElement>("begin").focus({ preventScroll: true });
    });
    try {
      if (guided || arcade || startSeed !== this.session.snapshot.shift?.seed || shiftId !== this.session.snapshot.shift?.id) {
        const response = await this.session.dispatch({ type: "reset", seed: startSeed, shiftId });
        if (this.loading !== loading) return;
        if (!response.accepted) { this.field("error").textContent = response.message; loading.update(false, response.message); return; }
      }
      if (this.loading === loading) loading.update(true);
    } catch (error) { if (this.loading === loading) { this.field("error").textContent = String(error); loading.update(false, String(error)); } }
    finally { if (this.element.isConnected && !this.starting) this.render({ snapshot: this.session.snapshot, status: this.session.status, pending: this.session.isPending, response: null, error: null }); }
  }

  destroy(): void {
    this.loading?.destroy();
    this.manual.destroy();
    this.unsubscribe(); this.element.removeEventListener("click", this.onClick);
    this.element.removeEventListener("change", this.onSelectionChange);
    this.element.remove();
  }
}
