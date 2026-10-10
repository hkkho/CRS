import { randomCoreSeed } from "../coreSeed";
import { isRunTerminal, type CanduCommandResponse } from "../protocol";
import type { SessionUpdate } from "../sessionController";
import type { CanduChannelSnapshot, CanduCommand, CanduSnapshot, PlaybackModeId } from "../protocol";
import { canIssueRefuel, createRefuelDraft, formatRefuelDirection, toRefuelRequest, type RefuelDraft } from "../commandState";
import { highestBurnupChannel, isChannelRefuellable, channelHeadroom, operationGuidance } from "../gameplayPresentation";
import { findAdjacentChannelIndex, gridCoordinateLabel } from "../channelCoordinates";
import { formatEffectiveK, formatLiquidZoneRegion, formatSimulationTime, formatClockDuration, getOverallStatus, getPowerLabel, getTiltLabel } from "../visuals";
import "./studio.css";
import type { PaceReading } from '../observedPace';
import { HistoryView } from "./HistoryView";

import type { StudioNavigation, StudioSession } from "./StudioNavigation";
export type { StudioNavigation } from "./StudioNavigation";
import { StudioMapView } from "./StudioMapView";
import { DayCalculationView } from "./DayCalculationView";
import { DailyPlanView } from "./DailyPlanView";
import { StudioOrderView } from "./StudioOrderView";
import { StudioStatusView } from "./StudioStatusView";
import { SessionPresentation } from "../SessionPresentation";
import { channelWatts, channelLimit, bundleLimit, channelColorMinimum, bundleColorMinimum, type MapMode } from "./powerReadings";
import { adjusterSummary } from "./devicePresentation";

/** A presentation of the existing bridge session, with no reactor state or rules. */
export class StudioView {
  public readonly element = document.createElement("section");
  private snapshot: CanduSnapshot;
  private readonly dailyView = new DailyPlanView();
  private readonly calculationView: DayCalculationView;
  private localDailyPlan: number[] = [];
  private get dailyPlan(): readonly number[] { return this.session.dailyPlan ?? this.localDailyPlan; }
  private setDailyPlan(channels: readonly number[]): void {
    if (this.session.isPending || this.inspectionSnapshot || isRunTerminal(this.snapshot)) return;
    this.localDailyPlan = [...new Set(channels)].sort((a, b) => a - b);
    this.session.setDailyPlan?.(this.localDailyPlan); this.render();
  }
  private draft: RefuelDraft | null = null;
  private selected = -1;
  private mapMode: MapMode = "burnup";
  private inspectionSnapshot: CanduSnapshot | null = null;
  private lastHandledResponse: CanduCommandResponse | null = null;
  private showedEnding = false;
  private readonly mapView: StudioMapView;
  private readonly orderView: StudioOrderView;
  private readonly statusView: StudioStatusView;
  private readonly presentation: SessionPresentation;
  private impactKey = "";
  private readonly fields = new Map<string, HTMLElement>();
  private readonly unsubscribe: () => void;
  private readonly historyView: HistoryView;
  private pace: PaceReading | undefined;
  private dayProgress: SessionUpdate["dayProgress"];

