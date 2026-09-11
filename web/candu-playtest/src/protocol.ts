export const PROTOCOL_VERSION = "candu-playtest-v1" as const;

export const CORE_CHANNEL_COUNT = 380 as const;
export const CORE_BUNDLE_POSITION_COUNT = 12 as const;
export const CORE_GRID_WIDTH = 22 as const;
export const CORE_GRID_HEIGHT = 22 as const;
export const BASE_CLOCK_SIMULATION_SECONDS_PER_WALL_SECOND = 1_800 as const;
export const BASE_CLOCK_WALL_SECONDS_PER_SIMULATION_HOUR = 2 as const;

export type ProtocolSource = "wasm" | "synthetic-fixture";
export type BridgeAvailability = ProtocolSource | "loading" | "unavailable";
export type PlaybackModeId = "pause" | "1x" | "10x" | "60x";
export type RefuellingDirection = "toward-end-a" | "toward-end-b";
export type DiagnosticLevel = "info" | "warning" | "error";
export type EventTone = "info" | "positive" | "warning";
export type ResponseKind = "full" | "compact";

export interface CanduDispatchOptions {
  responseMode?: ResponseKind;
  baseSequence?: number;
}

export interface BridgeStatus {
  source: BridgeAvailability;
  title: string;
  detail: string;
  isWasmAvailable: boolean;
  capabilities: readonly string[];
}

export interface CanduBundleSnapshot {
  position: number;
  bundleId: string;
  fuelTypeId: string;
  currentBurnupMwdPerKg: number;
  powerWatts: number;
  localPowerFraction: number;
  insertedAtSeconds: number;
  stateVersion: number;
  isFresh: boolean;
}

export interface CanduChannelSnapshot {
  channelIndex: number;
  gridColumn: number;
  gridRow: number;
  flowDirection: RefuellingDirection;
  averageBurnupMwdPerKg: number;
  powerWatts: number;
  localPowerFraction: number;
  localTiltFraction: number;
  xenon: CanduXenonChannelSnapshot;
  bundles: CanduBundleSnapshot[];
}

export interface CanduXenonChannelSnapshot {
  channelIndex: number;
  meanI135NumberDensityM3: number;
  maxI135NumberDensityM3: number;
  meanXe135NumberDensityM3: number;
  maxXe135NumberDensityM3: number;
  meanDynamicAbsorptionGroup1PerM: number;
  maxDynamicAbsorptionGroup1PerM: number;
  meanDynamicAbsorptionGroup2PerM: number;
  maxDynamicAbsorptionGroup2PerM: number;
}

export interface CanduXenonSnapshot {
  stateIdentity: string;
  stateDigestHex: string;
  stateVersion: number;
  simulationTimeSeconds: number;
  nodeCount: number;
  couplingIdentity: string;
  hasCoupling: boolean;
  baseCoefficientDigestHex: string;
  dynamicXenonDigestHex: string;
  effectiveCoefficientDigestHex: string;
  meanI135NumberDensityM3: number;
  maxI135NumberDensityM3: number;
  meanXe135NumberDensityM3: number;
  maxXe135NumberDensityM3: number;
  meanDynamicAbsorptionGroup1PerM: number;
  maxDynamicAbsorptionGroup1PerM: number;
  meanDynamicAbsorptionGroup2PerM: number;
  maxDynamicAbsorptionGroup2PerM: number;
  selectedChannelIndex: number;
  selectedChannel: CanduXenonChannelSnapshot | null;
}

export interface CanduRrsZoneSnapshot {
  logicalZoneId: number;
  fillFraction: number;
  referencePowerFraction: number;
  targetPowerFraction: number;
  measuredPowerFraction: number;
  shapeError: number;
}

