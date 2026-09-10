import { describe, expect, it } from "vitest";
import {
  CANDU6_ROW_LABELS,
  channelDisplayId,
  createIsoCoreLayout,
  diamondPoints,
  findAdjacentChannelIndex,
  gridCoordinateLabel,
  projectChannelToIso,
} from "./projection";

describe("Phaser core projection helpers", () => {
  const layout = createIsoCoreLayout(40, 120, 1180, 660);

  it("projects the stepped 22 by 22 grid around a stable isometric center", () => {
    const top = projectChannelToIso({ gridColumn: 0, gridRow: 0 }, layout);
    const bottom = projectChannelToIso({ gridColumn: 21, gridRow: 21 }, layout);
    const left = projectChannelToIso({ gridColumn: 0, gridRow: 21 }, layout);
    const right = projectChannelToIso({ gridColumn: 21, gridRow: 0 }, layout);

    expect(top.x).toBeCloseTo(bottom.x);
    expect(top.y).toBeLessThan(bottom.y);
    expect(left.x).toBeLessThan(layout.centerX);
    expect(right.x).toBeGreaterThan(layout.centerX);
    expect(diamondPoints({ x: 10, y: 20 }, 30, 16)).toEqual([
      { x: 10, y: 12 }, { x: 25, y: 20 }, { x: 10, y: 28 }, { x: -5, y: 20 },
    ]);
  });

  it("formats the CANDU row/column coordinate without inventing a channel", () => {
    expect(CANDU6_ROW_LABELS).toHaveLength(22);
    expect(gridCoordinateLabel({ gridColumn: 4, gridRow: 8 })).toBe("J05");
    expect(channelDisplayId(7)).toBe("CH 007");
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
