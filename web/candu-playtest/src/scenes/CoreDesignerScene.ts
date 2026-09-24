import Phaser from "phaser";
import { getRuntimeSession } from "../runtime";
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
import type { SessionUpdate } from "../sessionController";
import type {
  CanduBundleSnapshot,
  CanduChannelSnapshot,
  CanduSnapshot,
  CoreBoundaryFace,
} from "../protocol";
import {
  CANDU6_ROW_LABELS,
  createCoreFaceLayout,
  findAdjacentChannelIndex,
  gridCoordinateLabel,
  projectChannelToFace,
  type CoreFaceLayout,
} from "../projection";
import {
  formatEffectiveK,
  formatPowerWatts,
  formatSimulationTime,
  formatSolveHealth,
  getHeatColor,
} from "../visuals";

const VIEW_WIDTH = 1600;
const VIEW_HEIGHT = 900;
const HEADER_HEIGHT = 116;
const MAP = { x: 24, y: 136, width: 1032, height: 690 } as const;
const SIDE = { x: 1078, y: 136, width: 498, height: 690 } as const;
const AXIAL = { x: 1098, y: 282, width: 196, height: 492 } as const;
const EDITOR = { x: 1310, y: 282, width: 246, height: 492 } as const;
const FACE_ORDER: readonly CoreBoundaryFace[] = [
  "north",
  "east",
  "south",
  "west",
  "end-a",
  "end-b",
];

interface ChannelTile {
  channelIndex: number;
  container: Phaser.GameObjects.Container;
  graphics: Phaser.GameObjects.Graphics;
}

interface AxialRow {
  position: number;
  container: Phaser.GameObjects.Container;
  graphics: Phaser.GameObjects.Graphics;
  positionText: Phaser.GameObjects.Text;
  stateText: Phaser.GameObjects.Text;
  fluxText: Phaser.GameObjects.Text;
  powerText: Phaser.GameObjects.Text;
}

/**
 * Live Core Designer. This is a view over the same 380 × 12 snapshot used by
 * Operations; it has no second fixture or local reactor state.
 */
export class CoreDesignerScene extends Phaser.Scene {
  private readonly session = getRuntimeSession();
  private readonly tiles = new Map<number, ChannelTile>();
  private readonly axialRows: AxialRow[] = [];
  private readonly faceButtons = new Map<CoreBoundaryFace, TacticalButton>();
  private snapshot: CanduSnapshot = this.session.snapshot;
  private selectedChannelIndex = -1;
  private selectedPosition = 0;
  private pending = false;
  private resultMessage = "Select a channel, then an axial bundle position.";
  private lastResponseSequence = -1;
  private returnChannelIndex = -1;
  private layout: CoreFaceLayout = createCoreFaceLayout(MAP.x, MAP.y, MAP.width, MAP.height);
  private unsubscribe: (() => void) | null = null;
  private mapGraphics: Phaser.GameObjects.Graphics | null = null;
  private selectionGraphics: Phaser.GameObjects.Graphics | null = null;
  private channelText: Phaser.GameObjects.Text | null = null;
  private coordinateText: Phaser.GameObjects.Text | null = null;
  private channelSummaryText: Phaser.GameObjects.Text | null = null;
  private selectedCellText: Phaser.GameObjects.Text | null = null;
  private selectedStateText: Phaser.GameObjects.Text | null = null;
  private selectedFluxText: Phaser.GameObjects.Text | null = null;
  private selectedPowerText: Phaser.GameObjects.Text | null = null;
  private selectedFacesText: Phaser.GameObjects.Text | null = null;
  private solveText: Phaser.GameObjects.Text | null = null;
  private feedbackText: Phaser.GameObjects.Text | null = null;
  private livePowerText: Phaser.GameObjects.Text | null = null;
  private liveRrsText: Phaser.GameObjects.Text | null = null;
  private liveScoreText: Phaser.GameObjects.Text | null = null;
  private liveFreshText: Phaser.GameObjects.Text | null = null;
  private liveTimeText: Phaser.GameObjects.Text | null = null;
  private fuelButton: TacticalButton | null = null;
  private solveButton: TacticalButton | null = null;
  private backButton: TacticalButton | null = null;
  private unavailableOverlay: Phaser.GameObjects.Container | null = null;
  private unavailableTitle: Phaser.GameObjects.Text | null = null;
  private unavailableDetail: Phaser.GameObjects.Text | null = null;

  public constructor() {
    super("CoreDesignerScene");
  }

  public init(data: unknown): void {
    this.returnChannelIndex = isSceneChannelIndex(data) ? data.returnChannelIndex : -1;
  }

  public create(): void {
    // Stop only the browser clock while the operator is editing. Commands
    // still go to the active live GameSession through BridgeSessionController.
    this.session.stopShift();
    this.tiles.clear();
    this.axialRows.length = 0;
    this.faceButtons.clear();
    this.pending = false;
    this.lastResponseSequence = -1;
    this.resultMessage = "Select a channel, then an axial bundle position.";
    this.snapshot = this.session.snapshot;
    this.selectedChannelIndex = this.snapshot.core.channels.some((channel) =>
      channel.channelIndex === this.returnChannelIndex)
      ? this.returnChannelIndex
      : chooseInitialChannel(this.snapshot);
    this.selectedPosition = 0;

    this.createBackdrop();
    this.createToolbar();
    this.createMap();
    this.createInspector();
    this.createUnavailableOverlay();
    this.refresh();
    this.unsubscribe = this.session.subscribe((update) => this.receiveSessionUpdate(update));
    this.events.once("shutdown", () => this.unsubscribe?.());
    this.input.keyboard?.on("keydown", this.handleKeyDown, this);
    this.events.once("shutdown", () => this.input.keyboard?.off("keydown", this.handleKeyDown, this));
  }

