import { isRunTerminal, type CanduSnapshot } from "./protocol";
import type { CanduPlaytestBridgeLifecycle } from "./bridge";
import { interpolateDemo } from "./demoPresentation";

export const DEMO_STEP_MS = 5_000;
const MAX_WAITING_FUEL = 8;
type RecoveryBridge = CanduPlaytestBridgeLifecycle & { whenReady(): Promise<CanduSnapshot> };
type Step = { snapshot: CanduSnapshot; fuel: number[] };

/** One solved endpoint ahead of the display, at most one worker operation.
 * Interpolation is presentation only; every time step and fuel move belongs to Game.
 */
export class DemoLoop {
  snapshot: CanduSnapshot;
  running = true;
  visible = true;
  busy = false;
  message = "Preparing the core…";
  readonly waitingFuel = new Set<number>();
  private solved: CanduSnapshot;
  private from: CanduSnapshot;
  private active?: Step;
  private buffered?: Step;
  private readonly pending = new Set<number>();
  private elapsed = 0;
  private lastTime: number;
  private ready = false;
  private faulted = false;
  private resetting = false;
  private disposed = false;
  private readonly listeners = new Set<() => void>();
  private unsubscribe: () => void;
  private readonly timer: ReturnType<typeof setInterval>;

  constructor(private bridge: CanduPlaytestBridgeLifecycle, private readonly now = () => performance.now(), startPaused = false,
    private readonly createRecoveryBridge?: (seed: number) => RecoveryBridge) {
    this.running = !startPaused;
    this.snapshot = this.solved = this.from = bridge.getSnapshot();
    this.lastTime = now();
    this.unsubscribe = this.watch(bridge);
    if (!this.ready && bridge.status.isWasmAvailable && this.snapshot.pacingMode === "demo") this.initialize(this.snapshot);
    this.timer = setInterval(() => this.pump(), 50);
  }

  private watch(bridge: CanduPlaytestBridgeLifecycle): () => void {
    return bridge.subscribe((status, snapshot) => {
      if (this.disposed) return;
      // Dispatch subscriptions must never leak the ahead-of-display endpoint.
      if (!this.ready && status.isWasmAvailable && snapshot.pacingMode === "demo") this.initialize(snapshot);
      else if (!status.isWasmAvailable && status.source !== "loading") {
        this.running = false; this.faulted = true; this.message = status.detail;
      }
      this.emit();
    });
  }

  private initialize(snapshot: CanduSnapshot): void {
    this.snapshot = this.solved = this.from = snapshot;
    this.active = this.buffered = undefined; this.elapsed = 0; this.lastTime = this.now();
    this.ready = true; this.faulted = false; this.message = "Double-click to queue fuel for the next step.";
  }
  get isReady(): boolean { return this.ready; }
  get loadError(): string | undefined { return this.faulted ? this.message : undefined; }
  get dayProgress(): number { return this.elapsed / DEMO_STEP_MS; }
  get playbackDay(): number {
    const start = this.from.simulationTimeSeconds / 86400;
    return this.active ? start + (this.active.snapshot.simulationTimeSeconds / 86400 - start) * this.dayProgress : this.snapshot.simulationTimeSeconds / 86400;
  }
  get pendingFuel(): number[] { return [...this.pending]; }
  get canTogglePause(): boolean { return this.ready && !this.resetting && !this.faulted && !isRunTerminal(this.snapshot); }
  get canFuel(): boolean { return this.ready && !this.resetting && this.running && this.visible && !isRunTerminal(this.solved); }
  subscribe(listener: () => void): () => void { this.listeners.add(listener); listener(); return () => this.listeners.delete(listener); }

  fuel(index: number): void {
    if (!this.canFuel || this.disposed) return;
    const channel = this.solved.core.channels.find(c => c.channelIndex === index);
    if (!channel || channel.canRefuel === false) { this.message = channel?.refuellingIneligibilityReason || "This channel cannot be fuelled."; this.emit(); return; }
    if (this.pending.has(index)) return;
    if (this.waitingFuel.size >= MAX_WAITING_FUEL) { this.message = "Fuel queue is full (8 channels)."; this.emit(); return; }
    this.waitingFuel.add(index); this.pending.add(index);
    this.message = `${this.waitingFuel.size} channel${this.waitingFuel.size === 1 ? "" : "s"} queued for the next step.`;
    this.emit();
  }

