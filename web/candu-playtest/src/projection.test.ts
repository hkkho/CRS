import { describe, expect, it } from "vitest";
import {
  CANDU6_ROW_LABELS,
  channelDisplayId,
  createCoreFaceLayout,
  findAdjacentChannelIndex,
  gridCellFromPoint,
  gridCoordinateLabel,
  projectChannelToFace,
} from "./projection";

describe("Phaser front-facing core projection helpers", () => {
  const layout = createCoreFaceLayout(40, 120, 1180, 660);

  it("keeps the 22 by 22 stepped grid truly orthographic", () => {
    const topLeft = projectChannelToFace({ gridColumn: 0, gridRow: 0 }, layout);
    const topRight = projectChannelToFace({ gridColumn: 21, gridRow: 0 }, layout);
    const bottomLeft = projectChannelToFace({ gridColumn: 0, gridRow: 21 }, layout);
    const bottomRight = projectChannelToFace({ gridColumn: 21, gridRow: 21 }, layout);

    expect(topLeft.x).toBeLessThan(topRight.x);
    expect(topLeft.y).toBeCloseTo(topRight.y);
    expect(topLeft.y).toBeLessThan(bottomLeft.y);
    expect(bottomLeft.y).toBeCloseTo(bottomRight.y);
    expect(topLeft.x).toBeCloseTo(bottomLeft.x);
    expect(topRight.x).toBeCloseTo(bottomRight.x);
    expect(layout.tileWidth).toBeLessThan(layout.stepX);
    expect(layout.tileHeight).toBeLessThan(layout.stepY);
  });

  it("formats the CANDU row/column coordinate without inventing a channel", () => {
    expect(CANDU6_ROW_LABELS).toHaveLength(22);
    expect(gridCoordinateLabel({ gridColumn: 4, gridRow: 8 })).toBe("J05");
    expect(channelDisplayId(7)).toBe("CH 007");
  });

  it("maps pointer coordinates back to the same direct row and column", () => {
    const point = projectChannelToFace({ gridColumn: 13, gridRow: 8 }, layout);

    expect(gridCellFromPoint(point.x, point.y, layout)).toEqual({ gridColumn: 13, gridRow: 8 });
    expect(gridCellFromPoint(layout.gridX - 1, point.y, layout)).toBeNull();
  });

  it("moves to an exact neighbor and skips topology gaps predictably", () => {
    const channels = [
      { channelIndex: 10, gridColumn: 2, gridRow: 2 },
      { channelIndex: 11, gridColumn: 3, gridRow: 2 },
      { channelIndex: 12, gridColumn: 5, gridRow: 2 },
      { channelIndex: 13, gridColumn: 2, gridRow: 3 },
    ];

    expect(findAdjacentChannelIndex(channels, 10, 1, 0)).toBe(11);
    expect(findAdjacentChannelIndex(channels, 11, 1, 0)).toBe(12);
    expect(findAdjacentChannelIndex(channels, 10, 0, 1)).toBe(13);
  });
});
