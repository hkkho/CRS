// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { DayCalculationView } from "./DayCalculationView";
import { createSnapshot, createShift } from "../testSnapshot";
import type { CanduCommand, CanduCommandResponse } from "../protocol";

const views: DayCalculationView[] = [];
afterEach(() => { views.splice(0).forEach(view => view.destroy()); document.body.replaceChildren(); });
function harness() {
  const focus = vi.fn(), view = new DayCalculationView(focus); views.push(view); document.body.append(view.element);
  const snapshot = { ...createSnapshot(), pacingMode: "daily-turn" as const, completedDays: 0, isPaused: true };
  const command: CanduCommand = { type: "commit-day", expectedCompletedDays: 0, channelIndices: [210,211] };
  view.begin(snapshot, command.channelIndices, command);
  const response: CanduCommandResponse = { protocol: "candu-playtest-v2", accepted: true, sequence: 1, command,
    message: "Day complete", diagnostics: [], snapshot: { ...snapshot, sequence: 1, simulationTimeSeconds: 86400,
      scoreDelta: 1.25, scoreTotal: 9.75, runStatus: "paused" } };
  return { view, command, snapshot, response, focus };
}

describe("next-day waiting and outcome", () => {
  it("uses an indeterminate meter during the single 24-hour calculation", () => {
    const { view } = harness();
    view.progress({ simulationSecondsAdvanced: 0, requestedSimulationSeconds: 86400 });
    expect(view.element.querySelector("progress")!.hasAttribute("value")).toBe(false);
    expect(view.element.textContent).toContain("Calculating one 24-hour step");
  });
  it("shows actual calculation progress, allows inspection, and does not reopen for progress updates", () => {
    const { view, focus } = harness();
    expect(view.element.open).toBe(true); expect(view.element.dataset.phase).toBe("waiting");
    expect(view.element.textContent).toContain("2 channels · 16 fresh bundles");
    view.progress({ simulationSecondsAdvanced: 43200, requestedSimulationSeconds: 86400 });
    expect(view.element.querySelector("progress")!.value).toBe(43200);
    expect(view.element.textContent).toContain("50% of the day calculated");
    view.element.querySelector("button")!.click(); expect(focus).toHaveBeenCalledOnce();
    view.progress({ simulationSecondsAdvanced: 57600, requestedSimulationSeconds: 86400 });
    expect(view.element.open).toBe(false);
  });
  it("flashes success and shows the authoritative earned and total score", () => {
    const { view, response } = harness(); view.finish(response);
    expect(view.element.dataset.phase).toBe("success"); expect(view.element.textContent).toContain("Core stayed alive");
    expect(view.element.textContent).toContain("+1.25"); expect(view.element.textContent).toContain("Total score 9.75");
    expect((view.element.querySelector("[data-calculation-progress]") as HTMLElement).hidden).toBe(true);
  });
  it("reports an accepted operating loss as failure, including power-limit losses without RRS exhaustion", () => {
    const { view, response } = harness();
    view.finish({ ...response, snapshot: { ...response.snapshot, runStatus: "ended", runEndReason: "Channel power limit exceeded" } });
    expect(view.element.dataset.phase).toBe("failure"); expect(view.element.textContent).toContain("Core lost");
    expect(view.element.textContent).toContain("Channel power limit exceeded");
    expect((view.element.querySelector("[data-calculation-score]") as HTMLElement).hidden).toBe(true);
  });
  it("separates surviving the challenge day from meeting its fuel objective", () => {
    const { view, response } = harness();
    view.finish({ ...response, snapshot: { ...response.snapshot, runStatus: "completed", shift: { ...createShift(), outcome: "missed" } } });
    expect(view.element.dataset.phase).toBe("success"); expect(view.element.textContent).toContain("Challenge objective missed");
  });
  it("does not call a rejected plan a lost core or invent an outcome after interruption", () => {
    const { view, response } = harness(); view.finish({ ...response, accepted: false, message: "Insufficient fuel" });
    expect(view.element.textContent).toContain("Day not completed"); expect(view.element.textContent).toContain("core unchanged");
    const second = harness(); second.view.interrupted("Worker stopped responding");
    expect(second.view.element.textContent).toContain("Calculation interrupted");
    expect(second.view.element.textContent).toContain("outcome may be unknown");
  });
  it("ignores the previous day's response while a new day is pending", () => {
    const { view, snapshot, response, command } = harness(); view.finish(response);
    const next = { ...command, expectedCompletedDays: 1 };
    view.update({ snapshot: { ...snapshot, sequence: 1, completedDays: 1 }, pending: true, pendingCommand: next,
      response, error: null, status: { source: "wasm", title: "Ready", detail: "Ready", isWasmAvailable: true, capabilities: [] } }, []);
    expect(view.element.dataset.phase).toBe("waiting");
  });
});
