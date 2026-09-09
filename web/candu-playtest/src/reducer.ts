import type {
  BridgeStatus,
  CanduCommand,
  CanduCommandResponse,
  CanduReplayArchive,
  CanduSnapshot,
  RefuelPreview,
} from "./protocol";
import { isProtocolSnapshot } from "./protocol";

export type ConsoleMode = "play" | "lab";
export type CoreViewMode = "engine2d" | "grid";

export interface UiCommandHistoryEntry {
  index: number;
  command: CanduCommand;
  accepted: boolean;
  message: string;
  snapshotSequence: number;
  simulationTimeSeconds: number;
  recordedAt: string;
}

export interface PlaytestUiState {
  mode: ConsoleMode;
  coreViewMode: CoreViewMode;
  selectedChannelIndex: number;
  snapshot: CanduSnapshot;
  bridgeStatus: BridgeStatus;
  preview: RefuelPreview | null;
  lastResponse: CanduCommandResponse | null;
  history: UiCommandHistoryEntry[];
  replayStatus: string;
}

export type UiAction =
  | { type: "select-channel"; channelIndex: number }
  | { type: "set-mode"; mode: ConsoleMode }
  | { type: "bridge-state"; snapshot: CanduSnapshot; bridgeStatus: BridgeStatus }
  | { type: "set-core-view"; viewMode: CoreViewMode }
  | { type: "command-result"; response: CanduCommandResponse; record: boolean }
  | { type: "clear-preview" }
  | { type: "set-replay-status"; status: string }
  | { type: "clear-history" };

export function createInitialUiState(snapshot: CanduSnapshot, bridgeStatus: BridgeStatus): PlaytestUiState {
  const selectedChannelIndex = snapshot.core.channels.length === 0
    ? -1
    : snapshot.lastRefuelledChannel >= 0
      ? snapshot.lastRefuelledChannel
      : Math.min(189, snapshot.core.channels.length - 1);
  return {
    mode: "play",
    coreViewMode: "engine2d",
    selectedChannelIndex,
    snapshot,
    bridgeStatus,
    preview: null,
    lastResponse: null,
    history: [],
    replayStatus: "No replay saved in this session.",
  };
}

export function playtestUiReducer(state: PlaytestUiState, action: UiAction): PlaytestUiState {
  switch (action.type) {
    case "select-channel":
      if (action.channelIndex < 0 || action.channelIndex >= state.snapshot.core.channels.length) {
        return state;
      }
      return { ...state, selectedChannelIndex: action.channelIndex };
    case "set-mode":
      return {
        ...state,
        mode: action.mode,
        preview: null,
        lastResponse: null,
        history: [],
        replayStatus: "Mode changed; command history reset for the new bridge session.",
      };
    case "bridge-state":
      if (!isProtocolSnapshot(action.snapshot)) {
        return { ...state, bridgeStatus: action.bridgeStatus };
      }
      return {
        ...state,
        snapshot: action.snapshot,
        bridgeStatus: action.bridgeStatus,
        preview: null,
        lastResponse: null,
        selectedChannelIndex:
          action.snapshot.lastRefuelledChannel >= 0
            ? action.snapshot.lastRefuelledChannel
            : Math.min(state.selectedChannelIndex, action.snapshot.core.channels.length - 1),
      };
    case "set-core-view":
      return { ...state, coreViewMode: action.viewMode };
    case "clear-preview":
      return { ...state, preview: null };
    case "set-replay-status":
      return { ...state, replayStatus: action.status };
    case "clear-history":
      return { ...state, history: [], replayStatus: "Command history cleared." };
    case "command-result":
      return applyCommandResult(state, action.response, action.record);
  }
}

function applyCommandResult(
  state: PlaytestUiState,
  response: CanduCommandResponse,
  record: boolean,
): PlaytestUiState {
  if (!isProtocolSnapshot(response.snapshot)) {
    return state;
  }

  const history = record
    ? [
        ...state.history,
        {
          index: state.history.length + 1,
          command: response.command,
          accepted: response.accepted,
          message: response.message,
          snapshotSequence: response.snapshot.sequence,
          simulationTimeSeconds: response.snapshot.simulationTimeSeconds,
          recordedAt: new Date().toISOString(),
        },
      ]
    : state.history;

  const isPreviewResponse = response.command.type === "preview-refuel";
  const isCommitResponse = response.command.type === "commit-refuel";
  const preview = isPreviewResponse && response.accepted
    ? response.preview
    : isCommitResponse && response.accepted
      ? null
      : state.preview;

  return {
    ...state,
    snapshot: response.snapshot,
    lastResponse: response,
    preview,
    history,
    selectedChannelIndex:
      response.snapshot.lastRefuelledChannel >= 0
        ? response.snapshot.lastRefuelledChannel
        : state.selectedChannelIndex,
  };
}

export function isBridgeInteractive(bridgeStatus: BridgeStatus): boolean {
  return bridgeStatus.source === "wasm" && bridgeStatus.isWasmAvailable;
}

export function replayRecordsFromUiState(state: PlaytestUiState): CanduReplayArchive["commands"] {
  return state.history.map(({ recordedAt: _recordedAt, ...record }) => record);
}
