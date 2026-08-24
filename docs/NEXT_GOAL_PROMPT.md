# Clean-session prompt for the next goal

Copy the prompt below into a new Codex session from the repository root.

```text
Work on exactly one task selected from the current authoritative scope.

Requested lane / reasoning: GPT-5.6 Luna / high for bounded implementation or
documentation work, followed by the review and validation required by
AGENTS.md. Verify actual model/effort telemetry when available; otherwise
record the evidence as UNVERIFIED.

Before selecting work, query docs/PROJECT_SCOPE.md with the exact task, phase,
or gate key. Treat that row, the named approved specification, prerequisite
reports, and AGENTS.md as the current routing inputs. Do not reuse a hard-coded
task from an older handoff, and do not treat this prompt, a guide, or a site
snapshot as a technical authority.

Read completely before acting:
- AGENTS.md
- docs/Implementation_plan.md
- the exact current docs/PROJECT_SCOPE.md row and named prerequisite reports
- every approved specification named by the selected task

Execute one task ID at a time. Preserve unrelated work, use only the allowed
files, keep public schemas and serialized bytes stable unless the selected
task explicitly authorizes a change, and stop on an authority conflict,
critical gate blocker, missing artifact, licensing issue, nondeterminism,
invalid numeric state, or out-of-scope behavior. Do not invent equations,
units, signs, tolerances, golden values, external authority, shutdown/scram,
safety-system, full thermal-hydraulic, or operator-training behavior.

Run the cheapest proportionate validation required by the task. Escalate to
T3 and independent code review (high) for numerical behavior, determinism,
contracts, schemas, persistence, replay, canonical bytes/digests, public API,
or runtime-affecting toolchain changes. Record exact commands and results.

Finish with docs/tasks/<TASK-ID>.md using the task-report template. Record the
outcome, files, assumptions, requested/actual model and review evidence,
token/cost fields when available, validation, numerical differences,
deferred tests, risks, blockers, and the next eligible task. Update
docs/PROJECT_SCOPE.md only for the current handoff, commit the completed
checkpoint, inspect final status, and stop after that one task.
```
