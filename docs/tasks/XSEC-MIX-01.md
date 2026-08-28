# XSEC-MIX-01 - source mixture selection specification

## Outcome

Status: BLOCKED

The requested selection task is complete as a fail-closed negative decision.
The official TCWUX11 procedure maps its 31 source regions into ten mixture IDs,
and the two mapping-v2-selected MACROLIBs each contain ten mixtures and ten
per-mixture volumes. Official data-structure semantics identify those as a set
of mixture records, not a named whole-cell record. No unique source-supported
whole-cell locator exists in the approved capture boundary.

No mixture index, field value, volume weighting, material interpretation,
coefficient, runtime pack, reference/golden case, or Core/CLI/Unity behavior
was selected or changed.

Effectiveness: BLOCKED

Applicable P1-T08 rows are S1-R04, S1-R05, S5-R02, S5-R03, S5-R04, S5-R09,
and S6-R03. They remain methodology/applicability context only; the decision
uses the hash-bound official source procedure and IGE-351 data-structure
guidance rather than literature numerical evidence.

## Execution and model evidence

- Role: root implementer; independent code review (high) requested.
- Requested model / reasoning: GPT-5.6 Luna / high; code review (high).
- Actual model / reasoning: UNVERIFIED.
- Execution receipt or telemetry source: local Codex thread; reviewer
  `01a03b98-9b86-7f22-9068-76bb1ce6b01e`. No provider receipt exposes actual
  model/effort.
- Attempts: one bounded task-definition review and one external structural
  source/capture inspection; elapsed time unavailable.
- Artifact/checkpoint status: produced owner approval, reviewed request,
  blocked selection decision, path-free manifest, report, and scope routing.
  Raw source, documentation, captures, listings, and values remain external.
- Review disposition: PASS — blocked no-selection outcome upheld. The final
  same-reviewer recheck confirmed the corrected XSEC-03-R2 routing and no
  source-semantic mismatch.
- Review evidence verification: UNVERIFIED.
- Reviewer reuse/fresh-review rationale: the same XSEC reviewer returned PASS
  for the task-definition boundary before external inspection. The final
  candidate used that same source/capture context. The initial final outcome
  was CONDITIONAL PASS with one Medium routing finding; the same reviewer
  rechecked the scope correction and issued final PASS with no remaining
  findings.

## Files created or changed

- `docs/tasks/XSEC-MIX-01-OWNER-APPROVAL.md` - owner authority and hard
  boundaries for selection-only work.
- `docs/tasks/XSEC-MIX-01-REQUEST.md` - reviewed one-task request.
- `docs/spec/xsec-source-mixture-selection-v1.md` - blocked, source-traceable
  no-selection decision.
- `reference/manifests/xsec-mix-01-selection-v1.json` - path-free structural
  proof and non-admission record.
- `docs/tasks/XSEC-MIX-01.md` - this report.
- `docs/PROJECT_SCOPE.md` - current XSEC routing after the decision.

All existing mapping/deck specifications, source procedures, capture
interfaces, raw data, Core/CLI/Unity code, schemas, and data packs were
inspected but not changed.

## Assumptions and design choices

- The user explicitly authorized this bounded task after XSEC-03-R2 identified
  the missing mixture-selection authority.
- The selection criterion remains mapping-v1's one explicitly named,
  homogenized whole-cell mixture. This task does not weaken it to "a" mixture
  or choose an element by position or volume.
- The capture-state metadata and official source data-structure semantics are
  adequate to prove incompatibility of the approved capture boundary, but not
  to select or calculate a replacement output.

## Validation commands and results

### T0 - source, guide, specification, and manifest identity

~~~text
Get-FileHash -Algorithm SHA256 <official TCWUX11, IGE-351 EDITION guide,
  IGE-351 MACROLIB guide, PRE0 capture, PRE172 capture>
