import { AppShell } from "./AppShell";
import { SessionAnnouncements } from "./SessionAnnouncements";
import { BridgeSessionController } from "./sessionController";
import { setRuntimeSession } from "./runtime";
import { BridgeRecoveryView } from "./BridgeRecoveryView";

const session = new BridgeSessionController();
setRuntimeSession(session);

const statusMirror = document.getElementById("status-mirror");
const recovery = new BridgeRecoveryView(document.body);
const liveRegion = document.getElementById("session-announcements");
const announcements = liveRegion ? new SessionAnnouncements(liveRegion) : null;
session.subscribe(update => {
  recovery.update(update.status);
  announcements?.update(update);
  // Non-announcing diagnostic mirror for smoke/reproduction tooling.
  if (statusMirror) {
    const snapshot = update.snapshot;
    statusMirror.textContent = update.status.isWasmAvailable
      ? `CANDU live reactor online. ${snapshot.core.channelCount} channels available. ${snapshot.isPaused ? "Paused" : "Running"}. ${snapshot.freshBundlesAvailable} fresh bundles. ${snapshot.refuellingOperationCount} refuelling operations. Score ${snapshot.scoreTotal.toFixed(1)}. ${update.response?.command.type === "advance" ? "" : update.response?.message ?? "Ready for channel selection."}`
      : `${update.status.title}. ${update.status.detail}`;
  }
});

const shell = new AppShell(session, document.getElementById("game-root")!);

document.addEventListener("visibilitychange", () => {
  session.setVisible(document.visibilityState === "visible");
});
window.addEventListener("beforeunload", () => { shell.destroy(); session.dispose(); }, { once: true });
