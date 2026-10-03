import type { BridgeSessionController } from "../sessionController";
import type { RefuelDraft } from "../commandState";
import type { StudioTab } from "./HistoryView";
export interface StudioNavigation {
  selectedChannelIndex?: number;
  refuelDraft?: RefuelDraft;
  selectedTab?: StudioTab;
  mapMode?: "power" | "burnup";
}
export type StudioSession = Pick<BridgeSessionController, "snapshot" | "status" | "isPending" | "subscribe" | "dispatch" | "history"> &
  Partial<Pick<BridgeSessionController, "presentation">>;
export type WorkspaceSession = StudioSession & Pick<BridgeSessionController, "startShift" | "stopShift">;
export interface DesignerNavigation {
  returnChannelIndex?: number;
  studioNavigation?: StudioNavigation;
  onReturn?: (state: StudioNavigation) => void;
}
