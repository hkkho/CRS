# Independent fuel burnup and zone regulation

The cycle190 refuelling plot exposed a controller defect: after sixteen fresh
bundles, zone fills could remain fixed from hour 14 to about hour 26 while
burnup consumed positive net reactivity. The single combined least-squares
objective rejected corrections that traded spatial shape error for criticality.
Burnup was independent in Core already, but controller acceptance made it look
like fuel decay was being used as the regulation mechanism.

The shared Core controller now prioritizes measured net criticality:

- Outside a 0.01-mk band, solve a common fill request, clipped independently
  at each compartment's physical and eight-percentage-point event bounds.
  Filling supplies negative absorption reactivity; draining removes it.
- Inside the band, solve the bounded shape objective and add a common offset
  to cancel its estimated reactivity contribution. Accept a shape correction
  only when measured net reactivity remains in the criticality band.
- Every proposed correction is checked by the actual diffusion solver. Outside
  the band, accept only a decrease in absolute net reactivity; inside it,
  require a lower combined residual. A measured secant response refines the
  next correction. The event still uses at most four candidates.

No fuel energy, inventory age, simulation time or burnup coefficients change
while regulating a refuelling event. Subsequent burnup integrates thermal
power as before, with the retained accepted spatial solution. No Keff value
is forced to one. This remains a half-hour/event equilibrium game model,
without valve transients or plant-controller fidelity.

Controller identity is `synthetic-practice-liquid-zone-criticality-first-rrs-v2`;
response identity is `synthetic-practice-liquid-zone-response-common-shape-v3`.
The existing fuel pack, 2064 MW thermal rating and 6.5-mk zone worth are retained.

The replay in `artifacts/rrs-independent-refuel-response-2026-09-30` uses seed
1001, two eight-bundle old-channel operations at hour 14, and a matched control
through 72 hours. It contains seven zone-pair subplots and a reactivity/mean-fill
plot. A regression test checks immediate fill increase, bounded net reactivity,
unchanged energy of retained bundles at the refuelling timestamp, and continued
fuel decay plus zone drain at the next half-hour boundary.

## Reproduced response

At hour 14, mean fill increases from 44.1717% to 48.6993% after channel 75,
then 50.8627% after channel 324. At hour 14.5 it is already draining (49.9806%),
while empty-zone fuel reactivity also decreases. Maximum absolute post-refuel
net reactivity over all half-hour/event samples is 0.007897 mk. At hour 72,
mean fill is 24.9104% in the fuelled run and 18.4994% in the control.

The fresh seed-1001 no-refuel benchmark gives a tight-endpoint mean fuel loss
of 0.683639 mk/day over three days. Zone worth and the coefficient pack are
unchanged; the small decay-rate difference reflects the corrected spatial
solution used for burnup integration. The reactivity plot explicitly shows
zone contribution as net rho minus empty-zone rho for each live inventory.
