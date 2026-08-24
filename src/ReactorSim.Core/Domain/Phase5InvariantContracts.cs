using System;
using System.Collections.Generic;
using System.Linq;

namespace ReactorSim.Core
{
    /// <summary>
    /// The bounded Phase 5 invariant report scope.
    /// </summary>
    public enum Phase5InvariantScopeV1 : byte
    {
        Inventory = 0,
        BurnupInterval = 1,
        RefuelShift = 2
    }

    /// <summary>
    /// Exact invariant observations returned after a complete bounded check.
    /// Numeric equality checks use the approved binary64 values without a
    /// hidden tolerance.
    /// </summary>
    public sealed class Phase5InvariantReportV1
    {
        public const uint CurrentSchemaVersion = 1;

        internal Phase5InvariantReportV1(
            Phase5InvariantScopeV1 scope,
            int expectedBundleCount,
            int actualBundleCount,
            int uniqueBundleIdCount,
            int uniqueLocationCount,
            int nonnegativeBurnupCount,
            int monotonicBurnupCount,
            int energyRecordCount,
            int preservedBundleCount,
            int insertedBundleCount,
            int dischargedBundleCount,
            double totalCumulativeFissionEnergyJ,
            double totalIntervalEnergyJ)
        {
            SchemaVersion = CurrentSchemaVersion;
            Scope = scope;
            ExpectedBundleCount = expectedBundleCount;
            ActualBundleCount = actualBundleCount;
            UniqueBundleIdCount = uniqueBundleIdCount;
            UniqueLocationCount = uniqueLocationCount;
            NonnegativeBurnupCount = nonnegativeBurnupCount;
            MonotonicBurnupCount = monotonicBurnupCount;
            EnergyRecordCount = energyRecordCount;
            PreservedBundleCount = preservedBundleCount;
            InsertedBundleCount = insertedBundleCount;
            DischargedBundleCount = dischargedBundleCount;
            TotalCumulativeFissionEnergyJ = totalCumulativeFissionEnergyJ;
            TotalIntervalEnergyJ = totalIntervalEnergyJ;
        }

        public uint SchemaVersion { get; }

        public Phase5InvariantScopeV1 Scope { get; }

        public int ExpectedBundleCount { get; }

        public int ActualBundleCount { get; }

        public int UniqueBundleIdCount { get; }

        public int UniqueLocationCount { get; }

        public int NonnegativeBurnupCount { get; }

        public int MonotonicBurnupCount { get; }

        public int EnergyRecordCount { get; }

        public int PreservedBundleCount { get; }

        public int InsertedBundleCount { get; }

        public int DischargedBundleCount { get; }

        public double TotalCumulativeFissionEnergyJ { get; }

        public double TotalIntervalEnergyJ { get; }
    }

    /// <summary>
    /// Validates the exact Phase 5 identity, location, burnup, and energy
    /// invariants over the state transitions represented by current Core.
    /// The expected bundle count is explicit; no production or synthetic count
    /// is inferred from an array or a hidden default.
    /// </summary>
    public static class Phase5InvariantValidatorV1
    {
        public static ContractValidationResult<Phase5InvariantReportV1> TryValidateInventory(
            BundleInventory inventory,
            int expectedBundleCount)
        {
            ContractValidationResult<InventorySnapshot> snapshot =
                TryReadInventory(inventory, expectedBundleCount);
            if (!snapshot.IsValid)
            {
                return Invalid(snapshot.FirstDiagnostic);
            }

            InventorySnapshot value = snapshot.Value;
            return ContractValidationResult<Phase5InvariantReportV1>.Valid(
                CreateReport(
                    Phase5InvariantScopeV1.Inventory,
                    value,
                    0,
                    0,
                    0,
                    0.0));
        }

