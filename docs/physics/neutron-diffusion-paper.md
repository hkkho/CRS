# Two-group neutron diffusion in the Candu Refuelling Simulator

*Numerical formulation, verification, and browser latency*

*CRS technical paper · 9 November 2026 · Runtime revision* `4341ae8`

## Abstract

The Candu Refuelling Simulator (CRS) computes the steady neutron flux and
effective multiplication factor of a stylized CANDU-6 core in a browser.
The model has 4,560 finite volumes, two energy groups, and a sparse leakage
stencil. The solver applies power iteration with two inexact Jacobi solves
per outer iteration and normalizes the resulting flux to prescribed thermal
power. Reciprocal face currents conserve neutrons across interior faces,
and strict row diagonal dominance gives a convergence argument for the
fixed-source group solves. An analytic reflective-cell solution and spatial
regressions verify aspects of the discrete model. For gameplay, one frozen-flux
integration covers each simulated half-day, with at most one additional
equilibrium event for the entire refuelling batch. Warm starts, reused
candidate inputs, and ahead-of-time WebAssembly compilation further reduce
command latency. In a desktop Chromium run, all 27 measured AOT commands
finished within five seconds. Median times were 1.91 s, 3.82 s, and
3.94 s for zero, one, and two fuel orders respectively.

**Keywords** · neutron diffusion, finite volume, source iteration, Jacobi,
WebAssembly, CANDU, numerical verification

## 1. Problem and scope

After a player replaces fuel, CRS must recompute where fission power is
produced and how the liquid-zone regulator responds. A local fuel change affects
neighboring channels through leakage, so each equilibrium event solves the
whole configured core. The browser requests steps from the same authoritative
C# simulation used by the native tests. Core owns neutronics and inventory,
Game owns the transaction and operating-limit decisions, and the browser
renders accepted observations.
[Architecture and cadence](../IMPLEMENTATION_GUIDE.md) ·
[Browser worker](../../web/candu-playtest/src/wasmWorker.ts)

