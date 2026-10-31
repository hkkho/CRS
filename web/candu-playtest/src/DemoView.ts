import { ArcadeMap } from "./ArcadeMap";
import { createCanduPlaytestBridge } from "./bridge";
import { DemoTrends } from "./DemoTrends";
import { gridCoordinateLabel } from "./channelCoordinates";
import { randomCoreSeed } from "./coreSeed";
import { DemoLoop } from "./DemoLoop";
import { GameLoadingView } from "./GameLoadingView";
import { isRunTerminal, type CanduSnapshot } from "./protocol";
import { channelLimit, channelWatts } from "./studio/powerReadings";
import { timeMapLegend, type TimeMapMode } from "./timeMapPresentation";
import { zoneAlert } from "./demoPresentation";
import "./arcade.css";
import "./demo.css";

/** A separate ephemeral run: the daily session and its save/draft remain intact. */
export class DemoView {
  readonly element = document.createElement("main");
  private readonly loop: DemoLoop;
  private readonly unsubscribe: () => void;
  private map?: ArcadeMap;
  private readonly trends = new DemoTrends();
  private selected = 0;
  private mapMode: TimeMapMode = "power";
  private lastMapKey = "";
  private lastSnapshot?: CanduSnapshot;
  private loading?: GameLoadingView;
  private started = false;

