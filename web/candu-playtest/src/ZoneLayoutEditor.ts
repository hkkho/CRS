import type { BridgeSessionController } from "./sessionController";
import type { ZoneNodeBinding } from "./protocol";
import { isAbsorbing, moveAbsorberMask, zoneBindings } from "./zoneLayout";
import "./zoneLayout.css";

const palette = ["#c87952", "#daae52", "#6d9c6e", "#43888b", "#758abd", "#aa76a2", "#ba626b",
  "#994d28", "#9c7927", "#426d44", "#246365", "#4c6096", "#795679", "#8d4149"];

/** Editable draft; only Core/Game can validate and commit the resulting physics. */
export class ZoneLayoutEditor {
  private readonly dialog = document.createElement("dialog");
  private nodes: ZoneNodeBinding[];
  private undo: ZoneNodeBinding[][] = [];
  private selectedChannel = 189;
  private readonly unsubscribe: () => void;
  private readonly previousFocus = document.activeElement as HTMLElement | null;

  public constructor(private readonly session: BridgeSessionController, private readonly onClose: () => void) {
    const bindings = zoneBindings(session.snapshot);
    if (!bindings) throw new Error("Zone geometry requires an updated authoritative WASM snapshot.");
    if (session.snapshot.rrs.absorptionReferenceFillFraction === undefined)
      throw new Error("Zone calibration metadata requires an updated authoritative WASM snapshot.");
    this.nodes = bindings;
    this.dialog.className = "zone-layout-editor";
    this.dialog.setAttribute("aria-labelledby", "zone-layout-title");
    this.dialog.innerHTML = `
      <header><div><p>CORE DESIGNER / ZONE GEOMETRY</p><h1 id="zone-layout-title">Regions & absorber masks</h1></div><button data-action="close">Close</button></header>
      <p>Regions measure power. Absorber masks specify where each fill compartment changes absorption. These are homogenized solver cells, not physical tubes.</p>
      <div class="zone-layout-body"><section><div class="zone-layout-controls">
        <label>Axial bundle slice <select data-field="slice">${Array.from({ length: 12 }, (_, i) => `<option value="${i}">${i + 1} / End ${i < 6 ? "A" : "B"}</option>`).join("")}</select></label>
        <label>Show <select data-field="mode"><option value="region">Control regions</option><option value="absorber">Absorber masks</option></select></label>
      </div><svg data-field="map" viewBox="0 0 600 600" aria-label="Zone geometry core face"></svg>
      <div class="zone-layout-legend">${palette.map((color, i) => `<span><i style="background:${color}"></i>Z${i + 1}</span>`).join("")}</div>
      <p data-field="coverage"></p><p data-field="cell"></p><div data-field="rack" class="zone-layout-rack" aria-label="Selected channel axial membership"></div></section>
      <aside><h2>Edit selected cells</h2><p>Click a core cell to select it. Enable the brush to paint multiple cells with clicks or drags.</p>
        <label><input type="checkbox" data-field="brush"> Paint on click / drag</label>
        <label>Edit <select data-field="edit"><option value="region">Control-region membership</option><option value="absorber">Absorber compartment & slopes</option></select></label>
        <label>Zone <select data-field="zone">${palette.map((_, i) => `<option value="${i}">Z${i + 1}</option>`).join("")}</select></label>
        <label>Scope <select data-field="scope"><option value="cell">Selected bundle slice</option><option value="half">Selected axial half</option><option value="channel">Whole channel</option></select></label>
        <label>Fast slope (m⁻¹ / unit fill)<input type="number" data-field="fast" value="${bindings.find(n => n.channelIndex === this.selectedChannel)!.group1AbsorptionPerMPerFillFraction}" min="0" max="1" step="any"></label>
        <label>Thermal slope (m⁻¹ / unit fill)<input type="number" data-field="thermal" value="${bindings.find(n => n.channelIndex === this.selectedChannel)!.group2AbsorptionPerMPerFillFraction}" min="0" max="1" step="any"></label>
        <button data-action="paint">Set selected cells</button><button data-action="clear">Clear selected absorber cells</button>
        <h2>Move absorber mask</h2><p>Moves the selected compartment's entire mask; regions stay in place. Offsets use lattice columns, display rows and bundle positions.</p>
        <div class="zone-layout-offsets">${["dx", "dy", "dz"].map((f, i) => `<label>${["Column", "Row", "Axial"][i]}<input type="number" data-field="${f}" value="0" step="1"></label>`).join("")}</div>
        <button data-action="move">Move selected compartment</button>
        <h2>Draft controls</h2><div class="zone-layout-controls"><button data-action="undo">Undo</button><button data-action="reload">Reload live layout</button><button data-action="export">Export JSON</button></div>
        <p>Apply runs the shared core solver atomically. Invalid geometry or coefficients leave the live run unchanged. Spatial references are rebuilt; fuel, clock and score are preserved.</p>
        <button data-action="apply" class="zone-layout-apply">Apply layout & solve</button>
        <p data-field="message" role="status" aria-live="polite">Live geometry loaded. No draft edits applied.</p>
      </aside></div>`;
    document.body.append(this.dialog);
    this.dialog.addEventListener("click", this.handleClick);
    this.dialog.addEventListener("change", () => this.render());
    this.dialog.addEventListener("cancel", (e) => { e.preventDefault(); this.destroy(); });
    this.unsubscribe = session.subscribe(() => {
      this.field<HTMLButtonElement>("apply").disabled = session.isPending || !session.status.isWasmAvailable;
    });
    this.render();
    this.dialog.showModal();
  }

