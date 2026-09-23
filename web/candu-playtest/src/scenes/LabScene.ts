import Phaser from "phaser";
import { getRuntimeSession } from "../runtime";
import {
  COLORS,
  FONTS,
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
  CanduCommandResponse,
  CanduSnapshot,
  LabBoundaryFace,
} from "../protocol";
import {
  defaultReflectiveFaces,
  effectiveReflectiveFaces,
  formatLabFlux,
  isFixedExteriorFace,
  labCellAt,
  labCellKey,
  labFluxAt,
  sortReflectiveFaces,
  toggleReflectiveFace,
  type LabCellState,
} from "../lab";

const VIEW_WIDTH = 1600;
const VIEW_HEIGHT = 900;
const GRID = { x: 52, y: 136, width: 1060, height: 690 } as const;
const SIDE = { x: 1140, y: 136, width: 408, height: 690 } as const;
const CELL_GRID = { x: 100, y: 270, width: 970, height: 430 } as const;
const FACE_ORDER: readonly LabBoundaryFace[] = [
  "north",
  "east",
  "south",
  "west",
  "end-a",
  "end-b",
];

interface LabCellTile {
  channelIndex: number;
  position: number;
  container: Phaser.GameObjects.Container;
  graphics: Phaser.GameObjects.Graphics;
  stateText: Phaser.GameObjects.Text;
  fluxText: Phaser.GameObjects.Text;
}

export class LabScene extends Phaser.Scene {
  private readonly session = getRuntimeSession();
  private readonly tiles = new Map<string, LabCellTile>();
  private snapshot: CanduSnapshot = this.session.snapshot;
  private selectedCell: { channelIndex: number; position: number } = { channelIndex: 0, position: 0 };
  private pending = false;
  private lastResponse: CanduCommandResponse | null = null;
  private resultMessage = "Select a cell to configure its material and boundaries.";
  private unsubscribe: (() => void) | null = null;
  private gridGraphics: Phaser.GameObjects.Graphics | null = null;
  private selectionGraphics: Phaser.GameObjects.Graphics | null = null;
  private headerMode: Phaser.GameObjects.Text | null = null;
  private headerSolve: Phaser.GameObjects.Text | null = null;
  private selectedText: Phaser.GameObjects.Text | null = null;
  private selectedStateText: Phaser.GameObjects.Text | null = null;
  private selectedMaterialText: Phaser.GameObjects.Text | null = null;
  private selectedFluxText: Phaser.GameObjects.Text | null = null;
  private selectedFacesText: Phaser.GameObjects.Text | null = null;
  private kText: Phaser.GameObjects.Text | null = null;
  private powerText: Phaser.GameObjects.Text | null = null;
  private solveText: Phaser.GameObjects.Text | null = null;
  private feedbackText: Phaser.GameObjects.Text | null = null;
  private fuelButton: TacticalButton | null = null;
  private solveButton: TacticalButton | null = null;
  private resetButton: TacticalButton | null = null;
  private backButton: TacticalButton | null = null;
  private readonly faceButtons = new Map<LabBoundaryFace, TacticalButton>();
  private unavailableOverlay: Phaser.GameObjects.Container | null = null;
  private unavailableTitle: Phaser.GameObjects.Text | null = null;
  private unavailableDetail: Phaser.GameObjects.Text | null = null;

  public constructor() {
    super("LabScene");
  }

