# Two-group solver, normalization, and convergence v1

**Status:** frozen P2-T02 implementation input. G2 is FORCED CLOSED / WAIVED;
this administrative disposition does not authorize a new equation, unit, sign,
convergence rule, tolerance, or golden-data decision.

## Scope and non-goals

This document defines the first static two-group spatial eigenproblem over the
explicit nodes and edges from
[`topology-indexing-units-boundaries-v1.md`](topology-indexing-units-boundaries-v1.md).
It fixes the sign convention, coefficient meanings, finite-volume leakage
operator, fission source, fission-power normalization, deterministic source
iteration, convergence diagnostics, and invalid-state behavior.

It does not implement the solver, choose a runtime backend, add C# APIs, define
refuelling or burnup, couple kinetics/xenon/RRS, select a named station, or
approve reference tolerances. All numerical values in the worked examples are
clearly synthetic and are not reference or golden data.

## Authoritative state and node order

The spatial node identity is the explicit `(ChannelId, BundlePosition)` pair
from P2-T01. A production solve has 4,560 nodes. A synthetic solve may use a
smaller explicitly declared topology. The authoritative evaluation order is:

1. ascending `ChannelId`;
2. ascending `BundlePosition` within each channel; and
3. group 1 before group 2 for per-node group arrays.

This order is a deterministic reduction order only. It does not define physical
adjacency or flow direction. Every edge and boundary face is looked up from the
validated topology record; no neighbor is inferred from an array offset.

Within each node, neighbor terms are accumulated in the key
`(direction_rank, target_channel_id, target_position)`, where the cardinal
direction ranks are `North=0`, `East=1`, `South=2`, `West=3`,
`TowardEndA=4`, and `TowardEndB=5`. Boundary terms use
`(face_rank, position)` with `North=0`, `East=1`, `South=2`, `West=3`,
`EndA=4`, and `EndB=5`. Interior edges and boundary records are validated
against these keys before the solve; duplicate keys fail closed.

All reductions use scalar left-to-right IEEE-754 `double` addition in the
specified order. No reassociation, unordered parallel reduction, or
implementation-defined fused operation is part of v1. The linear-solver
traversal uses the same node and local-term order. If multiple diagnostics tie
for a maximum or first failure, the lowest canonical node/group/term key wins.

## Two-group quantities and units

For node `i` and energy group `g ∈ {1,2}`:

| Symbol/field | Meaning | Unit |
| --- | --- | --- |
| `φ_g,i` | scalar neutron flux | `m^-2 s^-1` |
| `Σ_a,g,i` | absorption macroscopic cross section | `m^-1` |
| `Σ_s,1→2,i` | downscatter macroscopic cross section | `m^-1` |
| `Σ_f,g,i` | fission macroscopic cross section | `m^-1` |
| `νΣ_f,g,i` | neutron-yield-weighted fission cross section | `m^-1` |
| `χ_g,i` | fission spectrum fraction | dimensionless (`1`) |
| `V_i` | node volume | `m^3` |
| `E_f,i` | energy released per fission used for the game power convention | `J` |
| `T_g,ij` | finite-volume edge conductance | `m^2` |
| `B_g,if` | finite-volume boundary conductance | `m^2` |
| `k` | effective multiplication factor | dimensionless (`1`) |
| `P_i` | integrated fission power | `W` |

All authoritative arithmetic uses finite `double` values. Coefficients are
per-node/per-group data; a later data-pack specification must define how they
are loaded, versioned, and hashed.

### Coefficient invariants

The v1 solver accepts only:

- `V_i > 0`, `E_f,i > 0`, and `Σ_f,g,i >= 0`;
- `Σ_a,g,i >= Σ_f,g,i`, because `Σ_a` includes fission absorption as well as
  non-fission absorption;
