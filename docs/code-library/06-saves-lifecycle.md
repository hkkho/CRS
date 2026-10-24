---
hide:
  - toc
---

# 6. Saves, restore and application lifecycle

[Previous: presentation](05-presentation.md) · [Library](index.md) · [Next: development](07-development.md)

A **replay save** records the initial conditions and sequence of commands needed
to reconstruct a run. Its reliability depends on deterministic simulation and
compatible model versions. A **state digest** is a compact identity calculated
from the protocol's defined state representation; comparing it after replay
checks whether reconstruction produced the expected result.

## Saves store replayable inputs

[runSave.ts](../../web/candu-playtest/src/runSave.ts) defines `RunSave`,
`RunJournal` and `parseRunSave()`. A saved run includes its seed and objective,
pack/scoring identities, command log, final state digest, summary measurements,
and version-2 pacing/draft/integration information.

The displayed `CanduSnapshot` is not serialized and later assigned into Core.
Restore reconstructs the run by replaying commands through a fresh authoritative
runtime. This keeps state creation behind the same validated operations as play.

## Step 1: journal exact bridge inputs

After a successful transport response, the controller calls
`RunJournal.record(command, response, responseMode)`. The journal retains a clone
of the command, whether Game accepted it, and the response representation. An
authoritatively rejected command can therefore appear in a replay log. A transport
exception with no response cannot be treated as a verified replay outcome.

An accepted reset clears the earlier log and records an explicit seed/objective
from its resulting snapshot. That makes an originally implicit “retry” reproducible
in a newly initialized worker. Geometry modifications make the journal unsaveable
for the supported standard-run path. Limits on commands and bytes bound saved
input size.

`capture()` requires a verified digest and unmodified run, includes the unfinished
daily plan, then validates the assembled save with `parseRunSave()`.
`startShift()`'s initial no-time pause makes a brand-new frozen decision saveable.

### Record commands as replay inputs

This is the journal's complete `record` method. A transport response may contain an
authoritative rejection, which still belongs in the replay log. A transport error
with no response cannot supply a verified acceptance or digest.

```annotated-source
path: web/candu-playtest/src/runSave.ts
title: Journal exact command inputs and returned acceptance
language: typescript
start: 'record(command: CanduCommand, response: CanduCommandResponse, responseMode: ''full'' | ''compact''): void {'
end: 'this.digest = response.stateDigest ?? '''';

  }'
steps:
- through: this.clear();
  explanation: '**Start a new journal after an accepted reset.** Earlier commands belong to the previous run. A rejected reset does not clear the current history.'
- through: 'command = { type: ''reset'', seed: response.snapshot.shift?.seed, shiftId: response.snapshot.shift?.id, ...(response.snapshot.pacingMode === "daily-turn" ? { pacingMode: "daily-turn" as const } : {}) };

    }'
  explanation: '**Make implicit reset choices explicit.** Copy the resulting seed and objective into the recorded command. A later replay in a new worker must reproduce this run, rather than draw another seed. The spread includes daily pacing when appropriate.'
- through: if (this.commands.length >= MAX_SAVE_COMMANDS) { this.invalid = true; return; }
  explanation: '**Protect the supported replay contract.** Geometry modification invalidates standard saves, and the command limit bounds log growth. Reaching the limit marks the journal invalid and stops appending.'
- through: 'this.digest = response.stateDigest ?? '''';

    }'
  explanation: '**Store a stable replay record.** `structuredClone` prevents later UI changes from mutating the recorded command. Retain acceptance and response representation, then remember the latest state digest. Capture later requires a verified, nonempty digest.'
```

## Step 2: validate compatibility before restore

[BridgeSessionController.restoreRun()](../../web/candu-playtest/src/sessionController.ts)
parses the file and checks:

- Required pacing; version 1 is interpreted as real-time and cannot resume in
  the public daily client.
- The run is not ended.
- Pack and score-policy identities match the currently loaded application.
- A version-2 daily save's integration identity matches the loaded cadence.
- No command/restore is already pending.

Parsing also validates supported command shapes, bounds, metadata and draft
structure. An old save remaining in storage does not mean the current public
runtime accepts it.

## Step 3: replay in an isolated candidate worker

**Figure 6. Restore with verification before adoption.** The current worker
retains the live reactor while a new worker reconstructs the saved run. Reading
downward follows the restore sequence. The `loop` region represents one dispatch
per saved command, and the `alt` region separates successful adoption from failure
cleanup.

