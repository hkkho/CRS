import { gridCoordinateLabel } from "./channelCoordinates";
import { exitBurnup } from "./CoreMapInteraction";
import type { DemoLoop } from "./DemoLoop";
import { isRunTerminal, type CanduChannelSnapshot, type CanduSnapshot } from "./protocol";
import { bundleLimit, channelLimit, channelWatts } from "./studio/powerReadings";
import { timeMapCell, type TimeMapMode } from "./timeMapPresentation";
import "./demoTutorial.css";

export const TUTORIAL_SEED = 1001;
const candidates = [210, 211] as const;
const lessons: readonly { title: string; map: TimeMapMode; text: string }[] = [
  { title: "Read channel and bundle power", map: "power",
    text: "Select a channel on the map or use the arrow keys. Channel power adds the heat from its twelve bundles. The peak bundle is the hottest one. Compare both readings because a channel can have room below its limit while one bundle is already hot." },
  { title: "Read ripple against the reference", map: "ripple",
    text: "Ripple compares a channel’s power with its fixed time-average reference. At 100% it matches that reference. A channel below its reference may have room for fresh fuel, but check its absolute power and its neighbours too. The game awards operating points for keeping the highest ripple low." },
  { title: "Follow the outgoing fuel", map: "burnup",
    text: "Burnup is accumulated thermal energy per kilogram of heavy metal. The map averages all twelve bundles. A move pushes eight bundles out in the channel’s flow direction and shifts the four retained bundles to new positions. Use the outgoing average to compare candidates. Each outgoing bundle scores its burnup in MWd/kg minus 6." },
  { title: "Read liquid-zone headroom", map: "inlet-zones",
    text: "Each map cell shows the inlet zone. Flow alternates between neighbours, and six of the eight fresh bundles go into the inlet half. The regulator varies water fill to absorb neutrons. Look for room to add water near the fresh fuel and check the core average. The trends show both ends." },
  { title: "Read tilt, multiplication and poison", map: "power",
    text: "Global axial tilt shows which end carries more thermal flux. Positive means End B and negative means End A. Keff measures neutron multiplication. At 1 the neutron population is steady. Iodine decays into neutron-absorbing xenon, which changes reactivity and the power distribution as its concentration changes." },
  { title: "Check all four operating limits", map: "power",
    text: "Check the whole core before committing. The game checks the solved fuel batch and the half-day endpoint. Equality at a limit is allowed. A solved state outside a limit ends the run, and a numerical rejection leaves the previous state available for inspection." },
  { title: "Compare two channels and refuel one", map: "burnup",
    text: "Look for high outgoing burnup with room below the power limits. Check ripple and inlet zone fill. Fresh fuel can raise power in nearby channels too. Engineers and physicists use core models to assess predicted power and burnup distributions against plant limits. Compare the current readings below, queue one channel and advance 12 hours." },
  { title: "Review the solved result", map: "power",
    text: "Compare the result with the starting readings. Trends plot selected-channel power and zone fills over simulated time. The score includes outgoing burnup minus fuel cost, the zone balance after refuelling, and operating points from ripple and tilt. In regular play, each half-day takes 7.5 seconds at normal speed. Pause to inspect the core." },
];
const exercise = lessons.length - 2;
const result = lessons.length - 1;
const pct = (value: number) => `${(value * 100).toFixed(1)}%`;
const kw = (watts: number) => `${Math.round(watts / 1000).toLocaleString("en-US")} kW`;
const peakBundle = (channel: CanduChannelSnapshot) => Math.max(0, ...channel.bundles.map(bundle => bundle.powerWatts));
const channelAt = (snapshot: CanduSnapshot, index: number) => snapshot.core.channels.find(c => c.channelIndex === index);

/** A presentation-only lesson sequence over the same commands and readings as time-based play. */
export class DemoTutorialView {
  readonly element = document.createElement("section");
  private readonly heading = document.createElement("h2");
  private readonly readings = document.createElement("p");
  private readonly previousResult = document.createElement("p");
  private readonly instruction = document.createElement("p");
  private readonly caption = document.createElement("p");
  private readonly status = document.createElement("p");
  private readonly examples = document.createElement("div");
  private readonly previous = document.createElement("button");
  private readonly next = document.createElement("button");
  private readonly remove = document.createElement("button");
  private readonly retry = document.createElement("button");
  private step = 0;
  private startingSnapshot?: CanduSnapshot;

