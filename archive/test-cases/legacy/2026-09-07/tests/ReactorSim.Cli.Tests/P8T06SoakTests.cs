using System;
using System.Collections.Generic;
using System.Linq;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Cli.Tests;

public sealed class P8T06SoakTests
{
    [Fact]
    public void LongRunSoakMaintainsInvariantsAndIsRepeatable()
    {
        ContractValidationResult<Phase8BaselinePolicySoakResultV1> first =
            CliApplication.RunBaselinePolicySoak();
        ContractValidationResult<Phase8BaselinePolicySoakResultV1> second =
            CliApplication.RunBaselinePolicySoak();

        Assert.True(
            first.IsValid,
            first.IsValid ? string.Empty : first.FirstDiagnostic.ToString());
        Assert.True(
            second.IsValid,
            second.IsValid ? string.Empty : second.FirstDiagnostic.ToString());
        Assert.Equal(Phase8SoakPlanV1.PlanId, first.Value.PlanId);
        Assert.Equal(Phase8SoakPlanV1.CyclesPerPolicy, first.Value.CyclesPerPolicy);
        Assert.Equal(
            Phase8SoakPlanV1.PolicyIds.Count * Phase8SoakPlanV1.CyclesPerPolicy,
            first.Value.Cycles.Count);
        Assert.True(first.Value.InvariantSampleCount > 0);
        Assert.Equal(first.Value.AggregateDigest, second.Value.AggregateDigest);
        Assert.Equal(first.Value.InvariantSampleCount, second.Value.InvariantSampleCount);
        Assert.Equal(first.Value.TotalSimulationTimeSeconds, second.Value.TotalSimulationTimeSeconds);
        Assert.Equal(first.Value.TotalWallElapsedSeconds, second.Value.TotalWallElapsedSeconds);
        Assert.Equal(first.Value.TotalLossCount, second.Value.TotalLossCount);

        Assert.Equal(
            first.Value.Cycles.Select(ToCycleFingerprint),
            second.Value.Cycles.Select(ToCycleFingerprint));
    }

    [Fact]
    public void LongRunSoakCoversEveryApprovedPolicyForEveryFreshCycle()
    {
        ContractValidationResult<Phase8BaselinePolicySoakResultV1> result =
            CliApplication.RunBaselinePolicySoak();

        Assert.True(
            result.IsValid,
            result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        var expectedPolicyIds = new HashSet<string>(
            Phase8SoakPlanV1.PolicyIds,
            StringComparer.Ordinal);
        Assert.True(
            expectedPolicyIds.SetEquals(result.Value.Cycles.Select(item => item.PolicyId)),
            "The soak result policy set does not match the approved P8-T05 policy set.");
        Assert.All(
            result.Value.Cycles.GroupBy(item => item.PolicyId),
            group =>
            {
                Assert.Equal(Phase8SoakPlanV1.CyclesPerPolicy, group.Count());
                Assert.Equal(
                    Enumerable.Range(0, Phase8SoakPlanV1.CyclesPerPolicy),
                    group.Select(item => item.CycleIndex).OrderBy(index => index));
                Assert.All(group, cycle =>
                {
                    Assert.NotEqual(0ul, cycle.ReplaySeed);
                    Assert.NotEmpty(cycle.ReplayDigest);
                    Assert.True(cycle.InvariantSampleCount > 0);
                });
            });
    }

    private static string ToCycleFingerprint(Phase8BaselinePolicySoakCycleResultV1 cycle)
    {
        return string.Join(
            "|",
            cycle.PolicyId,
            cycle.CycleIndex,
            cycle.ReplaySeed,
            cycle.Outcome,
            cycle.SimulationTimeSeconds.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            cycle.WallElapsedSeconds.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            cycle.ScoreTotal.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            cycle.LossCount,
            cycle.TurnSummaryCount,
            cycle.InvariantSampleCount,
            cycle.ReplayDigest);
    }
}
