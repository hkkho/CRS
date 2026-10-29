import { ReactorFrame } from "./ReactorFrame";
import { CoreMapTooltip, channelTooltip } from "./CoreMapInteraction";
import type { CanduSnapshot } from "./protocol";
import { gridCoordinateLabel } from "./channelCoordinates";
import { channelColorMinimum, channelLimit, channelWatts, powerColor, powerBand, burnupColor } from "./studio/powerReadings";

/** Spatial input only. Measurements and eligibility come from the shared snapshot. */
export class ArcadeMap {
  readonly element = document.createElement("div");
  private readonly cells = new Map<number, HTMLButtonElement>();
  private snapshot: CanduSnapshot;
  private selected: number;
  private locked = false;
  private readonly tooltip: CoreMapTooltip;
  private readonly frame: ReactorFrame;

  constructor(snapshot: CanduSnapshot, private readonly select: (index: number) => void, private readonly toggle: (index: number) => void = () => {}, private readonly instantFuel = false) {
    this.snapshot = snapshot;
    this.tooltip = new CoreMapTooltip(this.element, instantFuel);
    this.selected = snapshot.core.channels[0]?.channelIndex ?? 0;
    this.element.className = "arcade-map";
    this.element.setAttribute("role", "group");
    this.element.setAttribute("aria-label", "Core channels. Use arrow keys to inspect, Enter or R queues/removes. Double-click a channel to queue/remove.");
    if (instantFuel) this.element.setAttribute("aria-label", "Channel power map. Arrow keys select; Enter, Space, R or double-click fuels immediately.");
    const housing = document.createElementNS("http://www.w3.org/2000/svg", "svg");
    housing.setAttribute("viewBox", "0 0 560 560"); housing.setAttribute("aria-hidden", "true"); housing.classList.add("arcade-vessel-art");
    this.element.append(housing); this.frame = new ReactorFrame(housing, true);
    for (const channel of snapshot.core.channels) {
      const cell = document.createElement("button");
      cell.type = "button"; cell.dataset.channel = String(channel.channelIndex);
      cell.style.left = `${(channel.gridColumn + 1) / 24 * 100}%`;
      cell.style.top = `${(channel.gridRow + 1) / 24 * 100}%`;
      cell.textContent = gridCoordinateLabel(channel);
      this.cells.set(channel.channelIndex, cell); this.element.append(cell);
    }
    this.element.addEventListener("click", this.onClick);
    this.element.addEventListener("dblclick", this.onDoubleClick);
    this.element.addEventListener("keydown", this.onKey);
  }
  update(snapshot: CanduSnapshot, selected: number, planned: readonly number[], mode: "power" | "burnup", locked: boolean): void {
    this.frame.update(snapshot, selected);
    this.tooltip.update(snapshot);
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
        : burnupColor(channel.averageBurnupMwdPerKg);
      cell.dataset.alert = String(mode === "power" && powerBand(power, channelColorMinimum(snapshot), channelLimit(snapshot)) === "extreme");
      cell.setAttribute("aria-label", `${queued ? "In fuel plan. " : ""}${cell.dataset.alert === "true" ? "Very high power. " : ""}${channelTooltip(snapshot, channel)}`);
      if (this.instantFuel) {
        cell.textContent = (power / 1000).toFixed(0);
        cell.setAttribute("aria-label", `${gridCoordinateLabel(channel)} · ${(power / 1000).toFixed(0)} kW. ${queued ? "Fuel requested. " : ""}${channel.canRefuel === false ? "Cannot refuel." : "Double-click or press Enter to fuel."}`);
      }
    }
  }
  focusSelected(): void { this.cells.get(this.selected)?.focus({ preventScroll: true }); }
  private readonly onClick = (event: MouseEvent): void => {
    const cell = (event.target as Element).closest<HTMLButtonElement>("[data-channel]");
    if (cell && !this.locked) this.select(Number(cell.dataset.channel));
  };
  private readonly onDoubleClick = (event: MouseEvent): void => {
    const cell = (event.target as Element).closest<HTMLButtonElement>("[data-channel]");
    if (cell && !this.locked) { event.preventDefault(); this.toggle(Number(cell.dataset.channel)); }
  };
  private readonly onKey = (event: KeyboardEvent): void => {
    if (!this.locked && ["Enter", " ", "r", "R"].includes(event.key)) {
      event.preventDefault(); if (!event.repeat) this.toggle(this.selected); return;
    }
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
  destroy(): void { this.frame.destroy(); this.tooltip.destroy(); this.element.removeEventListener("click", this.onClick); this.element.removeEventListener("dblclick", this.onDoubleClick); this.element.removeEventListener("keydown", this.onKey); }
}
