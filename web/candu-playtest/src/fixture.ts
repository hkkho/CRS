import {
  BASE_CLOCK_SIMULATION_SECONDS_PER_WALL_SECOND,
  clamp,
  CORE_BUNDLE_POSITION_COUNT,
  CORE_CHANNEL_COUNT,
  CORE_GRID_HEIGHT,
  CORE_GRID_WIDTH,
  type CanduChannelSnapshot,
  type CanduCommand,
  type CanduCommandResponse,
  type CanduConvergenceStatus,
  type CanduDiagnostics,
  type CanduEvent,
  type CanduPhysicsSnapshot,
  type CanduPlaytestBridge,
  type CanduReplayArchive,
  type CanduSnapshot,
  type RefuelPreview,
  type RefuelRequest,
  type PlaybackModeId,
  PROTOCOL_VERSION,
} from "./protocol";
import type { BridgeStatus } from "./protocol";

const HOURS_TO_SECONDS = 60 * 60;
const REFERENCE_POWER_WATTS = 1_000_000_000;
const BUNDLE_HEAVY_METAL_MASS_KG = 19.2;
const JOULES_PER_MW_DAY_PER_KG = 8.64e10;
const PRACTICE_REGULATOR_CADENCE_ID = "deterministic-regulated-steady-state-long-step-v1";
const PRACTICE_REGULATOR_LOWER_BOUND = -0.25;
const PRACTICE_REGULATOR_UPPER_BOUND = 0.25;
const PRACTICE_REGULATOR_RESPONSE_TIME_SECONDS = 4;
const PLAYBACK_FACTORS: Record<PlaybackModeId, number> = {
  pause: 0,
  "1x": BASE_CLOCK_SIMULATION_SECONDS_PER_WALL_SECOND,
  "10x": BASE_CLOCK_SIMULATION_SECONDS_PER_WALL_SECOND * 10,
  "60x": BASE_CLOCK_SIMULATION_SECONDS_PER_WALL_SECOND * 60,
};
const ROW_LENGTHS = [
  6,
  12,
  14,
  16,
  18,
  18,
  20,
  20,
  22,
  22,
  22,
  22,
  22,
  22,
  20,
  20,
  18,
  18,
  16,
  14,
  12,
  6,
] as const;

interface FixtureChannel {
  channelIndex: number;
  gridColumn: number;
  gridRow: number;
  flowDirection: RefuelRequest["directionId"];
  averageBurnupMwdPerKg: number;
  bundles: CanduChannelSnapshot["bundles"];
}

interface FixturePowerProjection {
  referencePowerWatts: number;
  powerAmplitude: number;
  actualPowerFraction: number;
  targetPowerWatts: number;
  totalPowerWatts: number;
  meanChannelPowerWatts: number;
  meanBundlePowerWatts: number;
  effectiveK: number;
  reactivity: number;
  staticReactivity: number;
  staticReactivityMethodId: string;
  weightedPerturbationReactivity: number;
  reactivityNumerator: number;
  reactivityDenominator: number;
  reactivityIdentity: string;
  reactivityBindingDigestHex: string;
  coreReactivity: number;
  compensatedNetReactivity: number;
  compensationState: number;
  compensationCommand: number;
  compensationLowerBound: number;
  compensationUpperBound: number;
  compensationSaturated: boolean;
  compensationResponseTimeSeconds: number;
  cadenceIdentity: string;
  powerBalanceRelativeError: number;
  channelPowerWatts: number[];
  channelTiltFractions: number[];
  bundlePowerWatts: Map<string, number>;
}

interface FixtureState {
  sequence: number;
  simulationTimeSeconds: number;
  wallElapsedSeconds: number;
  normalizedPowerFraction: number;
  targetPowerFraction: number;
  absoluteTiltFraction: number;
  targetTiltFraction: number;
  controlMarginFraction: number;
  deviceAvailableFraction: number;
  pendingActionCount: number;
  scoreTotal: number;
  scoreDelta: number;
  isPaused: boolean;
  playbackModeId: PlaybackModeId;
  freshBundlesAvailable: number;
  refuellingOperationCount: number;
  lastRefuelledChannel: number;
  lastRefuellingDirectionId: RefuelRequest["directionId"] | null;
  lastRefuellingShiftCount: number;
  nextFreshBundleSequence: number;
  channels: FixtureChannel[];
  lastEvent: CanduEvent | null;
}

const fixtureStatus: BridgeStatus = {
  source: "synthetic-fixture",
  title: "SYNTHETIC FIXTURE",
  detail: "Deterministic compatibility data · WASM not detected",
  isWasmAvailable: false,
  capabilities: ["380-channel CANDU-6 face", "1 h / 2 s base clock", "refuelling preview", "replay-safe commands"],
};

/**
 * Test-only compatibility behavior for Milestone 0.5. This is deliberately not
 * an authoritative reactor simulator: it supplies stable protocol-shaped
 * state transitions and a matching reduced projection for frontend tests. It
 * is not wired into the application bridge.
 */