        public static ContractValidationResult<Phase5InvariantReportV1> TryValidateBurnupInterval(
            BurnupIntervalResultV1 interval,
            int expectedBundleCount)
        {
            if (interval == null)
            {
                return Invalid(
                    "Phase5Invariant.BurnupInterval.Missing",
                    "burnup_interval",
                    "A completed burnup interval result is required.");
            }

            ContractValidationResult<InventorySnapshot> source =
                TryReadInventory(interval.SourceInventory, expectedBundleCount);
            if (!source.IsValid)
            {
                return Invalid(source.FirstDiagnostic);
            }

            ContractValidationResult<InventorySnapshot> result =
                TryReadInventory(interval.ResultingInventory, expectedBundleCount);
            if (!result.IsValid)
            {
                return Invalid(result.FirstDiagnostic);
            }

            InventorySnapshot sourceValue = source.Value;
            InventorySnapshot resultValue = result.Value;
            if (!ReferenceEquals(
                    sourceValue.Inventory.Topology,
                    resultValue.Inventory.Topology))
            {
                return Invalid(
                    "Phase5Invariant.BurnupInterval.Topology.IdentityMismatch",
                    "burnup_interval.resulting_inventory.topology",
                    "A burnup interval must preserve the exact source topology instance.");
            }

            ContractValidationResult<bool> metadata = ValidateBurnupIntervalMetadata(interval);
            if (!metadata.IsValid)
            {
                return Invalid(metadata.FirstDiagnostic);
            }

            if (interval.Records == null ||
                interval.Records.Count != sourceValue.Bundles.Length)
            {
                return Invalid(
                    "Phase5Invariant.BurnupInterval.RecordCount.Mismatch",
                    "burnup_interval.records",
                    "A burnup interval requires exactly one canonical record per live bundle.");
            }

            Dictionary<StableId, BundlePowerSampleV1> powers =
                new Dictionary<StableId, BundlePowerSampleV1>();
            foreach (BundlePowerSampleV1 sample in interval.PowerSnapshot.BundlePowers)
            {
                if (sample == null || !powers.TryAdd(sample.BundleId, sample))
                {
                    return Invalid(
                        "Phase5Invariant.BurnupInterval.PowerSample.Duplicate",
                        "burnup_interval.power_snapshot.bundle_powers",
                        "Power samples must contain one unique identity per live bundle.");
                }
            }

            if (powers.Count != sourceValue.Bundles.Length)
            {
                return Invalid(
                    "Phase5Invariant.BurnupInterval.PowerSample.CountMismatch",
                    "burnup_interval.power_snapshot.bundle_powers",
                    "Power samples must contain exactly one entry per live bundle.");
            }

            double totalIntervalEnergy = 0.0;
            int monotonicCount = 0;
            for (int index = 0; index < sourceValue.Bundles.Length; index++)
            {
                BundleState before = sourceValue.Bundles[index];
                BundleState? after = resultValue.ById.TryGetValue(before.BundleId, out BundleState? found)
                    ? found
                    : null;
                if (after == null)
                {
                    return Invalid(
                        "Phase5Invariant.BurnupInterval.BundleId.Missing",
                        "burnup_interval.resulting_inventory",
                        "Every source bundle identity must remain present during a burnup interval.");
                }

                if (!SameIdentityAndHistory(before, after) ||
                    before.Node != after.Node)
                {
                    return Invalid(
                        "Phase5Invariant.BurnupInterval.IdentityOrLocation.Changed",
                        ContractValidation.NodePath(before.Node, ".bundle_id"),
                        "A burnup interval may change only cumulative energy and derived burnup.");
                }

                BurnupIntervalRecordV1 record = interval.Records[index];
                if (record == null || record.BundleId != before.BundleId ||
                    record.ChannelId != before.ChannelId || record.Position != before.Position)
                {
                    return Invalid(
                        "Phase5Invariant.BurnupInterval.RecordOrder.Invalid",
                        "burnup_interval.records[" + index + "]",
                        "Burnup records must use canonical bundle order and identity/location binding.");
                }

                BundlePowerSampleV1 sample;
                if (!powers.TryGetValue(before.BundleId, out sample!))
                {
                    return Invalid(
                        "Phase5Invariant.BurnupInterval.PowerSample.Missing",
                        ContractValidation.NodePath(before.Node, ".power_w"),
                        "Every live bundle requires one bound accepted power sample.");
                }

                if (sample.Node != before.Node)
                {
                    return Invalid(
                        "Phase5Invariant.BurnupInterval.PowerSample.LocationMismatch",
                        ContractValidation.NodePath(before.Node, ".power_w"),
                        "The accepted power sample must bind the bundle's explicit node.");
                }

                if (record.DeltaTimeSeconds != interval.DeltaTimeSeconds ||
                    record.PowerWatts != sample.PowerWatts ||
                    record.OldCumulativeFissionEnergyJ != before.CumulativeFissionEnergyJ ||
                    record.OldBurnupJPerKgHm != before.CurrentBurnupJPerKgHm ||
                    record.NewCumulativeFissionEnergyJ != after.CumulativeFissionEnergyJ ||
                    record.NewBurnupJPerKgHm != after.CurrentBurnupJPerKgHm)
                {
                    return Invalid(
                        "Phase5Invariant.BurnupInterval.Record.BindingMismatch",
                        ContractValidation.NodePath(before.Node, ".burnup"),
                        "The interval record must bind the exact source, result, power, and time values.");
                }

                double expectedIntervalEnergy = sample.PowerWatts * interval.DeltaTimeSeconds;
                if (!ContractValidation.IsFinite(expectedIntervalEnergy) || expectedIntervalEnergy < 0 ||
                    record.DeltaFissionEnergyJ != expectedIntervalEnergy)
                {
                    return Invalid(
                        "Phase5Invariant.BurnupInterval.EnergyAccounting.Mismatch",
                        ContractValidation.NodePath(before.Node, ".delta_fission_energy_j"),
                        "The interval energy must equal the exact accepted power times interval duration.");
                }

                double expectedEnergy = before.CumulativeFissionEnergyJ + expectedIntervalEnergy;
                if (expectedIntervalEnergy > 0 && expectedEnergy == before.CumulativeFissionEnergyJ)
                {
                    return Invalid(
                        "Phase5Invariant.BurnupInterval.CumulativeEnergy.NoRepresentableIncrease",
                        ContractValidation.NodePath(before.Node, ".cumulative_fission_energy_j"),
                        "A positive interval energy must produce a representable cumulative-energy increase.");
                }

                if (!ContractValidation.IsFinite(expectedEnergy) || expectedEnergy < before.CumulativeFissionEnergyJ ||
                    record.NewCumulativeFissionEnergyJ != expectedEnergy ||
                    after.CumulativeFissionEnergyJ != expectedEnergy)
                {
                    return Invalid(
                        "Phase5Invariant.BurnupInterval.CumulativeEnergy.Mismatch",
                        ContractValidation.NodePath(before.Node, ".cumulative_fission_energy_j"),
                        "Cumulative fission energy must equal old energy plus exact interval energy.");
                }

                double expectedBurnup = before.InitialBurnupJPerKgHm +
                    expectedEnergy / before.HeavyMetalMassKg;
                if (!ContractValidation.IsFinite(expectedBurnup) || expectedBurnup < before.CurrentBurnupJPerKgHm ||
                    record.NewBurnupJPerKgHm != expectedBurnup ||
                    after.CurrentBurnupJPerKgHm != expectedBurnup)
                {
                    return Invalid(
                        "Phase5Invariant.BurnupInterval.BurnupAccounting.Mismatch",
                        ContractValidation.NodePath(before.Node, ".burnup_j_per_kg_hm"),
                        "Derived burnup must equal initial burnup plus cumulative energy divided by mass.");
                }

                if (!ContractValidation.IsFinite(totalIntervalEnergy + expectedIntervalEnergy))
                {
                    return Invalid(
                        "Phase5Invariant.BurnupInterval.TotalEnergy.Invalid",
                        "burnup_interval.total_interval_energy_j",
                        "The canonical interval-energy total must remain finite.");
                }

                totalIntervalEnergy += expectedIntervalEnergy;
                monotonicCount++;
            }

            return ContractValidationResult<Phase5InvariantReportV1>.Valid(
                CreateReport(
                    Phase5InvariantScopeV1.BurnupInterval,
                    resultValue,
                    sourceValue.Bundles.Length,
                    monotonicCount,
                    0,
                    totalIntervalEnergy));
        }

