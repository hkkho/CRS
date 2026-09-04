import type { CanduChannelSnapshot, CanduSnapshot, RefuellingDirection } from "./protocol";

const HEAT_STOPS = [
  { at: 0, color: [20, 42, 59] },
  { at: 0.28, color: [38, 126, 133] },
  { at: 0.55, color: [73, 190, 165] },
  { at: 0.78, color: [240, 196, 105] },
  { at: 1, color: [248, 92, 105] },
] as const;

export function getHeatColor(powerFraction: number): string {
  const normalized = clamp((powerFraction - 0.68) / 0.58, 0, 1);
  for (let index = 1; index < HEAT_STOPS.length; index += 1) {
    const stop = HEAT_STOPS[index];
    if (normalized <= stop.at) {
      const previous = HEAT_STOPS[index - 1];
      const amount = (normalized - previous.at) / (stop.at - previous.at);
      const channels = previous.color.map((value, channelIndex) =>
        Math.round(value + (stop.color[channelIndex] - value) * amount),
      );
      return `rgb(${channels.join(", ")})`;
    }
  }
  const finalColor = HEAT_STOPS[HEAT_STOPS.length - 1].color;
  return `rgb(${finalColor.join(", ")})`;
}

export function getPowerLabel(powerFraction: number): string {
  return `${(powerFraction * 100).toFixed(1)}%`;
}

export function formatPowerWatts(powerWatts: number): string {
  const magnitude = Math.abs(powerWatts);
  if (magnitude >= 1e9) {
    return `${(powerWatts / 1e9).toFixed(3)} GW`;
  }
  if (magnitude >= 1e6) {
    return `${(powerWatts / 1e6).toFixed(1)} MW`;
  }
  if (magnitude >= 1e3) {
    return `${(powerWatts / 1e3).toFixed(1)} kW`;
  }
  return `${powerWatts.toFixed(0)} W`;
}

export function formatReactivity(reactivity: number): string {
  const milliK = reactivity * 1000;
  return `${milliK >= 0 ? "+" : ""}${milliK.toFixed(3)} mk`;
}

export function getTiltLabel(tiltFraction: number): string {
  return `${tiltFraction >= 0 ? "+" : ""}${(tiltFraction * 100).toFixed(2)}%`;
}

export function getFlowArrow(direction: RefuellingDirection): string {
  return direction === "toward-end-b" ? "→" : "←";
}

export function getFlowDirectionLabel(direction: RefuellingDirection): string {
  return direction === "toward-end-b" ? "END A → END B" : "END B → END A";
}

export function getChannelBand(channel: CanduChannelSnapshot): "low" | "nominal" | "high" {
  if (channel.localPowerFraction < 0.84) {
    return "low";
  }
  if (channel.localPowerFraction > 1.08) {
    return "high";
  }
  return "nominal";
}

export function getOverallStatus(snapshot: CanduSnapshot): "stable" | "watch" | "attention" {
  const powerError = Math.abs(snapshot.physics.actualPowerFraction - snapshot.targetPowerFraction);
  const tiltError = Math.abs(snapshot.absoluteTiltFraction - snapshot.targetTiltFraction);
  if (powerError > 0.06 || tiltError > 0.1 || snapshot.controlMarginFraction < 0.6) {
    return "attention";
  }
  if (powerError > 0.018 || tiltError > 0.045 || snapshot.controlMarginFraction < 0.72) {
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

export function formatSignedNumber(value: number, digits = 2): string {
  return `${value >= 0 ? "+" : ""}${value.toFixed(digits)}`;
}

function clamp(value: number, minimum: number, maximum: number): number {
  return Math.min(maximum, Math.max(minimum, value));
}
