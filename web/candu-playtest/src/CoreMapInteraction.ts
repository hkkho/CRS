import type { CanduChannelSnapshot, CanduSnapshot } from "./protocol";
import { gridCoordinateLabel } from "./channelCoordinates";
import { channelWatts } from "./studio/powerReadings";
import "./coreMap.css";

/** Movement preview uses the published position map, never a local reactor forecast. */
export function exitBurnup(snapshot: CanduSnapshot, channel: CanduChannelSnapshot): number | null {
  if (channel.canRefuel === false) return null;
  const plan = snapshot.refuellingPlans?.find(p => p.shiftCount === 8 && p.directionId === channel.flowDirection);
  if (!plan || plan.dischargedPositions.length !== 8) return null;
  const values = plan.dischargedPositions.map(position => channel.bundles.find(b => b.position === position && b.hasFuel)?.currentBurnupMwdPerKg);
  return values.every((v): v is number => v !== undefined && Number.isFinite(v)) ? values.reduce((sum, v) => sum + v, 0) / values.length : null;
}
export function channelTooltip(snapshot: CanduSnapshot, channel: CanduChannelSnapshot): string {
  const outgoing = exitBurnup(snapshot, channel);
  const ratio = snapshot.ripple?.channelRippleFractions[channel.channelIndex];
  const peak = Math.max(0, ...channel.bundles.filter(b => b.hasFuel).map(b => b.powerWatts));
  return [`Channel ${gridCoordinateLabel(channel)}`,
    `Current power: ${(channelWatts(snapshot, channel) / 1000).toFixed(0)} kW`,
    `Peak bundle: ${(peak / 1000).toFixed(0)} kW`,
    `Ripple: ${ratio === undefined ? "unavailable" : `${(ratio * 100).toFixed(1)}% of reference`}`,
    `Average burnup: ${channel.averageBurnupMwdPerKg.toFixed(2)} MWd/kg`,
    `Estimated average exit burnup: ${outgoing === null ? "unavailable" : `${outgoing.toFixed(2)} MWd/kg`}`,
    channel.canRefuel === false ? channel.refuellingIneligibilityReason || "Cannot refuel this channel." : "Exit estimate: 8 outgoing bundles at current burnup; before advancing the day.",
    "Power shown is current, not a post-refuelling forecast."].join("\n");
}

let tooltipId = 0;
export class CoreMapTooltip {
  private readonly tip = document.createElement("div");
  private active: Element | null = null;
  private timer: ReturnType<typeof setTimeout> | undefined;
  private touchInput = false;
  private snapshot: CanduSnapshot | undefined;
  private readings?: ReadonlyMap<number, string>;
  constructor(private readonly map: Element, private readonly instantFuel = false) {
    this.tip.className = "core-channel-tooltip";
    this.tip.id = `core-channel-tooltip-${++tooltipId}`;
    this.tip.setAttribute("role", "tooltip"); this.tip.hidden = true;
    document.body.append(this.tip);
    map.addEventListener("pointerover", this.show);
    map.addEventListener("pointerdown", this.onPointerDown);
    map.addEventListener("focusin", this.show);
    map.addEventListener("pointerout", this.leave);
    map.addEventListener("focusout", this.leave);
    this.tip.addEventListener("pointerenter", this.keep);
    this.tip.addEventListener("pointerleave", this.leave);
    document.addEventListener("keydown", this.onKey);
    window.addEventListener("scroll", this.hide, true);
    window.addEventListener("resize", this.hide);
  }
  update(snapshot: CanduSnapshot, readings?: ReadonlyMap<number, string>): void { this.snapshot = snapshot; this.readings = readings; if (this.active) this.render(); }
  private readonly keep = (): void => { clearTimeout(this.timer); };
  private readonly show = (event: Event): void => {
    if (event.type === "pointerover") this.touchInput = (event as PointerEvent).pointerType === "touch";
    if (this.touchInput) return;
    const target = (event.target as Element).closest("[data-channel]");
    if (!target || !this.map.contains(target)) return;
    this.keep(); this.active?.removeAttribute("aria-describedby");
    this.active = target; this.render();
  };
  private render(): void {
    const channel = this.snapshot?.core.channels.find(c => String(c.channelIndex) === this.active?.getAttribute("data-channel"));
    if (!channel || !this.snapshot || !this.active) { this.hide(); return; }
    const outgoing = exitBurnup(this.snapshot, channel);
    this.tip.textContent = this.instantFuel
      ? `${gridCoordinateLabel(channel)} · ${this.readings?.get(channel.channelIndex) ?? `${(channelWatts(this.snapshot, channel) / 1000).toFixed(0)} kW`}\nDouble-click to queue fuel at the first opportunity`
      : `${gridCoordinateLabel(channel)} · ${(channelWatts(this.snapshot, channel) / 1000).toFixed(0)} kW\nExit burnup ${outgoing === null ? "unavailable" : `${outgoing.toFixed(2)} MWd/kg`}\nClick to inspect · Double-click to queue/remove`;
    this.active.setAttribute("aria-describedby", this.tip.id);
    this.tip.hidden = false;
    const cell = this.active.getBoundingClientRect();
    const size = this.tip.getBoundingClientRect();
    this.tip.style.left = `${Math.max(8, Math.min(cell.right + 12, window.innerWidth - size.width - 8))}px`;
    this.tip.style.top = `${Math.max(8, Math.min(cell.top, window.innerHeight - size.height - 8))}px`;
  }
  private readonly leave = (): void => { this.keep(); this.timer = setTimeout(this.hide, 140); };
  private readonly onPointerDown = (event: Event): void => { this.touchInput = (event as PointerEvent).pointerType === "touch"; if (this.touchInput) this.hide(); };
  private readonly onKey = (event: KeyboardEvent): void => { this.touchInput = false; if (event.key === "Escape" && !this.tip.hidden) { this.hide(); } };
  private readonly hide = (): void => { this.keep(); this.tip.hidden = true; this.active?.removeAttribute("aria-describedby"); this.active = null; };
  destroy(): void {
    this.hide(); this.tip.remove();
    this.map.removeEventListener("pointerover", this.show); this.map.removeEventListener("focusin", this.show);
    this.map.removeEventListener("pointerdown", this.onPointerDown);
    this.map.removeEventListener("pointerout", this.leave); this.map.removeEventListener("focusout", this.leave);
    this.tip.removeEventListener("pointerenter", this.keep); this.tip.removeEventListener("pointerleave", this.leave);
    document.removeEventListener("keydown", this.onKey); window.removeEventListener("scroll", this.hide, true); window.removeEventListener("resize", this.hide);
  }
}

