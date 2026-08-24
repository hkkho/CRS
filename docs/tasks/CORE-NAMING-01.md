# CORE-NAMING-01 - task-ID type compatibility inventory and migration plan

## Outcome

Status: COMPLETE

This discovery task produced the required compatibility inventory and a
minimal no-break migration plan for task-ID-named public types. The inventory
searched authored Core, CLI, Unity, and test sources for public declarations
whose type names contain a `P#T#` task identifier.

The result is 21 public runtime Core types, all in the P6-T06 queue-transition
contract, and 34 public task-ID-named test classes. The runtime types split
into canonical-byte serialized contract components and transient internal
handoff/aggregate types. The test classes are test fixtures, not product
runtime API. No current CLI or Unity source consumer refers to a P6-T06 type;
Unity nevertheless loads the public `ReactorSim.Core` assembly, so unknown
external consumers cannot be ruled out from repository evidence alone.

No bulk rename, alias, facade, public API change, persistence change, or
serialized-byte change was made.

Effectiveness: SUCCESS

## Execution and model evidence

- Role: root implementer
- Requested model / reasoning: GPT-5.6 Luna / high
- Actual model / reasoning: `UNVERIFIED`
- Execution receipt or telemetry source: current goal/thread
  `01a031df-5487-7a53-8ec6-865fa2cb9353`; request-level receipt unavailable
- Attempts: 1; elapsed time: `Unavailable`
- Artifact/checkpoint status: produced; this report is the decision record
- Review disposition: not applicable; this task made no implementation or
  public-contract change
- Review evidence verification: not applicable
- Reviewer reuse/fresh-review rationale: not applicable; the approved task
  validation is T0 inventory plus focused build. A later public-contract
  migration requires its own independent code review (high).

## Authority, scope, and applicability

The exact `CORE-NAMING-01` row in `docs/PROJECT_SCOPE.md` and the approved
refactoring proposal authorize inventory and decision-record work only. The
proposal explicitly forbids a bulk rename and defers aliases/facades until
public API, persistence, reflection, and test impacts are known.

`P1-T08` applicability: `NotApplicable`. This task changes no physics
equation, coefficient, map value, unit, sign, normalization, convergence
rule, tolerance, reference case, golden value, or runtime behavior. It only
classifies names and records a future compatibility order.

## Inventory method and evidence

The declaration search covered public classes, enums, interfaces, structs,
records, and delegates in `src/ReactorSim.Core`, `src/ReactorSim.Cli`,
`unity`, and `tests`, using a task-ID name pattern. It found 55 declarations:

- 21 public runtime Core types, all `P6T06...` declarations in
  `src/ReactorSim.Core/Domain/Phase6QueueTransitionContracts.cs`;
- 34 public test fixture classes in the Core and Golden test assemblies; and
- no task-ID-named public runtime declaration in CLI or authored Unity source.

The consumer search found `P6T06` only in the Core contract file and the
P6-T06/P6-T07 Core test fixtures. No `P6T06` reference was found in
`src/ReactorSim.Cli` or authored `unity` source. The Unity assembly definition
references the compiled Core DLL, so public assembly exposure exists even
without a typed source reference. No direct `P6T06` reference was found in
`src/ReactorSim.Core/Serialization`; the types' compatibility-sensitive
serialization is their explicit canonical-byte and digest surface.

### Runtime public Core types

All rows below are public types, with the classification required for the
future migration. `Serialized contract` means the type or its enum/value is
part of an explicit canonical-byte/digest contract. `Internal-only handoff`
means it is a transient adapter, validation, motion-input, or aggregate type
with no direct canonical-byte representation. These classifications describe
current repository evidence; public assembly exposure means an external
consumer may still exist outside the repository.

