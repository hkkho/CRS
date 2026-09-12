using System;
using System.Linq;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class P7T03IntegrationTests
{
    private static readonly double NegativeZero = BitConverter.Int64BitsToDouble(long.MinValue);
    private static readonly double[] ExpectedSchedule = { 0.125, 0.125, 0.06 };

    [Fact]
    public void ComputesTheApprovedFiveTermMinimumAndCeilRemainder()
    {
        IntegrationStabilityPolicyV1 policy = CreatePolicy();

        Assert.Equal(0.125, policy.AllowedDurationSeconds, 12);

        IntegrationSubstepScheduleV1 schedule = AssertValid(
            policy.TryPartition(0.31, 2.0, 4.0)).Value;

        Assert.Equal(3, schedule.Count);
        Assert.Equal(0.125, schedule[0], 12);
        Assert.Equal(0.125, schedule[1], 12);
        Assert.Equal(0.06, schedule[2], 12);
        Assert.Equal(ExpectedSchedule, schedule.ToArray());
    }

    [Fact]
    public void ValidatesDirectBoundsDimensionlessEnvelopesAndActiveRateCoverage()
    {
        IntegrationStabilityPolicyV1 policy = CreatePolicy();

        Assert.True(policy.TryValidateSubstep(0.125, 2.0, 4.0).IsValid);
        AssertInvalid(
            policy.TryValidateSubstep(0.126, 2.0, 4.0),
            "IntegrationStability.Substep.AllowedBoundExceeded");
        AssertInvalid(
            policy.TryValidateSubstep(0.1, 2.1, 4.0),
            "IntegrationStability.DecayRate.Uncovered");
        AssertInvalid(
            policy.TryValidateSubstep(0.1, 2.0, 4.1),
            "IntegrationStability.PromptRate.Uncovered");
        AssertInvalid(
            policy.TryValidateSubstep(0.1, NegativeZero, 4.0),
            "IntegrationStability.DecayRate.Invalid");
    }

    [Fact]
    public void RejectsMissingIdentityNoncanonicalPolicyValuesAndUnrepresentableBounds()
    {
        AssertInvalid(
            IntegrationStabilityPolicyV1.TryCreate(
                "",
                Digest(0x31),
                1.0,
                1.0,
                1.0,
                1.0,
                1.0,
                1.0),
            "IntegrationStabilityPolicy.Version.Empty");
        AssertInvalid(
            IntegrationStabilityPolicyV1.TryCreate(
                "synthetic",
                null!,
                1.0,
                1.0,
                1.0,
                1.0,
                1.0,
                1.0),
            "IntegrationStabilityPolicy.Digest.Missing");
        AssertInvalid(
            IntegrationStabilityPolicyV1.TryCreate(
                "synthetic",
                Digest(0x32),
                NegativeZero,
                1.0,
                1.0,
                1.0,
                1.0,
                1.0),
            "IntegrationStabilityPolicy.MaximumKineticStep.Invalid");
        AssertInvalid(
            IntegrationStabilityPolicyV1.TryCreate(
                "synthetic",
                Digest(0x33),
                1.0,
                1.0,
                1.0,
                double.Epsilon,
                1.0,
                double.MaxValue),
            "IntegrationStabilityPolicy.DecayBound.Invalid");
    }

    [Fact]
    public void FailsClosedWhenGapSubdivisionCannotBeRepresented()
    {
        IntegrationStabilityPolicyV1 policy = AssertValid(
            IntegrationStabilityPolicyV1.TryCreate(
                "synthetic-small-step",
                Digest(0x34),
                1.0e-12,
                2.0e-12,
                3.0e-12,
                1.0,
                1.0,
                1.0)).Value;

        AssertInvalid(
            policy.TryPartition(1.0, 1.0, 1.0),
            "IntegrationStability.Partition.Count.Invalid");
        AssertInvalid(
            policy.TryPartition(NegativeZero, 1.0, 1.0),
            "IntegrationStability.Partition.Gap.Invalid");
        AssertInvalid(
            policy.TryPartition(double.MaxValue, 1.0, 1.0),
            "IntegrationStability.Partition.Count.Invalid");
    }

    [Fact]
    public void KeepsPolicyIdentityAndScheduleReadOnlyDeterministic()
    {
        IntegrationStabilityPolicyV1 first = CreatePolicy();
        IntegrationStabilityPolicyV1 second = CreatePolicy();
        IntegrationSubstepScheduleV1 firstSchedule = AssertValid(
            first.TryPartition(0.31, 2.0, 4.0)).Value;
        IntegrationSubstepScheduleV1 secondSchedule = AssertValid(
            second.TryPartition(0.31, 2.0, 4.0)).Value;

        Assert.Equal("synthetic-phase7-policy-v1", first.PolicyVersion);
        Assert.Equal(first.Digest, second.Digest);
        Assert.Equal(first.AllowedDurationSeconds, second.AllowedDurationSeconds, 12);
        Assert.Equal(firstSchedule.ToArray(), secondSchedule.ToArray());
        Assert.Equal(0.31, firstSchedule.Sum(), 12);
        Assert.Throws<ArgumentOutOfRangeException>(() => firstSchedule[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => firstSchedule[firstSchedule.Count]);
    }

    private static IntegrationStabilityPolicyV1 CreatePolicy()
    {
        return AssertValid(
            IntegrationStabilityPolicyV1.TryCreate(
                "synthetic-phase7-policy-v1",
                Digest(0x30),
                0.5,
                0.75,
                0.25,
                2.0,
                4.0,
                0.5)).Value;
    }

    private static Digest32 Digest(byte value)
    {
        return new Digest32(new[]
        {
            value, value, value, value, value, value, value, value,
            value, value, value, value, value, value, value, value,
            value, value, value, value, value, value, value, value,
            value, value, value, value, value, value, value, value
        });
    }

    private static ContractValidationResult<T> AssertValid<T>(
        ContractValidationResult<T> result)
    {
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        return result;
    }

    private static void AssertInvalid<T>(
        ContractValidationResult<T> result,
        string code)
    {
        Assert.False(result.IsValid);
        Assert.Equal(code, result.FirstDiagnostic.Code);
    }
}