export interface CanduRrsSnapshot {
  controllerIdentity: string;
  mappingIdentity: string;
  mappingDigestHex: string;
  overlayIdentity: string;
  overlayDigestHex: string;
  stateDigestHex: string;
  simulationTimeSeconds: number;
  nodeCount: number;
  averageFillFraction: number;
  minimumFillFraction: number;
  maximumFillFraction: number;
  measuredPowerWatts: number;
  targetPowerWatts: number;
  powerErrorWatts: number;
  coreReactivity: number;
  compensatedNetReactivity: number;
  commonModeRhoCorrection: number;
  controllerIterationCount: number;
  controllerConverged: boolean;
  responseModelIdentity: string;
  responseModelDigestHex: string;
  appliedFillCommand: number[];
  controlledBaselineWeightedResidual: number;
  combinedWeightedResidual: number;
  candidateSolveCount: number;
  verificationSolveCount: number;
  correctionSolveCount: number;
  correctionApplied: boolean;
  lowExhaustion: boolean;
  highExhaustion: boolean;
  isGameOver: boolean;
  gameOverReason: string;
  cadenceIdentity: string;
  zones: CanduRrsZoneSnapshot[];
}

export function createUnavailableRrsSnapshot(): CanduRrsSnapshot {
  return {
    controllerIdentity: "unavailable",
    mappingIdentity: "unavailable",
    mappingDigestHex: "",
    overlayIdentity: "unavailable",
    overlayDigestHex: "",
    stateDigestHex: "",
    simulationTimeSeconds: 0,
    nodeCount: 0,
    averageFillFraction: 0.5,
    minimumFillFraction: 0.5,
    maximumFillFraction: 0.5,
    measuredPowerWatts: 0,
    targetPowerWatts: 0,
    powerErrorWatts: 0,
    coreReactivity: 0,
    compensatedNetReactivity: 0,
    commonModeRhoCorrection: 0,
    controllerIterationCount: 0,
    controllerConverged: false,
    responseModelIdentity: "unavailable",
    responseModelDigestHex: "",
    appliedFillCommand: Array.from({ length: 14 }, () => 0),
    controlledBaselineWeightedResidual: 0,
    combinedWeightedResidual: 0,
    candidateSolveCount: 0,
    verificationSolveCount: 0,
    correctionSolveCount: 0,
    correctionApplied: false,
    lowExhaustion: false,
    highExhaustion: false,
    isGameOver: false,
    gameOverReason: "",
    cadenceIdentity: "unavailable",
    zones: [],
  };
}

export interface CanduConvergenceStatus {
  state: "converged" | "settling" | "pending" | "unavailable";
  iterations: number;
  residual: number;
  relativePowerError: number;
  lastSolveMilliseconds: number;
  solverLabel: string;
}

export interface CanduDiagnostics {
  convergence: CanduConvergenceStatus;
  checks: Array<{
    label: string;
    value: string;
    status: "pass" | "watch" | "info";
  }>;
}

export interface CanduPhysicsSnapshot {
  sourceId: string;
  formulationId: string;
  shapeMethodId: string;
  amplitudeMethodId: string;
  reactivityMethodId: string;
  solveState: string;
  isAuthoritative: boolean;
  bindingVersion: number;
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
  solverIdentity: string;
  solverIterationCount: number;
  solverResidualRelativeInfinity: number;
  adjointNormalizationIdentity?: string;
  adjointDigestHex?: string;
  adjointIterationCount?: number;
  adjointTransposeResidualRelativeInfinity?: number;
}

export interface CanduEvent {
  eventId: string;
  timeSeconds: number;
  title: string;
  detail: string;
  tone: EventTone;
}

export interface CanduCoreSnapshot {
  channelCount: typeof CORE_CHANNEL_COUNT;
  bundlePositionCount: typeof CORE_BUNDLE_POSITION_COUNT;
  gridWidth: typeof CORE_GRID_WIDTH;
  gridHeight: typeof CORE_GRID_HEIGHT;
  channels: CanduChannelSnapshot[];
}

export interface CanduLabSpatialSolveSnapshot {
  status: string;
  isConverged: boolean;
  hasUsableState: boolean;
  finalState: {
    iteration: number;
    eigenvalue: number;
    totalPowerW: number;
    group1Flux: number[];
    group2Flux: number[];
  } | null;
  diagnostics: {
    iterationCount: number;
    residualRelativeInfinity: number | null;
    sourceShapeChangeInfinity: number | null;
    powerBalanceRelative: number | null;
    convergenceReason: string;
    innerSolveStatus: string;
  };
}