        public static ContractValidationResult<Phase5InvariantReportV1> TryValidateRefuelShift(
            RefuelShiftResult shift,
            int expectedBundleCount)
        {
            if (shift == null)
            {
                return Invalid(
                    "Phase5Invariant.RefuelShift.Missing",
                    "refuel_shift",
                    "A completed refuelling shift result is required.");
            }

            ContractValidationResult<InventorySnapshot> source =
                TryReadInventory(shift.SourceInventory, expectedBundleCount);
            if (!source.IsValid)
            {
                return Invalid(source.FirstDiagnostic);
            }

            ContractValidationResult<InventorySnapshot> result =
                TryReadInventory(shift.ResultingInventory, expectedBundleCount);
            if (!result.IsValid)
            {
                return Invalid(result.FirstDiagnostic);
            }

            InventorySnapshot sourceValue = source.Value;
            InventorySnapshot resultValue = result.Value;
            if (!ReferenceEquals(
                    sourceValue.Inventory.Topology,
                    resultValue.Inventory.Topology))
            {
                return Invalid(
                    "Phase5Invariant.RefuelShift.Topology.IdentityMismatch",
                    "refuel_shift.resulting_inventory.topology",
                    "A refuelling shift must preserve the exact source topology instance.");
            }

            if (shift.PositionPlan == null || shift.PositionPlan.ShiftCount == 0 ||
                shift.PositionPlan.InsertedPositions.Count != shift.PositionPlan.ShiftCount ||
                shift.PositionPlan.DischargedPositions.Count != shift.PositionPlan.ShiftCount ||
                shift.PositionPlan.BundlePositionCount != sourceValue.Inventory.Topology.BundlePositionCount)
            {
                return Invalid(
                    "Phase5Invariant.RefuelShift.PositionPlan.Invalid",
                    "refuel_shift.position_plan",
                    "The refuelling shift requires a non-empty plan matching the topology.");
            }

            if (shift.ChannelId.Value >= sourceValue.Inventory.Topology.ChannelCount ||
                !ContractValidation.IsFinite(shift.EffectiveTimeSeconds) ||
                shift.EffectiveTimeSeconds < 0)
            {
                return Invalid(
                    "Phase5Invariant.RefuelShift.Binding.Invalid",
                    "refuel_shift",
                    "The shift channel and effective time must be valid under the topology contract.");
            }

            ChannelTopology channel = sourceValue.Inventory.Topology.GetChannel(shift.ChannelId);
            if (channel.FlowDirection != shift.PositionPlan.FlowDirection ||
                !RefuelSchemeDefinition.IsDirectionBindingValid(
                    channel.FlowDirection,
                    shift.PositionPlan.ShiftDirection))
            {
                return Invalid(
                    "Phase5Invariant.RefuelShift.Direction.Invalid",
                    "refuel_shift.position_plan.shift_direction",
                    "The shift direction must equal the channel's explicit flow-direction binding.");
            }

            if (shift.DischargedBundles == null ||
                shift.DischargedBundles.Count != shift.PositionPlan.ShiftCount)
            {
                return Invalid(
                    "Phase5Invariant.RefuelShift.DischargedCount.Mismatch",
                    "refuel_shift.discharged_bundles",
                    "The discharged state count must equal the declarative shift count.");
            }

            var dischargedIds = new HashSet<StableId>();
            for (int index = 0; index < shift.PositionPlan.DischargedPositions.Count; index++)
            {
                BundlePosition position = shift.PositionPlan.DischargedPositions[index];
                BundleState? expected = sourceValue.Inventory.Get(
                    new NodeKey(shift.ChannelId, position));
                BundleState discharged = shift.DischargedBundles[index];
                if (expected == null || discharged == null ||
                    !dischargedIds.Add(discharged.BundleId) ||
                    !SameBundleState(expected, discharged) ||
                    resultValue.ById.ContainsKey(discharged.BundleId))
                {
                    return Invalid(
                        "Phase5Invariant.RefuelShift.DischargedBinding.Invalid",
                        "refuel_shift.discharged_bundles[" + index + "]",
                        "Discharged states must match the canonical source boundary and be absent from live state.");
                }
            }

            var insertedIds = new HashSet<StableId>();
            int preservedCount = 0;
            for (int index = 0; index < sourceValue.Bundles.Length; index++)
            {
                BundleState before = sourceValue.Bundles[index];
                if (dischargedIds.Contains(before.BundleId))
                {
                    continue;
                }

                uint destinationPosition = before.Position.Value;
                if (before.ChannelId == shift.ChannelId)
                {
                    uint shiftCount = shift.PositionPlan.ShiftCount;
                    if (shift.PositionPlan.ShiftDirection == RefuelShiftDirection.TowardEndB)
                    {
                        if (destinationPosition > uint.MaxValue - shiftCount)
                        {
                            return Invalid(
                                "Phase5Invariant.RefuelShift.RetainedPosition.Invalid",
                                ContractValidation.NodePath(before.Node, ".bundle_id"),
                                "A retained bundle destination must fit the explicit unsigned physical-position type.");
                        }

                        destinationPosition += shiftCount;
                    }
                    else
                    {
                        if (destinationPosition < shiftCount)
                        {
                            return Invalid(
                                "Phase5Invariant.RefuelShift.RetainedPosition.Invalid",
                                ContractValidation.NodePath(before.Node, ".bundle_id"),
                                "A retained bundle destination must remain within the explicit physical-position range.");
                        }

                        destinationPosition -= shiftCount;
                    }
                }

                BundleState? after;
                if (!resultValue.Inventory.TryGet(
                        new NodeKey(before.ChannelId, new BundlePosition(destinationPosition)),
                        out after))
                {
                    after = null;
                }
                if (after == null || after.BundleId != before.BundleId ||
                    !SameBundleStateExceptLocation(before, after))
                {
                    return Invalid(
                        "Phase5Invariant.RefuelShift.RetainedBinding.Invalid",
                        ContractValidation.NodePath(before.Node, ".bundle_id"),
                        "Every retained bundle must move to the exact approved position and preserve its energy state.");
                }

                preservedCount++;
            }

            foreach (BundleState after in resultValue.Bundles)
            {
                if (sourceValue.ById.ContainsKey(after.BundleId))
                {
                    continue;
                }

                if (!insertedIds.Add(after.BundleId) ||
                    after.ChannelId != shift.ChannelId ||
                    !shift.PositionPlan.InsertedPositions.Contains(after.Position) ||
                    after.CumulativeFissionEnergyJ != 0.0 ||
                    after.InsertedAtSeconds != shift.EffectiveTimeSeconds ||
                    after.CurrentBurnupJPerKgHm != after.InitialBurnupJPerKgHm)
                {
                    return Invalid(
                        "Phase5Invariant.RefuelShift.InsertedBinding.Invalid",
                        ContractValidation.NodePath(after.Node, ".bundle_id"),
                        "Every new identity must be a fresh zero-energy bundle at an approved insertion position and time.");
                }
            }

            if (insertedIds.Count != shift.PositionPlan.ShiftCount)
            {
                return Invalid(
                    "Phase5Invariant.RefuelShift.InsertedCount.Mismatch",
                    "refuel_shift.resulting_inventory",
                    "The resulting inventory must contain exactly ShiftCount fresh identities.");
            }

            foreach (BundlePosition position in shift.PositionPlan.InsertedPositions)
            {
                BundleState? inserted = resultValue.Inventory.Get(new NodeKey(shift.ChannelId, position));
                if (inserted == null || sourceValue.ById.ContainsKey(inserted.BundleId))
                {
                    return Invalid(
                        "Phase5Invariant.RefuelShift.InsertedPosition.Invalid",
                        ContractValidation.NodePath(new NodeKey(shift.ChannelId, position), ".bundle_id"),
                        "Every canonical insertion position must contain one new bundle identity.");
                }
            }

            return ContractValidationResult<Phase5InvariantReportV1>.Valid(
                CreateReport(
                    Phase5InvariantScopeV1.RefuelShift,
                    resultValue,
                    0,
                    0,
                    preservedCount,
                    0.0,
                    insertedIds.Count,
                    dischargedIds.Count));
        }

