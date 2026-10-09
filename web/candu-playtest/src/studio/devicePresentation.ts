import type { CanduAdjusterSnapshot, CanduChannelSnapshot, CanduSnapshot } from "../protocol";

export function adjusterSummary(snapshot: CanduSnapshot): string {
  const rods = snapshot.core.adjusters;
  return rods === undefined ? "Adjuster locations unavailable from this host."
    : rods.length === 0 ? "No inserted adjusters in this model."
    : `${rods.length} fixed adjusters fully inserted · ${new Set(rods.map(r => r.axialPosition)).size} axial planes. Amber lines overlap in the face view; the plan below separates them.`;
}

export function channelAdjusters(snapshot: CanduSnapshot, channel: CanduChannelSnapshot | undefined): CanduAdjusterSnapshot[] {
  return channel ? (snapshot.core.adjusters ?? []).filter(r => r.affectedChannels.includes(channel.channelIndex)) : [];
}

/** Axial coordinates are supplied by the shared model, never inferred from fuel age or power. */
export function adjusterPowerMarkers(snapshot: CanduSnapshot, channel: CanduChannelSnapshot | undefined): string {
  return channelAdjusters(snapshot, channel).map(rod => {
    const bands = rod.bundlePositions.map(position => `<rect data-adjuster-bundle="${position}" x="${42 + position * 24 - 12}" y="27" width="24" height="50" class="studio-adjuster-band"/>`).join("");
    return `<g data-axial-adjuster="${rod.id}"><title>Adjuster ${rod.id}, inserted; absorption overlaps bundle positions ${rod.bundlePositions.map(p => p + 1).join(", ")}</title>${bands}<path class="studio-adjuster-centre" d="M${42 + rod.axialPosition * 24} 27V77"/></g>`;
  }).join("");
}

export function channelDeviceNote(snapshot: CanduSnapshot, channel: CanduChannelSnapshot | undefined): string {
  const rods = channelAdjusters(snapshot, channel);
  const adjusters = snapshot.core.adjusters === undefined ? "Adjuster overlap unavailable."
    : rods.length ? `Inserted adjuster absorption overlaps positions ${[...new Set(rods.flatMap(r => r.bundlePositions))].sort((a, b) => a - b).map(p => p + 1).join(", ")} (amber bands).`
    : "This channel has no direct adjuster-cell overlap; neighbouring flux can still respond.";
  const zoneIds = [...new Set(channel?.bundles.map(b => b.absorberZoneId).filter((id): id is number => id !== undefined) ?? [])].sort((a, b) => a - b);
  const zones = zoneIds.map(id => {
    const zone = snapshot.rrs.zones.find(z => z.logicalZoneId === id);
    return `Z${id + 1}${zone ? ` ${(zone.fillFraction * 100).toFixed(1)}% filled` : ""}`;
  });
  const tubes = channel ? snapshot.core.liquidZoneTubes?.filter(t => t.affectedChannels.includes(channel.channelIndex)) : [];
  const model = snapshot.core.liquidZoneTubes === undefined
    ? "This host does not publish localized LZC geometry; individual tube locations are unavailable."
    : tubes?.length ? `LZC tube footprints overlap positions ${[...new Set(tubes.flatMap(t => t.bundlePositions))].sort((a, b) => a - b).map(p => p + 1).join(", ")} (cyan bands). Water rises from each compartment's bottom; local absorption follows the filled overlap.`
    : "No direct LZC tube overlap in this channel; neighbouring flux can still respond.";
  return `${adjusters} ${zones.length ? `LZC: ${zones.join(" / ")}. ` : ""}${model} Powers and device effects are averaged over fuel cells. Bundle power also depends on fuel history, xenon and neighbouring flux.`;
}

export function liquidZonePowerMarkers(snapshot: CanduSnapshot, channel: CanduChannelSnapshot | undefined): string {
  return (snapshot.core.liquidZoneTubes ?? []).filter(t => channel && t.affectedChannels.includes(channel.channelIndex)).map(t =>
    `<g data-axial-lzc="${t.zoneId}"><title>Z${t.zoneId + 1} tube footprint; positions ${t.bundlePositions.map(p => p + 1).join(", ")}; water fill ${((snapshot.rrs.zones.find(z => z.logicalZoneId === t.zoneId)?.fillFraction ?? 0) * 100).toFixed(1)}%</title>${t.bundlePositions.map(p => `<rect x="${42 + p * 24 - 12}" y="27" width="24" height="50" class="studio-lzc-band"/>`).join("")}</g>`
  ).join("");
}
