import type { CanduCommand, CanduCommandResponse, CanduSnapshot, DayProgress } from "../protocol";
import type { SessionUpdate } from "../sessionController";
import { gridCoordinateLabel } from "../projection";

/** Animation follows actual work and authoritative outcomes; it never drives the clock. */
export class DayCalculationView {
  readonly element = document.createElement("dialog");
  private command: CanduCommand | null = null;
  private lastResult: CanduCommandResponse | null = null;
  private startSequence = 0;
  private phase: "idle" | "waiting" | "success" | "failure" | "rejected" = "idle";
  private readonly heading: HTMLElement;
  private readonly closeButton: HTMLButtonElement;

  constructor(private readonly returnFocus: () => void) {
    this.element.className = "studio-day-dialog";
    this.element.hidden = true;
    this.element.setAttribute("aria-labelledby", "studio-day-heading");
    this.element.innerHTML = `<div class="studio-day-card">
      <p class="studio-eyebrow" data-calculation-kicker>NEXT DAY / CORE CALCULATION</p>
      <h2 id="studio-day-heading" tabindex="-1">Calculating the next day</h2>
      <p data-calculation-plan></p>
      <div class="studio-day-reactor" aria-hidden="true"><svg viewBox="0 0 200 200">
        <circle class="studio-day-halo" cx="100" cy="100" r="87"/>
        <g class="studio-day-orbit"><circle cx="100" cy="100" r="76"/><circle cx="100" cy="24" r="5"/></g>
        <circle class="studio-day-vessel" cx="100" cy="100" r="61"/>
        <g class="studio-day-fuel">${[-2, -1, 0, 1, 2].flatMap(y => [-2, -1, 0, 1, 2].filter(x => x*x+y*y <= 5)
          .map(x => `<rect x="${94 + x*19}" y="${94 + y*19}" width="12" height="12" style="animation-delay:${(x+y+4)*.12}s"/>`)).join("")}</g>
        <path class="studio-day-scan" d="M44 100h112"/>
      </svg><span class="studio-day-stamp" data-calculation-stamp></span></div>
      <p class="studio-day-explanation" data-calculation-description>Calculating fuel burnup, iodine and xenon, power distribution, and regulating headroom.</p>
      <div data-calculation-progress><progress max="86400" value="0" aria-label="Next day calculation progress"></progress><p data-calculation-percent>Preparing today's fuel plan…</p></div>
      <div class="studio-day-score" data-calculation-score hidden><span>DAY'S EARNED POINTS</span><strong></strong><small></small></div>
      <p data-calculation-detail></p>
      <button type="button" class="studio-outline">Inspect while calculating</button>
    </div>`;
    this.heading = this.element.querySelector("h2")!;
    this.closeButton = this.element.querySelector("button")!;
    this.closeButton.addEventListener("click", this.dismiss);
    this.element.addEventListener("cancel", this.onCancel);
  }

