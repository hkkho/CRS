import Phaser from "phaser";
import { getRuntimeSession } from "../runtime";
import { StudioView, type StudioNavigation } from "../studio/StudioView";

export class StudioScene extends Phaser.Scene {
  private view: StudioView | null = null;
  private initial: StudioNavigation = {};

  public constructor() { super("StudioScene"); }

  public init(data: StudioNavigation = {}): void { this.initial = data; }

  public create(): void {
    const session = getRuntimeSession();
    session.startShift();
    const canvas = this.game.canvas;
    canvas.setAttribute("aria-hidden", "true");
    canvas.tabIndex = -1;
    this.view = new StudioView(session, document.getElementById("game-root")!, (scene, state) => {
      if (session.isPending) return;
      if (scene === "CoreDesignerScene") {
        session.stopShift();
        this.scene.start(scene, { returnChannelIndex: state.selectedChannelIndex, returnScene: "StudioScene" });
      } else this.scene.start(scene, state);
    }, this.initial);
    this.events.once("shutdown", () => {
      this.view?.destroy();
      this.view = null;
      canvas.removeAttribute("aria-hidden");
      canvas.tabIndex = 0;
      canvas.focus({ preventScroll: true });
    });
  }
}
