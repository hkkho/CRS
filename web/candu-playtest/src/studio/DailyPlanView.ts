import { isRunTerminal, type DayProgress, type CanduSnapshot } from "../protocol";
import { gridCoordinateLabel } from "../projection";

/** Draft and report presentation. Orders and reactor decisions remain in Game. */
export class DailyPlanView {
  readonly element = document.createElement("section");
  private key = "";
  constructor() {
    this.element.className = "studio-card studio-daily-plan";
    this.element.setAttribute("aria-label", "Daily fuel plan");
    this.element.innerHTML = `<p class="studio-eyebrow">TODAY'S FUEL PLAN</p><h2 data-day-heading></h2>
      <p>Choose channels while time is frozen. Orders use eight fresh bundles each and run in channel order before the day advances.</p>
      <p data-plan-total></p><ul data-plan-list></ul>
      <div class="studio-report-actions"><button data-action="clear-plan">Clear plan</button><button class="studio-primary" data-action="commit-day">Advance 1 day without refuelling</button></div>
      <p data-day-pending></p><section aria-label="Last day result" data-day-report hidden><h3>Last day</h3><p data-day-summary></p><ul data-day-moves></ul></section>`;
  }
  update(snapshot: CanduSnapshot, channels: readonly number[], pending: boolean, historical: boolean, progress?: DayProgress, ready = true): void {
    this.element.hidden = snapshot.pacingMode !== "daily-turn";
    if (this.element.hidden) return;
    const text = (selector: string, value: string) => { const e = this.element.querySelector(selector)!; if (e.textContent !== value) e.textContent = value; };
    text("[data-day-heading]", `Day ${(snapshot.completedDays ?? 0) + 1}`);
    text("[data-plan-total]", `${channels.length} channels · ${channels.length * 8} fresh bundles${snapshot.shift?.unlimitedFreshFuel ? " · Unlimited stock" : ` · ${snapshot.freshBundlesAvailable} in stock`}`);
    text("[data-day-pending]", pending ? `Advancing day…${progress && progress.simulationSecondsAdvanced > 0 ? ` ${(100 * progress.simulationSecondsAdvanced / progress.requestedSimulationSeconds).toFixed(0)}% calculated` : ""}` : historical ? "Return to live to edit or advance today's plan." : "Time stays frozen until you advance the day.");
    const key = `${snapshot.simulationTimeSeconds}:${snapshot.refuellingOperationCount}:${channels.join(",")}`;
    if (key !== this.key) {
      this.key = key;
      const list = this.element.querySelector("[data-plan-list]")!;
      const focused = document.activeElement instanceof HTMLButtonElement && list.contains(document.activeElement) ? document.activeElement.dataset.planChannel : undefined;
      list.replaceChildren(...channels.map(index => {
        const channel = snapshot.core.channels.find(c => c.channelIndex === index)!;
        const item = document.createElement("li");
        const plan = snapshot.refuellingPlans?.find(p => p.directionId === channel.flowDirection && p.shiftCount === 8);
        const outgoing = plan?.dischargedPositions.map(p => channel.bundles.find(b => b.position === p)?.currentBurnupMwdPerKg).filter((n): n is number => n !== undefined) ?? [];
        const label = document.createElement("span");
        label.textContent = `${gridCoordinateLabel(channel)} · 8 bundles${outgoing.length ? ` · outgoing ${Math.min(...outgoing).toFixed(1)}–${Math.max(...outgoing).toFixed(1)} MWd/kg` : ""} `;
        const remove = document.createElement("button"); remove.dataset.action = "remove-plan"; remove.dataset.planChannel = String(index);
        remove.textContent = `Remove ${gridCoordinateLabel(channel)}`;
        item.append(label, remove); return item;
      }));
      if (focused !== undefined) (list.querySelector<HTMLButtonElement>(`[data-plan-channel="${focused}"]`) ?? this.element.querySelector<HTMLButtonElement>('[data-action="commit-day"]'))?.focus();
      const result = snapshot.lastDayResult;
      (this.element.querySelector("[data-day-report]") as HTMLElement).hidden = !result;
      if (result) {
        const hours = (result.endSimulationTimeSeconds - result.startSimulationTimeSeconds) / 3600;
        text("[data-day-summary]", `${hours.toFixed(1)} hours · ${result.fuelUsed} bundles used · ${result.usefulBundlesDischarged} useful discharges · +${result.scoreDelta.toFixed(2)} points · ${result.thermalEnergyMwh.toFixed(1)} MWh thermal / ${result.electricalEnergyMwhEstimate.toFixed(1)} MWh electric (estimate) · LZC ${(result.averageLzcFillFraction * 100).toFixed(1)}% · tilt ${(result.axialTiltFraction * 100).toFixed(1)}%${result.endReason ? ` · ${result.endReason}` : ""}${result.unexecutedChannels.length ? ` · ${result.unexecutedChannels.length} orders not executed` : ""}`);
        this.element.querySelector("[data-day-moves]")!.replaceChildren(...result.movements.map(move => {
          const item = document.createElement("li");
          const channel = snapshot.core.channels.find(c => c.channelIndex === move.channelIndex)!;
          item.textContent = `${gridCoordinateLabel(channel)} · move #${move.operationId} · flow to End ${move.plan.outgoingEnd}`;
          const details = document.createElement("details"), summary = document.createElement("summary"), bundles = document.createElement("ul");
          summary.textContent = "Bundle identities, positions and burnup";
          bundles.replaceChildren(...move.bundles.map(b => {
            const row = document.createElement("li");
            row.textContent = `${b.bundleId}: ${b.beforePosition == null ? "fresh" : b.beforePosition + 1} → ${b.afterPosition == null ? "discharged" : b.afterPosition + 1} · ${b.burnupMwdPerKg.toFixed(2)} MWd/kg`;
            return row;
          }));
          details.append(summary, bundles); item.append(details); return item;
        }));
      }
    }
    this.element.querySelectorAll<HTMLButtonElement>("button").forEach(b => {
      b.disabled = !ready || pending || historical || isRunTerminal(snapshot);
      if (b.dataset.action === "clear-plan") b.disabled ||= channels.length === 0;
      if (b.dataset.action === "commit-day") {
        b.textContent = channels.length ? "Refuel & advance 1 day" : "Advance 1 day without refuelling";
        b.disabled ||= !snapshot.shift?.unlimitedFreshFuel && channels.length * 8 > snapshot.freshBundlesAvailable;
      }
    });
  }
}