  public create(): void {
    this.session.stopShift();
    this.snapshot = this.session.snapshot;
    this.createBackdrop();
    this.createGrid();
    this.createSidePanel();
    this.createUnavailableOverlay();
    this.refresh();
    this.unsubscribe = this.session.subscribe((update) => this.receiveSessionUpdate(update));
    this.events.once("shutdown", () => this.unsubscribe?.());
    this.input.keyboard?.on("keydown", this.handleKeyDown, this);
    this.events.once("shutdown", () => this.input.keyboard?.off("keydown", this.handleKeyDown, this));
    this.cameras.main.fadeIn(420, 7, 11, 27);
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
    background.fillStyle(COLORS.magentaDark, 0.08);
    background.fillTriangle(0, 120, 820, 120, 380, 900);
    background.fillStyle(COLORS.cyanDark, 0.07);
    background.fillTriangle(860, 120, 1600, 120, 1360, 900);
    background.lineStyle(1, COLORS.gold, 0.2);
    background.lineBetween(0, 116, VIEW_WIDTH, 116);
    background.lineBetween(0, 850, VIEW_WIDTH, 850);
    drawCornerBrackets(background, 18, 92, VIEW_WIDTH - 36, 776, COLORS.gold);
  }

  private createGrid(): void {
    this.gridGraphics = this.add.graphics().setDepth(0);
    this.selectionGraphics = this.add.graphics().setDepth(30);
    const graphics = this.gridGraphics;
    drawPanelFrame(graphics, GRID.x, GRID.y, GRID.width, GRID.height, {
      fill: COLORS.panel,
      alpha: 0.96,
      accent: COLORS.magenta,
      lineWidth: 1.5,
    });
    graphics.fillStyle(COLORS.ink, 0.72);
    graphics.fillRect(CELL_GRID.x, CELL_GRID.y, CELL_GRID.width, CELL_GRID.height);
    graphics.lineStyle(1.2, COLORS.cyan, 0.35);
    graphics.strokeRect(CELL_GRID.x, CELL_GRID.y, CELL_GRID.width, CELL_GRID.height);
    const cellWidth = CELL_GRID.width / 8;
    const cellHeight = CELL_GRID.height / 2;
    graphics.lineStyle(1, COLORS.grid, 0.34);
    for (let position = 0; position <= 8; position += 1) {
      const x = CELL_GRID.x + position * cellWidth;
      graphics.lineBetween(x, CELL_GRID.y, x, CELL_GRID.y + CELL_GRID.height);
    }
    graphics.lineBetween(CELL_GRID.x, CELL_GRID.y + cellHeight, CELL_GRID.x + CELL_GRID.width, CELL_GRID.y + cellHeight);

    makeText(this, GRID.x + 24, GRID.y + 20, "LAB / SPATIAL WORKBENCH", {
      fontFamily: FONTS.mono,
      fontSize: "12px",
      color: colorString(COLORS.magenta),
      letterSpacing: 1.8,
    }).setDepth(20);
    makeText(this, GRID.x + 24, GRID.y + 42, "2 CHANNELS × 8 AXIAL NODES / AUTHORITATIVE CORE TOPOLOGY", {
      fontFamily: FONTS.mono,
      fontSize: "10px",
      color: colorString(COLORS.ivoryMuted),
      letterSpacing: 0.8,
    }).setDepth(20);
    makeText(this, GRID.x + GRID.width - 24, GRID.y + 22, "FLUX FIELD / CELL STATE", {
      fontFamily: FONTS.mono,
      fontSize: "10px",
      color: colorString(COLORS.gold),
      letterSpacing: 1,
    }).setOrigin(1, 0).setDepth(20);

    for (let position = 0; position < 8; position += 1) {
      makeText(this, CELL_GRID.x + cellWidth * (position + 0.5), CELL_GRID.y - 23, `P${position + 1}`, {
        fontFamily: FONTS.mono,
        fontSize: "10px",
        color: colorString(COLORS.ivoryMuted),
      }).setOrigin(0.5).setDepth(20);
    }
    makeText(this, CELL_GRID.x - 20, CELL_GRID.y + cellHeight * 0.5, "CH 0", {
      fontFamily: FONTS.mono,
      fontSize: "10px",
      color: colorString(COLORS.cyan),
    }).setOrigin(1, 0.5).setDepth(20);
    makeText(this, CELL_GRID.x - 20, CELL_GRID.y + cellHeight * 1.5, "CH 1", {
      fontFamily: FONTS.mono,
      fontSize: "10px",
      color: colorString(COLORS.cyan),
    }).setOrigin(1, 0.5).setDepth(20);
    makeText(this, GRID.x + 24, GRID.y + GRID.height - 34, "CLICK CELL TO INSPECT · F TO TOGGLE FUEL · S SOLVE · R RESET", {
      fontFamily: FONTS.mono,
      fontSize: "9px",
      color: colorString(COLORS.ivoryMuted),
      letterSpacing: 0.6,
    }).setDepth(20);

    for (let channelIndex = 0; channelIndex < 2; channelIndex += 1) {
      for (let position = 0; position < 8; position += 1) {
        this.createCellTile(channelIndex, position, cellWidth, cellHeight);
      }
    }
  }

