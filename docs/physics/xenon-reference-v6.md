# Burnup-bound xenon replacement and reference-derived fuel decay

The active pack is `candu6-two-group-diffusion-v1-xenon-reference-v6`.
It preserves the complete v5 fuel coefficient tables and uses an explicit
included Xe-135 reference instead of the old fixed spatial startup offset.
The 21 adjusters remain fully inserted; thermal output remains 2,064 MW.

## Accounting correction

The [Naceur and Marleau depletion study](https://publications.polymtl.ca/5047/11/2018_Naceur_Neutronic_analysis_accident_tolerant_cladding.pdf)
includes poison accumulation in its burnup calculations and uses 31.9713 kW/kg
specific power. Its k curve cannot by itself identify the Xe-135 component.
The new embedded reference therefore **assigns an explicit authored component**
to this surrogate, using the existing analytic I-135/Xe-135 data at that reference
specific power. It does not claim extraction of measured source isotope densities
or a transport-validated poison-separated cross-section library.

Reference evolution starts with zero iodine/xenon and is generated offline with
frozen-flux analytic steps of at most six hours. The homogeneous two-group flux
ratio is `phi2/phi1 = Sigma_s12/Sigma_a2`; flux amplitude follows the declared
specific power, the game mass of 20.6 kg HM, cell volume and fission energy.
The 21 embedded density knots match the existing fuel knots exactly. Their
interpolation is linear and their domain is closed. Fresh fuel has zero included
reference Xe. Pack validation rejects inconsistent knots, negative densities,
unsupported identities, or a subtraction that removes fission absorption support.

Each live trial uses:

`Sigma_a_eff = Sigma_a_curve(B) + Sigma_a_adjusters + Sigma_a_zones + sigma_Xe * (Xe_actual - Xe_reference(B))`.

Equivalently, remove the declared included component and add actual Xe once.
The reference follows each bundle's current burnup, not a fixed spatial slot.
Retained actual inventories move with bundle ID; fresh bundles start with both
actual and included reference poison equal to zero. Burnup changes refresh only
the reference binding, without resetting actual iodine/xenon. The same frozen
actual poison field enters every controller candidate for one accepted event.

Archived undeclared packs retain their old behavior. Active included packs are
marked `XenonBasis=Included`; they cannot enter the Phase-7 primitive that requires
an explicitly Xe-free base. Its research fixture now subtracts the declared
component before declaring `xenon_basis: excluded`.

## Startup and channel reference

Startup settles local equilibrium inventories, the spatial flux and bounded RRS
at zero run time, using the same replacement overlay as play. Poison fixed-point
accuracy follows the configured diffusion source-shape tolerance; criticality
still uses the normal 0.05-mk band. Failure to settle rejects startup.

The time-average reference integrates both fuel coefficients and the included Xe
reference over the prescribed dwell ranges. It replaces the averaged included
component with self-consistent equilibrium Xe at the averaged power shape, with
all zones fixed at 50%. It retains 380 channels and sums to 2,064 MW thermal.
The reference identity is `cycle190-time-average-21-adjusters-xenon-reference-half-zones-v3`.
Targets span **4.977607–5.743994 MW/channel**. Initial RMS deviations for seeds
1001, 1002 and 1013 are 1.5769%, 1.5366% and 1.5823%.

## Decay calibration and independent checks

The selected policy retains the reference-derived fuel curve; **no 0.5-mk/FPD
contrast fit is applied**. Freeze actual Xe inventories and zone levels. Deposit
one full-power day of energy using the initial accepted shape, refresh the
included reference from the resulting burnup, and solve again. This isolates
fuel/depletion change from an evolving live xenon field.

| Measurement | Result |
| --- | ---: |
| Independent frozen-actual-Xe fuel loss | 0.347612 mk/FPD |
| Joint-fit frozen-actual-Xe fuel loss | 0.347627 mk/FPD |
| Joint-fit adjuster worth, fixed poison and zones | 17.000711 mk |
| Joint-fit liquid-zone worth, fixed poison | 6.999989 mk |
| Joint-fit half-zone k | 0.999999817 |

The earlier 0.412585 mk/FPD measurement used the pre-adjuster v4 shape and
unseparated reference curve. V5 with adjusters measured 0.377759 mk/FPD. These
are distinct model conditions, not universal CANDU fuel-decay constants. The
new measurement is about 0.35 mk/FPD after replacing the assigned embedded Xe
component while freezing actual Xe.

The refit preserves every fuel coefficient and interior conductance. It adjusts
only rod absorption strength, effective boundary leakage and a small zone-slope
correction to retain 17-mk rods, 7-mk zones and critical startup. Fast/thermal rod
strengths are 0.005257186889648438/0.052571868896484374 m^-1. Fast boundary
conductance is 0.0007871529201316833 m², with thermal half that. Zone slopes are
0.0017640042499876617/0.0007056016999950648 m^-1 per full fill.

- [Immutable v5 source](../../data/calibration/xenon-reference-v6/source-pack.json).
- [Reference history and joint fit](../../data/calibration/xenon-reference-v6/fit/fit.json).
- [Independent frozen-Xe audit](../../benchmarks/xenon-reference-v6-reactivity.json).
- [All channel targets](../../benchmarks/xenon-reference-v6-reference/reference.json) and
  [CSV](../../benchmarks/xenon-reference-v6-reference/reference.csv).

```powershell
dotnet run --project tools/AgedCoreBenchmark -c Release -- --fit-xenon-reference data/calibration/xenon-reference-v6/source-pack.json tmp/xenon-reference-v6-refit
# Review the proposal and matching zone slopes before staging a future revision.
tools/Sync-PhysicsPacks.ps1 -Stage
dotnet run --project tools/AgedCoreBenchmark -c Release -- --reactivity-scale benchmarks/xenon-reference-v6-reactivity.json
dotnet run --project tools/ChannelReferenceBenchmark -c Release -- benchmarks/xenon-reference-v6-reference
```

Core and Game checks include clean fresh fuel, retained poison/reference
movement, frozen actual poison during burnup, closed-domain metadata validation,
startup coupling, pause, refuelling, deterministic time partitions and normal
limits. Browser evidence is retained in the v6 smoke/reproduction reports. New
pack and reference identities intentionally change saved-run and wire digests.
The earlier 100-day v4 results remain historical; no v6 100-day claim is made.

## Verification

Core passed 122 tests, Game passed 74 tests, Browser passed 37 tests and the
frontend passed 149 tests. Both production frontend builds passed. The
[local Pages WASM matrix](../../benchmarks/xenon-reference-v6-browser.json)
matched six rows across two samples with identical digests and no console/page
errors. The runtime is Release non-AOT; its build-info commit is the base HEAD,
so these records describe the working tree, not a published deployment.
[Verification](../../benchmarks/xenon-reference-v6-verification.json) records
source/active pack hashes, preserved coefficients and final browser checks.

The [full Pages smoke](../../benchmarks/xenon-reference-v6-smoke.json) passed
refuels, fresh-fuel zero poison, history/session preservation, responsive layouts,
challenge completion with eight useful bundles and the badge, empty retry,
seeded retry/reset and recovery. All 382 shared/frontend tests passed. The local
preview was stopped after validation; no deployment was performed.
