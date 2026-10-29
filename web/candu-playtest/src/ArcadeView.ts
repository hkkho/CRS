import { CoreMapZoom, exitBurnup } from "./CoreMapInteraction";
import { mapLegend } from "./studio/powerReadings";
import { randomCoreSeed } from "./coreSeed";
import { gridCoordinateLabel } from "./channelCoordinates";
import { isRunTerminal, type CanduCommand } from "./protocol";
import type { SessionUpdate } from "./sessionController";
import type { WorkspaceSession } from "./studio/StudioNavigation";
import "./arcade.css";
import { ArcadeMap } from "./ArcadeMap";
import { FuelOrderList } from "./FuelOrderList";

/** Touch presentation of the Game-owned useful-fuel challenge. No local simulation or victory rules. */
export class ArcadeView {
  readonly element = document.createElement("main");
  private readonly unsubscribe: () => void;
  private plan: number[];
  private selected = 0;
  private readonly zoom: CoreMapZoom;
  private mapMode: "power" | "burnup" = "power";
  private readonly map: ArcadeMap;
  private readonly orders = new FuelOrderList("remove-sheet-channel");
  private busy = false;
  private reviewing = false;
  private disposed = false;
  private update: SessionUpdate;

  constructor(private readonly session: WorkspaceSession, parent: HTMLElement, private readonly studio: () => void) {
    this.plan = [...session.dailyPlan ?? []];
    this.update = { snapshot: session.snapshot, status: session.status, pending: session.isPending, response: null, error: null };
    this.element.className = "reactor-arcade";
    this.element.innerHTML = `<header><p class="arcade-kicker">CRS <span>/ ARCADE</span></p><span class="arcade-round">ENDLESS FREE PRACTICE</span></header>
      <div class="arcade-hud"><p>POINTS<strong data-score></strong></p></div>
      <section class="arcade-mission"><p class="arcade-kicker" data-phase>01 / PICK YOUR CHANNELS</p><h1>Make your move.</h1><p data-mission></p></section>
      <ol class="arcade-journey" aria-label="Challenge stages"><li data-stage="pick"><span>01</span> Explore</li><li data-stage="review"><span>02</span> Plan</li><li data-stage="result"><span>03</span> Result</li></ol>
      <section class="arcade-result" aria-live="polite" hidden><span class="arcade-medal" aria-hidden="true">★</span><h2 data-outcome tabindex="-1"></h2><p data-result></p>
      <div class="arcade-retry"><button class="arcade-primary" data-action="retry">Play again</button><button data-action="new">New core</button></div></section>
      <section data-picker aria-label="Choose fuel channels"><div class="arcade-map-toolbar"><label>Core map <select data-map-mode><option value="power">Power</option><option value="burnup">Burnup</option></select></label></div>
      <p class="arcade-caption">Tap a channel to inspect. Gold border = in your plan.</p><div class="arcade-map-scroll" data-map-scroll></div><p class="arcade-legend" data-legend></p>
      <section class="arcade-inspector" aria-label="Selected channel"><div><p class="arcade-kicker">SELECTED CHANNEL</p><h2 data-selected></h2></div><p data-reading></p><details class="arcade-outgoing"><summary data-exit-burnup></summary><p data-outgoing></p></details><button class="arcade-primary" data-action="toggle-channel">Add to plan · 8 bundles</button><p data-eligibility role="status"></p></section></section>
      <details class="arcade-details"><summary>How to play & reactor details</summary><p data-objective></p><p>Replace used fuel and survive the day. Score rewards a smooth power distribution. Planning is untimed.</p><p>Zone level: <strong data-level></strong>. Keep it at 10–90%, global tilt within ±20%, channels at or below 7,100 kW and bundles at or below 900 kW.</p><p>The selected channel shows the range of burnup in the outgoing fuel. Higher burnup means more-used fuel, not a guarantee of a safe move.</p><p data-seed></p><button data-action="studio">Open detailed Studio</button></details>
      <p data-message role="status"></p><footer><button class="arcade-primary" data-action="commit">Review picks →</button><button class="arcade-primary" data-action="confirm" hidden>Run the day ▶</button></footer>`;
    this.selected = session.snapshot.core.channels[0]?.channelIndex ?? 0;
    this.map = new ArcadeMap(session.snapshot, index => { this.selected = index; this.render(); }, index => this.toggleChannel(index));
    this.field("[data-map-scroll]").append(this.map.element);
    const sheet = document.createElement("aside"); sheet.className = "fuel-sheet arcade-fuel-sheet";
    sheet.setAttribute("aria-label", "Draft fuelling list");
    sheet.innerHTML = `<header class="fuel-sheet-header"><span>CORE SERVICES / F-08</span><span>DRAFT</span></header><h2 tabindex="-1" data-review-title>Fuelling list</h2><p class="fuel-sheet-reference">Eight fresh bundles per channel · with flow</p><div data-arcade-orders></div><p class="fuel-sheet-total"><span>FRESH BUNDLES REQUIRED</span><strong data-arcade-fuel-total></strong></p><p data-stock class="fuel-sheet-stock"></p><div class="arcade-sheet-actions"><button data-action="back" hidden>← Change picks</button><button data-action="clear">Clear all</button></div>`;
    sheet.querySelector("[data-arcade-orders]")!.append(this.orders.element); this.field("[data-picker]").append(sheet);
    sheet.append(this.element.querySelector("footer")!);
    this.zoom = new CoreMapZoom(this.field("[data-map-scroll]"), this.map.element);
    this.element.querySelector("[data-map-mode]")!.addEventListener("change", this.onMapMode);
    parent.append(this.element);
    this.element.addEventListener("click", this.onClick);
    this.unsubscribe = session.subscribe(update => { this.update = update; this.render(); });
  }