```mermaid
%%{init: {'sequence': {'useMaxWidth': false}}}%%
sequenceDiagram
    participant Panel as PlayerPanel
    participant Controller as Session controller
    participant Live as Current worker
    participant Candidate as New worker
    Panel->>Controller: restoreRun(save)
    Controller->>Controller: parse and check identities
    Controller->>Candidate: initialize(seed, pacing)
    loop Saved commands in order
        Controller->>Candidate: dispatch(command, responseMode)
        Candidate-->>Controller: acceptance and observation
        Controller->>Controller: compare acceptance
    end
    Controller->>Controller: verify final state digest and nonterminal state
    Controller->>Candidate: pause
    alt All verification succeeds
        Controller->>Live: dispose
        Controller->>Controller: adopt candidate, draft and journal
    else Any verification fails
        Controller->>Candidate: dispose
        Note over Controller,Live: current reactor preserved
    end
```

The two workers serve different roles: `Live` remains the current accepted
session, whereas `Candidate` is provisional. Replaying a command in the candidate
does not issue that command to the live reactor. Only the successful branch
disposes the old worker and makes the candidate the current session; the failed
branch disposes the candidate instead. The figure summarizes validation and
exception handling rather than showing every early return in the implementation.

The Mermaid `participant` statements declare the columns. `loop`/`end` encloses
repetition, `alt`/`else`/`end` expresses alternative paths, and `Note over` adds an
explanatory annotation. The opening width directive affects the figure's layout,
not the number of workers or the restore algorithm.

The controller reports replay progress, compares each command's acceptance with
the saved value, and verifies the final digest. It then pauses the restored run
and checks the draft's channel eligibility. Only after these checks does it
unsubscribe/dispose the old bridge and adopt the candidate.

History and transient presentation are cleared on success; the saved draft and
journal are adopted. A `finally` block disposes an unadopted candidate and clears
restore/pending status after either outcome. Full chart history is not reconstructed
as a persisted artifact of restore.

### Reconstruct and verify before replacing the live worker

The first excerpt contains the compatibility checks; the second contains the
candidate replay and adoption. Together they show the complete restore method.
The factory parameter lets tests supply a controlled bridge, while ordinary play
creates a new authoritative WASM worker.

```annotated-source
path: web/candu-playtest/src/sessionController.ts
title: Check saved-run compatibility and enter restore mode
language: typescript
start: 'public async restoreRun(input: unknown, progress: (done: number, total: number) => void = () => {},'
end: 'let candidate: CanduPlaytestBridgeLifecycle | null = null;'
steps:
- through: const save = parseRunSave(input);
  explanation: '**Parse the untrusted input.** The method accepts `unknown`, so `parseRunSave` validates the format, command shapes and size before any replay. `progress` defaults to a no-op callback and `factory` defaults to the real bridge constructor.'
- through: if (save.ended) throw new Error('Ended runs cannot be continued.');
  explanation: '**Require compatible pacing and a continuable run.** Version-1 saves are interpreted as real-time. The public daily client rejects them, and completed terminal runs cannot be restored for continued play.'
- through: throw new Error("This saved daily run uses a different time-step model. Start a new daily run.");
  explanation: '**Compare simulation identities.** Pack and scoring IDs must match the loaded runtime. A daily version-2 save must also match the integration cadence. The same seed and commands are insufficient if those rules changed.'
- through: 'let candidate: CanduPlaytestBridgeLifecycle | null = null;'
  explanation: '**Prevent overlapping work and enter pending state.** Restore cannot start during another command or restore. Updating scheduler and UI flags freezes normal interaction while replay is underway. The candidate remains nullable so cleanup can safely handle early failure.'
```


