import { describe, expect, it } from "vitest";
import { createSyntheticFixtureBridge } from "./fixture";
import {
  isProtocolSnapshot,
  parseProtocolSnapshot,
  parseReplayArchive,
  PROTOCOL_VERSION,
  type BridgeStatus,
  type CanduSnapshot,
} from "./protocol";
import {
  createInitialUiState,
  isBridgeInteractive,
  playtestUiReducer,
} from "./reducer";
import {
  createReplayArchive,
  loadReplayArchive,
  REPLAY_STORAGE_KEY,
  saveReplayArchive,
  serializeReplayForDownload,
} from "./replay";

describe("browser playtest UI seam", () => {
  it("WEB-UI-001: tracks channel selection, refuel preview/commit, clearing, and command history", async () => {
    const bridge = createSyntheticFixtureBridge();
    const initial = createInitialUiState(bridge.getSnapshot(), bridge.status);
    const selected = playtestUiReducer(initial, { type: "select-channel", channelIndex: 210 });
    expect(selected.selectedChannelIndex).toBe(210);

    const request = {
      channelIndex: 210,
      directionId: "toward-end-b" as const,
      shiftCount: 4 as const,
      fuelTypeId: "NAT-U-SYNTHETIC",
    };
    const previewResponse = await bridge.dispatch({ type: "preview-refuel", request });
    const withPreview = playtestUiReducer(selected, {
      type: "command-result",
      response: previewResponse,
      record: true,
    });

    expect(withPreview.preview).toMatchObject({ request });
    expect(withPreview.history).toHaveLength(1);
    expect(withPreview.history[0].command).toEqual({ type: "preview-refuel", request });

    const clearedPreview = playtestUiReducer(withPreview, { type: "clear-preview" });
    expect(clearedPreview.preview).toBeNull();
    expect(clearedPreview.history).toHaveLength(1);

    const commitResponse = await bridge.dispatch({ type: "commit-refuel", request });
    const committed = playtestUiReducer(clearedPreview, {
      type: "command-result",
      response: commitResponse,
      record: true,
    });

    expect(committed.preview).toBeNull();
    expect(committed.selectedChannelIndex).toBe(210);
    expect(committed.snapshot.refuellingOperationCount).toBe(1);
    expect(committed.history).toHaveLength(2);
    expect(committed.history[1].command).toEqual({ type: "commit-refuel", request });

    const clearedHistory = playtestUiReducer(committed, { type: "clear-history" });
    expect(clearedHistory.history).toEqual([]);
    expect(clearedHistory.snapshot).toBe(committed.snapshot);
  });

  it("WEB-UI-002: keeps valid state when selection or an incoming snapshot is invalid", () => {
    const bridge = createSyntheticFixtureBridge();
    const initial = createInitialUiState(bridge.getSnapshot(), bridge.status);
    const selected = playtestUiReducer(initial, { type: "select-channel", channelIndex: 210 });

    expect(playtestUiReducer(selected, { type: "select-channel", channelIndex: 380 })).toBe(selected);
    expect(playtestUiReducer(selected, { type: "select-channel", channelIndex: -1 })).toBe(selected);

    const malformedSnapshot = {
      ...selected.snapshot,
      core: {
        ...selected.snapshot.core,
        channels: selected.snapshot.core.channels.slice(0, -1),
      },
    } as CanduSnapshot;
    expect(isProtocolSnapshot(malformedSnapshot)).toBe(false);
    expect(() => parseProtocolSnapshot(malformedSnapshot)).toThrow(/malformed or incomplete/);

    const afterMalformedSnapshot = playtestUiReducer(selected, {
      type: "bridge-state",
      snapshot: malformedSnapshot,
      bridgeStatus: selected.bridgeStatus,
    });
    expect(afterMalformedSnapshot.snapshot).toBe(selected.snapshot);
    expect(afterMalformedSnapshot.selectedChannelIndex).toBe(210);
  });

  it("WEB-UI-003: disables interaction for an unavailable authoritative bridge and keeps the fixture non-authoritative", () => {
    const fixture = createSyntheticFixtureBridge();
    const unavailableStatus: BridgeStatus = {
      source: "unavailable",
      title: "AUTHORITATIVE WASM UNAVAILABLE",
      detail: "Authoritative WASM bridge unavailable",
      isWasmAvailable: false,
      capabilities: [],
    };

    const simulationControlsDisabled = !isBridgeInteractive(unavailableStatus);
    expect(simulationControlsDisabled).toBe(true);
    expect(unavailableStatus.title).toContain("AUTHORITATIVE WASM UNAVAILABLE");
    expect(fixture.status.source).toBe("synthetic-fixture");
    expect(fixture.status.isWasmAvailable).toBe(false);
    expect(isBridgeInteractive(fixture.status)).toBe(false);
    expect(fixture.getSnapshot().source).toBe("synthetic-fixture");
    expect(fixture.getSnapshot().physics.isAuthoritative).toBe(false);

    expect(
      isBridgeInteractive({
        ...unavailableStatus,
        source: "wasm",
        isWasmAvailable: true,
      }),
    ).toBe(true);
  });

  it("WEB-UI-004: round-trips local replay archives and rejects malformed or wrong-version input", async () => {
    const bridge = createSyntheticFixtureBridge();
    let state = createInitialUiState(bridge.getSnapshot(), bridge.status);
    for (const command of [
      { type: "pause" as const },
      { type: "step" as const, simulationSeconds: 3_600 },
    ]) {
      const response = await bridge.dispatch(command);
      state = playtestUiReducer(state, { type: "command-result", response, record: true });
    }

    const archive = createReplayArchive(state.history, "synthetic-fixture", "2026-01-01T00:00:00.000Z");
    const storage = createMemoryStorage();
    expect(saveReplayArchive(archive, storage)).toBe(true);
    expect(loadReplayArchive(storage)).toEqual(archive);
    expect(parseReplayArchive(serializeReplayForDownload(archive).trim())).toEqual(archive);
    expect(archive.protocol).toBe(PROTOCOL_VERSION);
    expect(archive.commands).toEqual(state.history.map(({ recordedAt: _recordedAt, ...record }) => record));

    storage.setItem(REPLAY_STORAGE_KEY, JSON.stringify({ protocol: PROTOCOL_VERSION, kind: "command-replay" }));
    expect(() => loadReplayArchive(storage)).toThrow(/command replay archive/);

    storage.setItem(
      REPLAY_STORAGE_KEY,
      JSON.stringify({ ...archive, protocol: "candu-playtest-v0" }),
    );
    expect(() => loadReplayArchive(storage)).toThrow(/candu-playtest-v1/);
  });
});

function createMemoryStorage(): Storage {
  const values = new Map<string, string>();
  return {
    getItem: (key: string) => values.get(key) ?? null,
    setItem: (key: string, value: string) => values.set(key, value),
  } as unknown as Storage;
}
