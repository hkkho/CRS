import type { CanduSnapshot, CoreBoundaryFace } from "./protocol";
import { gridCoordinateLabel } from "./projection";
import "./workspace.css";

const faces: CoreBoundaryFace[] = ["north", "east", "south", "west", "end-a", "end-b"];
export interface DesignerActions {
  selectChannel(index: number): void;
  selectPosition(position: number): void;
  toggleFuel(): void;
  toggleFace(face: CoreBoundaryFace): void;
  solve(): void;
  zones(): void;
  back(): void;
}

/** Native inspector over the scene's existing selection and command handlers. */
export class DesignerControls {
  readonly element = document.createElement("section");
  private channelIdentity = "";
  private blocked = false;
  private channelIndex = -1;
  private position = 0;
  constructor(parent: HTMLElement, private readonly actions: DesignerActions) {
    this.element.className = "designer-controls workspace-native";
    this.element.setAttribute("aria-label", "Core Designer controls");
    this.element.innerHTML = `<p class="workspace-kicker">LIVE CORE / DESIGNER</p><h1>Inspect & edit cells</h1>
      <button data-action="back">Return to Reactor Studio</button>
      <p>Inspection preserves the run. Accepted physical edits mark a modified sandbox.</p>
      <label>Channel <select data-field="channel"></select></label>
      <label>Bundle position <select data-field="position">${Array.from({ length: 12 }, (_, i) => `<option value="${i}">${i + 1} · End ${i < 6 ? "A" : "B"}</option>`).join("")}</select></label>
      <p data-field="reading"></p>
      <button data-action="fuel"></button>
      <fieldset><legend>Reflective boundaries</legend><div class="designer-faces">${faces.map(face => `<button data-face="${face}" aria-pressed="false">${face.replace("-", " ")}</button>`).join("")}</div></fieldset>
      <button data-action="solve">Solve live core</button><button data-action="zones">Zone geometry</button>
      <p data-field="result"></p>`;
    parent.append(this.element);
    this.element.addEventListener("click", this.onClick);
    this.element.addEventListener("change", this.onChange);
  }
  private field<T extends HTMLElement>(name: string): T { return this.element.querySelector(`[data-field="${name}"], [data-action="${name}"]`)! as T; }

  update(snapshot: CanduSnapshot, channelIndex: number, position: number, pending: boolean, ready: boolean, message: string): void {
    this.blocked = pending || !ready;
    this.channelIndex = channelIndex; this.position = position;
    const identity = snapshot.core.channels.map(channel => channel.channelIndex).join(",");
    const select = this.field<HTMLSelectElement>("channel");
    if (identity !== this.channelIdentity) {
      select.replaceChildren(...snapshot.core.channels.map(channel => new Option(`${gridCoordinateLabel(channel)} · CH ${channel.channelIndex}`, String(channel.channelIndex))));
      this.channelIdentity = identity;
    }
    select.value = String(channelIndex);
    this.field<HTMLSelectElement>("position").value = String(position);
    const bundle = snapshot.core.channels.find(channel => channel.channelIndex === channelIndex)?.bundles.find(bundle => bundle.position === position);
    this.field("reading").textContent = bundle ? `Position ${position + 1}: ${bundle.hasFuel ? "fuel" : "empty / moderator"}. Burnup ${bundle.currentBurnupMwdPerKg.toFixed(2)} MWd/kg. Power ${(bundle.powerWatts / 1000).toFixed(1)} kW thermal.` : "Cell data unavailable.";
    this.field("fuel").textContent = bundle?.hasFuel ? "Set empty / moderator" : "Set fuel / active";
    this.element.setAttribute("aria-busy", String(pending));
    this.element.querySelectorAll<HTMLButtonElement | HTMLSelectElement>("button, select").forEach(control => {
      const disabled = pending || (!ready && control.dataset.action !== "back") ||
        (!bundle && (control.dataset.action === "fuel" || control.hasAttribute("data-face")));
      control.setAttribute("aria-disabled", String(disabled));
      // Keep the active native control focused while an order is pending.
      control.disabled = disabled && control !== document.activeElement;
    });
    this.element.querySelectorAll<HTMLButtonElement>("[data-face]").forEach(button => {
      button.setAttribute("aria-pressed", String(bundle?.reflectiveFaces?.includes(button.dataset.face as CoreBoundaryFace) ?? false));
    });
    const text = pending ? "Solving your order…" : message;
    if (this.field("result").textContent !== text) this.field("result").textContent = text;
  }
  focus(): void { this.field("back").focus({ preventScroll: true }); }
  private readonly onClick = (event: MouseEvent): void => {
    const button = (event.target as Element).closest<HTMLButtonElement>("button");
    if (!button || button.disabled || button.getAttribute("aria-disabled") === "true") return;
    if (button.dataset.face) this.actions.toggleFace(button.dataset.face as CoreBoundaryFace);
    const action = button.dataset.action;
    if (action === "fuel") this.actions.toggleFuel();
    if (action === "solve") this.actions.solve();
    if (action === "zones") this.actions.zones();
    if (action === "back") this.actions.back();
  };
  private readonly onChange = (event: Event): void => {
    const target = event.target as HTMLSelectElement;
    if (this.blocked) {
      this.field<HTMLSelectElement>("channel").value = String(this.channelIndex);
      this.field<HTMLSelectElement>("position").value = String(this.position);
      return;
    }
    if (target.disabled) return;
    if (target.dataset.field === "channel") this.actions.selectChannel(Number(target.value));
    if (target.dataset.field === "position") this.actions.selectPosition(Number(target.value));
  };
  destroy(): void { this.element.removeEventListener("click", this.onClick); this.element.removeEventListener("change", this.onChange); this.element.remove(); }
}
