# Full-power game balance under channel and bundle limits

The former pack produced a seed-1001 peak of 13,501.737 kW/channel and
2,216.062 kW/bundle at 2,064 MW thermal. Enforcing 7,300 / 935 kW literally
therefore required a new spatial profile. The accepted pack is
`candu6-two-group-diffusion-v1-cycle190-650mwe-powerlimits-v2`.

## Authored fit

The offline experiment multiplies axial and transverse inter-node conductances
by four, reduces effective exterior conductances to one tenth, and rescales both
groups' neutron production by **1.0023328258680102**. The common production factor
restores seed-1001 criticality with half-filled zones; fission heating coefficients
and their burnup contrast remain unchanged. Conductances retain m² units; bundle
volume remains 0.05 m³. The original fuel cycle, inventory, poison model and zone
absorption slopes remain in use. Core solves actual powers from the resulting
two-group fluxes and normalizes their sum to 2,064 MW. There is no clipping,
derating, adjuster overlay, or frontend redistribution.

[CANDU Reactor Physics, radial flattening](https://www.nuceng.ca/canteach-vba/library/20030101.pdf)
describes the heavy-water radial reflector's flattening effect. This supports the
qualitative reduced-leakage direction, not the numerical factors selected here.
The axial and transverse coupling factors and effective boundaries are a
project-authored game surrogate, not reconstructed plant reflector geometry.

The tested six seeds at half-filled zones give:

| Seed | Peak channel, kW | Peak bundle, kW |
| --- | ---: | ---: |
| 1001 | 6505.284 | 664.213 |
| 1002 | 6406.918 | 652.125 |
| 1013 | 6557.067 | 665.655 |
| 1042 | 6496.301 | 661.044 |
| 1100 | 6674.698 | 680.593 |
| 2026 | 6571.301 | 667.569 |

Factory initialization additionally settles the usual bounded RRS passes.
Tests also exercise seeds 0 and UInt32.MaxValue, paused refuelling, the old-channel
pair at hour 14, time evolution, and an applied 120% target that trips the cap.
The fixed cycle-average reference ranges from 4,567.697 to 6,325.798 kW/channel;
its mean is 5,431.579 kW. Reference ratios still respond to fuel and poison.

## Reproduction

The archived pre-limit source and all four parameter trials are in
`data/calibration/power-limit-balance-2026-10-04`; `reference.json` records the accepted
reference and initial Game ripple measurements. Stronger coupling candidates
also satisfy starting caps but suppress spatial variation further; the fourfold
candidate preserves more visible ripple and refuelling response.

```powershell
dotnet run --project tools/ChannelReferenceBenchmark -- --fit-power tmp/power-balance data/calibration/power-limit-balance-2026-10-04/source-pack.json
dotnet run --project tools/ChannelReferenceBenchmark -- tmp/channel-reference
./tools/Test-DotNet.ps1 -Suite All
./tools/Test-Browser.ps1
```

The fit only emits proposals. Canonical and embedded runtime packs must be
identical. Earlier calibration reports retain their historical peaked-core
results; score comparisons should include the pack version and reference digest.
