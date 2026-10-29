import { isRunTerminal, type CanduCommand, type CanduSnapshot } from "./protocol";
import type { CanduPlaytestBridgeLifecycle } from "./bridge";

export const DEMO_DAY_MS = 5_000;
const MAX_WAITING_FUEL = 8;

/** One worker operation at a time. Coalesce missed deadlines, never replay a backlog.
 * Last solved powers remain on screen while the shared warm-started solver runs.
 * This class schedules commands only; it never predicts reactor physics locally.
 */
export class DemoLoop {
  snapshot: CanduSnapshot;
  running = true;
  visible = true;
  busy = false;
  message = "Preparing the core…";
  readonly waitingFuel = new Set<number>();
  private nextDayAt = 0;
  private ready = false;
  private disposed = false;
  private lastWasFuel = false;
  private readonly listeners = new Set<() => void>();
  private readonly unsubscribe: () => void;
  private readonly timer: ReturnType<typeof setInterval>;

  constructor(private readonly bridge: CanduPlaytestBridgeLifecycle, private readonly now = () => performance.now()) {
    this.snapshot = bridge.getSnapshot();
    this.unsubscribe = bridge.subscribe((status, snapshot) => {
      if (this.disposed) return;
      if (status.isWasmAvailable && snapshot.pacingMode === "demo") {
        this.snapshot = snapshot;
        if (!this.ready) { this.ready = true; this.nextDayAt = this.now() + DEMO_DAY_MS; this.message = "Double-click a channel to fuel it."; }
      } else if (status.source !== "loading") { this.ready = false; this.running = false; this.message = status.detail; }
      this.emit();
    });
    if (bridge.status.isWasmAvailable && this.snapshot.pacingMode === "demo") {
      this.ready = true; this.nextDayAt = this.now() + DEMO_DAY_MS;
      this.message = "Double-click a channel to fuel it.";
    }
    this.timer = setInterval(() => this.pump(), 100);
  }

  get isReady(): boolean { return this.ready; }
  get dayProgress(): number { return this.ready ? Math.max(0, Math.min(1, 1 - (this.nextDayAt - this.now()) / DEMO_DAY_MS)) : 0; }
  subscribe(listener: () => void): () => void { this.listeners.add(listener); listener(); return () => this.listeners.delete(listener); }

  fuel(index: number): void {
    if (!this.ready || !this.running || !this.visible || this.disposed || isRunTerminal(this.snapshot)) return;
    const channel = this.snapshot.core.channels.find(c => c.channelIndex === index);
    if (!channel || channel.canRefuel === false) { this.message = channel?.refuellingIneligibilityReason || "This channel cannot be fuelled."; this.emit(); return; }
    if (this.waitingFuel.has(index)) return;
    if (this.waitingFuel.size >= MAX_WAITING_FUEL) { this.message = "Fuel requests are full. Wait for a power update."; this.emit(); return; }
    this.waitingFuel.add(index);
    this.message = this.busy ? "Fuel requested · waiting for the current power update." : "Fuel requested.";
    this.emit(); this.pump();
  }

  togglePause(): void {
    this.running = !this.running;
    this.nextDayAt = this.now() + DEMO_DAY_MS;
    if (!this.running) this.waitingFuel.clear();
    this.message = this.running ? "Live · one day every 5 seconds." : "Paused. Any active solve will finish; waiting fuel requests cleared.";
    this.emit();
  }

  setVisible(visible: boolean): void {
    this.visible = visible; this.nextDayAt = this.now() + DEMO_DAY_MS;
    if (!visible) this.waitingFuel.clear();
    this.emit();
  }

  async restart(seed: number): Promise<void> {
    if (this.busy || !this.ready || this.disposed) return;
    this.waitingFuel.clear(); this.running = false;
    const accepted = await this.execute({ type: "reset", seed, shiftId: "free-practice", pacingMode: "demo" });
    if (this.disposed) return;
    this.running = accepted === true && !isRunTerminal(this.snapshot) && this.snapshot.simulationTimeSeconds === 0;
    this.nextDayAt = this.now() + DEMO_DAY_MS; this.emit();
  }

  private pump(): void {
    if (this.disposed || !this.ready || !this.running || !this.visible || isRunTerminal(this.snapshot)) return;
    this.emit();
    if (this.busy) return;
    const dayDue = this.now() >= this.nextDayAt;
    const index = this.waitingFuel.values().next().value as number | undefined;
    // A fuel burst cannot starve the clock. Each overdue day gets at most one
    // preceding fuel move; otherwise user input has priority over clock work.
    if (index !== undefined && (!dayDue || !this.lastWasFuel)) {
      const channel = this.snapshot.core.channels.find(c => c.channelIndex === index)!;
      this.lastWasFuel = true;
      void this.execute({ type: "commit-refuel", request: { channelIndex: index, directionId: channel.flowDirection, shiftCount: 8, fuelTypeId: "NAT-U-SYNTHETIC" } }, index);
    } else if (dayDue) {
      this.nextDayAt = this.now() + DEMO_DAY_MS;
      this.lastWasFuel = false;
      void this.execute({ type: "commit-day", expectedCompletedDays: this.snapshot.completedDays ?? 0, channelIndices: [] });
    }
  }

  private async execute(command: CanduCommand, channel?: number): Promise<boolean | undefined> {
    this.busy = true; this.emit();
    try {
      const result = await this.bridge.dispatch(command, { responseMode: "compact" });
      if (this.disposed) return;
      this.snapshot = result.snapshot;
      this.message = result.accepted ? command.type === "commit-refuel" ? "Fuel delivered · power updated." : "Power updated." : result.message;
      if (!result.accepted || isRunTerminal(this.snapshot)) { this.running = false; this.waitingFuel.clear(); }
      return result.accepted;
    } catch (error) {
      if (this.disposed) return;
      this.running = false; this.waitingFuel.clear(); this.message = String(error);
    } finally {
      this.busy = false;
      if (channel !== undefined) this.waitingFuel.delete(channel);
      if (!this.disposed) this.emit();
    }
  }

  private emit(): void { for (const listener of this.listeners) listener(); }
  destroy(): void { this.disposed = true; clearInterval(this.timer); this.unsubscribe(); this.bridge.dispose?.(); this.listeners.clear(); }
}
