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

interface FixtureChannel extends CanduChannelSnapshot {
  basePowerFraction: number;
  baseTiltFraction: number;
  refuelPowerOffset: number;
  refuelTiltOffset: number;
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
 * UI-only compatibility behavior for Milestone 0.5. This is deliberately not
 * a second reactor simulator: it supplies stable protocol-shaped values and
 * state transitions until the engine-neutral model is compiled to browser WASM.
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
    const azimuthalRipple =
      (Math.sin((position.column + 1) * 0.73) + Math.cos((position.row + 1) * 0.61)) * 0.005;
    const basePowerFraction = clamp(
      0.74 + radialShape * 0.34 + azimuthalRipple,
      0.68,
      1.12,
    );
    const baseTiltFraction = clamp(
      normalizedY * 0.035 + normalizedX * 0.012 + azimuthalRipple * 0.7,
      -0.08,
      0.08,
    );
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
        basePowerFraction * (0.76 + axialShape * 0.34),
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
      localPowerFraction: basePowerFraction,
      localTiltFraction: baseTiltFraction,
      bundles,
      basePowerFraction,
      baseTiltFraction,
      refuelPowerOffset: 0,
      refuelTiltOffset: 0,
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
  localPowerFraction: number,
  insertedAtSeconds: number,
  fuelTypeId = "NAT-U-SYNTHETIC",
  stateVersion = 0,
) {
  return {
    position,
    bundleId: `SYN-B-${String(sequence).padStart(6, "0")}`,
    fuelTypeId,
    currentBurnupMwdPerKg: burnup,
    localPowerFraction,
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

  state.simulationTimeSeconds += simulationSeconds;
  const hours = simulationSeconds / HOURS_TO_SECONDS;
  const response = clamp(hours * 0.08, 0, 0.24);
  state.normalizedPowerFraction += (state.targetPowerFraction - state.normalizedPowerFraction) * response;
  state.absoluteTiltFraction += (state.targetTiltFraction - state.absoluteTiltFraction) * response;
  state.controlMarginFraction = clamp(
    0.84 - Math.abs(state.targetPowerFraction - 1) * 0.22 - Math.abs(state.absoluteTiltFraction) * 0.38,
    0.56,
    0.9,
  );
  state.deviceAvailableFraction = clamp(0.98 - state.refuellingOperationCount * 0.004, 0.86, 0.98);

  let stabilityScore = 10 - Math.abs(state.normalizedPowerFraction - state.targetPowerFraction) * 90;
  stabilityScore -= Math.abs(state.absoluteTiltFraction - state.targetTiltFraction) * 32;
  state.scoreDelta = stabilityScore * hours * 0.16;
  state.scoreTotal = Math.max(0, state.scoreTotal + state.scoreDelta);

  for (const channel of state.channels) {
    const channelIncrement = hours * (0.0031 + channel.localPowerFraction * 0.0016);
    for (const bundle of channel.bundles) {
      if (!bundle.isFresh) {
        bundle.currentBurnupMwdPerKg = clamp(bundle.currentBurnupMwdPerKg + channelIncrement, 0, 24);
      }
    }
    channel.averageBurnupMwdPerKg = averageBurnup(channel);
    channel.localPowerFraction = getChannelPower(state, channel);
    channel.localTiltFraction = getChannelTilt(state, channel);
    for (const bundle of channel.bundles) {
      bundle.localPowerFraction = channel.localPowerFraction * (0.76 + (1 - Math.abs(bundle.position - 5.5) / 6.5) * 0.34);
      bundle.isFresh = bundle.currentBurnupMwdPerKg <= 0.001;
    }
  }

  const status: CanduConvergenceStatus = getConvergence(state);
  if (status.state === "settling") {
    setEvent(state, "Core response settling", "The compatibility response is converging on the queued targets.", "info");
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
  const channel = state.channels[request.channelIndex];
  const source = channel.bundles;
  const target = new Array(source.length);
  const insertedStart = request.directionId === "toward-end-b" ? 0 : CORE_BUNDLE_POSITION_COUNT - request.shiftCount;
  const inserted = request.shiftCount === 4 ? [0, 1, 2, 3] : [0, 1, 2, 3, 4, 5, 6, 7];

  for (const index of inserted) {
    const position = insertedStart + index;
    target[position] = createBundle(
      state.nextFreshBundleSequence + index,
      position,
      0,
      channel.localPowerFraction * 0.76,
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
  channel.refuelPowerOffset = clamp(channel.refuelPowerOffset + preview.localPowerDeltaFraction, -0.09, 0.09);
  channel.refuelTiltOffset = clamp(channel.refuelTiltOffset + preview.localTiltDeltaFraction, -0.08, 0.08);
  channel.averageBurnupMwdPerKg = averageBurnup(channel);
  state.freshBundlesAvailable -= request.shiftCount;
  state.refuellingOperationCount += 1;
  state.lastRefuelledChannel = request.channelIndex;
  state.lastRefuellingDirectionId = request.directionId;
  state.lastRefuellingShiftCount = request.shiftCount;
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
  const discharged = request.directionId === "toward-end-b"
    ? channel.bundles.slice(CORE_BUNDLE_POSITION_COUNT - request.shiftCount)
    : channel.bundles.slice(0, request.shiftCount);
  const directionSign = request.directionId === "toward-end-b" ? 1 : -1;
  const intensity = 0.008 + channel.localPowerFraction * 0.004;
  const localPowerDeltaFraction = directionSign * intensity * (request.shiftCount / 4);
  const localTiltDeltaFraction = directionSign * (0.004 + Math.abs(channel.localTiltFraction) * 0.012) * (request.shiftCount / 4);
  const projectedPowerFraction = clamp(state.normalizedPowerFraction + localPowerDeltaFraction * 0.08, 0.7, 1.3);
  const projectedTiltFraction = clamp(state.absoluteTiltFraction + localTiltDeltaFraction * 0.24, -0.3, 0.3);
  const projectedScoreDelta = clamp(
    1.8 + discharged.reduce((sum, bundle) => sum + bundle.currentBurnupMwdPerKg, 0) * 0.12 - Math.abs(localTiltDeltaFraction) * 14,
    -4,
    9,
  );

  return {
    request,
    dischargeBurnupMwdPerKg: discharged.reduce((sum, bundle) => sum + bundle.currentBurnupMwdPerKg, 0) / discharged.length,
    localPowerDeltaFraction,
    localTiltDeltaFraction,
    projectedPowerFraction,
    projectedTiltFraction,
    projectedScoreDelta,
    insertedBundleIds: Array.from({ length: request.shiftCount }, (_, index) =>
      `SYN-B-${String(state.nextFreshBundleSequence + index).padStart(6, "0")}`,
    ),
    dischargedBundleIds: discharged.map((bundle) => bundle.bundleId),
  };
}

function moveBundle(bundle: FixtureChannel["bundles"][number], position: number) {
  return {
    ...bundle,
    position,
    stateVersion: bundle.stateVersion + 1,
  };
}

function getChannelPower(state: FixtureState, channel: FixtureChannel): number {
  return clamp(
    channel.basePowerFraction + (state.normalizedPowerFraction - 1) * 0.18 + channel.refuelPowerOffset,
    0.64,
    1.28,
  );
}

function getChannelTilt(state: FixtureState, channel: FixtureChannel): number {
  return clamp(
    channel.baseTiltFraction + state.absoluteTiltFraction * 0.22 + channel.refuelTiltOffset,
    -0.25,
    0.25,
  );
}

function averageBurnup(channel: FixtureChannel): number {
  return channel.bundles.reduce((sum, bundle) => sum + bundle.currentBurnupMwdPerKg, 0) / channel.bundles.length;
}

function getConvergence(state: FixtureState): CanduConvergenceStatus {
  const distance = Math.abs(state.normalizedPowerFraction - state.targetPowerFraction) + Math.abs(state.absoluteTiltFraction - state.targetTiltFraction);
  const residual = 0.000008 + distance * 0.00042 + state.pendingActionCount * 0.00001;
  return {
    state: distance < 0.012 ? "converged" : distance < 0.06 ? "settling" : "pending",
    iterations: 18 + (state.sequence % 7),
    residual,
    relativePowerError: Math.abs(state.normalizedPowerFraction - state.targetPowerFraction),
    lastSolveMilliseconds: 0.32 + (state.sequence % 5) * 0.04,
    solverLabel: "compatibility response / no authoritative solver",
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
  return {
    protocol: PROTOCOL_VERSION,
    source: "synthetic-fixture",
    sequence: state.sequence,
    scenarioId: "CANDU6-PRACTICE-1001",
    dataPackId: "synthetic-candu6-v1",
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
        localPowerFraction: channel.localPowerFraction,
        localTiltFraction: channel.localTiltFraction,
        bundles: channel.bundles.map((bundle) => ({ ...bundle })),
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
