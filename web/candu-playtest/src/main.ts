import Phaser from "phaser";
import { COLORS, colorString } from "./drawing";
import { BridgeSessionController } from "./sessionController";
import { setRuntimeSession } from "./runtime";
import { BootScene } from "./scenes/BootScene";
import { OperationsScene } from "./scenes/OperationsScene";
import { CoreDesignerScene } from "./scenes/CoreDesignerScene";
import { TitleScene } from "./scenes/TitleScene";
import { StudioScene } from "./scenes/StudioScene";

const session = new BridgeSessionController();
setRuntimeSession(session);

const statusMirror = document.getElementById("status-mirror");
session.subscribe((update) => {
  if (statusMirror === null) {
    return;
  }
  const snapshot = update.snapshot;
  statusMirror.textContent = update.status.isWasmAvailable
    ? `CANDU live reactor online. ${snapshot.core.channelCount} channels available. ${snapshot.isPaused ? "Paused" : "Running"}. ${snapshot.freshBundlesAvailable} fresh bundles. ${snapshot.refuellingOperationCount} refuelling operations. Score ${snapshot.scoreTotal.toFixed(1)}. ${update.response?.command.type === "advance" ? "" : update.response?.message ?? "Ready for channel selection."}`
    : `${update.status.title}. ${update.status.detail}`;
});

const game = new Phaser.Game({
  type: Phaser.CANVAS,
  parent: "game-root",
  width: 1600,
  height: 900,
  backgroundColor: colorString(COLORS.void),
  scene: [BootScene, TitleScene, OperationsScene, CoreDesignerScene, StudioScene],
  scale: {
    mode: Phaser.Scale.FIT,
    autoCenter: Phaser.Scale.CENTER_BOTH,
    width: 1600,
    height: 900,
  },
  render: {
    antialias: true,
    roundPixels: false,
    transparent: false,
  },
  input: {
    activePointers: 3,
  },
  banner: false,
});

game.canvas.setAttribute("role", "application");
game.canvas.setAttribute("aria-label", "CANDU on-power refuelling tactical game");
game.canvas.tabIndex = 0;
game.canvas.focus();

document.addEventListener("visibilitychange", () => {
  session.setVisible(document.visibilityState === "visible");
});
window.addEventListener("beforeunload", () => session.dispose(), { once: true });
