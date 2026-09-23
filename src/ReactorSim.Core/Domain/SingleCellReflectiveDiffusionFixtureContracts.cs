using System;
using System.Collections.Generic;
using System.Linq;

namespace ReactorSim.Core
{
    /// <summary>
    /// One fresh NAT-U-SYNTHETIC material node with six reflective faces.
    /// This is a small composition fixture for exercising the real spatial
    /// two-group eigen solver; its coefficients and node volume come from the
    /// embedded full-core data pack rather than from fixture constants.
    /// The embedded pack is project-authored synthetic surrogate data.
    /// </summary>
    public sealed class SingleCellReflectiveDiffusionFixtureV1
    {
        public const string FixtureId = "single-cell-reflective-nat-u-synthetic-v1";

        public const string MaterialVariant = "NAT-U-SYNTHETIC";

        public const double TargetPowerWatts = 1.0;

        private const double TightLinearAbsoluteResidualTolerance = 1.0e-13;
        private const double TightLinearRelativeResidualTolerance = 1.0e-13;
        private const int TightMaximumInnerIterations = 2048;
        private const double TightKAbsoluteTolerance = 1.0e-12;
        private const double TightKRelativeTolerance = 1.0e-12;
        private const double TightResidualTolerance = 1.0e-12;
        private const double TightSourceShapeTolerance = 1.0e-12;
        private const double TightPowerBalanceTolerance = 1.0e-12;
        private const int TightMaximumOuterIterations = 256;

        private SingleCellReflectiveDiffusionFixtureV1(
            FullCoreDiffusionDataPackV1 dataPack,
            BurnupCoefficientLookupResultV1 materialLookup,
            CoreTopology topology,
            SpatialStencil stencil,
            SpatialCoefficientSet coefficients,
            SpatialLinearSolvePolicy linearSolvePolicy,
            SpatialConvergencePolicy convergencePolicy)
        {
            DataPack = dataPack;
            MaterialLookup = materialLookup;
            Topology = topology;
            Stencil = stencil;
            Coefficients = coefficients;
            LinearSolvePolicy = linearSolvePolicy;
            ConvergencePolicy = convergencePolicy;
            FreshBurnupJPerKgHm = materialLookup.InputBurnupJPerKgHm;
        }

        public FullCoreDiffusionDataPackV1 DataPack { get; }

        public BurnupCoefficientLookupResultV1 MaterialLookup { get; }

        public MaterialVariantId MaterialVariantId
        {
            get { return MaterialLookup.MaterialVariantId; }
        }

        public double FreshBurnupJPerKgHm { get; }

        public CoreTopology Topology { get; }

        public SpatialStencil Stencil { get; }

        public SpatialCoefficientSet Coefficients { get; }

        public SpatialLinearSolvePolicy LinearSolvePolicy { get; }

        public SpatialConvergencePolicy ConvergencePolicy { get; }

        public static ContractValidationResult<SingleCellReflectiveDiffusionFixtureV1> TryCreate()
        {
            ContractValidationResult<FullCoreDiffusionDataPackV1> dataPackResult =
                FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6();
            if (!dataPackResult.IsValid)
            {
                return Invalid(dataPackResult.FirstDiagnostic);
            }

            return TryCreate(dataPackResult.Value);
        }