export function createSyntheticFixtureBridge(): CanduPlaytestBridge {
  let state = createInitialState();

  return {
    status: fixtureStatus,
    getSnapshot: () => createSnapshot(state),
    dispatch: (command) => {
      if (command.type === "reset") {
        state = createInitialState();
        setEvent(state, "Run reset", "Practice state restored to deterministic seed 1001.", "info");
        return accept(state, command, "Practice run reset.");
      }

      return dispatchFixtureCommand(state, command);
    },
  };
}

function dispatchFixtureCommand(state: FixtureState, command: CanduCommand): CanduCommandResponse {
  switch (command.type) {
    case "advance":
      return advance(state, command);
    case "step":
      return step(state, command);
    case "set-playback-mode":
      return setPlaybackMode(state, command);
    case "pause":
      state.isPaused = true;
      state.playbackModeId = "pause";
      setEvent(state, "Simulation paused", "Time is held at the current reactor state.", "info");
      return accept(state, command, "Simulation paused.");
    case "resume":
      state.isPaused = false;
      if (state.playbackModeId === "pause") {
        state.playbackModeId = "1x";
      }
      setEvent(state, "Simulation resumed", `${state.playbackModeId} playback is active.`, "positive");
      return accept(state, command, `Simulation resumed at ${state.playbackModeId}.`);
    case "queue-power-target":
      return queuePowerTarget(state, command);
    case "queue-tilt-target":
      return queueTiltTarget(state, command);
    case "preview-refuel":
      return previewRefuel(state, command);
    case "commit-refuel":
      return commitRefuel(state, command);
    default:
      return reject(state, command, "Unsupported fixture command.", "fixture.command.unsupported");
  }
}

function createInitialState(): FixtureState {
  const positions = createGridPositions();
  const channels: FixtureChannel[] = [];
  let bundleSequence = 1;

  for (let channelIndex = 0; channelIndex < CORE_CHANNEL_COUNT; channelIndex += 1) {
    const position = positions[channelIndex];
    const normalizedX = (position.column - 10.5) / 11.5;
    const normalizedY = (position.row - 10.5) / 11.5;
    const radialDistance = Math.sqrt(normalizedX ** 2 + normalizedY ** 2);
    const radialShape = clamp(1 - radialDistance ** 1.45, 0, 1);
    const nominalBurnupMwdPerKg = clamp(
      5.8 + radialShape * 5.0 - normalizedY * 0.16 + normalizedX * 0.08,
      4.8,
      11.4,
    );
    const bundles = Array.from({ length: CORE_BUNDLE_POSITION_COUNT }, (_, bundlePosition) => {
      const axialShape = 1 - Math.abs(bundlePosition - 5.5) / 6.5;
      const burnup = clamp(
        nominalBurnupMwdPerKg * (0.66 + axialShape * 0.27) +
          (Math.sin((position.column + 1) * 0.37 + bundlePosition * 0.6) +
            Math.cos((position.row + 1) * 0.29 - bundlePosition * 0.25)) *
            0.06,
        3.4,
        13.9,
      );
      const bundle = createBundle(
        bundleSequence,
        bundlePosition,
        burnup,
        0,
      );
      bundleSequence += 1;
      return bundle;
    });

    channels.push({
      channelIndex,
      gridColumn: position.column,
      gridRow: position.row,
      flowDirection: getFlowDirection(position),
      averageBurnupMwdPerKg: bundles.reduce((sum, bundle) => sum + bundle.currentBurnupMwdPerKg, 0) / bundles.length,
      bundles,
    });
  }

  return {
    sequence: 0,
    simulationTimeSeconds: 0,
    wallElapsedSeconds: 0,
    normalizedPowerFraction: 0.988,
    targetPowerFraction: 1,
    absoluteTiltFraction: 0.018,
    targetTiltFraction: 0,
    controlMarginFraction: 0.84,
    deviceAvailableFraction: 0.98,
    pendingActionCount: 0,
    scoreTotal: 746,
    scoreDelta: 0,
    isPaused: false,
    playbackModeId: "1x",
    freshBundlesAvailable: 128,
    refuellingOperationCount: 0,
    lastRefuelledChannel: -1,
    lastRefuellingDirectionId: null,
    lastRefuellingShiftCount: 0,
    nextFreshBundleSequence: 100000,
    channels,
    lastEvent: {
      eventId: "fixture-event-0",
      timeSeconds: 0,
      title: "Practice session online",
      detail: "Base control is 1 simulated hour every 2 seconds; select a channel to inspect its 12-position bundle stack.",
      tone: "info",
    },
  };
}

function createGridPositions(): Array<{ column: number; row: number }> {
  const positions: Array<{ column: number; row: number }> = [];
  for (let row = 0; row < ROW_LENGTHS.length; row += 1) {
    const rowLength = ROW_LENGTHS[row];
    const firstColumn = Math.floor((CORE_GRID_WIDTH - rowLength) / 2);
    for (let offset = 0; offset < rowLength; offset += 1) {
      positions.push({ column: firstColumn + offset, row });
    }
  }
  if (positions.length !== CORE_CHANNEL_COUNT) {
    throw new Error("The deterministic compatibility fixture must contain 380 channels.");
  }
  return positions;
}

