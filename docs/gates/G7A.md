# G7A - Phase 7A kinetics/xenon review

## Result

`CONDITIONAL PASS` for the approved synthetic/test-only Phase 7A Core scope.

The original pre-`P7-T06` checkpoint and its findings are preserved below.
The current `P7-T06` re-entry records an approved synthetic-only authority
package; it does not change this gate to an external-reference or production
pass.

P7-T01 through P7-T05 are implemented and T3-validated. The gate evidence
adds long deterministic histories, timestep-refinement comparisons, and
finite/nonnegative inventory and concentration checks. At the original
checkpoint, no applicable Phase 7 reference or golden authority was present:
the existing five golden artifacts
are Phase 4 authorities and none is a kinetics/Xe case. The conditional result
therefore does not promote synthetic traces to production, external-reference,
or golden authority; an approved Phase 7 reference package and G7A re-entry are
required for an unconditional disposition.

## Scope and authority reviewed

- Tasks/reports since the previous gate: `P7-T01` through `P7-T05`, including
  `docs/tasks/P7-T05.md` and the current Phase 7A handoff in
  `docs/PROJECT_SCOPE.md`.
- Approved specifications and ADRs: `AGENTS.md`, the complete Phase 7 and G7A
  scope in `docs/Implementation_plan.md`,
  `docs/spec/kinetics-xenon-rrs-feedback-v1.md`, and the applicable P2-T04
  contracts and reports.
- Literature authority reviewed: `docs/reference/candu-literature-digest-v1.md`
  and `docs/tasks/P1-T08.md`; applicable rows are `S1-R09`, `S4-R05`,
  `S4-R06`, and `S5-R08`. They provide methodology, context, or candidate
  case-design evidence only and do not authorize runtime constants,
  tolerances, or golden values.
- Current-scope lookup: exact `PHASE-7A` / `G7A` entry in
  `docs/PROJECT_SCOPE.md`.
- Candidate/diffs or commits inspected: committed P7-T01 through P7-T05
  history through `4d8762d`, plus the candidate
  `tests/ReactorSim.Core.Tests/G7AKineticsXenonGateTests.cs`.
- Required validation: focused G7A history tests, full headless T3 Core/CLI/
  Golden regression, whitespace/diff checks, and the golden-authority audit.
- Gate-overlap status: G6 remains conditional only for its separate synthetic
  RRS comparison boundary. It does not block this independent Phase 7A scope;
  this gate preserves its own missing-reference condition.

## Evidence and commands

### Focused G7A long-history and refinement suite (T1)

```text
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'; $env:DOTNET_CLI_TELEMETRY_OPTOUT='1'; $env:DOTNET_GENERATE_ASPNET_CERTIFICATE='0'; $env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='false'; $env:DOTNET_CLI_HOME='C:\Users\infin\AppData\Local\Temp\candu-cli-home-p8-t01'; & 'C:\Users\infin\AppData\Local\Temp\candu-sdk-10.0.302\dotnet.exe' test 'tests/ReactorSim.Core.Tests/ReactorSim.Core.Tests.csproj' --no-restore --configuration Debug --filter 'FullyQualifiedName~G7AKineticsXenonGateTests' --nologo --logger 'console;verbosity=minimal' '-p:TargetPlatformIdentifier=' '-p:TargetPlatformVersion='
```

Result: exit code `0`; `2/2` passed, `0` failed, and `0` skipped. The test
package covers a `2048`-step kinetics history, a `1024`-step I/Xe history
with high-power production followed by relaxation, deterministic replay,
finite/nonnegative inventories and number densities, and coarse/medium/fine
explicit-step refinement ordering for both kinetics and Xe final states.

The first invocation without the two empty target-platform properties stopped
before compilation with the environment-only `MSB4184` Microsoft SDK-cache
access error. The recorded command above is the successful rerun; the
properties only avoid that restricted SDK lookup and do not alter repository
behavior or project files.

### Full headless regression (T3)

```text
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'; $env:DOTNET_CLI_TELEMETRY_OPTOUT='1'; $env:DOTNET_GENERATE_ASPNET_CERTIFICATE='0'; $env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='false'; $env:DOTNET_CLI_HOME='C:\Users\infin\AppData\Local\Temp\candu-cli-home-p8-t01'; & 'C:\Users\infin\AppData\Local\Temp\candu-sdk-10.0.302\dotnet.exe' test 'ReactorSim.sln' --no-restore --configuration Release --nologo --logger 'console;verbosity=minimal' '-p:TargetPlatformIdentifier=' '-p:TargetPlatformVersion='
```

Result: exit code `0`; CLI `5/5`, Core `189/189`, and Golden `19/19` passed,
with zero failures and zero skips.

### Format and whitespace verification (T0)

