import { LauncherView } from "./LauncherView";
import { StudioView } from "./studio/StudioView";
import { ArcadeView } from "./ArcadeView";
import type { WorkspaceSession } from "./studio/StudioNavigation";

/** Native launcher and Studio over one authoritative session. */
export class AppShell {
  private view: LauncherView | StudioView | ArcadeView | null = null;
  private disposed = false;

  constructor(private readonly session: WorkspaceSession, private readonly parent: HTMLElement) {
    this.view = new LauncherView(session, parent, () => this.showStudio(), () => this.showArcade());
  }

  public showArcade(): void {
    if (this.disposed) return;
    this.view?.destroy();
    this.session.startShift();
    this.view = new ArcadeView(this.session, this.parent, () => this.showStudio());
  }

  public showStudio(): void {
    if (this.disposed) return;
    this.view?.destroy();
    this.session.startShift();
    this.view = new StudioView(this.session, this.parent);
  }

  destroy(): void {
    this.disposed = true; this.session.stopShift(); this.view?.destroy();
  }
}