  private createCellTile(channelIndex: number, position: number, width: number, height: number): void {
    const x = CELL_GRID.x + width * (position + 0.5);
    const y = CELL_GRID.y + height * (channelIndex + 0.5);
    const container = this.add.container(x, y).setDepth(10);
    const graphics = this.add.graphics();
    const stateText = makeText(this, 0, -height * 0.16, "—", {
      fontFamily: FONTS.mono,
      fontSize: "11px",
      color: colorString(COLORS.ivory),
      fontStyle: "bold",
      align: "center",
    }).setOrigin(0.5);
    const fluxText = makeText(this, 0, height * 0.16, "—", {
      fontFamily: FONTS.mono,
      fontSize: "9px",
      color: colorString(COLORS.ivoryMuted),
      align: "center",
    }).setOrigin(0.5);
    container.add([graphics, stateText, fluxText]);
    container.setSize(width, height);
    container.setInteractive(
      new Phaser.Geom.Rectangle(-width / 2, -height / 2, width, height),
      Phaser.Geom.Rectangle.Contains,
    );
    container.on("pointerdown", () => {
      if (!this.pending) {
        this.selectedCell = { channelIndex, position };
        this.refresh();
      }
    });
    const tile: LabCellTile = { channelIndex, position, container, graphics, stateText, fluxText };
    this.tiles.set(labCellKey(channelIndex, position), tile);
  }

