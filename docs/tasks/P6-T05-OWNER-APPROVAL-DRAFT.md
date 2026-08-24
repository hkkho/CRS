# P6-T05 owner-approval packet - draft for user decision

Status: `DRAFT / PENDING USER APPROVAL`

This packet proposes one synthetic, test-only controller-gain/setpoint and RRS
influence-map fixture. It is not a runtime data pack, a production CANDU
controller, an external-reference result, or a golden baseline. No P6-T05
source or data implementation may consume these values until the owner accepts
the exact approval block.

## Recommended decision

Approve **Option A: a project-authored synthetic, engine-neutral limited RRS
controller fixture** for bounded P6-T05 work.

This authorizes validation of the frozen P2-T04 total-power and regional-tilt
controller equations, explicit setpoint/error/integral units, one automatic
cadence, one manual-command projection, negative-feedback sign ownership,
state-dependent local absorption overlay, deterministic command projection, and
fail-closed invalid-state checks. It does not authorize shared queue allocation
or transition, actuator motion, scenario packages, direct-reactivity duplicate
paths, production CANDU controller defaults, external-reference or golden
values, shutdown/scram/trip/safety behavior, or hidden physics.

## Frozen requirements this decision does not change

- The controller uses the frozen P2-T04 equations:
  `PowerError = PowerSetpoint - MeasuredPower`,
  `IntegralError_next = IntegralError + PowerError * Delta_t_RRS`,
  `TiltError_r = TargetFraction_r - MeasuredRegionFraction_r`,
  `TiltIntegral_next,r = TiltIntegral_r + TiltError_r * Delta_t_RRS`, and
  `ActuatorCommand_q = Bias_q + K_P,q * PowerError + K_I,q *
  IntegralError_next + sum_r(K_T,q,r * TiltError_r + K_TI,q,r *
  TiltIntegral_next,r)`.
- Power and power error are `W`; total-power integral is `W s`; tilt error is
  dimensionless; tilt integral is `s`; controller cadence, delay, and time are
  `s`; actuator state, command, bounds, and bias are dimensionless; actuator
  rate is `s^-1`; `K_P` is `W^-1`; `K_I` is `(W s)^-1`; `K_T` is `1`; and
  `K_TI` is `s^-1`.
- The map is the frozen local absorption overlay:
  `DeltaSigma_a,g,i = sum_q Weight_(q,i,g) *
  (ActuatorState_q - ReferenceActuatorState_q)`.
  It is not a direct reactivity term and is not also encoded in a base
  coefficient table.
- Automatic mode requires a frozen measurement snapshot, positive finite
  cadence interval, complete non-overlapping target regions whose fractions sum
  to one, finite gains and states, explicit negative-feedback sign certificate,
  and fail-closed validation. Manual mode uses an explicit command and does not
  advance integrals or `LastUpdateTime`. Held mode keeps actuator states and
  integrals unchanged.
- P6-T05 does not choose a numerical stability tolerance, production gain, or
  external comparison threshold. Any later approved runtime queue or
  actuator-motion behavior belongs to P6-T06.

## Proposed synthetic fixture

### Identity and scope

