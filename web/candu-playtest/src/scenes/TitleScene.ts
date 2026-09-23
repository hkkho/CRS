import Phaser from "phaser";
import { getRuntimeSession } from "../runtime";
import type { SessionUpdate } from "../sessionController";
import type { BridgeModeId } from "../protocol";
import { COLORS, FONTS, colorString, drawCornerBrackets, makeButton, makeText, type TacticalButton } from "../drawing";

interface TitleParticle {
  x: number;
  y: number;
  speed: number;
  phase: number;
  size: number;
  alpha: number;
}

export class TitleScene extends Phaser.Scene {
  private readonly reducedMotion = typeof window !== "undefined" && window.matchMedia?.("(prefers-reduced-motion: reduce)").matches === true;
  private readonly session = getRuntimeSession();
  private readonly particles: TitleParticle[] = [];
  private particleGraphics: Phaser.GameObjects.Graphics | null = null;
  private reactorGraphics: Phaser.GameObjects.Graphics | null = null;
  private titleStatus: Phaser.GameObjects.Text | null = null;
  private titleAvailability: Phaser.GameObjects.Text | null = null;
  private beginButton: TacticalButton | null = null;
  private modeButtons: Record<BridgeModeId, TacticalButton | null> = { play: null, lab: null };
  private selectedMode: BridgeModeId = "play";
  private modeError: string | null = null;
  private readyForShift = false;
  private shiftTransitionStarted = false;
  private fadeCamera: Phaser.Cameras.Scene2D.Camera | null = null;
  private unsubscribe: (() => void) | null = null;

  public constructor() {
    super("TitleScene");
  }

  public create(): void {
    this.shiftTransitionStarted = false;
    this.selectedMode = this.session.mode;
    this.modeError = null;
    this.cameras.main.setBackgroundColor(colorString(COLORS.void));
    this.drawBackdrop();
    this.createParticles();
    this.drawCrest();
    this.createTitleCopy();
    this.createBeginButton();
    this.createModeButtons();
    this.renderSessionState({
      status: this.session.status,
      snapshot: this.session.snapshot,
      pending: this.session.isPending,
      response: null,
      error: this.session.error,
    });
    this.unsubscribe = this.session.subscribe((update) => this.renderSessionState(update));
    this.events.once("shutdown", () => this.unsubscribe?.());
    this.input.keyboard?.on("keydown-ENTER", this.beginShift, this);
    this.input.keyboard?.on("keydown-SPACE", this.beginShift, this);
    this.input.keyboard?.on("keydown-P", this.selectPlayMode, this);
    this.input.keyboard?.on("keydown-L", this.selectLabMode, this);
    this.events.once("shutdown", () => {
      this.fadeCamera?.off(Phaser.Cameras.Scene2D.Events.FADE_OUT_COMPLETE, this.handleFadeOutComplete, this);
      this.fadeCamera = null;
      this.input.keyboard?.off("keydown-ENTER", this.beginShift, this);
      this.input.keyboard?.off("keydown-SPACE", this.beginShift, this);
      this.input.keyboard?.off("keydown-P", this.selectPlayMode, this);
      this.input.keyboard?.off("keydown-L", this.selectLabMode, this);
    });
    this.cameras.main.fadeIn(500, 7, 11, 27);
  }

  public update(time: number, delta: number): void {
    if (this.reducedMotion || this.particleGraphics === null || this.reactorGraphics === null) {
      return;
    }

    const seconds = time / 1000;
    const safeDelta = Math.min(50, Math.max(0, delta)) / 1000;
    this.drawParticles(seconds, safeDelta);
    this.drawReactor(seconds);
  }

  private drawBackdrop(): void {
    const graphics = this.add.graphics();
    graphics.fillStyle(COLORS.void, 1);
    graphics.fillRect(0, 0, 1600, 900);
    graphics.fillStyle(COLORS.navy, 0.82);
    graphics.fillRect(0, 0, 1600, 900);
    graphics.fillStyle(COLORS.indigo, 0.48);
    graphics.fillRect(0, 500, 1600, 400);
    graphics.fillStyle(COLORS.magentaDark, 0.1);
    graphics.fillTriangle(660, 0, 1180, 0, 970, 820);
    graphics.fillStyle(COLORS.cyanDark, 0.08);
    graphics.fillTriangle(860, 0, 1290, 0, 1060, 820);
    graphics.fillStyle(COLORS.white, 0.025);
    graphics.fillTriangle(1030, 0, 1100, 0, 940, 780);

    graphics.lineStyle(1, COLORS.gold, 0.2);
    graphics.lineBetween(0, 110, 1600, 110);
    graphics.lineBetween(0, 790, 1600, 790);
    graphics.lineStyle(2, COLORS.gold, 0.52);
    graphics.lineBetween(90, 110, 340, 110);
    graphics.lineBetween(90, 790, 340, 790);
    graphics.lineStyle(1, COLORS.cyan, 0.35);
    graphics.lineBetween(1180, 110, 1510, 110);
    graphics.lineBetween(1260, 790, 1510, 790);

    this.reactorGraphics = this.add.graphics();
    this.drawReactor(0);
    this.particleGraphics = this.add.graphics();
    drawCornerBrackets(graphics, 74, 74, 1452, 752, COLORS.gold);
  }

