import type { CanduChannelSnapshot, CanduSnapshot } from "./protocol";

/** Original vector housing and locator. Published channels remain the input/state layer. */
export class ReactorFrame {
  private readonly locator: SVGGElement;
  private readonly confirmation: SVGCircleElement;
  private lastOperation: number | undefined;
  private initialized = false;
  private animation: Animation | undefined;

  constructor(private readonly svg: SVGSVGElement, private readonly arcade = false) {
    const ns = "http://www.w3.org/2000/svg", prefix = arcade ? "arcade-housing" : "studio-housing";
    const defs = document.createElementNS(ns, "defs");
    defs.innerHTML = `<linearGradient id="${prefix}-metal" x2="1" y2="1"><stop stop-color="#a2b1a6"/><stop offset=".17" stop-color="#526b62"/><stop offset=".48" stop-color="#243d3d"/><stop offset=".73" stop-color="#789184"/><stop offset="1" stop-color="#172d32"/></linearGradient><pattern id="${prefix}-grain" width="5" height="5" patternUnits="userSpaceOnUse"><path d="M0 1H5M0 3H5" stroke="#9cb3a5" stroke-opacity=".06" stroke-width=".5"/></pattern><radialGradient id="${prefix}-well"><stop stop-color="#24433a"/><stop offset=".8" stop-color="#112a27"/><stop offset="1" stop-color="#081a20"/></radialGradient>`;
    svg.prepend(defs);
    const housing = document.createElementNS(ns, "g"); housing.classList.add("reactor-housing");
    housing.setAttribute("aria-hidden", "true"); housing.setAttribute("pointer-events", "none");
    housing.innerHTML = `<circle cx="280" cy="280" r="273" fill="#030d12"/><circle cx="280" cy="280" r="269" fill="url(#${prefix}-metal)" stroke="#86a291" stroke-width="1"/><circle cx="280" cy="280" r="254" fill="url(#${prefix}-well)" stroke="#0b2024" stroke-width="3"/><circle cx="280" cy="280" r="269" fill="url(#${prefix}-grain)"/><circle cx="280" cy="280" r="259" fill="none" stroke="#bdd0b0" stroke-opacity=".3" stroke-width=".7"/>
      ${Array.from({ length: 24 }, (_, i) => { const a = i * Math.PI / 12, x = 280 + Math.cos(a) * 262, y = 280 + Math.sin(a) * 262; return `<g transform="translate(${x.toFixed(2)} ${y.toFixed(2)}) rotate(${i * 15})"><circle r="3.7" fill="#102329" stroke="#a3b7a5" stroke-width=".8"/><path d="M-2 0H2" stroke="#9bb0a0" stroke-width=".7"/></g>`; }).join("")}<path d="M9 72V488M551 72V488" stroke="#81998a" stroke-opacity=".5" stroke-width="2"/>`;
    const channels = svg.querySelector('[data-field="channels"]');
    svg.insertBefore(housing, svg.querySelector(":scope > circle") ?? channels);
    svg.querySelector<SVGCircleElement>(":scope > circle")?.style.setProperty("fill", `url(#${prefix}-well)`);
    this.locator = document.createElementNS(ns, "g"); this.locator.classList.add("reactor-locator");
    this.locator.setAttribute("aria-hidden", "true"); this.locator.setAttribute("pointer-events", "none");
    this.locator.innerHTML = '<rect x="0" y="-11" width="27" height="22" rx="4" fill="#1b3335" stroke="#d6bb83"/><path d="M7-6V6M12-6V6M17-6V6" stroke="#8b9b88"/><path d="M27-5l9 5-9 5" fill="#dfbd77"/>';
    svg.append(this.locator);
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
    if (channel) this.locator.style.transform = `translate(2px, ${position(channel).y}px)`;
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
