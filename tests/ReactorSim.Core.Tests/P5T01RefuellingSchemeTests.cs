using System;
using System.Linq;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class P5T01RefuellingSchemeTests
{
    private static readonly string[] CanonicalSchemeIds = { "S4", "S8" };

    [Fact]
    public void S4AndS8DefinitionsProduceCanonicalPlansForBothFlowDirections()
    {
        RefuelSchemeDefinition s4 = CreateScheme("S4", 4);
        RefuelSchemeDefinition s8 = CreateScheme("S8", 8);
        ContractValidationResult<RefuelSchemeCatalog> catalogResult =
            RefuelSchemeCatalog.TryCreate(new[] { s8, s4 });
        AssertValid(catalogResult);

        Assert.Equal(CanonicalSchemeIds, catalogResult.Value.Definitions.Select(definition => definition.SchemeId));

        ContractValidationResult<RefuelSchemePositionPlan> s4TowardB =
            s4.TryCreatePositionPlan(12, FlowDirection.EndAtoEndB);
        ContractValidationResult<RefuelSchemePositionPlan> s4TowardA =
            s4.TryCreatePositionPlan(12, FlowDirection.EndBtoEndA);
        ContractValidationResult<RefuelSchemePositionPlan> s8TowardB =
            s8.TryCreatePositionPlan(12, FlowDirection.EndAtoEndB);
        ContractValidationResult<RefuelSchemePositionPlan> s8TowardA =
            s8.TryCreatePositionPlan(12, FlowDirection.EndBtoEndA);
        AssertValid(s4TowardB);
        AssertValid(s4TowardA);
        AssertValid(s8TowardB);
        AssertValid(s8TowardA);

        Assert.Equal(RefuelShiftDirection.TowardEndB, s4TowardB.Value.ShiftDirection);
        Assert.Equal(RefuelShiftDirection.TowardEndA, s4TowardA.Value.ShiftDirection);
        Assert.Equal(new uint[] { 0, 1, 2, 3 }, Positions(s4TowardB.Value.InsertedPositions));
        Assert.Equal(new uint[] { 8, 9, 10, 11 }, Positions(s4TowardB.Value.DischargedPositions));
        Assert.Equal(new uint[] { 8, 9, 10, 11 }, Positions(s4TowardA.Value.InsertedPositions));
        Assert.Equal(new uint[] { 0, 1, 2, 3 }, Positions(s4TowardA.Value.DischargedPositions));
        Assert.Equal(new uint[] { 0, 1, 2, 3, 4, 5, 6, 7 }, Positions(s8TowardB.Value.InsertedPositions));
        Assert.Equal(new uint[] { 4, 5, 6, 7, 8, 9, 10, 11 }, Positions(s8TowardB.Value.DischargedPositions));
        Assert.Equal(new uint[] { 4, 5, 6, 7, 8, 9, 10, 11 }, Positions(s8TowardA.Value.InsertedPositions));
        Assert.Equal(new uint[] { 0, 1, 2, 3, 4, 5, 6, 7 }, Positions(s8TowardA.Value.DischargedPositions));
    }

    [Fact]
    public void DirectionBindingIsExplicitAndDoesNotInferFromArrayOrder()
    {
        RefuelSchemeDefinition scheme = CreateScheme("S4", 4);

        Assert.True(RefuelSchemeDefinition.IsDirectionBindingValid(
            FlowDirection.EndAtoEndB,
            RefuelShiftDirection.TowardEndB));
        Assert.True(RefuelSchemeDefinition.IsDirectionBindingValid(
            FlowDirection.EndBtoEndA,
            RefuelShiftDirection.TowardEndA));
        Assert.False(RefuelSchemeDefinition.IsDirectionBindingValid(
            FlowDirection.EndAtoEndB,
            RefuelShiftDirection.TowardEndA));
        Assert.False(RefuelSchemeDefinition.IsDirectionBindingValid(
            FlowDirection.EndBtoEndA,
            RefuelShiftDirection.TowardEndB));
        Assert.False(RefuelSchemeDefinition.IsDirectionBindingValid(
            (FlowDirection)255,
            RefuelShiftDirection.TowardEndB));
    }

    [Fact]
    public void DefinitionRejectsUnsupportedCountsSchemaAndNonCanonicalOrders()
    {
        ContractValidationResult<RefuelSchemeDefinition> unsupportedCount =
            RefuelSchemeDefinition.TryCreate(
                "S1",
                1,
                "FT-SYN",
                RefuelSchemeDefinition.CurrentSchemaVersion,
                new[] { new BundlePosition(0) },
                new[] { new BundlePosition(0) });
        AssertInvalid(unsupportedCount, "RefuelScheme.ShiftCount.Unsupported");

        ContractValidationResult<RefuelSchemeDefinition> unsupportedSchema =
            RefuelSchemeDefinition.TryCreate(
                "S4",
                4,
                "FT-SYN",
                RefuelSchemeDefinition.CurrentSchemaVersion + 1,
                Enumerable.Range(8, 4).Select(value => new BundlePosition((uint)value)),
                Enumerable.Range(0, 4).Select(value => new BundlePosition((uint)value)));
        AssertInvalid(unsupportedSchema, "RefuelScheme.SchemaVersion.Unsupported");

        ContractValidationResult<RefuelSchemeDefinition> duplicateOrder =
            RefuelSchemeDefinition.TryCreate(
                "S4",
                4,
                "FT-SYN",
                RefuelSchemeDefinition.CurrentSchemaVersion,
                new[]
                {
                    new BundlePosition(8),
                    new BundlePosition(9),
                    new BundlePosition(9),
                    new BundlePosition(11)
                },
                new[]
                {
                    new BundlePosition(0),
                    new BundlePosition(1),
                    new BundlePosition(2),
                    new BundlePosition(3)
                });
        AssertInvalid(duplicateOrder, "RefuelScheme.InsertedSlotOrder.NotAscending");
    }

    [Fact]
    public void PositionPlanRejectsWrongChannelSizeAndWrongStoredBoundaryOrder()
    {
        RefuelSchemeDefinition s8 = CreateScheme("S8", 8);
        ContractValidationResult<RefuelSchemePositionPlan> tooSmall =
            s8.TryCreatePositionPlan(7, FlowDirection.EndAtoEndB);
        AssertInvalid(tooSmall, "RefuelScheme.PositionPlan.ShiftCount.ExceedsPositionCount");

        ContractValidationResult<RefuelSchemePositionPlan> tooLarge =
            s8.TryCreatePositionPlan(uint.MaxValue, FlowDirection.EndAtoEndB);
        AssertInvalid(tooLarge, "RefuelScheme.PositionPlan.PositionCount.ExceedsRuntimeBound");

        ContractValidationResult<RefuelSchemeDefinition> nonBoundary =
            RefuelSchemeDefinition.TryCreate(
                "S4",
                4,
                "FT-SYN",
                RefuelSchemeDefinition.CurrentSchemaVersion,
                new[]
                {
                    new BundlePosition(7),
                    new BundlePosition(8),
                    new BundlePosition(9),
                    new BundlePosition(10)
                },
                new[]
                {
                    new BundlePosition(0),
                    new BundlePosition(1),
                    new BundlePosition(2),
                    new BundlePosition(3)
                });
        AssertValid(nonBoundary);

        ContractValidationResult<RefuelSchemePositionPlan> invalidPlan =
            nonBoundary.Value.TryCreatePositionPlan(12, FlowDirection.EndBtoEndA);
        AssertInvalid(invalidPlan, "RefuelScheme.PositionPlan.InsertedSlotOrder.TowardEndA.Invalid");
    }

    [Fact]
    public void CatalogRejectsDuplicateIdentitiesAndResolvesCanonicalDefinitions()
    {
        RefuelSchemeDefinition s4 = CreateScheme("S4", 4);
        RefuelSchemeDefinition duplicate = CreateScheme("S4", 8);
        ContractValidationResult<RefuelSchemeCatalog> duplicateResult =
            RefuelSchemeCatalog.TryCreate(new[] { duplicate, s4 });
        AssertInvalid(duplicateResult, "RefuelSchemeCatalog.SchemeId.Duplicate");

        ContractValidationResult<RefuelSchemeCatalog> catalogResult =
            RefuelSchemeCatalog.TryCreate(new[] { CreateScheme("S8", 8), s4 });
        AssertValid(catalogResult);
        Assert.True(catalogResult.Value.TryGet("S4", out RefuelSchemeDefinition? resolved));
        Assert.Equal((ushort)4, resolved!.ShiftCount);
        Assert.False(catalogResult.Value.TryGet("missing", out _));

        ContractValidationResult<RefuelSchemeCatalog> missingEight =
            RefuelSchemeCatalog.TryCreate(new[] { s4 });
        AssertInvalid(missingEight, "RefuelSchemeCatalog.ShiftCount.EightMissing");

        ContractValidationResult<RefuelSchemeCatalog> missingFour =
            RefuelSchemeCatalog.TryCreate(new[] { CreateScheme("S8-only", 8) });
        AssertInvalid(missingFour, "RefuelSchemeCatalog.ShiftCount.FourMissing");
    }

    private static RefuelSchemeDefinition CreateScheme(string schemeId, ushort shiftCount)
    {
        uint towardEndBLast = shiftCount - 1u;
        uint towardEndAFirst = 12u - shiftCount;
        ContractValidationResult<RefuelSchemeDefinition> result =
            RefuelSchemeDefinition.TryCreate(
                schemeId,
                shiftCount,
                "FT-SYN-1000KG",
                RefuelSchemeDefinition.CurrentSchemaVersion,
                Enumerable.Range((int)towardEndAFirst, shiftCount)
                    .Select(value => new BundlePosition((uint)value)),
                Enumerable.Range(0, (int)towardEndBLast + 1)
                    .Select(value => new BundlePosition((uint)value)));
        AssertValid(result);
        return result.Value;
    }

    private static uint[] Positions(System.Collections.Generic.IReadOnlyList<BundlePosition> positions)
    {
        return positions.Select(position => position.Value).ToArray();
    }

    private static void AssertValid<T>(ContractValidationResult<T> result)
        where T : class
    {
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
    }

    private static void AssertInvalid<T>(ContractValidationResult<T> result, string code)
    {
        Assert.False(result.IsValid);
        Assert.Equal(code, result.FirstDiagnostic.Code);
    }
}
