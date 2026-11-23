# Iodine and xenon calibration

The active practice pack uses a cumulative I-135 yield of 0.064 and a direct
Xe-135 yield of 0.006 atoms per fission. It uses the 6.57-hour and 9.14-hour
half-lives. Iodine comes from fission and disappears by decay. Xenon comes
from direct fission and iodine decay and disappears by decay and neutron
capture. Core integrates both densities analytically over each frozen-source
interval. [CanTeach fission-product poisoning, §6.3](https://canteach.candu.org/Content%20Library/20044302.pdf)
and [CNSC reactor physics, chapter 5](https://canteach.candu.org/Content%20Library/20030101.pdf)

The compact pack's thermal flux is a homogenized, power-normalized group
quantity. Its fast-group xenon capture coefficient is zero. An effective
thermal capture coefficient of `9.4e-23 m²` puts about 90% of equilibrium
Xe-135 removal into capture at full power. An independent worth scale of 0.5
converts capture to the effective group absorption coefficient used by the diffusion solver. It
matches the approximately −28 mk equilibrium xenon worth without transferring
a microscopic thermal cross section directly into the compact surrogate.
[CanTeach xenon transients](https://canteach.candu.org/Content%20Library/20050512.pdf)
and [CNSC xenon worth](https://canteach.candu.org/Content%20Library/20030101.pdf)

The Custom physics thermal absorption multiplier scales both capture and the
diffusion absorption coefficient by the same factor. Custom physics can also
set a nonzero fast-group absorption coefficient; in that case the live poison
removal rate includes fast capture as well as the thermal term shown below for
the default reference. The diffusion overlay also adds the live-minus-reference
xenon density times that fast coefficient to fast-group absorption; the
thermal worth scale does not apply to this fast-group term.

## Fuel-table reference

Each nonzero burnup knot stores the local equilibrium Xe-135 density at
31,971.3 W/kg HM and 20.6 kg HM per bundle. The first nonzero knot corresponds
to about 120 hours at this specific power. The fresh knot stores zero.

```text
F_ref    = specific_power × bundle_HM_mass / (cell_volume × fission_energy)
phi2_ref = F_ref / (Sigma_f2 + Sigma_f1 × Sigma_a2 / Sigma_s12)
Xe_ref   = (gamma_I + gamma_Xe) × F_ref / (lambda_Xe + sigma_capture × phi2_ref)
Delta_Sigma_a2 = (Xe_live − Xe_ref) × sigma_capture × worth_scale
```

The reflective-lattice two-group flux ratio supplies `phi2_ref`. The
diffusion solve replaces the included reference with live xenon at the
current bundle burnup. Both neutron-production groups receive a
common multiplier of 1.0010823199232564, the reciprocal of the seed-1001
half-fill equilibrium multiplication factor before normalization. It brings
the half-fill state close to criticality while preserving the relative
production curve. The static report records `k = 1.00000398` for that state.
[V10 geometry fit](../../data/calibration/diffusion-v10/README.md)
and [Point Lepreau reference design, §§5–6](https://www.unene.ca/essentialcandu/pdf/2%20-%20Genealogy%20of%20CANDU%20Reactors.pdf)

With actual xenon frozen, the seed-1001 fuel-only reactivity loss is about
0.34 mk per full-power day. The full liquid-zone worth is about 7 mk.
The [static reactivity report](../../benchmarks/xenon-v11/reactivity.json)
records the pack, power, method and values.

## Spatial benchmark

The 96-hour benchmark applies the lateral 80/20 zone split at full power.
It compares a held split with a free response after all zones return to
50% at hour eight. Fuel inventory and burnup stay fixed and the benchmark
bypasses regulation and operating limits. Both runs use the v11 physics with
tighter solver tolerances than the default pack; the reports identify these
overrides with a custom pack ID and explicitly set the xenon absorption
multiplier to 1. The equilibrium worth is a shared static baseline. The
benchmark measures same-side peak spacing
and estimates damping from the change in successive amplitudes around the
untilted baseline.

| Seed 1001, v11 | Hourly steps | 15-minute steps with hourly samples |
| --- | ---: | ---: |
| Held-split periods | 25 h | 22–24 h |
| Released periods | 25–26 h | 22–25 h |
| First released same-side damping estimate | −0.074 h⁻¹ | −0.098 h⁻¹ |
| Equilibrium xenon worth | −27.75 mk | −27.75 mk |

The finer run retains the oscillation and shows a shorter first cycle. Both
runs fall within the 15–30-hour range described in the CNSC training manual.
The game surrogate damps faster than the proceedings case. Reducing lateral
coupling could bring the damping closer, but the tested reduction raised peak
bundle power and was left out of this calibration.
[CNSC reactor physics, spatial oscillations](https://canteach.candu.org/Content%20Library/20030101.pdf)

The Korean Nuclear Society study reports a natural-uranium side-to-side damping
factor of −0.02635 h⁻¹. Its adjuster perturbation and regional power metric
differ from this zone-fill benchmark. The CANDU-600 aging study compares bulk
power conditions and device worth, so it informs interpretation of the zone
response rather than supplying a lateral damping target.
[KNS proceedings, Figure 4 and Table 1](https://www.kns.org/files/pre_paper/29/99S-035.PDF),
[CNS proceedings](https://proceedings.cns-snc.ca/index.php/pcns/article/view/3430),
and [Stan and Ferat, 2025](https://reference-global.com/article/10.2478/awutp-2025-0003)

The default time-based game solves at half-day boundaries. The finer
fixed-zone benchmark measures a different integration cadence from play.
The [hourly](../../benchmarks/xenon-v11/hourly.json) and
[15-minute](../../benchmarks/xenon-v11/15min.json) reports contain the recorded
samples and cycle estimates. See [benchmark commands](../../tools/AgedCoreBenchmark/README.md).
