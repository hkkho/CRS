# 3. One time-based step

`DemoLoop` starts at most one solve ahead of playback. The wire command is `commit-day` in both pacing modes. Time-based runs use 43,200 simulated seconds per step and retained daily runs use 86,400 seconds.

```annotated-source
path: web/candu-playtest/src/DemoLoop.ts
title: "Submit a waiting batch"
language: typescript
start: "  private async advance(): Promise<void> {"
end: "        this.solved = result.snapshot; this.buffered = { snapshot: result.snapshot, fuel };"
steps:
- through: "    this.busy = true;"
  explanation: "Capture the waiting orders once. Input during the solve joins the following batch."
- through: "        this.solved = result.snapshot; this.buffered = { snapshot: result.snapshot, fuel };"
  explanation: "Send the current counter and channel indices. Journal an accepted response and buffer its authoritative endpoint for display."
```

```annotated-source
path: src/ReactorSim.Game/GameSession.cs
title: "Prepare the fuel moves on a detached session"
language: csharp
start: "public DailyTurnOperation BeginDay(uint expectedCompletedDays, IReadOnlyList<uint> channelIndices)"
end: "            return new DailyTurnOperation(this, candidate, ordered, movements);"
steps:
- through: "            var movements = new List<RefuellingMovement>();"
  explanation: "Validate the batch, sort channels and fork the run clock and equilibrium solver. The live session remains available for observations."
- through: "            return new DailyTurnOperation(this, candidate, ordered, movements);"
  explanation: "Apply moves to the candidate. A numerical rejection discards it. A physical loss stops later orders and can still be committed."
```

`CalculateDay` verifies that the live state is still the original. If the candidate is still operating, it integrates fission energy and poison once using post-fuelling readings, then solves the endpoint. Adoption includes the accepted movements, readings and score. See [shared step semantics](../gameplay/daily-turn-mode.md).
