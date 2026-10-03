using System.Diagnostics;
using System.Text.Json;
using ReactorSim.Core;

// Native diagnostic only. Browser complete-command timings remain the product gate.
var pack = Require(FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6());
var model = Require(FullCoreDiffusionModelV1.TryCreateCandu6(pack));
var reference = Require(model.TrySolve(SyntheticGameCoreStateV1.CreatePractice().EnumerateBundles(), 1e9));
var stencil = model.Stencil;
var rows = new List<object>();
foreach (bool cold in new[] { false, true })
{
    SpatialSolveResult? expected = null;
    foreach (int workers in new[] { 1, 2, 4 })
    {
        var timings = new List<double>();
        for (int repeat = 0; repeat < 4; repeat++)
        {
            var iteration = Require(SpatialEigenIteration.TryCreate(stencil, reference.Coefficients,
                pack.LinearSolvePolicy, reference.TotalPowerWatts, cold ? 1 : reference.EffectiveK * 1.01,
                cold ? null : reference.Group1Flux.Select((v, n) => v * (n % 2 == 0 ? 1.02 : .98)).ToArray(),
                cold ? null : reference.Group2Flux.Select((v, n) => v * (n % 2 == 0 ? .98 : 1.02)).ToArray(),
                workers));
            var solver = Require(SpatialEigenSolve.TryCreate(iteration, pack.ConvergencePolicy));
            var clock = Stopwatch.StartNew();
            var result = Require(solver.TrySolve());
            clock.Stop();
            if (!result.IsConverged) throw new InvalidOperationException("Solver did not converge.");
            expected ??= result;
            if (result.Diagnostics.IterationCount != expected.Diagnostics.IterationCount ||
                result.FinalState!.Eigenvalue != expected.FinalState!.Eigenvalue ||
                !result.FinalState.Group1Flux.SequenceEqual(expected.FinalState.Group1Flux) ||
                !result.FinalState.Group2Flux.SequenceEqual(expected.FinalState.Group2Flux))
                throw new InvalidOperationException("Parallel results differ from the serial reference.");
            if (repeat > 0) timings.Add(clock.Elapsed.TotalMilliseconds);
        }
        rows.Add(new
        {
            cold,
            workers,
            iterations = expected!.Diagnostics.IterationCount,
            medianMs = timings.Order().ElementAt(1),
            samplesMs = timings,
            identical = true
        });
    }
}
Console.WriteLine(JsonSerializer.Serialize(new
{
    format = "candu-cpu-parallel-native-v1",
    nodeCount = stencil.NodeCount,
    processorCount = Environment.ProcessorCount,
    runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
    rows
},
    new JsonSerializerOptions { WriteIndented = true }));

static T Require<T>(ContractValidationResult<T> result) => result.IsValid ? result.Value :
    throw new InvalidOperationException(result.FirstDiagnostic.ToString());