  private drawReactor(timeSeconds: number): void {
    if (this.reactorGraphics === null) {
      return;
    }
    const graphics = this.reactorGraphics;
    const pulse = 1 + Math.sin(timeSeconds * 1.7) * 0.035;
    graphics.clear();
    graphics.fillStyle(COLORS.cyanDark, 0.06);
    graphics.fillCircle(1050, 433, 280 * pulse);
    graphics.fillStyle(COLORS.magentaDark, 0.06);
    graphics.fillCircle(1050, 433, 215 * pulse);
    graphics.lineStyle(1, COLORS.cyan, 0.2);
    graphics.strokeCircle(1050, 433, 285 * pulse);
    graphics.lineStyle(2, COLORS.gold, 0.38);
    graphics.strokeCircle(1050, 433, 232 * pulse);
    graphics.lineStyle(1, COLORS.ivory, 0.25);
    graphics.strokeCircle(1050, 433, 178 * pulse);
    graphics.lineStyle(3, COLORS.magenta, 0.65);
    graphics.arc(1050, 433, 232 * pulse, -1.1, 0.7, false);
    graphics.lineStyle(3, COLORS.cyan, 0.65);
    graphics.arc(1050, 433, 232 * pulse, 2.0, 3.8, false);

    for (let ring = 0; ring < 3; ring += 1) {
      const radius = 122 - ring * 25;
      graphics.lineStyle(1.5, ring % 2 === 0 ? COLORS.cyan : COLORS.gold, 0.46);
      graphics.strokeCircle(1050, 433, radius * pulse);
    }
    graphics.fillStyle(COLORS.ink, 0.85);
    graphics.fillCircle(1050, 433, 74 * pulse);
    graphics.lineStyle(2, COLORS.gold, 0.85);
    graphics.strokeCircle(1050, 433, 74 * pulse);
    graphics.fillStyle(COLORS.cyan, 0.45 + Math.sin(timeSeconds * 2.2) * 0.12);
    graphics.fillCircle(1050, 433, 44 * pulse);
    graphics.lineStyle(1, COLORS.ivory, 0.5);
    graphics.lineBetween(968, 433, 1132, 433);
    graphics.lineBetween(1050, 351, 1050, 515);
    graphics.fillStyle(COLORS.gold, 0.85);
    for (let index = 0; index < 8; index += 1) {
      const angle = timeSeconds * 0.24 + (Math.PI * 2 * index) / 8;
      graphics.fillCircle(1050 + Math.cos(angle) * 120, 433 + Math.sin(angle) * 120, 3);
    }
  }

  private createParticles(): void {
    for (let index = 0; index < 46; index += 1) {
      this.particles.push({
        x: 700 + Math.random() * 780,
        y: 135 + Math.random() * 620,
        speed: 8 + Math.random() * 22,
        phase: Math.random() * Math.PI * 2,
        size: 1 + Math.random() * 2.4,
        alpha: 0.12 + Math.random() * 0.34,
      });
    }
    this.drawParticles(0, 0);
  }

  private drawParticles(timeSeconds: number, deltaSeconds: number): void {
    if (this.particleGraphics === null) {
      return;
    }
    const graphics = this.particleGraphics;
    graphics.clear();
    for (const particle of this.particles) {
      particle.y -= particle.speed * deltaSeconds;
      if (particle.y < 125) {
        particle.y = 760;
      }
      const alpha = particle.alpha * (0.58 + Math.sin(timeSeconds * 1.5 + particle.phase) * 0.42);
      graphics.fillStyle(particle.phase % 2 > 1 ? COLORS.cyan : COLORS.gold, Math.max(0.04, alpha));
      graphics.fillCircle(particle.x, particle.y, particle.size);
    }
  }

