using System;
using System.Collections.Generic;
using System.Linq;

namespace ReactorSim.Game
{
    /// <summary>Immutable report for one committed daily decision, including early endings.</summary>
    public sealed class DailyTurnResult
    {
        internal DailyTurnResult(double start, GameSessionSnapshot end, IReadOnlyList<uint> requested,
            IReadOnlyList<RefuellingMovement> movements, uint fuelBefore, uint usefulBefore,
            double scoreBefore, double energyBefore)
        {
            StartSimulationTimeSeconds = start;
            EndSimulationTimeSeconds = end.SimulationTimeSeconds;
            RequestedChannels = Array.AsReadOnly(requested.ToArray());
            Movements = Array.AsReadOnly(movements.ToArray());
            ExecutedChannels = Array.AsReadOnly(movements.Select(m => m.ChannelIndex).ToArray());
            UnexecutedChannels = Array.AsReadOnly(requested.Except(ExecutedChannels).ToArray());
            FuelUsed = end.Shift.FuelConsumed - fuelBefore;
            UsefulBundlesDischarged = end.Shift.UsefulBundlesDischarged - usefulBefore;
            ScoreDelta = end.ScoreTotal - scoreBefore;
            ThermalEnergyMwh = end.Shift.ThermalEnergyMwh - energyBefore;
            ElectricalEnergyMwhEstimate = ThermalEnergyMwh * PracticeGameSessionFactory.PracticeReferenceElectricalPowerWatts / PracticeGameSessionFactory.PracticeReferenceThermalPowerWatts;
            AverageLzcFillFraction = end.Rrs.AverageFillFraction;
            AxialTiltFraction = end.AxialTiltFraction;
            EndReason = end.GameOverReason;
        }
        public double StartSimulationTimeSeconds { get; }
        public double EndSimulationTimeSeconds { get; }
        public IReadOnlyList<uint> RequestedChannels { get; }
        public IReadOnlyList<uint> ExecutedChannels { get; }
        public IReadOnlyList<uint> UnexecutedChannels { get; }
        public IReadOnlyList<RefuellingMovement> Movements { get; }
        public uint FuelUsed { get; }
        public uint UsefulBundlesDischarged { get; }
        public double ScoreDelta { get; }
        public double ThermalEnergyMwh { get; }
        public double ElectricalEnergyMwhEstimate { get; }
        public double AverageLzcFillFraction { get; }
        public double AxialTiltFraction { get; }
        public string EndReason { get; }
    }
}
