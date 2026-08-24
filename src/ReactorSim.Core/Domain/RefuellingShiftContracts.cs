using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ReactorSim.Core
{
    /// <summary>
    /// The immutable result of one validated location-layer refuelling shift.
    /// The result contains the replacement inventory and the ordered bundle
    /// states discharged from the channel. Full lifecycle, I/Xe, command, and
    /// event envelopes remain later task contracts.
    /// </summary>
    public sealed class RefuelShiftResult
    {
        internal RefuelShiftResult(
            BundleInventory sourceInventory,
            BundleInventory resultingInventory,
            RefuelSchemePositionPlan positionPlan,
            ChannelId channelId,
            double effectiveTimeSeconds,
            IEnumerable<BundleState> dischargedBundles)
        {
            SourceInventory = sourceInventory;
            ResultingInventory = resultingInventory;
            PositionPlan = positionPlan;
            ChannelId = channelId;
            EffectiveTimeSeconds = effectiveTimeSeconds;
            DischargedBundles = new ReadOnlyCollection<BundleState>(dischargedBundles.ToArray());
        }

        /// <summary>
        /// The immutable pre-transition inventory used to derive the audit
        /// mapping. It is retained by reference; no live state is mutated.
        /// </summary>
        public BundleInventory SourceInventory { get; }

        public BundleInventory ResultingInventory { get; }

        public RefuelSchemePositionPlan PositionPlan { get; }

        public ChannelId ChannelId { get; }

        public double EffectiveTimeSeconds { get; }

        /// <summary>
        /// Discharged states in ascending physical old-position order.
        /// </summary>
        public IReadOnlyList<BundleState> DischargedBundles { get; }
    }

    /// <summary>
    /// Applies one validated declarative position plan as an atomic immutable
    /// replacement. This is intentionally limited to the fields represented
    /// by the current BundleInventory contract.
    /// </summary>
    public static class RefuelShiftTransition
    {
        public static ContractValidationResult<RefuelShiftResult> TryApply(
            BundleInventory inventory,
            ChannelId channelId,
            RefuelSchemePositionPlan positionPlan,
            IEnumerable<BundleState> insertedBundles,
            double currentTimeSeconds,
            double effectiveTimeSeconds)
        {
            if (inventory == null)
            {
                return Invalid(
                    "RefuelShift.Inventory.Missing",
                    "inventory",
                    "A validated bundle inventory is required.");
            }

            if (positionPlan == null)
            {
                return Invalid(
                    "RefuelShift.PositionPlan.Missing",
                    "position_plan",
                    "A validated refuelling position plan is required.");
            }

            if (insertedBundles == null)
            {
                return Invalid(
                    "RefuelShift.InsertedBundles.Missing",
                    "inserted_bundles",
                    "The ordered inserted bundle collection is required.");
            }

            if (channelId.Value >= inventory.Topology.ChannelCount)
            {
                return Invalid(
                    "RefuelShift.Channel.OutOfRange",
                    "channel_id",
                    "The target channel is outside the validated topology.");
            }

            if (positionPlan.BundlePositionCount != inventory.Topology.BundlePositionCount)
            {
                return Invalid(
                    "RefuelShift.PositionCount.Mismatch",
                    "position_plan.bundle_position_count",
                    "The position plan must match the validated topology position count.");
            }

            ChannelTopology channel = inventory.Topology.GetChannel(channelId);
            if (channel.FlowDirection != positionPlan.FlowDirection)
            {
                return Invalid(
                    "RefuelShift.FlowDirection.Mismatch",
                    ContractValidation.ChannelPath(channelId, ".flow_direction"),
                    "The position plan flow direction must match the target channel.");
            }

            if (!RefuelSchemeDefinition.IsDirectionBindingValid(
                    positionPlan.FlowDirection,
                    positionPlan.ShiftDirection))
            {
                return Invalid(
                    "RefuelShift.Direction.Invalid",
                    "position_plan.shift_direction",
                    "The shift direction must satisfy the approved endpoint binding.");
            }

            if (!ContractValidation.IsFinite(currentTimeSeconds) || currentTimeSeconds < 0)
            {
                return Invalid(
                    "RefuelShift.CurrentTime.Invalid",
                    "current_time_s",
                    "Current time must be finite and nonnegative SI seconds.");
            }

            if (!ContractValidation.IsFinite(effectiveTimeSeconds) || effectiveTimeSeconds < 0)
            {
                return Invalid(
                    "RefuelShift.EffectiveTime.Invalid",
                    "effective_time_s",
                    "Effective time must be finite and nonnegative SI seconds.");
            }

            if (currentTimeSeconds > effectiveTimeSeconds)
            {
                return Invalid(
                    "RefuelShift.EffectiveTime.BeforeCurrent",
                    "effective_time_s",
                    "Effective time may not precede current simulation time.");
            }

            BundleState?[] oldByPosition = new BundleState?[checked((int)positionPlan.BundlePositionCount)];
            for (uint position = 0; position < positionPlan.BundlePositionCount; position++)
            {
                NodeKey node = new NodeKey(channelId, new BundlePosition(position));
                BundleState? bundle = inventory.Get(node);
                if (bundle == null)
                {
                    return Invalid(
                        "RefuelShift.Channel.NotFullyOccupied",
                        ContractValidation.NodePath(node, ".bundle_id"),
                        "The target channel must contain one bundle at every physical position.");
                }

                oldByPosition[(int)position] = bundle;
            }

            BundleState[] inserted = insertedBundles.ToArray();
            if (inserted.Length != positionPlan.ShiftCount)
            {
                return Invalid(
                    "RefuelShift.InsertedBundles.CountMismatch",
                    "inserted_bundles",
                    "The inserted bundle count must equal the position plan shift count.");
            }

            var knownBundleIds = new HashSet<StableId>();
            foreach (BundleState existing in inventory.EnumerateOccupied())
            {
                knownBundleIds.Add(existing.BundleId);
            }

            var insertedIds = new HashSet<StableId>();
            for (int index = 0; index < inserted.Length; index++)
            {
                BundleState bundle = inserted[index];
                string path = "inserted_bundles[" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]";
                if (bundle == null)
                {
                    return Invalid(
                        "RefuelShift.InsertedBundle.Null",
                        path,
                        "An inserted bundle state may not be null.");
                }

                BundlePosition expectedPosition = positionPlan.InsertedPositions[index];
                if (bundle.ChannelId != channelId)
                {
                    return Invalid(
                        "RefuelShift.InsertedBundle.ChannelMismatch",
                        path + ".channel_id",
                        "An inserted bundle must target the command channel.");
                }

                if (bundle.Position != expectedPosition)
                {
                    return Invalid(
                        "RefuelShift.InsertedBundle.PositionMismatch",
                        path + ".position",
                        "Inserted bundles must be ordered by their exact physical destination positions.");
                }

                if (bundle.BundleId.IsEmpty)
                {
                    return Invalid(
                        "RefuelShift.InsertedBundle.BundleId.Empty",
                        path + ".bundle_id",
                        "Every inserted bundle requires an explicit stable identity.");
                }

                if (!insertedIds.Add(bundle.BundleId))
                {
                    return Invalid(
                        "RefuelShift.InsertedBundle.BundleId.Duplicate",
                        path + ".bundle_id",
                        "Inserted bundle identities must be unique within the shift.");
                }

                if (knownBundleIds.Contains(bundle.BundleId))
                {
                    return Invalid(
                        "RefuelShift.InsertedBundle.BundleId.AlreadyLive",
                        path + ".bundle_id",
                        "An inserted bundle identity may not already exist in the live inventory.");
                }

                if (string.IsNullOrWhiteSpace(bundle.MaterialVariantId.Value))
                {
                    return Invalid(
                        "RefuelShift.InsertedBundle.MaterialVariant.Empty",
                        path + ".material_variant_id",
                        "Every inserted bundle requires an explicit material variant identity.");
                }

                if (!ContractValidation.IsFinite(bundle.InitialBurnupJPerKgHm) || bundle.InitialBurnupJPerKgHm < 0)
                {
                    return Invalid(
                        "RefuelShift.InsertedBundle.InitialBurnup.Invalid",
                        path + ".initial_burnup_j_per_kg_hm",
                        "Initial burnup must be finite and nonnegative SI J/kg_HM.");
                }

                if (!ContractValidation.IsFinite(bundle.CumulativeFissionEnergyJ) ||
                    bundle.CumulativeFissionEnergyJ != 0)
                {
                    return Invalid(
                        "RefuelShift.InsertedBundle.Energy.NonZero",
                        path + ".cumulative_fission_energy_j",
                        "A fresh inserted bundle must have exactly zero cumulative fission energy.");
                }

                if (!ContractValidation.IsFinite(bundle.HeavyMetalMassKg) || bundle.HeavyMetalMassKg <= 0)
                {
                    return Invalid(
                        "RefuelShift.InsertedBundle.Mass.Invalid",
                        path + ".heavy_metal_mass_kg",
                        "Heavy-metal mass must be finite and strictly positive SI kilograms.");
                }

                if (!ContractValidation.IsFinite(bundle.InsertedAtSeconds) || bundle.InsertedAtSeconds < 0)
                {
                    return Invalid(
                        "RefuelShift.InsertedBundle.InsertedAt.Invalid",
                        path + ".inserted_at_s",
                        "InsertedAt must be finite and nonnegative SI seconds.");
                }

                if (bundle.InsertedAtSeconds != effectiveTimeSeconds)
                {
                    return Invalid(
                        "RefuelShift.InsertedBundle.InsertedAt.Mismatch",
                        path + ".inserted_at_s",
                        "A fresh inserted bundle must use the exact effective time.");
                }
            }

            var candidateBundles = new List<BundleState>(inventory.OccupiedCount);
            foreach (BundleState existing in inventory.EnumerateOccupied())
            {
                if (existing.ChannelId != channelId)
                {
                    candidateBundles.Add(existing);
                }
            }

            var dischargedBundles = new List<BundleState>(positionPlan.ShiftCount);
            for (int index = 0; index < positionPlan.ShiftCount; index++)
            {
                BundlePosition dischargedPosition = positionPlan.DischargedPositions[index];
                BundleState discharged = oldByPosition[checked((int)dischargedPosition.Value)]!;
                dischargedBundles.Add(discharged);
            }

            for (int index = 0; index < inserted.Length; index++)
            {
                candidateBundles.Add(inserted[index]);
            }

            int positionCount = checked((int)positionPlan.BundlePositionCount);
            int shiftCount = positionPlan.ShiftCount;
            if (positionPlan.ShiftDirection == RefuelShiftDirection.TowardEndB)
            {
                for (int source = 0; source < positionCount - shiftCount; source++)
                {
                    BundleState retained = oldByPosition[source]!;
                    candidateBundles.Add(retained.WithLocation(
                        channelId,
                        new BundlePosition(checked((uint)(source + shiftCount)))));
                }
            }
            else
            {
                for (int source = shiftCount; source < positionCount; source++)
                {
                    BundleState retained = oldByPosition[source]!;
                    candidateBundles.Add(retained.WithLocation(
                        channelId,
                        new BundlePosition(checked((uint)(source - shiftCount)))));
                }
            }

            ContractValidationResult<BundleInventory> inventoryResult = BundleInventory.TryCreate(
                inventory.Topology,
                candidateBundles);
            if (!inventoryResult.IsValid)
            {
                ContractDiagnostic diagnostic = inventoryResult.FirstDiagnostic;
                return Invalid(
                    "RefuelShift.Postcondition.InventoryInvalid",
                    diagnostic.Path,
                    diagnostic.Message);
            }

            BundleInventory resultingInventory = inventoryResult.Value;
            for (int index = 0; index < inserted.Length; index++)
            {
                BundleState? resulting = resultingInventory.Get(
                    new NodeKey(channelId, positionPlan.InsertedPositions[index]));
                if (resulting == null || resulting.BundleId != inserted[index].BundleId)
                {
                    return Invalid(
                        "RefuelShift.Postcondition.InsertedMappingMismatch",
                        "inserted_bundles[" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]",
                        "The committed inventory did not retain the canonical inserted mapping.");
                }
            }

            for (int index = 0; index < dischargedBundles.Count; index++)
            {
                if (resultingInventory.TryFind(dischargedBundles[index].BundleId, out _))
                {
                    return Invalid(
                        "RefuelShift.Postcondition.DischargedStillLive",
                        "discharged_bundles[" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "].bundle_id",
                        "A discharged bundle identity may not remain in the resulting live inventory.");
                }
            }

            return ContractValidationResult<RefuelShiftResult>.Valid(
                new RefuelShiftResult(
                    inventory,
                    resultingInventory,
                    positionPlan,
                    channelId,
                    effectiveTimeSeconds,
                    dischargedBundles));
        }

        private static ContractValidationResult<RefuelShiftResult> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<RefuelShiftResult>.Invalid(code, path, message);
        }
    }
}
