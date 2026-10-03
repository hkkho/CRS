# Equilibrium presentation and retained IQS research

The active Game/Browser consume `EquilibriumCoreProjectionV1` directly. Its
immutable arrays remain channel-major (380 channels × 12 positions), group 1
then group 2. Game presentation reads shape power/flux, effective k/reactivity,
solver diagnostics and binding digests; Browser additionally uses the accepted
projection object identity for detailed-core caching and its coefficient,
inventory and reactivity bindings for compact replacement decisions. No kinetics
candidate or adjoint compatibility result is constructed for equilibrium solves.
The projection is mapped at the accepted Game snapshot boundary; unchanged
projection identity is retained through rejected commands and clock-only updates.

`src/ReactorSim.Core.Research` retains `IqsSpatialCandidateV1` and
`IqsFullCoreSolver` in the original namespace for `BurnupAndPowerTests` research
coverage. Core tests explicitly reference this project; Game, Browser and WASM
Host do not. The original source was moved without numerical changes. The IQS
metadata pack remains in Core because adjoint/diffusion public research APIs
consume `IqsKineticsDataPackV1`; its resource identity and group ordering remain
unchanged. `AdjointContracts`, `AdjointReactivityContracts` and the kinetics-pack
parameter on `FullCoreDiffusionModelContracts` are retained consumers, not grounds
for wholesale deletion. Their wider classification belongs to task 21.

Active invariant coverage remains in equilibrium/burnup tests, Practice Xenon
and RRS tests, Game refuelling/rejection tests, and Browser compact-core/replay
checks. The two-seed phase-4 command corpus compares complete serialized response
byte hashes to its pre-refactor baseline, rather than substituting new expected
values. Protocol names, SI units, digests and omitted/null fields are unchanged.
