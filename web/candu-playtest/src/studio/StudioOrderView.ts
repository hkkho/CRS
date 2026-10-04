import type { CanduChannelSnapshot, CanduSnapshot } from "../protocol";
import type { RefuelDraft } from "../commandState";
import { patchMarkup } from "./domPatch";
import { bundleLimit } from "./powerReadings";

export class StudioOrderView {
  private bundlesKey = "";
  private movementKey = "";
  private resultKey = "";
  constructor(private readonly field: (name: string) => HTMLElement) {}
  update(snapshot: CanduSnapshot, draft: RefuelDraft | null, channel: CanduChannelSnapshot | undefined): void {
    this.renderBundles(snapshot, channel); this.renderMovement(snapshot, draft, channel);
  }
  private renderBundles(snapshot: CanduSnapshot, channel: CanduChannelSnapshot | undefined): void {
    const offset = (channel?.channelIndex ?? 0) * 12;
    const iodine = snapshot.xenon?.nodeI135NumberDensityM3?.slice(offset, offset + 12);
    const xenon = snapshot.xenon?.nodeXe135NumberDensityM3?.slice(offset, offset + 12);
    const key = JSON.stringify([channel?.bundles, iodine, xenon, bundleLimit(snapshot)]);
    if (key === this.bundlesKey) return;
    this.bundlesKey = key;
    patchMarkup(this.field("bundles"), [
      axialLineGraph(channel, "power", "POWER / kW THERMAL", bundle => bundle.powerWatts / 1000, bundleLimit(snapshot) / 1000),
      axialLineGraph(channel, "burnup", "BURNUP / MWd/kg HM", bundle => bundle.currentBurnupMwdPerKg),
      axialLineGraph(channel, "iodine", "IODINE-135 / 10²⁰ atoms/m³", bundle => iodine?.[bundle.position] === undefined ? null : iodine[bundle.position] / 1e20),
      axialLineGraph(channel, "xenon", "XENON-135 / 10²⁰ atoms/m³", bundle => xenon?.[bundle.position] === undefined ? null : xenon[bundle.position] / 1e20),
      '<p class="studio-description">Fresh bundles enter with zero iodine and xenon. Both build up as simulation time advances. The four retained bundles keep their inventories.</p>',
    ].join(""));
  }

  private renderMovement(snapshot: CanduSnapshot, draft: RefuelDraft | null, channel: CanduChannelSnapshot | undefined): void {
    const plan = snapshot.refuellingPlans?.find(p => p.directionId === draft?.directionId && p.shiftCount === draft?.shiftCount);
    const key = JSON.stringify([plan, channel?.channelIndex, channel?.bundles]);
    if (key !== this.movementKey) {
      this.movementKey = key;
      const box = this.field("movement-plan");
      if (plan && channel) {
        // Keep the fuel strip stable as burnup readings change on clock ticks.
        if (!box.firstElementChild) box.innerHTML = '<p></p><div class="studio-fuel-strip"></div>';
        const label = box.firstElementChild!;
        label.textContent = `Fresh fuel enters End ${plan.incomingEnd}; positions ${plan.dischargedPositions.map(p => p + 1).join(", ")} leave End ${plan.outgoingEnd}.`;
        const strip = box.children[1];
        while (strip.children.length > channel.bundles.length) strip.lastElementChild!.remove();
        channel.bundles.forEach((bundle, index) => {
          if (!strip.children[index]) strip.append(document.createElement("span"));
          const cell = strip.children[index] as HTMLElement; const outgoing = plan.dischargedPositions.includes(bundle.position);
          cell.dataset.movement = outgoing ? "outgoing" : "retained";
          cell.textContent = `${bundle.position + 1}: ${bundle.currentBurnupMwdPerKg.toFixed(1)}`;
          cell.title = outgoing ? "Will be discharged · MWd/kg HM" : `Moves to position ${(plan.retainedToPositions[plan.retainedFromPositions.indexOf(bundle.position)] ?? bundle.position) + 1} · MWd/kg HM`;
        });
      } else box.replaceChildren();
    }
    const move = snapshot.lastFuelMovement;
    const resultKey = JSON.stringify(move);
    if (resultKey === this.resultKey) return;
    this.resultKey = resultKey;
    const result = this.field("movement-result"); result.replaceChildren();
    if (!move) return;
    const label = document.createElement("p");
    const discharged = move.bundles.filter(b => b.afterPosition == null);
    label.textContent = `Confirmed move #${move.operationId} · CH ${move.channelIndex} · ${move.plan.shiftCount} fresh in End ${move.plan.incomingEnd}, ${discharged.length} out End ${move.plan.outgoingEnd} · score follows channel ripple as time advances.`;
    const details = document.createElement("details"); const title = document.createElement("summary"); title.textContent = "Bundle identities and movement";
    details.append(title);
    move.bundles.forEach(bundle => {
      const row = document.createElement("p");
      row.textContent = `${bundle.bundleId}: ${bundle.beforePosition == null ? "fresh" : `position ${bundle.beforePosition + 1}`} → ${bundle.afterPosition == null ? `discharged (${bundle.burnupMwdPerKg.toFixed(2)} MWd/kg HM)` : `position ${bundle.afterPosition + 1}`}`;
      details.append(row);
    });
    const strip = document.createElement("div"); strip.className = "studio-fuel-strip";
    move.bundles.filter(b => b.beforePosition == null || b.afterPosition == null).forEach(bundle => {
      const cell = document.createElement("span");
      cell.dataset.confirmed = bundle.afterPosition == null ? "discharged" : "inserted";
      cell.textContent = bundle.afterPosition == null ? `Out ${bundle.beforePosition! + 1}` : `New ${bundle.afterPosition + 1}`;
      cell.title = bundle.bundleId;
      strip.append(cell);
    });
    result.append(label, strip, details);
  }

}

