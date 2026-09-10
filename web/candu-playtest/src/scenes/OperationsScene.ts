import Phaser from "phaser";
import {
  canConfirmRefuel,
  createRefuelDraft,
  formatRefuelDirection,
  previewMatchesDraft,
  toggleRefuelDirection,
  toRefuelRequest,
  adjustTarget,
  type RefuelDraft,
} from "../commandState";
import {
  COLORS,
  FONTS,
  colorFromRgb,
  colorString,
  drawCornerBrackets,
  drawPanelFrame,
  makeButton,
  makeText,
  mixColor,
  type TacticalButton,
} from "../drawing";
import { getRuntimeSession } from "../runtime";
import type { SessionUpdate } from "../sessionController";
import type {
  CanduChannelSnapshot,
  CanduCommandResponse,
  CanduSnapshot,
  PlaybackModeId,
  RefuelPreview,
  RefuelRequest,
} from "../protocol";
import { createCoreFaceLayout, findAdjacentChannelIndex, getFlowVector, gridCoordinateLabel, projectChannelToFace, type CoreFaceLayout, type CorePoint } from "../projection";
import {
  formatReactivity,
  formatEffectiveK,
  formatSignedNumber,
  formatSimulationTime,
  formatSolveHealth,
  getFlowArrow,
  getHeatColor,
  getOverallStatus,
  getPowerLabel,
  getTiltLabel,
} from "../visuals";

const VIEW_WIDTH = 1600;
const VIEW_HEIGHT = 900;
const HUD_HEIGHT = 118;
const MAP = { x: 24, y: 136, width: 1182, height: 690 } as const;
const SIDE = { x: 1220, y: 136, width: 356, height: 690 } as const;
const ROW_LABELS = [
  "A", "B", "C", "D", "E", "F", "G", "H", "J", "K", "L", "M",
  "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W",
] as const;

interface ChannelTile {
  channel: CanduChannelSnapshot;
  container: Phaser.GameObjects.Container;
  graphics: Phaser.GameObjects.Graphics;
}

interface RefuelMotion {
  request: RefuelRequest;
  response: CanduCommandResponse;
  elapsed: number;
  duration: number;
  origin: CorePoint;
}

type ModalKind = "refuel" | "control";

interface ModalObject {
  destroy: () => void;
}

export class OperationsScene extends Phaser.Scene {
  private readonly session = getRuntimeSession();
  private readonly reducedMotion = typeof window !== "undefined" && window.matchMedia?.("(prefers-reduced-motion: reduce)").matches === true;
  private readonly tiles = new Map<number, ChannelTile>();
  private readonly modalObjects: ModalObject[] = [];
  private snapshot: CanduSnapshot = this.session.snapshot;
  private selectedChannelIndex = -1;
  private hoveredChannelIndex = -1;
  private layout: CoreFaceLayout = createCoreFaceLayout(MAP.x, MAP.y, MAP.width, MAP.height);
  private unsubscribe: (() => void) | null = null;
  private pending = false;
  private modalKind: ModalKind | null = null;
  private refuelDraft: RefuelDraft | null = null;
  private preview: RefuelPreview | null = null;
  private controlPowerTarget = 1;
  private controlTiltTarget = 0;
  private lastHandledResponseSequence = -1;
  private lastError = "";
  private resultMessage = "";
  private resultExpiresAt = 0;
  private motion: RefuelMotion | null = null;
  private modalBackdrop: Phaser.GameObjects.Graphics | null = null;
  private modalGraphics: Phaser.GameObjects.Graphics | null = null;
  private modalPreviewGraphics: Phaser.GameObjects.Graphics | null = null;
  private modalTitle: Phaser.GameObjects.Text | null = null;
  private modalSubtitle: Phaser.GameObjects.Text | null = null;
  private modalStatus: Phaser.GameObjects.Text | null = null;
  private modalDetail: Phaser.GameObjects.Text | null = null;
  private modalValueA: Phaser.GameObjects.Text | null = null;
  private modalValueB: Phaser.GameObjects.Text | null = null;
  private modalValueC: Phaser.GameObjects.Text | null = null;
  private modalValueD: Phaser.GameObjects.Text | null = null;
  private modalValueE: Phaser.GameObjects.Text | null = null;
  private modalButtons: TacticalButton[] = [];

  private hudGraphics: Phaser.GameObjects.Graphics | null = null;
  private mapGraphics: Phaser.GameObjects.Graphics | null = null;
  private ambientGraphics: Phaser.GameObjects.Graphics | null = null;
  private selectionGraphics: Phaser.GameObjects.Graphics | null = null;
  private previewGraphics: Phaser.GameObjects.Graphics | null = null;
  private motionGraphics: Phaser.GameObjects.Graphics | null = null;
  private motionText: Phaser.GameObjects.Text | null = null;
  private hudDay: Phaser.GameObjects.Text | null = null;
  private hudPower: Phaser.GameObjects.Text | null = null;
  private hudTilt: Phaser.GameObjects.Text | null = null;
  private hudScore: Phaser.GameObjects.Text | null = null;
  private hudFresh: Phaser.GameObjects.Text | null = null;
  private hudStatus: Phaser.GameObjects.Text | null = null;
  private hudSpeed: Phaser.GameObjects.Text | null = null;
  private sideChannelId: Phaser.GameObjects.Text | null = null;
  private sideCoordinate: Phaser.GameObjects.Text | null = null;
  private sidePower: Phaser.GameObjects.Text | null = null;
  private sideTilt: Phaser.GameObjects.Text | null = null;
  private sideBurnup: Phaser.GameObjects.Text | null = null;
  private sideFlow: Phaser.GameObjects.Text | null = null;
  private sidePhysicsCore: Phaser.GameObjects.Text | null = null;
  private sidePhysicsStatic: Phaser.GameObjects.Text | null = null;
  private sidePhysicsNet: Phaser.GameObjects.Text | null = null;
  private sidePhysicsK: Phaser.GameObjects.Text | null = null;
  private sidePhysicsSolve: Phaser.GameObjects.Text | null = null;
  private sideEvent: Phaser.GameObjects.Text | null = null;
  private sideProfileGraphics: Phaser.GameObjects.Graphics | null = null;
  private sideProfileLegend: Phaser.GameObjects.Text | null = null;
  private readonly sideProfileNumbers: Phaser.GameObjects.Text[] = [];
  private readonly sideProfileValues: Phaser.GameObjects.Text[] = [];
  private refuelButton: TacticalButton | null = null;
  private controlButton: TacticalButton | null = null;
  private playbackButtons: TacticalButton[] = [];
  private unavailableOverlay: Phaser.GameObjects.Container | null = null;
  private unavailableTitleText: Phaser.GameObjects.Text | null = null;
  private unavailableDetailText: Phaser.GameObjects.Text | null = null;

  public constructor() {
    super("OperationsScene");
  }

  public create(): void {
    this.snapshot = this.session.snapshot;
    this.selectedChannelIndex = chooseInitialChannel(this.snapshot);
    this.createBackdrop();
    this.createUnavailableOverlay();
    this.createMap();
    this.createHud();
    this.createSidePanel();
    this.createMotionLayer();
    this.refreshAll();
    this.unsubscribe = this.session.subscribe((update) => this.receiveSessionUpdate(update));
    this.events.once("shutdown", () => this.unsubscribe?.());
    this.input.keyboard?.on("keydown", this.handleKeyDown, this);
    this.events.once("shutdown", () => this.input.keyboard?.off("keydown", this.handleKeyDown, this));
    this.cameras.main.fadeIn(420, 7, 11, 27);
  }

  public update(time: number, delta: number): void {
    const safeDelta = Math.min(80, Math.max(0, delta));
    this.drawAmbient(time);
    if (this.motion !== null) {
      this.motion.elapsed += safeDelta;
      this.drawMotion();
      if (this.motion.elapsed >= this.motion.duration) {
        const finished = this.motion;
        this.motion = null;
        this.motionGraphics?.clear();
        this.motionText?.setVisible(false);
        this.resultMessage = finished.response.message;
        this.resultExpiresAt = time + 6800;
        this.refreshSidePanel();
      }
    }
    if (this.resultExpiresAt !== 0 && time >= this.resultExpiresAt) {
      this.resultExpiresAt = 0;
      this.resultMessage = "";
      this.refreshSidePanel();
    }
  }