  private drawCrest(): void {
    const graphics = this.add.graphics();
    graphics.fillStyle(COLORS.panel, 0.7);
    graphics.fillRoundedRect(138, 178, 302, 326, 12);
    graphics.lineStyle(1.5, COLORS.gold, 0.62);
    graphics.strokeRoundedRect(138, 178, 302, 326, 12);
    drawCornerBrackets(graphics, 138, 178, 302, 326, COLORS.cyan);
    graphics.fillStyle(COLORS.indigoLight, 0.88);
    graphics.fillPoints([
      { x: 289, y: 218 }, { x: 366, y: 247 }, { x: 352, y: 395 },
      { x: 289, y: 462 }, { x: 226, y: 395 }, { x: 212, y: 247 },
    ], true);
    graphics.lineStyle(2, COLORS.gold, 0.86);
    graphics.strokePoints([
      { x: 289, y: 218 }, { x: 366, y: 247 }, { x: 352, y: 395 },
      { x: 289, y: 462 }, { x: 226, y: 395 }, { x: 212, y: 247 },
    ], true);
    graphics.lineStyle(2, COLORS.cyan, 0.7);
    graphics.lineBetween(245, 292, 333, 292);
    graphics.lineBetween(289, 250, 289, 409);
    graphics.strokeCircle(289, 330, 38);
    graphics.fillStyle(COLORS.cyan, 0.68);
    graphics.fillCircle(289, 330, 20);
    graphics.lineStyle(1, COLORS.ivory, 0.72);
    graphics.strokeCircle(289, 330, 11);
    makeText(this, 289, 526, "CANDU", {
      fontFamily: FONTS.display,
      fontSize: "34px",
      color: colorString(COLORS.ivory),
      fontStyle: "bold",
      letterSpacing: 4,
    }).setOrigin(0.5);
  }

  private createTitleCopy(): void {
    makeText(this, 146, 570, "REACTOR OPERATIONS / SHIFT 01", {
      fontFamily: FONTS.mono,
      fontSize: "12px",
      color: colorString(COLORS.cyan),
      letterSpacing: 2,
    });
    makeText(this, 146, 600, "ON-POWER", {
      fontFamily: FONTS.display,
      fontSize: "48px",
      color: colorString(COLORS.ivory),
      fontStyle: "bold",
      letterSpacing: 2,
    });
    makeText(this, 146, 651, "REFUELLING", {
      fontFamily: FONTS.display,
      fontSize: "48px",
      color: colorString(COLORS.gold),
      fontStyle: "bold",
      letterSpacing: 2,
    });
    makeText(this, 148, 712, "STABILIZE THE CORE. CHOOSE THE NEXT CHANNEL.", {
      fontFamily: FONTS.mono,
      fontSize: "12px",
      color: colorString(COLORS.ivoryMuted),
      letterSpacing: 1.2,
    });
    makeText(this, 740, 714, "A TACTICAL STEADY-STATE SIMULATION", {
      fontFamily: FONTS.mono,
      fontSize: "11px",
      color: colorString(COLORS.ivoryMuted),
      letterSpacing: 1.6,
    });

    this.titleStatus = makeText(this, 1128, 142, "AUTHORITY LINK / CHECKING", {
      fontFamily: FONTS.mono,
      fontSize: "12px",
      color: colorString(COLORS.gold),
      letterSpacing: 1.2,
    }).setOrigin(1, 0.5);
    this.titleAvailability = makeText(this, 1128, 165, "", {
      fontFamily: FONTS.body,
      fontSize: "14px",
      color: colorString(COLORS.ivoryMuted),
      align: "right",
      wordWrap: { width: 440 },
    }).setOrigin(1, 0);
  }

  private createBeginButton(): void {
    this.beginButton = makeButton(
      this,
      300,
      820,
      304,
      58,
      "BEGIN SHIFT  ↗",
      () => this.beginShift(),
      { tone: "gold", fontSize: 15 },
    );
    makeText(this, 300, 858, "ENTER / SPACE", {
      fontFamily: FONTS.mono,
      fontSize: "10px",
      color: colorString(COLORS.ivoryMuted),
      letterSpacing: 1,
    }).setOrigin(0.5);
  }

  private createModeButtons(): void {
    makeText(this, 1050, 756, "START MODE  /  P PLAY · L LAB", {
      fontFamily: FONTS.mono,
      fontSize: "10px",
      color: colorString(COLORS.ivoryMuted),
      letterSpacing: 1,
    }).setOrigin(0.5);
    this.modeButtons.play = makeButton(
      this,
      880,
      820,
      250,
      58,
      "PLAY  /  380 CHANNELS",
      () => this.selectMode("play"),
      { tone: "cyan", fontSize: 11 },
    );
    this.modeButtons.lab = makeButton(
      this,
      1170,
      820,
      250,
      58,
      "LAB  /  2 × 8 CELLS",
      () => this.selectMode("lab"),
      { tone: "magenta", fontSize: 11 },
    );
    makeText(this, 880, 858, "FULL CORE LOOP", {
      fontFamily: FONTS.mono,
      fontSize: "9px",
      color: colorString(COLORS.ivoryMuted),
      letterSpacing: 0.8,
    }).setOrigin(0.5);
    makeText(this, 1170, 858, "TOPOLOGY WORKBENCH", {
      fontFamily: FONTS.mono,
      fontSize: "9px",
      color: colorString(COLORS.ivoryMuted),
      letterSpacing: 0.8,
    }).setOrigin(0.5);
  }

