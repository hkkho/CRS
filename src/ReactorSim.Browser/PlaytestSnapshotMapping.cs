using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReactorSim.Core;
using ReactorSim.Game;

namespace ReactorSim.Browser
{
    public sealed partial class PlaytestRuntime
    {
        private PlaytestSnapshotDto CreateSnapshot(
            BridgeRuntime runtime,
            double previousScore)
        {
            return CreateSnapshot(runtime, GetCurrentGameSnapshot(runtime), previousScore);
        }

        private PlaytestSnapshotDto CreateSnapshot(
            BridgeRuntime runtime,
            GameSessionSnapshot game,
            double previousScore)
        {
            double scoreDelta = game.ScoreTotal - previousScore;
            if (previousScore == 0.0 && runtime.Sequence == 0)
            {
                scoreDelta = 0.0;
            }

            string playback = GetPlaybackMode(game);

            return new PlaytestSnapshotDto
            {
                Protocol = PlaytestProtocolV2.ProtocolId,
                Source = "wasm",
                Sequence = runtime.Sequence,
                ScenarioId = game.ScenarioId,
                DataPackId = DefaultDataPackId,
                SimulationTimeSeconds = game.SimulationTimeSeconds,
                WallElapsedSeconds = game.WallElapsedSeconds,
                NormalizedPowerFraction = game.NormalizedPowerFraction,
                TargetPowerFraction = game.Physics.TargetPowerWatts / game.Physics.ReferencePowerWatts,
                AxialTiltFraction = game.AxialTiltFraction,
                RrsReserveFraction = game.RrsReserveFraction,
                DeviceAvailableFraction = game.DeviceAvailableFraction,
                PendingActionCount = game.PendingActionCount,
                ScoreTotal = game.ScoreTotal,
                Shift = game.Shift,
                Provenance = game.Provenance,
                ScorePolicyId = game.ScorePolicyId,
                Ripple = game.Ripple,
                LastRefuellingScore = game.LastRefuellingScore,
                LastFuelMovement = game.LastFuelMovement,
                RefuellingPlans = game.RefuellingPlans,
                ScoreDelta = scoreDelta,
                IsPaused = game.IsPaused,
                RunStatus = game.RunStatus,
                RunEndReason = game.GameOverReason,
                PlaybackModeId = playback,
                FreshBundlesAvailable = game.FreshBundlesAvailable,
                RefuellingOperationCount = game.RefuellingOperationCount,
                LastDischargedMaximumBurnupMwdPerKg = game.LastDischargedMaximumBurnupMwDayPerKg,
                MaximumDischargedBurnupMwdPerKg = game.MaximumDischargedBurnupMwDayPerKg,
                LastRefuelledChannel = game.RefuellingOperationCount == 0
                    ? -1
                    : game.LastRefuelledChannel,
                LastRefuellingDirectionId = game.RefuellingOperationCount == 0
                    ? null
                    : game.LastRefuellingDirectionId,
                LastRefuellingShiftCount = game.RefuellingOperationCount == 0
                    ? (ushort)0
                    : game.LastRefuellingShiftCount,
                Physics = CreatePhysicsSnapshot(game),
                Xenon = CreateXenonSnapshot(game),
                Rrs = CreateRrsSnapshot(game),
                Core = CreateCoreSnapshot(runtime, game.Core, game.Physics.MeanBundlePowerWatts),
                Diagnostics = CreateDiagnosticsSnapshot(runtime, game),
                LastEvent = CreateLastEvent(runtime, game)
            };
        }

