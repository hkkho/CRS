import type { CanduSnapshot } from "./protocol";
import { gridCoordinateLabel } from "./channelCoordinates";
import { channelColorMinimum, channelLimit, channelWatts, powerColor } from "./studio/powerReadings";

/** Spatial input only. Measurements and eligibility come from the shared snapshot. */
export class ArcadeMap {
  readonly element = document.createElement("div");
  private readonly cells = new Map<number, HTMLButtonElement>();
  private snapshot: CanduSnapshot;
  private selected: number;
  private locked = false;

  constructor(snapshot: CanduSnapshot, private readonly select: (index: number) => void) {
    this.snapshot = snapshot;
    this.selected = snapshot.core.channels[0]?.channelIndex ?? 0;
    this.element.className = "arcade-map";
    this.element.setAttribute("role", "group");
    this.element.setAttribute("aria-label", "Core channels. Use arrow keys to inspect, then Add to plan.");
    for (const channel of snapshot.core.channels) {
      const cell = document.createElement("button");
      cell.type = "button"; cell.dataset.channel = String(channel.channelIndex);
      cell.style.left = `${(channel.gridColumn + 1) / 24 * 100}%`;
      cell.style.top = `${(channel.gridRow + 1) / 24 * 100}%`;
      cell.textContent = gridCoordinateLabel(channel);
      this.cells.set(channel.channelIndex, cell); this.element.append(cell);
    }
    this.element.addEventListener("click", this.onClick);
    this.element.addEventListener("keydown", this.onKey);
  }
  update(snapshot: CanduSnapshot, selected: number, planned: readonly number[], mode: "power" | "burnup", locked: boolean): void {
    this.snapshot = snapshot; this.selected = selected; this.locked = locked;
    for (const channel of snapshot.core.channels) {
      const cell = this.cells.get(channel.channelIndex);
      if (!cell) continue;
      const queued = planned.includes(channel.channelIndex);
      cell.dataset.planned = String(queued);
      cell.setAttribute("aria-pressed", String(selected === channel.channelIndex));
      cell.tabIndex = selected === channel.channelIndex ? 0 : -1;
      cell.disabled = locked;
      const power = channelWatts(snapshot, channel);
      cell.style.background = mode === "power" ? powerColor(power, channelColorMinimum(snapshot), channelLimit(snapshot))
        : `hsl(${135 - Math.min(1, Math.max(0, channel.averageBurnupMwdPerKg / 10)) * 90} 72% 48%)`;
      cell.setAttribute("aria-label", `${gridCoordinateLabel(channel)}${queued ? ", in fuel plan" : ""}, ${(power / 1000).toFixed(0)} kW, ${channel.averageBurnupMwdPerKg.toFixed(1)} MWd/kg${channel.canRefuel === false ? ", cannot refuel" : ""}`);
      cell.title = cell.getAttribute("aria-label")!;
    }
  }
  focusSelected(): void { this.cells.get(this.selected)?.focus({ preventScroll: true }); }
  private readonly onClick = (event: MouseEvent): void => {
    const cell = (event.target as Element).closest<HTMLButtonElement>("[data-channel]");
    if (cell && !this.locked) this.select(Number(cell.dataset.channel));
  };
  private readonly onKey = (event: KeyboardEvent): void => {
    if (this.locked || !["ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown"].includes(event.key)) return;
    event.preventDefault();
    const current = this.snapshot.core.channels.find(c => c.channelIndex === this.selected);
    if (!current) return;
    const horizontal = event.key === "ArrowLeft" || event.key === "ArrowRight";
    const sign = event.key === "ArrowLeft" || event.key === "ArrowUp" ? -1 : 1;
    const candidates = this.snapshot.core.channels.filter(c => horizontal ? c.gridRow === current.gridRow && (c.gridColumn - current.gridColumn) * sign > 0
      : c.gridColumn === current.gridColumn && (c.gridRow - current.gridRow) * sign > 0);
    candidates.sort((a, b) => horizontal ? Math.abs(a.gridColumn - current.gridColumn) - Math.abs(b.gridColumn - current.gridColumn)
      : Math.abs(a.gridRow - current.gridRow) - Math.abs(b.gridRow - current.gridRow));
    if (candidates[0]) { this.select(candidates[0].channelIndex); this.cells.get(candidates[0].channelIndex)?.focus(); }
  };
  destroy(): void { this.element.removeEventListener("click", this.onClick); this.element.removeEventListener("keydown", this.onKey); }
}
