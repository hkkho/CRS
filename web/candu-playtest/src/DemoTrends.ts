import { isRunTerminal, type CanduSnapshot } from "./protocol";
import { channelWatts } from "./studio/powerReadings";
import { gridCoordinateLabel } from "./channelCoordinates";
import { channelZonePair, timeZoneLabel } from "./timeMapPresentation";
import { nextTabIndex } from "./demoPresentation";

type Sample = { day: number; peak: number; powers: number[]; mean: number; zoneFills: number[] };
const MAX_SAMPLES = 241;
const SAMPLE_DAYS = .05;

/** Bounded history of the displayed timeline, never ahead-of-playback solver data. */
export class DemoTrends {
  readonly element = document.createElement("aside");
  private samples: Sample[] = [];
  private seed?: number;
  private selected = -1;
  private activeTab = 0;

  constructor() {
    this.element.className = "demo-trends";
    this.element.setAttribute("aria-label", "Live trend plots");
    this.element.innerHTML = `<h2>Live trends</h2>
      <div class="demo-tabs demo-trend-tabs" role="tablist" aria-label="Live trends">
        <button type="button" id="demo-trend-power" role="tab" aria-selected="true" aria-controls="demo-trend-power-panel">Power</button>
        <button type="button" id="demo-trend-zones" role="tab" aria-selected="false" aria-controls="demo-trend-zones-panel" tabindex="-1">Zone fills</button>
      </div>
      <section class="demo-trend-card" id="demo-trend-power-panel" role="tabpanel" aria-labelledby="demo-trend-power" tabindex="0"><h3>Channel power <small>kW</small></h3><p class="demo-trend-key"><span class="trend-selected" data-trend-channel>Selected</span><span class="trend-peak">Core peak</span></p><svg data-chart="power" viewBox="0 0 260 180" role="img" aria-label="Selected channel and peak power history"></svg><p data-power-trend-reading></p></section>
      <section class="demo-trend-card" id="demo-trend-zones-panel" role="tabpanel" aria-labelledby="demo-trend-zones" tabindex="0" hidden><h3>Liquid-zone fill <small>%</small></h3><p class="demo-trend-key"><span class="trend-mean">Core average</span><span class="trend-end-a" data-trend-end-a></span><span class="trend-end-b" data-trend-end-b></span></p><svg data-chart="zones" viewBox="0 0 260 180" role="img" aria-label="Core average and selected channel End A and End B liquid zone history, with 10 and 90 percent limits"></svg><p data-zone-trend-reading></p><p class="demo-trend-note">Dashed lines: 10% / 90% limits</p></section>`;
    const tablist = this.element.querySelector('[role="tablist"]')!;
    const tabs = [...tablist.querySelectorAll<HTMLButtonElement>('[role="tab"]')];
    const panels = [...this.element.querySelectorAll<HTMLElement>('[role="tabpanel"]')];
    const selectTab = (index: number) => {
      this.activeTab = index;
      tabs.forEach((tab, i) => {
        const selected = i === index;
        tab.setAttribute("aria-selected", String(selected));
        tab.tabIndex = selected ? 0 : -1;
        panels[i].hidden = !selected;
      });
    };
    tabs.forEach((tab, i) => tab.addEventListener("click", () => selectTab(i)));
    tablist.addEventListener("keydown", event => {
      const key = event as KeyboardEvent;
      const next = nextTabIndex(key, this.activeTab, tabs.length);
      if (next === undefined) return;
      key.preventDefault();
      selectTab(next);
      tabs[next].focus();
    });
  }

