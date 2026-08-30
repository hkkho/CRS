# XSEC-ENERGY-01 - SAPHYB fission-energy companion route

## Outcome

Status: `COMPLETE`

The task proved a separate, source-preserving DRAGON5 SAP/SAPHYB route for the
mapping-required fission-only energy-production field. It established the
exact `MEVF * NFTOT` construction, fission-only inclusion rule, `MeV cm^-1` to
`eV cm^-1` conversion, two-state/two-group/one-mixture artifact structure, and
deterministic companion capture. It does not admit a numerical candidate,
runtime pack, Core/CLI/Unity behavior, DONJON case, golden/reference result,
or release.

Effectiveness: `SUCCESS`

## Execution and model evidence

- Role: root implementer.
- Requested model / reasoning: GPT-5.6 Luna / high by repository routing.
- Actual model / reasoning: `UNVERIFIED`.
- Execution receipt or telemetry source: local task thread; model/effort receipt unavailable to the repository.
- Attempts: 5 source-execution attempts (1 bounded pre-execution parser failure with no source calculation/capture; 4 completed isolated runs); elapsed time: unavailable.
- Artifact/checkpoint status: produced.
- Review disposition: deferred to XSEC phase closeout by the owner sequencing decision recorded in `XSEC-03-R4-OWNER-APPROVAL.md`.
- Review evidence verification: `UNVERIFIED`.
- Reviewer reuse/fresh-review rationale: not applicable; no review was requested at this task boundary.

## Files created or changed

- `docs/tasks/XSEC-ENERGY-01-OWNER-APPROVAL.md` - bounded owner authority.
- `docs/tasks/XSEC-ENERGY-01-REQUEST.md` - immutable task request.
- `docs/spec/xsec-saphyb-efis-route-v1.md` - primary-source route decision.
- `docs/spec/xsec-jeff-lattice-deck-v6.md` - companion-artifact deck delta.
- `docs/spec/xsec-source-runtime-mapping-v5.md` - cross-artifact EFIS mapping.
- `reference/manifests/xsec-energy-01-saphyb-route-v1.json` - path-free non-numerical evidence manifest.
- `docs/tasks/XSEC-ENERGY-01.md` - this report.
- `docs/PROJECT_SCOPE.md` - delivery-state handoff after this report.

Inspected but not changed: all source inputs/captures/listings/libraries,
existing deck/mapping versions, Core, CLI, Unity, runtime schemas/packs,
DONJON/full-core artifacts, and the unrelated untracked `docs/tasks/P10-T05-R1.md`.

## Assumptions and design choices

- Pinned IGE-335/IGE-351 documentation and matching Version5 source were treated as primary technical evidence. P1-T08 rows `S1-R04`, `S1-R05`, `S5-R02`, `S5-R03`, `S5-R04`, `S5-R09`, and `S6-R03` were methodology and applicability context only.
- The route uses SAP's documented `EFIS` selector, whose exporter branch uses `MEVF * NFTOT`; it does not use `H-FACTOR`, `PRODUCTION`, capture/gamma energy, a reconstructed field, or a direct-correction field.
- `MEVF` is primary-documented in MeV while the source macro fission cross section is in inverse centimetres. The exact conversion to the inherited mapping unit is multiplication by one million electron-volts per mega-electron-volt. No numerical value was read to establish that result.
- The companion artifact is explicitly separate from the rejected EDI WCFIELD MACROLIB EFIS route. A later task must prove WCFIELD/SAPHYB flux correspondence before using a cross-artifact energy ratio.
- The initial disposable probe had a local linked-list syntax error and failed before source execution. The corrected candidate was not silently accepted: the no-artifact attempt and its concrete correction are preserved here, then the accepted route was exercised with an exact duplicate and untouched control.
- The host emitted an underflow/denormal notice on both the changed probe and untouched control. Both reached normal end with all retained assertions, and the control exactly reproduced its prior capture. It is recorded as a baseline environment diagnostic, not a numerical acceptance result.

## Validation commands and results

### T0 - authorities, primary source, and document checks

```text
git status --short
rg -n -C 4 'MEVF|EFIS|H-FACTOR|RDATAX|FLUXS' <pinned IGE-335/IGE-351 and DRAGON5 SAP sources>
Get-Content -LiteralPath <XSEC predecessor reports/specifications>
```

Result: pass. The exact SAP `EFIS` meaning, `MEVF` MeV unit, `MEVF * NFTOT` exporter branch, separate gamma branch, SAPHYB address model, and integrated `FLUXS` meaning were bound to canonical-LF source identities. The unrelated P10 draft remained unmodified.

### T1/T2 - structural artifact and manifest checks

```text
docker run --rm --network none --read-only --tmpfs /tmp:exec <pinned DRAGON5 image> <read-only non-numerical SAPHYB observer>
Get-Content -LiteralPath reference\manifests\xsec-energy-01-saphyb-route-v1.json -Raw | ConvertFrom-Json
```

Result: pass. The observer read character metadata, directory names, and array shapes only: two elementary calculations, one mixture each, two group-shaped flux entries, state key `XSTA`, and reaction label `ENERGIE F.`. It did not read a numerical array or value. The manifest parses and declares no numerical or runtime/golden admission.

### T6 - fresh offline source reproduction and determinism

```text
docker run --rm --entrypoint /bin/sh --network none --read-only --tmpfs /tmp:exec --tmpfs /run -e OMP_NUM_THREADS=1 -v <disposable-root>:/dragon/5.1/Dragon <pinned DRAGON5 image> -lc './rdragon -q tcwux11sphcap.x2m'
```

Result: pass. The corrected SAPHYB probe, an exact duplicate input, and an untouched control each reached normal end with all four original assertions. The duplicate companion capture was byte-identical. The untouched control capture exactly matched its prior baseline. No raw procedure, listing, capture, array, value, or derived value was retained in the repository.

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

Not applicable; no numerical source or derived value was read or retained. The only comparison was byte identity of whole external captures and a boolean unchanged-control result.

## Deferred validation

- T3 full Core/CLI/Golden regression and code review (high) - deferred together to XSEC phase closeout by the owner's recorded sequencing decision.
- T4 Unity validation - not applicable; no Unity contract, asset, or code changed.
- Value-level cross-artifact field decode/invariants - deferred to separately authorized `XSEC-ENERGY-02`; this task intentionally stopped before numeric data access.

## Blockers, risks, and follow-up

- Blockers: none within the bounded route task.
- Risk triggers: source-field identity, artifact, unit, normalization, and preservation risks fired and were resolved only to the route level. Candidate numerical admission remains deliberately unperformed.
- Risks accepted or deferred: future cross-artifact state/group/mixture and flux correspondence must be proven value-by-value; the SAPHYB route is not evidence of runtime, golden, or full-core suitability.
- Follow-up work: `XSEC-ENERGY-02` must be separately defined and owner approved for a fresh deck-v6 cross-artifact semantic decode/admission task.

## Next eligible task

None until separately owner-approved `XSEC-ENERGY-02` is defined.

Source: [`AGENTS.md`](../../AGENTS.md).
