import { ArcadeMap } from "./ArcadeMap";
import { createCanduPlaytestBridge } from "./bridge";
import { DemoTrends } from "./DemoTrends";
import { gridCoordinateLabel } from "./channelCoordinates";
import { randomCoreSeed } from "./coreSeed";
import type { CustomPhysics } from "./customPhysics";
import type { RunSave } from "./runSave";
import { DemoLoop } from "./DemoLoop";
import { DemoTutorialView } from "./DemoTutorialView";
import { GameLoadingView } from "./GameLoadingView";
import { isRunTerminal, type CanduSnapshot } from "./protocol";
import { channelLimit, channelWatts } from "./studio/powerReadings";
import { refuellingZoneReadings, renderTimeMapScale, timeMapCell, timeMapLegend, timeZoneLabel, zoneDisplayOrder, type TimeMapMode } from "./timeMapPresentation";
import { nextTabIndex, zoneAlert } from "./demoPresentation";
import "./arcade.css";
import "./demo.css";

const maps = [["power", "Power"], ["ripple", "Ripple"], ["burnup", "Burnup"], ["inlet-zones", "Inlet zones"]] as const;
const mapModes = maps.map(([mode]) => mode);
const padDirections = ["ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight"] as const;

function shiftTime(minutes: number): string {
  return `Day ${Math.floor(minutes / 1440)} · ${String(Math.floor(minutes / 60) % 24).padStart(2, "0")}:${String(minutes % 60).padStart(2, "0")}`;
}

/** Time-based controls over the shared simulation. */
export class DemoView {
  readonly element = document.createElement("main");
  private readonly loop: DemoLoop;
  private readonly tutorial?: DemoTutorialView;
  private readonly unsubscribe: () => void;
  private readonly mapTabs: HTMLButtonElement[];
  private readonly endDialog: HTMLDialogElement;
  private endPresented = false;
  private map?: ArcadeMap;
  private readonly trends = new DemoTrends();
  private selected = 0;
  private mapMode: TimeMapMode = "power";
  private lastMapKey = "";
  private legendMode?: TimeMapMode;
  private lastSnapshot?: CanduSnapshot;
  private lastReceiptResult: CanduSnapshot["lastDayResult"];
  private loading?: GameLoadingView;
  private started = false;
  private readonly gamepadTimer: ReturnType<typeof setInterval>;
  private gamepadIndex: number | undefined;
  private gamepadButtons: readonly boolean[] = [];

