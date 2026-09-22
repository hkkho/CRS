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
            double powerBalanceRelativeError,
            XenonSpatialCouplingResultV1? xenonCoupling = null,
            StaticAbsorptionOverlayV1? staticAbsorptionOverlay = null)
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
            XenonCoupling = xenonCoupling;
            StaticAbsorptionOverlay = staticAbsorptionOverlay;
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

        internal ReadOnlyCollection<double> Group1FluxStorage
        {
            get { return _group1Flux; }
        }

        internal ReadOnlyCollection<double> Group2FluxStorage
        {
            get { return _group2Flux; }
        }

        internal ReadOnlyCollection<double> NodePowerWattsStorage
        {
            get { return _nodePowerWatts; }
        }

        public double TotalPowerWatts { get; }

        public double EffectiveK { get; }

        public double Reactivity { get; }

        public double PowerBalanceRelativeError { get; }

        /// <summary>
        /// The immutable dynamic-Xe coupling accepted by this solve, or null
        /// for a xenon-free base solve. The base solve remains the reference
        /// path used by B2 and older callers.
        /// </summary>
        public XenonSpatialCouplingResultV1? XenonCoupling { get; }

        /// <summary>
        /// The generic static absorption overlay accepted by this solve, or
        /// null for the unoverlaid base result. This seam is deliberately
        /// controller-agnostic and is not a xenon or transient model.
        /// </summary>
        public StaticAbsorptionOverlayV1? StaticAbsorptionOverlay { get; }

        public bool HasStaticAbsorptionOverlay
        {
            get { return StaticAbsorptionOverlay != null; }
        }

        public Digest32? StaticAbsorptionOverlayDigest
        {
            get
            {
                return StaticAbsorptionOverlay == null
                    ? null
                    : StaticAbsorptionOverlay.OverlayDigest;
            }
        }

        public bool HasXenonOverlay
        {
            get { return XenonCoupling != null; }
        }

        public Digest32? XenonDynamicDigest
        {
            get { return XenonCoupling == null ? null : XenonCoupling.DynamicXenonDigest; }
        }

        public Digest32? XenonEffectiveCoefficientDigest
        {
            get { return XenonCoupling == null ? null : XenonCoupling.EffectiveCoefficientDigest; }
        }

        public XenonSpatialStateBindingV1? XenonStateBinding
        {
            get { return XenonCoupling == null ? null : XenonCoupling.StateBinding; }
        }

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
    /// One validated inventory and its burnup-dependent base coefficient set,
    /// prepared for a bounded group of candidate solves. It is internal so
    /// callers cannot bypass the model ownership and validation boundary.
    /// </summary>
    internal sealed class FullCoreDiffusionPreparedSolveV1
    {
        internal FullCoreDiffusionPreparedSolveV1(
            FullCoreDiffusionModelV1 owner,
            BundleInventory inventory,
            SpatialCoefficientSet baseCoefficients,
            Digest32 inventoryBindingDigest)
        {
            Owner = owner;
            Inventory = inventory;
            BaseCoefficients = baseCoefficients;
            InventoryBindingDigest = inventoryBindingDigest;
        }

        internal FullCoreDiffusionModelV1 Owner { get; }

        internal BundleInventory Inventory { get; }

        internal SpatialCoefficientSet BaseCoefficients { get; }

        internal Digest32 InventoryBindingDigest { get; }
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
        private SpatialCoefficientSet? _validatedTopologyConductanceTemplate;

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

            return TrySolveWithCoefficients(
                inventoryResult.Value,
                coefficientResult.Value,
                targetPowerWatts,
                initialEigenvalue,
                initialGroup1Flux,
                initialGroup2Flux,
                null,
                null,
                null);
        }

        internal ContractValidationResult<FullCoreDiffusionPreparedSolveV1> TryPrepareSolve(
            IEnumerable<BundleState> bundles)
        {
            if (bundles == null)
            {
                return ContractValidationResult<FullCoreDiffusionPreparedSolveV1>.Invalid(
                    "FullCoreDiffusionSolve.Bundles.Missing",
                    "bundles",
                    "A prepared full-core solve requires one live bundle for every spatial node.");
            }

            ContractValidationResult<BundleInventory> inventoryResult =
                BundleInventory.TryCreate(_topology, bundles);
            if (!inventoryResult.IsValid)
            {
                return ContractValidationResult<FullCoreDiffusionPreparedSolveV1>.Invalid(
                    inventoryResult.FirstDiagnostic.Code,
                    inventoryResult.FirstDiagnostic.Path,
                    inventoryResult.FirstDiagnostic.Message);
            }

            ContractValidationResult<SpatialCoefficientSet> coefficientResult =
                BuildCoefficientSet(inventoryResult.Value);
            if (!coefficientResult.IsValid)
            {
                return ContractValidationResult<FullCoreDiffusionPreparedSolveV1>.Invalid(
                    coefficientResult.FirstDiagnostic.Code,
                    coefficientResult.FirstDiagnostic.Path,
                    coefficientResult.FirstDiagnostic.Message);
            }

            Digest32 inventoryDigest = FullCoreAdjointImportanceV1.ComputeReferenceStateDigest(
                _dataPack,
                inventoryResult.Value);
            return ContractValidationResult<FullCoreDiffusionPreparedSolveV1>.Valid(
                new FullCoreDiffusionPreparedSolveV1(
                    this,
                    inventoryResult.Value,
                    coefficientResult.Value,
                    inventoryDigest));
        }

        internal ContractValidationResult<FullCoreDiffusionSolveResultV1> TrySolvePrepared(
            FullCoreDiffusionPreparedSolveV1 prepared,
            double targetPowerWatts,
            double initialEigenvalue,
            IReadOnlyList<double>? initialGroup1Flux,
            IReadOnlyList<double>? initialGroup2Flux,
            StaticAbsorptionOverlayV1? staticAbsorptionOverlay = null)
        {
            if (prepared == null || !ReferenceEquals(prepared.Owner, this))
            {
                return InvalidSolve(
                    "FullCoreDiffusionSolve.Prepared.OwnerMismatch",
                    "prepared_solve",
                    "A prepared solve may only be used by the model that validated it.");
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

            SpatialCoefficientSet coefficients = prepared.BaseCoefficients;
            if (staticAbsorptionOverlay != null)
            {
                ContractValidationResult<bool> overlayBinding =
                    ValidateStaticAbsorptionOverlay(staticAbsorptionOverlay);
                if (!overlayBinding.IsValid)
                {
                    return InvalidSolve(
                        overlayBinding.FirstDiagnostic.Code,
                        overlayBinding.FirstDiagnostic.Path,
                        overlayBinding.FirstDiagnostic.Message);
                }

                ContractValidationResult<SpatialCoefficientSet> effective =
                    ApplyStaticAbsorptionOverlay(coefficients, staticAbsorptionOverlay);
                if (!effective.IsValid)
                {
                    return InvalidSolve(
                        effective.FirstDiagnostic.Code,
                        effective.FirstDiagnostic.Path,
                        effective.FirstDiagnostic.Message);
                }

                coefficients = effective.Value;
            }

            return TrySolveWithCoefficients(
                prepared.Inventory,
                coefficients,
                targetPowerWatts,
                initialEigenvalue,
                initialGroup1Flux,
                initialGroup2Flux,
                null,
                staticAbsorptionOverlay,
                prepared.InventoryBindingDigest);
        }

        /// <summary>
        /// Solves the full core with one explicit, immutable static
        /// absorption overlay. This is the generic composition seam used by
        /// the practice liquid-zone controller; it does not imply xenon,
        /// kinetics, or a time-substep model.
        /// </summary>
        public ContractValidationResult<FullCoreDiffusionSolveResultV1> TrySolve(
            IEnumerable<BundleState> bundles,
            StaticAbsorptionOverlayV1 staticAbsorptionOverlay,
            double targetPowerWatts,
            double initialEigenvalue = 1.0,
            IReadOnlyList<double>? initialGroup1Flux = null,
            IReadOnlyList<double>? initialGroup2Flux = null)
        {
            if (staticAbsorptionOverlay == null)
            {
                return InvalidSolve(
                    "FullCoreDiffusionSolve.StaticAbsorptionOverlay.Missing",
                    "static_absorption_overlay",
                    "A static-overlay solve requires a validated explicit overlay.");
            }

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

            ContractValidationResult<SpatialCoefficientSet> baseCoefficientResult =
                BuildCoefficientSet(inventoryResult.Value);
            if (!baseCoefficientResult.IsValid)
            {
                return InvalidSolve(
                    baseCoefficientResult.FirstDiagnostic.Code,
                    baseCoefficientResult.FirstDiagnostic.Path,
                    baseCoefficientResult.FirstDiagnostic.Message);
            }

            ContractValidationResult<bool> overlayBinding =
                ValidateStaticAbsorptionOverlay(staticAbsorptionOverlay);
            if (!overlayBinding.IsValid)
            {
                return InvalidSolve(
                    overlayBinding.FirstDiagnostic.Code,
                    overlayBinding.FirstDiagnostic.Path,
                    overlayBinding.FirstDiagnostic.Message);
            }

            ContractValidationResult<SpatialCoefficientSet> effectiveCoefficientResult =
                ApplyStaticAbsorptionOverlay(
                    baseCoefficientResult.Value,
                    staticAbsorptionOverlay);
            if (!effectiveCoefficientResult.IsValid)
            {
                return InvalidSolve(
                    effectiveCoefficientResult.FirstDiagnostic.Code,
                    effectiveCoefficientResult.FirstDiagnostic.Path,
                    effectiveCoefficientResult.FirstDiagnostic.Message);
            }

            return TrySolveWithCoefficients(
                inventoryResult.Value,
                effectiveCoefficientResult.Value,
                targetPowerWatts,
                initialEigenvalue,
                initialGroup1Flux,
                initialGroup2Flux,
                null,
                staticAbsorptionOverlay,
                null);
        }

        /// <summary>
        /// Solves the full core with one already validated dynamic-Xe
        /// coefficient overlay. The overload keeps the existing base solve
        /// untouched while making the overlay an explicit, immutable input
        /// to the same deterministic SpatialEigenSolve path.
        /// </summary>
        public ContractValidationResult<FullCoreDiffusionSolveResultV1> TrySolve(
            IEnumerable<BundleState> bundles,
            XenonSpatialCouplingResultV1 xenonCoupling,
            double targetPowerWatts,
            double initialEigenvalue = 1.0,
            IReadOnlyList<double>? initialGroup1Flux = null,
            IReadOnlyList<double>? initialGroup2Flux = null)
        {
            if (xenonCoupling == null)
            {
                return InvalidSolve(
                    "FullCoreDiffusionSolve.XenonCoupling.Missing",
                    "xenon_coupling",
                    "A dynamic-Xe solve requires a validated coupling result.");
            }

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

            if (inventoryResult.Value.OccupiedCount != _stencil.NodeCount)
            {
                return InvalidSolve(
                    "FullCoreDiffusionSolve.Inventory.Incomplete",
                    "bundles",
                    "A dynamic-Xe full-core solve requires one live bundle for every spatial node.");
            }

            ContractValidationResult<SpatialCoefficientSet> baseCoefficientResult =
                BuildCoefficientSet(inventoryResult.Value);
            if (!baseCoefficientResult.IsValid)
            {
                return InvalidSolve(
                    baseCoefficientResult.FirstDiagnostic.Code,
                    baseCoefficientResult.FirstDiagnostic.Path,
                    baseCoefficientResult.FirstDiagnostic.Message);
            }

            ContractValidationResult<bool> couplingBinding =
                ValidateXenonCoupling(
                    inventoryResult.Value,
                    baseCoefficientResult.Value,
                    xenonCoupling);
            if (!couplingBinding.IsValid)
            {
                return InvalidSolve(
                    couplingBinding.FirstDiagnostic.Code,
                    couplingBinding.FirstDiagnostic.Path,
                    couplingBinding.FirstDiagnostic.Message);
            }

            return TrySolveWithCoefficients(
                inventoryResult.Value,
                xenonCoupling.Coefficients,
                targetPowerWatts,
                initialEigenvalue,
                initialGroup1Flux,
                initialGroup2Flux,
                xenonCoupling,
                null,
                null);
        }

        /// <summary>
        /// Builds the explicit xenon-free base coefficient set for the live
        /// inventory and admits a nodewise Phase 7 coupling. All model and
        /// pack identities are checked at this boundary.
        /// </summary>
        public ContractValidationResult<XenonSpatialCouplingResultV1> TryCreateXenonCoupling(
            IEnumerable<BundleState> bundles,
            XenonSpatialStateBindingV1 stateBinding,
            IEnumerable<XenonSpatialNodeInputV1> nodeInputs)
        {
            if (bundles == null)
            {
                return InvalidXenonCoupling(
                    "FullCoreDiffusionCoupling.Bundles.Missing",
                    "bundles",
                    "A dynamic-Xe coupling requires one live bundle for every spatial node.");
            }

            if (stateBinding == null)
            {
                return InvalidXenonCoupling(
                    "FullCoreDiffusionCoupling.StateBinding.Missing",
                    "state_binding",
                    "A dynamic-Xe coupling requires the exact current state binding.");
            }

            if (nodeInputs == null)
            {
                return InvalidXenonCoupling(
                    "FullCoreDiffusionCoupling.Nodes.Missing",
                    "node_inputs",
                    "A dynamic-Xe coupling requires one state input per spatial node.");
            }

            BundleState[] bundleRecords = bundles.ToArray();
            ContractValidationResult<BundleInventory> inventoryResult =
                BundleInventory.TryCreate(_topology, bundleRecords);
            if (!inventoryResult.IsValid)
            {
                return InvalidXenonCoupling(
                    inventoryResult.FirstDiagnostic.Code,
                    inventoryResult.FirstDiagnostic.Path,
                    inventoryResult.FirstDiagnostic.Message);
            }

            if (inventoryResult.Value.OccupiedCount != _stencil.NodeCount)
            {
                return InvalidXenonCoupling(
                    "FullCoreDiffusionCoupling.Inventory.Incomplete",
                    "bundles",
                    "A dynamic-Xe coupling requires one live bundle for every spatial node.");
            }

            ContractValidationResult<bool> modelBinding = ValidateXenonStateBinding(stateBinding);
            if (!modelBinding.IsValid)
            {
                return InvalidXenonCoupling(
                    modelBinding.FirstDiagnostic.Code,
                    modelBinding.FirstDiagnostic.Path,
                    modelBinding.FirstDiagnostic.Message);
            }

            XenonSpatialNodeInputV1[] inputs = nodeInputs.ToArray();
            if (inputs.Any(input => input == null))
            {
                return InvalidXenonCoupling(
                    "FullCoreDiffusionCoupling.Node.Null",
                    "node_inputs",
                    "A dynamic-Xe node input may not be null.");
            }

            foreach (XenonSpatialNodeInputV1 input in inputs)
            {
                BundleState? bundle = inventoryResult.Value.Get(input.Node);
                if (bundle == null)
                {
                    return InvalidXenonCoupling(
                        "FullCoreDiffusionCoupling.Node.Unknown",
                        ContractValidation.NodePath(input.Node, ".nuclide_state"),
                        "Every dynamic-Xe input must match an occupied model node.");
                }

                if (input.State.BundleId != bundle.BundleId)
                {
                    return InvalidXenonCoupling(
                        "FullCoreDiffusionCoupling.BundleId.Mismatch",
                        ContractValidation.NodePath(input.Node, ".bundle_id"),
                        "A dynamic-Xe node state must bind the exact live bundle at that node.");
                }

                if (input.State.Data.MaterialVariantId != bundle.MaterialVariantId)
                {
                    return InvalidXenonCoupling(
                        "FullCoreDiffusionCoupling.MaterialVariant.Mismatch",
                        ContractValidation.NodePath(input.Node, ".material_variant_id"),
                        "A dynamic-Xe node state must bind the exact live material variant.");
                }
            }

            ContractValidationResult<SpatialCoefficientSet> baseCoefficientResult =
                BuildCoefficientSet(inventoryResult.Value);
            if (!baseCoefficientResult.IsValid)
            {
                return InvalidXenonCoupling(
                    baseCoefficientResult.FirstDiagnostic.Code,
                    baseCoefficientResult.FirstDiagnostic.Path,
                    baseCoefficientResult.FirstDiagnostic.Message);
            }

            return XenonSpatialCouplingV1.TryApply(
                baseCoefficientResult.Value,
                XenonSpatialCouplingV1.ComputeBaseCoefficientDigest(baseCoefficientResult.Value),
                stateBinding,
                inputs);
        }

        /// <summary>
        /// Convenience overload for the immutable spatial state owner.
        /// </summary>
        public ContractValidationResult<XenonSpatialCouplingResultV1> TryCreateXenonCoupling(
            IEnumerable<BundleState> bundles,
            XenonSpatialStateV1 state,
            double kineticAmplitude)
        {
            if (state == null)
            {
                return InvalidXenonCoupling(
                    "FullCoreDiffusionCoupling.State.Missing",
                    "state",
                    "A dynamic-Xe coupling requires a validated spatial state.");
            }

            if (!ReferenceEquals(state.Model, this))
            {
                return InvalidXenonCoupling(
                    "FullCoreDiffusionCoupling.Model.IdentityMismatch",
                    "state.model",
                    "A spatial state may only be coupled to the exact model that created it.");
            }

            ContractValidationResult<XenonSpatialStateBindingV1> binding =
                state.TryCreateBinding(kineticAmplitude);
            if (!binding.IsValid)
            {
                return InvalidXenonCoupling(
                    binding.FirstDiagnostic.Code,
                    binding.FirstDiagnostic.Path,
                    binding.FirstDiagnostic.Message);
            }

            return TryCreateXenonCoupling(
                bundles,
                binding.Value,
                state.NodeInputs);
        }

        /// <summary>
        /// Exposes the existing cadence adapter at the full-core model
        /// boundary for callers that need the accepted Phase 7 solve tuple.
        /// </summary>
        public ContractValidationResult<XenonSpatialSolveResultV1> TrySolveXenonAtCadence(
            XenonSpatialCouplingResultV1 xenonCoupling,
            SpatialRecomputeCadenceV1 cadence,
            double exactSimulationTimeSeconds,
            double targetPowerWatts,
            double initialEigenvalue,
            double[]? initialGroup1Flux = null,
            double[]? initialGroup2Flux = null)
        {
            if (xenonCoupling == null)
            {
                return InvalidXenonSolve(
                    "FullCoreDiffusionXenonSolve.Coupling.Missing",
                    "xenon_coupling",
                    "A dynamic-Xe spatial solve requires a validated coupling result.");
            }

            ContractValidationResult<bool> modelBinding = ValidateXenonStateBinding(
                xenonCoupling.StateBinding);
            if (!modelBinding.IsValid)
            {
                return InvalidXenonSolve(
                    modelBinding.FirstDiagnostic.Code,
                    modelBinding.FirstDiagnostic.Path,
                    modelBinding.FirstDiagnostic.Message);
            }

            return XenonSpatialSolveV1.TryApply(
                xenonCoupling,
                _stencil,
                cadence,
                exactSimulationTimeSeconds,
                _dataPack.LinearSolvePolicy,
                _dataPack.ConvergencePolicy,
                targetPowerWatts,
                initialEigenvalue,
                initialGroup1Flux,
                initialGroup2Flux);
        }

        private ContractValidationResult<FullCoreDiffusionSolveResultV1> TrySolveWithCoefficients(
            BundleInventory inventory,
            SpatialCoefficientSet coefficientSet,
            double targetPowerWatts,
            double initialEigenvalue,
            IReadOnlyList<double>? initialGroup1Flux,
            IReadOnlyList<double>? initialGroup2Flux,
            XenonSpatialCouplingResultV1? xenonCoupling,
            StaticAbsorptionOverlayV1? staticAbsorptionOverlay,
            Digest32? preparedInventoryBindingDigest)
        {

            double[]? group1WarmStart = initialGroup1Flux?.ToArray();
            double[]? group2WarmStart = initialGroup2Flux?.ToArray();
            ContractValidationResult<SpatialEigenIteration> iterationResult =
                SpatialEigenIteration.TryCreate(
                    _stencil,
                    coefficientSet,
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
                SpatialNodeCoefficients coefficients = coefficientSet.Nodes[nodeIndex];
                double power;
                if (coefficients.PowerResponse != null)
                {
                    power =
                        coefficients.PowerResponse.Group1WattsPerFluxDensity *
                            finalState.Group1Flux[nodeIndex] +
                        coefficients.PowerResponse.Group2WattsPerFluxDensity *
                            finalState.Group2Flux[nodeIndex];
                }
                else
                {
                    double fissionRate =
                        coefficients.FissionGroup1PerM * finalState.Group1Flux[nodeIndex] +
                        coefficients.FissionGroup2PerM * finalState.Group2Flux[nodeIndex];
                    power = coefficients.VolumeM3 * coefficients.EnergyPerFissionJ * fissionRate;
                    if (!ContractValidation.IsFinite(fissionRate) || fissionRate < 0.0)
                    {
                        return InvalidSolve(
                            "FullCoreDiffusionSolve.Power.NonFinite",
                            ContractValidation.NodePath(_stencil.Nodes[nodeIndex].Node, ".power_w"),
                            "The converged node fission rate must be finite and nonnegative.");
                    }
                }

                if (!ContractValidation.IsFinite(power) || power < 0.0)
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
                    coefficientSet,
                    preparedInventoryBindingDigest ??
                        FullCoreAdjointImportanceV1.ComputeReferenceStateDigest(
                            _dataPack,
                            inventory),
                    AdjointWeightedReactivityV1.ComputeCoefficientBindingDigest(
                        _dataPack,
                        coefficientSet),
                    finalState.Group1Flux,
                    finalState.Group2Flux,
                    nodePowerWatts,
                    totalPowerWatts,
                    effectiveK,
                    reactivity,
                    powerBalanceRelativeError,
                    xenonCoupling,
                    staticAbsorptionOverlay));
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

        private ContractValidationResult<bool> ValidateXenonStateBinding(
            XenonSpatialStateBindingV1 stateBinding)
        {
            if (stateBinding == null)
            {
                return ContractValidationResult<bool>.Invalid(
                    "FullCoreDiffusionCoupling.StateBinding.Missing",
                    "state_binding",
                    "A dynamic-Xe operation requires an exact state binding.");
            }

            if (!stateBinding.TopologyDigest.Equals(ModelTopologyDigest()) ||
                !stateBinding.DataPackDigest.Equals(ModelDataPackDigest()))
            {
                return ContractValidationResult<bool>.Invalid(
                    "FullCoreDiffusionCoupling.BindingDigest.Mismatch",
                    "state_binding.digests",
                    "The dynamic-Xe state binding must match this model's topology and diffusion data-pack identities.");
            }

            if (stateBinding.NodeVersions.Count != _stencil.NodeCount)
            {
                return ContractValidationResult<bool>.Invalid(
                    "FullCoreDiffusionCoupling.NodeVersions.CountMismatch",
                    "state_binding.node_versions",
                    "The dynamic-Xe state binding must contain one version for every full-core node.");
            }

            var seen = new HashSet<NodeKey>();
            foreach (XenonSpatialNuclideVersionV1 version in stateBinding.NodeVersions)
            {
                if (!seen.Add(version.Node) || !_stencil.Nodes.Any(node => node.Node == version.Node))
                {
                    return ContractValidationResult<bool>.Invalid(
                        "FullCoreDiffusionCoupling.NodeVersions.TopologyMismatch",
                        ContractValidation.NodePath(version.Node, ".nuclide_state_version"),
                        "The dynamic-Xe state binding node set must equal the model stencil node set.");
                }
            }

            return ContractValidationResult<bool>.Valid(true);
        }

        private ContractValidationResult<bool> ValidateXenonCoupling(
            BundleInventory inventory,
            SpatialCoefficientSet baseCoefficients,
            XenonSpatialCouplingResultV1 xenonCoupling)
        {
            if (xenonCoupling == null)
            {
                return ContractValidationResult<bool>.Invalid(
                    "FullCoreDiffusionSolve.XenonCoupling.Missing",
                    "xenon_coupling",
                    "A dynamic-Xe solve requires a validated coupling result.");
            }

            if (!ReferenceEquals(xenonCoupling.Coefficients.Stencil, _stencil))
            {
                return ContractValidationResult<bool>.Invalid(
                    "FullCoreDiffusionSolve.XenonCoupling.StencilMismatch",
                    "xenon_coupling.coefficients.stencil",
                    "The dynamic-Xe effective coefficients must use this model's exact assembled stencil.");
            }

            ContractValidationResult<bool> binding = ValidateXenonStateBinding(
                xenonCoupling.StateBinding);
            if (!binding.IsValid)
            {
                return binding;
            }

            Digest32 expectedBaseDigest = XenonSpatialCouplingV1.ComputeBaseCoefficientDigest(
                baseCoefficients);
            if (!expectedBaseDigest.Equals(xenonCoupling.BaseCoefficientDigest))
            {
                return ContractValidationResult<bool>.Invalid(
                    "FullCoreDiffusionSolve.XenonCoupling.BaseDigest.Stale",
                    "xenon_coupling.base_coefficient_digest",
                    "The dynamic-Xe coupling was built from a different live base coefficient set.");
            }

            if (xenonCoupling.Overlays.Count != _stencil.NodeCount ||
                xenonCoupling.Coefficients.NodeCount != _stencil.NodeCount)
            {
                return ContractValidationResult<bool>.Invalid(
                    "FullCoreDiffusionSolve.XenonCoupling.NodeCountMismatch",
                    "xenon_coupling",
                    "The dynamic-Xe coupling must contain one overlay and effective coefficient row per node.");
            }

            var overlayMap = new Dictionary<NodeKey, XenonSpatialOverlayValueV1>();
            foreach (XenonSpatialOverlayValueV1 overlay in xenonCoupling.Overlays)
            {
                if (!overlayMap.TryAdd(overlay.Node, overlay))
                {
                    return ContractValidationResult<bool>.Invalid(
                        "FullCoreDiffusionSolve.XenonCoupling.Node.Duplicate",
                        ContractValidation.NodePath(overlay.Node, ".overlay"),
                        "The dynamic-Xe coupling may contain only one overlay per node.");
                }

                BundleState? bundle = inventory.Get(overlay.Node);
                if (bundle == null || bundle.BundleId != overlay.BundleId)
                {
                    return ContractValidationResult<bool>.Invalid(
                        "FullCoreDiffusionSolve.XenonCoupling.BundleId.Stale",
                        ContractValidation.NodePath(overlay.Node, ".bundle_id"),
                        "The dynamic-Xe overlay does not bind the exact live bundle at this node.");
                }
            }

            foreach (SpatialNodeStencil node in _stencil.Nodes)
            {
                if (!overlayMap.ContainsKey(node.Node))
                {
                    return ContractValidationResult<bool>.Invalid(
                        "FullCoreDiffusionSolve.XenonCoupling.Node.Missing",
                        ContractValidation.NodePath(node.Node, ".overlay"),
                        "The dynamic-Xe coupling must contain every model node exactly once.");
                }
            }

            return ContractValidationResult<bool>.Valid(true);
        }

        private ContractValidationResult<bool> ValidateStaticAbsorptionOverlay(
            StaticAbsorptionOverlayV1 overlay)
        {
            if (overlay == null)
            {
                return ContractValidationResult<bool>.Invalid(
                    "FullCoreDiffusionSolve.StaticAbsorptionOverlay.Missing",
                    "static_absorption_overlay",
                    "A static absorption overlay is required at the model boundary.");
            }

            var modelNodes = new HashSet<NodeKey>(
                _stencil.Nodes.Select(node => node.Node));
            foreach (StaticAbsorptionOverlayEntryV1 entry in overlay.Entries)
            {
                if (!modelNodes.Contains(entry.Node))
                {
                    return ContractValidationResult<bool>.Invalid(
                        "FullCoreDiffusionSolve.StaticAbsorptionOverlay.Node.Unknown",
                        ContractValidation.NodePath(entry.Node, ".delta_absorption_m_inverse"),
                        "Every static absorption entry must match an occupied model node.");
                }
            }

            return ContractValidationResult<bool>.Valid(true);
        }

        private static ContractValidationResult<SpatialCoefficientSet> ApplyStaticAbsorptionOverlay(
            SpatialCoefficientSet baseCoefficients,
            StaticAbsorptionOverlayV1 overlay)
        {
            var effectiveNodes = new List<SpatialNodeCoefficients>(
                baseCoefficients.NodeCount);
            foreach (SpatialNodeCoefficients baseNode in baseCoefficients.Nodes)
            {
                double group1 = baseNode.AbsorptionGroup1PerM +
                    overlay.GetDeltaAbsorptionGroup1PerM(baseNode.Node);
                double group2 = baseNode.AbsorptionGroup2PerM +
                    overlay.GetDeltaAbsorptionGroup2PerM(baseNode.Node);
                if (!ContractValidation.IsFinite(group1) ||
                    !ContractValidation.IsFinite(group2))
                {
                    return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                        "FullCoreDiffusionSolve.StaticAbsorptionOverlay.NonFinite",
                        ContractValidation.NodePath(baseNode.Node, ".effective_absorption_m_inverse"),
                        "Static overlay application must leave both effective absorptions finite.");
                }

                effectiveNodes.Add(new SpatialNodeCoefficients(
                    baseNode.Node,
                    baseNode.VolumeM3,
                    NormalizeZero(group1),
                    NormalizeZero(group2),
                    baseNode.DownscatterGroup1To2PerM,
                    baseNode.FissionGroup1PerM,
                    baseNode.FissionGroup2PerM,
                    baseNode.NuFissionGroup1PerM,
                    baseNode.NuFissionGroup2PerM,
                    baseNode.ChiGroup1,
                    baseNode.ChiGroup2,
                    baseNode.EnergyPerFissionJ,
                    baseNode.PowerResponse));
            }

            return baseCoefficients.TryRebindCanonicalNodeCoefficients(effectiveNodes);
        }

        private static double NormalizeZero(double value)
        {
            return value == 0.0 ? 0.0 : value;
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

            SpatialCoefficientSet? template =
                System.Threading.Volatile.Read(ref _validatedTopologyConductanceTemplate);
            if (template != null)
            {
                return template.TryRebindCanonicalNodeCoefficients(nodeCoefficients);
            }

            ContractValidationResult<SpatialCoefficientSet> created =
                SpatialCoefficientSet.TryCreateWithXenonBasis(
                    _stencil,
                    nodeCoefficients,
                    _edgeConductances,
                    _boundaryConductances,
                    XenonBasisV1.Excluded,
                    0.0);
            if (created.IsValid)
            {
                // The first successful binding establishes an immutable
                // topology/conductance template. A racing valid preparation
                // may publish a different equivalent template, but neither
                // preparation mutates the object it returns.
                System.Threading.Interlocked.CompareExchange(
                    ref _validatedTopologyConductanceTemplate,
                    created.Value,
                    null);
            }

            return created;
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

        private Digest32 ModelTopologyDigest()
        {
            return new Digest32(_dataPack.Descriptor.TopologyDigest.ToArray());
        }

        private Digest32 ModelDataPackDigest()
        {
            return new Digest32(_dataPack.Descriptor.ContentDigest.ToArray());
        }

        private static ContractValidationResult<XenonSpatialCouplingResultV1> InvalidXenonCoupling(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<XenonSpatialCouplingResultV1>.Invalid(
                code,
                path,
                message);
        }

        private static ContractValidationResult<XenonSpatialSolveResultV1> InvalidXenonSolve(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<XenonSpatialSolveResultV1>.Invalid(
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
