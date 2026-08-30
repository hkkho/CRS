# XSEC-EFIS-01 - EFIS export identity specification

## Outcome

Status: `BLOCKED`

The task established a bounded, primary-source rejection decision for the
existing EDI WCFIELD MACROLIB path. Its `MICR ... REAC ... EFIS` wording filters
output-microlib reaction entries; it does not create or prove an EDI MACROLIB
`EFIS` record. The current route therefore cannot supply the exact mapping-v1/v4
fission-only energy-production field. `PRODUCTION` and `H-FACTOR` were rejected
without numerical decoding or substitution.

Effectiveness: `BLOCKED`

## Execution and model evidence

- Role: root implementer.
- Requested model / reasoning: GPT-5.6 Luna / high by repository routing.
- Actual model / reasoning: `UNVERIFIED`.
- Execution receipt or telemetry source: local task thread; model/effort receipt
  unavailable to the repository.
- Attempts: 1; elapsed time: unavailable.
- Artifact/checkpoint status: produced.
- Review disposition: deferred to XSEC phase closeout by the owner sequencing
  decision recorded in `XSEC-03-R4-OWNER-APPROVAL.md`.
- Review evidence verification: `UNVERIFIED`.
- Reviewer reuse/fresh-review rationale: not applicable; no review was requested
  at this task boundary.

## Files created or changed

- `docs/tasks/XSEC-EFIS-01-OWNER-APPROVAL.md` - bounded authority record.
- `docs/tasks/XSEC-EFIS-01-REQUEST.md` - immutable task boundary and acceptance
  criteria.
- `docs/spec/xsec-efis-export-v1.md` - source-identity rejection specification.
- `reference/manifests/xsec-efis-01-route-decision-v1.json` - path-free,
  non-numerical evidence manifest.
- `docs/tasks/XSEC-EFIS-01.md` - this task report.
- `docs/PROJECT_SCOPE.md` - delivery-state handoff after the report.

Inspected but not changed: the active source mappings v1/v4, lattice deck v5,
all predecessor task reports, Core, CLI, Unity, source decks, source data,
and runtime-pack artifacts.

## Assumptions and design choices

- The task treated IGE-335 and the matching pinned DRAGON5 source as primary
  evidence. The P1-T08 rows `S1-R04`, `S1-R05`, `S5-R02`, `S5-R03`, `S5-R04`,
  `S5-R09`, and `S6-R03` were read as methodology/limitation context only.
- The active mappings require fission-only `EFIS` in `eV cm^-1`; that approved
  requirement was not modified or relaxed.
- Static source proof was sufficient to reject the only retained candidate
  syntax, so no fresh source run was performed. This avoids creating a new
  numerical capture or treating a label as physics evidence.
- `PRODUCTION` was rejected because the EDI rate-slot source defines it as
  fixed sources / productions. `H-FACTOR` remained forbidden by active
  mappings. No field was relabelled, transformed, reconstructed, or inferred.
- The externally retained primary artifacts are identified only by canonical-LF
  hashes. No external path, raw procedure, listing, capture, array, or numeric
  value was retained in the repository.

## Validation commands and results

### T0 - document and primary-source identity checks

```text
Get-Content -LiteralPath 'docs\\tasks\\TASK_REPORT_TEMPLATE.md' -Raw
rg -n -C 3 'XSEC-03-R4|XSEC-FIELD-01|XSEC-DIFF-01' docs\\PROJECT_SCOPE.md
git status --short
```

Result: exit code 0. The required report structure, predecessor handoff, and
unrelated untracked `docs/tasks/P10-T05-R1.md` were identified before edits.

```text
$root = '<external pinned Version5 root>'; rg -n -C 3 'REAC|EFIS|output microlib|output.*microlib' <IGE335 sections>
```

Result: exit code 0. Primary documentation identifies EDI `REAC` as output
microlib selection and SAP `EFIS` as fission-only energy production.

```text
$srcRoot = '<external pinned Version5 root>\\Dragon\\src'; rg -n -C 5 'NW\\+4|RATECM|NOUT|PRODUCTION|H-FACTOR|EFIS' <EDI and SAP sources>
```

Result: exit code 0. The EDI source traces `NOUT/HVOUT` to microlib writing
and defines the MACROLIB `PRODUCTION` source slot as fixed sources / productions.

### T1 - static source-route decision check

```text
Get-Content <external pinned EDIMIC source> | Select-Object -Skip 760 -First 80
rg -n -C 3 'EFIS|fission|eV|H-FACTOR|PRODUCTION|forbidden' docs\\spec\\xsec-source-runtime-mapping-v1.md docs\\spec\\xsec-source-runtime-mapping-v4.md
```

Result: exit code 0. `NOUT/HVOUT` filtering appears inside the output-microlib
write block, and the active mappings confirm the required EFIS identity and
forbid H-FACTOR substitution.

### T2 - final manifest and worktree checks

```text
$manifest = Get-Content -LiteralPath 'reference\\manifests\\xsec-efis-01-route-decision-v1.json' -Raw | ConvertFrom-Json
if ($manifest.schema -ne 'xsec-efis-route-decision-v1' -or $manifest.disposition -ne 'REJECTED_BLOCKED' -or $manifest.observations.numericDataRead) { throw 'Invalid EFIS route decision manifest.' }
if ((Get-Content -LiteralPath 'reference\\manifests\\xsec-efis-01-route-decision-v1.json' -Raw) -match '(?i)([A-Z]:\\\\|/users/|appdata|temp)') { throw 'Path leakage detected.' }
git diff --check
```

Result: pass; the manifest parsed, declared the correct fail-closed state,
contained no path marker, and the final diff had no whitespace error.

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

Not applicable; no numerical behavior changed and no numerical source data was
read.

## Deferred validation

- T3 full Core/CLI/Golden regression and code review (high) - deferred together
  to XSEC phase closeout by the owner sequencing decision.
- T4 Unity validation - not applicable; no Unity contract or asset changed.
- T6 fresh source rerun - not applicable because static primary-source proof
  rejected the sole in-boundary candidate before a viable probe existed.

## Blockers, risks, and follow-up

- Blocker: no documented and source-proven EDI WCFIELD MACROLIB writer path
  produces mapping-required fission-only `EFIS`.
- Risk triggers: source-field identity, unit, normalization, and writer-path
  mismatch; fail-closed rule applied.
- Risks accepted or deferred: SAP establishes reaction semantics only; its
  possible artifact path and mapping consequences are deliberately unassessed.
- Follow-up work: none is eligible until a separately owner-approved task
  specifies a different source-to-runtime energy-field route and its exact
  artifact, identity, units, normalization, mapping, and preservation impact.

## Next eligible task

None until a separately owner-approved source-to-runtime energy-field route
task is defined.

Source: [`AGENTS.md`](../../AGENTS.md).