function getFlowDirection(position: { column: number; row: number }): RefuelRequest["directionId"] {
  return (position.column + position.row) % 2 === 0 ? "toward-end-b" : "toward-end-a";
}

function createBundle(
  sequence: number,
  position: number,
  burnup: number,
  insertedAtSeconds: number,
  fuelTypeId = "NAT-U-SYNTHETIC",
  stateVersion = 0,
): CanduChannelSnapshot["bundles"][number] {
  return {
    position,
    bundleId: `SYN-B-${String(sequence).padStart(6, "0")}`,
    fuelTypeId,
    currentBurnupMwdPerKg: burnup,
    powerWatts: 0,
    localPowerFraction: 0,
    insertedAtSeconds,
    stateVersion,
    isFresh: burnup <= 0.001,
  };
}

function advance(
  state: FixtureState,
  command: Extract<CanduCommand, { type: "advance" }>,
): CanduCommandResponse {
  if (!Number.isFinite(command.wallMilliseconds) || command.wallMilliseconds < 0) {
    return reject(state, command, "Advance time must be finite and nonnegative.", "fixture.advance.invalid");
  }

  const wallSeconds = command.wallMilliseconds / 1000;
  state.wallElapsedSeconds += wallSeconds;
  if (state.isPaused || state.playbackModeId === "pause") {
    return accept(state, command, "Simulation is paused; no simulated time was added.");
  }

  const simulationSeconds = wallSeconds * PLAYBACK_FACTORS[state.playbackModeId];
  advanceSimulation(state, simulationSeconds);
  return accept(state, command, `Advanced ${formatHours(simulationSeconds)} at ${state.playbackModeId}.`);
}

function step(
  state: FixtureState,
  command: Extract<CanduCommand, { type: "step" }>,
): CanduCommandResponse {
  if (!Number.isFinite(command.simulationSeconds) || command.simulationSeconds <= 0) {
    return reject(state, command, "A single step must be a positive finite duration.", "fixture.step.invalid");
  }
  advanceSimulation(state, command.simulationSeconds);
  return accept(state, command, `Single-stepped ${formatHours(command.simulationSeconds)}.`);
}

function advanceSimulation(state: FixtureState, simulationSeconds: number): void {
  if (simulationSeconds <= 0) {
    state.scoreDelta = 0;
    return;
  }

  let remainingSeconds = simulationSeconds;
  state.scoreDelta = 0;
  while (remainingSeconds > 0) {
    const stepSeconds = Math.min(600, remainingSeconds);
    const hours = stepSeconds / HOURS_TO_SECONDS;
    const response = clamp(hours * 0.08, 0, 0.24);
    state.normalizedPowerFraction += (state.targetPowerFraction - state.normalizedPowerFraction) * response;
    state.absoluteTiltFraction += (state.targetTiltFraction - state.absoluteTiltFraction) * response;
    state.simulationTimeSeconds += stepSeconds;
    state.controlMarginFraction = clamp(
      0.84 - Math.abs(state.targetPowerFraction - 1) * 0.22 - Math.abs(state.absoluteTiltFraction) * 0.38,
      0.56,
      0.9,
    );
    state.deviceAvailableFraction = clamp(0.98 - state.refuellingOperationCount * 0.004, 0.86, 0.98);

    const projection = createFixturePowerProjection(state);
    let stabilityScore = 10 - Math.abs(state.normalizedPowerFraction - state.targetPowerFraction) * 90;
    stabilityScore -= Math.abs(state.absoluteTiltFraction - state.targetTiltFraction) * 32;
    state.scoreDelta += stabilityScore * hours * 0.16;
    state.scoreTotal = Math.max(0, state.scoreTotal + stabilityScore * hours * 0.16);

    for (const channel of state.channels) {
      for (const bundle of channel.bundles) {
        const powerWatts = projection.bundlePowerWatts.get(bundle.bundleId) ?? 0;
        const burnupDelta = powerWatts * stepSeconds /
          BUNDLE_HEAVY_METAL_MASS_KG /
          JOULES_PER_MW_DAY_PER_KG;
        bundle.currentBurnupMwdPerKg = clamp(bundle.currentBurnupMwdPerKg + burnupDelta, 0, 24);
        bundle.isFresh = bundle.currentBurnupMwdPerKg <= 0.001;
      }
      channel.averageBurnupMwdPerKg = averageBurnup(channel);
    }

    remainingSeconds -= stepSeconds;
  }

  const status: CanduConvergenceStatus = getConvergence(state);
  if (status.state === "settling") {
    setEvent(state, "Core response settling", "The reduced projection is active while queued control targets converge.", "info");
  }
}

function setPlaybackMode(
  state: FixtureState,
  command: Extract<CanduCommand, { type: "set-playback-mode" }>,
): CanduCommandResponse {
  state.playbackModeId = command.modeId;
  state.isPaused = command.modeId === "pause";
  setEvent(
    state,
    command.modeId === "pause" ? "Simulation paused" : `Playback set to ${command.modeId}`,
    command.modeId === "pause" ? "Use a speed control or single-step to continue." : "Live requests will advance at the selected rate.",
    "info",
  );
  return accept(state, command, command.modeId === "pause" ? "Simulation paused." : `Playback set to ${command.modeId}.`);
}

