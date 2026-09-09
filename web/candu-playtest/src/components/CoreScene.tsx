import { useEffect, useRef } from "react";
import Phaser from "phaser";
import type { CanduChannelSnapshot } from "../protocol";
import { getFlowArrow, getFlowDirectionLabel, getHeatColor } from "../visuals";

const CANDU6_ROW_LABELS = ["A", "B", "C", "D", "E", "F", "G", "H", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W"] as const;
const CORE_GRID_SIZE = 22;
const CORE_GRID_SPACING = 0.83;
const CORE_GRID_OFFSET = 10.5;
const CORE_GRID_HALF_EXTENT = 9.2;
const CORE_VIEW_HALF_EXTENT = 10.25;
const CORE_CHANNEL_RADIUS = 0.31;

const CORE_FLOOR_COLOR = 0x0b1c2b;
const CORE_GRID_COLOR = 0x31556a;
const CORE_OUTLINE_COLOR = 0x5ed7c5;
const CORE_CHANNEL_EDGE_COLOR = 0x07131f;
const CORE_FLOW_COLOR = 0xd7fff1;
const CORE_MARKER_COLOR = 0xf9e4ac;

interface CoreSceneProps {
  channels: readonly CanduChannelSnapshot[];
  selectedChannelIndex: number;
  viewMode: "engine2d" | "grid";
  onSelectChannel: (channelIndex: number) => void;
  onChangeViewMode: (viewMode: "engine2d" | "grid") => void;
}

interface CorePoint {
  x: number;
  y: number;
}

interface CoreLayout {
  centerX: number;
  centerY: number;
  scale: number;
}

class PhaserCoreScene extends Phaser.Scene {
  private channels: readonly CanduChannelSnapshot[];
  private selectedChannelIndex: number;
  private readonly onSelectChannel: (channelIndex: number) => void;
  private readonly reducedMotion: boolean;
  private layout: CoreLayout = createCoreLayout(1, 1);
  private staticGraphics: Phaser.GameObjects.Graphics | null = null;
  private channelGraphics: Phaser.GameObjects.Graphics | null = null;
  private flowGraphics: Phaser.GameObjects.Graphics | null = null;
  private markerGraphics: Phaser.GameObjects.Graphics | null = null;
  private animationTime = 0;

  constructor(
    channels: readonly CanduChannelSnapshot[],
    selectedChannelIndex: number,
    onSelectChannel: (channelIndex: number) => void,
    reducedMotion: boolean,
  ) {
    super({ key: "candu-core-surface" });
    this.channels = channels;
    this.selectedChannelIndex = selectedChannelIndex;
    this.onSelectChannel = onSelectChannel;
    this.reducedMotion = reducedMotion;
  }

  create(): void {
    this.staticGraphics = this.add.graphics().setDepth(0);
    this.channelGraphics = this.add.graphics().setDepth(1);
    this.flowGraphics = this.add.graphics().setDepth(2);
    this.markerGraphics = this.add.graphics().setDepth(3);
    this.input.on("pointerdown", this.handlePointerDown, this);

    this.resize(this.scale.width, this.scale.height);
  }

  update(time: number): void {
    if (!this.reducedMotion) {
      this.animationTime = time;
      this.drawMarker();
    }
  }

  resize(width: number, height: number): void {
    this.layout = createCoreLayout(width, height);
    this.drawCore();
  }

  setChannels(channels: readonly CanduChannelSnapshot[], selectedChannelIndex: number): void {
    this.channels = channels;
    this.selectedChannelIndex = selectedChannelIndex;
    this.drawCore();
  }

  private drawCore(): void {
    if (
      this.staticGraphics === null ||
      this.channelGraphics === null ||
      this.flowGraphics === null ||
      this.markerGraphics === null
    ) {
      return;
    }

    this.drawStaticSurface(this.staticGraphics);
    this.drawChannels(this.channelGraphics);
    this.drawFlowCues(this.flowGraphics);
    this.drawMarker();
  }

  private drawStaticSurface(graphics: Phaser.GameObjects.Graphics): void {
    graphics.clear();
    graphics.fillStyle(CORE_FLOOR_COLOR, 0.84);
    const gridExtent = (CORE_GRID_SIZE / 2) * CORE_GRID_SPACING;
    const corners = [
      getIsoPoint(-gridExtent, -gridExtent, this.layout),
      getIsoPoint(gridExtent, -gridExtent, this.layout),
      getIsoPoint(gridExtent, gridExtent, this.layout),
      getIsoPoint(-gridExtent, gridExtent, this.layout),
    ];
    graphics.fillPoints(corners, true);

    graphics.lineStyle(Math.max(1, this.layout.scale * 0.025), CORE_GRID_COLOR, 0.3);
    for (let index = 0; index <= CORE_GRID_SIZE; index += 1) {
      const coordinate = -gridExtent + index * CORE_GRID_SPACING;
      const columnStart = getIsoPoint(coordinate, -gridExtent, this.layout);
      const columnEnd = getIsoPoint(coordinate, gridExtent, this.layout);
      const rowStart = getIsoPoint(-gridExtent, coordinate, this.layout);
      const rowEnd = getIsoPoint(gridExtent, coordinate, this.layout);
      graphics.lineBetween(columnStart.x, columnStart.y, columnEnd.x, columnEnd.y);
      graphics.lineBetween(rowStart.x, rowStart.y, rowEnd.x, rowEnd.y);
    }

    graphics.lineStyle(Math.max(1, this.layout.scale * 0.035), CORE_OUTLINE_COLOR, 0.7);
    graphics.strokePoints(corners, true);
  }

  private drawChannels(graphics: Phaser.GameObjects.Graphics): void {
    graphics.clear();
    const edgeWidth = Math.max(0.75, this.layout.scale * 0.025);
    for (const channel of this.channels) {
      const position = getCanvasChannelPosition(channel, this.layout);
      const width = CORE_CHANNEL_RADIUS * 2.1 * this.layout.scale;
      const height = width * (0.56 + channel.localPowerFraction * 0.1);
      const points = diamondPoints(position, width, height);
      graphics.fillStyle(parseRgbColor(getHeatColor(channel.localPowerFraction)), 0.94);
      graphics.fillPoints(points, true);
      graphics.lineStyle(edgeWidth, CORE_CHANNEL_EDGE_COLOR, 0.55);
      graphics.strokePoints(points, true);
    }
  }

  private drawFlowCues(graphics: Phaser.GameObjects.Graphics): void {
    graphics.clear();
    graphics.fillStyle(CORE_FLOW_COLOR, 0.72);
    for (const channel of this.channels) {
      const position = getCanvasChannelPosition(channel, this.layout);
      const pointsTowardEndB = channel.flowDirection === "toward-end-b";
      const direction = pointsTowardEndB ? 1 : -1;
      const centerX = position.x + direction * 0.14 * this.layout.scale;
      const centerY = position.y + 0.16 * this.layout.scale;
      const tipX = centerX + direction * 0.11 * this.layout.scale;
      const baseX = centerX - direction * 0.07 * this.layout.scale;
      const halfWidth = Math.max(1.5, this.layout.scale * 0.075);
      graphics.fillTriangle(tipX, centerY, baseX, centerY - halfWidth, baseX, centerY + halfWidth);
    }
  }

  private drawMarker(): void {
    if (this.markerGraphics === null) {
      return;
    }

    this.markerGraphics.clear();
    const selected = this.channels[this.selectedChannelIndex];
    if (selected === undefined) {
      return;
    }

    const position = getCanvasChannelPosition(selected, this.layout);
    const pulse = this.reducedMotion ? 1 : 1 + Math.sin(this.animationTime * 0.004) * 0.05;
    this.markerGraphics.lineStyle(Math.max(1.5, this.layout.scale * 0.06), CORE_MARKER_COLOR, 0.98);
    this.markerGraphics.strokeCircle(position.x, position.y, this.layout.scale * 0.43 * pulse);
  }

  private handlePointerDown(pointer: Phaser.Input.Pointer): void {
    const point = pointer.positionToCamera(this.cameras.main) as Phaser.Math.Vector2;
    const hitRadius = Math.max(this.layout.scale * 0.38, 9);
    let closestIndex = -1;
    let closestDistanceSquared = hitRadius * hitRadius;

    this.channels.forEach((channel, index) => {
      const position = getCanvasChannelPosition(channel, this.layout);
      const deltaX = point.x - position.x;
      const deltaY = point.y - position.y;
      const distanceSquared = deltaX * deltaX + deltaY * deltaY;
      if (distanceSquared <= closestDistanceSquared) {
        closestDistanceSquared = distanceSquared;
        closestIndex = index;
      }
    });

    if (closestIndex >= 0) {
      this.onSelectChannel(closestIndex);
    }
  }
}

export function CoreScene({
  channels,
  selectedChannelIndex,
  viewMode,
  onSelectChannel,
  onChangeViewMode,
}: CoreSceneProps) {
  const mountRef = useRef<HTMLDivElement>(null);
  const sceneRef = useRef<PhaserCoreScene | null>(null);
  const channelsRef = useRef(channels);
  const selectRef = useRef(onSelectChannel);
  const changeViewRef = useRef(onChangeViewMode);

  useEffect(() => {
    channelsRef.current = channels;
  }, [channels]);

  useEffect(() => {
    selectRef.current = onSelectChannel;
  }, [onSelectChannel]);

  useEffect(() => {
    changeViewRef.current = onChangeViewMode;
  }, [onChangeViewMode]);

  useEffect(() => {
    if (viewMode !== "engine2d" || mountRef.current === null) {
      return;
    }

    const mount = mountRef.current;
    const initialWidth = Math.max(1, mount.clientWidth);
    const initialHeight = Math.max(1, mount.clientHeight);
    const reducedMotion = window.matchMedia?.("(prefers-reduced-motion: reduce)").matches ?? false;
    const scene = new PhaserCoreScene(
      channelsRef.current,
      selectedChannelIndex,
      (channelIndex) => selectRef.current(channelIndex),
      reducedMotion,
    );
    sceneRef.current = scene;

    let game: Phaser.Game;
    try {
      game = new Phaser.Game({
        type: Phaser.CANVAS,
        parent: mount,
        width: initialWidth,
        height: initialHeight,
        scene,
        transparent: true,
        backgroundColor: "rgba(0,0,0,0)",
        canvasStyle: "display:block;width:100%;height:100%;touch-action:none;",
        antialias: true,
        pixelArt: false,
        roundPixels: false,
        banner: false,
        scale: {
          mode: Phaser.Scale.RESIZE,
          width: initialWidth,
          height: initialHeight,
        },
      });
    } catch {
      sceneRef.current = null;
      changeViewRef.current("grid");
      return;
    }

    game.canvas.className = "core-phaser-canvas";
    game.canvas.setAttribute("role", "img");
    game.canvas.setAttribute(
      "aria-label",
      "Interactive Phaser-rendered isometric CANDU 6 channel heat map. Use Grid Map for keyboard channel selection.",
    );

    const resize = () => {
      const width = Math.max(1, mount.clientWidth);
      const height = Math.max(1, mount.clientHeight);
      game.scale.resize(width, height);
      scene.resize(width, height);
    };
    resize();
    const observer = new ResizeObserver(resize);
    observer.observe(mount);

    return () => {
      observer.disconnect();
      game.destroy(true);
      sceneRef.current = null;
    };
  }, [viewMode]);

  useEffect(() => {
    sceneRef.current?.setChannels(channels, selectedChannelIndex);
  }, [channels, selectedChannelIndex]);

  return (
    <div className="core-scene-shell">
      <div className="core-scene-toolbar">
        <div>
          <p className="panel-kicker">Tactical core board</p>
          <p className="scene-caption">CANDU 6 · PHASER 2D · 380 CHANNELS · ANGLED CORE / FUELLING FLOW</p>
        </div>
        <div className="view-toggle" role="group" aria-label="Core rendering mode">
          <button
            className={viewMode === "engine2d" ? "view-toggle-button is-active" : "view-toggle-button"}
            type="button"
            aria-pressed={viewMode === "engine2d"}
            aria-label="Use Phaser-rendered tactical core map"
            title="Phaser-rendered tactical core map"
            onClick={() => onChangeViewMode("engine2d")}
          >
            PHASER MAP
          </button>
          <button
            className={viewMode === "grid" ? "view-toggle-button is-active" : "view-toggle-button"}
            type="button"
            aria-pressed={viewMode === "grid"}
            aria-label="Use accessible grid map"
            title="Keyboard-accessible HTML grid map"
            onClick={() => onChangeViewMode("grid")}
          >
            GRID MAP
          </button>
        </div>
      </div>
      {viewMode === "engine2d" ? (
        <div
          ref={mountRef}
          className="core-canvas-mount"
          title="Select a reactor channel in the Phaser tactical map"
        />
      ) : (
        <CoreMapGrid channels={channels} selectedChannelIndex={selectedChannelIndex} onSelectChannel={onSelectChannel} />
      )}
    </div>
  );
}

function CoreMapGrid({
  channels,
  selectedChannelIndex,
  onSelectChannel,
}: Pick<CoreSceneProps, "channels" | "selectedChannelIndex" | "onSelectChannel">) {
  return (
    <div className="core-map-2d" role="grid" aria-label="Keyboard-accessible CANDU 6 channel heat map with alternating fuelling directions">
      <div className="core-map-column-labels" aria-hidden="true">
        {Array.from({ length: 22 }, (_, index) => <span key={index}>{String(index + 1).padStart(2, "0")}</span>)}
      </div>
      <div className="core-map-row-labels" aria-hidden="true">
        {CANDU6_ROW_LABELS.map((label) => <span key={label}>{label}</span>)}
      </div>
      {channels.map((channel) => (
        <button
          className={channel.channelIndex === selectedChannelIndex ? "channel-cell is-selected" : "channel-cell"}
          key={channel.channelIndex}
          type="button"
          role="gridcell"
          aria-label={`Channel ${channel.channelIndex}, ${CANDU6_ROW_LABELS[channel.gridRow]}${String(channel.gridColumn + 1).padStart(2, "0")}, power ${(channel.localPowerFraction * 100).toFixed(1)} percent, ${getFlowDirectionLabel(channel.flowDirection)}`}
          aria-pressed={channel.channelIndex === selectedChannelIndex}
          title={`CH ${channel.channelIndex} · ${CANDU6_ROW_LABELS[channel.gridRow]}${String(channel.gridColumn + 1).padStart(2, "0")} · ${(channel.localPowerFraction * 100).toFixed(1)}% power · ${getFlowDirectionLabel(channel.flowDirection)}`}
          style={{
            gridColumn: channel.gridColumn + 1,
            gridRow: channel.gridRow + 1,
            background: getHeatColor(channel.localPowerFraction),
          }}
          onClick={() => onSelectChannel(channel.channelIndex)}
        >
          <span className="channel-cell-index">{channel.channelIndex}</span>
          <span className="channel-cell-flow" aria-hidden="true">{getFlowArrow(channel.flowDirection)}</span>
        </button>
      ))}
    </div>
  );
}

function createCoreLayout(width: number, height: number): CoreLayout {
  const safeWidth = Math.max(1, width);
  const safeHeight = Math.max(1, height);
  return {
    centerX: safeWidth / 2,
    centerY: safeHeight / 2,
    scale: Math.min(safeWidth, safeHeight) / (CORE_VIEW_HALF_EXTENT * 2),
  };
}

function getCanvasChannelPosition(channel: CanduChannelSnapshot, layout: CoreLayout): CorePoint {
    const worldX = (channel.gridColumn - channel.gridRow) * CORE_GRID_SPACING * 0.5;
    const worldY = (channel.gridColumn + channel.gridRow - CORE_GRID_SIZE + 1) * CORE_GRID_SPACING * 0.5;
    return {
      x: layout.centerX + worldX * layout.scale,
      y: layout.centerY + worldY * layout.scale,
    };
}

function getIsoPoint(worldColumn: number, worldRow: number, layout: CoreLayout): CorePoint {
  return {
    x: layout.centerX + (worldColumn - worldRow) * layout.scale * 0.5,
    y: layout.centerY + (worldColumn + worldRow) * layout.scale * 0.5,
  };
}

function diamondPoints(center: CorePoint, width: number, height: number): CorePoint[] {
  return [
    { x: center.x, y: center.y - height / 2 },
    { x: center.x + width / 2, y: center.y },
    { x: center.x, y: center.y + height / 2 },
    { x: center.x - width / 2, y: center.y },
  ];
}

function parseRgbColor(color: string): number {
  const channels = color.match(/\d+/g);
  if (channels === null || channels.length < 3) {
    return CORE_FLOOR_COLOR;
  }
  return (Number(channels[0]) << 16) | (Number(channels[1]) << 8) | Number(channels[2]);
}
