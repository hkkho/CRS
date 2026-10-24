import { AppShell } from "./AppShell";
import { SessionAnnouncements } from "./SessionAnnouncements";
import { BridgeSessionController } from "./sessionController";
import { BridgeRecoveryView } from "./BridgeRecoveryView";
import { PlayerPanel } from './PlayerPanel';
import { GameUpdateView } from './GameUpdateView';

import { createCanduPlaytestBridge } from "./bridge";
import "./observatory.css";

const session = new BridgeSessionController(createCanduPlaytestBridge({
  initialPacingMode: "daily-turn",
}), {}, "daily-turn");

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
    statusMirror.dataset.pacing = snapshot.pacingMode ?? "real-time";
    statusMirror.dataset.timeSeconds = String(snapshot.simulationTimeSeconds);
    statusMirror.dataset.completedDays = String(snapshot.completedDays ?? 0);
    statusMirror.dataset.fuelConsumed = String(snapshot.shift?.fuelConsumed ?? 0);
    statusMirror.textContent = update.status.isWasmAvailable
      ? `CANDU live reactor online. ${snapshot.core.channelCount} channels available. ${snapshot.isPaused ? "Paused" : "Running"}. ${snapshot.shift?.unlimitedFreshFuel ? "Unlimited fresh fuel" : `${snapshot.freshBundlesAvailable} fresh bundles`}. ${snapshot.refuellingOperationCount} refuelling operations. Score ${snapshot.scoreTotal.toFixed(1)}. ${update.response?.command.type === "advance" ? "" : update.response?.message ?? "Ready for channel selection."}`
      : `${update.status.title}. ${update.status.detail}`;
  }
});

const shell = new AppShell(session, document.getElementById("game-root")!);
const player = new PlayerPanel(session, () => shell.showStudio());
const updates = new GameUpdateView(session, () => player.saveBeforeUpdate());

void updates.initialize(import.meta.env.BASE_URL, import.meta.env.PROD);

document.addEventListener("visibilitychange", () => {
  session.setVisible(document.visibilityState === "visible");
});
window.addEventListener("beforeunload", () => { updates.destroy(); player.destroy(); shell.destroy(); session.dispose(); }, { once: true });