  constructor(parent: HTMLElement, seed: number, private readonly leave: () => void) {
    this.element.className = "reactor-arcade reactor-demo";
    this.element.innerHTML = `<header><div><p class="arcade-kicker">CRS / TIME BASED</p><h1>Chase the power.</h1></div><nav aria-label="Time based controls"><button data-action="pause">Pause</button><button data-action="leave">Back to turn based</button></nav></header>
      <div class="demo-hud"><div><span>SIMULATION TIME</span><strong data-day>Day 0 · 00:00</strong></div><div><span>PEAK CHANNEL</span><strong data-peak>—</strong></div><div><span>SCORE</span><strong data-score>0</strong></div><div class="demo-selected-hud"><div><span>SELECTED CHANNEL <b data-selected-channel>—</b></span><strong><b data-power>—</b> <small>kW</small></strong></div><button class="arcade-primary" data-action="fuel">Queue fuel</button></div></div>
      <progress class="demo-clock" max="1" value="0" aria-label="Time step progress"></progress>
      <section class="demo-workspace"><div class="demo-core"><div class="demo-map-heading"><select data-map-mode aria-label="Core map"><option value="power">Channel power · kW</option><option value="ripple">Ripple · % of reference</option><option value="burnup">Burnup · MWd/kg</option><option value="paired-zones">Paired zone fills · %</option></select><span data-live>Preparing core…</span></div><div class="arcade-map-scroll" data-map></div><p class="arcade-legend" data-legend></p></div>
      <aside class="demo-readout"><section class="demo-zones" aria-label="14 liquid zone levels"><h2>Liquid zones <small>14 levels</small></h2><div class="demo-zone-mean"><span>CORE AVERAGE</span><strong data-zone-mean>—</strong><p data-zone-risk></p></div><div class="demo-zone-grid">${Array.from({ length: 14 }, (_, i) => `<div class="demo-zone" data-zone="${i}"><span>Z${i + 1}</span><div class="demo-zone-track"><i></i></div><strong>—</strong><small>—</small></div>`).join("")}</div><p class="demo-help">Average must stay within 10–90%. Amber near an edge; red below 15% or above 85%. Individual zones show remaining control room.</p></section><p>Double-click to queue eight fresh bundles. Fuel is queued for the first available opportunity.</p><p>Arrows select · Enter / R queues. On touch, select a channel then tap Queue fuel.</p><p data-message role="status"></p><p data-ended role="status"></p><div class="demo-retry"><button data-action="retry">Retry seed</button><button data-action="new">New core</button></div><p class="demo-help">Temporary run · pause to stop the clock.</p></aside></section>`;
    this.element.querySelector(".demo-workspace")!.prepend(this.trends.element);
    parent.append(this.element);
    this.element.addEventListener("click", this.onClick);
    this.element.addEventListener("change", this.onMapChange);
    this.loop = new DemoLoop(createCanduPlaytestBridge({ initialPacingMode: "demo", initialSeed: seed }), undefined, true);
    this.loop.setVisible(document.visibilityState !== "hidden");
    this.loading = new GameLoadingView(this.element, () => {
      this.loading = undefined; this.started = true; this.loop.togglePause();
      this.field<HTMLButtonElement>("[data-action=pause]").focus();
    }, this.leave);
    this.unsubscribe = this.loop.subscribe(() => this.render());
    document.addEventListener("visibilitychange", this.onVisibility);
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
    this.element.dataset.days = String(snapshot.completedDays ?? 0);
    this.element.dataset.fuelConsumed = String(snapshot.shift?.fuelConsumed ?? 0);
    this.element.dataset.solving = String(loop.busy);
    this.element.dataset.running = String(loop.running);
    this.field<HTMLProgressElement>("progress").value = loop.dayProgress;
    this.text("[data-live]", !loop.isReady ? "Preparing core…" : ended ? "Run ended" : !loop.running ? "Paused" : "Live power");
    const minutes = Math.floor(loop.playbackDay * 24 * 60);
    this.text("[data-day]", `Day ${Math.floor(minutes / 1440)} · ${String(Math.floor(minutes / 60) % 24).padStart(2, "0")}:${String(minutes % 60).padStart(2, "0")}`);
    this.text("[data-score]", snapshot.scoreTotal.toFixed(1));
    this.text("[data-message]", loop.message);
    this.text("[data-ended]", ended ? `Run ended: ${snapshot.runEndReason}. Retry to play again.` : "");
    this.text("[data-action=pause]", ended ? "Run ended" : loop.running ? "Pause" : "Resume");
    this.field<HTMLButtonElement>("[data-action=pause]").disabled = !this.started || !loop.canTogglePause;
    for (const action of ["retry", "new"]) this.field<HTMLButtonElement>(`[data-action=${action}]`).disabled = !this.started || !loop.isReady || loop.busy;
    const selected = snapshot.core.channels.find(c => c.channelIndex === this.selected);
    this.field<HTMLButtonElement>("[data-action=fuel]").disabled = !loop.canFuel || selected?.canRefuel === false || loop.pendingFuel.includes(this.selected);
    if (!loop.isReady) return;
    if (!this.map) {
      this.map = new ArcadeMap(snapshot, index => { this.selected = index; this.render(); }, index => loop.fuel(index), true);
      this.field("[data-map]").append(this.map.element);
    }
    this.trends.update(snapshot, loop.playbackDay, this.selected, !loop.running);
    const key = `${this.mapMode}:${this.selected}:${loop.pendingFuel}:${ended}`;
    if (snapshot !== this.lastSnapshot || key !== this.lastMapKey) {
      this.map.update(snapshot, this.selected, loop.pendingFuel, this.mapMode, ended);
      this.lastSnapshot = snapshot; this.lastMapKey = key;
      this.text("[data-peak]", `${(Math.max(...snapshot.core.channels.map(c => channelWatts(snapshot, c))) / 1000).toFixed(0)} kW`);
      this.text("[data-legend]", timeMapLegend(snapshot, this.mapMode));
      this.text("[data-selected-channel]", selected ? gridCoordinateLabel(selected) : "—");
      this.text("[data-power]", selected ? (channelWatts(snapshot, selected) / 1000).toFixed(0) : "—");
      this.field("[data-power]").setAttribute("title", `Channel limit ${(channelLimit(snapshot) / 1000).toFixed(0)} kW`);
      this.renderZones(snapshot);
    }
  }
  private renderZones(snapshot: CanduSnapshot): void {
    const mean = snapshot.rrs.averageFillFraction;
    this.text("[data-zone-mean]", `${(mean * 100).toFixed(1)}%`);
    this.field(".demo-zone-mean").dataset.demoAlert = zoneAlert(mean);
    const margin = Math.min(mean - .1, .9 - mean) * 100;
    this.text("[data-zone-risk]", margin < 0 ? "Outside operating range" : `${margin.toFixed(1)} percentage points to ${mean < .5 ? "lower" : "upper"} limit`);
    for (const zone of snapshot.rrs.zones) {
      const card = this.element.querySelector<HTMLElement>(`[data-zone="${zone.logicalZoneId}"]`);
      if (!card) continue;
      const percent = (zone.fillFraction * 100).toFixed(0);
      card.dataset.demoAlert = zoneAlert(zone.fillFraction);
      card.querySelector<HTMLElement>("i")!.style.height = `${Math.max(0, Math.min(100, zone.fillFraction * 100))}%`;
      card.querySelector("strong")!.textContent = `${percent}%`;
      card.querySelector("small")!.textContent = zone.fillFraction < .25 ? "LOW" : zone.fillFraction > .75 ? "HIGH" : "OK";
      card.setAttribute("aria-label", `Zone ${zone.logicalZoneId + 1}: ${percent}% full`);
    }
  }
  private readonly onMapChange = (event: Event): void => {
    const select = event.target as HTMLSelectElement;
    if (!select.matches("[data-map-mode]")) return;
    if (!["power", "ripple", "burnup", "paired-zones"].includes(select.value)) return;
    this.mapMode = select.value as TimeMapMode; this.render();
  };
  private readonly onClick = (event: MouseEvent): void => {
    const action = (event.target as Element).closest<HTMLButtonElement>("[data-action]");
    if (!action || action.disabled) return;
    switch (action.dataset.action) {
      case "leave": this.leave(); break;
      case "pause": this.loop.togglePause(); break;
      case "fuel": this.loop.fuel(this.selected); break;
      case "retry": void this.loop.restart(this.loop.snapshot.shift?.seed ?? 1001); break;
      case "new": void this.loop.restart(randomCoreSeed(this.loop.snapshot.shift?.seed)); break;
    }
  };
  private readonly onVisibility = (): void => this.loop.setVisible(document.visibilityState !== "hidden");
  destroy(): void { this.loading?.destroy(); this.unsubscribe(); this.loop.destroy(); this.map?.destroy(); document.removeEventListener("visibilitychange", this.onVisibility); this.element.removeEventListener("click", this.onClick); this.element.removeEventListener("change", this.onMapChange); this.element.remove(); }
}
