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
        public const string PolicyId = "practice-fuel-and-operation-v2";
        public const double MaximumOperatingPointsPerHour = 1.0;

        public static double OperatingPoints(double simulationSeconds, double powerQuality, double tiltQuality)
        {
            if (!IsFinite(simulationSeconds) || simulationSeconds < 0.0 ||
                !IsFinite(powerQuality) || powerQuality < 0.0 || powerQuality > 1.0 ||
                !IsFinite(tiltQuality) || tiltQuality < 0.0 || tiltQuality > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(simulationSeconds),
                    "Operating score requires nonnegative finite time and quality fractions from zero to one.");
            }

            return simulationSeconds / 3600.0 * MaximumOperatingPointsPerHour *
                (0.7 * powerQuality + 0.3 * tiltQuality);
        }

        internal static double OperatingPointsFromReadings(double seconds, double powerFraction, double tiltFraction)
        {
            double powerQuality = 1.0 - Math.Max(0.0, Math.Min(1.0, Math.Abs(powerFraction - 1.0) / 0.02));
            double tiltQuality = 1.0 - Math.Max(0.0, Math.Min(1.0, Math.Abs(tiltFraction) / 0.05));
            return OperatingPoints(seconds, powerQuality, tiltQuality);
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
            return new RefuellingScoreBreakdown(points.Sum(point => point + 1.5),
                points.Length * 1.5, points.Sum());
        }

        public static double DischargeBundlePoints(double burnupMwDayPerKg)
        {
            if (double.IsNaN(burnupMwDayPerKg) || double.IsInfinity(burnupMwDayPerKg))
            {
                throw new ArgumentOutOfRangeException(nameof(burnupMwDayPerKg));
            }

            return 0.75 * Math.Max(0.0, Math.Min(10.0, burnupMwDayPerKg)) - 1.5;
        }
    }
}