        private PlaytestCoreDto CreateCoreSnapshot(
            BridgeRuntime runtime,
            GameCorePresentationSnapshot core,
            double meanBundlePowerWatts)
        {
#if RUNTIME_PROFILE
            using var profileScope = ReactorSim.Core.RuntimeProfile.Measure("bridge-core");
#endif
            Interlocked.Increment(ref _coreSnapshotMaterializationCount);
            return new PlaytestCoreDto
            {
                ChannelCount = GameCorePresentationConstants.ChannelCount,
                BundlePositionCount = GameCorePresentationConstants.BundlePositionCount,
                GridWidth = GameCorePresentationConstants.GridWidth,
                GridHeight = GameCorePresentationConstants.GridHeight,
                Channels = core.Channels
                    .Select(channel => new PlaytestChannelDto
                    {
                        ChannelIndex = channel.ChannelIndex,
                        GridColumn = channel.GridColumn,
                        GridRow = channel.GridRow,
                        FlowDirection = channel.FlowDirection == FlowDirection.EndAtoEndB
                            ? TowardEndB
                            : TowardEndA,
                        AverageBurnupMwdPerKg = channel.AverageBurnupMwDayPerKg,
                        CanRefuel = channel.CanRefuel,
                        RefuellingIneligibilityReason = channel.RefuellingIneligibilityReason,
                        PowerWatts = channel.PowerWatts,
                        LocalPowerFraction = channel.LocalPowerFraction,
                        LocalTiltFraction = channel.LocalTiltFraction,
                        Xenon = new PlaytestXenonChannelDto
                        {
                            ChannelIndex = channel.Xenon.ChannelIndex,
                            MeanI135NumberDensityM3 = channel.Xenon.MeanI135NumberDensityM3,
                            MaxI135NumberDensityM3 = channel.Xenon.MaxI135NumberDensityM3,
                            MeanXe135NumberDensityM3 = channel.Xenon.MeanXe135NumberDensityM3,
                            MaxXe135NumberDensityM3 = channel.Xenon.MaxXe135NumberDensityM3,
                            MeanDynamicAbsorptionGroup1PerM = channel.Xenon.MeanDynamicAbsorptionGroup1PerM,
                            MaxDynamicAbsorptionGroup1PerM = channel.Xenon.MaxDynamicAbsorptionGroup1PerM,
                            MeanDynamicAbsorptionGroup2PerM = channel.Xenon.MeanDynamicAbsorptionGroup2PerM,
                            MaxDynamicAbsorptionGroup2PerM = channel.Xenon.MaxDynamicAbsorptionGroup2PerM
                        },
                        Bundles = channel.Bundles
                            .Select(bundle => new PlaytestBundleDto
                            {
                                Position = bundle.Position,
                                BundleId = bundle.BundleId,
                                FuelTypeId = bundle.FuelTypeId,
                                CurrentBurnupMwdPerKg = bundle.CurrentBurnupMwDayPerKg,
                                PowerWatts = bundle.PowerWatts,
                                LocalPowerFraction = meanBundlePowerWatts <= 0.0
                                    ? 0.0
                                    : bundle.PowerWatts / meanBundlePowerWatts,
                                InsertedAtSeconds = bundle.InsertedAtSeconds,
                                StateVersion = bundle.StateVersion,
                                IsFresh = bundle.IsFresh,
                                HasFuel = runtime.PlaySession.IsFuelCell(
                                    channel.ChannelIndex,
                                    bundle.Position),
                                ReflectiveFaces = runtime.PlaySession.GetReflectiveFaces(
                                        channel.ChannelIndex,
                                        bundle.Position)
                                    .Select(TopologyFaceId)
                                    .ToList(),
                                Group1Flux = runtime.PlaySession.GetCellGroup1Flux(
                                    channel.ChannelIndex,
                                    bundle.Position),
                                Group2Flux = runtime.PlaySession.GetCellGroup2Flux(
                                    channel.ChannelIndex,
                                    bundle.Position),
                                LogicalZoneId = runtime.PlaySession.CurrentLiquidZoneRrs.Mapping.GetLogicalZoneId(
                                    new NodeKey(new ChannelId(channel.ChannelIndex), new BundlePosition(bundle.Position))),
                                AbsorberZoneId = runtime.PlaySession.CurrentLiquidZoneRrs.Mapping
                                    .GetNodeBinding(checked((int)(channel.ChannelIndex * 12 + bundle.Position))).AbsorberZoneId,
                                Group1AbsorptionPerMPerFillFraction = runtime.PlaySession.CurrentLiquidZoneRrs.Mapping
                                    .GetNodeBinding(checked((int)(channel.ChannelIndex * 12 + bundle.Position))).Group1AbsorptionPerMPerFillFraction,
                                Group2AbsorptionPerMPerFillFraction = runtime.PlaySession.CurrentLiquidZoneRrs.Mapping
                                    .GetNodeBinding(checked((int)(channel.ChannelIndex * 12 + bundle.Position))).Group2AbsorptionPerMPerFillFraction
                            })
                            .ToList()
                    })
                    .ToList()
            };
        }

