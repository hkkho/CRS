# XSEC-01 - Legal and Technical Source Inventory

## Outcome

Status: BLOCKED

Effectiveness: BLOCKED

Official-source research and offline technical evaluation established a
hash-bound DRAGON5/WLUP conversion and reader route, but did not establish the
artifact-specific permission required to derive or redistribute a Unity runtime
cross-section pack from JEFF-3.1/WLUP data. The task therefore produces a
technical-evaluation candidate only. It does not admit data, a derivative pack,
production use, Unity use, runtime use, authoritative evidence, or golden
evidence.

Applicable P1-T08 digest rows: S1-R02, S1-R03, S1-R04, S1-R05, S1-R06,
S1-R07, S1-R11, S5-R02, S5-R03, S5-R04, S5-R05, S5-R09, S6-R03, and
S6-R08.

## Execution and model evidence

- Role: root implementer.
- Requested model / reasoning: GPT-5.6 Luna / high.
- Actual model / reasoning: UNVERIFIED; execution telemetry is unavailable.
- Execution receipt or telemetry source: current Codex task; no provider
  request-level receipt was exposed.
- Attempts: one bounded XSEC-01 candidate; elapsed time: unavailable as an
  allocable task total. Hash-bound external run timestamps are recorded in
  xsec-01-execution-provenance-v1.json.
- Artifact/checkpoint status: produced; checkpoint commit follows this report
  and scope reconciliation.
- Review disposition: PASS - scope-limited. It approves the correctness of the
  blocked technical-evaluation record only; it does not clear rights or approve
  a runtime pack.
- Review evidence verification: UNVERIFIED.
- Reviewer reuse/fresh-review rationale: one bounded reviewer,
  01a03b98-9b86-7f22-9068-76bb1ce6b01e, requested as GPT-5.6 Sol / high.
  The same reviewer/context was reused after the documented corrections. Actual
  model/reasoning telemetry remained UNVERIFIED.

## Files created or changed

- docs/research/XSEC-01-license-technical-admission.md - official-source,
  licensing-boundary, toolchain, technical-evaluation, and candidate routing
  record.
- reference/manifests/xsec-01-source-admission-v1.json - path-free candidate,
  rights-boundary, toolchain, raw-input, and conversion identity record.
- reference/manifests/xsec-01-execution-provenance-v1.json - path-free
  permission-snapshot, fixture, command, timestamp, exit-code, lexeme, and
  external-artifact identity record.
- docs/tasks/XSEC-01.md - this task report.
- docs/PROJECT_SCOPE.md - updated only after this report is complete.

Pre-existing AGENTS.md, implementation/scope authorities, P1-T02 through
P1-T08 reports, G1, related specifications/ADRs, and existing provenance
manifests were inspected and not changed.

## Assumptions and design choices

- The Version5 image and tools are external/offline reference tooling only;
  neither executables nor source data are vendored.
- Direct IAEA jeff31gx.lib is formatted WIMSD input. The pinned DRAGON5 reader
  requires an unformatted WIMSD4 binary, so the official IAEA WILLIE FOBI route
  was used rather than inventing a converter.
- IAEA web terms authorize broad reuse of IAEA content but explicitly preserve
  third-party rights. Because the exact WLUP JEFF-3.1 catalogue does not grant
  artifact-specific derivative/redistribution rights, the candidate is blocked
  from all runtime-pack derivation or redistribution.
- P1-T02/P1-T03 remain external-private provenance smoke cases only. The NEA
  dragon/libraries assets remain unadmitted because the checked pinned source
  lacks asset-specific redistribution terms.
- The upstream tjeff31gx fixture is recorded as partial with an unresolved
  execution failure. The record does not attribute its cause, call it a full
  regression pass, or make its two completed assertions a baseline.

## Validation commands and results

### T0 - machine-record schema, path, and diff checks

~~~text
PowerShell ConvertFrom-Json checks for both XSEC manifests; required digest
rows, status, fixture count, run count, required boundary wording, and
host-path-leak assertions

git diff --check
~~~