  public constructor(
    private readonly session: StudioSession,
    parent: HTMLElement,
    initial: StudioNavigation = {},
  ) {
    this.snapshot = session.snapshot;
    this.element.className = "reactor-studio";
    this.element.tabIndex = -1;
    this.element.setAttribute("aria-label", "Reactor Studio game interface");
    this.element.innerHTML = `
      <div class="studio-shell">
        <header class="studio-header">
          <div class="studio-brand"><span class="studio-emblem">C<span>06</span></span><div><p class="studio-eyebrow">CANDU 6 / ON-POWER REFUELLING</p><h1>Reactor Studio<span class="studio-brand-dot">.</span></h1></div></div>
          <nav aria-label="Game controls"><button data-action="challenge" class="studio-quiet" data-field="challenge-button">One-day challenge</button><button data-action="reset" class="studio-quiet">New shift</button></nav>
        </header>
        <section class="studio-objective studio-card" aria-label="Shift objective" data-field="objective-card">
          <div><p class="studio-eyebrow" data-field="objective-title"></p><p data-field="objective"></p><small data-field="objective-reward"></small><p data-field="provenance" class="studio-provenance"></p></div>
          <div class="studio-objective-progress"><strong data-field="objective-progress"></strong><small data-field="remaining"></small></div>
        </section>
        <section class="studio-ending studio-card" data-field="ending" hidden aria-labelledby="studio-ending-title">
          <p class="studio-eyebrow">SHIFT REPORT</p><h2 id="studio-ending-title" data-field="ending-title" tabindex="-1"></h2>
          <p data-field="ending-provenance" class="studio-provenance"></p><p data-field="ending-reason"></p><p data-field="ending-reward"></p>
          <div class="studio-report-grid"><p>Energy delivered<strong data-field="energy"></strong><small data-field="thermal-energy"></small></p><p>Fuel consumed<strong data-field="fuel-used"></strong><small data-field="useful-fuel"></small></p><p>Score components<strong data-field="final-score"></strong><small data-field="score-components"></small></p></div>
          <div class="studio-report-actions"><button data-action="retry" class="studio-primary">Retry same seed</button><button data-action="new-seed">Try new seed</button></div>
        </section>
        <section class="studio-metrics" aria-label="Live reactor status">
          <article><span class="studio-eyebrow">LZC AVERAGE LEVEL</span><strong data-field="reserve"></strong><small>Keep between 10% and 90%</small><small data-field="status"></small></article>
          <article><span class="studio-eyebrow">RIPPLE SCORE</span><strong data-field="score"></strong><small data-field="ripple-score"></small><small><span data-field="operations"></span> fuel moves completed</small></article>
          <article><span class="studio-eyebrow">FRESH BUNDLES</span><strong data-field="stock"></strong><small>8 bundles per move · with flow</small></article>
          <article class="studio-clock"><span class="studio-eyebrow">SIMULATION CLOCK</span><strong data-field="time"></strong><div class="studio-segments" aria-label="Playback speed"><button data-action="pause" aria-label="Pause simulation" data-field="pause">Ⅱ</button><button data-action="speed" data-speed="1x">1×</button><button data-action="speed" data-speed="10x">10×</button><button data-action="speed" data-speed="60x">60×</button></div><small data-field="pace" title="Observed simulation minutes per real second includes time spent solving. Requested speed is a target; no catch-up work is queued."></small></article>
        </section>
        <div data-field="history"></div>
        <div id="studio-reactor-panel" role="tabpanel" aria-labelledby="studio-tab-reactor" data-field="reactor-panel">
        <main class="studio-workspace">
          <aside class="studio-card studio-watchlist">
            <p class="studio-eyebrow">01 / INSPECT</p><h2>Fuel watchlist</h2><p class="studio-description">Ranked by observed average burnup, not age or predicted score. Compare both ends.</p>
            <button data-action="oldest" class="studio-outline">◎ Highest burnup</button>
            <div data-field="watchlist" class="studio-candidates"></div>
            <div class="studio-tip"><span class="studio-eyebrow">RIPPLE TARGET</span><p>Keep each channel close to its reference power. Lower RMS ripple earns more points over time.</p><span data-field="tilt"></span></div>
          </aside>
          <section class="studio-card studio-core">
            <div class="studio-card-heading"><div><p class="studio-eyebrow">380 CHANNELS / CORE</p><h2>The reactor face</h2></div><div class="studio-segments studio-map-modes"><button data-action="map" data-map="burnup">Burnup</button><button data-action="map" data-map="power">Channel kW</button><button data-action="map" data-map="ripple">Ripple %</button><button data-action="map" data-map="bundle-power">Bundle kW</button></div></div>
            <div class="studio-map-wrap"><svg data-field="map" viewBox="0 0 560 560" aria-label="Core channel map" role="group"><defs><radialGradient id="studio-vessel"><stop stop-color="#2d4945"/><stop offset="1" stop-color="#142421"/></radialGradient></defs><circle cx="280" cy="280" r="256" fill="url(#studio-vessel)"/><circle cx="280" cy="280" r="249" fill="none" stroke="#58716a" stroke-width="1"/><circle cx="280" cy="280" r="238" fill="none" stroke="#58716a" stroke-dasharray="2 8"/><path d="M280 10v32 M280 518v32 M10 280h32 M518 280h32" stroke="#a9c4b2" stroke-width="1"/><g data-field="map-labels" fill="#becbc1" font-size="10" font-family="monospace"></g><g data-field="channels"></g></svg><span class="studio-map-tag" data-field="map-tag"></span></div>
            <div class="studio-map-legend"><span class="studio-legend-gradient"></span><span data-field="legend">Fresh → higher burnup · MWd/kg HM</span><span>Arrows select</span></div>
            <div class="studio-devices"><button data-action="devices" aria-pressed="true">Show adjusters</button><span class="studio-lzc-planes" aria-label="Liquid-zone face plane"><button data-action="lzc-plane" data-plane="0" aria-pressed="true">End A LZC</button><button data-action="lzc-plane" data-plane="1" aria-pressed="false">End B LZC</button></span><p data-field="device-note"></p><p data-field="lzc-device-note">Cyan: LZC tube compartments and current water levels. The face shows one axial plane at a time.</p><div data-field="device-plan"></div></div>
          </section>
          <section class="studio-card studio-inspector">
            <p class="studio-eyebrow">02 / MAKE A MOVE</p><div class="studio-channel-title"><h2 data-field="channel"></h2><span data-field="coordinate"></span></div>
            <div class="studio-channel-metrics"><div><span>CHANNEL / kW</span><strong data-field="local-power"></strong></div><div><span>RIPPLE / TARGET 100%</span><strong data-field="local-ripple"></strong></div><div><span>PEAK BUNDLE / kW</span><strong data-field="bundle-peak"></strong></div><div><span>SIGNED TILT · B+</span><strong data-field="local-tilt"></strong></div></div><p data-field="channel-reference"></p>
            <div class="studio-rack-heading"><span class="studio-eyebrow">12 BUNDLE POSITIONS</span><span data-field="burnup"></span></div>
            <div data-field="bundles" class="studio-axial-profile" aria-label="Selected channel axial power, burnup, iodine and xenon profiles"></div><div class="studio-rack-ends"><span>END A / 01</span><span>AXIAL BUNDLE POSITION</span><span>12 / END B</span></div>
            <div class="studio-order"><p class="studio-order-size"><span>Fuel with flow</span><strong data-field="direction"></strong></p>
            <button data-action="refuel" data-field="refuel" class="studio-primary">Refuel channel →</button><p class="studio-order-note" data-field="order-note"></p><div data-field="movement-plan" class="studio-movement"></div></div>
            <details class="studio-controls"><summary>Power &amp; time controls</summary><small>Applied target <span data-field="power-target"></span> · <span data-field="power-rating"></span></small><label>Power target <output data-field="target-output"></output><input data-field="target-input" type="range" min="80" max="120" step="1" aria-label="Power target percent" /></label><div><button data-action="target">Apply target</button><button data-action="step">Advance 1 hour</button></div><small>Resume to apply a power target. Pause to step time.</small></details>
          </section>
        </main>
        <section class="studio-bottom">
          <article class="studio-card studio-result"><div class="studio-card-heading"><h2>Last fuel move</h2><span class="studio-eyebrow">EQUILIBRIUM RESPONSE</span></div><p data-field="feedback"></p><div data-field="movement-result" class="studio-movement-result"></div><div class="studio-impact-grid" data-field="impact">Local power, tilt, LZC level and inventory will appear here after refuelling.</div></article>
          <article class="studio-card studio-zones"><div class="studio-card-heading"><h2>Regulating headroom</h2><span class="studio-eyebrow">14 LIQUID ZONES</span></div><div data-field="zones" class="studio-zone-strip"></div><p data-field="zone-note"></p></article>
        </section>
        </div>
        <footer class="studio-footer"><span data-field="guidance"></span><span>SPACE pause / resume · R refuel</span></footer>
      </div>`;
    this.element.querySelectorAll<HTMLElement>("[data-field]").forEach(field => this.fields.set(field.dataset.field!, field));
    parent.append(this.element);
    this.calculationView = new DayCalculationView(() => {
      if (this.session.isPending) { this.element.focus({ preventScroll: true }); return; }
      this.inspectionSnapshot = null;
      this.snapshot = this.session.snapshot;
      this.historyView.followLive();
      this.select(this.selected); this.render();
      (isRunTerminal(this.snapshot) ? this.field("ending-title") : this.element.querySelector<HTMLButtonElement>('[data-action="commit-day"]'))?.focus({ preventScroll: true });
    });
    this.element.append(this.calculationView.element);
    this.historyView = new HistoryView(session.history, tab => {
      this.field("reactor-panel").hidden = tab !== "reactor";
    }, snapshot => {
      this.inspectionSnapshot = snapshot;
      this.snapshot = snapshot ?? this.session.snapshot;
      this.select(this.selected);
      this.render();
    });
    this.field("history").append(this.historyView.element);
    this.element.querySelector(".studio-metrics")!.after(this.dailyView.element);
    this.historyView.setTab(initial.selectedTab ?? "reactor");
    this.mapMode = initial.mapMode ?? "burnup";
    this.mapView = new StudioMapView(name => this.field(name), this.snapshot.core.channels);
    this.orderView = new StudioOrderView(name => this.field(name));
    this.statusView = new StudioStatusView(this.element, name => this.field(name));
    this.presentation = session.presentation ?? new SessionPresentation();
    this.animatedOperation = this.snapshot.lastFuelMovement?.operationId;
    this.buildZones();
    this.select(initial.selectedChannelIndex ?? 210);
    this.element.addEventListener("click", this.onClick);
    this.element.addEventListener("keydown", this.onKeyDown);
    this.field<HTMLInputElement>("target-input").addEventListener("input", this.onTargetInput);
    this.field<HTMLInputElement>("target-input").value = String(Math.round(this.snapshot.targetPowerFraction * 100));
    this.onTargetInput();
    this.unsubscribe = session.subscribe(update => this.receive(update));
    this.element.focus({ preventScroll: true });
  }

