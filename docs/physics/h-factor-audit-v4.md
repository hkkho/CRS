# Active v4 H-factor audit — 2026-10-07

The active game uses fission cross sections, not an imported H-factor table. The equivalent bundle-node factors below follow exactly from the runtime power expression.

`P_bundle[W] = V * E_f * (Sigma_f1 * phi1_SI + Sigma_f2 * phi2_SI)`

`P_bundle[kW] = H1 * phi1_cm + H2 * phi2_cm`

`H_g = 10 * V * E_f * Sigma_fg` when flux is in n/cm²/s and power in kW. The factor 10 is 10,000 for flux area conversion divided by 1,000 for W-to-kW. Cell volume is 0.04044276185625 m³ and energy per fission is 200 MeV.

## Values

All table H entries multiply **10^-12 kW/(n cm^-2 s^-1)**. Group 1 is fast; group 2 is thermal. Values interpolate linearly between exact burnup knots. The domain ends at 30 MWd/kg HM.

| Burnup, MWd/kg HM | H1 | H2 |
| ---: | ---: | ---: |
| 0.000000 | 0.453575 | 2.002706 |
| 0.159857 | 0.453575 | 1.907396 |
| 0.319713 | 0.453575 | 1.907396 |
| 0.639426 | 0.453575 | 1.915859 |
| 0.959139 | 0.453575 | 1.920091 |
| 1.278852 | 0.453575 | 1.920091 |
| 1.598565 | 0.453575 | 1.915859 |
| 2.237991 | 0.453575 | 1.898933 |
| 2.877417 | 0.453575 | 1.882006 |
| 3.516843 | 0.453575 | 1.860848 |
| 4.156269 | 0.453575 | 1.839690 |
| 4.795695 | 0.453575 | 1.816416 |
| 6.394260 | 0.453575 | 1.759290 |
| 7.992825 | 0.453575 | 1.706395 |
| 9.591390 | 0.453575 | 1.655615 |
| 9.784587 | 0.453575 | 1.648608 |
| 11.741505 | 0.453575 | 1.578989 |
| 15.000000 | 0.453575 | 1.468369 |
| 20.000000 | 0.453575 | 1.310758 |
| 25.000000 | 0.453575 | 1.166711 |
| 30.000000 | 0.453575 | 1.035063 |

The six knots after 9.59139 MWd/kg are an authored tail, not a validated depletion dataset.

## Online comparison

The [2011 Wolsong RFSP-IST paper](https://proceedings.cns-snc.ca/index.php/pcns/article/download/6456/6455/6491), section 3, gives the same two-group bundle-power formula, the same 28.575 × 28.575 × 49.53 cm cell dimensions and channel power as the sum of twelve bundle powers. It uses cell-average group flux and lattice-code H factors.

[Rouben’s AECL reactor-physics notes](https://canteach.candu.org/Content%20Library/20040501.pdf), printed page 33, identify H factors as additional lattice properties that depend on burnup and operating conditions.

[Naceur and Marleau (2019)](https://publications.polymtl.ca/5048/11/2019_Naceur_Candu-6_operation_simulations_using_accident.pdf), section 3.3, uses recoverable-energy H factors and normalizes bundle powers to 2,061.4 MW thermal. Section 3.4 gives the fission-cross-section power relation with about 200 MeV per fission.

These sources support the units, formula and normalization approach. No matched public burnup-by-burnup H-factor table was found to independently validate the active coefficients. A k-infinity fit does not uniquely determine group fission cross sections or heating coefficients. The active pack uses an authored effective neutron yield of 2.45 and 200 MeV/fission; it does not separately model recoverable capture/gamma heating.

The separate four-row `CanduTwoGroupLatticeReferenceV1.TryCreateUserSupplied` fixture is not consumed by the game. Its H1 values are 1.25, 1.24, 1.23 and 1.22 × 10^-12; H2 values are 21.1, 22.6, 19.7 and 17.6 × 10^-12 at irradiation 0, 0.3, 1.0 and 1.8 n/kb. They are explicitly unverified and cannot be relabelled as burnup or treated as an online validated reference.

## Why the power peaks may look low

Game normalizes the flux solution to 2,064 MW thermal. Therefore average channel power is 5.431579 MW and average bundle power is 452.631579 kW. A uniform rescaling of the H factors would not raise that normalized total; it would primarily change the flux normalization at a fixed material/poison state. Relative factors across groups and burnups can change the power shape, and absolute flux can affect subsequent poison evolution.

The 100-day regional and reserve runs observed maximum bundle powers of 723.952 and 736.222 kW. These are lower peaks, not lower total core output. The 935-kW bundle and 7,300-kW channel settings are upper limits, not required operating peaks.

Compared with the archived v3 pack, v4 changes transverse fast conductance from 0.0048 to 0.019812 m² (4.1275×), axial fast conductance from 0.0032 to 0.00659423076923077 m² (2.0607×), and boundary conductance and fuel coefficients as well. Stronger interior coupling is a candidate explanation for smoother power distributions, but these changes are coupled and no isolation experiment has established causality. Validate the normalized spatial shape and burnup-dependent group heating together before changing coefficients.

No runtime changes were made for this audit. Exact calculated values, pack hash and source links are retained in [the audit JSON](../../benchmarks/h-factor-audit-v4.json).