- `Σ_s,1→2,i >= 0` and `νΣ_f,g,i >= 0`;
- `χ_g,i >= 0` with `χ_1,i + χ_2,i = 1` for every node; and
- exactly one positive preassembled `T_g` for every canonical reciprocal
  interior edge and exactly one `B_g` for every keyed boundary face, subject to
  the label rules below.

Negative, non-finite, missing, dimensionally incompatible, or version-incoherent
coefficients fail closed. V1 has downscatter only: a nonzero upscatter
coefficient is rejected rather than silently ignored. Fission spectrum and
energy-per-fission conventions are data inputs, not hidden constants in the
solver. If a data pack also supplies a dimensionless average neutron yield
`ν_g`, it must satisfy `νΣ_f,g,i = ν_g,i * Σ_f,g,i`; otherwise the supplied
`νΣ_f` product is authoritative and no hidden yield is reconstructed.

The fission fields must describe one reaction consistently. For every node and
group, `Σ_f = 0` if and only if `νΣ_f = 0`. When fission is nonzero, both fields
must be finite and positive and their implied yield
`νΣ_f / Σ_f` must be finite and positive. A supplied `ν_g` must also be finite
and positive for a nonzero fission field and must satisfy the product identity
above. A zero/nonzero mismatch is invalid even when `ν_g` is omitted.

## Sign convention and equations

The removal coefficients are:

```text
Σ_r,1,i = Σ_a,1,i + Σ_s,1→2,i
Σ_r,2,i = Σ_a,2,i
```

Group 1 is the fast group and group 2 is the thermal group. With downscatter
only, the steady eigenproblem is:

```text
A_1 φ_1 = (χ_1 / k) F(φ)
A_2 φ_2 = Σ_s,1→2 φ_1 + (χ_2 / k) F(φ)
F_i(φ) = νΣ_f,1,i φ_1,i + νΣ_f,2,i φ_2,i
```

Here `A_g` is the removal-plus-leakage operator defined below. The right-hand
side is a source, so positive fission and downscatter source terms have positive
signs. `k` is positive and dimensionless. `χ_g` is applied locally; a future
specification may authorize a nonlocal fission spectrum, but v1 does not.

`Σ_a,g` is total neutron absorption in group `g`, including fission absorption.
`Σ_f,g` is the fission subset of that absorption and therefore cannot exceed
`Σ_a,g`. `νΣ_f,g` is the same fission cross section weighted by the supplied
neutron yield; it is not an additional absorption term. Fission absorption is
already represented in `Σ_a` and must not be added a second time to `Σ_r`.

## Finite-volume leakage operator

For a validated node `i`, group `g`, and explicit interior-neighbor set
`N(i)`, define the leakage/removal operator:

```text
(A_g φ_g)_i =
    Σ_r,g,i φ_g,i
  + (1 / V_i) [ Σ_{j ∈ N(i)} T_g,ij (φ_g,i - φ_g,j)
                + Σ_{f ∈ boundary(i)} B_g,if φ_g,i ]
```

V1 uses preassembled conductances as the authoritative spatial coefficients;
`D_g` is not a runtime input and no unapproved geometry formula derives `T` or
`B` from it. `T_g,ij` is present only for a reciprocal transverse or
within-channel edge.
The difference is `φ_i - φ_j`, so leakage from a high-flux node is positive in
the left-hand operator and does not create source. A reflective boundary has
`B_g,if = 0`. A vacuum or specified-leakage face must supply an approved,
finite positive `B_g,if` in the data contract; P2-T02 does not invent a Marshak
factor or derive a coefficient from an unapproved geometry.

The operator is conservative across an interior edge because the same
conductance appears in both reciprocal rows. Boundary contributions are
nonnegative and cannot be represented by a missing edge. A topology boundary
label with no valid boundary conductance is invalid solver input.

### Conductance binding