  public update(time: number): void {
    this.drawSelection(time);
  }

  private createBackdrop(): void {
    const background = this.add.graphics().setDepth(-20);
    background.fillStyle(COLORS.void, 1);
    background.fillRect(0, 0, VIEW_WIDTH, VIEW_HEIGHT);
    background.fillStyle(COLORS.navy, 1);
    background.fillRect(0, 0, VIEW_WIDTH, VIEW_HEIGHT);
    background.fillStyle(COLORS.magentaDark, 0.06);
    background.fillTriangle(0, HEADER_HEIGHT, 720, HEADER_HEIGHT, 390, VIEW_HEIGHT);
    background.fillStyle(COLORS.cyanDark, 0.06);
    background.fillTriangle(900, HEADER_HEIGHT, VIEW_WIDTH, HEADER_HEIGHT, 1380, VIEW_HEIGHT);
    background.lineStyle(1, COLORS.gold, 0.2);
    background.lineBetween(0, HEADER_HEIGHT, VIEW_WIDTH, HEADER_HEIGHT);
    background.lineBetween(0, VIEW_HEIGHT - 50, VIEW_WIDTH, VIEW_HEIGHT - 50);
    background.fillStyle(COLORS.ink, 0.92);
    background.fillRect(0, 0, VIEW_WIDTH, HEADER_HEIGHT - 2);
    background.fillStyle(COLORS.magenta, 0.8);
    background.fillRect(24, 20, 5, 74);
    background.lineStyle(1.5, COLORS.gold, 0.7);
    background.lineBetween(0, HEADER_HEIGHT - 3, VIEW_WIDTH, HEADER_HEIGHT - 3);
    drawCornerBrackets(background, 18, 92, VIEW_WIDTH - 36, 734, COLORS.gold);
  }

  private createToolbar(): void {
    makeText(this, 48, 17, "CANDU", {
      fontFamily: FONTS.display,
      fontSize: "22px",
      color: colorString(COLORS.ivory),
      fontStyle: "bold",
      letterSpacing: 2,
    }).setDepth(100);
    makeText(this, 48, 49, "CORE DESIGNER", {
      fontFamily: FONTS.mono,
      fontSize: "11px",
      color: colorString(COLORS.magenta),
      letterSpacing: 1.9,
    }).setDepth(100);
    makeText(this, 48, 73, "LIVE 380 CHANNELS × 12 POSITIONS", {
      fontFamily: FONTS.mono,
      fontSize: "9px",
      color: colorString(COLORS.ivoryMuted),
      letterSpacing: 0.7,
    }).setDepth(100);

    makeText(this, 300, 18, "LIVE REACTOR", {
      fontFamily: FONTS.mono,
      fontSize: "9px",
      color: colorString(COLORS.cyan),
      letterSpacing: 1.2,
    }).setDepth(100);
    this.livePowerText = makeText(this, 300, 40, "POWER  —", {
      fontFamily: FONTS.mono,
      fontSize: "13px",
      color: colorString(COLORS.ivory),
      fontStyle: "bold",
    }).setDepth(100);
    this.liveRrsText = makeText(this, 445, 40, "RRS  —", {
      fontFamily: FONTS.mono,
      fontSize: "13px",
      color: colorString(COLORS.cyan),
      fontStyle: "bold",
    }).setDepth(100);
    this.liveScoreText = makeText(this, 575, 40, "SCORE  —", {
      fontFamily: FONTS.mono,
      fontSize: "13px",
      color: colorString(COLORS.gold),
      fontStyle: "bold",
    }).setDepth(100);
    this.liveFreshText = makeText(this, 725, 40, "FRESH  —", {
      fontFamily: FONTS.mono,
      fontSize: "13px",
      color: colorString(COLORS.cyan),
      fontStyle: "bold",
    }).setDepth(100);
    this.liveTimeText = makeText(this, 860, 40, "TIME  —", {
      fontFamily: FONTS.mono,
      fontSize: "13px",
      color: colorString(COLORS.ivory),
      fontStyle: "bold",
    }).setDepth(100);
    makeText(this, 300, 71, "CONFIGURE-CELL AND SOLVE WRITE TO THE LIVE CORE", {
      fontFamily: FONTS.mono,
      fontSize: "9px",
      color: colorString(COLORS.ivoryMuted),
      letterSpacing: 0.55,
    }).setDepth(100);
    makeText(this, 1552, 18, "AUTHORITATIVE SNAPSHOT", {
      fontFamily: FONTS.mono,
      fontSize: "9px",
      color: colorString(COLORS.gold),
      letterSpacing: 0.9,
    }).setOrigin(1, 0).setDepth(100);
    makeText(this, 1552, 40, "CLOCK PAUSED WHILE DESIGNING", {
      fontFamily: FONTS.mono,
      fontSize: "10px",
      color: colorString(COLORS.gold),
      align: "right",
    }).setOrigin(1, 0).setDepth(100);
    this.backButton = makeButton(
      this,
      1455,
      80,
      210,
      30,
      "RETURN TO OPS",
      () => this.backToOperations(),
      { tone: "cyan", compact: true, fontSize: 10 },
    );
    this.backButton.gameObject.setDepth(120);
  }

  private createMap(): void {
    this.mapGraphics = this.add.graphics().setDepth(0);
    this.selectionGraphics = this.add.graphics().setDepth(30);
    this.layout = createCoreFaceLayout(MAP.x + 8, MAP.y + 10, MAP.width - 16, MAP.height - 28);
    this.drawMapFoundation();
    for (const channel of this.snapshot.core.channels) {
      const tile = this.createChannelTile(channel);
      this.tiles.set(channel.channelIndex, tile);
    }
  }

