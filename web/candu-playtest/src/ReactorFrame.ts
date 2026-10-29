import type { CanduChannelSnapshot, CanduSnapshot } from "./protocol";

/** Original vector housing and selection frame. Published channels remain the input/state layer. */
export class ReactorFrame {
  private readonly confirmation: SVGCircleElement;
  private readonly selection: SVGGElement;
  private lastOperation: number | undefined;
  private initialized = false;
  private animation: Animation | undefined;

  constructor(private readonly svg: SVGSVGElement, private readonly arcade = false) {
    const ns = "http://www.w3.org/2000/svg", prefix = arcade ? "arcade-housing" : "studio-housing";
    const defs = document.createElementNS(ns, "defs");
    defs.innerHTML = `<linearGradient id="${prefix}-metal" x2=".7" y2="1"><stop stop-color="#d3d0ad"/><stop offset=".16" stop-color="#809385"/><stop offset=".4" stop-color="#344b4f"/><stop offset=".7" stop-color="#b2baa0"/><stop offset="1" stop-color="#385153"/></linearGradient><pattern id="${prefix}-grain" width="4" height="4" patternUnits="userSpaceOnUse"><path d="M0 1H4" stroke="#e2e7ca" stroke-opacity=".1" stroke-width=".6"/></pattern><radialGradient id="${prefix}-well" cx=".4" cy=".3"><stop stop-color="#334b46"/><stop offset=".8" stop-color="#1b3437"/><stop offset="1" stop-color="#10262e"/></radialGradient>`;
    svg.prepend(defs);
    const housing = document.createElementNS(ns, "g"); housing.classList.add("reactor-housing");
    housing.setAttribute("aria-hidden", "true"); housing.setAttribute("pointer-events", "none");
    housing.innerHTML = `      <circle cx="282" cy="284" r="273" fill="#04151d"/><circle cx="280" cy="280" r="269" fill="url(#${prefix}-metal)" stroke="#c3c9a4" stroke-width="1"/><circle cx="280" cy="280" r="254" fill="url(#${prefix}-well)" stroke="#0b2024" stroke-width="4"/><circle cx="280" cy="280" r="269" fill="url(#${prefix}-grain)"/><circle cx="280" cy="280" r="259" fill="none" stroke="#e6e2b3" stroke-opacity=".4" stroke-width=".8"/>
      ${Array.from({ length: 24 }, (_, i) => { const a = i * Math.PI / 12, x = 280 + Math.cos(a) * 262, y = 280 + Math.sin(a) * 262; return `<g transform="translate(${x.toFixed(2)} ${y.toFixed(2)}) rotate(${i * 15})"><path d="M-4-2l4-2 4 2v4l-4 2-4-2z" fill="#20353a" stroke="#c9cdae" stroke-width=".8"/><path d="M-2 0H2" stroke="#a4b396" stroke-width=".7"/></g>`; }).join("")}`;
    const channels = svg.querySelector('[data-field="channels"]');
    svg.insertBefore(housing, svg.querySelector(":scope > circle") ?? channels);
    svg.querySelector<SVGCircleElement>(":scope > circle")?.style.setProperty("fill", `url(#${prefix}-well)`);
    this.selection = document.createElementNS(ns, "g"); this.selection.classList.add("reactor-selection");
    this.selection.setAttribute("aria-hidden", "true"); this.selection.setAttribute("pointer-events", "none");
    this.selection.innerHTML = '<path d="M-6-13h-7v7M6-13h7v7M13 6v7H6M-13 6v7h7" fill="none" stroke="#fff1be" stroke-width="1.8"/>';
    svg.append(this.selection);
    this.confirmation = document.createElementNS(ns, "circle");
    this.confirmation.classList.add("reactor-confirmed-ring"); this.confirmation.setAttribute("r", arcade ? "14" : "11");
    this.confirmation.setAttribute("aria-hidden", "true"); this.confirmation.setAttribute("pointer-events", "none");
    this.confirmation.style.display = "none"; svg.append(this.confirmation);
  }

  sockets(channels: readonly CanduChannelSnapshot[]): void {
    if (this.arcade) return;
    const group = document.createElementNS("http://www.w3.org/2000/svg", "g");
    group.classList.add("reactor-sockets"); group.setAttribute("aria-hidden", "true"); group.setAttribute("pointer-events", "none");
    group.innerHTML = channels.map(c => `<g transform="translate(${70 + c.gridColumn * 20} ${70 + c.gridRow * 20})"><circle r="5.2" fill="#071b1db0" stroke="#e7edc63d" stroke-width=".8"/><circle r="2.5" fill="none" stroke="#a5bfa15c" stroke-width=".5"/><path d="M-3-1H3M-3 1H3" stroke="#0d2423" stroke-width=".8"/></g>`).join("");
    this.svg.querySelector('[data-field="channels"]')!.after(group);
  }

  update(snapshot: CanduSnapshot, selected: number): void {
    const channel = snapshot.core.channels.find(c => c.channelIndex === selected);
    const position = (c: CanduChannelSnapshot) => this.arcade
      ? { x: (c.gridColumn + 1.468) / 24 * 560, y: (c.gridRow + 1.468) / 24 * 560 }
      : { x: 70 + c.gridColumn * 20, y: 70 + c.gridRow * 20 };
    this.selection.style.display = channel ? "" : "none";
    if (channel) {
      const { x, y } = position(channel);
      this.selection.setAttribute("transform", `translate(${x} ${y})`);
    }
    const operation = snapshot.lastFuelMovement?.operationId;
    const moved = snapshot.core.channels.find(c => c.channelIndex === snapshot.lastFuelMovement?.channelIndex);
    this.confirmation.style.display = moved ? "" : "none";
    if (moved) {
      this.confirmation.setAttribute("cx", String(position(moved).x)); this.confirmation.setAttribute("cy", String(position(moved).y));
      if (this.initialized && operation !== this.lastOperation && !window.matchMedia?.("(prefers-reduced-motion: reduce)").matches) {
        this.animation?.cancel();
        this.animation = this.confirmation.animate?.([{ opacity: 1, strokeWidth: 5 }, { opacity: .6, strokeWidth: 1 }], { duration: 1000 });
      }
    }
    this.lastOperation = operation; this.initialized = true;
  }
  destroy(): void { this.animation?.cancel(); }
}