  private createBackdrop(): void {
    const background = this.add.graphics().setDepth(-20);
    background.fillStyle(COLORS.void, 1);
    background.fillRect(0, 0, VIEW_WIDTH, VIEW_HEIGHT);
    background.fillStyle(COLORS.navy, 1);
    background.fillRect(0, 0, VIEW_WIDTH, VIEW_HEIGHT);
    background.fillStyle(COLORS.indigo, 0.34);
    background.fillRect(0, HUD_HEIGHT, VIEW_WIDTH, VIEW_HEIGHT - HUD_HEIGHT);
    background.fillStyle(COLORS.magentaDark, 0.06);
    background.fillTriangle(0, HUD_HEIGHT, 730, HUD_HEIGHT, 370, VIEW_HEIGHT);
    background.fillStyle(COLORS.cyanDark, 0.05);
    background.fillTriangle(930, HUD_HEIGHT, 1600, HUD_HEIGHT, 1370, VIEW_HEIGHT);
    background.lineStyle(1, COLORS.gold, 0.22);
    background.lineBetween(0, HUD_HEIGHT, VIEW_WIDTH, HUD_HEIGHT);
    background.lineBetween(0, VIEW_HEIGHT - 52, VIEW_WIDTH, VIEW_HEIGHT - 52);
    background.lineStyle(1, COLORS.ivory, 0.08);
    for (let index = 0; index < 9; index += 1) {
      background.lineBetween(0, 156 + index * 76, VIEW_WIDTH, 156 + index * 76);
    }
    const corner = this.add.graphics().setDepth(-19);
    drawCornerBrackets(corner, 18, 122, 1188, 716, COLORS.gold);
    drawCornerBrackets(corner, SIDE.x, SIDE.y, SIDE.width, SIDE.height, COLORS.cyan);
  }

  private createMap(): void {
    this.layout = createCoreFaceLayout(MAP.x + 8, MAP.y + 10, MAP.width - 16, MAP.height - 28);
    this.mapGraphics = this.add.graphics().setDepth(0);
    this.ambientGraphics = this.add.graphics().setDepth(12);
    this.selectionGraphics = this.add.graphics().setDepth(13);
    this.previewGraphics = this.add.graphics().setDepth(14);
    this.drawMapFoundation();

    for (const channel of this.snapshot.core.channels) {
      const tile = this.createChannelTile(channel);
      this.tiles.set(channel.channelIndex, tile);
    }
    this.refreshCore();
  }

  private drawMapFoundation(): void {
    if (this.mapGraphics === null) {
      return;
    }
    const graphics = this.mapGraphics;
    graphics.clear();
    drawPanelFrame(graphics, MAP.x, MAP.y, MAP.width, MAP.height, {
      fill: COLORS.panel,
      alpha: 0.88,
      accent: COLORS.gold,
      lineWidth: 1.2,
    });

    graphics.fillStyle(COLORS.ink, 0.58);
    graphics.fillRect(this.layout.gridX, this.layout.gridY, this.layout.gridPixelWidth, this.layout.gridPixelHeight);
    graphics.lineStyle(1.5, COLORS.cyan, 0.42);
    graphics.strokeRect(this.layout.gridX, this.layout.gridY, this.layout.gridPixelWidth, this.layout.gridPixelHeight);
    graphics.lineStyle(1, COLORS.grid, 0.25);
    for (let index = 0; index <= 22; index += 1) {
      const x = this.layout.gridX + index * this.layout.stepX;
      const y = this.layout.gridY + index * this.layout.stepY;
      graphics.lineBetween(x, this.layout.gridY, x, this.layout.gridY + this.layout.gridPixelHeight);
      graphics.lineBetween(this.layout.gridX, y, this.layout.gridX + this.layout.gridPixelWidth, y);
    }
    graphics.lineStyle(1, COLORS.gold, 0.22);
    graphics.strokeRect(this.layout.gridX + this.layout.stepX * 7, this.layout.gridY + this.layout.stepY * 7, this.layout.stepX * 8, this.layout.stepY * 8);
    graphics.fillStyle(COLORS.cyan, 0.26);
    graphics.fillCircle(this.layout.centerX, this.layout.centerY, 3);

    makeText(this, MAP.x + 22, MAP.y + 18, "TACTICAL CORE", {
      fontFamily: FONTS.mono,
      fontSize: "12px",
      color: colorString(COLORS.cyan),
      letterSpacing: 1.8,
    }).setDepth(20);
    makeText(this, MAP.x + 22, MAP.y + 38, "CANDU 6 / 380 CHANNEL FACE / 22 × 22 STEPPED TOPOLOGY", {
      fontFamily: FONTS.mono,
      fontSize: "10px",
      color: colorString(COLORS.ivoryMuted),
      letterSpacing: 1,
    }).setDepth(20);
    makeText(this, MAP.x + MAP.width - 20, MAP.y + 22, "POWER / HEAT FIELD", {
      fontFamily: FONTS.mono,
      fontSize: "10px",
      color: colorString(COLORS.gold),
      letterSpacing: 1.2,
    }).setOrigin(1, 0).setDepth(20);

    for (let row = 0; row < 22; row += 1) {
      const left = projectChannelToFace({ gridColumn: 0, gridRow: row }, this.layout);
      makeText(this, this.layout.gridX - 14, left.y - 5, ROW_LABELS[row] ?? "?", {
        fontFamily: FONTS.mono,
        fontSize: "9px",
        color: colorString(COLORS.ivoryMuted),
        align: "right",
      }).setOrigin(1, 0).setDepth(20);
    }
    for (let column = 0; column < 22; column += 1) {
      const top = projectChannelToFace({ gridColumn: column, gridRow: 0 }, this.layout);
      makeText(this, top.x, this.layout.gridY - 18, String(column + 1).padStart(2, "0"), {
        fontFamily: FONTS.mono,
        fontSize: "8px",
        color: colorString(COLORS.ivoryMuted),
      }).setOrigin(0.5).setDepth(20);
    }

    const legendX = MAP.x + 28;
    const legendY = MAP.y + MAP.height - 36;
    graphics.fillStyle(colorFromRgb(getHeatColor(0.72)), 1);
    graphics.fillRect(legendX, legendY, 55, 5);
    graphics.fillStyle(colorFromRgb(getHeatColor(0.93)), 1);
    graphics.fillRect(legendX + 55, legendY, 55, 5);
    graphics.fillStyle(colorFromRgb(getHeatColor(1.18)), 1);
    graphics.fillRect(legendX + 110, legendY, 55, 5);
    makeText(this, legendX, legendY + 10, "LOW", { fontFamily: FONTS.mono, fontSize: "8px", color: colorString(COLORS.ivoryMuted) }).setDepth(20);
    makeText(this, legendX + 82, legendY + 10, "NOMINAL", { fontFamily: FONTS.mono, fontSize: "8px", color: colorString(COLORS.ivoryMuted) }).setDepth(20);
    makeText(this, legendX + 151, legendY + 10, "HIGH", { fontFamily: FONTS.mono, fontSize: "8px", color: colorString(COLORS.ivoryMuted) }).setDepth(20);
    makeText(this, MAP.x + MAP.width - 26, MAP.y + MAP.height - 34, "ARROWS = COOLANT / FUEL PATH", {
      fontFamily: FONTS.mono,
      fontSize: "9px",
      color: colorString(COLORS.ivoryMuted),
      letterSpacing: 0.8,
    }).setOrigin(1, 0).setDepth(20);
  }

  private createChannelTile(channel: CanduChannelSnapshot): ChannelTile {
    const container = this.add.container(0, 0).setDepth(40);
    const graphics = this.add.graphics();
    container.add(graphics);
    // Use the full orthographic cell as the hit target. The painted channel
    // face stays inset, keeping neighbors clear while selection stays easy.
    container.setSize(this.layout.stepX, this.layout.stepY);
    const hitArea = new Phaser.Geom.Rectangle(-this.layout.stepX / 2, -this.layout.stepY / 2, this.layout.stepX, this.layout.stepY);
    container.setInteractive(hitArea, Phaser.Geom.Rectangle.Contains);
    container.on("pointerover", () => {
      this.hoveredChannelIndex = channel.channelIndex;
      this.refreshCore();
    });
    container.on("pointerout", () => {
      if (this.hoveredChannelIndex === channel.channelIndex) {
        this.hoveredChannelIndex = -1;
        this.refreshCore();
      }
    });
    container.on("pointerdown", () => this.selectChannel(channel.channelIndex));
    return { channel, container, graphics };
  }

  private drawSelection(): void {
    if (this.selectionGraphics === null) {
      return;
    }
    const graphics = this.selectionGraphics;
    graphics.clear();
    const selected = this.getSelectedChannel();
    if (selected === undefined) {
      return;
    }
    const position = projectChannelToFace(selected, this.layout);
    const pulse = this.reducedMotion ? 1 : 1 + Math.sin(this.scene.systems.game.loop.time * 0.005) * 0.06;
    const outerWidth = this.layout.tileWidth + 12 * pulse;
    const outerHeight = this.layout.tileHeight + 10 * pulse;
    graphics.lineStyle(2.5, COLORS.gold, 0.96);
    graphics.strokeRoundedRect(position.x - outerWidth / 2, position.y - outerHeight / 2, outerWidth, outerHeight, 4);
    graphics.lineStyle(1, COLORS.cyan, 0.86);
    graphics.strokeRoundedRect(position.x - outerWidth / 2 - 4, position.y - outerHeight / 2 - 4, outerWidth + 8, outerHeight + 8, 6);
    graphics.lineBetween(position.x - outerWidth / 2 - 13, position.y, position.x - outerWidth / 2 - 5, position.y);
    graphics.lineBetween(position.x + outerWidth / 2 + 5, position.y, position.x + outerWidth / 2 + 13, position.y);
    graphics.lineBetween(position.x, position.y - outerHeight / 2 - 13, position.x, position.y - outerHeight / 2 - 5);
    graphics.lineBetween(position.x, position.y + outerHeight / 2 + 5, position.x, position.y + outerHeight / 2 + 13);
    graphics.fillStyle(COLORS.gold, 0.96);
    graphics.fillCircle(position.x, position.y, 2.5);
  }