| Type | Declaration | Classification | Compatibility evidence and risk |
|---|---|---|---|
| `P6T06QueueFamilyV1` | enum, line 16 | Serialized contract | Selects queue magic/owner identity and is embedded in canonical queue/command identity. |
| `P6T06SourceKindV1` | enum, line 26 | Serialized contract | Source kind is part of queued command identity/bytes. |
| `P6T06MotionModeV1` | enum, line 38 | Internal-only handoff | Validates motion policy at `TryMotionAndConsume`; it is a transient method input and is not directly emitted as a canonical record. |
| `P6T06QueueTransitionKindV1` | enum, line 47 | Serialized contract | Written into `P6T06QueueTransitionV1` canonical bytes. |
| `P6T06SaturationStateV1` | enum, line 53 | Serialized contract | Derived command/diagnostic state is emitted through command and saturation-diagnostic canonical bytes. |
| `P6T06TargetKindV1` | enum, line 61 | Serialized contract | Written by `P6T06TargetKeyV1.ToTargetBytes()` and therefore participates in identity/digest derivation. |
| `P6T06OwnerKeyV1` | class, line 233 | Serialized contract | Has `ToCanonicalBytes()` and is embedded in queue, command, and transition bytes. |
| `P6T06TargetKeyV1` | class, line 329 | Serialized contract | Has target canonical bytes used by command IDs and command/queue records. |
| `P6T06CommandCandidateV1` | class, line 503 | Internal-only handoff | Transient caller candidate consumed by atomic enqueue; no direct `ToCanonicalBytes()` surface. |
| `P6T06AvailableCommandV1` | class, line 660 | Serialized contract | Has `ToCanonicalBytes()` and is part of queue state and transition evidence. |
| `P6T06QueueCommandV1` | class, line 753 | Serialized contract | Has `ToCanonicalBytes()` and owns deterministic command identity/digest fields. |
| `P6T06QueuePhaseTokenV1` | class, line 1241 | Internal-only handoff | Queue/digest/time binding token used to admit a phase; no direct canonical-byte representation. |
| `P6T06SourceBindingTokenV1` | class, line 1303 | Internal-only handoff | Caller-validated source-event/digest handoff; no direct canonical-byte representation. |
| `P6T06RrsControllerEventIdentityV1` | class, line 1341 | Internal-only handoff | Non-serialized integration handoff binding controller event and projection digests. |
| `P6T06QueueStateV1` | class, line 1401 | Serialized contract | Owns queue transitions and has `ToCanonicalBytes()`/`ComputeDigest()` compatibility obligations. |
| `P6T06ActuatorStateV1` | class, line 2461 | Internal-only handoff | Physical motion input used by queue transition evaluation; no direct canonical-byte representation. |
| `P6T06SaturationDiagnosticV1` | class, line 2508 | Serialized contract | Has `ToCanonicalBytes()` and records deterministic saturation evidence. |
| `P6T06MotionDiagnosticV1` | class, line 2579 | Serialized contract | Has `ToCanonicalBytes()` and records motion/consume evidence and state digest. |
| `P6T06QueueTransitionV1` | class, line 2702 | Serialized contract | Has `ToCanonicalBytes()` and binds before/after queue digests, ordering, and command evidence. |
| `P6T06EnqueueResultV1` | class, line 2883 | Internal-only handoff | Transient aggregate returned by enqueue; contains serialized child records but has no direct record serializer. |
| `P6T06MotionResultV1` | class, line 2913 | Internal-only handoff | Transient aggregate returned by motion/consume; contains serialized child records but has no direct record serializer. |

Current in-repository production consumer classification for every runtime row:
none found in CLI or Unity source. The Core test assemblies are the only
current typed consumers found. The public Core assembly is loaded by Unity,
so an unknown external consumer remains a compatibility risk rather than an
evidence-backed absence.

### Public task-ID-named test fixtures

Every name in this section is classified as `Test fixture`. These classes are
public for the test runner and are not runtime Core types. Their names can
still be consumed by test filters, reports, IDE discovery, or external test
automation, so a future migration must preserve or deliberately migrate those
references.

Core test fixtures:

- P3: `P3T01DomainContractsTests`, `P3T02LifecycleSnapshotEventTests`,
  `P3T03CommandClockTests`, `P3T04SerializationReplayTests`,
  `P3T05CommandApplicationTests`.
- P4: `P4T01SpatialStencilTests`, `P4T02SpatialOperatorTests`,
  `P4T03SpatialEigenIterationTests`, `P4T04SpatialConvergenceTests`,
  `P4T05SyntheticSpatialCaseTests`.
- P5: `P5T01RefuellingSchemeTests`, `P5T02RefuellingShiftTests`,
  `P5T03RefuellingEventTests`, `P5T04BurnupTransitionTests`,
  `P5T05BurnupCoefficientTests`, `P5T06SpatialRecomputeTests`,
  `P5T07Phase5InvariantTests`, `P5T08DeterministicHistoryTests`,
  `P5T09CompleteStateTests`, `P5T11CompleteBurnupTransitionTests`.