/** Native scrolling supports touch panning; buttons also work with keyboard input. */
export class CoreMapZoom {
  private readonly controls = document.createElement("div");
  private readonly levels = [1, 1.5, 2, 3, 4];
  private level = 0;
  constructor(private readonly viewport: HTMLElement, private readonly canvas: HTMLElement | SVGElement) {
    viewport.classList.add("core-map-viewport"); canvas.classList.add("core-map-canvas");
    this.controls.className = "core-map-zoom";
    this.controls.setAttribute("role", "group"); this.controls.setAttribute("aria-label", "Core map zoom");
    this.controls.innerHTML = '<button type="button" data-zoom="out" aria-label="Zoom out">−</button><output aria-live="polite">100%</output><button type="button" data-zoom="in" aria-label="Zoom in">+</button><button type="button" data-zoom="fit">Fit core</button><small>Hover or focus for readings · scroll to pan</small>';
    viewport.before(this.controls); this.controls.addEventListener("click", this.onClick); this.render();
  }
  private readonly onClick = (event: MouseEvent): void => {
    const action = (event.target as Element).closest<HTMLButtonElement>("[data-zoom]")?.dataset.zoom;
    if (!action) return;
    this.level = action === "fit" ? 0 : Math.max(0, Math.min(this.levels.length - 1, this.level + (action === "in" ? 1 : -1)));
    this.render();
    const selected = this.canvas.querySelector('[data-channel][aria-pressed="true"]');
    if (selected && this.level) {
      const cell = selected.getBoundingClientRect(), port = this.viewport.getBoundingClientRect();
      this.viewport.scrollLeft += cell.left - port.left + cell.width / 2 - this.viewport.clientWidth / 2;
      this.viewport.scrollTop += cell.top - port.top + cell.height / 2 - this.viewport.clientHeight / 2;
    } else { this.viewport.scrollLeft = 0; this.viewport.scrollTop = 0; }
  };
  private render(): void {
    this.canvas.style.width = `${this.levels[this.level] * 100}%`;
    this.viewport.dataset.zoomed = String(this.level > 0);
    this.controls.querySelector("output")!.textContent = `${this.levels[this.level] * 100}%`;
    this.controls.querySelector<HTMLButtonElement>('[data-zoom="out"]')!.disabled = this.level === 0;
    this.controls.querySelector<HTMLButtonElement>('[data-zoom="in"]')!.disabled = this.level === this.levels.length - 1;
  }
  destroy(): void { this.controls.removeEventListener("click", this.onClick); this.controls.remove(); }
}
