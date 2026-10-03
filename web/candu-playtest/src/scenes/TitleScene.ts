import Phaser from "phaser";
import { getRuntimeSession } from "../runtime";
import { LauncherView } from "../LauncherView";

export class TitleScene extends Phaser.Scene {
  public constructor() { super("TitleScene"); }

  public create(): void {
    const session = getRuntimeSession();
    session.stopShift();
    const canvas = this.game.canvas;
    canvas.setAttribute("aria-hidden", "true");
    canvas.tabIndex = -1;
    const view = new LauncherView(session, document.getElementById("game-root")!, () => {
      session.startShift();
      this.scene.start("StudioScene");
    });
    this.events.once("shutdown", () => view.destroy());
  }
}
