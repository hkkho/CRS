import { LauncherView } from "./LauncherView";
import { StudioView } from "./studio/StudioView";
import { ArcadeView } from "./ArcadeView";
import { DemoView } from "./DemoView";
import type { WorkspaceSession } from "./studio/StudioNavigation";

/** Native launcher and Studio over one authoritative session. */
export class AppShell {
  private view: LauncherView | StudioView | ArcadeView | DemoView | null = null;
  private disposed = false;

  constructor(private readonly session: WorkspaceSession, private readonly parent: HTMLElement) {
    this.view = new LauncherView(session, parent, guided => this.showStudio(guided), () => this.showArcade(), seed => this.showDemo(seed));
  }

  public showDemo(seed: number): void {
    if (this.disposed) return;
    this.view?.destroy(); this.session.stopShift();
    this.view = new DemoView(this.parent, seed, () => {
      this.view?.destroy();
      this.view = new LauncherView(this.session, this.parent, guided => this.showStudio(guided), () => this.showArcade(), nextSeed => this.showDemo(nextSeed));
    });
  }

  public showArcade(): void {
    if (this.disposed) return;
    this.view?.destroy();
    this.session.startShift();
    this.view = new ArcadeView(this.session, this.parent, () => this.showStudio());
  }

  public showStudio(guided = false): void {
    if (this.disposed) return;
    this.view?.destroy();
    this.session.startShift();
    this.view = new StudioView(this.session, this.parent, { guided });
  }

  destroy(): void {
    this.disposed = true; this.session.stopShift(); this.view?.destroy();
  }
}
