# Active two-group diffusion solver

This document describes the equations used by the active `ReactorSim.Core`
spatial path. The browser and the retained Unity client consume the result of
this Core solver; neither client implements a second physics calculation.

The coefficients in the active pack are project-authored,
synthetic-calibrated surrogate data. They are useful for a deterministic
practice model and solver checks. They are not validated CANDU cross sections,
a plant prediction, or a reproduced DRAGON5/DONJON5 case. The runtime does not
require either external program.

## Pack and fresh material row

The embedded pack has data-pack version
`candu6-two-group-diffusion-v1-infinite-cell-calibrated`, units profile
`SI-v1`, energy-group order `fast, thermal`, model ID
`candu6-two-group-full-core-diffusion-v1`, and solver ID
`spatial-eigen-jacobi-v1`. The `NAT-U-SYNTHETIC` table is queried at the first
row, whose burnup is exactly `0 J/kg_HM`.

The fresh row contains the following material values. Group 1 is fast and
group 2 is thermal.

| Quantity | Value | Units | Role |
| --- | ---: | --- | --- |
| `Sigma_a1` | `0.30` | m^-1 | Fast absorption |
| `Sigma_a2` | `0.16` | m^-1 | Thermal absorption |
| `Sigma_f1` | `0.035` | m^-1 | Fast fission rate for power |
| `Sigma_f2` | `0.155` | m^-1 | Thermal fission rate for power |
| `nuSigma_f1` | `0.0840` | m^-1 | Fast neutron production |
| `nuSigma_f2` | `0.37665` | m^-1 | Thermal neutron production |
| `Sigma_s12` | `0.200` | m^-1 | Fast-to-thermal downscatter |
| `chi1` | `1.0` | dimensionless | Fast fission spectrum fraction |
| `chi2` | `0.0` | dimensionless | Derived as `1 - chi1` |
| `E_fission` | `3.204353268e-11` | J/fission | Power conversion |

The pack node volume is `V = 0.05 m^3`. The pack also carries the finite-volume
conductances used by the 380-channel by 12-position model:

| Face relation | Group 1 | Group 2 | Units |
| --- | ---: | ---: | --- |
| Axial interior edge | `0.0008` | `0.0004` | m^2 |
| Transverse interior edge | `0.0012` | `0.0006` | m^2 |
| Vacuum boundary face | `0.0024` | `0.0012` | m^2 |

Reflective faces have zero boundary conductance. The one-cell fixture used by
the Lab creates one node, six explicit reflective faces (`North`, `East`,
`South`, `West`, `EndA`, `EndB`), no neighbors, and six zero conductance
records. The six records are still retained in the topology and stencil, so a
missing face cannot silently become an implicit boundary condition.

## Finite-volume operator

For node `i`, group `g`, flux `phi_g,i`, volume `V_i`, interior neighbors
`j`, and explicit boundary faces `b`, Core applies the matrix-free operator

```text
L_1(phi_1)_i = (Sigma_a1_i + Sigma_s12_i) * phi_1,i
  + (1 / V_i) * [ sum_j C_1,ij * (phi_1,i - phi_1,j)
                 + sum_b C_1,ib * phi_1,i ]

L_2(phi_2)_i = Sigma_a2_i * phi_2,i
  + (1 / V_i) * [ sum_j C_2,ij * (phi_2,i - phi_2,j)
                 + sum_b C_2,ib * phi_2,i ]
```

`C` is a two-group conductance in m^2. The interior term is the reciprocal
edge current contribution. A vacuum boundary contributes its conductance times
the node flux. A reflective boundary contributes zero. Every removal and
leakage term has the same volumetric source units after division by volume.

The fission production source is

```text
F_i = nuSigma_f1_i * phi_1,i + nuSigma_f2_i * phi_2,i
```

The two-group eigenproblem solved by Core is

```text
L_1(phi_1)_i = chi1_i * F_i / k

L_2(phi_2)_i = Sigma_s12_i * phi_1,i + chi2_i * F_i / k
```

There is no upscatter term in this active model. `Sigma_f` is used for the
fission power rate, while `nuSigma_f` is used for neutron production and the
eigenvalue source. The two fields are deliberately kept separate.

## Inner solve and outer eigen iteration

For each group and node, Core forms the positive Jacobi diagonal

```text
D_1,i = Sigma_a1_i + Sigma_s12_i
        + (sum_j C_1,ij + sum_b C_1,ib) / V_i

D_2,i = Sigma_a2_i
        + (sum_j C_2,ij + sum_b C_2,ib) / V_i
```

It then performs the deterministic Jacobi correction

```text
phi_g,i^(m+1) = phi_g,i^m
  + [ S_g,i - L_g(phi_g^m)_i ] / D_g,i
```