  private drawMapFoundation(): void {
    if (this.mapGraphics === null) return;
    const graphics = this.mapGraphics;
    graphics.clear();
    drawPanelFrame(graphics, MAP.x, MAP.y, MAP.width, MAP.height, {
      fill: COLORS.panel,
      alpha: 0.9,
      accent: COLORS.gold,
      lineWidth: 1.3,
    });
    graphics.fillStyle(COLORS.ink, 0.56);
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
    graphics.lineStyle(1, COLORS.gold, 0.24);
    graphics.strokeRect(
      this.layout.gridX + this.layout.stepX * 7,
      this.layout.gridY + this.layout.stepY * 7,
      this.layout.stepX * 8,
      this.layout.stepY * 8,
    );
    graphics.fillStyle(COLORS.cyan, 0.25);
    graphics.fillCircle(this.layout.centerX, this.layout.centerY, 3);

    makeText(this, MAP.x + 22, MAP.y + 18, "LIVE CORE / CHANNEL SELECTOR", {
      fontFamily: FONTS.mono,
      fontSize: "12px",
      color: colorString(COLORS.cyan),
      letterSpacing: 1.7,
    }).setDepth(20);
    makeText(this, MAP.x + 22, MAP.y + 38, "380 CHANNEL FACE / 22 × 22 STEPPED TOPOLOGY", {
      fontFamily: FONTS.mono,
      fontSize: "10px",
      color: colorString(COLORS.ivoryMuted),
      letterSpacing: 0.9,
    }).setDepth(20);
    makeText(this, MAP.x + MAP.width - 20, MAP.y + 22, "LOCAL POWER FIELD", {
      fontFamily: FONTS.mono,
      fontSize: "10px",
      color: colorString(COLORS.gold),
      letterSpacing: 1,
    }).setOrigin(1, 0).setDepth(20);
    for (let row = 0; row < 22; row += 1) {
      const point = projectChannelToFace({ gridColumn: 0, gridRow: row }, this.layout);
      makeText(this, this.layout.gridX - 12, point.y - 5, CANDU6_ROW_LABELS[row] ?? "?", {
        fontFamily: FONTS.mono,
        fontSize: "8px",
        color: colorString(COLORS.ivoryMuted),
        align: "right",
      }).setOrigin(1, 0).setDepth(20);
    }
    for (let column = 0; column < 22; column += 1) {
      const point = projectChannelToFace({ gridColumn: column, gridRow: 0 }, this.layout);
      makeText(this, point.x, this.layout.gridY - 17, String(column + 1).padStart(2, "0"), {
        fontFamily: FONTS.mono,
        fontSize: "8px",
        color: colorString(COLORS.ivoryMuted),
      }).setOrigin(0.5).setDepth(20);
    }
    const legendX = MAP.x + 28;
    const legendY = MAP.y + MAP.height - 35;
    graphics.fillStyle(colorFromRgb(getHeatColor(0.72)), 1);
    graphics.fillRect(legendX, legendY, 55, 5);
    graphics.fillStyle(colorFromRgb(getHeatColor(0.93)), 1);
    graphics.fillRect(legendX + 55, legendY, 55, 5);
    graphics.fillStyle(colorFromRgb(getHeatColor(1.18)), 1);
    graphics.fillRect(legendX + 110, legendY, 55, 5);
    makeText(this, legendX, legendY + 10, "LOW", { fontFamily: FONTS.mono, fontSize: "8px", color: colorString(COLORS.ivoryMuted) }).setDepth(20);
    makeText(this, legendX + 78, legendY + 10, "NOMINAL", { fontFamily: FONTS.mono, fontSize: "8px", color: colorString(COLORS.ivoryMuted) }).setDepth(20);
    makeText(this, legendX + 151, legendY + 10, "HIGH", { fontFamily: FONTS.mono, fontSize: "8px", color: colorString(COLORS.ivoryMuted) }).setDepth(20);
    makeText(this, MAP.x + MAP.width - 24, MAP.y + MAP.height - 35, "CLICK A CHANNEL, THEN CLICK AN AXIAL POSITION", {
      fontFamily: FONTS.mono,
      fontSize: "8px",
      color: colorString(COLORS.ivoryMuted),
      letterSpacing: 0.35,
    }).setOrigin(1, 0).setDepth(20);
  }

  private createChannelTile(channel: CanduChannelSnapshot): ChannelTile {
    const container = this.add.container(0, 0).setDepth(40);
    const graphics = this.add.graphics();
    container.add(graphics);
    container.setSize(this.layout.stepX, this.layout.stepY);
    container.setInteractive(
      new Phaser.Geom.Rectangle(-this.layout.stepX / 2, -this.layout.stepY / 2, this.layout.stepX, this.layout.stepY),
      Phaser.Geom.Rectangle.Contains,
    );
    container.on("pointerdown", () => this.selectChannel(channel.channelIndex));
    return { channelIndex: channel.channelIndex, container, graphics };
  }