        private static ContractValidationResult<InventorySnapshot> TryReadInventory(
            BundleInventory inventory,
            int expectedBundleCount)
        {
            if (inventory == null)
            {
                return ContractValidationResult<InventorySnapshot>.Invalid(
                    "Phase5Invariant.Inventory.Missing",
                    "inventory",
                    "A validated bundle inventory is required.");
            }

            if (expectedBundleCount <= 0 || expectedBundleCount > inventory.Topology.SlotCount)
            {
                return ContractValidationResult<InventorySnapshot>.Invalid(
                    "Phase5Invariant.ExpectedBundleCount.Invalid",
                    "expected_bundle_count",
                    "The expected bundle count must be positive and fit the validated topology.");
            }

            BundleState[] bundles = inventory.EnumerateOccupied()
                .OrderBy(bundle => bundle.ChannelId.Value)
                .ThenBy(bundle => bundle.Position.Value)
                .ThenBy(bundle => bundle.BundleId)
                .ToArray();
            if (bundles.Length != expectedBundleCount || inventory.OccupiedCount != expectedBundleCount)
            {
                return ContractValidationResult<InventorySnapshot>.Invalid(
                    "Phase5Invariant.BundleCount.Mismatch",
                    "inventory.occupied_count",
                    "The live bundle count must equal the explicit expected bundle count.");
            }

            var byId = new Dictionary<StableId, BundleState>();
            var byNode = new Dictionary<NodeKey, BundleState>();
            double totalEnergy = 0.0;
            for (int index = 0; index < bundles.Length; index++)
            {
                BundleState bundle = bundles[index];
                string path = "bundles[" + index + "]";
                if (bundle.BundleId.IsEmpty)
                {
                    return ContractValidationResult<InventorySnapshot>.Invalid(
                        "Phase5Invariant.BundleId.Empty",
                        path + ".bundle_id",
                        "Every live bundle requires an explicit stable identity.");
                }

                if (!byId.TryAdd(bundle.BundleId, bundle))
                {
                    return ContractValidationResult<InventorySnapshot>.Invalid(
                        "Phase5Invariant.BundleId.Duplicate",
                        path + ".bundle_id",
                        "Live bundle identities must be unique.");
                }

                int flatIndex;
                if (!inventory.Topology.TryGetFlatIndex(bundle.Node, out flatIndex))
                {
                    return ContractValidationResult<InventorySnapshot>.Invalid(
                        "Phase5Invariant.Location.Illegal",
                        path + ".location",
                        "Every live bundle location must be legal under the explicit topology.");
                }

                if (!byNode.TryAdd(bundle.Node, bundle))
                {
                    return ContractValidationResult<InventorySnapshot>.Invalid(
                        "Phase5Invariant.Location.Duplicate",
                        path + ".location",
                        "Every live topology node may contain at most one bundle identity.");
                }

                if (!inventory.TryGet(bundle.Node, out BundleState? bound) ||
                    bound == null || bound.BundleId != bundle.BundleId ||
                    inventory.Topology.GetFlatIndex(bundle.Node) != flatIndex)
                {
                    return ContractValidationResult<InventorySnapshot>.Invalid(
                        "Phase5Invariant.Location.BindingMismatch",
                        path + ".location",
                        "The inventory slot binding must equal the bundle's explicit node identity.");
                }

                if (string.IsNullOrWhiteSpace(bundle.MaterialVariantId.Value))
                {
                    return ContractValidationResult<InventorySnapshot>.Invalid(
                        "Phase5Invariant.MaterialVariant.Empty",
                        path + ".material_variant_id",
                        "Every live bundle requires a material variant identity.");
                }

                if (!ContractValidation.IsFinite(bundle.InitialBurnupJPerKgHm) ||
                    bundle.InitialBurnupJPerKgHm < 0)
                {
                    return ContractValidationResult<InventorySnapshot>.Invalid(
                        "Phase5Invariant.InitialBurnup.Invalid",
                        path + ".initial_burnup_j_per_kg_hm",
                        "Initial burnup must be finite and nonnegative SI J/kg_HM.");
                }

                if (!ContractValidation.IsFinite(bundle.CumulativeFissionEnergyJ) ||
                    bundle.CumulativeFissionEnergyJ < 0)
                {
                    return ContractValidationResult<InventorySnapshot>.Invalid(
                        "Phase5Invariant.CumulativeEnergy.Invalid",
                        path + ".cumulative_fission_energy_j",
                        "Cumulative fission energy must be finite and nonnegative SI joules.");
                }

                if (!ContractValidation.IsFinite(bundle.HeavyMetalMassKg) ||
                    bundle.HeavyMetalMassKg <= 0)
                {
                    return ContractValidationResult<InventorySnapshot>.Invalid(
                        "Phase5Invariant.Mass.Invalid",
                        path + ".heavy_metal_mass_kg",
                        "Heavy-metal mass must be finite and strictly positive SI kilograms.");
                }

                if (!ContractValidation.IsFinite(bundle.InsertedAtSeconds) ||
                    bundle.InsertedAtSeconds < 0)
                {
                    return ContractValidationResult<InventorySnapshot>.Invalid(
                        "Phase5Invariant.InsertedAt.Invalid",
                        path + ".inserted_at_s",
                        "InsertedAt must be finite and nonnegative SI seconds.");
                }

                double expectedBurnup = bundle.InitialBurnupJPerKgHm +
                    bundle.CumulativeFissionEnergyJ / bundle.HeavyMetalMassKg;
                if (!ContractValidation.IsFinite(expectedBurnup) || expectedBurnup < 0 ||
                    bundle.CurrentBurnupJPerKgHm != expectedBurnup)
                {
                    return ContractValidationResult<InventorySnapshot>.Invalid(
                        "Phase5Invariant.Burnup.AccountingMismatch",
                        path + ".burnup_j_per_kg_hm",
                        "Derived burnup must equal initial burnup plus cumulative energy divided by mass.");
                }

                double nextTotalEnergy = totalEnergy + bundle.CumulativeFissionEnergyJ;
                if (!ContractValidation.IsFinite(nextTotalEnergy))
                {
                    return ContractValidationResult<InventorySnapshot>.Invalid(
                        "Phase5Invariant.TotalEnergy.Invalid",
                        "inventory.total_cumulative_fission_energy_j",
                        "The canonical cumulative-energy total must remain finite.");
                }

                totalEnergy = nextTotalEnergy;
            }

            return ContractValidationResult<InventorySnapshot>.Valid(
                new InventorySnapshot(
                    inventory,
                    bundles,
                    byId,
                    byNode,
                    totalEnergy,
                    expectedBundleCount));
        }

