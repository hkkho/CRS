using System;
using System.Linq;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class CpuParallelSpatialTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void ParallelRowsAndCoupledSolveMatchSerialExactly(int workers)
    {
        var model = Require(FullCoreDiffusionModelV1.TryCreateCandu6(Require(FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6())));
        var bundles = SyntheticGameCoreStateV1.CreatePractice().EnumerateBundles().ToArray();
        var reference = Require(model.TrySolve(bundles, 1e9));
        var coefficients = reference.Coefficients;
        var serial = Require(SpatialOperator.TryCreate(model.Stencil, coefficients, 1));
        var parallel = Require(SpatialOperator.TryCreate(model.Stencil, coefficients, workers));
        Assert.Equal(workers, parallel.WorkerCount);
        var flux = Enumerable.Range(0, model.NodeCount).Select(n => 1.0 + n % 17 * .03).ToArray();
        foreach (var group in new[] { SpatialEnergyGroup.Group1, SpatialEnergyGroup.Group2 })
        {
            var expected = new double[flux.Length];
            var actual = new double[flux.Length];
            Assert.True(serial.TryApply(group, flux, expected, out _));
            for (int repeat = 0; repeat < 3; repeat++)
            {
                Assert.True(parallel.TryApply(group, flux, actual, out _));
                Assert.Equal(expected, actual);
            }
        }
        SpatialSolveResult Solve(int count)
        {
            var iteration = Require(SpatialEigenIteration.TryCreate(model.Stencil, coefficients,
                model.DataPack.LinearSolvePolicy, 1e9, reference.EffectiveK * 1.01,
                reference.Group1Flux.Select((v, n) => v * (n % 2 == 0 ? 1.02 : .98)).ToArray(),
                reference.Group2Flux.Select((v, n) => v * (n % 2 == 0 ? .98 : 1.02)).ToArray(),
                cpuWorkerCount: count));
            return Require(Require(SpatialEigenSolve.TryCreate(iteration, model.DataPack.ConvergencePolicy)).TrySolve());
        }
        var a = Solve(1);
        var b = Solve(workers);
        Assert.True(a.IsConverged && b.IsConverged);
        Assert.Equal(a.Diagnostics.IterationCount, b.Diagnostics.IterationCount);
        Assert.Equal(a.FinalState!.Eigenvalue, b.FinalState!.Eigenvalue);
        Assert.Equal(a.FinalState.TotalPowerW, b.FinalState.TotalPowerW);
        Assert.Equal(a.FinalState.Group1Flux, b.FinalState.Group1Flux);
        Assert.Equal(a.FinalState.Group2Flux, b.FinalState.Group2Flux);
    }

    [Fact]
    public void ParallelFailuresChooseCanonicalDiagnosticAndClearAfterWritersFinish()
    {
        var model = Require(FullCoreDiffusionModelV1.TryCreateCandu6(Require(FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6())));
        var reference = Require(model.TrySolve(SyntheticGameCoreStateV1.CreatePractice().EnumerateBundles(), 1e9));
        var serial = Require(SpatialOperator.TryCreate(model.Stencil, reference.Coefficients, 1));
        var parallel = Require(SpatialOperator.TryCreate(model.Stencil, reference.Coefficients, 2));
        var invalid = Enumerable.Range(0, model.NodeCount).Select(n => n % 2 == 0 ? double.MaxValue : 0.0).ToArray();
        foreach (double bad in new[] { double.NaN, -1.0 })
        {
            invalid[0] = bad;
            var a = Enumerable.Repeat(42.0, invalid.Length).ToArray();
            var b = (double[])a.Clone();
            Assert.False(serial.TryApply(SpatialEnergyGroup.Group1, invalid, a, out var serialError));
            Assert.False(parallel.TryApply(SpatialEnergyGroup.Group1, invalid, b, out var parallelError));
            Assert.Equal(serialError.Code, parallelError.Code);
            Assert.Equal(serialError.Path, parallelError.Path);
            Assert.Equal(serialError.Message, parallelError.Message);
            Assert.All(b, v => Assert.Equal(0, v));
        }
        Assert.False(parallel.TryApply(SpatialEnergyGroup.Group1, invalid, invalid, out var aliasError));
        Assert.Equal("SpatialOperator.Buffers.Alias", aliasError.Code);
        Assert.All(invalid, v => Assert.Equal(0, v));
        Assert.False(SpatialOperator.TryCreate(model.Stencil, reference.Coefficients, 0).IsValid);
        Assert.False(SpatialOperator.TryCreate(model.Stencil, reference.Coefficients, 9).IsValid);
    }

    private static T Require<T>(ContractValidationResult<T> result)
    {
        Assert.True(result.IsValid, result.IsValid ? "" : result.FirstDiagnostic.ToString());
        return result.Value;
    }
}