        private static PlaytestSnapshotPatchDto CreateSnapshotPatch(
            BridgeRuntime runtime,
            GameSessionSnapshot game,
            double previousScore)
        {
#if RUNTIME_PROFILE
            using var profileScope = ReactorSim.Core.RuntimeProfile.Measure("bridge-patch");
#endif
            double scoreDelta = game.ScoreTotal - previousScore;
            if (previousScore == 0.0 && runtime.Sequence == 0)
            {
                scoreDelta = 0.0;
            }

            return new PlaytestSnapshotPatchDto
            {
                ScenarioId = game.ScenarioId,
                DataPackId = DefaultDataPackId,
                SimulationTimeSeconds = game.SimulationTimeSeconds,
                WallElapsedSeconds = game.WallElapsedSeconds,
                NormalizedPowerFraction = game.NormalizedPowerFraction,
                TargetPowerFraction = game.Physics.TargetPowerWatts / game.Physics.ReferencePowerWatts,
                AxialTiltFraction = game.AxialTiltFraction,
                RrsReserveFraction = game.RrsReserveFraction,
                DeviceAvailableFraction = game.DeviceAvailableFraction,
                PendingActionCount = game.PendingActionCount,
                ScoreTotal = game.ScoreTotal,
                Shift = game.Shift,
                Provenance = game.Provenance,
                ScorePolicyId = game.ScorePolicyId,
                Ripple = game.Ripple,
                LastRefuellingScore = game.LastRefuellingScore,
                LastFuelMovement = game.LastFuelMovement,
                RefuellingPlans = game.RefuellingPlans,
                ScoreDelta = scoreDelta,
                IsPaused = game.IsPaused,
                RunStatus = game.RunStatus,
                RunEndReason = game.GameOverReason,
                PlaybackModeId = GetPlaybackMode(game),
                FreshBundlesAvailable = game.FreshBundlesAvailable,
                RefuellingOperationCount = game.RefuellingOperationCount,
                LastDischargedMaximumBurnupMwdPerKg = game.LastDischargedMaximumBurnupMwDayPerKg,
                MaximumDischargedBurnupMwdPerKg = game.MaximumDischargedBurnupMwDayPerKg,
                LastRefuelledChannel = game.RefuellingOperationCount == 0
                    ? -1
                    : game.LastRefuelledChannel,
                LastRefuellingDirectionId = game.RefuellingOperationCount == 0
                    ? null
                    : game.LastRefuellingDirectionId,
                LastRefuellingShiftCount = game.RefuellingOperationCount == 0
                    ? (ushort)0
                    : game.LastRefuellingShiftCount,
                Physics = CreatePhysicsSnapshot(game),
                Xenon = CreateXenonSnapshot(game),
                Rrs = CreateRrsSnapshot(game),
                Diagnostics = CreateDiagnosticsSnapshot(runtime, game),
                LastEvent = CreateLastEvent(runtime, game)
            };
        }

        private static string GetPlaybackMode(GameSessionSnapshot game)
        {
            return game.IsPaused
                ? "pause"
                : game.PlaybackModeId == PracticeGameSessionFactory.RealTimePlaybackModeId
                    ? "1x"
                    : game.PlaybackModeId == PracticeGameSessionFactory.DebugPlaybackModeId
                        ? "60x"
                        : "10x";
        }

