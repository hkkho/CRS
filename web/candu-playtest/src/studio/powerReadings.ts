import type { CanduChannelSnapshot, CanduSnapshot } from "../protocol";
import { CHANNEL_COLOR_MIN_WATTS, BUNDLE_COLOR_MIN_WATTS } from "./displayScales";
export type MapMode = "burnup" | "power" | "ripple" | "bundle-power";
// Legacy hosts have no published limits; these defaults affect presentation only.
export const channelLimit = (snapshot: CanduSnapshot): number => snapshot.ripple?.maximumChannelPowerWatts ?? 7_100_000;
export const bundleLimit = (snapshot: CanduSnapshot): number => snapshot.ripple?.maximumBundlePowerWatts ?? 900_000;
export const channelColorMinimum = (snapshot: CanduSnapshot): number => channelLimit(snapshot) > CHANNEL_COLOR_MIN_WATTS ? CHANNEL_COLOR_MIN_WATTS : channelLimit(snapshot) * .6;
export const bundleColorMinimum = (snapshot: CanduSnapshot): number => bundleLimit(snapshot) > BUNDLE_COLOR_MIN_WATTS ? BUNDLE_COLOR_MIN_WATTS : bundleLimit(snapshot) * .6;
export function channelWatts(snapshot: CanduSnapshot, channel: CanduChannelSnapshot): number {
  const reference = snapshot.ripple?.referenceChannelPowerWatts[channel.channelIndex];
  const ratio = snapshot.ripple?.channelRippleFractions[channel.channelIndex];
  return reference !== undefined && ratio !== undefined ? reference * ratio : channel.powerWatts;
}
export function powerColor(watts: number, minimumWatts: number, limitWatts: number): string {
  return bandColors[powerBand(watts, minimumWatts, limitWatts)];
}
export const bandColors = { low: "#263346", moderate: "#98602f", normal: "#f5a623", high: "#db3535", extreme: "#ff6b6b", unavailable: "#657080" };
export type ValueBand = keyof typeof bandColors;
// Display bands only: orange does not predict a safe move or change Game-owned limits.
export function powerBand(watts: number, minimumWatts: number, limitWatts: number): ValueBand {
  if (!Number.isFinite(watts)) return "unavailable";
  return watts <= minimumWatts ? "low" : watts < (minimumWatts + limitWatts * .9) / 2 ? "moderate" : watts < limitWatts * .9 ? "normal" : watts < limitWatts * .97 ? "high" : "extreme";
}
export function rippleColor(ratio: number): string {
  return !Number.isFinite(ratio) ? bandColors.unavailable : ratio < .9 ? bandColors.low : ratio < .95 ? "#587b9d" : ratio <= 1.05 ? bandColors.normal : ratio < 1.15 ? bandColors.high : bandColors.extreme;
}
export function burnupColor(value: number): string {
  return !Number.isFinite(value) ? bandColors.unavailable : value < 2 ? "#263346" : value < 4 ? "#587b9d" : value < 6 ? "#d6aa49" : value < 8 ? "#fff0a6" : "#ffffff";
}
export function mapLegend(snapshot: CanduSnapshot, mode: MapMode): string {
  if (mode === "burnup") return "Burnup bands: <2 · 2–4 · 4–6 · 6–8 · ≥8 MWd/kg (dark → white)";
  if (mode === "ripple") return "Ripple: <90% dark · 90–95% blue · 95–105% orange · >105–<115% red · ≥115% bright red";
  const limit = mode === "power" ? channelLimit(snapshot) : bundleLimit(snapshot);
  const minimum = mode === "power" ? channelColorMinimum(snapshot) : bundleColorMinimum(snapshot);
  const kw = (n: number) => (n / 1000).toLocaleString("en-US", { maximumFractionDigits: 2, useGrouping: false });
  return `Dark ≤${kw(minimum)} · Bronze <${kw((minimum + limit * .9) / 2)} · Orange <${kw(limit * .9)} · Red <${kw(limit * .97)} · Shaking bright red ≥${kw(limit * .97)} · Limit ${kw(limit)} kW / ${mode === "power" ? "channel" : "hottest bundle"}. Display bands, not a safety prediction.`;
}