  public destroy(): void {
    this.unsubscribe();
    this.historyView.destroy();
    this.calculationView.destroy();
    this.element.removeEventListener("click", this.onClick);
    this.element.removeEventListener("keydown", this.onKeyDown);
    this.field("target-input").removeEventListener("input", this.onTargetInput);
    this.element.remove();
  }

  private field<T extends HTMLElement = HTMLElement>(name: string): T {
    return this.fields.get(name)! as T;
  }

  private text(name: string, text: string): void { const field = this.field(name); if (field.textContent !== text) field.textContent = text; }

  private buildZones(): void {
    for (let i = 0; i < 14; i++) {
      const zone = document.createElement("div");
      zone.className = "studio-zone";
      zone.innerHTML = `<span></span><div><i></i></div><small></small>`;
      this.field("zones").append(zone);
    }
  }

  private select(index: number): void {
    const channel = this.snapshot.core.channels.find(channel => channel.channelIndex === index) ?? this.snapshot.core.channels[0];
    this.selected = channel?.channelIndex ?? -1;
    this.draft = channel ? createRefuelDraft(channel) : null;
  }

  private receive(update: SessionUpdate): void {
    const response = update.response;
    if (response) {
      this.presentation.accept(response, this.snapshot);
      if (response.accepted && ["commit-day", "reset"].includes(response.command.type)) this.localDailyPlan = [];
      this.lastHandledResponse = response;
    }
    if (update.error) this.presentation.fail(update.error);
    if (this.presentation.result) this.element.dataset.result = this.presentation.result;
    else delete this.element.dataset.result;
    this.pace = update.pace;
    this.dayProgress = update.dayProgress;
    if (this.inspectionSnapshot && !this.session.history.inspectableSamples.some(point => this.session.history.snapshotAt(point) === this.inspectionSnapshot)) {
      this.inspectionSnapshot = null;
      this.historyView.followLive();
    }
    this.snapshot = this.inspectionSnapshot ?? update.snapshot;
    this.render(update.changeKind === "status");
    this.calculationView.update(update, this.dailyPlan);
    if (response?.accepted && response.command.type === "commit-refuel" && response === this.lastHandledResponse &&
        response.snapshot.lastFuelMovement?.operationId !== this.animatedOperation) {
      this.animatedOperation = response.snapshot.lastFuelMovement?.operationId;
      if (!window.matchMedia?.("(prefers-reduced-motion: reduce)").matches) {
        const towardB = response.snapshot.lastFuelMovement?.plan.outgoingEnd === "B";
        this.field("movement-result").querySelectorAll<HTMLElement>("[data-confirmed]").forEach(cell => {
          const outgoing = cell.dataset.confirmed === "discharged";
          const offset = (towardB ? 24 : -24) * (outgoing ? 1 : -1);
          cell.animate?.(outgoing
            ? [{ opacity: 1, transform: "translateX(0)" }, { opacity: 0.5, transform: `translateX(${offset}px)` }]
            : [{ opacity: 0.3, transform: `translateX(${offset}px)` }, { opacity: 1, transform: "translateX(0)" }], { duration: 450 });
        });
      }
    }
  }

