# <TASK-ID> - <short task title>

## Outcome

Status: <COMPLETE | BLOCKED | INCOMPLETE>

<Concise summary of the outcome and task boundary.>

Effectiveness: <SUCCESS | PARTIAL | NO_ARTIFACT | BLOCKED | INCOMPLETE>

## Execution and model evidence

- Role: <root implementer | Luna worker | code review (high) reviewer | other>
- Requested model / reasoning: <value>
- Actual model / reasoning: <value, or `UNVERIFIED`>
- Execution receipt or telemetry source: <thread/worker/reviewer ID and source, or `Unavailable`>
- Attempts: <count>; elapsed time: <value or `Unavailable`>
- Artifact/checkpoint status: <produced | NO_ARTIFACT | not applicable>
- Review disposition: <not applicable | PASS | CONDITIONAL PASS | FAIL | BLOCKED | INCOMPLETE>
- Review evidence verification: <not applicable | receipt-verified | UNVERIFIED>
- Reviewer reuse/fresh-review rationale: <not applicable, same reviewer ID, or concrete reason for change>

## Files created or changed

- `<path>` - <reason>

State explicitly when pre-existing files were inspected but not changed.

## Assumptions and design choices

- <assumption or bounded choice>
- <why it stayed within the approved specification>

## Validation commands and results

### <check name and level>

```text
<exact command>
```

Result: <exit code, pass/fail, counts, and relevant output summary>.

## Token and cost accounting

Record request-level or worker-level values only when telemetry exposes them.
Do not invent a per-worker allocation from an aggregate total.

| Field | Value | Source/notes |
|---|---:|---|
| Input tokens | <value or `Unavailable`> | <telemetry source> |
| Cached input tokens | <value or `Unavailable`> | <telemetry source> |
| Cache-write input tokens | <value or `Unavailable`> | <telemetry source> |
| Output tokens | <value or `Unavailable`> | <telemetry source> |
| Reasoning output tokens | <value or `Unavailable`> | <telemetry source> |
| Total tokens | <value or `Unavailable`> | <telemetry source; request or aggregate> |
| Estimated cost | <value or `Unavailable`> | <price source/date and formula> |
| Goal-service total | <value or `Unavailable`> | report separately; do not add to request totals |

Cost formula/basis: <provider billing semantics, prices, currency, and date; or why unavailable>.

## Numerical differences

<Record absolute and relative differences by observable when applicable. Otherwise state "Not applicable; no numerical behavior changed.">

## Deferred validation

- <test or suite not run> - deferred to <task or gate> because <reason>

## Blockers, risks, and follow-up

- Blockers: <none or exact blocker>
- Risk triggers: <none, or trigger and required escalation>
- Risks accepted or deferred: <none or named risk>
- Follow-up work: <task IDs; do not implement here>

## Next eligible task

<TASK-ID and title, or "None until <decision/gate>.">

Source: [`AGENTS.md`](../../AGENTS.md) and
[`CODEX_TASK_TEMPLATE.md`](../../CODEX_TASK_TEMPLATE.md).