  update(snapshot: CanduSnapshot, day: number, selected: number, paused = false): void {
    const previous = this.samples.at(-1);
    if (snapshot.shift?.seed !== this.seed || previous && day < previous.day) this.samples = [];
    this.seed = snapshot.shift?.seed;
    const last = this.samples.at(-1);
    const changedSelection = this.selected !== selected;
    this.selected = selected;
    if (!last || day - last.day >= SAMPLE_DAYS - 1e-6 || isRunTerminal(snapshot) || paused) {
      const powers: number[] = [];
      for (const c of snapshot.core.channels) powers[c.channelIndex] = channelWatts(snapshot, c) / 1000;
      const zoneFills: number[] = [];
      for (const zone of snapshot.rrs.zones) zoneFills[zone.logicalZoneId] = zone.fillFraction * 100;
      const sample = { day, powers, peak: Math.max(...powers.filter(Number.isFinite)), mean: snapshot.rrs.averageFillFraction * 100, zoneFills };
      // A fuel-limit loss can happen without advancing a whole day. Retain its
      // final reading at the same timestamp rather than leaving a stale graph.
      if (last && day === last.day) this.samples[this.samples.length - 1] = sample;
      else this.samples.push(sample);
      if (this.samples.length > MAX_SAMPLES) this.samples.shift();
    } else if (!changedSelection) return;
    const channel = snapshot.core.channels.find(c => c.channelIndex === selected);
    const label = channel ? gridCoordinateLabel(channel) : "Selected";
    this.element.querySelector("[data-trend-channel]")!.textContent = label;
    const latest = this.samples.at(-1)!;
    const selectedPower = latest.powers[selected];
    this.element.querySelector("[data-power-trend-reading]")!.textContent = `${label}: ${selectedPower?.toFixed(0) ?? "—"} · Peak: ${latest.peak.toFixed(0)} kW`;
    const [endA, endB] = channel ? channelZonePair(channel) : [];
    this.element.querySelector("[data-trend-end-a]")!.textContent = endA === undefined ? "End A" : `End A · ${timeZoneLabel(endA)}`;
    this.element.querySelector("[data-trend-end-b]")!.textContent = endB === undefined ? "End B" : `End B · ${timeZoneLabel(endB)}`;
    const zoneReading = (id: number | undefined) => {
      const fill = id === undefined ? NaN : latest.zoneFills[id];
      return Number.isFinite(fill) ? `${fill.toFixed(1)}%` : "—";
    };
    this.element.querySelector("[data-zone-trend-reading]")!.textContent = `${latest.mean.toFixed(1)}% core average · ${label} End A: ${zoneReading(endA)} · End B: ${zoneReading(endB)}`;
    const powerValues = this.samples.flatMap(s => [s.peak, s.powers[selected]]).filter(Number.isFinite);
    this.draw("power", Math.floor(Math.min(5000, ...powerValues) / 100) * 100, Math.ceil(Math.max(7100, ...powerValues) / 100) * 100,
      [{ values: this.samples.map(s => s.peak), color: "#ffc262" }, { values: this.samples.map(s => s.powers[selected]), color: "#68ddff" }], [6500, 7100]);
    const zoneLines = [{ values: this.samples.map(s => s.mean), color: "#52efbc" }];
    if (endA !== undefined) zoneLines.push({ values: this.samples.map(s => s.zoneFills[endA]), color: "#68ddff" });
    if (endB !== undefined) zoneLines.push({ values: this.samples.map(s => s.zoneFills[endB]), color: "#ffc262" });
    this.draw("zones", 0, 100, zoneLines, [10, 90]);
    this.element.dataset.samples = String(this.samples.length);
  }

  private draw(name: string, low: number, high: number, lines: { values: number[]; color: string }[], limits: number[]): void {
    const svg = this.element.querySelector<SVGSVGElement>(`[data-chart=${name}]`)!;
    const firstDay = this.samples[0].day, lastDay = this.samples.at(-1)!.day;
    const end = Math.max(firstDay + 1, lastDay);
    const x = (day: number) => 38 + (day - firstDay) / (end - firstDay) * 210;
    const y = (value: number) => 145 - (value - low) / (high - low) * 128;
    const axis = [low, (low + high) / 2, high].map(value => `<path class="trend-grid" d="M38 ${y(value)}H248"/><text x="32" y="${y(value) + 3}" text-anchor="end">${value.toFixed(0)}</text>`).join("");
    const thresholds = limits.map(value => `<path class="trend-limit" d="M38 ${y(value)}H248"/>`).join("");
    const paths = lines.map(line => {
      let penDown = false;
      const path = line.values.map((value, i) => {
        if (!Number.isFinite(value)) { penDown = false; return ""; }
        const part = `${penDown ? "L" : "M"}${x(this.samples[i].day).toFixed(1)},${y(value).toFixed(1)}`;
        penDown = true; return part;
      }).join(" ");
      const value = line.values.at(-1)!;
      return `<path class="trend-line" stroke="${line.color}" d="${path}"/>${Number.isFinite(value) ? `<circle cx="${x(lastDay)}" cy="${y(value)}" r="3" fill="${line.color}"/>` : ""}`;
    }).join("");
    svg.innerHTML = `${axis}${thresholds}${paths}<text x="38" y="170">Day ${firstDay.toFixed(1)}</text><text x="248" y="170" text-anchor="end">${end.toFixed(1)}</text>`;
  }
}
