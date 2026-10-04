import { isRunTerminal } from "./protocol";
import type { CanduChannelSnapshot, CanduSnapshot } from "./protocol";
import { getPowerLabel, getTiltLabel } from "./visuals";

/** Inspection aid only: average burnup is neither residence age nor a predicted response. */
export function isChannelRefuellable(channel: CanduChannelSnapshot | undefined): boolean {
  if (!channel) return false;
  // Older fixtures lack Game eligibility: conservatively display fully fuelled channels only.
  return channel.canRefuel ?? (channel.bundles.length > 0 && channel.bundles.every(bundle => bundle.hasFuel));
}

export function channelHeadroom(snapshot: CanduSnapshot, channel: CanduChannelSnapshot): string {
  const ids = [...new Set(channel.bundles.map(b => b.absorberZoneId).filter((id): id is number => id !== undefined))].sort((a,b) => a-b);
  return ids.map(id => {
    const zone = snapshot.rrs.zones.find(z => z.logicalZoneId === id);
    return zone ? `Z${id + 1}: ${(zone.fillFraction * 100).toFixed(0)}% drain / ${((1-zone.fillFraction)*100).toFixed(0)}% fill room` : "";
  }).filter(Boolean).join(" · ");
}

export function highestBurnupChannel(channels: readonly CanduChannelSnapshot[]): number | null {
  const fuelled = channels.filter(channel => isChannelRefuellable(channel));
  fuelled.sort((a, b) => b.averageBurnupMwdPerKg - a.averageBurnupMwdPerKg || a.channelIndex - b.channelIndex);
  return fuelled[0]?.channelIndex ?? null;
}

export function refuelImpactText(before: CanduSnapshot, after: CanduSnapshot, channelIndex: number): string {
  const oldChannel = before.core.channels.find(channel => channel.channelIndex === channelIndex);
  const newChannel = after.core.channels.find(channel => channel.channelIndex === channelIndex);
  if (!oldChannel || !newChannel) return "Channel response unavailable.";
  const breakdown = after.lastRefuellingScore;
  const score = breakdown?.netPoints ?? after.scoreTotal - before.scoreTotal;
  return [
    `CH ${channelIndex} · EQUILIBRIUM RESPONSE · ${score >= 0 ? "+" : ""}${score.toFixed(1)} SCORE`,
    ...(before.ripple && after.ripple ? [
      `Channel ripple  ${(before.ripple.channelRippleFractions[channelIndex] * 100).toFixed(2)}% → ${(after.ripple.channelRippleFractions[channelIndex] * 100).toFixed(2)}% (target 100%)`,
      `RMS ripple  ${(before.ripple.rmsDeviationFraction * 100).toFixed(2)}% → ${(after.ripple.rmsDeviationFraction * 100).toFixed(2)}%`,
      `Ripple points/hour  ${before.ripple.pointsPerHour.toFixed(3)} → ${after.ripple.pointsPerHour.toFixed(3)}`,
    ] : breakdown ? [
      `Discharge reward  +${breakdown.dischargeReward.toFixed(1)}`,
      `Fresh fuel cost  −${breakdown.freshFuelCost.toFixed(1)}`,
    ] : []),
    `Local power  ${getPowerLabel(oldChannel.localPowerFraction)} → ${getPowerLabel(newChannel.localPowerFraction)}`,
    `Local tilt   ${getTiltLabel(oldChannel.localTiltFraction)} → ${getTiltLabel(newChannel.localTiltFraction)}`,
    ...(after.rrs.decisionExplanation ? [`RRS decision  ${after.rrs.decisionExplanation}`,
      `Regulating residual  ${after.rrs.controlledBaselineWeightedResidual.toExponential(2)} → ${after.rrs.combinedWeightedResidual.toExponential(2)} (this solve)`] : []),
    `LZC average level  ${(before.rrs.averageFillFraction * 100).toFixed(1)}% → ${(after.rrs.averageFillFraction * 100).toFixed(1)}%`,
    `Fresh fuel   ${before.freshBundlesAvailable} → ${after.freshBundlesAvailable} bundles`,
    `Core power   ${getPowerLabel(before.physics.actualPowerFraction)} → ${getPowerLabel(after.physics.actualPowerFraction)} (regulated)`,
  ].join("\n");
}

export function operationGuidance(snapshot: CanduSnapshot): string {
  if (isRunTerminal(snapshot)) return `${snapshot.runEndReason || snapshot.rrs.gameOverReason || "Run complete"} — start a new shift to try a different fuel strategy.`;
  if (snapshot.freshBundlesAvailable < 8) return "Fuel budget spent — keep running for ripple score, or start a new shift.";
  return "Inspect burnup → refuel 8 bundles with channel flow → compare the response. Keep LZC average level between 10% and 90%, global tilt within ±20%, channel power at or below 7,300 kW, and every bundle at or below 935 kW.";
}
