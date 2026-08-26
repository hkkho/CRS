# XSEC-RUNNER-01 - reproduce the official TCWUX11 runner context

## Outcome

Status: COMPLETE

The supported DRAGON5 `rdragon` launcher was reproduced in an external-only,
network-isolated container context and ran the hash-bound official TCWUX11
procedure unchanged through all four source assertions and normal completion.
Two clean targeted runs had identical normalized listings and assertion
deltas. The full source-suite entry point still stops earlier in TCWUX01; that
pre-TCWUX11 failure is preserved as diagnostic evidence and is not attributed
to TCWUX11.

An additional audit-only clean run wrote the hash of the WLUP172 bytes copied
by the launcher access hook; it matches the staged source identity and also
passed all four assertions with the same normalized listing.

The complete runner contract is recorded in
`reference/manifests/xsec-runner-01-provenance-v1.json`. This task establishes
only launch/procedure-loader reproducibility. It does not admit source output,
cross sections, a semantic export, a mapping revision, a runtime pack, or
golden/reference evidence.

Effectiveness: SUCCESS

## Execution and model evidence

- Role: root implementer; code review (high) reviewer requested.
- Requested model / reasoning: GPT-5.6 Luna, high; code review (high).
- Actual model / reasoning: UNVERIFIED.
- Execution receipt or telemetry source: local thread; independent review
  submission pending at report-writing time.
- Attempts: five external-only probes/runs; two clean successful TCWUX11
  reproductions; elapsed time: Unavailable.
- Artifact/checkpoint status: produced: path-free provenance manifest, task
  report, and bounded next-task definition. Raw sources/data/logs remain
  external-only.
- Review disposition: PASS.
- Review evidence verification: UNVERIFIED.
- Reviewer reuse/fresh-review rationale: same reviewer
  `01a03b98-9b86-7f22-9068-76bb1ce6b01e`, preserving XSEC source/mapping
  context as required by `AGENTS.md`.

## Files created or changed

- `reference/manifests/xsec-runner-01-provenance-v1.json` - path-free tool,
  fixture, launcher, attempt, and output-identity evidence.
- `reference/manifests/xsec-runner-01-listing-normalization-v1.json` -
  hash-bound, minimal volatile-diagnostics normalization rule for listing
  identity comparison.
- `docs/tasks/XSEC-RUNNER-01.md` - immutable execution evidence.
- `docs/tasks/XSEC-SPH-02-REQUEST.md` - separately bounded next task
  definition; no capture or data work is included here.
- `docs/PROJECT_SCOPE.md` - updated after this report.
- `docs/tasks/ROUND-2026-08-25-XSEC-DATA-CHAIN.md` - updated routing only.

Existing source decks, procedures, data libraries, temporary drivers,
container files, and run logs were inspected externally and not added to the
repository.

## Assumptions and design choices

- Applicable P1-T08 literature digest rows are `S1-R04`, `S1-R05`, `S5-R03`,
  `S5-R04`, and `S6-R03`. This task concerns only tool launch semantics and
  does not derive a physics authority from them.
- The source distribution's `rdragon` contract is authoritative for this
  runner task: input under `Dragon/data`, same-basename `_proc` procedure
  directory, optional same-basename access hook, temporary copy/execution,
  and result storage under the platform directory.
- The access hook only copied the already hash-bound WLUP172 file into the
  launcher's temporary work directory. It did not transform it or alter any
  source procedure.
- The full-suite TCWUX01 failure is a separate pre-target source/tool
  compatibility finding. The successful target driver retained the original
  complete procedure declaration verbatim, called only TCWUX11, and used the
  unchanged source procedure files. It is sufficient for this task's narrowly
  scoped TCWUX11 runner reproduction, but not a whole-suite PASS.

## Validation commands and results

### T0 - launcher and fixture identity

```text
docker run --rm --network none --read-only --tmpfs /tmp:exec --tmpfs /run \
  --entrypoint sh <pinned-image> -c 'sha256sum Dragon rdragon; uname -sm'
Get-FileHash -Algorithm SHA256 <external target driver, access hook, source procedures, WLUP172, results>
```

Result: PASS. The pinned image, Linux x86_64 platform, Dragon executable
`3121023a...1510cd0`, rdragon launcher `ae1c4013...8388af`, four source
identities, command settings, and every external result hash are bound in the
provenance manifest. It also contains the exact path-free command, effective
environment, mount topology, access-hook operation, mechanical allowed-delta
record between the full and targeted entry points, and output/log identities.

