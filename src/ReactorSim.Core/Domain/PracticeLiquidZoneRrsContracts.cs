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
        public const double AbsorptionReferenceFillFraction = 0.0;
        public const double CalibratedTotalZoneWorthMk = 7.0;
        public const double Group1AbsorptionPerMPerFillFraction = 0.005554638539404282;
        public const double Group2AbsorptionPerMPerFillFraction = 0.055546385394042816;
        public const int ResponseVariableCount = (int)LogicalZoneCount;
        public const int ResponseOutputCount = ResponseVariableCount + 1;
        public const double ResponseGroup1AbsorptionWeight = 0.65;
        public const double ResponseGroup2AbsorptionWeight = 0.35;
        public const double ResponseShapeSensitivityScale = 40.0;
        public const double ResponseCommonModeSensitivityScale = 5.0;
        public const double ResponseShapeResidualWeight = 1.0;
        public const double ResponseCommonModeResidualWeight = 20.0;
        public const double ResponseRegularization = 1.0e-6;
        public const double MaxFillMovementPerEvent = 0.08;
        public const double MaxFillIncrementPerIteration = MaxFillMovementPerEvent;
        public const double FillCommandTolerance = 1.0e-12;
        // Absolute regional share error, expressed as a fraction of total power.
        public const double RegionalShapeTolerancePercentagePoints = 1.0;
        public const double ControllerTolerance = RegionalShapeTolerancePercentagePoints / 100.0;
        // Reactivity is dimensionless; 1 mk = 1e-3 rho.
        public const double CriticalityToleranceMk = 0.05;
        public const double CriticalityTolerance = CriticalityToleranceMk * 1.0e-3;
        public const double ResidualAcceptanceTolerance = 1.0e-10;
        public const int MaximumCandidateSolveCount = 4;
        public const int MaximumControllerPasses = 1;
        public const int MaximumControllerIterations = MaximumControllerPasses;
        public const string ControllerIdentity = "synthetic-practice-liquid-zone-criticality-first-rrs-v4";
        public const string ResponseModelIdentity =
            "synthetic-practice-liquid-zone-response-common-shape-v3";
        public const string MappingIdentity = "candu6-regions-localized-tubes-moving-water-380x12-v4";
        public const string OverlayIdentity = "synthetic-practice-liquid-zone-tube-water-absorption-v3";
        public const string CadenceIdentity = "equilibrium-three-minute-step-and-event-rrs-v3";
        public const string Provenance =
            "project-authored-synthetic; St-Aubin/Marleau 2018 Figs1-2 six tube layout; one-pitch by one-bundle homogenization; vertical endpoints rounded to lattice boundaries; bottom-up water overlap; independent regional measurement; authored absorption calibrated to 7 mk with seed-1001 reference critical at half fill";
    }

    /// <summary>
    /// One complete-map binding from a diffusion node to one of fourteen
    /// practice liquid zones. Weights are full-water absorption deltas in m^-1.
    /// Static masks scale linearly with fill; tube cells use WaterColumn overlap.
    /// </summary>
    public sealed class PracticeLiquidZoneRrsNodeBindingV1
    {
        public PracticeLiquidZoneRrsNodeBindingV1(
            NodeKey node,
            uint logicalZoneId,
            double group1AbsorptionPerMPerFillFraction,
            double group2AbsorptionPerMPerFillFraction,
            uint? absorberZoneId = null,
            PracticeLiquidZoneWaterColumnV1? waterColumn = null)
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
            AbsorberZoneId = absorberZoneId ?? logicalZoneId;
            if (AbsorberZoneId >= PracticeLiquidZoneRrsIdentityV1.LogicalZoneCount)
                throw new ArgumentOutOfRangeException(nameof(absorberZoneId));
            Group1AbsorptionPerMPerFillFraction = group1AbsorptionPerMPerFillFraction;
            Group2AbsorptionPerMPerFillFraction = group2AbsorptionPerMPerFillFraction;
            WaterColumn = waterColumn;
        }

        public NodeKey Node { get; }
        public PracticeLiquidZoneWaterColumnV1? WaterColumn { get; }

        public uint LogicalZoneId { get; }
        /// <summary>Fill compartment driving absorption at this node; independent of its measured power region.</summary>
        public uint AbsorberZoneId { get; }

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
    /// Seven regions in each axial half: two left, three centre, two right.
    /// Traditional numbering follows St-Aubin and Marleau (2018), Figure 2.
    /// Region boundaries are discretized onto the game's 22 by 22 lattice;
    /// absorption weights remain project-authored approximations.
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

        public static ContractValidationResult<PracticeLiquidZoneRrsMappingV1> TryCreateCandu6(
            double group1Strength = PracticeLiquidZoneRrsIdentityV1.Group1AbsorptionPerMPerFillFraction,
            double group2Strength = PracticeLiquidZoneRrsIdentityV1.Group2AbsorptionPerMPerFillFraction)
        {
            if (!ContractValidation.IsFinite(group1Strength) || !ContractValidation.IsFinite(group2Strength) ||
                group1Strength < 0 || group2Strength < 0)
                return Invalid("PracticeLiquidZoneRrs.Absorption.Invalid", "strength", "Tube absorption strengths must be finite and nonnegative.");
            if (group1Strength == 0) group1Strength = 0;
            if (group2Strength == 0) group2Strength = 0;
            var nodes = new List<PracticeLiquidZoneRrsNodeBindingV1>(
                checked((int)PracticeLiquidZoneRrsIdentityV1.NodeCount));
            for (uint channel = 0; channel < PracticeLiquidZoneRrsIdentityV1.ChannelCount; channel++)
            {
                Candu6GridPositionV1 position = Candu6CoreTopologyFactoryV1.GetPosition(channel);
                // Display rows run top to bottom. Plant zones 1..7 are
                // lower-left, upper-left, lower-centre, centre, upper-centre,
                // lower-right, upper-right; zones 8..14 repeat axially.
                uint faceZone = position.Column < 7
                    ? (position.DisplayRow < 11 ? 1U : 0U)
                    : position.Column >= 15
                        ? (position.DisplayRow < 11 ? 6U : 5U)
                        : position.DisplayRow < 8 ? 4U
                            : position.DisplayRow < 14 ? 3U : 2U;
                for (uint bundlePosition = 0;
                     bundlePosition < PracticeLiquidZoneRrsIdentityV1.BundlePositionCount;
                     bundlePosition++)
                {
                    uint axialHalf = bundlePosition <
                        PracticeLiquidZoneRrsIdentityV1.BundlePositionCount / 2U
                        ? 0U
                        : 1U;
                    uint zone = checked(axialHalf * 7U + faceZone);
                    var node = new NodeKey(new ChannelId(channel), new BundlePosition(bundlePosition));
                    var tube = PracticeLiquidZoneTubesV1.Binding(node);
                    nodes.Add(new PracticeLiquidZoneRrsNodeBindingV1(
                        node,
                        zone,
                        group1Strength * tube.Fraction,
                        group2Strength * tube.Fraction,
                        tube.Tube?.ZoneId ?? zone,
                        tube.Water));
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
                        Phase5CanonicalBytesV1.WriteUInt32(writer, node.AbsorberZoneId);
                        Phase5CanonicalBytesV1.WriteDouble(
                            writer,
                            node.Group1AbsorptionPerMPerFillFraction);
                        Phase5CanonicalBytesV1.WriteDouble(
                            writer,
                            node.Group2AbsorptionPerMPerFillFraction);
                        Phase5CanonicalBytesV1.WriteUInt32(writer, node.WaterColumn == null ? 0U : 1U);
                        if (node.WaterColumn != null)
                        {
                            Phase5CanonicalBytesV1.WriteDouble(writer, node.WaterColumn.BottomM);
                            Phase5CanonicalBytesV1.WriteDouble(writer, node.WaterColumn.TopM);
                            Phase5CanonicalBytesV1.WriteDouble(writer, node.WaterColumn.CellBottomM);
                            Phase5CanonicalBytesV1.WriteDouble(writer, node.WaterColumn.CellTopM);
                        }
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

            if (zoneFills.Any(fill => !IsCanonicalFraction(fill)))
                return ContractValidationResult<StaticAbsorptionOverlayV1>.Invalid(
                    "PracticeLiquidZoneRrs.Fill.Invalid", "zone_fills",
                    "Every practice RRS zone fill must be finite and within [0,1].");
            var entries = new List<StaticAbsorptionOverlayEntryV1>(_nodes.Count);
            for (int nodeIndex = 0; nodeIndex < _nodes.Count; nodeIndex++)
            {
                PracticeLiquidZoneRrsNodeBindingV1 binding = _nodes[nodeIndex];
                double fill = zoneFills[(int)binding.AbsorberZoneId];
                if (!IsCanonicalFraction(fill))
                {
                    return ContractValidationResult<StaticAbsorptionOverlayV1>.Invalid(
                        "PracticeLiquidZoneRrs.Fill.Invalid",
                        "zone_fills[" + binding.LogicalZoneId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]",
                        "Every practice RRS zone fill must be finite and within [0,1].");
                }

                double deltaFill = fill - PracticeLiquidZoneRrsIdentityV1.AbsorptionReferenceFillFraction;
                if (binding.WaterColumn != null) deltaFill = binding.WaterColumn.FilledOverlapFraction(fill);
                double group1 = deltaFill * binding.Group1AbsorptionPerMPerFillFraction;
                double group2 = deltaFill * binding.Group2AbsorptionPerMPerFillFraction;
                // An inactive footprint remains canonical zero even when the compartment drains.
                if (group1 == 0) group1 = 0;
                if (group2 == 0) group2 = 0;
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
    /// Project-authored local response model for the practice RRS. Each
    /// column is the predicted change in the residual vector
    /// <c>[target_fraction - measured_fraction, rho]</c> for one unit of fill
    /// movement in the corresponding logical zone. The first fourteen rows
    /// conserve normalized shape by construction; the final row is the
    /// explicit common-mode reactivity response. This is a gameplay
    /// surrogate, not a transient or a replacement for the authoritative
    /// full-core solve.
    /// </summary>
    public sealed class PracticeLiquidZoneRrsResponseModelV1
    {
        private readonly ReadOnlyCollection<double> _baselineZonalPowerFractions;
        private readonly ReadOnlyCollection<uint> _variableOrder;
        private readonly ReadOnlyCollection<IReadOnlyList<double>> _jacobian;
        private readonly ReadOnlyCollection<double> _residualWeights;
        private readonly ReadOnlyCollection<double> _commonModeReactivitySensitivities;

        private PracticeLiquidZoneRrsResponseModelV1(
            IEnumerable<double> baselineZonalPowerFractions,
            IEnumerable<uint> variableOrder,
            IEnumerable<IEnumerable<double>> jacobian,
            IEnumerable<double> residualWeights,
            IEnumerable<double> commonModeReactivitySensitivities,
            double effectiveAbsorptionPerMPerFillFraction)
        {
            _baselineZonalPowerFractions = new ReadOnlyCollection<double>(
                baselineZonalPowerFractions.ToArray());
            _variableOrder = new ReadOnlyCollection<uint>(variableOrder.ToArray());
            _jacobian = new ReadOnlyCollection<IReadOnlyList<double>>(
                jacobian
                    .Select(row => (IReadOnlyList<double>)new ReadOnlyCollection<double>(row.ToArray()))
                    .ToArray());
            _residualWeights = new ReadOnlyCollection<double>(residualWeights.ToArray());
            _commonModeReactivitySensitivities = new ReadOnlyCollection<double>(
                commonModeReactivitySensitivities.ToArray());
            EffectiveAbsorptionPerMPerFillFraction = effectiveAbsorptionPerMPerFillFraction;
            Identity = PracticeLiquidZoneRrsIdentityV1.ResponseModelIdentity;
            Provenance = PracticeLiquidZoneRrsIdentityV1.Provenance;
            Regularization = PracticeLiquidZoneRrsIdentityV1.ResponseRegularization;
            ShapeSensitivityScale = PracticeLiquidZoneRrsIdentityV1.ResponseShapeSensitivityScale;
            CommonModeReactivitySensitivityScale =
                PracticeLiquidZoneRrsIdentityV1.ResponseCommonModeSensitivityScale;
            ModelDigest = ComputeModelDigest();
        }

        public string Identity { get; }

        internal PracticeLiquidZoneRrsResponseModelV1 WithBaselineFractions(
            IEnumerable<double> fractions)
        {
            return new PracticeLiquidZoneRrsResponseModelV1(
                fractions, VariableOrder, Jacobian, ResidualWeights,
                CommonModeReactivitySensitivities, EffectiveAbsorptionPerMPerFillFraction);
        }

        /// <summary>
        /// Correct the response along a measured fill movement (Broyden secant).
        /// This uses the authoritative diffusion result, including spatial
        /// coupling, instead of repeating an inaccurate estimated command.
        /// </summary>
        internal PracticeLiquidZoneRrsResponseModelV1 WithMeasuredResponse(
            double[] fillMovement,
            double[] initialShapeErrors,
            double initialReactivity,
            double[] measuredShapeErrors,
            double measuredReactivity)
        {
            double normSquared = fillMovement.Sum(value => value * value);
            if (normSquared <= 1.0e-24)
            {
                return this;
            }

            var rows = new double[OutputCount][];
            for (int output = 0; output < OutputCount; output++)
            {
                rows[output] = Jacobian[output].ToArray();
                double observedChange = output < VariableCount
                    ? measuredShapeErrors[output] - initialShapeErrors[output]
                    : measuredReactivity - initialReactivity;
                double predictedChange = 0.0;
                for (int variable = 0; variable < VariableCount; variable++)
                {
                    predictedChange += rows[output][variable] * fillMovement[variable];
                }

                double correction = (observedChange - predictedChange) / normSquared;
                for (int variable = 0; variable < VariableCount; variable++)
                {
                    rows[output][variable] += correction * fillMovement[variable];
                }
            }

            return new PracticeLiquidZoneRrsResponseModelV1(
                BaselineZonalPowerFractions,
                VariableOrder,
                rows,
                ResidualWeights,
                rows[VariableCount],
                EffectiveAbsorptionPerMPerFillFraction);
        }

        public string ResponseModelIdentity
        {
            get { return Identity; }
        }

        public string Provenance { get; }

        public int VariableCount
        {
            get { return _variableOrder.Count; }
        }

        public int ResponseVariableCount
        {
            get { return VariableCount; }
        }

        public int OutputCount
        {
            get { return _jacobian.Count; }
        }

        public int ResponseOutputCount
        {
            get { return OutputCount; }
        }

        public IReadOnlyList<uint> VariableOrder
        {
            get { return _variableOrder; }
        }

        public IReadOnlyList<uint> LogicalZoneOrder
        {
            get { return VariableOrder; }
        }

        public IReadOnlyList<double> BaselineZonalPowerFractions
        {
            get { return _baselineZonalPowerFractions; }
        }

        public IReadOnlyList<IReadOnlyList<double>> Jacobian
        {
            get { return _jacobian; }
        }

        public IReadOnlyList<IReadOnlyList<double>> ResponseJacobian
        {
            get { return Jacobian; }
        }

        public IReadOnlyList<double> ResidualWeights
        {
            get { return _residualWeights; }
        }

        public IReadOnlyList<double> OutputWeights
        {
            get { return ResidualWeights; }
        }

        public IReadOnlyList<double> CommonModeReactivitySensitivities
        {
            get { return _commonModeReactivitySensitivities; }
        }

        public double EffectiveAbsorptionPerMPerFillFraction { get; }

        /// <summary>
        /// Positive project-authored magnitude for a unit common-mode fill
        /// change over a normalized full-core shape. Individual Jacobian
        /// entries are negative because increasing fill adds absorption.
        /// </summary>
        public double CommonModeReactivitySensitivity
        {
            get
            {
                return EffectiveAbsorptionPerMPerFillFraction *
                       CommonModeReactivitySensitivityScale;
            }
        }

        public double ShapeSensitivityScale { get; }

        public double CommonModeReactivitySensitivityScale { get; }

        public double ShapeResidualWeight
        {
            get { return _residualWeights[0]; }
        }

        public double CommonModeReactivityWeight
        {
            get { return _residualWeights[_residualWeights.Count - 1]; }
        }

        public double Regularization { get; }

        public Digest32 ModelDigest { get; }

        public Digest32 ResponseModelDigest
        {
            get { return ModelDigest; }
        }

        public static ContractValidationResult<PracticeLiquidZoneRrsResponseModelV1> TryCreate(
            IReadOnlyList<double> baselineZonalPowerFractions)
        {
            if (baselineZonalPowerFractions == null)
            {
                return ContractValidationResult<PracticeLiquidZoneRrsResponseModelV1>.Invalid(
                    "PracticeLiquidZoneRrs.Response.Baseline.Missing",
                    "baseline_zonal_power_fractions",
                    "A practice RRS response model requires measured baseline zone fractions.");
            }

            if (baselineZonalPowerFractions.Count !=
                PracticeLiquidZoneRrsIdentityV1.ResponseVariableCount)
            {
                return ContractValidationResult<PracticeLiquidZoneRrsResponseModelV1>.Invalid(
                    "PracticeLiquidZoneRrs.Response.Baseline.CountMismatch",
                    "baseline_zonal_power_fractions",
                    "A practice RRS response model requires exactly fourteen baseline zone fractions.");
            }

            double totalFraction = 0.0;
            for (int zone = 0; zone < baselineZonalPowerFractions.Count; zone++)
            {
                double fraction = baselineZonalPowerFractions[zone];
                if (!IsCanonicalNonnegative(fraction))
                {
                    return ContractValidationResult<PracticeLiquidZoneRrsResponseModelV1>.Invalid(
                        "PracticeLiquidZoneRrs.Response.Baseline.Invalid",
                        "baseline_zonal_power_fractions[" +
                        zone.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]",
                        "Every response-model baseline fraction must be finite and nonnegative.");
                }

                totalFraction += fraction;
            }

            if (!IsCanonicalPositive(totalFraction))
            {
                return ContractValidationResult<PracticeLiquidZoneRrsResponseModelV1>.Invalid(
                    "PracticeLiquidZoneRrs.Response.Baseline.Empty",
                    "baseline_zonal_power_fractions",
                    "A practice RRS response model requires a strictly positive baseline shape.");
            }

            var normalizedFractions = new double[
                PracticeLiquidZoneRrsIdentityV1.ResponseVariableCount];
            for (int zone = 0; zone < normalizedFractions.Length; zone++)
            {
                normalizedFractions[zone] = baselineZonalPowerFractions[zone] / totalFraction;
            }

            // Initial regional-response heuristic is independent of localized device
            // strength; measured secant responses replace it during controller trials.
            double effectiveAbsorption =
                0.0017640042499876617 *
                PracticeLiquidZoneRrsIdentityV1.ResponseGroup1AbsorptionWeight +
                0.0007056016999950648 *
                PracticeLiquidZoneRrsIdentityV1.ResponseGroup2AbsorptionWeight;
            if (!IsCanonicalPositive(effectiveAbsorption))
            {
                return ContractValidationResult<PracticeLiquidZoneRrsResponseModelV1>.Invalid(
                    "PracticeLiquidZoneRrs.Response.Absorption.Invalid",
                    "effective_absorption_m_inverse",
                    "The project-authored response absorption sensitivity must be finite and positive.");
            }

            var variableOrder = new uint[
                PracticeLiquidZoneRrsIdentityV1.ResponseVariableCount];
            for (int variable = 0; variable < variableOrder.Length; variable++)
            {
                variableOrder[variable] = checked((uint)variable);
            }

            var jacobian = new double[PracticeLiquidZoneRrsIdentityV1.ResponseOutputCount][];
            for (int output = 0; output < jacobian.Length; output++)
            {
                jacobian[output] = new double[
                    PracticeLiquidZoneRrsIdentityV1.ResponseVariableCount];
            }

            var commonModeSensitivities = new double[
                PracticeLiquidZoneRrsIdentityV1.ResponseVariableCount];
            for (int variable = 0; variable < variableOrder.Length; variable++)
            {
                double zoneFraction = normalizedFractions[variable];
                double localPowerLeverage =
                    effectiveAbsorption *
                    PracticeLiquidZoneRrsIdentityV1.ResponseShapeSensitivityScale *
                    zoneFraction;
                for (int output = 0;
                     output < PracticeLiquidZoneRrsIdentityV1.ResponseVariableCount;
                     output++)
                {
                    double conservationAwareContrast = output == variable
                        ? 1.0 - normalizedFractions[output]
                        : -normalizedFractions[output];
                    jacobian[output][variable] =
                        localPowerLeverage * conservationAwareContrast;
                }

                double commonModeSensitivity =
                    -effectiveAbsorption *
                    PracticeLiquidZoneRrsIdentityV1.ResponseCommonModeSensitivityScale *
                    zoneFraction;
                jacobian[PracticeLiquidZoneRrsIdentityV1.ResponseVariableCount][variable] =
                    commonModeSensitivity;
                commonModeSensitivities[variable] = commonModeSensitivity;
            }

            var residualWeights = new double[PracticeLiquidZoneRrsIdentityV1.ResponseOutputCount];
            for (int output = 0;
                 output < PracticeLiquidZoneRrsIdentityV1.ResponseVariableCount;
                 output++)
            {
                residualWeights[output] =
                    PracticeLiquidZoneRrsIdentityV1.ResponseShapeResidualWeight;
            }

            residualWeights[PracticeLiquidZoneRrsIdentityV1.ResponseVariableCount] =
                PracticeLiquidZoneRrsIdentityV1.ResponseCommonModeResidualWeight;

            return ContractValidationResult<PracticeLiquidZoneRrsResponseModelV1>.Valid(
                new PracticeLiquidZoneRrsResponseModelV1(
                    normalizedFractions,
                    variableOrder,
                    jacobian,
                    residualWeights,
                    commonModeSensitivities,
                    effectiveAbsorption));
        }

        private Digest32 ComputeModelDigest()
        {
            return new Digest32(Phase5CanonicalBytesV1.HashBody(
                "CANDU-PRACTICE-LIQUID-ZONE-RRS-RESPONSE-MODEL-V1",
                writer =>
                {
                    Phase5CanonicalBytesV1.WriteString(writer, Identity);
                    Phase5CanonicalBytesV1.WriteString(writer, Provenance);
                    Phase5CanonicalBytesV1.WriteUInt32(
                        writer,
                        checked((uint)VariableCount));
                    Phase5CanonicalBytesV1.WriteUInt32(
                        writer,
                        checked((uint)OutputCount));
                    for (int index = 0; index < _variableOrder.Count; index++)
                    {
                        Phase5CanonicalBytesV1.WriteUInt32(writer, _variableOrder[index]);
                    }

                    WriteDoubles(writer, _baselineZonalPowerFractions);
                    WriteDoubles(writer, _residualWeights);
                    Phase5CanonicalBytesV1.WriteDouble(
                        writer,
                        EffectiveAbsorptionPerMPerFillFraction);
                    Phase5CanonicalBytesV1.WriteDouble(writer, ShapeSensitivityScale);
                    Phase5CanonicalBytesV1.WriteDouble(
                        writer,
                        CommonModeReactivitySensitivityScale);
                    Phase5CanonicalBytesV1.WriteDouble(writer, Regularization);
                    for (int row = 0; row < _jacobian.Count; row++)
                    {
                        WriteDoubles(writer, _jacobian[row]);
                    }
                }));
        }

        private static void WriteDoubles(
            System.IO.BinaryWriter writer,
            IReadOnlyList<double> values)
        {
            Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)values.Count));
            for (int index = 0; index < values.Count; index++)
            {
                Phase5CanonicalBytesV1.WriteDouble(writer, values[index]);
            }
        }

        private static bool IsCanonicalPositive(double value)
        {
            return IsCanonicalFinite(value) && value > 0.0;
        }

        private static bool IsCanonicalNonnegative(double value)
        {
            return IsCanonicalFinite(value) && value >= 0.0;
        }

        private static bool IsCanonicalFraction(double value)
        {
            return IsCanonicalFinite(value) && value >= 0.0 && value <= 1.0;
        }

        private static bool IsCanonicalFinite(double value)
        {
            return ContractValidation.IsFinite(value) &&
                   (value != 0.0 || BitConverter.DoubleToInt64Bits(value) >= 0);
        }
    }

    /// <summary>
    /// Immutable accepted practice RRS state. It records the reference shape,
    /// current measured zonal shape, bounded fills, and the last static
    /// controller result. No kinetics or time integration is represented.
    /// </summary>
    public sealed partial class PracticeLiquidZoneRrsV1
    {
        private readonly ReadOnlyCollection<double> _zoneFills;
        private readonly ReadOnlyCollection<double> _referenceZonalPowerFractions;
        private readonly ReadOnlyCollection<double> _targetZonalPowerFractions;
        private readonly ReadOnlyCollection<double> _measuredZonalPowerFractions;
        private readonly ReadOnlyCollection<double> _zonalShapeErrors;
        private readonly ReadOnlyCollection<double> _appliedFillCommand;
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
            double simulationTimeSeconds,
            PracticeLiquidZoneRrsResponseModelV1 responseModel,
            PracticeLiquidZoneRrsResponseModelV1? correctionResponseModel,
            IEnumerable<double> appliedFillCommand,
            double uncompensatedWeightedResidual,
            double controlledBaselineWeightedResidual,
            double combinedWeightedResidual,
            int baseCandidateSolveCount,
            int controlledBaselineCandidateSolveCount,
            int verificationCandidateSolveCount,
            int correctionCandidateSolveCount,
            bool correctionApplied,
            string decisionCode = "initial-reference")
        {
            Mapping = mapping;
            _zoneFills = Copy(zoneFills);
            _referenceZonalPowerFractions = Copy(referenceZonalPowerFractions);
            _targetZonalPowerFractions = Copy(targetZonalPowerFractions);
            _measuredZonalPowerFractions = Copy(measuredZonalPowerFractions);
            _zonalShapeErrors = Copy(zonalShapeErrors);
            _appliedFillCommand = Copy(appliedFillCommand);
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
            ResponseModel = responseModel;
            CorrectionResponseModel = correctionResponseModel;
            UncompensatedWeightedResidual = uncompensatedWeightedResidual;
            ControlledBaselineWeightedResidual = controlledBaselineWeightedResidual;
            CombinedWeightedResidual = combinedWeightedResidual;
            BaseCandidateSolveCount = baseCandidateSolveCount;
            ControlledBaselineCandidateSolveCount = controlledBaselineCandidateSolveCount;
            VerificationCandidateSolveCount = verificationCandidateSolveCount;
            CorrectionCandidateSolveCount = correctionCandidateSolveCount;
            TotalCandidateSolveCount =
                baseCandidateSolveCount +
                controlledBaselineCandidateSolveCount +
                verificationCandidateSolveCount +
                correctionCandidateSolveCount;
            CorrectionApplied = correctionApplied;
            DecisionCode = LowExhaustion ? "exhausted-empty" : HighExhaustion ? "exhausted-full" : decisionCode;
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

        /// <summary>
        /// One immutable, project-authored response model used to compute the
        /// command for this event. Its variables are always logical zones
        /// zero through thirteen.
        /// </summary>
        public PracticeLiquidZoneRrsResponseModelV1 ResponseModel { get; }

        public PracticeLiquidZoneRrsResponseModelV1? CorrectionResponseModel { get; }

        public string ResponseModelIdentity
        {
            get { return ResponseModel.Identity; }
        }

        public Digest32 ResponseModelDigest
        {
            get { return ResponseModel.ModelDigest; }
        }

        public int ResponseVariableCount
        {
            get { return ResponseModel.VariableCount; }
        }

        public int ResponseOutputCount
        {
            get { return ResponseModel.OutputCount; }
        }

        public IReadOnlyList<uint> ResponseVariableOrder
        {
            get { return ResponseModel.VariableOrder; }
        }

        public IReadOnlyList<uint> LogicalZoneOrder
        {
            get { return ResponseModel.VariableOrder; }
        }

        public IReadOnlyList<IReadOnlyList<double>> ResponseJacobian
        {
            get { return ResponseModel.Jacobian; }
        }

        public IReadOnlyList<IReadOnlyList<double>> Jacobian
        {
            get { return ResponseModel.Jacobian; }
        }

        public IReadOnlyList<double> ResponseResidualWeights
        {
            get { return ResponseModel.ResidualWeights; }
        }

        public double ResponseRegularization
        {
            get { return ResponseModel.Regularization; }
        }

        public double CommonModeReactivitySensitivity
        {
            get { return ResponseModel.CommonModeReactivitySensitivity; }
        }

        public double CommonModeReactivityWeight
        {
            get { return ResponseModel.CommonModeReactivityWeight; }
        }

        public IReadOnlyList<double> AppliedFillCommand
        {
            get { return _appliedFillCommand; }
        }

        public IReadOnlyList<double> FillCommand
        {
            get { return AppliedFillCommand; }
        }

        public double UncompensatedWeightedResidual { get; }

        public double ControlledBaselineWeightedResidual { get; }

        public double BaselineWeightedResidual
        {
            get { return ControlledBaselineWeightedResidual; }
        }

        public double CombinedWeightedResidual { get; }

        public double WeightedResidual
        {
            get { return CombinedWeightedResidual; }
        }

        public int BaseCandidateSolveCount { get; }

        public int UncompensatedCandidateSolveCount
        {
            get { return BaseCandidateSolveCount; }
        }

        public int ControlledBaselineCandidateSolveCount { get; }

        public int BaselineCandidateSolveCount
        {
            get { return ControlledBaselineCandidateSolveCount; }
        }

        public int VerificationCandidateSolveCount { get; }

        public int VerificationSolveCount
        {
            get { return VerificationCandidateSolveCount; }
        }

        public int CorrectionCandidateSolveCount { get; }

        public int CorrectionSolveCount
        {
            get { return CorrectionCandidateSolveCount; }
        }

        public int TotalCandidateSolveCount { get; }

        public int CandidateSolveCount
        {
            get { return TotalCandidateSolveCount; }
        }

        public bool CorrectionApplied { get; }

        public string DecisionCode { get; }

        public bool Converged
        {
            get { return ControllerConverged; }
        }

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
            double simulationTimeSeconds = 0.0,
            IReadOnlyList<double>? initialFills = null)
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

            var fills = initialFills == null ? Enumerable.Repeat(
                PracticeLiquidZoneRrsIdentityV1.InitialFillFraction,
                (int)PracticeLiquidZoneRrsIdentityV1.LogicalZoneCount).ToArray() : initialFills.ToArray();
            ContractValidationResult<StaticAbsorptionOverlayV1> overlay =
                mapping.TryBuildOverlay(fills);
            if (!overlay.IsValid)
            {
                return Invalid(
                    overlay.FirstDiagnostic.Code,
                    overlay.FirstDiagnostic.Path,
                    overlay.FirstDiagnostic.Message);
            }

            ContractValidationResult<PracticeLiquidZoneRrsResponseModelV1> responseModel =
                PracticeLiquidZoneRrsResponseModelV1.TryCreate(measurement.Value.Fractions);
            if (!responseModel.IsValid)
            {
                return Invalid(
                    responseModel.FirstDiagnostic.Code,
                    responseModel.FirstDiagnostic.Path,
                    responseModel.FirstDiagnostic.Message);
            }

            double initialWeightedResidual = ComputeWeightedResidual(
                responseModel.Value,
                measurement.Value.Errors,
                initialEquilibrium.RelativeReactivity);
            bool converged = IsControllerConverged(
                measurement.Value.Errors,
                initialEquilibrium.RelativeReactivity);
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
                    simulationTimeSeconds,
                    responseModel.Value,
                    null,
                    Enumerable.Repeat(0.0, fills.Length),
                    initialWeightedResidual,
                    initialWeightedResidual,
                    initialWeightedResidual,
                    0,
                    0,
                    0,
                    0,
                    false));
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
                    simulationTimeSeconds,
                    ResponseModel,
                    CorrectionResponseModel,
                    AppliedFillCommand,
                    UncompensatedWeightedResidual,
                    ControlledBaselineWeightedResidual,
                    CombinedWeightedResidual,
                    BaseCandidateSolveCount,
                    ControlledBaselineCandidateSolveCount,
                    VerificationCandidateSolveCount,
                    CorrectionCandidateSolveCount,
                    CorrectionApplied, DecisionCode));
        }

        /// <summary>
        /// Runs one bounded static RRS/equilibrium event. The event evaluates
        /// an uncompensated solve and a controlled baseline, computes one
        /// fourteen-variable response command in memory, verifies it once,
        /// and may perform one bounded correction. Full-core candidate solves
        /// are therefore explicit and never exceed four. Short gameplay ticks
        /// do not call this method and reuse the accepted projection.
        /// </summary>
        public static ContractValidationResult<PracticeLiquidZoneRrsEquilibriumResultV1> TryRunEquilibrium(
            EquilibriumCoreSolverV1 equilibriumSolver,
            IEnumerable<BundleState> bundles,
            PracticeLiquidZoneRrsV1 previousState,
            double simulationTimeSeconds,
            FullCoreDiffusionSolveResultV1? warmStart = null,
            StaticAbsorptionOverlayV1? backgroundOverlay = null)
        {
#if RUNTIME_PROFILE
            using var profileScope = ReactorSim.Core.RuntimeProfile.Measure("rrs-event");
#endif
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
            ContractValidationResult<EquilibriumCorePreparedCandidatesV1> preparedCandidates =
                equilibriumSolver.TryPrepareCandidates(bundleRecords, backgroundOverlay);
            if (!preparedCandidates.IsValid)
            {
                return InvalidRun(
                    preparedCandidates.FirstDiagnostic.Code,
                    preparedCandidates.FirstDiagnostic.Path,
                    preparedCandidates.FirstDiagnostic.Message);
            }

            int baseCandidateSolveCount = 0;
            int controlledBaselineCandidateSolveCount = 0;
            int verificationCandidateSolveCount = 0;
            int correctionCandidateSolveCount = 0;

            ContractValidationResult<EquilibriumCoreProjectionV1> baseCandidate =
                equilibriumSolver.TrySolveCandidate(
                    preparedCandidates.Value,
                    warmStart ?? equilibriumSolver.CurrentSpatialSolve);
            baseCandidateSolveCount++;
            if (!baseCandidate.IsValid)
            {
                return InvalidRun(
                    baseCandidate.FirstDiagnostic.Code,
                    baseCandidate.FirstDiagnostic.Path,
                    baseCandidate.FirstDiagnostic.Message);
            }

            ContractValidationResult<ZonalPowerMeasurement> baseMeasurement =
                MeasureZonalPower(
                    previousState.Mapping,
                    baseCandidate.Value.ShapeNodePowerWatts,
                    previousState.TargetZonalPowerFractions);
            if (!baseMeasurement.IsValid)
            {
                return InvalidRun(
                    baseMeasurement.FirstDiagnostic.Code,
                    baseMeasurement.FirstDiagnostic.Path,
                    baseMeasurement.FirstDiagnostic.Message);
            }

            double[] previousFills = previousState.ZoneFills.ToArray();
            ContractValidationResult<StaticAbsorptionOverlayV1> baselineOverlay =
                previousState.Mapping.TryBuildOverlay(previousFills);
            if (!baselineOverlay.IsValid)
            {
                return InvalidRun(
                    baselineOverlay.FirstDiagnostic.Code,
                    baselineOverlay.FirstDiagnostic.Path,
                    baselineOverlay.FirstDiagnostic.Message);
            }

            ContractValidationResult<EquilibriumCoreProjectionV1> controlledBaseline =
                equilibriumSolver.TrySolveCandidate(
                    preparedCandidates.Value,
                    baseCandidate.Value.SpatialSolve,
                    baselineOverlay.Value);
            controlledBaselineCandidateSolveCount++;
            if (!controlledBaseline.IsValid)
            {
                return InvalidRun(
                    controlledBaseline.FirstDiagnostic.Code,
                    controlledBaseline.FirstDiagnostic.Path,
                    controlledBaseline.FirstDiagnostic.Message);
            }

            ContractValidationResult<ZonalPowerMeasurement> baselineMeasurement =
                MeasureZonalPower(
                    previousState.Mapping,
                    controlledBaseline.Value.ShapeNodePowerWatts,
                    previousState.TargetZonalPowerFractions);
            if (!baselineMeasurement.IsValid)
            {
                return InvalidRun(
                    baselineMeasurement.FirstDiagnostic.Code,
                    baselineMeasurement.FirstDiagnostic.Path,
                    baselineMeasurement.FirstDiagnostic.Message);
            }

            ContractValidationResult<PracticeLiquidZoneRrsResponseModelV1> responseModel =
                previousState.CorrectionResponseModel != null
                    ? ContractValidationResult<PracticeLiquidZoneRrsResponseModelV1>.Valid(
                        previousState.CorrectionResponseModel.WithBaselineFractions(
                            baselineMeasurement.Value.Fractions))
                    : PracticeLiquidZoneRrsResponseModelV1.TryCreate(baselineMeasurement.Value.Fractions);
            if (!responseModel.IsValid)
            {
                return InvalidRun(
                    responseModel.FirstDiagnostic.Code,
                    responseModel.FirstDiagnostic.Path,
                    responseModel.FirstDiagnostic.Message);
            }

            double uncompensatedWeightedResidual = ComputeWeightedResidual(
                responseModel.Value,
                baseMeasurement.Value.Errors,
                baseCandidate.Value.RelativeReactivity);
            double controlledBaselineWeightedResidual = ComputeWeightedResidual(
                responseModel.Value,
                baselineMeasurement.Value.Errors,
                controlledBaseline.Value.RelativeReactivity);

            var finalCandidate = new RrsCandidate(
                controlledBaseline.Value,
                baselineMeasurement.Value,
                previousFills,
                baselineOverlay.Value,
                controlledBaselineWeightedResidual,
                false);
            PracticeLiquidZoneRrsResponseModelV1? correctionResponseModel = null;

            bool baselineConverged = IsControllerConverged(baselineMeasurement.Value.Errors,
                controlledBaseline.Value.RelativeReactivity);
            bool commandAccepted = false;
            if (!baselineConverged)
            {
                ContractValidationResult<double[]> command = TrySolveBoundedFillCommand(
                    responseModel.Value,
                    baselineMeasurement.Value.Errors,
                    controlledBaseline.Value.RelativeReactivity,
                    previousFills,
                    previousFills);
                if (!command.IsValid)
                {
                    return InvalidRun(
                        command.FirstDiagnostic.Code,
                        command.FirstDiagnostic.Path,
                        command.FirstDiagnostic.Message);
                }

                double[] commandedFills = ApplyFillCommand(
                    previousFills,
                    previousFills,
                    command.Value);
                if (HasFillChange(commandedFills, previousFills))
                {
                    ContractValidationResult<StaticAbsorptionOverlayV1> verificationOverlay =
                        previousState.Mapping.TryBuildOverlay(commandedFills);
                    if (!verificationOverlay.IsValid)
                    {
                        return InvalidRun(
                            verificationOverlay.FirstDiagnostic.Code,
                            verificationOverlay.FirstDiagnostic.Path,
                            verificationOverlay.FirstDiagnostic.Message);
                    }

                    ContractValidationResult<EquilibriumCoreProjectionV1> verification =
                        equilibriumSolver.TrySolveCandidate(
                            preparedCandidates.Value,
                            controlledBaseline.Value.SpatialSolve,
                            verificationOverlay.Value);
                    verificationCandidateSolveCount++;
                    if (!verification.IsValid)
                    {
                        return InvalidRun(
                            verification.FirstDiagnostic.Code,
                            verification.FirstDiagnostic.Path,
                            verification.FirstDiagnostic.Message);
                    }

                    ContractValidationResult<ZonalPowerMeasurement> verificationMeasurement =
                        MeasureZonalPower(
                            previousState.Mapping,
                            verification.Value.ShapeNodePowerWatts,
                            previousState.TargetZonalPowerFractions);
                    if (!verificationMeasurement.IsValid)
                    {
                        return InvalidRun(
                            verificationMeasurement.FirstDiagnostic.Code,
                            verificationMeasurement.FirstDiagnostic.Path,
                            verificationMeasurement.FirstDiagnostic.Message);
                    }

                    double verificationWeightedResidual = ComputeWeightedResidual(
                        responseModel.Value,
                        verificationMeasurement.Value.Errors,
                        verification.Value.RelativeReactivity);
                    var verificationCandidate = new RrsCandidate(
                        verification.Value,
                        verificationMeasurement.Value,
                        commandedFills,
                        verificationOverlay.Value,
                        verificationWeightedResidual,
                        false);
                    bool verificationAccepted = IsBetterCandidate(verificationCandidate, finalCandidate);
                    if (verificationAccepted)
                    {
                        finalCandidate = verificationCandidate;
                        commandAccepted = true;
                    }

                    if (!IsControllerConverged(
                            finalCandidate.Measurement.Errors,
                            finalCandidate.Projection.RelativeReactivity))
                    {
                        PracticeLiquidZoneRrsResponseModelV1 correctionModel =
                            responseModel.Value.WithMeasuredResponse(
                                commandedFills.Zip(previousFills, (next, prior) => next - prior).ToArray(),
                                baselineMeasurement.Value.Errors,
                                controlledBaseline.Value.RelativeReactivity,
                                verificationMeasurement.Value.Errors,
                                verification.Value.RelativeReactivity);
                        correctionResponseModel = correctionModel;
                        ContractValidationResult<double[]> correctionCommand =
                            TrySolveBoundedFillCommand(
                                correctionModel,
                                finalCandidate.Measurement.Errors,
                                finalCandidate.Projection.RelativeReactivity,
                                finalCandidate.Fills,
                                previousFills);
                        if (!correctionCommand.IsValid)
                        {
                            return InvalidRun(
                                correctionCommand.FirstDiagnostic.Code,
                                correctionCommand.FirstDiagnostic.Path,
                                correctionCommand.FirstDiagnostic.Message);
                        }

                        double[] correctionFills = ApplyFillCommand(
                            finalCandidate.Fills,
                            previousFills,
                            correctionCommand.Value);
                        if (HasFillChange(correctionFills, finalCandidate.Fills))
                        {
                            ContractValidationResult<StaticAbsorptionOverlayV1>
                                correctionOverlay =
                                previousState.Mapping.TryBuildOverlay(correctionFills);
                            if (!correctionOverlay.IsValid)
                            {
                                return InvalidRun(
                                    correctionOverlay.FirstDiagnostic.Code,
                                    correctionOverlay.FirstDiagnostic.Path,
                                    correctionOverlay.FirstDiagnostic.Message);
                            }

                            ContractValidationResult<EquilibriumCoreProjectionV1> correction =
                                equilibriumSolver.TrySolveCandidate(
                                    preparedCandidates.Value,
                                    verification.Value.SpatialSolve,
                                    correctionOverlay.Value);
                            correctionCandidateSolveCount++;
                            if (!correction.IsValid)
                            {
                                return InvalidRun(
                                    correction.FirstDiagnostic.Code,
                                    correction.FirstDiagnostic.Path,
                                    correction.FirstDiagnostic.Message);
                            }

                            ContractValidationResult<ZonalPowerMeasurement> correctionMeasurement =
                                MeasureZonalPower(
                                    previousState.Mapping,
                                    correction.Value.ShapeNodePowerWatts,
                                    previousState.TargetZonalPowerFractions);
                            if (!correctionMeasurement.IsValid)
                            {
                                return InvalidRun(
                                    correctionMeasurement.FirstDiagnostic.Code,
                                    correctionMeasurement.FirstDiagnostic.Path,
                                    correctionMeasurement.FirstDiagnostic.Message);
                            }

                            double correctionWeightedResidual = ComputeWeightedResidual(
                                correctionModel,
                                correctionMeasurement.Value.Errors,
                                correction.Value.RelativeReactivity);
                            var correctionCandidate = new RrsCandidate(
                                correction.Value,
                                correctionMeasurement.Value,
                                correctionFills,
                                correctionOverlay.Value,
                                correctionWeightedResidual,
                                true);
                            if (IsBetterCandidate(correctionCandidate, finalCandidate))
                            {
                                finalCandidate = correctionCandidate;
                            }
                        }
                    }
                }
            }

            var state = new PracticeLiquidZoneRrsV1(
                previousState.Mapping,
                finalCandidate.Fills,
                previousState.ReferenceZonalPowerFractions,
                previousState.TargetZonalPowerFractions,
                finalCandidate.Measurement.Fractions,
                finalCandidate.Measurement.Errors,
                finalCandidate.Overlay,
                equilibriumSolver.TargetPowerWatts,
                finalCandidate.Projection.ShapePowerWatts,
                baseCandidate.Value.RelativeReactivity,
                finalCandidate.Projection.RelativeReactivity,
                finalCandidate.Projection.RelativeReactivity -
                baseCandidate.Value.RelativeReactivity,
                PracticeLiquidZoneRrsIdentityV1.MaximumControllerPasses,
                IsControllerConverged(
                    finalCandidate.Measurement.Errors,
                    finalCandidate.Projection.RelativeReactivity),
                simulationTimeSeconds,
                responseModel.Value,
                correctionResponseModel,
                Difference(finalCandidate.Fills, previousFills),
                uncompensatedWeightedResidual,
                controlledBaselineWeightedResidual,
                finalCandidate.WeightedResidual,
                baseCandidateSolveCount,
                controlledBaselineCandidateSolveCount,
                verificationCandidateSolveCount,
                correctionCandidateSolveCount,
                finalCandidate.CorrectionApplied,
                DescribeControllerDecision(baselineConverged, commandAccepted, finalCandidate.CorrectionApplied,
                    IsControllerConverged(finalCandidate.Measurement.Errors, finalCandidate.Projection.RelativeReactivity),
                    finalCandidate.Fills, Difference(finalCandidate.Fills, previousFills), correctionCandidateSolveCount > 0));

            return ContractValidationResult<PracticeLiquidZoneRrsEquilibriumResultV1>.Valid(
                new PracticeLiquidZoneRrsEquilibriumResultV1(state, finalCandidate.Projection));
        }

        /// <summary>Diagnostic classification from actual controller branch choices and measured bounds; no solver changes.</summary>
        public static string DescribeControllerDecision(bool baselineConverged, bool commandAccepted,
            bool correctionAccepted, bool finalConverged, IReadOnlyList<double> fills, IReadOnlyList<double> appliedCommand, bool correctionTested = false)
        {
            if (fills == null || appliedCommand == null || fills.Count != 14 || appliedCommand.Count != 14 ||
                fills.Any(f => double.IsNaN(f) || double.IsInfinity(f) || f < 0 || f > 1) ||
                appliedCommand.Any(f => double.IsNaN(f) || double.IsInfinity(f)))
                throw new ArgumentException("Decision facts require fourteen finite bounded fills and commands.");
            if (fills.All(f => f <= 0)) return "exhausted-empty";
            if (fills.All(f => f >= 1)) return "exhausted-full";
            if (baselineConverged) return "already-balanced";
            if (!finalConverged && fills.Any(f => f <= PracticeLiquidZoneRrsIdentityV1.FillCommandTolerance ||
                f >= 1 - PracticeLiquidZoneRrsIdentityV1.FillCommandTolerance)) return "fill-limits";
            if (!finalConverged && appliedCommand.Any(f => Math.Abs(f) >=
                PracticeLiquidZoneRrsIdentityV1.MaxFillMovementPerEvent - PracticeLiquidZoneRrsIdentityV1.FillCommandTolerance)) return "event-limit";
            if (correctionAccepted) return "correction-applied";
            if (commandAccepted && correctionTested) return "command-retained";
            return commandAccepted ? "command-applied" : "retained-best";
        }

        private static ContractValidationResult<double[]> TrySolveBoundedFillCommand(
            PracticeLiquidZoneRrsResponseModelV1 responseModel,
            double[] shapeErrors,
            double reactivity,
            double[] currentFills,
            double[] eventOriginFills)
        {
#if RUNTIME_PROFILE
            using var profileScope = ReactorSim.Core.RuntimeProfile.Measure("rrs-command");
#endif
            if (responseModel == null)
            {
                return ContractValidationResult<double[]>.Invalid(
                    "PracticeLiquidZoneRrs.Response.Missing",
                    "response_model",
                    "A bounded RRS command requires an explicit response model.");
            }

            int variableCount = responseModel.VariableCount;
            if (variableCount != PracticeLiquidZoneRrsIdentityV1.ResponseVariableCount ||
                responseModel.OutputCount != PracticeLiquidZoneRrsIdentityV1.ResponseOutputCount)
            {
                return ContractValidationResult<double[]>.Invalid(
                    "PracticeLiquidZoneRrs.Response.DimensionMismatch",
                    "response_model",
                    "The practice RRS response model must expose fourteen variables and fifteen outputs.");
            }

            if (shapeErrors == null || shapeErrors.Length != variableCount)
            {
                return ContractValidationResult<double[]>.Invalid(
                    "PracticeLiquidZoneRrs.Response.Residual.DimensionMismatch",
                    "shape_errors",
                    "A bounded RRS command requires one shape residual per logical zone.");
            }

            if (currentFills == null || currentFills.Length != variableCount ||
                eventOriginFills == null || eventOriginFills.Length != variableCount)
            {
                return ContractValidationResult<double[]>.Invalid(
                    "PracticeLiquidZoneRrs.Response.Fill.DimensionMismatch",
                    "fills",
                    "A bounded RRS command requires fourteen current and event-origin fills.");
            }

            if (!IsCanonicalFinite(reactivity))
            {
                return ContractValidationResult<double[]>.Invalid(
                    "PracticeLiquidZoneRrs.Response.Reactivity.Invalid",
                    "reactivity",
                    "The common-mode RRS residual must be finite and canonical.");
            }

            var lowerBounds = new double[variableCount];
            var upperBounds = new double[variableCount];
            for (int variable = 0; variable < variableCount; variable++)
            {
                double currentFill = currentFills[variable];
                double eventOriginFill = eventOriginFills[variable];
                if (!IsCanonicalFraction(currentFill) ||
                    !IsCanonicalFraction(eventOriginFill))
                {
                    return ContractValidationResult<double[]>.Invalid(
                        "PracticeLiquidZoneRrs.Response.Fill.Invalid",
                        "fills[" + variable.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]",
                        "Every bounded RRS command fill must be finite and within [0,1].");
                }

                double eventLower = Clamp(
                    eventOriginFill - PracticeLiquidZoneRrsIdentityV1.MaxFillMovementPerEvent,
                    0.0,
                    1.0);
                double eventUpper = Clamp(
                    eventOriginFill + PracticeLiquidZoneRrsIdentityV1.MaxFillMovementPerEvent,
                    0.0,
                    1.0);
                lowerBounds[variable] = eventLower - currentFill;
                upperBounds[variable] = eventUpper - currentFill;
            }

            // Regulate common-mode criticality independently of the shape
            // objective; burnup must not act as the reactivity controller.
            // Actual diffusion candidates verify this estimated response.
            if (Math.Abs(reactivity) > PracticeLiquidZoneRrsIdentityV1.CriticalityTolerance)
            {
                return ContractValidationResult<double[]>.Valid(SolveCommonModeCommand(
                    responseModel, reactivity, lowerBounds, upperBounds));
            }

            var residual = new double[responseModel.OutputCount];
            for (int output = 0; output < variableCount; output++)
            {
                residual[output] = shapeErrors[output];
                if (!IsCanonicalFinite(residual[output]))
                {
                    return ContractValidationResult<double[]>.Invalid(
                        "PracticeLiquidZoneRrs.Response.Residual.Invalid",
                        "shape_errors[" + output.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]",
                        "Every RRS shape residual must be finite and canonical.");
                }
            }

            residual[variableCount] = reactivity;
            var normalMatrix = new double[variableCount, variableCount];
            var gradient = new double[variableCount];
            for (int output = 0; output < responseModel.OutputCount; output++)
            {
                if (responseModel.Jacobian[output].Count != variableCount ||
                    !IsCanonicalFinite(responseModel.ResidualWeights[output]))
                {
                    return ContractValidationResult<double[]>.Invalid(
                        "PracticeLiquidZoneRrs.Response.Matrix.Invalid",
                        "response_model.jacobian",
                        "The RRS response matrix and output weights must be finite and rectangular.");
                }

                double weight = responseModel.ResidualWeights[output];
                double weightedResidual = weight * residual[output];
                if (!IsCanonicalFinite(weightedResidual))
                {
                    return ContractValidationResult<double[]>.Invalid(
                        "PracticeLiquidZoneRrs.Response.Residual.Overflow",
                        "response_model.residual_weights",
                        "Weighted RRS residuals must remain finite.");
                }

                for (int column = 0; column < variableCount; column++)
                {
                    double weightedResponse = weight * responseModel.Jacobian[output][column];
                    if (!IsCanonicalFinite(weightedResponse))
                    {
                        return ContractValidationResult<double[]>.Invalid(
                            "PracticeLiquidZoneRrs.Response.Matrix.NonFinite",
                            "response_model.jacobian",
                            "The project-authored RRS response matrix must remain finite.");
                    }

                    gradient[column] += weightedResponse * weightedResidual;
                    for (int other = 0; other < variableCount; other++)
                    {
                        normalMatrix[column, other] +=
                            weightedResponse *
                            weight *
                            responseModel.Jacobian[output][other];
                    }
                }
            }

            for (int diagonal = 0; diagonal < variableCount; diagonal++)
            {
                normalMatrix[diagonal, diagonal] += responseModel.Regularization;
            }

            var solution = new double[variableCount];
            var fixedVariables = new bool[variableCount];
            bool solved = false;
            for (int activePass = 0; activePass <= variableCount; activePass++)
            {
                var freeVariables = new List<int>(variableCount);
                for (int variable = 0; variable < variableCount; variable++)
                {
                    if (!fixedVariables[variable])
                    {
                        freeVariables.Add(variable);
                    }
                }

                if (freeVariables.Count == 0)
                {
                    solved = true;
                    break;
                }

                var system = new double[freeVariables.Count, freeVariables.Count];
                var rightHandSide = new double[freeVariables.Count];
                for (int row = 0; row < freeVariables.Count; row++)
                {
                    int variable = freeVariables[row];
                    rightHandSide[row] = -gradient[variable];
                    for (int fixedVariable = 0;
                         fixedVariable < variableCount;
                         fixedVariable++)
                    {
                        if (fixedVariables[fixedVariable])
                        {
                            rightHandSide[row] -=
                                normalMatrix[variable, fixedVariable] * solution[fixedVariable];
                        }
                    }

                    for (int column = 0; column < freeVariables.Count; column++)
                    {
                        system[row, column] = normalMatrix[
                            variable,
                            freeVariables[column]];
                    }
                }

                if (!TrySolveLinearSystem(system, rightHandSide, out double[] freeSolution))
                {
                    return ContractValidationResult<double[]>.Invalid(
                        "PracticeLiquidZoneRrs.Response.Solve.IllConditioned",
                        "response_model.normal_matrix",
                        "The regularized RRS response system is non-finite or ill-conditioned.");
                }

                bool fixedAny = false;
                for (int index = 0; index < freeVariables.Count; index++)
                {
                    int variable = freeVariables[index];
                    double proposed = freeSolution[index];
                    if (!IsCanonicalFinite(proposed))
                    {
                        return ContractValidationResult<double[]>.Invalid(
                            "PracticeLiquidZoneRrs.Response.Solve.NonFinite",
                            "response_model.command",
                            "The bounded RRS least-squares command must remain finite.");
                    }

                    solution[variable] = proposed;
                    if (proposed < lowerBounds[variable] -
                        PracticeLiquidZoneRrsIdentityV1.FillCommandTolerance)
                    {
                        solution[variable] = lowerBounds[variable];
                        fixedVariables[variable] = true;
                        fixedAny = true;
                    }
                    else if (proposed > upperBounds[variable] +
                             PracticeLiquidZoneRrsIdentityV1.FillCommandTolerance)
                    {
                        solution[variable] = upperBounds[variable];
                        fixedVariables[variable] = true;
                        fixedAny = true;
                    }
                }

                if (!fixedAny)
                {
                    solved = true;
                    break;
                }
            }

            if (!solved)
            {
                return ContractValidationResult<double[]>.Invalid(
                    "PracticeLiquidZoneRrs.Response.Solve.ActiveSetLimit",
                    "response_model.command",
                    "The bounded RRS active-set command did not settle within fourteen passes.");
            }

            for (int variable = 0; variable < variableCount; variable++)
            {
                if (!IsCanonicalFinite(solution[variable]) ||
                    solution[variable] < lowerBounds[variable] -
                    PracticeLiquidZoneRrsIdentityV1.FillCommandTolerance ||
                    solution[variable] > upperBounds[variable] +
                    PracticeLiquidZoneRrsIdentityV1.FillCommandTolerance)
                {
                    return ContractValidationResult<double[]>.Invalid(
                        "PracticeLiquidZoneRrs.Response.Solve.Bounds",
                        "response_model.command",
                        "The bounded RRS command must respect the event movement and fill bounds.");
                }

                if (Math.Abs(solution[variable]) <=
                    PracticeLiquidZoneRrsIdentityV1.FillCommandTolerance)
                {
                    solution[variable] = 0.0;
                }
            }

            // Shape corrections live in the common-mode nullspace: add a
            // bounded common offset so the estimated net reactivity stays zero.
            solution = SolveCommonModeCommand(responseModel, reactivity, lowerBounds, upperBounds, solution);
            double initialResidual = ComputeWeightedResidual(
                responseModel,
                shapeErrors,
                reactivity);
            double predictedResidual = ComputePredictedWeightedResidual(
                responseModel,
                residual,
                solution);
            if (!IsCanonicalFinite(predictedResidual) ||
                predictedResidual > initialResidual +
                PracticeLiquidZoneRrsIdentityV1.ResidualAcceptanceTolerance)
            {
                return ContractValidationResult<double[]>.Valid(new double[variableCount]);
            }

            return ContractValidationResult<double[]>.Valid(solution);
        }

        private static bool TrySolveLinearSystem(
            double[,] matrix,
            double[] rightHandSide,
            out double[] solution)
        {
            int dimension = rightHandSide.Length;
            solution = new double[dimension];
            if (matrix.GetLength(0) != dimension || matrix.GetLength(1) != dimension)
            {
                return false;
            }

            var work = new double[dimension, dimension];
            var rhs = new double[dimension];
            for (int row = 0; row < dimension; row++)
            {
                rhs[row] = rightHandSide[row];
                for (int column = 0; column < dimension; column++)
                {
                    work[row, column] = matrix[row, column];
                }
            }

            for (int pivot = 0; pivot < dimension; pivot++)
            {
                int pivotRow = pivot;
                double pivotMagnitude = Math.Abs(work[pivot, pivot]);
                for (int row = pivot + 1; row < dimension; row++)
                {
                    double candidateMagnitude = Math.Abs(work[row, pivot]);
                    // Strict comparison deliberately keeps the lowest row
                    // index when magnitudes tie.
                    if (candidateMagnitude > pivotMagnitude)
                    {
                        pivotRow = row;
                        pivotMagnitude = candidateMagnitude;
                    }
                }

                if (!ContractValidation.IsFinite(pivotMagnitude) || pivotMagnitude <= 1.0e-14)
                {
                    return false;
                }

                if (pivotRow != pivot)
                {
                    for (int column = pivot; column < dimension; column++)
                    {
                        double temporary = work[pivot, column];
                        work[pivot, column] = work[pivotRow, column];
                        work[pivotRow, column] = temporary;
                    }

                    double rightHandSideTemporary = rhs[pivot];
                    rhs[pivot] = rhs[pivotRow];
                    rhs[pivotRow] = rightHandSideTemporary;
                }

                for (int row = pivot + 1; row < dimension; row++)
                {
                    double factor = work[row, pivot] / work[pivot, pivot];
                    if (!ContractValidation.IsFinite(factor))
                    {
                        return false;
                    }

                    work[row, pivot] = 0.0;
                    for (int column = pivot + 1; column < dimension; column++)
                    {
                        work[row, column] -= factor * work[pivot, column];
                    }

                    rhs[row] -= factor * rhs[pivot];
                }
            }

            for (int row = dimension - 1; row >= 0; row--)
            {
                double value = rhs[row];
                for (int column = row + 1; column < dimension; column++)
                {
                    value -= work[row, column] * solution[column];
                }

                if (!ContractValidation.IsFinite(value) ||
                    !ContractValidation.IsFinite(work[row, row]) ||
                    Math.Abs(work[row, row]) <= 1.0e-14)
                {
                    return false;
                }

                // Elimination may produce signed zero; canonicalize only at
                // the contract boundary, not during valid finite arithmetic.
                solution[row] = value == 0.0 ? 0.0 : value / work[row, row];
                if (!ContractValidation.IsFinite(solution[row]))
                {
                    return false;
                }
            }

            return true;
        }

        private static double[] ApplyFillCommand(
            double[] currentFills,
            double[] eventOriginFills,
            double[] command)
        {
            var fills = new double[PracticeLiquidZoneRrsIdentityV1.ResponseVariableCount];
            for (int variable = 0; variable < fills.Length; variable++)
            {
                double eventLower = Clamp(
                    eventOriginFills[variable] -
                    PracticeLiquidZoneRrsIdentityV1.MaxFillMovementPerEvent,
                    0.0,
                    1.0);
                double eventUpper = Clamp(
                    eventOriginFills[variable] +
                    PracticeLiquidZoneRrsIdentityV1.MaxFillMovementPerEvent,
                    0.0,
                    1.0);
                fills[variable] = CanonicalizeZero(Clamp(
                    currentFills[variable] + command[variable],
                    eventLower,
                    eventUpper));
            }

            return fills;
        }

        private static double[] Difference(
            double[] left,
            double[] right)
        {
            var difference = new double[left.Length];
            for (int index = 0; index < difference.Length; index++)
            {
                difference[index] = CanonicalizeZero(left[index] - right[index]);
            }

            return difference;
        }

        private static bool HasFillChange(
            double[] first,
            double[] second)
        {
            for (int index = 0; index < first.Length; index++)
            {
                if (Math.Abs(first[index] - second[index]) >
                    PracticeLiquidZoneRrsIdentityV1.FillCommandTolerance)
                {
                    return true;
                }
            }

            return false;
        }

        private static double ComputeWeightedResidual(
            PracticeLiquidZoneRrsResponseModelV1 responseModel,
            double[] shapeErrors,
            double reactivity)
        {
            var residual = new double[responseModel.OutputCount];
            for (int output = 0; output < responseModel.VariableCount; output++)
            {
                residual[output] = shapeErrors[output];
            }

            residual[responseModel.VariableCount] = reactivity;
            return ComputePredictedWeightedResidual(
                responseModel,
                residual,
                new double[responseModel.VariableCount]);
        }

        private static double ComputePredictedWeightedResidual(
            PracticeLiquidZoneRrsResponseModelV1 responseModel,
            double[] residual,
            double[] command)
        {
            double squared = 0.0;
            for (int output = 0; output < responseModel.OutputCount; output++)
            {
                double predicted = residual[output];
                for (int variable = 0; variable < responseModel.VariableCount; variable++)
                {
                    predicted += responseModel.Jacobian[output][variable] * command[variable];
                }

                double weighted = predicted * responseModel.ResidualWeights[output];
                squared += weighted * weighted;
            }

            if (!IsCanonicalFinite(squared) || squared < 0.0)
            {
                return double.NaN;
            }

            return Math.Sqrt(squared);
        }

        internal static bool IsControllerConverged(
            double[] shapeErrors,
            double reactivity)
        {
            return MaxAbsolute(shapeErrors) <= PracticeLiquidZoneRrsIdentityV1.ControllerTolerance &&
                   Math.Abs(reactivity) <= PracticeLiquidZoneRrsIdentityV1.CriticalityTolerance;
        }

        private static bool IsBetterCandidate(RrsCandidate candidate, RrsCandidate incumbent)
        {
            double before = Math.Abs(incumbent.Projection.RelativeReactivity);
            double after = Math.Abs(candidate.Projection.RelativeReactivity);
            if (before > PracticeLiquidZoneRrsIdentityV1.CriticalityTolerance)
                return after + PracticeLiquidZoneRrsIdentityV1.ResidualAcceptanceTolerance < before;
            if (after > PracticeLiquidZoneRrsIdentityV1.CriticalityTolerance) return false;
            return candidate.WeightedResidual + PracticeLiquidZoneRrsIdentityV1.ResidualAcceptanceTolerance <
                incumbent.WeightedResidual;
        }

        private static double[] SolveCommonModeCommand(
            PracticeLiquidZoneRrsResponseModelV1 responseModel, double reactivity,
            double[] lowerBounds, double[] upperBounds, double[]? shapeCommand = null)
        {
            // One common fill request, clipped at each compartment's physical
            // and event bounds. Other compartments remain active at a limit.
            // A learned positive derivative cannot reverse absorption.
            double Change(double request)
            {
                double change = 0;
                for (int zone = 0; zone < lowerBounds.Length; zone++)
                    change += Math.Min(0, responseModel.CommonModeReactivitySensitivities[zone]) *
                        Clamp((shapeCommand?[zone] ?? 0) + request, lowerBounds[zone], upperBounds[zone]);
                return change;
            }
            double low = lowerBounds.Min() - (shapeCommand?.Max() ?? 0);
            double high = upperBounds.Max() - (shapeCommand?.Min() ?? 0);
            for (int iteration = 0; iteration < 56; iteration++)
            {
                double middle = (low + high) * 0.5;
                if (reactivity + Change(middle) > 0) low = middle;
                else high = middle;
            }
            double request = (low + high) * 0.5;
            return lowerBounds.Select((lower, zone) =>
                CanonicalizeZero(Clamp((shapeCommand?[zone] ?? 0) + request, lower, upperBounds[zone]))).ToArray();
        }

        private static double CanonicalizeZero(double value)
        {
            return value == 0.0 ? 0.0 : value;
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
                    Phase5CanonicalBytesV1.WriteString(
                        writer,
                        ResponseModel.Identity);
                    Phase5CanonicalBytesV1.WriteDigest(writer, ResponseModel.ModelDigest);
                    writer.Write(CorrectionResponseModel != null ? (byte)1 : (byte)0);
                    if (CorrectionResponseModel != null)
                    {
                        Phase5CanonicalBytesV1.WriteDigest(
                            writer,
                            CorrectionResponseModel.ModelDigest);
                    }
                    Phase5CanonicalBytesV1.WriteDigest(writer, Mapping.MappingDigest);
                    Phase5CanonicalBytesV1.WriteDigest(writer, AbsorptionOverlay.OverlayDigest);
                    Phase5CanonicalBytesV1.WriteDouble(writer, SimulationTimeSeconds);
                    Phase5CanonicalBytesV1.WriteDouble(writer, TargetPowerWatts);
                    Phase5CanonicalBytesV1.WriteDouble(writer, MeasuredPowerWatts);
                    Phase5CanonicalBytesV1.WriteDouble(writer, CoreReactivity);
                    Phase5CanonicalBytesV1.WriteDouble(writer, CompensatedNetReactivity);
                    Phase5CanonicalBytesV1.WriteDouble(writer, CommonModeRhoCorrection);
                    Phase5CanonicalBytesV1.WriteDouble(writer, UncompensatedWeightedResidual);
                    Phase5CanonicalBytesV1.WriteDouble(writer, ControlledBaselineWeightedResidual);
                    Phase5CanonicalBytesV1.WriteDouble(writer, CombinedWeightedResidual);
                    Phase5CanonicalBytesV1.WriteUInt32(
                        writer,
                        checked((uint)ResponseVariableCount));
                    Phase5CanonicalBytesV1.WriteUInt32(
                        writer,
                        checked((uint)ResponseOutputCount));
                    WriteUInts(writer, ResponseVariableOrder);
                    WriteDoubles(writer, _appliedFillCommand);
                    Phase5CanonicalBytesV1.WriteUInt32(
                        writer,
                        checked((uint)ControllerIterationCount));
                    Phase5CanonicalBytesV1.WriteUInt32(
                        writer,
                        checked((uint)BaseCandidateSolveCount));
                    Phase5CanonicalBytesV1.WriteUInt32(
                        writer,
                        checked((uint)ControlledBaselineCandidateSolveCount));
                    Phase5CanonicalBytesV1.WriteUInt32(
                        writer,
                        checked((uint)VerificationCandidateSolveCount));
                    Phase5CanonicalBytesV1.WriteUInt32(
                        writer,
                        checked((uint)CorrectionCandidateSolveCount));
                    Phase5CanonicalBytesV1.WriteUInt32(
                        writer,
                        checked((uint)TotalCandidateSolveCount));
                    writer.Write(CorrectionApplied ? (byte)1 : (byte)0);
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

        private static void WriteUInts(
            System.IO.BinaryWriter writer,
            IReadOnlyList<uint> values)
        {
            Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)values.Count));
            for (int index = 0; index < values.Count; index++)
            {
                Phase5CanonicalBytesV1.WriteUInt32(writer, values[index]);
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

        private static bool IsCanonicalFraction(double value)
        {
            return IsCanonicalFinite(value) && value >= 0.0 && value <= 1.0;
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

        private sealed class RrsCandidate
        {
            internal RrsCandidate(
                EquilibriumCoreProjectionV1 projection,
                ZonalPowerMeasurement measurement,
                IEnumerable<double> fills,
                StaticAbsorptionOverlayV1 overlay,
                double weightedResidual,
                bool correctionApplied)
            {
                Projection = projection;
                Measurement = measurement;
                Fills = fills.ToArray();
                Overlay = overlay;
                WeightedResidual = weightedResidual;
                CorrectionApplied = correctionApplied;
            }

            internal EquilibriumCoreProjectionV1 Projection { get; }

            internal ZonalPowerMeasurement Measurement { get; }

            internal double[] Fills { get; }

            internal StaticAbsorptionOverlayV1 Overlay { get; }

            internal double WeightedResidual { get; }

            internal bool CorrectionApplied { get; }
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