  private field<T extends HTMLElement = HTMLElement>(name: string): T {
    return this.dialog.querySelector(`[data-field="${name}"], [data-action="${name}"]`)! as T;
  }
  private value(name: string): string { return this.field<HTMLInputElement>(name).value; }
  private checkpoint(): void { this.undo.push(this.nodes.map(n => ({ ...n }))); if (this.undo.length > 20) this.undo.shift(); }
  private message(text: string): void { this.field("message").textContent = text; }

  private paint(clear = false): void {
    const slice = Number(this.value("slice")), scope = this.value("scope");
    const fast = Number(this.value("fast")), thermal = Number(this.value("thermal"));
    if (![fast, thermal].every(n => Number.isFinite(n) && n >= 0 && n <= 1)) throw new Error("Slopes must be between 0 and 1 m⁻¹ per unit fill.");
    this.checkpoint();
    for (const node of this.nodes) if (node.channelIndex === this.selectedChannel &&
      (scope === "channel" || (scope === "half" ? Math.floor(node.position / 6) === Math.floor(slice / 6) : node.position === slice))) {
      if (!clear && this.value("edit") === "region") node.logicalZoneId = Number(this.value("zone"));
      else {
        node.absorberZoneId = Number(this.value("zone"));
        node.group1AbsorptionPerMPerFillFraction = clear ? 0 : fast;
        node.group2AbsorptionPerMPerFillFraction = clear ? 0 : thermal;
      }
    }
    this.message("Draft changed. Apply layout & solve to commit.");
  }

  private readonly handleClick = (event: Event): void => {
    const target = event.target as Element;
    const cell = target.closest("[data-channel]");
    if (cell) {
      this.selectedChannel = Number(cell.getAttribute("data-channel"));
      this.paintWithBrush();
      this.render(); return;
    }
    const position = target.closest("[data-position]");
    if (position) { this.field<HTMLSelectElement>("slice").value = position.getAttribute("data-position")!; this.render(); return; }
    const action = target.closest("[data-action]")?.getAttribute("data-action");
    try {
      switch (action) {
        case "close": this.destroy(); return;
        case "paint": this.paint(); break;
        case "clear": this.paint(true); break;
        case "undo": if (this.undo.length) this.nodes = this.undo.pop()!; break;
        case "reload": this.nodes = zoneBindings(this.session.snapshot)!; this.undo = []; this.message("Live layout reloaded; draft discarded."); break;
        case "move": {
          const moved = moveAbsorberMask(this.nodes, this.session.snapshot, Number(this.value("zone")),
            Number(this.value("dx")), Number(this.value("dy")), Number(this.value("dz")));
          this.checkpoint(); this.nodes = moved; this.message("Absorber mask moved in draft. Apply to solve."); break;
        }
        case "export": {
          const url = URL.createObjectURL(new Blob([JSON.stringify({ format: "candu-zone-layout-v1", referenceFill: this.session.snapshot.rrs.absorptionReferenceFillFraction, nodes: this.nodes }, null, 2)], { type: "application/json" }));
          const link = document.createElement("a"); link.href = url; link.download = "candu-zone-layout.json"; link.click(); URL.revokeObjectURL(url); break;
        }
        case "apply": void this.apply(); return;
      }
    } catch (error) { this.message(error instanceof Error ? error.message : String(error)); }
    this.render();
  };

  private paintWithBrush(): void {
    if (!this.field<HTMLInputElement>("brush").checked) return;
    try { this.paint(); } catch (error) { this.message(error instanceof Error ? error.message : String(error)); }
  }

