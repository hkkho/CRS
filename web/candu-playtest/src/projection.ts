import type { CanduChannelSnapshot } from "./protocol";

export const CANDU6_ROW_LABELS = [
  "A", "B", "C", "D", "E", "F", "G", "H", "J", "K", "L", "M",
  "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W",
] as const;

/**
 * Layout for the reactor face. Coordinates are deliberately orthographic:
 * one grid column is always one horizontal screen step and one grid row is
 * always one vertical screen step. The snapshot owns which of the 22 × 22
 * cells are populated, so the stepped 380-channel topology remains visible.
 */
export interface CoreFaceLayout {
  gridX: number;
  gridY: number;
  gridPixelWidth: number;
  gridPixelHeight: number;
  centerX: number;
  centerY: number;
  stepX: number;
  stepY: number;
  tileWidth: number;
  tileHeight: number;
  gridWidth: number;
  gridHeight: number;
}

export interface CorePoint {
  x: number;
  y: number;
}

export interface CoreGridCell {
  gridColumn: number;
  gridRow: number;
}

export function createCoreFaceLayout(
  x: number,
  y: number,
  width: number,
  height: number,
  gridWidth = 22,
  gridHeight = 22,
): CoreFaceLayout {
  const safeWidth = Math.max(1, width);
  const safeHeight = Math.max(1, height);
  const leftGutter = 48;
  const rightGutter = 28;
  const topGutter = 66;
  const bottomGutter = 58;
  const gridPixelWidth = Math.max(gridWidth, safeWidth - leftGutter - rightGutter);
  const gridPixelHeight = Math.max(gridHeight, safeHeight - topGutter - bottomGutter);
  const stepX = gridPixelWidth / Math.max(1, gridWidth);
  const stepY = gridPixelHeight / Math.max(1, gridHeight);

  return {
    gridX: x + leftGutter,
    gridY: y + topGutter,
    gridPixelWidth,
    gridPixelHeight,
    centerX: x + leftGutter + gridPixelWidth / 2,
    centerY: y + topGutter + gridPixelHeight / 2,
    stepX,
    stepY,
    tileWidth: Math.max(14, stepX - 4),
    tileHeight: Math.max(10, stepY - 4),
    gridWidth,
    gridHeight,
  };
}

export function projectGridPoint(
  gridColumn: number,
  gridRow: number,
  layout: CoreFaceLayout,
): CorePoint {
  return {
    x: layout.gridX + (gridColumn + 0.5) * layout.stepX,
    y: layout.gridY + (gridRow + 0.5) * layout.stepY,
  };
}

export function projectChannelToFace(
  channel: Pick<CanduChannelSnapshot, "gridColumn" | "gridRow">,
  layout: CoreFaceLayout,
): CorePoint {
  return projectGridPoint(channel.gridColumn, channel.gridRow, layout);
}

export function gridCellFromPoint(x: number, y: number, layout: CoreFaceLayout): CoreGridCell | null {
  if (x < layout.gridX || y < layout.gridY ||
    x >= layout.gridX + layout.gridPixelWidth || y >= layout.gridY + layout.gridPixelHeight) {
    return null;
  }
  return {
    gridColumn: Math.floor((x - layout.gridX) / layout.stepX),
    gridRow: Math.floor((y - layout.gridY) / layout.stepY),
  };
}

export function gridCoordinateLabel(channel: Pick<CanduChannelSnapshot, "gridColumn" | "gridRow">): string {
  const row = CANDU6_ROW_LABELS[channel.gridRow] ?? "?";
  return `${row}${String(channel.gridColumn + 1).padStart(2, "0")}`;
}

export function channelDisplayId(channelIndex: number): string {
  return `CH ${String(channelIndex).padStart(3, "0")}`;
}

export function findAdjacentChannelIndex(
  channels: readonly Pick<CanduChannelSnapshot, "channelIndex" | "gridColumn" | "gridRow">[],
  selectedChannelIndex: number,
  deltaColumn: number,
  deltaRow: number,
): number {
  const selected = channels.find((channel) => channel.channelIndex === selectedChannelIndex);
  if (selected === undefined || (deltaColumn === 0 && deltaRow === 0)) {
    return selectedChannelIndex;
  }

  const targetColumn = selected.gridColumn + deltaColumn;
  const targetRow = selected.gridRow + deltaRow;
  const exact = channels.find(
    (channel) => channel.gridColumn === targetColumn && channel.gridRow === targetRow,
  );
  if (exact !== undefined) {
    return exact.channelIndex;
  }

  const directionColumn = Math.sign(deltaColumn);
  const directionRow = Math.sign(deltaRow);
  const candidates = channels
    .filter((channel) => {
      const columnDelta = channel.gridColumn - selected.gridColumn;
      const rowDelta = channel.gridRow - selected.gridRow;
      return (directionColumn === 0 || Math.sign(columnDelta) === directionColumn) &&
        (directionRow === 0 || Math.sign(rowDelta) === directionRow);
    })
    .sort((left, right) => {
      const leftDistance = Math.abs(left.gridColumn - targetColumn) + Math.abs(left.gridRow - targetRow);
      const rightDistance = Math.abs(right.gridColumn - targetColumn) + Math.abs(right.gridRow - targetRow);
      return leftDistance - rightDistance;
    });

  return candidates[0]?.channelIndex ?? selectedChannelIndex;
}

/** Refuelling moves along the horizontal pressure-tube axis on the face map. */
export function getFlowVector(direction: "toward-end-a" | "toward-end-b"): CorePoint {
  return direction === "toward-end-b" ? { x: 1, y: 0 } : { x: -1, y: 0 };
}
