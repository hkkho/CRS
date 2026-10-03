/** Display-only reduction. Full observations remain in ReactorHistory.
 * Keep bucket endpoints/extrema, gaps, equal-time discontinuities and every
 * step transition. Step series can intentionally exceed the display budget.
 */
export function displaySamples<T>(points: readonly T[], read: (point: T) => number | null,
  time: (point: T) => number, step: boolean, buckets = 200): readonly T[] {
  if (points.length <= buckets * 4) return points;
  const keep = new Set<number>([0, points.length - 1]);
  const size = Math.ceil(points.length / buckets);
  for (let start = 0; start < points.length; start += size) {
    const end = Math.min(points.length, start + size);
    let min = start, max = start;
    keep.add(start); keep.add(end - 1);
    for (let i = start; i < end; i++) {
      const value = read(points[i]);
      if (value !== null && Number.isFinite(value)) {
        if (!Number.isFinite(read(points[min])) || value < read(points[min])!) min = i;
        if (!Number.isFinite(read(points[max])) || value > read(points[max])!) max = i;
      }
      if (i && (value === null || !Number.isFinite(value) || read(points[i - 1]) === null ||
        !Number.isFinite(read(points[i - 1])) || time(points[i]) === time(points[i - 1]) ||
        step && value !== read(points[i - 1]))) { keep.add(i - 1); keep.add(i); }
    }
    keep.add(min); keep.add(max);
  }
  return [...keep].sort((a,b) => a-b).map(index => points[index]);
}
