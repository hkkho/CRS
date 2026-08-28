# XSEC-03-R2 - SPH-preserving DRAGON lattice semantic export

## Outcome

Status: BLOCKED

Four fresh network-isolated DRAGON5 reproductions succeeded: all passed the
four unchanged source assertions, source completion, and the recorded
source-emitted convergence/iteration detection rule. The two PRE0 captures
were byte-identical, the two PRE172 captures were byte-identical, and the
states were distinct.

The candidate cannot progress to field decoding or mapping. Both approved
MACROLIB selectors expose multiple mixture elements, but the inherited mapping
requires one explicitly named homogenized whole-cell mixture and does not name
or locate it. Selecting a mixture index, inferring one from order, or deriving
a whole-cell mixture would change numerical data authority. The task therefore
fails closed before any cross-section value is decoded.

Effectiveness: PARTIAL

Applicable P1-T08 rows are S1-R04, S1-R05, S5-R02, S5-R03, S5-R04, S5-R09, and
S6-R03, used only as methodology/applicability context. No row is numerical,
runtime, reference, or golden authority.

## Execution and model evidence

- Role: root implementer; independent code review (high) requested.
- Requested model / reasoning: GPT-5.6 Luna / high; code review (high).
- Actual model / reasoning: UNVERIFIED.
- Execution receipt or telemetry source: local Codex thread; reviewer
  01a03b98-9b86-7f22-9068-76bb1ce6b01e. No provider receipt exposes the
  actual model/effort.
- Attempts: four clean external reference runs, two per approved capture
  selector; one task-definition review correction cycle; elapsed time:
  unavailable.
- Artifact/checkpoint status: produced: owner approval, task request, path-free
  run/semantic manifest, task report, and scope routing. Raw inputs, captures,
  listings, and decoded values remain external-only.
- Review disposition: PASS — blocked outcome upheld. The final same-reviewer
  recheck found the official TCWUX11 canonical-LF identity bound at the source
  and every run record, and the scope routing accurately updated.
- Review evidence verification: UNVERIFIED.
- Reviewer reuse/fresh-review rationale: the same XSEC reviewer performed the
  required task-definition review. The initial CONDITIONAL PASS required an
  explicit hash-bound source convergence/iteration record; the same reviewer
  rechecked that correction and returned task-definition PASS. The final
  blocked-outcome recheck initially identified two documentation evidence
  omissions (official TCWUX11 identity per run and current scope routing); both
  were corrected, then the same reviewer returned PASS with no remaining
  findings.

## Files created or changed

- docs/tasks/XSEC-03-R2-OWNER-APPROVAL.md - owner authority for this bounded
  rerun and semantic preselection task.
- docs/tasks/XSEC-03-R2-REQUEST.md - reviewed task definition and fail-closed
  execution/retention boundary.
- reference/manifests/xsec-03-r2-run-semantic-v1.json - path-free official
  source/capture-variant input, run, convergence, determinism, and blocked
  semantic-preselection evidence.
- docs/tasks/XSEC-03-R2.md - this report.
- docs/PROJECT_SCOPE.md - current XSEC status after this report.

All mapping/deck authorities, source inputs, library bytes, drivers, hooks,
captures, listings, decoded values, Core/CLI/Unity code, data packs, and
schemas were inspected but not changed in the repository.

## Assumptions and design choices

- The owner authorization was bounded to the reviewed mapping v2/deck v3
  candidate. Both source SPH calls, all source assertions, source controls,
  group identity, and inherited field meanings stayed unchanged.
- The FILE-backed capture interface permits one selected state per run. Four
  fresh roots were therefore required: two PRE0 and two PRE172.
- Source listing status was checked without inventing a tolerance. The
  detection rule required four assertion markers, the normal completion
  markers, source-emitted external/power convergence lines, and no matching
  abnormal/nonconvergence diagnostic.
- The reported terminal underflow/denormal floating-point note was recorded as
  a nonfatal tool diagnostic. It did not prevent normal completion, source
  assertions, capture determinism, or source convergence markers. No selected
  coefficient was decoded, so no numerical validity claim is made from it.
- The semantic gate stopped at the multiple-mixture structure. No mixture was
  inferred from ordering, volume, index, or an unstated whole-cell assumption.

## Validation commands and results

### T0 - authority/input identity