  private animatedOperation: number | undefined;
  private watchKey = "";
  private watched: CanduChannelSnapshot[] = [];

  private render(statusOnly = false): void {
    const snapshot = this.snapshot;
    const ready = this.session.status.isWasmAvailable;
    const pending = this.session.isPending;
    this.element.querySelector(".studio-metrics")!.setAttribute("aria-label", this.inspectionSnapshot ? "Historical reactor status" : "Live reactor status");
    const impact = this.inspectionSnapshot ? snapshot.lastFuelMovement
      ? `Recorded fuel move #${snapshot.lastFuelMovement.operationId} · bundle movement at this observation is shown below.`
      : "No fuel moves had been completed at this observation." : this.presentation.impactText;
    if (this.impactKey !== impact) {
      this.impactKey = impact;
      this.field("impact").replaceChildren(...impact.split("\n").map(line => {
        const row = document.createElement("span"); row.textContent = line; return row;
      }));
    }
    if (!statusOnly) {
      this.historyView.render();
      this.renderShift();
      const channel = snapshot.core.channels.find(channel => channel.channelIndex === this.selected);
      this.text("power-target", getPowerLabel(snapshot.targetPowerFraction));
      this.text("power-rating", snapshot.physics.electricalPowerWatts === undefined ? "" :
        `${(snapshot.physics.electricalPowerWatts / 1e6).toFixed(0)} MW electric · ${(snapshot.physics.totalPowerWatts / 1e6).toFixed(0)} MW thermal`);
      this.text("reserve", `${(snapshot.rrs.averageFillFraction * 100).toFixed(1)}%`);
      this.text("score", snapshot.scoreTotal.toLocaleString("en-US", { maximumFractionDigits: 1 }));
      this.text("operations", String(snapshot.refuellingOperationCount));
      this.text("ripple-score", snapshot.ripple ? `RMS ripple ${(snapshot.ripple.rmsDeviationFraction * 100).toFixed(2)}% · ${snapshot.ripple.pointsPerHour.toFixed(3)} points/h` : "Reference unavailable from this host");
      this.text("stock", snapshot.shift?.unlimitedFreshFuel ? "∞" : String(snapshot.freshBundlesAvailable));
      this.text("time", snapshot.pacingMode === "daily-turn" ? `Day ${(snapshot.completedDays ?? 0) + 1}` : formatSimulationTime(snapshot.simulationTimeSeconds));
      (this.element.querySelector('.studio-clock .studio-segments') as HTMLElement).hidden = snapshot.pacingMode === "daily-turn";
      (this.element.querySelector('.studio-controls') as HTMLElement).hidden = snapshot.pacingMode === "daily-turn";
      this.element.querySelector(".studio-footer > span:last-child")!.textContent = snapshot.pacingMode === "daily-turn" ? "R add / remove channel · N highest burnup" : "SPACE pause / resume · R refuel";
      this.text("status", isRunTerminal(snapshot) ? "Shift complete" : getOverallStatus(snapshot));
      this.text("tilt", `Keff ${formatEffectiveK(snapshot.physics.effectiveK)} · Global tilt ${getTiltLabel(snapshot.axialTiltFraction)} · Limit ±20% · End B positive`);
      this.text("guidance", operationGuidance(snapshot));

      this.text("pause", snapshot.isPaused ? "▶" : "Ⅱ");
      this.field("pause").setAttribute("aria-label", snapshot.isPaused ? "Resume simulation" : "Pause simulation");
      this.text("channel", channel ? gridCoordinateLabel(channel) : "No channel");
      this.text("coordinate", channel ? "CORE CHANNEL" : "—");
      this.text("local-power", channel ? `${(channelWatts(snapshot, channel) / 1000).toFixed(0)} / ${(channelLimit(snapshot) / 1000).toFixed(0)}` : "—");
      const ripple = channel ? snapshot.ripple?.channelRippleFractions[channel.channelIndex] : undefined;
      this.text("local-ripple", ripple === undefined ? "—" : `${(ripple * 100).toFixed(2)}%`);
      this.text("bundle-peak", channel ? `${(Math.max(0, ...channel.bundles.filter(b => b.hasFuel).map(b => b.powerWatts)) / 1000).toFixed(0)} / ${(bundleLimit(snapshot) / 1000).toFixed(0)}` : "—");
      this.text("local-tilt", channel ? getTiltLabel(channel.localTiltFraction) : "—");
      this.text("channel-reference", channel && snapshot.ripple ? `Channel power ${(snapshot.ripple.referenceChannelPowerWatts[channel.channelIndex] * snapshot.ripple.channelRippleFractions[channel.channelIndex] / 1e6).toFixed(3)} MW / reference ${(snapshot.ripple.referenceChannelPowerWatts[channel.channelIndex] / 1e6).toFixed(3)} MW · ripple ${(snapshot.ripple.channelRippleFractions[channel.channelIndex] * 100).toFixed(2)}% (target 100%). Fixed reference: ${(snapshot.ripple.referenceThermalPowerWatts / 1e6).toLocaleString("en-US")} MW thermal${snapshot.core.adjusters ? `, ${snapshot.core.adjusters.length} inserted adjusters` : ""}.` : "Channel reference unavailable");
      this.text("device-note", adjusterSummary(snapshot));
      this.text("lzc-device-note", snapshot.core.liquidZoneTubes === undefined ? "LZC tube locations unavailable from this host."
        : snapshot.core.liquidZoneTubes.length ? "Cyan: LZC tube compartments and current water levels. The face shows one axial plane at a time."
        : "This host has no localized LZC tube geometry.");
      this.text("burnup", channel ? `${channel.averageBurnupMwdPerKg.toFixed(1)} avg` : "—");
      this.text("direction", this.draft ? formatRefuelDirection(this.draft.directionId) : "Select a channel");
      this.text("map-tag", `${this.inspectionSnapshot ? `HISTORY ${formatSimulationTime(snapshot.simulationTimeSeconds)}` : "LIVE"} / ${this.mapMode.toUpperCase()}`);
      this.element.dataset.mapMode = this.mapMode;
      this.text("legend", this.mapMode === "burnup" ? "Fresh → higher burnup · MWd/kg HM"
        : this.mapMode === "power" ? `Blue ≤${(channelColorMinimum(snapshot) / 1000).toFixed(0)} → red ≥${(channelLimit(snapshot) / 1000).toFixed(0)} kW / channel · red = limit`
        : this.mapMode === "bundle-power" ? `Blue ≤${(bundleColorMinimum(snapshot) / 1000).toFixed(0)} → red ≥${(bundleLimit(snapshot) / 1000).toFixed(0)} kW / hottest bundle · red = limit`
        : "Blue ≤85% · pale = 100% reference · red ≥115%");
      this.mapView.update(snapshot, this.selected, this.mapMode, this.dailyPlan);
      this.renderWatchlist();
      this.orderView.update(snapshot, this.draft, channel);
      snapshot.rrs.zones.forEach((zone, index) => {
        const element = this.field("zones").children[index] as HTMLElement | undefined;
        if (!element) return;
        element.children[0].textContent = `${(zone.fillFraction * 100).toFixed(0)}%`;
        (element.querySelector("i") as HTMLElement).style.height = `${Math.min(100, Math.max(0, zone.fillFraction * 100))}%`;
        element.querySelector("small")!.textContent = `Z${zone.logicalZoneId + 1}`;
        element.dataset.limiting = String(zone.logicalZoneId === snapshot.rrs.limitingZoneId);
        element.title = `${formatLiquidZoneRegion(zone.logicalZoneId)}: ${(zone.fillFraction * 100).toFixed(1)}% fill, shape error ${zone.shapeError.toFixed(5)}, ${(zone.fillFraction * 100).toFixed(1)}% drain room, ${((1-zone.fillFraction)*100).toFixed(1)}% fill room`;
        if (zone.meanXe135NumberDensityM3 !== undefined)
          element.title += `, xenon ${(zone.meanXe135NumberDensityM3 / 1e20).toFixed(2)} × 10²⁰ atoms/m³`;
      });
      const limiting = snapshot.rrs.zones.find(z => z.logicalZoneId === snapshot.rrs.limitingZoneId);
      this.field("zone-note").dataset.decision = snapshot.rrs.decisionCode ?? "unavailable";
      this.text("zone-note", `${snapshot.rrs.decisionExplanation ?? "Controller explanation unavailable from this host."}${limiting ? ` Z${limiting.logicalZoneId + 1} has least headroom: ${(limiting.fillFraction * 100).toFixed(1)}% drain / ${((1-limiting.fillFraction)*100).toFixed(1)}% fill room.` : ""}`);
    }
    this.statusView.update(snapshot, ready, pending, this.session.status.detail,
      this.presentation.message, this.pace, this.draft, this.selected, this.mapMode, this.inspectionSnapshot !== null);
    if (snapshot.pacingMode === "daily-turn") {
      const included = this.dailyPlan.includes(this.selected);
      this.text("refuel", this.inspectionSnapshot ? "Return to live to plan" : included ? "Remove from today's fuel plan" : "Add to today's fuel plan");
      const button = this.field<HTMLButtonElement>("refuel");
      button.disabled = !ready || pending || !!this.inspectionSnapshot || isRunTerminal(snapshot) || (!included && !isChannelRefuellable(snapshot.core.channels.find(c => c.channelIndex === this.selected)));
    }
    this.dailyView.update(snapshot, this.dailyPlan, pending, this.inspectionSnapshot !== null, this.dayProgress, ready);
  }

