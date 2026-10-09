export const PROTOCOL_VERSION = "candu-playtest-v2" as const;

export const CORE_CHANNEL_COUNT = 380 as const;
export const CORE_BUNDLE_POSITION_COUNT = 12 as const;
export const CORE_GRID_WIDTH = 22 as const;
export const CORE_GRID_HEIGHT = 22 as const;
export const BASE_CLOCK_SIMULATION_SECONDS_PER_WALL_SECOND = 1_800 as const;
export const BASE_CLOCK_WALL_SECONDS_PER_SIMULATION_HOUR = 2 as const;

export type ProtocolSource = "wasm" | "synthetic-fixture";
export type BridgeAvailability = ProtocolSource | "loading" | "unavailable";
export type BridgeModeId = "play";
export type PlaybackModeId = "pause" | "1x" | "10x" | "60x";
export type RefuellingDirection = "toward-end-a" | "toward-end-b";
export type CoreBoundaryFace = "north" | "east" | "south" | "west" | "end-a" | "end-b";
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
  /** Live cell geometry projected by the authoritative GameSession. */
  hasFuel: boolean;
  reflectiveFaces: CoreBoundaryFace[];
  group1Flux: number;
  group2Flux: number;
  /** Optional only for older fixtures; WASM supplies authoritative zone geometry. */
  logicalZoneId?: number;
  absorberZoneId?: number;
  group1AbsorptionPerMPerFillFraction?: number;
  group2AbsorptionPerMPerFillFraction?: number;
}

export interface ZoneNodeBinding {
  channelIndex: number;
  position: number;
  logicalZoneId: number;
  absorberZoneId: number;
  /** Full-water footprint strengths; localized tubes also have a moving water surface in Core. */
  group1AbsorptionPerMPerFillFraction: number;
  group2AbsorptionPerMPerFillFraction: number;
}

export interface CanduChannelSnapshot {
  canRefuel?: boolean;
  refuellingIneligibilityReason?: string;
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
  /** Current atoms/m³, indexed by channelIndex * 12 + bundle position. Optional for older v2 hosts. */
  nodeI135NumberDensityM3?: number[];
  nodeXe135NumberDensityM3?: number[];
  coupledSimulationTimeSeconds?: number;
  coupledStateDigestHex?: string;
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
  meanI135NumberDensityM3?: number;
  meanXe135NumberDensityM3?: number;
  logicalZoneId: number;
  fillFraction: number;
  referencePowerFraction: number;
  targetPowerFraction: number;
  measuredPowerFraction: number;
  shapeError: number;
}