- P6: `P6T01LiquidZoneContractTests`, `P6T02LiquidZoneInfluenceMapTests`,
  `P6T03AdjusterContractsTests`, `P6T04BulkPoisonContractsTests`,
  `P6T05RrsControllerContractsTests`, `P6T06QueueTransitionContractsTests`,
  `P6T07ScenarioEvidenceTests`.

Golden test fixtures:

- P4: `P4T06G4BAdmittedCandidateConsumerTests`,
  `P4T06G4EIndependentReproductionConsumerTests`,
  `P4T06G4ICandidateConsumerTests`, `P4T06G4IApprovedConsumerTests`,
  `P4T06G4JRepresentativeConsumerTests`,
  `P4T06G4KIndependentConsumerTests`.
- P5: `P5T10ReducedModelSequenceComparisonTests`.

The fixture inventory is 34 classes total: 27 Core test fixtures and 7 Golden
test fixtures.

## Compatibility risks

| Surface | Evidence | Risk | Required treatment |
|---|---|---|---|
| Public Core API | 21 public P6-T06 types in the netstandard Core assembly; Unity loads the compiled DLL | Unknown downstream consumers may compile against task-ID names or inspect them by reflection | Preserve existing types through any migration; do not remove or rename in this discovery task. |
| Canonical bytes/digests | 13 runtime types/enum families are represented directly or transitively in canonical bytes; queue/command/transition digests are tested | A rename implemented as a new type could accidentally alter ordering, identity, digest, error path, or byte layout | Characterize exact bytes, digests, error codes, and replay evidence before and after any implementation task. |
| Persistence/replay | No direct P6T06 reference is present in `src/ReactorSim.Core/Serialization`; explicit canonical bytes remain the contract surface | A future adapter may change the type used to reconstruct a queue or transition even if JSON names are absent | Treat canonical bytes, queue digests, transition bytes, and replay fixtures as persistence compatibility. |
| Reflection/tooling | Unity imports the Core assembly; test runners discover public fixture names | Assembly/type lookup and test filters can break silently | Add reflection/API-surface characterization before any public migration; migrate test filters with fixture names. |
| In-repo production consumers | No CLI/Unity typed P6T06 references found | Absence of local use does not prove absence of external use | Treat public visibility as externally consumable until a separately authorized owner confirms otherwise. |
| Test fixtures | 34 public task-ID-named classes across two test assemblies | Fully qualified test filters, reports, and IDE discovery may depend on names | Keep fixture names stable until a dedicated test-rename step updates all consumers. |
| Historical evidence | Task reports, scope rows, and fixture paths use task IDs | Renaming historical records would damage auditability | Preserve historical report filenames and references; only add forward links if needed. |

## Minimal no-break migration order

This is a plan for a later implementation task, not authorization to execute it.

1. Freeze this inventory as the baseline. Before any rename, add a focused API
   and reflection characterization that enumerates the 21 public Core types,
   checks assembly/type names, and records the exact canonical bytes/digests,
   error codes, and replay fixtures for the serialized contract types.
2. Decide neutral domain names and compatibility shape in a separately
   authorized implementation task. Prefer additive aliases/facades or
   delegation that leaves every existing `P6T06...` type available and keeps
   one implementation owner. Do not use a source-only C# alias as a substitute
   for a public compatibility type.
3. Add characterization tests for both names and verify that the neutral
   surface delegates to the existing implementation without changing
   constructor/validation behavior, canonical bytes, digests, ordering,
   diagnostic codes, or replay records. The serialized types must retain their
   exact v1 byte layout.
4. Migrate only known in-repository consumers in a bounded follow-up: Core
   tests first, then any actual CLI or Unity typed consumers if later evidence
   identifies them. Keep the task-ID-named test fixtures and test filters stable
   until their own migration is complete.
5. Publish a deprecation/compatibility decision only after external-consumer
   confirmation, reflection checks, persistence/replay checks, and full
   regression evidence. Removing old names, changing public schemas, or
   changing serialized bytes requires a new risk-triggered task with T3 and
   independent code review (high); it is not part of `CORE-NAMING-01`.
6. Preserve all historical task reports, gate reports, scope evidence, and
   fixture manifests under their existing task IDs. New neutral names may be
   linked from future records, but historical authority is not rewritten.