| Field | Proposed value |
|---|---|
| Fixture ID | `P6-T05-SYNTHETIC-RRS-CONTROLLER-MAP-V1` |
| Data version | `p6-t05-synthetic-rrs-controller-map-v1` |
| Map version | `p6-t05-synthetic-rrs-controller-map-v1` |
| Controller schema | `CANDU-RRS-CONTROLLER-STATE-V1` |
| Influence-map schema | `CANDU-RRS-INFLUENCE-MAP-V1` |
| Controller ID | `00000000-0000-0000-0000-00000000c501` |
| Influence-map ID | `00000000-0000-0000-0000-00000000c502` |
| Supplied queue ID | `00000000-0000-0000-0000-00000000c503` |
| RRS topology ID | `00000000-0000-0000-0000-00000000c504` |
| Region-set ID | `00000000-0000-0000-0000-00000000c505` |
| Total-power actuator ID | `00000000-0000-0000-0000-00000000c521` |
| Tilt actuator ID | `00000000-0000-0000-0000-00000000c522` |
| Left region ID | `00000000-0000-0000-0000-00000000c511` |
| Right region ID | `00000000-0000-0000-0000-00000000c512` |
| Owner | `Kevin Ho` |
| Evidence domain | `Synthetic / engine-neutral Core / ReducedModel` |
| Control polarity | `NegativeFeedback` |
| Sign certificate | `P6-T05-SYNTHETIC-NEGATIVE-FEEDBACK-V1` |
| Normalization | `None` |
| Map source unit | `1` (dimensionless actuator state) |
| Map target unit | `m^-1` |
| Reference-state digest | generated deterministically after approval |
| Target nodes | six explicit `(ChannelId=0..5, BundlePosition=0)` keys |
| Region partition | left `(0,0),(1,0),(2,0)` and right `(3,0),(4,0),(5,0)` |
| Map entry count | `24` (`2` actuators x `6` targets x `2` groups) |

The prior G4-R6/G4J RRS artifacts were inspected as historical candidate or
ReducedModel evidence only. This proposal creates a separately identified
P6-T05 authority and does not promote, mutate, or relabel those artifacts.

### Controller state, cadence, and reference values

| Field | Proposed value |
|---|---:|
| Initial mode | `Automatic` |
| Power setpoint | `1000 W` |
| Initial measured power | `1000 W` |
| Initial power error | `0 W` |
| Initial integral error | `0 W s` |
| Left target fraction | `0.5` |
| Right target fraction | `0.5` |
| Initial left/right measured fractions | `0.5 / 0.5` |
| Initial tilt errors | `0.0 / 0.0` |
| Initial tilt integrals | `0.0 s / 0.0 s` |
| Automatic generation cadence | `1.0 s` |
| Initial `LastUpdateTime` | `0.0 s` |
| Actuator reference/state/command/available values | `0.5 / 0.5 / 0.5 / 0.5` |
| Actuator bounds | `[0.0, 1.0]` |
| Actuator rate limits | `0.1 s^-1` each |
| Command delays | `0.5 s` each |
| Supplied queue generation cadence | `1.0 s` |

All reference/state values are synthetic fixture values. The queue ID identifies
the complete supplied queue projection used for read-only binding tests; P6-T05
does not allocate, enqueue, consume, transition, or roll back that queue.
The common actuator reference state is `0.5 / 0.5`; its canonical
`FeedbackReferenceStateDigest` is generated after approval and binds the map,
controller identity, region set, actuator references, units, polarity, and
normalization.

The complete supplied queue projection is explicitly:

| Queue field | Proposed value |
|---|---|
| `QueueId` | `00000000-0000-0000-0000-00000000c503` |
| `OwnerKey` | `Entity.Controller(00000000-0000-0000-0000-00000000c501)` |
| `GenerationCadenceOrNA` | `1.0 s` |
| `InitialNextSequence` / `NextSequence` | `0 / 0` |
| `LastMotionTime` | `0.0 s` |
| `AvailableCommand[c521]` | bounded `0.5`, lower `0.0`, upper `1.0`, rate `0.1 s^-1` |
| `AvailableCommand[c522]` | bounded `0.5`, lower `0.0`, upper `1.0`, rate `0.1 s^-1` |
| `PendingCommands` | empty list |
| `AppliedSourceEventIds` | empty list |
| `AllocatedCommandIds` | empty list |
| `QueueDigest` | generated deterministically after approval |

The map binds `MapVersion` and `FeedbackReferenceStateDigest` to this complete
supplied queue/controller reference. These fields are read-only P6-T05
preconditions; allocation, command-ID generation, and queue mutation remain
P6-T06 behavior.

### Proposed gains and biases