        private static ContractValidationResult<bool> ValidateBurnupIntervalMetadata(
            BurnupIntervalResultV1 interval)
        {
            if (!ContractValidation.IsFinite(interval.CurrentTimeSeconds) ||
                !ContractValidation.IsFinite(interval.TargetTimeSeconds) ||
                interval.TargetTimeSeconds <= interval.CurrentTimeSeconds ||
                !ContractValidation.IsFinite(interval.DeltaTimeSeconds) ||
                interval.DeltaTimeSeconds <= 0 ||
                interval.DeltaTimeSeconds != interval.TargetTimeSeconds - interval.CurrentTimeSeconds)
            {
                return ContractValidationResult<bool>.Invalid(
                    "Phase5Invariant.BurnupInterval.Time.Invalid",
                    "burnup_interval",
                    "A burnup interval must use finite explicit time with exact positive duration.");
            }

            if (interval.CoreStateVersionBefore == ulong.MaxValue ||
                interval.CoreStateVersionAfter != interval.CoreStateVersionBefore + 1 ||
                !interval.PowerSnapshotInvalidated ||
                interval.PowerSnapshot == null ||
                interval.PowerSnapshot.SnapshotTimeSeconds != interval.CurrentTimeSeconds ||
                interval.PowerSnapshot.CoreStateVersion != interval.CoreStateVersionBefore)
            {
                return ContractValidationResult<bool>.Invalid(
                    "Phase5Invariant.BurnupInterval.VersionBinding.Invalid",
                    "burnup_interval",
                    "A committed burnup interval must advance the core version exactly once and invalidate its source snapshot.");
            }

            return ContractValidationResult<bool>.Valid(true);
        }

