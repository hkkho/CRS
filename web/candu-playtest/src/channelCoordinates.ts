import type { CanduChannelSnapshot } from "./protocol";

export const CANDU6_ROW_LABELS = [
  "A", "B", "C", "D", "E", "F", "G", "H", "J", "K", "L", "M",
  "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W",
] as const;

export function gridCoordinateLabel(channel: Pick<CanduChannelSnapshot, "gridColumn" | "gridRow">): string {
  const row = CANDU6_ROW_LABELS[channel.gridRow] ?? "?";
  return `${row}${String(channel.gridColumn + 1).padStart(2, "0")}`;
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
