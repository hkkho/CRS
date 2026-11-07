# 6. Checkpoints and updates

The Pages game checkpoints on automatic updates. The replay journal stores commands and the last authoritative state digest in IndexedDB. A session storage entry carries the seed, journal ID and any custom settings. Wait for other game tabs to close before switching builds.

```annotated-source
path: web/candu-playtest/src/DemoLoop.ts
title: "Save the solved endpoint and pending orders"
language: typescript
start: "  async checkpoint(write: (seed: number, save?: RunSave) => Promise<void>): Promise<() => void> {"
end: "    } catch (error) { release(); throw error; }"
steps:
- through: "      const seed = this.solved.shift?.seed;"
  explanation: "Suspend playback and wait for an outstanding solve or restore. Read the seed from the latest solved endpoint."
- through: "        await write(seed, save);"
  explanation: "Create a journal entry if necessary to preserve orders queued before the first step. Capture the solved state and the orders still waiting."
- through: "    } catch (error) { release(); throw error; }"
  explanation: "Keep the game suspended until the updater finishes. If storage fails, release the checkpoint gate and report the error."
```

```annotated-source
path: web/candu-playtest/src/DemoLoop.ts
title: "Replay and verify before enabling Start"
language: typescript
start: "  private async restore(input: Promise<RunSave>, bridge: RecoveryBridge): Promise<void> {"
end: "      for (const index of save.draftChannels ?? []) { this.waitingFuel.add(index); this.pending.add(index); }"
steps:
- through: "      if (save.scorePolicyId !== initial.scorePolicyId) throw new Error(\"The saved run uses a different scoring policy.\");"
  explanation: "Check pacing, pack identity, cadence and score policy against the initialized worker. Custom options must reconstruct the same pack."
- through: "      if (digest !== save.stateDigest) throw new Error(\"The saved reactor state could not be verified.\");"
  explanation: "Replay each recorded command in order and compare its acceptance and the final digest."
- through: "      for (const index of save.draftChannels ?? []) { this.waitingFuel.add(index); this.pending.add(index); }"
  explanation: "Adopt the verified result and put unsubmitted fuel orders back in the queue."
```

[GameUpdateView](../../web/candu-playtest/src/GameUpdateView.ts) controls the update prompt and reload. If replay fails, the saved checkpoint stays stored and the player can go back to the launcher.