  togglePause(): void {
    if (!this.canTogglePause) return;
    this.running = !this.running; this.lastTime = this.now();
    this.message = this.running ? "Double-click to queue fuel for the next step." : "Paused · fuel queue retained.";
    this.emit();
  }
  setVisible(visible: boolean): void { this.visible = visible; this.lastTime = this.now(); this.emit(); }

  async restart(seed: number): Promise<void> {
    if (this.busy || !this.ready || this.disposed) return;
    this.running = false; this.busy = this.resetting = true; this.emit();
    try {
      let snapshot: CanduSnapshot;
      if (!this.bridge.status.isWasmAvailable && this.createRecoveryBridge) {
        const replacement = this.createRecoveryBridge(seed);
        this.unsubscribe();
        this.bridge.dispose?.();
        this.bridge = replacement;
        this.unsubscribe = this.watch(replacement);
        snapshot = await replacement.whenReady();
      } else {
        const result = await this.bridge.dispatch({ type: "reset", seed, shiftId: "free-practice", pacingMode: "demo" }, { responseMode: "compact" });
        if (this.disposed) return;
        if (!result.accepted) { this.message = result.message; return; }
        snapshot = result.snapshot;
      }
      if (this.disposed) return;
      this.waitingFuel.clear(); this.pending.clear(); this.initialize(snapshot);
      this.running = !isRunTerminal(snapshot);
    } catch (error) { if (!this.disposed) this.message = String(error); }
    finally { this.busy = this.resetting = false; if (!this.disposed) this.emit(); }
  }

  private pump(): void {
    const now = this.now(), delta = Math.max(0, now - this.lastTime); this.lastTime = now;
    if (this.disposed || !this.ready || !this.running || !this.visible) return;
    if (this.active) {
      this.elapsed = Math.min(DEMO_STEP_MS, this.elapsed + delta);
      this.snapshot = interpolateDemo(this.from, this.active.snapshot, this.dayProgress);
      if (this.elapsed === DEMO_STEP_MS) {
        for (const index of this.active.fuel) this.pending.delete(index);
        this.message = this.pending.size ? `${this.pending.size} fuel requests queued for the next step.` : "Double-click to queue fuel for the next step.";
        this.from = this.snapshot; this.active = undefined;
        if (isRunTerminal(this.snapshot)) {
          this.running = false; this.waitingFuel.clear(); this.pending.clear();
          this.message = "Run ended. Retry the seed or start a new core."; this.emit(); return;
        }
      }
    }
    if (!this.active && this.buffered) {
      this.active = this.buffered; this.buffered = undefined; this.elapsed = 0;
    }
    // Compute the next time step while the current solved pair is displayed.
    // A slow solve holds the endpoint; no extrapolation or catch-up burst.
    if (!this.busy && !this.buffered && !isRunTerminal(this.solved)) void this.advance();
    this.emit();
  }

  private async advance(): Promise<void> {
    // Capture the entire waiting batch once. Input arriving during this solve
    // belongs to the following step and cannot mutate an endpoint in playback.
    const fuel = [...this.waitingFuel];
    this.waitingFuel.clear();
    this.busy = true;
    try {
      const result = await this.bridge.dispatch({ type: "commit-day", expectedCompletedDays: this.solved.completedDays ?? 0, channelIndices: fuel }, { responseMode: "compact" });
      if (this.disposed) return;
      if (result.accepted) {
        this.solved = result.snapshot; this.buffered = { snapshot: result.snapshot, fuel };
      } else {
        for (const index of fuel) this.pending.delete(index);
        this.running = false; this.faulted = true; this.message = result.message;
      }
    } catch (error) {
      if (!this.disposed) { this.running = false; this.faulted = true; this.message = `Connection interrupted. Retry the core to continue. ${String(error)}`; }
    } finally { this.busy = false; if (!this.disposed) this.emit(); }
  }

  private emit(): void { for (const listener of this.listeners) listener(); }
  destroy(): void { this.disposed = true; clearInterval(this.timer); this.unsubscribe(); this.bridge.dispose?.(); this.listeners.clear(); }
}
