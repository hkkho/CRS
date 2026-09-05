# Technical Review: Improved Quasi-Static (IQS) Neutronic Solver for CANDU 6

## 1. Executive Summary

This document provides a detailed technical review of the **Improved Quasi-Static (IQS) Neutronic Solver** implemented in [`IqsFullCoreSolver.cs`](file:///c:/Users/infin/candu/src/ReactorSim.Core/Domain/IqsFullCoreSolver.cs), its accompanying data pack [`candu6-two-group-iqs-pack-v1.json`](file:///c:/Users/infin/candu/src/ReactorSim.Core/EmbeddedData/candu6-two-group-iqs-pack-v1.json), and its integration within [`GameSession.cs`](file:///c:/Users/infin/candu/src/ReactorSim.Game/GameSession.cs).

The solver was developed to provide time-dependent neutron kinetics and spatial power shape tracking for a steady-state CANDU 6 on-power refuelling simulation game. From a **software architecture and contract-engineering perspective**, the implementation is exemplary: deterministic, engine-neutral, fail-closed, memory-safe, and cleanly decoupled between simulation state transitions, game orchestration, and presentation layers.

However, from a **reactor physics and numerical methods perspective**, there is a significant divergence between the formal textbook definition of the **Improved Quasi-Static (IQS)** method and the current implementation, as well as several physical gaps regarding the unique neutronic behavior of a **CANDU 6** core. Specifically:
1. **Methodological Classification**: The solver implements an **Adiabatic Approximation** (static $k$-eigenvalue shape recomputation combined with a point-kinetics amplitude advance), rather than a true **Improved Quasi-Static (IQS)** method (which requires solving a time-dependent fixed-source shape equation with delayed neutron precursors and dynamic frequency terms).
2. **Adjoint Weighting**: The shape normalization constraint uses a uniform flat adjoint ($W(\vec{r}) = 1.0$), rather than the physical adjoint flux ($\Phi^*(\vec{r})$) of a CANDU 6 core.
3. **Reactivity Definition**: Dynamic reactivity is computed from the static eigenvalue delta $\Delta(1/k)$, bypassing bilinear adjoint weighting of local cross-section perturbations.
4. **Time Integration Scales**: Micro-steps of $600\text{ s}$ (10 minutes) and shape intervals of $3600\text{ s}$ (1 hour) bypass delayed neutron transient dynamics, effectively reducing the kinetics to an algebraic prompt-jump / precursor-equilibrium solver.
5. **CANDU 6 Physics Omissions**: Key CANDU 6 characteristics—such as prompt neutron generation time ($\Lambda \approx 0.9\text{ ms}$ vs. pack $0.1\text{ ms}$), heavy-water photoneutron groups, automatic Reactor Regulating System (RRS) / Liquid Zone Controller (LZC) reactivity compensation, and spatial Xenon-135 feedback—are either approximated with non-CANDU parameters or decoupled from the runtime loop.

This review details what works, what doesn't work, why these discrepancies exist, and provides concrete, constructive recommendations prioritized for the project's steady-state refuelling gameplay scope.

---

## 2. Theoretical Foundations: True IQS vs. Current Code

### 2.1 The Classical Improved Quasi-Static (IQS) Formulation

In space-time reactor kinetics (Ott & Meneley 1969; Stacey 2007; Dulla & Ravetto 2008), the time-dependent multigroup diffusion equation is written as:

$$\frac{1}{v_g} \frac{\partial \phi_g}{\partial t} = \nabla \cdot (D_g \nabla \phi_g) - \Sigma_{r,g} \phi_g + \sum_{g' \ne g} \Sigma_{s,g' \to g} \phi_{g'} + (1 - \beta) \chi_{p,g} F(\vec{r}, t) + \sum_{j=1}^m \lambda_j \chi_{d,j,g} C_j(\vec{r}, t)$$

$$\frac{\partial C_j}{\partial t} = \beta_j F(\vec{r}, t) - \lambda_j C_j(\vec{r}, t)$$

where $F(\vec{r}, t) = \sum_g \nu\Sigma_{f,g}(\vec{r}, t) \phi_g(\vec{r}, t)$.

The **Quasi-Static Factorization** splits the space-energy-time flux into a scalar amplitude $P(t)$ and a spatial shape function $\psi_g(\vec{r}, t)$:

$$\phi_g(\vec{r}, t) = P(t) \psi_g(\vec{r}, t)$$

To ensure uniqueness of the factorization, a **normalization constraint** is imposed using a fixed, time-independent weight function $W_g(\vec{r})$ (conventionally chosen as the initial adjoint flux $\phi_g^*(\vec{r}, 0)$):

$$\gamma(t) = \sum_g \int_V W_g(\vec{r}) \frac{1}{v_g} \psi_g(\vec{r}, t) \, dV = \gamma_0 = \text{constant}$$

Taking $\frac{d\gamma}{dt} = 0$, the **Exact Point Kinetics Equations (EPKE)** for the amplitude $P(t)$ are derived without approximation:

$$\frac{dP}{dt} = \left( \frac{\rho(t) - \beta_{\text{eff}}(t)}{\Lambda(t)} \right) P(t) + \sum_{j=1}^m \lambda_j c_j(t)$$

$$\frac{dc_j}{dt} = \frac{\beta_{j,\text{eff}}(t)}{\Lambda(t)} P(t) - \lambda_j c_j(t)$$

where $\rho(t)$, $\beta_{\text{eff}}(t)$, and $\Lambda(t)$ are defined via bilinear adjoint integrals:
- **Dynamic Reactivity**:
  $$\rho(t) = \frac{1}{F_{\text{adj}}(t)} \sum_g \int_V W_g \left[ -A_g \psi + F_g \psi \right] dV$$
- **Effective Delayed Fraction**:
  $$\beta_{j,\text{eff}}(t) = \frac{1}{F_{\text{adj}}(t)} \sum_g \int_V W_g \chi_{d,j,g} \beta_j F(\vec{r}, t) \psi dV$$
- **Prompt Generation Time**:
  $$\Lambda(t) = \frac{1}{F_{\text{adj}}(t)} \sum_g \int_V W_g \frac{1}{v_g} \psi_g dV$$

#### The True IQS Shape Equation
Substituting $\phi = P \psi$ into the multigroup diffusion equation and dividing by $P(t)$ yields the **shape equation**:

$$\frac{1}{v_g} \frac{\partial \psi_g}{\partial t} + \frac{1}{v_g} \left( \frac{1}{P}\frac{dP}{dt} \right) \psi_g = -A_g \psi_g + (1 - \beta) \chi_{p,g} F(\psi) + \frac{1}{P(t)} \sum_{j=1}^m \lambda_j \chi_{d,j,g} C_j(\vec{r}, t)$$

Discretizing the time derivative $\frac{\partial \psi_g}{\partial t} \approx \frac{\psi_g(t) - \psi_g(t - \Delta t)}{\Delta t}$, this becomes an **inhomogeneous (fixed-source) linear system** at each macro-step $\Delta t$:
- The matrix operator on the left includes leakage, removal, prompt fission, and the **dynamic frequency term** $\frac{1}{v_g} \left( \frac{\dot{P}}{P} + \frac{1}{\Delta t} \right) \psi_g$.
- The right-hand side is a **fixed source** composed of the spatial precursor decay $\frac{1}{P(t)} \sum \lambda_j C_j(\vec{r}, t)$ and the backward shape term $\frac{1}{v_g \Delta t} \psi_g(t - \Delta t)$.

---

### 2.2 What `IqsFullCoreSolver.cs` Implements

Inspecting [`IqsFullCoreSolver.cs`](file:///c:/Users/infin/candu/src/ReactorSim.Core/Domain/IqsFullCoreSolver.cs):

1. **Shape Equation (`TrySolveCandidate`)**:
   ```csharp
   ContractValidationResult<FullCoreDiffusionSolveResultV1> spatial =
       _spatialModel.TrySolve(
           bundles,
           _targetPowerWatts,
           _current.SpatialSolve.EffectiveK,
           _current.SpatialSolve.Group1Flux,
           _current.SpatialSolve.Group2Flux);
   ```
   `_spatialModel.TrySolve` solves the **static $k$-eigenvalue problem**:
   $$A_1 \phi_1 = \frac{1}{k} \chi_1 F(\phi)$$
   $$A_2 \phi_2 = \Sigma_{s,1\to 2} \phi_1 + \frac{1}{k} \chi_2 F(\phi)$$
   *Analysis*: It does **not** solve the IQS fixed-source shape equation. There is no $\frac{\dot{P}}{P}$ frequency term, no $\frac{\partial \psi}{\partial t}$ time-difference term, and no delayed neutron precursor source distribution $\frac{1}{P} \sum \lambda_j C_j(\vec{r})$. It solves for the static fundamental eigenmode of the perturbed state.

2. **Shape Normalization Constraint (`ComputeConstraint` and `BuildCandidate`)**:
   ```csharp
   private double ComputeConstraint(IReadOnlyList<double> group1, IReadOnlyList<double> group2)
   {
       double inverseVelocity1 = 1.0 / _dataPack.GroupVelocitiesMPerSecond[0];
       double inverseVelocity2 = 1.0 / _dataPack.GroupVelocitiesMPerSecond[1];
       double nodeVolume = _spatialModel.DataPack.NodeVolumeM3;
       double constraint = 0.0;
       for (int index = 0; index < group1.Count; index++)
       {
           constraint += (inverseVelocity1 * group1[index] + inverseVelocity2 * group2[index]) * nodeVolume;
       }
       return constraint;
   }
   ```
   *Analysis*: The weight function $W_g(\vec{r})$ is taken as $1.0$ everywhere (a uniform/flat adjoint). The factor $f = \frac{\gamma_0}{\gamma_{\text{raw}}}$ is computed to scale the static shape so that $\sum_i V_i (v_1^{-1} \psi_{1,i} + v_2^{-1} \psi_{2,i}) = \text{constant}$.

3. **Reactivity Formulation (`BuildCandidate`)**:
   ```csharp
   RelativeReactivity = spatial.Reactivity - _referenceReactivity;
   // where spatial.Reactivity = (k - 1) / k
   ```
   *Analysis*: Reactivity is calculated purely from the difference between the static $k_{\text{eff}}$ of the new inventory and the reference state $k_0$. It does not use the bilinear adjoint perturbation integral.

4. **Point Kinetics Integration (`TryAdvancePointKinetics`)**:
   ```csharp
   for (int group = 0; group < nextPrecursors.Length; group++)
   {
       double lambda = _dataPack.DecayConstantsPerSecond[group];
       double decay = Math.Exp(-lambda * dtSeconds);
       nextPrecursors[group] = _precursors[group] * decay +
           (_dataPack.BetaGroups[group] / _dataPack.GenerationTimeSeconds) *
           _amplitude * (1.0 - decay) / lambda;
       precursorSource += lambda * nextPrecursors[group];
   }

   double denominator = 1.0 -
       dtSeconds * (RelativeReactivity - _dataPack.BetaTotal) /
       _dataPack.GenerationTimeSeconds;
   double nextAmplitude = (_amplitude + dtSeconds * precursorSource) / denominator;
   ```
   *Analysis*: This is a semi-implicit Euler scheme with exact analytical precursor integration under a piecewise-constant amplitude assumption.

---

## 3. What Works Well

### 3.1 Software Engineering and Architectural Integrity
- **Engine-Neutral and Portable**: Implemented in clean, idiomatic C# (.NET 10 / netstandard2.1) without native libraries, third-party solvers, or runtime dependencies on DONJON5/DRAGON5.
- **Fail-Closed Contract Design**: All methods return `ContractValidationResult<T>`. Negative fluxes, non-finite values, nonpositive denominators, mismatched array lengths, or invalid JSON schemas fail cleanly with descriptive diagnostic codes rather than silently propagating `NaN` or throwing unhandled exceptions.
- **Candidate Immutability & Atomicity**: `IqsSpatialCandidateV1` is an immutable snapshot. Solving a candidate via `TrySolveCandidate` does not mutate the running solver state. This makes refuelling previews (`PreviewRefuelChannel`) and rollbacks upon candidate solve failure completely atomic and safe.
- **Strict IEEE-754 Determinism**: Calculations use deterministic loop orderings and canonical 4,560-node indexing ($380\text{ channels} \times 12\text{ bundles}$), ensuring identical replays across Windows, Linux, and browser WASM environments.
- **Separation of Concerns**: Presentation models (`GameCorePresentationSnapshot`), game logic (`GameSession`), and physics contracts (`IqsFullCoreSolver`) maintain strict boundaries. Presentation logic does not drive physics steps.

### 3.2 Numerical Robustness of the Point Kinetics Integrator
- **Unconditional Positivity under Subcriticality**: For normal subcritical or near-critical states ($\rho < \beta$), the denominator $1 - \Delta t \frac{\rho - \beta}{\Lambda} > 0$, ensuring positive amplitudes and avoiding numerical oscillations.
- **Equilibrium Preservation**: In steady state ($\rho_{\text{rel}} = 0$, $a = 1.0$), the precursor concentrations $C_j = \frac{\beta_j a}{\Lambda \lambda_j}$ remain invariant to machine precision under `TryAdvancePointKinetics`.
- **Directionally Correct Response**:
  - Adding fresh bundles (positive reactivity) produces $\rho_{\text{rel}} > 0$ and raises amplitude.
  - Core depletion/burnup (negative reactivity) produces $\rho_{\text{rel}} < 0$ and decreases amplitude.

---

## 4. What Doesn't Work (Deficiencies & Physics Gaps in CANDU 6 Simulation)

### 4.1 Methodological Gap: Adiabatic Approximation vs. True IQS
The current solver is technically an **Adiabatic Approximation with Point Kinetics**, not an **Improved Quasi-Static (IQS)** method:
- **Missing Delayed Neutron Shape Inertia**: In a true IQS solver, when a local perturbation occurs (e.g., refuelling channel L12), delayed neutron precursors emitted from the unperturbed distribution hold back the instantaneous flux tilt. The shape does not immediately snap to the perturbed critical eigenmode.
- **Static vs. Dynamic Eigenmode**: By re-solving $[M - \frac{1}{k}F]\psi = 0$, the code artificially forces the flux shape to the fundamental spatial mode of a fictitiously critical core. In a prompt or delayed transient, the actual shape satisfies an inhomogeneous equation where the prompt fission and delayed sources have distinct spatial distributions.

### 4.2 Inadequate Adjoint Weighting ($W = 1$ vs. Physical CANDU Adjoint)
- **CANDU 6 Adjoint Importance**: A CANDU 6 core has a diameter of $\approx 7.6\text{ m}$ and length of $\approx 6.0\text{ m}$. The flux and adjoint flux $\Phi^*(\vec{r})$ are peaked near the center and fall to zero at the radial and axial extrapolation boundaries.
- **Consequence of Flat Adjoint**: The constraint $\sum_i V_i (v_1^{-1}\psi_{1,i} + v_2^{-1}\psi_{2,i}) = \text{const}$ treats a neutron at the low-importance outer reflector boundary identically to a neutron in the high-importance central core. When refuelling occurs in a peripheral channel versus a central channel, the flat adjoint normalization introduces an unphysical distortion into the amplitude $P(t)$.

### 4.3 Inconsistent Time Integration Cadence
In [`candu6-two-group-iqs-pack-v1.json`](file:///c:/Users/infin/candu/src/ReactorSim.Core/EmbeddedData/candu6-two-group-iqs-pack-v1.json):
```json
"maximum_micro_step_seconds": 600.0,
"shape_recompute_interval_seconds": 3600.0
```
- **Precursor Dynamics Smeared Out**: Precursor group decay constants range from $\lambda_6 \approx 3.01\text{ s}^{-1}$ ($T_{1/2} \approx 0.23\text{ s}$) to $\lambda_1 \approx 0.0124\text{ s}^{-1}$ ($T_{1/2} \approx 56\text{ s}$).
- With $\Delta t = 600\text{ s}$ (10 minutes), $e^{-\lambda_j \Delta t} \approx 0$ for almost all groups. The precursor equations lose all differential transient information and instantly collapse into their asymptotic equilibrium values:
  $$C_j \approx \frac{\beta_j}{\Lambda \lambda_j} a$$
- Consequently, taking 10-minute micro-steps in point kinetics behaves as an algebraic steady-state approximation rather than a dynamic transient simulation.

### 4.4 Inaccurate CANDU 6 Nuclear Kinetics Parameters
1. **Prompt Neutron Generation Time ($\Lambda$)**:
   - Pack parameter: $\Lambda = 0.0001\text{ s}$ ($0.1\text{ ms}$).
   - **Real CANDU 6**: Because heavy water ($D_2O$) has an extremely small absorption cross section, thermal neutrons survive significantly longer before absorption than in light water reactors. The prompt neutron lifetime $l$ in CANDU 6 is **$0.89\text{ ms}$ to $1.0\text{ ms}$** ($8.9 \times 10^{-4}\text{ s}$ to $1.0 \times 10^{-3}\text{ s}$), and $\Lambda \approx 0.9\text{ ms}$.
   - The pack value is an order of magnitude too fast, representative of a Light Water Reactor (PWR/BWR), not a CANDU.
2. **Missing Photoneutron Groups**:
   - In heavy-water reactors, high-energy gamma rays ($E_\gamma > 2.223\text{ MeV}$) from fission products disintegrate deuterons: $^2\text{H}(\gamma, n)^1\text{H}$.
   - This creates an additional photoneutron delayed source with 8 or 9 effective groups having half-lives extending from minutes to several weeks (accounting for $\approx 5\text{--}10\%$ of total delayed neutrons).
   - The pack includes only the standard 6 fission delayed groups, omitting the long photoneutron tail characteristic of CANDU shutdown and load-following transients.
3. **Thermal Velocity**:
   - Pack parameter: $v_2 = 2200.0\text{ m/s}$.
   - While $2200\text{ m/s}$ corresponds to 0.0253 eV (room temperature), the operating CANDU 6 moderator and coolant temperatures ($65\text{--}310^\circ\text{C}$) harden the thermal spectrum, resulting in an average thermal neutron velocity $\bar{v}_2 \approx 2800\text{--}3000\text{ m/s}$.

### 4.5 Absence of Closed-Loop Reactivity Control (RRS / LZCs)
In a physical CANDU 6 at power:
- The core is inherently unstable or neutral to reactivity drift without the **Reactor Regulating System (RRS)**.
- RRS continuously adjusts the light-water fill levels in the **14 Liquid Zone Control compartments (LZCs)**.
- When an 8-bundle refuelling operation inserts $+0.3\text{ mk}$ of reactivity:
  - In reality: The LZCs in that zone and core-wide fill up with light water ($H_2O$ is a neutron poison in a heavy water lattice) within seconds, restoring $\rho_{\text{net}} \approx 0$ and suppressing the local tilt.
  - In `GameSession.cs`: There is **no active LZC loop**. The solver simply holds $\rho_{\text{rel}} > 0$. Over subsequent 600-second steps in `ApplyPracticeAdvance`, point kinetics drives the reactor amplitude $a$ into a continuous runaway increase.
  - Because `powerQuality` penalizes power deviation from 100%, the player's score degrades whenever they refuel unless burnup happens to offset the reactivity, creating gameplay friction that contradicts actual CANDU operating physics.

### 4.6 Omission of Spatial Xenon-135 Dynamics
- In a reactor as physically large and loosely coupled as CANDU 6, local flux increases from refuelling induce localized **Xenon-135 burnout**, which further increases local power over the first 4–8 hours, followed by **Xenon buildup** over 8–24 hours.
- While the repository has domain contracts for Xenon ([`Phase7XenonSpatialCouplingContracts.cs`](file:///c:/Users/infin/candu/src/ReactorSim.Core/Domain/Phase7XenonSpatialCouplingContracts.cs)), `IqsFullCoreSolver` is completely decoupled from it. Local refuelling peaks do not exhibit the spatial Xenon redistribution that is fundamental to CANDU core management.

---

## 5. Comparison Matrix

| Feature / Metric | Real CANDU 6 Physics | Textbook Improved Quasi-Static (IQS) | Current Repository Implementation ([`IqsFullCoreSolver`](file:///c:/Users/infin/candu/src/ReactorSim.Core/Domain/IqsFullCoreSolver.cs)) | Verdict |
| :--- | :--- | :--- | :--- | :--- |
| **Shape Solve Nature** | Dynamic space-time diffusion / transport | Inhomogeneous (fixed source) with delayed precursors & $\dot{P}/P$ | Static $k_{\text{eff}}$ eigenvalue solve ($[A - \frac{1}{k}F]\psi = 0$) | **Divergence**: Implements Adiabatic Approximation, not true IQS |
| **Shape Normalization** | $\langle \Phi_0^*, v^{-1}\psi \rangle = \text{const}$ | $\langle W, v^{-1}\psi \rangle = \text{const}$ ($W = \Phi_0^*$) | $\sum_i V_i (v_1^{-1}\psi_{1,i} + v_2^{-1}\psi_{2,i}) = \text{const}$ | **Deficiency**: Uniform/flat adjoint ($W=1.0$) ignores spatial importance |
| **Reactivity Definition** | Bilinear adjoint integral over perturbed cross sections | $\rho(t) = \frac{\langle W, (-A+F)\psi \rangle}{\langle W, F\psi \rangle}$ | $\rho(t) = \frac{k - 1}{k} - \frac{k_0 - 1}{k_0}$ | **Approximation**: Static $\Delta(1/k)$ ignores local adjoint weighting |
| **Prompt Gen. Time $\Lambda$** | $\approx 0.89\text{--}1.0\text{ ms}$ ($D_2O$ moderated) | Problem-dependent input | $0.1\text{ ms}$ ($1.0 \times 10^{-4}\text{ s}$) | **Inaccurate**: ~9x too fast; representative of LWRs, not CANDU |
| **Delayed Neutron Precursors** | 6 fission groups + 8–9 photoneutron groups | Arbitrary $M$ precursor groups | 6 fission groups only | **Incomplete**: Missing $D_2O$ photoneutron kinetics |
| **Kinetics Time Step** | Transient: $0.001\text{--}0.1\text{ s}$; Steady: $1\text{ s}$ | Adaptive $\Delta t_\mu \le 0.1\text{ s}$ | Micro-step: $600\text{ s}$; Macro-step: $3600\text{ s}$ | **Inappropriate**: Bypasses precursor transient dynamics |
| **Reactivity Control (RRS)** | 14 Liquid Zone Controllers automatically maintain $\rho \approx 0$ | External input / control system model | None in solver loop; reactivity remains uncompensated | **Gameplay Issue**: Power drifts continuously after refuelling |
| **Spatial Xenon-135** | Strong regional oscillations (~28 hr period) | Coupled through cross sections $\Sigma_a(Xe)$ | Not integrated into IQS shape solve | **Omission**: Core refuelling tilts omit Xenon follow-up |
| **Software Architecture** | N/A (legacy Fortran: CERBERUS/DONJON) | Mathematical specification | Clean, engine-neutral C#, immutable candidates, fail-closed | **Excellent**: Exceeds industry engineering standards |

---

## 6. Constructive Recommendations

To balance **physics realism** with the project's primary rule (**"Make the Unity steady-state CANDU refuelling game playable first"**), improvements should be staged:

### 6.1 Phase A: Immediate Gameplay & Parameter Fixes (Low Effort, High Impact)

1. **Correct the Prompt Generation Time**:
   - Update [`candu6-two-group-iqs-pack-v1.json`](file:///c:/Users/infin/candu/src/ReactorSim.Core/EmbeddedData/candu6-two-group-iqs-pack-v1.json):
     ```json
     "generation_time_seconds": 0.0009
     ```
   - Aligns the model with CANDU 6 heavy water kinetics ($\Lambda \approx 0.9\text{ ms}$).

2. **Introduce Closed-Loop LZC Reactivity Balancing in `GameSession`**:
   - When player refuels, do not allow uncompensated positive reactivity to run away in point kinetics.
   - Implement an automated bulk LZC model in `GameSession.ApplyPracticeAdvance`:
     $$\rho_{\text{net}} = \rho_{\text{core}} - \rho_{\text{LZC}}$$
     where $\rho_{\text{LZC}}$ responds on an actuator time constant ($\tau \approx 2\text{--}5\text{ s}$) to drive $\rho_{\text{net}} \to 0$.
   - This keeps reactor power stable near 100% while accurately preserving the local flux peak and channel tilt caused by refuelling.

3. **Re-evaluate the Micro-Step Interval**:
   - If the game loop is simulating steady-state core operation in increments of hours or days, running a high-frequency point-kinetics differential equation with 600-second steps is physically contradictory.
   - Either:
     - Use genuine sub-second micro-stepping ($\Delta t_\mu \le 1.0\text{ s}$) when observing short-term refuelling transients; or
     - For steady-state refuelling game loops, recognize that the reactor regulating system maintains $k_{\text{eff}} \equiv 1.0$, and couple refuelling directly to shape tilts and burnup without numerical amplitude drift.

### 6.2 Phase B: Physics Realism Upgrades (Medium Term)

1. **Physical Adjoint Flux Initialization**:
   - During solver initialization (`IqsFullCoreSolver.TryCreate`), compute or load a reference adjoint flux $\Phi_{g,i}^*$ (for example, by solving the adjoint 2-group diffusion problem $A^T \Phi^* = \frac{1}{k} F^T \Phi^*$).
   - Use $\Phi^*$ in `ComputeConstraint`:
     $$\gamma = \sum_{i=1}^N V_i \left( \Phi_{1,i}^* \frac{1}{v_1} \psi_{1,i} + \Phi_{2,i}^* \frac{1}{v_2} \psi_{2,i} \right)$$
   - This prevents unphysical distortion of the power amplitude when refuelling off-center channels.

2. **Incorporate Heavy Water Photoneutron Groups**:
   - Extend `DelayedNeutronData` to support CANDU photoneutrons (e.g., the standard 6 fission + 8 photoneutron representation).
   - This reproduces the authentic sluggish power tail when power maneuvers occur in CANDU.

3. **Integrate Spatial Xenon Coupling**:
   - Wire the existing [`Phase7XenonSpatialCouplingContracts.cs`](file:///c:/Users/infin/candu/src/ReactorSim.Core/Domain/Phase7XenonSpatialCouplingContracts.cs) into `TrySolveCandidate`.
   - Update bundle-level iodine-135 and xenon-135 concentrations during `ApplyPracticeAdvance`, adding the macroscopic xenon absorption overlay $\Sigma_{Xe,2}^a = \sigma_{Xe} N_{Xe}$ to the thermal group before the spatial solve.

### 6.3 Phase C: Full IQS Formulation (Long Term / Research Grade)

If plant-grade transient capability is ever desired (e.g., loss-of-regulation or adjuster bank withdrawal):
1. Replace `_spatialModel.TrySolve` with a true **fixed-source diffusion solver**:
   $$\left[ A_g + \frac{1}{v_g} \left( \frac{\dot{P}}{P} + \frac{1}{\Delta t_m} \right) \right] \psi_g(t) - (1-\beta)\chi_g F\psi(t) = \frac{1}{P(t)} \sum_j \lambda_j \chi_{d,j,g} C_j(\vec{r}, t) + \frac{1}{v_g \Delta t_m} \psi_g(t - \Delta t_m)$$
2. Update spatial precursor densities $C_j(\vec{r}, t)$ locally at every bundle node.
3. Compute dynamic reactivity $\rho(t)$ via bilinear inner product $\langle \Phi^*, [-\Delta A + \Delta F]\psi \rangle$ rather than eigenvalue differences.
4. Replace the point Jacobi relaxation with a Krylov solver (e.g., BiCGStab or GMRES with block-ILU preconditioning) to maintain real-time performance on 4,560 nodes.

---

## 7. Conclusion

The [`IqsFullCoreSolver`](file:///c:/Users/infin/candu/src/ReactorSim.Core/Domain/IqsFullCoreSolver.cs) in its current state is a **well-crafted, robust, deterministic software module** that successfully fulfills its immediate engineering objective: providing a smooth, responsive, fail-closed spatial power projection for the Unity CANDU Refuelling Game.

However, calling it an "IQS solver" is scientifically inaccurate; it is an **Adiabatic Point-Kinetics hybrid**. For authentic CANDU 6 simulation, its primary physical shortcomings are the **$W=1$ flat adjoint**, the **unrealistically small prompt generation time ($\Lambda = 0.1\text{ ms}$)**, the **$600\text{ s}$ micro-step smearing**, and the **lack of automatic LZC reactivity compensation**, which causes artificial power divergence following refuelling.

Applying the immediate recommendations in Section 6.1 will align the solver with CANDU 6 reactor behavior while preserving the playable, deterministic refuelling experience.
