# CRS neutron diffusion solver workflow

This diagram follows one equilibrium candidate on the 4,560-cell core.
Group 1 is fast and group 2 is thermal. The outer power iteration updates
the fission source, multiplication factor and power-normalized flux.
Each group uses the same Jacobi procedure to solve its fixed-source
equation. [Solver paper](neutron-diffusion-paper.md) ·
[Rendered diagram](neutron-diffusion-workflow.png)

```mermaid
---
title: CRS two-group neutron diffusion
config:
  theme: base
  look: classic
  layout: dagre
  htmlLabels: true
  themeVariables:
    fontFamily: "Segoe UI, sans-serif"
    edgeLabelBackground: "#ffffff"
  flowchart:
    curve: basis
    wrappingWidth: 420
    nodeSpacing: 30
    rankSpacing: 28
---
flowchart LR
    accTitle: CRS two-group neutron diffusion workflow
    accDescr: Assemble a fixed candidate on the full core, then iterate the fission source. Solve fast flux first and use its downscatter to solve thermal flux. Both group solves use Jacobi iteration. A failed inner solve aborts the outer candidate. After both group solves succeed, update multiplication and normalize thermal power before checking convergence.

    subgraph Outer["Full-core equilibrium candidate"]
        direction TB
        Input["<b>01 · Assemble the candidate</b><br/>Freeze coefficients across 4,560 cells and 2 energy groups.<br/>Warm-start flux and k from a compatible solve when available."]
        Operator["$$\begin{gathered}\textbf{Finite-volume neutron balance}\\\\\mathbf J_g=-D_g\nabla\phi_g,\qquad R_1=\Sigma_{a1}+\Sigma_{s12},\quad R_2=\Sigma_{a2}\\\\(A_g\phi_g)_i=R_{g,i}\phi_{g,i}+\frac{1}{V_i}\left[\sum_{j\sim i}C_{g,ij}(\phi_{g,i}-\phi_{g,j})+\sum_b C_{g,ib}\phi_{g,i}\right]\\\\\text{Vacuum leakage at exterior faces · zero current at reflective faces}\end{gathered}$$"]
        Source["$$\begin{gathered}\textbf{02 · Form the fission source}\\\\F_i=\nu\Sigma_{f1,i}\phi_{1,i}+\nu\Sigma_{f2,i}\phi_{2,i}\end{gathered}$$"]
        Fast["$$\begin{gathered}\textbf{03 · Solve fast group}\\\\A_1\widetilde\phi_1=s_1,\qquad s_1=\chi_1 F/k\end{gathered}$$"]
        Thermal["$$\begin{gathered}\textbf{04 · Solve thermal group}\\\\A_2\widetilde\phi_2=s_2,\qquad s_2=\Sigma_{s12}\widetilde\phi_1+\chi_2 F/k\end{gathered}$$"]
        Normalize["$$\begin{gathered}\textbf{05 · Update multiplication and normalize power}\\\\T(\phi)=\sum_i V_iF_i(\phi),\qquad k^+=k\,\frac{T(\widetilde\phi)}{T(\phi)}\\\\P(\phi)=\sum_i V_i E_{f,i}\sum_g\Sigma_{fg,i}\phi_{g,i},\qquad \phi^+=\frac{P_\star}{P(\widetilde\phi)}\,\widetilde\phi\end{gathered}$$"]
        Check["<b>06 · Check the normalized state</b><br/>Absolute or relative change in k · coupled residual<br/>Source-shape change · relative power error"]
        OuterGate{"All checks<br/>pass?"}
        Accepted["<b>Converged</b><br/>Return k, fluxes and cell powers"]
        OuterFail["<b>Outer cap reached</b><br/>Nonconverged · no usable state"]

        Input --> Operator --> Source --> Fast
        Fast -->|"New fast flux supplies downscatter"| Thermal
        Thermal --> Normalize --> Check --> OuterGate
        OuterGate -->|"Yes"| Accepted
        OuterGate -->|"No · budget remains"| Source
        OuterGate -->|"No · 1,600 iterations used"| OuterFail
    end

    subgraph Inner["Fixed-source Jacobi procedure · both groups"]
        direction TB
        Seed["<b>Start the group solve</b><br/>Use the current outer iterate as the initial flux"]
        Sweep["$$\begin{gathered}\textbf{Jacobi sweep}\\\\\phi_g^{m+1}=\phi_g^m+\mathcal D_g^{-1}(s_g-A_g\phi_g^m)\\\\\mathcal D_g=\operatorname{diag}(A_g)\end{gathered}$$"]
        Residual["$$\begin{gathered}\textbf{Recompute the residual}\\\\e_g=\|A_g\phi_g^{m+1}-s_g\|_\infty\\\\r_g=\frac{e_g}{\big\|\,|A_g\phi_g^{m+1}|+|s_g|\,\big\|_\infty}\\\\e_g\le 10^{-11}\quad\text{or}\quad r_g\le 10^{-7}\end{gathered}$$"]
        InnerGate{"Residual<br/>accepted?"}
        Return["Return the trial group flux"]
        InnerFail["<b>Return the inner diagnostic</b><br/>InnerSolve.Nonconverged"]
        Abort["<b>Abort the outer candidate</b><br/>Failed · inner_solve_failed<br/>No usable state"]
        Seed --> Sweep --> Residual --> InnerGate
        InnerGate -->|"Yes"| Return
        InnerGate -->|"No · under 128 sweeps"| Sweep
        InnerGate -->|"No · cap reached"| InnerFail
        InnerFail --> Abort
    end

    Outer -.-|"Fast and thermal solves use this procedure"| Inner

    classDef model fill:#eef5ff,stroke:#6685ad,color:#10233c
    classDef fast fill:#e6f0ff,stroke:#547ab1,color:#10233c
    classDef thermal fill:#e7f6f2,stroke:#448878,color:#102b28
    classDef method fill:#fff6e0,stroke:#a88335,color:#392a0f
    classDef pass fill:#e8f5e8,stroke:#4f8956,color:#173820
    classDef fail fill:#fceeee,stroke:#b46464,color:#492323
    class Input,Operator,Source,Normalize,Check,OuterGate model
    class Fast fast
    class Thermal thermal
    class Seed,Sweep,Residual,InnerGate,Return method
    class Accepted pass
    class InnerFail,Abort,OuterFail fail
    style Outer fill:#f9fbff,stroke:#b3c2d6,color:#10233c
    style Inner fill:#fffdf7,stroke:#beaa76,color:#392a0f
    linkStyle default stroke:#64748b,stroke-width:1.5px
```

The iteration normalizes the supplied or default initial flux to target power before forming the first source. The solver sets $r_g$ to zero when its denominator is zero. The outer gate
requires either the absolute or relative $k$ tolerance and all three other
checks. Invalid numerical states also fail closed.
The iteration limits and tolerances shown come from the default pack.
[Jacobi implementation](../../src/ReactorSim.Core/Domain/SpatialEigenIterationContracts.cs#L681) ·
[Inner failure propagation](../../src/ReactorSim.Core/Domain/SpatialConvergenceContracts.cs#L407) ·
[Outer convergence check](../../src/ReactorSim.Core/Domain/SpatialConvergenceContracts.cs#L525)

The active core uses equal cell volumes and one reciprocal conductance per
interior face, chosen from the pack by axial or transverse orientation.
With unequal cell volumes, multiplying each group balance by its own cell
volume gives the symmetric form $\operatorname{diag}(V_i)A_g$.
[Face assembly](../../src/ReactorSim.Core/Domain/FullCoreDiffusionModelContracts.cs#L1644) ·
[Active boundary conditions](active-two-group-solver.md)