where `S_1 = chi1 * F / k` and
`S_2 = Sigma_s12 * phi_1 + chi2 * F / k`. The inner iteration accepts when
the infinity norm of `L(phi) - S` is below the absolute tolerance or when the
relative value is below the relative tolerance. The relative denominator is
the maximum over rows of `abs(L(phi)) + abs(S)`.

The outer `SpatialEigenSolve` starts with a positive eigenvalue and a unit
flux seed when the caller does not supply a warm start. Each outer step:

1. Computes `F` from the current normalized flux.
2. Solves group 1 with the fission source divided by the current `k`.
3. Builds the group 2 source from downscatter and the fission spectrum.
4. Solves group 2.
5. Computes volume-weighted neutron production for the trial and current
   states and updates

   ```text
   k_new = k_old * (sum_i V_i * F_trial_i) /
                    (sum_i V_i * F_current_i)
   ```

6. Computes the trial fission power and scales both trial flux groups by

   ```text
   alpha = target_power_w / P_trial
   phi_new = alpha * phi_trial
   ```

   with

   ```text
   P = sum_i V_i * E_fission_i
                    * (Sigma_f1_i * phi_1,i + Sigma_f2_i * phi_2,i)
   ```

7. Re-evaluates the normalized state and records the eigenvalue change,
   source-shape change, eigen-equation residual, and power-balance error.

The outer result is converged only when the eigenvalue absolute or relative
criterion is satisfied and all of residual, source-shape, and power-balance
criteria are satisfied. A nonconverged result has no usable final state.

The embedded pack stores the normal runtime policy: Jacobi absolute residual
`1e-10`, relative residual `1e-6`, 64 inner iterations, outer `k` tolerances
`1e-4` and `1e-3`, residual tolerance `2e-3`, source-shape tolerance `1e-3`,
power-balance tolerance `1e-12`, and 300 outer iterations. The fixed
single-cell benchmark uses the same solver implementation with tighter
acceptance settings: `1e-13` inner absolute and relative residual tolerances,
`1e-12` for each outer metric, 2048 inner iterations, and 256 outer
iterations.

## Independent one-cell check

For the fresh row and six reflective faces, every leakage term is zero. The
operator therefore reduces to

```text
L_1 = (0.30 + 0.20) * phi_1 = 0.50 * phi_1
L_2 = 0.16 * phi_2
```

Because `chi1 = 1` and `chi2 = 0`, the thermal equation gives

```text
phi_2 / phi_1 = Sigma_s12 / Sigma_a2
                = 0.200 / 0.16
                = 1.25
```

Substitution into the fast equation gives the infinite-medium eigenvalue

```text
k_inf = (nuSigma_f1 + nuSigma_f2 * (phi_2 / phi_1)) /
        (Sigma_a1 + Sigma_s12)
      = (0.0840 + 0.37665 * 1.25) / 0.50
      = 1.109625
```

The one-watt normalization is computed from the actual `Sigma_f`, energy, and
volume values:

```text
1 W = 0.05 m^3 * 3.204353268e-11 J/fission
     * (0.035 * phi_1 + 0.155 * phi_2)
```

The resulting fluxes are
`phi_1 = 2.72852855714132e12 n/m^2/s` and
`phi_2 = 3.41066069642665e12 n/m^2/s`. These values are an analytic check on
the assembled one-cell equations. The benchmark result below comes from the
actual Core `SpatialEigenIteration` and `SpatialEigenSolve`, not from this
analytic expression.

## Actual Core result

`SingleCellReflectiveDiffusionFixtureV1.TrySolve()` returns:

| Result | Value |
| --- | ---: |
| Status | `Converged` |
| Effective `k` | `1.109625` |
| Fast flux | `2.72852855714132e12 n/m^2/s` |
| Thermal flux | `3.41066069642665e12 n/m^2/s` |
| Total power | `1 W` |
| Outer iterations | `2` |
| Relative residual infinity | `8.94770275946045e-17` |
| Relative power balance | `1.11022302462516e-16` |

The browser's Lab `singleCell` readout is populated from this Core result. It
shows the pack version and evidence class, material and fresh burnup, node
volume, all six reflective faces, the fresh-row coefficients, both fluxes,
`k`, total power, fission production, iteration count, residual, and balance.
The readout is labelled `AUTHORITATIVE CORE SOLVER`; if the Core fixture or
solve is unavailable, the browser reports that state instead of inventing a
placeholder result. The existing `2 x 8` Lab controls remain available for
topology and material experiments, while this fixed one-cell result provides
the direct reflective-boundary benchmark.

## Interpretation boundary

The project-authored pack is a deterministic surrogate selected for gameplay,
debugging, and solver acceptance. Its metadata, cross sections, conductances,
and the one-cell result do not establish agreement with measured CANDU data,
an operating reactor, a safety limit, or a full-core engineering analysis.