  private createSidePanel(): void {
    const graphics = this.add.graphics().setDepth(100);
    drawPanelFrame(graphics, SIDE.x, SIDE.y, SIDE.width, SIDE.height, {
      fill: COLORS.panel,
      alpha: 0.98,
      accent: COLORS.cyan,
      lineWidth: 1.5,
    });
    graphics.fillStyle(COLORS.magentaDark, 0.24);
    graphics.fillRect(SIDE.x + 14, SIDE.y + 15, SIDE.width - 28, 3);
    makeText(this, SIDE.x + 24, SIDE.y + 20, "CELL CONFIGURATION", {
      fontFamily: FONTS.mono,
      fontSize: "11px",
      color: colorString(COLORS.cyan),
      letterSpacing: 1.5,
    }).setDepth(110);
    this.headerMode = makeText(this, SIDE.x + SIDE.width - 24, SIDE.y + 20, "LAB MODE", {
      fontFamily: FONTS.mono,
      fontSize: "9px",
      color: colorString(COLORS.magenta),
      align: "right",
    }).setOrigin(1, 0).setDepth(110);

    this.selectedText = makeText(this, SIDE.x + 24, SIDE.y + 52, "CH 0  /  POSITION 01", {
      fontFamily: FONTS.display,
      fontSize: "27px",
      color: colorString(COLORS.ivory),
      fontStyle: "bold",
    }).setDepth(110);
    this.selectedStateText = makeText(this, SIDE.x + 26, SIDE.y + 91, "FUEL", {
      fontFamily: FONTS.mono,
      fontSize: "11px",
      color: colorString(COLORS.cyan),
      letterSpacing: 1,
    }).setDepth(110);
    this.selectedMaterialText = makeText(this, SIDE.x + SIDE.width - 24, SIDE.y + 91, "MATERIAL —", {
      fontFamily: FONTS.mono,
      fontSize: "9px",
      color: colorString(COLORS.ivoryMuted),
      align: "right",
    }).setOrigin(1, 0).setDepth(110);

    graphics.fillStyle(COLORS.indigo, 0.92);
    graphics.fillRoundedRect(SIDE.x + 18, SIDE.y + 120, SIDE.width - 36, 106, 6);
    graphics.lineStyle(1, COLORS.cyan, 0.32);
    graphics.strokeRoundedRect(SIDE.x + 18, SIDE.y + 120, SIDE.width - 36, 106, 6);
    makeText(this, SIDE.x + 30, SIDE.y + 134, "SPATIAL FEEDBACK", {
      fontFamily: FONTS.mono,
      fontSize: "9px",
      color: colorString(COLORS.gold),
      letterSpacing: 1,
    }).setDepth(110);
    this.selectedFluxText = makeText(this, SIDE.x + 30, SIDE.y + 160, "FLUX  —", {
      fontFamily: FONTS.mono,
      fontSize: "11px",
      color: colorString(COLORS.ivory),
      fontStyle: "bold",
    }).setDepth(110);
    this.kText = makeText(this, SIDE.x + 30, SIDE.y + 184, "K-EIGENVALUE  —", {
      fontFamily: FONTS.mono,
      fontSize: "11px",
      color: colorString(COLORS.cyan),
      fontStyle: "bold",
    }).setDepth(110);
    this.headerSolve = makeText(this, SIDE.x + SIDE.width - 30, SIDE.y + 184, "—", {
      fontFamily: FONTS.mono,
      fontSize: "9px",
      color: colorString(COLORS.green),
      align: "right",
    }).setOrigin(1, 0).setDepth(110);
    this.powerText = makeText(this, SIDE.x + 30, SIDE.y + 205, "TOTAL POWER  —", {
      fontFamily: FONTS.mono,
      fontSize: "9px",
      color: colorString(COLORS.ivoryMuted),
    }).setDepth(110);

    this.fuelButton = makeButton(
      this,
      SIDE.x + SIDE.width / 2,
      SIDE.y + 260,
      SIDE.width - 48,
      40,
      "SET NONFUEL / MODERATOR",
      () => this.toggleFuel(),
      { tone: "magenta", fontSize: 11 },
    );
    this.fuelButton.gameObject.setDepth(120);
    makeText(this, SIDE.x + 24, SIDE.y + 304, "REFLECTIVE FACES  /  INTERIOR SELECTABLE", {
      fontFamily: FONTS.mono,
      fontSize: "9px",
      color: colorString(COLORS.gold),
      letterSpacing: 0.8,
    }).setDepth(110);

    const facePositions: Array<[LabBoundaryFace, string, number, number]> = [
      ["north", "NORTH", SIDE.x + 102, SIDE.y + 350],
      ["east", "EAST", SIDE.x + 306, SIDE.y + 350],
      ["south", "SOUTH", SIDE.x + 102, SIDE.y + 395],
      ["west", "WEST", SIDE.x + 306, SIDE.y + 395],
      ["end-a", "END A", SIDE.x + 102, SIDE.y + 440],
      ["end-b", "END B", SIDE.x + 306, SIDE.y + 440],
    ];
    for (const [face, label, x, y] of facePositions) {
      const button = makeButton(this, x, y, 170, 32, label, () => this.toggleFace(face), {
        tone: "gold",
        fontSize: 9,
        compact: true,
      });
      button.gameObject.setDepth(120);
      this.faceButtons.set(face, button);
    }
    this.selectedFacesText = makeText(this, SIDE.x + 24, SIDE.y + 492, "REFLECTORS  —", {
      fontFamily: FONTS.mono,
      fontSize: "9px",
      color: colorString(COLORS.ivoryMuted),
      wordWrap: { width: SIDE.width - 48 },
    }).setDepth(110);
    this.solveText = makeText(this, SIDE.x + 24, SIDE.y + 530, "SOLVE STATUS  —", {
      fontFamily: FONTS.mono,
      fontSize: "9px",
      color: colorString(COLORS.green),
      wordWrap: { width: SIDE.width - 48 },
    }).setDepth(110);
    this.feedbackText = makeText(this, SIDE.x + 24, SIDE.y + 565, this.resultMessage, {
      fontFamily: FONTS.body,
      fontSize: "11px",
      color: colorString(COLORS.ivoryMuted),
      wordWrap: { width: SIDE.width - 48 },
      lineSpacing: 3,
    }).setDepth(110);
    this.solveButton = makeButton(
      this,
      SIDE.x + 84,
      SIDE.y + 638,
      140,
      38,
      "SOLVE  S  ↗",
      () => this.solve(),
      { tone: "cyan", fontSize: 10 },
    );
    this.solveButton.gameObject.setDepth(120);
    this.resetButton = makeButton(
      this,
      SIDE.x + 218,
      SIDE.y + 638,
      100,
      38,
      "RESET  R",
      () => this.reset(),
      { tone: "gold", fontSize: 9, compact: true },
    );
    this.resetButton.gameObject.setDepth(120);
    this.backButton = makeButton(
      this,
      SIDE.x + SIDE.width - 50,
      SIDE.y + 638,
      72,
      38,
      "MODE  ↩",
      () => this.backToTitle(),
      { tone: "quiet", fontSize: 9, compact: true },
    );
    this.backButton.gameObject.setDepth(120);
  }

