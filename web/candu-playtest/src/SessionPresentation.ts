import type { CanduCommandResponse, CanduSnapshot } from "./protocol";
import { refuelImpactText } from "./gameplayPresentation";

/** Bounded view-only response summary, retained by the shared session across navigation. */
export class SessionPresentation {
  message = "Choose a channel. Inspect the fuel. Make your move.";
  impactText = "Local power, tilt, LZC level and inventory will appear here after refuelling.";
  result: "accepted" | "rejected" | undefined;
  private lastResponse: CanduCommandResponse | null = null;
  accept(response: CanduCommandResponse, previous: CanduSnapshot): void {
    if (response === this.lastResponse) return;
    this.lastResponse = response;
    if (response.command.type === "commit-refuel") {
      this.message = response.message;
      if (response.accepted) this.impactText = refuelImpactText(previous, response.snapshot, response.command.request.channelIndex);
      this.result = response.accepted ? "accepted" : "rejected";
    } else if (response.command.type === "reset" && response.accepted) {
      this.message = "New shift ready. Fresh fuel restocked.";
      this.impactText = "Make your first fuel move to compare its response.";
      this.result = undefined;
    } else if (!response.accepted) {
      this.message = response.message; this.result = "rejected";
    }
  }
  fail(message: string): void { this.message = message; }
  clear(): void {
    this.lastResponse = null;
    this.message = "Choose a channel. Inspect the fuel. Make your move.";
    this.impactText = "Local power, tilt, LZC level and inventory will appear here after refuelling.";
    this.result = undefined;
  }
}
