import "./simulatorManual.css";
import { manualChapters as chapters } from "./manualContent";

/** On-demand documentation only; opening it never changes the shared session. */
export class SimulatorManual {
  readonly element = document.createElement("dialog");
  private opener: HTMLElement | null = null;
  constructor(private readonly host: HTMLElement) {
    this.element.className = "simulator-manual";
    this.element.setAttribute("aria-label", "Fuelling simulator manual");
    this.element.innerHTML = `<header><div><small>CANDU / REFERENCE</small><h1>Simulator manual</h1></div><button type="button" data-manual-close aria-label="Close manual">Close ×</button></header><div class="manual-layout"><nav aria-label="Manual chapters">${chapters.map(([id, title], i) => `<button type="button" data-manual-chapter="${id}" aria-pressed="${i === 0}" aria-controls="manual-${id}">${title}</button>`).join("")}<a class="manual-library-link" href="${import.meta.env.BASE_URL}docs/manual/" target="_blank" rel="noopener">Manual, theory &amp; code library ↗</a></nav><div class="manual-pages">${chapters.map(([id, title, content], i) => `<section id="manual-${id}" tabindex="0" aria-label="${title}" ${i ? "hidden" : ""}>${content}</section>`).join("")}</div></div>`;
    host.append(this.element);
    host.addEventListener("click", this.onClick);
    host.addEventListener("keydown", this.onKeyDown);
    this.element.addEventListener("close", this.restoreFocus);
    this.element.addEventListener("cancel", this.onCancel);
  }
  open(opener?: HTMLElement): void {
    if (this.element.open) return;
    this.opener = opener ?? (document.activeElement instanceof HTMLElement ? document.activeElement : null);
    if (typeof this.element.showModal === "function") this.element.showModal();
    else this.element.setAttribute("open", "");
    this.element.querySelector<HTMLButtonElement>("[data-manual-close]")!.focus();
  }
  private close(): void {
    if (typeof this.element.close === "function") this.element.close();
    else { this.element.removeAttribute("open"); this.restoreFocus(); }
  }
  private readonly restoreFocus = (): void => { if (this.opener?.isConnected) this.opener.focus({ preventScroll: true }); };
  private readonly onCancel = (event: Event): void => { event.preventDefault(); this.close(); };
  private readonly onClick = (event: MouseEvent): void => {
    const button = event.target instanceof Element ? event.target.closest<HTMLButtonElement>("button") : null;
    if (!button) return;
    if (button.hasAttribute("data-manual-open")) this.open(button);
    if (button.hasAttribute("data-manual-close")) this.close();
    const chapter = button.dataset.manualChapter;
    if (chapter) {
      this.element.querySelectorAll<HTMLElement>("[data-manual-chapter]").forEach(item => item.setAttribute("aria-pressed", String(item === button)));
      this.element.querySelectorAll<HTMLElement>(".manual-pages > section").forEach(page => { page.hidden = page.id !== `manual-${chapter}`; });
      this.element.querySelector<HTMLElement>(`#manual-${chapter}`)!.scrollTop = 0;
      this.element.scrollTop = 0;
    }
  };
  private readonly onKeyDown = (event: KeyboardEvent): void => {
    if (event.key === "F1") { event.preventDefault(); this.open(); }
    if (event.key === "Tab" && this.element.open) {
      const controls = [...this.element.querySelectorAll<HTMLElement>('button, a[href], input, select, textarea, [tabindex="0"]')]
        .filter(control => !control.closest("[hidden]") && !control.matches(":disabled"));
      const current = controls.indexOf(document.activeElement as HTMLElement);
      const next = (current + (event.shiftKey ? -1 : 1) + controls.length) % controls.length;
      event.preventDefault(); controls[next]?.focus();
    }
    if (event.key === "Escape" && this.element.open) { event.preventDefault(); event.stopPropagation(); this.close(); }
  };
  destroy(): void {
    this.host.removeEventListener("click", this.onClick);
    this.host.removeEventListener("keydown", this.onKeyDown);
    this.element.removeEventListener("close", this.restoreFocus);
    this.element.removeEventListener("cancel", this.onCancel);
    this.element.remove();
  }
}
