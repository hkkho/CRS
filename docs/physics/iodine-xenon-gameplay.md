# Spatial iodine and xenon in the browser game

The practice game owns a compact immutable `PracticeXenonStateV1` in Core.
There are 4,560 pairs of I-135/Xe-135 number densities, in channel-major order,
bound to live bundle identities. Game owns candidate/commit orchestration;
Browser serializes the results and Studio displays observations.

## Authored data and calibration

`PracticeXenonDataV1` embeds the gameplay data and a digest of every parameter:

| Parameter | Value | Unit |
|---|---:|---|
| I-135 yield | 0.063 | atoms/fission |
| direct Xe-135 yield | 0.003 | atoms/fission |
| I-135 half-life | 6.57 | hours |
| Xe-135 half-life | 9.14 | hours |
| fast Xe absorption cross section | 0 | m² |
| thermal Xe absorption cross section | 1.3e-24 | m² |

The thermal cross section is deliberately softened for this project's synthetic
compact diffusion pack. This is a gameplay approximation, not plant nuclear data.
The production/decay/absorption structure follows the DOE reactor theory handbook:
https://ncsp.llnl.gov/sites/ncsp/files/2024-03/doe_fundamentals_handbook_nuclear_physics_and_reactor_theory_vol_2_of_2.pdf

The active [v6 reference replacement](xenon-reference-v6.md) declares an included
Xe-135 reference at every fuel burnup knot. Each trial uses
`base + sigma * (Xe_current - Xe_reference(current_burnup)) + zones + adjusters`.
Startup settles equilibrium poison and RRS with the same accounting. The reference
is an authored decomposition at the source specific power, not measured isotope
data. It follows current bundle burnup through movement and energy deposition;
fresh actual and reference poison are both zero. The original fixed spatial
startup rebase remains only for archived undeclared packs. Effective absorption
still passes the finite-value and `absorption >= fission` validation.

## Analytic integration

For each node, accepted group fluxes are multiplied by actual power amplitude.
The accepted fission coefficients supply `F = Sigma_f1 * phi1 + Sigma_f2 * phi2`.
Number densities are in atoms/m³, flux in m⁻²s⁻¹ and time in seconds:

```text
dI/dt = gamma_I * F - lambda_I * I
dX/dt = gamma_X * F + lambda_I * I - b * X
b = lambda_X + sigma_1 * phi1 + sigma_2 * phi2
```

For a frozen source/flux interval, use the exact triangular Bateman solution.
`Phi(x) = (1 - exp(-x)) / x` uses its short-interval series near zero.
The divided exponential is evaluated as
`exp(-min(lambda_I,b)*dt) * dt * Phi(abs(b-lambda_I)*dt)`.
This handles equal rates and large intervals without unstable explicit Euler
substeps. Invalid or nonfinite results reject the candidate; no density clamp
hides an invalid update. The legacy explicit Euler contracts remain unchanged.

## Coupling and fuel behavior

Short ticks integrate burnup and poison using the retained accepted shape.
Each 180-second browser simulation step performs a warm-started diffusion/RRS
event with updated burnup and a frozen poison overlay. The same overlay enters
the uncompensated baseline and every zone verification/correction trial.
There is no second direct xenon-reactivity term. More regional poison changes
regional power through the spatial operator; the fourteen-zone controller responds
to those measured powers and the resulting net reactivity.

Refuelling carries retained inventories by bundle ID. New bundles have zero
iodine and xenon; discharged inventories disappear. A refuel solves immediately
with the moved poison field and only commits it with the accepted inventory,
projection, clock, zone fills and score. Failed commands leave poison unchanged.
Pausing freezes poison. A new shift recreates the seeded equilibrium state.
Designer edits retain history; cells without fuel have zero fission source and
both actual and included-reference absorption are masked while configured without fuel.
Their retained inventory still undergoes decay and local flux-dependent removal.

The game remains a slowly evolving regulated equilibrium model. It does not
add prompt kinetics, shutdown, scram or accident scenarios. Browser 1x advances
30 simulated minutes per wall second; numerical accuracy and responsiveness
must therefore be checked in simulation time and wall time separately.

## Presentation and verification

Existing xenon diagnostics now report live core/channel summaries. RRS regions
add iodine/xenon means using the current measured-region map, independently of
absorber masks. Studio's channel inspector plots all twelve bundle iodine/xenon densities.
Both full and compact responses publish current channel-major inventory vectors,
so short ticks refresh these plots independently of the retained spatial solve.
Fresh bundles are visibly zero immediately after a paused refuel; the four
retained bundles remain nonzero. Studio's Iodine & xenon tab charts core means and fourteen
regional xenon traces; the zone strip includes xenon in its tooltip.
History is bounded observation data and contains no poison calculation.
The current poison time/digest is distinct from the poison time/digest used by
the retained spatial solve; the bridge publishes both explicitly. Coefficients
and zone power remain the accepted solve until the next scheduled boundary.
Poison digests and overlays are cached lazily, so intermediate integration
candidates do not allocate thousands of coefficient records or hash discarded
states. Once requested, an immutable state's digest/overlay is reused.

The [implementation benchmark report](iodine-xenon-benchmarks.md) records the
measured AOT/worker performance, remaining pacing limits and verification results.

Focused tests cover frozen-source equilibrium, independent RK4 comparison,
large steps, equal removal rates, invalid inputs, inventory movement, pause,
asymmetric spatial/control response, and wall-time partition determinism.
Use `tools/Test-DotNet.ps1`, `tools/Test-Browser.ps1`, and the browser reproduction
benchmark after rebuilding the WASM host. Benchmark build mode must be recorded;
non-AOT timings must not be presented as AOT production performance.
`npm run benchmark:xenon -- <url> 20` additionally measures actual production
worker round trips over ten simulated hours after refuelling and a power change,
checks poison/zone evolution, and samples the faster playback modes. Its report
states whether the proposed 250 ms p95 target and one-second service budget pass.