  private drawPreviewVisual(): void {
    if (this.previewGraphics === null) {
      return;
    }
    const graphics = this.previewGraphics;
    graphics.clear();
    if (this.modalKind !== "refuel" || this.preview === null || this.refuelDraft === null || !previewMatchesDraft(this.preview, this.refuelDraft)) {
      return;
    }
    const selected = this.getSelectedChannel();
    if (selected === undefined) {
      return;
    }
    const position = projectChannelToFace(selected, this.layout);
    const vector = getFlowVector(this.refuelDraft.directionId);
    const magnitude = Math.hypot(vector.x, vector.y);
    const vx = vector.x / magnitude;
    const vy = vector.y / magnitude;
    graphics.lineStyle(12, COLORS.magenta, 0.16);
    graphics.lineBetween(position.x - vx * 74, position.y - vy * 74, position.x + vx * 74, position.y + vy * 74);
    graphics.lineStyle(2, COLORS.cyan, 0.9);
    graphics.lineBetween(position.x - vx * 74, position.y - vy * 74, position.x + vx * 74, position.y + vy * 74);
    for (let index = 0; index < 12; index += 1) {
      const offset = -60 + index * 11;
      const tokenX = position.x + vx * offset;
      const tokenY = position.y + vy * offset;
      const tokenColor = index < this.refuelDraft.shiftCount ? COLORS.cyan : COLORS.magenta;
      graphics.fillStyle(tokenColor, index < this.refuelDraft.shiftCount ? 0.94 : 0.36);
      graphics.fillRoundedRect(tokenX - 4, tokenY - 3, 8, 6, 2);
    }
    graphics.fillStyle(COLORS.gold, 0.95);
    graphics.fillTriangle(
      position.x + vx * 85,
      position.y + vy * 85,
      position.x + vx * 73 - vy * 6,
      position.y + vy * 73 + vx * 6,
      position.x + vx * 73 + vy * 6,
      position.y + vy * 73 - vx * 6,
    );
  }

  private drawAmbient(time: number): void {
    if (this.ambientGraphics === null) {
      return;
    }
    const graphics = this.ambientGraphics;
    graphics.clear();
    if (this.reducedMotion) {
      return;
    }
    const seconds = time / 1000;
    for (let index = 0; index < 28; index += 1) {
      const channel = this.snapshot.core.channels[(index * 17 + 5) % Math.max(1, this.snapshot.core.channels.length)];
      if (channel === undefined) {
        continue;
      }
      const position = projectChannelToFace(channel, this.layout);
      const alpha = 0.16 + (Math.sin(seconds * 1.6 + index * 0.9) + 1) * 0.1;
      graphics.fillStyle(index % 2 === 0 ? COLORS.cyan : COLORS.gold, alpha);
      graphics.fillCircle(position.x + Math.sin(seconds * 0.8 + index) * 3, position.y - 7, 1.2);
    }
    this.drawSelection();
  }

  private createHud(): void {
    this.hudGraphics = this.add.graphics().setDepth(180);
    const graphics = this.hudGraphics;
    graphics.fillStyle(COLORS.ink, 0.92);
    graphics.fillRect(0, 0, VIEW_WIDTH, HUD_HEIGHT);
    graphics.lineStyle(1.5, COLORS.gold, 0.7);
    graphics.lineBetween(0, HUD_HEIGHT - 2, VIEW_WIDTH, HUD_HEIGHT - 2);
    graphics.lineStyle(1, COLORS.ivory, 0.16);
    for (const x of [226, 455, 680, 890, 1128, 1270]) {
      graphics.lineBetween(x, 22, x, 94);
    }
    graphics.fillStyle(COLORS.magenta, 0.8);
    graphics.fillRect(24, 22, 5, 72);
    makeText(this, 48, 18, "CANDU", {
      fontFamily: FONTS.display,
      fontSize: "22px",
      color: colorString(COLORS.ivory),
      fontStyle: "bold",
      letterSpacing: 2,
    }).setDepth(190);
    makeText(this, 48, 49, "OPERATIONS", {
      fontFamily: FONTS.mono,
      fontSize: "10px",
      color: colorString(COLORS.cyan),
      letterSpacing: 2,
    }).setDepth(190);
    makeText(this, 48, 72, "ON-POWER REFUELLING", {
      fontFamily: FONTS.mono,
      fontSize: "9px",
      color: colorString(COLORS.ivoryMuted),
      letterSpacing: 1,
    }).setDepth(190);

    this.hudDay = makeText(this, 250, 41, "D01 · 00:00", { fontFamily: FONTS.mono, fontSize: "21px", color: colorString(COLORS.ivory), fontStyle: "bold" }).setDepth(190);
    makeText(this, 250, 20, "DAY / SIMULATION TIME", { fontFamily: FONTS.mono, fontSize: "9px", color: colorString(COLORS.gold), letterSpacing: 1.2 }).setDepth(190);
    makeText(this, 250, 75, "BASE CLOCK  /  2s = 1h", { fontFamily: FONTS.mono, fontSize: "9px", color: colorString(COLORS.ivoryMuted) }).setDepth(190);

    makeText(this, 480, 20, "REACTOR POWER", { fontFamily: FONTS.mono, fontSize: "9px", color: colorString(COLORS.cyan), letterSpacing: 1.2 }).setDepth(190);
    this.hudPower = makeText(this, 480, 39, "100.0%", { fontFamily: FONTS.mono, fontSize: "25px", color: colorString(COLORS.ivory), fontStyle: "bold" }).setDepth(190);
    makeText(this, 480, 75, "TARGET  100.0%", { fontFamily: FONTS.mono, fontSize: "9px", color: colorString(COLORS.ivoryMuted) }).setDepth(190);

    makeText(this, 705, 20, "AXIAL TILT", { fontFamily: FONTS.mono, fontSize: "9px", color: colorString(COLORS.magenta), letterSpacing: 1.2 }).setDepth(190);
    this.hudTilt = makeText(this, 705, 39, "+0.00%", { fontFamily: FONTS.mono, fontSize: "25px", color: colorString(COLORS.ivory), fontStyle: "bold" }).setDepth(190);
    makeText(this, 705, 75, "CONTROL MARGIN  100%", { fontFamily: FONTS.mono, fontSize: "9px", color: colorString(COLORS.ivoryMuted) }).setDepth(190);

    makeText(this, 915, 20, "SHIFT SCORE", { fontFamily: FONTS.mono, fontSize: "9px", color: colorString(COLORS.gold), letterSpacing: 1.2 }).setDepth(190);
    this.hudScore = makeText(this, 915, 39, "000000", { fontFamily: FONTS.mono, fontSize: "25px", color: colorString(COLORS.gold), fontStyle: "bold" }).setDepth(190);
    this.hudFresh = makeText(this, 915, 75, "128 FRESH BUNDLES", { fontFamily: FONTS.mono, fontSize: "9px", color: colorString(COLORS.ivoryMuted) }).setDepth(190);

    this.hudStatus = makeText(this, 1150, 31, "STABLE", { fontFamily: FONTS.mono, fontSize: "14px", color: colorString(COLORS.green), fontStyle: "bold", align: "right" }).setOrigin(1, 0).setDepth(190);
    this.hudSpeed = makeText(this, 1150, 59, "10x / LIVE", { fontFamily: FONTS.mono, fontSize: "10px", color: colorString(COLORS.ivoryMuted), align: "right" }).setOrigin(1, 0).setDepth(190);
    makeText(this, 1150, 80, "R  REFUEL    C  CONTROL", { fontFamily: FONTS.mono, fontSize: "9px", color: colorString(COLORS.ivoryMuted), align: "right" }).setOrigin(1, 0).setDepth(190);

    const modes: Array<{ id: PlaybackModeId; label: string }> = [
      { id: "pause", label: "Ⅱ" }, { id: "1x", label: "1X" }, { id: "10x", label: "10X" }, { id: "60x", label: "60X" },
    ];
    this.playbackButtons = modes.map((mode, index) => {
      const button = makeButton(this, 1294 + index * 67, 59, 58, 30, mode.label, () => this.setPlayback(mode.id), { tone: mode.id === "pause" ? "magenta" : "cyan", compact: true, fontSize: 11 });
      button.gameObject.setDepth(195);
      return button;
    });
  }

