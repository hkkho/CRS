# XSEC-SPH-03 - official persistent pre-SPH capture interface proof

## Outcome

Status: COMPLETE

Effectiveness: SUCCESS

The official Version5 persistent-file interface is now proven for both required
TCWUX11 post-EDI/pre-SPH states. The caller follows the official `twlup`
ordering—procedure declaration, `SEQ_ASCII ... :: FILE`, then parameterized
procedure call. Each external source variant copies its selected `EDITION` to
`PRE0` or `PRE172` immediately after the relevant `EDI`, retains both original
`EDITION := SPH: EDITION VOLMATF INTLINF ;` transitions verbatim, and serializes
only the copied object after the fourth unchanged source assertion. The pinned
launcher save hook retained the FILE-backed capture after normal completion.

Two clean roots for each state produced byte-identical captures. Every run
matched the four XSEC-RUNNER-01 assertion deltas and completed normally. This
proves the capture interface only; no object was decoded, mapped, admitted as
runtime data, labelled golden, or wired into Core, CLI, or Unity.

Applicable P1-T08 digest rows: `S1-R04`, `S1-R05`, `S5-R02`, `S5-R03`,
`S5-R04`, `S5-R09`, and `S6-R03`. They remain methodology/applicability
context only and authorize no numerical value, mapping, tolerance, or golden
claim.

## Execution and model evidence

- Role: root implementer.
- Requested model / reasoning: GPT-5.6 Luna / high; independent code review (high).
- Actual model / reasoning: UNVERIFIED; no task-level execution receipt was exposed.
- Execution receipt or telemetry source: local Codex task; reviewer `01a03b98-9b86-7f22-9068-76bb1ce6b01e`.
- Attempts: four clean source runs plus bounded compilation diagnostics inherited from XSEC-SPH-02; elapsed time: Unavailable.
- Artifact/checkpoint status: produced path-free manifest and this report; all raw source, data, modified procedures, captures, and listings remain external-only.
- Review disposition: PASS. The final reviewer confirmed all five required evidence checks: documented FILE/equality support, official caller ordering, post-EDI/pre-SPH timing with verbatim SPH calls, four assertion deltas plus normal completion and exported-object markers, and byte-identical external-only captures/routing.
- Review evidence verification: UNVERIFIED; no service receipt exposed actual model/reasoning telemetry.
- Reviewer reuse/fresh-review rationale: the same reviewer first returned `BLOCKED` solely because this report/manifest did not yet exist. The same reviewer then issued the required post-correction final PASS; two bounded reviewer dispositions, no fresh-review swarm.

## Files created or changed

- `reference/manifests/xsec-sph-03-capture-interface-v1.json` - path-free source-interface, run, result, and retained-capture identities.
- `docs/tasks/XSEC-SPH-03.md` - this task evidence.
- `docs/PROJECT_SCOPE.md` - task routing after final review.

No raw DRAGON source, WLUP data, modified deck, LCM serialization, complete
listing, executable, or generated cross-section value is retained in the
repository. Existing approved mapping/deck documents were inspected but not
changed.

## Assumptions and design choices

- The authoritative runner remains the completed XSEC-RUNNER-01 pinned Docker
  contract: network isolated, read-only root, executable `/tmp`, and one thread.
- The official Version5 FAQ and `twlup.x2m` example establish the FILE-backed
  `SEQ_ASCII` plus `PARAMETER` procedure-output pattern. IGE-335 section 3.11
  is used narrowly: it confirms SPH creates a new corrected edition from the
  original RHS; it does not select a mapping or field.
- `PRE0` is copied directly after the first `EDI`; `PRE172` directly after the
  second `EDI`. The next source statement in each case is the unchanged SPH
  call on `EDITION`; the copied object is never an SPH operand.
- `K-INFINITY` found in serialized output is an LCM record label with finite
  values, not a NaN or infinity numeric value. No NaN or non-finite numeric
  token was observed.

## Validation commands and results

### T0 - interface, fixture, and source-stage validation

```text
docker run --rm --network none --read-only --tmpfs /tmp:exec --tmpfs /run \
  --entrypoint sh -v <external-data>:/dragon/5.1/Dragon/data:ro \
  -v <external-results>:/dragon/5.1/Dragon/Linux_x86_64 \
  -w /dragon/5.1/Dragon <pinned-image> -c './rdragon -q tcwux11sphcap.x2m'
```