  private renderShift(): void {
    const shift = this.snapshot.shift;
    const provenance = this.snapshot.provenance;
    const provenanceText = provenance ? `${provenance.label}${provenance.isModified ? ` · ${provenance.reasons.join("; ")} · Excluded from standard challenge results.` : ""}` : "";
    this.text("provenance", provenanceText);
    this.text("ending-provenance", provenanceText);
    this.element.dataset.runKind = provenance?.kind ?? "unknown";

    this.field("objective-card").hidden = !shift;
    this.text("challenge-button", shift?.id === "useful-fuel-day-v1" ? "Free practice" : "One-day challenge");
    const terminal = isRunTerminal(this.snapshot);
    this.field("ending").hidden = !terminal;
    if (shift) {
      this.text("objective-title", `${shift.title} / SEED ${shift.seed}`);
      this.text("objective", shift.objective);
      this.text("objective-reward", provenance?.isModified ? "Explore freely; start a new shift to begin a standard run." : shift.reward ? `Reward: ${shift.reward} · Inspect both ends of the burnup graph before refuelling.` : "No scripted moves. Choose your own refuelling strategy.");
      this.text("objective-progress", shift.usefulBundlesRequired > 0 ?
        `${shift.usefulBundlesDischarged} / ${shift.usefulBundlesRequired} useful bundles` : `${shift.usefulBundlesDischarged} useful bundles discharged`);
      this.text("remaining", shift.isEndless ? "Endless run · Unlimited fresh fuel" : `${formatClockDuration(shift.remainingSeconds)} remaining · ${this.snapshot.freshBundlesAvailable} / ${shift.fuelBudget} fresh bundles left`);
      this.text("ending-title", `${provenance?.isModified ? "Sandbox · " : ""}${shift.outcome === "missed" ? "Objective missed" : shift.outcome === "ended" ? "Shift ended early" : "Shift completed"}`);
      this.text("ending-reward", provenance?.isModified ? "Sandbox result: excluded from the standard challenge badge and score comparisons." : shift.rewardEarned ? `Earned: ${shift.reward}` : shift.outcome === "missed" ?
        `Discharged ${shift.usefulBundlesDischarged} of ${shift.usefulBundlesRequired} required useful bundles. Compare burnup at both ends and try again.` : "");
      this.text("energy", `${shift.electricalEnergyMwhEstimate.toLocaleString("en-US", { maximumFractionDigits: 1 })} MWh electric (estimate)`);
      this.text("thermal-energy", `${shift.thermalEnergyMwh.toLocaleString("en-US", { maximumFractionDigits: 1 })} MWh thermal`);
      this.text("fuel-used", shift.unlimitedFreshFuel ? `${shift.fuelConsumed} bundles` : `${shift.fuelConsumed} / ${shift.fuelBudget} bundles`);
      this.text("useful-fuel", `${shift.usefulBundlesDischarged} discharged at ≥ ${shift.usefulBurnupThresholdMwdPerKg} MWd/kg`);
      this.text("final-score", `${this.snapshot.scoreTotal.toLocaleString("en-US", { maximumFractionDigits: 1 })} points`);
      this.text("score-components", `Ripple points ${shift.operatingPoints.toFixed(1)} · closer channel powers earn more · maximum 1 point/h`);
    } else {
      this.text("ending-title", "Shift complete");
    }
    this.text("ending-reason", this.snapshot.runEndReason ?? this.snapshot.rrs.gameOverReason);
    if (terminal && !this.showedEnding) this.field("ending-title").focus({ preventScroll: false });
    if (!terminal && this.showedEnding) this.element.focus({ preventScroll: false });
    this.showedEnding = terminal;
  }

