import type { CanduChannelSnapshot } from "./protocol";

export const CANDU6_ROW_LABELS = [
  "A", "B", "C", "D", "E", "F", "G", "H", "J", "K", "L", "M",
  "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W",
] as const;

export interface IsoCoreLayout {
  centerX: number;
  centerY: number;
  stepX: number;
  stepY: number;
  tileWidth: number;
  tileHeight: number;
  gridWidth: number;
  gridHeight: number;
}

export interface IsoPoint {
  x: number;
  y: number;
}

export function createIsoCoreLayout(
  x: number,
  y: number,
  width: number,
  height: number,
  gridWidth = 22,
  gridHeight = 22,
): IsoCoreLayout {
  const safeWidth = Math.max(1, width);
  const safeHeight = Math.max(1, height);
  const tileWidth = Math.min(34, Math.max(20, safeWidth / 34));
  const tileHeight = Math.min(22, Math.max(14, safeHeight / 31));
  const stepX = Math.min(28, Math.max(16, (safeWidth - tileWidth - 16) / Math.max(1, (gridWidth - 1) * 2)));
  const stepY = Math.min(15, Math.max(9, (safeHeight - tileHeight - 16) / Math.max(1, (gridHeight - 1) * 2)));

  return {
    centerX: x + safeWidth / 2,
    centerY: y + safeHeight / 2,
    stepX,
    stepY,
    tileWidth,
    tileHeight,
    gridWidth,
    gridHeight,
  };
}

export function projectGridPoint(
  gridColumn: number,
  gridRow: number,
  layout: IsoCoreLayout,
): IsoPoint {
  return {
    x: layout.centerX + (gridColumn - gridRow) * layout.stepX,
    y: layout.centerY + (gridColumn + gridRow - (layout.gridWidth - 1)) * layout.stepY,
  };
}

export function projectChannelToIso(channel: Pick<CanduChannelSnapshot, "gridColumn" | "gridRow">, layout: IsoCoreLayout): IsoPoint {
  return projectGridPoint(channel.gridColumn, channel.gridRow, layout);
}

export function diamondPoints(center: IsoPoint, width: number, height: number): IsoPoint[] {
  return [
    { x: center.x, y: center.y - height / 2 },
    { x: center.x + width / 2, y: center.y },
    { x: center.x, y: center.y + height / 2 },
    { x: center.x - width / 2, y: center.y },
  ];
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

export function getFlowVector(direction: "toward-end-a" | "toward-end-b"): IsoPoint {
  return direction === "toward-end-b" ? { x: 1, y: 0.46 } : { x: -1, y: -0.46 };
}
