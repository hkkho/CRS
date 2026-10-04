# Active browser game architecture

This page describes the current product. The ordered work plan is
[WEB_ROADMAP.md](WEB_ROADMAP.md); task completion and remaining acceptance are in
[REFACTORING_TASK_GUIDE.md](REFACTORING_TASK_GUIDE.md).

```text
Native launcher / Studio DOM and SVG
                       |
              one session controller
         typed v2 commands / serialized worker
                       |
             BrowserHost browser-WASM
                       |
             instance PlaytestRuntime
                       |
            GameSession / practice clock
       scoring / immutable candidates / presentation
                       |
           Core equilibrium / RRS / xenon
       topology / inventory / refuelling / burnup
                       |
          authored embedded physics packs
```

The client formats observations and drafts; it does not calculate reactor
responses. Accepted shared candidates own fuel, poison, equilibrium, RRS, clock
and score. Failed commands preserve physical state. The bridge preserves protocol
v2 names, missing/null encoding, units and digest algorithms. The controller owns
history and view-only response summaries across Studio history tab changes.

The active clock is accelerated: requested browser 1× is 30 simulation minutes
per real second; 10×/60× multiply that request. One pending quantum limits backlog.
The observed sim-minutes/second display includes solver waits and never drives
the simulation. Phase-era playback IDs remain wire identifiers, not claims about
real-time plant kinetics. Practice ends at its horizon or exhausted RRS headroom;
commands and clock ticks stop at terminal state. Challenges have their own
published objective/horizon. Physical engineering edits mark modified sandbox
and exclude standard rewards until reset.

Default publishing is single-threaded and needs no WebGPU/shared memory. GPU
fixtures/shaders/exports are present only with `EnableResearchExperiments=true`;
profiling exports require `EnableRuntimeProfiling=true`, and CPU threads require
`EnableCpuParallelism=true`. Experiment publishing targets `tmp`, preserving the
normal browser runtime. Research uses the same CPU row equations and published
comparison budgets; results cannot commit game state. See
[research build instructions](maintenance/research-builds.md).

IQS solver/candidate remain in `ReactorSim.Core.Research` for retained research
coverage; shared metadata APIs still have Core adjoint/diffusion consumers.
Legacy complete transitions, device queues/maps, kinetics, reduced models and
state archives now live in research; shared state/digest/xenon primitives remain
in Core. See the [consumer audit](maintenance/core-research-boundary.md).
Tool/data ownership, archive status and checked canonical pack staging are in
the [maintained-tool index](maintenance/research-tools.md).
External analysis programs are development-only and are never loaded by gameplay.
Plausible authored packs are sufficient; provenance describes approximations
and is not a mandatory source-verification gate.
