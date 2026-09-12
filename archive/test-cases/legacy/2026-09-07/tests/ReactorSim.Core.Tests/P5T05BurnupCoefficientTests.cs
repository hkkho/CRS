using System;
using System.Collections.Generic;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class P5T05BurnupCoefficientTests
{
    [Fact]
    public void InterpolatesCanonicalRowsAndDerivesChiTwo()
    {
        BurnupCoefficientTableV1 table = CreateTable();

        ContractValidationResult<BurnupCoefficientLookupResultV1> result =
            table.TryLookup(80000.0);

        AssertValid(result);
        BurnupCoefficientLookupResultV1 lookup = result.Value;
        Assert.Equal(1, lookup.BracketLowerIndex);
        Assert.Equal(2, lookup.BracketUpperIndex);
        Assert.Equal(0.6, lookup.InterpolationFraction, 12);
        Assert.Equal(80000.0, lookup.InputBurnupJPerKgHm, 12);
        Assert.Equal(table.TableId, lookup.TableId);
        Assert.Equal(table.MaterialVariantId, lookup.MaterialVariantId);
        Assert.Equal(table.UnitsProfileId, lookup.UnitsProfileId);
        Assert.True(table.Checksum.Equals(lookup.Checksum));

        BurnupCoefficientValuesV1 values = lookup.Coefficients;
        Assert.Equal(0.56, values.AbsorptionGroup1PerM, 12);
        Assert.Equal(0.46, values.AbsorptionGroup2PerM, 12);
        Assert.Equal(0.132, values.FissionGroup1PerM, 12);
        Assert.Equal(0.066, values.FissionGroup2PerM, 12);
        Assert.Equal(0.198, values.NuFissionGroup1PerM, 12);
        Assert.Equal(0.112, values.NuFissionGroup2PerM, 12);
        Assert.Equal(0.132, values.DownscatterGroup1To2PerM, 12);
        Assert.Equal(0.54, values.ChiGroup1, 12);
        Assert.Equal(0.46, values.ChiGroup2, 12);
        Assert.Equal(216.0, values.EnergyPerFissionJ, 12);
        Assert.Equal(1.0, values.ChiGroup1 + values.ChiGroup2, 12);
    }

    [Fact]
    public void SelectsExactRowsAndRejectsOutOfDomainBurnup()
    {
        BurnupCoefficientTableV1 table = CreateTable();

        ContractValidationResult<BurnupCoefficientLookupResultV1> first = table.TryLookup(0.0);
        AssertValid(first);
        Assert.Equal(0, first.Value.BracketLowerIndex);
        Assert.Equal(0, first.Value.BracketUpperIndex);
        Assert.Equal(0.0, first.Value.InterpolationFraction, 12);
        Assert.Equal(0.4, first.Value.Coefficients.AbsorptionGroup1PerM, 12);

        ContractValidationResult<BurnupCoefficientLookupResultV1> middle = table.TryLookup(50000.0);
        AssertValid(middle);
        Assert.Equal(1, middle.Value.BracketLowerIndex);
        Assert.Equal(1, middle.Value.BracketUpperIndex);
        Assert.Equal(0.0, middle.Value.InterpolationFraction, 12);
        Assert.Equal(0.6, middle.Value.Coefficients.ChiGroup1, 12);

        ContractValidationResult<BurnupCoefficientLookupResultV1> last = table.TryLookup(100000.0);
        AssertValid(last);
        Assert.Equal(2, last.Value.BracketLowerIndex);
        Assert.Equal(2, last.Value.BracketUpperIndex);
        Assert.Equal(1.0, last.Value.InterpolationFraction, 12);
        Assert.Equal(0.5, last.Value.Coefficients.ChiGroup1, 12);

        AssertInvalid(table.TryLookup(100000.0000001), "BurnupCoefficientLookup.OutOfRange");
        AssertInvalid(table.TryLookup(-1.0), "BurnupCoefficientLookup.Burnup.Invalid");
        AssertInvalid(table.TryLookup(double.NaN), "BurnupCoefficientLookup.Burnup.Invalid");
        AssertInvalid(table.TryLookup(double.PositiveInfinity), "BurnupCoefficientLookup.Burnup.Invalid");
    }

    [Fact]
    public void RejectsInvalidCoefficientRelationshipsAndTableMetadata()
    {
        ContractValidationResult<BurnupCoefficientValuesV1> negative =
            BurnupCoefficientValuesV1.TryCreate(
                0.4,
                0.3,
                0.1,
                0.05,
                0.15,
                0.08,
                -0.1,
                0.7,
                200.0);
        AssertInvalid(negative, "BurnupCoefficientValues.Negative");

        ContractValidationResult<BurnupCoefficientValuesV1> absorption =
            BurnupCoefficientValuesV1.TryCreate(
                0.09,
                0.3,
                0.1,
                0.05,
                0.15,
                0.08,
                0.1,
                0.7,
                200.0);
        AssertInvalid(absorption, "BurnupCoefficientValues.AbsorptionBelowFission");

        ContractValidationResult<BurnupCoefficientValuesV1> support =
            BurnupCoefficientValuesV1.TryCreate(
                0.4,
                0.3,
                0.1,
                0.05,
                0.0,
                0.08,
                0.1,
                0.7,
                200.0);
        AssertInvalid(support, "BurnupCoefficientValues.FissionSupportMismatch");

        ContractValidationResult<BurnupCoefficientValuesV1> chi =
            BurnupCoefficientValuesV1.TryCreate(
                0.4,
                0.3,
                0.1,
                0.05,
                0.15,
                0.08,
                0.1,
                1.1,
                200.0);
        AssertInvalid(chi, "BurnupCoefficientValues.Chi1.OutOfRange");

        BurnupCoefficientRowV1[] rows = CreateRows();
        ContractValidationResult<BurnupCoefficientTableV1> duplicate = CreateTableResult(
            new[] { rows[0], rows[1], CreateRow(50000.0, 1) });
        AssertInvalid(duplicate, "BurnupCoefficientTable.Knots.NotStrictlyIncreasing");

        ContractValidationResult<BurnupCoefficientTableV1> reordered = CreateTableResult(
            new[] { rows[0], rows[2], rows[1] });
        AssertInvalid(reordered, "BurnupCoefficientTable.Knots.NotStrictlyIncreasing");

        ContractValidationResult<BurnupCoefficientTableV1> missingChecksum =
            BurnupCoefficientTableV1.TryCreate(
                TableId,
                BurnupCoefficientTableV1.CurrentSchemaVersion,
                "synthetic-v1",
                new MaterialVariantId("MAT-SYN"),
                "SI-v1",
                "synthetic:P5-T05",
                null!,
                rows);
        AssertInvalid(missingChecksum, "BurnupCoefficientTable.Checksum.Missing");

        ContractValidationResult<BurnupCoefficientTableV1> privatePath =
            BurnupCoefficientTableV1.TryCreate(
                TableId,
                BurnupCoefficientTableV1.CurrentSchemaVersion,
                "synthetic-v1",
                new MaterialVariantId("MAT-SYN"),
                "SI-v1",
                "C:\\private\\listing.h5",
                CreateDigest(3),
                rows);
        AssertInvalid(privatePath, "BurnupCoefficientTable.SourceProvenance.Path");

        ContractValidationResult<BurnupCoefficientTableV1> posixPrivatePath =
            BurnupCoefficientTableV1.TryCreate(
                TableId,
                BurnupCoefficientTableV1.CurrentSchemaVersion,
                "synthetic-v1",
                new MaterialVariantId("MAT-SYN"),
                "SI-v1",
                "/home/user/private/listing.h5",
                CreateDigest(3),
                rows);
        AssertInvalid(posixPrivatePath, "BurnupCoefficientTable.SourceProvenance.Path");
    }

    [Fact]
    public void RejectsNegativeKnotsAndReturnsRepeatableLookupValues()
    {
        ContractValidationResult<BurnupCoefficientRowV1> negativeRow =
            BurnupCoefficientRowV1.TryCreate(-1.0, CreateValues(0));
        AssertInvalid(negativeRow, "BurnupCoefficientRow.Burnup.Invalid");

        BurnupCoefficientTableV1 table = CreateTable();
        ContractValidationResult<BurnupCoefficientLookupResultV1> first = table.TryLookup(80000.0);
        ContractValidationResult<BurnupCoefficientLookupResultV1> second = table.TryLookup(80000.0);
        AssertValid(first);
        AssertValid(second);
        Assert.Equal(first.Value.BracketLowerIndex, second.Value.BracketLowerIndex);
        Assert.Equal(first.Value.BracketUpperIndex, second.Value.BracketUpperIndex);
        Assert.Equal(first.Value.InterpolationFraction, second.Value.InterpolationFraction);
        Assert.Equal(first.Value.Coefficients.AbsorptionGroup1PerM, second.Value.Coefficients.AbsorptionGroup1PerM);
        Assert.Equal(first.Value.Coefficients.ChiGroup2, second.Value.Coefficients.ChiGroup2);
    }

    private static readonly StableId TableId =
        StableId.Parse("00000000-0000-0000-0000-0000000005a1");

    private static BurnupCoefficientTableV1 CreateTable()
    {
        ContractValidationResult<BurnupCoefficientTableV1> result = CreateTableResult(CreateRows());
        AssertValid(result);
        return result.Value;
    }

    private static ContractValidationResult<BurnupCoefficientTableV1> CreateTableResult(
        IEnumerable<BurnupCoefficientRowV1> rows)
    {
        return BurnupCoefficientTableV1.TryCreate(
            TableId,
            BurnupCoefficientTableV1.CurrentSchemaVersion,
            "synthetic-v1",
            new MaterialVariantId("MAT-SYN"),
            "SI-v1",
            "synthetic:P5-T05",
            CreateDigest(5),
            rows);
    }

    private static BurnupCoefficientRowV1[] CreateRows()
    {
        return new[]
        {
            CreateRow(0.0, 0),
            CreateRow(50000.0, 1),
            CreateRow(100000.0, 2)
        };
    }

    private static BurnupCoefficientRowV1 CreateRow(double burnup, int index)
    {
        ContractValidationResult<BurnupCoefficientRowV1> result =
            BurnupCoefficientRowV1.TryCreate(burnup, CreateValues(index));
        AssertValid(result);
        return result.Value;
    }

    private static BurnupCoefficientValuesV1 CreateValues(int index)
    {
        ContractValidationResult<BurnupCoefficientValuesV1> result =
            BurnupCoefficientValuesV1.TryCreate(
                0.4 + (0.1 * index),
                0.3 + (0.1 * index),
                0.1 + (0.02 * index),
                0.05 + (0.01 * index),
                0.15 + (0.03 * index),
                0.08 + (0.02 * index),
                0.1 + (0.02 * index),
                0.7 - (0.1 * index),
                200.0 + (10.0 * index));
        AssertValid(result);
        return result.Value;
    }

    private static Digest32 CreateDigest(byte marker)
    {
        byte[] bytes = new byte[32];
        bytes[0] = marker;
        return new Digest32(bytes);
    }

    private static void AssertValid<T>(ContractValidationResult<T> result)
    {
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
    }

    private static void AssertInvalid<T>(ContractValidationResult<T> result, string code)
    {
        Assert.False(result.IsValid);
        Assert.Equal(code, result.FirstDiagnostic.Code);
    }
}
