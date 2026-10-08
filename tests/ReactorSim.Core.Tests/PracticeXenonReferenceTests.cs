using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class PracticeXenonReferenceTests
{
    [Fact]
    public void FreshFuelHasNoIncludedOrLiveXenonAndRetainedReferenceMovesWithBurnup()
    {
        var core = SyntheticGameCoreStateV1.CreateAgedPractice(1001);
        var model = Require(FullCoreDiffusionModelV1.TryCreateCandu6(Require(FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6())));
        var solver = Require(EquilibriumCoreSolverV1.TryCreate(model, core.EnumerateBundles(), 2_064_000_000));
        var poison = PracticeXenonStateV1.CreateEquilibrium(core, solver.CurrentProjection, 0);
        var old = core.EnumerateBundles().Select((b, n) => (b.BundleId,
            I: poison.Iodine[n], X: poison.Xenon[n], R: poison.IncludedReferenceXenon[n])).ToDictionary(b => b.BundleId);
        var moved = Require(core.TryRefuel(210, GameRefuellingDirectionV1.TowardEndA, 8, "NAT-U-SYNTHETIC", 0)).ResultingState;
        var rebound = poison.Rebind(moved);
        int n = 0, fresh = 0;
        foreach (var bundle in moved.EnumerateBundles())
        {
            if (old.TryGetValue(bundle.BundleId, out var retained))
            {
                Assert.Equal(retained.I, rebound.Iodine[n]);
                Assert.Equal(retained.X, rebound.Xenon[n]);
                Assert.Equal(retained.R, rebound.IncludedReferenceXenon[n]);
            }
            else
            {
                fresh++;
                Assert.Equal(0, rebound.Iodine[n]);
                Assert.Equal(0, rebound.Xenon[n]);
                Assert.Equal(0, rebound.IncludedReferenceXenon[n]);
                var node = new NodeKey(bundle.ChannelId, bundle.Position);
                Assert.Equal(0, rebound.Overlay.GetDeltaAbsorptionGroup2PerM(node));
            }
            n++;
        }
        Assert.Equal(8, fresh);
    }

    [Fact]
    public void FrozenActualXenonSurvivesBurnupWhileTheIncludedReferenceUpdates()
    {
        var core = SyntheticGameCoreStateV1.CreateAgedPractice(1001);
        var pack = Require(FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6());
        Assert.Equal(XenonBasisV1.Included, pack.DeclaredXenonBasis);
        Assert.NotNull(pack.XenonReference);
        var solver = Require(EquilibriumCoreSolverV1.TryCreate(Require(FullCoreDiffusionModelV1.TryCreateCandu6(pack)),
            core.EnumerateBundles(), 2_064_000_000));
        var poison = PracticeXenonStateV1.CreateEquilibrium(core, solver.CurrentProjection, 0);
        var burned = Require(core.TryAddFissionEnergy(solver.CurrentProjection.ShapeNodePowerWatts.Select(p => p * 86400).ToArray()));
        var bound = poison.BindBurnupReference(burned);
        Assert.Equal(poison.Iodine, bound.Iodine);
        Assert.Equal(poison.Xenon, bound.Xenon);
        Assert.Equal(poison.SimulationTimeSeconds, bound.SimulationTimeSeconds);
        Assert.NotEqual(poison.StateDigest, bound.StateDigest);
        Assert.Contains(poison.IncludedReferenceXenon.Zip(bound.IncludedReferenceXenon, (a, b) => a != b), changed => changed);
        int n = 0;
        foreach (var bundle in burned.EnumerateBundles())
            Assert.Equal(pack.XenonReference!.At(bundle.CurrentBurnupJPerKgHm), bound.IncludedReferenceXenon[n++]);
        Assert.Throws<ArgumentOutOfRangeException>(() => pack.XenonReference!.At(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => pack.XenonReference!.At(30 * 8.64e10 + 1));
    }

    [Fact]
    public void ADeclaredReferenceCannotAssignPoisonToFreshFuelOrUseDifferentBurnupKnots()
    {
        using var stream = typeof(FullCoreDiffusionDataPackV1).Assembly.GetManifestResourceStream(
            FullCoreDiffusionDataPackV1.EmbeddedResourceName)!;
        using var reader = new StreamReader(stream);
        var root = JObject.Parse(reader.ReadToEnd());
        root["xenon_reference"]!["rows"]![0]!["xe135_number_density_m3"] = 1e10;
        var result = FullCoreDiffusionDataPackV1.TryLoadJson(root.ToString());
        Assert.False(result.IsValid);
        Assert.Equal("FullCoreDiffusionDataPack.XenonReference.Fresh.Invalid", result.FirstDiagnostic.Code);
        root["xenon_reference"]!["rows"]![0]!["xe135_number_density_m3"] = 0;
        root["xenon_reference"]!["rows"]![1]!["burnup_j_per_kg_hm"] = 1;
        result = FullCoreDiffusionDataPackV1.TryLoadJson(root.ToString());
        Assert.False(result.IsValid);
        Assert.Equal("FullCoreDiffusionDataPack.XenonReference.Row.Invalid", result.FirstDiagnostic.Code);
    }
    private static T Require<T>(ContractValidationResult<T> result) => result.IsValid ? result.Value :
        throw new InvalidOperationException(result.FirstDiagnostic.ToString());
}