  private field(selector: string): HTMLElement { return this.element.querySelector(selector)!; }
  private render(): void {
    if (this.disposed) return;
    const { snapshot: s, status, pending } = this.update;
    const locked = this.busy || pending || !status.isWasmAvailable;
    const terminal = isRunTerminal(s);
    const shift = s.shift;
    this.element.setAttribute("aria-busy", String(this.busy || pending));
    this.element.dataset.phase = locked ? "busy" : terminal ? (shift?.rewardEarned ? "won" : "ended") : this.reviewing ? "review" : "pick";
    this.element.querySelectorAll<HTMLElement>("[data-stage]").forEach(stage => {
      if (stage.dataset.stage === (terminal ? "result" : this.reviewing ? "review" : "pick")) stage.setAttribute("aria-current", "step");
      else stage.removeAttribute("aria-current");
    });
    this.field("[data-phase]").textContent = locked ? "RUNNING YOUR CORE…" : terminal ? "ROUND COMPLETE" : this.reviewing ? "02 / LOCK IN YOUR PLAN" : "01 / PICK YOUR CHANNELS";
    this.field("[data-mission]").textContent = shift?.isEndless ? "Keep power close to its reference. Build score, one day at a time." : shift?.objective ?? "Keep the core balanced.";
    this.field(".arcade-round").textContent = `FREE PRACTICE / DAY ${(s.completedDays ?? 0) + 1}`;
    this.field("[data-seed]").textContent = `Core seed ${shift?.seed ?? "—"}`;
    this.field("[data-picker]").hidden = terminal;
    this.field("[data-action=back]").hidden = !this.reviewing;
    this.field("[data-action=clear]").hidden = this.reviewing;
    this.field("[data-review-title]").textContent = this.reviewing ? "Review today's sheet" : "Fuelling list";
    this.field(".fuel-sheet-header > span:last-child").textContent = locked ? "SUBMITTED" : this.reviewing ? "REVIEW" : "DRAFT";
    this.field("footer").hidden = terminal;
    this.field('[data-action="commit"]').hidden = this.reviewing;
    this.field('[data-action="confirm"]').hidden = !this.reviewing;
    this.field("[data-objective]").textContent = shift?.objective ?? "Waiting for the challenge…";
    this.field("[data-score]").textContent = s.scoreTotal.toFixed(2);
    this.field("[data-level]").textContent = `${(s.rrs.averageFillFraction * 100).toFixed(1)}%`;
    this.field("[data-stock]").textContent = s.shift?.unlimitedFreshFuel ? "Unlimited fresh fuel" : `${s.freshBundlesAvailable} fresh bundles available`;
    this.orders.update(s, this.plan, locked || terminal || this.reviewing);
    this.field("[data-arcade-fuel-total]").textContent = String(this.plan.length * 8);
    this.field(".arcade-result").hidden = !terminal;
    this.field("[data-outcome]").textContent = shift?.rewardEarned ? "Challenge cleared!" : shift?.outcome === "missed" ? "Goal missed — try another plan" : "Run ended";
    this.field("[data-result]").textContent = shift?.isEndless ? `${s.completedDays ?? 0} days completed · ${s.scoreTotal.toFixed(2)} points. ${s.runEndReason ?? ""}` : `${shift?.usefulBundlesDischarged ?? 0} / ${shift?.usefulBundlesRequired ?? 8} useful bundles discharged · ${s.scoreTotal.toFixed(2)} points. ${s.runEndReason ?? ""}${shift?.rewardEarned ? ` · ${shift.reward}` : ""}`;
    this.map.update(s, this.selected, this.plan, this.mapMode, locked || terminal || this.reviewing);
    this.field("[data-legend]").textContent = mapLegend(s, this.mapMode);
    const channel = s.core.channels.find(c => c.channelIndex === this.selected);
    const included = this.plan.includes(this.selected);
    this.field("[data-selected]").textContent = channel ? gridCoordinateLabel(channel) : "Select a channel";
    this.field("[data-reading]").textContent = channel ? `${(channel.powerWatts / 1000).toFixed(0)} kW · ${channel.averageBurnupMwdPerKg.toFixed(1)} MWd/kg average` : "";
    const exit = channel ? exitBurnup(s, channel) : null;
    this.field("[data-exit-burnup]").textContent = `Estimated average exit burnup: ${exit === null ? "unavailable" : `${exit.toFixed(2)} MWd/kg`}`;
    const movement = s.refuellingPlans?.find(p => p.shiftCount === 8 && p.directionId === channel?.flowDirection);
    const outgoing = movement?.dischargedPositions.map(position => channel?.bundles.find(b => b.position === position)?.currentBurnupMwdPerKg);
    this.field("[data-outgoing]").textContent = `Outgoing fuel: ${outgoing?.map(v => v?.toFixed(1) ?? "?").join(" · ") ?? "unavailable"} MWd/kg`;
    this.field('[data-action="toggle-channel"]').textContent = included ? "Remove from plan" : "Add to plan · 8 bundles";
    const cannotAdd = !channel || channel.canRefuel === false || (!included && !s.shift?.unlimitedFreshFuel && (this.plan.length + 1) * 8 > s.freshBundlesAvailable);
    this.field("[data-eligibility]").textContent = channel?.canRefuel === false ? channel.refuellingIneligibilityReason || "This channel cannot be refuelled." : cannotAdd ? "Not enough fresh fuel for another channel." : included ? "Queued for today. Pick another channel on the map." : "";
    this.element.querySelectorAll<HTMLButtonElement>("[data-action]").forEach(button => {
      const action = button.dataset.action;
      button.disabled = locked || (action === "remove-sheet-channel" && (terminal || this.reviewing)) || (action === "toggle-channel" && (terminal || this.reviewing || cannotAdd)) ||
        (action === "clear" && (terminal || !this.plan.length)) || (["commit", "confirm"].includes(action ?? "") && (terminal || !s.shift?.unlimitedFreshFuel && this.plan.length * 8 > s.freshBundlesAvailable));
    });
    this.field('[data-action="commit"]').textContent = locked ? "Running…" : this.plan.length ? "Review picks →" : "Review no-fuel day →";
    this.field('[data-action="confirm"]').textContent = locked ? "Running…" : "Run the day ▶";
    if (this.update.error) this.field("[data-message]").textContent = this.update.error;
  }