Result: PASS for four fresh external roots. The source/driver/save-hook/capture
SHA-256 identities, official evidence, source order, and runner identity are
in `xsec-sph-03-capture-interface-v1.json`.

### T1 - source regression, normal completion, and retained-capture repeat

| State | Pair capture SHA-256 | Bytes | Pair normalized listing SHA-256 |
|---|---|---:|---|
| `PRE0` | `d40a67dba6a91e33179a2ecef8386723a78530669563efa750f90eb25344a572` | 1,746,213 | `9c704290fac4efc64dc8f1ba28c27d4987d199294673548f97e250f1f333e6d9` |
| `PRE172` | `71dee765bf872a37865844a8fa006f62ce86bfa0a750f25ca6bbe822f35328f3` | 3,857,772 | `c6a10be098cc1200b73aac89d576d3d80a8cf94dbee5229fe4450bd4c797b044` |

Result: PASS. Each pair is byte-identical; the two state captures are distinct.
Each listing records `DRVEQU: A LCM OBJECT NAMED 'PRE0'` or `PRE172` exported
to the FILE-backed `res` object, all four `TEST SUCCESSFUL` deltas exactly
match `2.131634e-06`, `9.490768e-07`, `9.059420e-06`, and `7.328338e-06`, and
each run records `test TCWU11 completed` and normal end. Raw listing hashes
vary only in known volatile timing/memory diagnostics; the existing
XSEC-RUNNER-01 normalization rule makes every within-state pair identical.

### T3/T6

Not run. This task changes no approved deck/mapping authority, runtime
interface, numerical input, or reference baseline. `XSEC-03-R2` owns its fresh
T6 execution, semantic validation, and any later data admission.

## Numerical differences

No runtime numerical behavior or source-data mapping changed. The four source
assertion deltas above exactly reproduce the XSEC-RUNNER-01 baseline and are
source regression evidence only, not project tolerances or golden values.

## Token and cost accounting

| Field | Value | Source/notes |
|---|---:|---|
| Input tokens | Unavailable | No task-level telemetry exposed |
| Cached input tokens | Unavailable | No task-level telemetry exposed |
| Cache-write input tokens | Unavailable | No task-level telemetry exposed |
| Output tokens | Unavailable | No task-level telemetry exposed |
| Reasoning output tokens | Unavailable | No task-level telemetry exposed |
| Total tokens | Unavailable | No task-level telemetry exposed |
| Estimated cost | Unavailable | No verified task-level price allocation |
| Goal-service total | Unavailable | Not allocable and not added to request totals |

Cost formula/basis: unavailable; no per-worker or review cost is invented.

## Deferred validation

- T3 Core/Golden/CLI regression - not triggered: this task does not alter a
  project contract, runtime code, deck/mapping authority, or numerical data.
- T6 semantic export, decode, mapping validation, and independent fresh rerun -
  deferred to `XSEC-03-R2` after this final review and scope reconciliation.
- Full-core, converter, Core/CLI, Unity, and golden/reference admission -
  remain owned by later XSEC tasks and the applicable gate.

## Blockers, risks, and follow-up

- Blockers: none for the capture-interface proof after final review.
- Risk triggers: source-object/serialization ambiguity triggered the task; the
  documented interface, two clean pairs, unchanged source regression evidence,
  and independent review are the required escalation response.
- Risks accepted or deferred: raw source data and captures remain external;
  source output has not been decoded or shown to satisfy any mapping contract.
- Follow-up work: `XSEC-03-R2` is eligible only after this report receives a
  final independent high-review PASS and `PROJECT_SCOPE.md` is reconciled. It
  must independently execute T6 and must not inherit a data/golden PASS.

## Next eligible task

`XSEC-03-R2` - independently execute the source semantic-export admission
task using the proved capture interface; it still owns source reruns, semantic
field validation, mapping checks, and any data-admission decision.

Source: [`AGENTS.md`](../../AGENTS.md),
[`XSEC-SPH-03-REQUEST.md`](XSEC-SPH-03-REQUEST.md),
[`XSEC-RUNNER-01.md`](XSEC-RUNNER-01.md), and
[`xsec-sph-03-capture-interface-v1.json`](../../reference/manifests/xsec-sph-03-capture-interface-v1.json).