  private createSidePanel(): void {
    const graphics = this.add.graphics().setDepth(160);
    drawPanelFrame(graphics, SIDE.x, SIDE.y, SIDE.width, SIDE.height, { fill: COLORS.panel, alpha: 0.97, accent: COLORS.cyan, lineWidth: 1.5 });
    graphics.fillStyle(COLORS.magentaDark, 0.22);
    graphics.fillRect(SIDE.x + 14, SIDE.y + 15, SIDE.width - 28, 3);
    makeText(this, SIDE.x + 24, SIDE.y + 20, "CHANNEL DOSSIER", { fontFamily: FONTS.mono, fontSize: "11px", color: colorString(COLORS.cyan), letterSpacing: 1.6 }).setDepth(170);
    makeText(this, SIDE.x + SIDE.width - 22, SIDE.y + 20, "INSPECT", { fontFamily: FONTS.mono, fontSize: "9px", color: colorString(COLORS.ivoryMuted), letterSpacing: 1 }).setOrigin(1, 0).setDepth(170);
    this.sideChannelId = makeText(this, SIDE.x + 24, SIDE.y + 48, "CH 000", { fontFamily: FONTS.display, fontSize: "35px", color: colorString(COLORS.ivory), fontStyle: "bold" }).setDepth(170);
    this.sideCoordinate = makeText(this, SIDE.x + 26, SIDE.y + 91, "A01  /  STEPPED GRID", { fontFamily: FONTS.mono, fontSize: "11px", color: colorString(COLORS.gold), letterSpacing: 1 }).setDepth(170);
    this.sideFlow = makeText(this, SIDE.x + SIDE.width - 22, SIDE.y + 91, "→  WITH FLOW", { fontFamily: FONTS.mono, fontSize: "9px", color: colorString(COLORS.cyan), align: "right" }).setOrigin(1, 0).setDepth(170);

    graphics.fillStyle(COLORS.indigo, 0.9);
    graphics.fillRoundedRect(SIDE.x + 18, SIDE.y + 124, 152, 72, 6);
    graphics.fillRoundedRect(SIDE.x + 184, SIDE.y + 124, 152, 72, 6);
    graphics.lineStyle(1, COLORS.cyan, 0.34);
    graphics.strokeRoundedRect(SIDE.x + 18, SIDE.y + 124, 152, 72, 6);
    graphics.strokeRoundedRect(SIDE.x + 184, SIDE.y + 124, 152, 72, 6);
    makeText(this, SIDE.x + 30, SIDE.y + 137, "LOCAL POWER", { fontFamily: FONTS.mono, fontSize: "8px", color: colorString(COLORS.ivoryMuted), letterSpacing: 0.7 }).setDepth(170);
    makeText(this, SIDE.x + 196, SIDE.y + 137, "AXIAL TILT", { fontFamily: FONTS.mono, fontSize: "8px", color: colorString(COLORS.ivoryMuted), letterSpacing: 0.7 }).setDepth(170);
    this.sidePower = makeText(this, SIDE.x + 30, SIDE.y + 157, "100.0%", { fontFamily: FONTS.mono, fontSize: "20px", color: colorString(COLORS.ivory), fontStyle: "bold" }).setDepth(170);
    this.sideTilt = makeText(this, SIDE.x + 196, SIDE.y + 157, "+0.00%", { fontFamily: FONTS.mono, fontSize: "20px", color: colorString(COLORS.magenta), fontStyle: "bold" }).setDepth(170);
    makeText(this, SIDE.x + 30, SIDE.y + 181, "POWER FIELD", { fontFamily: FONTS.mono, fontSize: "8px", color: colorString(COLORS.ivoryMuted) }).setDepth(170);
    this.sideBurnup = makeText(this, SIDE.x + 196, SIDE.y + 181, "AVG BURNUP —", { fontFamily: FONTS.mono, fontSize: "8px", color: colorString(COLORS.ivoryMuted) }).setDepth(170);

    graphics.fillStyle(COLORS.indigo, 0.78);
    graphics.fillRoundedRect(SIDE.x + 18, SIDE.y + 208, SIDE.width - 36, 132, 6);
    graphics.lineStyle(1, COLORS.gold, 0.38);
    graphics.strokeRoundedRect(SIDE.x + 18, SIDE.y + 208, SIDE.width - 36, 132, 6);
    makeText(this, SIDE.x + 30, SIDE.y + 219, "REACTOR PHYSICS", { fontFamily: FONTS.mono, fontSize: "9px", color: colorString(COLORS.gold), letterSpacing: 1.1 }).setDepth(170);
    makeText(this, SIDE.x + SIDE.width - 30, SIDE.y + 219, "LIVE SOLVE", { fontFamily: FONTS.mono, fontSize: "8px", color: colorString(COLORS.cyan), align: "right" }).setOrigin(1, 0).setDepth(170);
    this.sidePhysicsCore = makeText(this, SIDE.x + 30, SIDE.y + 243, "CORE REACTIVITY   —", { fontFamily: FONTS.mono, fontSize: "10px", color: colorString(COLORS.ivory), fontStyle: "bold" }).setDepth(170);
    this.sidePhysicsStatic = makeText(this, SIDE.x + 30, SIDE.y + 264, "STATIC            —", { fontFamily: FONTS.mono, fontSize: "10px", color: colorString(COLORS.ivoryMuted) }).setDepth(170);
    this.sidePhysicsNet = makeText(this, SIDE.x + 30, SIDE.y + 285, "NET COMP          —", { fontFamily: FONTS.mono, fontSize: "10px", color: colorString(COLORS.cyan), fontStyle: "bold" }).setDepth(170);
    this.sidePhysicsK = makeText(this, SIDE.x + 30, SIDE.y + 306, "EFFECTIVE K       —", { fontFamily: FONTS.mono, fontSize: "10px", color: colorString(COLORS.gold), fontStyle: "bold" }).setDepth(170);
    this.sidePhysicsSolve = makeText(this, SIDE.x + 30, SIDE.y + 322, "SOLVE —", { fontFamily: FONTS.mono, fontSize: "8px", color: colorString(COLORS.green) }).setDepth(170);

    makeText(this, SIDE.x + 24, SIDE.y + 358, "AXIAL BUNDLE PROFILE", { fontFamily: FONTS.mono, fontSize: "9px", color: colorString(COLORS.gold), letterSpacing: 1 }).setDepth(170);
    this.sideProfileLegend = makeText(this, SIDE.x + SIDE.width - 22, SIDE.y + 358, "END A  →  END B", { fontFamily: FONTS.mono, fontSize: "8px", color: colorString(COLORS.ivoryMuted) }).setOrigin(1, 0).setDepth(170);
    this.sideProfileGraphics = this.add.graphics().setDepth(170);
    for (let index = 0; index < 12; index += 1) {
      const y = SIDE.y + 383 + index * 11.5;
      this.sideProfileNumbers.push(makeText(this, SIDE.x + 24, y - 1, String(index + 1).padStart(2, "0"), { fontFamily: FONTS.mono, fontSize: "8px", color: colorString(COLORS.ivoryMuted) }).setDepth(172));
      this.sideProfileValues.push(makeText(this, SIDE.x + 281, y - 1, "—", { fontFamily: FONTS.mono, fontSize: "8px", color: colorString(COLORS.ivoryMuted), align: "right" }).setOrigin(1, 0).setDepth(172));
    }
    graphics.lineStyle(1, COLORS.ivory, 0.14);
    graphics.lineBetween(SIDE.x + 24, SIDE.y + 535, SIDE.x + SIDE.width - 24, SIDE.y + 535);
    makeText(this, SIDE.x + 24, SIDE.y + 548, "SELECTED CHANNEL", { fontFamily: FONTS.mono, fontSize: "8px", color: colorString(COLORS.ivoryMuted), letterSpacing: 0.8 }).setDepth(170);
    makeText(this, SIDE.x + SIDE.width - 22, SIDE.y + 548, "ENTER / R  REFUEL", { fontFamily: FONTS.mono, fontSize: "8px", color: colorString(COLORS.cyan), align: "right" }).setOrigin(1, 0).setDepth(170);
    this.sideEvent = makeText(this, SIDE.x + 24, SIDE.y + 571, "Ready for an on-power shift.", { fontFamily: FONTS.body, fontSize: "11px", color: colorString(COLORS.ivoryMuted), wordWrap: { width: SIDE.width - 48 }, lineSpacing: 3 }).setDepth(170);

    this.refuelButton = makeButton(this, SIDE.x + 98, SIDE.y + 604, 150, 42, "REFUEL  ↗", () => this.openRefuelModal(), { tone: "gold", fontSize: 12 });
    this.refuelButton.gameObject.setDepth(180);
    this.controlButton = makeButton(this, SIDE.x + 258, SIDE.y + 604, 92, 42, "CTRL  C", () => this.openControlModal(), { tone: "magenta", fontSize: 11, compact: true });
    this.controlButton.gameObject.setDepth(180);
    makeText(this, SIDE.x + 24, SIDE.y + 660, "ARROWS / WASD  MOVE   ·   ESC  CLOSE", { fontFamily: FONTS.mono, fontSize: "8px", color: colorString(COLORS.ivoryMuted), letterSpacing: 0.3 }).setDepth(170);
  }

