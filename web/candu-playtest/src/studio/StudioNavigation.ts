import type { BridgeSessionController } from "../sessionController";
import type { RefuelDraft } from "../commandState";
import type { StudioTab } from "./HistoryView";
import type { MapMode } from "./powerReadings";
export interface StudioNavigation {
  selectedChannelIndex?: number;
  refuelDraft?: RefuelDraft;
  selectedTab?: StudioTab;
  mapMode?: MapMode;
}
export type StudioSession = Pick<BridgeSessionController, "snapshot" | "status" | "isPending" | "subscribe" | "dispatch" | "history"> &
  Partial<Pick<BridgeSessionController, "presentation" | "dailyPlan" | "setDailyPlan">>;
export type WorkspaceSession = StudioSession & Pick<BridgeSessionController, "startShift" | "stopShift">;