  private renderSessionState(update: SessionUpdate): void {
    const labReady = update.snapshot.lab?.core.channelCount === 2 &&
      update.snapshot.lab.core.bundlePositionCount === 8 &&
      update.snapshot.lab.core.cells !== undefined;
    this.readyForShift = update.status.isWasmAvailable &&
      (this.selectedMode === "lab" ? labReady : update.snapshot.core.channels.length === 380);
    this.beginButton?.setEnabled(this.readyForShift && !update.pending);
    for (const [mode, button] of Object.entries(this.modeButtons) as Array<[BridgeModeId, TacticalButton | null]>) {
      button?.setEnabled(update.status.isWasmAvailable && !update.pending && !this.shiftTransitionStarted);
      button?.gameObject.setAlpha(mode === this.selectedMode ? 1 : 0.68);
    }
    this.beginButton?.setLabel(this.selectedMode === "lab" ? "BEGIN LAB  ↗" : "BEGIN SHIFT  ↗");
    if (this.titleStatus === null || this.titleAvailability === null) {
      return;
    }

    if (update.status.source === "loading") {
      this.titleStatus.setText("AUTHORITY LINK / CONNECTING").setColor(colorString(COLORS.gold));
      this.titleAvailability.setText("Opening the ReactorSim command bridge…");
    } else if (!update.status.isWasmAvailable) {
      this.titleStatus.setText("AUTHORITY LINK / UNAVAILABLE").setColor(colorString(COLORS.red));
      this.titleAvailability.setText("SHIFT LOCKED · The authoritative browser bridge is unavailable. Reload after the WASM pack is staged.");
    } else if (this.modeError !== null) {
      this.titleStatus.setText("AUTHORITY LINK / ERROR").setColor(colorString(COLORS.red));
      this.titleAvailability.setText(`MODE SWITCH FAILED · ${this.modeError}`);
    } else if (this.readyForShift) {
      this.titleStatus.setText("AUTHORITY LINK / ONLINE").setColor(colorString(COLORS.green));
      this.titleAvailability.setText(this.selectedMode === "lab"
        ? "LAB MODE READY · 2 × 8 CELLS · CONFIGURE FUEL AND REFLECTIVE BOUNDARIES"
        : "PLAY MODE READY · 380 CHANNELS · SELECT A PATH AND HOLD POWER");
    } else {
      this.titleStatus.setText("AUTHORITY LINK / SYNCING").setColor(colorString(COLORS.gold));
      this.titleAvailability.setText(this.selectedMode === "lab"
        ? "Waiting for the Lab spatial solve…"
        : "Waiting for the full reactor snapshot…");
    }
  }

  private selectPlayMode(): void {
    this.selectMode("play");
  }

  private selectLabMode(): void {
    this.selectMode("lab");
  }

  private selectMode(mode: BridgeModeId): void {
    if (this.shiftTransitionStarted || this.session.isPending || !this.session.status.isWasmAvailable) {
      return;
    }
    this.modeError = null;
    this.selectedMode = mode;
    this.renderSessionState({
      status: this.session.status,
      snapshot: this.session.snapshot,
      pending: this.session.isPending,
      response: null,
      error: this.session.error,
    });
    if (this.session.mode === mode) {
      return;
    }
    void this.session.initializeMode(mode).catch((error: unknown) => {
      this.modeError = error instanceof Error ? error.message : String(error);
      this.selectedMode = this.session.mode;
      this.renderSessionState({
        status: this.session.status,
        snapshot: this.session.snapshot,
        pending: this.session.isPending,
        response: null,
        error: this.session.error,
      });
    });
  }

  private beginShift(): void {
    if (!this.readyForShift || this.session.isPending || this.shiftTransitionStarted) {
      return;
    }
    this.shiftTransitionStarted = true;
    this.beginButton?.setEnabled(false);
    if (this.selectedMode === "play") {
      this.session.startShift();
    } else {
      this.session.stopShift();
    }
    const fadeCamera = this.cameras.main;
    this.fadeCamera = fadeCamera;
    fadeCamera.once(
      Phaser.Cameras.Scene2D.Events.FADE_OUT_COMPLETE,
      this.handleFadeOutComplete,
      this,
    );
    fadeCamera.fadeOut(450, 7, 11, 27);
  }

  private handleFadeOutComplete(): void {
    if (!this.shiftTransitionStarted) {
      return;
    }
    this.scene.start(this.selectedMode === "lab" ? "LabScene" : "OperationsScene");
  }
}