  private createMotionLayer(): void {
    this.motionGraphics = this.add.graphics().setDepth(150);
    this.motionText = makeText(this, 0, 0, "", { fontFamily: FONTS.mono, fontSize: "10px", color: colorString(COLORS.gold), backgroundColor: colorString(COLORS.ink), padding: { left: 6, right: 6, top: 4, bottom: 4 }, letterSpacing: 0.7 }).setOrigin(0.5).setDepth(151).setVisible(false);
  }

  private refreshAll(): void {
    this.refreshHud();
    this.refreshCore();
    this.refreshSidePanel();
    this.refreshModal();
    this.refreshUnavailableState();
  }

  private refreshHud(): void {
    const power = finiteOr(this.snapshot.physics.actualPowerFraction, this.snapshot.normalizedPowerFraction);
    const status = getOverallStatus(this.snapshot);
    this.hudDay?.setText(formatSimulationTime(this.snapshot.simulationTimeSeconds));
    this.hudPower?.setText(getPowerLabel(power));
    this.hudTilt?.setText(getTiltLabel(this.snapshot.absoluteTiltFraction));
    this.hudScore?.setText(String(Math.round(this.snapshot.scoreTotal)).padStart(6, "0"));
    this.hudFresh?.setText(`${this.snapshot.freshBundlesAvailable} FRESH BUNDLES`);
    this.hudStatus?.setText(status.toUpperCase()).setColor(colorString(status === "stable" ? COLORS.green : status === "watch" ? COLORS.gold : COLORS.red));
    this.hudSpeed?.setText(`${this.snapshot.isPaused ? "PAUSED" : this.snapshot.playbackModeId} / ${this.pending ? "COMMAND" : "LIVE"}`);
    for (const [index, button] of this.playbackButtons.entries()) {
      button.setEnabled(!this.pending && this.snapshot.core.channels.length === 380);
      if (this.snapshot.playbackModeId === ["pause", "1x", "10x", "60x"][index]) {
        button.gameObject.setAlpha(1);
      } else {
        button.gameObject.setAlpha(0.72);
      }
    }
  }

  private refreshCore(): void {
    if (this.snapshot.core.channels.length !== this.tiles.size) {
      return;
    }
    for (const tile of this.tiles.values()) {
      const next = this.snapshot.core.channels.find((channel) => channel.channelIndex === tile.channel.channelIndex);
      if (next !== undefined) {
        tile.channel = next;
      }
    }
    for (const tile of this.tiles.values()) {
      const position = projectChannelToFace(tile.channel, this.layout);
      const isSelected = tile.channel.channelIndex === this.selectedChannelIndex;
      const isHovered = tile.channel.channelIndex === this.hoveredChannelIndex;
      tile.container.setPosition(position.x, position.y);
      tile.container.setDepth(40 + tile.channel.gridRow / 1000);
      const width = this.layout.tileWidth + (isSelected ? 4 : isHovered ? 3 : 0);
      const height = this.layout.tileHeight + (isSelected ? 3 : isHovered ? 2 : 0);
      const heat = colorFromRgb(getHeatColor(tile.channel.localPowerFraction));
      tile.graphics.clear();
      tile.graphics.fillStyle(COLORS.ink, isSelected || isHovered ? 0.82 : 0.6);
      tile.graphics.fillRoundedRect(-width / 2 + 2, -height / 2 + 3, width + 2, height + 2, 3);
      tile.graphics.fillStyle(mixColor(heat, COLORS.white, isHovered ? 0.16 : 0), 0.98);
      tile.graphics.fillRoundedRect(-width / 2, -height / 2, width, height, 3);
      tile.graphics.fillStyle(COLORS.white, 0.08);
      tile.graphics.fillRoundedRect(-width / 2 + 1, -height / 2 + 1, width - 2, Math.max(2, height * 0.34), 2);
      tile.graphics.lineStyle(isSelected ? 2 : 0.8, isSelected ? COLORS.gold : COLORS.ink, isSelected ? 1 : 0.68);
      tile.graphics.strokeRoundedRect(-width / 2, -height / 2, width, height, 3);
      drawTileArrow(tile.graphics, tile.channel.flowDirection, width, height, isSelected ? COLORS.ink : COLORS.ivoryMuted);
    }
    this.drawSelection();
    this.drawPreviewVisual();
  }

  private refreshSidePanel(): void {
    this.refreshPhysicsReadout();
    const channel = this.getSelectedChannel();
    if (channel === undefined) {
      this.sideChannelId?.setText("NO CHANNEL");
      this.sideCoordinate?.setText("WAITING FOR CORE");
      this.sidePower?.setText("—");
      this.sideTilt?.setText("—");
      this.sideBurnup?.setText("AVG BURNUP —");
      this.sideFlow?.setText("—");
      this.sideEvent?.setText("CHANNEL DATA UNAVAILABLE");
      return;
    }
    this.sideChannelId?.setText(`CH ${String(channel.channelIndex).padStart(3, "0")}`);
    this.sideCoordinate?.setText(`${gridCoordinateLabel(channel)}  /  GRID COORDINATE`);
    this.sideFlow?.setText(`${getFlowArrow(channel.flowDirection)}  ${channel.flowDirection === "toward-end-b" ? "WITH FLOW" : "REVERSE"}`);
    this.sidePower?.setText(getPowerLabel(channel.localPowerFraction));
    this.sideTilt?.setText(getTiltLabel(channel.localTiltFraction));
    this.sideBurnup?.setText(`${channel.averageBurnupMwdPerKg.toFixed(1)} MWd/kg`);
    const lastEventDetail = this.snapshot.lastEvent?.detail;
    const contextualEvent = lastEventDetail !== undefined && !/select a channel/i.test(lastEventDetail)
      ? lastEventDetail
      : "Ready for an on-power shift.";
    this.sideEvent?.setText(this.resultMessage || contextualEvent);
    if (this.sideProfileGraphics !== null) {
      const graphics = this.sideProfileGraphics;
      graphics.clear();
      const maxPower = Math.max(1, ...channel.bundles.map((bundle) => bundle.powerWatts));
      channel.bundles.forEach((bundle, index) => {
        const y = SIDE.y + 383 + index * 11.5;
        const width = 198;
        const barWidth = Math.max(4, width * Math.max(0, bundle.powerWatts) / maxPower);
        const color = bundle.isFresh ? COLORS.cyan : mixColor(COLORS.magentaDark, COLORS.gold, Math.min(1, bundle.currentBurnupMwdPerKg / 8_000));
        graphics.fillStyle(COLORS.ink, 0.78);
        graphics.fillRoundedRect(SIDE.x + 75, y, width, 9, 2);
        graphics.fillStyle(color, bundle.isFresh ? 0.82 : 0.9);
        graphics.fillRoundedRect(SIDE.x + 75, y, barWidth, 9, 2);
        graphics.lineStyle(1, COLORS.ivory, 0.12);
        graphics.strokeRoundedRect(SIDE.x + 75, y, width, 9, 2);
        this.sideProfileNumbers[index]?.setText(String(index + 1).padStart(2, "0"));
        this.sideProfileValues[index]?.setText(bundle.isFresh ? "FRESH" : `${(bundle.currentBurnupMwdPerKg / 1000).toFixed(1)}k`).setColor(colorString(bundle.isFresh ? COLORS.cyan : COLORS.ivoryMuted));
      });
    }
    this.refuelButton?.setEnabled(!this.pending && this.snapshot.core.channels.length === 380 && this.snapshot.freshBundlesAvailable >= 4);
    this.controlButton?.setEnabled(!this.pending && this.snapshot.core.channels.length === 380);
  }

  private refreshPhysicsReadout(): void {
    const physics = this.snapshot.physics;
    this.sidePhysicsCore?.setText(`CORE REACTIVITY   ${formatReactivity(physics.coreReactivity)}`);
    this.sidePhysicsStatic?.setText(`STATIC            ${formatReactivity(physics.staticReactivity)}`);
    this.sidePhysicsNet?.setText(`NET COMP          ${formatReactivity(physics.compensatedNetReactivity)}`);
    this.sidePhysicsK?.setText(`EFFECTIVE K       ${formatEffectiveK(physics.effectiveK)}`);
    const solveColor = this.snapshot.diagnostics.convergence.state === "converged" ? COLORS.green : COLORS.gold;
    this.sidePhysicsSolve?.setText(`SOLVE ${formatSolveHealth(this.snapshot)}`).setColor(colorString(solveColor));
  }

  private refreshUnavailableState(): void {
    if (this.snapshot.core.channels.length === 380 && this.session.status.isWasmAvailable) {
      this.unavailableOverlay?.setVisible(false);
      return;
    }
    this.unavailableOverlay?.setVisible(true);
    this.unavailableTitleText?.setText(this.session.status.source === "loading" ? "CONNECTING TO COMMAND BRIDGE" : "AUTHORITATIVE BRIDGE UNAVAILABLE");
    this.unavailableDetailText?.setText(this.session.status.detail);
  }

