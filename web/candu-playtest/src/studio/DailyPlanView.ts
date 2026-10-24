import { isRunTerminal, type DayProgress, type CanduSnapshot } from "../protocol";
import { gridCoordinateLabel } from "../channelCoordinates";
import { FuelOrderList } from "../FuelOrderList";

/** Draft and report presentation. Orders and reactor decisions remain in Game. */
export class DailyPlanView {
  readonly element = document.createElement("section");
  private key = "";
  private readonly orders = new FuelOrderList();
  constructor() {
    this.element.className = "studio-daily-plan fuel-sheet";
    this.element.setAttribute("aria-label", "Daily fuel plan");
    this.element.innerHTML = `<header class="fuel-sheet-header"><span>CORE SERVICES / F-08</span><span data-sheet-state>DRAFT</span></header><p class="studio-eyebrow">TODAY'S FUEL PLAN</p><h2 data-day-heading></h2>
      <div class="fuel-sheet-reference"><span>On-power fuelling list</span><span data-sheet-seed></span></div>
      <details data-plan-review><summary>Fuel plan · <span data-plan-total></span></summary><p>Rows execute in channel order, with channel flow. Drafting spends no fuel or time. Exit burnup describes the current outgoing fuel, not a power forecast.</p></details>
      <div data-sheet-rows></div><p class="fuel-sheet-total"><span>FRESH BUNDLES REQUIRED</span><strong data-sheet-cost></strong></p><p class="fuel-sheet-stock" data-sheet-stock></p>
      <div class="studio-report-actions"><button data-action="clear-plan">Clear plan</button><button class="studio-primary" data-action="commit-day">Advance 1 day without refuelling</button></div>
      <p data-day-pending></p><details aria-label="Last day result" data-day-report hidden><summary>Day report</summary><p data-day-summary></p><ul data-day-moves></ul></details>`;
    this.element.querySelector("[data-sheet-rows]")!.append(this.orders.element);
  }
  update(snapshot: CanduSnapshot, channels: readonly number[], pending: boolean, historical: boolean, progress?: DayProgress, ready = true): void {
    this.element.hidden = snapshot.pacingMode !== "daily-turn";
    if (this.element.hidden) return;
    const text = (selector: string, value: string) => { const e = this.element.querySelector(selector)!; if (e.textContent !== value) e.textContent = value; };
    text("[data-day-heading]", `Day ${isRunTerminal(snapshot) ? Math.max(1, snapshot.completedDays ?? 0) : (snapshot.completedDays ?? 0) + 1}`);
    text("[data-plan-total]", `${channels.length} ${channels.length === 1 ? "channel" : "channels"} · ${channels.length * 8} bundles`);
    text("[data-sheet-cost]", String(channels.length * 8));
    text("[data-sheet-seed]", `CORE ${snapshot.shift?.seed ?? "—"}`);
    text("[data-sheet-state]", historical ? "READ ONLY" : pending ? "SUBMITTED" : isRunTerminal(snapshot) ? "CLOSED" : "DRAFT");
    text("[data-sheet-stock]", snapshot.shift?.unlimitedFreshFuel ? "Unlimited fresh stock · consumption is recorded"
      : channels.length * 8 > snapshot.freshBundlesAvailable ? `This list needs ${channels.length * 8} bundles; only ${snapshot.freshBundlesAvailable} are available. Remove a channel before advancing.`
      : `${snapshot.freshBundlesAvailable} fresh bundles available for this run`);
    this.orders.update(snapshot, channels, !ready || pending || historical || isRunTerminal(snapshot));
    text("[data-day-pending]", pending ? `Advancing day…${progress && progress.simulationSecondsAdvanced > 0 ? ` ${(100 * progress.simulationSecondsAdvanced / progress.requestedSimulationSeconds).toFixed(0)}% calculated` : ""}` : historical ? "Return to live to edit or advance today's plan." : "");
    const key = `${snapshot.simulationTimeSeconds}:${snapshot.refuellingOperationCount}:${channels.join(",")}`;
    if (key !== this.key) {
      this.key = key;
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
        b.textContent = channels.length ? "Refuel & advance" : "Advance day";
        b.setAttribute("aria-label", channels.length ? "Refuel & advance 1 day" : "Advance 1 day without refuelling");
        b.disabled ||= !snapshot.shift?.unlimitedFreshFuel && channels.length * 8 > snapshot.freshBundlesAvailable;
      }
    });
  }
}