  private createInspector(): void {
    const graphics = this.add.graphics().setDepth(100);
    drawPanelFrame(graphics, SIDE.x, SIDE.y, SIDE.width, SIDE.height, {
      fill: COLORS.panel,
      alpha: 0.98,
      accent: COLORS.cyan,
      lineWidth: 1.5,
    });
    graphics.fillStyle(COLORS.magentaDark, 0.24);
    graphics.fillRect(SIDE.x + 14, SIDE.y + 15, SIDE.width - 28, 3);
    makeText(this, SIDE.x + 24, SIDE.y + 20, "CHANNEL INSPECTOR / LIVE CELL EDITOR", {
      fontFamily: FONTS.mono,
      fontSize: "11px",
      color: colorString(COLORS.cyan),
      letterSpacing: 1.35,
    }).setDepth(110);
    makeText(this, SIDE.x + SIDE.width - 22, SIDE.y + 20, "CONFIGURE-CELL", {
      fontFamily: FONTS.mono,
      fontSize: "8px",
      color: colorString(COLORS.gold),
      letterSpacing: 0.6,
    }).setOrigin(1, 0).setDepth(110);
    this.channelText = makeText(this, SIDE.x + 24, SIDE.y + 48, "CH 000", {
      fontFamily: FONTS.display,
      fontSize: "30px",
      color: colorString(COLORS.ivory),
      fontStyle: "bold",
    }).setDepth(110);
    this.coordinateText = makeText(this, SIDE.x + 26, SIDE.y + 86, "—", {
      fontFamily: FONTS.mono,
      fontSize: "10px",
      color: colorString(COLORS.gold),
      letterSpacing: 0.8,
    }).setDepth(110);
    this.channelSummaryText = makeText(this, SIDE.x + SIDE.width - 22, SIDE.y + 86, "POWER  —", {
      fontFamily: FONTS.mono,
      fontSize: "9px",
      color: colorString(COLORS.ivoryMuted),
      align: "right",
    }).setOrigin(1, 0).setDepth(110);
    graphics.lineStyle(1, COLORS.ivory, 0.16);
    graphics.lineBetween(SIDE.x + 20, SIDE.y + 110, SIDE.x + SIDE.width - 20, SIDE.y + 110);

    makeText(this, AXIAL.x, SIDE.y + 122, "AXIAL POSITIONS", {
      fontFamily: FONTS.mono,
      fontSize: "9px",
      color: colorString(COLORS.gold),
      letterSpacing: 1,
    }).setDepth(110);
    makeText(this, EDITOR.x, SIDE.y + 122, "CELL INSPECTOR", {
      fontFamily: FONTS.mono,
      fontSize: "9px",
      color: colorString(COLORS.gold),
      letterSpacing: 1,
    }).setDepth(110);
    graphics.fillStyle(COLORS.indigo, 0.84);
    graphics.fillRoundedRect(AXIAL.x, AXIAL.y, AXIAL.width, AXIAL.height, 6);
    graphics.lineStyle(1, COLORS.cyan, 0.32);
    graphics.strokeRoundedRect(AXIAL.x, AXIAL.y, AXIAL.width, AXIAL.height, 6);
    graphics.fillStyle(COLORS.indigo, 0.92);
    graphics.fillRoundedRect(EDITOR.x, EDITOR.y, EDITOR.width, EDITOR.height, 6);
    graphics.lineStyle(1, COLORS.cyan, 0.32);
    graphics.strokeRoundedRect(EDITOR.x, EDITOR.y, EDITOR.width, EDITOR.height, 6);
    this.createAxialRows();

    this.selectedCellText = makeText(this, EDITOR.x + 14, EDITOR.y + 16, "P01 / CELL", {
      fontFamily: FONTS.display,
      fontSize: "19px",
      color: colorString(COLORS.ivory),
      fontStyle: "bold",
    }).setDepth(112);
    this.selectedStateText = makeText(this, EDITOR.x + 15, EDITOR.y + 48, "FUEL STATE  —", {
      fontFamily: FONTS.mono,
      fontSize: "10px",
      color: colorString(COLORS.cyan),
      letterSpacing: 0.6,
    }).setDepth(112);
    this.selectedFluxText = makeText(this, EDITOR.x + 15, EDITOR.y + 79, "G1 FLUX  —\nG2 FLUX  —", {
      fontFamily: FONTS.mono,
      fontSize: "10px",
      color: colorString(COLORS.ivory),
      lineSpacing: 4,
    }).setDepth(112);
    this.selectedPowerText = makeText(this, EDITOR.x + 15, EDITOR.y + 128, "LOCAL POWER  —\nBUNDLE POWER  —", {
      fontFamily: FONTS.mono,
      fontSize: "9px",
      color: colorString(COLORS.ivoryMuted),
      lineSpacing: 4,
    }).setDepth(112);
    makeText(this, EDITOR.x + 15, EDITOR.y + 174, "REFLECTIVE FACES", {
      fontFamily: FONTS.mono,
      fontSize: "9px",
      color: colorString(COLORS.gold),
      letterSpacing: 0.8,
    }).setDepth(112);
    const faceWidth = 108;
    const faceHeight = 28;
    const faceX = [EDITOR.x + 60, EDITOR.x + 182];
    const faceY = [EDITOR.y + 214, EDITOR.y + 250, EDITOR.y + 286];
    FACE_ORDER.forEach((face, index) => {
      const button = makeButton(
        this,
        faceX[index % 2],
        faceY[Math.floor(index / 2)],
        faceWidth,
        faceHeight,
        face.toUpperCase(),
        () => this.toggleFace(face),
        { tone: "gold", fontSize: 8, compact: true },
      );
      button.gameObject.setDepth(120);
      this.faceButtons.set(face, button);
    });
    this.selectedFacesText = makeText(this, EDITOR.x + 15, EDITOR.y + 328, "REFLECTIVE  —", {
      fontFamily: FONTS.mono,
      fontSize: "8px",
      color: colorString(COLORS.ivoryMuted),
      wordWrap: { width: EDITOR.width - 30 },
    }).setDepth(112);
    this.fuelButton = makeButton(
      this,
      EDITOR.x + EDITOR.width / 2,
      EDITOR.y + 382,
      EDITOR.width - 28,
      34,
      "FUEL STATE",
      () => this.toggleFuel(),
      { tone: "magenta", fontSize: 9, compact: true },
    );
    this.fuelButton.gameObject.setDepth(120);
    this.solveText = makeText(this, EDITOR.x + 15, EDITOR.y + 426, "SOLVE  —", {
      fontFamily: FONTS.mono,
      fontSize: "8px",
      color: colorString(COLORS.green),
      wordWrap: { width: EDITOR.width - 30 },
    }).setDepth(112);
    this.feedbackText = makeText(this, EDITOR.x + 15, EDITOR.y + 450, this.resultMessage, {
      fontFamily: FONTS.body,
      fontSize: "9px",
      color: colorString(COLORS.ivoryMuted),
      wordWrap: { width: EDITOR.width - 30 },
      lineSpacing: 2,
    }).setDepth(112);
    this.solveButton = makeButton(
      this,
      EDITOR.x + EDITOR.width / 2,
      SIDE.y + SIDE.height - 26,
      EDITOR.width - 28,
      34,
      "SOLVE LIVE CORE",
      () => this.solve(),
      { tone: "cyan", fontSize: 9, compact: true },
    );
    this.solveButton.gameObject.setDepth(120);
  }