  private renderWatchlist(): void {
    const key = this.snapshot.core.channels.map(c => `${c.channelIndex}:${isChannelRefuellable(c)}:${c.averageBurnupMwdPerKg}`).join("|");
    if (key !== this.watchKey) {
      this.watchKey = key;
      this.watched = this.snapshot.core.channels.filter(isChannelRefuellable)
        .sort((a, b) => b.averageBurnupMwdPerKg - a.averageBurnupMwdPerKg || a.channelIndex - b.channelIndex).slice(0, 5);
    }
    const channels = this.watched.map(c => this.snapshot.core.channels.find(current => current.channelIndex === c.channelIndex)!);
    const list = this.field("watchlist");
    while (list.children.length < channels.length) {
      const button = document.createElement("button"); button.innerHTML = "<span></span><strong></strong><small></small>"; list.append(button);
    }
    while (list.children.length > channels.length) list.lastElementChild!.remove();
    channels.forEach((channel, index) => {
      const button = list.children[index] as HTMLButtonElement;
      button.dataset.action = "channel"; button.dataset.channel = String(channel.channelIndex);
      button.setAttribute("aria-pressed", String(channel.channelIndex === this.selected));
      button.children[0].textContent = `0${index + 1} / FUEL CHANNEL`;
      button.children[1].textContent = gridCoordinateLabel(channel);
      button.children[2].textContent = `${channel.averageBurnupMwdPerKg.toFixed(1)} MWd/kg · ${(channelWatts(this.snapshot, channel) / 1000).toFixed(0)} kW`;
      button.title = channelHeadroom(this.snapshot, channel) || "Zone headroom unavailable";
    });
  }