The default topology follows the 380-channel, 12-bundle geometry of the
CANDU-6 reference design. CRS homogenizes each bundle position into one
volume with two scalar flux unknowns, giving 9,120 flux unknowns and one
effective multiplication factor. The active v11 pack is a calibrated gameplay
surrogate with authored coefficients, effective boundaries and localized
absorption. This paper assesses the balance equations of that surrogate and
the cost of solving them. Plant-level predictive accuracy would require
independent reference data and model-error studies. [1](#ref-1) ·
[Active data pack](../../data/packs/candu6-two-group-diffusion-pack-v1.json) ·
[Calibration provenance](calibration-provenance.md)

## 2. Physical and spatial formulation

### 2.1 Steady two-group balance

The diffusion approximation closes the angular transport balance with Fick's
law, $\mathbf J_g=-D_g\nabla\phi_g$. Here $\phi_g$ is group-integrated scalar
flux in neutrons m$^{-2}$ s$^{-1}$, $D_g$ is an effective diffusion coefficient
in metres, and $\mathbf J_g$ is the net current density. Index 1 denotes fast
neutrons and index 2 thermal neutrons. With the material state frozen,
downscatter transfers neutrons from group 1 to group 2, and the model neglects
upscatter. The steady production, removal and leakage equations are
[2](#ref-2) · [3](#ref-3)

$$
\begin{aligned}
-\nabla\cdot(D_1\nabla\phi_1)+(\Sigma_{a1}+\Sigma_{s12})\phi_1
 &= \chi_1 F/k,\\
-\nabla\cdot(D_2\nabla\phi_2)+\Sigma_{a2}\phi_2
 &= \Sigma_{s12}\phi_1+\chi_2 F/k,\\
F &= \nu\Sigma_{f1}\phi_1+\nu\Sigma_{f2}\phi_2,
\qquad \chi_1+\chi_2=1.
\end{aligned}\tag{1}
$$

Macroscopic cross sections have units m$^{-1}$, so every term in the
balance is a volumetric rate. $F$ counts fission neutrons produced per
volume per second. The default fuel rows put all fission births in the fast
group. The eigenvalue $k$ scales production to achieve steady balance. A
configuration at $k=1$ is critical in the model, and the associated dimensionless
reactivity is $\rho=(k-1)/k$. Because the eigenproblem is homogeneous in flux,
the amplitude is fixed separately using the target thermal power $P_\star$.
The solver computes the heat in cell $i$ from its fission rate and energy
per fission $E_{f,i}$.
[Pack binding](../../src/ReactorSim.Core/Domain/FullCoreDiffusionModelContracts.cs#L1480) ·
[Eigen iteration and power](../../src/ReactorSim.Core/Domain/SpatialEigenIterationContracts.cs#L831)

$$
P_i=V_iE_{f,i}\big(\Sigma_{f1,i}\phi_{1,i}+\Sigma_{f2,i}\phi_{2,i}\big),
\qquad P(\phi)=\sum_i P_i=P_\star .
\tag{2}
$$

The default $P_\star$ is 2,064 MW thermal. The heating coefficients
$\Sigma_f$ and the neutron-production coefficients $\nu\Sigma_f$ enter
different equations. A configuration may have $k\ne 1$ at the prescribed
power because normalization sets the amplitude of a steady shape. The model
uses that shape to calculate subsequent burnup and poison exposure.
[Active solver and coefficients](active-two-group-solver.md) ·
[Reference power](channel-power-reference.md)

### 2.2 Conservative finite volumes

Integrate (1) over cell $i$, apply the divergence theorem and divide by
its volume $V_i$. Approximating the integrated face current by a
conductance times a two-point flux difference gives the group operator
[3](#ref-3) · [Stencil assembly](../../src/ReactorSim.Core/Domain/SpatialStencilContracts.cs#L116) ·
[Operator evaluation](../../src/ReactorSim.Core/Domain/SpatialOperatorContracts.cs#L1213)

$$
\begin{aligned}
(A_g\phi_g)_i &= R_{g,i}\phi_{g,i}+\frac{1}{V_i}\left[
\sum_{j\sim i} C_{g,ij}(\phi_{g,i}-\phi_{g,j})
+\sum_{b\in\partial i} C_{g,ib}\phi_{g,i}
\right],\\
R_{1,i} &= \Sigma_{a1,i}+\Sigma_{s12,i},
\qquad R_{2,i}=\Sigma_{a2,i}.
\end{aligned}\tag{3}
$$

For an interior face, the two nodes share the same $C_{g,ij}=C_{g,ji}$.
Multiplying each row by its cell volume before summing cancels these two
currents exactly in the algebra. Summing the two groups then cancels
downscatter as an internal transfer. The remaining whole-core balance is
fission production divided by $k$ equal to absorption plus outward
boundary leakage. Conductances are in m$^2$, consistent with a face
area times diffusion coefficient divided by distance.
[Conductance binding](../../src/ReactorSim.Core/Domain/SpatialOperatorContracts.cs#L1011) ·
[Boundary and conductance values](active-two-group-solver.md)

The pack uses effective diffusion coefficients $D_1=0.015$ m and
$D_2=0.0075$ m, with $V=0.04044276185625$ m$^3$ per node. Interior conductance is of the
form $D_g A_f/d$ for face area $A_f$ and node-centre separation $d$.
At a radial vacuum face the distance is from the node centre to the
5.55 m extrapolation circle along the outward coordinate ray. At an axial
vacuum face the distance is half the 0.4953 m cell length plus 0.22 m.
The extrapolated flux is zero in both cases. A reflective face has zero
conductance. These are effective leakage closures on the channel lattice.
[Geometry calibration](../../data/calibration/diffusion-v10/README.md) ·
[Radial boundary implementation](../../src/ReactorSim.Core/Domain/CircularRadialBoundary.cs)

Fuel coefficients are bound by bundle burnup from the pack's piecewise-linear
tables. Fixed adjusters and liquid-zone water change local absorption. Live
xenon replaces the xenon already included in the fuel table by adding
$(N_{\rm Xe}-N_{\rm Xe,ref})\sigma_{\rm capture}w$ to thermal absorption,
where $w$ is the pack's separate worth scale. The spatial solve holds
these coefficients fixed for each candidate.
[Burnup lookup](../../src/ReactorSim.Core/Domain/BurnupCoefficientContracts.cs) ·
[Xenon calibration](xenon-calibration.md)

## 3. Eigenvalue algorithm and numerical correctness

### 3.1 Source iteration

Let $A$ be block lower triangular with $A_1$ and $A_2$ on the
diagonal and $-S_{12}$ below it, where $S_{12}$ is the diagonal downscatter
matrix. Let $Q\phi=F$ compute the cellwise fission neutron source, and let
$X$ distribute it into the two groups according to $\chi$. Equation (1)
after discretization is $A\phi=XQ\phi/k$. One outer step forms $F$ from
the current flux, approximately solves $A_1$ with source $\chi_1F/k$,
then approximately solves $A_2$ with source $S_{12}\widetilde\phi_1+
\chi_2F/k$ using the trial fast flux $\widetilde\phi_1$. With
$T(\phi)=\sum_i V_i(Q\phi)_i$, the implementation updates $k$ before
normalizing both groups by the same factor.
[Source iteration implementation](../../src/ReactorSim.Core/Domain/SpatialEigenIterationContracts.cs#L388)

$$
k_{\rm next}=k\,\frac{T(\widetilde\phi)}{T(\phi)},\qquad
\phi_{\rm next}=\frac{P_\star}{P(\widetilde\phi)}\,\widetilde\phi.
\tag{4}
$$

With exact group solves, the new source is proportional to
$M F$ for $M=QA^{-1}X$. Thus the direction of the source follows power
iteration. At a dominant eigenmode $MF=kF$, the production ratio in (4)
returns that same $k$. For the connected default mesh, positive interior
couplings and positive removal give $A_1^{-1}$ strictly positive entries.
With default fast-group birth, the fast-group production contribution
$\operatorname{diag}(\nu\Sigma_{f1})A_1^{-1}$ is strictly positive
on the fissioning cells. The thermal contribution is nonnegative
because $A_2^{-1}$, downscatter and thermal production are nonnegative.
Thus $M$ restricted to the fissioning cells is strictly positive. The
Perron–Frobenius theorem then supplies a simple positive dominant source
mode and convergence in direction from a positive source for the exact
iteration. This argument relies on the stated connectivity and coefficient
conditions, and convergence speed depends on the dominance ratio. The
bounded, inexact implementation is assessed through its own acceptance
criteria. [4](#ref-4)

### 3.2 Jacobi contraction and the acceptance gate

Write $A_g=\mathcal D_g-B_g$, where $\mathcal D_g$ is the diagonal and
$B_g$ has nonnegative off-diagonal entries $C_{g,ij}/V_i$. If removal is
positive in every cell, as in the default pack with the active overlays, then
each diagonal exceeds the sum of its row's off-diagonal magnitudes. The
Jacobi update $\phi^{m+1}=\phi^m+\mathcal D_g^{-1}(s-A_g\phi^m)$ has
error operator $\mathcal D_g^{-1}B_g$ with infinity norm
[5](#ref-5) ·
[Diagonal construction](../../src/ReactorSim.Core/Domain/SpatialEigenIterationContracts.cs#L907)

$$
q_g=\max_i
\frac{\sum_j C_{g,ij}}
{V_iR_{g,i}+\sum_j C_{g,ij}+\sum_b C_{g,ib}}<1 .
\tag{5}
$$

This proves contraction for the exact-arithmetic fixed-source iteration.
The equivalent formula $\mathcal D_g^{-1}(s+B_g\phi^m)$ also preserves
nonnegative iterates for nonnegative initial flux and source. The
implementation explicitly checks finite and nonnegative flux because it
computes a residual correction in floating point. Rewriting the inverse as
a Neumann series in $\mathcal D_g^{-1}B_g$ shows it is nonnegative, and
strictly positive between connected cells. With the fresh fuel row and no
overlays, the ratio in (5) is about 0.63 fast and 0.73 thermal at an interior
cell with six neighbors. These are representative row values. The outer
iteration and the capped inner solve each require a separate acceptance check.
[Jacobi loop](../../src/ReactorSim.Core/Domain/SpatialEigenIterationContracts.cs#L681)

| Check | Default pack criterion |
| --- | --- |
| Inner fixed-source residual | Absolute infinity norm $\le 10^{-11}$ or relative $\le 10^{-7}$ |
| Outer $k$ change | Absolute $\le 2\times10^{-7}$ or relative $\le 2\times10^{-6}$ |
| Outer coupled equation residual | Relative infinity norm $\le 2\times10^{-5}$ |
| Outer source-shape change | Infinity norm $\le 10^{-5}$ |
| Outer relative power error | $\lvert P-P_\star\rvert/P_\star \le 10^{-12}$ |

The relative residual is $\max\lvert L-R\rvert/\max(\lvert L\rvert+\lvert
R\rvert)$ over the relevant rows, where $L$ and $R$ are the equation's
two sides. The outer residual recomputes both groups using the new $k$ and
normalized flux. Source shape is the vector of cell fractions
$V_iF_i/\sum_j V_jF_j$. The relative $k$ change uses the larger magnitude of
the old and new eigenvalues as its scale. All four outer checks must pass
together, with either $k$ test sufficient for that row. Caps are 128 inner
iterations per group solve and 1,600 outer iterations. An exhausted solve
has no usable final state. The residual measures agreement with the
assembled equations. Continuum and plant-model errors require separate evidence.
[Policy and diagnostics](../../src/ReactorSim.Core/Domain/SpatialConvergenceContracts.cs#L455) ·
[Pack policy](../../data/packs/candu6-two-group-diffusion-pack-v1.json)

## 4. How CRS gets below five seconds

### 4.1 Fewer equilibrium events

The first daily-turn implementation subdivided a simulated day into 480
intervals of 180 seconds. The archived development record reports roughly
9–11 minutes per local browser turn for that prototype. The replacement
used one frozen-flux interval and one endpoint event. In the archived
daily-mode acceptance, a two-channel day took 4.154 s and the following
empty day took 1.487 s. Those historical observations show how strongly
the workload depends on the integration cadence. Section 5 uses the
current v11 workload for the build comparison.
[Archived daily-turn verification](https://github.com/hkkho/CRS/blob/2015dd334782358e910681bbd10f25d8fb3cf2d7/docs/performance/daily-turns.md)

The public time-based mode takes one 43,200 s interval per command. When
there is fuel to move, Game stages all eight-bundle orders in canonical
channel order and solves the combined inventory in one equilibrium event.
The resulting power and flux are held fixed for a burnup update and a
closed-form iodine–xenon integration in each cell. Game then solves one
endpoint equilibrium event if the run can continue. With an empty batch it
starts from the accepted shape and goes straight to exposure and the
endpoint event. The batch removes the need for one pre-exposure event per
order. Freezing the shape removes neutron-shape substeps inside the
half-day and permits the analytic poison update. The approximation leaves
intrainterval flux and power unresolved. A combined post-fuel limit
violation can end the run before exposure. A numerical failure discards
the detached transaction.
[Half-day transaction](../../src/ReactorSim.Game/GameSession.cs#L147) ·
[Poison integration](../../src/ReactorSim.Core/Domain/PracticeXenonContracts.cs#L27) ·
[Shared step semantics](../gameplay/daily-turn-mode.md)

For fixed fission rate density $f$ and flux, the poison equations in each
cell have constant coefficients. The iodine density $N_I$ feeds xenon
density $N_{\rm Xe}$ by decay. The two equations can be integrated over
the entire interval using exponentials, with $\gamma$ denoting fission
yields, $\lambda$ decay constants, and $\sigma_{c,g}$ effective capture
coefficients. The frozen flux makes the xenon removal coefficient constant.
[Poison model and calibration](xenon-calibration.md)

$$
\begin{aligned}
\dot N_I &= \gamma_I f-\lambda_I N_I,\\
\dot N_{\rm Xe} &= \gamma_{\rm Xe} f+\lambda_I N_I
-\big(\lambda_{\rm Xe}+\sigma_{c,1}\phi_1+\sigma_{c,2}\phi_2\big)N_{\rm Xe}.
\end{aligned}\tag{6}
$$

### 4.2 Less work per event and per iteration

An equilibrium event is a sequence of full-core eigensolves. The liquid-zone
regulator first solves a baseline without liquid-zone absorption and a
controlled baseline with the previous fills. It then uses a small bounded
fill calculation to propose a change if needed. Verification and one
possible correction solve the actual spatial equations with the proposed
overlays. This bounds a normal event to two through four candidates.
The regulator prepares the validated inventory and base coefficient set
once for the event, then reuses those inputs and the bound edge and
boundary records as absorption changes. Each candidate still assembles
its operator and diagonal from its effective coefficients. The first
candidate starts from the previous accepted or supplied flux and $k$,
and later candidates seed from the preceding candidate. Each inner solve
also starts from the current outer flux for its group. These choices
reuse a plausible shape while checking the newly assembled equations.
[Regulator candidates](../../src/ReactorSim.Core/Domain/PracticeLiquidZoneRrsContracts.cs#L1420) ·
[Prepared solve](../../src/ReactorSim.Core/Domain/FullCoreDiffusionModelContracts.cs#L490) ·
[Prepared-input change](https://github.com/hkkho/CRS/commit/e0c839718a46dd30159c33b4785cbfd4ea421cb1)

The stencil stores at most six face terms per cell, and the operator compiles
the neighbor indices and conductances before iteration. A group application
visits the rows and their faces in a fixed order, with $O(N)$ work and
$O(N)$ operator storage for fixed stencil degree. The two group solves
exploit triangular coupling and use reusable scratch vectors. Each Jacobi
loop performs one initial operator application. After a correction, the
application used to check the new residual also supplies the operator value
for the next correction. With $m$ corrections this costs $m+1$
applications in place of the original $2m$. The outer convergence check still
applies both operators to the new coupled state.
[Operator compilation](../../src/ReactorSim.Core/Domain/SpatialOperatorContracts.cs#L1039) ·
[Residual-reuse change](https://github.com/hkkho/CRS/commit/15fc41460f9920d3cf73cec399c2a4a62626ee8e)

### 4.3 Browser execution and the five-second interval

Pages builds .NET in Release with ahead-of-time compilation (AOT) to
WebAssembly. The production host runs in one Web Worker with WASM threads
disabled and one CPU operator partition. AOT changes how managed work
executes. The worker keeps that work off the UI thread. The compact
response carries an authoritative snapshot patch and may also carry a
core replacement when the core changes. This reduces avoidable wire and
presentation work.
[6](#ref-6) · [Pages build](../../.github/workflows/deploy-candu-playtest.yml) ·
[Wire protocol](../spec/browser-playtest-protocol-v2.md)

The client computes at most one endpoint ahead and blends accepted
readings for presentation over 7.5 s by default, 10 s at slow speed, or
5 s at fast speed. Computation can overlap that display interval. If an
endpoint is late, the display holds the last solved state. The five-second
interval gives an upcoming fast-playback command a latency budget.
Startup and device coverage are separate measurements.
[Playback loop](../../web/candu-playtest/src/DemoLoop.ts)

## 5. Browser latency experiment

### 5.1 Method

The experiment used the local GitHub Pages build at the `/CRS/` base path
on Windows 11 Pro (build 26200), AMD Ryzen 7 5800X3D, .NET SDK 10.0.303,
and headless Playwright Chromium 151.0.7922.34. Both builds use the same
source revision, the default v11 pack and a single-threaded Release host
with profiling disabled. One build enables AOT and the other leaves it
disabled. The AOT run measures the full command stream. The non-AOT
comparison replays its first block for each seed.

For each of seeds 1001, 1002, and 1003, the benchmark initializes a new
production worker in `demo` mode. It excludes startup and one empty warm-up
half-day, then submits blocks of three compact `commit-day` commands
with zero, one, and two refuelling orders. The AOT run collects three
blocks per seed and the non-AOT run collects one. These are consecutive
half-days in each run. The harness checks acceptance, sequence, exact
elapsed simulation time, and fuel consumption. It records endpoint state
digests and solver diagnostics. Samples within each seed are serially
dependent. The production AOT timings were collected before the other
validation runs. The comparison build ran while validation was in progress.
[Benchmark script](../../web/candu-playtest/scripts/benchmark-demo-latency.mjs)

Elapsed time starts just before serializing and posting the request and
ends after receipt and JSON parsing on the page. It includes the worker's
WASM calls, any yield in the worker, snapshot mapping and serialization,
transport, and parsing. It excludes the app's subsequent patch
materialization, rendering and interpolation. The worker also reports its
own dispatch duration. Both timers aggregate work across every candidate
eigensolve in the command.
[Worker timing boundary](../../web/candu-playtest/src/wasmWorker.ts#L35) ·
[Application materialization](../../web/candu-playtest/src/bridge.ts#L451)

### 5.2 Results

| Refuelling orders per command | Warm AOT samples | AOT median (s) | AOT maximum (s) | AOT below 5 s |
| ---: | ---: | ---: | ---: | ---: |
| 0 | 9 | 1.913 | 2.036 | 9/9 |
| 1 | 9 | 3.825 | 4.054 | 9/9 |
| 2 | 9 | 3.939 | 4.201 | 9/9 |

One order adds the post-fuel equilibrium event to the empty-step workload.
The second order shares that event, although the perturbation and resulting
iteration count can differ. Across the AOT run, the worker timer accounted
for nearly all elapsed time. The remaining time includes message transport
and parsing. Compact responses still carried about 2.75–2.76 MB of UTF-8
JSON for these changing cores. Every measured endpoint recorded three
regulator candidate solves in its final event. The accepted endpoint
candidate took 24–70 outer iterations, and the largest reported endpoint
relative residual was $1.9974\times10^{-5}$. Those diagnostics describe
the endpoint and exclude work on a pre-exposure event.

| Refuelling orders per command | Matched samples per build | AOT median (s) | Non-AOT median (s) | Non-AOT maximum (s) |
| ---: | ---: | ---: | ---: | ---: |
| 0 | 3 | 1.689 | 12.620 | 14.421 |
| 1 | 3 | 3.634 | 27.865 | 30.674 |
| 2 | 3 | 4.118 | 32.244 | 34.920 |

The matched non-AOT medians were about 7.5–7.8 times the corresponding
AOT medians. All 12 paired endpoints, including warm-ups, had matching
state digests, endpoint candidate counts, and endpoint outer iteration
counts. This supports AOT as a major contributor to lower execution
time for this workload. The comparison uses three samples per row and
different background load, so the ratios are descriptive. The
27-command production result supports the five-second finding on this
desktop for the tested seeds and batches. The uncompressed native WASM
module was 22.6 MB with AOT and 1.50 MB without it. Cold download and
startup are separate from the warm command measurements.
[Per-command samples](../../benchmarks/diffusion-solver-latency-2026-11-09.csv)

An older instrumented real-time workload attributed 44.8% of the measured
scheduled-step dispatch time to operator applications and 0.05% to poison
integration. That profile used a different cadence and a build whose
instrumentation affected optimization. It helps explain the emphasis on
reducing spatial solves and stencil passes, while the present timings
measure the production command without profiling.
[Archived runtime profile](https://github.com/hkkho/CRS/blob/2015dd334782358e910681bbd10f25d8fb3cf2d7/docs/physics/runtime-profile.md)

### 5.3 Reproduction

From the repository root, stage the runtime and run the Pages build.
Start the preview in one terminal and run the benchmark in a second from
`web/candu-playtest`. To obtain the comparison build, repeat with
`-RunAOTCompilation` omitted and with fresh MSBuild outputs for the second
configuration. On Windows, a separate `ArtifactsPath` with
`UseArtifactsOutput=true` can isolate the build intermediates. Add `1`
after the URL to run one block per seed, as used for the non-AOT comparison.
The report contains raw millisecond samples and build metadata.
Isolate the timing run for a controlled comparison.
[Build and preview guide](../maintenance/hosting.md)

```powershell
# From the repository root
./tools/Build-BrowserWasm.ps1 -Configuration Release `
  -RunAOTCompilation -OmitPrecompressedAssets
cd web/candu-playtest
npm run build:pages
npm run preview -- --configLoader runner --outDir dist-pages `
  --base /CRS/ --host 127.0.0.1 --port 4183 --strictPort

# From web/candu-playtest in a second terminal
node scripts/benchmark-demo-latency.mjs http://127.0.0.1:4183/CRS/
```

## 6. Verification and limits of inference

For one fresh cell with six reflective faces, leakage vanishes. With
$\chi_1=1$ and $\chi_2=0$, the analytic solution is

$$
\begin{aligned}
\frac{\phi_2}{\phi_1} &= \frac{0.200}{0.160}=1.25,\\
k_\infty &= \frac{0.0856539290262777+1.25(0.37819461018325856)}
{0.30+0.20}=1.1167943835107017 .
\end{aligned}\tag{7}
$$

The regression drives the production eigen solver with the one-cell
stencil and tighter tolerances. At a 1 W target it checks $k$ to ten
decimal places, each flux to relative error $10^{-11}$, and relative
equation and power residuals at most $10^{-12}$. It tests group coupling,
zero leakage and normalization. Spatial tests add two- and three-cell
cases, canonical ordering, failure handling, and a 4,560-node
power-sum check. The radial and axial tests check the authored
boundary conductance laws. A full-core tolerance comparison at two
fixed zone fills tests $10^{-7}$ versus $10^{-8}$ inner relative tolerance.
It checks reactivity differences within 0.05 mk and cell heating
differences within $10^{-4}$ of peak reference cell power.
[Reflective fixture](../../tests/ReactorSim.Core.Tests/SingleCellReflectiveDiffusionFixtureTests.cs) ·
[Spatial tests](../../tests/ReactorSim.Core.Tests/SpatialSolveTests.cs) ·
[Radial tests](../../tests/ReactorSim.Core.Tests/CircularRadialBoundaryTests.cs) ·
[Axial tests](../../tests/ReactorSim.Core.Tests/AxialVacuumBoundaryTests.cs) ·
[Tolerance regression](../../tests/ReactorSim.Core.Tests/SpatialToleranceRegressionTests.cs)

Game and Browser tests check one post-batch equilibrium event, one
accepted half-day increment, deterministic replay, and rollback on
numerical failure. Together these checks cover analytic correctness for
small cases, behavior of the assembled discrete equations, and the
transaction that delivers their result. The one-cell case exercises zero
leakage. The full-core tests exercise the deployed mesh, while refinement
error and independent plant accuracy remain open. The v10 geometry and
v11 xenon comparisons are calibration evidence. The frozen-source half-day
approximates the time dependence between solved endpoints.
[Game step tests](../../tests/ReactorSim.Game.Tests/DailyTurnTests.cs) ·
[Browser demo tests](../../tests/ReactorSim.Browser.Tests/DemoModeTests.cs) ·
[Geometry fit](../../data/calibration/diffusion-v10/README.md) ·
[Xenon fit](xenon-calibration.md)

The Release Core rerun passed all 131 tests, including the prepared-input
comparison of coefficient digests, eigenvalue, flux and node power. An
earlier run had reported a coefficient digest mismatch. The focused
rerun and the complete rerun both passed that assertion. On the local
Pages AOT build, the direct WASM reproduction matrix matched all six
cold and warm case digests with no reported browser errors. That matrix
checks repeatability at a fixed build. The half-day benchmark uses the
production worker and tests a different command sequence. All 93 Release
Game tests, the focused Browser demo test, the Pages frontend build and
all 247 Vitest tests passed. The local gameplay smoke passed on rerun
after a Mono runtime assertion during seed retry in the first attempt.
Its fuel receipt check includes queueing and playback, outside the
command timer in Section 5.
[Prepared-input regression](../../tests/ReactorSim.Core.Tests/EquilibriumCoreSolverTests.cs#L37) ·
[Pages reproduction harness](../../web/candu-playtest/scripts/benchmark-wasm.mjs) ·
[Gameplay smoke](../../web/candu-playtest/scripts/demo-smoke.mjs)

## 7. Conclusion

At the tested operating point, CRS can spend a half-day of burnup and poison
exposure in one browser command while preserving cell-resolved neutron
balance at the solved endpoints. The latency headroom between the slowest
measured production command and the fast playback interval was about
0.80 s. That headroom applies to the worker command boundary on the measured
desktop. The temporal approximation lies between equilibria, while the
coupled residual and verification cases assess the equilibria themselves.

## Acknowledgment

Gemini 4 Argon reviewed the numerical argument and the attribution of
browser latency. The review informed the distinction between exact
power iteration, inexact acceptance, and complete worker timing.

## References

<a id="ref-1"></a>**[1]** Chaplin, R. “Genealogy of CANDU Reactors.”
*The Essential CANDU*, Chapter 2, UNENE, 2016.
[Chapter](https://www.unene.ca/essentialcandu/pdf/2%20-%20Genealogy%20of%20CANDU%20Reactors.pdf)

<a id="ref-2"></a>**[2]** MIT OpenCourseWare. “Criticality Conditions.”
*22.05 Neutron Science and Reactor Physics*, part 13, 2006.
[Lecture notes](https://dspace.mit.edu/bitstream/handle/1721.1/74136/22-05-fall-2006/contents/lecture-notes/lecture13.pdf)

<a id="ref-3"></a>**[3]** Bernal et al. “Resolution of the Generalized
Eigenvalue Problem in the Neutron Diffusion Equation Discretized by the
Finite Volume Method.” *Abstract and Applied Analysis*, 2014.
[doi:10.1155/2014/913043](https://doi.org/10.1155/2014/913043)

<a id="ref-4"></a>**[4]** Boyd, S. *Convex Optimization I*, lecture 6,
Stanford Engineering Everywhere.
[Perron–Frobenius theorem](https://see.stanford.edu/materials/lsocoee364a/transcripts/ConvexOptimizationI-Lecture06.html)

<a id="ref-5"></a>**[5]** Barrett, R. et al. *Templates for the Solution
of Linear Systems: Building Blocks for Iterative Methods*. SIAM, 1994.
[Jacobi method and convergence](https://www.netlib.org/templates/templates.html)

<a id="ref-6"></a>**[6]** Microsoft. “Features and configurations.”
*.NET WebAssembly runtime documentation*.
[Ahead-of-time compilation](https://github.com/dotnet/runtime/blob/main/src/mono/wasm/features.md#aot)
