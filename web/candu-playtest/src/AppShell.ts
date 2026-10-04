import { LauncherView } from "./LauncherView";
import { StudioView } from "./studio/StudioView";
import type { WorkspaceSession } from "./studio/StudioNavigation";

/** Native launcher and Studio over one authoritative session. */
export class AppShell {
  private view: LauncherView | StudioView | null = null;
  private disposed = false;

  constructor(private readonly session: WorkspaceSession, private readonly parent: HTMLElement) {
    this.view = new LauncherView(session, parent, () => this.showStudio());
  }

  private showStudio(): void {
    if (this.disposed) return;
    this.view?.destroy();
    this.session.startShift();
    this.view = new StudioView(this.session, this.parent);
  }

  destroy(): void {
    this.disposed = true; this.session.stopShift(); this.view?.destroy();
  }
}