function queuePowerTarget(
  state: FixtureState,
  command: Extract<CanduCommand, { type: "queue-power-target" }>,
): CanduCommandResponse {
  if (!Number.isFinite(command.targetFraction) || command.targetFraction < 0.8 || command.targetFraction > 1.2) {
    return reject(state, command, "Power target must be between 80% and 120%.", "fixture.power-target.invalid");
  }
  state.targetPowerFraction = command.targetFraction;
  setEvent(state, "Power target queued", `Automatic regulation is targeting ${(command.targetFraction * 100).toFixed(1)}%.`, "positive");
  return accept(state, command, `Power target queued at ${(command.targetFraction * 100).toFixed(1)}%.`);
}

function queueTiltTarget(
  state: FixtureState,
  command: Extract<CanduCommand, { type: "queue-tilt-target" }>,
): CanduCommandResponse {
  if (!Number.isFinite(command.targetFraction) || command.targetFraction < -0.2 || command.targetFraction > 0.2) {
    return reject(state, command, "Tilt target must be between -20% and +20%.", "fixture.tilt-target.invalid");
  }
  state.targetTiltFraction = command.targetFraction;
  setEvent(state, "Tilt target queued", `The axial tilt target is now ${formatSignedPercent(command.targetFraction)}.`, "positive");
  return accept(state, command, `Tilt target queued at ${formatSignedPercent(command.targetFraction)}.`);
}

function previewRefuel(
  state: FixtureState,
  command: Extract<CanduCommand, { type: "preview-refuel" }>,
): CanduCommandResponse {
  const validationMessage = validateRefuelRequest(state, command.request);
  if (validationMessage !== null) {
    return reject(state, command, validationMessage, "fixture.refuel.invalid");
  }
  const preview = makePreview(state, normalizeRequest(command.request));
  return accept(state, command, `Preview ready for channel ${command.request.channelIndex}.`, preview);
}

function commitRefuel(
  state: FixtureState,
  command: Extract<CanduCommand, { type: "commit-refuel" }>,
): CanduCommandResponse {
  const validationMessage = validateRefuelRequest(state, command.request);
  if (validationMessage !== null) {
    return reject(state, command, validationMessage, "fixture.refuel.invalid");
  }
  if (state.freshBundlesAvailable < command.request.shiftCount) {
    return reject(state, command, "There are not enough fresh bundles for this shift.", "fixture.refuel.inventory");
  }

  const request = normalizeRequest(command.request);
  const preview = makePreview(state, request);
  state.channels = createRefuelCandidateChannels(state, request);
  state.freshBundlesAvailable -= request.shiftCount;
  state.refuellingOperationCount += 1;
  state.lastRefuelledChannel = request.channelIndex;
  state.lastRefuellingDirectionId = request.directionId;
  state.lastRefuellingShiftCount = request.shiftCount;
  state.nextFreshBundleSequence += request.shiftCount;
  state.scoreDelta = preview.projectedScoreDelta;
  state.scoreTotal = Math.max(0, state.scoreTotal + state.scoreDelta);
  setEvent(
    state,
    `Channel ${request.channelIndex} refuelled`,
    `${request.shiftCount} bundles moved ${request.directionId === "toward-end-b" ? "End A → End B" : "End B → End A"}; ${preview.dischargeBurnupMwdPerKg.toFixed(1)} MWd/kg discharged.`,
    "positive",
  );
  return accept(state, command, `Committed ${request.shiftCount}-bundle shift on channel ${request.channelIndex}.`, preview);
}

function validateRefuelRequest(state: FixtureState, request: RefuelRequest): string | null {
  if (!Number.isInteger(request.channelIndex) || request.channelIndex < 0 || request.channelIndex >= CORE_CHANNEL_COUNT) {
    return "Choose a channel from 0 through 379.";
  }
  if (request.directionId !== "toward-end-a" && request.directionId !== "toward-end-b") {
    return "Choose either toward-end-a or toward-end-b.";
  }
  if (request.shiftCount !== 4 && request.shiftCount !== 8) {
    return "The practice core supports four- or eight-bundle shifts.";
  }
  if (request.fuelTypeId.trim().length === 0) {
    return "Choose a fresh-fuel type.";
  }
  if (state.freshBundlesAvailable < request.shiftCount) {
    return "There are not enough fresh bundles for this shift.";
  }
  return null;
}

function normalizeRequest(request: RefuelRequest): RefuelRequest {
  return { ...request, fuelTypeId: request.fuelTypeId.trim() };
}

