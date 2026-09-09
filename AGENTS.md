# Repository working instructions

These instructions apply to the whole repository.

## Product priority

- Make the Unity steady-state CANDU refuelling game playable first.
- Prioritize the live refuelling loop, immediate feedback, scoring, and a debug
  menu that exposes simulation controls and state for functional playtesting.
- Keep shutdown, scram, accident progression, and operator-training scenarios
  out of scope.
- Start with the deterministic project-authored model. Add realism afterward
  using lawfully usable, offline DRAGON5/DONJON5-derived data packs.

Read `README.md` and `docs/IMPLEMENTATION_GUIDE.md` before substantial work.

## Execution strategy

- Use GPT-5.6 Luna subagents whenever work can be separated into bounded coding,
  inventory, research, documentation, mechanical editing, or focused test
  tasks. Keep architecture, cross-cutting integration, difficult debugging,
  verification, and final commits with the primary agent, which is responsible
  for checking and integrating Luna's results.
- When creating an agent, delegate all coding and implementation work to
  GPT-5.6 Luna and set its reasoning effort to `max` automatically.
- Use GPT-5.6 Terra only when clarification is needed and set its reasoning
  effort to `high`.
- Use GPT-5.6 Sol only when something is critically stuck and set its reasoning
  effort to `medium`.
- Do not substitute Terra or Sol for ordinary coding work when Luna can handle
  the bounded task.
- If the primary agent cannot stage or commit because `.git` access is blocked,
  delegate only the final staging and commit operation to the existing
  `Publisher` chat as a subagent. Give it the exact file allowlist, commit
  message, and completed checks; it must preserve unrelated changes and return
  the commit hash.
- Work autonomously within the requested scope and preserve unrelated user
  changes.
- Keep `unity/ReactorGame` runnable after each implementation slice.
- Prefer the smallest useful implementation over speculative infrastructure.
- Do not create task reports, gate reports, approval records, review records,
  or mandatory validation checklists.

## Architecture and physics boundaries

- `src/ReactorSim.Core` remains engine-neutral and owns deterministic
  simulation state transitions.
- `unity/ReactorGame` is the interactive presentation and input layer; do not
  duplicate simulation rules there.
- DRAGON5 and DONJON5 remain offline tools. Unity consumes compact, versioned
  data packs and never invokes those programs at runtime.
- Preserve units, energy-group ordering, provenance, licensing boundaries, and
  deterministic behavior when physics data changes.

## Testing and completion

- Add only focused unit tests needed to prove newly implemented behavior.
- Run proportionate build, focused-test, and Unity import checks. The owner's
  functional testing through the playable game and debug menu is the primary
  acceptance path.
- Inspect the final diff, then commit every completed change. Report the commit
  hash and any checks that were run.