export interface CanduLabSnapshot {
  fixtureId: string;
  simulationTimeSeconds: number;
  freshBundlesAvailable: number;
  refuellingOperationCount: number;
  lastRefuelledChannel: number;
  lastRefuellingDirectionId: string | null;
  lastRefuellingShiftCount: number;
  core: {
    fixtureId: string;
    channelCount: number;
    bundlePositionCount: number;
    channels: Array<{
      channelIndex: number;
      coordinateX: number;
      coordinateY: number;
      flowDirection: string;
      bundles: Array<{
        position: number;
        bundleId: string;
        fuelTypeId: string;
        currentBurnupMwDayPerKg: number;
        currentBurnupJPerKgHm: number;
        insertedAtSeconds: number;
        stateVersion: number;
      }>;
    }>;
  };
  spatialSolve: CanduLabSpatialSolveSnapshot;
}

export interface CanduSnapshot {
  protocol: typeof PROTOCOL_VERSION;
  source: ProtocolSource;
  sequence: number;
  scenarioId: string;
  dataPackId: string;
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
  lastRefuellingDirectionId: RefuellingDirection | null;
  lastRefuellingShiftCount: number;
  physics: CanduPhysicsSnapshot;
  xenon: CanduXenonSnapshot;
  rrs: CanduRrsSnapshot;
  core: CanduCoreSnapshot;
  diagnostics: CanduDiagnostics;
  lastEvent: CanduEvent | null;
  lab?: CanduLabSnapshot;
}

export type CanduSnapshotPatch = Pick<CanduSnapshot,
  | "scenarioId"
  | "dataPackId"
  | "simulationTimeSeconds"
  | "wallElapsedSeconds"
  | "normalizedPowerFraction"
  | "targetPowerFraction"
  | "absoluteTiltFraction"
  | "targetTiltFraction"
  | "controlMarginFraction"
  | "deviceAvailableFraction"
  | "pendingActionCount"
  | "scoreTotal"
  | "scoreDelta"
  | "isPaused"
  | "playbackModeId"
  | "freshBundlesAvailable"
  | "refuellingOperationCount"
  | "lastRefuelledChannel"
  | "lastRefuellingDirectionId"
  | "lastRefuellingShiftCount"
  | "physics"
  | "xenon"
  | "rrs"
  | "diagnostics"
  | "lastEvent">;

export interface RefuelRequest {
  channelIndex: number;
  directionId: RefuellingDirection;
  shiftCount: 4 | 8;
  fuelTypeId: string;
}

export type CanduCommand =
  | { type: "advance"; wallMilliseconds: number }
  | { type: "step"; simulationSeconds: number }
  | { type: "set-playback-mode"; modeId: PlaybackModeId }
  | { type: "pause" }
  | { type: "resume" }
  | { type: "queue-power-target"; targetFraction: number }
  | { type: "queue-tilt-target"; targetFraction: number }
  | { type: "commit-refuel"; request: RefuelRequest }
  | { type: "reset" };

export interface CanduCommandResponse {
  protocol: typeof PROTOCOL_VERSION;
  accepted: boolean;
  sequence: number;
  command: CanduCommand;
  message: string;
  responseKind?: ResponseKind;
  baseSequence?: number;
  requiresResync?: boolean;
  snapshotPatch?: CanduSnapshotPatch | null;
  coreReplacement?: CanduCoreSnapshot | null;
  stateDigest?: string;
  replayDigest?: string;
  diagnostics: Array<{
    level: DiagnosticLevel;
    code: string;
    message: string;
  }>;
  snapshot: CanduSnapshot;
  lab?: CanduLabSnapshot;
  labPreview?: CanduLabSnapshot | null;
}

export interface CanduPlaytestWasmExports {
  getCapabilities?: () => string | Promise<string>;
  initialize?: (requestJson: string) => string | Promise<string>;
  getSnapshotJson: () => string;
  dispatchJson: (commandJson: string) => string | Promise<string>;
}

export type BridgeResult<T> = T | Promise<T>;