function makePreview(state: FixtureState, request: RefuelRequest): RefuelPreview {
  const channel = state.channels[request.channelIndex];
  const sourceProjection = createFixturePowerProjection(state);
  const candidateState: FixtureState = {
    ...state,
    channels: createRefuelCandidateChannels(state, request),
  };
  const candidateProjection = createFixturePowerProjection(candidateState);
  const sourceChannelPower = sourceProjection.channelPowerWatts[request.channelIndex];
  const candidateChannelPower = candidateProjection.channelPowerWatts[request.channelIndex];
  const sourceLocalPower = sourceProjection.meanChannelPowerWatts <= 0
    ? 1
    : sourceChannelPower / sourceProjection.meanChannelPowerWatts;
  const candidateLocalPower = candidateProjection.meanChannelPowerWatts <= 0
    ? 1
    : candidateChannelPower / candidateProjection.meanChannelPowerWatts;
  const sourceLocalTilt = sourceProjection.channelTiltFractions[request.channelIndex];
  const candidateLocalTilt = candidateProjection.channelTiltFractions[request.channelIndex];
  const discharged = request.directionId === "toward-end-b"
    ? channel.bundles.slice(CORE_BUNDLE_POSITION_COUNT - request.shiftCount)
    : channel.bundles.slice(0, request.shiftCount);
  const dischargeBurnupMwdPerKg = discharged.reduce((sum, bundle) => sum + bundle.currentBurnupMwdPerKg, 0) / discharged.length;
  const localPowerDeltaFraction = candidateLocalPower - sourceLocalPower;
  const localTiltDeltaFraction = candidateLocalTilt - sourceLocalTilt;
  const projectedScoreDelta = clamp(
    1.8 + discharged.reduce((sum, bundle) => sum + bundle.currentBurnupMwdPerKg, 0) * 0.12 - Math.abs(localTiltDeltaFraction) * 14,
    -4,
    9,
  );

  return {
    request,
    dischargeBurnupMwdPerKg,
    localPowerDeltaFraction,
    localTiltDeltaFraction,
    predictedReactivityDelta: candidateProjection.reactivity - sourceProjection.reactivity,
    projectedPowerFraction: state.normalizedPowerFraction,
    projectedTiltFraction: state.absoluteTiltFraction,
    projectedScoreDelta,
    insertedBundleIds: Array.from({ length: request.shiftCount }, (_, index) =>
      `SYN-B-${String(state.nextFreshBundleSequence + index).padStart(6, "0")}`,
    ),
    dischargedBundleIds: discharged.map((bundle) => bundle.bundleId),
  };
}

function createRefuelCandidateChannels(
  state: FixtureState,
  request: RefuelRequest,
): FixtureChannel[] {
  const channels = state.channels.map((channel) => ({
    ...channel,
    bundles: channel.bundles.map((bundle) => ({ ...bundle })),
  }));
  const channel = channels[request.channelIndex];
  const source = channel.bundles;
  const target: CanduChannelSnapshot["bundles"] = new Array(source.length);
  const insertedStart = request.directionId === "toward-end-b"
    ? 0
    : CORE_BUNDLE_POSITION_COUNT - request.shiftCount;

  for (let index = 0; index < request.shiftCount; index += 1) {
    const position = insertedStart + index;
    target[position] = createBundle(
      state.nextFreshBundleSequence + index,
      position,
      0,
      state.simulationTimeSeconds,
      request.fuelTypeId,
    );
  }

  if (request.directionId === "toward-end-b") {
    for (let position = 0; position < CORE_BUNDLE_POSITION_COUNT - request.shiftCount; position += 1) {
      target[position + request.shiftCount] = moveBundle(source[position], position + request.shiftCount);
    }
  } else {
    for (let position = request.shiftCount; position < CORE_BUNDLE_POSITION_COUNT; position += 1) {
      target[position - request.shiftCount] = moveBundle(source[position], position - request.shiftCount);
    }
  }

  channel.bundles = target;
  channel.averageBurnupMwdPerKg = averageBurnup(channel);
  return channels;
}

function moveBundle(bundle: FixtureChannel["bundles"][number], position: number) {
  return {
    ...bundle,
    position,
    stateVersion: bundle.stateVersion + 1,
  };
}

function averageBurnup(channel: FixtureChannel): number {
  return channel.bundles.reduce((sum, bundle) => sum + bundle.currentBurnupMwdPerKg, 0) / channel.bundles.length;
}

