export const PROTOCOL_VERSION = "candu-playtest-v1" as const;

export const CORE_CHANNEL_COUNT = 380 as const;
export const CORE_BUNDLE_POSITION_COUNT = 12 as const;
export const CORE_GRID_WIDTH = 22 as const;
export const CORE_GRID_HEIGHT = 22 as const;
export const BASE_CLOCK_SIMULATION_SECONDS_PER_WALL_SECOND = 1_800 as const;
export const BASE_CLOCK_WALL_SECONDS_PER_SIMULATION_HOUR = 2 as const;

export type ProtocolSource = "wasm" | "synthetic-fixture";
export type BridgeAvailability = ProtocolSource | "loading" | "unavailable";
export type BridgeModeId = "play" | "lab";
export type PlaybackModeId = "pause" | "1x" | "10x" | "60x";
export type RefuellingDirection = "toward-end-a" | "toward-end-b";
export type LabBoundaryFace = "north" | "east" | "south" | "west" | "end-a" | "end-b";
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
    normalizationScale?: number;
    fissionProductionRate?: number;
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

export interface CanduLabCellSnapshot {
  /** Present on flat core cell lists; omitted when the cell is nested in a channel. */
  channelIndex?: number;
  position: number;
  hasFuel: boolean;
  materialId?: string;
  reflectiveFaces: LabBoundaryFace[];
}

export interface CanduLabChannelSnapshot {
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
  /** New Lab topology projection. Nested cells are preferred when present. */
  cells?: CanduLabCellSnapshot[];
}

export interface CanduLabCoreSnapshot {
  fixtureId: string;
  channelCount: number;
  bundlePositionCount: number;
  channels: CanduLabChannelSnapshot[];
  /** Compatibility with hosts that flatten the 2 × 8 cell projection. */
  cells?: CanduLabCellSnapshot[];
}

