using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace ReactorSim.Core
{
    /// <summary>
    /// Explicit marker for the xenon basis of a P2-T02 coefficient set.
    /// Phase 7A accepts only an explicitly xenon-free base table.
    /// </summary>
    public enum XenonBasisV1 : byte
    {
        Excluded = 0,
        Included = 1,
        Equilibrium = 2,
        Unspecified = 255
    }

    /// <summary>
    /// One explicit node-to-nuclide-state-version binding carried by a
    /// kinetics/spatial acceptance tuple.
    /// </summary>
    public sealed class XenonSpatialNuclideVersionV1
    {
        private XenonSpatialNuclideVersionV1(NodeKey node, ulong stateVersion)
        {
            Node = node;
            StateVersion = stateVersion;
        }

        public NodeKey Node { get; }

        public ulong StateVersion { get; }

        public static ContractValidationResult<XenonSpatialNuclideVersionV1> TryCreate(
            NodeKey node,
            ulong stateVersion)
        {
            return ContractValidationResult<XenonSpatialNuclideVersionV1>.Valid(
                new XenonSpatialNuclideVersionV1(node, stateVersion));
        }
    }

    /// <summary>
    /// Exact state/time/version tuple that a dynamic-Xe spatial solve accepts.
    /// It is caller-supplied and contains no hidden simulation-clock or
    /// lifecycle defaults.
    /// </summary>
    public sealed class XenonSpatialStateBindingV1
    {
        private readonly ReadOnlyCollection<XenonSpatialNuclideVersionV1> _nodeVersions;

        private XenonSpatialStateBindingV1(
            double simulationTimeSeconds,
            double kineticAmplitude,
            ulong coreStateVersion,
            Digest32 stateDigest,
            Digest32 topologyDigest,
            Digest32 dataPackDigest,
            IEnumerable<XenonSpatialNuclideVersionV1> nodeVersions)
        {
            SimulationTimeSeconds = simulationTimeSeconds;
            KineticAmplitude = kineticAmplitude;
            CoreStateVersion = coreStateVersion;
            StateDigest = stateDigest;
            TopologyDigest = topologyDigest;
            DataPackDigest = dataPackDigest;
            _nodeVersions = new ReadOnlyCollection<XenonSpatialNuclideVersionV1>(
                nodeVersions.OrderBy(version => version.Node).ToArray());
        }

        public double SimulationTimeSeconds { get; }

        public double KineticAmplitude { get; }

        public ulong CoreStateVersion { get; }

        public Digest32 StateDigest { get; }

        public Digest32 TopologyDigest { get; }

        public Digest32 DataPackDigest { get; }

        public IReadOnlyList<XenonSpatialNuclideVersionV1> NodeVersions
        {
            get { return _nodeVersions; }
        }

        public static ContractValidationResult<XenonSpatialStateBindingV1> TryCreate(
            double simulationTimeSeconds,
            double kineticAmplitude,
            ulong coreStateVersion,
            Digest32 stateDigest,
            Digest32 topologyDigest,
            Digest32 dataPackDigest,
            IEnumerable<XenonSpatialNuclideVersionV1> nodeVersions)
        {
            if (!KineticContractValidation.IsCanonicalNonnegativeFinite(simulationTimeSeconds))
            {
                return Invalid(
                    "XenonSpatialBinding.Time.Invalid",
                    "simulation_time_s",
                    "The spatial binding time must be finite, canonical, and nonnegative SI seconds.");
            }

            if (!KineticContractValidation.IsCanonicalNonnegativeFinite(kineticAmplitude))
            {
                return Invalid(
                    "XenonSpatialBinding.Amplitude.Invalid",
                    "kinetic_amplitude",
                    "The spatial binding amplitude must be finite, canonical, and nonnegative.");
            }

            if (stateDigest == null || topologyDigest == null || dataPackDigest == null)
            {
                return Invalid(
                    "XenonSpatialBinding.Digest.Missing",
                    "binding_digests",
                    "A spatial binding requires state, topology, and data-pack digests.");
            }

            if (nodeVersions == null)
            {
                return Invalid(
                    "XenonSpatialBinding.NodeVersions.Missing",
                    "node_versions",
                    "A spatial binding requires every current node's nuclide-state version.");
            }

            XenonSpatialNuclideVersionV1[] versions = nodeVersions.ToArray();
            if (versions.Any(version => version == null))
            {
                return Invalid(
                    "XenonSpatialBinding.NodeVersion.Null",
                    "node_versions",
                    "A node-version binding may not be null.");
            }

            var seen = new HashSet<NodeKey>();
            foreach (XenonSpatialNuclideVersionV1 version in versions)
            {
                if (!seen.Add(version.Node))
                {
                    return Invalid(
                        "XenonSpatialBinding.NodeVersion.Duplicate",
                        ContractValidation.NodePath(version.Node, ".nuclide_state_version"),
                        "Exactly one nuclide-state version is required per node.");
                }
            }

            return ContractValidationResult<XenonSpatialStateBindingV1>.Valid(
                new XenonSpatialStateBindingV1(
                    simulationTimeSeconds,
                    kineticAmplitude,
                    coreStateVersion,
                    stateDigest,
                    topologyDigest,
                    dataPackDigest,
                    versions));
        }

        public ContractValidationResult<bool> TryValidateExact(
            XenonSpatialStateBindingV1 current)
        {
            if (current == null)
            {
                return ContractValidationResult<bool>.Invalid(
                    "XenonSpatialBinding.Current.Missing",
                    "current_binding",
                    "An exact current state binding is required before positive-duration advancement.");
            }

            if (SimulationTimeSeconds != current.SimulationTimeSeconds)
            {
                return InvalidExact(
                    "XenonSpatialBinding.Time.Stale",
                    "simulation_time_s",
                    "The accepted solve time does not equal the current simulation time.");
            }

            if (KineticAmplitude != current.KineticAmplitude)
            {
                return InvalidExact(
                    "XenonSpatialBinding.Amplitude.Stale",
                    "kinetic_amplitude",
                    "The accepted solve does not bind the current kinetic amplitude.");
            }

            if (CoreStateVersion != current.CoreStateVersion)
            {
                return InvalidExact(
                    "XenonSpatialBinding.CoreStateVersion.Stale",
                    "core_state_version",
                    "The accepted solve does not bind the current core-state version.");
            }

            if (!StateDigest.Equals(current.StateDigest) ||
                !TopologyDigest.Equals(current.TopologyDigest) ||
                !DataPackDigest.Equals(current.DataPackDigest))
            {
                return InvalidExact(
                    "XenonSpatialBinding.Digest.Stale",
                    "binding_digests",
                    "The accepted solve does not bind the current state, topology, and data-pack digests.");
            }

            if (_nodeVersions.Count != current._nodeVersions.Count)
            {
                return InvalidExact(
                    "XenonSpatialBinding.NodeVersions.CountMismatch",
                    "node_versions",
                    "The accepted solve does not bind the same node-version set as the current state.");
            }

            for (int index = 0; index < _nodeVersions.Count; index++)
            {
                if (_nodeVersions[index].Node != current._nodeVersions[index].Node ||
                    _nodeVersions[index].StateVersion != current._nodeVersions[index].StateVersion)
                {
                    return InvalidExact(
                        "XenonSpatialBinding.NodeVersion.Stale",
                        ContractValidation.NodePath(
                            current._nodeVersions[index].Node,
                            ".nuclide_state_version"),
                        "The accepted solve does not bind the current node nuclide-state version.");
                }
            }

            return ContractValidationResult<bool>.Valid(true);
        }

        private static ContractValidationResult<XenonSpatialStateBindingV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<XenonSpatialStateBindingV1>.Invalid(code, path, message);
        }

        private static ContractValidationResult<bool> InvalidExact(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<bool>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// One node-keyed I/Xe state supplied to the controlled spatial overlay.
    /// The state remains owned by the validated Phase 5 envelope.
    /// </summary>
    public sealed class XenonSpatialNodeInputV1
    {
        private XenonSpatialNodeInputV1(NodeKey node, NuclideStateEnvelopeV1 state)
        {
            Node = node;
            State = state;
        }

        public NodeKey Node { get; }

        public NuclideStateEnvelopeV1 State { get; }

        public static ContractValidationResult<XenonSpatialNodeInputV1> TryCreate(
            NodeKey node,
            NuclideStateEnvelopeV1 state)
        {
            if (state == null)
            {
                return ContractValidationResult<XenonSpatialNodeInputV1>.Invalid(
                    "XenonSpatialInput.State.Missing",
                    ContractValidation.NodePath(node, ".nuclide_state"),
                    "Every spatial node requires a validated I-135/Xe-135 state.");
            }

            return ContractValidationResult<XenonSpatialNodeInputV1>.Valid(
                new XenonSpatialNodeInputV1(node, state));
        }
    }

    /// <summary>
    /// Canonical node-level dynamic Xe absorption contribution. The values are
    /// derived from the validated atom inventory and material-keyed data; no
    /// production constants are selected here.
    /// </summary>
    public sealed class XenonSpatialOverlayValueV1
    {
        internal XenonSpatialOverlayValueV1(
            NodeKey node,
            StableId bundleId,
            string nuclideDataId,
            Digest32 nuclideDataDigest,
            double xe135AtomInventory,
            double xe135NumberDensity,
            double dynamicAbsorptionGroup1PerM,
            double dynamicAbsorptionGroup2PerM,
            ulong stateVersion)
        {
            Node = node;
            BundleId = bundleId;
            NuclideDataId = nuclideDataId;
            NuclideDataDigest = nuclideDataDigest;
            Xe135AtomInventory = xe135AtomInventory;
            Xe135NumberDensity = xe135NumberDensity;
            DynamicAbsorptionGroup1PerM = dynamicAbsorptionGroup1PerM;
            DynamicAbsorptionGroup2PerM = dynamicAbsorptionGroup2PerM;
            StateVersion = stateVersion;
        }

        public NodeKey Node { get; }

        /// <summary>
        /// Persistent bundle identity bound to this node overlay. Keeping it
        /// in the overlay prevents a numerically identical burnup row from
        /// being reused after a refuelling inventory transition.
        /// </summary>
        public StableId BundleId { get; }

        public string NuclideDataId { get; }

        public Digest32 NuclideDataDigest { get; }

        public double Xe135AtomInventory { get; }

        public double Xe135NumberDensity { get; }

        public double DynamicAbsorptionGroup1PerM { get; }

        public double DynamicAbsorptionGroup2PerM { get; }

        public ulong StateVersion { get; }
    }

    /// <summary>
    /// Effective coefficient set and digest identities produced by one
    /// controlled dynamic-Xe coupling. It is immutable and contains no
    /// accepted spatial state until the caller runs the P2-T02 solve.
    /// </summary>
    public sealed class XenonSpatialCouplingResultV1
    {
        internal XenonSpatialCouplingResultV1(
            SpatialCoefficientSet coefficients,
            XenonBasisV1 xenonBasis,
            double referenceXeNumberDensityM3,
            Digest32 baseCoefficientDigest,
            Digest32 dynamicXenonDigest,
            Digest32 effectiveCoefficientDigest,
            XenonSpatialStateBindingV1 stateBinding,
            IEnumerable<XenonSpatialOverlayValueV1> overlays)
        {
            Coefficients = coefficients;
            XenonBasis = xenonBasis;
            ReferenceXeNumberDensityM3 = referenceXeNumberDensityM3;
            BaseCoefficientDigest = baseCoefficientDigest;
            DynamicXenonDigest = dynamicXenonDigest;
            EffectiveCoefficientDigest = effectiveCoefficientDigest;
            StateBinding = stateBinding;
            Overlays = new ReadOnlyCollection<XenonSpatialOverlayValueV1>(overlays.ToArray());
        }

        public SpatialCoefficientSet Coefficients { get; }

        public XenonBasisV1 XenonBasis { get; }

        public double ReferenceXeNumberDensityM3 { get; }

        public Digest32 BaseCoefficientDigest { get; }

        public Digest32 DynamicXenonDigest { get; }

        public Digest32 EffectiveCoefficientDigest { get; }

        public XenonSpatialStateBindingV1 StateBinding { get; }

        public IReadOnlyList<XenonSpatialOverlayValueV1> Overlays { get; }

        /// <summary>
        /// Verifies that the coefficient identities accepted by a spatial solve
        /// are exactly the identities produced by this coupling. A caller must
        /// perform this check before starting positive-duration advancement.
        /// </summary>
        public ContractValidationResult<bool> TryValidateSpatialSolveBinding(
            Digest32 acceptedDynamicXenonDigest,
            Digest32 acceptedEffectiveCoefficientDigest)
        {
            if (acceptedDynamicXenonDigest == null ||
                acceptedEffectiveCoefficientDigest == null)
            {
                return ContractValidationResult<bool>.Invalid(
                    "XenonSpatialSolveBinding.Digest.Missing",
                    "accepted_digests",
                    "A positive-duration binding requires both accepted dynamic-Xe and effective-coefficient digests.");
            }

            if (!DynamicXenonDigest.Equals(acceptedDynamicXenonDigest))
            {
                return ContractValidationResult<bool>.Invalid(
                    "XenonSpatialSolveBinding.DynamicDigest.Stale",
                    "accepted_dynamic_xenon_digest",
                    "The accepted spatial solve does not bind the current dynamic Xe atom-inventory digest.");
            }

            if (!EffectiveCoefficientDigest.Equals(acceptedEffectiveCoefficientDigest))
            {
                return ContractValidationResult<bool>.Invalid(
                    "XenonSpatialSolveBinding.CoefficientDigest.Stale",
                    "accepted_effective_coefficient_digest",
                    "The accepted spatial solve does not bind the current effective coefficient digest.");
            }

            return ContractValidationResult<bool>.Valid(true);
        }
    }

    /// <summary>
    /// Applies the approved dynamic Xe absorption overlay and revalidates the
    /// complete effective P2-T02 coefficient set. The caller supplies the
    /// already validated xenon-free base coefficients and the exact current
    /// node states.
    /// </summary>
    public static class XenonSpatialCouplingV1
    {
        public const uint CurrentSchemaVersion = 1;

        public static ContractValidationResult<XenonSpatialCouplingResultV1> TryApply(
            SpatialCoefficientSet baseCoefficients,
            Digest32 baseCoefficientDigest,
            XenonSpatialStateBindingV1 stateBinding,
            IEnumerable<XenonSpatialNodeInputV1> nodeInputs)
        {
            if (baseCoefficients == null)
            {
                return Invalid(
                    "XenonSpatial.BaseCoefficients.Missing",
                    "base_coefficients",
                    "Dynamic Xe coupling requires a validated base coefficient set.");
            }

            if (baseCoefficients.XenonBasis != XenonBasisV1.Excluded)
            {
                return Invalid(
                    "XenonSpatial.Basis.Invalid",
                    "base_coefficients.xenon_basis",
                    "Dynamic Xe coupling requires a base coefficient set explicitly marked XenonBasis=Excluded.");
            }

            if (!IsCanonicalNonnegative(baseCoefficients.ReferenceXeNumberDensityM3) ||
                baseCoefficients.ReferenceXeNumberDensityM3 != 0.0)
            {
                return Invalid(
                    "XenonSpatial.ReferenceXe.Invalid",
                    "base_coefficients.reference_xe_number_density_m3",
                    "Dynamic Xe coupling requires an exact canonical zero reference Xe number density in m^-3.");
            }

            if (baseCoefficientDigest == null)
            {
                return Invalid(
                    "XenonSpatial.BaseDigest.Missing",
                    "base_coefficient_digest",
                    "Dynamic Xe coupling requires the accepted base coefficient digest.");
            }

            if (stateBinding == null)
            {
                return Invalid(
                    "XenonSpatial.StateBinding.Missing",
                    "state_binding",
                    "Dynamic Xe coupling requires the exact current state/time/version binding.");
            }

            Digest32 computedBaseCoefficientDigest = ComputeBaseCoefficientDigest(baseCoefficients);
            if (!computedBaseCoefficientDigest.Equals(baseCoefficientDigest))
            {
                return Invalid(
                    "XenonSpatial.BaseDigest.Mismatch",
                    "base_coefficient_digest",
                    "The supplied base coefficient digest does not bind the exact xenon-free coefficient set, topology, and basis metadata.");
            }

            if (nodeInputs == null)
            {
                return Invalid(
                    "XenonSpatial.Nodes.Missing",
                    "node_inputs",
                    "Dynamic Xe coupling requires one current I/Xe state for every spatial node.");
            }

            XenonSpatialNodeInputV1[] inputs = nodeInputs.ToArray();
            if (inputs.Any(input => input == null))
            {
                return Invalid(
                    "XenonSpatial.Node.Null",
                    "node_inputs",
                    "A node input may not be null.");
            }

            if (inputs.Length != baseCoefficients.NodeCount)
            {
                return Invalid(
                    "XenonSpatial.Nodes.CountMismatch",
                    "node_inputs",
                    "Dynamic Xe coupling requires exactly one node input per coefficient-set node.");
            }

            var inputMap = new Dictionary<NodeKey, XenonSpatialNodeInputV1>();
            foreach (XenonSpatialNodeInputV1 input in inputs)
            {
                if (!baseCoefficients.Nodes.Any(node => node.Node == input.Node))
                {
                    return Invalid(
                        "XenonSpatial.Node.Unknown",
                        ContractValidation.NodePath(input.Node, ".nuclide_state"),
                        "Every dynamic Xe input must match a base coefficient-set node.");
                }

                if (!inputMap.TryAdd(input.Node, input))
                {
                    return Invalid(
                        "XenonSpatial.Node.Duplicate",
                        ContractValidation.NodePath(input.Node, ".nuclide_state"),
                        "Exactly one dynamic Xe input is required per spatial node.");
                }
            }

            if (stateBinding.NodeVersions.Count != baseCoefficients.NodeCount)
            {
                return Invalid(
                    "XenonSpatial.StateBinding.NodeVersions.CountMismatch",
                    "state_binding.node_versions",
                    "The state binding must contain exactly one nuclide-state version per coefficient-set node.");
            }

            var stateVersions = stateBinding.NodeVersions.ToDictionary(
                version => version.Node,
                version => version.StateVersion);
            foreach (SpatialNodeCoefficients baseNode in baseCoefficients.Nodes)
            {
                ulong stateVersion;
                if (!stateVersions.TryGetValue(baseNode.Node, out stateVersion))
                {
                    return Invalid(
                        "XenonSpatial.StateBinding.NodeVersion.Missing",
                        ContractValidation.NodePath(baseNode.Node, ".nuclide_state_version"),
                        "The state binding must contain the current nuclide-state version for every node.");
                }

                if (!inputMap.TryGetValue(baseNode.Node, out XenonSpatialNodeInputV1 boundInput) ||
                    boundInput.State.NuclideStateVersion != stateVersion)
                {
                    return Invalid(
                        "XenonSpatial.StateBinding.NodeVersion.Stale",
                        ContractValidation.NodePath(baseNode.Node, ".nuclide_state_version"),
                        "The state binding version must equal the exact current node nuclide-state version.");
                }
            }

            var overlays = new List<XenonSpatialOverlayValueV1>(baseCoefficients.NodeCount);
            var effectiveNodes = new List<SpatialNodeCoefficients>(baseCoefficients.NodeCount);
            foreach (SpatialNodeCoefficients baseNode in baseCoefficients.Nodes)
            {
                XenonSpatialNodeInputV1 input;
                if (!inputMap.TryGetValue(baseNode.Node, out input!))
                {
                    return Invalid(
                        "XenonSpatial.Node.Missing",
                        ContractValidation.NodePath(baseNode.Node, ".nuclide_state"),
                        "Exactly one dynamic Xe input is required per spatial node.");
                }

                if (input.State == null)
                {
                    return Invalid(
                        "XenonSpatialInput.State.Missing",
                        ContractValidation.NodePath(baseNode.Node, ".nuclide_state"),
                        "Every spatial node requires a validated I-135/Xe-135 state.");
                }

                if (input.State.NodeVolumeM3 != baseNode.VolumeM3)
                {
                    return Invalid(
                        "XenonSpatial.NodeVolume.Mismatch",
                        ContractValidation.NodePath(baseNode.Node, ".node_volume_m3"),
                        "The nuclide control volume must equal the exact P2-T02 spatial node volume.");
                }

                double dynamicGroup1 = input.State.Data.SigmaXeGroup1M2 *
                    input.State.Xe135NumberDensity;
                double dynamicGroup2 = input.State.Data.SigmaXeGroup2M2 *
                    input.State.Xe135NumberDensity;
                if (!IsCanonicalNonnegative(dynamicGroup1) ||
                    !IsCanonicalNonnegative(dynamicGroup2))
                {
                    return Invalid(
                        "XenonSpatial.DynamicAbsorption.Invalid",
                        ContractValidation.NodePath(baseNode.Node, ".dynamic_sigma_xe_m_inverse"),
                        "Dynamic Xe absorption must remain finite, canonical, and nonnegative without overflow or clamping.");
                }

                double effectiveGroup1 = baseNode.AbsorptionGroup1PerM + dynamicGroup1;
                double effectiveGroup2 = baseNode.AbsorptionGroup2PerM + dynamicGroup2;
                if (!IsCanonicalNonnegative(effectiveGroup1) ||
                    !IsCanonicalNonnegative(effectiveGroup2))
                {
                    return Invalid(
                        "XenonSpatial.EffectiveAbsorption.Invalid",
                        ContractValidation.NodePath(baseNode.Node, ".effective_absorption_m_inverse"),
                        "The effective absorption contribution must remain finite, canonical, and nonnegative without clamping.");
                }

                overlays.Add(new XenonSpatialOverlayValueV1(
                    baseNode.Node,
                    input.State.BundleId,
                    input.State.NuclideDataId,
                    input.State.NuclideDataDigest,
                    input.State.Xe135AtomInventory,
                    input.State.Xe135NumberDensity,
                    NormalizeZero(dynamicGroup1),
                    NormalizeZero(dynamicGroup2),
                    input.State.NuclideStateVersion));
                effectiveNodes.Add(new SpatialNodeCoefficients(
                    baseNode.Node,
                    baseNode.VolumeM3,
                    NormalizeZero(effectiveGroup1),
                    NormalizeZero(effectiveGroup2),
                    baseNode.DownscatterGroup1To2PerM,
                    baseNode.FissionGroup1PerM,
                    baseNode.FissionGroup2PerM,
                    baseNode.NuFissionGroup1PerM,
                    baseNode.NuFissionGroup2PerM,
                    baseNode.ChiGroup1,
                    baseNode.ChiGroup2,
                    baseNode.EnergyPerFissionJ));
            }

            ContractValidationResult<SpatialCoefficientSet> effectiveResult =
                baseCoefficients.TryRebindNodeCoefficients(effectiveNodes);
            if (!effectiveResult.IsValid)
            {
                return Invalid(
                    effectiveResult.FirstDiagnostic.Code,
                    effectiveResult.FirstDiagnostic.Path,
                    effectiveResult.FirstDiagnostic.Message);
            }

            Digest32 dynamicDigest = ComputeDynamicDigest(
                baseCoefficients.XenonBasis,
                baseCoefficients.ReferenceXeNumberDensityM3,
                baseCoefficientDigest,
                stateBinding,
                overlays);
            Digest32 effectiveDigest = ComputeEffectiveDigest(
                baseCoefficients.XenonBasis,
                    baseCoefficients.ReferenceXeNumberDensityM3,
                    baseCoefficientDigest,
                    dynamicDigest,
                    stateBinding,
                    effectiveResult.Value.Nodes);

            return ContractValidationResult<XenonSpatialCouplingResultV1>.Valid(
                new XenonSpatialCouplingResultV1(
                    effectiveResult.Value,
                    baseCoefficients.XenonBasis,
                    0.0,
                    baseCoefficientDigest,
                    dynamicDigest,
                    effectiveDigest,
                    stateBinding,
                    overlays));
        }

        /// <summary>
        /// Computes the canonical identity used to authenticate a base
        /// coefficient set before a dynamic Xe overlay is admitted. The
        /// identity covers the explicit basis/reference marker, stencil
        /// topology, node coefficients, conductances, and boundary terms.
        /// </summary>
        public static Digest32 ComputeBaseCoefficientDigest(
            SpatialCoefficientSet baseCoefficients)
        {
            if (baseCoefficients == null)
            {
                throw new ArgumentNullException(nameof(baseCoefficients));
            }

            byte[] digest = Phase5CanonicalBytesV1.HashBody(
                "CANDU-XENON-BASE-COEFFICIENT-V1",
                writer =>
                {
                    Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                    writer.Write((byte)baseCoefficients.XenonBasis);
                    Phase5CanonicalBytesV1.WriteDouble(
                        writer,
                        baseCoefficients.ReferenceXeNumberDensityM3);
                    SpatialStencil stencil = baseCoefficients.Stencil;
                    Phase5CanonicalBytesV1.WriteUInt32(writer, stencil.ChannelCount);
                    Phase5CanonicalBytesV1.WriteUInt32(writer, stencil.BundlePositionCount);
                    Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)stencil.Nodes.Count));
                    foreach (SpatialNodeStencil node in stencil.Nodes)
                    {
                        WriteNodeKey(writer, node.Node);
                        Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)node.FlatIndex));
                        Phase5CanonicalBytesV1.WriteUInt32(
                            writer,
                            checked((uint)node.NeighborTerms.Count));
                        foreach (SpatialNeighborTerm neighbor in node.NeighborTerms)
                        {
                            writer.Write((byte)neighbor.Direction);
                            WriteNodeKey(writer, neighbor.TargetNode);
                            Phase5CanonicalBytesV1.WriteUInt32(
                                writer,
                                checked((uint)neighbor.TargetFlatIndex));
                        }

                        Phase5CanonicalBytesV1.WriteUInt32(
                            writer,
                            checked((uint)node.BoundaryTerms.Count));
                        foreach (SpatialBoundaryTerm boundary in node.BoundaryTerms)
                        {
                            writer.Write((byte)boundary.Face);
                            writer.Write((byte)boundary.Classification);
                        }
                    }

                    Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)baseCoefficients.NodeCount));
                    foreach (SpatialNodeCoefficients node in baseCoefficients.Nodes)
                    {
                        WriteSpatialNode(writer, node);
                    }

                    SpatialEdgeKey[] edgeKeys = stencil.Nodes
                        .SelectMany(node => node.NeighborTerms.Select(term =>
                            new SpatialEdgeKey(node.Node, term.TargetNode)))
                        .Distinct()
                        .OrderBy(key => key)
                        .ToArray();
                    Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)edgeKeys.Length));
                    foreach (SpatialEdgeKey key in edgeKeys)
                    {
                        SpatialConductancePair conductance;
                        if (!baseCoefficients.TryGetEdge(key, out conductance))
                        {
                            throw new InvalidOperationException(
                                "The validated coefficient set is missing an expected edge conductance.");
                        }

                        WriteNodeKey(writer, key.First);
                        WriteNodeKey(writer, key.Second);
                        Phase5CanonicalBytesV1.WriteDouble(writer, conductance.Group1);
                        Phase5CanonicalBytesV1.WriteDouble(writer, conductance.Group2);
                    }

                    SpatialBoundaryKey[] boundaryKeys = stencil.Nodes
                        .SelectMany(node => node.BoundaryTerms.Select(term =>
                            new SpatialBoundaryKey(node.Node, term.Face)))
                        .Distinct()
                        .OrderBy(key => key)
                        .ToArray();
                    Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)boundaryKeys.Length));
                    foreach (SpatialBoundaryKey key in boundaryKeys)
                    {
                        SpatialConductancePair conductance;
                        if (!baseCoefficients.TryGetBoundary(key, out conductance))
                        {
                            throw new InvalidOperationException(
                                "The validated coefficient set is missing an expected boundary conductance.");
                        }

                        WriteNodeKey(writer, key.Node);
                        writer.Write((byte)key.Face);
                        Phase5CanonicalBytesV1.WriteDouble(writer, conductance.Group1);
                        Phase5CanonicalBytesV1.WriteDouble(writer, conductance.Group2);
                    }
                });
            return new Digest32(digest);
        }

        private static Digest32 ComputeDynamicDigest(
            XenonBasisV1 xenonBasis,
            double referenceXeNumberDensityM3,
            Digest32 baseCoefficientDigest,
            XenonSpatialStateBindingV1 stateBinding,
            List<XenonSpatialOverlayValueV1> overlays)
        {
            byte[] digest = Phase5CanonicalBytesV1.HashBody(
                "CANDU-XENON-DYNAMIC-OVERLAY-V1",
                writer =>
                {
                    Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                    writer.Write((byte)xenonBasis);
                    Phase5CanonicalBytesV1.WriteDouble(writer, referenceXeNumberDensityM3);
                    Phase5CanonicalBytesV1.WriteDigest(writer, baseCoefficientDigest);
                    WriteStateBinding(writer, stateBinding);
                    Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)overlays.Count));
                    foreach (XenonSpatialOverlayValueV1 overlay in overlays)
                    {
                        Phase5CanonicalBytesV1.WriteUInt32(writer, overlay.Node.ChannelId.Value);
                        Phase5CanonicalBytesV1.WriteUInt32(writer, overlay.Node.Position.Value);
                        Phase5CanonicalBytesV1.WriteStableId(writer, overlay.BundleId);
                        Phase5CanonicalBytesV1.WriteString(writer, overlay.NuclideDataId);
                        Phase5CanonicalBytesV1.WriteDigest(writer, overlay.NuclideDataDigest);
                        Phase5CanonicalBytesV1.WriteDouble(writer, overlay.Xe135AtomInventory);
                        Phase5CanonicalBytesV1.WriteDouble(writer, overlay.Xe135NumberDensity);
                        Phase5CanonicalBytesV1.WriteDouble(writer, overlay.DynamicAbsorptionGroup1PerM);
                        Phase5CanonicalBytesV1.WriteDouble(writer, overlay.DynamicAbsorptionGroup2PerM);
                        Phase5CanonicalBytesV1.WriteUInt64(writer, overlay.StateVersion);
                    }
                });
            return new Digest32(digest);
        }

        private static void WriteSpatialNode(
            BinaryWriter writer,
            SpatialNodeCoefficients node)
        {
            WriteNodeKey(writer, node.Node);
            Phase5CanonicalBytesV1.WriteDouble(writer, node.VolumeM3);
            Phase5CanonicalBytesV1.WriteDouble(writer, node.AbsorptionGroup1PerM);
            Phase5CanonicalBytesV1.WriteDouble(writer, node.AbsorptionGroup2PerM);
            Phase5CanonicalBytesV1.WriteDouble(writer, node.DownscatterGroup1To2PerM);
            Phase5CanonicalBytesV1.WriteDouble(writer, node.FissionGroup1PerM);
            Phase5CanonicalBytesV1.WriteDouble(writer, node.FissionGroup2PerM);
            Phase5CanonicalBytesV1.WriteDouble(writer, node.NuFissionGroup1PerM);
            Phase5CanonicalBytesV1.WriteDouble(writer, node.NuFissionGroup2PerM);
            Phase5CanonicalBytesV1.WriteDouble(writer, node.ChiGroup1);
            Phase5CanonicalBytesV1.WriteDouble(writer, node.ChiGroup2);
            Phase5CanonicalBytesV1.WriteDouble(writer, node.EnergyPerFissionJ);
        }

        private static void WriteNodeKey(BinaryWriter writer, NodeKey node)
        {
            Phase5CanonicalBytesV1.WriteUInt32(writer, node.ChannelId.Value);
            Phase5CanonicalBytesV1.WriteUInt32(writer, node.Position.Value);
        }

        private static Digest32 ComputeEffectiveDigest(
            XenonBasisV1 xenonBasis,
            double referenceXeNumberDensityM3,
            Digest32 baseCoefficientDigest,
            Digest32 dynamicDigest,
            XenonSpatialStateBindingV1 stateBinding,
            IReadOnlyList<SpatialNodeCoefficients> nodes)
        {
            byte[] digest = Phase5CanonicalBytesV1.HashBody(
                "CANDU-XENON-EFFECTIVE-COEFFICIENT-V1",
                writer =>
                {
                    Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                    writer.Write((byte)xenonBasis);
                    Phase5CanonicalBytesV1.WriteDouble(writer, referenceXeNumberDensityM3);
                    Phase5CanonicalBytesV1.WriteDigest(writer, baseCoefficientDigest);
                    Phase5CanonicalBytesV1.WriteDigest(writer, dynamicDigest);
                    WriteStateBinding(writer, stateBinding);
                    Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)nodes.Count));
                    foreach (SpatialNodeCoefficients node in nodes)
                    {
                        Phase5CanonicalBytesV1.WriteUInt32(writer, node.Node.ChannelId.Value);
                        Phase5CanonicalBytesV1.WriteUInt32(writer, node.Node.Position.Value);
                        Phase5CanonicalBytesV1.WriteDouble(writer, node.VolumeM3);
                        Phase5CanonicalBytesV1.WriteDouble(writer, node.AbsorptionGroup1PerM);
                        Phase5CanonicalBytesV1.WriteDouble(writer, node.AbsorptionGroup2PerM);
                        Phase5CanonicalBytesV1.WriteDouble(writer, node.DownscatterGroup1To2PerM);
                        Phase5CanonicalBytesV1.WriteDouble(writer, node.FissionGroup1PerM);
                        Phase5CanonicalBytesV1.WriteDouble(writer, node.FissionGroup2PerM);
                        Phase5CanonicalBytesV1.WriteDouble(writer, node.NuFissionGroup1PerM);
                        Phase5CanonicalBytesV1.WriteDouble(writer, node.NuFissionGroup2PerM);
                        Phase5CanonicalBytesV1.WriteDouble(writer, node.ChiGroup1);
                        Phase5CanonicalBytesV1.WriteDouble(writer, node.ChiGroup2);
                        Phase5CanonicalBytesV1.WriteDouble(writer, node.EnergyPerFissionJ);
                    }
                });
            return new Digest32(digest);
        }

        private static void WriteStateBinding(
            BinaryWriter writer,
            XenonSpatialStateBindingV1 stateBinding)
        {
            Phase5CanonicalBytesV1.WriteDouble(writer, stateBinding.SimulationTimeSeconds);
            Phase5CanonicalBytesV1.WriteDouble(writer, stateBinding.KineticAmplitude);
            Phase5CanonicalBytesV1.WriteUInt64(writer, stateBinding.CoreStateVersion);
            Phase5CanonicalBytesV1.WriteDigest(writer, stateBinding.StateDigest);
            Phase5CanonicalBytesV1.WriteDigest(writer, stateBinding.TopologyDigest);
            Phase5CanonicalBytesV1.WriteDigest(writer, stateBinding.DataPackDigest);
            Phase5CanonicalBytesV1.WriteUInt32(
                writer,
                checked((uint)stateBinding.NodeVersions.Count));
            foreach (XenonSpatialNuclideVersionV1 version in stateBinding.NodeVersions)
            {
                WriteNodeKey(writer, version.Node);
                Phase5CanonicalBytesV1.WriteUInt64(writer, version.StateVersion);
            }
        }

        private static bool IsCanonicalNonnegative(double value)
        {
            return ContractValidation.IsFinite(value) && value >= 0.0 &&
                   BitConverter.DoubleToInt64Bits(value) >= 0;
        }

        private static double NormalizeZero(double value)
        {
            return value == 0.0 ? 0.0 : value;
        }

        private static ContractValidationResult<XenonSpatialCouplingResultV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<XenonSpatialCouplingResultV1>.Invalid(code, path, message);
        }
    }

    /// <summary>
    /// Runs the existing P2-T02 spatial iteration/outer solve using one
    /// dynamic-Xe effective coefficient set at an explicit caller-scheduled
    /// spatial cadence. No cadence interval or convergence tolerance is chosen
    /// by this adapter.
    /// </summary>
    public sealed class XenonSpatialSolveResultV1
    {
        internal XenonSpatialSolveResultV1(
            XenonSpatialCouplingResultV1 coupling,
            SpatialSolveResult spatialSolve,
            SpatialRecomputeCadenceV1 nextCadence,
            double simulationTimeSeconds)
        {
            Coupling = coupling;
            SpatialSolve = spatialSolve;
            NextCadence = nextCadence;
            SimulationTimeSeconds = simulationTimeSeconds;
            AcceptedDynamicXenonDigest = coupling.DynamicXenonDigest;
            AcceptedEffectiveCoefficientDigest = coupling.EffectiveCoefficientDigest;
            AcceptedStateBinding = coupling.StateBinding;
        }

        public XenonSpatialCouplingResultV1 Coupling { get; }

        public SpatialSolveResult SpatialSolve { get; }

        public SpatialRecomputeCadenceV1 NextCadence { get; }

        public double SimulationTimeSeconds { get; }

        public Digest32 AcceptedDynamicXenonDigest { get; }

        public Digest32 AcceptedEffectiveCoefficientDigest { get; }

        public XenonSpatialStateBindingV1 AcceptedStateBinding { get; }

        public bool IsConverged
        {
            get { return SpatialSolve.IsConverged; }
        }

        public ContractValidationResult<bool> TryValidateForPositiveDuration(
            XenonSpatialStateBindingV1 currentStateBinding,
            XenonSpatialCouplingResultV1 currentCoupling)
        {
            if (currentStateBinding == null)
            {
                return ContractValidationResult<bool>.Invalid(
                    "XenonSpatialSolveBinding.StateBinding.Missing",
                    "current_state_binding",
                    "A positive-duration interval requires the exact current state/time/version binding.");
            }

            if (currentCoupling == null)
            {
                return ContractValidationResult<bool>.Invalid(
                    "XenonSpatialSolveBinding.Coupling.Missing",
                    "current_coupling",
                    "A positive-duration interval requires a current dynamic-Xe coupling.");
            }

            if (!IsConverged || SpatialSolve.FinalState == null)
            {
                return ContractValidationResult<bool>.Invalid(
                    "XenonSpatialSolveBinding.Solve.NotConverged",
                    "spatial_solve",
                    "A positive-duration interval requires a converged spatial solve with a usable final state.");
            }

            if (!ReferenceEquals(Coupling, currentCoupling))
            {
                return ContractValidationResult<bool>.Invalid(
                    "XenonSpatialSolveBinding.Coupling.Stale",
                    "current_coupling",
                    "The accepted solve must bind the exact current dynamic-Xe coupling object.");
            }

            ContractValidationResult<bool> stateBinding = AcceptedStateBinding.TryValidateExact(
                currentStateBinding);
            if (!stateBinding.IsValid)
            {
                return stateBinding;
            }

            if (!AcceptedDynamicXenonDigest.Equals(currentCoupling.DynamicXenonDigest))
            {
                return ContractValidationResult<bool>.Invalid(
                    "XenonSpatialSolveBinding.DynamicDigest.Stale",
                    "current_coupling.dynamic_xenon_digest",
                    "The accepted solve does not bind the current dynamic Xe atom-inventory digest.");
            }

            if (!AcceptedEffectiveCoefficientDigest.Equals(currentCoupling.EffectiveCoefficientDigest))
            {
                return ContractValidationResult<bool>.Invalid(
                    "XenonSpatialSolveBinding.CoefficientDigest.Stale",
                    "current_coupling.effective_coefficient_digest",
                    "The accepted solve does not bind the current effective coefficient digest.");
            }

            return ContractValidationResult<bool>.Valid(true);
        }
    }

    public static class XenonSpatialSolveV1
    {
        public static ContractValidationResult<XenonSpatialSolveResultV1> TryApply(
            XenonSpatialCouplingResultV1 coupling,
            SpatialStencil stencil,
            SpatialRecomputeCadenceV1 cadence,
            double exactSimulationTimeSeconds,
            SpatialLinearSolvePolicy linearSolvePolicy,
            SpatialConvergencePolicy convergencePolicy,
            double targetPowerW,
            double initialEigenvalue,
            double[]? initialGroup1Flux = null,
            double[]? initialGroup2Flux = null)
        {
            if (coupling == null)
            {
                return Invalid(
                    "XenonSpatialSolve.Coupling.Missing",
                    "coupling",
                    "A spatial solve requires a dynamic-Xe coupling result.");
            }

            if (stencil == null)
            {
                return Invalid(
                    "XenonSpatialSolve.Stencil.Missing",
                    "stencil",
                    "A spatial solve requires the exact assembled stencil.");
            }

            if (cadence == null)
            {
                return Invalid(
                    "XenonSpatialSolve.Cadence.Missing",
                    "cadence",
                    "A spatial solve requires an explicit caller-supplied cadence.");
            }

            if (!IsCanonicalTime(exactSimulationTimeSeconds))
            {
                return Invalid(
                    "XenonSpatialSolve.Time.Invalid",
                    "simulation_time_s",
                    "The spatial solve time must be finite, canonical, and nonnegative SI seconds.");
            }

            ContractValidationResult<double> scheduledTime = cadence.TryGetNextEventTime();
            if (!scheduledTime.IsValid)
            {
                return Invalid(
                    scheduledTime.FirstDiagnostic.Code,
                    scheduledTime.FirstDiagnostic.Path,
                    scheduledTime.FirstDiagnostic.Message);
            }

            if (scheduledTime.Value != exactSimulationTimeSeconds)
            {
                return Invalid(
                    "XenonSpatialSolve.Cadence.TimeMismatch",
                    "cadence.next_event_time_s",
                    "The solve must occur at the exact next caller-scheduled spatial event.");
            }

            if (coupling.StateBinding.SimulationTimeSeconds != exactSimulationTimeSeconds)
            {
                return Invalid(
                    "XenonSpatialSolve.StateBinding.TimeMismatch",
                    "coupling.state_binding.simulation_time_s",
                    "The dynamic-Xe state binding time must equal the exact scheduled spatial-solve time.");
            }

            if (!ReferenceEquals(coupling.Coefficients.Stencil, stencil))
            {
                return Invalid(
                    "XenonSpatialSolve.Stencil.IdentityMismatch",
                    "coupling.coefficients.stencil",
                    "The dynamic-Xe coefficient set must be solved on the exact stencil that produced it.");
            }

            ContractValidationResult<SpatialEigenIteration> iteration =
                SpatialEigenIteration.TryCreate(
                    stencil,
                    coupling.Coefficients,
                    linearSolvePolicy,
                    targetPowerW,
                    initialEigenvalue,
                    initialGroup1Flux,
                    initialGroup2Flux);
            if (!iteration.IsValid)
            {
                return Invalid(
                    iteration.FirstDiagnostic.Code,
                    iteration.FirstDiagnostic.Path,
                    iteration.FirstDiagnostic.Message);
            }

            ContractValidationResult<SpatialEigenSolve> solve =
                SpatialEigenSolve.TryCreate(iteration.Value, convergencePolicy);
            if (!solve.IsValid)
            {
                return Invalid(
                    solve.FirstDiagnostic.Code,
                    solve.FirstDiagnostic.Path,
                    solve.FirstDiagnostic.Message);
            }

            ContractValidationResult<SpatialSolveResult> spatialResult = solve.Value.TrySolve();
            if (!spatialResult.IsValid)
            {
                return Invalid(
                    spatialResult.FirstDiagnostic.Code,
                    spatialResult.FirstDiagnostic.Path,
                    spatialResult.FirstDiagnostic.Message);
            }

            if (!spatialResult.Value.IsConverged ||
                spatialResult.Value.FinalState == null)
            {
                return Invalid(
                    "XenonSpatialSolve.Solve.NotConverged",
                    "spatial_solve",
                    "A dynamic-Xe spatial solve must converge before its cadence or result can be accepted.");
            }

            ContractValidationResult<SpatialRecomputeCadenceV1> advanced = cadence.TryAdvance();
            if (!advanced.IsValid)
            {
                return Invalid(
                    advanced.FirstDiagnostic.Code,
                    advanced.FirstDiagnostic.Path,
                    advanced.FirstDiagnostic.Message);
            }

            return ContractValidationResult<XenonSpatialSolveResultV1>.Valid(
                new XenonSpatialSolveResultV1(
                    coupling,
                    spatialResult.Value,
                    advanced.Value,
                    exactSimulationTimeSeconds));
        }

        private static bool IsCanonicalTime(double value)
        {
            return ContractValidation.IsFinite(value) && value >= 0.0 &&
                   BitConverter.DoubleToInt64Bits(value) >= 0;
        }

        private static ContractValidationResult<XenonSpatialSolveResultV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<XenonSpatialSolveResultV1>.Invalid(code, path, message);
        }
    }
}
