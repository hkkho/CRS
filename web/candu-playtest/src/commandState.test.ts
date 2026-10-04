import { describe, expect, it } from "vitest";
import {
  canIssueRefuel,
  createRefuelDraft,
  formatRefuelDirection,
  toRefuelRequest,
} from "./commandState";

describe("Refuelling draft state", () => {
  const channel = { channelIndex: 210, flowDirection: "toward-end-b" as const };

  it("creates a playable order from the selected channel", () => {
    const draft = createRefuelDraft(channel);
    expect(toRefuelRequest(draft)).toEqual({
      channelIndex: 210,
      directionId: "toward-end-b",
      shiftCount: 8,
      fuelTypeId: "NAT-U-SYNTHETIC",
    });
    expect(formatRefuelDirection(draft.directionId)).toContain("END A");
  });

  it("allows a direct order when the selected shift is affordable", () => {
    const draft = createRefuelDraft(channel);

    expect(canIssueRefuel(draft, 8, false)).toBe(true);
    expect(canIssueRefuel(draft, 0, false, true)).toBe(true);
    expect(canIssueRefuel(draft, 0, true, true)).toBe(false);
    expect(canIssueRefuel(null, 0, false, true)).toBe(false);
    expect(canIssueRefuel({ ...draft, shiftCount: 8 }, 8, false)).toBe(true);
    expect(canIssueRefuel({ ...draft, shiftCount: 8 }, 4, false)).toBe(false);
    expect(canIssueRefuel(draft, 8, true)).toBe(false);
    expect(canIssueRefuel(null, 8, false)).toBe(false);
  });

});