export interface CanduLabSnapshot {
  fixtureId: string;
  simulationTimeSeconds: number;
  freshBundlesAvailable: number;
  refuellingOperationCount: number;
  lastRefuelledChannel: number;
  lastRefuellingDirectionId: string | null;
  lastRefuellingShiftCount: number;
  core: CanduLabCoreSnapshot;
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
  | { type: "configure-cell"; channelIndex: number; position: number; hasFuel: boolean; reflectiveFaces: LabBoundaryFace[] }
  | { type: "solve" }
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

export function isPlaybackModeId(value: unknown): value is PlaybackModeId {
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

function isString(value: unknown): value is string {
  return typeof value === "string";
}

function isBoolean(value: unknown): value is boolean {
  return typeof value === "boolean";
}

function hasOwn(value: Record<string, unknown>, key: string): boolean {
  return Object.prototype.hasOwnProperty.call(value, key);
}

function hasOwnProperties(
  value: Record<string, unknown>,
  keys: readonly string[],
): boolean {
  return keys.every((key) => hasOwn(value, key));
}

function hasFiniteNumberFields(
  value: Record<string, unknown>,
  keys: readonly string[],
): boolean {
  return keys.every((key) => hasOwn(value, key) && isFiniteNumber(value[key]));
}

function hasIntegerFields(
  value: Record<string, unknown>,
  keys: readonly string[],
): boolean {
  return keys.every((key) => hasOwn(value, key) && isInteger(value[key]));
}

function hasNonNegativeIntegerFields(
  value: Record<string, unknown>,
  keys: readonly string[],
): boolean {
  return keys.every((key) => hasOwn(value, key) && isNonNegativeInteger(value[key]));
}

function hasStringFields(
  value: Record<string, unknown>,
  keys: readonly string[],
): boolean {
  return keys.every((key) => hasOwn(value, key) && isString(value[key]));
}

function hasBooleanFields(
  value: Record<string, unknown>,
  keys: readonly string[],
): boolean {
  return keys.every((key) => hasOwn(value, key) && isBoolean(value[key]));
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

function isNonNegativeInteger(value: unknown): value is number {
  return isInteger(value) && value >= 0;
}

function isProtocolSource(value: unknown): value is ProtocolSource {
  return value === "wasm" || value === "synthetic-fixture";
}

function isRefuellingDirection(value: unknown): value is RefuellingDirection {
  return value === "toward-end-a" || value === "toward-end-b";
}

function isDiagnosticLevel(value: unknown): value is DiagnosticLevel {
  return value === "info" || value === "warning" || value === "error";
}

function isEventTone(value: unknown): value is EventTone {
  return value === "info" || value === "positive" || value === "warning";
}

function isResponseKind(value: unknown): value is ResponseKind {
  return value === "full" || value === "compact";
}

function isRefuellingShiftCount(value: unknown): value is 0 | 4 | 8 {
  return value === 0 || value === 4 || value === 8;
}

function isChannelIndex(value: unknown): value is number {
  return isNonNegativeInteger(value) && value < CORE_CHANNEL_COUNT;
}

function isCanduXenonChannelSnapshot(
  value: unknown,
  expectedChannelIndex?: number,
): value is CanduXenonChannelSnapshot {
  if (!isRecord(value) || !hasOwnProperties(value, [
    "channelIndex",
    "meanI135NumberDensityM3",
    "maxI135NumberDensityM3",
    "meanXe135NumberDensityM3",
    "maxXe135NumberDensityM3",
    "meanDynamicAbsorptionGroup1PerM",
    "maxDynamicAbsorptionGroup1PerM",
    "meanDynamicAbsorptionGroup2PerM",
    "maxDynamicAbsorptionGroup2PerM",
  ]) || !isChannelIndex(value.channelIndex) ||
      (expectedChannelIndex !== undefined && value.channelIndex !== expectedChannelIndex)) {
    return false;
  }

  return hasFiniteNumberFields(value, [
    "meanI135NumberDensityM3",
    "maxI135NumberDensityM3",
    "meanXe135NumberDensityM3",
    "maxXe135NumberDensityM3",
    "meanDynamicAbsorptionGroup1PerM",
    "maxDynamicAbsorptionGroup1PerM",
    "meanDynamicAbsorptionGroup2PerM",
    "maxDynamicAbsorptionGroup2PerM",
  ]);
}

function isCanduBundleSnapshot(
  value: unknown,
  expectedPosition?: number,
): value is CanduBundleSnapshot {
  if (!isRecord(value) || !hasOwnProperties(value, [
    "position",
    "bundleId",
    "fuelTypeId",
    "currentBurnupMwdPerKg",
    "powerWatts",
    "localPowerFraction",
    "insertedAtSeconds",
    "stateVersion",
    "isFresh",
  ]) || !isNonNegativeInteger(value.position) ||
      (expectedPosition !== undefined && value.position !== expectedPosition) ||
      !hasStringFields(value, ["bundleId", "fuelTypeId"]) ||
      !hasFiniteNumberFields(value, [
        "currentBurnupMwdPerKg",
        "powerWatts",
        "localPowerFraction",
        "insertedAtSeconds",
      ]) || !hasNonNegativeIntegerFields(value, ["stateVersion"]) ||
      !hasBooleanFields(value, ["isFresh"])) {
    return false;
  }

  return true;
}

function isCanduChannelSnapshot(
  value: unknown,
  expectedChannelIndex: number,
): value is CanduChannelSnapshot {
  if (!isRecord(value) || !hasOwnProperties(value, [
    "channelIndex",
    "gridColumn",
    "gridRow",
    "flowDirection",
    "averageBurnupMwdPerKg",
    "powerWatts",
    "localPowerFraction",
    "localTiltFraction",
    "xenon",
    "bundles",
  ]) || value.channelIndex !== expectedChannelIndex ||
      !isNonNegativeInteger(value.gridColumn) || value.gridColumn >= CORE_GRID_WIDTH ||
      !isNonNegativeInteger(value.gridRow) || value.gridRow >= CORE_GRID_HEIGHT ||
      !isRefuellingDirection(value.flowDirection) ||
      !hasFiniteNumberFields(value, [
        "averageBurnupMwdPerKg",
        "powerWatts",
        "localPowerFraction",
        "localTiltFraction",
      ]) || !isCanduXenonChannelSnapshot(value.xenon, expectedChannelIndex) ||
      !Array.isArray(value.bundles) || value.bundles.length !== CORE_BUNDLE_POSITION_COUNT) {
    return false;
  }

  return value.bundles.every((bundle, position) =>
    isCanduBundleSnapshot(bundle, position));
}

function isCanduCoreSnapshot(value: unknown): value is CanduCoreSnapshot {
  if (!isRecord(value) || !hasOwnProperties(value, [
    "channelCount",
    "bundlePositionCount",
    "gridWidth",
    "gridHeight",
    "channels",
  ]) || value.channelCount !== CORE_CHANNEL_COUNT ||
      value.bundlePositionCount !== CORE_BUNDLE_POSITION_COUNT ||
      value.gridWidth !== CORE_GRID_WIDTH || value.gridHeight !== CORE_GRID_HEIGHT ||
      !Array.isArray(value.channels) || value.channels.length !== CORE_CHANNEL_COUNT) {
    return false;
  }

  return value.channels.every((channel, channelIndex) =>
    isCanduChannelSnapshot(channel, channelIndex));
}

function isCanduPhysicsSnapshot(value: unknown): value is CanduPhysicsSnapshot {
  if (!isRecord(value)) {
    return false;
  }

  if (!hasOwnProperties(value, [
    "sourceId",
    "formulationId",
    "shapeMethodId",
    "amplitudeMethodId",
    "reactivityMethodId",
    "solveState",
    "isAuthoritative",
    "bindingVersion",
    "referencePowerWatts",
    "powerAmplitude",
    "actualPowerFraction",
    "targetPowerWatts",
    "totalPowerWatts",
    "meanChannelPowerWatts",
    "meanBundlePowerWatts",
    "effectiveK",
    "reactivity",
    "staticReactivity",
    "staticReactivityMethodId",
    "weightedPerturbationReactivity",
    "reactivityNumerator",
    "reactivityDenominator",
    "reactivityIdentity",
    "reactivityBindingDigestHex",
    "coreReactivity",
    "compensatedNetReactivity",
    "compensationState",
    "compensationCommand",
    "compensationLowerBound",
    "compensationUpperBound",
    "compensationSaturated",
    "compensationResponseTimeSeconds",
    "cadenceIdentity",
    "powerBalanceRelativeError",
    "solverIdentity",
    "solverIterationCount",
    "solverResidualRelativeInfinity",
  ]) || !hasStringFields(value, [
    "sourceId",
    "formulationId",
    "shapeMethodId",
    "amplitudeMethodId",
    "reactivityMethodId",
    "solveState",
    "staticReactivityMethodId",
    "reactivityIdentity",
    "reactivityBindingDigestHex",
    "cadenceIdentity",
    "solverIdentity",
  ]) || !hasBooleanFields(value, ["isAuthoritative", "compensationSaturated"]) ||
      !hasNonNegativeIntegerFields(value, ["bindingVersion", "solverIterationCount"]) ||
      !hasFiniteNumberFields(value, [
        "referencePowerWatts",
        "powerAmplitude",
        "actualPowerFraction",
        "targetPowerWatts",
        "totalPowerWatts",
        "meanChannelPowerWatts",
        "meanBundlePowerWatts",
        "effectiveK",
        "reactivity",
        "staticReactivity",
        "weightedPerturbationReactivity",
        "reactivityNumerator",
        "reactivityDenominator",
        "coreReactivity",
        "compensatedNetReactivity",
        "compensationState",
        "compensationCommand",
        "compensationLowerBound",
        "compensationUpperBound",
        "compensationResponseTimeSeconds",
        "powerBalanceRelativeError",
        "solverResidualRelativeInfinity",
      ])) {
    return false;
  }

  return (!hasOwn(value, "adjointNormalizationIdentity") ||
      isString(value.adjointNormalizationIdentity)) &&
    (!hasOwn(value, "adjointDigestHex") || isString(value.adjointDigestHex)) &&
    (!hasOwn(value, "adjointIterationCount") || isNonNegativeInteger(value.adjointIterationCount)) &&
    (!hasOwn(value, "adjointTransposeResidualRelativeInfinity") ||
      isFiniteNumber(value.adjointTransposeResidualRelativeInfinity));
}

function isCanduXenonSnapshot(value: unknown): value is CanduXenonSnapshot {
  if (!isRecord(value)) {
    return false;
  }

  if (!hasOwnProperties(value, [
    "stateIdentity",
    "stateDigestHex",
    "stateVersion",
    "simulationTimeSeconds",
    "nodeCount",
    "couplingIdentity",
    "hasCoupling",
    "baseCoefficientDigestHex",
    "dynamicXenonDigestHex",
    "effectiveCoefficientDigestHex",
    "meanI135NumberDensityM3",
    "maxI135NumberDensityM3",
    "meanXe135NumberDensityM3",
    "maxXe135NumberDensityM3",
    "meanDynamicAbsorptionGroup1PerM",
    "maxDynamicAbsorptionGroup1PerM",
    "meanDynamicAbsorptionGroup2PerM",
    "maxDynamicAbsorptionGroup2PerM",
    "selectedChannelIndex",
    "selectedChannel",
  ]) || !hasStringFields(value, [
    "stateIdentity",
    "stateDigestHex",
    "couplingIdentity",
    "baseCoefficientDigestHex",
    "dynamicXenonDigestHex",
    "effectiveCoefficientDigestHex",
  ]) || !hasBooleanFields(value, ["hasCoupling"]) ||
      !hasNonNegativeIntegerFields(value, ["stateVersion", "nodeCount"]) ||
      !isFiniteNumber(value.simulationTimeSeconds) ||
      !hasFiniteNumberFields(value, [
        "meanI135NumberDensityM3",
        "maxI135NumberDensityM3",
        "meanXe135NumberDensityM3",
        "maxXe135NumberDensityM3",
        "meanDynamicAbsorptionGroup1PerM",
        "maxDynamicAbsorptionGroup1PerM",
        "meanDynamicAbsorptionGroup2PerM",
        "maxDynamicAbsorptionGroup2PerM",
      ]) || !isInteger(value.selectedChannelIndex) ||
      (value.selectedChannelIndex !== -1 && !isChannelIndex(value.selectedChannelIndex))) {
    return false;
  }

  return value.selectedChannel === null ||
    isCanduXenonChannelSnapshot(value.selectedChannel, value.selectedChannelIndex);
}

function isCanduRrsZoneSnapshot(
  value: unknown,
  expectedZoneId: number,
): value is CanduRrsZoneSnapshot {
  return isRecord(value) && hasOwnProperties(value, [
    "logicalZoneId",
    "fillFraction",
    "referencePowerFraction",
    "targetPowerFraction",
    "measuredPowerFraction",
    "shapeError",
  ]) && value.logicalZoneId === expectedZoneId &&
    hasFiniteNumberFields(value, [
      "fillFraction",
      "referencePowerFraction",
      "targetPowerFraction",
      "measuredPowerFraction",
      "shapeError",
    ]);
}

function isCanduRrsSnapshot(value: unknown): value is CanduRrsSnapshot {
  if (!isRecord(value) || !hasOwnProperties(value, [
    "controllerIdentity",
    "mappingIdentity",
    "mappingDigestHex",
    "overlayIdentity",
    "overlayDigestHex",
    "stateDigestHex",
    "simulationTimeSeconds",
    "nodeCount",
    "averageFillFraction",
    "minimumFillFraction",
    "maximumFillFraction",
    "measuredPowerWatts",
    "targetPowerWatts",
    "powerErrorWatts",
    "coreReactivity",
    "compensatedNetReactivity",
    "commonModeRhoCorrection",
    "controllerIterationCount",
    "controllerConverged",
    "responseModelIdentity",
    "responseModelDigestHex",
    "appliedFillCommand",
    "controlledBaselineWeightedResidual",
    "combinedWeightedResidual",
    "candidateSolveCount",
    "verificationSolveCount",
    "correctionSolveCount",
    "correctionApplied",
    "lowExhaustion",
    "highExhaustion",
    "isGameOver",
    "gameOverReason",
    "cadenceIdentity",
    "zones",
  ]) || !hasStringFields(value, [
    "controllerIdentity",
    "mappingIdentity",
    "mappingDigestHex",
    "overlayIdentity",
    "overlayDigestHex",
    "stateDigestHex",
    "responseModelIdentity",
    "responseModelDigestHex",
    "gameOverReason",
    "cadenceIdentity",
  ]) || !hasBooleanFields(value, [
    "controllerConverged",
    "correctionApplied",
    "lowExhaustion",
    "highExhaustion",
    "isGameOver",
  ]) || !hasNonNegativeIntegerFields(value, [
    "nodeCount",
    "controllerIterationCount",
    "candidateSolveCount",
    "verificationSolveCount",
    "correctionSolveCount",
  ]) || !hasFiniteNumberFields(value, [
    "simulationTimeSeconds",
    "averageFillFraction",
    "minimumFillFraction",
    "maximumFillFraction",
    "measuredPowerWatts",
    "targetPowerWatts",
    "powerErrorWatts",
    "coreReactivity",
    "compensatedNetReactivity",
    "commonModeRhoCorrection",
    "controlledBaselineWeightedResidual",
    "combinedWeightedResidual",
  ]) || !Array.isArray(value.appliedFillCommand) ||
      value.appliedFillCommand.length !== 14 ||
      !value.appliedFillCommand.every(isFiniteNumber) ||
      !Array.isArray(value.zones) ||
      (value.zones.length !== 0 && value.zones.length !== 14)) {
    return false;
  }

  return value.zones.every((zone, zoneIndex) =>
    isCanduRrsZoneSnapshot(zone, zoneIndex));
}

function isCanduConvergenceStatus(value: unknown): value is CanduConvergenceStatus {
  return isRecord(value) && hasOwnProperties(value, [
    "state",
    "iterations",
    "residual",
    "relativePowerError",
    "lastSolveMilliseconds",
    "solverLabel",
  ]) && (value.state === "converged" || value.state === "settling" ||
    value.state === "pending" || value.state === "unavailable") &&
    isNonNegativeInteger(value.iterations) &&
    isFiniteNumber(value.residual) &&
    isFiniteNumber(value.relativePowerError) &&
    isFiniteNumber(value.lastSolveMilliseconds) &&
    isString(value.solverLabel);
}

function isCanduDiagnosticCheck(value: unknown): boolean {
  return isRecord(value) && hasOwnProperties(value, ["label", "value", "status"]) &&
    isString(value.label) && isString(value.value) &&
    (value.status === "pass" || value.status === "watch" || value.status === "info");
}

function isCanduDiagnostics(value: unknown): value is CanduDiagnostics {
  return isRecord(value) && hasOwnProperties(value, ["convergence", "checks"]) &&
    isCanduConvergenceStatus(value.convergence) && Array.isArray(value.checks) &&
    value.checks.every(isCanduDiagnosticCheck);
}

function isCanduEvent(value: unknown): value is CanduEvent {
  return isRecord(value) && hasOwnProperties(value, [
    "eventId",
    "timeSeconds",
    "title",
    "detail",
    "tone",
  ]) && hasStringFields(value, ["eventId", "title", "detail"]) &&
    isFiniteNumber(value.timeSeconds) && isEventTone(value.tone);
}

function isLabBoundaryFace(value: unknown): value is LabBoundaryFace {
  return value === "north" || value === "east" || value === "south" ||
    value === "west" || value === "end-a" || value === "end-b";
}

function isCanduLabCellSnapshot(value: unknown): value is CanduLabCellSnapshot {
  if (!isRecord(value) || !hasOwnProperties(value, [
    "position",
    "hasFuel",
    "reflectiveFaces",
  ]) || !isNonNegativeInteger(value.position) ||
      !isBoolean(value.hasFuel) || !Array.isArray(value.reflectiveFaces) ||
      !value.reflectiveFaces.every(isLabBoundaryFace)) {
    return false;
  }

  return (!hasOwn(value, "channelIndex") || isNonNegativeInteger(value.channelIndex)) &&
    (!hasOwn(value, "materialId") || isString(value.materialId));
}

function isCanduLabBundleSnapshot(value: unknown): boolean {
  return isRecord(value) && hasOwnProperties(value, [
    "position",
    "bundleId",
    "fuelTypeId",
    "currentBurnupMwDayPerKg",
    "currentBurnupJPerKgHm",
    "insertedAtSeconds",
    "stateVersion",
  ]) && isNonNegativeInteger(value.position) &&
    hasStringFields(value, ["bundleId", "fuelTypeId"]) &&
    hasFiniteNumberFields(value, [
      "currentBurnupMwDayPerKg",
      "currentBurnupJPerKgHm",
      "insertedAtSeconds",
    ]) && hasNonNegativeIntegerFields(value, ["stateVersion"]);
}

function isCanduLabChannelSnapshot(value: unknown): value is CanduLabChannelSnapshot {
  if (!isRecord(value) || !hasOwnProperties(value, [
    "channelIndex",
    "coordinateX",
    "coordinateY",
    "flowDirection",
  ]) || !isNonNegativeInteger(value.channelIndex) ||
      !isInteger(value.coordinateX) || !isInteger(value.coordinateY) ||
      !isString(value.flowDirection)) {
    return false;
  }

  if (hasOwn(value, "bundles") &&
      (!Array.isArray(value.bundles) || !value.bundles.every(isCanduLabBundleSnapshot))) {
    return false;
  }
  if (hasOwn(value, "cells") &&
      (!Array.isArray(value.cells) || !value.cells.every(isCanduLabCellSnapshot))) {
    return false;
  }
  return hasOwn(value, "bundles") || hasOwn(value, "cells");
}

function isCanduLabSpatialSolveSnapshot(value: unknown): value is CanduLabSpatialSolveSnapshot {
  if (!isRecord(value) || !hasOwnProperties(value, [
    "status",
    "isConverged",
    "hasUsableState",
    "finalState",
    "diagnostics",
  ]) || !isString(value.status) || !isBoolean(value.isConverged) ||
      !isBoolean(value.hasUsableState) || !isRecord(value.diagnostics)) {
    return false;
  }

  const finalState = value.finalState;
  if (finalState !== null && (!isRecord(finalState) ||
      !hasOwnProperties(finalState, [
        "iteration",
        "eigenvalue",
        "totalPowerW",
        "group1Flux",
        "group2Flux",
      ]) || !isNonNegativeInteger(finalState.iteration) ||
      !hasFiniteNumberFields(finalState, ["eigenvalue", "totalPowerW"]) ||
      !Array.isArray(finalState.group1Flux) ||
      !Array.isArray(finalState.group2Flux) ||
      !finalState.group1Flux.every(isFiniteNumber) ||
      !finalState.group2Flux.every(isFiniteNumber))) {
    return false;
  }

  if (finalState !== null && hasOwn(finalState, "normalizationScale") &&
      !isFiniteNumber(finalState.normalizationScale)) {
    return false;
  }
  if (finalState !== null && hasOwn(finalState, "fissionProductionRate") &&
      !isFiniteNumber(finalState.fissionProductionRate)) {
    return false;
  }

  const diagnostics = value.diagnostics;
  return hasOwnProperties(diagnostics, ["iterationCount", "convergenceReason", "innerSolveStatus"]) &&
    isNonNegativeInteger(diagnostics.iterationCount) &&
    isString(diagnostics.convergenceReason) && isString(diagnostics.innerSolveStatus) &&
    [
      "eigenvalueChangeAbsolute",
      "eigenvalueChangeRelative",
      "residualAbsoluteInfinity",
      "residualRelativeInfinity",
      "sourceShapeChangeInfinity",
      "powerBalanceRelative",
    ].every((key) => !hasOwn(diagnostics, key) || diagnostics[key] === null || isFiniteNumber(diagnostics[key]));
}

function isCanduLabSnapshot(value: unknown): value is CanduLabSnapshot {
  if (!isRecord(value) || !hasOwnProperties(value, [
    "fixtureId",
    "simulationTimeSeconds",
    "freshBundlesAvailable",
    "refuellingOperationCount",
    "lastRefuelledChannel",
    "lastRefuellingDirectionId",
    "lastRefuellingShiftCount",
    "core",
    "spatialSolve",
  ]) || !isString(value.fixtureId) ||
      !hasFiniteNumberFields(value, ["simulationTimeSeconds"]) ||
      !isNonNegativeInteger(value.freshBundlesAvailable) ||
      !isNonNegativeInteger(value.refuellingOperationCount) ||
      !isInteger(value.lastRefuelledChannel) ||
      (value.lastRefuellingDirectionId !== null && !isString(value.lastRefuellingDirectionId)) ||
      !isNonNegativeInteger(value.lastRefuellingShiftCount) || !isRecord(value.core) ||
      !isString(value.core.fixtureId) || !isNonNegativeInteger(value.core.channelCount) ||
      !isNonNegativeInteger(value.core.bundlePositionCount) ||
      !Array.isArray(value.core.channels) ||
      !value.core.channels.every(isCanduLabChannelSnapshot) ||
      !isCanduLabSpatialSolveSnapshot(value.spatialSolve)) {
    return false;
  }

  if (hasOwn(value.core, "cells") &&
      (!Array.isArray(value.core.cells) || !value.core.cells.every(isCanduLabCellSnapshot))) {
    return false;
  }
  return true;
}

const SNAPSHOT_STATE_FIELDS = [
  "scenarioId",
  "dataPackId",
  "simulationTimeSeconds",
  "wallElapsedSeconds",
  "normalizedPowerFraction",
  "targetPowerFraction",
  "absoluteTiltFraction",
  "targetTiltFraction",
  "controlMarginFraction",
  "deviceAvailableFraction",
  "pendingActionCount",
  "scoreTotal",
  "scoreDelta",
  "isPaused",
  "playbackModeId",
  "freshBundlesAvailable",
  "refuellingOperationCount",
  "lastRefuelledChannel",
  "lastRefuellingDirectionId",
  "lastRefuellingShiftCount",
  "physics",
  "xenon",
  "rrs",
  "diagnostics",
  "lastEvent",
] as const;

function hasValidSnapshotStateFields(value: Record<string, unknown>): boolean {
  return hasOwnProperties(value, SNAPSHOT_STATE_FIELDS) &&
    hasStringFields(value, ["scenarioId", "dataPackId"]) &&
    hasFiniteNumberFields(value, [
      "simulationTimeSeconds",
      "wallElapsedSeconds",
      "normalizedPowerFraction",
      "targetPowerFraction",
      "absoluteTiltFraction",
      "targetTiltFraction",
      "controlMarginFraction",
      "deviceAvailableFraction",
      "scoreTotal",
      "scoreDelta",
    ]) && isNonNegativeInteger(value.pendingActionCount) &&
    isBoolean(value.isPaused) && isPlaybackModeId(value.playbackModeId) &&
    isNonNegativeInteger(value.freshBundlesAvailable) &&
    isNonNegativeInteger(value.refuellingOperationCount) &&
    isInteger(value.lastRefuelledChannel) &&
    value.lastRefuelledChannel >= -1 && value.lastRefuelledChannel < CORE_CHANNEL_COUNT &&
    (value.lastRefuellingDirectionId === null ||
      isRefuellingDirection(value.lastRefuellingDirectionId)) &&
    isRefuellingShiftCount(value.lastRefuellingShiftCount) &&
    (value.lastEvent === null || isCanduEvent(value.lastEvent));
}

function isRefuelRequest(value: unknown): value is RefuelRequest {
  return isRecord(value) && hasOwnProperties(value, [
    "channelIndex",
    "directionId",
    "shiftCount",
    "fuelTypeId",
  ]) && isChannelIndex(value.channelIndex) &&
    isRefuellingDirection(value.directionId) &&
    (value.shiftCount === 4 || value.shiftCount === 8) &&
    isString(value.fuelTypeId);
}

function isCanduCommand(value: unknown): value is CanduCommand {
  if (!isRecord(value) || !hasOwn(value, "type") || !isString(value.type)) {
    return false;
  }

  switch (value.type) {
    case "advance":
      return hasOwn(value, "wallMilliseconds") && isFiniteNumber(value.wallMilliseconds);
    case "step":
      return hasOwn(value, "simulationSeconds") && isFiniteNumber(value.simulationSeconds);
    case "set-playback-mode":
      return hasOwn(value, "modeId") && isPlaybackModeId(value.modeId);
    case "pause":
    case "resume":
    case "reset":
      return true;
    case "queue-power-target":
      return hasOwn(value, "targetFraction") && isFiniteNumber(value.targetFraction);
    case "queue-tilt-target":
      return hasOwn(value, "targetFraction") && isFiniteNumber(value.targetFraction);
    case "commit-refuel":
      return hasOwn(value, "request") && isRefuelRequest(value.request);
    case "configure-cell":
      return hasOwnProperties(value, [
        "channelIndex",
        "position",
        "hasFuel",
        "reflectiveFaces",
      ]) && isNonNegativeInteger(value.channelIndex) &&
        isNonNegativeInteger(value.position) && isBoolean(value.hasFuel) &&
        Array.isArray(value.reflectiveFaces) &&
        value.reflectiveFaces.every(isLabBoundaryFace);
    case "solve":
      return true;
    default:
      return false;
  }
}

function isProtocolDiagnostic(value: unknown): boolean {
  return isRecord(value) && hasOwnProperties(value, ["level", "code", "message"]) &&
    isDiagnosticLevel(value.level) && isString(value.code) && isString(value.message);
}

function isProtocolResponseEnvelope(value: unknown): value is Record<string, unknown> {
  if (!isRecord(value) || !hasOwnProperties(value, [
    "protocol",
    "accepted",
    "sequence",
    "command",
    "message",
    "diagnostics",
  ]) || value.protocol !== PROTOCOL_VERSION || !isBoolean(value.accepted) ||
      !isInteger(value.sequence) || !isCanduCommand(value.command) ||
      !isString(value.message) || !Array.isArray(value.diagnostics) ||
      !value.diagnostics.every(isProtocolDiagnostic)) {
    return false;
  }

  return (!hasOwn(value, "responseKind") || isResponseKind(value.responseKind)) &&
    (!hasOwn(value, "baseSequence") || isInteger(value.baseSequence)) &&
    (!hasOwn(value, "requiresResync") || isBoolean(value.requiresResync)) &&
    (!hasOwn(value, "stateDigest") || isString(value.stateDigest)) &&
    (!hasOwn(value, "replayDigest") || isString(value.replayDigest)) &&
    (!hasOwn(value, "coreReplacement") || value.coreReplacement === null ||
      isCanduCoreSnapshot(value.coreReplacement)) &&
    (!hasOwn(value, "snapshotPatch") || value.snapshotPatch === null ||
      isRecord(value.snapshotPatch)) &&
    (!hasOwn(value, "lab") || value.lab === null || isCanduLabSnapshot(value.lab)) &&
    (!hasOwn(value, "labPreview") || value.labPreview === null || isCanduLabSnapshot(value.labPreview));
}

function normalizeInitializeResponse(value: unknown): unknown {
  if (!isRecord(value) || value.operation !== "initialize" ||
      (hasOwn(value, "command") && value.command !== null)) {
    return value;
  }

  return { ...value, command: { type: "reset" } };
}

function normalizeInitialSnapshot(value: unknown): unknown {
  if (!isRecord(value) || hasOwn(value, "lastRefuellingDirectionId") ||
      value.refuellingOperationCount !== 0) {
    return value;
  }

  return { ...value, lastRefuellingDirectionId: null };
}

function isCanduSnapshotPatch(value: unknown): value is CanduSnapshotPatch {
  return isRecord(value) && hasValidSnapshotStateFields(value) &&
    isCanduPhysicsSnapshot(value.physics) && isCanduXenonSnapshot(value.xenon) &&
    isCanduRrsSnapshot(value.rrs) && isCanduDiagnostics(value.diagnostics);
}

export function isProtocolSnapshot(value: unknown): value is CanduSnapshot {
  if (!isRecord(value) || !hasOwnProperties(value, [
    "protocol",
    "source",
    "sequence",
    "core",
    ...SNAPSHOT_STATE_FIELDS,
  ]) || value.protocol !== PROTOCOL_VERSION || !isProtocolSource(value.source) ||
      !isNonNegativeInteger(value.sequence) || !hasValidSnapshotStateFields(value) ||
      !isCanduPhysicsSnapshot(value.physics) || !isCanduXenonSnapshot(value.xenon) ||
      !isCanduRrsSnapshot(value.rrs) || !isCanduDiagnostics(value.diagnostics) ||
      !isCanduCoreSnapshot(value.core) ||
      (hasOwn(value, "lab") && value.lab !== null && !isCanduLabSnapshot(value.lab))) {
    return false;
  }

  return !hasOwn(value, "lab") || value.lab === null || isRecord(value.lab);
}

export function parseProtocolSnapshot(raw: string | unknown): CanduSnapshot {
  const value = normalizeInitialSnapshot(
    unwrapPayload(parseJson(raw), "snapshot"),
  );
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
  const value = normalizeInitializeResponse(
    unwrapPayload(parseJson(raw), "response"),
  );
  if (!isProtocolResponseEnvelope(value)) {
    throw new Error("candu-playtest-v1 response envelope is malformed.");
  }

  const isCompact = value.responseKind === "compact" ||
    hasOwn(value, "snapshotPatch") ||
    hasOwn(value, "coreReplacement");
  if (isCompact) {
    if (value.requiresResync === true ||
        !isInteger(value.baseSequence) ||
        previousSnapshot === undefined ||
        value.baseSequence !== previousSnapshot.sequence) {
      if (resyncSnapshot !== undefined) {
        if (!isProtocolSnapshot(resyncSnapshot)) {
          throw new Error("candu-playtest-v1 resync snapshot is malformed.");
        }
        return {
          ...value,
          responseKind: "compact",
          snapshot: resyncSnapshot,
        } as unknown as CanduCommandResponse;
      }
      throw new ProtocolResyncRequiredError(value);
    }

    const patch = value.snapshotPatch;
    if (!isCanduSnapshotPatch(patch)) {
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
  const snapshot = normalizeInitialSnapshot(value.snapshot);
  if (!isProtocolSnapshot(snapshot)) {
    throw new Error("candu-playtest-v1 response snapshot is malformed.");
  }
  return { ...value, snapshot } as unknown as CanduCommandResponse;
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
  if (!isNonNegativeInteger(response.sequence) || !isCanduSnapshotPatch(patchValue)) {
    throw new Error("candu-playtest-v1 compact snapshot patch is malformed.");
  }

  let core = base.core;
  if (hasOwn(response, "coreReplacement")) {
    if (!isCanduCoreSnapshot(response.coreReplacement)) {
      if (response.coreReplacement !== null) {
        throw new Error("candu-playtest-v1 compact core replacement is malformed.");
      }
    } else {
      core = response.coreReplacement;
    }
  }

  const snapshot: CanduSnapshot = {
    ...base,
    sequence: response.sequence,
    ...patchValue,
    core,
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