  constructor(private readonly loop: DemoLoop, private readonly fuel: HTMLButtonElement, private readonly showMap: (map: TimeMapMode) => void,
    private readonly select: (index: number) => void, private readonly finish: () => void) {
    this.element.className = "demo-tutorial";
    this.element.hidden = true;
    this.fuel.hidden = true;
    this.element.setAttribute("aria-labelledby", "demo-tutorial-heading");
    this.heading.id = "demo-tutorial-heading";
    this.heading.tabIndex = -1;
    this.caption.className = "demo-tutorial-kicker";
    this.readings.className = "demo-tutorial-readings";
    this.previousResult.className = "demo-tutorial-readings";
    this.previousResult.hidden = true;
    this.status.setAttribute("role", "status");
    this.status.className = "demo-tutorial-status";
    this.examples.className = "demo-tutorial-examples";
    const controls = document.createElement("div");
    controls.className = "demo-tutorial-controls";
    this.next.className = "arcade-primary";
    this.previous.textContent = "Back a lesson";
    this.remove.textContent = "Remove order";
    this.previous.onclick = () => this.openLesson(this.step - 1);
    this.remove.onclick = () => {
      const [index] = this.loop.waitingFuel;
      if (index !== undefined) this.loop.removeFuel(index);
    };
    this.retry.onclick = () => { void this.restart(); };
    this.next.onclick = () => {
      if (this.step === result) this.finish();
      else if (this.step === exercise && !this.loop.snapshot.lastDayResult) {
        this.startingSnapshot = this.loop.snapshot;
        this.loop.stepOnce();
      } else this.openLesson(this.step + 1);
    };
    for (const button of [this.previous, this.remove, this.retry, this.next]) button.type = "button";
    controls.append(this.previous, this.fuel, this.remove, this.retry, this.next);
    const source = document.createElement("a");
    source.textContent = "CANDU fuel management reference";
    source.href = "https://www-pub.iaea.org/MTCD/Publications/PDF/TE-1926web.pdf";
    source.target = "_blank";
    source.rel = "noopener";
    this.element.append(this.caption, this.heading, this.instruction, this.readings, this.previousResult, this.examples, this.status, controls, source);
    this.examples.append(...candidates.map(index => {
      const card = document.createElement("div");
      const inspect = document.createElement("button");
      inspect.type = "button";
      inspect.onclick = () => this.select(index);
      card.append(inspect, document.createElement("p"));
      return card;
    }));
  }

  begin(): void {
    this.element.hidden = false;
    this.select(candidates[0]);
    this.openLesson(0);
  }

  canQueue(index: number): boolean {
    return this.step === exercise && candidates.some(candidate => candidate === index);
  }

  update(selected: number): void {
    if (this.element.hidden) return;
    const snapshot = this.loop.snapshot;
    const channel = channelAt(snapshot, selected);
    const solved = snapshot.lastDayResult;
    const [waitingIndex] = this.loop.waitingFuel;
    const waiting = waitingIndex !== undefined;
    const queuedChannel = waiting ? channelAt(snapshot, waitingIndex) : undefined;
    const busy = this.loop.busy;
    const failed = this.loop.loadError;
    const practice = this.step === exercise;
    this.fuel.hidden = !practice || !!solved;
    this.readings.textContent = this.describe(snapshot, channel);
    this.previousResult.hidden = this.step !== result || !this.previousResult.textContent;
    this.examples.hidden = !practice || !!solved;
    if (!this.examples.hidden) candidates.forEach((index, i) => {
      const candidate = channelAt(snapshot, index);
      if (!candidate) return;
      const card = this.examples.children[i];
      const inspect = card.querySelector("button")!;
      inspect.textContent = `Inspect ${gridCoordinateLabel(candidate)}`;
      inspect.setAttribute("aria-pressed", String(selected === index));
      const ripple = snapshot.ripple?.channelRippleFractions[index];
      const exit = exitBurnup(snapshot, candidate);
      card.querySelector("p")!.textContent =
        `${kw(channelWatts(snapshot, candidate))} channel · ${kw(peakBundle(candidate))} peak bundle · ${ripple === undefined ? "—" : pct(ripple)} ripple · ${exit?.toFixed(2) ?? "—"} MWd/kg outgoing. ${timeMapCell(snapshot, candidate, "inlet-zones").reading}.`;
    });
    this.previous.hidden = this.step === 0 || !!solved;
    this.previous.disabled = busy || waiting;
    this.remove.hidden = !practice || !waiting;
    this.remove.disabled = busy;
    this.retry.hidden = !failed && this.step !== result;
    this.retry.disabled = busy;
    this.retry.textContent = this.step === result ? "Try the other channel" : "Retry from the same core";
    this.next.disabled = busy || !!failed || (practice && !solved && !this.loop.canStep);
    this.next.textContent = practice ? solved ? "See result" : "Advance 12 hours" : this.step === result ? "Finish tutorial" : "Next lesson";
    const message = busy ? "Waiting for the simulator…" : failed ?? (practice
      ? solved ? "The solved result is ready." : queuedChannel ? `${gridCoordinateLabel(queuedChannel)} queued. Advance when ready or remove the order to change your choice.`
        : "Choose one of the two example channels, then press Queue fuel." : "");
    if (this.status.textContent !== message) this.status.textContent = message;
  }