| Actuator | Bias | `K_P` | `K_I` | `K_T(left)` | `K_T(right)` | `K_TI(left)` | `K_TI(right)` |
|---|---:|---:|---:|---:|---:|---:|---:|
| Total-power `c521` | `0.5` | `0.001 W^-1` | `0.0001 (W s)^-1` | `0.0` | `0.0` | `0.0 s^-1` | `0.0 s^-1` |
| Tilt `c522` | `0.5` | `0.0 W^-1` | `0.0 (W s)^-1` | `-0.5` | `+0.5` | `-0.05 s^-1` | `+0.05 s^-1` |

The signed tilt gains and map weights are synthetic sign-test data. They are
not a physical RRS gain selection and are not inferred from DRAGON5, DONJON5,
or a production controller.

### Exact influence-map entries

Entries are sorted by actuator identity, target node, then group. Every entry
uses source unit `1` (dimensionless actuator state), target unit `m^-1`, and
weight unit `m^-1 per 1`; there is no implicit normalization.

| Actuator | Target nodes | Group 0 weight | Group 1 weight |
|---|---|---:|---:|
| Total-power `c521` | all six explicit nodes | `-0.02 m^-1 per 1` | `-0.01 m^-1 per 1` |
| Tilt `c522` | left `(0,0),(1,0),(2,0)` | `+0.01 m^-1 per 1` | `+0.006 m^-1 per 1` |
| Tilt `c522` | right `(3,0),(4,0),(5,0)` | `-0.01 m^-1 per 1` | `-0.006 m^-1 per 1` |

The sign certificate asserts the following local behavior: a positive total-
power error produces a total-power command above its reference, and the
negative total-power weights produce a power-increasing (negative absorption)
overlay when the supplied total-power actuator state rises above reference; a
left-region negative tilt error and right-region positive tilt error produce a
tilt command above reference, adding absorption on the left and reducing it on
the right. This is an explicit synthetic sign assertion, not a production
reactivity claim.

### Deterministic controller check vector

For one automatic event at `t=1.0 s`, the supplied measurement snapshot is:

| Measurement | Value |
|---|---:|
| Measured total power | `800 W` |
| Measured left-region power | `480 W` |
| Measured right-region power | `320 W` |
| Power error | `+200 W` |
| Left tilt error | `-0.1` |
| Right tilt error | `+0.1` |
| Next total-power integral | `200 W s` |
| Next tilt integrals | `-0.1 s / +0.1 s` |
| Total-power requested command | `0.72` |
| Tilt requested command | `0.61` |

Both commands are within `[0,1]`. A manual command example is total-power
command `0.65` at `t=2.0 s` from a separate explicit `Mode=Manual` pre-state
with the same actuator reference/state and a complete supplied queue. The
manual projection must not advance either integral or `LastUpdateTime`; it
produces one explicit manual command projection only. The automatic check
vector and manual check are separate pre-states; no implicit Automatic-to-Manual
transition is inferred. These expected values are deterministic synthetic
evidence only and are not golden data.

## Required checks and boundaries

The validator must reject duplicate or overlapping region nodes, region target
fractions that do not sum to one, nonpositive or stale measurement totals,
non-finite values, negative or out-of-range actuator states, invalid units,
missing gains, missing sign certificate, identity/version/reference digest
mismatch, duplicate map keys, direct-reactivity targets, and any nonzero
disabled contribution. It must preserve input bytes on rejected local
controller/map scratch evaluation.

P6-T05 may validate controller-owned state, manual/automatic command
projections from a complete supplied measurement and queue state, local RRS
overlay evaluation, explicit sign/setpoint/integral/cadence behavior, and local
scratch rollback. It may not allocate, enqueue, consume, transition, or roll
back shared queue state; queue allocation, command identity allocation,
same-time ordering, actuator motion, and atomic queue rollback remain reserved
for P6-T06. Scenario packages remain reserved for P6-T07. No shutdown, scram,
trip, safety response, thermal-hydraulic/CFD behavior, hidden reactivity path,
production, external-reference, or golden-data behavior is authorized.