  private async apply(): Promise<void> {
    if (this.session.isPending) return;
    this.message("Solving the draft layout…");
    try {
      const response = await this.session.dispatch({ type: "configure-zone-layout", nodes: this.nodes.map(n => ({ ...n })) }, { responseMode: "full" });
      if (response.accepted) { this.nodes = zoneBindings(this.session.snapshot)!; this.undo = []; }
      this.message(`${response.accepted ? "Applied" : "Rejected"}: ${response.message}`);
    } catch (error) { this.message(error instanceof Error ? error.message : String(error)); }
    this.render();
  }

  private render(): void {
    const slice = Number(this.value("slice")), absorber = this.value("mode") === "absorber";
    const svg = this.dialog.querySelector("svg")!;
    const focusedChannel = document.activeElement?.getAttribute("data-channel");
    svg.replaceChildren();
    const sliceNodes = new Map(this.nodes.filter(n => n.position === slice).map(n => [n.channelIndex, n]));
    for (const channel of this.session.snapshot.core.channels) {
      const node = sliceNodes.get(channel.channelIndex)!;
      const rect = document.createElementNS("http://www.w3.org/2000/svg", "rect");
      rect.setAttribute("x", String(36 + channel.gridColumn * 24)); rect.setAttribute("y", String(36 + channel.gridRow * 24));
      rect.setAttribute("width", "22"); rect.setAttribute("height", "22"); rect.setAttribute("rx", "3");
      rect.setAttribute("fill", absorber && !isAbsorbing(node) ? "#e7e9e1" : palette[absorber ? node.absorberZoneId : node.logicalZoneId]!);
      rect.setAttribute("stroke", channel.channelIndex === this.selectedChannel ? "#141f21" : "#fff");
      rect.setAttribute("stroke-width", channel.channelIndex === this.selectedChannel ? "3" : "0.5");
      rect.setAttribute("data-channel", String(channel.channelIndex)); rect.setAttribute("role", "button"); rect.setAttribute("tabindex", "0");
      const label = `Channel ${channel.channelIndex}, column ${channel.gridColumn + 1}, row ${channel.gridRow + 1}: region Z${node.logicalZoneId + 1}, absorber ${isAbsorbing(node) ? `Z${node.absorberZoneId + 1}` : "none"}`;
      rect.setAttribute("aria-label", label);
      const title = document.createElementNS("http://www.w3.org/2000/svg", "title"); title.textContent = label; rect.append(title);
      rect.addEventListener("keydown", (e) => { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); this.selectedChannel = channel.channelIndex; this.paintWithBrush(); this.render(); } });
      rect.addEventListener("pointerenter", (e) => { if (e.buttons === 1 && this.field<HTMLInputElement>("brush").checked) { this.selectedChannel = channel.channelIndex; this.paintWithBrush(); this.render(); } });
      svg.append(rect);
    }
    const node = sliceNodes.get(this.selectedChannel)!;
    this.field("cell").textContent = `Channel ${node.channelIndex} / bundle ${slice + 1}: region Z${node.logicalZoneId + 1}; absorber Z${node.absorberZoneId + 1}; slopes ${node.group1AbsorptionPerMPerFillFraction.toFixed(5)} / ${node.group2AbsorptionPerMPerFillFraction.toFixed(5)} m⁻¹ per unit fill.`;
    const count = this.nodes.filter(isAbsorbing).length;
    this.field("coverage").textContent = `Draft absorber coverage: ${count} / ${this.nodes.length} solver nodes (${(100 * count / this.nodes.length).toFixed(1)}%). Default geometry covers 100%; this is not tube volume.`;
    this.field("rack").innerHTML = this.nodes.filter(n => n.channelIndex === this.selectedChannel).map(n => `<button data-position="${n.position}" aria-label="Bundle ${n.position + 1}, region Z${n.logicalZoneId + 1}" style="border-top-color:${palette[absorber ? n.absorberZoneId : n.logicalZoneId]}">${n.position + 1}<small>Z${(absorber ? n.absorberZoneId : n.logicalZoneId) + 1}</small></button>`).join("");
    this.field<HTMLButtonElement>("undo").disabled = this.undo.length === 0;
    this.field<HTMLButtonElement>("apply").disabled = this.session.isPending || !this.session.status.isWasmAvailable;
    if (focusedChannel) svg.querySelector<SVGGraphicsElement>(`[data-channel="${focusedChannel}"]`)?.focus();
  }

  public destroy(): void { this.unsubscribe(); this.dialog.close(); this.dialog.remove(); this.onClose(); this.previousFocus?.focus(); }
}