```annotated-source
path: web/candu-playtest/src/sessionController.ts
title: Replay, verify the final digest and adopt the candidate
language: typescript
start: candidate = factory();
end: 'candidate?.dispose?.(); this.restoring = false; this.modePending = false; this.syncScheduler(); this.emit();

  }

  }'
steps:
- through: 'let result: CanduCommandResponse | null = null;'
  explanation: '**Create a separate reactor.** Initialize the candidate from the saved seed and pacing. The current bridge remains alive throughout replay. `result` will retain the final response for verification.'
- through: 'progress(i + 1, save.commands.length);

    }'
  explanation: '**Replay every recorded command in order.** Abort if the controller is disposed. Use each record''s original response mode and compare its accepted/rejected outcome. The progress callback describes completed replay records, not intermediate reactor time.'
- through: throw new Error('Saved state verification failed. The current run was preserved.');
  explanation: '**Verify the reconstructed endpoint.** A missing final response, different state digest or terminal snapshot rejects restoration. This prevents substituting a merely similar reactor for the exact saved endpoint.'
- through: if (restoredDraft.some(i => !paused.snapshot.core.channels.find(c => c.channelIndex === i)?.canRefuel)) throw new Error("Saved fuel plan contains an ineligible channel.");
  explanation: '**Pause and validate the unfinished plan.** The reconstructed reactor must accept pause. Every saved draft channel must still be eligible in the paused authoritative observation. The draft is browser planning data, so it is restored after replay rather than executed as fuel movement.'
- through: this.statusValue = this.checkedStatus(this.bridge.status, paused.snapshot); this.snapshotValue = paused.snapshot;
  explanation: '**Replace the bridge after verification.** Unsubscribe and dispose the old worker, transfer candidate ownership, then subscribe to the replacement. Setting `candidate = null` prevents the cleanup block from disposing the newly adopted live bridge.'
- through: this.lastResponse = paused; this.lastError = null; this.presentation.clear(); this.history.clear();
  explanation: '**Restore local session context.** Copy the draft, adopt the command journal and record the final pause. Reset old-run feedback and observations so the interface starts from the verified restored state.'
- through: 'candidate?.dispose?.(); this.restoring = false; this.modePending = false; this.syncScheduler(); this.emit();

    }

    }'
  explanation: '**Clean up on both success and failure.** An unadopted candidate is disposed. Restore flags are cleared and subscribers are notified even when a check throws. Failed replay or endpoint verification occurs before bridge replacement, preserving the live reactor.'
```

## Step 4: store and optionally synchronize

[PlayerPanel.ts](../../web/candu-playtest/src/PlayerPanel.ts) orchestrates account,
save/load and update-save UI.
[PlayerStore](../../web/candu-playtest/src/playerStore.ts) uses IndexedDB so large
command logs do not block rendering through synchronous browser storage. Stored
records include owner/run identity.

[cloudPlayer.ts](../../web/candu-playtest/src/cloudPlayer.ts) provides the optional
cloud/account adapter. The database schema and ownership/revision rules live in
the repository's `supabase` migrations. Those services store and synchronize saves
and related player data; the local WASM worker remains the simulation authority.
Guest/offline play remains available. See the existing
[accounts and saves guide](../maintenance/player-accounts.md) for configuration
and migration requirements, including version-2 daily saves.

`cloudOwnerId` and `cloudRevision` carry synchronization identity. They should
not be discarded during local edits or treated as permission to overwrite another
owner's save. `RunJournal.markSynced()` updates synchronization metadata only
for the matching run.

## Step 5: identify and apply complete game updates

[GameUpdateView.ts](../../web/candu-playtest/src/GameUpdateView.ts) and the service
worker generated by [offlinePlugin.ts](../../web/candu-playtest/offlinePlugin.ts)
manage update discovery/application. The loaded WASM build is identified by generated
build metadata. The user can save locally before applying a waiting update;
a failed save prevents reload. Other game tabs must close before replacing the
old worker and loading the current complete build.

This matters because the JavaScript protocol, managed exports, pack identities
and integration model must agree. Loading a newer UI with an older runtime is
not a safe partial update. Startup's daily-cadence checks and recovery view
make incompatibility visible.

The update flow and old-tab behavior are detailed in
[hosting and verification](../maintenance/hosting.md). The game's Pages artifact
is produced by its deployment workflow; the documentation generator uses a
separate output directory.

## Step 6: release resources

`main.ts` forwards visibility changes to the controller and cleans up on unload.
The controller disposes subscriptions, the scheduler and bridge; the bridge
clears deadlines and terminates its worker. Views release their own DOM listeners
and subscriptions. A failed restore also releases its additional worker.

Retained real-time scheduling code is shared support for comparison paths.
`isPlaybackActive()` explicitly disables it for the required daily client and
daily snapshots. Hiding/reopening the page never silently advances a daily run.
