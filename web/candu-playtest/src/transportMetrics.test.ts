import { createSnapshot } from "./testSnapshot";
import { describe, expect, it } from "vitest";
import {
  PROTOCOL_VERSION,
  ProtocolResyncRequiredError,
  parseProtocolResponse,
  type CanduCommand,
  type CanduSnapshot,
} from "./protocol";
import { WasmProtocolBridge } from "./bridge";

describe("compact protocol materialization and local transport metrics", () => {
  it("merges a compact patch without replacing the authoritative core", () => {
    const base = createSnapshot();
    const response = parseProtocolResponse(JSON.stringify({
      protocol: PROTOCOL_VERSION,
      operation: "dispatch",
      ok: true,
      accepted: true,
      sequence: 1,
      responseKind: "compact",
      baseSequence: 0,
      snapshotPatch: {
        ...patchFor(base),
        isPaused: true,
      },
      diagnostics: [],
      command: { type: "pause" },
      message: "paused",
    }), base);

    expect(response.snapshot.sequence).toBe(1);
    expect(response.snapshot.isPaused).toBe(true);
    expect(response.snapshot.core).toBe(base.core);
    expect(response.responseKind).toBe("compact");
  });

  it("rejects a compact response whose base sequence is not the local sequence", () => {
    const base = createSnapshot();
    expect(() => parseProtocolResponse(JSON.stringify({
      protocol: PROTOCOL_VERSION,
      operation: "dispatch",
      ok: false,
      accepted: false,
      sequence: 9,
      responseKind: "compact",
      baseSequence: 3,
      requiresResync: true,
      diagnostics: [],
      command: { type: "resume" },
      message: "resync",
    }), base)).toThrow(ProtocolResyncRequiredError);
  });

  it("records compact response size and timing in memory", async () => {
    const base = createSnapshot();
    const command: CanduCommand = { type: "pause" };
    const exports = {
      initialize: () => JSON.stringify({
        protocol: PROTOCOL_VERSION,
        operation: "initialize",
        ok: true,
        accepted: true,
        sequence: base.sequence,
        command,
        message: "ready",
        diagnostics: [],
        snapshot: base,
      }),
      getSnapshotJson: () => JSON.stringify(base),
      dispatchJson: () => JSON.stringify({
        protocol: PROTOCOL_VERSION,
        operation: "dispatch",
        ok: true,
        accepted: true,
        sequence: 1,
        responseKind: "compact",
        baseSequence: 0,
        message: "paused",
        command,
        diagnostics: [],
        snapshotPatch: {
          ...patchFor(base),
          isPaused: true,
        },
      }),
    };
    const bridge = new WasmProtocolBridge(exports);

    await bridge.initialize("play");
    const response = await bridge.dispatch(command, {
      responseMode: "compact",
      baseSequence: 0,
    });
    const metric = bridge.getTransportMetrics().at(-1);

    expect(response.snapshot.isPaused).toBe(true);
    expect(metric?.commandType).toBe("pause");
    expect(metric?.responseKind).toBe("compact");
    expect(metric?.returnedUtf8PayloadBytes).toBeGreaterThan(0);
    expect(metric?.wasmCallDurationMs).toBeGreaterThanOrEqual(0);
    expect(metric?.jsonParseMaterializationDurationMs).toBeGreaterThanOrEqual(0);
    expect(metric?.coreReplacementIncluded).toBe(false);
  });
});

function patchFor(snapshot: CanduSnapshot): Record<string, unknown> {
  return {
    scenarioId: snapshot.scenarioId,
    dataPackId: snapshot.dataPackId,
    simulationTimeSeconds: snapshot.simulationTimeSeconds,
    wallElapsedSeconds: snapshot.wallElapsedSeconds,
    normalizedPowerFraction: snapshot.normalizedPowerFraction,
    targetPowerFraction: snapshot.targetPowerFraction,
    axialTiltFraction: snapshot.axialTiltFraction,
    rrsReserveFraction: snapshot.rrsReserveFraction,
    deviceAvailableFraction: snapshot.deviceAvailableFraction,
    pendingActionCount: snapshot.pendingActionCount,
    scoreTotal: snapshot.scoreTotal,
    scoreDelta: snapshot.scoreDelta,
    isPaused: snapshot.isPaused,
    playbackModeId: snapshot.playbackModeId,
    freshBundlesAvailable: snapshot.freshBundlesAvailable,
    refuellingOperationCount: snapshot.refuellingOperationCount,
    lastRefuelledChannel: snapshot.lastRefuelledChannel,
    lastRefuellingDirectionId: snapshot.lastRefuellingDirectionId,
    lastRefuellingShiftCount: snapshot.lastRefuellingShiftCount,
    physics: snapshot.physics,
    xenon: snapshot.xenon,
    rrs: snapshot.rrs,
    diagnostics: snapshot.diagnostics,
    lastEvent: snapshot.lastEvent,
  };
}
