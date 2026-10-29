import type { CanduSnapshot } from "./protocol";

/** Shared goal progress with one disclosure for the complete description. */
export class RunGoalView {
  readonly element = document.createElement("details");
  constructor() {
    this.element.className = "run-goal";
    this.element.setAttribute("aria-label", "Run objective and progress");
    this.element.innerHTML = `<summary><strong></strong><span data-goal-progress></span></summary><div class="run-goal-description"><p data-goal-description></p><small></small></div>`;
  }
  update(snapshot: CanduSnapshot): void {
    const goal = snapshot.shift;
    this.element.hidden = !goal;
    if (!goal) return;
    this.element.querySelector("strong")!.textContent = goal.title;
    this.element.querySelector("[data-goal-progress]")!.textContent = goal.isEndless ? "Endless · unlimited fuel" : `${snapshot.completedDays ?? 0}/${goal.horizonSeconds / 86400} days${goal.usefulBundlesRequired ? ` · ${goal.usefulBundlesDischarged}/${goal.usefulBundlesRequired} useful` : ""}`;
    this.element.querySelector("[data-goal-description]")!.textContent = goal.objective;
    this.element.querySelector("small")!.textContent = `${goal.rewardEarned ? `Earned: ${goal.reward}` : goal.outcome === "missed" ? "Run complete · useful-fuel goal missed" : goal.outcome === "ended" ? "Run ended · inspect the report" : goal.reward ? `Reward: ${goal.reward}` : "Planning is untimed"}${snapshot.provenance?.isModified ? ` · ${snapshot.provenance.label} · Excluded from standard rewards` : ""}`;
    this.element.dataset.outcome = goal.outcome;
  }
}
