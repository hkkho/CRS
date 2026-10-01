import type { BridgeSessionController, SessionUpdate } from "../sessionController";
import type { CanduChannelSnapshot, CanduCommand, CanduSnapshot, PlaybackModeId } from "../protocol";
import { canIssueRefuel, createRefuelDraft, formatRefuelDirection, toggleRefuelDirection, toRefuelRequest, type RefuelDraft } from "../commandState";
import { oldestFuelChannel, operationGuidance, refuelImpactText } from "../gameplayPresentation";
import { CANDU6_ROW_LABELS, findAdjacentChannelIndex, gridCoordinateLabel } from "../projection";
import { formatEffectiveK, formatLiquidZoneRegion, formatSimulationTime, getOverallStatus, getPowerLabel, getTiltLabel } from "../visuals";
import "./studio.css";

type StudioSession = Pick<BridgeSessionController, "snapshot" | "status" | "isPending" | "subscribe" | "dispatch">;
export interface StudioNavigation {
  selectedChannelIndex?: number;
  refuelDraft?: RefuelDraft;
}

/** A presentation of the existing bridge session, with no reactor state or rules. */
export class StudioView {
  public readonly element = document.createElement("section");
  private snapshot: CanduSnapshot;
  private draft: RefuelDraft | null = null;
  private selected = -1;
  private mapMode: "power" | "burnup" = "burnup";
  private lastSequence: number;
  private message = "Choose a channel. Inspect the fuel. Make your move.";
  private readonly circles = new Map<number, SVGCircleElement>();
  private readonly fields = new Map<string, HTMLElement>();
  private readonly unsubscribe: () => void;

