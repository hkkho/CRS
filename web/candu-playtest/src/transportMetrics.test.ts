import { describe, expect, it } from "vitest";
import {
  PROTOCOL_VERSION,
  ProtocolResyncRequiredError,
  createUnavailableRrsSnapshot,
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

function createSnapshot(): CanduSnapshot {
  const channels = Array.from({ length: 380 }, (_, channelIndex) => ({
    channelIndex,
    gridColumn: channelIndex % 22,
    gridRow: Math.floor(channelIndex / 22),
    flowDirection: "toward-end-a" as const,
    averageBurnupMwdPerKg: 0,
    powerWatts: 1,
    localPowerFraction: 1,
    localTiltFraction: 0,
    xenon: {
      channelIndex,
      meanI135NumberDensityM3: 0,
      maxI135NumberDensityM3: 0,
      meanXe135NumberDensityM3: 0,
      maxXe135NumberDensityM3: 0,
      meanDynamicAbsorptionGroup1PerM: 0,
      maxDynamicAbsorptionGroup1PerM: 0,
      meanDynamicAbsorptionGroup2PerM: 0,
      maxDynamicAbsorptionGroup2PerM: 0,
    },
    bundles: Array.from({ length: 12 }, (_, position) => ({
      position,
      bundleId: `bundle-${channelIndex}-${position}`,
      fuelTypeId: "NAT-U-SYNTHETIC",
      currentBurnupMwdPerKg: 0,
      powerWatts: 1,
      localPowerFraction: 1,
      insertedAtSeconds: 0,
      stateVersion: 0,
      isFresh: true,
      hasFuel: true,
      reflectiveFaces: [],
      group1Flux: 1,
      group2Flux: 1,
    })),
  }));

  return {
    protocol: PROTOCOL_VERSION,
    source: "wasm",
    sequence: 0,
    scenarioId: "test",
    dataPackId: "test",
    simulationTimeSeconds: 0,
    wallElapsedSeconds: 0,
    normalizedPowerFraction: 1,
    targetPowerFraction: 1,
    axialTiltFraction: 0,
    rrsReserveFraction: 1,
    deviceAvailableFraction: 1,
    pendingActionCount: 0,
    scoreTotal: 0,
    scoreDelta: 0,
    isPaused: false,
    playbackModeId: "1x",
    freshBundlesAvailable: 8,
    refuellingOperationCount: 0,
    lastRefuelledChannel: -1,
    lastRefuellingDirectionId: null,
    lastRefuellingShiftCount: 0,
    physics: {
      sourceId: "transport-test",
      formulationId: "transport-test",
      shapeMethodId: "transport-test",
      amplitudeMethodId: "transport-test",
      reactivityMethodId: "transport-test",
      solveState: "converged",
      isAuthoritative: true,
      bindingVersion: 0,
      referencePowerWatts: 1,
      powerAmplitude: 1,
      actualPowerFraction: 1,
      targetPowerWatts: 1,
      totalPowerWatts: 1,
      meanChannelPowerWatts: 1,
      meanBundlePowerWatts: 1,
      effectiveK: 1,
      reactivity: 0,
      weightedPerturbationReactivity: 0,
      reactivityNumerator: 0,
      reactivityDenominator: 1,
      reactivityIdentity: "transport-test",
      reactivityBindingDigestHex: "",
      coreReactivity: 0,
      compensatedNetReactivity: 0,
      compensationState: 0,
      compensationCommand: 0,
      compensationLowerBound: -0.25,
      compensationUpperBound: 0.25,
      compensationSaturated: false,
      compensationResponseTimeSeconds: 4,
      cadenceIdentity: "transport-test",
      powerBalanceRelativeError: 0,
      solverIdentity: "transport-test",
      solverIterationCount: 1,
      solverResidualRelativeInfinity: 0,
    },
    xenon: {
      stateIdentity: "transport-test",
      stateDigestHex: "",
      stateVersion: 0,
      simulationTimeSeconds: 0,
      nodeCount: 0,
      couplingIdentity: "transport-test",
      hasCoupling: false,
      baseCoefficientDigestHex: "",
      dynamicXenonDigestHex: "",
      effectiveCoefficientDigestHex: "",
      meanI135NumberDensityM3: 0,
      maxI135NumberDensityM3: 0,
      meanXe135NumberDensityM3: 0,
      maxXe135NumberDensityM3: 0,
      meanDynamicAbsorptionGroup1PerM: 0,
      maxDynamicAbsorptionGroup1PerM: 0,
      meanDynamicAbsorptionGroup2PerM: 0,
      maxDynamicAbsorptionGroup2PerM: 0,
      selectedChannelIndex: -1,
      selectedChannel: null,
    },
    rrs: createUnavailableRrsSnapshot(),
    core: {
      channelCount: 380,
      bundlePositionCount: 12,
      gridWidth: 22,
      gridHeight: 22,
      channels,
    },
    diagnostics: {
      convergence: {
        state: "converged",
        iterations: 1,
        residual: 0,
        relativePowerError: 0,
        lastSolveMilliseconds: 0,
        solverLabel: "test",
      },
      checks: [],
    },
    lastEvent: null,
  };
}