  private createUnavailableOverlay(): void {
    const container = this.add.container(0, 0).setDepth(600).setVisible(false);
    const graphics = this.add.graphics();
    graphics.fillStyle(COLORS.ink, 0.88);
    graphics.fillRect(0, HUD_HEIGHT, VIEW_WIDTH, VIEW_HEIGHT - HUD_HEIGHT);
    graphics.fillStyle(COLORS.panelRaised, 0.96);
    graphics.fillRoundedRect(430, 300, 740, 240, 10);
    graphics.lineStyle(2, COLORS.red, 0.76);
    graphics.strokeRoundedRect(430, 300, 740, 240, 10);
    drawCornerBrackets(graphics, 430, 300, 740, 240, COLORS.gold);
    container.add(graphics);
    const title = makeText(this, 800, 355, "", { fontFamily: FONTS.display, fontSize: "30px", color: colorString(COLORS.ivory), fontStyle: "bold", align: "center" }).setOrigin(0.5);
    const detail = makeText(this, 800, 420, "", { fontFamily: FONTS.body, fontSize: "16px", color: colorString(COLORS.ivoryMuted), align: "center", wordWrap: { width: 610 } }).setOrigin(0.5);
    container.add([title, detail]);
    this.unavailableOverlay = container;
    this.unavailableTitleText = title;
    this.unavailableDetailText = detail;
  }

  private receiveSessionUpdate(update: SessionUpdate): void {
    this.snapshot = update.snapshot;
    this.pending = update.pending;
    if (update.error !== null && update.error !== this.lastError) {
      this.lastError = update.error;
      this.showToast(update.error, COLORS.red);
    }
    if (update.response !== null && update.response.sequence !== this.lastHandledResponseSequence) {
      this.lastHandledResponseSequence = update.response.sequence;
      this.handleCommandResponse(update.response);
    }
    this.refreshAll();
  }

  private handleCommandResponse(response: CanduCommandResponse): void {
    if (response.command.type === "preview-refuel") {
      this.preview = response.preview;
      this.refreshModal();
      return;
    }
    if (response.command.type === "commit-refuel") {
      const request = response.command.request;
      const origin = projectChannelToFace({ gridColumn: this.getSelectedChannel()?.gridColumn ?? 0, gridRow: this.getSelectedChannel()?.gridRow ?? 0 }, this.layout);
      this.modalKind = null;
      this.destroyModal();
      this.motion = { request, response, elapsed: 0, duration: this.reducedMotion ? 1 : 920, origin };
      this.motionText?.setVisible(true).setText(`TRANSFER  /  CH ${String(request.channelIndex).padStart(3, "0")}`);
      this.resultMessage = "TRANSFER IN PROGRESS…";
      this.resultExpiresAt = 0;
      this.refreshSidePanel();
      return;
    }
    if (response.command.type !== "advance") {
      this.refreshModal();
    }
  }

  private selectChannel(channelIndex: number): void {
    if (this.modalKind !== null || !this.snapshot.core.channels.some((channel) => channel.channelIndex === channelIndex)) {
      return;
    }
    this.selectedChannelIndex = channelIndex;
    this.preview = null;
    this.lastError = "";
    this.refreshCore();
    this.refreshSidePanel();
  }

  private openRefuelModal(): void {
    const channel = this.getSelectedChannel();
    if (channel === undefined || this.pending || !this.session.status.isWasmAvailable) {
      return;
    }
    this.destroyModal();
    this.modalKind = "refuel";
    this.refuelDraft = createRefuelDraft(channel);
    this.preview = null;
    this.createRefuelModalObjects();
    this.refreshModal();
    this.refreshCore();
  }

  private createRefuelModalObjects(): void {
    this.createModalFrame("REFUELLING ORDER", "STAGE AN ON-POWER SHIFT");
    this.modalButtons.push(
      this.addModalButton(438, 696, 150, 42, "4 BUNDLES", () => { if (this.refuelDraft !== null) { this.refuelDraft.shiftCount = 4; this.preview = null; this.refreshModal(); this.refreshCore(); } }, { tone: "cyan", fontSize: 11 }),
      this.addModalButton(600, 696, 150, 42, "8 BUNDLES", () => { if (this.refuelDraft !== null) { this.refuelDraft.shiftCount = 8; this.preview = null; this.refreshModal(); this.refreshCore(); } }, { tone: "cyan", fontSize: 11 }),
      this.addModalButton(438, 748, 312, 42, "REVERSE DIRECTION  ↔", () => { if (this.refuelDraft !== null) { this.refuelDraft.directionId = toggleRefuelDirection(this.refuelDraft.directionId); this.preview = null; this.refreshModal(); this.refreshCore(); } }, { tone: "magenta", fontSize: 11 }),
      this.addModalButton(842, 748, 150, 42, "CANCEL", () => this.closeModal(), { tone: "quiet", fontSize: 11 }),
      this.addModalButton(1002, 748, 206, 42, "PREVIEW SHIFT  ↗", () => this.dispatchRefuelPreview(), { tone: "gold", fontSize: 11 }),
      this.addModalButton(1002, 696, 206, 42, "CONFIRM SHIFT  ✓", () => this.dispatchRefuelCommit(), { tone: "gold", fontSize: 11 }),
    );
  }

  private dispatchRefuelPreview(): void {
    if (this.refuelDraft === null || this.pending) {
      return;
    }
    void this.session.dispatch({ type: "preview-refuel", request: toRefuelRequest(this.refuelDraft) }).catch(() => undefined);
  }

  private dispatchRefuelCommit(): void {
    if (this.refuelDraft === null || !canConfirmRefuel(this.preview, this.refuelDraft, this.snapshot.freshBundlesAvailable, this.pending)) {
      return;
    }
    void this.session.dispatch({ type: "commit-refuel", request: toRefuelRequest(this.refuelDraft) }).catch(() => undefined);
  }

  private openControlModal(): void {
    if (this.pending || !this.session.status.isWasmAvailable) {
      return;
    }
    this.destroyModal();
    this.modalKind = "control";
    this.controlPowerTarget = this.snapshot.targetPowerFraction;
    this.controlTiltTarget = this.snapshot.targetTiltFraction;
    this.createControlModalObjects();
    this.refreshModal();
  }

  private createControlModalObjects(): void {
    this.createModalFrame("REACTOR CONTROL", "SETPOINTS / TIME MANAGEMENT");
    this.modalDetail?.setFontSize("14px");
    this.modalValueE?.setPosition(805, 555);
    this.modalButtons.push(
      this.addModalButton(660, 430, 55, 42, "−", () => { this.controlPowerTarget = adjustTarget(this.controlPowerTarget, -0.01, 0.8, 1.2); this.refreshModal(); }, { tone: "cyan", fontSize: 20, compact: true }),
      this.addModalButton(725, 430, 55, 42, "+", () => { this.controlPowerTarget = adjustTarget(this.controlPowerTarget, 0.01, 0.8, 1.2); this.refreshModal(); }, { tone: "cyan", fontSize: 20, compact: true }),
      this.addModalButton(1010, 430, 172, 42, "QUEUE POWER", () => this.queuePowerTarget(), { tone: "cyan", fontSize: 11 }),
      this.addModalButton(660, 515, 55, 42, "−", () => { this.controlTiltTarget = adjustTarget(this.controlTiltTarget, -0.01, -0.2, 0.2); this.refreshModal(); }, { tone: "magenta", fontSize: 20, compact: true }),
      this.addModalButton(725, 515, 55, 42, "+", () => { this.controlTiltTarget = adjustTarget(this.controlTiltTarget, 0.01, -0.2, 0.2); this.refreshModal(); }, { tone: "magenta", fontSize: 20, compact: true }),
      this.addModalButton(1010, 515, 172, 42, "QUEUE TILT", () => this.queueTiltTarget(), { tone: "magenta", fontSize: 11 }),
      this.addModalButton(468, 606, 150, 42, "+1 HOUR", () => this.stepSimulation(3600), { tone: "gold", fontSize: 11 }),
      this.addModalButton(628, 606, 150, 42, "+8 HOURS", () => this.stepSimulation(28_800), { tone: "gold", fontSize: 11 }),
      this.addModalButton(788, 606, 150, 42, "+1 DAY", () => this.stepSimulation(86_400), { tone: "gold", fontSize: 11 }),
      this.addModalButton(1010, 606, 172, 42, this.snapshot.isPaused ? "RESUME  ▶" : "PAUSE  Ⅱ", () => this.setPlayback(this.snapshot.isPaused ? "10x" : "pause"), { tone: "quiet", fontSize: 11 }),
      this.addModalButton(1010, 696, 172, 42, "CLOSE", () => this.closeModal(), { tone: "quiet", fontSize: 11 }),
    );
  }

  private queuePowerTarget(): void {
    if (this.pending) return;
    void this.session.dispatch({ type: "queue-power-target", targetFraction: this.controlPowerTarget }).catch(() => undefined);
  }

  private queueTiltTarget(): void {
    if (this.pending) return;
    void this.session.dispatch({ type: "queue-tilt-target", targetFraction: this.controlTiltTarget }).catch(() => undefined);
  }

  private stepSimulation(simulationSeconds: number): void {
    if (this.pending || !this.snapshot.isPaused) return;
    void this.session.dispatch({ type: "step", simulationSeconds }).catch(() => undefined);
  }