  constructor(parent: HTMLElement, seed: number, private readonly leave: () => void, restore?: Promise<RunSave>,
    onStart?: () => void, customPhysics?: CustomPhysics, readonly isTutorial = false) {
    this.element.className = "reactor-arcade reactor-demo";
    this.element.innerHTML = `<header><div><p class="arcade-kicker">CRS / TIME BASED</p><h1>Reactor Simulator</h1></div><nav aria-label="Time based controls">
      <div class="demo-playback" role="group" aria-label="Playback">
        <button type="button" data-action="slow" aria-label="0.5× speed (10 seconds per half-day)" aria-pressed="false" title="0.5× · 10 seconds per half-day. Press again for normal speed.">0.5×</button>
        <button type="button" data-action="pause" aria-label="Resume" title="Resume">
          <svg aria-hidden="true" width="18" height="18" viewBox="0 0 18 18" fill="currentColor">
            <path data-play-icon d="M5 2.5 15 9 5 15.5Z"/>
            <path data-pause-icon d="M4 3h3v12H4zm7 0h3v12h-3z"/>
          </svg>
        </button>
        <button type="button" data-action="fast" aria-label="1.5× speed (5 seconds per half-day)" aria-pressed="false" title="1.5× · 5 seconds per half-day. Press again for normal speed.">1.5×</button>
      </div><button data-action="leave">Main screen</button></nav></header>
      <div class="demo-hud"><div><span>SIMULATION TIME</span><strong data-day>Day 0 · 00:00</strong></div><div><span>PEAK CHANNEL</span><strong data-peak>—</strong></div><div class="demo-zone-mean"><span>AVG. ZONE FILL</span><strong data-zone-mean>—</strong><small data-zone-risk></small></div><div><span>SCORE</span><strong data-score>0</strong><small data-score-rate></small></div><div class="demo-selected-hud"><div><span>SELECTED CHANNEL <b data-selected-channel>—</b></span><strong><b data-power>—</b> <small>kW</small></strong><span data-channel-limit></span><span data-selected-zone hidden></span></div><button class="arcade-primary" data-action="fuel">Queue fuel</button></div></div>
      <progress class="demo-clock" max="1" value="0" aria-label="Time step progress"></progress>
      <section class="demo-workspace"><div class="demo-core"><div class="demo-map-heading">
        <div class="demo-tabs demo-map-tabs" role="tablist" aria-label="Core map">${maps.map(([mode, label]) => `
          <button type="button" role="tab" id="demo-map-${mode}" data-action="map" data-map-tab="${mode}" aria-controls="demo-map-panel" aria-selected="${mode === this.mapMode}" tabindex="${mode === this.mapMode ? 0 : -1}">${label}</button>`).join("")}</div>
        <span data-live>Preparing core…</span></div>
        <div id="demo-map-panel" role="tabpanel" aria-labelledby="demo-map-power">
          <div class="arcade-map-scroll" data-map></div><div class="demo-map-scale" data-scale role="group"></div><p class="arcade-legend" data-legend></p>
        </div>
        <div class="demo-command-deck" aria-label="Fuel orders"><div><span class="demo-deck-label">FUEL ORDERS</span><strong data-fuel-queue>Queue empty</strong></div><p data-fuel-receipt role="status"></p><p data-message role="status"></p></div><p class="demo-queue-key"><span>Yellow inset: waiting</span> &middot; <span>Blue inset: submitted</span> &middot; White outline: selected</p><p class="demo-controls"><kbd>Arrows</kbd> Inspect <kbd>Enter</kbd> Queue or remove <span data-gamepad-hint hidden><kbd>D-pad</kbd> Inspect <kbd>Bottom button</kbd> Queue or remove <kbd>Shoulders</kbd> Change map <kbd>Start</kbd> Pause</span></p></div>
        <div class="demo-readout">
          <section class="demo-zones" aria-label="14 liquid zone levels"><h2>Liquid zones <small>14 levels</small></h2>
            <div class="demo-zone-grid">${zoneDisplayOrder.map((id, i) => `<div class="demo-zone" data-zone="${id}"><span>Z${i + 1}</span><div class="demo-zone-track"><i></i></div><strong>—</strong><small>—</small></div>`).join("")}</div>
          </section>
          <div class="demo-retry"><button type="button" data-action="retry">Retry seed</button><button type="button" data-action="new">New core</button><button type="button" data-action="result" hidden>Run result</button></div>
        </div>
      </section>
      <dialog class="demo-end-dialog" aria-labelledby="demo-end-title" aria-describedby="demo-end-reason">
        <h2 id="demo-end-title">Run ended</h2><p id="demo-end-reason"></p>
        <p data-restart-status role="status"></p>
        <div class="demo-end-actions">
          <button type="button" class="arcade-primary" data-action="retry-ended" autofocus>Retry seed</button>
          <button type="button" data-action="new-ended">New core</button>
          <button type="button" data-action="leave-ended">Main screen</button>
        </div>
      </dialog>`;
    this.field(".demo-retry").before(this.trends.element);
    this.endDialog = this.field(".demo-end-dialog");
    this.endDialog.addEventListener("close", this.onEndDialogClose);
    this.field(".demo-command-deck").insertAdjacentHTML("beforeend", `<details class="demo-zone-receipt" data-fuel-zone-response hidden>
      <summary>Zone response to last fuel batch</summary>
      <p>Before refuelling → After refuelling → End of step</p><p data-fuel-zone-values></p>
    </details>`);
    this.mapTabs = [...this.element.querySelectorAll<HTMLButtonElement>("[data-map-tab]")];
    parent.append(this.element);
    this.element.addEventListener("click", this.onClick);
    this.field(".demo-map-tabs").addEventListener("keydown", this.onMapTabKey);
    if (customPhysics) this.text(".arcade-kicker", "CRS / CUSTOM PHYSICS");
    const createBridge = (coreSeed: number) => createCanduPlaytestBridge({ initialPacingMode: "demo", initialSeed: coreSeed, customPhysics });
    this.loop = new DemoLoop(createBridge(seed), undefined, true, createBridge, restore, isTutorial);
    if (isTutorial) {
      this.element.classList.add("reactor-tutorial");
      this.text(".arcade-kicker", "CRS / TUTORIAL");
      this.text("h1", "Guided refuelling");
      this.field(".demo-playback").hidden = true;
      this.field(".demo-retry").hidden = true;
      const controllerHint = this.field("[data-gamepad-hint]");
      controllerHint.lastChild?.remove();
      controllerHint.querySelector("kbd:last-of-type")?.remove();
      this.tutorial = new DemoTutorialView(this.loop, this.field("[data-action=fuel]"), (mode, trend) => {
        this.trends.show(trend);
        this.setMapMode(mode);
      }, index => {
        this.selected = index;
        this.render();
      }, this.leave);
      this.field(".demo-workspace").prepend(this.tutorial.element);
    }
    this.loop.setVisible(document.visibilityState !== "hidden");
    this.loading = new GameLoadingView(this.element, () => {
      this.loading = undefined;
      this.started = true;
      if (this.tutorial) this.tutorial.begin();
      else {
        onStart?.();
        if (isRunTerminal(this.loop.snapshot)) this.render();
        else {
          this.loop.togglePause();
          this.map?.focusSelected();
        }
      }
    }, this.leave);
    this.unsubscribe = this.loop.subscribe(() => this.render());
    document.addEventListener("visibilitychange", this.onVisibility);
    this.gamepadTimer = setInterval(this.pollGamepad, 40);
  }
  get snapshot(): CanduSnapshot { return this.loop.snapshot; }
  subscribe(listener: () => void): () => void { return this.loop.subscribe(listener); }
  checkpoint(write: (seed: number, save?: RunSave) => Promise<void>): Promise<() => void> {
    return this.loop.checkpoint(write);
  }

