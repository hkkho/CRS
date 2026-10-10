// Presentation ranges from the seed-1001 daily benchmarks; v8 axial Marshak.
// These affect visual contrast only; operating limits come from the snapshot.
export const CHANNEL_COLOR_MIN_WATTS = 4_500_000;
export const BUNDLE_COLOR_MIN_WATTS = 400_000;
export const CHANNEL_PEAK_RANGE: [number, number] = [5.7, 6.2]; // MW
export const BUNDLE_PEAK_RANGE: [number, number] = [730, 830]; // kW
export const ZONE_LEVEL_RANGE: [number, number] = [45, 55]; // percent
export const AXIAL_POWER_RANGE: [number, number] = [100, 800]; // kW

/** Keep a useful initial view, then expand to show every finite reading. */
export function readingRange(values: readonly number[], preferred?: readonly [number, number],
  zero = false, minimumSpan = .01): [number, number] {
  const finite = values.filter(Number.isFinite);
  if (!finite.length) return preferred ? [...preferred] : [0, 1];
  const minimum = Math.min(...finite), maximum = Math.max(...finite);
  const padding = Math.max((maximum - minimum) * .12, minimumSpan / 2);
  let low = preferred ? Math.min(preferred[0], minimum - padding) : minimum - padding;
  let high = preferred ? Math.max(preferred[1], maximum + padding) : maximum + padding;
  if (preferred && preferred[0] >= 0 && minimum >= 0) low = Math.max(0, low);
  if (zero) { low = Math.min(0, minimum); high = Math.max(high, minimumSpan); }
  return [low, high];
}