  private createAxialRows(): void {
    const rowHeight = AXIAL.height / 12;
    for (let position = 0; position < 12; position += 1) {
      const y = AXIAL.y + rowHeight * (position + 0.5);
      const container = this.add.container(AXIAL.x + AXIAL.width / 2, y).setDepth(115);
      const graphics = this.add.graphics();
      const positionText = makeText(this, -AXIAL.width / 2 + 12, -8, `P${String(position + 1).padStart(2, "0")}`, {
        fontFamily: FONTS.mono,
        fontSize: "10px",
        color: colorString(COLORS.ivory),
        fontStyle: "bold",
      });
      const stateText = makeText(this, -AXIAL.width / 2 + 54, -8, "—", {
        fontFamily: FONTS.mono,
        fontSize: "8px",
        color: colorString(COLORS.ivoryMuted),
      });
      const fluxText = makeText(this, AXIAL.width / 2 - 52, -8, "G —", {
        fontFamily: FONTS.mono,
        fontSize: "7px",
        color: colorString(COLORS.ivoryMuted),
        align: "right",
      }).setOrigin(1, 0);
      const powerText = makeText(this, AXIAL.width / 2 - 52, 7, "P —", {
        fontFamily: FONTS.mono,
        fontSize: "7px",
        color: colorString(COLORS.gold),
        align: "right",
      }).setOrigin(1, 0);
      container.add([graphics, positionText, stateText, fluxText, powerText]);
      container.setSize(AXIAL.width - 8, rowHeight - 2);
      container.setInteractive(
        new Phaser.Geom.Rectangle(-AXIAL.width / 2 + 4, -rowHeight / 2 + 1, AXIAL.width - 8, rowHeight - 2),
        Phaser.Geom.Rectangle.Contains,
      );
      container.on("pointerdown", () => {
        if (!this.pending) {
          this.selectedPosition = position;
          this.refresh();
        }
      });
      this.axialRows.push({ position, container, graphics, positionText, stateText, fluxText, powerText });
    }
  }

  private createUnavailableOverlay(): void {
    const container = this.add.container(0, 0).setDepth(600).setVisible(false);
    const graphics = this.add.graphics();
    graphics.fillStyle(COLORS.ink, 0.9);
    graphics.fillRect(0, HEADER_HEIGHT, VIEW_WIDTH, VIEW_HEIGHT - HEADER_HEIGHT);
    graphics.fillStyle(COLORS.panelRaised, 0.98);
    graphics.fillRoundedRect(400, 300, 800, 230, 10);
    graphics.lineStyle(2, COLORS.red, 0.76);
    graphics.strokeRoundedRect(400, 300, 800, 230, 10);
    drawCornerBrackets(graphics, 400, 300, 800, 230, COLORS.gold);
    container.add(graphics);
    this.unavailableTitle = makeText(this, 800, 356, "LIVE CORE DESIGNER UNAVAILABLE", {
      fontFamily: FONTS.display,
      fontSize: "27px",
      color: colorString(COLORS.ivory),
      fontStyle: "bold",
      align: "center",
    }).setOrigin(0.5);
    this.unavailableDetail = makeText(this, 800, 425, "", {
      fontFamily: FONTS.body,
      fontSize: "16px",
      color: colorString(COLORS.ivoryMuted),
      align: "center",
      wordWrap: { width: 660 },
    }).setOrigin(0.5);
    container.add([this.unavailableTitle, this.unavailableDetail]);
    this.unavailableOverlay = container;
  }

  private refresh(): void {
    this.keepSelectionValid();
    this.refreshToolbar();
    this.refreshCore();
    this.refreshAxialRows();
    this.refreshEditor();
    const unavailable = !this.session.status.isWasmAvailable || this.snapshot.core.channels.length !== 380;
    this.unavailableOverlay?.setVisible(unavailable);
    this.unavailableDetail?.setText(!this.session.status.isWasmAvailable
      ? this.session.status.detail
      : "The live snapshot does not contain the full 380-channel core.");
  }

  private keepSelectionValid(): void {
    if (!this.snapshot.core.channels.some((channel) => channel.channelIndex === this.selectedChannelIndex)) {
      this.selectedChannelIndex = chooseInitialChannel(this.snapshot);
    }
    this.selectedPosition = Math.max(0, Math.min(11, this.selectedPosition));
  }

