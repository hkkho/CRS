import type { CanduChannelSnapshot, CanduSnapshot } from "../protocol";
import { CANDU6_ROW_LABELS, gridCoordinateLabel } from "../projection";
import { bundleLimit, channelLimit, channelWatts, powerColor, rippleColor, type MapMode } from "./powerReadings";
import { setAttribute } from "./domPatch";
import { patchMarkup } from "./domPatch";

export class StudioMapView {
  private readonly channelCells = new Map<number, SVGRectElement>();
  private readonly devices = document.createElementNS("http://www.w3.org/2000/svg", "g");
  private deviceKey = "";
  private readonly tubes = document.createElementNS("http://www.w3.org/2000/svg", "g");
  private tubeKey = "";
  private plane = 0;
  setLiquidZonePlane(plane: number): void { this.plane = plane; }
  showAdjusters(visible: boolean): void { this.devices.style.display = visible ? "" : "none"; }
  constructor(private readonly field: (name: string) => HTMLElement, channels: CanduChannelSnapshot[]) { this.build(channels); }
  focus(index: number): void { this.channelCells.get(index)?.focus({ preventScroll: true }); }
  update(snapshot: CanduSnapshot, selected: number, mode: MapMode): void {
    this.updateDevices(snapshot);
    for (const current of snapshot.core.channels) {
      const circle = this.channelCells.get(current.channelIndex);
      if (!circle) continue;
      const watts = channelWatts(snapshot, current);
      const ratio = snapshot.ripple?.channelRippleFractions[current.channelIndex];
      const peak = Math.max(0, ...current.bundles.filter(b => b.hasFuel).map(b => b.powerWatts));
      const amount = current.averageBurnupMwdPerKg / 10;
      const fraction = Math.min(1, Math.max(0, amount));
      const color = mode === "power" ? powerColor(watts / channelLimit(snapshot))
        : mode === "bundle-power" ? powerColor(peak / bundleLimit(snapshot))
        : mode === "ripple" ? ratio !== undefined ? rippleColor(ratio) : "#53665e"
        : `hsl(${135 - fraction * 90} 72% ${32 + fraction * 30}%)`;
      setAttribute(circle, "fill", color);
      setAttribute(circle, "stroke", current.channelIndex === selected ? "#e1ffb0" : "#102a1c");
      setAttribute(circle, "stroke-width", current.channelIndex === selected ? "3" : "0.7");
      setAttribute(circle, "tabindex", current.channelIndex === selected ? "0" : "-1");
      setAttribute(circle, "aria-pressed", String(current.channelIndex === selected));
      const label = `Channel ${gridCoordinateLabel(current)}, burnup ${current.averageBurnupMwdPerKg.toFixed(1)} MWd/kg, power ${(watts / 1000).toFixed(0)} kW, ripple ${ratio === undefined ? "unavailable" : `${(ratio * 100).toFixed(2)}%`}, peak bundle ${(peak / 1000).toFixed(0)} kW`;
      setAttribute(circle, "aria-label", label); if (circle.firstElementChild!.textContent !== label) circle.firstElementChild!.textContent = label;
    }
  }
  private updateDevices(snapshot: CanduSnapshot): void {
    const rods = snapshot.core.adjusters ?? [];
    const tubes = snapshot.core.liquidZoneTubes ?? [];
    const planes = [...new Set(tubes.map(t => t.axialPosition))].sort((a, b) => a - b);
    const tubeKey = JSON.stringify([tubes, snapshot.rrs.zones.map(z => z.fillFraction), this.plane]);
    if (tubeKey !== this.tubeKey) {
      this.tubeKey = tubeKey;
      this.tubes.replaceChildren();
      this.tubes.setAttribute("class", "studio-lzc-face");
      this.tubes.setAttribute("aria-hidden", "true");
      if (!this.tubes.parentNode) this.field("map").append(this.tubes);
      for (const tube of tubes.filter(t => t.axialPosition === planes[this.plane])) {
        const fill = snapshot.rrs.zones.find(z => z.logicalZoneId === tube.zoneId)?.fillFraction ?? 0;
        const x = 70 + tube.gridColumn * 20, y = 70 + tube.gridRowStart * 20;
        const height = (tube.gridRowEnd - tube.gridRowStart) * 20;
        const outline = document.createElementNS("http://www.w3.org/2000/svg", "rect");
        for (const [key, value] of Object.entries({ x: x - 3, y, width: 6, height })) outline.setAttribute(key, String(value));
        outline.setAttribute("class", "studio-lzc-outline"); outline.dataset.faceLzc = String(tube.zoneId);
        const water = document.createElementNS("http://www.w3.org/2000/svg", "rect");
        for (const [key, value] of Object.entries({ x: x - 2, y: y + height * (1 - fill), width: 4, height: height * fill })) water.setAttribute(key, String(value));
        water.setAttribute("class", "studio-lzc-water");
        const title = document.createElementNS("http://www.w3.org/2000/svg", "title");
        title.textContent = `Z${tube.zoneId + 1}, ${(fill * 100).toFixed(1)}% water; axial bundle coordinate ${(tube.axialPosition + 1).toFixed(1)}`;
        const label = document.createElementNS("http://www.w3.org/2000/svg", "text");
        label.setAttribute("x", String(x + 5)); label.setAttribute("y", String(y + 10));
        label.setAttribute("class", "studio-lzc-label"); label.textContent = `Z${tube.zoneId + 1}`;
        outline.append(title); this.tubes.append(outline, water, label);
      }
    }
    const key = JSON.stringify([rods, tubes]);
    if (key === this.deviceKey) return;
    this.deviceKey = key;
    this.devices.replaceChildren();
    this.devices.setAttribute("class", "studio-adjuster-face");
    this.devices.setAttribute("aria-hidden", "true");
    this.field("map").append(this.devices);
    for (const column of [...new Set(rods.map(r => r.gridColumn))]) {
      const projected = rods.filter(r => r.gridColumn === column);
      const line = document.createElementNS("http://www.w3.org/2000/svg", "path");
      line.setAttribute("d", `M${70 + column * 20} ${70 + projected[0].gridRowStart * 20}V${70 + projected[0].gridRowEnd * 20}`);
      line.setAttribute("class", "studio-adjuster-centre");
      line.dataset.adjusterColumn = String(column);
      const title = document.createElementNS("http://www.w3.org/2000/svg", "title");
      title.textContent = `Inserted adjusters ${projected.map(r => r.id).join(", ")}; ${projected.length} axial planes overlap here`;
      line.append(title); this.devices.append(line);
    }
    const positions = [...new Set(rods.map(r => r.axialPosition))].sort((a, b) => a - b);
    const columns = [...new Set([...rods, ...tubes].map(r => r.gridColumn))].sort((a, b) => a - b);
    const dots = rods.map(r => `<circle data-plan-adjuster="${r.id}" cx="${42 + r.axialPosition * 24}" cy="${32 + columns.indexOf(r.gridColumn) * 10}" r="3"><title>Adjuster ${r.id}; horizontal column coordinate ${(r.gridColumn + 1).toFixed(1)}; axial bundle coordinate ${(r.axialPosition + 1).toFixed(1)}; fully inserted</title></circle>`).join("");
    const labels = positions.map(p => `<text x="${42 + p * 24}" y="108" text-anchor="middle">${(p + 1).toFixed(1)}</text>`).join("");
    const assemblies = tubes.filter((t, i) => tubes.findIndex(other => other.axialPosition === t.axialPosition && other.gridColumn === t.gridColumn) === i);
    const tubeDots = assemblies.map(t =>
      `<rect data-plan-lzc="${t.zoneId}" x="${42 + t.axialPosition * 24 - 3}" y="${32 + columns.indexOf(t.gridColumn) * 10 - 3}" width="6" height="6" class="studio-lzc-water"><title>LZC assembly at axial bundle coordinate ${(t.axialPosition + 1).toFixed(1)}</title></rect>`).join("");
    patchMarkup(this.field("device-plan"), rods.length || tubes.length ? `<svg class="studio-adjuster-plan" viewBox="0 0 324 132" role="img" aria-label="Plan view: ${rods.length} inserted adjusters in amber, ${assemblies.length} LZC assemblies in cyan"><title>Device plan · viewed from above</title><text x="12" y="14">PLAN / AXIAL BUNDLE COORDINATE</text>${dots}${tubeDots}${labels}${planes.map(p => `<text x="${42 + p * 24}" y="108" text-anchor="middle">${(p + 1).toFixed(1)}</text>`).join("")}<text x="12" y="126">END A → END B · TOP TO BOTTOM: LEFT → RIGHT</text></svg>` : "");
  }
  private build(channels: CanduChannelSnapshot[]): void {
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
    for (const channel of channels) {
      const cell = document.createElementNS(svgNamespace, "rect");
      cell.setAttribute("x", String(62 + channel.gridColumn * 20));
      cell.setAttribute("y", String(62 + channel.gridRow * 20));
      cell.setAttribute("width", "16"); cell.setAttribute("height", "16");
      cell.setAttribute("role", "button");
      cell.dataset.channel = String(channel.channelIndex);
      const title = document.createElementNS(svgNamespace, "title"); cell.append(title);
      this.field("channels").append(cell);
      this.channelCells.set(channel.channelIndex, cell);
    }
  }

}
