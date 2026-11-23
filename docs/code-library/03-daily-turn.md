# 3. One time-based step

`DemoLoop` starts at most one solve ahead of playback. The wire command is `commit-day` in both pacing modes. Time-based runs use 43,200 simulated seconds per step and daily runs use 86,400 seconds.

```annotated-source
path: web/candu-playtest/src/DemoLoop.ts
title: "Submit a waiting batch"
language: typescript
start: "  private async advance(): Promise<void> {"
end: "          this.buffered = { snapshot: result.snapshot, fuel };"
steps:
- through: "    this.busy = true;"
  explanation: "Capture the waiting orders once. Input during the solve joins the following batch."
- through: "          this.buffered = { snapshot: result.snapshot, fuel };"
  explanation: "Send the current counter and channel indices. Journal an accepted response and buffer its endpoint for playback. The tutorial displays the endpoint immediately."
```

```annotated-source
path: src/ReactorSim.Game/GameSession.cs
title: "Prepare the fuel moves on a detached session"
language: csharp
start: "public DailyTurnOperation BeginDay(uint expectedCompletedDays, IReadOnlyList<uint> channelIndices)"
end: "                return new DailyTurnOperation(this, candidate, ordered, movements);"
steps:
- through: "            var movements = new List<RefuellingMovement>();"
  explanation: "Validate the batch, sort channels and clone the candidate session, including forks of its clock and equilibrium solver. The live session remains available for observations."
- through: "                return new DailyTurnOperation(this, candidate, ordered, movements);"
  explanation: "In time-based mode, apply every move and solve the combined inventory on the candidate. A numerical rejection discards it. A limit violation marks the candidate terminal; the following calculation adopts the moves without advancing time."
```

`CalculateDay` checks the live clock, generation, inventory and spatial projection against the values captured at preparation. If the candidate is still operating, it integrates fission energy, burnup, poison and operating score once using post-fuelling readings, then solves the endpoint. Adoption includes the accepted movements, readings and score. Daily mode instead solves each move on the candidate before the interval and stops the remaining orders if a move ends the run. See [shared step semantics](../gameplay/daily-turn-mode.md).
