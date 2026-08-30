# XSEC-ENERGY-02 - fresh SAPHYB/WCFIELD cross-artifact semantic decode

## Outcome

Status: `BLOCKED`

The task fresh-ran the approved deck-v6 SAPHYB and WCFIELD terminal-capture
variants, then stopped before cross-section value decoding. The SAPHYB artifact
contains the expected `ENERGIE F.` reaction label, two calculations, one
mixture per calculation, and two groups, but its mixture directories contain no
`RDATAX` cross-section array and report non-positive cross-section-array
length. Mapping v5 consequently has no fission-energy values to transform or
compare. The candidate is rejected fail-closed.

Effectiveness: `BLOCKED`

## Execution and model evidence

- Role: root implementer.
- Requested model / reasoning: GPT-5.6 Luna / high by repository routing.
- Actual model / reasoning: `UNVERIFIED`.
- Execution receipt or telemetry source: local task thread; model/effort receipt unavailable to the repository.
- Attempts: 2 fresh isolated source runs, one per terminal capture target; elapsed time: unavailable.
- Artifact/checkpoint status: produced.
- Review disposition: deferred to XSEC phase closeout by the owner sequencing decision recorded in `XSEC-03-R4-OWNER-APPROVAL.md`.
- Review evidence verification: `UNVERIFIED`.
- Reviewer reuse/fresh-review rationale: not applicable; no review was requested at this task boundary.

## Files created or changed

- `docs/tasks/XSEC-ENERGY-02-OWNER-APPROVAL.md` - bounded owner authority.
- `docs/tasks/XSEC-ENERGY-02-REQUEST.md` - immutable task request.
- `reference/manifests/xsec-energy-02-cross-artifact-decision-v1.json` - path-free rejection evidence.
- `docs/tasks/XSEC-ENERGY-02.md` - this report.
- `docs/PROJECT_SCOPE.md` - delivery-state handoff after this report.

Inspected but not changed: deck v6, mappings v1/v4/v5, source inputs,
libraries, captures, source/derived values, Core, CLI, Unity, runtime
schemas/packs, DONJON/full-core artifacts, and unrelated
`docs/tasks/P10-T05-R1.md`.

## Assumptions and design choices

- Pinned IGE-335/IGE-351 documents and matching Version5 source were primary
  evidence. P1-T08 rows `S1-R04`, `S1-R05`, `S5-R02`, `S5-R03`, `S5-R04`,
  `S5-R09`, and `S6-R03` were methodology/applicability context only.
- Paired source roots differ only at the final external capture target. Both
  retained deck-v6's original source operations, SAPHYB initialisation and
  recoveries, three EDI lexemes, two SPH calls, `WHOLECELL0`, GOLVER, and four
  assertions.
- The source failure is structural, not an interpretation of a numerical
  result. SAPCA2 constructs `MEVF * NFTOT` while processing selected
  microscopic isotopes; its macroscopic branch instead retrieves a record
  named by the selected reaction from the input MACROLIB. WCFIELD has no
  `ENERGIE F.` MACROLIB record, so the macro cross-section array remains empty.
- Micro-level aggregation to a homogenized macro field is not inferred. It
  would require a separately approved source/mapping specification and is not
  a repair available to this task.
- The host underflow/denormal notice recurred during the fresh runs. It is not
  relied on for acceptance and does not change the independent structural
  blocker.

## Validation commands and results

### T0 - authority and primary-source route check

```text
Get-Content -LiteralPath AGENTS.md, docs\Implementation_plan.md, <P1-T08 and XSEC prerequisites>
rg -n -C 6 'ENERGIE F.|MEVF|NFTOT|RDATAX|MACR' <pinned SAP/SAPCA2 source and IGE-335/IGE-351 guides>
git status --short
```

Result: pass. The task boundary, literature rows, unit/route authorities, and
unrelated untracked P10 draft were established before edits. Source code shows
the microscopic fission-energy multiplication and the distinct macro-record
lookup path.

### T1/T2 - fresh capture and structural decoder check

```text
docker run --rm --entrypoint /bin/sh --network none --read-only --tmpfs /tmp:exec --tmpfs /run -e OMP_NUM_THREADS=1 -v <disposable-root>:/dragon/5.1/Dragon <pinned DRAGON5 image> -lc './rdragon -q tcwux11sphcap.x2m'
docker run --rm --network none --read-only --tmpfs /tmp:exec <pinned DRAGON5 image> <external structural decoder>
```

Result: both runs reached normal end with four original assertion successes.
The external decoder observed WCFIELD's two states/two groups and SAPHYB's two
calculations/one mixture/two groups plus the `ENERGIE F.` label, then rejected
both SAPHYB calculations because `RDATAX` was absent and its declared length
was non-positive. No numerical cross section, flux, or derived value was read.

### T6 - fresh source reproduction

```text
Compare source-control completion/assertion outcomes for the paired fresh deck-v6 capture roots.
```

Result: pass for source reproduction; both source roots completed with retained
controls. Candidate semantic admission is blocked by the missing field,
independently of source-run completion.

## Token and cost accounting

| Field | Value | Source/notes |
|---|---:|---|
| Input tokens | Unavailable | No repository-visible telemetry. |
| Cached input tokens | Unavailable | No repository-visible telemetry. |
| Cache-write input tokens | Unavailable | No repository-visible telemetry. |
| Output tokens | Unavailable | No repository-visible telemetry. |
| Reasoning output tokens | Unavailable | No repository-visible telemetry. |
| Total tokens | Unavailable | No repository-visible telemetry. |
| Estimated cost | Unavailable | No verified provider receipt or price basis. |
| Goal-service total | Unavailable | Not exposed to this task. |

Cost formula/basis: unavailable; no request-level billing telemetry was exposed.

## Numerical differences

Not applicable. The required SAPHYB cross-section array was absent, so no
source value, converted value, ratio, difference, or tolerance was calculated
or retained.

## Deferred validation

- Value-level cross-artifact flux equality, mapping invariants, and candidate
  admission - blocked by absent SAPHYB `RDATAX`.
- T3 full Core/CLI/Golden regression and code review (high) - deferred together to XSEC phase closeout by owner sequencing; no runtime-facing change occurred.
- T4 Unity validation - not applicable; no Unity contract, asset, or code changed.

## Blockers, risks, and follow-up

- Blocker: `XSEC-ENERGY-02-MISSING-MACRO-EFIS`. The current SAPHYB macro route
  carries a reaction label but no `RDATAX` fission-energy data.
- Risk triggers: required source-field identity/artifact mismatch; fail-closed
  rule applied before numerical decode.
- Risks accepted or deferred: a microscopic `MEVF * NFTOT` branch exists but
  has no approved homogenization/aggregation mapping to replace the missing
  macro field.
- Follow-up work: none until a separately owner-approved task defines a
  lawful, source-proven homogenized macro fission-energy route or rejects the
  pipeline conclusively. It must not aggregate microscopic values, relabel a
  record, or change mapping v5 without a new approved specification.

## Next eligible task

None until a separately owner-approved macro fission-energy source-route task
is defined.

Source: [`AGENTS.md`](../../AGENTS.md).
