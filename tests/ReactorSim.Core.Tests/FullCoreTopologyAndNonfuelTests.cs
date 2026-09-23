using System;
using System.Collections.Generic;
using System.Linq;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class FullCoreTopologyAndNonfuelTests
{
    [Fact]
    public void ReflectiveInteriorOverrideRemovesSharedEdgeAndMirrorsBothFaces()
    {
        CoreTopology baseline = Require(Candu6CoreTopologyFactoryV1.TryCreate());
        NeighborRecord relation = baseline.Channels
            .SelectMany(channel => channel.Neighbors)
            .First(neighbor => IsCardinal(neighbor.Direction));
        NodeKey source = new NodeKey(relation.SourceChannelId, relation.SourcePosition);
        NodeKey target = new NodeKey(relation.TargetChannelId, relation.TargetPosition);
        TopologyFace sourceFace = (TopologyFace)(byte)relation.Direction;
        TopologyFace targetFace = Inverse(sourceFace);

        CoreTopology reflected = Require(
            Candu6CoreTopologyFactoryV1.TryCreate(
                new[] { new ReflectiveFaceOverrideV1(source, sourceFace) }));

        ChannelTopology reflectedSource = reflected.GetChannel(source.ChannelId);
        ChannelTopology reflectedTarget = reflected.GetChannel(target.ChannelId);
        Assert.DoesNotContain(
            reflectedSource.Neighbors,
            candidate => candidate.SourcePosition == source.Position &&
                         candidate.Direction == (NeighborDirection)(byte)sourceFace);
        Assert.DoesNotContain(
            reflectedTarget.Neighbors,
            candidate => candidate.SourcePosition == target.Position &&
                         candidate.Direction == (NeighborDirection)(byte)targetFace);
        Assert.Contains(
            reflectedSource.BoundaryFaces,
            boundary => boundary.Position == source.Position &&
                        boundary.Face == sourceFace &&
                        boundary.Classification == BoundaryClassification.Reflective);
        Assert.Contains(
            reflectedTarget.BoundaryFaces,
            boundary => boundary.Position == target.Position &&
                        boundary.Face == targetFace &&
                        boundary.Classification == BoundaryClassification.Reflective);

        SpatialStencil reflectedStencil = Require(SpatialStencil.TryCreate(reflected));
        Assert.DoesNotContain(
            reflectedStencil.Nodes[reflected.GetFlatIndex(source)].NeighborTerms,
            neighbor => neighbor.TargetNode == target);
        Assert.DoesNotContain(
            reflectedStencil.Nodes[reflected.GetFlatIndex(target)].NeighborTerms,
            neighbor => neighbor.TargetNode == source);
    }

    [Fact]
    public void ReflectiveOuterOverrideChangesVacuumConductanceAndInputOrderIsStable()
    {
        CoreTopology baseline = Require(Candu6CoreTopologyFactoryV1.TryCreate());
        BoundaryFaceRecord vacuum = baseline.Channels
            .SelectMany(channel => channel.BoundaryFaces)
            .First(boundary => boundary.Classification == BoundaryClassification.Vacuum);
        NodeKey node = new NodeKey(vacuum.ChannelId, vacuum.Position);
        var outerOverride = new ReflectiveFaceOverrideV1(node, vacuum.Face);

        CoreTopology reflected = Require(
            Candu6CoreTopologyFactoryV1.TryCreate(new[] { outerOverride }));
        BoundaryFaceRecord changed = reflected.GetChannel(vacuum.ChannelId).BoundaryFaces
            .Single(boundary => boundary.Position == vacuum.Position &&
                                boundary.Face == vacuum.Face);
        Assert.Equal(BoundaryClassification.Reflective, changed.Classification);

        FullCoreDiffusionDataPackV1 pack = Require(
            FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6());
        SyntheticGameCoreStateV1 state = SyntheticGameCoreStateV1.CreatePractice();
        FullCoreDiffusionModelV1 baselineModel = Require(
            FullCoreDiffusionModelV1.TryCreateCandu6(pack));
        FullCoreDiffusionModelV1 reflectedModel = Require(
            FullCoreDiffusionModelV1.TryCreate(
                pack,
                reflected,
                Require(SpatialStencil.TryCreate(reflected))));
        FullCoreDiffusionPreparedSolveV1 baselinePrepared = Require(
            baselineModel.TryPrepareSolve(state.EnumerateBundles()));
        FullCoreDiffusionPreparedSolveV1 reflectedPrepared = Require(
            reflectedModel.TryPrepareSolve(state.EnumerateBundles()));
        SpatialBoundaryKey boundaryKey = new SpatialBoundaryKey(node, vacuum.Face);
        Assert.True(baselinePrepared.BaseCoefficients.TryGetBoundary(
            boundaryKey,
            out SpatialConductancePair baselineConductance));
        Assert.True(reflectedPrepared.BaseCoefficients.TryGetBoundary(
            boundaryKey,
            out SpatialConductancePair reflectedConductance));
        Assert.True(baselineConductance.Group1 > 0.0);
        Assert.True(baselineConductance.Group2 > 0.0);
        Assert.Equal(0.0, reflectedConductance.Group1, 14);
        Assert.Equal(0.0, reflectedConductance.Group2, 14);
    }

    [Fact]
    public void FullCoreNonfuelNodeRetainsBundleAndHasZeroLocalPower()
    {
        FullCoreDiffusionDataPackV1 pack = Require(
            FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6());
        SyntheticGameCoreStateV1 state = SyntheticGameCoreStateV1.CreatePractice();
        NodeKey nonfuel = new NodeKey(new ChannelId(0), new BundlePosition(0));
        FullCoreDiffusionModelV1 model = Require(
            FullCoreDiffusionModelV1.TryCreateCandu6(pack, new[] { nonfuel }));

        Assert.Single(model.NonfuelNodes);
        Assert.Equal(nonfuel, Assert.Single(model.NonfuelNodes));
        Assert.True(model.IsNonfuel(nonfuel));
        Assert.False(model.IsNonfuel(new NodeKey(new ChannelId(0), new BundlePosition(1))));

        FullCoreDiffusionPreparedSolveV1 prepared = Require(
            model.TryPrepareSolve(state.EnumerateBundles()));
        int flatIndex = model.Topology.GetFlatIndex(nonfuel);
        SpatialNodeCoefficients moderator = prepared.BaseCoefficients.Nodes[flatIndex];
        Assert.Equal(0.025, moderator.AbsorptionGroup1PerM, 14);
        Assert.Equal(0.012, moderator.AbsorptionGroup2PerM, 14);
        Assert.Equal(0.080, moderator.DownscatterGroup1To2PerM, 14);
        Assert.Equal(0.0, moderator.FissionGroup1PerM, 14);
        Assert.Equal(0.0, moderator.FissionGroup2PerM, 14);
        Assert.Equal(0.0, moderator.NuFissionGroup1PerM, 14);
        Assert.Equal(0.0, moderator.NuFissionGroup2PerM, 14);
        Assert.Equal(state.GetBundle(0, 0).BundleId, state.EnumerateBundles().ElementAt(flatIndex).BundleId);

        FullCoreDiffusionSolveResultV1 solved = Require(
            model.TrySolve(state.EnumerateBundles(), 1_000_000_000.0));
        Assert.Equal(0.0, solved.NodePowerWatts[flatIndex], 14);
        Assert.Equal(1_000_000_000.0, solved.TotalPowerWatts, 3);
    }

    [Fact]
    public void ReflectiveOverrideAndNonfuelInputsRejectInvalidNodesFacesAndDuplicates()
    {
        AssertInvalid(
            Candu6CoreTopologyFactoryV1.TryCreate(
                (IEnumerable<ReflectiveFaceOverrideV1>)null!),
            "Topology.ReflectiveOverride.Missing");
        AssertInvalid(
            Candu6CoreTopologyFactoryV1.TryCreate(
                new[] { (ReflectiveFaceOverrideV1)null! }),
            "Topology.ReflectiveOverride.Null");
        AssertInvalid(
            Candu6CoreTopologyFactoryV1.TryCreate(
                new[]
                {
                    new ReflectiveFaceOverrideV1(
                        new NodeKey(
                            new ChannelId(Candu6CoreTopologyFactoryV1.ChannelCount),
                            new BundlePosition(0)),
                        TopologyFace.North)
                }),
            "Topology.ReflectiveOverride.Node.OutOfRange");
        AssertInvalid(
            Candu6CoreTopologyFactoryV1.TryCreate(
                new[]
                {
                    new ReflectiveFaceOverrideV1(
                        new NodeKey(new ChannelId(0), new BundlePosition(0)),
                        (TopologyFace)99)
                }),
            "Topology.ReflectiveOverride.Face.Invalid");

        ReflectiveFaceOverrideV1 duplicate = new ReflectiveFaceOverrideV1(
            new NodeKey(new ChannelId(0), new BundlePosition(0)),
            TopologyFace.EndA);
        AssertInvalid(
            Candu6CoreTopologyFactoryV1.TryCreate(new[] { duplicate, duplicate }),
            "Topology.ReflectiveOverride.Duplicate");

        FullCoreDiffusionDataPackV1 pack = Require(
            FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6());
        AssertInvalid(
            FullCoreDiffusionModelV1.TryCreateCandu6(
                pack,
                (IEnumerable<NodeKey>)null!),
            "FullCoreDiffusionModel.NonfuelNodes.Missing");
        NodeKey invalidNode = new NodeKey(
            new ChannelId(Candu6CoreTopologyFactoryV1.ChannelCount),
            new BundlePosition(0));
        AssertInvalid(
            FullCoreDiffusionModelV1.TryCreateCandu6(pack, new[] { invalidNode }),
            "FullCoreDiffusionModel.NonfuelNodes.OutOfRange");
        NodeKey duplicateNode = new NodeKey(new ChannelId(0), new BundlePosition(0));
        AssertInvalid(
            FullCoreDiffusionModelV1.TryCreateCandu6(
                pack,
                new[] { duplicateNode, duplicateNode }),
            "FullCoreDiffusionModel.NonfuelNodes.Duplicate");
    }

    [Fact]
    public void DefaultFactoriesKeepCanonicalFuelOnlySemantics()
    {
        CoreTopology defaultTopology = Require(Candu6CoreTopologyFactoryV1.TryCreate());
        CoreTopology explicitDefaultTopology = Require(
            Candu6CoreTopologyFactoryV1.TryCreate(
                Array.Empty<ReflectiveFaceOverrideV1>()));
        Assert.Equal(TopologySignature(defaultTopology), TopologySignature(explicitDefaultTopology));

        FullCoreDiffusionDataPackV1 pack = Require(
            FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6());
        FullCoreDiffusionModelV1 defaultModel = Require(
            FullCoreDiffusionModelV1.TryCreateCandu6(pack));
        Assert.Empty(defaultModel.NonfuelNodes);
        Assert.False(defaultModel.IsNonfuel(new NodeKey(new ChannelId(0), new BundlePosition(0))));
    }

    private static string TopologySignature(CoreTopology topology)
    {
        return string.Join(
            "|",
            topology.Channels.SelectMany(channel =>
                channel.Neighbors.Select(neighbor =>
                    "n:" + neighbor.SourceChannelId.Value + ":" +
                    neighbor.SourcePosition.Value + ":" +
                    (byte)neighbor.Direction + ":" +
                    neighbor.TargetChannelId.Value + ":" +
                    neighbor.TargetPosition.Value)
                .Concat(channel.BoundaryFaces.Select(boundary =>
                    "b:" + boundary.ChannelId.Value + ":" +
                    boundary.Position.Value + ":" +
                    (byte)boundary.Face + ":" +
                    (byte)boundary.Classification))));
    }

    private static bool IsCardinal(NeighborDirection direction)
    {
        return direction == NeighborDirection.North ||
               direction == NeighborDirection.East ||
               direction == NeighborDirection.South ||
               direction == NeighborDirection.West;
    }

    private static TopologyFace Inverse(TopologyFace face)
    {
        switch (face)
        {
            case TopologyFace.North:
                return TopologyFace.South;
            case TopologyFace.East:
                return TopologyFace.West;
            case TopologyFace.South:
                return TopologyFace.North;
            case TopologyFace.West:
                return TopologyFace.East;
            case TopologyFace.EndA:
                return TopologyFace.EndB;
            case TopologyFace.EndB:
                return TopologyFace.EndA;
            default:
                throw new ArgumentOutOfRangeException(nameof(face));
        }
    }

    private static T Require<T>(ContractValidationResult<T> result)
    {
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        return result.Value;
    }

    private static void AssertInvalid<T>(ContractValidationResult<T> result, string expectedCode)
    {
        Assert.False(result.IsValid);
        Assert.Equal(expectedCode, result.FirstDiagnostic.Code);
    }
}
