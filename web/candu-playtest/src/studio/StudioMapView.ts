import type { CanduChannelSnapshot, CanduSnapshot } from "../protocol";
import { CANDU6_ROW_LABELS, gridCoordinateLabel } from "../projection";
import { bundleLimit, channelLimit, channelWatts, powerColor, rippleColor, type MapMode } from "./powerReadings";
import { setAttribute } from "./domPatch";

export class StudioMapView {
  private readonly channelCells = new Map<number, SVGRectElement>();
  constructor(private readonly field: (name: string) => HTMLElement, channels: CanduChannelSnapshot[]) { this.build(channels); }
  focus(index: number): void { this.channelCells.get(index)?.focus({ preventScroll: true }); }
  update(snapshot: CanduSnapshot, selected: number, mode: MapMode): void {
    for (const current of snapshot.core.channels) {
      const circle = this.channelCells.get(current.channelIndex);
      if (!circle) continue;
      const watts = channelWatts(snapshot, current);
      const reference = snapshot.ripple?.referenceChannelPowerWatts[current.channelIndex];
      const ratio = snapshot.ripple?.channelRippleFractions[current.channelIndex];
      const peak = Math.max(0, ...current.bundles.filter(b => b.hasFuel).map(b => b.powerWatts));
      const amount = current.averageBurnupMwdPerKg / 10;
      const fraction = Math.min(1, Math.max(0, amount));
      const color = mode === "power" ? powerColor(watts / channelLimit(snapshot))
        : mode === "bundle-power" ? powerColor(peak / bundleLimit(snapshot))
        : mode === "ripple" ? ratio !== undefined && reference !== undefined ? rippleColor(ratio, channelLimit(snapshot) / reference) : "#53665e"
        : `hsl(${135 - fraction * 90} 72% ${32 + fraction * 30}%)`;
      setAttribute(circle, "fill", color);
      setAttribute(circle, "stroke", current.channelIndex === selected ? "#e1ffb0" : "#102a1c");
      setAttribute(circle, "stroke-width", current.channelIndex === selected ? "3" : "0.7");
      setAttribute(circle, "tabindex", current.channelIndex === selected ? "0" : "-1");
      setAttribute(circle, "aria-pressed", String(current.channelIndex === selected));
      const label = `Channel ${current.channelIndex}, ${gridCoordinateLabel(current)}, burnup ${current.averageBurnupMwdPerKg.toFixed(1)} MWd/kg, power ${(watts / 1000).toFixed(0)} kW, ripple ${ratio === undefined ? "unavailable" : `${(ratio * 100).toFixed(2)}%`}, peak bundle ${(peak / 1000).toFixed(0)} kW`;
      setAttribute(circle, "aria-label", label); if (circle.firstElementChild!.textContent !== label) circle.firstElementChild!.textContent = label;
    }
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
