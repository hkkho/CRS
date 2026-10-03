using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using ReactorSim.Core;

if (args.Length > 0 && args[0] == "--fifteen-minute-campaign")
{
    CampaignComparison.RunFifteenMinuteCampaign(args[1], args[2]);
    return;
}

if (args.Length > 0 && args[0] == "--rerun-core-case")
{
    CampaignComparison.RerunCoreCase(args[1], args[2], ulong.Parse(args[3], CultureInfo.InvariantCulture));
    return;
}

if (args.Length > 0 && args[0] == "--recover-initialization")
{
    CampaignComparison.RunRecovery(args[1]);
    return;
}

if (args.Length > 0 && args[0] == "--initialization-probe")
{
    CampaignComparison.ProbeInitialization(ulong.Parse(args[1], CultureInfo.InvariantCulture), int.Parse(args[2], CultureInfo.InvariantCulture));
    return;
}

if (args.Length > 0 && args[0] == "--campaign")
{
    CampaignComparison.Run(args.Length > 1 ? args[1] : "artifacts/two-check-campaign", args.Contains("--resume"));
    return;
}

// Experimental order only; not a replacement for the browser GameSession.
double stepSeconds = args.Length > 0 ? double.Parse(args[0], CultureInfo.InvariantCulture) : 900;
int steps = args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 20;
int checks = args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 2;
if (!double.IsFinite(stepSeconds) || stepSeconds <= 0 || stepSeconds > 1800 || steps < 2 || steps > 1000 || checks < 1 || checks > 2)
    throw new ArgumentException("Usage: SingleSolveBenchmark [step seconds (0,1800]] [steps 2..1000] [spatial checks 1|2]");

var core = SyntheticGameCoreStateV1.CreateAgedPractice(1001);
var pack = Require(FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6());
var model = Require(FullCoreDiffusionModelV1.TryCreateCandu6(pack));
var solver = Require(EquilibriumCoreSolverV1.TryCreate(model, core.EnumerateBundles(), 2064e6));
var mapping = Require(PracticeLiquidZoneRrsMappingV1.TryCreateCandu6());
var initial = Require(PracticeLiquidZoneRrsV1.TryCreate(mapping, solver.CurrentProjection));
var clock = Stopwatch.StartNew();
var accepted = Require(PracticeLiquidZoneRrsV1.TryInitializeSingleSolve(solver, core.EnumerateBundles(), initial));
clock.Stop();
double bootstrapMs = clock.Elapsed.TotalMilliseconds;
int bootstrapSolves = accepted.CandidateSolveCount;
bool bootstrapShapeConverged = accepted.ControllerConverged;
double bootstrapReactivity = accepted.Projection.RelativeReactivity;
var xenon = PracticeXenonStateV1.CreateEquilibrium(core, accepted.Projection, 0);
var rows = new List<object>();
double energyJ = 0;
for (int step = 0; step < steps; step++)
{
    double time = step * stepSeconds;
    clock.Restart();
    // Only step zero has no previous accepted snapshot. Its settled bootstrap
    // already supplies the first interval's shape; no duplicate solve is needed.
    var next = step == 0 ? accepted : Require(checks == 1
        ? PracticeLiquidZoneRrsV1.TryRunSingleSolve(solver, core.EnumerateBundles(), accepted, time, xenon.Overlay)
        : PracticeLiquidZoneRrsV1.TryRunTwoSolve(solver, core.EnumerateBundles(), accepted, time, xenon.Overlay));
    if (step > 0 && (!ReferenceEquals(next.WarmStartProjection, accepted.Projection) || next.CandidateSolveCount != checks))
        throw new InvalidOperationException("Routine steps must use the requested solve count, starting from the last accepted state.");
    double amplitude = 0.95;
    double[] energies = next.Projection.ShapeNodePowerWatts.Select(power => power * amplitude * stepSeconds).ToArray();
    var nextCore = Require(core.TryAddFissionEnergy(energies));
    var nextXenon = xenon.Advance(next.Projection, amplitude, stepSeconds);
    Require(solver.TryCommitCandidate(next.Projection));
    // Accept all end-of-interval material state together. No partial seed update.
    accepted = next;
    core = nextCore;
    xenon = nextXenon;
    energyJ += energies.Sum();
    clock.Stop();
    rows.Add(new
    {
        step,
        snapshotTimeSeconds = time,
        endTimeSeconds = xenon.SimulationTimeSeconds,
        candidateSolves = step == 0 ? 0 : next.CandidateSolveCount,
        elapsedMs = clock.Elapsed.TotalMilliseconds,
        iterations = step == 0 ? 0 : next.SpatialIterationCount,
        predictionIterations = next.PredictedProjection?.SolverIterationCount,
        fineTuningIterations = next.FineTunedProjection?.SolverIterationCount,
        predictedReactivity = next.PredictedProjection?.RelativeReactivity,
        fineTunedReactivity = next.FineTunedProjection?.RelativeReactivity,
        fineTuningAccepted = next.FineTuningAccepted,
        reactivity = next.Projection.RelativeReactivity,
        controllerConverged = next.ControllerConverged,
        maxShapeError = next.ZonalShapeErrors.Max(Math.Abs),
        zoneFills = next.ZoneFills,
        warmStartedFromLastAccepted = step > 0
    });
}
Console.WriteLine(JsonSerializer.Serialize(new
{
    format = "candu-snapshot-check-experiment-v2",
    spatialChecks = checks,
    stepSeconds,
    steps,
    bootstrapMs,
    bootstrapSolves,
    bootstrapReactivity,
    bootstrapShapeConverged,
    energyJ,
    iodine = xenon.Iodine.Sum(),
    xenon = xenon.Xenon.Sum(),
    rows
}, new JsonSerializerOptions { WriteIndented = true }));

static T Require<T>(ContractValidationResult<T> result) => result.IsValid ? result.Value :
    throw new InvalidOperationException(result.FirstDiagnostic.ToString());
