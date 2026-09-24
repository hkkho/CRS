import { describe, expect, it } from "vitest";
import {
  CORE_BUNDLE_POSITION_COUNT,
  CORE_CHANNEL_COUNT,
  CORE_GRID_HEIGHT,
  CORE_GRID_WIDTH,
  PROTOCOL_VERSION,
  createUnavailableRrsSnapshot,
  isProtocolSnapshot,
  parseProtocolResponse,
  parseProtocolSnapshot,
  type CanduSnapshot,
} from "./protocol";

describe("candu-playtest-v2 protocol validation", () => {
  it("requires authoritative live designer fields on every bundle", () => {
    const snapshot = createSnapshot();
    const bundle = snapshot.core.channels[0].bundles[0];
    expect(bundle.hasFuel).toBe(true);
    expect(bundle.reflectiveFaces).toEqual([]);
    expect(bundle.group1Flux).toBe(1);
    expect(bundle.group2Flux).toBe(1);

    const malformed = structuredClone(snapshot) as unknown as Record<string, unknown>;
    const core = malformed.core as { channels: Array<{ bundles: Array<Record<string, unknown>> }> };
    delete core.channels[0].bundles[0].group1Flux;
    expect(isProtocolSnapshot(malformed)).toBe(false);
    expect(() => parseProtocolSnapshot(malformed)).toThrow();
  });

  it("rejects the retired lab snapshot and response fields", () => {
    const snapshot = createSnapshot();
    const legacySnapshot = { ...snapshot, lab: {} };
    expect(isProtocolSnapshot(legacySnapshot)).toBe(false);

    const legacyResponse = {
      ...responseEnvelope(snapshot, snapshot.sequence),
      lab: {},
    };
    expect(() => parseProtocolResponse(JSON.stringify(legacyResponse))).toThrow();
  });

  it("preserves a valid full snapshot and compact response", () => {
    const base = createSnapshot();

    expect(isProtocolSnapshot(base)).toBe(true);
    expect(parseProtocolSnapshot(JSON.stringify(base))).toEqual(base);

    const patch = patchFor(base);
    patch.isPaused = true;
    patch.playbackModeId = "pause";
    const response = parseProtocolResponse(JSON.stringify({
      ...responseEnvelope(base, 1),
      responseKind: "compact",
      baseSequence: base.sequence,
      snapshotPatch: patch,
    }), base);

    expect(response.snapshot.sequence).toBe(1);
    expect(response.snapshot.isPaused).toBe(true);
    expect(response.snapshot.playbackModeId).toBe("pause");
    expect(response.snapshot.core).toBe(base.core);
    expect(response.responseKind).toBe("compact");
  });

  it("normalizes initialize command metadata to reset when null or omitted", () => {
    const snapshot = createSnapshot();
    const response: Record<string, unknown> = {
      ...responseEnvelope(snapshot, snapshot.sequence),
      operation: "initialize",
      command: null,
    };

    expect(parseProtocolResponse(JSON.stringify(response)).command).toEqual({ type: "reset" });

    delete response.command;
    expect(parseProtocolResponse(JSON.stringify(response)).command).toEqual({ type: "reset" });
  });

  it("normalizes an omitted initial refuelling direction to null", () => {
    const snapshot = createSnapshot();
    const omittedSnapshot = structuredClone(snapshot) as unknown as Record<string, unknown>;
    delete omittedSnapshot.lastRefuellingDirectionId;

    expect(parseProtocolSnapshot(omittedSnapshot).lastRefuellingDirectionId).toBeNull();

    const response: Record<string, unknown> = {
      ...responseEnvelope(snapshot, snapshot.sequence),
      operation: "initialize",
      command: null,
      snapshot: omittedSnapshot,
    };
    expect(parseProtocolResponse(JSON.stringify(response)).snapshot.lastRefuellingDirectionId).toBeNull();
  });

  it.each(["physics", "xenon"] as const)("rejects an empty %s snapshot", (field) => {
    const malformed = structuredClone(createSnapshot()) as unknown as Record<string, unknown>;
    malformed[field] = {};

    expect(isProtocolSnapshot(malformed)).toBe(false);
    expect(() => parseProtocolSnapshot(malformed)).toThrow();
  });

  it.each([
    ["absent required scalar", (snapshot: Record<string, unknown>) => { delete snapshot.scoreTotal; }],
    ["nonfinite direct-object value", (snapshot: Record<string, unknown>) => { snapshot.simulationTimeSeconds = Number.POSITIVE_INFINITY; }],
    ["invalid playback mode", (snapshot: Record<string, unknown>) => { snapshot.playbackModeId = "warp"; }],
    ["invalid refuelling direction", (snapshot: Record<string, unknown>) => { snapshot.lastRefuellingDirectionId = "sideways"; }],
    ["omitted post-refuelling direction", (snapshot: Record<string, unknown>) => {
      delete snapshot.lastRefuellingDirectionId;
      snapshot.refuellingOperationCount = 1;
    }],
    ["invalid refuelling count", (snapshot: Record<string, unknown>) => { snapshot.lastRefuellingShiftCount = 6; }],
    ["malformed nested UI value", (snapshot: Record<string, unknown>) => {
      (snapshot.physics as Record<string, unknown>).actualPowerFraction = "not-a-number";
    }],
  ])("rejects malformed full snapshots: %s", (_name, mutate) => {
    const malformed = structuredClone(createSnapshot()) as unknown as Record<string, unknown>;
    mutate(malformed);

    expect(isProtocolSnapshot(malformed)).toBe(false);
    expect(() => parseProtocolSnapshot(malformed)).toThrow();
  });

  it.each([
    ["absent required patch scalar", (patch: Record<string, unknown>) => { delete patch.normalizedPowerFraction; }],
    ["nonfinite patch value", (patch: Record<string, unknown>) => { patch.scoreTotal = Number.NaN; }],
    ["invalid patch playback mode", (patch: Record<string, unknown>) => { patch.playbackModeId = "warp"; }],
    ["invalid patch refuelling direction", (patch: Record<string, unknown>) => { patch.lastRefuellingDirectionId = "sideways"; }],
    ["invalid patch refuelling count", (patch: Record<string, unknown>) => { patch.lastRefuellingShiftCount = 6; }],
    ["malformed patch UI value", (patch: Record<string, unknown>) => {
      (patch.physics as Record<string, unknown>).solverResidualRelativeInfinity = Number.NaN;
    }],
  ])("rejects malformed compact patches: %s", (_name, mutate) => {
    const base = createSnapshot();
    const patch = patchFor(base);
    mutate(patch);

    expect(() => parseProtocolResponse(JSON.stringify({
      ...responseEnvelope(base, 1),
      responseKind: "compact",
      baseSequence: base.sequence,
      snapshotPatch: patch,
    }), base)).toThrow();
  });

  it.each([
    ["accepted", (response: Record<string, unknown>) => { response.accepted = "true"; }],
    ["sequence", (response: Record<string, unknown>) => { response.sequence = Number.NaN; }],
    ["command", (response: Record<string, unknown>) => { response.command = { type: "unknown" }; }],
    ["null dispatch command", (response: Record<string, unknown>) => {
      response.operation = "dispatch";
      response.command = null;
    }],
    ["message", (response: Record<string, unknown>) => { response.message = 42; }],
  ])("rejects a malformed response envelope: %s", (_name, mutate) => {
    const snapshot = createSnapshot();
    const response = responseEnvelope(snapshot, snapshot.sequence);
    response.snapshot = snapshot;
    mutate(response);

    expect(() => parseProtocolResponse(JSON.stringify(response))).toThrow();
  });
});

