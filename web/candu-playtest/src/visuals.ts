import type { CanduSnapshot } from "./protocol";

export function getPowerLabel(powerFraction: number): string {
  return `${(powerFraction * 100).toFixed(1)}%`;
}

export function formatEffectiveK(effectiveK: number): string {
  return Number.isFinite(effectiveK) ? effectiveK.toFixed(6) : "—";
}

export function formatLiquidZoneRegion(logicalZoneId: number): string {
  const regions = ["lower left", "upper left", "lower centre", "centre", "upper centre", "lower right", "upper right"];
  return `Z${logicalZoneId + 1} · End ${logicalZoneId < 7 ? "A" : "B"} ${regions[logicalZoneId % 7] ?? "unknown"}`;
}

export function getTiltLabel(tiltFraction: number): string {
  const roundedPercent = Number((tiltFraction * 100).toFixed(2));
  return `${roundedPercent >= 0 ? "+" : ""}${roundedPercent.toFixed(2)}%`;
}

export function getOverallStatus(snapshot: CanduSnapshot): "stable" | "watch" | "attention" {
  const powerError = Math.abs(snapshot.physics.actualPowerFraction - snapshot.targetPowerFraction);
  const tiltError = Math.abs(snapshot.axialTiltFraction);
  const level = snapshot.rrs.averageFillFraction;
  if (powerError > 0.06 || tiltError > 0.1 || level < 0.2 || level > 0.8) {
    return "attention";
  }
  if (powerError > 0.018 || tiltError > 0.045 || level < 0.3 || level > 0.7) {
    return "watch";
  }
  return "stable";
}

export function formatSimulationTime(seconds: number): string {
  const totalHours = Math.max(0, seconds) / 3600;
  const day = Math.floor(totalHours / 24) + 1;
  const hour = Math.floor(totalHours % 24);
  const minute = Math.floor((totalHours * 60) % 60);
  return `D${String(day).padStart(2, "0")} · ${String(hour).padStart(2, "0")}:${String(minute).padStart(2, "0")}`;
}

export function formatClockDuration(seconds: number): string {
  const safeSeconds = Math.max(0, Math.floor(seconds));
  const hours = Math.floor(safeSeconds / 3600);
  const minutes = Math.floor((safeSeconds % 3600) / 60);
  return `${String(hours).padStart(2, "0")}h ${String(minutes).padStart(2, "0")}m`;
}
