import { isRunTerminal, type CanduCommand, type CanduCommandResponse, type CanduSnapshot, type PacingMode } from './protocol';

export const SAVE_VERSION = 2;
export const MAX_SAVE_COMMANDS = 100_000;
export const MAX_SAVE_BYTES = 12_000_000;
export interface RunSave {
  version: 1 | 2;
  pacingMode?: PacingMode;
  dailyIntegrationId?: string;
  draftChannels?: number[];
  id: string;
  savedAt: string;
  dataPackId: string;
  scorePolicyId: string;
  seed: number;
  shiftId: string;
  score: number;
  seconds: number;
  fuelConsumed: number;
  ended: boolean;
  commands: { command: CanduCommand; accepted: boolean; responseMode: 'full' | 'compact' }[];
  stateDigest: string;
  cloudRevision?: number;
  cloudOwnerId?: string;
}

/** Records the exact bridge inputs. Never integrates time or recreates reactor rules. */
export class RunJournal {
  id: string = crypto.randomUUID();
  private commands: RunSave['commands'] = [];
  private digest = '';
  private invalid = false;
  private cloudRevision?: number;
  private cloudOwnerId?: string;
  get hasCommands(): boolean { return this.commands.length > 0; }

  record(command: CanduCommand, response: CanduCommandResponse, responseMode: 'full' | 'compact'): void {
    if (command.type === 'reset' && response.accepted) {
      this.clear();
      // Make an implicit reset seed/objective explicit for a newly initialized worker.
      command = { type: 'reset', seed: response.snapshot.shift?.seed, shiftId: response.snapshot.shift?.id, ...(response.snapshot.pacingMode === "daily-turn" ? { pacingMode: "daily-turn" as const } : {}) };
    }
    if (command.type === 'configure-cell' || command.type === 'configure-zone-layout') this.invalid = true;
    if (this.commands.length >= MAX_SAVE_COMMANDS) { this.invalid = true; return; }
    this.commands.push({ command: structuredClone(command), accepted: response.accepted, responseMode });
    this.digest = response.stateDigest ?? '';
  }

  clear(): void { this.id = crypto.randomUUID(); this.commands = []; this.digest = ''; this.invalid = false; this.cloudRevision = undefined; this.cloudOwnerId = undefined; }

  markSynced(save: RunSave): void {
    if (this.id === save.id) { this.cloudRevision = save.cloudRevision; this.cloudOwnerId = save.cloudOwnerId; }
  }

  capture(snapshot: CanduSnapshot, draftChannels: readonly number[] = []): RunSave {
    if (this.invalid || snapshot.provenance?.isModified) throw new Error('This run cannot be saved (modified run or replay limit reached).');
    if (!this.digest || !snapshot.shift || !snapshot.scorePolicyId) throw new Error('Play or pause the run before saving.');
    const save: RunSave = {
      version: SAVE_VERSION, pacingMode: snapshot.pacingMode ?? "real-time", draftChannels: [...draftChannels], id: this.id, savedAt: new Date().toISOString(),
      dailyIntegrationId: snapshot.pacingMode === "daily-turn" ? snapshot.physics.cadenceIdentity : undefined,
      dataPackId: snapshot.dataPackId, scorePolicyId: snapshot.scorePolicyId,
      seed: snapshot.shift.seed, shiftId: snapshot.shift.id, score: snapshot.scoreTotal,
      seconds: snapshot.simulationTimeSeconds, fuelConsumed: snapshot.shift.fuelConsumed,
      ended: isRunTerminal(snapshot), commands: structuredClone(this.commands), stateDigest: this.digest,
      cloudRevision: this.cloudRevision, cloudOwnerId: this.cloudOwnerId,
    };
    return parseRunSave(save);
  }

  adopt(save: RunSave): void {
    this.id = save.id; this.commands = structuredClone(save.commands); this.digest = save.stateDigest; this.invalid = false;
    this.cloudRevision = save.cloudRevision; this.cloudOwnerId = save.cloudOwnerId;
  }
}

