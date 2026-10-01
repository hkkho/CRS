import type { CanduSnapshot, ZoneNodeBinding } from "./protocol";

/** Geometry comes exclusively from the shared simulation snapshot. */
export function zoneBindings(snapshot: CanduSnapshot): ZoneNodeBinding[] | null {
  const nodes: ZoneNodeBinding[] = [];
  for (const channel of snapshot.core.channels) for (const bundle of channel.bundles) {
    if (bundle.logicalZoneId === undefined || bundle.absorberZoneId === undefined ||
        bundle.group1AbsorptionPerMPerFillFraction === undefined || bundle.group2AbsorptionPerMPerFillFraction === undefined) return null;
    nodes.push({ channelIndex: channel.channelIndex, position: bundle.position,
      logicalZoneId: bundle.logicalZoneId, absorberZoneId: bundle.absorberZoneId,
      group1AbsorptionPerMPerFillFraction: bundle.group1AbsorptionPerMPerFillFraction,
      group2AbsorptionPerMPerFillFraction: bundle.group2AbsorptionPerMPerFillFraction });
  }
  return nodes;
}

export function isAbsorbing(node: ZoneNodeBinding): boolean {
  return node.group1AbsorptionPerMPerFillFraction > 0 || node.group2AbsorptionPerMPerFillFraction > 0;
}

/** Moves a draft mask without calculating physics or altering regional ownership. */
export function moveAbsorberMask(nodes: ZoneNodeBinding[], snapshot: CanduSnapshot, zone: number,
  dx: number, dy: number, dz: number): ZoneNodeBinding[] {
  if (![dx, dy, dz].every(Number.isInteger)) throw new Error("Move offsets must be whole cells.");
  const channels = new Map(snapshot.core.channels.map(c => [c.channelIndex, c]));
  const byCoordinate = new Map(snapshot.core.channels.map(c => [`${c.gridColumn},${c.gridRow}`, c.channelIndex]));
  const byKey = new Map(nodes.map(n => [`${n.channelIndex},${n.position}`, n]));
  const moves = nodes.filter(n => n.absorberZoneId === zone && isAbsorbing(n)).map(source => {
    const channel = channels.get(source.channelIndex)!;
    const targetChannel = byCoordinate.get(`${channel.gridColumn + dx},${channel.gridRow + dy}`);
    const target = targetChannel === undefined ? undefined : byKey.get(`${targetChannel},${source.position + dz}`);
    if (!target) throw new Error("Move would place absorber cells outside the core. Draft retained.");
    if (isAbsorbing(target) && target.absorberZoneId !== zone) throw new Error("Move overlaps another absorber mask. Clear those cells first.");
    return { source, target };
  });
  if (!moves.length) throw new Error("Selected compartment has no absorbing cells to move.");
  const updated = new Map(nodes.map(n => [`${n.channelIndex},${n.position}`, { ...n }]));
  for (const { source } of moves) {
    const n = updated.get(`${source.channelIndex},${source.position}`)!;
    n.group1AbsorptionPerMPerFillFraction = n.group2AbsorptionPerMPerFillFraction = 0;
  }
  for (const { source, target } of moves) {
    const n = updated.get(`${target.channelIndex},${target.position}`)!;
    n.absorberZoneId = zone;
    n.group1AbsorptionPerMPerFillFraction = source.group1AbsorptionPerMPerFillFraction;
    n.group2AbsorptionPerMPerFillFraction = source.group2AbsorptionPerMPerFillFraction;
  }
  return [...updated.values()];
}
