import type { CanduChannelSnapshot, RefuelRequest } from "./protocol";

export interface RefuelDraft {
  channelIndex: number;
  directionId: RefuelRequest["directionId"];
  shiftCount: RefuelRequest["shiftCount"];
  fuelTypeId: string;
}

export function createRefuelDraft(channel: Pick<CanduChannelSnapshot, "channelIndex" | "flowDirection">): RefuelDraft {
  return {
    channelIndex: channel.channelIndex,
    directionId: channel.flowDirection,
    shiftCount: 8,
    fuelTypeId: "NAT-U-SYNTHETIC",
  };
}

export function toRefuelRequest(draft: RefuelDraft): RefuelRequest {
  return { ...draft };
}

export function canIssueRefuel(
  draft: RefuelDraft | null,
  freshBundlesAvailable: number,
  commandPending: boolean,
): draft is RefuelDraft {
  return draft !== null && !commandPending && freshBundlesAvailable >= draft.shiftCount;
}

export function formatRefuelDirection(direction: RefuelRequest["directionId"]): string {
  return direction === "toward-end-b" ? "END A  →  END B" : "END B  →  END A";
}
