using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("ReactorSim.Game.Tests")]

namespace ReactorSim.Game
{
    /// <summary>Game-owned objective and cumulative results, independent of the reactor solver.</summary>
    public sealed class ShiftProgress
    {
        public const string PracticeId = "free-practice";
        public const string ChallengeId = "useful-fuel-day-v1";
        public const uint UsefulBundleGoal = 8;
        public const double UsefulBurnupThresholdMwDayPerKg = 6.0;

        internal ShiftProgress(bool challenge, ulong seed, double horizon, double time,
            bool terminal, bool horizonCompleted, uint fuelConsumed, uint usefulDischarged,
            double thermalEnergyJoules, double dischargeReward, double freshFuelCost, double score, bool standardRun = true)
        {
            Id = challenge ? ChallengeId : PracticeId;
            Seed = seed;
            Title = challenge ? "One useful fuel day" : "Free practice";
            Objective = challenge
                ? "Discharge 8 bundles at 6 MWd/kg or above, then finish the day with regulating headroom."
                : "Keep channel powers close to their time-average reference to reduce ripple and build score.";
            HorizonSeconds = horizon;
            RemainingSeconds = Math.Max(0.0, horizon - time);
            FuelBudget = ReactorSim.Core.SyntheticGameCoreStateV1.DefaultFreshBundleCount;
            FuelConsumed = fuelConsumed;
            UsefulBundlesDischarged = usefulDischarged;
            UsefulBundlesRequired = challenge ? UsefulBundleGoal : 0;
            UsefulBurnupThresholdMwdPerKg = UsefulBurnupThresholdMwDayPerKg;
            ThermalEnergyMwh = thermalEnergyJoules / 3_600_000_000.0;
            ElectricalEnergyMwhEstimate = ThermalEnergyMwh *
                PracticeGameSessionFactory.PracticeReferenceElectricalPowerWatts /
                PracticeGameSessionFactory.PracticeReferenceThermalPowerWatts;
            DischargeReward = dischargeReward;
            FreshFuelCost = freshFuelCost;
            OperatingPoints = score - (dischargeReward - freshFuelCost);
            Outcome = !terminal ? "in-progress" : !horizonCompleted ? "ended" :
                challenge && usefulDischarged < UsefulBundleGoal ? "missed" : "success";
            Reward = challenge ? "Efficient refueller badge" : "";
            RewardEarned = challenge && standardRun && Outcome == "success";
        }

        public string Id { get; }
        public ulong Seed { get; }
        public string Title { get; }
        public string Objective { get; }
        public double HorizonSeconds { get; }
        public double RemainingSeconds { get; }
        public uint FuelBudget { get; }
        public uint FuelConsumed { get; }
        public uint UsefulBundlesDischarged { get; }
        public uint UsefulBundlesRequired { get; }
        public double UsefulBurnupThresholdMwdPerKg { get; }
        public double ThermalEnergyMwh { get; }
        public double ElectricalEnergyMwhEstimate { get; }
        public double DischargeReward { get; }
        public double FreshFuelCost { get; }
        public double OperatingPoints { get; }
        public string Outcome { get; }
        public string Reward { get; }
        public bool RewardEarned { get; }
    }
}
