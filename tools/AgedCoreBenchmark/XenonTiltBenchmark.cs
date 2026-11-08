using System.Text.Json;
using System.Text.Json.Nodes;
using ReactorSim.Core;
using ReactorSim.Game;

internal static class XenonTiltBenchmark
{
    private const int Hours = 96;
    private const int ReleaseHour = 8;
    private static readonly JsonSerializerOptions ReportJson = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void Run(string outputPath, ulong seed, int stepsPerHour, double xenonAbsorptionMultiplier, string? packPath)
    {
        if (stepsPerHour <= 0 || 3600 % stepsPerHour != 0)
            throw new ArgumentOutOfRangeException(nameof(stepsPerHour), "Use a positive divisor of 3600.");
        int stepSeconds = 3600 / stepsPerHour;
        var core = SyntheticGameCoreStateV1.CreateAgedPractice(seed);
        var inventory = core.EnumerateBundles().ToArray();
        // Resolve small late-time tilts above the drift of gameplay-tolerance warm starts.
        var overrides = new Dictionary<string, double>
        {
            ["solver.absolute_residual_tolerance"] = 1e-12,
            ["solver.relative_residual_tolerance"] = 1e-9,
            ["solver.k_absolute_tolerance"] = 2e-9,
            ["solver.k_relative_tolerance"] = 2e-9,
            ["solver.residual_tolerance"] = 2e-7,
            ["solver.source_shape_tolerance"] = 1e-7,
            ["practice.xenon_thermal_absorption_multiplier"] = xenonAbsorptionMultiplier
        };
        FullCoreDiffusionDataPackV1 pack;
        if (packPath == null)
        {
            pack = Require(FullCoreDiffusionDataPackV1.TryLoadPracticeOverrides(overrides));
        }
        else
        {
            if (xenonAbsorptionMultiplier != 1)
                throw new ArgumentException("Specify the absorption directly in the supplied pack.");
            var candidate = JsonNode.Parse(File.ReadAllText(packPath))!;
            foreach (var setting in overrides.Where(pair => pair.Key.StartsWith("solver.", StringComparison.Ordinal)))
                candidate["solver"]![setting.Key["solver.".Length..]] = setting.Value;
            pack = Require(FullCoreDiffusionDataPackV1.TryLoadJson(candidate.ToJsonString()));
        }
        var model = Require(FullCoreDiffusionModelV1.TryCreateCandu6(pack));
        const double thermalPowerWatts = PracticeGameSessionFactory.PracticeReferenceThermalPowerWatts;
        var solver = Require(EquilibriumCoreSolverV1.TryCreate(model, inventory, thermalPowerWatts));
        var mapping = pack.CreatePracticeZoneMapping();
        var compartments = PracticeLiquidZoneTubesV1.Compartments.OrderBy(t => t.ZoneId).ToArray();
        var neutralFills = compartments.Select(_ => .5).ToArray();
        var tiltedFills = compartments.Select(t => t.HorizontalCentreM < 0 ? .8 : t.HorizontalCentreM > 0 ? .2 : .5).ToArray();
        var neutralZones = Require(mapping.TryBuildOverlay(neutralFills));
        var tiltedZones = Require(mapping.TryBuildOverlay(tiltedFills));
        if (neutralZones.Entries.Count != inventory.Length || tiltedZones.Entries.Count != inventory.Length)
            throw new InvalidOperationException("The fixed-zone overlays must cover the complete poison inventory.");
        var initial = Require(PracticeXenonEquilibriumV1.TryCreate(solver, core, neutralFills));
        var left = Enumerable.Range(0, inventory.Length).Where(n =>
            Candu6CoreTopologyFactoryV1.GetPosition(inventory[n].Node.ChannelId.Value).Column <= 10).ToArray();
        var right = Enumerable.Range(0, inventory.Length).Where(n =>
            Candu6CoreTopologyFactoryV1.GetPosition(inventory[n].Node.ChannelId.Value).Column >= 11).ToArray();

        Sample Measure(int hour, EquilibriumCoreProjectionV1 projection, PracticeXenonStateV1 poison)
        {
            var power = projection.ShapeNodePowerWatts;
            Region Summarize(int[] nodes) => new(nodes.Sum(n => power[n]), nodes.Average(n => poison.Iodine[n]),
                nodes.Average(n => poison.Xenon[n]), nodes.Average(n => projection.ShapeGroup2[n]));
            var west = Summarize(left);
            var east = Summarize(right);
            double total = west.PowerWatts + east.PowerWatts;
            if (Math.Abs(total - thermalPowerWatts) > .01)
                throw new InvalidOperationException($"Hour {hour}: unexpected total thermal power {total:R} W.");
            return new Sample(hour, west, east, 100 * (east.PowerWatts - west.PowerWatts) / total,
                projection.SpatialSolve.EffectiveK, projection.SpatialSolve.ResidualRelativeInfinity);
        }

        EquilibriumCoreProjectionV1 Solve(StaticAbsorptionOverlayV1 zones, PracticeXenonStateV1 poison,
            EquilibriumCoreProjectionV1 previous)
        {
            var overlay = Require(StaticAbsorptionOverlayV1.TryCreate("xenon-tilt-fixed-zones",
                zones.Entries.Select(e => new StaticAbsorptionOverlayEntryV1(e.Node,
                    e.DeltaAbsorptionGroup1PerM + poison.Overlay.GetDeltaAbsorptionGroup1PerM(e.Node),
                    e.DeltaAbsorptionGroup2PerM + poison.Overlay.GetDeltaAbsorptionGroup2PerM(e.Node)))));
            return Require(solver.TrySolveCandidate(inventory, overlay, previous.SpatialSolve));
        }

        double? equilibriumXenonWorthMk = null;
        if (pack.XenonReference != null)
        {
            var noXenon = Require(StaticAbsorptionOverlayV1.TryCreate("xenon-tilt-remove-included-xe",
                inventory.Select((bundle, n) => new StaticAbsorptionOverlayEntryV1(bundle.Node,
                    neutralZones.GetDeltaAbsorptionGroup1PerM(bundle.Node) -
                        initial.Poison.FastAbsorptionCrossSectionM2 * initial.Poison.IncludedReferenceXenon[n],
                    neutralZones.GetDeltaAbsorptionGroup2PerM(bundle.Node) -
                        initial.Poison.ThermalAbsorptionCrossSectionM2 * initial.Poison.IncludedReferenceXenon[n]))));
            var unpoisoned = Require(solver.TrySolveCandidate(inventory, noXenon, initial.Projection.SpatialSolve));
            equilibriumXenonWorthMk = 1000 * (initial.Projection.RelativeReactivity - unpoisoned.RelativeReactivity);
        }
        var tilted = Solve(tiltedZones, initial.Poison, initial.Projection);
        var baseline = Measure(0, initial.Projection, initial.Poison);
        (EquilibriumCoreProjectionV1 Projection, PracticeXenonStateV1 Poison) RunTransient(
            StaticAbsorptionOverlayV1 zones, int startHour, int endHour, EquilibriumCoreProjectionV1 projection,
            PracticeXenonStateV1 poison, List<Sample> samples)
        {
            for (int hour = startHour + 1; hour <= endHour; hour++)
            {
                for (int step = 0; step < stepsPerHour; step++)
                {
                    poison = poison.Advance(projection, 1, stepSeconds);
                    projection = Solve(zones, poison, projection);
                }
                samples.Add(Measure(hour, projection, poison));
            }
            return (projection, poison);
        }

        Console.WriteLine("Solving 96 hours with the 80/20 split held...");
        var held = new List<Sample> { Measure(0, tilted, initial.Poison) };
        var (atRelease, releasePoison) = RunTransient(tiltedZones, 0, ReleaseHour, tilted, initial.Poison, held);
        RunTransient(tiltedZones, ReleaseHour, Hours, atRelease, releasePoison, held);
        Console.WriteLine($"Solving the free response after restoring 50% fills at hour {ReleaseHour}...");
        var neutralAtRelease = Solve(neutralZones, releasePoison, atRelease);
        var released = new List<Sample> { Measure(ReleaseHour, neutralAtRelease, releasePoison) };
        RunTransient(neutralZones, ReleaseHour, Hours, neutralAtRelease, releasePoison, released);
        var report = new
        {
            dataPack = pack.Descriptor.DataPackVersion,
            baseDataPack = packPath == null ? PracticeGameSessionFactory.DiffusionDataPackVersion : pack.Descriptor.DataPackVersion,
            overrides,
            seed,
            thermalPowerWatts,
            xenonAbsorptionMultiplier,
            xenonThermalAbsorptionM2 = initial.Poison.ThermalAbsorptionCrossSectionM2,
            xenonThermalCaptureM2 = initial.Poison.ThermalCaptureCrossSectionM2,
            xenonDecayPerSecond = PracticeXenonDataV1.LambdaXe,
            equilibriumXenonWorthMk,
            stepSeconds,
            sampleIntervalSeconds = 3600,
            releaseHour = ReleaseHour,
            leftNodeCount = left.Length,
            rightNodeCount = right.Length,
            method = "Fixed burnup, inventory and full thermal power. The 80/20 step starts at hour zero with spatial regulation disabled. The released trace branches from the held run and samples the release hour immediately after all zones return to 50%.",
            zones = compartments.Select(t => new
            {
                zone = t.ZoneId + 1,
                t.HorizontalCentreM,
                t.AxialCentreM,
                tiltedFill = tiltedFills[t.ZoneId],
                neutralFill = neutralFills[t.ZoneId]
            }),
            baseline,
            frozenPoisonHeldTilt = held[0],
            held = Analyze(held),
            released = Analyze(released, baseline.TiltPercent)
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(report, ReportJson));
        Console.WriteLine($"Wrote {outputPath}");
    }