  private openLesson(step: number): void {
    this.step = step;
    const lesson = lessons[step];
    this.caption.textContent = `LESSON ${step + 1} / ${lessons.length} · SEED ${TUTORIAL_SEED}`;
    this.heading.textContent = lesson.title;
    this.instruction.textContent = lesson.text;
    this.showMap(lesson.map);
    this.heading.focus();
  }

  private async restart(): Promise<void> {
    const moved = this.loop.snapshot.lastDayResult?.movements[0]?.channelIndex;
    const comparison = this.step === result ? this.readings.textContent : undefined;
    if (!await this.loop.restart(TUTORIAL_SEED) || !this.element.isConnected) return;
    if (comparison) this.previousResult.textContent = `Previous choice · ${comparison}`;
    this.select(moved === candidates[0] ? candidates[1] : candidates[0]);
    this.openLesson(exercise);
  }

  private describe(snapshot: CanduSnapshot, channel?: CanduChannelSnapshot): string {
    if (!channel) return "";
    const label = gridCoordinateLabel(channel);
    switch (this.step) {
      case 0: return `${label} · ${kw(channelWatts(snapshot, channel))} channel · ${kw(peakBundle(channel))} peak bundle. Core thermal output ${(snapshot.physics.totalPowerWatts / 1e6).toFixed(0)} MW.`;
      case 1: {
        const ripple = snapshot.ripple?.channelRippleFractions[channel.channelIndex];
        const reference = snapshot.ripple?.referenceChannelPowerWatts[channel.channelIndex];
        return `${label} · ${ripple === undefined ? "—" : pct(ripple)} of ${reference === undefined ? "—" : kw(reference)} reference. ${kw(channelWatts(snapshot, channel))} now.`;
      }
      case 2: {
        const outgoing = exitBurnup(snapshot, channel);
        const plan = snapshot.refuellingPlans?.find(p => p.shiftCount === 8 && p.directionId === channel.flowDirection);
        return `${label} · ${channel.averageBurnupMwdPerKg.toFixed(2)} MWd/kg across all bundles · ${outgoing?.toFixed(2) ?? "—"} MWd/kg outgoing${plan ? ` at End ${plan.outgoingEnd}` : ""}.`;
      }
      case 3: return `${label} · ${timeMapCell(snapshot, channel, "inlet-zones").reading}. Core average ${pct(snapshot.rrs.averageFillFraction)} · individual fills ${pct(snapshot.rrs.minimumFillFraction)} to ${pct(snapshot.rrs.maximumFillFraction)}.`;
      case 4: return `Core tilt ${pct(snapshot.axialTiltFraction)} · Keff ${snapshot.physics.effectiveK.toFixed(6)}. ${label} average iodine ${channel.xenon.meanI135NumberDensityM3.toExponential(2)} and xenon ${channel.xenon.meanXe135NumberDensityM3.toExponential(2)} atoms/m³.`;
      case 5: return `Core average fill ${pct(snapshot.rrs.averageFillFraction)} (10–90%) · tilt ${pct(snapshot.axialTiltFraction)} (−20% to +20%) · peak channel ${kw(Math.max(...snapshot.core.channels.map(c => channelWatts(snapshot, c))))} (limit ${kw(channelLimit(snapshot))}) · peak bundle ${kw(Math.max(...snapshot.core.channels.map(peakBundle)))} (limit ${kw(bundleLimit(snapshot))}).`;
      case result: {
        const step = snapshot.lastDayResult;
        const movement = step?.movements[0];
        if (!step || !movement) return snapshot.runEndReason || "No fuel move was committed.";
        const moved = channelAt(snapshot, movement.channelIndex);
        const before = this.startingSnapshot;
        const oldChannel = before && channelAt(before, movement.channelIndex);
        const burnups = movement.bundles.filter(bundle => bundle.afterPosition == null).map(bundle => bundle.burnupMwdPerKg);
        const outgoing = burnups.length ? (burnups.reduce((sum, burnup) => sum + burnup, 0) / burnups.length).toFixed(2) : "—";
        return `${moved ? gridCoordinateLabel(moved) : "Selected channel"} · ${step.fuelUsed} bundles delivered · ${outgoing} MWd/kg outgoing. Channel power ${oldChannel && before ? kw(channelWatts(before, oldChannel)) : "—"} → ${moved ? kw(channelWatts(snapshot, moved)) : "—"}. Average fill ${before ? pct(before.rrs.averageFillFraction) : "—"} → ${pct(snapshot.rrs.averageFillFraction)}. Step score ${step.scoreDelta.toFixed(2)}. ${isRunTerminal(snapshot) ? snapshot.runEndReason : "Core remains in play."}`;
      }
      default: return `Selected ${label}.`;
    }
  }
}