export interface CanduPlaytestBridge {
  readonly status: BridgeStatus;
  getSnapshot(): CanduSnapshot;
  dispatch(command: CanduCommand, options?: CanduDispatchOptions): BridgeResult<CanduCommandResponse>;
}

export interface ReplayCommandRecord {
  index: number;
  command: CanduCommand;
  accepted: boolean;
  message: string;
  snapshotSequence: number;
  simulationTimeSeconds: number;
}

export interface CanduReplayArchive {
  protocol: typeof PROTOCOL_VERSION;
  kind: "command-replay";
  source: ProtocolSource;
  createdAt: string;
  commands: ReplayCommandRecord[];
}

export function isPlaybackModeId(value: string): value is PlaybackModeId {
  return value === "pause" || value === "1x" || value === "10x" || value === "60x";
}

export function clamp(value: number, minimum: number, maximum: number): number {
  return Math.min(maximum, Math.max(minimum, value));
}

export function formatProtocolNumber(value: number, digits = 6): string {
  return Number.isFinite(value) ? value.toFixed(digits) : "nan";
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function canonicalize(value: unknown): unknown {
  if (Array.isArray(value)) {
    return value.map(canonicalize);
  }

  if (isRecord(value)) {
    return Object.keys(value)
      .sort()
      .reduce<Record<string, unknown>>((result, key) => {
        const child = value[key];
        if (child !== undefined) {
          result[key] = canonicalize(child);
        }
        return result;
      }, {});
  }

  return value;
}

export function canonicalJson(value: unknown): string {
  const result = JSON.stringify(canonicalize(value));
  if (result === undefined) {
    throw new Error("Protocol serialization produced no JSON value.");
  }
  return result;
}

export function serializeProtocolCommand(
  command: CanduCommand,
  options: CanduDispatchOptions = {},
): string {
  return canonicalJson({
    protocol: PROTOCOL_VERSION,
    type: "command",
    ...(options.responseMode !== undefined ? { responseMode: options.responseMode } : {}),
    ...(options.baseSequence !== undefined ? { baseSequence: options.baseSequence } : {}),
    payload: command,
  });
}

export function serializeReplayArchive(archive: CanduReplayArchive): string {
  return canonicalJson(archive);
}

function parseJson(raw: string | unknown): unknown {
  if (typeof raw !== "string") {
    return raw;
  }

  try {
    return JSON.parse(raw) as unknown;
  } catch (error) {
    const message = error instanceof Error ? error.message : "invalid JSON";
    throw new Error(`candu-playtest-v1 JSON parse failed: ${message}`);
  }
}

function unwrapPayload(value: unknown, expectedType: "snapshot" | "response"): unknown {
  if (!isRecord(value)) {
    return value;
  }

  if (value.type === expectedType && "payload" in value) {
    return value.payload;
  }

  return value;
}

function assertProtocol(value: unknown): asserts value is Record<string, unknown> {
  if (!isRecord(value) || value.protocol !== PROTOCOL_VERSION) {
    throw new Error(`Expected ${PROTOCOL_VERSION} protocol payload.`);
  }
}

export class ProtocolResyncRequiredError extends Error {
  public readonly response: Record<string, unknown>;

  public constructor(response: Record<string, unknown>) {
    super("The compact protocol response requires an authoritative full snapshot.");
    this.name = "ProtocolResyncRequiredError";
    this.response = response;
  }
}

function isFiniteNumber(value: unknown): value is number {
  return typeof value === "number" && Number.isFinite(value);
}

function isInteger(value: unknown): value is number {
  return isFiniteNumber(value) && Number.isInteger(value);
}

export function isProtocolSnapshot(value: unknown): value is CanduSnapshot {
  if (!isRecord(value) || value.protocol !== PROTOCOL_VERSION ||
      (value.source !== "wasm" && value.source !== "synthetic-fixture") ||
      !isInteger(value.sequence) || !isFiniteNumber(value.simulationTimeSeconds) ||
      !isInteger(value.lastRefuelledChannel) || !isRecord(value.core)) {
    return false;
  }

  const core = value.core;
  if (core.channelCount !== CORE_CHANNEL_COUNT ||
      core.bundlePositionCount !== CORE_BUNDLE_POSITION_COUNT ||
      core.gridWidth !== CORE_GRID_WIDTH || core.gridHeight !== CORE_GRID_HEIGHT ||
      !Array.isArray(core.channels) || core.channels.length !== CORE_CHANNEL_COUNT) {
    return false;
  }

  if (value.lastRefuelledChannel < -1 || value.lastRefuelledChannel >= core.channels.length) {
    return false;
  }

  return core.channels.every((channel, channelIndex) =>
    isRecord(channel) && channel.channelIndex === channelIndex &&
    Array.isArray(channel.bundles) && channel.bundles.length === CORE_BUNDLE_POSITION_COUNT);
}

export function parseProtocolSnapshot(raw: string | unknown): CanduSnapshot {
  const value = unwrapPayload(parseJson(raw), "snapshot");
  if (!isProtocolSnapshot(value)) {
    throw new Error("candu-playtest-v1 snapshot is malformed or incomplete.");
  }
  return value;
}

export function parseProtocolResponse(
  raw: string | unknown,
  previousSnapshot?: CanduSnapshot,
  resyncSnapshot?: CanduSnapshot,
): CanduCommandResponse {
  return parseProtocolResponseWithBase(raw, previousSnapshot, resyncSnapshot);
}

function parseProtocolResponseWithBase(
  raw: string | unknown,
  previousSnapshot?: CanduSnapshot,
  resyncSnapshot?: CanduSnapshot,
): CanduCommandResponse {
  const value = unwrapPayload(parseJson(raw), "response");
  assertProtocol(value);

  const isCompact = value.responseKind === "compact" ||
    "snapshotPatch" in value ||
    "coreReplacement" in value;
  if (isCompact) {
    if (value.requiresResync === true ||
        !isInteger(value.baseSequence) ||
        previousSnapshot === undefined ||
        value.baseSequence !== previousSnapshot.sequence) {
      if (resyncSnapshot !== undefined) {
        return {
          ...value,
          responseKind: "compact",
          snapshot: resyncSnapshot,
        } as unknown as CanduCommandResponse;
      }
      throw new ProtocolResyncRequiredError(value);
    }

    const patch = value.snapshotPatch;
    if (!isRecord(patch)) {
      throw new Error("candu-playtest-v1 compact response is missing snapshotPatch.");
    }

    const snapshot = materializeCompactSnapshot(
      previousSnapshot,
      value,
      patch,
    );
    return {
      ...value,
      responseKind: "compact",
      snapshot,
    } as unknown as CanduCommandResponse;
  }

  if (!isRecord(value.snapshot)) {
    throw new Error("candu-playtest-v1 response is missing a snapshot.");
  }
  parseProtocolSnapshot(value.snapshot);
  return value as unknown as CanduCommandResponse;
}

export function parseProtocolResponseWithSnapshot(
  raw: string | unknown,
  previousSnapshot: CanduSnapshot | undefined,
  resyncSnapshot: CanduSnapshot,
): CanduCommandResponse {
  return parseProtocolResponseWithBase(raw, previousSnapshot, resyncSnapshot);
}

export function materializeCompactSnapshot(
  base: CanduSnapshot,
  response: Record<string, unknown>,
  patchValue: Record<string, unknown>,
): CanduSnapshot {
  if (!isInteger(response.sequence) ||
      !isRecord(patchValue.physics) ||
      !isRecord(patchValue.xenon) ||
      !isRecord(patchValue.rrs) ||
      !isRecord(patchValue.diagnostics) ||
      !isFiniteNumber(patchValue.simulationTimeSeconds) ||
      !isFiniteNumber(patchValue.wallElapsedSeconds) ||
      typeof patchValue.scenarioId !== "string" ||
      typeof patchValue.dataPackId !== "string" ||
      typeof patchValue.playbackModeId !== "string" ||
      ("coreReplacement" in response &&
        response.coreReplacement !== null &&
        !isRecord(response.coreReplacement))) {
    throw new Error("candu-playtest-v1 compact snapshot patch is malformed.");
  }

  const replacement = response.coreReplacement;
  const core = replacement === undefined || replacement === null
    ? base.core
    : replacement as CanduCoreSnapshot;
  const snapshot: CanduSnapshot = {
    ...base,
    sequence: response.sequence,
    scenarioId: patchValue.scenarioId as string,
    dataPackId: patchValue.dataPackId as string,
    simulationTimeSeconds: patchValue.simulationTimeSeconds as number,
    wallElapsedSeconds: patchValue.wallElapsedSeconds as number,
    normalizedPowerFraction: patchValue.normalizedPowerFraction as number,
    targetPowerFraction: patchValue.targetPowerFraction as number,
    absoluteTiltFraction: patchValue.absoluteTiltFraction as number,
    targetTiltFraction: patchValue.targetTiltFraction as number,
    controlMarginFraction: patchValue.controlMarginFraction as number,
    deviceAvailableFraction: patchValue.deviceAvailableFraction as number,
    pendingActionCount: patchValue.pendingActionCount as number,
    scoreTotal: patchValue.scoreTotal as number,
    scoreDelta: patchValue.scoreDelta as number,
    isPaused: patchValue.isPaused as boolean,
    playbackModeId: patchValue.playbackModeId as PlaybackModeId,
    freshBundlesAvailable: patchValue.freshBundlesAvailable as number,
    refuellingOperationCount: patchValue.refuellingOperationCount as number,
    lastRefuelledChannel: patchValue.lastRefuelledChannel as number,
    lastRefuellingDirectionId: "lastRefuellingDirectionId" in patchValue
      ? patchValue.lastRefuellingDirectionId as RefuellingDirection | null
      : base.lastRefuellingDirectionId,
    lastRefuellingShiftCount: patchValue.lastRefuellingShiftCount as number,
    physics: patchValue.physics as unknown as CanduPhysicsSnapshot,
    xenon: patchValue.xenon as unknown as CanduXenonSnapshot,
    rrs: patchValue.rrs as unknown as CanduRrsSnapshot,
    core,
    diagnostics: patchValue.diagnostics as unknown as CanduDiagnostics,
    lastEvent: "lastEvent" in patchValue
      ? patchValue.lastEvent as CanduEvent | null
      : base.lastEvent,
  };

  if (!isProtocolSnapshot(snapshot)) {
    throw new Error("candu-playtest-v1 compact snapshot patch produced an invalid snapshot.");
  }
  return snapshot;
}

export function parseReplayArchive(raw: string | unknown): CanduReplayArchive {
  const value = parseJson(raw);
  assertProtocol(value);
  if (value.kind !== "command-replay" || !Array.isArray(value.commands)) {
    throw new Error("Expected a candu-playtest-v1 command replay archive.");
  }
  return value as unknown as CanduReplayArchive;
}

export function findWasmExports(): CanduPlaytestWasmExports | null {
  const root = globalThis as typeof globalThis & {
    canduPlaytestWasm?: unknown;
    __canduPlaytestWasm?: unknown;
    CanduPlaytestWasm?: unknown;
  };
  const candidates = [root.canduPlaytestWasm, root.__canduPlaytestWasm, root.CanduPlaytestWasm];

  for (const candidate of candidates) {
    if (!isRecord(candidate)) {
      continue;
    }

    const getSnapshotJson = candidate.getSnapshotJson;
    const dispatchJson = candidate.dispatchJson;
    if (typeof getSnapshotJson === "function" && typeof dispatchJson === "function") {
      const getCapabilities = candidate.getCapabilities;
      const initialize = candidate.initialize;
      return {
        ...(typeof getCapabilities === "function" ? { getCapabilities: getCapabilities as CanduPlaytestWasmExports["getCapabilities"] } : {}),
        ...(typeof initialize === "function" ? { initialize: initialize as CanduPlaytestWasmExports["initialize"] } : {}),
        getSnapshotJson: getSnapshotJson as CanduPlaytestWasmExports["getSnapshotJson"],
        dispatchJson: dispatchJson as CanduPlaytestWasmExports["dispatchJson"],
      };
    }
  }

  return null;
}
