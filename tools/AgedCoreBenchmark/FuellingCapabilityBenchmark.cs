using System.Diagnostics;
using System.Text.Json;
using ReactorSim.Core;
using ReactorSim.Game;

internal static class FuellingCapabilityBenchmark
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public static void Run(string directory, uint seed)
    {
        Directory.CreateDirectory(directory);
        var timer = Stopwatch.StartNew();
        var session = PracticeGameSessionFactory.CreateBrowserPlaytest(seed, dailyTurns: false);
        var samples = new List<object>();
        var moves = new List<object>();
        var regionByChannel = session.CurrentLiquidZoneRrs.Mapping.Nodes
            .GroupBy(n => n.Node.ChannelId.Value).ToDictionary(g => g.Key, g => (int)(g.First().LogicalZoneId % 7));
        double peakChannel = 0, peakBundle = 0, minMean = 1, maxMean = 0, maxTilt = 0;
        int steps = 0;
        // Offline player policy: replenish below 45% mean fill. When paired
        // regional fills differ by more than one percentage point, refuel old
        // fuel in the lowest-fill region; otherwise select the oldest globally.
        // Uses shared mapping, actual flow and normal accepted game commands.
        void Capture(string phase)
        {
            var s = session.Snapshot;
            double mean = session.CurrentLiquidZoneRrs.ZoneFills.Average();
            double channel = s.Core.Channels.Max(c => c.PowerWatts);
            double bundle = s.Core.Channels.SelectMany(c => c.Bundles).Max(b => b.PowerWatts);
            double total = s.Core.Channels.Sum(c => c.PowerWatts);
            double bundleTotal = s.Core.Channels.SelectMany(c => c.Bundles).Sum(b => b.PowerWatts);
            if (!double.IsFinite(total) || Math.Abs(total - s.Physics.TotalPowerWatts) > total * 1e-8 ||
                Math.Abs(total - bundleTotal) > total * 1e-8 || s.Shift.FuelConsumed != s.RefuellingOperationCount * 8)
                throw new InvalidOperationException("Power or fuel accounting failed.");
            peakChannel = Math.Max(peakChannel, channel); peakBundle = Math.Max(peakBundle, bundle);
            minMean = Math.Min(minMean, mean); maxMean = Math.Max(maxMean, mean);
            maxTilt = Math.Max(maxTilt, Math.Abs(s.AxialTiltFraction));
            if (phase != "step" || steps % 20 == 0 || s.IsGameOver)
                samples.Add(new
                {
                    phase,
                    days = s.SimulationTimeSeconds / 86400,
                    meanFillPercent = mean * 100,
                    zoneFillsPercent = session.CurrentLiquidZoneRrs.ZoneFills.Select(f => f * 100).ToArray(),
                    channelKw = channel / 1000,
                    bundleKw = bundle / 1000,
                    tiltPercent = s.AxialTiltFraction * 100,
                    rhoMk = session.CurrentLiquidZoneRrs.CompensatedNetReactivity * 1000,
                    burnup = s.Core.Channels.Average(c => c.AverageBurnupMwDayPerKg),
                    s.ScoreTotal,
                    s.Shift.FuelConsumed,
                    wallSeconds = timer.Elapsed.TotalSeconds
                });
        }
        void Save()
        {
            var s = session.Snapshot;
            File.WriteAllText(Path.Combine(directory, "report.json"), JsonSerializer.Serialize(new
            {
                format = "fuelling-capability-100-days-v1",
                seed,
                requestedDays = 100,
                completedDays = s.SimulationTimeSeconds / 86400,
                completed = s.SimulationTimeSeconds == 100 * 86400 && !s.IsGameOver,
                terminal = s.IsGameOver,
                reason = s.GameOverReason,
                pack = PracticeGameSessionFactory.DiffusionDataPackVersion,
                policy = "At every 180-second browser step, refuel when mean zone fill is below 45%. If paired regional fills differ by more than one percentage point, select the oldest eligible channel in the lowest-fill region; otherwise select the oldest globally. Eight bundles with channel flow; unlimited fuel; normal xenon, RRS and terminal limits. No physics overrides.",
                steps,
                operations = s.RefuellingOperationCount,
                fuelBundles = s.Shift.FuelConsumed,
                peakChannelKw = peakChannel / 1000,
                peakBundleKw = peakBundle / 1000,
                minimumMeanFillPercent = minMean * 100,
                maximumMeanFillPercent = maxMean * 100,
                maximumAbsoluteTiltPercent = maxTilt * 100,
                wallSeconds = timer.Elapsed.TotalSeconds,
                samples,
                moves
            }, JsonOptions));
        }
        Capture("initial"); Save();
        Console.WriteLine($"100-day fuelling attempt: seed {seed}, every-step LZC, normal limits.");
        while (session.Snapshot.SimulationTimeSeconds < 100 * 86400 && !session.Snapshot.IsGameOver)
        {
            if (session.CurrentLiquidZoneRrs.ZoneFills.Average() < .45)
            {
                double[] fills = session.CurrentLiquidZoneRrs.ZoneFills.ToArray();
                double[] pairs = Enumerable.Range(0, 7).Select(r => (fills[r] + fills[r + 7]) / 2).ToArray();
                int lowestRegion = Enumerable.Range(0, 7).OrderBy(r => pairs[r]).ThenBy(r => r).First();
                var candidates = session.Snapshot.Core.Channels.Where(c => c.CanRefuel);
                if (pairs.Max() - pairs.Min() > .01)
                    candidates = candidates.Where(c => regionByChannel[c.ChannelIndex] == lowestRegion);
                var c = candidates.OrderByDescending(c => c.AverageBurnupMwDayPerKg).ThenBy(c => c.ChannelIndex).First();
                string direction = c.FlowDirection == FlowDirection.EndAtoEndB ? "toward-end-b" : "toward-end-a";
                double before = session.Snapshot.SimulationTimeSeconds;
                var result = session.RefuelChannel(c.ChannelIndex, direction, 8, "NAT-U-SYNTHETIC");
                if (!result.Accepted) throw new InvalidOperationException(result.DiagnosticMessage);
                if (session.Snapshot.SimulationTimeSeconds != before) throw new InvalidOperationException("Refuel advanced time.");
                moves.Add(new
                {
                    days = before / 86400,
                    c.ChannelIndex,
                    channelName = $"{"ABCDEFGHJKLMNOPQRSTUVW"[c.GridRow]}{c.GridColumn + 1:00}",
                    direction,
                    region = regionByChannel[c.ChannelIndex] + 1,
                    preChannelMeanBurnup = c.AverageBurnupMwDayPerKg
                });
                Capture("refuel");
                if (session.Snapshot.IsGameOver) break;
            }
            double previous = session.Snapshot.SimulationTimeSeconds;
            var advance = session.AdvanceWallMilliseconds(100);
            if (!advance.Accepted || advance.Snapshot.SimulationTimeSeconds <= previous) throw new InvalidOperationException(advance.DiagnosticMessage);
            if (Math.Abs(advance.Snapshot.SimulationTimeSeconds - previous - 180) > 1e-8)
                throw new InvalidOperationException("Benchmark requires one 180-second browser simulation step per command.");
            steps++; Capture("step");
            if (steps % 20 == 0 || session.Snapshot.IsGameOver)
            {
                Save();
            }
            if (steps % 480 == 0 || session.Snapshot.IsGameOver)
            {
                Console.WriteLine($"Day {session.Snapshot.SimulationTimeSeconds / 86400:F3}: {session.Snapshot.RefuellingOperationCount} moves, fill {session.CurrentLiquidZoneRrs.ZoneFills.Average() * 100:F2}%, elapsed {timer.Elapsed.TotalSeconds:F1}s");
            }
        }
        Capture("final"); Save();
        Console.WriteLine($"Finished: {session.Snapshot.SimulationTimeSeconds / 86400:F3} days; {session.Snapshot.GameOverReason}; report {Path.GetFullPath(directory)}");
    }
}
