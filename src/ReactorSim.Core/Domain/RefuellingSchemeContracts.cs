using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ReactorSim.Core
{
    /// <summary>
    /// The explicit endpoint direction of one declarative refuelling scheme.
    /// This is deliberately separate from the spatial-neighbor direction enum.
    /// </summary>
    public enum RefuelShiftDirection : byte
    {
        TowardEndA = 0,
        TowardEndB = 1
    }

    /// <summary>
    /// Canonical position-only plan produced from one validated scheme and
    /// one channel flow direction. It contains no bundle mutation or physics.
    /// </summary>
    public sealed class RefuelSchemePositionPlan
    {
        internal RefuelSchemePositionPlan(
            string schemeId,
            uint bundlePositionCount,
            FlowDirection flowDirection,
            RefuelShiftDirection shiftDirection,
            IEnumerable<BundlePosition> insertedPositions,
            IEnumerable<BundlePosition> dischargedPositions)
        {
            SchemeId = schemeId;
            BundlePositionCount = bundlePositionCount;
            FlowDirection = flowDirection;
            ShiftDirection = shiftDirection;
            InsertedPositions = new ReadOnlyCollection<BundlePosition>(insertedPositions.ToArray());
            DischargedPositions = new ReadOnlyCollection<BundlePosition>(dischargedPositions.ToArray());
        }

        public string SchemeId { get; }

        public uint BundlePositionCount { get; }

        public FlowDirection FlowDirection { get; }

        public RefuelShiftDirection ShiftDirection { get; }

        public IReadOnlyList<BundlePosition> InsertedPositions { get; }

        public IReadOnlyList<BundlePosition> DischargedPositions { get; }

        public ushort ShiftCount
        {
            get { return checked((ushort)InsertedPositions.Count); }
        }
    }

    /// <summary>
    /// Versioned declarative S4/S8 scheme metadata. The definition describes
    /// position order and template identity only; it does not apply a shift.
    /// Callers must validate it against the channel's position count before
    /// using its position plan.
    /// </summary>
    public sealed class RefuelSchemeDefinition
    {
        public const uint CurrentSchemaVersion = 1;

        private readonly ReadOnlyCollection<BundlePosition> _towardEndAInsertedSlotOrder;
        private readonly ReadOnlyCollection<BundlePosition> _towardEndBInsertedSlotOrder;

        private RefuelSchemeDefinition(
            string schemeId,
            ushort shiftCount,
            string freshFuelTemplateId,
            uint schemaVersion,
            IEnumerable<BundlePosition> towardEndAInsertedSlotOrder,
            IEnumerable<BundlePosition> towardEndBInsertedSlotOrder)
        {
            SchemeId = schemeId;
            ShiftCount = shiftCount;
            FreshFuelTemplateId = freshFuelTemplateId;
            SchemaVersion = schemaVersion;
            _towardEndAInsertedSlotOrder =
                new ReadOnlyCollection<BundlePosition>(towardEndAInsertedSlotOrder.ToArray());
            _towardEndBInsertedSlotOrder =
                new ReadOnlyCollection<BundlePosition>(towardEndBInsertedSlotOrder.ToArray());
        }

        public string SchemeId { get; }

        public ushort ShiftCount { get; }

        public string FreshFuelTemplateId { get; }

        public uint SchemaVersion { get; }

        /// <summary>
        /// The ascending physical positions occupied by inserted bundles when
        /// the channel flows from EndB toward EndA.
        /// </summary>
        public IReadOnlyList<BundlePosition> TowardEndAInsertedSlotOrder
        {
            get { return _towardEndAInsertedSlotOrder; }
        }

        /// <summary>
        /// The ascending physical positions occupied by inserted bundles when
        /// the channel flows from EndA toward EndB.
        /// </summary>
        public IReadOnlyList<BundlePosition> TowardEndBInsertedSlotOrder
        {
            get { return _towardEndBInsertedSlotOrder; }
        }

        public static ContractValidationResult<RefuelSchemeDefinition> TryCreate(
            string? schemeId,
            ushort shiftCount,
            string? freshFuelTemplateId,
            uint schemaVersion,
            IEnumerable<BundlePosition>? towardEndAInsertedSlotOrder,
            IEnumerable<BundlePosition>? towardEndBInsertedSlotOrder)
        {
            if (string.IsNullOrWhiteSpace(schemeId))
            {
                return ContractValidationResult<RefuelSchemeDefinition>.Invalid(
                    "RefuelScheme.SchemeId.Missing",
                    "scheme_id",
                    "A scheme requires a non-empty identity.");
            }

            if (!IsSupportedShiftCount(shiftCount))
            {
                return ContractValidationResult<RefuelSchemeDefinition>.Invalid(
                    "RefuelScheme.ShiftCount.Unsupported",
                    "shift_count",
                    "The v1 declarative scheme count must be exactly 4 or 8.");
            }

            if (string.IsNullOrWhiteSpace(freshFuelTemplateId))
            {
                return ContractValidationResult<RefuelSchemeDefinition>.Invalid(
                    "RefuelScheme.FreshFuelTemplateId.Missing",
                    "fresh_fuel_template_id",
                    "A scheme requires an explicit fresh-fuel template identity.");
            }

            if (schemaVersion != CurrentSchemaVersion)
            {
                return ContractValidationResult<RefuelSchemeDefinition>.Invalid(
                    "RefuelScheme.SchemaVersion.Unsupported",
                    "schema_version",
                    "The scheme schema version is not the approved v1 version.");
            }

            if (towardEndAInsertedSlotOrder == null)
            {
                return ContractValidationResult<RefuelSchemeDefinition>.Invalid(
                    "RefuelScheme.InsertedSlotOrder.TowardEndA.Missing",
                    "toward_end_a_inserted_slot_order",
                    "The EndA insertion order is required.");
            }

            if (towardEndBInsertedSlotOrder == null)
            {
                return ContractValidationResult<RefuelSchemeDefinition>.Invalid(
                    "RefuelScheme.InsertedSlotOrder.TowardEndB.Missing",
                    "toward_end_b_inserted_slot_order",
                    "The EndB insertion order is required.");
            }

            BundlePosition[] towardEndA = towardEndAInsertedSlotOrder.ToArray();
            BundlePosition[] towardEndB = towardEndBInsertedSlotOrder.ToArray();

            ContractDiagnostic? orderDiagnostic = ValidateOrder(
                towardEndA,
                shiftCount,
                "toward_end_a_inserted_slot_order");
            if (orderDiagnostic != null)
            {
                return ContractValidationResult<RefuelSchemeDefinition>.Invalid(
                    orderDiagnostic.Code,
                    orderDiagnostic.Path,
                    orderDiagnostic.Message);
            }

            orderDiagnostic = ValidateOrder(
                towardEndB,
                shiftCount,
                "toward_end_b_inserted_slot_order");
            if (orderDiagnostic != null)
            {
                return ContractValidationResult<RefuelSchemeDefinition>.Invalid(
                    orderDiagnostic.Code,
                    orderDiagnostic.Path,
                    orderDiagnostic.Message);
            }

            return ContractValidationResult<RefuelSchemeDefinition>.Valid(
                new RefuelSchemeDefinition(
                    schemeId,
                    shiftCount,
                    freshFuelTemplateId,
                    schemaVersion,
                    towardEndA,
                    towardEndB));
        }

        public ContractValidationResult<RefuelSchemePositionPlan> TryCreatePositionPlan(
            uint bundlePositionCount,
            FlowDirection flowDirection)
        {
            if (bundlePositionCount == 0)
            {
                return ContractValidationResult<RefuelSchemePositionPlan>.Invalid(
                    "RefuelScheme.PositionPlan.PositionCount.Invalid",
                    "bundle_position_count",
                    "A channel must contain at least one position.");
            }

            if (bundlePositionCount > int.MaxValue)
            {
                return ContractValidationResult<RefuelSchemePositionPlan>.Invalid(
                    "RefuelScheme.PositionPlan.PositionCount.ExceedsRuntimeBound",
                    "bundle_position_count",
                    "A channel position count must fit the validated Core topology's int-backed storage bound.");
            }

            if ((uint)ShiftCount > bundlePositionCount)
            {
                return ContractValidationResult<RefuelSchemePositionPlan>.Invalid(
                    "RefuelScheme.PositionPlan.ShiftCount.ExceedsPositionCount",
                    "shift_count",
                    "A scheme cannot shift more bundles than the channel contains.");
            }

            RefuelShiftDirection requiredDirection;
            if (!TryGetRequiredShiftDirection(flowDirection, out requiredDirection))
            {
                return ContractValidationResult<RefuelSchemePositionPlan>.Invalid(
                    "RefuelScheme.PositionPlan.FlowDirection.Invalid",
                    "flow_direction",
                    "Flow direction must be EndAtoEndB or EndBtoEndA.");
            }

            BundlePosition[] expectedTowardEndB = CreatePositionRange(0, ShiftCount);
            BundlePosition[] expectedTowardEndA = CreatePositionRange(
                bundlePositionCount - ShiftCount,
                ShiftCount);
            if (!expectedTowardEndA.SequenceEqual(_towardEndAInsertedSlotOrder))
            {
                return ContractValidationResult<RefuelSchemePositionPlan>.Invalid(
                    "RefuelScheme.PositionPlan.InsertedSlotOrder.TowardEndA.Invalid",
                    "toward_end_a_inserted_slot_order",
                    "EndA insertion positions must be the ascending positions at the EndB boundary.");
            }

            if (!expectedTowardEndB.SequenceEqual(_towardEndBInsertedSlotOrder))
            {
                return ContractValidationResult<RefuelSchemePositionPlan>.Invalid(
                    "RefuelScheme.PositionPlan.InsertedSlotOrder.TowardEndB.Invalid",
                    "toward_end_b_inserted_slot_order",
                    "EndB insertion positions must be the ascending positions at the EndA boundary.");
            }

            BundlePosition[] inserted = requiredDirection == RefuelShiftDirection.TowardEndB
                ? _towardEndBInsertedSlotOrder.ToArray()
                : _towardEndAInsertedSlotOrder.ToArray();
            BundlePosition[] discharged = requiredDirection == RefuelShiftDirection.TowardEndB
                ? CreatePositionRange(bundlePositionCount - ShiftCount, ShiftCount)
                : CreatePositionRange(0, ShiftCount);

            return ContractValidationResult<RefuelSchemePositionPlan>.Valid(
                new RefuelSchemePositionPlan(
                    SchemeId,
                    bundlePositionCount,
                    flowDirection,
                    requiredDirection,
                    inserted,
                    discharged));
        }

        public static bool IsDirectionBindingValid(
            FlowDirection flowDirection,
            RefuelShiftDirection shiftDirection)
        {
            RefuelShiftDirection requiredDirection;
            return TryGetRequiredShiftDirection(flowDirection, out requiredDirection) &&
                   requiredDirection == shiftDirection;
        }

        public static bool TryGetRequiredShiftDirection(
            FlowDirection flowDirection,
            out RefuelShiftDirection shiftDirection)
        {
            switch (flowDirection)
            {
                case FlowDirection.EndAtoEndB:
                    shiftDirection = RefuelShiftDirection.TowardEndB;
                    return true;
                case FlowDirection.EndBtoEndA:
                    shiftDirection = RefuelShiftDirection.TowardEndA;
                    return true;
                default:
                    shiftDirection = default(RefuelShiftDirection);
                    return false;
            }
        }

        private static bool IsSupportedShiftCount(ushort shiftCount)
        {
            return shiftCount == 4 || shiftCount == 8;
        }

        private static ContractDiagnostic? ValidateOrder(
            BundlePosition[] order,
            ushort shiftCount,
            string path)
        {
            if (order.Length != shiftCount)
            {
                return new ContractDiagnostic(
                    "RefuelScheme.InsertedSlotOrder.Count",
                    path,
                    "The inserted slot order must contain exactly ShiftCount positions.");
            }

            for (int i = 1; i < order.Length; i++)
            {
                if (order[i - 1].Value >= order[i].Value)
                {
                    return new ContractDiagnostic(
                        "RefuelScheme.InsertedSlotOrder.NotAscending",
                        path + "[" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]",
                        "Inserted positions must be strictly ascending and unique.");
                }
            }

            return null;
        }

        private static BundlePosition[] CreatePositionRange(uint firstPosition, ushort count)
        {
            var positions = new BundlePosition[count];
            for (uint index = 0; index < count; index++)
            {
                positions[index] = new BundlePosition(firstPosition + index);
            }

            return positions;
        }
    }

    /// <summary>
    /// Immutable lookup collection for declarative scheme definitions. It
    /// validates identity uniqueness but does not resolve fuel templates.
    /// </summary>
    public sealed class RefuelSchemeCatalog
    {
        private readonly ReadOnlyCollection<RefuelSchemeDefinition> _definitions;

        private RefuelSchemeCatalog(IEnumerable<RefuelSchemeDefinition> definitions)
        {
            _definitions = new ReadOnlyCollection<RefuelSchemeDefinition>(definitions.ToArray());
        }

        public IReadOnlyList<RefuelSchemeDefinition> Definitions
        {
            get { return _definitions; }
        }

        public static ContractValidationResult<RefuelSchemeCatalog> TryCreate(
            IEnumerable<RefuelSchemeDefinition>? definitions)
        {
            if (definitions == null)
            {
                return ContractValidationResult<RefuelSchemeCatalog>.Invalid(
                    "RefuelSchemeCatalog.Definitions.Missing",
                    "definitions",
                    "At least one declarative scheme definition is required.");
            }

            RefuelSchemeDefinition?[] records = definitions.Cast<RefuelSchemeDefinition?>().ToArray();
            if (records.Length == 0)
            {
                return ContractValidationResult<RefuelSchemeCatalog>.Invalid(
                    "RefuelSchemeCatalog.Definitions.Empty",
                    "definitions",
                    "At least one declarative scheme definition is required.");
            }

            if (records.Any(definition => definition == null))
            {
                return ContractValidationResult<RefuelSchemeCatalog>.Invalid(
                    "RefuelSchemeCatalog.Definition.Null",
                    "definitions",
                    "A scheme catalog may not contain a null definition.");
            }

            RefuelSchemeDefinition[] ordered = records
                .Select(definition => definition!)
                .OrderBy(definition => definition.SchemeId, StringComparer.Ordinal)
                .ToArray();
            for (int i = 1; i < ordered.Length; i++)
            {
                if (string.Equals(ordered[i - 1].SchemeId, ordered[i].SchemeId, StringComparison.Ordinal))
                {
                    return ContractValidationResult<RefuelSchemeCatalog>.Invalid(
                        "RefuelSchemeCatalog.SchemeId.Duplicate",
                        "definitions[scheme_id=" + ordered[i].SchemeId + "]",
                        "Scheme identities must be unique within one catalog.");
                }
            }

            if (!ordered.Any(definition => definition.ShiftCount == 4))
            {
                return ContractValidationResult<RefuelSchemeCatalog>.Invalid(
                    "RefuelSchemeCatalog.ShiftCount.FourMissing",
                    "definitions",
                    "The v1 catalog must contain at least one four-bundle scheme.");
            }

            if (!ordered.Any(definition => definition.ShiftCount == 8))
            {
                return ContractValidationResult<RefuelSchemeCatalog>.Invalid(
                    "RefuelSchemeCatalog.ShiftCount.EightMissing",
                    "definitions",
                    "The v1 catalog must contain at least one eight-bundle scheme.");
            }

            return ContractValidationResult<RefuelSchemeCatalog>.Valid(
                new RefuelSchemeCatalog(ordered));
        }

        public bool TryGet(string? schemeId, out RefuelSchemeDefinition? definition)
        {
            if (!string.IsNullOrWhiteSpace(schemeId))
            {
                for (int i = 0; i < _definitions.Count; i++)
                {
                    if (string.Equals(_definitions[i].SchemeId, schemeId, StringComparison.Ordinal))
                    {
                        definition = _definitions[i];
                        return true;
                    }
                }
            }

            definition = null;
            return false;
        }
    }
}
