# Bounded task request template

Use one task ID and one independently verifiable outcome. This template
structures a request; [AGENTS.md](AGENTS.md) is the operational authority,
the [implementation plan](docs/Implementation_plan.md) defines product and
phase scope, and [the delivery register](docs/PROJECT_SCOPE.md) records the
current handoff.

```text
Task ID: <PHASE-TASK>
Requested lane / reasoning: <GPT-5.6 Luna / high by default; code review (high) when required>
Objective: <one bounded outcome>
Allowed files/subsystems: <explicit paths or subsystems>
Approved inputs: <specifications, ADRs, fixtures, and prerequisite reports>
Current scope lookup: <exact task/gate/phase key searched in PROJECT_SCOPE>
Literature digest applicability (physics tasks): <P1-T08 row IDs and use, or NotApplicable with a concrete reason>
Required behavior and invariants:
- <behavior>
- <invariant>
Explicit non-goals:
- <excluded work>
Validation:
- T0: <format/build/schema/manual check>
- T1: <focused command or check>
- Escalation/gate evidence: <only when explicitly required or triggered>
Gate overlap (if relevant): <upstream gate, frozen input, assumption, and deferred evidence>
Required report path: docs/tasks/<PHASE-TASK>.md
Definition of done:
- <observable completion condition>
- Final scope inspection is clean
- Required report is complete
```

## Execution footer

```text
Read AGENTS.md, the implementation plan, the approved inputs above, and every
prerequisite report. Search PROJECT_SCOPE.md for the exact current key before
opening unrelated records. Execute only <PHASE-TASK>.

For physics-related work, confirm P1-T08 is COMPLETE, read its report and
literature digest, and record applicable row IDs or a concrete NotApplicable
reason. P1-T08 itself and task-definition/report-only work are the only
exceptions.

Preserve unrelated changes. Do not select or alter equations, units, signs,
tolerances, golden data, public schemas, dependency direction, or architecture
without explicit approved authority. Follow AGENTS.md for review routing,
receipt verification, NO_ARTIFACT handling, escalation, and stop conditions.

Run only the listed validation unless a named gate or risk trigger requires
more. Inspect final scope, write the task report from
docs/tasks/TASK_REPORT_TEMPLATE.md, update PROJECT_SCOPE.md when its current
status/frontier changed, and stop after this task.
```
