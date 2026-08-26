# TEST-INFRA-02 - portable CLI approved-data artifact recovery

**Status:** AUTHORIZED TASK DEFINITION

**Authority:** the owner-authorized licensed cross-section data workstream and
the XSEC-02 portable-artifact T3 failure recorded in `XSEC-02.md`. This is a
separate, non-physics recovery task; it does not reopen XSEC-02 mapping choices.

## Objective

Make the already-approved Phase 8 CLI scenario, scoring, and baseline-policy
data declarations available in a relocated `dotnet test --artifacts-path`
output tree, preserving their current fail-closed runtime validation and
repository-relative manifest/approval bindings.

## Frozen inputs

- `AGENTS.md`, `docs/Implementation_plan.md`, `docs/PROJECT_SCOPE.md`, and
  `docs/tasks/TEST-INFRA-01.md`.
- The existing `Phase8ScenarioParameterPack`, `Phase8ScoringParameterPack`,
  and baseline-policy loading behavior. Their `FindDefaultPath`, manifest,
  approval-record, hashing, and validation semantics are frozen for this task.
- Existing approved artifacts and manifests under `data/scenarios/`, owner
  records `P8-T02-OWNER-APPROVAL.md`, `P8-T03-OWNER-APPROVAL.md`, and
  `P8-T05-OWNER-APPROVAL.md`, and the root `AGENTS.md` marker required by the
  current manifest binding.

Literature digest applicability: `NotApplicable`; this task selects no physics
equation, constant, unit, normalization, tolerance, golden value, source data,
or numerical baseline.

## Bounded implementation

The task may change only:

- `tests/ReactorSim.Cli.Tests/ReactorSim.Cli.Tests.csproj` to declare copies of
  the exact existing approved scenario/scoring/policy artifacts, their
  manifests, the three existing approval records, and `AGENTS.md` into the test
  assembly output tree while preserving the repository-relative paths expected
  by current validation.
- A focused CLI-test file only if an output-root assertion is required to prove
  the declared layout. It must not alter runtime code or data semantics.
- `docs/tasks/TEST-INFRA-02.md` and `docs/PROJECT_SCOPE.md` for evidence and
  routing.

It must not change `src/`, `unity/`, any pack/manifest/approval bytes, XSEC
documents, physics specifications, schemas, tolerances, golden values, CI,
dependencies, or runtime path-discovery behavior. It may not copy raw JEF/JEFF,
DRAGON5, or DONJON5 data into the repository or test output.

## Required validation and completion

Run `git diff --check`, a focused CLI output-layout test when added, the direct
full headless solution suite, and the standard
`Test-FullHeadlessSuite.ps1 -ConfirmFullSuite` wrapper with a fresh temporary
`-ArtifactsPath`. Record exact counts. The wrapper must pass Core, Golden, and
CLI with zero failures/skips before this recovery can be complete. Verify final
diff/status, write the task report, reconcile scope, and commit the task
checkpoint. Independent review is not required unless implementation crosses
the frozen runtime/pack boundary or another AGENTS.md trigger fires.

## Stop conditions

Stop if copying declared test inputs cannot preserve the existing manifest and
approval binding without changing runtime behavior, or if a fix requires a
schema, approval, dependency, physics, or data change. In that case report the
exact conflict and do not modify XSEC-02.

## Next routing

After a passing checkpoint, define `XSEC-02-R1` as a revalidation-only task to
rerun its mandatory portable T3 and either complete or retain the mapping
blocker. `XSEC-03` remains ineligible until that revalidation passes.
