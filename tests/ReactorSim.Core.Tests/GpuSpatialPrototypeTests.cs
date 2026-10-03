#if RESEARCH_EXPERIMENTS
using System;
using System.Linq;
using ReactorSim.Core;
using ReactorSim.Game;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class GpuSpatialPrototypeTests
{
    [Theory]
    [InlineData(SpatialEnergyGroup.Group1)]
    [InlineData(SpatialEnergyGroup.Group2)]
    public void PackedOperatorPreservesCanonicalCpuOperatorAndFixedStepReference(SpatialEnergyGroup group)
    {
        var session = PracticeGameSessionFactory.CreateBrowserPlaytest();
        var solve = session.CurrentSpatialCandidate.SpatialSolve;
        var zero = GpuSpatialPrototypeFixtureV1.Create(solve, group, 0);
        var odd = GpuSpatialPrototypeFixtureV1.Create(solve, group, 3);
        var even = GpuSpatialPrototypeFixtureV1.Create(solve, group, 32);
        Assert.Equal(4560, zero.NodeCount);
        Assert.Equal(4561, zero.RowOffsets.Count);
        Assert.Equal(zero.Targets.Count, zero.Conductances.Count);
        Assert.Equal((uint)zero.Targets.Count, zero.RowOffsets[^1]);
        Assert.Equal(zero.InputFlux.Select(x => (double)x), zero.ReferenceFlux);
        Assert.Equal(zero.CoefficientDigestHex, even.CoefficientDigestHex);
        Assert.Equal(zero.KernelDigestHex, even.KernelDigestHex);
        Assert.Contains("fn applyOperator", zero.ShaderSource);
        Assert.True(even.ReferenceRelativeResidual < odd.ReferenceRelativeResidual);
        for (int n = 0; n < zero.NodeCount; n++)
        {
            double x = zero.InputFlux[n], leakage = 0;
            for (uint edge = zero.RowOffsets[n]; edge < zero.RowOffsets[n + 1]; edge++)
            {
                uint target = zero.Targets[(int)edge];
                leakage += zero.Conductances[(int)edge] *
                    (target == uint.MaxValue ? x : x - zero.InputFlux[(int)target]);
            }
            double packed = zero.NodeData[n * 4] * x + leakage / zero.NodeData[n * 4 + 1];
            Assert.InRange(Math.Abs(packed - zero.ReferenceApplied[n]) / Math.Max(1, Math.Abs(zero.ReferenceApplied[n])), 0, 2e-6);
        }
    }

    [Fact]
    public void InvalidGroupAndIterationBoundsAreRejected()
    {
        var solve = PracticeGameSessionFactory.CreateBrowserPlaytest().CurrentSpatialCandidate.SpatialSolve;
        Assert.Throws<ArgumentOutOfRangeException>(() => GpuSpatialPrototypeFixtureV1.Create(solve, (SpatialEnergyGroup)0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => GpuSpatialPrototypeFixtureV1.Create(solve, SpatialEnergyGroup.Group1, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => GpuSpatialPrototypeFixtureV1.Create(solve, SpatialEnergyGroup.Group1, 129));
    }
}

#endif
