using ReactorSim.Core;
using ReactorSim.Game;

// Analysis of published powers only. The session remains the physics authority.
internal sealed class CampaignPowerAnalysis
{
    private readonly double target;
    private readonly double[] reference;
    private readonly int[] columns;
    private readonly int[] rows;
    private readonly double[] maxima;
    private readonly double[] maximumDays;
    private readonly double[] maximumRatios;
    private readonly double[] maximumAbsoluteDeviations;
    private readonly List<object> timeline = new();
    private double elapsed, weightedMaximumAbsolute, weightedMaximumPositive, weightedMaximumRatio, weightedRms;

    internal CampaignPowerAnalysis(GameSessionSnapshot initial, double powerTarget)
    {
        target = powerTarget;
        reference = initial.Ripple.ReferenceChannelPowerWatts.ToArray();
        columns = initial.Core.Channels.Select(c => c.GridColumn).ToArray();
        rows = initial.Core.Channels.Select(c => c.GridRow).ToArray();
        maxima = new double[reference.Length];
        maximumDays = new double[reference.Length];
        maximumRatios = new double[reference.Length];
        maximumAbsoluteDeviations = new double[reference.Length];
    }

    internal void Observe(GameSessionSnapshot snapshot, string operation)
    {
        // At t=0 use the fixed target that applies at the first control tick,
        // excluding the zero-duration pre-play initialization at full power.
        double initialScale = snapshot.SimulationTimeSeconds == 0 ? target / snapshot.NormalizedPowerFraction : 1;
        double[] powers = snapshot.Core.Channels.Select(c => c.PowerWatts * initialScale).ToArray();
        UpdatePeaks(powers, snapshot.SimulationTimeSeconds);
        var metrics = Metrics(powers);
        timeline.Add(new
        {
            day = snapshot.SimulationTimeSeconds / 86400,
            operation,
            maxAbsoluteRipplePercent = metrics.Absolute * 100,
            maxPositiveRipplePercent = metrics.Positive * 100,
            maxPowerReferencePercent = metrics.Ratio * 100,
            rmsRipplePercent = metrics.Rms * 100,
            maximumChannelPowerKw = powers.Max() / 1000
        });
    }

    internal void Integrate(GameSessionSnapshot before, GameSessionSnapshot after)
    {
        double seconds = after.SimulationTimeSeconds - before.SimulationTimeSeconds;
        if (seconds <= 0) return;
        // These runs hold one target. Game applies the queued target at the
        // first control tick and retains the pre-step shape within this
        // half-hour interval. Normalize only that initially queued amplitude.
        double scale = target / before.NormalizedPowerFraction;
        double[] powers = before.Core.Channels.Select(c => c.PowerWatts * scale).ToArray();
        UpdatePeaks(powers, before.SimulationTimeSeconds);
        var metrics = Metrics(powers);
        elapsed += seconds;
        weightedMaximumAbsolute += metrics.Absolute * seconds;
        weightedMaximumPositive += metrics.Positive * seconds;
        weightedMaximumRatio += metrics.Ratio * seconds;
        weightedRms += metrics.Rms * seconds;
    }

    private (double Absolute, double Positive, double Ratio, double Rms) Metrics(double[] powers)
    {
        double absolute = 0, ratio = 0, sumSquares = 0;
        for (int c = 0; c < powers.Length; c++)
        {
            double r = powers[c] / reference[c];
            double error = r - 1;
            absolute = Math.Max(absolute, Math.Abs(error));
            ratio = Math.Max(ratio, r);
            sumSquares += error * error;
        }
        return (absolute, Math.Max(0, ratio - 1), ratio, Math.Sqrt(sumSquares / powers.Length));
    }

    private void UpdatePeaks(double[] powers, double seconds)
    {
        for (int c = 0; c < powers.Length; c++)
        {
            if (powers[c] > maxima[c]) { maxima[c] = powers[c]; maximumDays[c] = seconds / 86400; }
            double ratio = powers[c] / reference[c];
            maximumRatios[c] = Math.Max(maximumRatios[c], ratio);
            maximumAbsoluteDeviations[c] = Math.Max(maximumAbsoluteDeviations[c], Math.Abs(ratio - 1));
        }
    }

    internal object Report() => new
    {
        referenceId = PracticeChannelPowerReference.ModelId,
        elapsedSimulationSeconds = elapsed,
        averaging = "simulation-time weighted, left-held accepted spatial powers; instantaneous moves have zero duration",
        rippleReference = "fixed full-power channel reference used by Game scoring; not rescaled for an 80% target",
        excludesUnappliedStartupTargetFromPeaks = target != 1,
        timeAverageMaximumAbsoluteRipplePercent = weightedMaximumAbsolute / elapsed * 100,
        timeAverageMaximumPositiveRipplePercent = weightedMaximumPositive / elapsed * 100,
        timeAverageMaximumPowerReferencePercent = weightedMaximumRatio / elapsed * 100,
        timeAverageRmsRipplePercent = weightedRms / elapsed * 100,
        meanOfChannelTemporalMaximumAbsoluteRipplePercent = maximumAbsoluteDeviations.Average() * 100,
        channels = Enumerable.Range(0, reference.Length).Select(c => new
        {
            channelIndex = c,
            gridColumn = columns[c],
            gridRow = rows[c],
            referencePowerWatts = reference[c],
            maximumPowerWatts = maxima[c],
            maximumPowerDay = maximumDays[c],
            maximumPowerReferencePercent = maximumRatios[c] * 100,
            maximumAbsoluteRipplePercent = maximumAbsoluteDeviations[c] * 100
        }).ToArray(),
        timeline
    };
}