  private createUnavailableOverlay(): void {
    const container = this.add.container(0, 0).setDepth(600).setVisible(false);
    const graphics = this.add.graphics();
    graphics.fillStyle(COLORS.ink, 0.9);
    graphics.fillRect(0, 116, VIEW_WIDTH, VIEW_HEIGHT - 116);
    graphics.fillStyle(COLORS.panelRaised, 0.98);
    graphics.fillRoundedRect(420, 300, 760, 230, 10);
    graphics.lineStyle(2, COLORS.red, 0.76);
    graphics.strokeRoundedRect(420, 300, 760, 230, 10);
    drawCornerBrackets(graphics, 420, 300, 760, 230, COLORS.gold);
    container.add(graphics);
    this.unavailableTitle = makeText(this, 800, 356, "LAB SNAPSHOT UNAVAILABLE", {
      fontFamily: FONTS.display,
      fontSize: "29px",
      color: colorString(COLORS.ivory),
      fontStyle: "bold",
      align: "center",
    }).setOrigin(0.5);
    this.unavailableDetail = makeText(this, 800, 425, "", {
      fontFamily: FONTS.body,
      fontSize: "16px",
      color: colorString(COLORS.ivoryMuted),
      align: "center",
      wordWrap: { width: 620 },
    }).setOrigin(0.5);
    container.add([this.unavailableTitle, this.unavailableDetail]);
    this.unavailableOverlay = container;
  }

  private refresh(): void {
    this.refreshGrid();
    this.refreshSidePanel();
    const lab = this.snapshot.lab;
    const unavailable = !this.session.status.isWasmAvailable || lab === undefined;
    this.unavailableOverlay?.setVisible(unavailable);
    this.unavailableDetail?.setText(!this.session.status.isWasmAvailable
      ? this.session.status.detail
      : "The authoritative Lab session did not return a 2 × 8 topology.");
  }

