# The spatial solver and half-day approximation

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

## Multiplication, power and boundaries

Keff describes how neutron production balances removal and leakage for the solved shape. A value of 1 is critical, below 1 is subcritical and above 1 is supercritical. Reactivity is (k − 1) / k, and 1 mk is 0.001 in that dimensionless quantity. The solver normalizes the resulting shape to the run's nominal thermal power. Multiplication and actual thermal output describe different quantities.

V10 extrapolates radial flux to zero on a concentric circle and axial flux to zero 0.22 m beyond each end face. Reflective faces have zero boundary conductance. See the [active solver equations](../../physics/active-two-group-solver.md) and [pack fit](../../../data/calibration/diffusion-v10/README.md).

## Time-based exposure

After the waiting fuel batch, the shared model uses the accepted post-fuelling shape and flux to integrate energy, burnup and poison over 43,200 seconds. The next equilibrium solve produces the endpoint. Iterations improve the endpoint solution. They do not sample power inside the interval. The browser blends the readings for playback.

The [shared step contract](../../gameplay/daily-turn-mode.md) explains adoption and failure. Retained daily and real-time comparison modes use separate integration cadences.