function createFixturePowerProjection(state: FixtureState): FixturePowerProjection {
  const channelAverageBurnups = state.channels.map((channel) => averageBurnup(channel));
  const channelsByCoordinate = new Map(
    state.channels.map((channel) => [`${channel.gridColumn}:${channel.gridRow}`, channel]),
  );
  const nodes: Array<{
    channel: FixtureChannel;
    bundle: FixtureChannel["bundles"][number];
    rawWeight: number;
  }> = [];
  let rawWeightTotal = 0;
  let worthTotal = 0;
  let radialBalanceTotal = 0;
  let flowTotal = 0;

  for (const channel of state.channels) {
    const normalizedX = (channel.gridColumn - 10.5) / 11.5;
    const normalizedY = (channel.gridRow - 10.5) / 11.5;
    const radialDistance = clamp(
      Math.sqrt(normalizedX ** 2 + normalizedY ** 2) / Math.sqrt(2),
      0,
      1,
    );
    const neighbours = [
      channelsByCoordinate.get(`${channel.gridColumn}:${channel.gridRow - 1}`),
      channelsByCoordinate.get(`${channel.gridColumn + 1}:${channel.gridRow}`),
      channelsByCoordinate.get(`${channel.gridColumn}:${channel.gridRow + 1}`),
      channelsByCoordinate.get(`${channel.gridColumn - 1}:${channel.gridRow}`),
    ].filter((candidate): candidate is FixtureChannel => candidate !== undefined);
    const neighbouringAverageBurnupMwdPerKg = neighbours.length === 0
      ? channelAverageBurnups[0]
      : neighbours.reduce(
          (sum, candidate) => sum + channelAverageBurnups[candidate.channelIndex],
          0,
        ) / neighbours.length;
    const flowDirectionSign = channel.flowDirection === "toward-end-b" ? 1 : -1;
    const channelAverageBurnupMwdPerKg = channelAverageBurnups[channel.channelIndex];

    for (const bundle of channel.bundles) {
      const burnup = bundle.currentBurnupMwdPerKg;
      const radialShape = 0.84 + 0.30 * (1 - radialDistance ** 1.35);
      const axialPosition = bundle.position / (CORE_BUNDLE_POSITION_COUNT - 1);
      const axialShape = 0.86 + 0.28 * Math.sin(Math.PI * axialPosition);
      const burnupShape = clamp(1.04 - 0.006 * burnup, 0.90, 1.04);
      const neighbourShape = clamp(
        1 + (channelAverageBurnupMwdPerKg - neighbouringAverageBurnupMwdPerKg) * 0.0025,
        0.96,
        1.04,
      );
      const flowShape = 1 + flowDirectionSign * 0.004;
      const materialPowerFactor = bundle.fuelTypeId === "NAT-U-SYNTHETIC" ? 1 : 0.99;
      const materialShape = clamp(materialPowerFactor, 0.85, 1.15);
      const rawWeight = radialShape * axialShape * burnupShape * neighbourShape * flowShape * materialShape;
      nodes.push({ channel, bundle, rawWeight });
      rawWeightTotal += rawWeight;

      const burnupWorth = clamp(1.03 - 0.004 * burnup, 0.90, 1.03);
      const neighbourWorth = clamp(
        1 + (neighbouringAverageBurnupMwdPerKg - channelAverageBurnupMwdPerKg) * 0.001,
        0.98,
        1.02,
      );
      worthTotal += materialShape * burnupWorth * neighbourWorth * (1 + flowDirectionSign * 0.001);
    }

    radialBalanceTotal += channel.bundles.length * (1 - radialDistance);
    flowTotal += channel.bundles.length * flowDirectionSign;
  }

  const powerAmplitude = clamp(state.normalizedPowerFraction, 0, 1.5);
  const targetPowerWatts = REFERENCE_POWER_WATTS * powerAmplitude;
  const channelPowerWatts = new Array<number>(CORE_CHANNEL_COUNT).fill(0);
  const channelAxialPowerMoments = new Array<number>(CORE_CHANNEL_COUNT).fill(0);
  const channelTiltFractions = new Array<number>(CORE_CHANNEL_COUNT).fill(0);
  const bundlePowerWatts = new Map<string, number>();
  let totalPowerWatts = 0;
  let burnupTotal = 0;
  for (const node of nodes) {
    const powerWatts = rawWeightTotal <= 0
      ? 0
      : targetPowerWatts * node.rawWeight / rawWeightTotal;
    bundlePowerWatts.set(node.bundle.bundleId, powerWatts);
    channelPowerWatts[node.channel.channelIndex] += powerWatts;
    channelAxialPowerMoments[node.channel.channelIndex] += powerWatts *
      (2 * node.bundle.position / (CORE_BUNDLE_POSITION_COUNT - 1) - 1);
    totalPowerWatts += powerWatts;
    burnupTotal += node.bundle.currentBurnupMwdPerKg;
  }

  if (nodes.length > 0 && targetPowerWatts > 0) {
    const last = nodes[nodes.length - 1];
    const correction = targetPowerWatts - totalPowerWatts;
    const correctedPower = (bundlePowerWatts.get(last.bundle.bundleId) ?? 0) + correction;
    bundlePowerWatts.set(last.bundle.bundleId, correctedPower);
    channelPowerWatts[last.channel.channelIndex] += correction;
    channelAxialPowerMoments[last.channel.channelIndex] += correction *
      (2 * last.bundle.position / (CORE_BUNDLE_POSITION_COUNT - 1) - 1);
    totalPowerWatts += correction;
  }

  for (const channel of state.channels) {
    const channelPower = channelPowerWatts[channel.channelIndex];
    channelTiltFractions[channel.channelIndex] = channelPower <= 0
      ? 0
      : Math.abs(channelAxialPowerMoments[channel.channelIndex] / channelPower);
  }

  const nodeCount = nodes.length;
  const meanBundlePowerWatts = totalPowerWatts / nodeCount;
  const meanChannelPowerWatts = totalPowerWatts / CORE_CHANNEL_COUNT;
  const meanWorth = worthTotal / nodeCount;
  const radialBalance = radialBalanceTotal / nodeCount;
  const meanFlow = flowTotal / nodeCount;
  const effectiveK = clamp(
    1 + 0.012 * (meanWorth - 1) + 0.0008 * (radialBalance - 0.55) + 0.0002 * meanFlow,
    0.90,
    1.10,
  );
  const reactivity = (effectiveK - 1) / effectiveK;
  const weightedPerturbationReactivity = reactivity;
  const reactivityDenominator = 1;
  const reactivityNumerator = weightedPerturbationReactivity * reactivityDenominator;
  const compensationCommand = clamp(
    -reactivity,
    PRACTICE_REGULATOR_LOWER_BOUND,
    PRACTICE_REGULATOR_UPPER_BOUND,
  );
  return {
    referencePowerWatts: REFERENCE_POWER_WATTS,
    powerAmplitude,
    actualPowerFraction: powerAmplitude,
    targetPowerWatts,
    totalPowerWatts,
    meanChannelPowerWatts,
    meanBundlePowerWatts,
    effectiveK,
    reactivity,
    staticReactivity: reactivity,
    staticReactivityMethodId: "compatibility-reduced-effective-k-rho-v2",
    weightedPerturbationReactivity,
    reactivityNumerator,
    reactivityDenominator,
    reactivityIdentity: "compatibility-reduced-adjoint-weighted-unavailable-v1",
    reactivityBindingDigestHex: "0".repeat(64),
    coreReactivity: reactivity,
    compensatedNetReactivity: reactivity + compensationCommand,
    compensationState: compensationCommand,
    compensationCommand,
    compensationLowerBound: PRACTICE_REGULATOR_LOWER_BOUND,
    compensationUpperBound: PRACTICE_REGULATOR_UPPER_BOUND,
    compensationSaturated: compensationCommand !== -reactivity,
    compensationResponseTimeSeconds: PRACTICE_REGULATOR_RESPONSE_TIME_SECONDS,
    cadenceIdentity: PRACTICE_REGULATOR_CADENCE_ID,
    powerBalanceRelativeError: targetPowerWatts <= 0 ? 0 : Math.abs(totalPowerWatts - targetPowerWatts) / targetPowerWatts,
    channelPowerWatts,
    channelTiltFractions,
    bundlePowerWatts,
  };
}

