import { isRunTerminal, type CanduCommandResponse } from "./protocol";
import type { SessionUpdate } from "./sessionController";

/** One announcement per meaningful event, independent of telemetry emissions. */
export class SessionAnnouncements {
  private connection = "";
  private response: CanduCommandResponse | null = null;
  private error: string | null = null;
  private terminal = false;
  private terminalSequence = -1;

  constructor(private readonly region: HTMLElement) {}

  update(update: SessionUpdate): void {
    const connection = update.status.isWasmAvailable ? "online" : update.status.source;
    let message = "";
    if (connection !== this.connection) {
      this.connection = connection;
      // Recovery owns unavailable announcements through its alert panel.
      if (connection === "online") message = `CANDU live reactor online. ${update.snapshot.core.channelCount} channels available. Ready for channel selection.`;
      else if (connection === "loading") message = "Connecting to the live reactor.";
    }
    const terminal = isRunTerminal(update.snapshot);
    if (terminal && !this.terminal) {
      this.terminalSequence = update.snapshot.sequence;
      message = `Shift ended. ${update.snapshot.runEndReason || update.snapshot.rrs.gameOverReason || "The run is complete."} Review the shift report or retry.`;
    }
    if (!terminal) this.terminalSequence = -1;
    this.terminal = terminal;

    const response = update.response;
    if (response && response !== this.response) {
      this.response = response;
      if (connection !== "unavailable" && response.snapshot.sequence !== this.terminalSequence &&
          (!response.accepted || response.command.type !== "advance")) {
        message = `${response.accepted ? "Accepted" : "Rejected"}: ${response.message}`;
      }
    }
    if (update.error && update.error !== this.error && connection !== "unavailable") message = `Order failed: ${update.error}`;
    this.error = update.error;
    if (message) {
      // Replace once per event, even if two distinct orders have identical text.
      this.region.replaceChildren(document.createTextNode(message));
    }
  }
}
