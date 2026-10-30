import type { CanduSnapshot } from "./protocol";
import { channelWatts } from "./studio/powerReadings";

/** Display-only blend between known endpoints. Never submitted to the bridge. */
export function interpolateDemo(from: CanduSnapshot, to: CanduSnapshot, fraction: number): CanduSnapshot {
  if (fraction <= 0) return from;
  if (fraction >= 1) return to;
  const mix = (a: number, b: number) => a + (b - a) * fraction;
  const targets = new Map(to.core.channels.map(c => [c.channelIndex, c]));
  const channels = from.core.channels.map(c => ({ ...c,
    powerWatts: mix(channelWatts(from, c), channelWatts(to, targets.get(c.channelIndex) ?? c)),
  }));
  const powers = new Map(channels.map(c => [c.channelIndex, c.powerWatts]));
  return {
    ...from,
    // Discrete outcomes, inventory and terminal status appear only at the endpoint.
    core: { ...from.core, channels },
    ripple: from.ripple ? { ...from.ripple, channelRippleFractions: from.ripple.channelRippleFractions.map((r, i) => {
      const watts = powers.get(i);
      return watts !== undefined && from.ripple!.referenceChannelPowerWatts[i] > 0 ? watts / from.ripple!.referenceChannelPowerWatts[i] : r;
    }) } : undefined,
    rrs: { ...from.rrs,
      averageFillFraction: mix(from.rrs.averageFillFraction, to.rrs.averageFillFraction),
      zones: from.rrs.zones.map(z => ({ ...z, fillFraction: mix(z.fillFraction, to.rrs.zones.find(t => t.logicalZoneId === z.logicalZoneId)?.fillFraction ?? z.fillFraction) })),
    },
  };
}

export function demoPowerColor(watts: number): string {
  const fraction = Math.max(0, Math.min(1, (watts / 1000 - 5000) / 1500));
  // Saturated blue → cyan → green → yellow → red over the requested fixed range.
  return `hsl(${(220 * (1 - fraction)).toFixed(1)} 92% 48%)`;
}
export function demoPowerAlert(watts: number): "normal" | "flash" | "shake" {
  return watts > 7_000_000 ? "shake" : watts > 6_500_000 ? "flash" : "normal";
}
export function zoneAlert(fill: number): "normal" | "flash" | "shake" {
  return fill < .15 || fill > .85 ? "shake" : fill < .25 || fill > .75 ? "flash" : "normal";
}
