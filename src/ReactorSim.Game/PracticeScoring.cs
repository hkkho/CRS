using System;
using System.Collections.Generic;
using System.Linq;
using ReactorSim.Core;

namespace ReactorSim.Game
{
    public sealed class RefuellingScoreBreakdown
    {
        internal RefuellingScoreBreakdown(double dischargeReward, double earlyDischargePenalty, double zoneBalanceBonus)
        {
            DischargeReward = dischargeReward;
            FreshFuelCost = earlyDischargePenalty;
            ZoneBalanceBonus = zoneBalanceBonus;
        }

        public string PolicyId { get; } = PracticeScoring.PolicyId;
        public double DischargeReward { get; }
        // Legacy contract name for the point deduction on a fuel move.
        public double FreshFuelCost { get; }
        public double ZoneBalanceBonus { get; }
        public double NetPoints => DischargeReward - FreshFuelCost + ZoneBalanceBonus;
    }

    /// <summary>Authored gameplay points; never a reactor feedback input.</summary>
    public static class PracticeScoring
    {
        public const string PolicyId = "practice-burnup-zone-ripple-tilt-v4";

        public static double OperatingPoints(double simulationSeconds, double maximumRipple, double axialTiltFraction)
        {
            if (!IsFinite(simulationSeconds) || simulationSeconds < 0.0)
                throw new ArgumentOutOfRangeException(nameof(simulationSeconds));

            return simulationSeconds / 3600.0 * (RipplePointsPerHour(maximumRipple) + TiltPointsPerHour(axialTiltFraction));
        }

        public static double RipplePointsPerHour(double maximumRipple)
        {
            if (!IsFinite(maximumRipple) || maximumRipple < 0.0)
                throw new ArgumentOutOfRangeException(nameof(maximumRipple));

            if (maximumRipple < 1.08) return 0.5;
            return maximumRipple <= 1.10 ? 0.25 : 0.0;
        }

        public static double TiltPointsPerHour(double axialTiltFraction)
        {
            if (!IsFinite(axialTiltFraction))
                throw new ArgumentOutOfRangeException(nameof(axialTiltFraction));

            return 0.5 * Math.Max(0.0, 1.0 - Math.Abs(axialTiltFraction) / 0.20);
        }

        internal static double OperatingPoints(double simulationSeconds, EquilibriumCoreProjectionV1 projection,
            double amplitude, PracticeChannelPowerReference? customReference)
        {
            double maximumRipple = 0.0;
            var referenceWatts = (customReference ?? PracticeGameSessionFactory.ReferenceChannelPower).ChannelPowerWatts;
            for (int i = 0; i < referenceWatts.Count; i++)
                maximumRipple = Math.Max(maximumRipple, projection.ShapeChannelPowerWatts[i] * amplitude / referenceWatts[i]);

            return OperatingPoints(simulationSeconds, maximumRipple,
                GamePresentationProjector.ComputeSignedAxialTiltFraction(projection.SpatialSolve.Group2Flux));
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

        internal static RefuellingScoreBreakdown DescribeRefuelling(GameRefuellingResultV1 result, PracticeLiquidZoneRrsV1 rrs)
        {
            double meanDistance = rrs.FillFractions.Average(fill => Math.Abs(fill - 0.5));
            double zoneBalanceBonus = 2.0 * (1.0 - meanDistance / 0.5);
            return DescribeDischarge(result.DischargedBundles.Select(bundle =>
                bundle.CurrentBurnupJPerKgHm / GameCorePresentationConstants.JoulesPerMegaWattDayPerKilogram),
                zoneBalanceBonus);
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        public static RefuellingScoreBreakdown DescribeDischarge(IEnumerable<double> burnupsMwDayPerKg) =>
            DescribeDischarge(burnupsMwDayPerKg, 0.0);

        private static RefuellingScoreBreakdown DescribeDischarge(IEnumerable<double> burnupsMwDayPerKg, double zoneBalanceBonus)
        {
            if (burnupsMwDayPerKg == null) throw new ArgumentNullException(nameof(burnupsMwDayPerKg));
            double reward = 0.0, penalty = 0.0;
            foreach (double burnup in burnupsMwDayPerKg)
            {
                double points = DischargeBundlePoints(burnup);
                reward += Math.Max(0.0, points);
                penalty += Math.Max(0.0, -points);
            }
            return new RefuellingScoreBreakdown(reward, penalty, zoneBalanceBonus);
        }

        public static double DischargeBundlePoints(double burnupMwDayPerKg)
        {
            if (!IsFinite(burnupMwDayPerKg) || burnupMwDayPerKg < 0.0)
                throw new ArgumentOutOfRangeException(nameof(burnupMwDayPerKg));

            return burnupMwDayPerKg - ShiftProgress.UsefulBurnupThresholdMwDayPerKg;
        }
    }
}
