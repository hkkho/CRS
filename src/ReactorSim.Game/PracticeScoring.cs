using System;
using System.Collections.Generic;
using System.Linq;
using ReactorSim.Core;

namespace ReactorSim.Game
{
    public sealed class RefuellingScoreBreakdown
    {
        internal RefuellingScoreBreakdown(double dischargeReward, double freshFuelCost, double netPoints)
        {
            DischargeReward = dischargeReward;
            FreshFuelCost = freshFuelCost;
            NetPoints = netPoints;
        }

        public string PolicyId { get; } = PracticeScoring.PolicyId;
        public double DischargeReward { get; }
        public double FreshFuelCost { get; }
        public double NetPoints { get; }
    }

    /// <summary>Authored gameplay points; never a reactor feedback input.</summary>
    public static class PracticeScoring
    {
        public const string PolicyId = "practice-channel-ripple-v3";
        public const double MaximumOperatingPointsPerHour = 1.0;
        public const double RippleScaleFraction = 0.10;

        public static double OperatingPoints(double simulationSeconds, double rmsRippleFraction)
        {
            if (!IsFinite(simulationSeconds) || simulationSeconds < 0.0 ||
                !IsFinite(rmsRippleFraction) || rmsRippleFraction < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(simulationSeconds),
                    "Ripple score requires nonnegative finite time and RMS deviation.");
            }

            double relativeError = rmsRippleFraction / RippleScaleFraction;
            return simulationSeconds / 3600.0 * MaximumOperatingPointsPerHour / (1.0 + relativeError * relativeError);
        }

        public static double RmsRipple(IReadOnlyList<double> channelPowerWatts, IReadOnlyList<double> referenceWatts, double amplitude = 1.0)
        {
            if (channelPowerWatts == null || referenceWatts == null || channelPowerWatts.Count == 0 ||
                channelPowerWatts.Count != referenceWatts.Count || !IsFinite(amplitude) || amplitude < 0)
                throw new ArgumentException("Matching channel vectors and finite nonnegative amplitude are required.");
            double sum = 0;
            for (int i = 0; i < channelPowerWatts.Count; i++)
            {
                if (!IsFinite(channelPowerWatts[i]) || channelPowerWatts[i] < 0 || !IsFinite(referenceWatts[i]) || referenceWatts[i] <= 0)
                    throw new ArgumentException("Channel powers must be finite and nonnegative; references must be positive.");
                double error = channelPowerWatts[i] * amplitude / referenceWatts[i] - 1.0;
                sum += error * error;
            }
            return Math.Sqrt(sum / channelPowerWatts.Count);
        }

        internal static RefuellingScoreBreakdown DescribeRefuelling(GameRefuellingResultV1 result)
        {
            return DescribeDischarge(result.DischargedBundles.Select(bundle =>
                bundle.CurrentBurnupJPerKgHm / GameCorePresentationConstants.JoulesPerMegaWattDayPerKilogram));
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        public static RefuellingScoreBreakdown DescribeDischarge(IEnumerable<double> burnupsMwDayPerKg)
        {
            if (burnupsMwDayPerKg == null) throw new ArgumentNullException(nameof(burnupsMwDayPerKg));
            double[] points = burnupsMwDayPerKg.Select(DischargeBundlePoints).ToArray();
            return new RefuellingScoreBreakdown(0.0, 0.0, points.Sum());
        }

        public static double DischargeBundlePoints(double burnupMwDayPerKg)
        {
            if (double.IsNaN(burnupMwDayPerKg) || double.IsInfinity(burnupMwDayPerKg))
            {
                throw new ArgumentOutOfRangeException(nameof(burnupMwDayPerKg));
            }

            return 0.0;
        }
    }
}
