import type { CanduSnapshot } from "./protocol";
import { gridCoordinateLabel } from "./channelCoordinates";

/** A ruled order sheet over the shared draft. It never executes fuel moves. */
export class FuelOrderList {
  readonly element = document.createElement("div");
  private readonly rows = new Map<number, HTMLLIElement>();
  private readonly list: HTMLOListElement;

  constructor(private readonly removeAction = "remove-plan") {
    this.element.className = "fuel-order-list";
    this.element.innerHTML = `<div class="fuel-sheet-columns" aria-hidden="true"><span>NO.</span><span>CHANNEL / FLOW</span><span>FRESH</span><span></span></div><ol data-plan-list aria-label="Channels in execution order"></ol>
      <div class="fuel-sheet-blank" aria-hidden="true"><span>01</span><i></i><span>02</span><i></i><span>03</span><i></i></div>
      <p class="fuel-sheet-empty">Your list is empty. Select a channel, then add it to this sheet.</p>`;
    this.list = this.element.querySelector("ol")!;
  }

  update(snapshot: CanduSnapshot, channels: readonly number[], locked: boolean): void {
    const ordered = [...new Set(channels)].sort((a, b) => a - b);
    const active = document.activeElement instanceof HTMLButtonElement && this.list.contains(document.activeElement)
      ? document.activeElement.dataset.planChannel : undefined;
    for (const [index, row] of this.rows) if (!ordered.includes(index)) { row.remove(); this.rows.delete(index); }
    ordered.forEach((index, order) => {
      let row = this.rows.get(index);
      if (!row) {
        row = document.createElement("li");
        row.innerHTML = `<span class="fuel-sheet-number"></span><div><strong></strong><span class="fuel-sheet-flow"></span><small></small></div><span class="fuel-sheet-bundles">8</span><button type="button">×</button>`;
        this.rows.set(index, row);
      }
      const channel = snapshot.core.channels.find(c => c.channelIndex === index);
      const label = channel ? gridCoordinateLabel(channel) : `#${index}`;
      row.querySelector(".fuel-sheet-number")!.textContent = String(order + 1).padStart(2, "0");
      row.querySelector("strong")!.textContent = label;
      row.querySelector(".fuel-sheet-flow")!.textContent = channel ? channel.flowDirection === "toward-end-a" ? "B → A" : "A → B" : "—";
      const movement = snapshot.refuellingPlans?.find(p => p.directionId === channel?.flowDirection && p.shiftCount === 8);
      const outgoing = movement?.dischargedPositions.map(p => channel?.bundles.find(b => b.position === p)?.currentBurnupMwdPerKg);
      const known = outgoing?.filter((n): n is number => n !== undefined && Number.isFinite(n));
      row.querySelector("small")!.textContent = known?.length === 8
        ? `Exit ${Math.min(...known).toFixed(1)}–${Math.max(...known).toFixed(1)} MWd/kg` : "Exit burnup unavailable";
      const remove = row.querySelector("button")!;
      remove.dataset.action = this.removeAction; remove.dataset.planChannel = String(index);
      remove.setAttribute("aria-label", `Remove ${label}`); remove.disabled = locked;
      if (this.list.children[order] !== row) this.list.insertBefore(row, this.list.children[order] ?? null);
    });
    this.element.classList.toggle("is-filled", ordered.length > 0);
    if (active !== undefined && !ordered.includes(Number(active))) {
      (this.list.querySelector<HTMLButtonElement>("button") ?? this.element.closest("section, aside")?.querySelector<HTMLButtonElement>('[data-action="commit-day"], [data-action="commit"]'))?.focus({ preventScroll: true });
    }
  }
}
