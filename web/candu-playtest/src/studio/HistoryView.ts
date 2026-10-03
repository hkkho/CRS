import { formatLiquidZoneRegion } from "../visuals";
import { HISTORY_CAPACITY, ReactorHistory, type ReactorHistoryPoint } from "./ReactorHistory";
import { displaySamples } from './displaySamples';
import { patchMarkup } from './domPatch';

export type StudioTab = "reactor" | "power" | "burnup" | "zones" | "tilt" | "reactivity" | "xenon" | "fuel";
const TABS: [StudioTab, string][] = [["reactor", "Reactor"], ["power", "Power peaks"],
  ["burnup", "Discharge burnup"], ["zones", "Zone levels"], ["tilt", "Tilt"],
  ["reactivity", "Reactivity"], ["xenon", "Iodine & xenon"], ["fuel", "Fuel & score"]];
const COLORS = ["#a8eda3", "#ffc56c", "#80cfff", "#e2e183", "#ccacf5", "#70dfd3", "#ff9db0",
  "#68cf75", "#ffa875", "#a6baff", "#d1d66b", "#eba7e6", "#8de9c2", "#f1b898", "#e4ffd5"];
interface Series {
  name: string;
  read: (point: ReactorHistoryPoint) => number | null;
  color: string;
  dashed?: boolean;
  step?: boolean;
}
interface Chart {
  title: string;
  unit: string;
  series: Series[];
  zero?: boolean;
  domain?: [number, number];
  reference?: number;
}
const series = (name: string, field: keyof Omit<ReactorHistoryPoint, "zoneFills" | "zoneXenon">, color = COLORS[0], step = false): Series =>
  ({ name, read: point => point[field], color, step });

function chartsFor(tab: StudioTab): Chart[] {
  switch (tab) {
    case "xenon": return [
      { title: "Core iodine and xenon", unit: "10²⁰ atoms/m³", zero: true,
        series: [series("Iodine-135", "meanIodine"), series("Xenon-135", "meanXenon", COLORS[1])] },
      { title: "Xenon by measured zone", unit: "10²⁰ atoms/m³", zero: true,
        series: Array.from({ length: 14 }, (_, zone) => ({ name: formatLiquidZoneRegion(zone),
          color: COLORS[zone], dashed: zone >= 7, read: (point: ReactorHistoryPoint) => point.zoneXenon[zone] })) },
    ];
    case "power": return [
      { title: "Maximum channel power", unit: "MW thermal", zero: true, series: [series("Hottest channel", "maxChannelMw")] },
      { title: "Maximum bundle power", unit: "kW thermal", zero: true, series: [series("Hottest bundle", "maxBundleKw", COLORS[1])] },
      { title: "Total reactor output", unit: "MW", zero: true, series: [series("Thermal", "thermalMw"), series("Electrical", "electricalMw", COLORS[2])] },
    ];
    case "burnup": return [
      { title: "Maximum discharged bundle burnup", unit: "MWd/kg HM", zero: true,
        series: [series("Last discharge maximum", "lastDischargeBurnup", COLORS[1], true), series("Shift record", "maximumDischargeBurnup", COLORS[0], true)] },
      { title: "Fuel still in the core", unit: "MWd/kg HM", zero: true,
        series: [series("Maximum bundle burnup", "maximumFuelBurnup"), series("Average bundle burnup", "meanFuelBurnup", COLORS[2])] },
    ];
    case "zones": return [{ title: "Fourteen liquid-zone levels", unit: "% full", domain: [0, 100], series: [
      ...Array.from({ length: 14 }, (_, zone) => ({ name: formatLiquidZoneRegion(zone),
        color: COLORS[zone], dashed: zone >= 7, read: (point: ReactorHistoryPoint) => point.zoneFills[zone] })),
      { name: "Core average", color: COLORS[14], dashed: true, read: point => point.meanZoneFill },
    ] }];
    case "tilt": return [
      { title: "Signed core axial tilt", unit: "% · End B positive", reference: 0, series: [series("Core axial tilt", "axialTiltPercent")] },
      { title: "Largest local axial tilt", unit: "% absolute", zero: true, series: [series("Maximum channel tilt", "maxLocalTiltPercent", COLORS[1])] },
    ];
    case "reactivity": return [
      { title: "Effective multiplication factor", unit: "Keff", reference: 1, series: [series("Solved Keff", "effectiveK")] },
      { title: "Reactivity", unit: "mk", reference: 0, series: [series("Regulated net", "reactivityMk"), series("Core before RRS", "coreReactivityMk", COLORS[1])] },
      { title: "Regulating headroom", unit: "% reserve", domain: [0, 100], series: [series("RRS reserve", "reservePercent", COLORS[2])] },
    ];
    case "fuel": return [
      { title: "Fresh fuel remaining", unit: "bundles", zero: true, series: [series("Fresh inventory", "freshBundles", COLORS[0], true)] },
      { title: "Shift score", unit: "points", series: [series("Total score", "score", COLORS[1])] },
      { title: "Completed refuelling", unit: "operations", zero: true, series: [series("Fuel moves", "operations", COLORS[2], true)] },
    ];
    default: return [];
  }
}