        private static PlaytestPhysicsDto CreatePhysicsSnapshot(GameSessionSnapshot game)
        {
            return new PlaytestPhysicsDto
            {
                SourceId = game.Physics.SourceId,
                FormulationId = game.Physics.FormulationId,
                ShapeMethodId = game.Physics.ShapeMethodId,
                AmplitudeMethodId = game.Physics.AmplitudeMethodId,
                ReactivityMethodId = game.Physics.ReactivityMethodId,
                SolveState = game.Physics.SolveState,
                IsAuthoritative = game.Physics.IsAuthoritative,
                BindingVersion = game.Physics.BindingVersion,
                ReferencePowerWatts = game.Physics.ReferencePowerWatts,
                PowerAmplitude = game.Physics.PowerAmplitude,
                ActualPowerFraction = game.Physics.ActualPowerFraction,
                TargetPowerWatts = game.Physics.TargetPowerWatts,
                TotalPowerWatts = game.Physics.TotalPowerWatts,
                ElectricalPowerWatts = game.Physics.ElectricalPowerWatts,
                MeanChannelPowerWatts = game.Physics.MeanChannelPowerWatts,
                MeanBundlePowerWatts = game.Physics.MeanBundlePowerWatts,
                EffectiveK = game.Physics.EffectiveK,
                Reactivity = game.Physics.Reactivity,
                WeightedPerturbationReactivity = game.Physics.WeightedPerturbationReactivity,
                ReactivityNumerator = game.Physics.ReactivityNumerator,
                ReactivityDenominator = game.Physics.ReactivityDenominator,
                ReactivityIdentity = game.Physics.ReactivityIdentity,
                ReactivityBindingDigestHex = game.Physics.ReactivityBindingDigestHex,
                CoreReactivity = game.Physics.CoreReactivity,
                CompensatedNetReactivity = game.Physics.CompensatedNetReactivity,
                CompensationState = game.Physics.CompensationState,
                CompensationCommand = game.Physics.CompensationCommand,
                CompensationLowerBound = game.Physics.CompensationLowerBound,
                CompensationUpperBound = game.Physics.CompensationUpperBound,
                CompensationSaturated = game.Physics.CompensationSaturated,
                CompensationResponseTimeSeconds = game.Physics.CompensationResponseTimeSeconds,
                CadenceIdentity = game.Physics.CadenceIdentity,
                AdjointNormalizationIdentity = game.Physics.AdjointNormalizationIdentity,
                AdjointDigestHex = game.Physics.AdjointDigestHex,
                AdjointIterationCount = game.Physics.AdjointIterationCount,
                AdjointTransposeResidualRelativeInfinity = game.Physics.AdjointTransposeResidualRelativeInfinity,
                PowerBalanceRelativeError = game.Physics.PowerBalanceRelativeError,
                SolverIdentity = game.Physics.SolverIdentity,
                SolverIterationCount = game.Physics.SolverIterationCount,
                SolverResidualRelativeInfinity = game.Physics.SolverResidualRelativeInfinity
            };
        }

