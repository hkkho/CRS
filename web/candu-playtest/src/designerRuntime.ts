import type { WorkspaceSession } from "./studio/StudioNavigation";
import Phaser from "phaser";
import { CoreDesignerScene } from "./scenes/CoreDesignerScene";
import type { DesignerRuntime } from "./AppShell";

/** Phaser is confined to this optional feature, with no bridge initialization. */
export function createDesignerRuntime(parent: HTMLElement, session: WorkspaceSession): Promise<DesignerRuntime> {
  const stage = document.createElement("div"); stage.className = "designer-stage"; stage.hidden = true; parent.append(stage);
  return new Promise(resolve => {
    new Phaser.Game({
      type: Phaser.CANVAS, parent: stage, width: 1600, height: 900, backgroundColor: "#07100b",
      scale: { mode: Phaser.Scale.FIT, autoCenter: Phaser.Scale.CENTER_BOTH, width: 1600, height: 900 },
      render: { antialias: true, roundPixels: false, transparent: false }, input: { activePointers: 3 }, banner: false,
      callbacks: { postBoot: game => {
        game.canvas.setAttribute("aria-hidden", "true"); game.canvas.tabIndex = -1;
        game.scene.add("CoreDesignerScene", new CoreDesignerScene(session), false); game.loop.sleep();
        resolve({
          start: (state, onReturn) => {
            stage.hidden = false; game.loop.wake();
            game.scale.getParentBounds();
            game.scale.refresh();
            game.scene.start("CoreDesignerScene", { returnChannelIndex: state.selectedChannelIndex, studioNavigation: state, onReturn });
          },
          stop: () => { game.scene.stop("CoreDesignerScene"); game.loop.sleep(); stage.hidden = true; },
          destroy: () => { game.destroy(true); stage.remove(); },
        });
      } },
    });
  });
}
