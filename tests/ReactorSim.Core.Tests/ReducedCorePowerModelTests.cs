using System.Collections.Generic;
using System.Linq;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class ReducedCorePowerModelTests
{
    [Fact]
    public void ProjectionIsDeterministicNormalizedAndDerivesRhoFromK()
    {
        List<ReducedPowerNodeInputV1> inputs = CreateInputs(6.0);
        const double referencePowerWatts = 1_000_000.0;

        ContractValidationResult<ReducedCorePowerProjectionV1> first =
            ReducedCorePowerModelV1.TryProject(inputs, referencePowerWatts, 0.75);
        ContractValidationResult<ReducedCorePowerProjectionV1> second =
            ReducedCorePowerModelV1.TryProject(inputs, referencePowerWatts, 0.75);

        Assert.True(first.IsValid, first.IsValid ? string.Empty : first.FirstDiagnostic.ToString());
        Assert.True(second.IsValid, second.IsValid ? string.Empty : second.FirstDiagnostic.ToString());
        Assert.Equal(first.Value.TotalPowerWatts, second.Value.TotalPowerWatts, 12);
        Assert.Equal(first.Value.EffectiveK, second.Value.EffectiveK, 12);
        Assert.Equal(first.Value.Reactivity, second.Value.Reactivity, 12);
        Assert.Equal(referencePowerWatts * 0.75, first.Value.TotalPowerWatts, 10);
        Assert.Equal(
            first.Value.EffectiveK,
            1.0 / (1.0 - first.Value.Reactivity),
            12);
        Assert.InRange(first.Value.PowerBalanceRelativeError, 0.0, 1e-12);
        Assert.Equal(inputs.Count, first.Value.Nodes.Count);
        Assert.Equal(
            first.Value.TotalPowerWatts,
            first.Value.Nodes.Sum(node => node.PowerWatts),
            10);
        Assert.All(first.Value.Nodes, node =>
        {
            Assert.True(node.PowerWatts >= 0.0);
            Assert.True(node.ShapeFraction > 0.0);
        });
    }

    [Fact]
    public void ProjectionChangesWhenBundleStateChangesWithoutPerBundleReactivity()
    {
        List<ReducedPowerNodeInputV1> baselineInputs = CreateInputs(6.0);
        List<ReducedPowerNodeInputV1> changedInputs = CreateInputs(18.0);

        ReducedCorePowerProjectionV1 baseline = Require(
            ReducedCorePowerModelV1.TryProject(baselineInputs, 1_000_000.0, 1.0));
        ReducedCorePowerProjectionV1 changed = Require(
            ReducedCorePowerModelV1.TryProject(changedInputs, 1_000_000.0, 1.0));

        Assert.NotEqual(baseline.EffectiveK, changed.EffectiveK);
        Assert.NotEqual(baseline.Reactivity, changed.Reactivity);
        Assert.NotEqual(baseline.Nodes[0].ShapeFraction, changed.Nodes[0].ShapeFraction);
        Assert.Equal(baseline.TotalPowerWatts, changed.TotalPowerWatts, 10);
    }

    [Fact]
    public void ProjectionRejectsDuplicateNodes()
    {
        ReducedPowerNodeInputV1 input = CreateInputs(6.0)[0];

        ContractValidationResult<ReducedCorePowerProjectionV1> result =
            ReducedCorePowerModelV1.TryProject(
                new[] { input, input },
                1_000_000.0,
                1.0);

        Assert.False(result.IsValid);
        Assert.Equal("ReducedPowerProjection.Node.Duplicate", result.FirstDiagnostic.Code);
    }

    private static List<ReducedPowerNodeInputV1> CreateInputs(double burnup)
    {
        var result = new List<ReducedPowerNodeInputV1>();
        for (uint channel = 0; channel < 3; channel++)
        {
            for (uint position = 0; position < 2; position++)
            {
                ContractValidationResult<ReducedPowerNodeInputV1> input =
                    ReducedPowerNodeInputV1.TryCreate(
                        new NodeKey(new ChannelId(channel), new BundlePosition(position)),
                        channel / 2.0,
                        position,
                        burnup + channel + position,
                        burnup + channel,
                        burnup + channel + 0.5,
                        channel % 2 == 0 ? 1 : -1,
                        1.0);
                Assert.True(input.IsValid, input.IsValid ? string.Empty : input.FirstDiagnostic.ToString());
                result.Add(input.Value);
            }
        }

        return result;
    }

    private static T Require<T>(ContractValidationResult<T> result)
    {
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        return result.Value;
    }
}
