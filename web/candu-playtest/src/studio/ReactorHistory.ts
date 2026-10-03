import type { CanduSnapshot } from "../protocol";

export const HISTORY_CAPACITY = 4096;

export interface ReactorHistoryPoint {
  timeSeconds: number;
  maxChannelMw: number;
  maxBundleKw: number;
  thermalMw: number;
  electricalMw: number | null;
  lastDischargeBurnup: number | null;
  maximumDischargeBurnup: number | null;
  maximumFuelBurnup: number;
  meanFuelBurnup: number;
  zoneFills: readonly (number | null)[];
  meanZoneFill: number;
  axialTiltPercent: number;
  maxLocalTiltPercent: number;
  effectiveK: number;
  reactivityMk: number;
  coreReactivityMk: number;
  reservePercent: number;
  freshBundles: number;
  score: number;
  operations: number;
  meanIodine: number | null;
  meanXenon: number | null;
  zoneXenon: readonly (number | null)[];
}

/** Bounded observation history. Aggregates published measurements only. */
export class ReactorHistory {
  private readonly points: ReactorHistoryPoint[] = [];
  private lastSnapshot: CanduSnapshot | null = null;
  public version = 0;
  public get samples(): readonly ReactorHistoryPoint[] { return this.points; }

  public clear(): void {
    this.points.length = 0;
    this.lastSnapshot = null;
    this.version++;
  }

  public record(snapshot: CanduSnapshot): void {
    if (snapshot === this.lastSnapshot || snapshot.source !== "wasm" || !snapshot.physics.isAuthoritative) return;
    this.lastSnapshot = snapshot;
    const previous = this.points.at(-1);
    if (previous && (snapshot.simulationTimeSeconds < previous.timeSeconds ||
      snapshot.refuellingOperationCount < previous.operations)) this.clear();
    const channels = snapshot.core.channels;
    let maximumBundlePower = 0, maximumBurnup = 0, burnupSum = 0, fuelCount = 0;
    for (const channel of channels) for (const bundle of channel.bundles) {
      if (!bundle.hasFuel) continue;
      maximumBundlePower = Math.max(maximumBundlePower, bundle.powerWatts);
      maximumBurnup = Math.max(maximumBurnup, bundle.currentBurnupMwdPerKg);
      burnupSum += bundle.currentBurnupMwdPerKg;
      fuelCount++;
    }
    const zoneFills: (number | null)[] = Array(14).fill(null);
    for (const zone of snapshot.rrs.zones) zoneFills[zone.logicalZoneId] = zone.fillFraction * 100;
    const point: ReactorHistoryPoint = {
      timeSeconds: snapshot.simulationTimeSeconds,
      maxChannelMw: Math.max(0, ...channels.map(channel => channel.powerWatts)) / 1e6,
      maxBundleKw: maximumBundlePower / 1e3,
      thermalMw: snapshot.physics.totalPowerWatts / 1e6,
      electricalMw: snapshot.physics.electricalPowerWatts === undefined ? null : snapshot.physics.electricalPowerWatts / 1e6,
      lastDischargeBurnup: snapshot.lastDischargedMaximumBurnupMwdPerKg ?? null,
      maximumDischargeBurnup: snapshot.maximumDischargedBurnupMwdPerKg ?? null,
      maximumFuelBurnup: maximumBurnup,
      meanFuelBurnup: fuelCount ? burnupSum / fuelCount : 0,
      zoneFills,
      meanZoneFill: snapshot.rrs.averageFillFraction * 100,
      axialTiltPercent: snapshot.axialTiltFraction * 100,
      maxLocalTiltPercent: Math.max(0, ...channels.map(channel => Math.abs(channel.localTiltFraction))) * 100,
      effectiveK: snapshot.physics.effectiveK,
      reactivityMk: snapshot.physics.reactivity * 1000,
      coreReactivityMk: snapshot.rrs.coreReactivity * 1000,
      reservePercent: snapshot.rrsReserveFraction * 100,
      freshBundles: snapshot.freshBundlesAvailable,
      score: snapshot.scoreTotal,
      operations: snapshot.refuellingOperationCount,
      meanIodine: snapshot.xenon?.hasCoupling ? snapshot.xenon.meanI135NumberDensityM3 / 1e20 : null,
      meanXenon: snapshot.xenon?.hasCoupling ? snapshot.xenon.meanXe135NumberDensityM3 / 1e20 : null,
      zoneXenon: Array.from({ length: 14 }, (_, zone) => {
        const value = snapshot.rrs.zones.find(z => z.logicalZoneId === zone)?.meanXe135NumberDensityM3;
        return value === undefined ? null : value / 1e20;
      }),
    };
    const last = this.points.at(-1);
    // Pending/status emissions and rejected commands do not add samples.
    if (last && Object.keys(point).every(key => {
      const field = key as keyof ReactorHistoryPoint;
      return field === "zoneFills" || field === "zoneXenon"
        ? point[field].every((value, i) => value === last[field][i]) : point[field] === last[field];
    })) return;
    this.points.push(point);
    if (this.points.length > HISTORY_CAPACITY) this.points.shift();
    this.version++;
  }
}
