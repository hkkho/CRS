import { LauncherView } from "./LauncherView";
import { StudioView, type StudioNavigation } from "./studio/StudioView";
import type { WorkspaceSession } from "./studio/StudioNavigation";

export interface DesignerRuntime {
  start(state: StudioNavigation, onReturn: (state: StudioNavigation) => void): void;
  stop(): void;
  destroy(): void;
}
/** DOM entry path; the optional Designer shares this session and loads once. */
export class AppShell {
  private view: LauncherView | StudioView | null = null;
  private designer: DesignerRuntime | null = null;
  private loading = false;
  private disposed = false;
  private readonly notice = document.createElement("section");

  constructor(private readonly session: WorkspaceSession, private readonly parent: HTMLElement,
    private readonly loadDesigner: (parent: HTMLElement) => Promise<DesignerRuntime> = async parent => (await import("./designerRuntime")).createDesignerRuntime(parent, session)) {
    this.notice.className = "designer-loading workspace-native";
    this.notice.hidden = true; this.notice.setAttribute("role", "status");
    parent.append(this.notice);
    this.view = new LauncherView(session, parent, () => this.showStudio());
  }

  private showStudio(state: StudioNavigation = {}): void {
    if (this.disposed) return;
    this.designer?.stop(); this.view?.destroy();
    this.session.startShift();
    this.view = new StudioView(this.session, this.parent, (_scene, navigation) => { void this.openDesigner(navigation); }, state);
  }

  private async openDesigner(state: StudioNavigation): Promise<void> {
    if (this.loading || this.session.isPending || this.disposed) return;
    this.loading = true; this.session.stopShift();
    this.notice.textContent = "Opening Core Designer…"; this.notice.hidden = false;
    try {
      this.designer ??= await this.loadDesigner(this.parent);
      if (this.disposed) { this.designer.destroy(); return; }
      this.view?.destroy(); this.view = null;
      this.designer.start(state, navigation => this.showStudio(navigation));
      this.notice.hidden = true;
    } catch {
      if (this.disposed) return;
      if (!this.view) {
        this.designer?.destroy(); this.designer = null;
        this.showStudio(state);
      } else this.session.startShift();
      this.notice.textContent = "Core Designer could not open. Your shift is still here. ";
      const retry = document.createElement("button"); retry.textContent = "Try opening Designer again";
      retry.addEventListener("click", () => { void this.openDesigner(state); });
      this.notice.append(retry); retry.focus();
    } finally { this.loading = false; }
  }

  destroy(): void {
    this.disposed = true; this.session.stopShift(); this.view?.destroy(); this.designer?.destroy(); this.notice.remove();
  }
}
