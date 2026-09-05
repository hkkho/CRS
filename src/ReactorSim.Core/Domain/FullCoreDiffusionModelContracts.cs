using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ReactorSim.Core
{
    /// <summary>
    /// A converged full-core two-group diffusion result in canonical
    /// channel-major, bundle-position order. Fluxes are retained so a later
    /// solve can warm-start without changing the deterministic equations.
    /// </summary>
    public sealed class FullCoreDiffusionSolveResultV1
    {
        private readonly ReadOnlyCollection<double> _group1Flux;
        private readonly ReadOnlyCollection<double> _group2Flux;
        private readonly ReadOnlyCollection<double> _nodePowerWatts;

        internal FullCoreDiffusionSolveResultV1(
            FullCoreDiffusionDataPackV1 dataPack,
            SpatialSolveResult spatialSolve,
            SpatialCoefficientSet coefficients,
            Digest32 inventoryBindingDigest,
            Digest32 coefficientBindingDigest,
            IEnumerable<double> group1Flux,
            IEnumerable<double> group2Flux,
            IEnumerable<double> nodePowerWatts,
            double totalPowerWatts,
            double effectiveK,
            double reactivity,
            double powerBalanceRelativeError)
        {
            DataPack = dataPack;
            SpatialSolve = spatialSolve;
            Coefficients = coefficients;
            InventoryBindingDigest = inventoryBindingDigest;
            CoefficientBindingDigest = coefficientBindingDigest;
            _group1Flux = new ReadOnlyCollection<double>(group1Flux.ToArray());
            _group2Flux = new ReadOnlyCollection<double>(group2Flux.ToArray());
            _nodePowerWatts = new ReadOnlyCollection<double>(nodePowerWatts.ToArray());
            TotalPowerWatts = totalPowerWatts;
            EffectiveK = effectiveK;
            Reactivity = reactivity;
            PowerBalanceRelativeError = powerBalanceRelativeError;
        }

        public FullCoreDiffusionDataPackV1 DataPack { get; }

        public SpatialSolveResult SpatialSolve { get; }

        /// <summary>
        /// The immutable material rows used by this solve. Core owns this
        /// nodewise contract; presentation layers receive only derived
        /// scalar diagnostics.
        /// </summary>
        public SpatialCoefficientSet Coefficients { get; }

        public Digest32 InventoryBindingDigest { get; }

        public Digest32 CoefficientBindingDigest { get; }

        public IReadOnlyList<double> Group1Flux
        {
            get { return _group1Flux; }
        }

        public IReadOnlyList<double> Group2Flux
        {
            get { return _group2Flux; }
        }

        public IReadOnlyList<double> NodePowerWatts
        {
            get { return _nodePowerWatts; }
        }

        public double TotalPowerWatts { get; }

        public double EffectiveK { get; }

        public double Reactivity { get; }

        public double PowerBalanceRelativeError { get; }

        public int IterationCount
        {
            get { return SpatialSolve.Diagnostics.IterationCount; }
        }

        public double ResidualRelativeInfinity
        {
            get { return SpatialSolve.Diagnostics.ResidualRelativeInfinity ?? 0.0; }
        }

        public string SolverIdentity
        {
            get { return DataPack.SolverId + "/" + DataPack.Descriptor.DataPackVersion; }
        }
    }

    /// <summary>
    /// Shared full-core solver adapter used by the Game and Browser layers.
    /// It assembles the 380 by 12 CANDU-6 stencil once, binds material rows
    /// from the selected pack for every live bundle, and delegates the actual
    /// two-group solve to the existing deterministic SpatialEigenSolve.
    /// </summary>
    public sealed class FullCoreDiffusionModelV1
    {
        private readonly CoreTopology _topology;
        private readonly SpatialStencil _stencil;
        private readonly FullCoreDiffusionDataPackV1 _dataPack;
        private readonly ReadOnlyCollection<SpatialEdgeConductance> _edgeConductances;
        private readonly ReadOnlyCollection<SpatialBoundaryConductance> _boundaryConductances;

        private FullCoreDiffusionModelV1(
            CoreTopology topology,
            SpatialStencil stencil,
            FullCoreDiffusionDataPackV1 dataPack,
            IEnumerable<SpatialEdgeConductance> edgeConductances,
            IEnumerable<SpatialBoundaryConductance> boundaryConductances)
        {
            _topology = topology;
            _stencil = stencil;
            _dataPack = dataPack;
            _edgeConductances = new ReadOnlyCollection<SpatialEdgeConductance>(
                edgeConductances.ToArray());
            _boundaryConductances = new ReadOnlyCollection<SpatialBoundaryConductance>(
                boundaryConductances.ToArray());
        }

        public FullCoreDiffusionDataPackV1 DataPack
        {
            get { return _dataPack; }
        }

        public CoreTopology Topology
        {
            get { return _topology; }
        }

        public SpatialStencil Stencil
        {
            get { return _stencil; }
        }

        public int NodeCount
        {
            get { return _stencil.NodeCount; }
        }

        public static ContractValidationResult<FullCoreDiffusionModelV1> TryCreateCandu6(
            FullCoreDiffusionDataPackV1 dataPack)
        {
            if (dataPack == null)
            {
                return ContractValidationResult<FullCoreDiffusionModelV1>.Invalid(
                    "FullCoreDiffusionModel.DataPack.Missing",
                    "data_pack",
                    "A full-core diffusion model requires a validated data pack.");
            }

            ContractValidationResult<CoreTopology> topologyResult =
                Candu6CoreTopologyFactoryV1.TryCreate();
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

            return TryCreate(dataPack, topologyResult.Value, stencilResult.Value);
        }

        public static ContractValidationResult<FullCoreDiffusionModelV1> TryCreate(
            FullCoreDiffusionDataPackV1 dataPack,
            CoreTopology topology,
            SpatialStencil stencil)
        {
            if (dataPack == null)
            {
                return ContractValidationResult<FullCoreDiffusionModelV1>.Invalid(
                    "FullCoreDiffusionModel.DataPack.Missing",
                    "data_pack",
                    "A full-core diffusion model requires a validated data pack.");
            }

            if (topology == null)
            {
                return ContractValidationResult<FullCoreDiffusionModelV1>.Invalid(
                    "FullCoreDiffusionModel.Topology.Missing",
                    "topology",
                    "A full-core diffusion model requires a validated topology.");
            }

            if (stencil == null)
            {
                return ContractValidationResult<FullCoreDiffusionModelV1>.Invalid(
                    "FullCoreDiffusionModel.Stencil.Missing",
                    "stencil",
                    "A full-core diffusion model requires an assembled stencil.");
            }

            ContractValidationResult<bool> compatibility =
                dataPack.Descriptor.ValidateCompatibility(topology);
            if (!compatibility.IsValid)
            {
                return Invalid(compatibility.FirstDiagnostic);
            }

            if (!string.Equals(
                    dataPack.Descriptor.TopologySchemaId,
                    Candu6CoreTopologyFactoryV1.TopologySchemaId,
                    StringComparison.Ordinal) ||
                stencil.ChannelCount != Candu6CoreTopologyFactoryV1.ChannelCount ||
                stencil.BundlePositionCount != Candu6CoreTopologyFactoryV1.BundlePositionCount ||
                stencil.NodeCount != topology.SlotCount)
            {
                return ContractValidationResult<FullCoreDiffusionModelV1>.Invalid(
                    "FullCoreDiffusionModel.Topology.Incompatible",
                    "topology",
                    "The model requires the canonical CANDU-6 380 by 12 topology and stencil.");
            }

            List<SpatialEdgeConductance> edges = BuildEdgeConductances(
                stencil,
                dataPack,
                out ContractDiagnostic? edgeFailure);
            if (edgeFailure != null)
            {
                return Invalid(edgeFailure);
            }

            List<SpatialBoundaryConductance> boundaries = BuildBoundaryConductances(
                stencil,
                dataPack,
                out ContractDiagnostic? boundaryFailure);
            if (boundaryFailure != null)
            {
                return Invalid(boundaryFailure);
            }

            return ContractValidationResult<FullCoreDiffusionModelV1>.Valid(
                new FullCoreDiffusionModelV1(
                    topology,
                    stencil,
                    dataPack,
                    edges,
                    boundaries));
        }

        public ContractValidationResult<FullCoreDiffusionSolveResultV1> TrySolve(
            IEnumerable<BundleState> bundles,
            double targetPowerWatts,
            double initialEigenvalue = 1.0,
            IReadOnlyList<double>? initialGroup1Flux = null,
            IReadOnlyList<double>? initialGroup2Flux = null)
        {
            if (bundles == null)
            {
                return InvalidSolve(
                    "FullCoreDiffusionSolve.Bundles.Missing",
                    "bundles",
                    "A full-core solve requires one live bundle for every spatial node.");
            }

            if (!ContractValidation.IsFinite(targetPowerWatts) || targetPowerWatts <= 0.0)
            {
                return InvalidSolve(
                    "FullCoreDiffusionSolve.TargetPower.Invalid",
                    "target_power_w",
                    "The full-core target power must be finite and strictly positive SI watts.");
            }

            if ((initialGroup1Flux == null) != (initialGroup2Flux == null))
            {
                return InvalidSolve(
                    "FullCoreDiffusionSolve.InitialFlux.Incomplete",
                    "initial_flux",
                    "Both warm-start flux vectors must be supplied together or both omitted.");
            }

            BundleState[] bundleRecords = bundles.ToArray();
            ContractValidationResult<BundleInventory> inventoryResult =
                BundleInventory.TryCreate(_topology, bundleRecords);
            if (!inventoryResult.IsValid)
            {
                return InvalidSolve(
                    inventoryResult.FirstDiagnostic.Code,
                    inventoryResult.FirstDiagnostic.Path,
                    inventoryResult.FirstDiagnostic.Message);
            }

            ContractValidationResult<SpatialCoefficientSet> coefficientResult =
                BuildCoefficientSet(inventoryResult.Value);
            if (!coefficientResult.IsValid)
            {
                return InvalidSolve(
                    coefficientResult.FirstDiagnostic.Code,
                    coefficientResult.FirstDiagnostic.Path,
                    coefficientResult.FirstDiagnostic.Message);
            }

            double[]? group1WarmStart = initialGroup1Flux?.ToArray();
            double[]? group2WarmStart = initialGroup2Flux?.ToArray();
            ContractValidationResult<SpatialEigenIteration> iterationResult =
                SpatialEigenIteration.TryCreate(
                    _stencil,
                    coefficientResult.Value,
                    _dataPack.LinearSolvePolicy,
                    targetPowerWatts,
                    initialEigenvalue,
                    group1WarmStart,
                    group2WarmStart);
            if (!iterationResult.IsValid)
            {
                return InvalidSolve(
                    iterationResult.FirstDiagnostic.Code,
                    iterationResult.FirstDiagnostic.Path,
                    iterationResult.FirstDiagnostic.Message);
            }

            ContractValidationResult<SpatialEigenSolve> solveWrapperResult =
                SpatialEigenSolve.TryCreate(
                    iterationResult.Value,
                    _dataPack.ConvergencePolicy);
            if (!solveWrapperResult.IsValid)
            {
                return InvalidSolve(
                    solveWrapperResult.FirstDiagnostic.Code,
                    solveWrapperResult.FirstDiagnostic.Path,
                    solveWrapperResult.FirstDiagnostic.Message);
            }

            ContractValidationResult<SpatialSolveResult> spatialResult =
                solveWrapperResult.Value.TrySolve();
            if (!spatialResult.IsValid)
            {
                return InvalidSolve(
                    spatialResult.FirstDiagnostic.Code,
                    spatialResult.FirstDiagnostic.Path,
                    spatialResult.FirstDiagnostic.Message);
            }

            if (!spatialResult.Value.IsConverged || spatialResult.Value.FinalState == null)
            {
                ContractDiagnostic? failure = spatialResult.Value.Diagnostics.FailureDiagnostic;
                SpatialSolveDiagnostics diagnostics = spatialResult.Value.Diagnostics;
                string failureText = failure == null ? string.Empty : " " + failure.Message;
                return InvalidSolve(
                    "FullCoreDiffusionSolve.NotConverged",
                    "spatial_solve",
                    "The full-core diffusion solve did not converge within the pack policy after " +
                    diagnostics.IterationCount.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                    " outer iterations (k delta=" +
                    FormatDiagnosticValue(diagnostics.EigenvalueChangeRelative) +
                    ", residual=" + FormatDiagnosticValue(diagnostics.ResidualRelativeInfinity) +
                    ", shape=" + FormatDiagnosticValue(diagnostics.SourceShapeChangeInfinity) +
                    ")." + failureText);
            }

            SpatialEigenIterationState finalState = spatialResult.Value.FinalState;
            var nodePowerWatts = new double[_stencil.NodeCount];
            double totalPowerWatts = 0.0;
            for (int nodeIndex = 0; nodeIndex < _stencil.NodeCount; nodeIndex++)
            {
                SpatialNodeCoefficients coefficients = coefficientResult.Value.Nodes[nodeIndex];
                double fissionRate =
                    coefficients.FissionGroup1PerM * finalState.Group1Flux[nodeIndex] +
                    coefficients.FissionGroup2PerM * finalState.Group2Flux[nodeIndex];
                double power = coefficients.VolumeM3 * coefficients.EnergyPerFissionJ * fissionRate;
                if (!ContractValidation.IsFinite(fissionRate) || fissionRate < 0.0 ||
                    !ContractValidation.IsFinite(power) || power < 0.0)
                {
                    return InvalidSolve(
                        "FullCoreDiffusionSolve.Power.NonFinite",
                        ContractValidation.NodePath(_stencil.Nodes[nodeIndex].Node, ".power_w"),
                        "The converged node power must be finite and nonnegative.");
                }

                nodePowerWatts[nodeIndex] = power;
                totalPowerWatts += power;
                if (!ContractValidation.IsFinite(totalPowerWatts))
                {
                    return InvalidSolve(
                        "FullCoreDiffusionSolve.Power.TotalNonFinite",
                        "total_power_w",
                        "The ordered node-power reduction became non-finite.");
                }
            }

            double effectiveK = finalState.Eigenvalue;
            double reactivity = (effectiveK - 1.0) / effectiveK;
            double powerBalanceRelativeError =
                spatialResult.Value.Diagnostics.PowerBalanceRelative ??
                Math.Abs(totalPowerWatts - targetPowerWatts) / targetPowerWatts;
            if (!ContractValidation.IsFinite(totalPowerWatts) || totalPowerWatts <= 0.0 ||
                !ContractValidation.IsFinite(effectiveK) || effectiveK <= 0.0 ||
                !ContractValidation.IsFinite(reactivity) ||
                !ContractValidation.IsFinite(powerBalanceRelativeError) ||
                powerBalanceRelativeError < 0.0)
            {
                return InvalidSolve(
                    "FullCoreDiffusionSolve.Result.Invalid",
                    "result",
                    "The converged full-core result contained an invalid scalar metric.");
            }

            return ContractValidationResult<FullCoreDiffusionSolveResultV1>.Valid(
                new FullCoreDiffusionSolveResultV1(
                    _dataPack,
                    spatialResult.Value,
                    coefficientResult.Value,
                    FullCoreAdjointImportanceV1.ComputeReferenceStateDigest(
                        _dataPack,
                        inventoryResult.Value),
                    AdjointWeightedReactivityV1.ComputeCoefficientBindingDigest(
                        _dataPack,
                        coefficientResult.Value),
                    finalState.Group1Flux,
                    finalState.Group2Flux,
                    nodePowerWatts,
                    totalPowerWatts,
                    effectiveK,
                    reactivity,
                    powerBalanceRelativeError));
        }

        /// <summary>
        /// Solves the deterministic two-group transpose eigenproblem for the
        /// supplied full-core reference inventory.  The result is bound to
        /// both the diffusion and ordered kinetics pack identities so the
        /// caller cannot accidentally normalize a shape with a stale or
        /// differently ordered importance field.
        /// </summary>
        public ContractValidationResult<FullCoreAdjointImportanceV1> TrySolveReferenceAdjoint(
            IEnumerable<BundleState> bundles,
            IqsKineticsDataPackV1 kineticsDataPack,
            double referenceEigenvalue)
        {
            if (bundles == null)
            {
                return InvalidAdjoint(
                    "FullCoreAdjoint.Bundles.Missing",
                    "bundles",
                    "A reference adjoint requires one live bundle for every spatial node.");
            }

            if (kineticsDataPack == null)
            {
                return InvalidAdjoint(
                    "FullCoreAdjoint.KineticsDataPack.Missing",
                    "kinetics_data_pack",
                    "A reference adjoint requires a validated ordered kinetics pack.");
            }

            if (!ContractValidation.IsFinite(referenceEigenvalue) || referenceEigenvalue <= 0.0)
            {
                return InvalidAdjoint(
                    "FullCoreAdjoint.ReferenceEigenvalue.Invalid",
                    "reference_eigenvalue",
                    "The reference eigenvalue must be finite and strictly positive.");
            }

            if (!EnergyGroupsMatch(
                    _dataPack.EnergyGroupOrder,
                    kineticsDataPack.EnergyGroupOrder))
            {
                return InvalidAdjoint(
                    "FullCoreAdjoint.EnergyGroupOrder.Mismatch",
                    "energy_group_order",
                    "The diffusion and kinetics packs must use the exact canonical [fast, thermal] order.");
            }

            ContractValidationResult<BundleInventory> inventoryResult =
                BundleInventory.TryCreate(_topology, bundles);
            if (!inventoryResult.IsValid)
            {
                return InvalidAdjoint(
                    inventoryResult.FirstDiagnostic.Code,
                    inventoryResult.FirstDiagnostic.Path,
                    inventoryResult.FirstDiagnostic.Message);
            }

            if (inventoryResult.Value.OccupiedCount != _stencil.NodeCount)
            {
                return InvalidAdjoint(
                    "FullCoreAdjoint.Inventory.Incomplete",
                    "bundles",
                    "The reference adjoint requires a bundle bound to every canonical spatial node.");
            }

            ContractValidationResult<SpatialCoefficientSet> coefficientResult =
                BuildCoefficientSet(inventoryResult.Value);
            if (!coefficientResult.IsValid)
            {
                return InvalidAdjoint(
                    coefficientResult.FirstDiagnostic.Code,
                    coefficientResult.FirstDiagnostic.Path,
                    coefficientResult.FirstDiagnostic.Message);
            }

            ContractValidationResult<SpatialAdjointSolveResultV1> solveResult =
                SpatialAdjointEigenSolve.TrySolve(
                    _stencil,
                    coefficientResult.Value,
                    _dataPack.LinearSolvePolicy,
                    _dataPack.ConvergencePolicy,
                    referenceEigenvalue,
                    kineticsDataPack.GroupVelocitiesMPerSecond,
                    kineticsDataPack.EnergyGroupOrder);
            if (!solveResult.IsValid)
            {
                return InvalidAdjoint(
                    solveResult.FirstDiagnostic.Code,
                    solveResult.FirstDiagnostic.Path,
                    solveResult.FirstDiagnostic.Message);
            }

            Digest32 referenceStateDigest = FullCoreAdjointImportanceV1.ComputeReferenceStateDigest(
                _dataPack,
                inventoryResult.Value);
            return FullCoreAdjointImportanceV1.TryCreate(
                _dataPack,
                kineticsDataPack,
                _topology,
                _stencil,
                referenceStateDigest,
                solveResult.Value);
        }

        private ContractValidationResult<SpatialCoefficientSet> BuildCoefficientSet(
            BundleInventory inventory)
        {
            var tables = _dataPack.CoefficientTables.ToDictionary(
                table => table.MaterialVariantId,
                table => table);
            var nodeCoefficients = new List<SpatialNodeCoefficients>(_stencil.NodeCount);

            foreach (SpatialNodeStencil node in _stencil.Nodes)
            {
                BundleState? bundle = inventory.Slots[node.FlatIndex];
                if (bundle == null)
                {
                    return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                        "FullCoreDiffusionSolve.Inventory.Bundle.Missing",
                        ContractValidation.NodePath(node.Node, ".bundle_id"),
                        "Every full-core spatial node must contain a live bundle.");
                }

                if (!tables.TryGetValue(bundle.MaterialVariantId, out BurnupCoefficientTableV1? table))
                {
                    return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                        "FullCoreDiffusionSolve.Table.Missing",
                        "bundle[" + bundle.BundleId + "].material_variant_id",
                        "The data pack has no burnup table for the live material variant.");
                }

                double burnup = bundle.CurrentBurnupJPerKgHm;
                if (!ContractValidation.IsFinite(burnup) || burnup < 0.0)
                {
                    return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                        "FullCoreDiffusionSolve.Burnup.Invalid",
                        "bundle[" + bundle.BundleId + "].burnup_j_per_kg_hm",
                        "Live bundle burnup must be finite and nonnegative SI J/kg_HM.");
                }

                ContractValidationResult<BurnupCoefficientLookupResultV1> lookup =
                    table.TryLookup(burnup);
                if (!lookup.IsValid)
                {
                    return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                        lookup.FirstDiagnostic.Code,
                        "bundle[" + bundle.BundleId + "]." + lookup.FirstDiagnostic.Path,
                        lookup.FirstDiagnostic.Message);
                }

                BurnupCoefficientValuesV1 values = lookup.Value.Coefficients;
                nodeCoefficients.Add(new SpatialNodeCoefficients(
                    node.Node,
                    _dataPack.NodeVolumeM3,
                    values.AbsorptionGroup1PerM,
                    values.AbsorptionGroup2PerM,
                    values.DownscatterGroup1To2PerM,
                    values.FissionGroup1PerM,
                    values.FissionGroup2PerM,
                    values.NuFissionGroup1PerM,
                    values.NuFissionGroup2PerM,
                    values.ChiGroup1,
                    values.ChiGroup2,
                    values.EnergyPerFissionJ));
            }

            return SpatialCoefficientSet.TryCreate(
                _stencil,
                nodeCoefficients,
                _edgeConductances,
                _boundaryConductances);
        }

        private static List<SpatialEdgeConductance> BuildEdgeConductances(
            SpatialStencil stencil,
            FullCoreDiffusionDataPackV1 dataPack,
            out ContractDiagnostic? failure)
        {
            failure = null;
            var edges = new List<SpatialEdgeConductance>();
            var seen = new HashSet<SpatialEdgeKey>();
            foreach (SpatialNodeStencil node in stencil.Nodes)
            {
                foreach (SpatialNeighborTerm neighbor in node.NeighborTerms)
                {
                    SpatialEdgeKey key = new SpatialEdgeKey(node.Node, neighbor.TargetNode);
                    if (!seen.Add(key))
                    {
                        continue;
                    }

                    TwoGroupConductanceV1 conductance =
                        IsAxial(neighbor.Direction)
                            ? dataPack.AxialConductance
                            : dataPack.TransverseConductance;
                    edges.Add(new SpatialEdgeConductance(
                        key.First,
                        key.Second,
                        conductance.Group1M2,
                        conductance.Group2M2));
                }
            }

            return edges;
        }

        private static List<SpatialBoundaryConductance> BuildBoundaryConductances(
            SpatialStencil stencil,
            FullCoreDiffusionDataPackV1 dataPack,
            out ContractDiagnostic? failure)
        {
            failure = null;
            var boundaries = new List<SpatialBoundaryConductance>();
            foreach (SpatialNodeStencil node in stencil.Nodes)
            {
                foreach (SpatialBoundaryTerm boundary in node.BoundaryTerms)
                {
                    if (boundary.Classification == BoundaryClassification.Reflective)
                    {
                        boundaries.Add(new SpatialBoundaryConductance(
                            node.Node,
                            boundary.Face,
                            0.0,
                            0.0));
                        continue;
                    }

                    TwoGroupConductanceV1 conductance = dataPack.VacuumBoundaryConductance;
                    boundaries.Add(new SpatialBoundaryConductance(
                        node.Node,
                        boundary.Face,
                        conductance.Group1M2,
                        conductance.Group2M2));
                }
            }

            return boundaries;
        }

        private static bool IsAxial(NeighborDirection direction)
        {
            return direction == NeighborDirection.TowardEndA ||
                   direction == NeighborDirection.TowardEndB;
        }

        private static ContractValidationResult<FullCoreDiffusionModelV1> Invalid(
            ContractDiagnostic diagnostic)
        {
            return ContractValidationResult<FullCoreDiffusionModelV1>.Invalid(
                diagnostic.Code,
                diagnostic.Path,
                diagnostic.Message);
        }

        private static bool EnergyGroupsMatch(
            IReadOnlyList<string> left,
            IReadOnlyList<string> right)
        {
            return left.SequenceEqual(right, StringComparer.Ordinal) &&
                   left.Count == 2 &&
                   string.Equals(left[0], "fast", StringComparison.Ordinal) &&
                   string.Equals(left[1], "thermal", StringComparison.Ordinal);
        }

        private static ContractValidationResult<FullCoreAdjointImportanceV1> InvalidAdjoint(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<FullCoreAdjointImportanceV1>.Invalid(
                code,
                path,
                message);
        }

        private static ContractValidationResult<FullCoreDiffusionSolveResultV1> InvalidSolve(
            ContractDiagnostic diagnostic)
        {
            return InvalidSolve(diagnostic.Code, diagnostic.Path, diagnostic.Message);
        }

        private static ContractValidationResult<FullCoreDiffusionSolveResultV1> InvalidSolve(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<FullCoreDiffusionSolveResultV1>.Invalid(
                code,
                path,
                message);
        }

        private static string FormatDiagnosticValue(double? value)
        {
            return value.HasValue
                ? value.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                : "unavailable";
        }
    }
}
