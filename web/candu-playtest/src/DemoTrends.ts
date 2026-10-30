import { isRunTerminal, type CanduSnapshot } from "./protocol";
import { channelWatts } from "./studio/powerReadings";
import { gridCoordinateLabel } from "./channelCoordinates";

type Sample = { day: number; peak: number; powers: number[]; mean: number };
const MAX_SAMPLES = 241;
const SAMPLE_DAYS = .05;

/** Bounded history of the displayed timeline, never ahead-of-playback solver data. */
export class DemoTrends {
  readonly element = document.createElement("aside");
  private samples: Sample[] = [];
  private seed?: number;
  private selected = -1;

  constructor() {
    this.element.className = "demo-trends";
    this.element.setAttribute("aria-label", "Live trend plots");
    this.element.innerHTML = `<h2>Live trends</h2><p class="demo-trend-note">Recent displayed days</p>
      <section class="demo-trend-card"><h3>Channel power <small>kW</small></h3><p class="demo-trend-key"><span class="trend-selected" data-trend-channel>Selected</span><span class="trend-peak">Core peak</span></p><svg data-chart="power" viewBox="0 0 260 180" role="img" aria-label="Selected channel and peak power history"></svg><p data-power-trend-reading></p></section>
      <section class="demo-trend-card"><h3>Average zone level <small>%</small></h3><svg data-chart="zones" viewBox="0 0 260 180" role="img" aria-label="Average liquid zone level history with 10 and 90 percent limits"></svg><p data-zone-trend-reading></p><p class="demo-trend-note">Dashed lines: 10% / 90% limits</p></section>`;
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
      const sample = { day, powers, peak: Math.max(...powers.filter(Number.isFinite)), mean: snapshot.rrs.averageFillFraction * 100 };
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
    this.element.querySelector("[data-zone-trend-reading]")!.textContent = `${latest.mean.toFixed(1)}% core average`;
    const powerValues = this.samples.flatMap(s => [s.peak, s.powers[selected]]).filter(Number.isFinite);
    this.draw("power", Math.floor(Math.min(5000, ...powerValues) / 100) * 100, Math.ceil(Math.max(7100, ...powerValues) / 100) * 100,
      [{ values: this.samples.map(s => s.peak), color: "#ffc262" }, { values: this.samples.map(s => s.powers[selected]), color: "#68ddff" }], [6500, 7100]);
    this.draw("zones", 0, 100, [{ values: this.samples.map(s => s.mean), color: "#52efbc" }], [10, 90]);
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