function timeLabel(seconds: number): string {
  const hours = seconds / 3600;
  return hours >= 48 ? `${(hours / 24).toFixed(1)} d` : `${hours.toFixed(1)} h`;
}
function numberLabel(value: number, unit: string): string {
  return unit === "Keff" ? value.toFixed(6) : Math.abs(value) >= 1000 ? value.toFixed(0) : value.toFixed(2);
}

/** Accessible, responsive line charts over the session's observation history. */
export class HistoryView {
  public readonly element = document.createElement("section");
  public tab: StudioTab = "reactor";
  private readonly panel: HTMLElement;
  private readonly plots: HTMLElement;
  private readonly inspector: HTMLInputElement;
  private readonly hiddenSeries = new Set<string>();
  private windowSeconds = 0;
  private inspectionTime: number | null = null;
  private inspectionPoint: ReactorHistoryPoint | null = null;
  private lastVersion = -1;
  private charts: Chart[] = [];

  public constructor(private readonly history: ReactorHistory, private readonly onTab: (tab: StudioTab) => void) {
    this.element.className = "studio-history";
    this.element.innerHTML = `<div class="studio-tabs" role="tablist" aria-label="Reactor Studio tabs">${TABS.map(([id, label]) =>
      `<button id="studio-tab-${id}" role="tab" data-tab="${id}" aria-controls="${id === "reactor" ? "studio-reactor-panel" : "studio-history-panel"}" aria-selected="${id === "reactor"}" tabindex="${id === "reactor" ? 0 : -1}">${label}</button>`).join("")}</div>
      <section id="studio-history-panel" class="studio-history-panel" role="tabpanel" hidden>
        <div class="studio-history-heading"><div><p class="studio-eyebrow">SHIFT HISTORY / SIMULATION TIME</p><h2 data-history="title"></h2><p data-history="description"></p></div>
          <label>Time window <select data-history="window" aria-label="Graph time window"><option value="0">All recorded</option><option value="21600">Last 6 hours</option><option value="86400">Last 24 hours</option><option value="604800">Last 7 days</option></select></label></div>
        <div class="studio-history-inspect"><label>Inspect a sample <input type="range" min="0" max="0" value="0" aria-label="Inspect history sample" data-history="inspector"></label><output data-history="readout"></output><button data-history="live">Latest</button></div>
        <div data-history="plots" class="studio-trend-grid"></div>
        <p data-history="note" class="studio-history-note"></p>
      </section>`;
    this.panel = this.element.querySelector("#studio-history-panel")!;
    this.plots = this.element.querySelector('[data-history="plots"]')!;
    this.inspector = this.element.querySelector('[data-history="inspector"]')!;
    this.element.addEventListener("click", this.onClick);
    this.element.addEventListener("keydown", this.onKeyDown);
    this.element.addEventListener("change", this.onWindow);
    this.inspector.addEventListener("input", this.onInspect);
    this.plots.addEventListener("pointermove", this.onPointer);
  }

  public setTab(tab: StudioTab): void {
    if (!TABS.some(([id]) => id === tab)) return;
    this.tab = tab;
    this.element.querySelectorAll<HTMLButtonElement>("[data-tab]").forEach(button => {
      const selected = button.dataset.tab === tab;
      button.setAttribute("aria-selected", String(selected)); button.tabIndex = selected ? 0 : -1;
    });
    this.panel.hidden = tab === "reactor";
    this.panel.setAttribute("aria-labelledby", `studio-tab-${tab}`);
    this.onTab(tab);
    this.charts = chartsFor(tab);
    this.hiddenSeries.clear();
    this.plots.replaceChildren();
    this.plots.classList.toggle("is-zones", tab === "zones");
    this.text("title", TABS.find(([id]) => id === tab)![1]);
    this.text("description", tab === "burnup" ? "Confirmed discharge readings appear after a fuel move. The shift record never decreases."
      : tab === "zones" ? "Each compartment's water level, plus the core average. Toggle traces below; dashed traces belong to End B."
      : "Published reactor measurements plotted against simulated elapsed time. Hover a graph or use the sample slider.");
    this.render(true);
  }

