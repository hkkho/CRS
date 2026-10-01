import type { CanduChannelSnapshot, CanduSnapshot } from "./protocol";
import { getPowerLabel, getTiltLabel } from "./visuals";

/** Inspection aid only: sort observed fuel age, never predict a reactor response. */
export function oldestFuelChannel(channels: readonly CanduChannelSnapshot[]): number | null {
  const fuelled = channels.filter(channel => channel.bundles.some(bundle => bundle.hasFuel));
  fuelled.sort((a, b) => b.averageBurnupMwdPerKg - a.averageBurnupMwdPerKg || a.channelIndex - b.channelIndex);
  return fuelled[0]?.channelIndex ?? null;
}

export function refuelImpactText(before: CanduSnapshot, after: CanduSnapshot, channelIndex: number): string {
  const oldChannel = before.core.channels.find(channel => channel.channelIndex === channelIndex);
  const newChannel = after.core.channels.find(channel => channel.channelIndex === channelIndex);
  if (!oldChannel || !newChannel) return "Channel response unavailable.";
  const score = after.scoreTotal - before.scoreTotal;
  return [
    `CH ${channelIndex} · EQUILIBRIUM RESPONSE · ${score >= 0 ? "+" : ""}${score.toFixed(1)} SCORE`,
    `Local power  ${getPowerLabel(oldChannel.localPowerFraction)} → ${getPowerLabel(newChannel.localPowerFraction)}`,
    `Local tilt   ${getTiltLabel(oldChannel.localTiltFraction)} → ${getTiltLabel(newChannel.localTiltFraction)}`,
    `RRS reserve  ${(before.rrsReserveFraction * 100).toFixed(1)}% → ${(after.rrsReserveFraction * 100).toFixed(1)}%`,
    `Fresh fuel   ${before.freshBundlesAvailable} → ${after.freshBundlesAvailable} bundles`,
    `Core power   ${getPowerLabel(before.physics.actualPowerFraction)} → ${getPowerLabel(after.physics.actualPowerFraction)} (regulated)`,
  ].join("\n");
}

export function operationGuidance(snapshot: CanduSnapshot): string {
  if (snapshot.rrs.isGameOver) return "Run complete — start a new shift to try a different fuel strategy.";
  if (snapshot.freshBundlesAvailable < 4) return "Fuel budget spent — keep running for operating score, or start a new shift.";
  return "Inspect older fuel → choose 4 or 8 bundles → refuel → compare the response. Keep RRS reserve away from zero.";
}
