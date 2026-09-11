using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace ReactorSim.Core
{
    /// <summary>
    /// Identities and bounded constants for the playable practice RRS. The
    /// values are project-authored synthetic controls, deliberately isolated
    /// so a future offline data pack can replace them without changing the
    /// solver or Game transaction boundary.
    /// </summary>
    public static class PracticeLiquidZoneRrsIdentityV1
    {
        public const uint CurrentSchemaVersion = 1;
        public const uint LogicalZoneCount = 14;
        public const uint ChannelCount = 380;
        public const uint BundlePositionCount = 12;
        public const uint NodeCount = ChannelCount * BundlePositionCount;
        public const double InitialFillFraction = 0.5;
        public const double Group1AbsorptionPerMPerFillFraction = 0.020;
        public const double Group2AbsorptionPerMPerFillFraction = 0.008;
        public const double ShapeErrorGain = 0.80;
        public const double CommonModeRhoGain = 2.00;
        public const double MaxFillIncrementPerIteration = 0.08;
        public const double ControllerTolerance = 1.0e-4;
        public const int MaximumControllerIterations = 8;
        public const string ControllerIdentity = "synthetic-practice-liquid-zone-rrs-v1";
        public const string MappingIdentity = "synthetic-practice-liquid-zone-map-380x12-v1";
        public const string OverlayIdentity = "synthetic-practice-liquid-zone-absorption-overlay-v1";
        public const string CadenceIdentity = "equilibrium-static-event-rrs-v1";
        public const string Provenance =
            "project-authored-synthetic; future offline data-pack replaceable";
    }

    /// <summary>
    /// One complete-map binding from a diffusion node to one of fourteen
    /// practice liquid zones. Weights are static absorption deltas in m^-1
    /// per unit fill fraction.
    /// </summary>
    public sealed class PracticeLiquidZoneRrsNodeBindingV1
    {
        public PracticeLiquidZoneRrsNodeBindingV1(
            NodeKey node,
            uint logicalZoneId,
            double group1AbsorptionPerMPerFillFraction,
            double group2AbsorptionPerMPerFillFraction)
        {
            if (logicalZoneId >= PracticeLiquidZoneRrsIdentityV1.LogicalZoneCount)
            {
                throw new ArgumentOutOfRangeException(nameof(logicalZoneId));
            }

            if (!IsCanonicalNonnegative(group1AbsorptionPerMPerFillFraction) ||
                !IsCanonicalNonnegative(group2AbsorptionPerMPerFillFraction))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(group1AbsorptionPerMPerFillFraction),
                    "Practice RRS absorption weights must be finite, canonical, and nonnegative.");
            }

            Node = node;
            LogicalZoneId = logicalZoneId;
            Group1AbsorptionPerMPerFillFraction = group1AbsorptionPerMPerFillFraction;
            Group2AbsorptionPerMPerFillFraction = group2AbsorptionPerMPerFillFraction;
        }

        public NodeKey Node { get; }

        public uint LogicalZoneId { get; }

        public double Group1AbsorptionPerMPerFillFraction { get; }

        public double Group2AbsorptionPerMPerFillFraction { get; }

        private static bool IsCanonicalNonnegative(double value)
        {
            return ContractValidation.IsFinite(value) &&
                   value >= 0.0 &&
                   (value != 0.0 || BitConverter.DoubleToInt64Bits(value) >= 0);
        }
    }

    /// <summary>
    /// Complete deterministic 380 by 12 node mapping for the practice RRS.
    /// The compact synthetic map divides the stepped 22 by 22 channel outline
    /// into seven face bands across two axial bundle halves. It is a game
    /// contract, not an external CANDU plant map.
    /// </summary>
    public sealed class PracticeLiquidZoneRrsMappingV1
    {
        private readonly ReadOnlyCollection<PracticeLiquidZoneRrsNodeBindingV1> _nodes;
        private readonly Dictionary<NodeKey, PracticeLiquidZoneRrsNodeBindingV1> _byNode;
        private readonly ReadOnlyCollection<IReadOnlyList<int>> _nodeIndicesByZone;
        private readonly string _mappingIdentity;
        private readonly string _provenance;

        private PracticeLiquidZoneRrsMappingV1(
            IEnumerable<PracticeLiquidZoneRrsNodeBindingV1> nodes,
            IEnumerable<IReadOnlyList<int>> nodeIndicesByZone,
            Digest32 mappingDigest)
        {
            _nodes = new ReadOnlyCollection<PracticeLiquidZoneRrsNodeBindingV1>(nodes.ToArray());
            _byNode = _nodes.ToDictionary(node => node.Node, node => node);
            _nodeIndicesByZone = new ReadOnlyCollection<IReadOnlyList<int>>(
                nodeIndicesByZone
                    .Select(indices => (IReadOnlyList<int>)new ReadOnlyCollection<int>(indices.ToArray()))
                    .ToArray());
            _mappingIdentity = PracticeLiquidZoneRrsIdentityV1.MappingIdentity;
            _provenance = PracticeLiquidZoneRrsIdentityV1.Provenance;
            MappingDigest = mappingDigest;
        }

        public string MappingIdentity
        {
            get { return _mappingIdentity; }
        }

        public string Provenance
        {
            get { return _provenance; }
        }

        public IReadOnlyList<PracticeLiquidZoneRrsNodeBindingV1> Nodes
        {
            get { return _nodes; }
        }

        public Digest32 MappingDigest { get; }

        public string MappingDigestHex
        {
            get { return DigestHex(MappingDigest); }
        }

        public IReadOnlyList<IReadOnlyList<int>> NodeIndicesByZone
        {
            get { return _nodeIndicesByZone; }
        }

        public static ContractValidationResult<PracticeLiquidZoneRrsMappingV1> TryCreateCandu6()
        {
            var nodes = new List<PracticeLiquidZoneRrsNodeBindingV1>(
                checked((int)PracticeLiquidZoneRrsIdentityV1.NodeCount));
            for (uint channel = 0; channel < PracticeLiquidZoneRrsIdentityV1.ChannelCount; channel++)
            {
                Candu6GridPositionV1 position = Candu6CoreTopologyFactoryV1.GetPosition(channel);
                uint radialBand = checked((uint)Math.Min(6, (position.Column * 7) / 22));
                for (uint bundlePosition = 0;
                     bundlePosition < PracticeLiquidZoneRrsIdentityV1.BundlePositionCount;
                     bundlePosition++)
                {
                    uint axialHalf = bundlePosition <
                        PracticeLiquidZoneRrsIdentityV1.BundlePositionCount / 2U
                        ? 0U
                        : 1U;
                    uint zone = checked(axialHalf * 7U + radialBand);
                    nodes.Add(new PracticeLiquidZoneRrsNodeBindingV1(
                        new NodeKey(
                            new ChannelId(channel),
                            new BundlePosition(bundlePosition)),
                        zone,
                        PracticeLiquidZoneRrsIdentityV1.Group1AbsorptionPerMPerFillFraction,
                        PracticeLiquidZoneRrsIdentityV1.Group2AbsorptionPerMPerFillFraction));
                }
            }

            return TryCreate(nodes);
        }

        public static ContractValidationResult<PracticeLiquidZoneRrsMappingV1> TryCreate(
            IEnumerable<PracticeLiquidZoneRrsNodeBindingV1> nodes)
        {
            if (nodes == null)
            {
                return Invalid(
                    "PracticeLiquidZoneRrs.Mapping.Nodes.Missing",
                    "nodes",
                    "The practice RRS requires a complete node mapping.");
            }

            PracticeLiquidZoneRrsNodeBindingV1[] records = nodes.ToArray();
            if (records.Length != (int)PracticeLiquidZoneRrsIdentityV1.NodeCount)
            {
                return Invalid(
                    "PracticeLiquidZoneRrs.Mapping.NodeCount.Invalid",
                    "nodes",
                    "The practice RRS mapping must contain exactly 380 by 12 node bindings.");
            }

            PracticeLiquidZoneRrsNodeBindingV1[] canonical = records
                .OrderBy(node => node.Node)
                .ToArray();
            var expectedZoneCounts = new int[PracticeLiquidZoneRrsIdentityV1.LogicalZoneCount];
            for (int index = 0; index < canonical.Length; index++)
            {
                PracticeLiquidZoneRrsNodeBindingV1 node = canonical[index];
                if (node.Node.ChannelId.Value >= PracticeLiquidZoneRrsIdentityV1.ChannelCount ||
                    node.Node.Position.Value >= PracticeLiquidZoneRrsIdentityV1.BundlePositionCount)
                {
                    return Invalid(
                        "PracticeLiquidZoneRrs.Mapping.Node.OutOfRange",
                        ContractValidation.NodePath(node.Node, ".mapping"),
                        "Every RRS mapping node must belong to the canonical 380 by 12 topology.");
                }

                if (index > 0 && canonical[index - 1].Node == node.Node)
                {
                    return Invalid(
                        "PracticeLiquidZoneRrs.Mapping.Node.Duplicate",
                        ContractValidation.NodePath(node.Node, ".mapping"),
                        "The complete RRS mapping may contain only one binding per node.");
                }

                NodeKey expected = new NodeKey(
                    new ChannelId((uint)(index / (int)PracticeLiquidZoneRrsIdentityV1.BundlePositionCount)),
                    new BundlePosition((uint)(index % (int)PracticeLiquidZoneRrsIdentityV1.BundlePositionCount)));
                if (node.Node != expected)
                {
                    return Invalid(
                        "PracticeLiquidZoneRrs.Mapping.Node.Missing",
                        ContractValidation.NodePath(expected, ".mapping"),
                        "The RRS mapping must cover every canonical channel-major node exactly once.");
                }

                expectedZoneCounts[(int)node.LogicalZoneId]++;
            }

            for (int zone = 0; zone < expectedZoneCounts.Length; zone++)
            {
                if (expectedZoneCounts[zone] == 0)
                {
                    return Invalid(
                        "PracticeLiquidZoneRrs.Mapping.Zone.Empty",
                        "zones[" + zone.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]",
                        "Every one of the fourteen logical RRS zones must own at least one node.");
                }
            }

            var indicesByZone = new List<int>[PracticeLiquidZoneRrsIdentityV1.LogicalZoneCount];
            for (int zone = 0; zone < indicesByZone.Length; zone++)
            {
                indicesByZone[zone] = new List<int>(expectedZoneCounts[zone]);
            }

            for (int index = 0; index < canonical.Length; index++)
            {
                indicesByZone[(int)canonical[index].LogicalZoneId].Add(index);
            }

            Digest32 digest = new Digest32(Phase5CanonicalBytesV1.HashBody(
                "CANDU-PRACTICE-LIQUID-ZONE-MAP-V1",
                writer =>
                {
                    Phase5CanonicalBytesV1.WriteUInt32(
                        writer,
                        PracticeLiquidZoneRrsIdentityV1.CurrentSchemaVersion);
                    Phase5CanonicalBytesV1.WriteString(
                        writer,
                        PracticeLiquidZoneRrsIdentityV1.MappingIdentity);
                    Phase5CanonicalBytesV1.WriteString(
                        writer,
                        PracticeLiquidZoneRrsIdentityV1.Provenance);
                    Phase5CanonicalBytesV1.WriteUInt32(
                        writer,
                        PracticeLiquidZoneRrsIdentityV1.LogicalZoneCount);
                    Phase5CanonicalBytesV1.WriteUInt32(
                        writer,
                        checked((uint)canonical.Length));
                    foreach (PracticeLiquidZoneRrsNodeBindingV1 node in canonical)
                    {
                        Phase5CanonicalBytesV1.WriteUInt32(writer, node.Node.ChannelId.Value);
                        Phase5CanonicalBytesV1.WriteUInt32(writer, node.Node.Position.Value);
                        Phase5CanonicalBytesV1.WriteUInt32(writer, node.LogicalZoneId);
                        Phase5CanonicalBytesV1.WriteDouble(
                            writer,
                            node.Group1AbsorptionPerMPerFillFraction);
                        Phase5CanonicalBytesV1.WriteDouble(
                            writer,
                            node.Group2AbsorptionPerMPerFillFraction);
                    }
                }));

            return ContractValidationResult<PracticeLiquidZoneRrsMappingV1>.Valid(
                new PracticeLiquidZoneRrsMappingV1(canonical, indicesByZone, digest));
        }

        public uint GetLogicalZoneId(NodeKey node)
        {
            if (!_byNode.TryGetValue(node, out PracticeLiquidZoneRrsNodeBindingV1 binding))
            {
                throw new KeyNotFoundException("The node is not present in the practice RRS mapping.");
            }

            return binding.LogicalZoneId;
        }

        public PracticeLiquidZoneRrsNodeBindingV1 GetNodeBinding(int nodeIndex)
        {
            if (nodeIndex < 0 || nodeIndex >= _nodes.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(nodeIndex));
            }

            return _nodes[nodeIndex];
        }

        public ContractValidationResult<StaticAbsorptionOverlayV1> TryBuildOverlay(
            IReadOnlyList<double> zoneFills)
        {
            if (zoneFills == null || zoneFills.Count != (int)PracticeLiquidZoneRrsIdentityV1.LogicalZoneCount)
            {
                return ContractValidationResult<StaticAbsorptionOverlayV1>.Invalid(
                    "PracticeLiquidZoneRrs.Fill.CountMismatch",
                    "zone_fills",
                    "A practice RRS overlay requires exactly fourteen zone fills.");
            }

            var entries = new List<StaticAbsorptionOverlayEntryV1>(_nodes.Count);
            for (int nodeIndex = 0; nodeIndex < _nodes.Count; nodeIndex++)
            {
                PracticeLiquidZoneRrsNodeBindingV1 binding = _nodes[nodeIndex];
                double fill = zoneFills[(int)binding.LogicalZoneId];
                if (!IsCanonicalFraction(fill))
                {
                    return ContractValidationResult<StaticAbsorptionOverlayV1>.Invalid(
                        "PracticeLiquidZoneRrs.Fill.Invalid",
                        "zone_fills[" + binding.LogicalZoneId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]",
                        "Every practice RRS zone fill must be finite and within [0,1].");
                }

                double deltaFill = fill - PracticeLiquidZoneRrsIdentityV1.InitialFillFraction;
                double group1 = deltaFill * binding.Group1AbsorptionPerMPerFillFraction;
                double group2 = deltaFill * binding.Group2AbsorptionPerMPerFillFraction;
                if (!IsCanonicalFinite(group1) || !IsCanonicalFinite(group2))
                {
                    return ContractValidationResult<StaticAbsorptionOverlayV1>.Invalid(
                        "PracticeLiquidZoneRrs.Overlay.NonFinite",
                        ContractValidation.NodePath(binding.Node, ".delta_absorption_m_inverse"),
                        "Practice RRS fill conversion must remain finite and canonical.");
                }

                entries.Add(new StaticAbsorptionOverlayEntryV1(binding.Node, group1, group2));
            }

            return StaticAbsorptionOverlayV1.TryCreate(
                PracticeLiquidZoneRrsIdentityV1.OverlayIdentity,
                entries);
        }

        private static bool IsCanonicalFinite(double value)
        {
            return ContractValidation.IsFinite(value) &&
                   (value != 0.0 || BitConverter.DoubleToInt64Bits(value) >= 0);
        }

        private static bool IsCanonicalFraction(double value)
        {
            return IsCanonicalFinite(value) && value >= 0.0 && value <= 1.0;
        }

        private static string DigestHex(Digest32 digest)
        {
            var builder = new StringBuilder(digest.Bytes.Count * 2 + 7);
            builder.Append("sha256:");
            foreach (byte value in digest.Bytes)
            {
                builder.Append(value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static ContractValidationResult<PracticeLiquidZoneRrsMappingV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<PracticeLiquidZoneRrsMappingV1>.Invalid(
                code,
                path,
                message);
        }
    }

    /// <summary>
    /// Immutable accepted practice RRS state. It records the reference shape,
    /// current measured zonal shape, bounded fills, and the last static
    /// controller result. No kinetics or time integration is represented.
    /// </summary>
    public sealed class PracticeLiquidZoneRrsV1
    {
        private readonly ReadOnlyCollection<double> _zoneFills;
        private readonly ReadOnlyCollection<double> _referenceZonalPowerFractions;
        private readonly ReadOnlyCollection<double> _targetZonalPowerFractions;
        private readonly ReadOnlyCollection<double> _measuredZonalPowerFractions;
        private readonly ReadOnlyCollection<double> _zonalShapeErrors;
        private readonly string _controllerIdentity;
        private readonly string _cadenceIdentity;

        private PracticeLiquidZoneRrsV1(
            PracticeLiquidZoneRrsMappingV1 mapping,
            IEnumerable<double> zoneFills,
            IEnumerable<double> referenceZonalPowerFractions,
            IEnumerable<double> targetZonalPowerFractions,
            IEnumerable<double> measuredZonalPowerFractions,
            IEnumerable<double> zonalShapeErrors,
            StaticAbsorptionOverlayV1 absorptionOverlay,
            double targetPowerWatts,
            double measuredPowerWatts,
            double coreReactivity,
            double compensatedNetReactivity,
            double commonModeRhoCorrection,
            int controllerIterationCount,
            bool controllerConverged,
            double simulationTimeSeconds)
        {
            Mapping = mapping;
            _zoneFills = Copy(zoneFills);
            _referenceZonalPowerFractions = Copy(referenceZonalPowerFractions);
            _targetZonalPowerFractions = Copy(targetZonalPowerFractions);
            _measuredZonalPowerFractions = Copy(measuredZonalPowerFractions);
            _zonalShapeErrors = Copy(zonalShapeErrors);
            _controllerIdentity = PracticeLiquidZoneRrsIdentityV1.ControllerIdentity;
            _cadenceIdentity = PracticeLiquidZoneRrsIdentityV1.CadenceIdentity;
            AbsorptionOverlay = absorptionOverlay;
            TargetPowerWatts = targetPowerWatts;
            MeasuredPowerWatts = measuredPowerWatts;
            PowerErrorWatts = targetPowerWatts - measuredPowerWatts;
            CoreReactivity = coreReactivity;
            CompensatedNetReactivity = compensatedNetReactivity;
            CommonModeRhoCorrection = commonModeRhoCorrection;
            ControllerIterationCount = controllerIterationCount;
            ControllerConverged = controllerConverged;
            SimulationTimeSeconds = simulationTimeSeconds;
            StateDigest = ComputeStateDigest();
        }

        public PracticeLiquidZoneRrsMappingV1 Mapping { get; }

        public string ControllerIdentity
        {
            get { return _controllerIdentity; }
        }

        public string MappingIdentity
        {
            get { return Mapping.MappingIdentity; }
        }

        public string CadenceIdentity
        {
            get { return _cadenceIdentity; }
        }

        public IReadOnlyList<double> ZoneFills
        {
            get { return _zoneFills; }
        }

        public IReadOnlyList<double> FillFractions
        {
            get { return ZoneFills; }
        }

        public IReadOnlyList<double> ReferenceZonalPowerFractions
        {
            get { return _referenceZonalPowerFractions; }
        }

        public IReadOnlyList<double> TargetZonalPowerFractions
        {
            get { return _targetZonalPowerFractions; }
        }

        public IReadOnlyList<double> MeasuredZonalPowerFractions
        {
            get { return _measuredZonalPowerFractions; }
        }

        public IReadOnlyList<double> ZonalShapeErrors
        {
            get { return _zonalShapeErrors; }
        }

        public double AverageFillFraction
        {
            get { return _zoneFills.Average(); }
        }

        public double MinimumFillFraction
        {
            get { return _zoneFills.Min(); }
        }

        public double MaximumFillFraction
        {
            get { return _zoneFills.Max(); }
        }

        public double AverageFill
        {
            get { return AverageFillFraction; }
        }

        public double MinimumFill
        {
            get { return MinimumFillFraction; }
        }

        public double MaximumFill
        {
            get { return MaximumFillFraction; }
        }

        public double TargetPowerWatts { get; }

        public double MeasuredPowerWatts { get; }

        public double PowerErrorWatts { get; }

        public double CoreReactivity { get; }

        public double UncompensatedCoreReactivity
        {
            get { return CoreReactivity; }
        }

        public double CompensatedNetReactivity { get; }

        public double NetReactivity
        {
            get { return CompensatedNetReactivity; }
        }

        public double CommonModeRhoCorrection { get; }

        public int ControllerIterationCount { get; }

        public bool ControllerConverged { get; }

        public bool LowExhaustion
        {
            get { return AverageFillFraction <= 0.0; }
        }

        public bool HighExhaustion
        {
            get { return AverageFillFraction >= 1.0; }
        }

        public bool IsGameOver
        {
            get { return LowExhaustion || HighExhaustion; }
        }

        public string GameOverReason
        {
            get
            {
                if (LowExhaustion)
                {
                    return "liquid-zone-average-empty";
                }

                return HighExhaustion
                    ? "liquid-zone-average-full"
                    : string.Empty;
            }
        }

        public double SimulationTimeSeconds { get; }

        public StaticAbsorptionOverlayV1 AbsorptionOverlay { get; }

        public Digest32 MappingDigest
        {
            get { return Mapping.MappingDigest; }
        }

        public Digest32 OverlayDigest
        {
            get { return AbsorptionOverlay.OverlayDigest; }
        }

        public Digest32 StateDigest { get; }

        public string StateDigestHex
        {
            get { return DigestHex(StateDigest); }
        }

        public static ContractValidationResult<PracticeLiquidZoneRrsV1> TryCreate(
            PracticeLiquidZoneRrsMappingV1 mapping,
            EquilibriumCoreProjectionV1 initialEquilibrium,
            double simulationTimeSeconds = 0.0)
        {
            if (mapping == null)
            {
                return Invalid(
                    "PracticeLiquidZoneRrs.Mapping.Missing",
                    "mapping",
                    "A practice RRS requires its complete node mapping.");
            }

            if (initialEquilibrium == null)
            {
                return Invalid(
                    "PracticeLiquidZoneRrs.InitialEquilibrium.Missing",
                    "initial_equilibrium",
                    "A practice RRS requires the initial accepted equilibrium shape.");
            }

            if (!IsCanonicalTime(simulationTimeSeconds))
            {
                return Invalid(
                    "PracticeLiquidZoneRrs.Time.Invalid",
                    "simulation_time_s",
                    "Practice RRS time must be finite, nonnegative, and canonical.");
            }

            ContractValidationResult<ZonalPowerMeasurement> measurement =
                MeasureZonalPower(mapping, initialEquilibrium.ShapeNodePowerWatts);
            if (!measurement.IsValid)
            {
                return Invalid(
                    measurement.FirstDiagnostic.Code,
                    measurement.FirstDiagnostic.Path,
                    measurement.FirstDiagnostic.Message);
            }

            var fills = Enumerable.Repeat(
                PracticeLiquidZoneRrsIdentityV1.InitialFillFraction,
                (int)PracticeLiquidZoneRrsIdentityV1.LogicalZoneCount).ToArray();
            ContractValidationResult<StaticAbsorptionOverlayV1> overlay =
                mapping.TryBuildOverlay(fills);
            if (!overlay.IsValid)
            {
                return Invalid(
                    overlay.FirstDiagnostic.Code,
                    overlay.FirstDiagnostic.Path,
                    overlay.FirstDiagnostic.Message);
            }

            bool converged = Math.Abs(initialEquilibrium.RelativeReactivity) <=
                PracticeLiquidZoneRrsIdentityV1.ControllerTolerance;
            return ContractValidationResult<PracticeLiquidZoneRrsV1>.Valid(
                new PracticeLiquidZoneRrsV1(
                    mapping,
                    fills,
                    measurement.Value.Fractions,
                    measurement.Value.Fractions,
                    measurement.Value.Fractions,
                    Enumerable.Repeat(0.0, fills.Length),
                    overlay.Value,
                    initialEquilibrium.ShapePowerWatts,
                    initialEquilibrium.ShapePowerWatts,
                    initialEquilibrium.RelativeReactivity,
                    initialEquilibrium.RelativeReactivity,
                    0.0,
                    0,
                    converged,
                    simulationTimeSeconds));
        }

        public ContractValidationResult<StaticAbsorptionOverlayV1> TryBuildOverlay()
        {
            return Mapping.TryBuildOverlay(ZoneFills);
        }

        /// <summary>
        /// Advances only the shared event-clock binding. No controller or
        /// equilibrium solve is performed; short gameplay ticks reuse the
        /// accepted static projection and carry this immutable timestamp
        /// forward atomically with the burnup candidate.
        /// </summary>
        public ContractValidationResult<PracticeLiquidZoneRrsV1> TryWithSimulationTime(
            double simulationTimeSeconds)
        {
            if (!IsCanonicalTime(simulationTimeSeconds) ||
                simulationTimeSeconds + 1.0e-8 < SimulationTimeSeconds)
            {
                return Invalid(
                    "PracticeLiquidZoneRrs.Time.Order",
                    "simulation_time_s",
                    "An accepted RRS state may only move forward on the shared simulation clock.");
            }

            return ContractValidationResult<PracticeLiquidZoneRrsV1>.Valid(
                new PracticeLiquidZoneRrsV1(
                    Mapping,
                    ZoneFills,
                    ReferenceZonalPowerFractions,
                    TargetZonalPowerFractions,
                    MeasuredZonalPowerFractions,
                    ZonalShapeErrors,
                    AbsorptionOverlay,
                    TargetPowerWatts,
                    MeasuredPowerWatts,
                    CoreReactivity,
                    CompensatedNetReactivity,
                    CommonModeRhoCorrection,
                    ControllerIterationCount,
                    ControllerConverged,
                    simulationTimeSeconds));
        }

        /// <summary>
        /// Runs a bounded static RRS/equilibrium event. Each iteration is a
        /// complete full-core equilibrium solve; short gameplay ticks do not
        /// call this method and therefore reuse the accepted projection.
        /// </summary>
        public static ContractValidationResult<PracticeLiquidZoneRrsEquilibriumResultV1> TryRunEquilibrium(
            EquilibriumCoreSolverV1 equilibriumSolver,
            IEnumerable<BundleState> bundles,
            PracticeLiquidZoneRrsV1 previousState,
            double simulationTimeSeconds)
        {
            if (equilibriumSolver == null)
            {
                return InvalidRun(
                    "PracticeLiquidZoneRrs.Solver.Missing",
                    "equilibrium_solver",
                    "A practice RRS event requires the owning equilibrium solver.");
            }

            if (bundles == null)
            {
                return InvalidRun(
                    "PracticeLiquidZoneRrs.Bundles.Missing",
                    "bundles",
                    "A practice RRS event requires a complete bundle inventory.");
            }

            if (previousState == null)
            {
                return InvalidRun(
                    "PracticeLiquidZoneRrs.State.Missing",
                    "previous_state",
                    "A practice RRS event requires the last accepted immutable state.");
            }

            if (!IsCanonicalTime(simulationTimeSeconds) ||
                simulationTimeSeconds + 1.0e-8 < previousState.SimulationTimeSeconds)
            {
                return InvalidRun(
                    "PracticeLiquidZoneRrs.Time.Order",
                    "simulation_time_s",
                    "A practice RRS event must use a canonical time at or after the accepted state time.");
            }

            BundleState[] bundleRecords = bundles.ToArray();
            ContractValidationResult<EquilibriumCoreProjectionV1> baseCandidate =
                equilibriumSolver.TrySolveCandidate(bundleRecords);
            if (!baseCandidate.IsValid)
            {
                return InvalidRun(
                    baseCandidate.FirstDiagnostic.Code,
                    baseCandidate.FirstDiagnostic.Path,
                    baseCandidate.FirstDiagnostic.Message);
            }

            double[] fills = previousState.ZoneFills.ToArray();
            EquilibriumCoreProjectionV1 acceptedProjection = baseCandidate.Value;
            ZonalPowerMeasurement acceptedMeasurement;
            double coreReactivity = baseCandidate.Value.RelativeReactivity;
            int iterationCount = 0;
            bool controllerConverged = false;

            for (int iteration = 0;
                 iteration < PracticeLiquidZoneRrsIdentityV1.MaximumControllerIterations;
                 iteration++)
            {
                ContractValidationResult<StaticAbsorptionOverlayV1> overlay =
                    previousState.Mapping.TryBuildOverlay(fills);
                if (!overlay.IsValid)
                {
                    return InvalidRun(
                        overlay.FirstDiagnostic.Code,
                        overlay.FirstDiagnostic.Path,
                        overlay.FirstDiagnostic.Message);
                }

                ContractValidationResult<EquilibriumCoreProjectionV1> candidate =
                    equilibriumSolver.TrySolveCandidate(
                        bundleRecords,
                        overlay.Value,
                        acceptedProjection.SpatialSolve);
                if (!candidate.IsValid)
                {
                    return InvalidRun(
                        candidate.FirstDiagnostic.Code,
                        candidate.FirstDiagnostic.Path,
                        candidate.FirstDiagnostic.Message);
                }

                ContractValidationResult<ZonalPowerMeasurement> measurement =
                    MeasureZonalPower(
                        previousState.Mapping,
                        candidate.Value.ShapeNodePowerWatts,
                        previousState.TargetZonalPowerFractions);
                if (!measurement.IsValid)
                {
                    return InvalidRun(
                        measurement.FirstDiagnostic.Code,
                        measurement.FirstDiagnostic.Path,
                        measurement.FirstDiagnostic.Message);
                }

                acceptedProjection = candidate.Value;
                acceptedMeasurement = measurement.Value;
                iterationCount = iteration + 1;
                double maxShapeError = MaxAbsolute(acceptedMeasurement.Errors);
                controllerConverged =
                    maxShapeError <= PracticeLiquidZoneRrsIdentityV1.ControllerTolerance &&
                    Math.Abs(acceptedProjection.RelativeReactivity) <=
                    PracticeLiquidZoneRrsIdentityV1.ControllerTolerance;
                if (controllerConverged || iteration + 1 >= PracticeLiquidZoneRrsIdentityV1.MaximumControllerIterations)
                {
                    break;
                }

                bool fillChanged = false;
                for (int zone = 0; zone < fills.Length; zone++)
                {
                    double shapeCommand = -PracticeLiquidZoneRrsIdentityV1.ShapeErrorGain *
                        acceptedMeasurement.Errors[zone];
                    double commonModeCommand = PracticeLiquidZoneRrsIdentityV1.CommonModeRhoGain *
                        acceptedProjection.RelativeReactivity;
                    double increment = Clamp(
                        shapeCommand + commonModeCommand,
                        -PracticeLiquidZoneRrsIdentityV1.MaxFillIncrementPerIteration,
                        PracticeLiquidZoneRrsIdentityV1.MaxFillIncrementPerIteration);
                    double nextFill = Clamp(fills[zone] + increment, 0.0, 1.0);
                    if (Math.Abs(nextFill - fills[zone]) > 1.0e-15)
                    {
                        fillChanged = true;
                    }

                    fills[zone] = nextFill;
                }

                if (!fillChanged)
                {
                    break;
                }
            }

            // The loop always performs at least one solve when the state and
            // inventory are valid, so this assignment is only a defensive
            // guard for future changes to the bounded iteration policy.
            if (iterationCount == 0)
            {
                return InvalidRun(
                    "PracticeLiquidZoneRrs.Iteration.Empty",
                    "controller",
                    "The bounded practice RRS event did not produce an equilibrium candidate.");
            }

            ContractValidationResult<StaticAbsorptionOverlayV1> finalOverlay =
                previousState.Mapping.TryBuildOverlay(fills);
            if (!finalOverlay.IsValid)
            {
                return InvalidRun(
                    finalOverlay.FirstDiagnostic.Code,
                    finalOverlay.FirstDiagnostic.Path,
                    finalOverlay.FirstDiagnostic.Message);
            }

            ContractValidationResult<ZonalPowerMeasurement> finalMeasurement =
                MeasureZonalPower(
                    previousState.Mapping,
                    acceptedProjection.ShapeNodePowerWatts,
                    previousState.TargetZonalPowerFractions);
            if (!finalMeasurement.IsValid)
            {
                return InvalidRun(
                    finalMeasurement.FirstDiagnostic.Code,
                    finalMeasurement.FirstDiagnostic.Path,
                    finalMeasurement.FirstDiagnostic.Message);
            }

            double finalShapeError = MaxAbsolute(finalMeasurement.Value.Errors);
            controllerConverged =
                finalShapeError <= PracticeLiquidZoneRrsIdentityV1.ControllerTolerance &&
                Math.Abs(acceptedProjection.RelativeReactivity) <=
                PracticeLiquidZoneRrsIdentityV1.ControllerTolerance;
            var state = new PracticeLiquidZoneRrsV1(
                previousState.Mapping,
                fills,
                previousState.ReferenceZonalPowerFractions,
                previousState.TargetZonalPowerFractions,
                finalMeasurement.Value.Fractions,
                finalMeasurement.Value.Errors,
                finalOverlay.Value,
                equilibriumSolver.TargetPowerWatts,
                acceptedProjection.ShapePowerWatts,
                coreReactivity,
                acceptedProjection.RelativeReactivity,
                acceptedProjection.RelativeReactivity - coreReactivity,
                iterationCount,
                controllerConverged,
                simulationTimeSeconds);

            return ContractValidationResult<PracticeLiquidZoneRrsEquilibriumResultV1>.Valid(
                new PracticeLiquidZoneRrsEquilibriumResultV1(state, acceptedProjection));
        }

        private Digest32 ComputeStateDigest()
        {
            return new Digest32(Phase5CanonicalBytesV1.HashBody(
                "CANDU-PRACTICE-LIQUID-ZONE-RRS-STATE-V1",
                writer =>
                {
                    Phase5CanonicalBytesV1.WriteUInt32(
                        writer,
                        PracticeLiquidZoneRrsIdentityV1.CurrentSchemaVersion);
                    Phase5CanonicalBytesV1.WriteString(
                        writer,
                        PracticeLiquidZoneRrsIdentityV1.ControllerIdentity);
                    Phase5CanonicalBytesV1.WriteDigest(writer, Mapping.MappingDigest);
                    Phase5CanonicalBytesV1.WriteDigest(writer, AbsorptionOverlay.OverlayDigest);
                    Phase5CanonicalBytesV1.WriteDouble(writer, SimulationTimeSeconds);
                    Phase5CanonicalBytesV1.WriteDouble(writer, TargetPowerWatts);
                    Phase5CanonicalBytesV1.WriteDouble(writer, MeasuredPowerWatts);
                    Phase5CanonicalBytesV1.WriteDouble(writer, CoreReactivity);
                    Phase5CanonicalBytesV1.WriteDouble(writer, CompensatedNetReactivity);
                    Phase5CanonicalBytesV1.WriteDouble(writer, CommonModeRhoCorrection);
                    Phase5CanonicalBytesV1.WriteUInt32(
                        writer,
                        checked((uint)ControllerIterationCount));
                    writer.Write(ControllerConverged ? (byte)1 : (byte)0);
                    WriteDoubles(writer, _zoneFills);
                    WriteDoubles(writer, _referenceZonalPowerFractions);
                    WriteDoubles(writer, _targetZonalPowerFractions);
                    WriteDoubles(writer, _measuredZonalPowerFractions);
                    WriteDoubles(writer, _zonalShapeErrors);
                }));
        }

        private static void WriteDoubles(
            System.IO.BinaryWriter writer,
            ReadOnlyCollection<double> values)
        {
            Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)values.Count));
            for (int index = 0; index < values.Count; index++)
            {
                Phase5CanonicalBytesV1.WriteDouble(writer, values[index]);
            }
        }

        private static ContractValidationResult<ZonalPowerMeasurement> MeasureZonalPower(
            PracticeLiquidZoneRrsMappingV1 mapping,
            IReadOnlyList<double> nodePowerWatts,
            IReadOnlyList<double>? targetFractions = null)
        {
            if (nodePowerWatts == null ||
                nodePowerWatts.Count != (int)PracticeLiquidZoneRrsIdentityV1.NodeCount)
            {
                return ContractValidationResult<ZonalPowerMeasurement>.Invalid(
                    "PracticeLiquidZoneRrs.Measurement.NodeCountMismatch",
                    "node_power_watts",
                    "RRS zonal measurement requires exactly one power value per diffusion node.");
            }

            double totalPowerWatts = 0.0;
            for (int index = 0; index < nodePowerWatts.Count; index++)
            {
                double power = nodePowerWatts[index];
                if (!IsCanonicalNonnegative(power))
                {
                    return ContractValidationResult<ZonalPowerMeasurement>.Invalid(
                        "PracticeLiquidZoneRrs.Measurement.Power.Invalid",
                        "node_power_watts[" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]",
                        "RRS zonal measurement requires finite nonnegative node power.");
                }

                totalPowerWatts += power;
            }

            if (!IsCanonicalPositive(totalPowerWatts))
            {
                return ContractValidationResult<ZonalPowerMeasurement>.Invalid(
                    "PracticeLiquidZoneRrs.Measurement.TotalPower.Invalid",
                    "node_power_watts",
                    "RRS zonal measurement requires strictly positive total power.");
            }

            var fractions = new double[PracticeLiquidZoneRrsIdentityV1.LogicalZoneCount];
            if (targetFractions != null &&
                targetFractions.Count != (int)PracticeLiquidZoneRrsIdentityV1.LogicalZoneCount)
            {
                return ContractValidationResult<ZonalPowerMeasurement>.Invalid(
                    "PracticeLiquidZoneRrs.Measurement.TargetCountMismatch",
                    "target_zonal_power_fractions",
                    "The RRS target shape must contain exactly fourteen zone fractions.");
            }

            var errors = new double[PracticeLiquidZoneRrsIdentityV1.LogicalZoneCount];
            for (int zone = 0; zone < fractions.Length; zone++)
            {
                double zonePower = 0.0;
                foreach (int nodeIndex in mapping.NodeIndicesByZone[zone])
                {
                    zonePower += nodePowerWatts[nodeIndex];
                }

                fractions[zone] = zonePower / totalPowerWatts;
                if (!IsCanonicalNonnegative(fractions[zone]))
                {
                    return ContractValidationResult<ZonalPowerMeasurement>.Invalid(
                        "PracticeLiquidZoneRrs.Measurement.Fraction.Invalid",
                        "zones[" + zone.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]",
                        "RRS zonal power fractions must remain finite and nonnegative.");
                }

                errors[zone] = targetFractions == null
                    ? 0.0
                    : targetFractions[zone] - fractions[zone];
                if (!IsCanonicalFinite(errors[zone]))
                {
                    return ContractValidationResult<ZonalPowerMeasurement>.Invalid(
                        "PracticeLiquidZoneRrs.Measurement.Error.Invalid",
                        "zones[" + zone.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]",
                        "RRS zonal shape error must remain finite and canonical.");
                }
            }

            return ContractValidationResult<ZonalPowerMeasurement>.Valid(
                new ZonalPowerMeasurement(totalPowerWatts, fractions, errors));
        }

        private static double MaxAbsolute(double[] values)
        {
            double maximum = 0.0;
            for (int index = 0; index < values.Length; index++)
            {
                maximum = Math.Max(maximum, Math.Abs(values[index]));
            }

            return maximum;
        }

        private static ReadOnlyCollection<double> Copy(IEnumerable<double> values)
        {
            return new ReadOnlyCollection<double>(values.ToArray());
        }

        private static bool IsCanonicalTime(double value)
        {
            return IsCanonicalFinite(value) && value >= 0.0;
        }

        private static bool IsCanonicalPositive(double value)
        {
            return IsCanonicalFinite(value) && value > 0.0;
        }

        private static bool IsCanonicalNonnegative(double value)
        {
            return IsCanonicalFinite(value) && value >= 0.0;
        }

        private static bool IsCanonicalFinite(double value)
        {
            return ContractValidation.IsFinite(value) &&
                   (value != 0.0 || BitConverter.DoubleToInt64Bits(value) >= 0);
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static string DigestHex(Digest32 digest)
        {
            var builder = new StringBuilder(digest.Bytes.Count * 2 + 7);
            builder.Append("sha256:");
            foreach (byte value in digest.Bytes)
            {
                builder.Append(value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static ContractValidationResult<PracticeLiquidZoneRrsV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<PracticeLiquidZoneRrsV1>.Invalid(code, path, message);
        }

        private static ContractValidationResult<PracticeLiquidZoneRrsEquilibriumResultV1> InvalidRun(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<PracticeLiquidZoneRrsEquilibriumResultV1>.Invalid(
                code,
                path,
                message);
        }

        private sealed class ZonalPowerMeasurement
        {
            internal ZonalPowerMeasurement(
                double totalPowerWatts,
                IEnumerable<double> fractions,
                IEnumerable<double> errors)
            {
                TotalPowerWatts = totalPowerWatts;
                Fractions = fractions.ToArray();
                Errors = errors.ToArray();
            }

            internal double TotalPowerWatts { get; }

            internal double[] Fractions { get; }

            internal double[] Errors { get; }
        }
    }

    /// <summary>
    /// Immutable pair returned by one bounded practice RRS event. The caller
    /// commits both values together with its inventory transaction.
    /// </summary>
    public sealed class PracticeLiquidZoneRrsEquilibriumResultV1
    {
        internal PracticeLiquidZoneRrsEquilibriumResultV1(
            PracticeLiquidZoneRrsV1 state,
            EquilibriumCoreProjectionV1 projection)
        {
            State = state;
            Projection = projection;
        }

        public PracticeLiquidZoneRrsV1 State { get; }

        public EquilibriumCoreProjectionV1 Projection { get; }

        public StaticAbsorptionOverlayV1 AbsorptionOverlay
        {
            get { return State.AbsorptionOverlay; }
        }
    }
}
