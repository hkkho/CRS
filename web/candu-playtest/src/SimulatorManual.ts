import "./simulatorManual.css";

const chapters = [
  ["start", "Quick start", `<h2>One day at a time</h2><ol><li>Read the core’s LZC average, tilt and power peaks.</li><li>Select a channel on the core map, or use <strong>Highest burnup</strong>. Compare its power and burnup profiles at both ends.</li><li>Select <strong>Add channel</strong> to draft an eight-bundle move. Inspect the outgoing fuel strip. Add more channels if needed.</li><li>Open <strong>Fuel plan</strong> to review the orders, then select <strong>Refuel &amp; advance</strong>. With an empty plan, <strong>Advance day</strong> advances without fuel.</li><li>Read the day result and repeat. Time stays frozen while you inspect, plan or read this manual.</li></ol><p>Free practice has unlimited fresh fuel and no time limit. The one-day challenge has finite fuel: discharge at least eight bundles at 6 MWd/kg HM or above and finish within the operating limits to earn Efficient refueller.</p><p>Highest burnup ranks eligible channels by observed average burnup. It is a candidate list, not a prediction of the best move. Compare outgoing bundles, channel power and zone headroom before choosing.</p>`],
  ["fuel", "Fuelling", `<h2>Eight in, eight out</h2><p>Each channel holds twelve axial bundle positions, numbered from End A to End B. A move inserts eight fresh bundles with the channel’s flow and discharges eight at the opposite end. The other four shift along the channel. Adjacent channels have opposite flow directions.</p><p>The preview marks outgoing positions in gold and retained positions in blue. Numbers show position and burnup in MWd/kg HM. Hover a position for its destination. Confirmed reports retain bundle identities, old and new positions, and discharge burnup.</p><p>Adding a channel uses no fuel or time. Remove it with the same button, remove an individual plan entry, or clear the whole plan. Moves execute in displayed channel order before the simulation advances twenty-four hours.</p><p>A channel containing nonfuel cells cannot be refuelled. A finite-stock plan must have eight fresh bundles available per channel.</p><h3>Choosing fuel</h3><p>Burnup measures thermal energy extracted per kilogram of heavy metal. Older fuel generally contributes less reactivity. Fresh fuel can raise local power and alter the core shape; inspect both the channel and hottest bundle. Average channel burnup alone does not describe the eight outgoing bundles.</p>`],
  ["physics", "Reactor theory", `<h2>Power, fuel and regulation</h2><dl><dt>Reactivity &amp; Keff</dt><dd>Keff describes neutron multiplication. Keff = 1 is critical equilibrium. Reactivity is ρ = (Keff − 1) / Keff; 1 mk = 0.001 in reactivity. The shared solver balances the core using two neutron energy groups and spatial fuel/device data.</dd><dt>Liquid-zone control (LZC)</dt><dd>Water in fourteen compartments absorbs neutrons. Filling reduces reactivity; draining adds it. The regulating system adjusts levels and shape. High average level leaves less fill room; low average level leaves less drain room. Individual zones can have little headroom even when the average looks comfortable. The highlighted zone has the least headroom.</dd><dt>Adjusters</dt><dd>Twenty-one fixed absorber rods depress power near their positions. Amber marks show their geometry; shaded bands show overlap with the selected channel. They are fixed in this game.</dd><dt>Iodine &amp; xenon</dt><dd>Iodine-135 decays to xenon-135. Xenon absorbs neutrons and is removed by decay and neutron absorption. Inventories follow bundle identity. Fresh bundles start with zero iodine and xenon; retained bundles carry their inventories. Advancing a day builds poison in fresh fuel and changes the solved power shape.</dd><dt>Axial tilt</dt><dd>Signed tilt measures the End A / End B power imbalance. Positive means higher power toward End B. Global tilt and local channel tilt are separate readings; the run limit applies to global tilt.</dd><dt>Power units</dt><dd>Channel and bundle kW are thermal fission power. The reference core is 2,064 MW thermal, corresponding to an authored estimate of 650 MW electrical. Burnup uses thermal energy, not electrical output.</dd></dl>`],
  ["maps", "Maps & readings", `<h2>Read the core</h2><dl><dt>Burnup</dt><dd>Fresh-to-higher exposure in MWd/kg HM. The shortlist ranks average channel burnup.</dd><dt>Channel kW / Bundle kW</dt><dd>Absolute channel power or the hottest bundle in each channel. Blue through yellow to red; red marks the operating limit. The legend states the scale.</dd><dt>Ripple %</dt><dd>Channel power as a percentage of its fixed reference. Pale is 100%; blue is low and red is high. This reference varies by channel.</dd><dt>Channel profiles</dt><dd>Power and burnup run from position 01 at End A to position 12 at End B. Open Poison &amp; device detail for iodine, xenon and absorber overlap. Power plots retain a labelled limit even when it falls outside the plotted range.</dd><dt>Devices</dt><dd>Toggle amber adjusters on the face. Switch End A / End B LZC planes to see cyan tube compartments and water levels. Geometry comes from the shared simulation.</dd><dt>History</dt><dd>The view dropdown shows power peaks/output, discharged burnup, zone levels, tilt, reactivity, poison, fuel and score. Toggle traces and choose a time window. Move View time or select a graph sample to inspect a recorded state. Return to live before changing the plan. Up to 4,096 observations are retained in this browser session; a new shift clears them.</dd></dl>`],
  ["score", "Limits & score", `<h2>Keep the run productive</h2><table><caption>Run limits — equality is allowed</caption><thead><tr><th>Reading</th><th>Allowed</th></tr></thead><tbody><tr><td>Average LZC</td><td>10–90%</td></tr><tr><td>Global axial tilt</td><td>−20 to +20%</td></tr><tr><td>Channel power</td><td>≤ 7,300 kW</td></tr><tr><td>Bundle power</td><td>≤ 935 kW</td></tr></tbody></table><p>Limits are checked after each fuel move and at the day boundary. A violating move ends the run and stops remaining orders. A day-end loss retains the completed day. An invalid plan or numerical failure consumes no fuel or time; an interrupted calculation may have an unknown outcome, which is reported separately.</p><h3>Ripple points</h3><p>Each channel has a fixed time-average power reference. The score uses RMS relative deviation from those references: points/hour = 1 / (1 + (RMS / 0.10)²). Smaller ripple earns more, up to one point per simulated hour. Fuel moves have no direct bonus or cost; their later power response changes points earned.</p><p>The day report records executed and unexecuted orders, fuel, useful discharges, energy, earned points and any ending reason. After a loss, Retry same seed repeats the starting core and objective; Try new seed draws another core.</p>`],
  ["controls", "Controls & saves", `<h2>Controls</h2><table><tbody><tr><th>Tab / Shift+Tab</th><td>Move between controls</td></tr><tr><th>Enter / Space</th><td>Activate a focused control</td></tr><tr><th>Arrow keys</th><td>Move across channels when the map has focus; switch views inside the open view menu</td></tr><tr><th>N</th><td>Select highest burnup</td></tr><tr><th>R</th><td>Add / remove selected channel</td></tr><tr><th>F1</th><td>Open manual</td></tr><tr><th>Escape</th><td>Close manual</td></tr></tbody></table><h3>Saves &amp; starting cores</h3><p>Menu → Manual opens this guide. Saves · Stats · Login opens local saves, optional GitHub login, private cloud saves and the endless leaderboard. Guest/offline play is available. Daily saves include the unfinished fuel plan. Older real-time saves cannot resume in the public daily game.</p><p>The same seed recreates the same aged core. New shift draws a new seed; retry preserves the current seed and objective. A new run clears history, reports and badges.</p><p>The loaded build and physics version are shown in the update strip. Save locally &amp; update preserves a local save before loading an available update. Close other game tabs first; keep browser site data to retain local saves.</p>`],
  ["model", "Simulation model", `<h2>What each day calculates</h2><p>The browser presents one authoritative shared simulation. It does not calculate its own reactor rules. Daily planning freezes the state. After refuelling, one large twenty-four-hour step updates fuel burnup, energy, score and analytic poison using the post-refuelling power/flux. A final equilibrium and LZC solve produces the next day’s readings. Intermediate intraday states are not calculated.</p><p>The current axial model uses zero incoming partial current at both reactor ends (Marshak boundaries). End-bundle power falls sharply. Authored compact physics packs provide fuel coefficients and device geometry; external analysis programs are not runtime dependencies.</p><p>These are project-authored approximations for plausible gameplay, rather than plant-grade operating predictions. The simulator focuses on on-power refuelling; shutdown, scram, accidents and full plant operations are outside its scope.</p><p>Engineering edits mark a modified sandbox, excluding standard challenge rewards until a new standard run starts.</p>`],
] as const;

/** On-demand documentation only; opening it never changes the shared session. */
export class SimulatorManual {
  readonly element = document.createElement("dialog");
  private opener: HTMLElement | null = null;
  constructor(private readonly host: HTMLElement) {
    this.element.className = "simulator-manual";
    this.element.setAttribute("aria-label", "Fuelling simulator manual");
    this.element.innerHTML = `<header><div><small>CANDU / REFERENCE</small><h1>Simulator manual</h1></div><button type="button" data-manual-close aria-label="Close manual">Close ×</button></header><div class="manual-layout"><nav aria-label="Manual chapters">${chapters.map(([id, title], i) => `<button type="button" data-manual-chapter="${id}" aria-pressed="${i === 0}" aria-controls="manual-${id}">${title}</button>`).join("")}</nav><div class="manual-pages">${chapters.map(([id, title, content], i) => `<section id="manual-${id}" tabindex="0" aria-label="${title}" ${i ? "hidden" : ""}>${content}</section>`).join("")}</div></div>`;
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
