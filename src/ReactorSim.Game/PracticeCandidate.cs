using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using ReactorSim.Core;

namespace ReactorSim.Game
{
    internal sealed class PracticeCandidate
    {
        internal PracticeCandidate(
            SyntheticGameCoreStateV1 coreState,
            EquilibriumCoreProjectionV1 spatialCandidate,
            PracticeLiquidZoneRrsV1 rrs,
            double lastFullCoreSolveSimulationTime,
            double syntheticScore,
            ulong powerProjectionVersion,
            PracticeXenonStateV1 xenon,
            PracticeXenonStateV1? coupledXenon = null,
            double thermalEnergyJoules = 0.0, PracticeRunAdvance? clock = null,
            double operatingPowerAmplitude = 1.0)
        {
            CoreState = coreState;
            SpatialCandidate = spatialCandidate;
            Rrs = rrs;
            LastFullCoreSolveSimulationTime = lastFullCoreSolveSimulationTime;
            SyntheticScore = syntheticScore;
            PowerProjectionVersion = powerProjectionVersion;
            Xenon = xenon;
            CoupledXenon = coupledXenon ?? xenon;
            ThermalEnergyJoules = thermalEnergyJoules; Clock = clock;
            OperatingPowerAmplitude = operatingPowerAmplitude;
        }

        internal SyntheticGameCoreStateV1 CoreState { get; }
        internal PracticeXenonStateV1 Xenon { get; }
        internal PracticeXenonStateV1 CoupledXenon { get; }

        internal EquilibriumCoreProjectionV1 SpatialCandidate { get; }

        internal PracticeLiquidZoneRrsV1 Rrs { get; }

        internal double LastFullCoreSolveSimulationTime { get; }

        internal double SyntheticScore { get; }
        internal double ThermalEnergyJoules { get; }

        internal ulong PowerProjectionVersion { get; }
        internal PracticeRunAdvance? Clock { get; }
        internal double OperatingPowerAmplitude { get; }
    }
    internal sealed class PracticeAdvanceBuilder
    {
        internal PracticeAdvanceBuilder(
            SyntheticGameCoreStateV1 coreState,
            EquilibriumCoreProjectionV1 spatialCandidate,
            PracticeLiquidZoneRrsV1 rrs,
            double lastFullCoreSolveSimulationTime,
            double syntheticScore,
            ulong powerProjectionVersion,
            PracticeXenonStateV1 xenon,
            PracticeXenonStateV1? coupledXenon = null,
            double thermalEnergyJoules = 0.0)
        {
            CoreState = coreState;
            SpatialCandidate = spatialCandidate;
            Rrs = rrs;
            LastFullCoreSolveSimulationTime = lastFullCoreSolveSimulationTime;
            SyntheticScore = syntheticScore;
            PowerProjectionVersion = powerProjectionVersion;
            Xenon = xenon;
            CoupledXenon = coupledXenon ?? xenon;
            ThermalEnergyJoules = thermalEnergyJoules;
        }

        internal SyntheticGameCoreStateV1 CoreState;
        internal PracticeXenonStateV1 Xenon;
        internal PracticeXenonStateV1 CoupledXenon;

        internal EquilibriumCoreProjectionV1 SpatialCandidate;

        internal PracticeLiquidZoneRrsV1 Rrs;

        internal double LastFullCoreSolveSimulationTime;

        internal double SyntheticScore;
        internal double ThermalEnergyJoules;

        internal ulong PowerProjectionVersion;

        internal PracticeCandidate Freeze(PracticeRunAdvance clock, double? operatingPowerAmplitude = null) => new PracticeCandidate(
            CoreState, SpatialCandidate, Rrs, LastFullCoreSolveSimulationTime,
            SyntheticScore, PowerProjectionVersion, Xenon, CoupledXenon, ThermalEnergyJoules, clock,
            operatingPowerAmplitude ?? clock.Candidate.NormalizedPowerFraction);
    }
}
