using System;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class DiffusionDataPackAuditTests
{
    [Fact]
    public void LiteratureGeometryUsesMetricCellVolumeAndConsistentInteriorDiffusion()
    {
        var pack = Require(FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6());
        const double pitch = .28575, length = .4953;
        Assert.Equal(pitch * pitch * length, pack.NodeVolumeM3, 14);
        Assert.Equal(.04, pack.AxialConductance.Group1M2 * length / (pitch * pitch), 12);
        Assert.Equal(.04, pack.TransverseConductance.Group1M2 / length, 12);
        Assert.Equal(.02, pack.AxialConductance.Group2M2 * length / (pitch * pitch), 12);
        Assert.Equal(.02, pack.TransverseConductance.Group2M2 / length, 12);
    }

    private static readonly double[] ExpectedBurnupKnotsMwDayPerKgHm =
        { 0, .1598565, .319713, .639426, .959139, 1.278852, 1.598565,
          2.237991, 2.877417, 3.516843, 4.156269, 4.795695, 6.39426,
          7.992825, 9.59139, 9.784587378640776, 11.74150485436893, 15, 20, 25, 30 };

    [Fact]
    public void EmbeddedPackAuditClassifiesCurrentSyntheticRepresentation()
    {
        FullCoreDiffusionDataPackV1 pack = Require(
            FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6());
        DiffusionDataPackAuditV1 audit = Require(
            DiffusionDataPackAuditV1.TryAudit(pack));
        DiffusionCoefficientTableAuditV1 table = Assert.Single(audit.Tables);

        Assert.Equal(ExpectedBurnupKnotsMwDayPerKgHm.Length, table.BurnupKnotsMwDayPerKgHm.Count);
        for (int index = 0; index < ExpectedBurnupKnotsMwDayPerKgHm.Length; index++)
            Assert.Equal(ExpectedBurnupKnotsMwDayPerKgHm[index], table.BurnupKnotsMwDayPerKgHm[index], 12);
        Assert.Equal(
            30.0 * InfiniteCellDiffusionModelV1.JoulesPerMegaWattDayPerKilogramHm,
            table.BurnupKnotsJPerKgHm[table.BurnupKnotsJPerKgHm.Count - 1],
            6);

        AssertClassification(
            table,
            DiffusionAuditFieldV1.D1,
            CoefficientAuditClassificationV1.IndirectConstant);
        AssertClassification(
            table,
            DiffusionAuditFieldV1.D2,
            CoefficientAuditClassificationV1.IndirectConstant);

        DiffusionAuditFieldV1[] explicitConstants =
        {
            DiffusionAuditFieldV1.SigmaA1,
            DiffusionAuditFieldV1.SigmaA2,
            DiffusionAuditFieldV1.SigmaS1To2,
            DiffusionAuditFieldV1.SigmaF1,
            DiffusionAuditFieldV1.NuSigmaF1,
            DiffusionAuditFieldV1.Chi1,
            DiffusionAuditFieldV1.EnergyPerFission
        };
        foreach (DiffusionAuditFieldV1 field in explicitConstants)
        {
            AssertClassification(
                table,
                field,
                CoefficientAuditClassificationV1.ExplicitConstant);
        }

        AssertClassification(
            table,
            DiffusionAuditFieldV1.SigmaF2,
            CoefficientAuditClassificationV1.ExplicitBurnupDependent);
        AssertClassification(
            table,
            DiffusionAuditFieldV1.NuSigmaF2,
            CoefficientAuditClassificationV1.ExplicitBurnupDependent);
        AssertClassification(
            table,
            DiffusionAuditFieldV1.Chi2,
            CoefficientAuditClassificationV1.Derived);

        DiffusionPackAuditFindingV1 xenonBasis = audit.GetFinding(
            DiffusionAuditFieldV1.XenonBasis);
        Assert.Equal(CoefficientAuditClassificationV1.ExplicitBurnupDependent, xenonBasis.Classification);
        Assert.True(xenonBasis.IsPackLevel);
        Assert.Contains("burnup-bound Xe-135 reference", xenonBasis.Message, StringComparison.OrdinalIgnoreCase);

        Assert.Equal("synthetic-calibrated", pack.EvidenceClass);
        Assert.True(audit.IsSyntheticOrProvisional);
        Assert.True(audit.IsCurrentGameplayAdmissible);
        Assert.True(audit.RequiresReplacementForFinalCalibratedPhysics);
        Assert.False(audit.IsFinalCalibratedPhysicsReady);
        Assert.All(audit.Checks, check => Assert.True(check.Passed, check.Message));
    }

    [Fact]
    public void EmbeddedPackAuditProvesExactKnotsInterpolationAndClosedDomain()
    {
        FullCoreDiffusionDataPackV1 pack = Require(
            FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6());
        BurnupCoefficientTableV1 table = Assert.Single(pack.CoefficientTables);

        for (int index = 0; index < table.Rows.Count; index++)
        {
            ContractValidationResult<BurnupCoefficientLookupResultV1> lookup =
                table.TryLookup(table.Rows[index].BurnupJPerKgHm);

            Assert.True(lookup.IsValid, lookup.IsValid ? string.Empty : lookup.FirstDiagnostic.ToString());
            Assert.Equal(index, lookup.Value.BracketLowerIndex);
            Assert.Equal(index, lookup.Value.BracketUpperIndex);
            Assert.Equal(
                table.Rows[index].Coefficients.FissionGroup2PerM,
                lookup.Value.Coefficients.FissionGroup2PerM,
                14);
            Assert.Equal(
                table.Rows[index].Coefficients.ChiGroup2,
                1.0 - lookup.Value.Coefficients.ChiGroup1,
                14);
        }

        double midpoint = table.Rows[1].BurnupJPerKgHm +
                          ((table.Rows[2].BurnupJPerKgHm - table.Rows[1].BurnupJPerKgHm) * 0.5);
        ContractValidationResult<BurnupCoefficientLookupResultV1> interpolated =
            table.TryLookup(midpoint);
        Assert.True(interpolated.IsValid, interpolated.IsValid ? string.Empty : interpolated.FirstDiagnostic.ToString());
        Assert.Equal(1, interpolated.Value.BracketLowerIndex);
        Assert.Equal(2, interpolated.Value.BracketUpperIndex);
        Assert.Equal(0.5, interpolated.Value.InterpolationFraction, 14);
        Assert.Equal(
            (table.Rows[1].Coefficients.FissionGroup2PerM +
             table.Rows[2].Coefficients.FissionGroup2PerM) * 0.5,
            interpolated.Value.Coefficients.FissionGroup2PerM,
            14);
        Assert.Equal(
            (table.Rows[1].Coefficients.NuFissionGroup2PerM +
             table.Rows[2].Coefficients.NuFissionGroup2PerM) * 0.5,
            interpolated.Value.Coefficients.NuFissionGroup2PerM,
            14);

        Assert.False(table.TryLookup(table.Rows[0].BurnupJPerKgHm - 1.0).IsValid);
        Assert.False(table.TryLookup(table.Rows[^1].BurnupJPerKgHm + 1.0).IsValid);
    }

    private static void AssertClassification(
        DiffusionCoefficientTableAuditV1 table,
        DiffusionAuditFieldV1 field,
        CoefficientAuditClassificationV1 expected)
    {
        Assert.Equal(expected, table.GetFinding(field).Classification);
    }

    private static T Require<T>(ContractValidationResult<T> result)
    {
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        return result.Value;
    }
}
