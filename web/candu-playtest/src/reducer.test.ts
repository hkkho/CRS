import { describe, expect, it } from "vitest";
import { createSyntheticFixtureBridge } from "./fixture";
import { createInitialUiState, playtestUiReducer } from "./reducer";
import type { CanduCommandResponse } from "./protocol";

describe("playtest UI reducer", () => {
  it("stores a preview and clears it after an accepted commit", async () => {
    const bridge = createSyntheticFixtureBridge();
    const initial = createInitialUiState(bridge.getSnapshot(), bridge.status);
    const previewResponse = await bridge.dispatch({
      type: "preview-refuel",
      request: {
        channelIndex: 189,
        directionId: "toward-end-b",
        shiftCount: 4,
        fuelTypeId: "NAT-U-SYNTHETIC",
      },
    });
    const withPreview = playtestUiReducer(initial, {
      type: "command-result",
      response: previewResponse,
      record: true,
    });

    expect(withPreview.preview?.request.channelIndex).toBe(189);
    expect(withPreview.history).toHaveLength(1);

    const commitResponse = await bridge.dispatch({ type: "commit-refuel", request: previewResponse.preview!.request });
    const afterCommit = playtestUiReducer(withPreview, {
      type: "command-result",
      response: commitResponse,
      record: true,
    });

    expect(afterCommit.preview).toBeNull();
    expect(afterCommit.snapshot.freshBundlesAvailable).toBe(124);
    expect(afterCommit.snapshot.refuellingOperationCount).toBe(1);
    expect(afterCommit.selectedChannelIndex).toBe(189);
    expect(afterCommit.history).toHaveLength(2);
  });

  it("ignores a channel selection outside the protocol core", () => {
    const bridge = createSyntheticFixtureBridge();
    const initial = createInitialUiState(bridge.getSnapshot(), bridge.status);
    const next = playtestUiReducer(initial, { type: "select-channel", channelIndex: 999 });
    expect(next.selectedChannelIndex).toBe(initial.selectedChannelIndex);
  });
});

// Keep the response type imported in the test module so the async bridge
// contract is exercised as a typed protocol result rather than an untyped mock.
void (undefined as CanduCommandResponse | undefined);