  public render(force = false): void {
    if (this.tab === "reactor" || !force && this.lastVersion === this.history.version) return;
    this.lastVersion = this.history.version;
    const all = this.history.samples;
    const end = all.at(-1)?.timeSeconds ?? 0;
    const points = all.filter(point => !this.windowSeconds || point.timeSeconds >= end - this.windowSeconds);
    let index = points.length - 1;
    const exactIndex = this.inspectionPoint ? points.indexOf(this.inspectionPoint) : -1;
    if (exactIndex >= 0) index = exactIndex;
    else if (this.inspectionTime !== null && points.length) {
      index = points.reduce((best, point, i) => Math.abs(point.timeSeconds - this.inspectionTime!) < Math.abs(points[best].timeSeconds - this.inspectionTime!) ? i : best, index);
    }
    this.inspector.max = String(Math.max(0, points.length - 1));
    this.inspector.value = String(Math.max(0, index)); this.inspector.disabled = points.length < 2;
    const selected = points[index];
    const readout = selected ? `${timeLabel(selected.timeSeconds)} since shift start${this.inspectionTime === null ? " · latest" : " · inspecting"}` : "Waiting for the first reactor snapshot";
    this.text("readout", readout); this.inspector.setAttribute("aria-valuetext", readout);
    this.text("note", `${points.length} samples shown · ${all.length} / ${HISTORY_CAPACITY} retained · History clears on New shift. Lines connect observed snapshots; time uses the simulation clock.`);
    this.charts.forEach((chart, chartIndex) => {
      let card = this.plots.children[chartIndex] as HTMLElement | undefined;
      if (!card) {
        card = document.createElement("article"); card.className = "studio-card studio-trend";
        card.innerHTML = `<div class="studio-card-heading"><h3>${chart.title}</h3><span class="studio-eyebrow">${chart.unit}</span></div><div class="studio-trend-plot"></div><div class="studio-trend-legend"></div>`;
        chart.series.forEach((trace, traceIndex) => {
          const button = document.createElement("button");
          button.dataset.trace = `${chartIndex}:${traceIndex}`; button.title = trace.name;
          button.innerHTML = `<i style="background:${trace.color}"></i><span></span>`;
          card!.querySelector(".studio-trend-legend")!.append(button);
        });
        this.plots.append(card);
      }
      patchMarkup(card.querySelector(".studio-trend-plot")!, this.drawChart(chart, chartIndex, points, selected));
      chart.series.forEach((trace, traceIndex) => {
        const button = card!.querySelector<HTMLButtonElement>(`[data-trace="${chartIndex}:${traceIndex}"]`)!;
        const visible = !this.hiddenSeries.has(button.dataset.trace!);
        button.setAttribute("aria-pressed", String(visible)); button.classList.toggle("is-muted", !visible);
        const value = selected ? trace.read(selected) : null;
        button.querySelector("span")!.textContent = `${trace.name}: ${value === null || !Number.isFinite(value) ? "—" : numberLabel(value, chart.unit)}`;
      });
    });
  }

  private drawChart(chart: Chart, chartIndex: number, points: readonly ReactorHistoryPoint[], selected?: ReactorHistoryPoint): string {
    const traces = chart.series.filter((_, index) => !this.hiddenSeries.has(`${chartIndex}:${index}`));
    const values = traces.flatMap(trace => points.map(trace.read)).filter((value): value is number => value !== null && Number.isFinite(value));
    if (chart.reference !== undefined) values.push(chart.reference);
    if (chart.zero) values.push(0);
    const minimum = Math.min(...values), maximum = Math.max(...values);
    const padding = Math.max((maximum - minimum) * .12, chart.unit === "Keff" ? .00001 : Math.abs(maximum) * .04, .000001);
    const [low, high] = chart.domain ?? (values.length ? [chart.zero ? 0 : minimum - padding, maximum + padding] : [0, 1]);
    const start = points[0]?.timeSeconds ?? 0, end = points.at(-1)?.timeSeconds ?? start;
    const x = (time: number) => 74 + (end === start ? .5 : (time - start) / (end - start)) * 802;
    const y = (value: number) => 210 - (value - low) / (high - low) * 184;
    const grid = Array.from({ length: 5 }, (_, i) => {
      const value = low + (high - low) * i / 4, position = y(value);
      return `<path d="M74 ${position}H876" class="trend-gridline"/><text x="64" y="${position + 4}" text-anchor="end">${numberLabel(value, chart.unit)}</text>`;
    }).join("");
    const times = Array.from({ length: 5 }, (_, i) => `<text x="${74 + 802 * i / 4}" y="236" text-anchor="middle">${timeLabel(start + (end - start) * i / 4)}</text>`).join("");
    const paths = traces.map(trace => {
      let path = "", inSegment = false, previousValue: number | null = null;
      for (const point of displaySamples(points, trace.read, point => point.timeSeconds, !!trace.step)) {
        const value = trace.read(point);
        if (value === null || !Number.isFinite(value)) { inSegment = false; continue; }
        path += !inSegment ? `M${x(point.timeSeconds)} ${y(value)}` : trace.step
          ? `H${x(point.timeSeconds)}V${y(value)}` : `L${x(point.timeSeconds)} ${y(value)}`;
        inSegment = true; previousValue = value;
      }
      const last = points.at(-1);
      return `<path d="${path}" fill="none" stroke="${trace.color}" stroke-width="2" ${trace.dashed ? 'stroke-dasharray="6 4"' : ""}/>${last && previousValue !== null ? `<circle cx="${x(last.timeSeconds)}" cy="${y(previousValue)}" r="3" fill="${trace.color}"/>` : ""}`;
    }).join("");
    const reference = chart.reference === undefined ? "" : `<path d="M74 ${y(chart.reference)}H876" stroke="#83a98a" stroke-dasharray="3 5"/>`;
    const cursor = selected ? `<path d="M${x(selected.timeSeconds)} 26V210" stroke="#ffc56c" opacity=".6"/>` : "";
    const noData = !traces.some(trace => points.some(point => trace.read(point) !== null))
      ? `<text x="475" y="120" text-anchor="middle">${this.tab === "burnup" ? "No discharged fuel yet — refuel a channel to begin." : "No recorded readings for the selected traces."}</text>` : "";
    return `<svg viewBox="0 0 900 252" role="img" aria-label="${chart.title} over simulated time; ${chart.unit}"><title>${chart.title}</title><desc>Time runs left to right. Read exact values with the sample slider and legend buttons.</desc>${grid}${times}${reference}${paths}${cursor}${noData}</svg>`;
  }