  private refreshGrid(): void {
    const lab = this.snapshot.lab;
    for (const tile of this.tiles.values()) {
      const cell = lab === undefined
        ? null
        : labCellAt(lab, tile.channelIndex, tile.position);
      const flux = lab === undefined ? null : labFluxAt(lab, tile.channelIndex, tile.position);
      const selected = tile.channelIndex === this.selectedCell.channelIndex && tile.position === this.selectedCell.position;
      const normalized = flux?.normalized ?? 0;
      const fill = cell === null
        ? COLORS.indigo
        : cell.hasFuel
          ? mixColor(COLORS.cyanDark, COLORS.gold, Math.min(1, Math.max(0, normalized)))
          : COLORS.magentaDark;
      tile.graphics.clear();
      tile.graphics.fillStyle(COLORS.ink, 0.9);
      tile.graphics.fillRoundedRect(-54, -98, 108, 196, 5);
      tile.graphics.fillStyle(fill, cell === null ? 0.3 : cell.hasFuel ? 0.95 : 0.86);
      tile.graphics.fillRoundedRect(-50, -94, 100, 188, 4);
      tile.graphics.fillStyle(COLORS.white, 0.08);
      tile.graphics.fillRect(-48, -92, 96, 28);
      tile.graphics.lineStyle(selected ? 2.5 : 1, selected ? COLORS.ivory : COLORS.grid, selected ? 0.98 : 0.75);
      tile.graphics.strokeRoundedRect(-50, -94, 100, 188, 4);
      if (cell !== null) {
      this.drawReflectiveFaces(tile.graphics, cell);
      }
      tile.stateText.setText(cell === null ? "—" : cell.hasFuel ? "FUEL" : "NONFUEL");
      tile.stateText.setColor(colorString(cell?.hasFuel ? COLORS.ink : COLORS.ivory));
      tile.fluxText.setText(flux === null ? "FLUX —" : `Φ ${formatLabFlux(flux.total)}`);
      tile.fluxText.setColor(colorString(cell?.hasFuel ? COLORS.ink : COLORS.ivoryMuted));
    }
  }

  private drawReflectiveFaces(graphics: Phaser.GameObjects.Graphics, cell: LabCellState): void {
    const faces = effectiveReflectiveFaces(cell);
    if (faces.length === 0) {
      return;
    }
    graphics.lineStyle(5, COLORS.gold, 0.94);
    if (faces.includes("north")) graphics.lineBetween(-49, -93, 49, -93);
    if (faces.includes("east")) graphics.lineBetween(49, -93, 49, 93);
    if (faces.includes("south")) graphics.lineBetween(-49, 93, 49, 93);
    if (faces.includes("west")) graphics.lineBetween(-49, -93, -49, 93);
    if (faces.includes("end-a")) graphics.lineBetween(-43, -88, -43, 88);
    if (faces.includes("end-b")) graphics.lineBetween(43, -88, 43, 88);
  }

  private drawSelection(time: number): void {
    if (this.selectionGraphics === null) return;
    const graphics = this.selectionGraphics;
    graphics.clear();
    const cellWidth = CELL_GRID.width / 8;
    const cellHeight = CELL_GRID.height / 2;
    const x = CELL_GRID.x + cellWidth * (this.selectedCell.position + 0.5);
    const y = CELL_GRID.y + cellHeight * (this.selectedCell.channelIndex + 0.5);
    const pulse = 1 + Math.sin(time * 0.005) * 0.04;
    graphics.lineStyle(2, COLORS.ivory, 0.96);
    graphics.strokeRoundedRect(x - 55 * pulse, y - 99 * pulse, 110 * pulse, 198 * pulse, 6);
    graphics.lineStyle(1, COLORS.cyan, 0.8);
    graphics.strokeRoundedRect(x - 60 * pulse, y - 104 * pulse, 120 * pulse, 208 * pulse, 7);
  }

