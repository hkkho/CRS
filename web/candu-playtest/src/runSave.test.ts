import { describe, expect, it, vi } from 'vitest';
import { RunJournal, parseRunSave, type RunSave } from './runSave';
import { createSnapshot, createShift } from './testSnapshot';
import { BridgeSessionController } from './sessionController';
import type { CanduPlaytestBridgeLifecycle } from './bridge';
import type { CanduCommand, CanduCommandResponse } from './protocol';

function snapshot() { return { ...createSnapshot(), shift: createShift(), scorePolicyId: 'test-policy' }; }
function response(command: CanduCommand, accepted = true): CanduCommandResponse {
  return { protocol: 'candu-playtest-v2', accepted, sequence: 1, command, message: 'test', diagnostics: [], stateDigest: 'digest', snapshot: snapshot() };
}
function saved(): RunSave {
  const journal = new RunJournal(); journal.record({ type: 'pause' }, response({ type: 'pause' }), 'compact');
  return journal.capture(snapshot());
}
function bridge(digest = 'digest'): CanduPlaytestBridgeLifecycle & { dispose: ReturnType<typeof vi.fn> } {
  const s = snapshot();
  return {
    status: { source: 'wasm', title: 'ready', detail: '', isWasmAvailable: true, capabilities: [] },
    getSnapshot: () => s,
    initializeMode: async () => s,
    subscribe: () => () => {},
    dispatch: vi.fn(async command => ({ ...response(command), stateDigest: digest, snapshot: { ...s, isPaused: true } })),
    dispose: vi.fn(),
  };
}

describe('run saves', () => {
  it('records rejected commands and preserves exact advance intervals and response modes', () => {
    const journal = new RunJournal();
    const command: CanduCommand = { type: 'advance', wallMilliseconds: 87.5321 };
    journal.record(command, response(command), 'compact'); command.wallMilliseconds = 10;
    journal.record({ type: 'resume' }, response({ type: 'resume' }, false), 'full');
    expect(journal.capture(snapshot()).commands).toEqual([
      { command: { type: 'advance', wallMilliseconds: 87.5321 }, accepted: true, responseMode: 'compact' },
      { command: { type: 'resume' }, accepted: false, responseMode: 'full' },
    ]);
  });
  it('starts a fresh journal only for accepted resets and stores the resolved seed', () => {
    const journal = new RunJournal(); const id = journal.id;
    journal.record({ type: 'reset' }, response({ type: 'reset' }, false), 'full');
    expect(journal.id).toBe(id);
    journal.record({ type: 'reset' }, response({ type: 'reset' }), 'full');
    const save = journal.capture(snapshot());
    expect(save.id).not.toBe(id);
    expect(save.commands).toEqual([{ command: { type: 'reset', seed: 42, shiftId: 'useful-fuel-day-v1' }, accepted: true, responseMode: 'full' }]);
  });
  it('rejects corrupt, unbounded and engineering-command saves', () => {
    const save = saved();
    for (const changed of [{ version: 99 }, { seconds: Infinity }, { commands: [] },
      { commands: [{ command: { type: 'advance', wallMilliseconds: Infinity }, accepted: true, responseMode: 'compact' }] },
      { commands: [{ command: { type: 'configure-cell' }, accepted: true, responseMode: 'compact' }] }]) {
      expect(() => parseRunSave({ ...save, ...changed })).toThrow();
    }
  });
  it('keeps ended runs as stats but refuses continuation before creating a worker', async () => {
    const controller = new BridgeSessionController(bridge()); const factory = vi.fn(() => bridge());
    await expect(controller.restoreRun({ ...saved(), ended: true }, undefined, factory)).rejects.toThrow('Ended');
    expect(factory).not.toHaveBeenCalled(); controller.dispose();
  });
  it('rejects version mismatches and preserves the current worker on digest mismatch', async () => {
    const old = bridge(); const candidate = bridge('wrong'); const controller = new BridgeSessionController(old);
    await expect(controller.restoreRun({ ...saved(), dataPackId: 'other' }, undefined, () => candidate)).rejects.toThrow('different');
    await expect(controller.restoreRun(saved(), undefined, () => candidate)).rejects.toThrow('verification');
    expect(old.dispose).not.toHaveBeenCalled(); expect(candidate.dispose).toHaveBeenCalledOnce();
    expect(controller.snapshot).toBe(old.getSnapshot()); controller.dispose();
  });
  it('adopts a verified replay, pauses it, and retains its run ID', async () => {
    const old = bridge(); const candidate = bridge(); const controller = new BridgeSessionController(old);
    const save = saved(); await controller.restoreRun(save, undefined, () => candidate);
    expect(old.dispose).toHaveBeenCalledOnce(); expect(candidate.dispose).not.toHaveBeenCalled();
    expect(controller.snapshot.isPaused).toBe(true); expect(controller.captureRun().id).toBe(save.id);
    expect(controller.captureRun().commands).toHaveLength(2); controller.dispose();
  });
});