function getConvergence(state: FixtureState): CanduConvergenceStatus {
  const projection = createFixturePowerProjection(state);
  return {
    state: "settling",
    iterations: 0,
    residual: projection.powerBalanceRelativeError,
    relativePowerError: Math.abs(state.normalizedPowerFraction - state.targetPowerFraction),
    lastSolveMilliseconds: 0,
    solverLabel: "compatibility reduced power / no full-core WASM",
  };
}

function createDiagnostics(state: FixtureState): CanduDiagnostics {
  const convergence = getConvergence(state);
  return {
    convergence,
    checks: [
      { label: "Topology", value: "380 × 12", status: "pass" },
      { label: "Power target", value: `${(state.targetPowerFraction * 100).toFixed(1)}%`, status: "pass" },
      { label: "Control margin", value: `${(state.controlMarginFraction * 100).toFixed(0)}%`, status: state.controlMarginFraction > 0.65 ? "pass" : "watch" },
      { label: "Data provenance", value: "synthetic fixture", status: "info" },
    ],
  };
}

function createSnapshot(state: FixtureState): CanduSnapshot {
  const projection = createFixturePowerProjection(state);
  const physics: CanduPhysicsSnapshot = {
    sourceId: "compatibility-reduced-synthetic-candu6-power-v2",
    formulationId: "compatibility-reduced-model-v2",
    shapeMethodId: "compatibility-reduced-power-shape-v2",
    amplitudeMethodId: "compatibility-reduced-power-amplitude-v2",
    reactivityMethodId: "compatibility-reduced-effective-k-rho-v2",
    solveState: "accepted-reduced",
    isAuthoritative: false,
    bindingVersion: state.sequence,
    referencePowerWatts: projection.referencePowerWatts,
    powerAmplitude: projection.powerAmplitude,
    actualPowerFraction: projection.actualPowerFraction,
    targetPowerWatts: projection.targetPowerWatts,
    totalPowerWatts: projection.totalPowerWatts,
    meanChannelPowerWatts: projection.meanChannelPowerWatts,
    meanBundlePowerWatts: projection.meanBundlePowerWatts,
    effectiveK: projection.effectiveK,
    reactivity: projection.reactivity,
    staticReactivity: projection.staticReactivity,
    staticReactivityMethodId: projection.staticReactivityMethodId,
    weightedPerturbationReactivity: projection.weightedPerturbationReactivity,
    reactivityNumerator: projection.reactivityNumerator,
    reactivityDenominator: projection.reactivityDenominator,
    reactivityIdentity: projection.reactivityIdentity,
    reactivityBindingDigestHex: projection.reactivityBindingDigestHex,
    coreReactivity: projection.coreReactivity,
    compensatedNetReactivity: projection.compensatedNetReactivity,
    compensationState: projection.compensationState,
    compensationCommand: projection.compensationCommand,
    compensationLowerBound: projection.compensationLowerBound,
    compensationUpperBound: projection.compensationUpperBound,
    compensationSaturated: projection.compensationSaturated,
    compensationResponseTimeSeconds: projection.compensationResponseTimeSeconds,
    cadenceIdentity: projection.cadenceIdentity,
    powerBalanceRelativeError: projection.powerBalanceRelativeError,
    solverIdentity: "compatibility-reduced-power-v2",
    solverIterationCount: 0,
    solverResidualRelativeInfinity: projection.powerBalanceRelativeError,
  };
  return {
    protocol: PROTOCOL_VERSION,
    source: "synthetic-fixture",
    sequence: state.sequence,
    scenarioId: "CANDU6-PRACTICE-1001",
    dataPackId: "compatibility-synthetic-candu6-v1",
    simulationTimeSeconds: state.simulationTimeSeconds,
    wallElapsedSeconds: state.wallElapsedSeconds,
    normalizedPowerFraction: state.normalizedPowerFraction,
    targetPowerFraction: state.targetPowerFraction,
    absoluteTiltFraction: state.absoluteTiltFraction,
    targetTiltFraction: state.targetTiltFraction,
    controlMarginFraction: state.controlMarginFraction,
    deviceAvailableFraction: state.deviceAvailableFraction,
    pendingActionCount: state.pendingActionCount,
    scoreTotal: state.scoreTotal,
    scoreDelta: state.scoreDelta,
    isPaused: state.isPaused,
    playbackModeId: state.playbackModeId,
    freshBundlesAvailable: state.freshBundlesAvailable,
    refuellingOperationCount: state.refuellingOperationCount,
    lastRefuelledChannel: state.lastRefuelledChannel,
    lastRefuellingDirectionId: state.lastRefuellingDirectionId,
    lastRefuellingShiftCount: state.lastRefuellingShiftCount,
    physics,
    core: {
      channelCount: CORE_CHANNEL_COUNT,
      bundlePositionCount: CORE_BUNDLE_POSITION_COUNT,
      gridWidth: CORE_GRID_WIDTH,
      gridHeight: CORE_GRID_HEIGHT,
      channels: state.channels.map((channel) => ({
        channelIndex: channel.channelIndex,
        gridColumn: channel.gridColumn,
        gridRow: channel.gridRow,
        flowDirection: channel.flowDirection,
        averageBurnupMwdPerKg: channel.averageBurnupMwdPerKg,
        powerWatts: projection.channelPowerWatts[channel.channelIndex],
        localPowerFraction: projection.meanChannelPowerWatts <= 0
          ? 1
          : projection.channelPowerWatts[channel.channelIndex] / projection.meanChannelPowerWatts,
        localTiltFraction: projection.channelTiltFractions[channel.channelIndex],
        bundles: channel.bundles.map((bundle) => ({
          ...bundle,
          powerWatts: projection.bundlePowerWatts.get(bundle.bundleId) ?? 0,
          localPowerFraction: projection.meanBundlePowerWatts <= 0
            ? 0
            : (projection.bundlePowerWatts.get(bundle.bundleId) ?? 0) / projection.meanBundlePowerWatts,
        })),
      })),
    },
    diagnostics: createDiagnostics(state),
    lastEvent: state.lastEvent,
  };
}