  private setPlayback(modeId: PlaybackModeId): void {
    if (this.pending || !this.session.status.isWasmAvailable) return;
    void this.session.dispatch({ type: "set-playback-mode", modeId }).catch(() => undefined);
  }

  private createModalFrame(title: string, subtitle: string): void {
    this.modalBackdrop = this.add.graphics().setDepth(490);
    this.modalBackdrop.fillStyle(COLORS.ink, 0.78);
    this.modalBackdrop.fillRect(0, 0, VIEW_WIDTH, VIEW_HEIGHT);
    this.modalGraphics = this.add.graphics().setDepth(500);
    drawPanelFrame(this.modalGraphics, 376, 184, 832, 640, { fill: COLORS.panel, alpha: 0.985, accent: COLORS.gold, lineWidth: 2 });
    this.modalGraphics.fillStyle(COLORS.magentaDark, 0.24);
    this.modalGraphics.fillRect(398, 210, 788, 3);
    this.modalGraphics.lineStyle(1, COLORS.ivory, 0.17);
    this.modalGraphics.lineBetween(416, 318, 1168, 318);
    this.modalGraphics.lineBetween(416, 662, 1168, 662);
    this.modalTitle = this.addModalText(416, 224, title, { fontFamily: FONTS.display, fontSize: "27px", color: colorString(COLORS.ivory), fontStyle: "bold" });
    this.modalSubtitle = this.addModalText(418, 268, subtitle, { fontFamily: FONTS.mono, fontSize: "10px", color: colorString(COLORS.cyan), letterSpacing: 1.4 });
    this.modalStatus = this.addModalText(416, 283, "", { fontFamily: FONTS.mono, fontSize: "10px", color: colorString(COLORS.gold), align: "right" }).setOrigin(1, 0);
    this.modalStatus.setX(1168);
    this.modalPreviewGraphics = this.add.graphics().setDepth(505);
    this.modalDetail = this.addModalText(416, 335, "", { fontFamily: FONTS.body, fontSize: "15px", color: colorString(COLORS.ivoryMuted), wordWrap: { width: 324 }, lineSpacing: 4 });
    this.modalValueA = this.addModalText(805, 345, "", { fontFamily: FONTS.mono, fontSize: "16px", color: colorString(COLORS.ivory), fontStyle: "bold" });
    this.modalValueB = this.addModalText(805, 386, "", { fontFamily: FONTS.mono, fontSize: "16px", color: colorString(COLORS.cyan), fontStyle: "bold" });
    this.modalValueC = this.addModalText(805, 427, "", { fontFamily: FONTS.mono, fontSize: "16px", color: colorString(COLORS.magenta), fontStyle: "bold" });
    this.modalValueD = this.addModalText(805, 468, "", { fontFamily: FONTS.mono, fontSize: "16px", color: colorString(COLORS.gold), fontStyle: "bold" });
    this.modalValueE = this.addModalText(805, 509, "", { fontFamily: FONTS.mono, fontSize: "16px", color: colorString(COLORS.green), fontStyle: "bold" });
  }

  private addModalText(x: number, y: number, value: string, style: Phaser.Types.GameObjects.Text.TextStyle): Phaser.GameObjects.Text {
    const text = makeText(this, x, y, value, style).setDepth(510);
    this.modalObjects.push(text);
    return text;
  }

  private addModalButton(x: number, y: number, width: number, height: number, label: string, onClick: () => void, options: Parameters<typeof makeButton>[7] = {}): TacticalButton {
    const button = makeButton(this, x + width / 2, y + height / 2, width, height, label, onClick, options);
    button.gameObject.setDepth(520);
    this.modalObjects.push(button.gameObject);
    return button;
  }

  private refreshModal(): void {
    if (this.modalKind === null) {
      return;
    }
    if (this.modalKind === "refuel") {
      this.refreshRefuelModal();
    } else {
      this.refreshControlModal();
    }
  }

  private refreshRefuelModal(): void {
    if (this.refuelDraft === null || this.modalDetail === null || this.modalValueA === null || this.modalValueB === null || this.modalValueC === null || this.modalValueD === null || this.modalValueE === null || this.modalStatus === null || this.modalPreviewGraphics === null) {
      return;
    }
    const channel = this.getSelectedChannel();
    if (channel === undefined) return;
    const previewReady = previewMatchesDraft(this.preview, this.refuelDraft);
    this.modalStatus.setText(previewReady ? "AUTHORITATIVE PREVIEW READY" : this.pending ? "COMMAND IN FLIGHT…" : "AWAITING PREVIEW").setColor(colorString(previewReady ? COLORS.green : COLORS.gold));
    this.modalDetail.setText(`${String(channel.channelIndex).padStart(3, "0")}  /  ${gridCoordinateLabel(channel)}\n\n${formatRefuelDirection(this.refuelDraft.directionId)}\n\n${this.refuelDraft.shiftCount} bundle transfer\nNAT-U-SYNTHETIC\n\n${previewReady ? "The highlighted path is the projected local transition. Confirm only after reviewing the response." : "Choose a shift size and direction, then request an authoritative preview from the live command bridge."}`);
    this.modalValueA.setText(`POWER        ${previewReady && this.preview !== null ? formatSignedNumber(this.preview.localPowerDeltaFraction * 100, 2) + " pts" : "—"}`);
    this.modalValueB.setText(`TILT         ${previewReady && this.preview !== null ? formatSignedNumber(this.preview.localTiltDeltaFraction * 100, 2) + " pts" : "—"}`);
    this.modalValueC.setText(`REACTIVITY   ${previewReady && this.preview !== null ? formatReactivity(this.preview.predictedReactivityDelta) : "—"}`);
    this.modalValueD.setText(`SCORE        ${previewReady && this.preview !== null ? formatSignedNumber(this.preview.projectedScoreDelta, 1) : "—"}`);
    this.modalValueE.setText(`DISCHARGE    ${previewReady && this.preview !== null ? this.preview.dischargeBurnupMwdPerKg.toFixed(1) + " MWd/kg" : "—"}`);
    this.modalPreviewGraphics.clear();
    const vector = getFlowVector(this.refuelDraft.directionId);
    const length = Math.hypot(vector.x, vector.y);
    const vx = vector.x / length;
    const vy = vector.y / length;
    const centerX = 1005;
    const centerY = 585;
    const pathLength = 110;
    this.modalPreviewGraphics.lineStyle(12, COLORS.magenta, 0.13);
    this.modalPreviewGraphics.lineBetween(centerX - vx * pathLength, centerY - vy * pathLength, centerX + vx * pathLength, centerY + vy * pathLength);
    this.modalPreviewGraphics.lineStyle(2, previewReady ? COLORS.cyan : COLORS.gold, 0.88);
    this.modalPreviewGraphics.lineBetween(centerX - vx * pathLength, centerY - vy * pathLength, centerX + vx * pathLength, centerY + vy * pathLength);
    for (let index = 0; index < 12; index += 1) {
      const offset = -93 + index * 17;
      const tokenColor = index < this.refuelDraft.shiftCount ? COLORS.cyan : COLORS.magenta;
      this.modalPreviewGraphics.fillStyle(tokenColor, index < this.refuelDraft.shiftCount ? 0.94 : 0.35);
      this.modalPreviewGraphics.fillRoundedRect(centerX + vx * offset - 10, centerY + vy * offset - 6.5, 20, 13, 3);
    }
    this.modalPreviewGraphics.fillStyle(COLORS.gold, 0.95);
    this.modalPreviewGraphics.fillTriangle(centerX + vx * 132, centerY + vy * 132, centerX + vx * 112 - vy * 11, centerY + vy * 112 + vx * 11, centerX + vx * 112 + vy * 11, centerY + vy * 112 - vx * 11);
    this.modalButtons[0]?.setEnabled(!this.pending);
    this.modalButtons[1]?.setEnabled(!this.pending);
    this.modalButtons[2]?.setEnabled(!this.pending);
    this.modalButtons[3]?.setEnabled(!this.pending);
    this.modalButtons[4]?.setEnabled(!this.pending);
    this.modalButtons[5]?.setEnabled(!this.pending && previewReady && canConfirmRefuel(this.preview, this.refuelDraft, this.snapshot.freshBundlesAvailable, false));
  }

