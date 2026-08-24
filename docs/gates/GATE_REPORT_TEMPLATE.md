# <GATE-ID> - <gate title>

## Result

<PASS | CONDITIONAL PASS | FAIL | BLOCKED | INCOMPLETE | FORCED CLOSED / WAIVED>

<One concise rationale. A forced closure is an administrative user waiver, not
a technical pass; preserve the unresolved evidence and state its downstream
boundary.>

## Scope and authority reviewed

- Tasks/reports since the previous gate: <IDs>
- Approved specifications and ADRs: <paths/IDs>
- Current-scope lookup: <exact gate/phase key in PROJECT_SCOPE>
- Candidate/diffs or commits inspected: <identifiers>
- Required validation: <T3-T6 and any focused diagnostics>
- Gate-overlap status: <not applicable, or upstream gate/assumption/deferred evidence>

## Evidence and commands

### <check name and validation level>

```text
<exact command>
```

Result: <exit code, counts, and relevant output>.

## Independent review evidence

- Requested lane / reasoning: <code review (high) / high, or not applicable>
- Actual model / reasoning: <verified value, or UNVERIFIED>
- Receipt or telemetry source: <reviewer ID/source, or Unavailable>
- Attempts and reviewer continuity: <count; same reviewer/context or documented change>
- Final technical disposition: <PASS | CONDITIONAL PASS | FAIL | BLOCKED | INCOMPLETE>
- Evidence verification: <receipt-verified | UNVERIFIED | not applicable>

## Findings

List in descending severity. Use `None` only when no findings remain.

### <Critical | High | Medium | Low>: <finding title>

- Location: `<path:line>`
- Evidence and impact: <what was observed and why it matters>
- Required action: <corrective task ID or disposition>

## Numerical comparison summary

<For each applicable observable: reference, actual, absolute/relative error,
approved tolerance, and pass/fail. State "Not applicable" when this gate has
no numerical comparison.>

## Boundary review

- Dependency direction: <pass/fail and evidence>
- Determinism and failure diagnostics: <pass/fail/not applicable and evidence>
- Scope exclusions: <pass/fail and evidence>
- Licensing/provenance: <pass/fail/not applicable and evidence>

## Required corrective tasks and deferred evidence

- <task ID, bounded outcome, validation, and owner gate>

Use `None` only for an unconditional technical pass.

## Risks and next eligible work

- Accepted/deferred risks: <risk, rationale, owner, and target gate/task>
- Dependent work blocked: <none or exact boundary>
- Independent overlap-eligible work: <none or exact frozen-scope condition>
- Next bounded tasks: <up to eight IDs, or a specific decision needed>

For `FAIL`, `BLOCKED`, or `INCOMPLETE`, stop only dependent work. List
independent work only when the gate-overlap policy in `AGENTS.md` is satisfied.

## Token and cost accounting

Record available request-level or aggregate telemetry without inventing a
per-worker allocation. State the price source/date and formula, or
`Unavailable`.

## Sign-off

- Reviewer/model lane: <code review (high) reviewer or not applicable>
- Date: <YYYY-MM-DD>
- Final disposition: <same as Result>

Source: [`AGENTS.md`](../../AGENTS.md),
[`docs/Implementation_plan.md`](../Implementation_plan.md), and
[`docs/PROJECT_SCOPE.md`](../PROJECT_SCOPE.md).
