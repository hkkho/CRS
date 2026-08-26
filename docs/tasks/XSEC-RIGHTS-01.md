# XSEC-RIGHTS-01 - JEFF Internal-Use Permission Attestation

## Outcome

Status: COMPLETE

Effectiveness: SUCCESS

This task recorded the bounded owner-rights authority required to re-route the
blocked XSEC data workstream. It authorizes only offline internal use of JEF/
JEFF data through DRAGON5/DONJON5 for derived case candidates; raw source data
and converted library binaries stay external, non-runtime, and prohibited from
game distribution. It selects no physics or runtime data.

Applicable P1-T08 digest rows: NotApplicable. This is a rights/authority
record only; it does not select, validate, or alter physics evidence.

## Execution and model evidence

- Role: root implementer.
- Requested model / reasoning: GPT-5.6 Luna / high.
- Actual model / reasoning: UNVERIFIED; execution telemetry is unavailable.
- Execution receipt or telemetry source: current Codex task; no provider
  request-level receipt was exposed.
- Attempts: one bounded owner-attestation task; elapsed time: unavailable as
  an allocable task total.
- Artifact/checkpoint status: produced; checkpoint follows this completed
  report and scope reconciliation.
- Review disposition: PASS. The initial same-context review was conditional on
  three wording/routing corrections; all corrections were made and the final
  disposition found no remaining findings.
- Review evidence verification: UNVERIFIED; no execution receipt exposed the
  actual reviewer model or reasoning effort.
- Reviewer reuse/fresh-review rationale: one bounded reviewer,
  01a03b98-9b86-7f22-9068-76bb1ce6b01e, requested as code review (high).
  The same reviewer/context issued the final PASS after the three documented
  corrections. Actual model/reasoning telemetry remains UNVERIFIED.

## Files created or changed

- docs/tasks/XSEC-RIGHTS-01-OWNER-APPROVAL.md - bounded owner attestation.
- reference/manifests/xsec-rights-attestation-v1.json - path-free machine
  record of the permitted scope and constraints.
- docs/tasks/ROUND-2026-08-25-XSEC-DATA-CHAIN.md - inserts this separate
  rights task before XSEC-02.
- docs/PROJECT_SCOPE.md - records current routing state after the authority
  record is created.
- docs/tasks/XSEC-RIGHTS-01.md - this report.

Historical XSEC-01 evidence was inspected but not changed.

## Assumptions and design choices

- The owner’s explicit statement is recorded as an internal project authority,
  not as a public legal opinion or third-party license text.
- Raw nuclear data remains external and nonredistributed. Any later derived
  pack must pass the separately specified technical and gate admission path.

## Validation commands and results

### T0/T1 - documentation, manifest, and routing validation

~~~text
PowerShell ConvertFrom-Json required-field checks for
reference/manifests/xsec-rights-attestation-v1.json; exact restrictive-control,
non-public-derived-pack, non-legal-determination, path-free, historical-record,
and deferred-scope assertions

git diff --check
~~~

Result: PASS. The manifest parsed and supplied every required field; raw-data
retention, game-redistribution, and runtime-use controls were all restrictive;
derived-pack release wording and the non-legal limitation were present; no
host-local paths appeared; the historical XSEC-01 statement remained present;
and PROJECT_SCOPE was not advanced before final completion.

### Independent code review (high)

~~~text
Independent reviewer 01a03b98-9b86-7f22-9068-76bb1ce6b01e,
requested code review (high); same context reused for final disposition.
~~~

Result: PASS. The initial CONDITIONAL PASS required explicit prohibition of
derived-pack redistribution/publication/release/store delivery, a clear
owner-scoped/non-external-legal-determination qualifier, and deferred scope
reconciliation. All three corrections were verified in the final same-context
review. Actual model/reasoning telemetry: UNVERIFIED.

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

Not applicable; no numerical behavior changed.

## Deferred validation

- T2 through T6 - not applicable to this authority-only task. The relevant
  validation is owned by XSEC-02 through G4-R7.

## Blockers, risks, and follow-up

- Blockers: none for this authority-record task.
- Risk triggers: licensing/redistribution controls remain active. The owner
  attestation is deliberately limited to internal use and does not permit raw
  data redistribution.
- Risks accepted or deferred: exact physics mapping, source-case reproduction,
  and any derived-pack distribution decision remain deferred.
- Follow-up work: XSEC-02 remains responsible for the still-unapproved
  source-to-runtime mapping specification. It must not infer any physics from
  this rights record.

## Next eligible task

XSEC-02 - bounded DRAGON/DONJON source-to-runtime mapping specification/ADR.
It may start after this checkpoint. It must read its named P1-T08 digest rows,
select no unsupported physics, and stop on any mapping/source/reproduction
conflict.

Source: AGENTS.md and CODEX_TASK_TEMPLATE.md.