  private refreshSidePanel(): void {
    const lab = this.snapshot.lab;
    const cell = lab === undefined ? null : labCellAt(lab, this.selectedCell.channelIndex, this.selectedCell.position);
    const flux = lab === undefined ? null : labFluxAt(lab, this.selectedCell.channelIndex, this.selectedCell.position);
    const state = lab?.spatialSolve.finalState;
    const diagnostics = lab?.spatialSolve.diagnostics;
    this.selectedText?.setText(`CH ${this.selectedCell.channelIndex}  /  POSITION ${String(this.selectedCell.position + 1).padStart(2, "0")}`);
    this.selectedStateText?.setText(cell === null ? "NO CELL DATA" : cell.hasFuel ? "FUEL CELL" : "NONFUEL CELL")
      .setColor(colorString(cell?.hasFuel ? COLORS.cyan : COLORS.magenta));
    this.selectedMaterialText?.setText(`MATERIAL  ${cell?.materialId ?? (cell?.hasFuel ? "FUEL" : "MODERATOR")}`);
    this.selectedFluxText?.setText(flux === null
      ? "FLUX  —"
      : `FLUX  ${formatLabFlux(flux.total)}   G1 ${formatLabFlux(flux.group1)}   G2 ${formatLabFlux(flux.group2)}`);
    this.kText?.setText(`K-EIGENVALUE  ${state === null || state === undefined ? "—" : state.eigenvalue.toFixed(6)}`);
    this.powerText?.setText(`TOTAL POWER  ${state === null || state === undefined ? "—" : `${state.totalPowerW.toExponential(3)} W`}`);
    this.headerSolve?.setText(lab?.spatialSolve.isConverged ? "CONVERGED" : lab === undefined ? "—" : "SETTLING")
      .setColor(colorString(lab?.spatialSolve.isConverged ? COLORS.green : COLORS.gold));
    this.solveText?.setText(diagnostics === undefined
      ? "SOLVE STATUS  —"
      : `SOLVE STATUS  ${lab?.spatialSolve.status.toUpperCase()} / ${diagnostics.iterationCount} ITER / ${diagnostics.convergenceReason}\nRELATIVE RESIDUAL  ${diagnostics.residualRelativeInfinity === null ? "—" : diagnostics.residualRelativeInfinity.toExponential(3)}`);
    const fixedFaces = cell === null
      ? []
      : FACE_ORDER.filter((face) => isFixedExteriorFace(cell.channelIndex, cell.position, face));
    const configuredFaces = cell === null
      ? []
      : sortReflectiveFaces(cell.reflectiveFaces.filter((face) => !fixedFaces.includes(face)));
    this.selectedFacesText?.setText(cell === null
      ? "REFLECTORS  —"
      : `FIXED OUTER  ${fixedFaces.map((face) => face.toUpperCase()).join(" · ")}\nSELECTED INTERIOR  ${configuredFaces.length === 0 ? "NONE" : configuredFaces.map((face) => face.toUpperCase()).join(" · ")}`);
    this.feedbackText?.setText(this.pending ? "COMMAND PENDING…" : this.resultMessage)
      .setColor(colorString(this.pending ? COLORS.gold : this.resultMessage.startsWith("REJECTED") || this.resultMessage.startsWith("FAILED") ? COLORS.red : COLORS.ivoryMuted));

    const controlsEnabled = lab !== undefined && !this.pending && this.session.status.isWasmAvailable && cell !== null;
    this.fuelButton?.setLabel(cell?.hasFuel ? "SET NONFUEL  /  MODERATOR" : "SET FUEL  /  ACTIVE");
    this.fuelButton?.setEnabled(controlsEnabled);
    this.solveButton?.setEnabled(lab !== undefined && !this.pending && this.session.status.isWasmAvailable);
    this.resetButton?.setEnabled(lab !== undefined && !this.pending && this.session.status.isWasmAvailable);
    this.backButton?.setEnabled(!this.pending);
    for (const face of FACE_ORDER) {
      const button = this.faceButtons.get(face);
      const fixed = cell !== null && isFixedExteriorFace(cell.channelIndex, cell.position, face);
      button?.setLabel(`${face.toUpperCase()}${fixed ? "  /  FIXED" : ""}`);
      button?.setEnabled(controlsEnabled && cell !== null && !cell.hasFuel && !fixed);
      button?.gameObject.setAlpha(cell?.reflectiveFaces.includes(face) ? 1 : 0.52);
    }
  }

