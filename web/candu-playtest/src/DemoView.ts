import { ArcadeMap } from "./ArcadeMap";
import { createCanduPlaytestBridge } from "./bridge";
import { CoreMapZoom } from "./CoreMapInteraction";
import { gridCoordinateLabel } from "./channelCoordinates";
import { randomCoreSeed } from "./coreSeed";
import { DemoLoop } from "./DemoLoop";
import { isRunTerminal, type CanduSnapshot } from "./protocol";
import { channelLimit, channelWatts, mapLegend } from "./studio/powerReadings";
import "./arcade.css";
import "./demo.css";

/** A separate ephemeral run: the daily session and its save/draft remain intact. */
export class DemoView {
  readonly element = document.createElement("main");
  private readonly loop: DemoLoop;
  private readonly unsubscribe: () => void;
  private map?: ArcadeMap;
  private zoom?: CoreMapZoom;
  private selected = 0;
  private lastMapKey = "";
  private lastSnapshot?: CanduSnapshot;

  constructor(parent: HTMLElement, seed: number, private readonly leave: () => void) {
    this.element.className = "reactor-arcade reactor-demo";
    this.element.innerHTML = `<header><div><p class="arcade-kicker">CRS / LIVE DEMO</p><h1>Chase the power.</h1></div><nav aria-label="Demo controls"><button data-action="pause">Pause</button><button data-action="leave">Back to daily run</button></nav></header>
      <div class="demo-hud"><div><span>DAY</span><strong data-day>0</strong></div><div><span>PEAK CHANNEL</span><strong data-peak>—</strong></div><div><span>SCORE</span><strong data-score>0</strong></div><div><span>RHYTHM</span><strong>1 day / 5 sec</strong></div></div>
      <progress class="demo-clock" max="1" value="0" aria-label="Time until next day"></progress>
      <section class="demo-workspace"><div><div class="demo-map-heading"><h2>Channel power <small>kW</small></h2><span data-live>Preparing core…</span></div><div class="arcade-map-scroll" data-map></div><p class="arcade-legend" data-legend></p></div>
      <aside class="demo-readout"><p class="arcade-kicker">SELECTED CHANNEL <span data-selected-channel>—</span></p><strong data-power>—</strong><p>kW · latest solved power</p><button class="arcade-primary" data-action="fuel">Fuel selected channel</button><p>Double-click any channel to fuel immediately. No plan or confirmation.</p><p>Arrows select · Enter / R fuels. On touch, select a channel then tap Fuel.</p><p class="demo-help">Keep the power distribution balanced. Brighter red means higher channel power. Each move inserts eight fresh bundles.</p><p data-message role="status"></p><p data-ended role="status"></p><div class="demo-retry"><button data-action="retry">Retry seed</button><button data-action="new">New core</button></div><p class="demo-help">Demo run is temporary. Days and fuel moves solve in the background; slower devices may run below the target pace.</p></aside></section>`;
    parent.append(this.element);
    this.element.addEventListener("click", this.onClick);
    this.loop = new DemoLoop(createCanduPlaytestBridge({ initialPacingMode: "demo", initialSeed: seed }));
    this.loop.setVisible(document.visibilityState !== "hidden");
    this.unsubscribe = this.loop.subscribe(() => this.render());
    document.addEventListener("visibilitychange", this.onVisibility);
    this.field<HTMLButtonElement>("[data-action=pause]").focus();
  }
  private field<T extends HTMLElement>(selector: string): T { return this.element.querySelector<T>(selector)!; }
  private text(selector: string, value: string): void {
    const field = this.field(selector);
    if (field.textContent !== value) field.textContent = value;
  }
  private render(): void {
    const loop = this.loop, snapshot = loop.snapshot, ended = isRunTerminal(snapshot);
    this.element.dataset.days = String(snapshot.completedDays ?? 0);
    this.element.dataset.fuelConsumed = String(snapshot.shift?.fuelConsumed ?? 0);
    this.element.dataset.solving = String(loop.busy);
    this.element.dataset.running = String(loop.running);
    this.field<HTMLProgressElement>("progress").value = loop.running && loop.visible ? loop.dayProgress : 0;
    this.text("[data-live]", !loop.isReady ? "Preparing core…" : ended ? "Run ended" : loop.busy ? "Updating power · map stays live" : !loop.running ? "Paused" : "Live power");
    this.text("[data-day]", String(snapshot.completedDays ?? 0));
    this.text("[data-score]", snapshot.scoreTotal.toFixed(1));
    this.text("[data-message]", loop.message);
    this.text("[data-ended]", ended ? `Run ended: ${snapshot.runEndReason}. Retry to play again.` : "");
    this.text("[data-action=pause]", loop.running ? "Pause" : "Resume");
    this.field<HTMLButtonElement>("[data-action=pause]").disabled = !loop.isReady || ended;
    for (const action of ["retry", "new"]) this.field<HTMLButtonElement>(`[data-action=${action}]`).disabled = !loop.isReady || loop.busy;
    const selected = snapshot.core.channels.find(c => c.channelIndex === this.selected);
    this.field<HTMLButtonElement>("[data-action=fuel]").disabled = !loop.isReady || ended || !loop.running || selected?.canRefuel === false || loop.waitingFuel.has(this.selected);
    if (!loop.isReady) return;
    if (!this.map) {
      this.map = new ArcadeMap(snapshot, index => { this.selected = index; this.render(); }, index => loop.fuel(index), true);
      this.field("[data-map]").append(this.map.element);
      this.zoom = new CoreMapZoom(this.field("[data-map]"), this.map.element);
    }
    const key = `${this.selected}:${[...loop.waitingFuel]}:${ended}`;
    if (snapshot !== this.lastSnapshot || key !== this.lastMapKey) {
      this.map.update(snapshot, this.selected, [...loop.waitingFuel], "power", ended);
      this.lastSnapshot = snapshot; this.lastMapKey = key;
      this.text("[data-peak]", `${(Math.max(...snapshot.core.channels.map(c => channelWatts(snapshot, c))) / 1000).toFixed(0)} kW`);
      this.text("[data-legend]", mapLegend(snapshot, "power"));
      this.text("[data-selected-channel]", selected ? gridCoordinateLabel(selected) : "—");
      this.text("[data-power]", selected ? (channelWatts(snapshot, selected) / 1000).toFixed(0) : "—");
      this.field("[data-power]").setAttribute("title", `Channel limit ${(channelLimit(snapshot) / 1000).toFixed(0)} kW`);
    }
  }
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
  destroy(): void { this.unsubscribe(); this.loop.destroy(); this.zoom?.destroy(); this.map?.destroy(); document.removeEventListener("visibilitychange", this.onVisibility); this.element.removeEventListener("click", this.onClick); this.element.remove(); }
}