  private text(selector: string, value: string): void {
    const field = this.element.querySelector(selector)!;
    if (field.textContent !== value) field.textContent = value;
  }
  private show(): void {
    this.element.hidden = false;
    if (!this.element.open) {
      if (typeof this.element.showModal === "function") this.element.showModal();
      else this.element.setAttribute("open", "");
    }
    this.heading.focus({ preventScroll: true });
  }
  begin(snapshot: CanduSnapshot, channels: readonly number[], command: CanduCommand): void {
    if (this.command === command) return;
    this.command = command; this.phase = "waiting";
    this.startSequence = snapshot.sequence;
    this.element.dataset.phase = this.phase; this.element.setAttribute("aria-busy", "true");
    this.heading.textContent = `Calculating Day ${(snapshot.completedDays ?? 0) + 1}`;
    const labels = channels.slice(0, 6).map(i => {
      const channel = snapshot.core.channels.find(c => c.channelIndex === i);
      return channel ? gridCoordinateLabel(channel) : String(i);
    });
    this.text("[data-calculation-plan]", channels.length ? `${channels.length} channels · ${channels.length * 8} fresh bundles · ${labels.join(" / ")}${channels.length > 6 ? " / …" : ""}` : "No refuelling today · advance one day with the current fuel.");
    this.text("[data-calculation-description]", "Calculating fuel burnup, iodine and xenon, power distribution, and regulating headroom.");
    this.text("[data-calculation-stamp]", "CALCULATING");
    this.text("[data-calculation-detail]", "The core stays frozen on screen until the day's result is ready.");
    (this.element.querySelector("[data-calculation-score]") as HTMLElement).hidden = true;
    (this.element.querySelector("[data-calculation-progress]") as HTMLElement).hidden = false;
    this.element.querySelector("progress")!.removeAttribute("value");
    this.text("[data-calculation-percent]", "Preparing today's fuel plan…");
    this.closeButton.textContent = "Inspect while calculating";
    this.show();
  }
  progress(progress: DayProgress | undefined): void {
    if (this.phase !== "waiting" || !progress) return;
    const meter = this.element.querySelector("progress")!;
    if (progress.simulationSecondsAdvanced === 0) {
      meter.removeAttribute("value");
      this.text("[data-calculation-percent]", "Calculating one 24-hour step…"); return;
    }
    meter.max = progress.requestedSimulationSeconds; meter.value = progress.simulationSecondsAdvanced;
    this.text("[data-calculation-percent]", `${(100 * meter.value / meter.max).toFixed(0)}% of the day calculated`);
  }
  finish(response: CanduCommandResponse): void {
    if (response === this.lastResult || response.command.type !== "commit-day") return;
    this.lastResult = response;
    const snapshot = response.snapshot, day = snapshot.lastDayResult;
    const lost = snapshot.runStatus === "ended" || snapshot.shift?.outcome === "ended" || snapshot.rrs.isGameOver;
    this.phase = !response.accepted ? "rejected" : lost ? "failure" : "success";
    this.element.dataset.phase = this.phase; this.element.setAttribute("aria-busy", "false");
    this.heading.textContent = this.phase === "success" ? "Core stayed alive" : this.phase === "failure" ? "Core lost" : "Day not completed";
    this.text("[data-calculation-stamp]", this.phase === "success" ? "SUCCESS" : this.phase === "failure" ? "FAIL" : "REJECTED");
    this.text("[data-calculation-description]", this.phase === "failure" ? snapshot.runEndReason || snapshot.rrs.gameOverReason : this.phase === "rejected" ? response.message : "Your refuelling decision kept the core within its operating limits.");
    const score = this.element.querySelector<HTMLElement>("[data-calculation-score]")!;
    score.hidden = this.phase !== "success";
    if (!score.hidden) {
      score.querySelector("strong")!.textContent = `+${(day?.scoreDelta ?? snapshot.scoreDelta).toFixed(2)}`;
      score.querySelector("small")!.textContent = `Total score ${snapshot.scoreTotal.toLocaleString("en-US", { maximumFractionDigits: 2 })}`;
    }
    (this.element.querySelector("[data-calculation-progress]") as HTMLElement).hidden = true;
    this.text("[data-calculation-detail]", !response.accepted ? "The rejected plan leaves the core unchanged. Edit your plan and try again." :
      `${day ? `${((day.endSimulationTimeSeconds - day.startSimulationTimeSeconds) / 3600).toFixed(1)} hours · ${day.fuelUsed} bundles used` : response.message}${snapshot.shift?.outcome === "missed" ? " · Challenge objective missed" : snapshot.shift?.rewardEarned ? ` · ${snapshot.shift.reward}` : ""}${day?.unexecutedChannels.length ? ` · ${day.unexecutedChannels.length} orders not executed` : ""}`);
    this.closeButton.textContent = lost ? "Review the shift report" : "Continue";
    this.show();
  }
  interrupted(message: string): void {
    if (this.phase !== "waiting") return;
    this.phase = "rejected"; this.element.dataset.phase = this.phase;
    this.element.setAttribute("aria-busy", "false");
    this.heading.textContent = "Calculation interrupted";
    this.text("[data-calculation-stamp]", "INTERRUPTED");
    this.text("[data-calculation-description]", message);
    this.text("[data-calculation-detail]", "The last order's outcome may be unknown. It will not be retried automatically.");
    (this.element.querySelector("[data-calculation-progress]") as HTMLElement).hidden = true;
    this.closeButton.textContent = "Return to the reactor"; this.show();
  }
  update(update: SessionUpdate, channels: readonly number[]): void {
    if (update.pending && update.pendingCommand?.type === "commit-day") this.begin(update.snapshot, channels, update.pendingCommand);
    this.progress(update.dayProgress);
    if (update.response && (!update.pending || update.response.sequence > this.startSequence)) this.finish(update.response);
    if (update.error) this.interrupted(update.error);
  }
  private readonly dismiss = (): void => {
    if (typeof this.element.close === "function") this.element.close();
    else this.element.removeAttribute("open");
    this.element.hidden = true; this.returnFocus();
  };
  private readonly onCancel = (event: Event): void => { event.preventDefault(); this.dismiss(); };
  destroy(): void {
    this.closeButton.removeEventListener("click", this.dismiss);
    this.element.removeEventListener("cancel", this.onCancel);
    if (this.element.open && typeof this.element.close === "function") this.element.close();
    this.element.remove();
  }
}
