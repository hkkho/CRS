import type { CanduChannelSnapshot, CanduSnapshot } from "./protocol";
import { channelLimit, channelWatts } from "./studio/powerReadings";

export type TimeMapMode = "power" | "ripple" | "burnup" | "inlet-zones";
const unavailable = "#657080";
type Stop = readonly [value: number, red: number, green: number, blue: number];

// Internal IDs follow the calibrated model. Display order follows the letter
// rows and numbered columns of CANTEACH TTR-570 Figure 5.14.
export const zoneDisplayOrder: readonly number[] = [1, 0, 4, 3, 2, 6, 5, 8, 7, 11, 10, 9, 13, 12];
export const timeZoneLabel = (logicalZoneId: number): string => `Z${zoneDisplayOrder.indexOf(logicalZoneId) + 1}`;

// Fixed across frames and seeds. Closely spaced stops give common readings more
// color space. See the v9 benchmark notes for the sampled operating ranges.
const scales: Record<TimeMapMode, { unit: string; stops: readonly Stop[] }> = {
  power: { unit: "kW", stops: [
    [3900, 35, 53, 116], [4500, 52, 100, 192], [5000, 36, 153, 195], [5500, 48, 199, 154],
    [6000, 185, 218, 88], [6450, 255, 176, 68], [7100, 240, 67, 83],
  ] },
  ripple: { unit: "%", stops: [
    [94, 43, 60, 128], [97, 56, 113, 201], [99, 104, 197, 224], [100, 221, 234, 222],
    [101, 249, 205, 120], [103, 241, 130, 67], [107, 170, 53, 92],
  ] },
  burnup: { unit: "MWd/kg", stops: [
    [0, 25, 35, 66], [1.5, 43, 74, 127], [2.3, 35, 125, 155], [3.3, 48, 173, 157],
    [4.4, 148, 201, 101], [5.4, 240, 216, 117], [7, 255, 242, 202],
  ] },
  "inlet-zones": { unit: "%", stops: [
    [0, 32, 48, 107], [35, 55, 102, 190], [42, 50, 167, 211], [50, 86, 208, 152],
    [57, 222, 220, 113], [65, 250, 159, 74], [100, 219, 62, 87],
  ] },
};

const rgb = (red: number, green: number, blue: number): string => `rgb(${red}, ${green}, ${blue})`;

function color(mode: TimeMapMode, value: number): string {
  if (!Number.isFinite(value)) return unavailable;
  const stops = scales[mode].stops;
  const upper = stops.findIndex(stop => value <= stop[0]);
  if (upper <= 0) {
    const [, red, green, blue] = upper === 0 ? stops[0] : stops[stops.length - 1];
    return rgb(red, green, blue);
  }
  const [low, r, g, b] = stops[upper - 1];
  const [high, red, green, blue] = stops[upper];
  const fraction = (value - low) / (high - low);
  const mix = (a: number, b: number) => Math.round(a + (b - a) * fraction);
  return rgb(mix(r, red), mix(g, green), mix(b, blue));
}

export function renderTimeMapScale(element: HTMLElement, mode: TimeMapMode): void {
  const { stops, unit } = scales[mode];
  element.style.setProperty("--map-scale", `linear-gradient(90deg, ${stops.map(([, r, g, b]) => rgb(r, g, b)).join(", ")})`);
  const ticks = stops.map(([value], i) => {
    const tick = document.createElement("span");
    tick.textContent = `${value.toLocaleString("en-US")}${unit === "%" ? "%" : ""}`;
    tick.style.left = `${i / (stops.length - 1) * 100}%`;
    return tick;
  });
  element.setAttribute("aria-label", `Color scale in ${unit}`);
  element.replaceChildren(...ticks);
}

export function channelZonePair(channel: CanduChannelSnapshot): [number | undefined, number | undefined] {
  // Use published measurement regions from the shared snapshot.
  const ids = [...new Set(channel.bundles.map(b => b.logicalZoneId).filter((id): id is number => id !== undefined))];
  const endA = ids.filter(id => id < 7);
  const endB = ids.filter(id => id >= 7);
  return [endA.length === 1 ? endA[0] : undefined, endB.length === 1 ? endB[0] : undefined];
}

export function refuellingZoneReadings(snapshot: CanduSnapshot): string[] {
  const step = snapshot.lastDayResult;
  const fills = step?.refuellingZoneFills;
  if (!fills) return [];
  const zones = new Set<number>();
  for (const { channelIndex } of step.movements) {
    const channel = snapshot.core.channels.find(c => c.channelIndex === channelIndex);
    if (!channel) continue;
    for (const zone of channelZonePair(channel)) if (zone !== undefined) zones.add(zone);
  }
  const percent = (fill: number) => `${(fill * 100).toFixed(1)}%`;
  return zoneDisplayOrder.filter(zone => zones.has(zone)).map(zone =>
    `${timeZoneLabel(zone)} ${[fills.before, fills.after, fills.end].map(phase => percent(phase[zone])).join(" → ")}`);
}

export function timeMapCell(snapshot: CanduSnapshot, channel: CanduChannelSnapshot, mode: TimeMapMode): { background: string; reading: string } {
  if (mode === "power") {
    const kw = channelWatts(snapshot, channel) / 1000;
    return { background: color(mode, kw), reading: Number.isFinite(kw) ? `${kw.toFixed(0)} kW` : "Power unavailable" };
  }
  if (mode === "burnup") {
    const burnup = channel.averageBurnupMwdPerKg;
    return { background: color(mode, burnup), reading: Number.isFinite(burnup) ? `Burnup ${burnup.toFixed(2)} MWd/kg` : "Burnup unavailable" };
  }
  if (mode === "ripple") {
    const percent = (snapshot.ripple?.channelRippleFractions[channel.channelIndex] ?? NaN) * 100;
    return { background: color(mode, percent), reading: Number.isFinite(percent) ? `Ripple ${percent.toFixed(1)}% of reference` : "Ripple unavailable" };
  }
  const plan = snapshot.refuellingPlans?.find(p => p.shiftCount === 8 && p.directionId === channel.flowDirection);
  if (!plan) return { background: unavailable, reading: "Inlet zone unavailable" };
  const [endA, endB] = channelZonePair(channel);
  const id = plan.incomingEnd === "A" ? endA : endB;
  const zone = snapshot.rrs.zones.find(z => z.logicalZoneId === id);
  if (!zone) return { background: unavailable, reading: "Inlet zone unavailable" };
  const percent = zone.fillFraction * 100;
  return { background: color(mode, percent), reading: `Inlet End ${plan.incomingEnd} · ${timeZoneLabel(zone.logicalZoneId)} · ${percent.toFixed(1)}% filled` };
}

export function timeMapLegend(snapshot: CanduSnapshot, mode: TimeMapMode): string {
  if (mode === "power") return `Channel power (kW) · Above 6,500 pulses · Above 7,000 shakes · Limit ${(channelLimit(snapshot) / 1000).toLocaleString("en-US")} kW`;
  if (mode === "inlet-zones") return "Inlet zone fill (%) · Six of eight fresh bundles go into this half · Grey = unavailable";
  if (mode === "ripple") return "Ripple · Percent of reference · 100% is the channel reference";
  return "Average channel burnup (MWd/kg)";
}