  private toggleChannel(index: number): void {
    const s = this.update.snapshot, included = this.plan.includes(index), channel = s.core.channels.find(c => c.channelIndex === index);
    if (this.busy || this.session.isPending || !this.update.status.isWasmAvailable || isRunTerminal(s) || this.reviewing) return;
    if (!included && (!channel || channel.canRefuel === false || !s.shift?.unlimitedFreshFuel && (this.plan.length + 1) * 8 > s.freshBundlesAvailable)) return;
    this.selected = index; this.setPlan(included ? this.plan.filter(i => i !== index) : [...this.plan, index]);
    this.field("[data-eligibility]").textContent = `${channel ? gridCoordinateLabel(channel) : index} ${included ? "removed from" : "added to"} today's sheet.`;
  }
  private setPlan(plan: number[]): void { this.plan = plan.sort((a, b) => a - b); this.session.setDailyPlan?.(this.plan); this.render(); }
  private async send(command: CanduCommand): Promise<void> {
    this.busy = true; this.field("[data-message]").textContent = command.type === "reset" ? "Preparing your core…" : "Running your fuel plan. Hang tight…"; this.render();
    try {
      const response = await this.session.dispatch(command);
      if (this.disposed) return;
      if (response.accepted) { this.reviewing = false; this.setPlan([]); }
      this.field("[data-message]").textContent = command.type === "commit-day" && response.accepted && !isRunTerminal(response.snapshot)
        ? `Day ${response.snapshot.completedDays} complete · +${(response.snapshot.lastDayResult?.scoreDelta ?? 0).toFixed(2)} points · ${response.snapshot.lastDayResult?.fuelUsed ?? 0} bundles used. Plan your next day.` : response.message;
    } catch (error) { if (!this.disposed) this.field("[data-message]").textContent = `Unable to complete the order: ${String(error)}`; }
    finally { this.busy = false; this.render(); if (!this.disposed && isRunTerminal(this.update.snapshot)) this.field("[data-outcome]").focus(); }
  }
  private readonly onClick = (event: MouseEvent): void => {
    const button = (event.target as Element).closest<HTMLButtonElement>("button");
    if (!button || button.disabled || this.busy || this.session.isPending) return;
    if (button.dataset.action === "inspect-plan") { this.selected = Number(button.dataset.planChannel); this.render(); this.map.focusSelected(); return; }
    if (button.dataset.action === "remove-sheet-channel") { this.setPlan(this.plan.filter(i => i !== Number(button.dataset.planChannel))); return; }
    switch (button.dataset.action) {
      case "studio": this.studio(); break;
      case "toggle-channel": this.toggleChannel(this.selected); break;
      case "clear": this.setPlan([]); break;
      case "back": this.reviewing = false; this.render(); this.map.focusSelected(); break;
      case "commit": this.reviewing = true; this.render(); this.field('[data-review-title]').focus(); break;
      case "confirm": if (this.reviewing) void this.send({ type: "commit-day", expectedCompletedDays: this.update.snapshot.completedDays ?? 0, channelIndices: [...this.plan] }); break;
      case "retry": case "new": void this.send({ type: "reset", shiftId: "free-practice", seed: button.dataset.action === "new" ? randomCoreSeed(this.update.snapshot.shift?.seed) : this.update.snapshot.shift?.seed }); break;
    }
  };
  private readonly onMapMode = (event: Event): void => {
    this.mapMode = (event.target as HTMLSelectElement).value === "burnup" ? "burnup" : "power"; this.render();
  };
  destroy(): void { this.zoom.destroy(); this.map.destroy(); this.element.querySelector("[data-map-mode]")!.removeEventListener("change", this.onMapMode); this.disposed = true; this.unsubscribe(); this.element.removeEventListener("click", this.onClick); this.element.remove(); }
}