The canonical interior-edge key is the lexicographically ordered pair of
endpoint slot tuples, `(min(endpoint_a, endpoint_b), max(endpoint_a,
endpoint_b))`. For each group, the input contains exactly one shared positive
`T_g` for that key. A duplicate, missing, extra, or asymmetric conductance is
invalid. The key must correspond to exactly one validated reciprocal topology
edge from P2-T01.

The canonical boundary-face key is
`(channel_id, position, face_id, group)`. Each required boundary face has
exactly one `B_g` record: zero for `Reflective`, positive for `Vacuum` or
`SpecifiedLeakage`. A conductance record for an interior face, an unknown face,
or a boundary key absent from the topology is invalid. The label-to-conductance
rule is the complete v1 boundary representation; no implicit default exists.

## Fission power and normalization

The local fission power is:

```text
P_i(φ) = V_i E_f,i [Σ_f,1,i φ_1,i + Σ_f,2,i φ_2,i]
P_total(φ) = Σ_i P_i(φ)
```

`Σ_f,g` is the fission cross section used for power and is supplied alongside
`νΣ_f,g`; it has unit `m^-1`. The requested normalization target `P_target` is
a finite positive SI watt value supplied by the caller/configuration. After a
finite, componentwise nonnegative shape with strictly positive total fission
power is obtained, the solver applies:

```text
α = P_target / P_total(φ)
φ_g,i ← α φ_g,i  for both groups and every node
```

Normalization changes flux amplitudes and reported powers but does not change
`k` or the normalized shape ratios. The solver records `P_total_before`,
`P_target`, `α`, and `P_total_after`; the relative power-balance error is
`abs(P_total_after - P_target) / P_target`. A zero/non-finite total power,
non-positive target, or non-finite scale is an invalid state.

The multiplication factor is not normalized to one. It remains the eigenvalue
of the supplied coefficients and boundary conductances. Power normalization is
therefore separate from criticality and cannot conceal a coefficient or
convergence error.

## Deterministic source iteration

The v1 solve is a sequential power/source iteration. It uses preallocated flat
arrays and no implicit wall-clock or Unity-frame input.

1. Validate topology, coefficients, target power, and convergence policy.
2. Validate the initial finite, componentwise nonnegative flux shape. If no initial flux is supplied, create
   the deterministic all-ones shape and record that choice in diagnostics.
   Normalize the supplied or constructed componentwise nonnegative shape to
   `P_target` before
   assigning `φ^(0)`, and record the initial normalization scale.
3. For iteration `n`, compute `F^(n)` and `Q^(n) = Σ_i V_i F^(n)_i` from the
   current, already power-normalized componentwise nonnegative flux `φ^(n)`
   (including `n=0`).
   Set `k^(n)` to the supplied positive initial eigenvalue for `n=0`.
4. Form group 1's trial fission source and solve
   `A_1 ψ_1 = χ_1 F^(n) / k^(n)`.
5. Form group 2's trial downscatter plus fission source and solve
   `A_2 ψ_2 = Σ_s,1→2 ψ_1 + χ_2 F^(n) / k^(n)`.
6. Reject a failed linear solve, non-finite trial result, or negative trial
   flux component, or invalid diagnostic. Flux vectors are componentwise
   nonnegative, while total production and normalized power must be finite and
   strictly positive. The backend-neutral inner acceptance contract is
   defined below; no silent approximate success is allowed.
7. Compute trial `F_trial`, `Q_trial`, and the unnormalized eigenvalue update
   `k^(n+1) = k^(n) * (Q_trial / Q^(n))`.
8. Normalize the trial shape `ψ` to `P_target`, yielding `φ^(n+1)`. Recompute
   `F^(n+1)` and `Q^(n+1)` from that normalized flux. The next iteration uses
   these recomputed values, never stale trial values. Build the residual right
   sides from `F^(n+1)` and `k^(n+1)`.
9. Record all diagnostics, test convergence, and either return a converged
   immutable result or continue with `n ← n+1` and the normalized state
   `(φ^(n+1), F^(n+1), Q^(n+1), k^(n+1))`.