  private refreshToolbar(): void {
    const power = finiteOr(this.snapshot.physics.actualPowerFraction, this.snapshot.normalizedPowerFraction);
    this.livePowerText?.setText(`POWER  ${formatPercent(power)}`);
    this.liveRrsText?.setText(`RRS  ${formatPercent(this.snapshot.rrs.averageFillFraction)}`);
    this.liveScoreText?.setText(`SCORE  ${Math.round(this.snapshot.scoreTotal).toString().padStart(6, "0")}`);
    this.liveFreshText?.setText(`FRESH  ${this.snapshot.freshBundlesAvailable}`);
    this.liveTimeText?.setText(`TIME  ${formatSimulationTime(this.snapshot.simulationTimeSeconds)}`);
  }

  private refreshCore(): void {
    if (this.snapshot.core.channels.length !== this.tiles.size && this.snapshot.core.channels.length === 380) {
      // The bridge should keep channel identity stable. A scene rebuild is
      // unnecessary for ordinary snapshot updates, but this keeps tiles safe
      // if a full response arrives with a newly materialized core array.
      this.rebuildMapTiles();
    }
    for (const tile of this.tiles.values()) {
      const channel = this.snapshot.core.channels.find((candidate) => candidate.channelIndex === tile.channelIndex);
      if (channel === undefined) continue;
      const point = projectChannelToFace(channel, this.layout);
      const selected = channel.channelIndex === this.selectedChannelIndex;
      const width = this.layout.tileWidth + (selected ? 4 : 0);
      const height = this.layout.tileHeight + (selected ? 3 : 0);
      const heat = Number.isFinite(channel.localPowerFraction)
        ? colorFromRgb(getHeatColor(channel.localPowerFraction))
        : COLORS.indigo;
      tile.container.setPosition(point.x, point.y);
      tile.container.setDepth(40 + channel.gridRow / 1000);
      tile.graphics.clear();
      tile.graphics.fillStyle(COLORS.ink, selected ? 0.88 : 0.62);
      tile.graphics.fillRoundedRect(-width / 2 + 2, -height / 2 + 3, width + 2, height + 2, 3);
      tile.graphics.fillStyle(mixColor(heat, COLORS.white, selected ? 0.15 : 0), 0.98);
      tile.graphics.fillRoundedRect(-width / 2, -height / 2, width, height, 3);
      tile.graphics.fillStyle(COLORS.white, 0.08);
      tile.graphics.fillRoundedRect(-width / 2 + 1, -height / 2 + 1, width - 2, Math.max(2, height * 0.34), 2);
      tile.graphics.lineStyle(selected ? 2 : 0.8, selected ? COLORS.gold : COLORS.ink, selected ? 1 : 0.68);
      tile.graphics.strokeRoundedRect(-width / 2, -height / 2, width, height, 3);
    }
  }

  private rebuildMapTiles(): void {
    for (const tile of this.tiles.values()) tile.container.destroy();
    this.tiles.clear();
    for (const channel of this.snapshot.core.channels) {
      this.tiles.set(channel.channelIndex, this.createChannelTile(channel));
    }
  }

  private refreshAxialRows(): void {
    const channel = this.getSelectedChannel();
    const rowHeight = AXIAL.height / 12;
    this.axialRows.forEach((row, index) => {
      const bundle = channel?.bundles.find((candidate) => candidate.position === index);
      const selected = index === this.selectedPosition;
      row.graphics.clear();
      row.graphics.fillStyle(selected ? COLORS.cyanDark : COLORS.ink, selected ? 0.62 : 0.42);
      row.graphics.fillRoundedRect(-AXIAL.width / 2 + 4, -rowHeight / 2 + 1, AXIAL.width - 8, rowHeight - 2, 4);
      row.graphics.lineStyle(selected ? 1.5 : 0.7, selected ? COLORS.ivory : COLORS.grid, selected ? 0.9 : 0.54);
      row.graphics.strokeRoundedRect(-AXIAL.width / 2 + 4, -rowHeight / 2 + 1, AXIAL.width - 8, rowHeight - 2, 4);
      const hasFuel = bundle?.hasFuel;
      row.positionText.setColor(colorString(selected ? COLORS.ivory : COLORS.ivoryMuted));
      row.stateText.setText(bundle === undefined ? "—" : hasFuel === undefined ? "STATE ?" : hasFuel ? "FUEL" : "EMPTY");
      row.stateText.setColor(colorString(bundle === undefined ? COLORS.ivoryMuted : hasFuel === undefined ? COLORS.gold : hasFuel ? COLORS.cyan : COLORS.magenta));
      row.fluxText.setText(bundle === undefined ? "G —" : `G ${formatBundleFlux(bundle)}`);
      row.powerText.setText(bundle === undefined ? "P —" : `P ${formatPercent(bundle.localPowerFraction)}`);
      row.fluxText.setColor(colorString(COLORS.ivoryMuted));
      row.powerText.setColor(colorString(COLORS.gold));
    });
  }

