using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ReactorSim.Core
{
    /// <summary>
    /// Immutable authoritative I-135/Xe-135 state for every node of one
    /// full-core model. The state owns only nuclide transitions; neutron
    /// production and local flux are supplied by the already accepted spatial
    /// shape. This keeps the state engine-neutral and makes each transition a
    /// side-effect-free candidate until its caller commits it.
    /// </summary>
    public sealed class XenonSpatialStateV1
    {
        public const uint CurrentSchemaVersion = 1;
        public const string Identity = "xenon-spatial-state-v1";

        private readonly ReadOnlyCollection<XenonSpatialNodeInputV1> _nodeInputs;
        private readonly ReadOnlyCollection<NuclideStateEnvelopeV1> _nodeStates;
        private readonly Dictionary<string, NuclideDataV1> _dataByMaterial;
        private readonly FullCoreDiffusionModelV1 _model;
        private readonly double _nodeVolumeM3;

        private XenonSpatialStateV1(
            FullCoreDiffusionModelV1 model,
            double simulationTimeSeconds,
            ulong coreStateVersion,
            IEnumerable<XenonSpatialNodeInputV1> nodeInputs,
            IEnumerable<NuclideStateEnvelopeV1> nodeStates)
        {
            _model = model;
            SimulationTimeSeconds = simulationTimeSeconds;
            CoreStateVersion = coreStateVersion;
            TopologyDigest = new Digest32(model.DataPack.Descriptor.TopologyDigest.ToArray());
            DataPackDigest = new Digest32(model.DataPack.Descriptor.ContentDigest.ToArray());
            _nodeInputs = new ReadOnlyCollection<XenonSpatialNodeInputV1>(
                nodeInputs.ToArray());
            _nodeStates = new ReadOnlyCollection<NuclideStateEnvelopeV1>(
                nodeStates.ToArray());
            _nodeVolumeM3 = model.DataPack.NodeVolumeM3;
            _dataByMaterial = _nodeStates
                .Select(state => state.Data)
                .GroupBy(data => data.MaterialVariantId.Value, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            StateDigest = ComputeStateDigest(
                model,
                simulationTimeSeconds,
                coreStateVersion,
                _nodeInputs);
        }

        public FullCoreDiffusionModelV1 Model
        {
            get { return _model; }
        }

        public CoreTopology Topology
        {
            get { return _model.Topology; }
        }

        public SpatialStencil Stencil
        {
            get { return _model.Stencil; }
        }

        public Digest32 TopologyDigest { get; }

        public Digest32 DataPackDigest { get; }

        public double SimulationTimeSeconds { get; }

        public ulong CoreStateVersion { get; }

        public Digest32 StateDigest { get; }

        public string StateDigestHex
        {
            get { return ToHex(StateDigest); }
        }

        public IReadOnlyList<XenonSpatialNodeInputV1> NodeInputs
        {
            get { return _nodeInputs; }
        }

        public IReadOnlyList<NuclideStateEnvelopeV1> NodeStates
        {
            get { return _nodeStates; }
        }

        public double MeanXe135NumberDensityM3
        {
            get { return _nodeStates.Average(state => state.Xe135NumberDensity); }
        }

        public double MaxXe135NumberDensityM3
        {
            get { return _nodeStates.Max(state => state.Xe135NumberDensity); }
        }

        public double MeanDynamicAbsorptionGroup1PerM
        {
            get { return _nodeStates.Average(state => state.Data.SigmaXeGroup1M2 * state.Xe135NumberDensity); }
        }

        public double MeanDynamicAbsorptionGroup2PerM
        {
            get { return _nodeStates.Average(state => state.Data.SigmaXeGroup2M2 * state.Xe135NumberDensity); }
        }

        public double MaxDynamicAbsorptionGroup1PerM
        {
            get { return _nodeStates.Max(state => state.Data.SigmaXeGroup1M2 * state.Xe135NumberDensity); }
        }

        public double MaxDynamicAbsorptionGroup2PerM
        {
            get { return _nodeStates.Max(state => state.Data.SigmaXeGroup2M2 * state.Xe135NumberDensity); }
        }

        public static ContractValidationResult<XenonSpatialStateV1> TryCreate(
            FullCoreDiffusionModelV1 model,
            IEnumerable<BundleState> bundles,
            NuclideDataV1 data)
        {
            return TryCreate(model, bundles, data, 0.0, 0UL);
        }

        public static ContractValidationResult<XenonSpatialStateV1> TryCreate(
            FullCoreDiffusionModelV1 model,
            IEnumerable<BundleState> bundles,
            NuclideDataV1 data,
            double simulationTimeSeconds,
            ulong coreStateVersion)
        {
            if (model == null)
            {
                return Invalid(
                    "XenonSpatialState.Model.Missing",
                    "model",
                    "A spatial I/Xe state requires the exact full-core diffusion model.");
            }

            if (data == null)
            {
                return Invalid(
                    "XenonSpatialState.Data.Missing",
                    "data",
                    "A spatial I/Xe state requires validated material data.");
            }

            if (!IsCanonicalTime(simulationTimeSeconds))
            {
                return Invalid(
                    "XenonSpatialState.Time.Invalid",
                    "simulation_time_s",
                    "The spatial I/Xe state time must be finite, canonical, and nonnegative SI seconds.");
            }

            ContractValidationResult<BundleInventory> inventoryResult =
                TryFullInventory(model, bundles);
            if (!inventoryResult.IsValid)
            {
                return Invalid(
                    inventoryResult.FirstDiagnostic.Code,
                    inventoryResult.FirstDiagnostic.Path,
                    inventoryResult.FirstDiagnostic.Message);
            }

            var states = new List<NuclideStateEnvelopeV1>(model.NodeCount);
            foreach (SpatialNodeStencil node in model.Stencil.Nodes)
            {
                BundleState? bundle = inventoryResult.Value.Get(node.Node);
                if (bundle == null)
                {
                    return Invalid(
                        "XenonSpatialState.Inventory.Bundle.Missing",
                        ContractValidation.NodePath(node.Node, ".bundle_id"),
                        "Every spatial I/Xe node requires an occupied live bundle.");
                }

                if (bundle.MaterialVariantId != data.MaterialVariantId)
                {
                    return Invalid(
                        "XenonSpatialState.MaterialVariant.Mismatch",
                        ContractValidation.NodePath(node.Node, ".material_variant_id"),
                        "The supplied nuclide data must match every live bundle material variant.");
                }

                ContractValidationResult<NuclideStateEnvelopeV1> fresh =
                    NuclideStateEnvelopeV1.TryCreateFresh(
                        bundle.BundleId,
                        0.0,
                        0.0,
                        model.DataPack.NodeVolumeM3,
                        0UL,
                        data);
                if (!fresh.IsValid)
                {
                    return Invalid(
                        fresh.FirstDiagnostic.Code,
                        fresh.FirstDiagnostic.Path,
                        fresh.FirstDiagnostic.Message);
                }

                states.Add(fresh.Value);
            }

            return CreateValidated(model, simulationTimeSeconds, coreStateVersion, states);
        }

        /// <summary>
        /// Creates a state from caller-supplied per-bundle envelopes. This is
        /// the controlled seam used by deterministic tests and by a future
        /// persisted gameplay state; inputs are reordered into stencil order.
        /// </summary>
        public static ContractValidationResult<XenonSpatialStateV1> TryCreate(
            FullCoreDiffusionModelV1 model,
            IEnumerable<BundleState> bundles,
            IEnumerable<NuclideStateEnvelopeV1> nodeStates,
            double simulationTimeSeconds = 0.0,
            ulong coreStateVersion = 0UL)
        {
            if (model == null)
            {
                return Invalid(
                    "XenonSpatialState.Model.Missing",
                    "model",
                    "A spatial I/Xe state requires the exact full-core diffusion model.");
            }

            if (!IsCanonicalTime(simulationTimeSeconds))
            {
                return Invalid(
                    "XenonSpatialState.Time.Invalid",
                    "simulation_time_s",
                    "The spatial I/Xe state time must be finite, canonical, and nonnegative SI seconds.");
            }

            ContractValidationResult<BundleInventory> inventoryResult =
                TryFullInventory(model, bundles);
            if (!inventoryResult.IsValid)
            {
                return Invalid(
                    inventoryResult.FirstDiagnostic.Code,
                    inventoryResult.FirstDiagnostic.Path,
                    inventoryResult.FirstDiagnostic.Message);
            }

            if (nodeStates == null)
            {
                return Invalid(
                    "XenonSpatialState.NodeStates.Missing",
                    "node_states",
                    "A spatial I/Xe state requires one envelope per full-core node.");
            }

            NuclideStateEnvelopeV1[] stateRecords = nodeStates.ToArray();
            if (stateRecords.Any(state => state == null))
            {
                return Invalid(
                    "XenonSpatialState.NodeStates.Null",
                    "node_states",
                    "A spatial I/Xe state may not contain null node envelopes.");
            }

            if (stateRecords.Length != model.NodeCount)
            {
                return Invalid(
                    "XenonSpatialState.NodeStates.CountMismatch",
                    "node_states",
                    "A spatial I/Xe state requires exactly one envelope per full-core node.");
            }

            var statesByBundle = new Dictionary<StableId, NuclideStateEnvelopeV1>();
            foreach (NuclideStateEnvelopeV1 state in stateRecords)
            {
                if (!statesByBundle.TryAdd(state.BundleId, state))
                {
                    return Invalid(
                        "XenonSpatialState.NodeStates.BundleId.Duplicate",
                        "node_states",
                        "A spatial I/Xe state may contain only one envelope per persistent bundle.");
                }
            }

            var orderedStates = new List<NuclideStateEnvelopeV1>(model.NodeCount);
            foreach (SpatialNodeStencil node in model.Stencil.Nodes)
            {
                BundleState? bundle = inventoryResult.Value.Get(node.Node);
                if (bundle == null ||
                    !statesByBundle.TryGetValue(bundle.BundleId, out NuclideStateEnvelopeV1 state))
                {
                    return Invalid(
                        "XenonSpatialState.BundleId.Mismatch",
                        ContractValidation.NodePath(node.Node, ".bundle_id"),
                        "Every node envelope must bind the exact live bundle at that node.");
                }

                if (state.Data.MaterialVariantId != bundle.MaterialVariantId)
                {
                    return Invalid(
                        "XenonSpatialState.MaterialVariant.Mismatch",
                        ContractValidation.NodePath(node.Node, ".material_variant_id"),
                        "Every node envelope must retain the live bundle material variant.");
                }

                if (state.NodeVolumeM3 != model.DataPack.NodeVolumeM3)
                {
                    return Invalid(
                        "XenonSpatialState.NodeVolume.Mismatch",
                        ContractValidation.NodePath(node.Node, ".node_volume_m3"),
                        "Every node envelope volume must equal the model's exact spatial control volume.");
                }

                orderedStates.Add(state);
            }

            return CreateValidated(model, simulationTimeSeconds, coreStateVersion, orderedStates);
        }

        public ContractValidationResult<XenonSpatialStateBindingV1> TryCreateBinding(
            double kineticAmplitude)
        {
            if (!IsCanonicalNonnegative(kineticAmplitude))
            {
                return ContractValidationResult<XenonSpatialStateBindingV1>.Invalid(
                    "XenonSpatialState.Binding.Amplitude.Invalid",
                    "kinetic_amplitude",
                    "The state binding amplitude must be finite, canonical, and nonnegative.");
            }

            XenonSpatialNuclideVersionV1[] versions = _nodeInputs
                .Select(input => XenonSpatialNuclideVersionV1.TryCreate(
                    input.Node,
                    input.State.NuclideStateVersion))
                .Select(result => result.Value)
                .ToArray();
            return XenonSpatialStateBindingV1.TryCreate(
                SimulationTimeSeconds,
                kineticAmplitude,
                CoreStateVersion,
                StateDigest,
                TopologyDigest,
                DataPackDigest,
                versions);
        }

        /// <summary>
        /// Advances all node envelopes with one left-endpoint Euler interval.
        /// The supplied state binding is checked before any transition is
        /// attempted, and a failed node leaves this state untouched.
        /// </summary>
        public ContractValidationResult<XenonSpatialAdvanceResultV1> TryAdvance(
            XenonSpatialStateBindingV1 acceptedBinding,
            double targetSimulationTimeSeconds,
            double kineticAmplitude,
            SpatialCoefficientSet coefficients,
            IReadOnlyList<double> group1Flux,
            IReadOnlyList<double> group2Flux,
            double fluxScale,
            StableId ownerEventId)
        {
            ContractValidationResult<XenonSpatialStateBindingV1> expectedBinding =
                TryCreateBinding(kineticAmplitude);
            if (!expectedBinding.IsValid)
            {
                return InvalidAdvance(
                    expectedBinding.FirstDiagnostic.Code,
                    expectedBinding.FirstDiagnostic.Path,
                    expectedBinding.FirstDiagnostic.Message);
            }

            ContractValidationResult<bool> binding = acceptedBinding == null
                ? ContractValidationResult<bool>.Invalid(
                    "XenonSpatialState.Advance.Binding.Missing",
                    "accepted_binding",
                    "A positive-duration I/Xe advance requires the exact current state binding.")
                : acceptedBinding.TryValidateExact(expectedBinding.Value);
            if (!binding.IsValid)
            {
                return InvalidAdvance(
                    binding.FirstDiagnostic.Code,
                    binding.FirstDiagnostic.Path,
                    binding.FirstDiagnostic.Message);
            }

            if (!IsCanonicalTime(targetSimulationTimeSeconds) ||
                targetSimulationTimeSeconds <= SimulationTimeSeconds)
            {
                return InvalidAdvance(
                    "XenonSpatialState.Advance.Time.Invalid",
                    "target_simulation_time_s",
                    "An I/Xe advance must move to a finite, canonical, strictly later simulation time.");
            }

            double deltaTimeSeconds = targetSimulationTimeSeconds - SimulationTimeSeconds;
            if (!IsCanonicalPositive(deltaTimeSeconds))
            {
                return InvalidAdvance(
                    "XenonSpatialState.Advance.Duration.Invalid",
                    "delta_time_s",
                    "The I/Xe advance duration must be finite, canonical, and strictly positive.");
            }

            if (coefficients == null)
            {
                return InvalidAdvance(
                    "XenonSpatialState.Advance.Coefficients.Missing",
                    "coefficients",
                    "An I/Xe advance requires the accepted spatial coefficient set.");
            }

            if (!ReferenceEquals(coefficients.Stencil, Stencil))
            {
                return InvalidAdvance(
                    "XenonSpatialState.Advance.Coefficients.StencilMismatch",
                    "coefficients.stencil",
                    "I/Xe production must use coefficients from the exact state model stencil.");
            }

            if (group1Flux == null || group2Flux == null ||
                group1Flux.Count != NodeInputs.Count ||
                group2Flux.Count != NodeInputs.Count)
            {
                return InvalidAdvance(
                    "XenonSpatialState.Advance.Flux.CountMismatch",
                    "flux",
                    "I/Xe production requires one fast and thermal flux value per spatial node.");
            }

            if (!IsCanonicalNonnegative(fluxScale))
            {
                return InvalidAdvance(
                    "XenonSpatialState.Advance.FluxScale.Invalid",
                    "flux_scale",
                    "The I/Xe flux scale must be finite, canonical, and nonnegative.");
            }

            for (int nodeIndex = 0; nodeIndex < NodeInputs.Count; nodeIndex++)
            {
                if (!IsCanonicalNonnegative(group1Flux[nodeIndex]) ||
                    !IsCanonicalNonnegative(group2Flux[nodeIndex]))
                {
                    return InvalidAdvance(
                        "XenonSpatialState.Advance.LocalFlux.Invalid",
                        ContractValidation.NodePath(NodeInputs[nodeIndex].Node, ".flux"),
                        "Each supplied local group flux must be finite, canonical, and nonnegative.");
                }
            }

            if (ownerEventId.IsEmpty)
            {
                return InvalidAdvance(
                    "XenonSpatialState.Advance.OwnerEvent.Empty",
                    "owner_event_id",
                    "Every I/Xe interval requires an explicit owner event identity.");
            }

            if (CoreStateVersion == ulong.MaxValue)
            {
                return InvalidAdvance(
                    "XenonSpatialState.Advance.CoreStateVersion.Overflow",
                    "core_state_version",
                    "The spatial I/Xe core-state version cannot increment beyond UInt64.MaxValue.");
            }

            var nextStates = new List<NuclideStateEnvelopeV1>(NodeInputs.Count);
            var transitions = new List<NuclideTransitionRecordV1>(NodeInputs.Count);
            foreach (int nodeIndex in Enumerable.Range(0, NodeInputs.Count))
            {
                SpatialNodeCoefficients nodeCoefficients = coefficients.Nodes[nodeIndex];
                double actualGroup1Flux = NormalizeZero(group1Flux[nodeIndex] * fluxScale);
                double actualGroup2Flux = NormalizeZero(group2Flux[nodeIndex] * fluxScale);
                double fissionRateDensity = NormalizeZero(
                    nodeCoefficients.FissionGroup1PerM * actualGroup1Flux +
                    nodeCoefficients.FissionGroup2PerM * actualGroup2Flux);
                if (!IsCanonicalNonnegative(actualGroup1Flux) ||
                    !IsCanonicalNonnegative(actualGroup2Flux) ||
                    !IsCanonicalNonnegative(fissionRateDensity))
                {
                    return InvalidAdvance(
                        "XenonSpatialState.Advance.LocalFlux.Invalid",
                        ContractValidation.NodePath(NodeInputs[nodeIndex].Node, ".flux"),
                        "Each local I/Xe flux and fission-rate value must remain finite, canonical, and nonnegative.");
                }

                NuclideStateEnvelopeV1 current = NodeInputs[nodeIndex].State;
                ContractValidationResult<NuclideIntegrationInputV1> input =
                    NuclideIntegrationInputV1.TryCreate(
                        ownerEventId,
                        current.NuclideStateVersion,
                        EventRankV1.KineticNuclideStep,
                        SimulationTimeSeconds,
                        deltaTimeSeconds,
                        CoreStateVersion,
                        fissionRateDensity,
                        actualGroup1Flux,
                        actualGroup2Flux,
                        current.Data,
                        StateDigest);
                if (!input.IsValid)
                {
                    return InvalidAdvance(
                        input.FirstDiagnostic.Code,
                        input.FirstDiagnostic.Path,
                        input.FirstDiagnostic.Message);
                }

                ContractValidationResult<NuclideIntegrationResultV1> transition =
                    NuclideIntegrationTransitionV1.TryApply(current, input.Value);
                if (!transition.IsValid)
                {
                    return InvalidAdvance(
                        transition.FirstDiagnostic.Code,
                        ContractValidation.NodePath(NodeInputs[nodeIndex].Node, ".nuclide_state"),
                        transition.FirstDiagnostic.Message);
                }

                nextStates.Add(transition.Value.ResultingState);
                transitions.Add(transition.Value.Transition);
            }

            ContractValidationResult<XenonSpatialStateV1> next = CreateValidated(
                _model,
                targetSimulationTimeSeconds,
                CoreStateVersion + 1UL,
                nextStates);
            if (!next.IsValid)
            {
                return InvalidAdvance(
                    next.FirstDiagnostic.Code,
                    next.FirstDiagnostic.Path,
                    next.FirstDiagnostic.Message);
            }

            return ContractValidationResult<XenonSpatialAdvanceResultV1>.Valid(
                new XenonSpatialAdvanceResultV1(
                    this,
                    next.Value,
                    deltaTimeSeconds,
                    transitions));
        }

        /// <summary>
        /// Convenience seam for callers that already hold a full-core solve.
        /// It preserves the solve's exact coefficient and flux identities
        /// instead of copying node arrays through a presentation layer.
        /// </summary>
        public ContractValidationResult<XenonSpatialAdvanceResultV1> TryAdvance(
            XenonSpatialStateBindingV1 acceptedBinding,
            double targetSimulationTimeSeconds,
            double kineticAmplitude,
            FullCoreDiffusionSolveResultV1 spatialSolve,
            double fluxScale,
            StableId ownerEventId)
        {
            if (spatialSolve == null)
            {
                return InvalidAdvance(
                    "XenonSpatialState.Advance.SpatialSolve.Missing",
                    "spatial_solve",
                    "An I/Xe advance requires the exact accepted full-core spatial solve.");
            }

            if (!new Digest32(spatialSolve.DataPack.Descriptor.TopologyDigest.ToArray())
                    .Equals(TopologyDigest) ||
                !new Digest32(spatialSolve.DataPack.Descriptor.ContentDigest.ToArray())
                    .Equals(DataPackDigest))
            {
                return InvalidAdvance(
                    "XenonSpatialState.Advance.SpatialSolve.IdentityMismatch",
                    "spatial_solve.data_pack",
                    "The I/Xe advance must use a spatial solve from the exact state topology and data pack.");
            }

            if (spatialSolve.XenonStateBinding != null && acceptedBinding != null)
            {
                ContractValidationResult<bool> solveBinding =
                    spatialSolve.XenonStateBinding.TryValidateExact(acceptedBinding);
                if (!solveBinding.IsValid)
                {
                    return InvalidAdvance(
                        solveBinding.FirstDiagnostic.Code,
                        solveBinding.FirstDiagnostic.Path,
                        solveBinding.FirstDiagnostic.Message);
                }
            }

            return TryAdvance(
                acceptedBinding!,
                targetSimulationTimeSeconds,
                kineticAmplitude,
                spatialSolve.Coefficients,
                spatialSolve.Group1Flux,
                spatialSolve.Group2Flux,
                fluxScale,
                ownerEventId);
        }

        public ContractValidationResult<XenonSpatialAdvanceResultV1> TryAdvance(
            double targetSimulationTimeSeconds,
            double kineticAmplitude,
            SpatialCoefficientSet coefficients,
            IReadOnlyList<double> group1Flux,
            IReadOnlyList<double> group2Flux,
            double fluxScale,
            StableId ownerEventId)
        {
            ContractValidationResult<XenonSpatialStateBindingV1> binding =
                TryCreateBinding(kineticAmplitude);
            if (!binding.IsValid)
            {
                return InvalidAdvance(
                    binding.FirstDiagnostic.Code,
                    binding.FirstDiagnostic.Path,
                    binding.FirstDiagnostic.Message);
            }

            return TryAdvance(
                binding.Value,
                targetSimulationTimeSeconds,
                kineticAmplitude,
                coefficients,
                group1Flux,
                group2Flux,
                fluxScale,
                ownerEventId);
        }

        public ContractValidationResult<XenonSpatialAdvanceResultV1> TryAdvance(
            double targetSimulationTimeSeconds,
            double kineticAmplitude,
            FullCoreDiffusionSolveResultV1 spatialSolve,
            double fluxScale,
            StableId ownerEventId)
        {
            ContractValidationResult<XenonSpatialStateBindingV1> binding =
                TryCreateBinding(kineticAmplitude);
            if (!binding.IsValid)
            {
                return InvalidAdvance(
                    binding.FirstDiagnostic.Code,
                    binding.FirstDiagnostic.Path,
                    binding.FirstDiagnostic.Message);
            }

            return TryAdvance(
                binding.Value,
                targetSimulationTimeSeconds,
                kineticAmplitude,
                spatialSolve,
                fluxScale,
                ownerEventId);
        }

        /// <summary>
        /// Rebinds the immutable nuclide state to a new full inventory after a
        /// refuelling transition. Retained bundle histories move with their
        /// persistent identity; fresh bundles receive zero I/Xe inventories.
        /// </summary>
        public ContractValidationResult<XenonSpatialStateV1> TryRebindInventory(
            IEnumerable<BundleState> bundles,
            ulong nextCoreStateVersion)
        {
            if (nextCoreStateVersion != CoreStateVersion + 1UL)
            {
                if (CoreStateVersion == ulong.MaxValue)
                {
                    return Invalid(
                        "XenonSpatialState.Rebind.CoreStateVersion.Overflow",
                        "core_state_version",
                        "The spatial I/Xe core-state version cannot increment beyond UInt64.MaxValue.");
                }

                return Invalid(
                    "XenonSpatialState.Rebind.CoreStateVersion.Mismatch",
                    "core_state_version",
                    "A refuelling rebind must advance the core-state version exactly once.");
            }

            ContractValidationResult<BundleInventory> inventoryResult =
                TryFullInventory(_model, bundles);
            if (!inventoryResult.IsValid)
            {
                return Invalid(
                    inventoryResult.FirstDiagnostic.Code,
                    inventoryResult.FirstDiagnostic.Path,
                    inventoryResult.FirstDiagnostic.Message);
            }

            var byBundleId = _nodeStates.ToDictionary(state => state.BundleId, state => state);
            var nextStates = new List<NuclideStateEnvelopeV1>(_model.NodeCount);
            foreach (SpatialNodeStencil node in _model.Stencil.Nodes)
            {
                BundleState? bundle = inventoryResult.Value.Get(node.Node);
                if (bundle == null)
                {
                    return Invalid(
                        "XenonSpatialState.Rebind.Bundle.Missing",
                        ContractValidation.NodePath(node.Node, ".bundle_id"),
                        "Every rebound spatial node requires an occupied live bundle.");
                }

                NuclideStateEnvelopeV1? retained;
                if (byBundleId.TryGetValue(bundle.BundleId, out retained))
                {
                    if (retained.Data.MaterialVariantId != bundle.MaterialVariantId)
                    {
                        return Invalid(
                            "XenonSpatialState.Rebind.MaterialVariant.Mismatch",
                            ContractValidation.NodePath(node.Node, ".material_variant_id"),
                            "A retained bundle may not change its nuclide material identity during rebind.");
                    }

                    ContractValidationResult<NuclideStateEnvelopeV1> moved =
                        retained.TryMoveToVolume(_nodeVolumeM3);
                    if (!moved.IsValid)
                    {
                        return Invalid(
                            moved.FirstDiagnostic.Code,
                            moved.FirstDiagnostic.Path,
                            moved.FirstDiagnostic.Message);
                    }

                    nextStates.Add(moved.Value);
                    continue;
                }

                if (!_dataByMaterial.TryGetValue(
                        bundle.MaterialVariantId.Value,
                        out NuclideDataV1 freshData))
                {
                    return Invalid(
                        "XenonSpatialState.Rebind.Data.Missing",
                        ContractValidation.NodePath(node.Node, ".nuclide_data"),
                        "A fresh bundle requires an explicit nuclide data identity for its material variant.");
                }

                ContractValidationResult<NuclideStateEnvelopeV1> fresh =
                    NuclideStateEnvelopeV1.TryCreateFresh(
                        bundle.BundleId,
                        0.0,
                        0.0,
                        _nodeVolumeM3,
                        0UL,
                        freshData);
                if (!fresh.IsValid)
                {
                    return Invalid(
                        fresh.FirstDiagnostic.Code,
                        fresh.FirstDiagnostic.Path,
                        fresh.FirstDiagnostic.Message);
                }

                nextStates.Add(fresh.Value);
            }

            return CreateValidated(
                _model,
                SimulationTimeSeconds,
                nextCoreStateVersion,
                nextStates);
        }

        public ContractValidationResult<XenonSpatialStateV1> TryRebindInventory(
            IEnumerable<BundleState> bundles)
        {
            if (CoreStateVersion == ulong.MaxValue)
            {
                return Invalid(
                    "XenonSpatialState.Rebind.CoreStateVersion.Overflow",
                    "core_state_version",
                    "The spatial I/Xe core-state version cannot increment beyond UInt64.MaxValue.");
            }

            return TryRebindInventory(bundles, CoreStateVersion + 1UL);
        }

        public bool TryGetNodeState(NodeKey node, out NuclideStateEnvelopeV1? state)
        {
            int index;
            if (!_model.Topology.TryGetFlatIndex(node, out index) ||
                index >= _nodeStates.Count)
            {
                state = null;
                return false;
            }

            state = _nodeStates[index];
            return true;
        }

        private static ContractValidationResult<XenonSpatialStateV1> CreateValidated(
            FullCoreDiffusionModelV1 model,
            double simulationTimeSeconds,
            ulong coreStateVersion,
            IEnumerable<NuclideStateEnvelopeV1> states)
        {
            NuclideStateEnvelopeV1[] stateRecords = states.ToArray();
            if (stateRecords.Length != model.NodeCount || stateRecords.Any(state => state == null))
            {
                return Invalid(
                    "XenonSpatialState.NodeStates.CountMismatch",
                    "node_states",
                    "A spatial I/Xe state requires one non-null envelope per full-core node.");
            }

            var inputs = new List<XenonSpatialNodeInputV1>(model.NodeCount);
            for (int index = 0; index < model.Stencil.Nodes.Count; index++)
            {
                ContractValidationResult<XenonSpatialNodeInputV1> input =
                    XenonSpatialNodeInputV1.TryCreate(
                        model.Stencil.Nodes[index].Node,
                        stateRecords[index]);
                if (!input.IsValid)
                {
                    return Invalid(
                        input.FirstDiagnostic.Code,
                        input.FirstDiagnostic.Path,
                        input.FirstDiagnostic.Message);
                }

                inputs.Add(input.Value);
            }

            return ContractValidationResult<XenonSpatialStateV1>.Valid(
                new XenonSpatialStateV1(
                    model,
                    simulationTimeSeconds,
                    coreStateVersion,
                    inputs,
                    stateRecords));
        }

        private static ContractValidationResult<BundleInventory> TryFullInventory(
            FullCoreDiffusionModelV1 model,
            IEnumerable<BundleState> bundles)
        {
            if (bundles == null)
            {
                return ContractValidationResult<BundleInventory>.Invalid(
                    "XenonSpatialState.Bundles.Missing",
                    "bundles",
                    "A spatial I/Xe state requires one live bundle for every spatial node.");
            }

            ContractValidationResult<BundleInventory> inventory =
                BundleInventory.TryCreate(model.Topology, bundles);
            if (!inventory.IsValid)
            {
                return inventory;
            }

            if (inventory.Value.OccupiedCount != model.NodeCount)
            {
                return ContractValidationResult<BundleInventory>.Invalid(
                    "XenonSpatialState.Inventory.Incomplete",
                    "bundles",
                    "A spatial I/Xe state requires one live bundle for every spatial node.");
            }

            return inventory;
        }

        private static Digest32 ComputeStateDigest(
            FullCoreDiffusionModelV1 model,
            double simulationTimeSeconds,
            ulong coreStateVersion,
            ReadOnlyCollection<XenonSpatialNodeInputV1> inputs)
        {
            return new Digest32(Phase5CanonicalBytesV1.HashBody(
                "CANDU-XENON-SPATIAL-STATE-V1",
                writer =>
                {
                    Phase5CanonicalBytesV1.WriteUInt32(writer, CurrentSchemaVersion);
                    Phase5CanonicalBytesV1.WriteString(writer, Identity);
                    Phase5CanonicalBytesV1.WriteString(writer, model.DataPack.ModelId);
                    Phase5CanonicalBytesV1.WriteString(
                        writer,
                        model.DataPack.Descriptor.DataPackVersion);
                    Phase5CanonicalBytesV1.WriteDouble(writer, simulationTimeSeconds);
                    Phase5CanonicalBytesV1.WriteUInt64(writer, coreStateVersion);
                    Phase5CanonicalBytesV1.WriteDigest(
                        writer,
                        new Digest32(model.DataPack.Descriptor.TopologyDigest.ToArray()));
                    Phase5CanonicalBytesV1.WriteDigest(
                        writer,
                        new Digest32(model.DataPack.Descriptor.ContentDigest.ToArray()));
                    Phase5CanonicalBytesV1.WriteUInt32(writer, checked((uint)inputs.Count));
                    foreach (XenonSpatialNodeInputV1 input in inputs)
                    {
                        Phase5CanonicalBytesV1.WriteUInt32(
                            writer,
                            input.Node.ChannelId.Value);
                        Phase5CanonicalBytesV1.WriteUInt32(
                            writer,
                            input.Node.Position.Value);
                        Phase5CanonicalBytesV1.WriteBytes(
                            writer,
                            input.State.NuclideStateDigest.ToArray());
                    }
                }));
        }

        private static string ToHex(Digest32 digest)
        {
            var builder = new StringBuilder(digest.Bytes.Count * 2 + 7);
            builder.Append("sha256:");
            foreach (byte value in digest.Bytes)
            {
                builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static bool IsCanonicalTime(double value)
        {
            return IsCanonicalNonnegative(value);
        }

        private static bool IsCanonicalPositive(double value)
        {
            return IsCanonicalNonnegative(value) && value > 0.0;
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

        private static ContractValidationResult<XenonSpatialStateV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<XenonSpatialStateV1>.Invalid(
                code,
                path,
                message);
        }

        private static ContractValidationResult<XenonSpatialAdvanceResultV1> InvalidAdvance(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<XenonSpatialAdvanceResultV1>.Invalid(
                code,
                path,
                message);
        }
    }

    /// <summary>
    /// Immutable result of one all-node I/Xe transition. Both the resulting
    /// state and every accepted node transition are retained for deterministic
    /// replay and test inspection.
    /// </summary>
    public sealed class XenonSpatialAdvanceResultV1
    {
        private readonly ReadOnlyCollection<NuclideTransitionRecordV1> _transitions;

        internal XenonSpatialAdvanceResultV1(
            XenonSpatialStateV1 previousState,
            XenonSpatialStateV1 resultingState,
            double deltaTimeSeconds,
            IEnumerable<NuclideTransitionRecordV1> transitions)
        {
            PreviousState = previousState;
            ResultingState = resultingState;
            DeltaTimeSeconds = deltaTimeSeconds;
            _transitions = new ReadOnlyCollection<NuclideTransitionRecordV1>(
                transitions.ToArray());
        }

        public XenonSpatialStateV1 PreviousState { get; }

        public XenonSpatialStateV1 ResultingState { get; }

        public double DeltaTimeSeconds { get; }

        public IReadOnlyList<NuclideTransitionRecordV1> Transitions
        {
            get { return _transitions; }
        }
    }
}