```text
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'; $env:DOTNET_CLI_TELEMETRY_OPTOUT='1'; $env:DOTNET_GENERATE_ASPNET_CERTIFICATE='0'; $env:DOTNET_ADD_GLOBAL_TO_PATH='false'; $env:DOTNET_CLI_HOME='C:\Users\infin\AppData\Local\Temp\candu-cli-home-p8-t01'; & 'C:\Users\infin\AppData\Local\Temp\candu-sdk-10.0.302\dotnet.exe' format 'ReactorSim.sln' whitespace --verify-no-changes --no-restore --verbosity minimal
git diff --check
```

Result: exit code `0`; no formatting changes were required and no whitespace
errors were reported. The successful format command used the repository's
normal invocation; `dotnet format` does not accept the target-platform
properties used by the test command.

### Phase 7 golden-authority audit (T0)

```text
$all=@(Get-ChildItem -LiteralPath 'data/golden' -File -Recurse); $p7=@($all | Where-Object { $_.Name -match '(?i)(p7|kinetic|xenon|ixe)' }); Write-Output ('G7A_GOLDEN_AUDIT total_golden=' + $all.Count + ' applicable_phase7_files=' + $p7.Count); $p7 | Select-Object -ExpandProperty FullName
```

Result: exit code `0`; `G7A_GOLDEN_AUDIT total_golden=5
applicable_phase7_files=0`. The five artifacts are the existing Phase 4
manufactured/independent authorities and are not applicable Phase 7 cases.

### T4-T6 applicability

No Unity adapter, serialization contract, mobile/platform implementation, or
reference baseline changed in this gate. T4, T5, and T6 are therefore not
applicable. The gate does not claim external-reference reproduction or a
production CANDU baseline.

## Independent review evidence

- Requested lane / reasoning: independent code review (high), requested
  GPT-5.6 Luna / high reasoning.
- Actual model / reasoning: `UNVERIFIED`; reviewer telemetry is unavailable.
- Receipt or telemetry source: same-context Ampere reviewer
  `01a034f1-5b7b-7ab2-839a-6823f6d8fbcc`; no verified execution receipt was
  exposed.
- Attempts and reviewer continuity: one bounded G7A gate-review attempt; the
  existing Ampere reviewer/context was reused from P7-T05. No fresh review
  swarm was started.
- Final technical disposition: `CONDITIONAL PASS` limited to the synthetic/
  test-only Phase 7A Core scope; no actionable code findings remain.
- Evidence verification: `UNVERIFIED`.

## Findings

### Medium: applicable Phase 7 reference/golden authority is absent

- Location: G7A reference-case requirement in
  `docs/Implementation_plan.md` and the evidence-gap register in
  `docs/PROJECT_SCOPE.md`.
- Evidence and impact: the golden audit found five existing artifacts and zero
  applicable Phase 7/kinetics/Xe files. The focused traces and convergence
  comparisons therefore validate deterministic contract behavior only; they
  cannot establish a production, external-reference, or golden numerical
  baseline.
- Required action: admit a separately owner-authorized Phase 7 reference case
  with exact model/tool/version, nuclear data, geometry, units, normalization,
  observables, tolerances, provenance, and reproducible manifest, then perform
  G7A re-entry. No implementation correction is required for this gate.

No critical, high, low, or additional code findings remain.

## Numerical comparison summary

No approved external/reference/golden numerical comparison is applicable, so
no absolute or relative error, tolerance, or golden-value pass is claimed.
Internal refinement evidence is qualitative and test-asserted only:

| Observable | Synthetic evidence | Comparison status |
|---|---|---|
| Kinetics final amplitude | `0.20 s`, `0.10 s`, and `0.05 s` explicit steps over `20.0 s`; medium-to-fine difference is smaller than coarse-to-fine difference | Internal convergence only; no approved tolerance |
| Xe-135 final inventory | `0.40 s`, `0.20 s`, and `0.10 s` explicit steps over `8.0 s`; medium-to-fine difference is smaller than coarse-to-fine difference | Internal convergence only; no approved tolerance |
| Long kinetics history | `2048` steps over `204.8 s`; deterministic replay and finite/nonnegative amplitude/precursors | Synthetic evidence only |
| Long I/Xe history | `1024` steps over `102.4 s`; high-power prefix, relaxation tail, finite/nonnegative inventories/densities | Synthetic evidence only |

## Boundary review

- Dependency direction: **PASS**. The gate adds only Core tests and gate
  evidence; Core remains engine-neutral, and no Unity/runtime dependency is
  introduced.
- Determinism and failure diagnostics: **PASS**. Long histories replay to the
  same final states/digests; finite/nonnegative checks and refinement ordering
  pass; the full T3 regression is green.
- Scope exclusions: **PASS**. No shutdown/scram/trip, safety-system,
  accident-progression, thermal-hydraulic, CFD, operator-training, OpenMC,
  production CANDU, or hidden feedback behavior is admitted.
- Licensing/provenance: **PASS** for the synthetic/test-only scope. The test
  inputs are project-owned synthetic values; literature remains context only;
  no external artifact is promoted.

## Required corrective tasks and deferred evidence