  private field<T extends HTMLElement>(selector: string): T { return this.element.querySelector<T>(selector)!; }
  private text(selector: string, value: string): void {
    const field = this.field(selector);
    if (field.textContent !== value) field.textContent = value;
  }
  private render(): void {
    const loop = this.loop, snapshot = loop.snapshot, ended = isRunTerminal(snapshot);
    this.loading?.update(loop.isReady && !loop.loadError, loop.loadError);
    this.element.dataset.mapMode = this.mapMode;
    this.element.dataset.timeSeconds = String(snapshot.simulationTimeSeconds);
    this.element.dataset.days = String(snapshot.completedDays ?? 0);
    this.element.dataset.fuelConsumed = String(snapshot.shift?.fuelConsumed ?? 0);
    this.element.dataset.solving = String(loop.busy);
    this.element.dataset.running = String(loop.running);
    this.field<HTMLProgressElement>("progress").value = loop.dayProgress;
    this.text("[data-live]", !loop.isReady ? "Preparing core…" : ended ? "Final core" : this.tutorial
      ? loop.busy ? "Calculating…" : "Tutorial · paused" : !loop.running ? "Paused" : "Live power");
    const minutes = Math.floor(loop.playbackDay * 24 * 60);
    this.text("[data-day]", shiftTime(minutes));
    this.text("[data-score]", snapshot.scoreTotal.toFixed(1));
    const ripple = snapshot.ripple;
    this.text("[data-score-rate]", ripple?.ripplePointsPerHour !== undefined && ripple.tiltPointsPerHour !== undefined
      ? `Ripple ${ripple.ripplePointsPerHour.toFixed(2)} · Tilt ${ripple.tiltPointsPerHour.toFixed(2)} pts/h`
      : "");
    this.text("[data-message]", ended ? "" : loop.message);
    const waiting = loop.waitingFuel.size, pending = loop.pendingFuel.length;
    this.text("[data-fuel-queue]", pending ? `${waiting} waiting · ${pending - waiting} submitted` : "Queue empty");
    this.renderFuelReceipt(snapshot);
    const playbackDisabled = !this.started || !loop.canTogglePause;
    const pause = this.field<HTMLButtonElement>("[data-action=pause]");
    const pauseLabel = ended ? "Run ended" : loop.running ? "Pause" : "Resume";
    pause.disabled = playbackDisabled;
    pause.setAttribute("aria-label", pauseLabel);
    pause.title = pauseLabel;
    for (const speed of ["slow", "fast"] as const) {
      const button = this.field<HTMLButtonElement>(`[data-action=${speed}]`);
      button.disabled = playbackDisabled;
      button.setAttribute("aria-pressed", String(loop.speed === speed));
    }
    const restartDisabled = !this.started || !loop.isReady || loop.busy;
    for (const action of ["retry", "new"]) {
      const button = this.field<HTMLButtonElement>(`[data-action=${action}]`);
      button.hidden = ended;
      button.disabled = restartDisabled;
      this.field<HTMLButtonElement>(`[data-action=${action}-ended]`).disabled = restartDisabled;
    }
    this.renderRunEnd(ended);
    const selected = snapshot.core.channels.find(c => c.channelIndex === this.selected);
    const fuel = this.field<HTMLButtonElement>("[data-action=fuel]");
    const removal = !this.tutorial && loop.waitingFuel.has(this.selected);
    const submitted = !removal && loop.pendingFuel.includes(this.selected);
    const fuelLabel = removal ? "Remove order" : submitted && !this.tutorial ? "Order submitted" : "Queue fuel";
    if (fuel.textContent !== fuelLabel) fuel.textContent = fuelLabel;
    fuel.disabled = !this.started || (removal ? !loop.canRemoveFuel(this.selected)
      : !this.canQueue(this.selected) || !selected || selected.canRefuel === false || submitted);
    this.tutorial?.update(this.selected);
    if (!loop.isReady) return;
    if (!this.map) {
      this.map = new ArcadeMap(snapshot, index => { this.selected = index; this.render(); }, index => this.editFuel(index), true);
      this.field("[data-map]").append(this.map.element);
    }
    if (this.legendMode !== this.mapMode) {
      renderTimeMapScale(this.field("[data-scale]"), this.mapMode);
      this.legendMode = this.mapMode;
    }
    this.trends.update(snapshot, loop.playbackDay, this.selected, !loop.running);
    const key = `${this.mapMode}:${this.selected}:${loop.pendingFuel}:${waiting}:${ended}`;
    if (snapshot !== this.lastSnapshot || key !== this.lastMapKey) {
      this.map.update(snapshot, this.selected, loop.pendingFuel, this.mapMode, ended, loop.waitingFuel);
      this.lastSnapshot = snapshot; this.lastMapKey = key;
      this.text("[data-peak]", `${(Math.max(...snapshot.core.channels.map(c => channelWatts(snapshot, c))) / 1000).toFixed(0)} kW`);
      this.text("[data-legend]", timeMapLegend(snapshot, this.mapMode));
      this.text("[data-selected-channel]", selected ? gridCoordinateLabel(selected) : "—");
      this.text("[data-power]", selected ? (channelWatts(snapshot, selected) / 1000).toFixed(0) : "—");
      this.text("[data-channel-limit]", `Limit ${(channelLimit(snapshot) / 1000).toLocaleString("en-US")} kW`);
      const zoneReading = this.field("[data-selected-zone]");
      zoneReading.hidden = this.mapMode !== "inlet-zones" || !selected;
      if (!zoneReading.hidden && selected) zoneReading.textContent = timeMapCell(snapshot, selected, this.mapMode).reading;
      this.renderZones(snapshot);
    }
  }
  private renderRunEnd(ended: boolean): void {
    if (this.tutorial) return;
    this.field("[data-action=result]").hidden = !ended;
    if (!ended) {
      this.endPresented = false;
      if (this.endDialog.open) this.closeEndDialog();
      return;
    }
    this.text("#demo-end-reason", this.loop.snapshot.runEndReason || "The run reached its end.");
    if (this.started && !this.endPresented) this.showEndDialog();
  }
  private showEndDialog(): void {
    if (this.endDialog.open) return;
    this.endPresented = true;
    if (typeof this.endDialog.showModal === "function") this.endDialog.showModal();
    else this.endDialog.setAttribute("open", "");
  }
  private closeEndDialog(): void {
    if (typeof this.endDialog.close === "function") this.endDialog.close();
    else this.endDialog.removeAttribute("open");
  }
  private readonly onEndDialogClose = (): void => {
    if (isRunTerminal(this.loop.snapshot)) this.field("[data-action=result]").focus({ preventScroll: true });
  };
  private async restart(seed: number, fromResult: boolean): Promise<void> {
    if (fromResult) this.text("[data-restart-status]", "Preparing core…");
    const restarted = await this.loop.restart(seed);
    if (!fromResult || !this.element.isConnected) return;
    this.text("[data-restart-status]", restarted ? "" : this.loop.message || "Unable to restart the core.");
    if (restarted && !isRunTerminal(this.loop.snapshot)) this.map?.focusSelected();
  }
  private renderFuelReceipt(snapshot: CanduSnapshot): void {
    const result = snapshot.lastDayResult;
    if (result === this.lastReceiptResult) return;
    this.lastReceiptResult = result;
    const response = this.field<HTMLDetailsElement>("[data-fuel-zone-response]");
    if (!result) {
      this.text("[data-fuel-receipt]", "");
      this.text("[data-fuel-zone-values]", "");
      response.hidden = true;
      response.open = false;
      return;
    }
    // Keep the latest fuel receipt available across steps with an empty queue.
    if (!result.fuelUsed && !result.unexecutedChannels.length) return;
    const skipped = result.unexecutedChannels.length;
    const burnup = result.movements.reduce((points, { score }) => points + score.dischargeReward - score.freshFuelCost, 0);
    const zones = result.movements.reduce((points, { score }) => points + (score.zoneBalanceBonus ?? 0), 0);
    const fuelScore = result.fuelUsed ? ` · Burnup ${burnup >= 0 ? "+" : ""}${burnup.toFixed(1)} · Zones +${zones.toFixed(1)} pts` : "";
    this.text("[data-fuel-receipt]", `${result.fuelUsed} bundles delivered${fuelScore} · ${shiftTime(Math.floor(result.endSimulationTimeSeconds / 60))}${skipped ? ` · ${skipped} orders skipped` : ""}`);
    const fills = refuellingZoneReadings(snapshot);
    response.hidden = fills.length === 0;
    this.text("[data-fuel-zone-values]", fills.join(" · "));
  }
  private renderZones(snapshot: CanduSnapshot): void {
    const mean = snapshot.rrs.averageFillFraction;
    this.text("[data-zone-mean]", `${(mean * 100).toFixed(1)}%`);
    const alert = zoneAlert(mean);
    this.field(".demo-zone-mean").dataset.demoAlert = alert;
    const margin = Math.min(mean - .1, .9 - mean) * 100;
    this.text("[data-zone-risk]", alert === "normal" ? "" : margin < 0 ? "Outside operating range" : `${margin.toFixed(1)} percentage points to ${mean < .5 ? "lower" : "upper"} limit`);
    for (const zone of snapshot.rrs.zones) {
      const card = this.element.querySelector<HTMLElement>(`[data-zone="${zone.logicalZoneId}"]`);
      if (!card) continue;
      const percent = (zone.fillFraction * 100).toFixed(0);
      card.dataset.demoAlert = zoneAlert(zone.fillFraction);
      card.querySelector<HTMLElement>("i")!.style.height = `${Math.max(0, Math.min(100, zone.fillFraction * 100))}%`;
      card.querySelector("strong")!.textContent = `${percent}%`;
      card.querySelector("small")!.textContent = zone.fillFraction < .25 ? "LOW" : zone.fillFraction > .75 ? "HIGH" : "OK";
      card.setAttribute("aria-label", `${timeZoneLabel(zone.logicalZoneId)}, ${percent}% full`);
    }
  }
  private readonly onMapTabKey = (event: KeyboardEvent): void => {
    const next = nextTabIndex(event, mapModes.indexOf(this.mapMode), mapModes.length);
    if (next === undefined) return;
    event.preventDefault();
    this.setMapMode(mapModes[next]);
    this.mapTabs[next].focus();
  };
  private setMapMode(mode: TimeMapMode): void {
    this.mapMode = mode;
    for (const tab of this.mapTabs) {
      const active = tab.dataset.mapTab === mode;
      tab.setAttribute("aria-selected", String(active));
      tab.tabIndex = active ? 0 : -1;
    }
    this.field("#demo-map-panel").setAttribute("aria-labelledby", `demo-map-${mode}`);
    this.render();
  }
  private canQueue(index: number): boolean {
    return this.loop.canFuel && (!this.tutorial || this.started && this.tutorial.canQueue(index));
  }
  private editFuel(index: number): void {
    if (!this.started || this.tutorial && !this.tutorial.canQueue(index)) return;
    if (this.loop.waitingFuel.has(index)) this.loop.removeFuel(index);
    else this.loop.fuel(index);
  }
  private readonly pollGamepad = (): void => {
    let pad: Gamepad | undefined;
    if (document.visibilityState !== "hidden" && document.hasFocus()) {
      try {
        pad = Array.from(navigator.getGamepads?.() ?? []).find(candidate => candidate?.connected && candidate.mapping === "standard") ?? undefined;
      } catch {
        // An embedding page can disable controller access.
      }
    }
    this.field("[data-gamepad-hint]").hidden = !pad;
    const buttons = pad?.buttons.map(button => button.pressed) ?? [];
    const pressed = (index: number) => buttons[index] && !this.gamepadButtons[index];
    if (pad && pad.index === this.gamepadIndex) {
      if (!this.started) {
        const start = this.element.querySelector<HTMLButtonElement>("[data-game-start]");
        if ((pressed(0) || pressed(9)) && start && !start.disabled && !start.hidden) start.click();
      } else if (this.endDialog.open) {
        const choices = [...this.endDialog.querySelectorAll<HTMLButtonElement>("button:not(:disabled)")];
        const current = choices.findIndex(button => button === document.activeElement);
        const offset = Number(!!(pressed(13) || pressed(15))) - Number(!!(pressed(12) || pressed(14)));
        if (offset && choices.length) choices[(Math.max(current, 0) + offset + choices.length) % choices.length].focus();
        if (pressed(0)) (choices.find(button => button === document.activeElement) ?? choices[0])?.click();
      } else if (!this.tutorial && isRunTerminal(this.loop.snapshot)) {
        if (pressed(0) || pressed(9)) this.showEndDialog();
      } else {
        const direction = padDirections.find((_, index) => pressed(index + 12));
        if (direction) this.map?.moveSelection(direction);
        if (pressed(9)) this.loop.togglePause();
        else if (pressed(0)) this.editFuel(this.selected);
        const offset = Number(pressed(5) === true) - Number(pressed(4) === true);
        if (offset) this.setMapMode(mapModes[(mapModes.indexOf(this.mapMode) + offset + mapModes.length) % mapModes.length]);
      }
    }
    this.gamepadIndex = pad?.index;
    this.gamepadButtons = buttons;
  };
  private readonly onClick = (event: MouseEvent): void => {
    const action = (event.target as Element).closest<HTMLButtonElement>("[data-action]");
    if (!action || action.disabled) return;
    switch (action.dataset.action) {
      case "leave":
      case "leave-ended": this.leave(); break;
      case "result": this.showEndDialog(); break;
      case "pause": this.loop.togglePause(); break;
      case "slow": this.loop.toggleSpeed("slow"); break;
      case "fast": this.loop.toggleSpeed("fast"); break;
      case "map": {
        const mode = mapModes.find(mode => mode === action.dataset.mapTab);
        if (mode) this.setMapMode(mode);
        break;
      }
      case "fuel": this.editFuel(this.selected); break;
      case "retry":
      case "retry-ended": void this.restart(this.loop.snapshot.shift?.seed ?? 1001, action.dataset.action === "retry-ended"); break;
      case "new":
      case "new-ended": void this.restart(randomCoreSeed(this.loop.snapshot.shift?.seed), action.dataset.action === "new-ended"); break;
    }
  };
  private readonly onVisibility = (): void => this.loop.setVisible(document.visibilityState !== "hidden");
  destroy(): void { clearInterval(this.gamepadTimer); this.loading?.destroy(); this.unsubscribe(); this.loop.destroy(); this.map?.destroy(); document.removeEventListener("visibilitychange", this.onVisibility); this.element.removeEventListener("click", this.onClick); this.field(".demo-map-tabs").removeEventListener("keydown", this.onMapTabKey); this.endDialog.removeEventListener("close", this.onEndDialogClose); if (this.endDialog.open) this.closeEndDialog(); this.element.remove(); }
}