/** Plot published bundle measurements in physical End A to End B order. */
function axialLineGraph(channel: CanduChannelSnapshot | undefined, metric: string, label: string,
  read: (bundle: CanduChannelSnapshot["bundles"][number]) => number | null, limit?: number): string {
  const values = Array.from({ length: 12 }, (_, position) => {
    const bundle = channel?.bundles.find(bundle => bundle.position === position);
    return bundle?.hasFuel ? read(bundle) : null;
  });
  const top = limit === undefined ? Math.max(1, ...values.filter((value): value is number => value !== null)) * 1.12
    : Math.max(limit * 1.12, ...values.filter((value): value is number => value !== null));
  const x = (position: number) => 42 + position * 24;
  const y = (value: number) => 75 - value / top * 46;
  let path = "", connected = false;
  const points = values.map((value, position) => {
    if (value === null) { connected = false; return ""; }
    path += `${connected ? "L" : "M"}${x(position)} ${y(value)}`; connected = true;
    return `<rect data-axial-position="${position}" x="${x(position) - 2}" y="${y(value) - 2}" width="4" height="4"><title>Position ${position + 1}: ${value.toFixed(2)} ${label.split(" / ")[1]}</title></rect>`;
  }).join("");
  const grid = [0, .5, 1].map(fraction => `<path class="axial-grid" d="M42 ${y(top * fraction)}H306"/><text x="35" y="${y(top * fraction) + 3}" text-anchor="end">${(top * fraction).toFixed(top >= 100 ? 0 : 1)}</text>`).join("");
  const positions = values.map((_, position) => `<text x="${x(position)}" y="93" text-anchor="middle">${String(position + 1).padStart(2, "0")}</text>`).join("");
  const readings = values.map((value, position) => `${position + 1}: ${value === null ? "empty/unavailable" : value.toFixed(2)}`).join("; ");
  const threshold = limit === undefined ? "" : `<path data-power-limit="${limit}" d="M42 ${y(limit)}H306" stroke="#f76c6c" stroke-dasharray="4 3"/><text x="306" y="${y(limit) - 3}" text-anchor="end" fill="#ff9999">${limit} kW limit</text>`;
  return `<svg data-axial="${metric}" viewBox="0 0 324 100" role="img" aria-label="Axial ${label}; End A to End B"><title>${label}</title><desc>${readings}</desc><text class="axial-label" x="42" y="15">${label}</text>${grid}${threshold}<path class="axial-line" d="${path}"/>${points}${positions}</svg>`;
}