  private refreshEditor(): void {
    const channel = this.getSelectedChannel();
    const bundle = this.getSelectedBundle();
    if (channel === undefined) {
      this.channelText?.setText("NO CHANNEL");
      this.coordinateText?.setText("WAITING FOR LIVE CORE");
      this.channelSummaryText?.setText("POWER  —");
      this.selectedCellText?.setText("NO CELL");
      this.selectedStateText?.setText("FUEL STATE  —");
      this.selectedFluxText?.setText("G1 FLUX  —\nG2 FLUX  —");
      this.selectedPowerText?.setText("LOCAL POWER  —\nBUNDLE POWER  —");
      this.selectedFacesText?.setText("REFLECTIVE  —");
      this.solveText?.setText("SOLVE  —");
      this.feedbackText?.setText(this.pending ? "COMMAND PENDING…" : this.resultMessage);
      this.setEditorControls(false, null);
      return;
    }
    this.channelText?.setText(`CH ${String(channel.channelIndex).padStart(3, "0")}`);
    this.coordinateText?.setText(`${gridCoordinateLabel(channel)}  /  ${channel.flowDirection.toUpperCase()}`);
    this.channelSummaryText?.setText(`POWER ${formatPercent(channel.localPowerFraction)}  ·  AVG ${formatBurnup(channel.averageBurnupMwdPerKg)}`);
    this.selectedCellText?.setText(`P${String(this.selectedPosition + 1).padStart(2, "0")} / CELL`);
    this.selectedStateText?.setText(`FUEL STATE  ${formatFuelState(bundle)}`)
      .setColor(colorString(bundle?.hasFuel === true ? COLORS.cyan : bundle?.hasFuel === false ? COLORS.magenta : COLORS.gold));
    this.selectedFluxText?.setText(`G1 FLUX  ${formatFlux(bundle?.group1Flux)}\nG2 FLUX  ${formatFlux(bundle?.group2Flux)}`);
    this.selectedPowerText?.setText(`LOCAL POWER  ${formatPercent(bundle?.localPowerFraction)}\nBUNDLE POWER  ${formatPower(bundle?.powerWatts)}`);
    const faces = bundle?.reflectiveFaces ?? [];
    this.selectedFacesText?.setText(bundle === undefined
      ? "REFLECTIVE  —"
      : `REFLECTIVE  ${faces.length === 0 ? "NONE REPORTED" : faces.map((face) => face.toUpperCase()).join(" · ")}`);
    const solveState = this.snapshot.diagnostics.convergence.state;
    this.solveText?.setText(`SOLVE  ${formatSolveHealth(this.snapshot)}\nK  ${formatEffectiveK(this.snapshot.physics.effectiveK)}`)
      .setColor(colorString(solveState === "converged" ? COLORS.green : COLORS.gold));
    this.feedbackText?.setText(this.pending ? "COMMAND PENDING…" : this.resultMessage)
      .setColor(colorString(this.pending ? COLORS.gold : this.resultMessage.startsWith("REJECTED") || this.resultMessage.startsWith("FAILED") ? COLORS.red : COLORS.ivoryMuted));
    this.setEditorControls(this.session.status.isWasmAvailable && !this.pending, bundle ?? null);
  }

  private setEditorControls(enabled: boolean, bundle: CanduBundleSnapshot | null): void {
    const selectedFaces = bundle?.reflectiveFaces ?? [];
    this.fuelButton?.setLabel(bundle?.hasFuel === undefined
      ? "FUEL STATE UNAVAILABLE"
      : bundle.hasFuel ? "SET EMPTY / MODERATOR" : "SET FUEL / ACTIVE");
    this.fuelButton?.setEnabled(enabled && bundle !== null && bundle.hasFuel !== undefined);
    this.solveButton?.setEnabled(enabled);
    this.backButton?.setEnabled(!this.pending);
    FACE_ORDER.forEach((face) => {
      const button = this.faceButtons.get(face);
      const eligible = this.isEligibleFace(face);
      const active = selectedFaces.includes(face);
      button?.setLabel(`${face.toUpperCase()}  ${eligible ? active ? "●" : "○" : "EDGE"}`);
      button?.setEnabled(enabled && bundle !== null && eligible);
      button?.gameObject.setAlpha(active ? 1 : eligible ? 0.64 : 0.42);
    });
  }

  private drawSelection(time: number): void {
    if (this.selectionGraphics === null) return;
    const channel = this.getSelectedChannel();
    if (channel === undefined) {
      this.selectionGraphics.clear();
      return;
    }
    const point = projectChannelToFace(channel, this.layout);
    const pulse = 1 + Math.sin(time * 0.005) * 0.05;
    const width = this.layout.tileWidth + 12 * pulse;
    const height = this.layout.tileHeight + 10 * pulse;
    const graphics = this.selectionGraphics;
    graphics.clear();
    graphics.lineStyle(2.5, COLORS.ivory, 0.96);
    graphics.strokeRoundedRect(point.x - width / 2, point.y - height / 2, width, height, 4);
    graphics.lineStyle(1, COLORS.cyan, 0.84);
    graphics.strokeRoundedRect(point.x - width / 2 - 4, point.y - height / 2 - 4, width + 8, height + 8, 6);
    graphics.fillStyle(COLORS.gold, 0.96);
    graphics.fillCircle(point.x, point.y, 2.5);
  }

  private selectChannel(channelIndex: number): void {
    if (this.pending || !this.snapshot.core.channels.some((channel) => channel.channelIndex === channelIndex)) return;
    this.selectedChannelIndex = channelIndex;
    this.selectedPosition = 0;
    this.refresh();
  }

  private toggleFuel(): void {
    const bundle = this.getSelectedBundle();
    if (bundle?.hasFuel === undefined || this.pending) return;
    this.dispatchCell({ hasFuel: !bundle.hasFuel, reflectiveFaces: bundle.reflectiveFaces ?? [] });
  }

  private toggleFace(face: CoreBoundaryFace): void {
    const bundle = this.getSelectedBundle();
    if (bundle === undefined || bundle.hasFuel === undefined || this.pending || !this.isEligibleFace(face)) return;
    const faces = new Set(bundle.reflectiveFaces ?? []);
    if (faces.has(face)) faces.delete(face);
    else faces.add(face);
    this.dispatchCell({ hasFuel: bundle.hasFuel, reflectiveFaces: sortFaces([...faces]) });
  }

