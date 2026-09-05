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
  bundles: CanduBundleSnapshot[];
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
  core: CanduCoreSnapshot;
  diagnostics: CanduDiagnostics;
  lastEvent: CanduEvent | null;
  lab?: CanduLabSnapshot;
}

export interface RefuelRequest {
  channelIndex: number;
  directionId: RefuellingDirection;
  shiftCount: 4 | 8;
  fuelTypeId: string;
}

export interface RefuelPreview {
  request: RefuelRequest;
  dischargeBurnupMwdPerKg: number;
  localPowerDeltaFraction: number;
  localTiltDeltaFraction: number;
  predictedReactivityDelta: number;
  projectedPowerFraction: number;
  projectedTiltFraction: number;
  projectedScoreDelta: number;
  insertedBundleIds: string[];
  dischargedBundleIds: string[];
}

export type CanduCommand =
  | { type: "advance"; wallMilliseconds: number }
  | { type: "step"; simulationSeconds: number }
  | { type: "set-playback-mode"; modeId: PlaybackModeId }
  | { type: "pause" }
  | { type: "resume" }
  | { type: "queue-power-target"; targetFraction: number }
  | { type: "queue-tilt-target"; targetFraction: number }
  | { type: "preview-refuel"; request: RefuelRequest }
  | { type: "commit-refuel"; request: RefuelRequest }
  | { type: "reset" };

export interface CanduCommandResponse {
  protocol: typeof PROTOCOL_VERSION;
  accepted: boolean;
  sequence: number;
  command: CanduCommand;
  message: string;
  diagnostics: Array<{
    level: DiagnosticLevel;
    code: string;
    message: string;
  }>;
  snapshot: CanduSnapshot;
  preview: RefuelPreview | null;
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
  dispatch(command: CanduCommand): BridgeResult<CanduCommandResponse>;
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

export function serializeProtocolCommand(command: CanduCommand): string {
  return canonicalJson({
    protocol: PROTOCOL_VERSION,
    type: "command",
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

export function parseProtocolSnapshot(raw: string | unknown): CanduSnapshot {
  const value = unwrapPayload(parseJson(raw), "snapshot");
  assertProtocol(value);
  if (!isRecord(value.core) || !Array.isArray(value.core.channels)) {
    throw new Error("candu-playtest-v1 snapshot is missing core channels.");
  }
  return value as unknown as CanduSnapshot;
}

export function parseProtocolResponse(raw: string | unknown): CanduCommandResponse {
  const value = unwrapPayload(parseJson(raw), "response");
  assertProtocol(value);
  if (!isRecord(value.snapshot)) {
    throw new Error("candu-playtest-v1 response is missing a snapshot.");
  }
  return value as unknown as CanduCommandResponse;
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