  public destroy(): void {
    this.element.removeEventListener("click", this.onClick); this.element.removeEventListener("keydown", this.onKeyDown);
    this.element.removeEventListener("change", this.onWindow); this.inspector.removeEventListener("input", this.onInspect);
    this.plots.removeEventListener("pointermove", this.onPointer); this.element.remove();
  }
  private text(field: string, value: string): void { this.element.querySelector(`[data-history="${field}"]`)!.textContent = value; }
  private readonly onClick = (event: MouseEvent): void => {
    const button = (event.target as Element).closest<HTMLButtonElement>("button");
    if (button?.dataset.tab) this.setTab(button.dataset.tab as StudioTab);
    else if (button?.dataset.trace) {
      const key = button.dataset.trace;
      if (this.hiddenSeries.has(key)) this.hiddenSeries.delete(key); else this.hiddenSeries.add(key);
      this.render(true);
    } else if (button?.dataset.history === "live") { this.inspectionTime = null; this.inspectionPoint = null; this.render(true); }
  };
  private readonly onKeyDown = (event: KeyboardEvent): void => {
    const target = event.target as HTMLElement;
    if (!target.hasAttribute("data-tab")) return;
    const index = TABS.findIndex(([tab]) => tab === this.tab);
    const next = ({ ArrowRight: (index + 1) % TABS.length, ArrowLeft: (index + TABS.length - 1) % TABS.length, Home: 0, End: TABS.length - 1 } as Record<string, number>)[event.key];
    if (next === undefined) return;
    event.preventDefault(); event.stopPropagation(); this.setTab(TABS[next][0]);
    this.element.querySelector<HTMLButtonElement>(`[data-tab="${this.tab}"]`)!.focus();
  };
  private readonly onWindow = (event: Event): void => {
    if ((event.target as HTMLElement).dataset.history !== "window") return;
    this.windowSeconds = Number((event.target as HTMLSelectElement).value);
    this.inspectionTime = null; this.inspectionPoint = null; this.render(true);
  };
  private readonly onInspect = (): void => {
    const end = this.history.samples.at(-1)?.timeSeconds ?? 0;
    const points = this.history.samples.filter(point => !this.windowSeconds || point.timeSeconds >= end - this.windowSeconds);
    this.inspectionPoint = points[Number(this.inspector.value)] ?? null;
    this.inspectionTime = this.inspectionPoint?.timeSeconds ?? null; this.render(true);
  };
  private readonly onPointer = (event: PointerEvent): void => {
    const svg = (event.target as Element).closest("svg");
    if (!svg || !this.history.samples.length) return;
    const bounds = svg.getBoundingClientRect();
    const end = this.history.samples.at(-1)!.timeSeconds;
    const start = this.history.samples.find(point => !this.windowSeconds || point.timeSeconds >= end - this.windowSeconds)!.timeSeconds;
    const fraction = Math.max(0, Math.min(1, ((event.clientX - bounds.left) / bounds.width * 900 - 74) / 802));
    this.inspectionPoint = null; this.inspectionTime = start + fraction * (end - start); this.render(true);
  };
}