  private refreshControlModal(): void {
    if (this.modalDetail === null || this.modalValueA === null || this.modalValueB === null || this.modalValueC === null || this.modalValueD === null || this.modalValueE === null || this.modalStatus === null) return;
    this.modalStatus.setText(this.snapshot.isPaused ? "SHIFT PAUSED / TIME STEP AVAILABLE" : "SHIFT RUNNING / TARGETS QUEUED").setColor(colorString(this.snapshot.isPaused ? COLORS.gold : COLORS.green));
    this.modalDetail.setText("Queue targets to automatic regulation.\nTime steps are available while paused.\n\n↑/↓  POWER     ←/→  TILT\nP  QUEUE POWER     T  QUEUE TILT");
    this.modalValueA.setText(`POWER TARGET   ${getPowerLabel(this.controlPowerTarget)}`);
    this.modalValueB.setText(`ACTIVE POWER   ${getPowerLabel(finiteOr(this.snapshot.physics.actualPowerFraction, this.snapshot.normalizedPowerFraction))}`);
    this.modalValueC.setText(`TILT TARGET    ${getTiltLabel(this.controlTiltTarget)}`);
    this.modalValueD.setText(`ACTIVE TILT    ${getTiltLabel(this.snapshot.absoluteTiltFraction)}`);
    this.modalValueE.setText(`TIME           ${formatSimulationTime(this.snapshot.simulationTimeSeconds)}`);
    const stepEnabled = !this.pending && this.snapshot.isPaused;
    this.modalButtons[0]?.setEnabled(!this.pending);
    this.modalButtons[1]?.setEnabled(!this.pending);
    this.modalButtons[2]?.setEnabled(!this.pending);
    this.modalButtons[3]?.setEnabled(!this.pending);
    this.modalButtons[4]?.setEnabled(!this.pending);
    this.modalButtons[5]?.setEnabled(!this.pending);
    this.modalButtons[6]?.setEnabled(stepEnabled);
    this.modalButtons[7]?.setEnabled(stepEnabled);
    this.modalButtons[8]?.setEnabled(stepEnabled);
    this.modalButtons[9]?.setEnabled(!this.pending);
    this.modalButtons[10]?.setEnabled(!this.pending);
  }

  private closeModal(): void {
    this.modalKind = null;
    this.refuelDraft = null;
    this.preview = null;
    this.destroyModal();
    this.refreshCore();
    this.refreshSidePanel();
  }

  private destroyModal(): void {
    for (const object of this.modalObjects) {
      object.destroy();
    }
    this.modalObjects.length = 0;
    this.modalButtons.length = 0;
    this.modalBackdrop?.destroy();
    this.modalGraphics?.destroy();
    this.modalPreviewGraphics?.destroy();
    this.modalBackdrop = null;
    this.modalGraphics = null;
    this.modalPreviewGraphics = null;
    this.modalTitle = null;
    this.modalSubtitle = null;
    this.modalStatus = null;
    this.modalDetail = null;
    this.modalValueA = null;
    this.modalValueB = null;
    this.modalValueC = null;
    this.modalValueD = null;
    this.modalValueE = null;
  }

  private drawMotion(): void {
    if (this.motionGraphics === null || this.motion === null) return;
    const graphics = this.motionGraphics;
    graphics.clear();
    const motion = this.motion;
    const vector = getFlowVector(motion.request.directionId);
    const length = Math.hypot(vector.x, vector.y);
    const vx = vector.x / length;
    const vy = vector.y / length;
    const progress = Phaser.Math.Clamp(motion.elapsed / motion.duration, 0, 1);
    const eased = progress * progress * (3 - 2 * progress);
    graphics.fillStyle(COLORS.magenta, 0.14);
    graphics.fillCircle(motion.origin.x, motion.origin.y, 46 + eased * 18);
    graphics.lineStyle(5, COLORS.cyan, 0.82);
    graphics.lineBetween(motion.origin.x - vx * 86, motion.origin.y - vy * 86, motion.origin.x + vx * 86, motion.origin.y + vy * 86);
    for (let index = 0; index < motion.request.shiftCount; index += 1) {
      const stagger = (index - (motion.request.shiftCount - 1) / 2) * 18;
      const offset = (eased - 0.5) * 160 + stagger;
      const x = motion.origin.x + vx * offset;
      const y = motion.origin.y + vy * offset;
      graphics.fillStyle(index % 2 === 0 ? COLORS.cyan : COLORS.gold, 0.96);
      graphics.fillRoundedRect(x - 7.5, y - 5, 15, 10, 3);
      graphics.lineStyle(1, COLORS.ivory, 0.68);
      graphics.strokeRoundedRect(x - 7.5, y - 5, 15, 10, 3);
    }
    graphics.fillStyle(COLORS.magenta, 0.92);
    for (let index = 0; index < motion.request.shiftCount; index += 1) {
      const offset = (0.5 - eased) * 150 + (index - (motion.request.shiftCount - 1) / 2) * 18;
      const x = motion.origin.x + vx * offset;
      const y = motion.origin.y + vy * offset + 8;
      graphics.fillRoundedRect(x - 4.5, y - 3, 9, 6, 2);
    }
    this.motionText?.setPosition(motion.origin.x, motion.origin.y - 44).setVisible(true);
  }

  private showToast(message: string, color: number): void {
    const toast = this.add.container(800, 850).setDepth(700);
    const graphics = this.add.graphics();
    graphics.fillStyle(COLORS.ink, 0.94);
    graphics.fillRoundedRect(-300, -20, 600, 40, 6);
    graphics.lineStyle(1, color, 0.86);
    graphics.strokeRoundedRect(-300, -20, 600, 40, 6);
    const text = makeText(this, 0, 0, message, { fontFamily: FONTS.mono, fontSize: "11px", color: colorString(COLORS.ivory), align: "center", wordWrap: { width: 560 } }).setOrigin(0.5);
    toast.add([graphics, text]);
    this.time.delayedCall(4200, () => toast.destroy());
  }

  private handleKeyDown(event: KeyboardEvent): void {
    const key = event.key.toLowerCase();
    if (this.modalKind === "refuel") {
      if (key === "escape") { this.closeModal(); return; }
      if (key === "4" && this.refuelDraft !== null) { this.refuelDraft.shiftCount = 4; this.preview = null; this.refreshModal(); this.refreshCore(); return; }
      if (key === "8" && this.refuelDraft !== null) { this.refuelDraft.shiftCount = 8; this.preview = null; this.refreshModal(); this.refreshCore(); return; }
      if (key === "d" && this.refuelDraft !== null) { this.refuelDraft.directionId = toggleRefuelDirection(this.refuelDraft.directionId); this.preview = null; this.refreshModal(); this.refreshCore(); return; }
      if (key === "enter") { if (previewMatchesDraft(this.preview, this.refuelDraft!)) this.dispatchRefuelCommit(); else this.dispatchRefuelPreview(); return; }
      return;
    }
    if (this.modalKind === "control") {
      if (key === "escape") { this.closeModal(); return; }
      if (key === "arrowup" || key === "w") { this.controlPowerTarget = adjustTarget(this.controlPowerTarget, 0.01, 0.8, 1.2); this.refreshModal(); return; }
      if (key === "arrowdown" || key === "s") { this.controlPowerTarget = adjustTarget(this.controlPowerTarget, -0.01, 0.8, 1.2); this.refreshModal(); return; }
      if (key === "arrowright" || key === "d") { this.controlTiltTarget = adjustTarget(this.controlTiltTarget, 0.01, -0.2, 0.2); this.refreshModal(); return; }
      if (key === "arrowleft" || key === "a") { this.controlTiltTarget = adjustTarget(this.controlTiltTarget, -0.01, -0.2, 0.2); this.refreshModal(); return; }
      if (key === "p") { this.queuePowerTarget(); return; }
      if (key === "t") { this.queueTiltTarget(); return; }
      return;
    }
    if (key === "escape") return;
    if (key === "r" || key === "enter") { this.openRefuelModal(); return; }
    if (key === "c") { this.openControlModal(); return; }
    if (key === "1") { this.setPlayback("1x"); return; }
    if (key === "2") { this.setPlayback("10x"); return; }
    if (key === "3") { this.setPlayback("60x"); return; }
    if (key === "4" || key === " ") { this.setPlayback("pause"); return; }
    const movement: Record<string, [number, number]> = {
      arrowleft: [-1, 0], a: [-1, 0], arrowright: [1, 0], d: [1, 0],
      arrowup: [0, -1], w: [0, -1], arrowdown: [0, 1], s: [0, 1],
    };
    const delta = movement[key];
    if (delta !== undefined) {
      const next = findAdjacentChannelIndex(this.snapshot.core.channels, this.selectedChannelIndex, delta[0], delta[1]);
      this.selectChannel(next);
    }
  }

  private getSelectedChannel(): CanduChannelSnapshot | undefined {
    return this.snapshot.core.channels.find((channel) => channel.channelIndex === this.selectedChannelIndex);
  }
}

function chooseInitialChannel(snapshot: CanduSnapshot): number {
  if (snapshot.core.channels.some((channel) => channel.channelIndex === 210)) return 210;
  return snapshot.core.channels[0]?.channelIndex ?? -1;
}

function finiteOr(value: number, fallback: number): number {
  return Number.isFinite(value) ? value : fallback;
}

function drawTileArrow(graphics: Phaser.GameObjects.Graphics, flowDirection: RefuelRequest["directionId"], width: number, height: number, color: number): void {
  const direction = flowDirection === "toward-end-b" ? 1 : -1;
  graphics.fillStyle(color, 0.86);
  graphics.fillTriangle(direction * width * 0.28, height * 0.02, direction * width * 0.07, -height * 0.15, direction * width * 0.07, height * 0.18);
}
