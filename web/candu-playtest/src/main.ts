import { AppShell } from "./AppShell";
import { SessionAnnouncements } from "./SessionAnnouncements";
import { BridgeSessionController } from "./sessionController";
import { BridgeRecoveryView } from "./BridgeRecoveryView";
import { PlayerPanel } from './PlayerPanel';

const session = new BridgeSessionController();

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
      ? `CANDU live reactor online. ${snapshot.core.channelCount} channels available. ${snapshot.isPaused ? "Paused" : "Running"}. ${snapshot.shift?.unlimitedFreshFuel ? "Unlimited fresh fuel" : `${snapshot.freshBundlesAvailable} fresh bundles`}. ${snapshot.refuellingOperationCount} refuelling operations. Score ${snapshot.scoreTotal.toFixed(1)}. ${update.response?.command.type === "advance" ? "" : update.response?.message ?? "Ready for channel selection."}`
      : `${update.status.title}. ${update.status.detail}`;
  }
});

const shell = new AppShell(session, document.getElementById("game-root")!);
const player = new PlayerPanel(session, () => shell.showStudio());

if (import.meta.env.PROD && 'serviceWorker' in navigator) {
  void navigator.serviceWorker.register(`${import.meta.env.BASE_URL}sw.js`, { scope: import.meta.env.BASE_URL, updateViaCache: 'none' })
    .then(registration => {
      const installing = registration.installing;
      installing?.addEventListener('statechange', () => {
        if (installing.state === 'redundant' && !registration.active)
          window.dispatchEvent(new Event('candu-offline-unavailable'));
      });
      return navigator.serviceWorker.ready;
    })
    .then(() => window.dispatchEvent(new Event('candu-offline-ready')))
    .catch(() => window.dispatchEvent(new Event('candu-offline-unavailable')));
}

document.addEventListener("visibilitychange", () => {
  session.setVisible(document.visibilityState === "visible");
});
window.addEventListener("beforeunload", () => { player.destroy(); shell.destroy(); session.dispose(); }, { once: true });
