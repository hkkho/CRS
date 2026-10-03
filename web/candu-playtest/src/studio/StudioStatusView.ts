import { isRunTerminal, type CanduSnapshot } from "../protocol";
import { canIssueRefuel, type RefuelDraft } from "../commandState";
import { isChannelRefuellable, channelHeadroom } from "../gameplayPresentation";
import type { PaceReading } from "../observedPace";
import { setAttribute } from "./domPatch";

export class StudioStatusView {
  constructor(private readonly element: HTMLElement, private readonly field: (name: string) => HTMLElement) {}
  private text(name: string, value: string): void { const element = this.field(name); if (element.textContent !== value) element.textContent = value; }
  update(snapshot: CanduSnapshot, ready: boolean, pending: boolean, detail: string, message: string,
    pace: PaceReading | undefined, draft: RefuelDraft | null, selected: number, mapMode: "power" | "burnup"): void {
    const channel = snapshot.core.channels.find(channel => channel.channelIndex === selected);
    this.text("feedback", !ready ? detail : pending ? "Reactor is solving your order…" : message);
    this.text('pace', `Requested ${pace?.requested ?? (snapshot.isPaused ? 'Paused' : snapshot.playbackModeId.replace('x', '×'))} · Observed ${pace?.simulatedMinutesPerSecond == null ? '—' : `${pace.simulatedMinutesPerSecond.toFixed(1)} sim min/s`}${pace?.solving ? ' · Solving…' : ''}`);
    setAttribute(this.element, "aria-busy", String(pending));
    this.element.querySelectorAll<HTMLButtonElement>("button[data-action]").forEach(button => {
      const action = button.dataset.action;
      button.disabled = action !== "map" && (!ready || pending);
      if (["size", "direction", "refuel"].includes(action ?? "")) button.disabled ||= isRunTerminal(snapshot) || !channel;
      if (action === "challenge") button.disabled ||= !snapshot.shift;
      if (action === "step") button.disabled ||= !snapshot.isPaused || isRunTerminal(snapshot);
      if (action === "target") button.disabled ||= snapshot.isPaused;
      if (["pause", "speed", "target"].includes(action ?? "")) button.disabled ||= isRunTerminal(snapshot);
      if (action === "refuel") button.disabled ||= !isChannelRefuellable(channel) || !canIssueRefuel(draft, snapshot.freshBundlesAvailable, pending);
      if (action === "map") button.setAttribute("aria-pressed", String(button.dataset.map === mapMode));
      if (action === "size") button.setAttribute("aria-pressed", String(Number(button.dataset.size) === draft?.shiftCount));
      if (action === "speed") button.setAttribute("aria-pressed", String(button.dataset.speed === snapshot.playbackModeId && !snapshot.isPaused));
    });
    this.text("refuel", isRunTerminal(snapshot) ? "Shift complete" : pending ? "Solving…" : `Refuel ${draft?.shiftCount ?? 4} bundles →`);
    this.text("order-note", !isChannelRefuellable(channel) ? channel?.refuellingIneligibilityReason || "This channel contains nonfuel cells and cannot be refuelled." : snapshot.freshBundlesAvailable < (draft?.shiftCount ?? 4) ? "Not enough fresh fuel. Start a new shift to restock." : `Cost: ${draft?.shiftCount ?? 4} fresh bundles · ${snapshot.freshBundlesAvailable} in stock${channel ? ` · ${channelHeadroom(snapshot, channel)}` : ""}`);
  }
}