All sums use the authoritative node/group order in this document. A future
parallel backend must reproduce the scalar result within a separately approved
equivalence contract; P2-T02 introduces no parallelism or lower precision.

### Inner linear-solve acceptance

Each group solve consumes a backend-neutral `LinearSolvePolicy` containing a
deterministic `method_id` and version, positive `absolute_residual_tolerance`,
positive `relative_residual_tolerance`, and positive integer
`maximum_inner_iterations`. The backend may choose an approved scalar method,
but it must expose the same acceptance contract for `A_g x = b`:

```text
r_inner = A_g x - b
```

The scalar acceptance metrics are global infinity norms over the group, not a
componentwise relative maximum:

```text
inner_absolute_residual_inf = max_i abs(r_inner,i)  [m^-3 s^-1]
inner_scale_inf = max_i (abs((A_g x)_i) + abs(b_i)) [m^-3 s^-1]
inner_relative_residual_inf = 0                     if inner_scale_inf = 0
                               inner_absolute_residual_inf / inner_scale_inf
                                                       otherwise [dimensionless]
```

The inner solve succeeds when either one of the two scalar residual inequalities
is satisfied, all values are finite, and `x >= 0` where a flux solve requires
nonnegative values. It records `inner_converged`, method/version,
iteration count, maximum residual, and failure reason. Exhausting the inner
limit, encountering a non-finite value, or failing the policy fails the outer
solve. The outer solver cannot reinterpret an inner failure as convergence.

For an explicit scalar acceptance check, let `y_i = (A_g x)_i` and
`r_inner,i = y_i - b_i` for every node in the group. The two scalar metrics
above are recomputed from these values using the canonical node order. The
group solve is accepted when at least one explicit inequality holds:

```text
inner_absolute_residual_inf <= absolute_residual_tolerance OR
inner_relative_residual_inf <= relative_residual_tolerance
```

`absolute_residual_tolerance` therefore has units `m^-3 s^-1`; the relative
tolerance is dimensionless. The recorded `maximum residual` is the scalar
`inner_absolute_residual_inf`, and the first invalid node in canonical order is
reported for a failed check.

## Residuals and convergence

For each node and group, let `lhs_g,i = (A_g φ_g)_i` and `rhs_g,i` be the
corresponding eigenproblem source. Define the zero-safe global residual metrics
over both groups and all nodes:

```text
residual_absolute_inf = max_g,i abs(lhs_g,i - rhs_g,i) [m^-3 s^-1]
residual_scale_inf = max_g,i (abs(lhs_g,i) + abs(rhs_g,i)) [m^-3 s^-1]
ρ_inf = 0                                      if residual_scale_inf = 0
        residual_absolute_inf / residual_scale_inf otherwise [dimensionless]
```

The global scale prevents a zero-source row with a tiny floating-point
roundoff from becoming a unit relative residual. Both
`residual_absolute_inf` and `ρ_inf` are recorded; `ρ_inf` is the dimensionless
metric used by `residual_tolerance`.

The iteration also records:

```text
δk_abs = abs(k^(n+1) - k^(n))
δk_rel = δk_abs / max(abs(k^(n+1)), abs(k^(n)))
s^(n)_i = V_i F^(n)_i / Q^(n)
source_shape_change_inf = max over nodes i of abs(s^(n+1)_i - s^(n)_i)
power_balance_rel = abs(P_total_after - P_target) / P_target
```

The caller must provide a complete `ConvergencePolicy` containing finite,
positive `k_absolute_tolerance`, positive `k_relative_tolerance`, positive
`residual_tolerance`, positive `source_shape_tolerance`, positive
`power_balance_tolerance`, and a positive integer `maximum_iterations`. These
are policy inputs, not constants selected by P2-T02. Missing, non-positive, or
non-finite policy values fail before iteration.

