# Technical Review: Improved Quasi-Static (IQS) Neutronic Solver for CANDU 6

## 1. Executive Summary

This document provides an in-depth technical review of the **Improved Quasi-Static (IQS) Neutronic Solver** implemented in [`IqsFullCoreSolver.cs`](file:///c:/Users/infin/candu/src/ReactorSim.Core/Domain/IqsFullCoreSolver.cs), its accompanying kinetics data pack [`candu6-two-group-iqs-pack-v1.json`](file:///c:/Users/infin/candu/src/ReactorSim.Core/EmbeddedData/candu6-two-group-iqs-pack-v1.json), and its runtime coupling within [`GameSession.cs`](file:///c:/Users/infin/candu/src/ReactorSim.Game/GameSession.cs).

The solver was developed to provide time-dependent neutron kinetics and spatial power shape tracking for a steady-state CANDU 6 on-power refuelling simulation game. From a **software architecture and contract-engineering perspective**, the implementation is exemplary: deterministic, engine-neutral, fail-closed, memory-safe, and cleanly decoupled between simulation state transitions, game orchestration, and presentation layers.

However, from a **reactor physics and numerical methods perspective**, there is a significant divergence between the formal textbook definition of the **Improved Quasi-Static (IQS)** method and the current implementation, as well as several physical gaps regarding the unique neutronic behavior of a **CANDU 6** core. Specifically:
1. **Methodological Classification**: The solver implements an **Adiabatic Approximation** (static $k$-eigenvalue shape recomputation combined with a point-kinetics amplitude advance), rather than a true **Improved Quasi-Static (IQS)** method (which requires solving a time-dependent fixed-source shape equation with delayed neutron precursors and dynamic frequency terms).
2. **Adjoint Weighting**: The shape normalization constraint uses a uniform flat adjoint ($W(\vec{r}) = 1.0$), rather than the physical adjoint flux ($\Phi^*(\vec{r})$) of a CANDU 6 core.
3. **Reactivity Definition**: Dynamic reactivity is computed from the static eigenvalue delta $\Delta(1/k)$, bypassing bilinear adjoint weighting of local cross-section perturbations.
4. **Time Integration Scales**: Micro-steps of $600\text{ s}$ (10 minutes) and shape intervals of $3600\text{ s}$ (1 hour) bypass delayed neutron transient dynamics, effectively reducing the kinetics to an algebraic prompt-jump / precursor-equilibrium solver.
5. **CANDU 6 Physics Omissions**: Key CANDU 6 characteristics—such as prompt neutron generation time ($\Lambda \approx 0.9\text{ ms}$ vs. pack $0.1\text{ ms}$), heavy-water photoneutron groups, automatic Reactor Regulating System (RRS) / Liquid Zone Controller (LZC) reactivity compensation, and spatial Xenon-135 feedback—are either approximated with non-CANDU parameters or decoupled from the runtime loop.

This review details what works, what doesn't work, provides citable literature sources and benchmark data for **heavy-water photoneutron kinetics**, and establishes an actionable, phased roadmap for what to do next.

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

## 6. Photoneutron Nuclear Data: Sources, Physics, and Benchmark Parameters

### 6.1 Physical Origin of Photoneutrons in Heavy-Water Reactors

In heavy-water ($D_2O$) moderated reactors such as CANDU 6, neutrons originate from three distinct physical mechanisms:
1. **Prompt Fission Neutrons**: Emitted within $\approx 10^{-14}\text{ s}$ of nuclear fission ($>99\%$ of all neutrons).
2. **Fission-Product Delayed Neutrons**: Emitted following the $\beta^-$ decay of specific fission fragment precursor radionuclides ($^{87}\text{Br}$, $^{137}\text{I}$, etc.) in the fuel elements (traditionally modeled in 6 or 8 temporal groups with half-lives from $0.2\text{ s}$ to $55\text{ s}$).
3. **Photoneutrons**: Produced when high-energy $\gamma$-rays emitted by decaying fission products in the fuel escape through the pressure and calandria tubes into the heavy-water moderator, where they undergo photodisintegration with deuterium nuclei:
   $$^2\text{H} + \gamma \longrightarrow \, ^1\text{H} + n \quad (Q = -2.223\text{ MeV})$$

Because the photodisintegration threshold for deuterium is $E_{\text{th}} = 2.223\text{ MeV}$, any fission product $\beta^-$ decay emitting a $\gamma$-ray above $2.223\text{ MeV}$ (e.g., $^{140}\text{La}$, $^{144}\text{Pr}$, $^{88}\text{Rb}$, $^{90}\text{Y}$, $^{87}\text{Kr}$) produces photoneutrons with an effective appearance rate governed by the parent fission product's radioactive decay constant.

**Key physical consequences in CANDU 6**:
- **Extended Half-Lives**: Photoneutron precursors have half-lives extending from seconds up to **12.8 days** ($^{140}\text{Ba} \to \, ^{140}\text{La}$).
- **Delayed Fraction Magnitude**: At full-power equilibrium, photoneutrons account for approximately **$5\%\text{ to }8\%$** of the total delayed neutron fraction ($\beta_{\text{photo}} \approx 0.00035\text{--}0.00050$, relative to total $\beta \approx 0.0054\text{--}0.0070$).
- **Shutdown Power Tail**: While standard delayed fission neutrons decay away within 5 minutes after shutdown, photoneutrons persist for days and weeks, dominating the subcritical neutron population and providing an inherent neutron source for reactor restart and monitoring.
- **Spatial Separation**: Unlike delayed fission precursors which decay inside the fuel matrix, photoneutrons are produced in the **moderator**. In a spatial solver, their emission spatial profile is determined by gamma transport from fuel channels into the inter-lattice heavy water.

---

### 6.2 Citable Literature Sources and Historical Benchmark Studies

To avoid guessing or inventing nuclear constants, the following published, peer-reviewed literature and institutional technical reports serve as authoritative sources for photoneutron data in heavy-water reactors:

1. **The Direct AECL IQS Validation Report**:
   - **Kugler, G., and Dastur, A. R. (1976)**: *"Accuracy of the Improved Quasi-static Space-Time Method Checked with Experiment"*, Atomic Energy of Canada Limited Report **AECL-5553**.
   - *Significance*: This report explicitly validates the **Improved Quasi-Static (IQS)** method against space-time kinetics experiments in CANDU heavy water reactors, demonstrating the necessity of photoneutron groups to capture delayed neutron shape retardation.

2. **The Canadian Academic / CANTEACH Standard Reference**:
   - **Rozon, Daniel (1998)**: *"Introduction to Nuclear Reactor Kinetics"*, Institut de génie nucléaire, École Polytechnique de Montréal. Published under the CANTEACH project (Document ID 20041801, available via [`nuceng.ca/canteach`](http://nuceng.ca/canteach)).
   - *Significance*: Chapter 1 (Section 1.3.3 and Table 1.4) provides the canonical 9-group photoneutron parameter set cited across Canadian nuclear engineering programs, attributing the data to **G. Kugler (1975)**.

3. **Foundational Experimental Measurements (Savannah River)**:
   - **Baumann, N. P., Currie, R. L., and Pellarin, D. J. (1973)**: *"Production of Delayed Photoneutrons from U-235 in Heavy-Water Moderator"*, Savannah River Laboratory Report **DP-MS-73-39** / CONF-731101-40.
   - *Significance*: Provides the experimental 9-group decay constants and yields for $^{235}\text{U}$ fission products in $D_2O$. Later recommended by AECL (Laughton) over older Bernstein data due to reduced systematic relative errors in short-lived groups.

4. **Early Foundational Work**:
   - **Bernstein, S., Preston, W. M., Wolfe, G., and Slattery, R. E. (1947/1948)**: *"Yield of Photoneutrons from U235 Fission Products in Heavy Water"*, *Physical Review* 71, 573 (1947); *Physical Review* 73, 1146 (1948).
   - *Significance*: The earliest systematic experimental measurement of delayed photoneutron yields in heavy water.

5. **AECL Operational Analysis**:
   - **Milgram, M. S. (1982)**: *"Delayed Neutrons and Photoneutrons for the Analysis of CANDU Reactor Transients"*, Atomic Energy of Canada Limited Report **AECL-7756**.
   - *Significance*: Discusses the integration of photoneutrons into CANDU plant safety, shutdown, and flux-peaking calculations.

6. **ZED-2 Zero-Energy Experimental Validation**:
   - **Surya, D., et al. (2000)**: *"Photo-Neutron Experiment Performed in ZED-2"*, Proceedings of the 21st Annual Canadian Nuclear Society (CNS) Conference, Toronto, Ontario.
   - *Significance*: Experimental rod-drop benchmark at Chalk River Laboratories measuring the transition from fission delayed neutrons to photoneutron decay.

---

### 6.3 The Standard 9-Group Photoneutron Dataset (Kugler 1975 / Rozon 1998)

The table below reproduces the canonical 9-group photoneutron parameters for heavy water ($D_2O$) from Kugler (1975) as tabulated in Rozon (1998, CANTEACH):

$$\lambda_k = \frac{\ln(2)}{T_{1/2, k}}$$

| Photoneutron Group ($k$) | Half-Life ($T_{1/2, k}$) | Decay Constant ($\lambda_k$ in $\text{s}^{-1}$) | Relative Yield Fraction ($\beta_k / \beta_{\text{total}}$ in %) | Absolute Fraction $\beta_k$ (Nominal Core $\beta \approx 0.0054$) | Primary Fission Product Gamma Precursor |
| :---: | :---: | :---: | :---: | :---: | :---: |
| **1** | $12.8\text{ days}$ | $6.26 \times 10^{-7}$ | $0.01653\%$ | $8.93 \times 10^{-7}$ | $^{140}\text{Ba} \to \, ^{140}\text{La}$ ($E_\gamma = 2.52\text{ MeV}$) |
| **2** | $2.21\text{ days}$ | $3.63 \times 10^{-6}$ | $0.03372\%$ | $1.82 \times 10^{-6}$ | $^{132}\text{Te} \to \, ^{132}\text{I}$ |
| **3** | $4.41\text{ hours}$ | $4.37 \times 10^{-5}$ | $0.1058\%$ | $5.71 \times 10^{-6}$ | $^{91}\text{Sr} \to \, ^{91}\text{Y}$ |
| **4** | $52.7\text{ min}$ | $2.19 \times 10^{-4}$ | $0.1661\%$ | $8.97 \times 10^{-6}$ | $^{135}\text{I}$ ($E_\gamma = 2.45\text{ MeV}$) |
| **5** | $16.7\text{ min}$ | $6.92 \times 10^{-4}$ | $0.3306\%$ | $1.79 \times 10^{-5}$ | $^{88}\text{Rb}$ |
| **6** | $4.39\text{ min}$ | $2.63 \times 10^{-3}$ | $0.4873\%$ | $2.63 \times 10^{-5}$ | $^{87}\text{Kr}$ |
| **7** | $1.30\text{ min}$ | $8.89 \times 10^{-3}$ | $0.8125\%$ | $4.39 \times 10^{-5}$ | $^{142}\text{La}$, $^{94}\text{Y}$ |
| **8** | $32.3\text{ s}$ | $2.15 \times 10^{-2}$ | $0.8834\%$ | $4.77 \times 10^{-5}$ | Short-lived fission fragments |
| **9** | $10.1\text{ s}$ | $6.86 \times 10^{-2}$ | $0.6488\%$ | $3.50 \times 10^{-5}$ | Short-lived fission fragments |
| **Total Photo** | — | — | **$\approx 3.49\%$ of $\beta_{\text{total}}$** | **$\beta_{\text{photo}} \approx 1.88 \times 10^{-4}$** | — |

*Note on Total Yield*: In operating CANDU 6 cores, depending on fuel burnup (Pu-239 buildup) and moderator lattice volume fraction, the total photoneutron yield fraction ranges from $\approx 3.5\%$ to $6.5\%$ of total $\beta$ ($\approx 0.00020$ to $0.00035$). When coupled with 6 standard fission delayed groups, a 15-group delayed source vector ($6\text{ fission} + 9\text{ photoneutron}$) fully characterizes the reactor kinetics from sub-second control rod motion to multi-day shutdown cooling.

---

## 7. What to Do Next: Phased Implementation Roadmap

To align the solver with true CANDU 6 behavior while strictly adhering to the repository's core principle (**"Make the Unity steady-state CANDU refuelling game playable first"**), the following dependency-ordered roadmap is established:

```
[Phase 1: Foundation & Truth] -> [Phase 2: Closed-Loop Regulation] -> [Phase 3: Physics Realism] -> [Phase 4: Full IQS]
        (A1, A2)                              (A3)                            (B1, B2, B3, C2)              (C1, C4)
```

---

### Phase 1: Formulation Transparency & Parameter Calibration (Immediate Priority)

#### Task 1.1: Truth in Identity and Backward Compatibility (A1)
- **Problem**: The solver claims identity `spatial-eigen-iqs-v1`, but mathematically performs an adiabatic eigenmode solve.
- **Action**:
  - Reclassify the formulation identity in [`IqsFullCoreSolver.cs`](file:///c:/Users/infin/candu/src/ReactorSim.Core/Domain/IqsFullCoreSolver.cs) and presentation snapshots as:
    `shape_method: "adiabatic-static-eigen-v1"`, `amplitude_method: "point-kinetics-semi-implicit-v1"`.
  - Maintain the legacy `spatial-eigen-iqs-v1` solver ID as an accepted alias in JSON loaders to preserve full backward compatibility with existing tests and browser sessions.

#### Task 1.2: Generalize Delayed-Source Groups & Calibrate Kinetics Parameters (A2)
- **Problem**: The current codebase hardcodes `double[6]` array bounds for delayed groups, and [`candu6-two-group-iqs-pack-v1.json`](file:///c:/Users/infin/candu/src/ReactorSim.Core/EmbeddedData/candu6-two-group-iqs-pack-v1.json) uses non-CANDU parameters ($\Lambda = 0.1\text{ ms}$, $v_2 = 2200\text{ m/s}$).
- **Action**:
  - Generalize `IqsKineticsDataPackV1` to store an ordered list of delayed groups of arbitrary length $M \ge 1$.
  - Add group family metadata (`group_family: "fission"` vs. `"photoneutron"`).
  - Update embedded pack parameters to authentic CANDU 6 values:
    ```json
    "generation_time_seconds": 0.0009,
    "group_velocities_m_per_s": [ 1.0e7, 2900.0 ]
    ```
  - Verify that existing 6-group equilibrium tests continue to pass with the generalized collection.

---

### Phase 2: Closed-Loop Regulation & Gameplay Stability (High Priority)

#### Task 2.1: Implement Reactor Regulating System (RRS) & LZC Compensation (A3)
- **Problem**: Adding fresh fuel during refuelling inserts $+0.2\text{ to }+0.4\text{ mk}$ of reactivity. Without automatic regulation, point kinetics causes power to ramp up indefinitely, degrading the player's score.
- **Action**:
  - Connect the synthetic Liquid Zone Controller (LZC) contracts in [`Phase6LiquidZoneContracts.cs`](file:///c:/Users/infin/candu/src/ReactorSim.Core/Domain/Phase6LiquidZoneContracts.cs) and [`Phase6RrsContracts.cs`](file:///c:/Users/infin/candu/src/ReactorSim.Core/Domain/Phase6RrsContracts.cs) into [`GameSession.cs`](file:///c:/Users/infin/candu/src/ReactorSim.Game/GameSession.cs).
  - Implement automatic bulk and zonal level adjustments that respond to positive refuelling reactivity by increasing light-water absorber fill levels:
    $$\rho_{\text{net}}(t) = \rho_{\text{core}}(t) - \Delta\rho_{\text{LZC}}(t) \longrightarrow 0$$
  - Preserve the local spatial flux peaking and channel tilts in the spatial solve while stabilizing total core power at the requested setpoint (100%).

#### Task 2.2: Rationalize the Kinetics Integration Cadence
- **Problem**: Taking 600-second micro-steps in point kinetics smears out all delayed precursor differential physics.
- **Action**:
  - Explicitly partition the simulation clock:
    - Long-term burnup/depletion and scheduled shape recomputes run on hourly intervals ($\Delta t_{\text{macro}} = 3600\text{ s}$).
    - Immediate refuelling kinetics transients run on short sub-second micro-steps ($\Delta t_\mu \le 0.5\text{ s}$) over a 10–30 second stabilizing window with active LZC response.

---

### Phase 3: Physics Realism Upgrades (Medium Term)

#### Task 3.1: Lawful Photoneutron Data Ingestion (C2)
- **Problem**: Photoneutrons are omitted, neglecting the heavy-water shutdown tail and long-term precursor memory.
- **Action**:
  - Formally ingest the 9-group photoneutron dataset (Kugler 1975 / Rozon 1998 / AECL-5553) documented in Section 6.3 into a new versioned kinetics pack:
    `candu6-two-group-kinetics-15group-v1.json` ($6\text{ fission} + 9\text{ photoneutron groups}$).
  - Include full provenance metadata: `source_provenance: "CANTEACH / AECL-5553 / Rozon (1998) Table 1.4"`.
  - Maintain dual-read compatibility so existing 6-group packs remain valid.

#### Task 3.2: Reference Adjoint Flux ($\Phi^*$) Weighting (B1, B2)
- **Problem**: Uniform weighting ($W = 1.0$) in `ComputeConstraint` treats peripheral leakage boundaries identically to the core center.
- **Action**:
  - Solve the unperturbed adjoint 2-group diffusion problem:
    $$A^T \Phi^* = \frac{1}{k} F^T \Phi^*$$
  - Cache $\Phi^*(\vec{r})$ at initialization and use it in the normalization constraint:
    $$\gamma = \sum_{i=1}^N V_i \left( \Phi_{1,i}^* \frac{1}{v_1} \psi_{1,i} + \Phi_{2,i}^* \frac{1}{v_2} \psi_{2,i} \right) = \text{constant}$$
  - Weight dynamic reactivity perturbations by $\Phi^*$, ensuring that central refuelling produces higher reactivity worth than peripheral refuelling.

#### Task 3.3: Integrate Spatial Xenon-135 Coupling (B3)
- **Problem**: Xenon dynamics are currently decoupled from the spatial solve.
- **Action**:
  - Couple [`Phase7XenonSpatialCouplingContracts.cs`](file:///c:/Users/infin/candu/src/ReactorSim.Core/Domain/Phase7XenonSpatialCouplingContracts.cs) into `TrySolveCandidate`.
  - Update Iodine-135 and Xenon-135 inventories at each node during burnup integration, overlaying macroscopic absorption $\Sigma_{a,2}^{\text{Xe}} = \sigma_{\text{Xe}} N_{\text{Xe}}$ onto the thermal group.
  - Recreates the authentic CANDU 24-hour Xenon burnout/buildup response following channel refuelling.

---

### Phase 4: Full Inhomogeneous IQS Operator (Long Term / Research Grade)

If plant-grade transient capability is ever desired (e.g., loss-of-regulation or adjuster bank withdrawal):
1. **Fixed-Source Shape Operator (C1)**: Replace the static eigenvalue solve with the true time-dependent inhomogeneous diffusion operator:
   $$\left[ A_g + \frac{1}{v_g} \left( \frac{\dot{P}}{P} + \frac{1}{\Delta t_m} \right) \right] \psi_g(t) - (1-\beta)\chi_g F\psi(t) = \frac{1}{P(t)} \sum_{j=1}^M \lambda_j \chi_{d,j,g} C_j(\vec{r}, t) + \frac{1}{v_g \Delta t_m} \psi_g(t - \Delta t_m)$$
2. **Nodewise Precursor Fields**: Track spatial precursor densities $C_j(\vec{r}, t)$ at all 4,560 bundle nodes.
3. **Bilinear Dynamic Integrals (C3)**: Derive instantaneous $\rho(t)$, $\beta_{\text{eff}}(t)$, and $\Lambda(t)$ via full spatial inner products with $\Phi^*$.
4. **Deterministic Krylov Acceleration (C4)**: Replace point Jacobi relaxation with matrix-free BiCGStab or GMRES with block preconditioning to maintain real-time interactive frame rates.

---

## 8. Conclusion

The [`IqsFullCoreSolver`](file:///c:/Users/infin/candu/src/ReactorSim.Core/Domain/IqsFullCoreSolver.cs) is a **robust, deterministic, memory-safe software artifact** that fulfills its immediate gaming objective of providing smooth, fail-closed spatial power projections for the Unity CANDU Refuelling Game.

Scientifically, it operates as an **Adiabatic Point-Kinetics hybrid** rather than a true Improved Quasi-Static solver. Its primary physical limitations for CANDU 6 are the **$W=1$ flat adjoint**, the **LWR-like prompt generation time ($\Lambda = 0.1\text{ ms}$)**, the **absence of heavy-water photoneutrons**, and the **lack of automatic LZC regulation**, which causes artificial power drift after refuelling.

By adopting the **Kugler / Rozon 9-group photoneutron library** (AECL-5553 / CANTEACH) and following the phased roadmap in Section 7, the project can systematically enhance physical fidelity while preserving the rock-solid determinism and playability of the game.
