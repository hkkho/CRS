# Axial zero incoming current, v8

The default browser pack is `candu6-two-group-diffusion-v1-axial-marshak-v8`.
Core applies the diffusion Marshak condition at non-reflective End A/B faces:
`phi + 2 D_g d(phi)/dn = 0`, equivalent to zero incoming partial current.

The explicit pack declaration is:

```json
"axial_boundary": {
  "condition_id": "zero-incoming-current-v1",
  "cell_length_m": 0.4953
}
```

For the node-centred mesh, `D_g = C_interior,g h²/V`, `A = V/h`, and
`C_end,g = D_g A/(h/2 + 2D_g)`. Fast/thermal end conductances are
0.009968327483595302 / 0.005677250999478533 m². This term enters the spatial
loss operator, not the material absorption table. Radial faces retain the v7
effective-reflector conductance. Reflective overrides remain exact zero leakage.
Absent declarations retain legacy fitted boundaries; invalid declarations fail
closed. Pack content and coefficient digests include the change.

## Finite-core refit

Stronger end leakage alone made the old fixed state subcritical. The reproducible
[fit](../../data/calibration/axial-marshak-v8/fit/fit.json) holds the Marshak
boundary, interior conductances, radial boundary, fuel absorption, fission heating,
burnup knots and included xenon reference fixed. It adjusts one common neutron
production multiplier and the authored device absorption strengths:

| Quantity | Verified value |
| --- | ---: |
| Common nu-fission multiplier relative to v7 | 1.0339620328061294 |
| Effective nu (previously 2.45) | 2.533206980375017 |
| Half-fill seed-1001 Keff | 1.0000002556464018 |
| Adjuster worth, frozen equilibrium Xe | 16.999970 mk |
| Empty/full LZC worth, frozen equilibrium Xe | 7.000017 mk |
| Thermal adjuster strength | 0.03462771818219497 m⁻¹ |
| Full-footprint fast/thermal water strength | 0.005934480366805991 / 0.05934480366805991 m⁻¹ |
| Initial maximum channel/bundle power | 5,907.982 / 771.425 kW thermal |
| Frozen-Xe fuel-only loss | 0.439384 mk/full-power day |

The production change preserves the relative burnup curve but increases its
absolute infinite-medium k values by 3.3962%; fresh k-infinity is now
1.1560181488927948. It is explicitly an authored finite-core normalization,
not a new transport-generated literature curve. Sigma_f and energy per fission
remain fixed. Exact checksums and provenance accompany the staged pack.

The [archived v7 source](../../data/calibration/axial-marshak-v8/source-pack.json)
and fit trials retain the before state. The channel scoring reference is
automatically recomputed from the shared model and has a new reference identity.
Prior pack saves are rejected by existing simulation-version checks.

## Playable verification

The [acceptance archive](../../benchmarks/axial-marshak-v8-2026-10-10/README.md)
contains final M11 absorption/power curves, the channel reference and campaigns.
M11's end bundles now produce approximately 17% of its peak with adjusters in.

The old fixed two-highest-burnup-channels/day policy ended on day 76 at the low
LZC limit. An automated **player** policy refuelling two channels, or three when
the published prior-boundary LZC is below 45%, completed 100 days at normal
power with 226 operations, 1,808 bundles, 2,304.752 points and final LZC 46.30%.
This policy does not change simulation rules or automatically choose channels
for a human player. The nominal 190-day aged-start convention is not a guarantee
that two channels/day balance every power shape.

Verification passed 130 Core, 81 Game, 45 Browser and 172 frontend/local database
tests. The exact wire fixtures were intentionally regenerated. Release AOT WASM,
frontend and `/CRS/` builds passed. Actual production browser checks passed daily
planning, two full days, survival/loss feedback, offline save/replay and 320px
layout. Twelve cold/warm reproduction rows matched deterministic digests with no
reported browser errors. These are local checks; no hosted deployment was made.

Initial test runs exposed superseded v7 golden values, a missing copied test
asset, and one test-host crash during concurrent compilation. The numerical
goldens were replaced by independently measured v8 values, the asset was
declared for portable output, and the clean final Game suite passed.

## Reproduction

```powershell
dotnet run --project tools/AgedCoreBenchmark -c Release -- --fit-axial-marshak data/calibration/axial-marshak-v8/source-pack.json tmp/axial-marshak-fit
dotnet run --project tools/AgedCoreBenchmark -c Release -- --channel-reference benchmarks/axial-marshak-v8-2026-10-10/channel-reference.json
```

Fitting writes an offline proposal. Before promotion, recompute each table's
SHA-256 over compact sorted-key JSON excluding `checksum`, and retain exact
pack bytes in both canonical and embedded files. `tools/Sync-PhysicsPacks.ps1
-Stage` synchronizes them. The archived promoted proposal includes these refreshed
checksums. Do not overwrite the immutable source with a new run.
