# Refuelling and nominal regional flux audit

Audit date: 2026-10-04. Measurements below use the pre-limit `innerrel1e7` pack;
the later [power-limit balance](power-limit-balance.md) changes spatial profiles,
so its numerical responses must be measured separately. This records the controller, not a replacement
control policy. The controller remains
`synthetic-practice-liquid-zone-criticality-first-rrs-v4`.

## Expected response

[Ben Rouben, AECL, CANDU Fuel Management Course, sections 3.1 and 7.7.3.2](https://www.nuceng.ca/canteach-rev2/library/20031101.pdf)
describes differential control using current/reference zone flux ratios. Zones
with a ratio above the average receive more water; zones below it receive less.
The reference is normally taken from a time-average flux calculation, either
at detector locations or averaged over the zone. The game's zone averages are
a suitable surrogate because it does not model point detectors.

For the existing fourteen regions, a candidate nominal-flux measurement is:

```
phi_nom[z] = mean(time_average_solve.Group2Flux[node], node in region z)
ratio[z]   = mean(current_solve.Group2Flux[node], node in region z) / phi_nom[z]
tilt[z]    = ratio[z] / mean(ratio[0..13]) - 1
```

This preserves the naturally peaked no-adjuster shape and separates the spatial
error from bulk power. The nominal node flux must come from the same
[no-adjuster time-average solve](channel-power-reference.md) as channel power;
channel power alone does not determine flux when fuel coefficients change.

The course describes bulk control every half second and spatial control every
two seconds. [UNENE, Essential CANDU, I&C section 4.1](https://unene.ca/essentialcandu/pdf/10%20-%20IandC.pdf)
also describes combined bulk and spatial valve feedback at these intervals.
These are control updates; physical water levels do not jump instantaneously.
The game instead commits bounded equilibrium fill changes during a refuelling
transaction.

## Current implementation

- `GameSession.TryBuildRefuellingTransaction` rebinds iodine/xenon to the new
  bundle inventory and calls `TryBuildRrsEquilibrium` immediately, including
  while paused. No simulation time elapses for this action.
- `PracticeLiquidZoneRrsV1.TryCreate` takes its fourteen references from the
  seed's initial instantaneous **fission-power fractions**.
- `MeasureZonalPower` sums node fission power and divides by total core power.
  It never reads thermal group flux. Changing fuel fission coefficients can
  change this measurement independently of a regional flux change.
- `PracticeGameSessionFactory.ReferenceChannelPower` is consumed by scoring
  and presentation. It is not passed into RRS. Its underlying solve computes
  both flux groups, but the reference object retains only channel powers.
- Outside the +/-0.05 mk criticality band, `TrySolveBoundedFillCommand` requests
  common fill movement. `IsBetterCandidate` accepts an improvement in absolute
  net reactivity without requiring spatial error to improve. Differential
  power-shape control is attempted inside the band.
- Spatial convergence permits 0.01 absolute error in each region's fraction of
  total power. This is not a 1% error relative to each region's nominal flux.
- Each event permits one controller pass, at most four diffusion candidates,
  and eight percentage points of fill movement per compartment. Subsequent
  scheduled control events occur every 1,800 simulated seconds.

Therefore immediate refuelling feedback exists, but fixed nominal regional
flux balancing is not implemented. Correcting bulk criticality alone does not
establish that regional flux ripple decreased.

## Measured response

All twelve cases passed the immediate-control checks. Ten commands were uniform
across all fourteen compartments; the two channel-75 cases for seed 1001 also
received differential power-shape correction. Absolute bulk reactivity improved
in all cases, leaving at most 0.014762 mk. Nominal regional flux RMS increased
slightly in five cases. These are independent refuelling cases, not a sequence
of two moves in one session.

| Seed | Hour | Channel index | Mean fill before -> after (%) | Flux RMS at old fills -> controlled (%) |
| --- | --- | --- | --- | --- |
| 1001 | 0 | 75 | 50.00000 -> 56.04484 | 12.375354 -> 12.189784 |
| 1001 | 0 | 324 | 50.00000 -> 53.37015 | 6.120141 -> 6.125169 |
| 1001 | 14 | 75 | 44.80722 -> 50.15164 | 11.846871 -> 11.299358 |
| 1001 | 14 | 324 | 44.80722 -> 47.56737 | 5.872295 -> 5.882403 |
| 1002 | 0 | 75 | 45.29939 -> 46.79534 | 4.600466 -> 4.599158 |
| 1002 | 0 | 324 | 45.29939 -> 47.96576 | 6.743944 -> 6.775477 |
| 1002 | 14 | 75 | 40.17712 -> 41.04817 | 4.955482 -> 4.956166 |
| 1002 | 14 | 324 | 40.17712 -> 42.14591 | 6.783900 -> 6.810238 |
| 1013 | 0 | 75 | 47.64881 -> 49.65800 | 11.911120 -> 11.908819 |
| 1013 | 0 | 324 | 47.64881 -> 50.36318 | 12.101104 -> 12.088103 |
| 1013 | 14 | 75 | 42.28116 -> 43.48407 | 12.013271 -> 12.004929 |
| 1013 | 14 | 324 | 42.28116 -> 44.11650 | 12.039047 -> 12.021437 |

Channel 75 flows toward End A; channel 324 flows toward End B. The audit uses
the topology's direction for each operation. The flux-RMS columns compare the
same freshly refuelled inventory with and without the immediate fill response;
they are not the unrefuelled core's RMS and are not the channel-power score.

## Reproduction

`PracticeRrsFluxAuditTests` exercises paused refuelling in both channel flows.
Its ordinary regression checks seed 1001 at startup. The explicit audit matrix
also checks fourteen hours and seeds 1002 and 1013. It asserts
same-time control, bounded fill changes, preserved targets, and no score gain
on the paused refuelling action. It reports rather than asserts improvement of
the missing nominal-flux objective.

The comparison solve uses the new inventory, actual rebound xenon overlay,
and **pre-refuelling fills**. Its flux ripple isolates the refuelling
perturbation from the water-level response. The reported RMS is the equal-zone
RMS of `tilt[z]` above; it is distinct from the channel-power scoring RMS.

```powershell
$env:CANDU_RRS_FLUX_AUDIT_MATRIX = '1'
dotnet test tests/ReactorSim.Core.Tests --filter FullyQualifiedName~PracticeRrsFluxAuditTests --logger 'trx;LogFileName=rrs-flux-audit.trx' --results-directory tmp/rrs-flux-audit
Remove-Item Env:CANDU_RRS_FLUX_AUDIT_MATRIX
```

The TRX captures the complete JSON measurement rows, including all fourteen
fill commands. This audit does not claim reproduction of the literal PHINOM
routine from a particular station's LZC manual; the cited AECL course supplies
the documented nominal-flux balancing rule.