function validCommand(value: unknown): value is CanduCommand {
  if (!value || typeof value !== 'object') return false;
  const c = value as Record<string, unknown>;
  const finite = (v: unknown, min: number, max: number) => typeof v === 'number' && Number.isFinite(v) && v >= min && v <= max;
  switch (c.type) {
    case 'commit-day': return Number.isInteger(c.expectedCompletedDays) && finite(c.expectedCompletedDays, 0, 4294967295) && Array.isArray(c.channelIndices) && c.channelIndices.length <= 380 && c.channelIndices.every(i => Number.isInteger(i) && finite(i, 0, 379));
    case 'pause': case 'resume': case 'solve': return true;
    case 'advance': return finite(c.wallMilliseconds, 0, 60_000);
    case 'step': return finite(c.simulationSeconds, 0, 86_400);
    case 'set-playback-mode': return ['pause', '1x', '10x', '60x'].includes(String(c.modeId));
    case 'queue-power-target': return finite(c.targetFraction, 0, 2);
    case 'reset': return finite(c.seed, 0, 4294967295) && Number.isInteger(c.seed) && ['free-practice', 'useful-fuel-day-v1'].includes(String(c.shiftId)) && (c.pacingMode === undefined || ['daily-turn', 'real-time'].includes(String(c.pacingMode)));
    case 'commit-refuel': {
      const r = c.request as Record<string, unknown> | undefined;
      return !!r && Number.isInteger(r.channelIndex) && finite(r.channelIndex, 0, 379) &&
        ['toward-end-a', 'toward-end-b'].includes(String(r.directionId)) && r.shiftCount === 8 &&
        typeof r.fuelTypeId === 'string' && r.fuelTypeId.length < 256;
    }
    default: return false;
  }
}

export function parseRunSave(input: unknown): RunSave {
  const s = input as RunSave | null;
  if (!s || ![1, 2].includes(s.version) || typeof s.id !== 'string' || !/^[0-9a-f-]{36}$/i.test(s.id) ||
    (s.version === 2 && (!['daily-turn', 'real-time'].includes(s.pacingMode ?? '') || !Array.isArray(s.draftChannels) || s.draftChannels.length > 380 || new Set(s.draftChannels).size !== s.draftChannels.length || !s.draftChannels.every(i => Number.isInteger(i) && i >= 0 && i < 380))) ||
    (s.dailyIntegrationId !== undefined && (typeof s.dailyIntegrationId !== 'string' || !s.dailyIntegrationId)) ||
    typeof s.savedAt !== 'string' || !Number.isFinite(Date.parse(s.savedAt)) ||
    typeof s.dataPackId !== 'string' || typeof s.scorePolicyId !== 'string' ||
    !Number.isInteger(s.seed) || s.seed < 0 || s.seed > 4294967295 ||
    !['free-practice', 'useful-fuel-day-v1'].includes(s.shiftId) ||
    ![s.score, s.seconds, s.fuelConsumed].every(n => Number.isFinite(n) && n >= 0) ||
    typeof s.ended !== 'boolean' || typeof s.stateDigest !== 'string' || !s.stateDigest ||
    (s.cloudRevision !== undefined && (!Number.isInteger(s.cloudRevision) || s.cloudRevision < 1)) ||
    (s.cloudOwnerId !== undefined && typeof s.cloudOwnerId !== 'string') ||
    !Array.isArray(s.commands) || !s.commands.length || s.commands.length > MAX_SAVE_COMMANDS ||
    !s.commands.every(r => r && validCommand(r.command) && typeof r.accepted === 'boolean' && ['full', 'compact'].includes(r.responseMode)) ||
    new TextEncoder().encode(JSON.stringify(s)).length > MAX_SAVE_BYTES) {
    throw new Error('The saved run is damaged, unsupported, or too large.');
  }
  return s;
}
