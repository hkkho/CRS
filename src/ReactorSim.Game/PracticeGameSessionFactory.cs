using System;
using System.Collections.Generic;
using System.Linq;
using ReactorSim.Core;

namespace ReactorSim.Game
{
    public static class PracticeGameSessionFactory
    {
        public const string ScenarioId = "tutorial-equilibrium";
        public const string DifficultyId = "practice";
        public const string RealTimePlaybackModeId = "audit-real-time-1x";
        public const string PlayPlaybackModeId = "play-accelerated-10x";
        public const string DebugPlaybackModeId = "debug-accelerated-60x";
        public const uint WallControlTickMilliseconds = 100;
        // Browser 1x: 30 simulated minutes per real second (3 minutes per tick).
        public const double BrowserBaseSimulationSecondsPerWallSecond = 1_800.0;
        public const double BrowserScenarioHorizonSeconds = 30.0 * 24.0 * 60.0 * 60.0;
        public const string DiffusionDataPackVersion =
            "candu6-two-group-diffusion-v1-cycle190-650mwe-powerlimits-v2";
        public const double FullCoreDiffusionRecomputeIntervalSeconds = 1_800.0;
        // Burnup integrates thermal fission energy. Electrical output is a
        // separate presentation estimate at the authored conversion ratio.
        public const double PracticeReferenceThermalPowerWatts = 2_064_000_000.0;
        public const double PracticeReferenceElectricalPowerWatts = 650_000_000.0;
        public const double PracticeReferencePowerWatts = PracticeReferenceThermalPowerWatts;
        private static readonly Lazy<PracticeChannelPowerReference> ChannelReference =
            new Lazy<PracticeChannelPowerReference>(() => PracticeChannelPowerReference.Create(
                Require(FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6()), PracticeReferencePowerWatts));
        public static PracticeChannelPowerReference ReferenceChannelPower => ChannelReference.Value;

        public static GameSession Create(ulong seed = 1001)
        {
            return CreateSession(
                1.0,
                10.0,
                60.0,
                6.0,
                600.0,
                PlayPlaybackModeId, seed);
        }

        public static GameSession CreateBrowserPlaytest(ulong seed = 1001, bool challenge = false)
        {
            return CreateBrowserSession(seed, challenge, !challenge);
        }

        /// <summary>Retained bounded configuration for offline comparisons and scenario fixtures.</summary>
        public static GameSession CreateBoundedBrowserPlaytest(ulong seed = 1001, bool challenge = false)
        {
            return CreateBrowserSession(seed, challenge, false);
        }

        private static GameSession CreateBrowserSession(ulong seed, bool challenge, bool endless)
        {
            return CreateSession(
                BrowserBaseSimulationSecondsPerWallSecond,
                BrowserBaseSimulationSecondsPerWallSecond * 10.0,
                BrowserBaseSimulationSecondsPerWallSecond * 60.0,
                BrowserBaseSimulationSecondsPerWallSecond * 60.0 * WallControlTickMilliseconds / 1000.0,
                endless ? 0 : challenge ? 24.0 * 60.0 * 60.0 : BrowserScenarioHorizonSeconds,
                RealTimePlaybackModeId, seed, challenge, endless);
        }

        private static GameSession CreateSession(
            double realTimeAccelerationFactor,
            double playAccelerationFactor,
            double debugAccelerationFactor,
            double maximumPresentationAdvancePerWallTickSeconds,
            double scenarioHorizonSeconds,
            string initialPlaybackModeId,
            ulong seed,
            bool challenge = false,
            bool unlimitedFreshFuel = false)
        {
            Phase8TimeModelV1 timeModel = Require(
                Phase8TimeModelV1.TryCreate(
                    WallControlTickMilliseconds,
                    maximumPresentationAdvancePerWallTickSeconds,
                    playAccelerationFactor,
                    debugAccelerationFactor,
                    true));
            Phase8PlaybackModeV1 realTime = Require(
                Phase8PlaybackModeV1.TryCreate(RealTimePlaybackModeId, realTimeAccelerationFactor, timeModel));
            Phase8PlaybackModeV1 play = Require(
                Phase8PlaybackModeV1.TryCreate(PlayPlaybackModeId, playAccelerationFactor, timeModel));
            Phase8PlaybackModeV1 debug = Require(
                Phase8PlaybackModeV1.TryCreate(DebugPlaybackModeId, debugAccelerationFactor, timeModel));
            var playbackModes = new Dictionary<string, Phase8PlaybackModeV1>(StringComparer.Ordinal)
            {
                [realTime.ModeId] = realTime,
                [play.ModeId] = play,
                [debug.ModeId] = debug
            };

            var runtime = new PracticeRunClock(
                challenge ? ShiftProgress.ChallengeId : ScenarioId, seed,
                scenarioHorizonSeconds, timeModel, playbackModes[initialPlaybackModeId]);
            FullCoreDiffusionDataPackV1 dataPack = Require(
                FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6());
            FullCoreDiffusionModelV1 fullCoreModel = Require(
                FullCoreDiffusionModelV1.TryCreateCandu6(dataPack));
            SyntheticGameCoreStateV1 coreState = SyntheticGameCoreStateV1.CreateAgedPractice(seed);
            if (unlimitedFreshFuel) coreState = coreState.WithUnlimitedFreshFuel();
            EquilibriumCoreSolverV1 equilibriumSolver = Require(
                EquilibriumCoreSolverV1.TryCreate(
                    fullCoreModel,
                    coreState.EnumerateBundles(),
                    PracticeReferencePowerWatts));
            PracticeLiquidZoneRrsMappingV1 rrsMapping = Require(
                PracticeLiquidZoneRrsMappingV1.TryCreateCandu6());
            PracticeLiquidZoneRrsV1 initialRrs = Require(
                PracticeLiquidZoneRrsV1.TryCreate(
                    rrsMapping,
                    equilibriumSolver.CurrentProjection,
                    runtime.SimulationTimeSeconds));
            PracticeLiquidZoneRrsEquilibriumResultV1 acceptedRrs = Require(
                PracticeLiquidZoneRrsV1.TryRunEquilibrium(
                    equilibriumSolver,
                    coreState.EnumerateBundles(),
                    initialRrs,
                    runtime.SimulationTimeSeconds));
            Require(equilibriumSolver.TryCommitCandidate(acceptedRrs.Projection));
            // Settle the assumed pre-run core without consuming run time/fuel.
            // Each controller pass retains its normal bounded movement contract.
            for (int pass = 0; pass < 7 &&
                Math.Abs(acceptedRrs.State.CompensatedNetReactivity) > PracticeLiquidZoneRrsIdentityV1.CriticalityTolerance;
                pass++)
            {
                acceptedRrs = Require(PracticeLiquidZoneRrsV1.TryRunEquilibrium(
                    equilibriumSolver, coreState.EnumerateBundles(), acceptedRrs.State, runtime.SimulationTimeSeconds));
                Require(equilibriumSolver.TryCommitCandidate(acceptedRrs.Projection));
            }
            var session = new GameSession(
                runtime,
                playbackModes,
                WallControlTickMilliseconds,
                coreState,
                equilibriumSolver,
                acceptedRrs.State, challenge);
            // Let the player read the objective and inspect fuel before the challenge clock starts.
            if (challenge) session.Pause();
            return session;
        }

        private static T Require<T>(ContractValidationResult<T> result)
        {
            if (!result.IsValid)
            {
                throw new InvalidOperationException(result.FirstDiagnostic.ToString());
            }

            return result.Value;
        }
    }
}