  private async send(command: CanduCommand): Promise<void> {
    if (this.session.isPending || !this.session.status.isWasmAvailable) return;
    if (this.inspectionSnapshot && command.type !== "reset") return;
    if (command.type === "commit-day") this.calculationView.begin(this.snapshot, this.dailyPlan, command);
    try { const response = await this.session.dispatch(command); if (command.type === "commit-day") this.calculationView.finish(response); }
    catch (error) { const message = error instanceof Error ? error.message : String(error); this.presentation.fail(message); this.calculationView.interrupted(message); this.render(); }
  }

  private readonly onTargetInput = (): void => {
    this.text("target-output", `${this.field<HTMLInputElement>("target-input").value}%`);
  };

  private readonly onClick = (event: MouseEvent): void => {
    const target = event.target instanceof Element ? event.target.closest<HTMLElement>("[data-action], [data-channel]") : null;
    if (!target || target instanceof HTMLButtonElement && target.disabled) return;
    if (target.dataset.action === "remove-plan") { this.setDailyPlan(this.dailyPlan.filter(c => c !== Number(target.dataset.planChannel))); return; }
    if (target.dataset.channel) { this.select(Number(target.dataset.channel)); this.render(); return; }
    switch (target.dataset.action) {
      case "lzc-plane":
        this.element.querySelectorAll<HTMLElement>('[data-action="lzc-plane"]').forEach(b => b.setAttribute("aria-pressed", String(b === target)));
        this.mapView.setLiquidZonePlane(Number(target.dataset.plane));
        this.render();
        break;
      case "devices": {
        const visible = target.getAttribute("aria-pressed") !== "true";
        target.setAttribute("aria-pressed", String(visible));
        this.mapView.showAdjusters(visible);
        break;
      }
      case "map": this.mapMode = target.dataset.map as MapMode; this.render(); break;
      case "oldest": this.select(highestBurnupChannel(this.snapshot.core.channels) ?? this.selected); this.render(); break;
      case "commit-day": void this.send({ type: "commit-day", expectedCompletedDays: this.snapshot.completedDays ?? 0, channelIndices: [...this.dailyPlan] }); break;
      case "clear-plan": this.setDailyPlan([]); break;
      case "refuel": if (this.snapshot.pacingMode === "daily-turn") {
        this.setDailyPlan(this.dailyPlan.includes(this.selected) ? this.dailyPlan.filter(c => c !== this.selected) : [...this.dailyPlan, this.selected]); break;
      }
      if (isChannelRefuellable(this.snapshot.core.channels.find(c => c.channelIndex === this.selected)) && canIssueRefuel(this.draft, this.snapshot.freshBundlesAvailable, this.session.isPending, this.snapshot.shift?.unlimitedFreshFuel) && !isRunTerminal(this.snapshot)) void this.send({ type: "commit-refuel", request: toRefuelRequest(this.draft) }); break;
      case "pause": void this.send({ type: this.snapshot.isPaused ? "resume" : "pause" }); break;
      case "speed": void this.send({ type: "set-playback-mode", modeId: target.dataset.speed as PlaybackModeId }); break;
      case "retry": void this.send({ type: "reset" }); break;
      case "reset":
      case "new-seed": void this.send({ type: "reset", seed: randomCoreSeed(this.snapshot.shift?.seed) }); break;
      case "challenge": void this.send({ type: "reset", seed: randomCoreSeed(this.snapshot.shift?.seed), shiftId: this.snapshot.shift?.id === "useful-fuel-day-v1" ? "free-practice" : "useful-fuel-day-v1" }); break;
      case "step": void this.send({ type: "step", simulationSeconds: 3600 }); break;
      case "target": void this.send({ type: "queue-power-target", targetFraction: Number(this.field<HTMLInputElement>("target-input").value) / 100 }); break;
    }
  };