        private static Phase5InvariantReportV1 CreateReport(
            Phase5InvariantScopeV1 scope,
            InventorySnapshot snapshot,
            int energyRecordCount,
            int monotonicBurnupCount,
            int preservedBundleCount,
            double totalIntervalEnergyJ,
            int insertedBundleCount = 0,
            int dischargedBundleCount = 0)
        {
            return new Phase5InvariantReportV1(
                scope,
                snapshot.ExpectedBundleCount,
                snapshot.Bundles.Length,
                snapshot.ById.Count,
                snapshot.ByNode.Count,
                snapshot.Bundles.Length,
                monotonicBurnupCount,
                energyRecordCount,
                preservedBundleCount,
                insertedBundleCount,
                dischargedBundleCount,
                snapshot.TotalCumulativeFissionEnergyJ,
                totalIntervalEnergyJ);
        }

        private static bool SameBundleState(BundleState left, BundleState right)
        {
            return left.BundleId == right.BundleId &&
                   left.ChannelId == right.ChannelId &&
                   left.Position == right.Position &&
                   SameBundleStateExceptLocation(left, right);
        }

        private static bool SameBundleStateExceptLocation(BundleState left, BundleState right)
        {
            return SameIdentityAndHistory(left, right) &&
                   left.CumulativeFissionEnergyJ == right.CumulativeFissionEnergyJ;
        }

