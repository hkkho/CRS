export interface PaceReading {
  requested: string;
  simulatedMinutesPerSecond: number | null;
  solving: boolean;
}
/** Presentation measurement only: authoritative time divided by monotonic wall
 * time. Includes solver waits; never feeds elapsed time back into simulation. */
export class ObservedPace {
  private key = '';
  private points: { wall: number; simulation: number }[] = [];
  public observe(key: string | null, simulation: number, wall: number): number | null {
    if (key === null) { this.key = ''; this.points = []; return null; }
    let previous = this.points.at(-1);
    if (this.key !== key || previous && (simulation < previous.simulation || wall < previous.wall)) {
      this.key = key; this.points = [];
      previous = undefined;
    }
    if (!previous || previous.wall !== wall || previous.simulation !== simulation) this.points.push({ wall, simulation });
    while (this.points.length > 120 || this.points.length > 2 && wall - this.points[1].wall > 15000) this.points.shift();
    const first = this.points[0];
    return wall - first.wall < 500 ? null : (simulation - first.simulation) / 60 / ((wall - first.wall) / 1000);
  }
}