~~~text
Get-FileHash -Algorithm SHA256 <external capture procedure, TCWU05Lib,
assertS, WLUP172, driver, access hook, save hook>
docker run --rm --network none --read-only --tmpfs /tmp:exec --tmpfs /run
  ... <pinned DRAGON5 image> -c './rdragon -q tcwux11sphcap.x2m'
~~~

Result: PASS. The pinned image, Dragon/rdragon identities, original official
TCWUX11 procedure, official library, assertion procedure, WLUP172, driver,
capture variants, and hooks are
hash-bound in the manifest. Both source SPH calls remained preserved. Final
static JSON/scope validation passed; the path-free manifest SHA-256 is
`82814ca2ed8edd2182972ba8529a66363367122ce62a8a5c9e0f9f225392389a`.

### T6 - fresh source reproduction and deterministic capture

~~~text
Four independent clean external roots: PRE0 a/b and PRE172 a/b.
For each: invoke the pinned offline rdragon command with network disabled,
read-only root, executable /tmp tmpfs, /run tmpfs, and OMP_NUM_THREADS=1.
~~~

Result: PASS. All four runs exited successfully, contained four source assertion
success markers, normal completion, and ten extracted source convergence/iteration
markers with no matching abnormal/nonconverged diagnostic. Per-state captures
and normalized listings were byte-identical; the two states were distinct.
Exact hashes and detection fingerprints are in the manifest.

### T1 - semantic preselection / fail-closed mixture gate

~~~text
Inspect only the approved direct REF-CASE/MACROLIB selectors:
PRE0/REF-CASE0001/MACROLIB and PRE172/REF-CASE0002/MACROLIB.
Assert selector uniqueness, expected two-group ENERGY identity, day-300
timestamp, and no direct SPH, SPH-EPSILON, or ADF subtree.
Before decoding field arrays, require an approved explicit whole-cell mixture
identity and locator.
~~~

Result: BLOCKED as designed. The selector/energy/correction-subtree checks pass,
but each selected MACROLIB represents multiple mixture elements. Mapping v1/v2
contains no exact mixture name, index, state locator, or whole-cell
homogenization rule. The required field array cannot be mapped without choosing
a numerical source row, so no field value was decoded.

### T3/T4/T5

Not run. This task changed no repository runtime, numerical interface,
data-pack artifact, Core/CLI/Unity code, or serialization contract. The
mandatory reference reproduction level was T6 and it ran.

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

Not applicable. No candidate row, coefficient, transform, runtime behavior,
reference baseline, or golden value was admitted. The manifest contains only
hashes, counts, identities, and boolean outcomes.

## Deferred validation

- Field decode, units, scattering-profile decode, finite/invariant checks,
  source-to-Core transformation, semantic fingerprints, and candidate mapping
  validation are blocked pending a separate approved mixture-selection
  specification.
- Converter/pack, Core/CLI consumer, Unity consumer, DONJON/full-core work,
  plots/PDF, and golden/reference admission remain out of this task.
- T3/T4/T5 are not triggered by the external-only blocked evidence.

## Blockers, risks, and follow-up

- Blocker: no approved source mixture identity, exact locator, or
  whole-cell-homogenization rule exists for the one mixture that mapping v1/v2
  requires. The selected MACROLIB contains multiple mixture elements.
- Risk trigger: source-to-runtime field selection ambiguity. Required response:
  stop before numerical decode and preserve path-free evidence.
- Risks accepted or deferred: source runs are reproducible but do not prove
  that any mixture is an admissible runtime coefficient source. The terminal
  underflow/denormal note remains a recorded nonfatal source-tool diagnostic,
  not an ignored numerical result.
- Follow-up work: create and independently review a separate bounded
  mixture-selection specification/owner-approval task. It must name the exact
  source mixture identity and selector for both states, its source-supported
  whole-cell/homogenization meaning, units, and the no-guess mapping rule.
  XSEC-03-R2 must then be re-executed; do not reuse this blocked result as a
  candidate-field admission.

## Next eligible task

None until a separately approved, bounded source-mixture selection
specification is defined. XSEC-03-R2 must be rerun after that specification.

Source: AGENTS.md, XSEC-03-R2-REQUEST.md, XSEC-MAP-R2.md, and
xsec-03-r2-run-semantic-v1.json.