function responseEnvelope(snapshot: CanduSnapshot, sequence: number): Record<string, unknown> {
  return {
    protocol: PROTOCOL_VERSION,
    accepted: true,
    sequence,
    command: { type: "pause" },
    message: "paused",
    diagnostics: [],
    snapshot,
  };
}

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
  const channels = Array.from({ length: CORE_CHANNEL_COUNT }, (_, channelIndex) => ({
    channelIndex,
    gridColumn: channelIndex % CORE_GRID_WIDTH,
    gridRow: Math.floor(channelIndex / CORE_GRID_WIDTH),
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
    bundles: Array.from({ length: CORE_BUNDLE_POSITION_COUNT }, (_, position) => ({
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
    scenarioId: "protocol-test",
    dataPackId: "protocol-test-pack",
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
      sourceId: "protocol-test",
      formulationId: "protocol-test",
      shapeMethodId: "protocol-test",
      amplitudeMethodId: "protocol-test",
      reactivityMethodId: "protocol-test",
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
      reactivityIdentity: "protocol-test",
      reactivityBindingDigestHex: "",
      coreReactivity: 0,
      compensatedNetReactivity: 0,
      compensationState: 0,
      compensationCommand: 0,
      compensationLowerBound: -0.25,
      compensationUpperBound: 0.25,
      compensationSaturated: false,
      compensationResponseTimeSeconds: 4,
      cadenceIdentity: "protocol-test",
      powerBalanceRelativeError: 0,
      solverIdentity: "protocol-test",
      solverIterationCount: 1,
      solverResidualRelativeInfinity: 0,
    },
    xenon: {
      stateIdentity: "protocol-test",
      stateDigestHex: "",
      stateVersion: 0,
      simulationTimeSeconds: 0,
      nodeCount: 0,
      couplingIdentity: "protocol-test",
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
    rrs: {
      ...createUnavailableRrsSnapshot(),
      zones: Array.from({ length: 14 }, (_, logicalZoneId) => ({
        logicalZoneId,
        fillFraction: 0.5,
        referencePowerFraction: 1,
        targetPowerFraction: 1,
        measuredPowerFraction: 1,
        shapeError: 0,
      })),
    },
    core: {
      channelCount: CORE_CHANNEL_COUNT,
      bundlePositionCount: CORE_BUNDLE_POSITION_COUNT,
      gridWidth: CORE_GRID_WIDTH,
      gridHeight: CORE_GRID_HEIGHT,
      channels,
    },
    diagnostics: {
      convergence: {
        state: "converged",
        iterations: 1,
        residual: 0,
        relativePowerError: 0,
        lastSolveMilliseconds: 0,
        solverLabel: "protocol-test",
      },
      checks: [],
    },
    lastEvent: null,
  };
}