  public constructor(
    private readonly session: StudioSession,
    parent: HTMLElement,
    private readonly navigate: (scene: "OperationsScene" | "CoreDesignerScene", state: StudioNavigation) => void,
    initial: StudioNavigation = {},
  ) {
    this.snapshot = session.snapshot;
    this.lastSequence = this.snapshot.sequence;
    this.element.className = "reactor-studio";
    this.element.tabIndex = -1;
    this.element.setAttribute("aria-label", "Reactor Studio game interface");
    this.element.innerHTML = `
      <div class="studio-shell">
        <header class="studio-header">
          <div class="studio-brand"><span class="studio-emblem">C<span>06</span></span><div><p class="studio-eyebrow">ON-POWER REFUELLING / ALTERNATIVE VIEW</p><h1>Reactor Studio<span class="studio-brand-dot">.</span></h1></div></div>
          <nav aria-label="Game views"><button data-action="classic">↗ Tactical view</button><button data-action="designer">Core designer</button><button data-action="reset" class="studio-quiet">New shift</button></nav>
        </header>
        <section class="studio-metrics" aria-label="Live reactor status">
          <article><span class="studio-eyebrow">REGULATED POWER</span><strong data-field="power"></strong><small>Target <span data-field="power-target"></span></small><small data-field="power-rating"></small></article>
          <article><span class="studio-eyebrow">RRS RESERVE</span><strong data-field="reserve"></strong><small data-field="status"></small></article>
          <article><span class="studio-eyebrow">SHIFT SCORE</span><strong data-field="score"></strong><small><span data-field="operations"></span> fuel moves completed</small></article>
          <article><span class="studio-eyebrow">FRESH BUNDLES</span><strong data-field="stock"></strong><small>4 or 8 bundles per move</small></article>
          <article class="studio-clock"><span class="studio-eyebrow">SIMULATION CLOCK</span><strong data-field="time"></strong><div class="studio-segments" aria-label="Playback speed"><button data-action="pause" aria-label="Pause simulation" data-field="pause">Ⅱ</button><button data-action="speed" data-speed="1x">1×</button><button data-action="speed" data-speed="10x">10×</button><button data-action="speed" data-speed="60x">60×</button></div></article>
        </section>
        <main class="studio-workspace">
          <aside class="studio-card studio-watchlist">
            <p class="studio-eyebrow">01 / INSPECT</p><h2>Fuel watchlist</h2><p class="studio-description">Start with older fuel.</p>
            <button data-action="oldest" class="studio-outline">◎ Find oldest fuel</button>
            <div data-field="watchlist" class="studio-candidates"></div>
            <div class="studio-tip"><span class="studio-eyebrow">THE BARGAIN</span><p>Useful burnup earns points. Fresh fuel waste costs points.</p><span data-field="tilt"></span></div>
          </aside>
          <section class="studio-card studio-core">
            <div class="studio-card-heading"><div><p class="studio-eyebrow">380 CHANNELS / LIVE CORE</p><h2>The reactor face</h2></div><div class="studio-segments"><button data-action="map" data-map="burnup">Burnup</button><button data-action="map" data-map="power">Power</button></div></div>
            <div class="studio-map-wrap"><svg data-field="map" viewBox="0 0 560 560" aria-label="Core channel map" role="group"><defs><radialGradient id="studio-vessel"><stop stop-color="#2d4945"/><stop offset="1" stop-color="#142421"/></radialGradient></defs><circle cx="280" cy="280" r="256" fill="url(#studio-vessel)"/><circle cx="280" cy="280" r="249" fill="none" stroke="#58716a" stroke-width="1"/><circle cx="280" cy="280" r="238" fill="none" stroke="#58716a" stroke-dasharray="2 8"/><path d="M280 10v32 M280 518v32 M10 280h32 M518 280h32" stroke="#a9c4b2" stroke-width="1"/><g data-field="map-labels" fill="#becbc1" font-size="10" font-family="monospace"></g><g data-field="channels"></g></svg><span class="studio-map-tag" data-field="map-tag"></span></div>
            <div class="studio-map-legend"><span class="studio-legend-gradient"></span><span data-field="legend">Fresh → higher burnup · MWd/kg HM</span><span>Arrows select</span></div>
          </section>
          <section class="studio-card studio-inspector">
            <p class="studio-eyebrow">02 / MAKE A MOVE</p><div class="studio-channel-title"><h2 data-field="channel"></h2><span data-field="coordinate"></span></div>
            <div class="studio-channel-metrics"><div><span>LOCAL POWER</span><strong data-field="local-power"></strong></div><div><span>SIGNED TILT · B+</span><strong data-field="local-tilt"></strong></div></div>
            <div class="studio-rack-heading"><span class="studio-eyebrow">12 BUNDLE POSITIONS</span><span data-field="burnup"></span></div>
            <div data-field="bundles" class="studio-bundle-rack" aria-label="Selected channel bundles"></div><div class="studio-rack-ends"><span>END A</span><span>Burnup · MWd/kg HM</span><span>END B</span></div>
            <div class="studio-order"><label>Fuel direction<button data-action="direction" data-field="direction"></button></label><div class="studio-order-size"><span>Shift size</span><div class="studio-segments"><button data-action="size" data-size="4">4 bundles</button><button data-action="size" data-size="8">8 bundles</button></div></div>
            <button data-action="refuel" data-field="refuel" class="studio-primary">Refuel channel →</button><p class="studio-order-note" data-field="order-note"></p></div>
            <details class="studio-controls"><summary>Power &amp; time controls</summary><label>Power target <output data-field="target-output"></output><input data-field="target-input" type="range" min="80" max="120" step="1" aria-label="Power target percent" /></label><div><button data-action="target">Apply target</button><button data-action="step">Advance 1 hour</button></div><small>Resume to apply a power target. Pause to step time.</small></details>
          </section>
        </main>
        <section class="studio-bottom">
          <article class="studio-card studio-result"><div class="studio-card-heading"><h2>Last fuel move</h2><span class="studio-eyebrow">EQUILIBRIUM RESPONSE</span></div><p data-field="feedback" role="status" aria-live="polite"></p><div class="studio-impact-grid" data-field="impact">Local power, tilt, reserve and inventory will appear here after refuelling.</div></article>
          <article class="studio-card studio-zones"><div class="studio-card-heading"><h2>Regulating headroom</h2><span class="studio-eyebrow">14 LIQUID ZONES</span></div><div data-field="zones" class="studio-zone-strip"></div><p data-field="zone-note"></p></article>
        </section>
        <footer class="studio-footer"><span data-field="guidance"></span><span>SPACE pause / resume · R refuel · D direction</span></footer>
      </div>`;
    this.element.querySelectorAll<HTMLElement>("[data-field]").forEach(field => this.fields.set(field.dataset.field!, field));
    parent.append(this.element);
    this.buildMap();
    this.buildZones();
    this.select(initial.selectedChannelIndex ?? 210);
    if (initial.refuelDraft?.channelIndex === this.selected) this.draft = { ...initial.refuelDraft };
    this.element.addEventListener("click", this.onClick);
    this.element.addEventListener("keydown", this.onKeyDown);
    this.field<HTMLInputElement>("target-input").addEventListener("input", this.onTargetInput);
    this.field<HTMLInputElement>("target-input").value = String(Math.round(powerTargetFraction(this.snapshot) * 100));
    this.onTargetInput();
    this.unsubscribe = session.subscribe(update => this.receive(update));
    this.element.focus({ preventScroll: true });
  }