    private static object Analyze(List<Sample> samples, double? equilibriumTilt = null)
    {
        const double minimumSwing = .05;
        var extrema = new List<Extremum>();
        var turningPoint = samples[0];
        int direction = 0;
        foreach (var sample in samples.Skip(1))
        {
            double change = sample.TiltPercent - turningPoint.TiltPercent;
            if (direction == 0)
            {
                if (Math.Abs(change) < minimumSwing) continue;
                direction = Math.Sign(change);
                turningPoint = sample;
            }
            else if (change * direction > 0)
            {
                turningPoint = sample;
            }
            else if (change * direction <= -minimumSwing)
            {
                extrema.Add(new Extremum(turningPoint.Hour, turningPoint.TiltPercent, direction > 0));
                direction = -direction;
                turningPoint = sample;
            }
        }
        // Boundary samples can be forced by a zone step. Use only confirmed internal reversals.
        var cycles = new List<Cycle>();
        for (int n = 2; n < extrema.Count; n++)
        {
            var first = extrema[n - 2];
            var middle = extrema[n - 1];
            var last = extrema[n];
            int period = last.Hour - first.Hour;
            double? dampingPerHour = null;
            if (equilibriumTilt is double equilibrium &&
                (first.TiltPercent - equilibrium) * (last.TiltPercent - equilibrium) > 0)
                dampingPerHour = Math.Log(Math.Abs((last.TiltPercent - equilibrium) / (first.TiltPercent - equilibrium))) / period;
            cycles.Add(new Cycle(first.Hour, middle.Hour, last.Hour, period, first.IsPeak, dampingPerHour));
        }
        return new
        {
            minimumSwingPercentagePoints = minimumSwing,
            extrema,
            cycles,
            samples
        };
    }

    private sealed record Region(double PowerWatts, double MeanI135NumberDensityM3, double MeanXe135NumberDensityM3,
        double MeanThermalFluxPerM2Second);
    private sealed record Sample(int Hour, Region Left, Region Right, double TiltPercent, double EffectiveK, double Residual);
    private sealed record Extremum(int Hour, double TiltPercent, bool IsPeak);
    private sealed record Cycle(int StartHour, int OppositeExtremumHour, int EndHour, int PeriodHours, bool StartsAtPeak,
        double? DampingPerHour);

    private static T Require<T>(ContractValidationResult<T> result) => result.IsValid ? result.Value :
        throw new InvalidOperationException(result.FirstDiagnostic.ToString());
}
