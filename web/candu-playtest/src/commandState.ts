import type { CanduChannelSnapshot, RefuelRequest } from "./protocol";
import { clamp } from "./protocol";

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
    shiftCount: 4,
    fuelTypeId: "NAT-U-SYNTHETIC",
  };
}

export function toRefuelRequest(draft: RefuelDraft): RefuelRequest {
  return { ...draft };
}

export function toggleRefuelDirection(direction: RefuelRequest["directionId"]): RefuelRequest["directionId"] {
  return direction === "toward-end-a" ? "toward-end-b" : "toward-end-a";
}

export function toggleShiftCount(shiftCount: RefuelRequest["shiftCount"]): RefuelRequest["shiftCount"] {
  return shiftCount === 4 ? 8 : 4;
}

export function canIssueRefuel(
  draft: RefuelDraft | null,
  freshBundlesAvailable: number,
  commandPending: boolean,
): draft is RefuelDraft {
  return draft !== null && !commandPending && freshBundlesAvailable >= draft.shiftCount;
}

export function adjustTarget(value: number, delta: number, minimum: number, maximum: number): number {
  return clamp(Number((value + delta).toFixed(3)), minimum, maximum);
}

export function formatRefuelDirection(direction: RefuelRequest["directionId"]): string {
  return direction === "toward-end-b" ? "END A  →  END B" : "END B  →  END A";
}
