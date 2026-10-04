# Liquid-zone regulating system

Core owns all fourteen fills, node membership, absorption overlays and feedback.
Game commits the inventory, solved flux and controller state together. Browser
views display the resulting snapshots, including the solved eigenvalue Keff.

## Traditional CANDU 6 regions

The layout follows Figure 2 in [St-Aubin and Marleau, 2018](https://publications.polymtl.ca/5143/2/2018_St-Aubin_Candu-6_reactivity_devices_optimization_advanced.pdf).
There are seven regions in each axial half and six vertical assemblies in total:
two compartments in each outer assembly and three in each central assembly.
The game's End A/End B convention uses the same transverse viewing direction
for both halves; it does not mirror the far end.

| Face region | End A zone | End B zone |
| --- | --- | --- |
| Lower left | 1 | 8 |
| Upper left | 2 | 9 |
| Lower centre | 3 | 10 |
| Centre | 4 | 11 |
| Upper centre | 5 | 12 |
| Lower right | 6 | 13 |
| Upper right | 7 | 14 |

Protocol logical IDs remain zero-based (0–13); visible labels are Z1–Z14.
Bundle positions 0–5 belong to End A and 6–11 to End B. The source drawing's
boundaries are discretized onto the game's 22-column lattice: columns 0–6 are
left, 7–14 centre, and 15–21 right. Display rows increase downwards. Outer
regions split at row 11; central regions split at rows 8 and 14. Every one of
the 4,560 nodes belongs to exactly one zone. This is a discretized regional
absorption model, rather than an explicit model of individual absorber tubes.

Region membership and absorber compartment ownership are independent Core
fields. The browser displays the default regions in Studio's liquid-zone view.
The default applies effective absorption at every node. The
[calibration](zone-calibration.md) sets empty-to-full worth to 6.5 mk, with
nonnegative absorption referenced to empty zones. The earlier
[geometry audit](zone-geometry-audit.md) records the superseded excessive worth.

## Feedback and limits

Each solve measures the fourteen fractions of total fission power. The initial
equilibrium shape supplies their references, preserving the intended radial
profile rather than imposing equal power on regions of different sizes.
These power-fraction references are independent of the time-average channel
targets used by scoring. The [nominal-flux audit](rrs-flux-audit.md) identifies
the remaining gap in regional flux balancing.
The controller treats net criticality and spatial shape as separate objectives.
Controller convergence allows one percentage point of absolute error in each
region's share of total power (fraction 0.01), alongside ±0.05 mk net reactivity.
Outside a 0.05-mk net-reactivity band, a common fill request removes excess
reactivity or supplies a deficit, with each compartment clipped at its bounds.
Inside that band, bounded least squares corrects `target fraction - measured
fraction`; a common offset cancels its estimated reactivity change.
Filling adds absorption; draining removes it. Common movement regulates
criticality and differential movement restores the reference shape, including
axial and transverse imbalance.

A measured diffusion response updates the estimated Jacobian using a Broyden
secant. A rejected first command can therefore be corrected instead of freezing
the fills. The measured model is retained for subsequent events. Each event
uses at most four full-core candidates: uncompensated, current-fill baseline,
first command, and correction. Outside the criticality band, actual absolute net reactivity must decrease.
Inside it, shape corrections must improve the combined residual while retaining
criticality. Fuel burnup never participates in this acceptance decision. Movement is bounded to eight percentage points per zone per event,
and fills stay in [0,1]. No failed candidate is committed. Solves within a
multi-boundary time advance warm-start from the preceding transaction-local
projection, preserving replay across wall-time partitions.

The [Essential CANDU I&C chapter, section 4.1](https://unene.ca/essentialcandu/pdf/10%20-%20IandC.pdf)
describes combined bulk-power and spatial feedback, light-water absorption,
and independent control of fourteen compartments. Actual plant control runs
on much shorter intervals. This game uses a quasi-static equilibrium model:
it recomputes after refuelling and every 1,800 simulated seconds.
Between solves it integrates burnup using the retained power shape. Browser 1x
is 1,800 simulated seconds per real second, with a 100 ms wall control tick;
10x and 60x multiply that base. Keff is the solver result and is never forced
to exactly one. The RRS drives it towards one while retaining finite residuals.

Absorption strengths, response estimates, movement limits and equilibrium
cadence are project-authored gameplay approximations. Tube geometry, valve
dynamics, detector delays, and additional plant reactivity devices are outside
this implementation. Live [iodine/xenon departures](iodine-xenon-gameplay.md)
are included in the controlled diffusion candidates.