After approval, P6-T05 must generate and record canonical controller, region,
map, reference-state, overlay, command-projection, package, and manifest
digests from the exact approved fields. Those digests are not placeholders and
are not supplied by this draft.

## Approval block

To authorize the recommended synthetic fixture, reply with:

```text
APPROVED: P6-T05-SYNTHETIC-RRS-CONTROLLER-MAP-V1
OWNER: Kevin Ho
SCOPE: Synthetic test-only engine-neutral Core / ReducedModel P6-T05
       limited total-power and regional-tilt controller state, explicit
       setpoint/error/integral units, automatic cadence, approved manual
       command projection, negative-feedback sign certificate, two-actuator
       local RRS influence map, deterministic command/overlay evidence, and
       fail-closed invalid-state checks; no production CANDU, external
       reference, or golden-data claim.
SIGN: Approved as written: power setpoint 1000 W, automatic cadence 1.0 s,
      two disjoint target regions with fractions 0.5/0.5, two actuators with
      reference/state/command 0.5, bounds [0,1], rate limits 0.1 s^-1,
      delays 0.5 s, the proposed gains/biases, negative-feedback sign
      certificate, six explicit target nodes, two groups, and 24 proposed
      map entries.
IDENTITY: Approved as written: FixtureId
          P6-T05-SYNTHETIC-RRS-CONTROLLER-MAP-V1, DataVersion
          p6-t05-synthetic-rrs-controller-map-v1, ControllerId
          00000000-0000-0000-0000-00000000c501, MapId
          00000000-0000-0000-0000-00000000c502, QueueId
          00000000-0000-0000-0000-00000000c503, TopologyId
          00000000-0000-0000-0000-00000000c504, RegionSetId
          00000000-0000-0000-0000-00000000c505, and deterministic digest
          generation/review after approval, including the
          FeedbackReferenceStateDigest.
CONTROL: Approved as written: ControlPolarity NegativeFeedback, sign
         certificate P6-T05-SYNTHETIC-NEGATIVE-FEEDBACK-V1, source unit 1,
         target unit m^-1, and normalization None.
QUEUE: Approved as written: the complete supplied read-only queue projection
       has QueueId 00000000-0000-0000-0000-00000000c503, typed controller owner
       c501, GenerationCadenceOrNA 1.0 s, InitialNextSequence 0,
       NextSequence 0, LastMotionTime 0.0 s, two bounded available commands
       at 0.5 with bounds [0,1] and rate 0.1 s^-1, empty pending/source-event/
       allocated-command registries, and deterministic QueueDigest generation
       after approval. P6-T05 may bind/read this state only; P6-T06 owns queue
       allocation and mutation.
MANUAL: Approved as written: the manual check uses a separate explicit
        Mode=Manual pre-state at t=2.0 s with command 0.65; it does not infer a
        mode transition and does not advance integral state or LastUpdateTime.
BOUNDARY: P6-T05 may validate controller-owned state, explicit measurement
          setpoint/error/integral/cadence behavior, manual/automatic command
          projections from the complete supplied state, local RRS overlay,
          sign checks, and local state/map scratch rollback. It may not
          allocate, enqueue, consume, transition, or roll back shared queue
          state; queue allocation, command identity allocation, same-time
          ordering, actuator motion, and atomic queue rollback remain reserved
          for P6-T06. Scenario packages remain reserved for P6-T07. No hidden
          reactivity, shutdown, scram, trip, safety, production,
          external-reference, or golden-data behavior.
DATE: 2026-08-20
```

If any proposed gain, sign, identity, map entry, or boundary is not acceptable,
reply `REJECT` with corrected fields. A production or external controller
package requires a separate complete authority, licensing, reproducibility,
unit, normalization, and sign record before it can be considered.
