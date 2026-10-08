using System;
using System.Collections.Generic;
using System.Linq;

namespace ReactorSim.Core
{
    /// <summary>Read-only equilibrium candidate with fixed liquid-zone fills.
    /// Includes the same burnup-reference replacement used by live Game trials.</summary>
    public sealed class PracticeXenonEquilibriumV1
    {
        private PracticeXenonEquilibriumV1(EquilibriumCoreProjectionV1 projection, PracticeXenonStateV1 poison)
        { Projection = projection; Poison = poison; }
        public EquilibriumCoreProjectionV1 Projection { get; }
        public PracticeXenonStateV1 Poison { get; }
        public static ContractValidationResult<PracticeXenonEquilibriumV1> TryCreate(
            EquilibriumCoreSolverV1 solver, SyntheticGameCoreStateV1 core, IReadOnlyList<double> fills,
            double simulationTimeSeconds = 0)
        {
            var mapping = PracticeLiquidZoneRrsMappingV1.TryCreateCandu6();
            if (!mapping.IsValid) return Invalid(mapping.FirstDiagnostic);
            var zone = mapping.Value.TryBuildOverlay(fills);
            if (!zone.IsValid) return Invalid(zone.FirstDiagnostic);
            var candidate = solver.TrySolveCandidate(core.EnumerateBundles(), zone.Value);
            if (!candidate.IsValid) return Invalid(candidate.FirstDiagnostic);
            for (int pass = 0; pass < 16; pass++)
            {
                var poison = PracticeXenonStateV1.CreateEquilibrium(core, candidate.Value, simulationTimeSeconds);
                var combined = StaticAbsorptionOverlayV1.TryCreate("fixed-zone-equilibrium-xenon-replacement",
                    zone.Value.Entries.Select(e => new StaticAbsorptionOverlayEntryV1(e.Node,
                        e.DeltaAbsorptionGroup1PerM,
                        e.DeltaAbsorptionGroup2PerM + poison.Overlay.GetDeltaAbsorptionGroup2PerM(e.Node))));
                if (!combined.IsValid) return Invalid(combined.FirstDiagnostic);
                candidate = solver.TrySolveCandidate(core.EnumerateBundles(), combined.Value);
                if (!candidate.IsValid) return Invalid(candidate.FirstDiagnostic);
                var next = PracticeXenonStateV1.CreateEquilibrium(core, candidate.Value, simulationTimeSeconds);
                double error = next.Xenon.Select((x, n) => Math.Abs(x - poison.Xenon[n]) / Math.Max(1, x)).Max();
                if (error <= Math.Max(1e-7, 4 * candidate.Value.DataPack.ConvergencePolicy.SourceShapeTolerance))
                    return ContractValidationResult<PracticeXenonEquilibriumV1>.Valid(
                        new PracticeXenonEquilibriumV1(candidate.Value, poison));
            }
            return ContractValidationResult<PracticeXenonEquilibriumV1>.Invalid(
                "PracticeXenonEquilibrium.NotConverged", "xenon", "Fixed-zone poison equilibrium did not settle.");
        }
        private static ContractValidationResult<PracticeXenonEquilibriumV1> Invalid(ContractDiagnostic failure) =>
            ContractValidationResult<PracticeXenonEquilibriumV1>.Invalid(failure.Code, failure.Path, failure.Message);
    }
}