        private static bool SameIdentityAndHistory(BundleState left, BundleState right)
        {
            return left.BundleId == right.BundleId &&
                   left.MaterialVariantId == right.MaterialVariantId &&
                   left.InitialBurnupJPerKgHm == right.InitialBurnupJPerKgHm &&
                   left.HeavyMetalMassKg == right.HeavyMetalMassKg &&
                   left.InsertedAtSeconds == right.InsertedAtSeconds;
        }

        private static ContractValidationResult<Phase5InvariantReportV1> Invalid(
            ContractDiagnostic diagnostic)
        {
            return Invalid(diagnostic.Code, diagnostic.Path, diagnostic.Message);
        }

        private static ContractValidationResult<Phase5InvariantReportV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<Phase5InvariantReportV1>.Invalid(code, path, message);
        }

        private sealed class InventorySnapshot
        {
            public InventorySnapshot(
                BundleInventory inventory,
                BundleState[] bundles,
                Dictionary<StableId, BundleState> byId,
                Dictionary<NodeKey, BundleState> byNode,
                double totalCumulativeFissionEnergyJ,
                int expectedBundleCount)
            {
                Inventory = inventory;
                Bundles = bundles;
                ById = byId;
                ByNode = byNode;
                TotalCumulativeFissionEnergyJ = totalCumulativeFissionEnergyJ;
                ExpectedBundleCount = expectedBundleCount;
            }

            public BundleInventory Inventory { get; }

            public BundleState[] Bundles { get; }

            public Dictionary<StableId, BundleState> ById { get; }

            public Dictionary<NodeKey, BundleState> ByNode { get; }

            public double TotalCumulativeFissionEnergyJ { get; }

            public int ExpectedBundleCount { get; }
        }
    }
}