  private readonly onKeyDown = (event: KeyboardEvent): void => {
    if (event.repeat || event.altKey || event.ctrlKey || event.metaKey) return;
    const target = event.target as Element;
    if (target.closest(".studio-day-dialog")) return;
    if (target.closest(".studio-history")) return;
    if (target.matches("input, select, textarea")) return;
    const key = event.key.toLowerCase();
    const moves: Record<string, [number, number]> = { arrowleft: [-1, 0], arrowright: [1, 0], arrowup: [0, -1], arrowdown: [0, 1] };
    if (moves[key] && target.closest("[data-channel]")) {
      event.preventDefault(); this.select(findAdjacentChannelIndex(this.snapshot.core.channels, this.selected, ...moves[key])); this.render();
      this.mapView.focus(this.selected); return;
    }
    // Let native buttons retain Space/Enter activation and focus behavior.
    if ((key === " " || key === "enter") && target.closest("button, summary")) return;
    if ((key === "enter" || key === " ") && target.hasAttribute("data-channel")) { event.preventDefault(); return; }
    const action = ({ " ": this.snapshot.pacingMode === "daily-turn" ? "" : "pause", r: "refuel", n: "oldest" } as Record<string, string>)[key];
    if (action) { event.preventDefault(); this.element.querySelector<HTMLButtonElement>(`button[data-action="${action}"]`)?.click(); }
  };
}