Result: PASS. Both JSON documents parsed; required rows/statuses and all
17 staged fixture identities/three run records were present; no host-local path
was present; documentation records the rights block; no whitespace error.

### T1 - exact external identity and converter checks

~~~text
Get-FileHash -Algorithm SHA256 <external IAEA archive, formatted library,
for.src, extracted dckspl, dckspl binary, willie.for, willie binary,
generated binary>

docker run --rm --platform linux/amd64 --network none --pull never --read-only
... gfortran ... dckspl ... willie ... ./willie < willie_fobi.inp
~~~

Result: PASS. The exact formatted IAEA JEFF-3.1/XMAS-172 source,
official converter source, generated binary, tools, and listings matched every
recorded SHA-256. The official WILLIE FOBI conversion completed with exit code
0. Exact source lexemes, commands, timestamps, and output identities are in
the execution-provenance manifest.

### T1 - offline DRAGON reader check

~~~text
docker run --rm --platform linux/amd64 --network none --pull never --read-only
... /dragon/5.1/Dragon/bin/Linux_x86_64/Dragon < tjeff31gx.x2m
~~~

Result: PARTIAL. Direct formatted input exited 2 at the expected format
mismatch. The converted binary loaded and completed two source assertions:

| Observable | Expected lexeme | Observed lexeme | Absolute difference |
|---|---:|---:|---:|
| K-EFFECTIVE | 0.8227208 | 8.227200E-01 | 8.0e-7 |
| K-EFFECTIVE | 0.8228317 | 8.228318E-01 | 1.0e-7 |

The unmodified fixture then exited 1 at its EXCELL transition. Cause:
unresolved. This is not T6 reference reproduction evidence.

### Independent code review (high)

~~~text
Independent reviewer 01a03b98-9b86-7f22-9068-76bb1ce6b01e,
requested GPT-5.6 Sol / high; same reviewer reused for final disposition.
~~~

Result: PASS - scope-limited after corrections. The reviewer verified the
rights boundary, exact catalogue/terms evidence locator, path-free provenance,
fixtures, command/run/log identities, source lexemes, and partial-fixture
framing. Actual telemetry: UNVERIFIED.

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

Cost formula/basis: unavailable. No per-worker or review cost is invented from
aggregate service usage.

## Numerical differences

No runtime numerical behavior changed. The external technical-evaluation
observations are recorded above and in the execution manifest. They are not
approved tolerances, a reference baseline, or golden data.

## Deferred validation

- T2 parser/exporter/pack validation - blocked; no pack/schema is authorized.
- T3 Core/CLI/Golden regression - blocked; no runtime numerical input changed.
- T4 Unity import/EditMode/PlayMode/player smoke - blocked; no Unity data
  asset or adapter is authorized.
- T6 DRAGON/DONJON reference reproduction - deferred; the fixture did not
  complete and no exact legally admitted case/mapping exists.
- G4-R7 - deferred; no candidate runtime/reference pack exists.

## Blockers, risks, and follow-up

- Blocker: explicit artifact-specific rights for use, derivation, retention,
  and redistribution of the exact JEFF-3.1/WLUP and WILLIE chain are absent.
  The IAEA terms expressly preserve third-party rights, while the IAEA and NEA
  source pages establish provenance/availability but not a derivative-pack
  permission.
- Risk triggers: licensing/redistribution critical blocker fired. Under
  AGENTS.md, dependent physics, implementation, pack, Core/CLI, Unity, PDF,
  and gate work must not proceed.
- Risks accepted or deferred: the external technical conversion remains useful
  only for future rights-cleared evaluation. The incomplete fixture cause is
  deferred and must not be treated as an upstream defect.
- Follow-up work: obtain explicit written rights evidence from the relevant
  rights holder(s), then create a separate owner-approval/task-definition
  record for its exact scope. Do not reopen or alter this historical task
  record.

## Next eligible task

None. A new bounded rights/owner-approval task-definition record is required
after explicit artifact-specific permission is available. XSEC-02 is blocked.

Source: AGENTS.md and CODEX_TASK_TEMPLATE.md.
