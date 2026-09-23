# Repository working instructions

These instructions apply to the whole repository.

## Product priority

- Make `web/candu-playtest` deployed on Vercel the primary product and
  acceptance path until the web version is highly functional.
- Prioritize the live browser refuelling loop, immediate feedback, scoring, and
  the debug/playtest controls that expose simulation state for functional
  acceptance.
- Unity feature development is paused during this web-first phase. Do not add
  Unity presentation, input, gameplay, or polish work; shared engine-neutral
  changes are allowed only when they are strictly required by the web path.
- Keep shutdown, scram, accident progression, and operator-training scenarios
  out of scope.
- Use the project-authored deterministic two-group diffusion model as the
  active path. Do not add external-source validation or data-pack prerequisites
  to web work.

Read `README.md` and `docs/IMPLEMENTATION_GUIDE.md` before substantial work.

## Execution strategy

- Use GPT-5.6 Luna subagents whenever work can be separated into bounded coding,
  inventory, research, documentation, mechanical editing, benchmark execution,
  test runs, log triage, or focused diff review. Keep architecture,
  cross-cutting integration, difficult debugging, final acceptance, and final
  commits with the primary agent, which is responsible for checking and
  integrating delegated results.
- When creating an agent, delegate all coding and implementation work to
  GPT-5.6 Luna and set its reasoning effort to `max` automatically.
- Use GPT-5.6 Terra at `high` for small read-only inventory, straightforward
  summaries, status checks, and routine command or test verification when a
  Luna implementation agent would be unnecessary. Terra may also be used when
  clarification is needed.
- Use GPT-5.6 Sol only when something is critically stuck and set its reasoning
  effort to `medium`.
- Do not substitute Terra or Sol for ordinary coding work when Luna can handle
  the bounded task.
- Before delegating command-running work, verify that the spawned task can run
  with full access and without approval prompts. If it pauses for permission,
  correct the host permission selection or relaunch it instead of duplicating
  the delegated work in the primary context.
- If the primary agent cannot stage or commit because `.git` access is blocked,
  delegate only the final staging and commit operation to the existing
  `Publisher` chat as a subagent. Give it the exact file allowlist, commit
  message, and completed checks; it must preserve unrelated changes and return
  the commit hash.
- Work autonomously within the requested scope and preserve unrelated user
  changes.
- Keep `web/candu-playtest` runnable after each implementation slice. Do not
  touch Unity while the web-first freeze is active unless a shared change
  strictly required by the web path makes it necessary.
- Prefer the smallest useful implementation over speculative infrastructure.
- Do not create task reports, gate reports, approval records, review records,
  or mandatory validation checklists.

## Architecture and physics boundaries

- `src/ReactorSim.Core` remains engine-neutral and owns deterministic
  simulation state transitions.
- `unity/ReactorGame` is the interactive presentation and input layer; do not
  duplicate simulation rules there.
- The active runtime consumes the project-authored compact, versioned data
  pack. Unity and the browser never invoke external analysis programs at
  runtime.
- Preserve units, energy-group ordering, provenance, licensing boundaries, and
  deterministic behavior when physics data changes.

## Testing and completion

- Add only focused unit tests needed to prove newly implemented behavior.
- Run proportionate browser build, focused-test, and deployed Vercel
  benchmark/smoke checks. The owner's functional testing through the deployed
  web playtest is the primary acceptance path.
- Run Unity checks only when a shared engine-neutral change required by the web
  path affects Unity integration; do not use them to reopen Unity feature work.
- Inspect the final diff, then commit every completed change. Report the commit
  hash and any checks that were run.
