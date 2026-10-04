import type { CanduChannelSnapshot, CanduSnapshot } from "../protocol";
export type MapMode = "burnup" | "power" | "ripple" | "bundle-power";
// Legacy hosts have no published limits; these defaults affect presentation only.
export const channelLimit = (snapshot: CanduSnapshot): number => snapshot.ripple?.maximumChannelPowerWatts ?? 7_300_000;
export const bundleLimit = (snapshot: CanduSnapshot): number => snapshot.ripple?.maximumBundlePowerWatts ?? 935_000;
export function channelWatts(snapshot: CanduSnapshot, channel: CanduChannelSnapshot): number {
  const reference = snapshot.ripple?.referenceChannelPowerWatts[channel.channelIndex];
  const ratio = snapshot.ripple?.channelRippleFractions[channel.channelIndex];
  return reference !== undefined && ratio !== undefined ? reference * ratio : channel.powerWatts;
}
export function powerColor(fractionOfLimit: number): string {
  const fraction = Math.min(1, Math.max(0, fractionOfLimit));
  return `hsl(${220 * (1 - fraction)} 78% 55%)`;
}
export function rippleColor(ratio: number, limitRatio: number): string {
  // Neutral at the fixed reference. Red reaches this channel's absolute cap.
  if (ratio === 1) return "hsl(0 0% 90%)";
  if (ratio < 1) return `hsl(210 ${75 * (1 - Math.max(0, ratio))}% ${35 + 55 * Math.max(0, ratio)}%)`;
  const fraction = Math.min(1, (ratio - 1) / Math.max(.001, limitRatio - 1));
  return `hsl(0 ${80 * fraction}% ${90 - 40 * fraction}%)`;
}