export interface CanduRrsSnapshot {
  decisionCode?: string;
  decisionExplanation?: string;
  limitingZoneId?: number;
  absorptionReferenceFillFraction?: number;
  calibratedTotalZoneWorthMk?: number;
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
  electricalPowerWatts?: number;
  meanChannelPowerWatts: number;
  meanBundlePowerWatts: number;
  effectiveK: number;
  reactivity: number;
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

/** Fixed inserted devices and their homogenized fuel-cell overlaps, supplied by Game/Core. */
export interface CanduAdjusterSnapshot {
  id: number;
  gridColumn: number;
  gridRowStart: number;
  gridRowEnd: number;
  axialPosition: number;
  affectedChannels: number[];
  bundlePositions: number[];
}

export interface CanduCoreSnapshot {
  liquidZoneTubes?: CanduLiquidZoneTubeSnapshot[];
  /** Absent in older bridges; an empty list explicitly means no inserted adjusters. */
  adjusters?: CanduAdjusterSnapshot[];
  channelCount: typeof CORE_CHANNEL_COUNT;
  bundlePositionCount: typeof CORE_BUNDLE_POSITION_COUNT;
  gridWidth: typeof CORE_GRID_WIDTH;
  gridHeight: typeof CORE_GRID_HEIGHT;
  channels: CanduChannelSnapshot[];
}

export interface CanduLiquidZoneTubeSnapshot extends Omit<CanduAdjusterSnapshot, "id"> {
  zoneId: number;
}

export type ShiftId = "free-practice" | "useful-fuel-day-v1";
export interface ShiftProgress {
  id: ShiftId;
  seed: number;
  title: string;
  objective: string;
  horizonSeconds: number;
  remainingSeconds: number;
  isEndless?: boolean;
  unlimitedFreshFuel?: boolean;
  fuelBudget: number;
  fuelConsumed: number;
  usefulBundlesDischarged: number;
  usefulBundlesRequired: number;
  usefulBurnupThresholdMwdPerKg: number;
  thermalEnergyMwh: number;
  electricalEnergyMwhEstimate: number;
  dischargeReward: number;
  freshFuelCost: number;
  operatingPoints: number;
  outcome: "in-progress" | "success" | "missed" | "ended";
  reward: string;
  rewardEarned: boolean;
}

export interface RefuellingPlan {
  directionId: RefuellingDirection; shiftCount: 4 | 8; incomingEnd: "A" | "B"; outgoingEnd: "A" | "B";
  insertedPositions: number[]; dischargedPositions: number[]; retainedFromPositions: number[]; retainedToPositions: number[];
}
export interface FuelMovement {
  operationId: number; channelIndex: number; plan: RefuellingPlan;
  score: { policyId: string; dischargeReward: number; freshFuelCost: number; netPoints: number };
  bundles: { bundleId: string; beforePosition?: number | null; afterPosition?: number | null; burnupMwdPerKg: number }[];
}

export interface RunProvenance {
  kind: "standard-challenge" | "free-practice" | "modified-sandbox";
  label: string; isModified: boolean; eligibleForStandardChallenge: boolean; reasons: string[];
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
  axialTiltFraction: number;
  rrsReserveFraction: number;
  deviceAvailableFraction: number;
  pendingActionCount: number;
  /** Optional for older v2 hosts. All objective rules come from Game. */
  shift?: ShiftProgress;
  provenance?: RunProvenance;
  refuellingPlans?: RefuellingPlan[];
  lastFuelMovement?: FuelMovement | null;
  scorePolicyId?: string;
  ripple?: ChannelRippleSnapshot;
  lastRefuellingScore?: { policyId: string; dischargeReward: number; freshFuelCost: number; netPoints: number } | null;
  scoreTotal: number;
  scoreDelta: number;
  isPaused: boolean;
  /** Optional only for older v2 hosts. New hosts publish authoritative run state. */
  runStatus?: "running" | "paused" | "completed" | "ended";
  runEndReason?: string;
  playbackModeId: PlaybackModeId;
  freshBundlesAvailable: number;
  refuellingOperationCount: number;
  /** Null until fuel is discharged; optional for older fixture snapshots. */
  lastDischargedMaximumBurnupMwdPerKg?: number | null;
  maximumDischargedBurnupMwdPerKg?: number | null;
  lastRefuelledChannel: number;
  lastRefuellingDirectionId: RefuellingDirection | null;
  lastRefuellingShiftCount: number;
  physics: CanduPhysicsSnapshot;
  xenon: CanduXenonSnapshot;
  rrs: CanduRrsSnapshot;
  core: CanduCoreSnapshot;
  diagnostics: CanduDiagnostics;
  lastEvent: CanduEvent | null;
}

export interface ChannelRippleSnapshot {
  referenceId: string;
  dataPackVersion: string;
  coefficientBindingDigestHex: string;
  referenceThermalPowerWatts: number;
  maximumChannelPowerWatts?: number;
  maximumBundlePowerWatts?: number;
  referenceChannelPowerWatts: number[];
  channelRippleFractions: number[];
  rmsDeviationFraction: number;
  pointsPerHour: number;
}

export type CanduSnapshotPatch = Pick<CanduSnapshot,
  | "scenarioId"
  | "dataPackId"
  | "simulationTimeSeconds"
  | "wallElapsedSeconds"
  | "normalizedPowerFraction"
  | "targetPowerFraction"
  | "axialTiltFraction"
  | "rrsReserveFraction"
  | "deviceAvailableFraction"
  | "pendingActionCount"
  | "shift"
  | "provenance"
  | "refuellingPlans"
  | "lastFuelMovement"
  | "scorePolicyId"
  | "ripple"
  | "lastRefuellingScore"
  | "scoreTotal"
  | "scoreDelta"
  | "isPaused"
  | "runStatus"
  | "runEndReason"
  | "playbackModeId"
  | "freshBundlesAvailable"
  | "refuellingOperationCount"
  | "lastDischargedMaximumBurnupMwdPerKg"
  | "maximumDischargedBurnupMwdPerKg"
  | "lastRefuelledChannel"
  | "lastRefuellingDirectionId"
  | "lastRefuellingShiftCount"
  | "physics"
  | "xenon"
  | "rrs"
  | "diagnostics"
  | "lastEvent">;

/** Run completion is separate from physical RRS exhaustion. */
export function isRunTerminal(snapshot: CanduSnapshot): boolean {
  return snapshot.runStatus === "completed" || snapshot.runStatus === "ended" || snapshot.rrs.isGameOver;
}

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
  | { type: "commit-refuel"; request: RefuelRequest }
  | { type: "configure-cell"; channelIndex: number; position: number; hasFuel: boolean; reflectiveFaces: CoreBoundaryFace[] }
  | { type: "configure-zone-layout"; nodes: ZoneNodeBinding[] }
  | { type: "solve" }
  | { type: "reset"; seed?: number; shiftId?: ShiftId };

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
  replayDigestAlgorithm?: string;
  diagnostics: Array<{
    level: DiagnosticLevel;
    code: string;
    message: string;
  }>;
  snapshot: CanduSnapshot;
}