  public destroy(): void {
    this.unsubscribe();
    this.element.removeEventListener("click", this.onClick);
    this.element.removeEventListener("keydown", this.onKeyDown);
    this.field("target-input").removeEventListener("input", this.onTargetInput);
    this.element.remove();
  }

  private field<T extends HTMLElement = HTMLElement>(name: string): T {
    return this.fields.get(name)! as T;
  }

  private text(name: string, text: string): void { this.field(name).textContent = text; }

  private buildMap(): void {
    const svgNamespace = "http://www.w3.org/2000/svg";
    for (let i = 0; i < 22; i++) {
      const column = document.createElementNS(svgNamespace, "text");
      column.setAttribute("x", String(70 + i * 20)); column.setAttribute("y", "52"); column.setAttribute("text-anchor", "middle");
      column.textContent = String(i + 1).padStart(2, "0");
      const row = document.createElementNS(svgNamespace, "text");
      row.setAttribute("x", "47"); row.setAttribute("y", String(74 + i * 20)); row.setAttribute("text-anchor", "middle");
      row.textContent = CANDU6_ROW_LABELS[i];
      this.field("map-labels").append(column, row);
    }
    for (const channel of this.snapshot.core.channels) {
      const circle = document.createElementNS(svgNamespace, "circle");
      circle.setAttribute("cx", String(70 + channel.gridColumn * 20));
      circle.setAttribute("cy", String(70 + channel.gridRow * 20));
      circle.setAttribute("r", "7.2"); circle.setAttribute("role", "button");
      circle.dataset.channel = String(channel.channelIndex);
      const title = document.createElementNS(svgNamespace, "title"); circle.append(title);
      this.field("channels").append(circle);
      this.circles.set(channel.channelIndex, circle);
    }
  }

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
    if (response && response.sequence > this.lastSequence) {
      this.lastSequence = response.sequence;
      if (response.command.type === "commit-refuel") {
        this.message = response.message;
        if (response.accepted) {
          this.field("impact").replaceChildren(...refuelImpactText(this.snapshot, response.snapshot, response.command.request.channelIndex).split("\n").map(line => {
            const row = document.createElement("span"); row.textContent = line; return row;
          }));
        }
        this.element.dataset.result = response.accepted ? "accepted" : "rejected";
      } else if (response.command.type === "reset" && response.accepted) {
        this.message = "New shift ready. Fresh fuel restocked.";
        this.text("impact", "Make your first fuel move to compare its response.");
      } else if (!response.accepted) {
        this.message = response.message;
        this.element.dataset.result = "rejected";
      }
    }
    if (update.error) this.message = update.error;
    this.snapshot = update.snapshot;
    this.render();
  }

  private render(): void {
    const snapshot = this.snapshot;
    const ready = this.session.status.isWasmAvailable;
    const pending = this.session.isPending;
    const channel = snapshot.core.channels.find(channel => channel.channelIndex === this.selected);
    this.text("power", getPowerLabel(snapshot.physics.actualPowerFraction));
    this.text("power-target", getPowerLabel(powerTargetFraction(snapshot)));
    this.text("power-rating", snapshot.physics.electricalPowerWatts === undefined ? "" :
      `${(snapshot.physics.electricalPowerWatts / 1e6).toFixed(0)} MW electric · ${(snapshot.physics.totalPowerWatts / 1e6).toFixed(0)} MW thermal`);
    this.text("reserve", `${(snapshot.rrsReserveFraction * 100).toFixed(0)}%`);
    this.text("score", Math.round(snapshot.scoreTotal).toLocaleString("en-US"));
    this.text("operations", String(snapshot.refuellingOperationCount));
    this.text("stock", String(snapshot.freshBundlesAvailable));
    this.text("time", formatSimulationTime(snapshot.simulationTimeSeconds));
    this.text("status", snapshot.rrs.isGameOver ? "Shift complete" : getOverallStatus({ ...snapshot, targetPowerFraction: powerTargetFraction(snapshot) }));
    this.text("tilt", `Keff ${formatEffectiveK(snapshot.physics.effectiveK)} · Core tilt ${getTiltLabel(snapshot.axialTiltFraction)} · End B positive`);
    this.text("guidance", operationGuidance(snapshot));
    this.text("feedback", !ready ? this.session.status.detail : pending ? "Reactor is solving your order…" : this.message);
    this.element.setAttribute("aria-busy", String(pending));
    this.text("pause", snapshot.isPaused ? "▶" : "Ⅱ");
    this.field("pause").setAttribute("aria-label", snapshot.isPaused ? "Resume simulation" : "Pause simulation");
    this.text("channel", channel ? `Channel ${String(channel.channelIndex).padStart(3, "0")}` : "No channel");
    this.text("coordinate", channel ? gridCoordinateLabel(channel) : "—");
    this.text("local-power", channel ? getPowerLabel(channel.localPowerFraction) : "—");
    this.text("local-tilt", channel ? getTiltLabel(channel.localTiltFraction) : "—");
    this.text("burnup", channel ? `${channel.averageBurnupMwdPerKg.toFixed(1)} avg` : "—");
    this.text("direction", this.draft ? formatRefuelDirection(this.draft.directionId) : "Select a channel");
    this.text("map-tag", `LIVE / ${this.mapMode.toUpperCase()}`);
    this.text("legend", this.mapMode === "burnup" ? "Fresh → higher burnup · MWd/kg HM" : "Low → high local power · relative to core mean");
    for (const current of snapshot.core.channels) {
      const circle = this.circles.get(current.channelIndex);
      if (!circle) continue;
      const amount = this.mapMode === "burnup" ? current.averageBurnupMwdPerKg / 10 : (current.localPowerFraction - 0.4) / 1.1;
      const fraction = Math.min(1, Math.max(0, amount));
      circle.setAttribute("fill", `hsl(${155 - fraction * 126} 58% ${40 + fraction * 24}%)`);
      circle.setAttribute("stroke", current.channelIndex === this.selected ? "#ffffff" : "#162923");
      circle.setAttribute("stroke-width", current.channelIndex === this.selected ? "3" : "0.7");
      circle.setAttribute("tabindex", current.channelIndex === this.selected ? "0" : "-1");
      circle.setAttribute("aria-pressed", String(current.channelIndex === this.selected));
      const label = `Channel ${current.channelIndex}, ${gridCoordinateLabel(current)}, burnup ${current.averageBurnupMwdPerKg.toFixed(1)} MWd/kg, power ${getPowerLabel(current.localPowerFraction)}`;
      circle.setAttribute("aria-label", label); circle.firstElementChild!.textContent = label;
    }
    this.renderWatchlist();
    this.renderBundles(channel);
    snapshot.rrs.zones.forEach((zone, index) => {
      const element = this.field("zones").children[index] as HTMLElement | undefined;
      if (!element) return;
      element.children[0].textContent = `${(zone.fillFraction * 100).toFixed(0)}%`;
      (element.querySelector("i") as HTMLElement).style.height = `${Math.min(100, Math.max(0, zone.fillFraction * 100))}%`;
      element.querySelector("small")!.textContent = `Z${zone.logicalZoneId + 1}`;
      element.title = `${formatLiquidZoneRegion(zone.logicalZoneId)}: ${(zone.fillFraction * 100).toFixed(1)}% fill, shape error ${zone.shapeError.toFixed(5)}`;
    });
    const effort = Math.max(0, ...snapshot.rrs.appliedFillCommand.map(Math.abs));
    this.text("zone-note", effort > 0.00001 ? `Largest fill change: ${(effort * 100).toFixed(2)} pp · Keep room to absorb or release reactivity.` : "No fill movement on this solve. Keep room to absorb or release reactivity.");
    this.element.querySelectorAll<HTMLButtonElement>("button[data-action]").forEach(button => {
      const action = button.dataset.action;
      button.disabled = action !== "map" && (!ready || pending);
      if (["size", "direction", "refuel"].includes(action ?? "")) button.disabled ||= snapshot.rrs.isGameOver || !channel;
      if (action === "step") button.disabled ||= !snapshot.isPaused || snapshot.rrs.isGameOver;
      if (action === "target") button.disabled ||= snapshot.isPaused;
      if (["pause", "speed", "target"].includes(action ?? "")) button.disabled ||= snapshot.rrs.isGameOver;
      if (action === "refuel") button.disabled ||= !canIssueRefuel(this.draft, snapshot.freshBundlesAvailable, pending);
      if (action === "map") button.setAttribute("aria-pressed", String(button.dataset.map === this.mapMode));
      if (action === "size") button.setAttribute("aria-pressed", String(Number(button.dataset.size) === this.draft?.shiftCount));
      if (action === "speed") button.setAttribute("aria-pressed", String(button.dataset.speed === snapshot.playbackModeId && !snapshot.isPaused));
    });
    this.text("refuel", snapshot.rrs.isGameOver ? "Shift complete" : pending ? "Solving…" : `Refuel ${this.draft?.shiftCount ?? 4} bundles →`);
    this.text("order-note", snapshot.freshBundlesAvailable < (this.draft?.shiftCount ?? 4) ? "Not enough fresh fuel. Start a new shift to restock." : `Cost: ${this.draft?.shiftCount ?? 4} fresh bundles · ${snapshot.freshBundlesAvailable} in stock`);
  }

  private renderWatchlist(): void {
    const channels = this.snapshot.core.channels.filter(channel => channel.bundles.some(bundle => bundle.hasFuel))
      .sort((a, b) => b.averageBurnupMwdPerKg - a.averageBurnupMwdPerKg || a.channelIndex - b.channelIndex).slice(0, 5);
    const list = this.field("watchlist");
    while (list.children.length < channels.length) {
      const button = document.createElement("button"); button.innerHTML = "<span></span><strong></strong><small></small>"; list.append(button);
    }
    while (list.children.length > channels.length) list.lastElementChild!.remove();
    channels.forEach((channel, index) => {
      const button = list.children[index] as HTMLButtonElement;
      button.dataset.action = "channel"; button.dataset.channel = String(channel.channelIndex);
      button.setAttribute("aria-pressed", String(channel.channelIndex === this.selected));
      button.children[0].textContent = `0${index + 1} / ${gridCoordinateLabel(channel)}`;
      button.children[1].textContent = `CH ${String(channel.channelIndex).padStart(3, "0")}`;
      button.children[2].textContent = `${channel.averageBurnupMwdPerKg.toFixed(1)} MWd/kg`;
    });
  }

  private renderBundles(channel: CanduChannelSnapshot | undefined): void {
    const rack = this.field("bundles");
    if (rack.children.length === 0) {
      for (let i = 0; i < 12; i++) {
        const cell = document.createElement("div"); cell.innerHTML = "<small></small><div><i></i></div><span></span>"; rack.append(cell);
      }
    }
    const maximumPower = Math.max(1, ...(channel?.bundles.map(bundle => bundle.powerWatts) ?? []));
    for (let i = 0; i < 12; i++) {
      const cell = rack.children[i] as HTMLElement;
      const bundle = channel?.bundles[i];
      cell.children[0].textContent = String(i + 1).padStart(2, "0");
      cell.querySelector("span")!.textContent = bundle?.hasFuel ? bundle.currentBurnupMwdPerKg.toFixed(1) : "—";
      cell.classList.toggle("is-fresh", bundle?.isFresh ?? false);
      (cell.querySelector("i") as HTMLElement).style.height = `${Math.max(3, (bundle?.powerWatts ?? 0) / maximumPower * 100)}%`;
      cell.title = bundle ? `Position ${i + 1}: ${bundle.hasFuel ? `${bundle.currentBurnupMwdPerKg.toFixed(2)} MWd/kg, ${bundle.isFresh ? "fresh" : "used"} fuel` : "empty"}` : "Unavailable";
    }
  }

  private async send(command: CanduCommand): Promise<void> {
    if (this.session.isPending || !this.session.status.isWasmAvailable) return;
    try { await this.session.dispatch(command); }
    catch (error) { this.message = error instanceof Error ? error.message : String(error); this.render(); }
  }

  private readonly onTargetInput = (): void => {
    this.text("target-output", `${this.field<HTMLInputElement>("target-input").value}%`);
  };

  private readonly onClick = (event: MouseEvent): void => {
    const target = event.target instanceof Element ? event.target.closest<HTMLElement>("[data-action], [data-channel]") : null;
    if (!target || target instanceof HTMLButtonElement && target.disabled) return;
    if (target.dataset.channel) { this.select(Number(target.dataset.channel)); this.render(); return; }
    switch (target.dataset.action) {
      case "classic": this.navigate("OperationsScene", { selectedChannelIndex: this.selected, refuelDraft: this.draft ?? undefined }); break;
      case "designer": this.navigate("CoreDesignerScene", { selectedChannelIndex: this.selected }); break;
      case "map": this.mapMode = target.dataset.map === "power" ? "power" : "burnup"; this.render(); break;
      case "oldest": this.select(oldestFuelChannel(this.snapshot.core.channels) ?? this.selected); this.render(); break;
      case "size": if (this.draft) this.draft.shiftCount = target.dataset.size === "8" ? 8 : 4; this.render(); break;
      case "direction": if (this.draft) this.draft.directionId = toggleRefuelDirection(this.draft.directionId); this.render(); break;
      case "refuel": if (canIssueRefuel(this.draft, this.snapshot.freshBundlesAvailable, this.session.isPending) && !this.snapshot.rrs.isGameOver) void this.send({ type: "commit-refuel", request: toRefuelRequest(this.draft) }); break;
      case "pause": void this.send({ type: this.snapshot.isPaused ? "resume" : "pause" }); break;
      case "speed": void this.send({ type: "set-playback-mode", modeId: target.dataset.speed as PlaybackModeId }); break;
      case "reset": void this.send({ type: "reset" }); break;
      case "step": void this.send({ type: "step", simulationSeconds: 3600 }); break;
      case "target": void this.send({ type: "queue-power-target", targetFraction: Number(this.field<HTMLInputElement>("target-input").value) / 100 }); break;
    }
  };

  private readonly onKeyDown = (event: KeyboardEvent): void => {
    if (event.repeat || event.altKey || event.ctrlKey || event.metaKey) return;
    const target = event.target as Element;
    if (target.matches("input, select, textarea")) return;
    const key = event.key.toLowerCase();
    const moves: Record<string, [number, number]> = { arrowleft: [-1, 0], arrowright: [1, 0], arrowup: [0, -1], arrowdown: [0, 1] };
    if (moves[key]) {
      event.preventDefault(); this.select(findAdjacentChannelIndex(this.snapshot.core.channels, this.selected, ...moves[key])); this.render();
      this.circles.get(this.selected)?.focus({ preventScroll: true }); return;
    }
    // Let native buttons retain Space/Enter activation and focus behavior.
    if ((key === " " || key === "enter") && target.closest("button, summary")) return;
    if ((key === "enter" || key === " ") && target.hasAttribute("data-channel")) { event.preventDefault(); return; }
    const action = ({ " ": "pause", r: "refuel", d: "direction", n: "oldest" } as Record<string, string>)[key];
    if (action) { event.preventDefault(); this.element.querySelector<HTMLButtonElement>(`button[data-action="${action}"]`)?.click(); }
  };
}

/** Normalize the accepted solver target for display; the legacy headline target is fixed at 1. */
function powerTargetFraction(snapshot: CanduSnapshot): number {
  const physics = snapshot.physics;
  return physics.referencePowerWatts > 0 && Number.isFinite(physics.targetPowerWatts)
    ? physics.targetPowerWatts / physics.referencePowerWatts : snapshot.targetPowerFraction;
}
