using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ReactorSim.Core
{
    /// <summary>
    /// The immutable identity/location portion of one bundle row. This task
    /// stores the approved SI fields but does not perform depletion or movement.
    /// </summary>
    public sealed class BundleState
    {
        public BundleState(
            StableId bundleId,
            ChannelId channelId,
            BundlePosition position,
            MaterialVariantId materialVariantId,
            double initialBurnupJPerKgHm,
            double cumulativeFissionEnergyJ,
            double heavyMetalMassKg,
            double insertedAtSeconds)
            : this(
                bundleId,
                channelId,
                position,
                materialVariantId,
                initialBurnupJPerKgHm,
                cumulativeFissionEnergyJ,
                heavyMetalMassKg,
                insertedAtSeconds,
                null,
                OptionalPowerWattsV1.NotApplicable,
                OptionalStableId.NotApplicable,
                Array.Empty<PowerHistoryRecordV1>(),
                null,
                0)
        {
        }

        public BundleState(
            StableId bundleId,
            ChannelId channelId,
            BundlePosition position,
            MaterialVariantId materialVariantId,
            double initialBurnupJPerKgHm,
            double cumulativeFissionEnergyJ,
            double heavyMetalMassKg,
            double insertedAtSeconds,
            NuclideStateEnvelopeV1? nuclideState,
            OptionalPowerWattsV1? powerWatts,
            OptionalStableId? powerSnapshotId,
            IEnumerable<PowerHistoryRecordV1>? powerHistory,
            BundleCoefficientBindingV1? coefficientBinding,
            ulong stateVersion)
        {
            BundleId = bundleId;
            ChannelId = channelId;
            Position = position;
            MaterialVariantId = materialVariantId;
            InitialBurnupJPerKgHm = initialBurnupJPerKgHm;
            CumulativeFissionEnergyJ = cumulativeFissionEnergyJ;
            HeavyMetalMassKg = heavyMetalMassKg;
            InsertedAtSeconds = insertedAtSeconds;
            NuclideState = nuclideState;
            PowerWatts = powerWatts ?? OptionalPowerWattsV1.NotApplicable;
            PowerSnapshotId = powerSnapshotId ?? OptionalStableId.NotApplicable;
            _powerHistory = new ReadOnlyCollection<PowerHistoryRecordV1>(
                (powerHistory ?? Array.Empty<PowerHistoryRecordV1>()).ToArray());
            CoefficientBinding = coefficientBinding;
            StateVersion = stateVersion;
        }

        private readonly ReadOnlyCollection<PowerHistoryRecordV1> _powerHistory;

        public StableId BundleId { get; }

        public ChannelId ChannelId { get; }

        public BundlePosition Position { get; }

        public MaterialVariantId MaterialVariantId { get; }

        public double InitialBurnupJPerKgHm { get; }

        public double CumulativeFissionEnergyJ { get; }

        public double HeavyMetalMassKg { get; }

        public double InsertedAtSeconds { get; }

        /// <summary>
        /// Complete P2-T04 I/Xe ownership. Legacy basic fixtures may leave it
        /// absent; every P5-T09 complete-state transition requires it.
        /// </summary>
        public NuclideStateEnvelopeV1? NuclideState { get; }

        public OptionalPowerWattsV1 PowerWatts { get; }

        public OptionalStableId PowerSnapshotId { get; }

        public IReadOnlyList<PowerHistoryRecordV1> PowerHistory
        {
            get { return _powerHistory; }
        }

        public BundleCoefficientBindingV1? CoefficientBinding { get; }

        public ulong StateVersion { get; }

        /// <summary>
        /// The current burnup derived from the immutable initial burnup,
        /// cumulative fission energy, and heavy-metal mass fields. The
        /// owning inventory transition validates these fields before this
        /// value is used as authoritative derived state.
        /// </summary>
        public double CurrentBurnupJPerKgHm
        {
            get { return InitialBurnupJPerKgHm + CumulativeFissionEnergyJ / HeavyMetalMassKg; }
        }

        public NodeKey Node
        {
            get { return new NodeKey(ChannelId, Position); }
        }

        /// <summary>
        /// Creates the immutable location update used when a retained bundle
        /// crosses positions during an atomic refuelling shift. Every stored
        /// identity and basic state field is preserved exactly.
        /// </summary>
        public BundleState WithLocation(ChannelId channelId, BundlePosition position)
        {
            return new BundleState(
                BundleId,
                channelId,
                position,
                MaterialVariantId,
                InitialBurnupJPerKgHm,
                CumulativeFissionEnergyJ,
                HeavyMetalMassKg,
                InsertedAtSeconds,
                NuclideState,
                PowerWatts,
                PowerSnapshotId,
                PowerHistory,
                CoefficientBinding,
                StateVersion);
        }

        /// <summary>
        /// Creates a retained-bundle location update and rebinds the derived
        /// I/Xe densities to the explicit destination node volume.
        /// </summary>
        public ContractValidationResult<BundleState> TryWithLocationAndNodeVolume(
            ChannelId channelId,
            BundlePosition position,
            double nodeVolumeM3)
        {
            if (NuclideState == null)
            {
                return ContractValidationResult<BundleState>.Valid(
                    WithLocation(channelId, position));
            }

            ContractValidationResult<NuclideStateEnvelopeV1> moved =
                NuclideState.TryMoveToVolume(nodeVolumeM3);
            if (!moved.IsValid)
            {
                return ContractValidationResult<BundleState>.Invalid(
                    moved.FirstDiagnostic.Code,
                    moved.FirstDiagnostic.Path,
                    moved.FirstDiagnostic.Message);
            }

            return ContractValidationResult<BundleState>.Valid(
                new BundleState(
                    BundleId,
                    channelId,
                    position,
                    MaterialVariantId,
                    InitialBurnupJPerKgHm,
                    CumulativeFissionEnergyJ,
                    HeavyMetalMassKg,
                    InsertedAtSeconds,
                    moved.Value,
                    PowerWatts,
                    PowerSnapshotId,
                    PowerHistory,
                    CoefficientBinding,
                    StateVersion));
        }

        /// <summary>
        /// Creates the immutable energy update used by the legacy explicit
        /// burnup interval. A new energy state invalidates the current power
        /// snapshot and coefficient binding; append-only power history is
        /// retained for replay and discharge evidence.
        /// </summary>
        public BundleState WithEnergy(double cumulativeFissionEnergyJ)
        {
            return new BundleState(
                BundleId,
                ChannelId,
                Position,
                MaterialVariantId,
                InitialBurnupJPerKgHm,
                cumulativeFissionEnergyJ,
                HeavyMetalMassKg,
                InsertedAtSeconds,
                NuclideState,
                OptionalPowerWattsV1.NotApplicable,
                OptionalStableId.NotApplicable,
                PowerHistory,
                null,
                StateVersion);
        }

        /// <summary>
        /// Creates the complete-state burnup result after the new burnup has
        /// been looked up in the versioned coefficient table. The current
        /// power snapshot is invalidated while the newly selected
        /// coefficient identity is retained for the next spatial solve.
        /// </summary>
        public BundleState WithEnergyAndCoefficientBinding(
            double cumulativeFissionEnergyJ,
            BundleCoefficientBindingV1 coefficientBinding)
        {
            if (coefficientBinding == null)
            {
                throw new ArgumentNullException(nameof(coefficientBinding));
            }

            return new BundleState(
                BundleId,
                ChannelId,
                Position,
                MaterialVariantId,
                InitialBurnupJPerKgHm,
                cumulativeFissionEnergyJ,
                HeavyMetalMassKg,
                InsertedAtSeconds,
                NuclideState,
                OptionalPowerWattsV1.NotApplicable,
                OptionalStableId.NotApplicable,
                PowerHistory,
                coefficientBinding,
                StateVersion);
        }

        /// <summary>
        /// Retains a post-transition coefficient lookup while keeping the
        /// current power snapshot explicitly not applicable. This is used by
        /// the strict complete refuelling boundary after the destination
        /// locations and node volumes have been validated.
        /// </summary>
        public BundleState WithCoefficientBinding(BundleCoefficientBindingV1 coefficientBinding)
        {
            if (coefficientBinding == null)
            {
                throw new ArgumentNullException(nameof(coefficientBinding));
            }

            return new BundleState(
                BundleId,
                ChannelId,
                Position,
                MaterialVariantId,
                InitialBurnupJPerKgHm,
                CumulativeFissionEnergyJ,
                HeavyMetalMassKg,
                InsertedAtSeconds,
                NuclideState,
                OptionalPowerWattsV1.NotApplicable,
                OptionalStableId.NotApplicable,
                PowerHistory,
                coefficientBinding,
                StateVersion);
        }

        /// <summary>
        /// Adds one accepted P2-T02 power record while retaining all other
        /// authoritative bundle state.
        /// </summary>
        public ContractValidationResult<BundleState> TryWithAcceptedPowerSnapshot(
            StableId powerSnapshotId,
            PowerHistoryRecordV1 historyRecord,
            BundleCoefficientBindingV1 coefficientBinding)
        {
            if (powerSnapshotId.IsEmpty || historyRecord == null || coefficientBinding == null)
            {
                return ContractValidationResult<BundleState>.Invalid(
                    "BundleState.PowerSnapshot.Binding.Missing",
                    "power_snapshot",
                    "An accepted bundle power requires snapshot, history, and coefficient identities.");
            }

            ContractValidationResult<OptionalPowerWattsV1> power =
                OptionalPowerWattsV1.TryApplicable(historyRecord.PowerWatts);
            if (!power.IsValid)
            {
                return ContractValidationResult<BundleState>.Invalid(
                    power.FirstDiagnostic.Code,
                    power.FirstDiagnostic.Path,
                    power.FirstDiagnostic.Message);
            }

            var history = PowerHistory.ToList();
            if (history.Any(record => record == null) ||
                (history.Count > 0 && ComparePowerHistory(history[history.Count - 1], historyRecord) >= 0))
            {
                return ContractValidationResult<BundleState>.Invalid(
                    "BundleState.PowerHistory.Order.Invalid",
                    "power_history",
                    "Accepted power history must be append-only and strictly canonical.");
            }

            history.Add(historyRecord);
            return ContractValidationResult<BundleState>.Valid(
                new BundleState(
                    BundleId,
                    ChannelId,
                    Position,
                    MaterialVariantId,
                    InitialBurnupJPerKgHm,
                    CumulativeFissionEnergyJ,
                    HeavyMetalMassKg,
                    InsertedAtSeconds,
                    NuclideState,
                    power.Value,
                    OptionalStableId.Applicable(powerSnapshotId),
                    history,
                    coefficientBinding,
                    StateVersion));
        }

        public BundleState WithNuclideState(NuclideStateEnvelopeV1 nuclideState)
        {
            return new BundleState(
                BundleId,
                ChannelId,
                Position,
                MaterialVariantId,
                InitialBurnupJPerKgHm,
                CumulativeFissionEnergyJ,
                HeavyMetalMassKg,
                InsertedAtSeconds,
                nuclideState,
                PowerWatts,
                PowerSnapshotId,
                PowerHistory,
                CoefficientBinding,
                StateVersion);
        }

        /// <summary>
        /// Invalidates the current accepted-power binding after a state or
        /// location commit. Append-only power history remains retained for
        /// discharge and replay evidence; a new accepted snapshot must bind
        /// the next usable power value.
        /// </summary>
        public BundleState WithPowerBindingInvalidated()
        {
            return new BundleState(
                BundleId,
                ChannelId,
                Position,
                MaterialVariantId,
                InitialBurnupJPerKgHm,
                CumulativeFissionEnergyJ,
                HeavyMetalMassKg,
                InsertedAtSeconds,
                NuclideState,
                OptionalPowerWattsV1.NotApplicable,
                OptionalStableId.NotApplicable,
                PowerHistory,
                null,
                StateVersion);
        }

        private static int ComparePowerHistory(
            PowerHistoryRecordV1 left,
            PowerHistoryRecordV1 right)
        {
            int result = left.SnapshotTimeSeconds.CompareTo(right.SnapshotTimeSeconds);
            if (result != 0) return result;
            result = left.CoreStateVersion.CompareTo(right.CoreStateVersion);
            if (result != 0) return result;
            result = left.SpatialStateVersion.CompareTo(right.SpatialStateVersion);
            return result != 0 ? result : left.PowerSnapshotVersion.CompareTo(right.PowerSnapshotVersion);
        }
    }

    /// <summary>
    /// Flat, immutable bundle storage. The flat index is only a storage
    /// address; identity and topology remain explicit in every BundleState.
    /// </summary>
    public sealed class BundleInventory
    {
        private readonly ReadOnlyCollection<BundleState?> _slots;

        private BundleInventory(CoreTopology topology, BundleState?[] slots, int occupiedCount)
        {
            Topology = topology;
            _slots = new ReadOnlyCollection<BundleState?>(slots);
            OccupiedCount = occupiedCount;
        }

        public CoreTopology Topology { get; }

        public int SlotCount
        {
            get { return _slots.Count; }
        }

        public int OccupiedCount { get; }

        public IReadOnlyList<BundleState?> Slots
        {
            get { return _slots; }
        }

        public static ContractValidationResult<BundleInventory> TryCreate(
            CoreTopology topology,
            IEnumerable<BundleState> bundles)
        {
            if (topology == null)
            {
                return ContractValidationResult<BundleInventory>.Invalid(
                    "BundleInventory.Topology.Missing",
                    "topology",
                    "A validated topology is required.");
            }

            if (bundles == null)
            {
                return ContractValidationResult<BundleInventory>.Invalid(
                    "BundleInventory.Bundles.Missing",
                    "bundles",
                    "The bundle collection is required.");
            }

            var records = bundles.ToList();
            if (records.Count > topology.SlotCount)
            {
                return ContractValidationResult<BundleInventory>.Invalid(
                    "BundleInventory.Capacity.Exceeded",
                    "bundles",
                    "The number of bundles may not exceed the validated slot count.");
            }

            BundleState[] canonical = records
                .OrderBy(bundle => bundle == null ? StableId.Empty : bundle.BundleId)
                .ThenBy(bundle => bundle == null ? uint.MaxValue : bundle.ChannelId.Value)
                .ThenBy(bundle => bundle == null ? uint.MaxValue : bundle.Position.Value)
                .ToArray();

            for (int i = 0; i < canonical.Length; i++)
            {
                BundleState bundle = canonical[i];
                if (bundle == null)
                {
                    return ContractValidationResult<BundleInventory>.Invalid(
                        "BundleInventory.Bundle.Null",
                        "bundles",
                        "A bundle record may not be null.");
                }

                if (bundle.BundleId.IsEmpty)
                {
                    return ContractValidationResult<BundleInventory>.Invalid(
                        "BundleInventory.BundleId.Empty",
                        "bundles[bundle_id=00000000-0000-0000-0000-000000000000]",
                        "Every retained bundle requires an explicit stable identity.");
                }

                if (!topology.TryGetFlatIndex(bundle.Node, out _))
                {
                    return ContractValidationResult<BundleInventory>.Invalid(
                        "BundleInventory.Location.OutOfRange",
                        "bundle[" + bundle.BundleId + "].location",
                        "The bundle location is outside the validated topology.");
                }

                if (string.IsNullOrWhiteSpace(bundle.MaterialVariantId.Value))
                {
                    return ContractValidationResult<BundleInventory>.Invalid(
                        "BundleInventory.MaterialVariant.Empty",
                        "bundle[" + bundle.BundleId + "].material_variant_id",
                        "Every bundle requires an explicit material variant identity.");
                }

                if (!ContractValidation.IsFinite(bundle.InitialBurnupJPerKgHm) || bundle.InitialBurnupJPerKgHm < 0)
                {
                    return ContractValidationResult<BundleInventory>.Invalid(
                        "BundleInventory.InitialBurnup.Invalid",
                        "bundle[" + bundle.BundleId + "].initial_burnup_j_per_kg_hm",
                        "Initial burnup must be finite and nonnegative SI J/kg_HM.");
                }

                if (!ContractValidation.IsFinite(bundle.CumulativeFissionEnergyJ) || bundle.CumulativeFissionEnergyJ < 0)
                {
                    return ContractValidationResult<BundleInventory>.Invalid(
                        "BundleInventory.Energy.Invalid",
                        "bundle[" + bundle.BundleId + "].cumulative_fission_energy_j",
                        "Cumulative energy must be finite and nonnegative SI joules.");
                }

                if (!ContractValidation.IsFinite(bundle.HeavyMetalMassKg) || bundle.HeavyMetalMassKg <= 0)
                {
                    return ContractValidationResult<BundleInventory>.Invalid(
                        "BundleInventory.Mass.Invalid",
                        "bundle[" + bundle.BundleId + "].heavy_metal_mass_kg",
                        "Heavy-metal mass must be finite and strictly positive SI kilograms.");
                }

                double derivedBurnup = bundle.InitialBurnupJPerKgHm +
                    bundle.CumulativeFissionEnergyJ / bundle.HeavyMetalMassKg;
                if (!ContractValidation.IsFinite(derivedBurnup) || derivedBurnup < 0)
                {
                    return ContractValidationResult<BundleInventory>.Invalid(
                        "BundleInventory.Burnup.Invalid",
                        "bundle[" + bundle.BundleId + "].burnup_j_per_kg_hm",
                        "Derived burnup must be finite and nonnegative SI J/kg_HM.");
                }

                if (!ContractValidation.IsFinite(bundle.InsertedAtSeconds) || bundle.InsertedAtSeconds < 0)
                {
                    return ContractValidationResult<BundleInventory>.Invalid(
                        "BundleInventory.InsertedAt.Invalid",
                        "bundle[" + bundle.BundleId + "].inserted_at_s",
                        "InsertedAt must be finite and nonnegative SI seconds.");
                }

                if (bundle.NuclideState != null)
                {
                    if (bundle.NuclideState.BundleId != bundle.BundleId ||
                        bundle.NuclideState.Data.MaterialVariantId != bundle.MaterialVariantId)
                    {
                        return ContractValidationResult<BundleInventory>.Invalid(
                            "BundleInventory.NuclideState.BindingMismatch",
                            "bundle[" + bundle.BundleId + "].nuclide_state",
                            "A complete nuclide envelope must bind the same bundle and material identity.");
                    }
                }

                if (bundle.PowerWatts == null || bundle.PowerSnapshotId == null || bundle.PowerHistory == null)
                {
                    return ContractValidationResult<BundleInventory>.Invalid(
                        "BundleInventory.PowerState.Missing",
                        "bundle[" + bundle.BundleId + "].power_state",
                        "Power applicability and history wrappers are required.");
                }

                if (bundle.PowerWatts.IsApplicable != bundle.PowerSnapshotId.IsApplicable)
                {
                    return ContractValidationResult<BundleInventory>.Invalid(
                        "BundleInventory.PowerState.BindingMismatch",
                        "bundle[" + bundle.BundleId + "].power_state",
                        "Current power and PowerSnapshotId must use the same applicability state.");
                }

                PowerHistoryRecordV1[] powerHistory = bundle.PowerHistory.ToArray();
                for (int historyIndex = 0; historyIndex < powerHistory.Length; historyIndex++)
                {
                    if (powerHistory[historyIndex] == null ||
                        (historyIndex > 0 && ComparePowerHistory(
                            powerHistory[historyIndex - 1],
                            powerHistory[historyIndex]) >= 0))
                    {
                        return ContractValidationResult<BundleInventory>.Invalid(
                            "BundleInventory.PowerHistory.Order.Invalid",
                            "bundle[" + bundle.BundleId + "].power_history",
                            "Power history must be append-only and strictly canonical.");
                    }
                }
            }

            for (int i = 1; i < canonical.Length; i++)
            {
                BundleState previous = canonical[i - 1];
                BundleState current = canonical[i];
                if (previous.BundleId == current.BundleId)
                {
                    return ContractValidationResult<BundleInventory>.Invalid(
                        "BundleInventory.BundleId.Duplicate",
                        "bundle[" + current.BundleId + "].bundle_id",
                        "Stable bundle identities must be unique.");
                }
            }

            BundleState[] byNode = canonical
                .OrderBy(bundle => bundle.ChannelId.Value)
                .ThenBy(bundle => bundle.Position.Value)
                .ThenBy(bundle => bundle.BundleId)
                .ToArray();
            for (int i = 1; i < byNode.Length; i++)
            {
                BundleState previous = byNode[i - 1];
                BundleState current = byNode[i];
                if (previous.Node == current.Node)
                {
                    return ContractValidationResult<BundleInventory>.Invalid(
                        "BundleInventory.Location.Duplicate",
                        ContractValidation.NodePath(current.Node, ".bundle_id"),
                        "A slot may contain at most one bundle state.");
                }
            }

            var slots = new BundleState?[topology.SlotCount];
            foreach (BundleState bundle in canonical)
            {
                int index = topology.GetFlatIndex(bundle.Node);
                slots[index] = bundle;
            }

            return ContractValidationResult<BundleInventory>.Valid(
                new BundleInventory(topology, slots, canonical.Length));
        }

        private static int ComparePowerHistory(
            PowerHistoryRecordV1 left,
            PowerHistoryRecordV1 right)
        {
            int result = left.SnapshotTimeSeconds.CompareTo(right.SnapshotTimeSeconds);
            if (result != 0) return result;
            result = left.CoreStateVersion.CompareTo(right.CoreStateVersion);
            if (result != 0) return result;
            result = left.SpatialStateVersion.CompareTo(right.SpatialStateVersion);
            return result != 0 ? result : left.PowerSnapshotVersion.CompareTo(right.PowerSnapshotVersion);
        }

        public BundleState? Get(NodeKey node)
        {
            return _slots[Topology.GetFlatIndex(node)];
        }

        public bool TryGet(NodeKey node, out BundleState? bundle)
        {
            int index;
            if (!Topology.TryGetFlatIndex(node, out index))
            {
                bundle = null;
                return false;
            }

            bundle = _slots[index];
            return true;
        }

        public bool TryFind(StableId bundleId, out BundleState? bundle)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                BundleState? candidate = _slots[i];
                if (candidate != null && candidate.BundleId == bundleId)
                {
                    bundle = candidate;
                    return true;
                }
            }

            bundle = null;
            return false;
        }

        public BundleState?[] ToFlatArray()
        {
            return _slots.ToArray();
        }

        public IEnumerable<BundleState> EnumerateOccupied()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                BundleState? bundle = _slots[i];
                if (bundle != null)
                {
                    yield return bundle;
                }
            }
        }
    }
}