An iteration is converged only when all of the following are true:

```text
δk_abs <= k_absolute_tolerance OR δk_rel <= k_relative_tolerance
ρ_inf <= residual_tolerance
source_shape_change_inf <= source_shape_tolerance
power_balance_rel <= power_balance_tolerance
```

The solver records `converged`, `convergence_reason`, final residuals,
iteration count, policy values, and all clamp/invalid-state counters. Reaching
`maximum_iterations` without satisfying every condition is `nonconverged` and
is a failed solve. No tolerance is loosened, replaced, or inferred from a
reference result.

## Diagnostics and fail-closed behavior

Every result carries:

- input/data-pack identity and topology version;
- `k`, total power before/after normalization, normalization scale, and the
  deterministic iteration count;
- `ρ_inf`, `δk_abs`, `δk_rel`, `source_shape_change_inf`, and power-balance
  error for the final iteration;
- convergence policy values and reason;
- counts of invalid coefficients, negative fluxes, non-finite values, failed
  linear solves, rejected upscatter, and any explicit clamps; and
- the first deterministic invalid/nonconverged diagnostic when the result
  fails.

NaN, infinity, negative flux, zero production, invalid boundary conductance,
negative power, failed linear solve, incompatible dimensions, rejected
upscatter, or nonconvergence returns a failed result with no usable state. It
does not return the last iterate as if it were converged. V1 authorizes no
clamps: a successful result has clamp count zero, and any attempted clamp,
including replacing a negative or non-finite value with a boundary value,
fails closed.

## Worked synthetic examples

These examples are algebraic contract checks only. Their coefficients are
synthetic placeholders and are not copied from DRAGON5, DONJON5, a private
listing, or a golden dataset.

### One node, no leakage, downscatter only

For one node with `χ_1 = 1`, `χ_2 = 0`, and no leakage, the equations reduce to:

```text
Σ_r,1 φ_1 = (νΣ_f,1 φ_1 + νΣ_f,2 φ_2) / k
Σ_a,2 φ_2 = Σ_s,1→2 φ_1
k = [νΣ_f,1 + νΣ_f,2 (Σ_s,1→2 / Σ_a,2)] / Σ_r,1
```

Use the explicitly synthetic values
`Σ_a,1=0.3 m^-1`, `Σ_f,1=0.1 m^-1`, `Σ_a,2=0.2 m^-1`,
`Σ_f,2=0.1 m^-1`, `Σ_s,1→2=0.1 m^-1`,
`νΣ_f,1=0.15 m^-1`, and `νΣ_f,2=0.25 m^-1`. Then
`Σ_r,1=0.4 m^-1`, `φ_2/φ_1=0.5`, and `k=0.6875`.

For one complete source iteration, use `V=1 m^3`, `E_f=1 J`,
`P_target=0.30 W`, raw initial shape `(1,1)`, and `k^(0)=1.0`. Step 2
normalizes that raw shape to `φ^(0)=(1.5,1.5)`. The initial fission
production is `F^(0)=0.60` and `Q^(0)=0.60 s^-1`. The trial solves give
`ψ=(1.5,0.75)`, `F_trial=0.4125`, `Q_trial=0.4125 s^-1`, and
`k^(1)=1.0*(0.4125/0.60)=0.6875`. Trial power is
`0.1*1.5 + 0.1*0.75 = 0.225 W`, so `α=0.30/0.225=4/3` and the normalized state is
`φ^(1)=(2,1)`. Recomputing from that normalized state gives
`F^(1)=0.55`, `Q^(1)=0.55 s^-1`, and `P_total=0.30 W`.

The next iteration must use `F^(1)` and `Q^(1)`, not stale trial `Q_trial`:
`F^(1)/k^(1)=0.55/0.6875=0.80`, which solves back to `(2,1)` and leaves
`k=0.6875`. The residuals are zero for both groups, the one-node source-shape
change is zero, and power-balance error is zero in exact arithmetic. This
example is synthetic and does not specify runtime tolerances.

