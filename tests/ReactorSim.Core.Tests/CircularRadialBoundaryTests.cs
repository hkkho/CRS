using Newtonsoft.Json.Linq;
using ReactorSim.Core;
using ReactorSim.TestInfrastructure;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class CircularRadialBoundaryTests
{
    private static JObject Source() => JObject.Parse(File.ReadAllText(TestDataLocator.RequireRepositoryFile(
        "data/calibration/radial-circle-v9/source-pack.json")));

    private static JObject Declaration(double distance = .65) => new()
    {
        ["condition_id"] = CircularRadialBoundary.ConditionId,
        ["pitch_m"] = .28575,
        ["core_radius_m"] = 3.35,
        ["extrapolation_distance_m"] = distance
    };

    [Fact]
    public void FaceRaysIntersectOneCircleAndAxialConductanceIsUnchanged()
    {
        var json = Source();
        var old = Require(FullCoreDiffusionDataPackV1.TryLoadJson(json.ToString()));
        json["geometry"]!["radial_boundary"] = Declaration();
        var pack = Require(FullCoreDiffusionDataPackV1.TryLoadJson(json.ToString()));
        Assert.Equal(old.AxialVacuumBoundaryConductance.Group1M2, pack.AxialVacuumBoundaryConductance.Group1M2);
        Assert.Equal(old.AxialVacuumBoundaryConductance.Group2M2, pack.AxialVacuumBoundaryConductance.Group2M2);
        var model = Require(FullCoreDiffusionModelV1.TryCreateCandu6(pack));
        var coefficients = Require(model.TryPrepareSolve(SyntheticGameCoreStateV1.CreateAgedPractice(1001).EnumerateBundles())).BaseCoefficients;
        var circle = pack.RadialBoundary!;
        var distances = new List<double>();
        foreach (var node in model.Stencil.Nodes)
            foreach (var term in node.BoundaryTerms)
            {
                Assert.True(coefficients.TryGetBoundary(new SpatialBoundaryKey(node.Node, term.Face), out var c));
                if (term.Face is TopologyFace.EndA or TopologyFace.EndB)
                {
                    Assert.Equal(pack.AxialVacuumBoundaryConductance.Group1M2, c.Group1);
                    continue;
                }
                var channel = model.Stencil.Topology.GetChannel(node.Node.ChannelId);
                double x = (channel.CoordinateX - 10.5) * circle.PitchM;
                double y = (channel.CoordinateY - 10.5) * circle.PitchM;
                double distance = pack.TransverseConductance.Group1M2 * circle.PitchM / c.Group1;
                distances.Add(distance);
                if (term.Face == TopologyFace.East) x += distance;
                if (term.Face == TopologyFace.West) x -= distance;
                if (term.Face == TopologyFace.North) y += distance;
                if (term.Face == TopologyFace.South) y -= distance;
                Assert.Equal(circle.ExtrapolatedRadiusM, Math.Sqrt(x * x + y * y), 12);
                Assert.Equal(pack.TransverseConductance.Group2M2 * circle.PitchM / distance, c.Group2, 14);
            }
        Assert.True(distances.Max() - distances.Min() > .1);
        Assert.Null(old.RadialBoundary);
    }

    [Theory]
    [InlineData("extrapolation_distance_m", 0)]
    [InlineData("extrapolation_distance_m", -1)]
    [InlineData("core_radius_m", 3)]
    [InlineData("pitch_m", 0)]
    [InlineData("core_radius_m", 1e308)]
    public void InvalidCircularGeometryFailsClosed(string field, double value)
    {
        var json = Source();
        var radial = Declaration();
        radial[field] = value;
        json["geometry"]!["radial_boundary"] = radial;
        var result = FullCoreDiffusionDataPackV1.TryLoadJson(json.ToString());
        Assert.False(result.IsValid);
        Assert.Equal("FullCoreDiffusionDataPack.RadialBoundary.Invalid", result.FirstDiagnostic.Code);
    }

    [Fact]
    public void RadialReflectiveOverrideStillHasExactlyZeroLeakage()
    {
        var json = Source();
        json["geometry"]!["radial_boundary"] = Declaration();
        var pack = Require(FullCoreDiffusionDataPackV1.TryLoadJson(json.ToString()));
        var topology = Candu6CoreTopologyFactoryV1.Create();
        var boundary = topology.Channels.SelectMany(c => c.BoundaryFaces)
            .First(b => b.Face is not TopologyFace.EndA and not TopologyFace.EndB);
        var node = new NodeKey(boundary.ChannelId, boundary.Position);
        var reflected = Require(Candu6CoreTopologyFactoryV1.TryCreate(new[] { new ReflectiveFaceOverrideV1(node, boundary.Face) }));
        var model = Require(FullCoreDiffusionModelV1.TryCreate(pack, reflected, Require(SpatialStencil.TryCreate(reflected))));
        var coefficients = Require(model.TryPrepareSolve(SyntheticGameCoreStateV1.CreateAgedPractice().EnumerateBundles())).BaseCoefficients;
        Assert.True(coefficients.TryGetBoundary(new SpatialBoundaryKey(node, boundary.Face), out var c));
        Assert.Equal(0, c.Group1);
        Assert.Equal(0, c.Group2);
    }

    private static T Require<T>(ContractValidationResult<T> result) => result.IsValid ? result.Value : throw new InvalidOperationException(result.FirstDiagnostic.ToString());
}
