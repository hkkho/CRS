using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class SpatialToleranceRegressionTests
{
    [Theory]
    [InlineData(0.5)]
    [InlineData(0.8)]
    public void RelaxedInnerPolicyPreservesFullCorePowerAndReactivity(double fill)
    {
        using var stream = typeof(FullCoreDiffusionDataPackV1).Assembly.GetManifestResourceStream(
            FullCoreDiffusionDataPackV1.EmbeddedResourceName)!;
        using var reader = new StreamReader(stream);
        var json = JObject.Parse(reader.ReadToEnd());
        var current = Require(FullCoreDiffusionDataPackV1.TryLoadJson(json.ToString()));
        Assert.Equal(1e-7, current.LinearSolvePolicy.RelativeResidualTolerance);
        json["solver"]!["relative_residual_tolerance"] = 1e-8;
        var previous = Require(FullCoreDiffusionDataPackV1.TryLoadJson(json.ToString()));
        Assert.False(previous.Descriptor.ContentDigest.SequenceEqual(current.Descriptor.ContentDigest));
        var bundles = SyntheticGameCoreStateV1.CreatePractice().EnumerateBundles().ToArray();
        var mapping = Require(PracticeLiquidZoneRrsMappingV1.TryCreateCandu6());
        var overlay = Require(mapping.TryBuildOverlay(Enumerable.Repeat(fill, 14).ToArray()));
        var oldSolve = Require(Require(FullCoreDiffusionModelV1.TryCreateCandu6(previous))
            .TrySolve(bundles, overlay, 1e9));
        var newSolve = Require(Require(FullCoreDiffusionModelV1.TryCreateCandu6(current))
            .TrySolve(bundles, overlay, 1e9));
        Assert.InRange(Math.Abs(newSolve.Reactivity - oldSolve.Reactivity) * 1000, 0, 0.05);
        Assert.InRange(newSolve.PowerBalanceRelativeError, 0, current.ConvergencePolicy.PowerBalanceTolerance);
        double scale = oldSolve.NodePowerWatts.Max();
        for (int n = 0; n < newSolve.NodePowerWatts.Count; n++)
            Assert.InRange(Math.Abs(newSolve.NodePowerWatts[n] - oldSolve.NodePowerWatts[n]) / scale, 0, 1e-4);
    }

    private static T Require<T>(ContractValidationResult<T> result)
    {
        Assert.True(result.IsValid, result.IsValid ? "" : result.FirstDiagnostic.ToString());
        return result.Value;
    }
}