        private static PlaytestRrsDto CreateRrsSnapshot(GameSessionSnapshot game)
        {
            return new PlaytestRrsDto
            {
                ControllerIdentity = game.Rrs.ControllerIdentity,
                DecisionCode = game.Rrs.DecisionCode,
                DecisionExplanation = game.Rrs.DecisionExplanation,
                LimitingZoneId = game.Rrs.LimitingZoneId,
                MappingIdentity = game.Rrs.MappingIdentity,
                MappingDigestHex = game.Rrs.MappingDigestHex,
                OverlayIdentity = game.Rrs.OverlayIdentity,
                OverlayDigestHex = game.Rrs.OverlayDigestHex,
                StateDigestHex = game.Rrs.StateDigestHex,
                SimulationTimeSeconds = game.Rrs.SimulationTimeSeconds,
                NodeCount = game.Rrs.NodeCount,
                AbsorptionReferenceFillFraction = PracticeLiquidZoneRrsIdentityV1.AbsorptionReferenceFillFraction,
                CalibratedTotalZoneWorthMk = PracticeLiquidZoneRrsIdentityV1.CalibratedTotalZoneWorthMk,
                AverageFillFraction = game.Rrs.AverageFillFraction,
                MinimumFillFraction = game.Rrs.MinimumFillFraction,
                MaximumFillFraction = game.Rrs.MaximumFillFraction,
                MeasuredPowerWatts = game.Rrs.MeasuredPowerWatts,
                TargetPowerWatts = game.Rrs.TargetPowerWatts,
                PowerErrorWatts = game.Rrs.PowerErrorWatts,
                CoreReactivity = game.Rrs.CoreReactivity,
                CompensatedNetReactivity = game.Rrs.CompensatedNetReactivity,
                CommonModeRhoCorrection = game.Rrs.CommonModeRhoCorrection,
                ControllerIterationCount = game.Rrs.ControllerIterationCount,
                ControllerConverged = game.Rrs.ControllerConverged,
                ResponseModelIdentity = game.Rrs.ResponseModelIdentity,
                ResponseModelDigestHex = game.Rrs.ResponseModelDigestHex,
                AppliedFillCommand = game.Rrs.AppliedFillCommand.ToList(),
                ControlledBaselineWeightedResidual = game.Rrs.ControlledBaselineWeightedResidual,
                CombinedWeightedResidual = game.Rrs.CombinedWeightedResidual,
                CandidateSolveCount = game.Rrs.CandidateSolveCount,
                VerificationSolveCount = game.Rrs.VerificationSolveCount,
                CorrectionSolveCount = game.Rrs.CorrectionSolveCount,
                CorrectionApplied = game.Rrs.CorrectionApplied,
                LowExhaustion = game.Rrs.LowExhaustion,
                HighExhaustion = game.Rrs.HighExhaustion,
                IsGameOver = game.Rrs.IsGameOver,
                GameOverReason = game.Rrs.GameOverReason,
                CadenceIdentity = game.Rrs.CadenceIdentity,
                Zones = game.Rrs.Zones
                    .Select(zone => new PlaytestRrsZoneDto
                    {
                        LogicalZoneId = zone.LogicalZoneId,
                        FillFraction = zone.FillFraction,
                        ReferencePowerFraction = zone.ReferencePowerFraction,
                        TargetPowerFraction = zone.TargetPowerFraction,
                        MeasuredPowerFraction = zone.MeasuredPowerFraction,
                        ShapeError = zone.ShapeError,
                        MeanI135NumberDensityM3 = zone.MeanI135NumberDensityM3,
                        MeanXe135NumberDensityM3 = zone.MeanXe135NumberDensityM3
                    })
                    .ToList()
            };
        }

        private static PlaytestDiagnosticsDto CreateDiagnosticsSnapshot(
            BridgeRuntime runtime,
            GameSessionSnapshot game)
        {
            double relativePowerError = game.Physics.TargetPowerWatts <= 0.0
                ? 0.0
                : Math.Abs(game.Physics.TotalPowerWatts - game.Physics.TargetPowerWatts) /
                  game.Physics.TargetPowerWatts;
            PlaytestConvergenceDto convergence = new PlaytestConvergenceDto
            {
                State = game.Physics.SolveState,
                Iterations = game.Physics.SolverIterationCount,
                Residual = game.Physics.SolverResidualRelativeInfinity,
                RelativePowerError = relativePowerError,
                LastSolveMilliseconds = 0.0,
                SolverLabel = game.Physics.SolverIdentity
            };

            return new PlaytestDiagnosticsDto
            {
                Convergence = convergence,
                Checks = new List<PlaytestCheckDto>
                {
                    new PlaytestCheckDto
                    {
                        Label = "Topology",
                        Value = "380 × 12",
                        Status = "pass"
                    },
                    new PlaytestCheckDto
                    {
                        Label = "LZC average level",
                        Value = FormatPercent(game.Rrs.AverageFillFraction),
                        Status = game.Rrs.AverageFillFraction >= 0.3 && game.Rrs.AverageFillFraction <= 0.7 ? "pass" : "watch"
                    },
                    new PlaytestCheckDto
                    {
                        Label = "Data provenance",
                        Value = "synthetic-calibrated full-core pack",
                        Status = "info"
                    }
                }
            };
        }