- Approved Phase 7 reference/golden case and reproducible comparison manifest:
  deferred to a separately owner-authorized task, followed by G7A re-entry.
- Production/external CANDU authority, reference tolerances, and absolute
  numerical acceptance thresholds: remain deferred and are not inferred from
  the synthetic convergence tests.
- Phase 7B temperature/purity feedback remains optional and disabled: no
  approved feedback data currently enables a P7B implementation task, and this
  conditional G7A result does not silently activate it.

## Risks and next eligible work

- Accepted/deferred risks: the long histories and refinement ordering establish
  deterministic synthetic contract behavior, not physical validation against a
  named reactor or production data pack. Owner: Phase 7 reference admission;
  target: G7A re-entry.
- Dependent work blocked: unconditional Phase 7A reference/production/golden
  claims and any release claim relying on them.
- Independent overlap-eligible work: none within Phase 7B until approved
  temperature/purity feedback data and a bounded task authority are admitted.
- Next bounded work: owner-authorized Phase 7 reference-case admission and G7A
  re-entry. Phase 7B remains optional/deferred under the frozen plan.

## Token and cost accounting

Request-level input, cached input, cache-write input, output, reasoning output,
total tokens, price source/date, cost formula, and goal-service totals were not
exposed. Cost is `Unavailable`; no per-worker or per-reviewer allocation is
inferred.

## Sign-off

- Reviewer/model lane: independent code review (high), requested GPT-5.6 Luna /
  high; actual receipt `UNVERIFIED`.
- Date: `2026-08-24`.
- Final disposition: `CONDITIONAL PASS` limited to the synthetic/test-only
  Phase 7A Core scope. An approved Phase 7 reference/golden package and G7A
  re-entry are required before an unconditional comparison, production, or
  external-reference claim.

Source: [`AGENTS.md`](../../AGENTS.md),
[`docs/Implementation_plan.md`](../Implementation_plan.md), and
[`docs/PROJECT_SCOPE.md`](../PROJECT_SCOPE.md).

## P7-T06 re-entry — 2026-08-24

`P7-T06` is `COMPLETE` for the bounded authority-admission objective. The
deep-research route did not admit an external CANDU/PHWR case: the public
sources supplied context or incomplete/ non-mappable cases, while complete
official comparators were for other reactor classes or required separate
package access. The source ledger and stopping rationale are recorded in
[`docs/research/P7-T06-report-source.md`](../research/P7-T06-report-source.md).
Applicable literature rows remain `S1-R09`, `S4-R05`, `S4-R06`, and `S5-R08`;
none is promoted to an executable oracle.

The authorized alternate route is now admitted as **Synthetic / test-only**:

- Definition: `data/comparisons/p7-t06-synthetic-definition-v1.json`.
- Candidate: `data/comparisons/p7-t06-synthetic-authority-v1.json`, SHA-256
  `45abbc28094d0a21f32978316bd13cb979b7cf760ad7553d113d7cec30b30b0f`,
  `103256` bytes, status `Deferred`/`NoGolden`.
- Approved synthetic consumer: `data/golden/p7-t06-synthetic-authority-v1.json`,
  SHA-256
  `03d91f70628f09b82b28434678c60f2bc3be601fd4f2bc194e71e15e86bc643f`,
  `103268` bytes, status `Approved`/`ApprovedGolden`.
- The standalone generator has no `ReactorSim.Core` reference. Candidate and
  approved manifests bind definition/artifact bytes, preserve `Synthetic`
  coverage, and record an equal independent repeat hash.

Validation evidence for the re-entry is:

- T0 path/format audit: `10` required files present, no NUL bytes; `git diff
  --check` and pinned-SDK whitespace verification passed.
- T1/T6 standalone generation and validation: candidate and approved artifacts
  both passed exact manifest comparison, independent repeat equality, zero
  balance residual, nonnegative checks, and refinement ordering.
- Focused Golden consumer: `P7T06` `3/3` passed.
- Full T3 suite: CLI `5/5`, Core `189/189`, Golden `22/22`, zero failures and
  zero skips.
- Independent high review: `CONDITIONAL PASS`, no P0-P2 findings; reviewer
  `Goodall`, attempt `1`, requested `GPT-5.6 Luna/high`, actual receipt
  `UNVERIFIED`. The SDK pin observation was an environment note, not a source
  defect, because the required pinned SDK executable was used for the final
  format check.

Final re-entry disposition: **`CONDITIONAL PASS` remains**. The approved
package is authoritative only for deterministic synthetic/test-only regression
consumption. It is not a CANDU physics baseline, external reference, or
production tolerance source. No runtime, equation, unit, sign, convergence
rule, tolerance, public schema, or Phase 7B temperature/purity behavior was
changed or enabled. The external production/reference evidence gap remains
visible and deferred.

Required follow-up is an owner-authorized task with a complete, reproducible,
licensable CANDU/PHWR case if an external or production comparison is desired.
Phase 7B remains optional/deferred until its approved temperature/purity data
authority exists.