### T1 - source entry point and targeted TCWUX11 reproduction

```text
docker run --rm --network none --read-only --tmpfs /tmp:exec --tmpfs /run \
  --entrypoint sh -v <external-data>:/dragon/5.1/Dragon/data:ro \
  -v <external-results>:/dragon/5.1/Dragon/Linux_x86_64 \
  -w /dragon/5.1/Dragon <pinned-image> \
  -c './rdragon -q tcwux11target.x2m'
```

Result: PASS twice (exit 0). Both clean runs recorded four `TEST SUCCESSFUL`
markers, `test TCWU11 completed`, and DRAGON normal end. The assertion
relative deltas were `2.131634e-06`, `9.490768e-07`, `9.059420e-06`, and
`7.328338e-06` for the four official reference lexemes, respectively.

The raw listing hashes differ only because module timing/memory and CLE CPU
time are volatile. After replacing only those diagnostics, both listings hash
to `546b6db39ba5bfa2a6872f651d1279076a61c471dcaf800d742763f09015f42d`.
No numerical nondeterminism was observed.

The audit-only third run recorded a copied-WLUP172 SHA-256 equal to the
hash-bound staged identity. Its access hook is separately hashed and its full
result, audit-log, normalization-rule, and assertion evidence are all bound in
the provenance manifest.

The unmodified full source-suite entry point was also executed through the
same launcher. It stopped before TCWUX11 in TCWUX01 with the recorded
SYBIL/EXCELL track-type error; this is not a TCWUX11 failure.

### T3 / T6

Not run. No repository runtime/numerical interface, mapping, or data changed.
T6 belongs to a later authorized XSEC-03 rerun after XSEC-SPH-02.

## Token and cost accounting

| Field | Value | Source/notes |
|---|---:|---|
| Input tokens | Unavailable | No task-level telemetry exposed. |
| Cached input tokens | Unavailable | No task-level telemetry exposed. |
| Cache-write input tokens | Unavailable | No task-level telemetry exposed. |
| Output tokens | Unavailable | No task-level telemetry exposed. |
| Reasoning output tokens | Unavailable | No task-level telemetry exposed. |
| Total tokens | Unavailable | No task-level telemetry exposed. |
| Estimated cost | Unavailable | No verified price/usage receipt. |
| Goal-service total | Not allocable | Goal service is not added to task totals. |

Cost formula/basis: unavailable; no per-task provider billing telemetry was
exposed.

## Numerical differences

No runtime or golden numerical behavior changed. The external source assertion
deltas and the run-to-run normalized-listing comparison are recorded above and
in the provenance manifest.

## Deferred validation

- A source-supported pre-SPH snapshot/capture proof - deferred to
  `XSEC-SPH-02` because that is a separate source-stage/mapping-authority task.
- Fresh reference reruns, semantic export, and T6 - deferred to `XSEC-03-R2`
  after XSEC-SPH-02 independently establishes the capture authority.

## Blockers, risks, and follow-up

- Blockers: none for the narrow TCWUX11 runner objective.
- Risk triggers: none. The full-suite TCWUX01 incompatibility is preserved as
  a separate limitation and does not invalidate the isolated TCWUX11 result.
- Risks accepted or deferred: the targeted driver is not a whole-source-suite
  PASS and does not admit any source output to the project.
- Follow-up work: execute `XSEC-SPH-02` exactly as defined; do not start
  XSEC-03-R2 until its capture authority is complete and reviewed.

Independent review: final disposition `PASS` by reviewer
`01a03b98-9b86-7f22-9068-76bb1ce6b01e`, telemetry UNVERIFIED. The reviewer
required exact path-free command/environment/mount evidence, a mechanical
driver-delta record, access-hook byte-copy proof, a hash-bound listing
normalization rule, and four assertion records for both baseline runs. These
corrections are in the two runner manifests. The final recheck additionally
confirmed that the audit hook differs from the baseline hook by only the
single hash-output line bound in the provenance record.

## Next eligible task

`XSEC-SPH-02 - prove non-mutating pre-SPH capture for TCWUX11`.

Source: [`AGENTS.md`](../../AGENTS.md) and
[`CODEX_TASK_TEMPLATE.md`](../../CODEX_TASK_TEMPLATE.md).
