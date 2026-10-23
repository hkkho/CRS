# The spatial solver and daily approximation

CRS calculates neutron balance over 4,560 spatial nodes: 380 channels multiplied
by twelve bundle positions. Each node has fast and thermal flux, material
coefficients and explicit neighbor/boundary relationships. The numerical model
connects local fuel/device changes to a core-wide power distribution.

## Diffusion and finite volumes

The diffusion approximation describes the spatial neutron field through
production, removal, scattering and leakage. In the finite-volume representation,
each node has a volume, neighboring nodes exchange leakage contributions, and
exterior faces contribute an explicit boundary term. The active operator is
matrix-free: it evaluates these relationships through the node and edge records.

**Equation 3. Two-group balance and thermal fission power.** The notation below
summarizes the node balance and the sum of node powers. Spatial subscripts are
omitted for readability:

```text
L1(phi1) = chi1 * F / k
L2(phi2) = Sigma_s12 * phi1 + chi2 * F / k
F = nuSigma_f1 * phi1 + nuSigma_f2 * phi2
P = sum(volume * E_fission * (Sigma_f1 * phi1 + Sigma_f2 * phi2))
```

`phi1` and `phi2` are the fast and thermal fluxes. `L1` and `L2` include removal
and spatial leakage; fast removal also includes downscatter. `chi1` and `chi2`
partition fission neutrons between groups, and `k` is the effective multiplication
eigenvalue. `Sigma_s12` supplies thermal neutrons by downscatter. `F` is neutron
production from the neutron-yield-weighted coefficients `nuSigma_f1/2`.

In the last line, `Sigma_f1/2` turn flux into fission rate per volume. Multiplying
by node volume and `E_fission`, the energy per fission, gives node power in watts.
`sum(...)` adds those powers into total thermal power `P`. These are balance and
normalization relationships, not executable code or a transient time-step solver.

## Iteration and acceptance

An outer eigenvalue iteration updates the fission source, solves each energy
group, revises `k` and normalizes power. Inner linear solves use deterministic
Jacobi corrections. A warm start reuses a compatible previous flux field as an
initial guess, reducing repeated work without changing the requested equations.

The solve checks eigenvalue change, equation residual, source-shape change and
power balance. Convergence requires the combined policy to pass. The session
adopts a candidate only after its surrounding transaction is valid; numerical
work on a detached candidate does not partially replace the live reactor.

## Axial boundaries

The active v8 model uses zero incoming partial neutron current at nonreflective
axial ends. Its Marshak conductance permits outward leakage and produces lower
end-bundle power. This is separate from the authored radial leakage fit. A
reflective face instead has zero boundary conductance. Material absorption and
boundary leakage remain distinct terms in the neutron balance.

The [v8 boundary notes](../../physics/axial-marshak-v8.md) explain the refit, and
the [active solver equations](../../physics/active-two-group-solver.md) specify the
operator, numerical tolerances and analytic one-cell check.

## One large daily step

The public game leaves time frozen during planning. On commitment it evaluates
each fuel move, then uses the surviving post-refuelling shape/flux for one
86,400-second energy and analytic poison interval. A final equilibrium/RRS solve
produces the next displayed decision.

Many inner and outer numerical iterations may be needed for that final solve.
They refine a boundary solution; they are not observations at intermediate times
through the day. The game therefore does not claim to detect an intraday peak
between the states it calculates. Retained comparison tools can use a different
three-minute integration cadence, which is identified separately from the public
`practice-daily-one-step-v1` model.

The [daily contract](../../gameplay/daily-turn-mode.md) defines the transaction and
failure semantics, and the [implementation walkthrough](../03-daily-turn.md)
connects them to the relevant C# operations.