  private dispatchCell(change: { hasFuel: boolean; reflectiveFaces: CoreBoundaryFace[] }): void {
    if (this.selectedChannelIndex < 0 || this.pending || !this.session.status.isWasmAvailable) return;
    this.resultMessage = "CONFIGURE-CELL PENDING…";
    this.refreshEditor();
    void this.session.dispatch({
      type: "configure-cell",
      channelIndex: this.selectedChannelIndex,
      position: this.selectedPosition,
      hasFuel: change.hasFuel,
      reflectiveFaces: sortFaces(change.reflectiveFaces),
    }, { responseMode: "full" }).catch((error: unknown) => {
      this.resultMessage = `FAILED  /  ${error instanceof Error ? error.message : String(error)}`;
      this.refreshEditor();
    });
  }

  private solve(): void {
    if (this.pending || !this.session.status.isWasmAvailable) return;
    this.resultMessage = "LIVE SOLVE PENDING…";
    this.refreshEditor();
    void this.session.dispatch({ type: "solve" }, { responseMode: "full" }).catch((error: unknown) => {
      this.resultMessage = `FAILED  /  ${error instanceof Error ? error.message : String(error)}`;
      this.refreshEditor();
    });
  }

  private receiveSessionUpdate(update: SessionUpdate): void {
    this.snapshot = update.snapshot;
    this.pending = update.pending;
    if (update.error !== null) this.resultMessage = `FAILED  /  ${update.error}`;
    const response = update.response;
    if (response !== null && response.sequence !== this.lastResponseSequence &&
        (response.command.type === "configure-cell" || response.command.type === "solve")) {
      this.lastResponseSequence = response.sequence;
      this.resultMessage = response.accepted
        ? `ACCEPTED  /  ${response.message}`
        : `REJECTED  /  ${response.message}`;
    }
    this.refresh();
  }

  private getSelectedChannel(): CanduChannelSnapshot | undefined {
    return this.snapshot.core.channels.find((channel) => channel.channelIndex === this.selectedChannelIndex);
  }

  private getSelectedBundle(): CanduBundleSnapshot | undefined {
    return this.getSelectedChannel()?.bundles.find((bundle) => bundle.position === this.selectedPosition);
  }

  private isEligibleFace(face: CoreBoundaryFace): boolean {
    // The live core accepts vacuum-to-reflective overrides on every geometric
    // face, including the outer radial boundary and the two axial ends.
    return this.getSelectedChannel() !== undefined;
  }

  private handleKeyDown(event: KeyboardEvent): void {
    const key = event.key.toLowerCase();
    if (key === "escape") {
      this.backToOperations();
      return;
    }
    if (key === "f2") {
      this.backToOperations();
      return;
    }
    if (key === "s") {
      this.solve();
      return;
    }
    if (key === "f") {
      this.toggleFuel();
      return;
    }
    const position = axialPositionForKey(key);
    if (position !== null && !this.pending) {
      this.selectedPosition = position;
      this.refresh();
      return;
    }
    const movement: Record<string, [number, number]> = {
      arrowleft: [-1, 0], a: [-1, 0], arrowright: [1, 0], d: [1, 0],
      arrowup: [0, -1], w: [0, -1], arrowdown: [0, 1],
    };
    const delta = movement[key];
    if (delta !== undefined) {
      this.selectChannel(findAdjacentChannelIndex(this.snapshot.core.channels, this.selectedChannelIndex, delta[0], delta[1]));
    }
  }

  private backToOperations(): void {
    if (this.pending) return;
    this.session.startShift();
    this.scene.start("OperationsScene", { selectedChannelIndex: this.selectedChannelIndex });
  }
}

function chooseInitialChannel(snapshot: CanduSnapshot): number {
  if (snapshot.core.channels.some((channel) => channel.channelIndex === 210)) return 210;
  return snapshot.core.channels[0]?.channelIndex ?? -1;
}

function isSceneChannelIndex(value: unknown): value is { returnChannelIndex: number } {
  return typeof value === "object" && value !== null &&
    "returnChannelIndex" in value && typeof value.returnChannelIndex === "number" &&
    Number.isInteger(value.returnChannelIndex);
}

function finiteOr(value: number, fallback: number): number {
  return Number.isFinite(value) ? value : fallback;
}

function formatPercent(value: number | undefined): string {
  return value !== undefined && Number.isFinite(value) ? `${(value * 100).toFixed(1)}%` : "—";
}

function formatFuelState(bundle: CanduBundleSnapshot | undefined): string {
  if (bundle === undefined || bundle.hasFuel === undefined) return "—";
  return bundle.hasFuel ? "FUEL / ACTIVE" : "EMPTY / MODERATOR";
}

function formatFlux(value: number | undefined): string {
  if (value === undefined || !Number.isFinite(value)) return "—";
  return value.toExponential(2).replace("e+", "e");
}

function formatBundleFlux(bundle: CanduBundleSnapshot): string {
  if (bundle.group1Flux === undefined || bundle.group2Flux === undefined ||
      !Number.isFinite(bundle.group1Flux) || !Number.isFinite(bundle.group2Flux)) return "—";
  const total = bundle.group1Flux + bundle.group2Flux;
  return Number.isFinite(total) ? total.toExponential(1).replace("e+", "e") : "—";
}

function formatPower(value: number | undefined): string {
  return value !== undefined && Number.isFinite(value) ? formatPowerWatts(value) : "—";
}

function formatBurnup(value: number): string {
  return Number.isFinite(value) ? `${value.toFixed(1)} MWd/kg` : "—";
}

function sortFaces(faces: readonly CoreBoundaryFace[]): CoreBoundaryFace[] {
  return [...new Set(faces)].sort((left, right) => FACE_ORDER.indexOf(left) - FACE_ORDER.indexOf(right));
}

function axialPositionForKey(key: string): number | null {
  if (/^[1-9]$/.test(key)) return Number(key) - 1;
  if (key === "0") return 9;
  if (key === "-") return 10;
  if (key === "=") return 11;
  return null;
}