PowerShell static assertions: parse xsec-mix-01-selection-v1.json; assert
  BLOCKED disposition, 31 assignments to IDs 1..10, two capture states,
  groupCount=2, mixtureCount=10, volumeRecordLength=10, no selected locator,
  no field decode, and path-free evidence.
git diff --check
~~~

Result: PASS. The manifest binds the exact official TCWUX11 procedure,
official data-structure source locations, and both previously fresh,
hash-matched external captures. No raw value is retained. The path-free
selection manifest SHA-256 is
`2c3c409af1ec8f3a2dcf31af38d3d4a35f40441363d4d7901d2933d48a82ce26`.

### T1 - external structural source/capture proof

~~~text
Read-only PowerShell inspection of the hash-matched official TCWUX11 EDI block
and the direct PRE0/REF-CASE0001/MACROLIB and
PRE172/REF-CASE0002/MACROLIB structures. Extract only MERG REGI cardinality,
distinct mixture IDs, MACROLIB signature, state-vector counts, VOLUME record
length, and direct mixture-name-record presence; do not decode field arrays.
~~~

Result: PASS. The EDI block has 31 assignments to ten IDs (`1..10`), not a
single whole-cell assignment. Each selected MACROLIB is `L_MACROLIB` with two
groups, ten mixtures, and a ten-entry `VOLUME` record; neither has a direct
mixture-name record. The required unique locator is therefore absent, and the
task stopped before field decoding.

### T3/T4/T5/T6

Not run. This task adds no numerical artifact, runtime interface, source
baseline, Core/CLI/Unity change, or reference result. Its risk-triggered
independent high review applies to the selection decision itself.

## Token and cost accounting

| Field | Value | Source/notes |
|---|---:|---|
| Input tokens | Unavailable | No task-level telemetry exposed. |
| Cached input tokens | Unavailable | No task-level telemetry exposed. |
| Cache-write input tokens | Unavailable | No task-level telemetry exposed. |
| Output tokens | Unavailable | No task-level telemetry exposed. |
| Reasoning output tokens | Unavailable | No task-level telemetry exposed. |
| Total tokens | Unavailable | No verified request-level receipt. |
| Estimated cost | Unavailable | No verified price/usage receipt. |
| Goal-service total | Not allocable | Not added to task totals. |

Cost formula/basis: unavailable; no task-level provider billing telemetry was
exposed.

## Numerical differences

Not applicable. No numerical source field, equation, unit, normalization,
tolerance, runtime behavior, reference baseline, or golden value changed.

## Deferred validation

- Field decode, invariant checks, source-to-Core transformation, and candidate
  admission remain unavailable because the required whole-cell locator does not
  exist in the approved source boundary.
- T3/T4/T5/T6 and converter/pack/Core/CLI/Unity/DONJON/PDF work remain deferred
  to separately authorized successor tasks.

## Blockers, risks, and follow-up

- Blocker: approved mapping v1 requires one source-named homogenized whole-cell
  mixture, while the exact official TCWUX11 EDI output has ten mixture records
  and no source-defined whole-cell locator in either selected capture.
- Risk trigger: any selection would choose a numerical source row. Required
  response: fail closed and obtain a separate source/deck/mapping authority.
- Risks accepted or deferred: a new whole-cell output may be technically
  possible, but authorizing its source operation, semantics, and fresh source
  regression proof is outside this task.
- Follow-up work: if still desired, define and independently review a separate
  owner-approved source/deck and mapping task that explicitly produces and
  names one whole-cell homogenized output. It must then schedule a fresh
  XSEC-03-R2 rerun; no existing ten-mixture record may be reused.

## Next eligible task

None. A separate owner-approved whole-cell source/deck and mapping task must
be defined before XSEC-03-R2 can be rerun.

Source: `AGENTS.md`, `XSEC-MIX-01-REQUEST.md`, mapping v1/v2, deck v3,
`XSEC-03-R2.md`, and `xsec-mix-01-selection-v1.json`.
