# XSEC-DECK-01 - exact JEFF lattice deck authority

## Outcome

Status: COMPLETE

The exact executable JEFF lattice basis is the official Version5 `TCWUX11`
procedure with its paired `TCWU05Lib` procedure and shared `assertS` procedure.
This creates candidate `xsec-c6-37-nu-jeff31-4ev-v2`, replacing only v1's deck
basis. It does not modify TCWU11 assertions, run DRAGON, create a runtime pack,
or admit a golden/reference/full-core/Unity result.

Effectiveness: SUCCESS

Applicable P1-T08 digest rows: `S1-R04`, `S1-R05`, `S5-R02`, `S5-R03`,
`S5-R04`, `S5-R09`, and `S6-R03`.

## Execution and model evidence

- Role: root implementer.
- Requested model / reasoning: GPT-5.6 Luna / high.
- Actual model / reasoning: UNVERIFIED; request-level execution telemetry is unavailable.
- Execution receipt or telemetry source: current Codex task; no provider receipt was exposed.
- Attempts: one bounded deck-authority decision task.
- Artifact/checkpoint status: produced; checkpoint pending after report and scope reconciliation.
- Review disposition: PASS.
- Review evidence verification: UNVERIFIED.
- Reviewer reuse/fresh-review rationale: Popper (`01a03b98-9b86-7f22-9068-76bb1ce6b01e`) was reused for its v1 context, but performed one new bounded high review for the changed v2 candidate; submission `01a03fdb-492f-7be2-85d9-86040d553349`.

## Files created or changed

- `docs/tasks/XSEC-DECK-01-OWNER-APPROVAL.md` - bounded owner decision selecting official JEFF TCWUX11 and prohibiting assertion edits.
- `docs/spec/xsec-jeff-lattice-deck-v2.md` - exact source basis, exhaustive deltas, inherited mapping, and fail-closed rules.
- `reference/manifests/xsec-deck-01-source-admission-v2.json` - path-free v2 source identity record.
- `docs/tasks/XSEC-DECK-01.md` - this report.

Raw source procedures, JEFF/WLUP data, source decks, binaries, LCM objects, and
failed-run logs remain external. Core/CLI/Unity source, data packs, schemas,
tolerances, golden values, and XSEC v1 mapping files were not changed.

## Decision and evidence

- V1's TCWU11 route contains WLUP assertions incompatible with JEFF.
- Official TCWUX11 supplies its own JEFF source lexemes and source assertions:
  `1.118478`, `0.9420414`, `1.118481`, and `1.073615`.
- The official source comparison has 15 additions and 19 removals, including
  group count, tracking controls, EDI operand order, and assertions. Those are
  selected official source differences, not project edits.
- Only two SPH calls may be omitted; an external read-only decoder may consume
  the source-created `res := EDITION` serialization. Source assertions remain
  mandatory regression guards.

## Validation commands and results

### T0 - source identity and mapping record

```text
# Canonical-LF UTF-8 SHA-256 of external TCWUX11, TCWU05Lib, and assertS.
# Assert v2 manifest identity, assertions, exactly two deltas, source stage,
# and candidate-only states; then run git diff --check.
```

Result: PASS. The canonical-LF SHA-256 values are TCWUX11
`9d1865089741aecdd145a167cf7ea729760b4b4f1744a7b9c045480065371813`, TCWU05Lib
`896de7f647fc7b3050815d1006f41cd938aca276b3a85c254d34e5c7d41e0040`, and
assertS `c6291cd7a2496f48ab012c294e2d823170401549cc5df3e366c9924f0a34040c`.

### T0 - official procedure comparison

```text
git diff --no-index --ignore-space-at-eol <external>/TCWU11.c2m <external>/TCWUX11.c2m
```

Result: PASS as a diagnostic comparison. It confirms TCWUX11 is a distinct
official JEFF procedure, so v2 selects it rather than modifying v1.

### T3 - fresh portable full-suite wrapper

```text
.\tools\Test-FullHeadlessSuite.ps1 -ConfirmFullSuite -ArtifactsPath $env:TEMP\candu-xsec-deck-01-t3-<guid>
```

Result: PASS (exit 0): Core `208/208`, Golden `26/26`, CLI `38/38`; zero
failures and zero skips.

### Independent code review (high)

```text
Popper 01a03b98-9b86-7f22-9068-76bb1ce6b01e, submission 01a03fdb-492f-7be2-85d9-86040d553349
```

Result: PASS with no required corrections. The reviewer verified source
identities, assertions, two SPH calls, `res` serialization, delta set,
inherited mapping, exclusions, and routing. Actual telemetry: UNVERIFIED.

## Token and cost accounting

| Field | Value | Source/notes |
|---|---:|---|
| Input tokens | Unavailable | No task-level telemetry exposed |
| Cached input tokens | Unavailable | No task-level telemetry exposed |
| Cache-write input tokens | Unavailable | No task-level telemetry exposed |
| Output tokens | Unavailable | No task-level telemetry exposed |
| Reasoning output tokens | Unavailable | No task-level telemetry exposed |
| Total tokens | Unavailable | No task-level telemetry exposed |
| Estimated cost | Unavailable | No reliable task-level price allocation |
| Goal-service total | Unavailable | Reported separately; not added to this task |

Cost formula/basis: unavailable; no per-worker or review cost is invented.

## Numerical differences

No reproduced numerical difference is admitted. The assertion lexemes are source
regression guards, not reproduced outputs, thresholds, or golden values.

## Deferred validation

- DRAGON execution, semantic export, invariants, and T6 repeat - owned by XSEC-03-R1.
- DONJON static-core authority and run - deferred to XSEC-CORE-01 and XSEC-04.
- Converter/runtime/Core/CLI/Unity consumption - deferred to XSEC-05 through XSEC-07.

## Blockers, risks, and follow-up

- Blockers: none for this authority task.
- Risk triggers: source-deck identity changed; T3 and independent high review passed.
- Risks accepted or deferred: XSEC-RIGHTS-01 internal-use/raw-data restrictions remain unchanged. V2 is candidate-only.
- Follow-up work: XSEC-03-R1 must use only v2, retain all assertions, omit only the two SPH calls, and perform two fresh matching semantic exports.

## Next eligible task

`XSEC-03-R1` - fresh DRAGON5 TCWUX11/JEFF v2 lattice reproduction and T6 semantic-export comparison.

Source: [`AGENTS.md`](../../AGENTS.md), [`Implementation_plan.md`](../Implementation_plan.md),
[`PROJECT_SCOPE.md`](../PROJECT_SCOPE.md), [`XSEC-03.md`](XSEC-03.md), and
[`XSEC-DECK-01-REQUEST.md`](XSEC-DECK-01-REQUEST.md).
