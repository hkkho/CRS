import { describe, expect, it } from "vitest";
import {
  CANDU6_ROW_LABELS,
  findAdjacentChannelIndex,
  gridCoordinateLabel,
} from "./channelCoordinates";

describe("Channel coordinates and keyboard navigation", () => {
  it("formats the CANDU row/column coordinate without inventing a channel", () => {
    expect(CANDU6_ROW_LABELS).toHaveLength(22);
    expect(gridCoordinateLabel({ gridColumn: 4, gridRow: 8 })).toBe("J05");
    expect(gridCoordinateLabel({ gridColumn: 4, gridRow: 10 })).toBe("L05");
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