## Files created or changed

- `docs/tasks/CORE-NAMING-01.md` - this compatibility inventory and decision
  record.
- `docs/PROJECT_SCOPE.md` - records completion and the next eligible task.

Pre-existing runtime, test, CLI, Unity, serialization, specification, gate,
and task-report files were inspected but not changed. No source or project
configuration file was modified.

## Validation commands and results

### T0 - public declaration and consumer inventory

```text
rg -n -P "^\s*public\s+(?:(?:sealed|abstract|static|readonly|partial|unsafe)\s+)*(?:class|enum|interface|struct|record|delegate)\s+\w*P\d+T\d+\w*" src/ReactorSim.Core src/ReactorSim.Cli unity tests
rg -n "P6T06" src/ReactorSim.Cli unity --glob '!**/Library/**' --glob '!**/Temp/**' --glob '!**/obj/**' --glob '!**/bin/**'
```

Result: the declaration search found 55 public task-ID-named declarations:
21 runtime Core types and 34 test fixtures. No P6-T06 source consumer was
found in CLI or authored Unity code. The only Unity evidence is the assembly
reference to the compiled Core DLL.

### T0 - focused Core build

```text
$sdkPath = 'C:\Users\infin\AppData\Local\Temp\candu-sdk-10.0.302'
$env:DOTNET_ROOT = $sdkPath
$env:Path = "$sdkPath;$env:Path"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_HOME = 'C:\Users\infin\AppData\Local\Temp\candu-cli-home-core-naming'
& (Join-Path $sdkPath 'dotnet.exe') build 'src/ReactorSim.Core/ReactorSim.Core.csproj' --no-restore --verbosity:minimal
```

Result: exit code 0; `ReactorSim.Core` build succeeded with 0 warnings and 0
errors.

The first attempted build added the test-only `--logger` switch and returned
`MSB1007` before compilation; the corrected command above succeeded. This
was a recoverable command invocation error, not a source or validation
failure.

## Token and cost accounting

| Field | Value | Source/notes |
|---|---:|---|
| Input tokens | `Unavailable` | request-level telemetry unavailable |
| Cached input tokens | `Unavailable` | request-level telemetry unavailable |
| Cache-write input tokens | `Unavailable` | request-level telemetry unavailable |
| Output tokens | `Unavailable` | request-level telemetry unavailable |
| Reasoning output tokens | `Unavailable` | request-level telemetry unavailable |
| Total tokens | `Unavailable` | request-level telemetry unavailable |
| Estimated cost | `Unavailable` | no allocable request receipt or price basis |
| Goal-service total | `Unavailable` | goal aggregate telemetry unavailable; not added to request totals |

Cost formula/basis: unavailable; no request-level billing receipt or
price/date basis was exposed, so no per-task allocation is invented.

## Numerical differences

Not applicable; no numerical, serialization, or runtime behavior changed.

## Deferred validation

- Public-contract migration, alias/facade behavior, reflection compatibility,
  and byte-for-byte before/after characterization - deferred to a separately
  authorized implementation task because `CORE-NAMING-01` is discovery-only.
- T3 full regression and independent code review - deferred until a future
  task actually changes the public contract, type identity, persistence, or
  serialized behavior; the current task changed no source.
- Unity route/device checks - not applicable to this documentation-only task.

## Blockers, risks, and follow-up

- Blockers: none within the discovery boundary.
- Risk triggers: none fired by this task; no runtime, public contract,
  persistence, serialization, or test code changed. A future type migration
  is explicitly a risk-triggered task.
- Risks accepted or deferred: external consumers cannot be ruled out from
  repository evidence; the plan therefore preserves all old names until an
  owner-authorized compatibility implementation and external-consumer check.
- Follow-up work: `STATUS-VISIBILITY-01` is the next independent documentation
  surface task. Any implementation of the migration plan is a separate task
  and must not be folded into status or naming work.

## Next eligible task

`STATUS-VISIBILITY-01` - align derived guide/site and LLM handoff text with
authoritative scope and evidence without creating a new status authority.

Source: [`AGENTS.md`](../../AGENTS.md),
[`Implementation_plan.md`](../Implementation_plan.md),
[`PROJECT_SCOPE.md`](../PROJECT_SCOPE.md), and
[`Refactoring_implementation_plan_2026-08-21.md`](../Refactoring_implementation_plan_2026-08-21.md).