  private toggleFuel(): void {
    const lab = this.snapshot.lab;
    if (lab === undefined || this.pending) return;
    const cell = labCellAt(lab, this.selectedCell.channelIndex, this.selectedCell.position);
    const hasFuel = !cell.hasFuel;
    const reflectiveFaces = hasFuel
      ? []
      : cell.reflectiveFaces.length > 0
        ? cell.reflectiveFaces
        : defaultReflectiveFaces(this.selectedCell.channelIndex, this.selectedCell.position);
    this.dispatchCell({ hasFuel, reflectiveFaces });
  }

  private toggleFace(face: LabBoundaryFace): void {
    const lab = this.snapshot.lab;
    if (lab === undefined || this.pending) return;
    const cell = labCellAt(lab, this.selectedCell.channelIndex, this.selectedCell.position);
    if (cell.hasFuel || isFixedExteriorFace(cell.channelIndex, cell.position, face)) return;
    this.dispatchCell({ hasFuel: false, reflectiveFaces: toggleReflectiveFace(cell.reflectiveFaces, face) });
  }

  private dispatchCell(change: { hasFuel: boolean; reflectiveFaces: LabBoundaryFace[] }): void {
    const command: Extract<import("../protocol").CanduCommand, { type: "configure-cell" }> = {
      type: "configure-cell",
      channelIndex: this.selectedCell.channelIndex,
      position: this.selectedCell.position,
      hasFuel: change.hasFuel,
      reflectiveFaces: sortReflectiveFaces(change.reflectiveFaces),
    };
    this.resultMessage = "CONFIGURE PENDING…";
    this.refreshSidePanel();
    void this.session.dispatch(command).catch((error: unknown) => {
      const message = error instanceof Error ? error.message : String(error);
      this.resultMessage = `FAILED  /  ${message}`;
      this.refreshSidePanel();
    });
  }

  private solve(): void {
    if (this.snapshot.lab === undefined || this.pending) return;
    this.resultMessage = "SOLVE PENDING…";
    this.refreshSidePanel();
    void this.session.dispatch({ type: "solve" }).catch((error: unknown) => {
      const message = error instanceof Error ? error.message : String(error);
      this.resultMessage = `FAILED  /  ${message}`;
      this.refreshSidePanel();
    });
  }

  private reset(): void {
    if (this.pending || !this.session.status.isWasmAvailable) return;
    this.resultMessage = "RESET PENDING…";
    this.refreshSidePanel();
    void this.session.dispatch({ type: "reset" }).catch((error: unknown) => {
      const message = error instanceof Error ? error.message : String(error);
      this.resultMessage = `FAILED  /  ${message}`;
      this.refreshSidePanel();
    });
  }

  private receiveSessionUpdate(update: SessionUpdate): void {
    this.snapshot = update.snapshot;
    this.pending = update.pending;
    if (update.error !== null) {
      this.resultMessage = `FAILED  /  ${update.error}`;
    }
    const response = update.response;
    if (response !== null && response !== this.lastResponse) {
      this.lastResponse = response;
      this.resultMessage = response.accepted
        ? `ACCEPTED  /  ${response.message}`
        : `REJECTED  /  ${response.message}`;
    }
    this.refresh();
  }

  private handleKeyDown(event: KeyboardEvent): void {
    const key = event.key.toLowerCase();
    if (key === "escape") {
      this.backToTitle();
      return;
    }
    if (key === "f") {
      this.toggleFuel();
      return;
    }
    if (key === "s") {
      this.solve();
      return;
    }
    if (key === "r") {
      this.reset();
      return;
    }
    if (/^[1-8]$/.test(key) && !this.pending) {
      this.selectedCell = { ...this.selectedCell, position: Number(key) - 1 };
      this.refresh();
    }
  }

  private backToTitle(): void {
    if (this.pending) return;
    this.session.stopShift();
    this.scene.start("TitleScene");
  }
}
