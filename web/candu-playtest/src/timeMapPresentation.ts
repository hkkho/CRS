import type { CanduChannelSnapshot, CanduSnapshot } from "./protocol";
import { demoPowerColor } from "./demoPresentation";
import { burnupColor, channelWatts, mapLegend, rippleColor } from "./studio/powerReadings";

export type TimeMapMode = "power" | "ripple" | "burnup" | "paired-zones";
const unavailable = "#657080";

export function timeMapCell(snapshot: CanduSnapshot, channel: CanduChannelSnapshot, mode: TimeMapMode): { background: string; reading: string } {
  if (mode === "power") return { background: demoPowerColor(channelWatts(snapshot, channel)), reading: `${(channelWatts(snapshot, channel) / 1000).toFixed(0)} kW` };
  if (mode === "burnup") return { background: burnupColor(channel.averageBurnupMwdPerKg), reading: `Burnup ${channel.averageBurnupMwdPerKg.toFixed(2)} MWd/kg` };
  if (mode === "ripple") {
    const ratio = snapshot.ripple?.channelRippleFractions[channel.channelIndex];
    return { background: rippleColor(ratio ?? NaN), reading: ratio === undefined ? "Ripple unavailable" : `Ripple ${(ratio * 100).toFixed(1)}% of reference` };
  }
  // Published measurement regions, not the localized absorber footprint or grid guesses.
  const ids = [...new Set(channel.bundles.map(b => b.logicalZoneId).filter((id): id is number => id !== undefined))];
  const half = (endA: boolean) => {
    const matches = ids.filter(id => endA ? id < 7 : id >= 7);
    const zone = matches.length === 1 ? snapshot.rrs.zones.find(z => z.logicalZoneId === matches[0]) : undefined;
    const fill = zone?.fillFraction;
    return fill === undefined || !Number.isFinite(fill)
      ? { color: unavailable, reading: `End ${endA ? "A" : "B"} fill unavailable` }
      : { color: `hsl(${(220 * (1 - Math.max(0, Math.min(1, fill)))).toFixed(1)} 80% 48%)`, reading: `End ${endA ? "A" : "B"} Z${zone!.logicalZoneId + 1}: ${(fill * 100).toFixed(1)}% filled` };
  };
  const a = half(true), b = half(false);
  return { background: `linear-gradient(to right, ${a.color} 0%, ${a.color} 50%, ${b.color} 50%, ${b.color} 100%)`, reading: `${a.reading} · ${b.reading}` };
}

export function timeMapLegend(snapshot: CanduSnapshot, mode: TimeMapMode): string {
  if (mode === "power") return `5,000 kW blue → 6,500 kW red · >6,500 pulses · >7,000 shakes · Limit ${((snapshot.ripple?.maximumChannelPowerWatts ?? 7_100_000) / 1000).toLocaleString("en-US")} kW`;
  if (mode === "paired-zones") return "Paired zone fills: left half = End A · right half = End B · 0% blue → 100% red · Grey = unavailable. Hover or focus for zone IDs and fill percentages.";
  return mapLegend(snapshot, mode);
}