### Two symmetric nodes

For two equal-volume nodes with reciprocal equal conductance, identical
coefficients, reflective outer boundaries, and equal initial flux, the edge
terms cancel because `φ_i - φ_j = 0`. The two nodes retain equal flux and
power at every scalar iteration, and total-power normalization scales both by
the same `α`. This checks reciprocal conservation, symmetry, and normalization
without asserting a physical reactor value.

For an independently checkable unequal-volume conservation check, use
`V_1=2 m^3`, `V_2=1 m^3`, `T=0.6 m^2`, `φ_1=3`, and `φ_2=1` as synthetic
values. The volume-integrated edge terms are `V_1 L_1 = 0.6*(3-1)=1.2` and
`V_2 L_2 = 0.6*(1-3)=-1.2`; their ordered scalar sum is zero. Shuffling input
records does not change this result because the canonical endpoint and local
term keys restore the specified order.

For a stronger deterministic-reduction check, use one synthetic assembled row
whose three canonical local terms, in `North`, `East`, `South` order, are
`1.2`, `-0.0000003`, and `0.045` `m^-3 s^-1`. Input permutation A presents
`[South, North, East]`; permutation B presents `[East, South, North]`. Both
permutations are sorted by the required local ranks before scalar reduction, so
both produce the ordered double sum `1.2449997 m^-3 s^-1`. The result is not
allowed to depend on the input record order.

For the normalized one-node state above, `A_1 φ_1=0.4*2=0.8` and
`χ_1 F/k=0.55/0.6875=0.8`; `A_2 φ_2=0.2*1=0.2` and
`Σ_s,1→2 φ_1=0.1*2=0.2`. Thus both local normalized residuals are zero and
the max residual is zero before any configured tolerance is applied.

### Deliberate nonconvergence

For a numerical policy check, reuse the normalized one-node example and set the
synthetic policy to `maximum_iterations=1`,
`k_absolute_tolerance=0.01`, `k_relative_tolerance=0.01`,
`residual_tolerance=0.000001`, `source_shape_tolerance=0.000001`, and
`power_balance_tolerance=0.000001`. These values are example inputs only, not
v1 defaults. The first iteration gives
`δk_abs=abs(0.6875-1.0)=0.3125` and `δk_rel=0.3125`, so the named eigenvalue
condition fails even though the residual, source-shape, and power-balance
conditions are zero. Exhausting the one-iteration limit therefore returns
`nonconverged`, includes the final residual diagnostics, and contains no usable
converged state. The implementation must not increase the limit or relax a
tolerance automatically.

## Reference-case boundary

The P1 compact exports are provenance and parser evidence, not solver inputs.
The DONJON smoke case's `6x6x1 reflective` description can inform a later
synthetic topology fixture, but the compact export supplies no coefficients,
node coordinates, edge conductances, or approved solver tolerance. The DRAGON
KINF observations likewise cannot tune this solver. P2-T02 does not add a
DONJON golden comparison or regenerate reference data.

## Definition of done for P2-T02

- The two-group unknowns, coefficients, units, signs, and fission spectrum are
  defined without an implicit convention.
- The topology-driven finite-volume operator and boundary conductance policy
  are explicit and conservative across reciprocal edges.
- Fission production, K-effective update, fission-power normalization, and
  dimensionless residuals are separate and auditable.
- Convergence requires a complete explicit policy and fails closed on
  nonconvergence or invalid numeric state; no tolerance is invented.
- Deterministic ordering, diagnostics, and synthetic one-/two-node checks are
  documented.
- No runtime solver, parallel backend, golden data, or private reference
  artifact is added by this task.

Source: the retained P2-T02 specification and
[`topology-indexing-units-boundaries-v1.md`](topology-indexing-units-boundaries-v1.md).