        public static ContractValidationResult<SingleCellReflectiveDiffusionFixtureV1> TryCreate(
            FullCoreDiffusionDataPackV1 dataPack)
        {
            if (dataPack == null)
            {
                return Invalid(
                    "SingleCellReflectiveFixture.DataPack.Missing",
                    "data_pack",
                    "A single-cell reflective fixture requires a validated diffusion data pack.");
            }

            BurnupCoefficientTableV1? table = dataPack.CoefficientTables
                .SingleOrDefault(candidate => string.Equals(
                    candidate.MaterialVariantId.Value,
                    MaterialVariant,
                    StringComparison.Ordinal));
            if (table == null)
            {
                return Invalid(
                    "SingleCellReflectiveFixture.MaterialTable.Missing",
                    "data_pack.coefficient_tables",
                    "The active data pack must contain the NAT-U-SYNTHETIC material table.");
            }

            if (table.Rows.Count == 0 || table.Rows[0].BurnupJPerKgHm != 0.0)
            {
                return Invalid(
                    "SingleCellReflectiveFixture.FreshRow.Missing",
                    "data_pack.coefficient_tables[NAT-U-SYNTHETIC].rows[0]",
                    "The NAT-U-SYNTHETIC table must begin with a burnup-zero fresh-fuel row.");
            }

            double freshBurnup = table.Rows[0].BurnupJPerKgHm;
            ContractValidationResult<BurnupCoefficientLookupResultV1> lookupResult =
                table.TryLookup(freshBurnup);
            if (!lookupResult.IsValid)
            {
                return Invalid(lookupResult.FirstDiagnostic);
            }

            ContractValidationResult<CoreTopology> topologyResult = CreateTopology();
            if (!topologyResult.IsValid)
            {
                return Invalid(topologyResult.FirstDiagnostic);
            }

            ContractValidationResult<SpatialStencil> stencilResult =
                SpatialStencil.TryCreate(topologyResult.Value);
            if (!stencilResult.IsValid)
            {
                return Invalid(stencilResult.FirstDiagnostic);
            }

            NodeKey node = new NodeKey(new ChannelId(0), new BundlePosition(0));
            BurnupCoefficientValuesV1 values = lookupResult.Value.Coefficients;
            var nodeCoefficients = new SpatialNodeCoefficients(
                node,
                dataPack.NodeVolumeM3,
                values.AbsorptionGroup1PerM,
                values.AbsorptionGroup2PerM,
                values.DownscatterGroup1To2PerM,
                values.FissionGroup1PerM,
                values.FissionGroup2PerM,
                values.NuFissionGroup1PerM,
                values.NuFissionGroup2PerM,
                values.ChiGroup1,
                values.ChiGroup2,
                values.EnergyPerFissionJ);

            SpatialBoundaryConductance[] boundaryConductances = stencilResult.Value.Nodes[0]
                .BoundaryTerms
                .Select(boundary => new SpatialBoundaryConductance(
                    node,
                    boundary.Face,
                    0.0,
                    0.0))
                .ToArray();
            ContractValidationResult<SpatialCoefficientSet> coefficientsResult =
                SpatialCoefficientSet.TryCreate(
                    stencilResult.Value,
                    new[] { nodeCoefficients },
                    Array.Empty<SpatialEdgeConductance>(),
                    boundaryConductances);
            if (!coefficientsResult.IsValid)
            {
                return Invalid(coefficientsResult.FirstDiagnostic);
            }

            ContractValidationResult<SpatialLinearSolvePolicy> linearPolicyResult =
                SpatialLinearSolvePolicy.TryCreate(
                    SpatialLinearSolvePolicy.DeterministicJacobiMethodId,
                    SpatialLinearSolvePolicy.DeterministicJacobiMethodVersion,
                    TightLinearAbsoluteResidualTolerance,
                    TightLinearRelativeResidualTolerance,
                    TightMaximumInnerIterations);
            if (!linearPolicyResult.IsValid)
            {
                return Invalid(linearPolicyResult.FirstDiagnostic);
            }

            ContractValidationResult<SpatialConvergencePolicy> convergencePolicyResult =
                SpatialConvergencePolicy.TryCreate(
                    TightKAbsoluteTolerance,
                    TightKRelativeTolerance,
                    TightResidualTolerance,
                    TightSourceShapeTolerance,
                    TightPowerBalanceTolerance,
                    TightMaximumOuterIterations);
            if (!convergencePolicyResult.IsValid)
            {
                return Invalid(convergencePolicyResult.FirstDiagnostic);
            }

            return ContractValidationResult<SingleCellReflectiveDiffusionFixtureV1>.Valid(
                new SingleCellReflectiveDiffusionFixtureV1(
                    dataPack,
                    lookupResult.Value,
                    topologyResult.Value,
                    stencilResult.Value,
                    coefficientsResult.Value,
                    linearPolicyResult.Value,
                    convergencePolicyResult.Value));
        }

