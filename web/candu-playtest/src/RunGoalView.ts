import type { CanduSnapshot } from "./protocol";

/** Progress is a presentation of Game's goal and outcome, never a local win rule. */
export class RunGoalView {
  readonly element = document.createElement("section");
  constructor() {
    this.element.className = "run-goal";
    this.element.setAttribute("aria-label", "Run objective and progress");
    this.element.innerHTML = `<div><p class="studio-eyebrow">YOUR RUN</p><h2></h2><p data-goal-description></p></div><div class="run-goal-progress"><strong></strong><progress aria-label="Completed days"></progress><small></small></div>`;
  }
  update(snapshot: CanduSnapshot): void {
    const goal = snapshot.shift;
    this.element.hidden = !goal;
    if (!goal) return;
    const days = goal.horizonSeconds / 86400, completed = snapshot.completedDays ?? 0;
    this.element.querySelector("h2")!.textContent = goal.title;
    this.element.querySelector("[data-goal-description]")!.textContent = goal.objective;
    this.element.querySelector("strong")!.textContent = goal.isEndless ? `Day ${completed + 1} · Free practice` : `${completed} / ${days} days complete`;
    const progress = this.element.querySelector("progress")!;
    progress.hidden = !!goal.isEndless; progress.max = days || 1; progress.value = completed;
    this.element.querySelector("small")!.textContent = goal.outcome === "success" ? goal.rewardEarned ? `Earned: ${goal.reward}` : "Run complete"
      : goal.outcome === "missed" ? "Run complete · useful-fuel goal missed"
      : goal.outcome === "ended" ? "Run ended · inspect the report"
      : goal.usefulBundlesRequired ? `${goal.usefulBundlesDischarged} / ${goal.usefulBundlesRequired} useful bundles · ${snapshot.freshBundlesAvailable} fresh left`
      : goal.unlimitedFreshFuel ? "Unlimited fuel · Planning is untimed" : `${snapshot.freshBundlesAvailable} fresh bundles for the whole run`;
    this.element.dataset.outcome = goal.outcome;
  }
}
