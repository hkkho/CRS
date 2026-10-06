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
export function rippleColor(ratio: number): string {
  // Fixed 85%-115% scale, neutral at 100%; outliers clip to endpoint colors.
  if (ratio === 1) return "hsl(0 0% 90%)";
  const fraction = Math.min(1, Math.abs(ratio - 1) / (ratio < 1 ? 1 - .85 : 1.15 - 1));
  if (ratio < 1) return `hsl(210 ${75 * fraction}% ${90 - 55 * fraction}%)`;
  return `hsl(0 ${80 * fraction}% ${90 - 40 * fraction}%)`;
}