        /// <summary>
        /// Creates the actual P2-T02 one-step source iteration used by the
        /// outer spatial eigen solve. Null initial flux selects its validated
        /// unit-flux seed in SpatialEigenIteration.
        /// </summary>
        public ContractValidationResult<SpatialEigenIteration> TryCreateIteration(
            double initialEigenvalue = 1.0,
            double[]? initialGroup1Flux = null,
            double[]? initialGroup2Flux = null)
        {
            return SpatialEigenIteration.TryCreate(
                Stencil,
                Coefficients,
                LinearSolvePolicy,
                TargetPowerWatts,
                initialEigenvalue,
                initialGroup1Flux,
                initialGroup2Flux);
        }

        /// <summary>
        /// Creates the actual bounded outer SpatialEigenSolve over this
        /// fixture's validated stencil and pack-bound coefficients.
        /// </summary>
        public ContractValidationResult<SpatialEigenSolve> TryCreateSolve(
            double initialEigenvalue = 1.0,
            double[]? initialGroup1Flux = null,
            double[]? initialGroup2Flux = null)
        {
            ContractValidationResult<SpatialEigenIteration> iterationResult =
                TryCreateIteration(initialEigenvalue, initialGroup1Flux, initialGroup2Flux);
            if (!iterationResult.IsValid)
            {
                return ContractValidationResult<SpatialEigenSolve>.Invalid(
                    iterationResult.FirstDiagnostic.Code,
                    iterationResult.FirstDiagnostic.Path,
                    iterationResult.FirstDiagnostic.Message);
            }

            return SpatialEigenSolve.TryCreate(
                iterationResult.Value,
                ConvergencePolicy);
        }

        /// <summary>
        /// Runs the real SpatialEigenSolve and returns its authoritative
        /// convergence state, fluxes, eigenvalue, and power diagnostics.
        /// </summary>
        public ContractValidationResult<SpatialSolveResult> TrySolve(
            double initialEigenvalue = 1.0,
            double[]? initialGroup1Flux = null,
            double[]? initialGroup2Flux = null)
        {
            ContractValidationResult<SpatialEigenSolve> solveResult =
                TryCreateSolve(initialEigenvalue, initialGroup1Flux, initialGroup2Flux);
            if (!solveResult.IsValid)
            {
                return ContractValidationResult<SpatialSolveResult>.Invalid(
                    solveResult.FirstDiagnostic.Code,
                    solveResult.FirstDiagnostic.Path,
                    solveResult.FirstDiagnostic.Message);
            }

            return solveResult.Value.TrySolve();
        }

        private static ContractValidationResult<CoreTopology> CreateTopology()
        {
            ChannelId channelId = new ChannelId(0);
            BundlePosition position = new BundlePosition(0);
            TopologyFace[] faces =
            {
                TopologyFace.North,
                TopologyFace.East,
                TopologyFace.South,
                TopologyFace.West,
                TopologyFace.EndA,
                TopologyFace.EndB
            };
            BoundaryFaceRecord[] boundaries = faces
                .Select(face => new BoundaryFaceRecord(
                    channelId,
                    position,
                    face,
                    BoundaryClassification.Reflective))
                .ToArray();
            var channel = new ChannelTopology(
                channelId,
                0,
                0,
                FlowDirection.EndAtoEndB,
                position,
                position,
                Array.Empty<NeighborRecord>(),
                boundaries);
            return CoreTopology.TryCreate(1, 1, new[] { channel });
        }

        private static ContractValidationResult<SingleCellReflectiveDiffusionFixtureV1> Invalid(
            ContractDiagnostic diagnostic)
        {
            return ContractValidationResult<SingleCellReflectiveDiffusionFixtureV1>.Invalid(
                diagnostic.Code,
                diagnostic.Path,
                diagnostic.Message);
        }

        private static ContractValidationResult<SingleCellReflectiveDiffusionFixtureV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<SingleCellReflectiveDiffusionFixtureV1>.Invalid(
                code,
                path,
                message);
        }
    }
}