function accept(
  state: FixtureState,
  command: CanduCommand,
  message: string,
  preview: RefuelPreview | null = null,
): CanduCommandResponse {
  state.sequence += 1;
  return {
    protocol: PROTOCOL_VERSION,
    accepted: true,
    sequence: state.sequence,
    command,
    message,
    diagnostics: [],
    snapshot: createSnapshot(state),
    preview,
  };
}

function reject(
  state: FixtureState,
  command: CanduCommand,
  message: string,
  code: string,
): CanduCommandResponse {
  state.sequence += 1;
  return {
    protocol: PROTOCOL_VERSION,
    accepted: false,
    sequence: state.sequence,
    command,
    message,
    diagnostics: [{ level: "error", code, message }],
    snapshot: createSnapshot(state),
    preview: null,
  };
}

function setEvent(state: FixtureState, title: string, detail: string, tone: CanduEvent["tone"]): void {
  state.lastEvent = {
    eventId: `fixture-event-${state.sequence + 1}`,
    timeSeconds: state.simulationTimeSeconds,
    title,
    detail,
    tone,
  };
}

function formatHours(seconds: number): string {
  const hours = seconds / HOURS_TO_SECONDS;
  return hours >= 1 ? `${hours.toFixed(hours >= 10 ? 0 : 1)} h` : `${seconds.toFixed(0)} s`;
}

function formatSignedPercent(value: number): string {
  return `${value >= 0 ? "+" : ""}${(value * 100).toFixed(1)}%`;
}

// Keep this import-shaped reference visible to TypeScript consumers that use
// replay archives with the fixture without making the fixture a replay owner.
export type FixtureReplayCompatibility = Pick<CanduReplayArchive, "protocol" | "commands">;