export interface CanduPlaytestWasmExports {
  getCapabilities?: () => string | Promise<string>;
  initialize?: (requestJson: string) => string | Promise<string>;
  getSnapshotJson: () => string;
  dispatchJson: (commandJson: string) => string | Promise<string>;
  dispatchProfileJson?: (commandJson: string) => string | Promise<string>;
  getGpuPrototypeFixtureJson?: (requestJson: string) => string | Promise<string>;
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
    throw new Error(`candu-playtest-v2 JSON parse failed: ${message}`);
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
    "hasFuel",
    "reflectiveFaces",
    "group1Flux",
    "group2Flux",
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

  return isBoolean(value.hasFuel) &&
    Array.isArray(value.reflectiveFaces) && value.reflectiveFaces.every(isCoreBoundaryFace) &&
    isFiniteNumber(value.group1Flux) && isFiniteNumber(value.group2Flux) &&
    (value.logicalZoneId === undefined || isZoneNodeBinding({ ...value, channelIndex: 0 }));
}

function isZoneNodeBinding(value: unknown): value is ZoneNodeBinding {
  return isRecord(value) && isNonNegativeInteger(value.channelIndex) && value.channelIndex < 380 &&
    isNonNegativeInteger(value.position) && value.position < 12 &&
    isNonNegativeInteger(value.logicalZoneId) && value.logicalZoneId < 14 &&
    isNonNegativeInteger(value.absorberZoneId) && value.absorberZoneId < 14 &&
    isFiniteNumber(value.group1AbsorptionPerMPerFillFraction) && value.group1AbsorptionPerMPerFillFraction >= 0 && value.group1AbsorptionPerMPerFillFraction <= 1 &&
    isFiniteNumber(value.group2AbsorptionPerMPerFillFraction) && value.group2AbsorptionPerMPerFillFraction >= 0 && value.group2AbsorptionPerMPerFillFraction <= 1;
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

  return (!hasOwn(value, "canRefuel") || isBoolean(value.canRefuel)) &&
    (!hasOwn(value, "refuellingIneligibilityReason") || isString(value.refuellingIneligibilityReason)) &&
    value.bundles.every((bundle, position) =>
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

  return (value.liquidZoneTubes === undefined || Array.isArray(value.liquidZoneTubes) &&
    value.liquidZoneTubes.every(t => isRecord(t) && isNonNegativeInteger(t.zoneId) && t.zoneId < 14 &&
      isCanduAdjusterSnapshot({ ...t, id: t.zoneId + 1 })) &&
    new Set(value.liquidZoneTubes.map(t => t.zoneId)).size === value.liquidZoneTubes.length) &&
    (value.adjusters === undefined || Array.isArray(value.adjusters) &&
    value.adjusters.every(isCanduAdjusterSnapshot) &&
    new Set(value.adjusters.map(rod => rod.id)).size === value.adjusters.length) &&
    value.channels.every((channel, channelIndex) =>
    isCanduChannelSnapshot(channel, channelIndex));
}

function isCanduAdjusterSnapshot(value: unknown): value is CanduAdjusterSnapshot {
  return isRecord(value) && isNonNegativeInteger(value.id) && value.id > 0 &&
    isFiniteNumber(value.gridColumn) && value.gridColumn >= -0.5 && value.gridColumn <= 21.5 &&
    isFiniteNumber(value.gridRowStart) && isFiniteNumber(value.gridRowEnd) &&
    value.gridRowStart >= -0.5 && value.gridRowEnd <= 21.5 && value.gridRowStart < value.gridRowEnd &&
    isFiniteNumber(value.axialPosition) && value.axialPosition >= -0.5 && value.axialPosition <= 11.5 &&
    Array.isArray(value.affectedChannels) && value.affectedChannels.length > 0 && value.affectedChannels.every(isChannelIndex) &&
    new Set(value.affectedChannels).size === value.affectedChannels.length &&
    Array.isArray(value.bundlePositions) && value.bundlePositions.length > 0 &&
    value.bundlePositions.every(p => isNonNegativeInteger(p) && p < 12) &&
    new Set(value.bundlePositions).size === value.bundlePositions.length;
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

  return (!hasOwn(value, "electricalPowerWatts") ||
      (isFiniteNumber(value.electricalPowerWatts) && value.electricalPowerWatts >= 0)) &&
    (!hasOwn(value, "adjointNormalizationIdentity") ||
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
      (value.coupledSimulationTimeSeconds !== undefined &&
        (!isFiniteNumber(value.coupledSimulationTimeSeconds) || value.coupledSimulationTimeSeconds < 0 ||
          value.coupledSimulationTimeSeconds > value.simulationTimeSeconds)) ||
      (value.coupledStateDigestHex !== undefined && typeof value.coupledStateDigestHex !== "string") ||
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

  const hasInventories = value.nodeI135NumberDensityM3 !== undefined || value.nodeXe135NumberDensityM3 !== undefined;
  if (hasInventories && (![value.nodeI135NumberDensityM3, value.nodeXe135NumberDensityM3].every(inventory =>
    Array.isArray(inventory) && inventory.length === CORE_CHANNEL_COUNT * CORE_BUNDLE_POSITION_COUNT &&
      inventory.length === value.nodeCount && inventory.every(density => isFiniteNumber(density) && density >= 0)))) return false;
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
    ["meanI135NumberDensityM3", "meanXe135NumberDensityM3"].every(field =>
      value[field] === undefined || (typeof value[field] === "number" && Number.isFinite(value[field]) && value[field] >= 0)) &&
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

  if (value.absorptionReferenceFillFraction !== undefined &&
      (!isFiniteNumber(value.absorptionReferenceFillFraction) ||
       value.absorptionReferenceFillFraction < 0 || value.absorptionReferenceFillFraction > 1)) return false;
  if (value.calibratedTotalZoneWorthMk !== undefined &&
      (!isFiniteNumber(value.calibratedTotalZoneWorthMk) || value.calibratedTotalZoneWorthMk <= 0)) return false;

  if (value.decisionCode !== undefined && (!isString(value.decisionCode) || ![
    "already-balanced", "command-applied", "command-retained", "correction-applied", "retained-best", "fill-limits", "event-limit",
    "exhausted-empty", "exhausted-full", "initial-reference"].includes(value.decisionCode))) return false;
  if (value.decisionExplanation !== undefined && !isString(value.decisionExplanation)) return false;
  if (value.limitingZoneId !== undefined && (!isNonNegativeInteger(value.limitingZoneId) || value.limitingZoneId >= 14)) return false;
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

function isCoreBoundaryFace(value: unknown): value is CoreBoundaryFace {
  return value === "north" || value === "east" || value === "south" ||
    value === "west" || value === "end-a" || value === "end-b";
}

const SNAPSHOT_STATE_FIELDS = [
  "scenarioId",
  "dataPackId",
  "simulationTimeSeconds",
  "wallElapsedSeconds",
  "normalizedPowerFraction",
  "targetPowerFraction",
  "axialTiltFraction",
  "rrsReserveFraction",
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

function isChannelRipple(value: unknown): boolean {
  return isRecord(value) && isString(value.referenceId) && value.referenceId.length > 0 &&
    isString(value.dataPackVersion) && /^[0-9a-f]{64}$/.test(value.coefficientBindingDigestHex as string) &&
    isFiniteNumber(value.referenceThermalPowerWatts) && value.referenceThermalPowerWatts > 0 &&
    (!hasOwn(value, "maximumChannelPowerWatts") || isFiniteNumber(value.maximumChannelPowerWatts) && value.maximumChannelPowerWatts > 0) &&
    (!hasOwn(value, "maximumBundlePowerWatts") || isFiniteNumber(value.maximumBundlePowerWatts) && value.maximumBundlePowerWatts > 0) &&
    isFiniteNumber(value.rmsDeviationFraction) && value.rmsDeviationFraction >= 0 &&
    isFiniteNumber(value.pointsPerHour) && value.pointsPerHour >= 0 && value.pointsPerHour <= 1 &&
    Array.isArray(value.referenceChannelPowerWatts) && value.referenceChannelPowerWatts.length === CORE_CHANNEL_COUNT &&
    value.referenceChannelPowerWatts.every(p => isFiniteNumber(p) && p > 0) &&
    Array.isArray(value.channelRippleFractions) && value.channelRippleFractions.length === CORE_CHANNEL_COUNT &&
    value.channelRippleFractions.every(p => isFiniteNumber(p) && p >= 0);
}

function isRefuellingScore(value: unknown): boolean {
  return isRecord(value) && isString(value.policyId) && value.policyId.length > 0 &&
    hasFiniteNumberFields(value, ["dischargeReward", "freshFuelCost", "netPoints"]) &&
    (value.dischargeReward as number) >= 0 && (value.freshFuelCost as number) >= 0;
}

function isRefuellingPlan(value: unknown): boolean {
  return isRecord(value) && isRefuellingDirection(value.directionId) && [4, 8].includes(value.shiftCount as number) &&
    ["A", "B"].includes(value.incomingEnd as string) && ["A", "B"].includes(value.outgoingEnd as string) &&
    ["insertedPositions", "dischargedPositions", "retainedFromPositions", "retainedToPositions"].every(key =>
      Array.isArray(value[key]) && (value[key] as unknown[]).every(p => isNonNegativeInteger(p) && (p as number) < 12));
}
function isFuelMovement(value: unknown): boolean {
  return isRecord(value) && isNonNegativeInteger(value.operationId) && isNonNegativeInteger(value.channelIndex) &&
    (value.channelIndex as number) < CORE_CHANNEL_COUNT && isRefuellingPlan(value.plan) && isRefuellingScore(value.score) &&
    Array.isArray(value.bundles) && value.bundles.every(b => isRecord(b) && isString(b.bundleId) &&
      isFiniteNumber(b.burnupMwdPerKg) && b.burnupMwdPerKg >= 0 &&
      ["beforePosition", "afterPosition"].every(k => b[k] == null || isNonNegativeInteger(b[k]) && (b[k] as number) < 12));
}

function isRunProvenance(value: unknown): boolean {
  return isRecord(value) && ["standard-challenge", "free-practice", "modified-sandbox"].includes(value.kind as string) &&
    isString(value.label) && isBoolean(value.isModified) && isBoolean(value.eligibleForStandardChallenge) &&
    Array.isArray(value.reasons) && value.reasons.every(r => isString(r) && r.length > 0) &&
    value.isModified === (value.reasons.length > 0) && value.isModified === (value.kind === "modified-sandbox") &&
    value.eligibleForStandardChallenge === (value.kind === "standard-challenge");
}

function isShiftProgress(value: unknown): value is ShiftProgress {
  return isRecord(value) && ["free-practice", "useful-fuel-day-v1"].includes(value.id as string) &&
    (value.isEndless === undefined || isBoolean(value.isEndless)) &&
    (value.unlimitedFreshFuel === undefined || isBoolean(value.unlimitedFreshFuel)) &&
    (value.isEndless !== true || (value.horizonSeconds === 0 && value.remainingSeconds === 0)) &&
    (value.unlimitedFreshFuel !== true || value.fuelBudget === 0) &&
    hasStringFields(value, ["title", "objective", "reward"]) &&
    hasFiniteNumberFields(value, ["horizonSeconds", "remainingSeconds", "usefulBurnupThresholdMwdPerKg",
      "thermalEnergyMwh", "electricalEnergyMwhEstimate", "dischargeReward", "freshFuelCost", "operatingPoints"]) &&
    ["seed", "fuelBudget", "fuelConsumed", "usefulBundlesDischarged", "usefulBundlesRequired"].every(key => isNonNegativeInteger(value[key])) &&
    ["horizonSeconds", "remainingSeconds", "usefulBurnupThresholdMwdPerKg", "thermalEnergyMwh", "electricalEnergyMwhEstimate",
      "dischargeReward", "freshFuelCost"].every(key => (value[key] as number) >= 0) &&
    ["in-progress", "success", "missed", "ended"].includes(value.outcome as string) && isBoolean(value.rewardEarned);
}

function hasValidSnapshotStateFields(value: Record<string, unknown>): boolean {
  return hasOwnProperties(value, SNAPSHOT_STATE_FIELDS) &&
    hasStringFields(value, ["scenarioId", "dataPackId"]) &&
    hasFiniteNumberFields(value, [
      "simulationTimeSeconds",
      "wallElapsedSeconds",
      "normalizedPowerFraction",
      "targetPowerFraction",
      "axialTiltFraction",
      "rrsReserveFraction",
      "deviceAvailableFraction",
      "scoreTotal",
      "scoreDelta",
    ]) && isNonNegativeInteger(value.pendingActionCount) &&
    (!hasOwn(value, "shift") || isShiftProgress(value.shift)) &&
    (!hasOwn(value, "provenance") || isRunProvenance(value.provenance)) &&
    (!hasOwn(value, "refuellingPlans") || Array.isArray(value.refuellingPlans) && value.refuellingPlans.every(isRefuellingPlan)) &&
    (!hasOwn(value, "lastFuelMovement") || value.lastFuelMovement === null || isFuelMovement(value.lastFuelMovement)) &&
    (!hasOwn(value, "scorePolicyId") || isString(value.scorePolicyId) && value.scorePolicyId.length > 0) &&
    (!hasOwn(value, "ripple") || isChannelRipple(value.ripple)) &&
    (!hasOwn(value, "lastRefuellingScore") || value.lastRefuellingScore === null ||
      isRefuellingScore(value.lastRefuellingScore)) &&
    isBoolean(value.isPaused) && isPlaybackModeId(value.playbackModeId) &&
    (!hasOwn(value, "runStatus") || ["running", "paused", "completed", "ended"].includes(value.runStatus as string)) &&
    (!hasOwn(value, "runEndReason") || isString(value.runEndReason)) &&
    isNonNegativeInteger(value.freshBundlesAvailable) &&
    isNonNegativeInteger(value.refuellingOperationCount) &&
    ["lastDischargedMaximumBurnupMwdPerKg", "maximumDischargedBurnupMwdPerKg"].every(key =>
      !hasOwn(value, key) || value[key] === null || isFiniteNumber(value[key]) && value[key] >= 0) &&
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
      return true;
    case "reset":
      return (value.seed === undefined || (isNonNegativeInteger(value.seed) && value.seed <= 4294967295)) &&
        (value.shiftId === undefined || ["free-practice", "useful-fuel-day-v1"].includes(value.shiftId as string));
    case "queue-power-target":
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
        value.reflectiveFaces.every(isCoreBoundaryFace);
    case "configure-zone-layout":
      return Array.isArray(value.nodes) && value.nodes.length === 4560 && value.nodes.every(isZoneNodeBinding);
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

  return !hasOwn(value, "lab") && !hasOwn(value, "labPreview") &&
    (!hasOwn(value, "responseKind") || isResponseKind(value.responseKind)) &&
    (!hasOwn(value, "baseSequence") || isInteger(value.baseSequence)) &&
    (!hasOwn(value, "requiresResync") || isBoolean(value.requiresResync)) &&
    (!hasOwn(value, "stateDigest") || isString(value.stateDigest)) &&
    (!hasOwn(value, "replayDigest") || isString(value.replayDigest)) &&
    (!hasOwn(value, "replayDigestAlgorithm") || isString(value.replayDigestAlgorithm)) &&
    (!hasOwn(value, "coreReplacement") || value.coreReplacement === null ||
      isCanduCoreSnapshot(value.coreReplacement)) &&
    (!hasOwn(value, "snapshotPatch") || value.snapshotPatch === null ||
      isRecord(value.snapshotPatch));
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
      !isCanduCoreSnapshot(value.core) || hasOwn(value, "lab")) {
    return false;
  }

  return true;
}

export function parseProtocolSnapshot(raw: string | unknown): CanduSnapshot {
  const value = normalizeInitialSnapshot(
    unwrapPayload(parseJson(raw), "snapshot"),
  );
  if (!isProtocolSnapshot(value)) {
    throw new Error("candu-playtest-v2 snapshot is malformed or incomplete.");
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
    throw new Error("candu-playtest-v2 response envelope is malformed.");
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
          throw new Error("candu-playtest-v2 resync snapshot is malformed.");
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
      throw new Error("candu-playtest-v2 compact response is missing snapshotPatch.");
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
    throw new Error("candu-playtest-v2 response is missing a snapshot.");
  }
  const snapshot = normalizeInitialSnapshot(value.snapshot);
  if (!isProtocolSnapshot(snapshot)) {
    throw new Error("candu-playtest-v2 response snapshot is malformed.");
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
    throw new Error("candu-playtest-v2 compact snapshot patch is malformed.");
  }

  let core = base.core;
  if (hasOwn(response, "coreReplacement")) {
    if (!isCanduCoreSnapshot(response.coreReplacement)) {
      if (response.coreReplacement !== null) {
        throw new Error("candu-playtest-v2 compact core replacement is malformed.");
      }
    } else {
      core = response.coreReplacement;
    }
  }

  if (response.coreMeasurements !== undefined && response.coreMeasurements !== null) {
    const readings = response.coreMeasurements;
    const count = CORE_CHANNEL_COUNT * CORE_BUNDLE_POSITION_COUNT;
    if (!isRecord(readings) || !Array.isArray(readings.bundleBurnupMwdPerKg) || readings.bundleBurnupMwdPerKg.length !== count ||
        !readings.bundleBurnupMwdPerKg.every(v => isFiniteNumber(v) && v >= 0) ||
        !Array.isArray(readings.bundleStateVersions) || readings.bundleStateVersions.length !== count || !readings.bundleStateVersions.every(isNonNegativeInteger) ||
        !Array.isArray(readings.bundleIsFresh) || readings.bundleIsFresh.length !== count || !readings.bundleIsFresh.every(isBoolean) ||
        !Array.isArray(readings.channelAverageBurnupMwdPerKg) || readings.channelAverageBurnupMwdPerKg.length !== CORE_CHANNEL_COUNT || !readings.channelAverageBurnupMwdPerKg.every(v => isFiniteNumber(v) && v >= 0))
      throw new Error("candu-playtest-v2 compact core measurements are malformed.");
    const burnup = readings.bundleBurnupMwdPerKg as number[], versions = readings.bundleStateVersions as number[];
    const fresh = readings.bundleIsFresh as boolean[], averageBurnup = readings.channelAverageBurnupMwdPerKg as number[];
    core = { ...core, channels: core.channels.map(channel => {
      const bundles = channel.bundles.map(bundle => {
        const index = channel.channelIndex * CORE_BUNDLE_POSITION_COUNT + bundle.position;
        return { ...bundle, currentBurnupMwdPerKg: burnup[index], stateVersion: versions[index], isFresh: fresh[index] };
      });
      return { ...channel, bundles, averageBurnupMwdPerKg: averageBurnup[channel.channelIndex] };
    }) };
  }

  const snapshot: CanduSnapshot = {
    ...base,
    sequence: response.sequence,
    ...patchValue,
    core,
  };

  if (!isProtocolSnapshot(snapshot)) {
    throw new Error("candu-playtest-v2 compact snapshot patch produced an invalid snapshot.");
  }
  return snapshot;
}

export function parseReplayArchive(raw: string | unknown): CanduReplayArchive {
  const value = parseJson(raw);
  assertProtocol(value);
  if (value.kind !== "command-replay" || !Array.isArray(value.commands)) {
    throw new Error("Expected a candu-playtest-v2 command replay archive.");
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
        ...(typeof candidate.dispatchProfileJson === "function" ? { dispatchProfileJson: candidate.dispatchProfileJson as CanduPlaytestWasmExports["dispatchProfileJson"] } : {}),
        ...(typeof candidate.getGpuPrototypeFixtureJson === "function" ? { getGpuPrototypeFixtureJson: candidate.getGpuPrototypeFixtureJson as CanduPlaytestWasmExports["getGpuPrototypeFixtureJson"] } : {}),
      };
    }
  }

  return null;
}