        private static PlaytestEventDto CreateLastEvent(
            BridgeRuntime runtime,
            GameSessionSnapshot game)
        {
            return runtime.LastEvent ?? new PlaytestEventDto
            {
                EventId = "wasm-event-ready",
                TimeSeconds = game.SimulationTimeSeconds,
                Title = "Practice session online",
                Detail = "Select a channel to inspect the 12-position bundle stack.",
                Tone = "info"
            };
        }

        private static PlaytestXenonDto CreateXenonSnapshot(GameSessionSnapshot game)
        {
            GameXenonPresentationSnapshot xenon = game.Xenon;
            int selectedChannelIndex = xenon.SelectedChannelIndex;
            return new PlaytestXenonDto
            {
                StateIdentity = xenon.StateIdentity,
                StateDigestHex = xenon.StateDigestHex,
                StateVersion = xenon.StateVersion,
                SimulationTimeSeconds = xenon.SimulationTimeSeconds,
                NodeCount = xenon.NodeCount,
                NodeI135NumberDensityM3 = xenon.NodeI135NumberDensityM3,
                NodeXe135NumberDensityM3 = xenon.NodeXe135NumberDensityM3,
                CouplingIdentity = xenon.CouplingIdentity,
                HasCoupling = xenon.HasCoupling,
                BaseCoefficientDigestHex = xenon.BaseCoefficientDigestHex,
                DynamicXenonDigestHex = xenon.DynamicXenonDigestHex,
                CoupledSimulationTimeSeconds = xenon.CoupledSimulationTimeSeconds,
                CoupledStateDigestHex = xenon.CoupledStateDigestHex,
                EffectiveCoefficientDigestHex = xenon.EffectiveCoefficientDigestHex,
                MeanI135NumberDensityM3 = xenon.MeanI135NumberDensityM3,
                MaxI135NumberDensityM3 = xenon.MaxI135NumberDensityM3,
                MeanXe135NumberDensityM3 = xenon.MeanXe135NumberDensityM3,
                MaxXe135NumberDensityM3 = xenon.MaxXe135NumberDensityM3,
                MeanDynamicAbsorptionGroup1PerM = xenon.MeanDynamicAbsorptionGroup1PerM,
                MaxDynamicAbsorptionGroup1PerM = xenon.MaxDynamicAbsorptionGroup1PerM,
                MeanDynamicAbsorptionGroup2PerM = xenon.MeanDynamicAbsorptionGroup2PerM,
                MaxDynamicAbsorptionGroup2PerM = xenon.MaxDynamicAbsorptionGroup2PerM,
                SelectedChannelIndex = selectedChannelIndex,
                SelectedChannel = xenon.SelectedChannel == null
                    ? null
                    : CreateXenonChannelSnapshot(xenon.SelectedChannel)
            };
        }

        private static PlaytestXenonChannelDto CreateXenonChannelSnapshot(
            GameXenonChannelPresentationSnapshot channel)
        {
            return new PlaytestXenonChannelDto
            {
                ChannelIndex = channel.ChannelIndex,
                MeanI135NumberDensityM3 = channel.MeanI135NumberDensityM3,
                MaxI135NumberDensityM3 = channel.MaxI135NumberDensityM3,
                MeanXe135NumberDensityM3 = channel.MeanXe135NumberDensityM3,
                MaxXe135NumberDensityM3 = channel.MaxXe135NumberDensityM3,
                MeanDynamicAbsorptionGroup1PerM = channel.MeanDynamicAbsorptionGroup1PerM,
                MaxDynamicAbsorptionGroup1PerM = channel.MaxDynamicAbsorptionGroup1PerM,
                MeanDynamicAbsorptionGroup2PerM = channel.MeanDynamicAbsorptionGroup2PerM,
                MaxDynamicAbsorptionGroup2PerM = channel.MaxDynamicAbsorptionGroup2PerM
            };
        }

    }
}
