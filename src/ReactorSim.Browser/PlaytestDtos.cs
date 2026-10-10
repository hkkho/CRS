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
    internal sealed class PlaytestResponseDto
    {
        public string Protocol { get; set; } = PlaytestProtocolV2.ProtocolId;

        public uint SchemaVersion { get; set; } = PlaytestProtocolV2.SchemaVersion;

        public string Operation { get; set; } = string.Empty;

        public bool Ok { get; set; }

        public bool Accepted { get; set; }

        public string Mode { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public ulong Sequence { get; set; }

        public string? ResponseKind { get; set; }

        public ulong? BaseSequence { get; set; }

        public bool? RequiresResync { get; set; }

        public JsonElement? Command { get; set; }

        public PlaytestSnapshotDto? Snapshot { get; set; }

        public PlaytestSnapshotPatchDto? SnapshotPatch { get; set; }

        public PlaytestCoreDto? CoreReplacement { get; set; }
        public PlaytestCoreMeasurementsDto? CoreMeasurements { get; set; }

        public string StateDigest { get; set; } = string.Empty;

        public string ReplayDigest { get; set; } = string.Empty;

        public string ReplayDigestAlgorithm { get; set; } = PlaytestProtocolV2.ReplayDigestAlgorithm;

        public List<PlaytestDiagnosticDto> Diagnostics { get; set; } =
            new List<PlaytestDiagnosticDto>();
    }

    internal sealed class PlaytestCoreMeasurementsDto
    {
        public double[] BundleBurnupMwdPerKg { get; set; } = Array.Empty<double>();
        public ulong[] BundleStateVersions { get; set; } = Array.Empty<ulong>();
        public bool[] BundleIsFresh { get; set; } = Array.Empty<bool>();
        public double[] ChannelAverageBurnupMwdPerKg { get; set; } = Array.Empty<double>();
    }

    /// <summary>
    /// Compact authoritative projection carried by an opt-in dispatch
    /// response. The detailed core remains separate so the browser can merge
    /// ordinary commands without transferring the 380-channel array.
    /// </summary>
    internal sealed class PlaytestSnapshotPatchDto
    {
        public string ScenarioId { get; set; } = string.Empty;

        public string DataPackId { get; set; } = string.Empty;

        public double SimulationTimeSeconds { get; set; }

        public double WallElapsedSeconds { get; set; }

        public double NormalizedPowerFraction { get; set; }

        public double TargetPowerFraction { get; set; }

        public double AxialTiltFraction { get; set; }


        public double RrsReserveFraction { get; set; }

        public double DeviceAvailableFraction { get; set; }

        public uint PendingActionCount { get; set; }

        public RunProvenance? Provenance { get; set; }
        public ShiftProgress Shift { get; set; } = null!;

        public string ScorePolicyId { get; set; } = PracticeScoring.PolicyId;
        public ChannelRippleSnapshot Ripple { get; set; } = null!;

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public RefuellingScoreBreakdown? LastRefuellingScore { get; set; }
        public RefuellingMovement? LastFuelMovement { get; set; }
        public IReadOnlyList<GameRefuellingPlanV1> RefuellingPlans { get; set; } = Array.Empty<GameRefuellingPlanV1>();

        public double ScoreTotal { get; set; }

        public double ScoreDelta { get; set; }

        public bool IsPaused { get; set; }

        public string? PacingMode { get; set; }
        public uint? CompletedDays { get; set; }
        public DailyTurnResult? LastDayResult { get; set; }

        public string RunStatus { get; set; } = "running";

        public string RunEndReason { get; set; } = string.Empty;

        public string PlaybackModeId { get; set; } = "1x";

        public uint FreshBundlesAvailable { get; set; }

        public uint RefuellingOperationCount { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public double? LastDischargedMaximumBurnupMwdPerKg { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public double? MaximumDischargedBurnupMwdPerKg { get; set; }

        public int LastRefuelledChannel { get; set; } = -1;

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public string? LastRefuellingDirectionId { get; set; }

        public ushort LastRefuellingShiftCount { get; set; }

        public PlaytestPhysicsDto Physics { get; set; } = new PlaytestPhysicsDto();

        public PlaytestXenonDto Xenon { get; set; } = new PlaytestXenonDto();

        public PlaytestRrsDto Rrs { get; set; } = new PlaytestRrsDto();

        public PlaytestDiagnosticsDto Diagnostics { get; set; } = new PlaytestDiagnosticsDto();

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public PlaytestEventDto? LastEvent { get; set; }
    }

    internal sealed class PlaytestDiagnosticDto
    {
        public string Level { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
    }

    internal sealed class PlaytestSnapshotDto
    {
        public string Protocol { get; set; } = PlaytestProtocolV2.ProtocolId;

        public string Source { get; set; } = "wasm";

        public ulong Sequence { get; set; }

        public string ScenarioId { get; set; } = string.Empty;

        public string DataPackId { get; set; } = string.Empty;

        public double SimulationTimeSeconds { get; set; }

        public double WallElapsedSeconds { get; set; }

        public double NormalizedPowerFraction { get; set; }

        public double TargetPowerFraction { get; set; }

        public double AxialTiltFraction { get; set; }


        public double RrsReserveFraction { get; set; }

        public double DeviceAvailableFraction { get; set; }

        public uint PendingActionCount { get; set; }

        public RunProvenance? Provenance { get; set; }
        public ShiftProgress Shift { get; set; } = null!;

        public string ScorePolicyId { get; set; } = PracticeScoring.PolicyId;
        public ChannelRippleSnapshot Ripple { get; set; } = null!;

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public RefuellingScoreBreakdown? LastRefuellingScore { get; set; }
        public RefuellingMovement? LastFuelMovement { get; set; }
        public IReadOnlyList<GameRefuellingPlanV1> RefuellingPlans { get; set; } = Array.Empty<GameRefuellingPlanV1>();

        public double ScoreTotal { get; set; }

        public double ScoreDelta { get; set; }

        public bool IsPaused { get; set; }

        public string? PacingMode { get; set; }
        public uint? CompletedDays { get; set; }
        public DailyTurnResult? LastDayResult { get; set; }

        public string RunStatus { get; set; } = "running";

        public string RunEndReason { get; set; } = string.Empty;

        public string PlaybackModeId { get; set; } = "1x";

        public uint FreshBundlesAvailable { get; set; }

        public uint RefuellingOperationCount { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public double? LastDischargedMaximumBurnupMwdPerKg { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public double? MaximumDischargedBurnupMwdPerKg { get; set; }

        public int LastRefuelledChannel { get; set; } = -1;

        public string? LastRefuellingDirectionId { get; set; }

        public ushort LastRefuellingShiftCount { get; set; }

        public PlaytestCoreDto Core { get; set; } = new PlaytestCoreDto();

        public PlaytestXenonDto Xenon { get; set; } = new PlaytestXenonDto();

        public PlaytestRrsDto Rrs { get; set; } = new PlaytestRrsDto();

        public PlaytestPhysicsDto Physics { get; set; } = new PlaytestPhysicsDto();

        public PlaytestDiagnosticsDto Diagnostics { get; set; } = new PlaytestDiagnosticsDto();

        public PlaytestEventDto? LastEvent { get; set; }

    }

    internal sealed class PlaytestCoreDto
    {
        public List<PlaytestAdjusterDto> Adjusters { get; set; } = new List<PlaytestAdjusterDto>();
        public List<PlaytestLiquidZoneTubeDto> LiquidZoneTubes { get; set; } = new List<PlaytestLiquidZoneTubeDto>();
        public uint ChannelCount { get; set; }

        public uint BundlePositionCount { get; set; }

        public int GridWidth { get; set; }

        public int GridHeight { get; set; }

        public List<PlaytestChannelDto> Channels { get; set; } = new List<PlaytestChannelDto>();
    }

    internal sealed class PlaytestAdjusterDto
    {
        public int Id { get; set; }
        public double GridColumn { get; set; }
        public double GridRowStart { get; set; }
        public double GridRowEnd { get; set; }
        public double AxialPosition { get; set; }
        public IReadOnlyList<uint> AffectedChannels { get; set; } = Array.Empty<uint>();
        public IReadOnlyList<uint> BundlePositions { get; set; } = Array.Empty<uint>();
    }

    internal sealed class PlaytestLiquidZoneTubeDto
    {
        public uint ZoneId { get; set; }
        public double GridColumn { get; set; }
        public double GridRowStart { get; set; }
        public double GridRowEnd { get; set; }
        public double AxialPosition { get; set; }
        public IReadOnlyList<uint> AffectedChannels { get; set; } = Array.Empty<uint>();
        public IReadOnlyList<uint> BundlePositions { get; set; } = Array.Empty<uint>();
    }

    internal sealed class PlaytestChannelDto
    {
        public uint ChannelIndex { get; set; }

        public int GridColumn { get; set; }

        public int GridRow { get; set; }

        public string FlowDirection { get; set; } = string.Empty;

        public bool CanRefuel { get; set; }
        public string RefuellingIneligibilityReason { get; set; } = string.Empty;

        public double AverageBurnupMwdPerKg { get; set; }

        public double PowerWatts { get; set; }

        public double LocalPowerFraction { get; set; }

        public double LocalTiltFraction { get; set; }

        public PlaytestXenonChannelDto Xenon { get; set; } = new PlaytestXenonChannelDto();

        public List<PlaytestBundleDto> Bundles { get; set; } = new List<PlaytestBundleDto>();
    }

    internal sealed class PlaytestBundleDto
    {
        public uint LogicalZoneId { get; set; }
        public uint AbsorberZoneId { get; set; }
        public double Group1AbsorptionPerMPerFillFraction { get; set; }
        public double Group2AbsorptionPerMPerFillFraction { get; set; }
        public uint Position { get; set; }

        public string BundleId { get; set; } = string.Empty;

        public string FuelTypeId { get; set; } = string.Empty;

        public double CurrentBurnupMwdPerKg { get; set; }

        public double PowerWatts { get; set; }

        public double LocalPowerFraction { get; set; }

        public double InsertedAtSeconds { get; set; }

        public ulong StateVersion { get; set; }

        public bool IsFresh { get; set; }

        public bool HasFuel { get; set; }

        public List<string> ReflectiveFaces { get; set; } = new List<string>();

        public double Group1Flux { get; set; }

        public double Group2Flux { get; set; }
    }

    internal sealed class PlaytestXenonDto
    {
        public IReadOnlyList<double> NodeI135NumberDensityM3 { get; set; } = Array.Empty<double>();
        public IReadOnlyList<double> NodeXe135NumberDensityM3 { get; set; } = Array.Empty<double>();
        public double CoupledSimulationTimeSeconds { get; set; }
        public string CoupledStateDigestHex { get; set; } = string.Empty;
        public string StateIdentity { get; set; } = string.Empty;

        public string StateDigestHex { get; set; } = string.Empty;

        public ulong StateVersion { get; set; }

        public double SimulationTimeSeconds { get; set; }

        public int NodeCount { get; set; }

        public string CouplingIdentity { get; set; } = string.Empty;

        public bool HasCoupling { get; set; }

        public string BaseCoefficientDigestHex { get; set; } = string.Empty;

        public string DynamicXenonDigestHex { get; set; } = string.Empty;

        public string EffectiveCoefficientDigestHex { get; set; } = string.Empty;

        public double MeanI135NumberDensityM3 { get; set; }

        public double MaxI135NumberDensityM3 { get; set; }

        public double MeanXe135NumberDensityM3 { get; set; }

        public double MaxXe135NumberDensityM3 { get; set; }

        public double MeanDynamicAbsorptionGroup1PerM { get; set; }

        public double MaxDynamicAbsorptionGroup1PerM { get; set; }

        public double MeanDynamicAbsorptionGroup2PerM { get; set; }

        public double MaxDynamicAbsorptionGroup2PerM { get; set; }

        public int SelectedChannelIndex { get; set; } = -1;

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public PlaytestXenonChannelDto? SelectedChannel { get; set; }
    }

    internal sealed class PlaytestXenonChannelDto
    {
        public uint ChannelIndex { get; set; }

        public double MeanI135NumberDensityM3 { get; set; }

        public double MaxI135NumberDensityM3 { get; set; }

        public double MeanXe135NumberDensityM3 { get; set; }

        public double MaxXe135NumberDensityM3 { get; set; }

        public double MeanDynamicAbsorptionGroup1PerM { get; set; }

        public double MaxDynamicAbsorptionGroup1PerM { get; set; }

        public double MeanDynamicAbsorptionGroup2PerM { get; set; }

        public double MaxDynamicAbsorptionGroup2PerM { get; set; }
    }

    internal sealed class PlaytestRrsDto
    {
        public string DecisionCode { get; set; } = string.Empty;
        public string DecisionExplanation { get; set; } = string.Empty;
        public int LimitingZoneId { get; set; }
        public double AbsorptionReferenceFillFraction { get; set; }
        public double CalibratedTotalZoneWorthMk { get; set; }
        public string ControllerIdentity { get; set; } = string.Empty;

        public string MappingIdentity { get; set; } = string.Empty;

        public string MappingDigestHex { get; set; } = string.Empty;

        public string OverlayIdentity { get; set; } = string.Empty;

        public string OverlayDigestHex { get; set; } = string.Empty;

        public string StateDigestHex { get; set; } = string.Empty;

        public double SimulationTimeSeconds { get; set; }

        public int NodeCount { get; set; }

        public double AverageFillFraction { get; set; }

        public double MinimumFillFraction { get; set; }

        public double MaximumFillFraction { get; set; }

        public double MeasuredPowerWatts { get; set; }

        public double TargetPowerWatts { get; set; }

        public double PowerErrorWatts { get; set; }

        public double CoreReactivity { get; set; }

        public double CompensatedNetReactivity { get; set; }

        public double CommonModeRhoCorrection { get; set; }

        public int ControllerIterationCount { get; set; }

        public bool ControllerConverged { get; set; }

        public string ResponseModelIdentity { get; set; } = string.Empty;

        public string ResponseModelDigestHex { get; set; } = string.Empty;

        public List<double> AppliedFillCommand { get; set; } = new List<double>();

        public double ControlledBaselineWeightedResidual { get; set; }

        public double CombinedWeightedResidual { get; set; }

        public int CandidateSolveCount { get; set; }

        public int VerificationSolveCount { get; set; }

        public int CorrectionSolveCount { get; set; }

        public bool CorrectionApplied { get; set; }

        public bool LowExhaustion { get; set; }

        public bool HighExhaustion { get; set; }

        public bool IsGameOver { get; set; }

        public string GameOverReason { get; set; } = string.Empty;

        public string CadenceIdentity { get; set; } = string.Empty;

        public List<PlaytestRrsZoneDto> Zones { get; set; } = new List<PlaytestRrsZoneDto>();
    }

    internal sealed class PlaytestRrsZoneDto
    {
        public double MeanI135NumberDensityM3 { get; set; }
        public double MeanXe135NumberDensityM3 { get; set; }
        public uint LogicalZoneId { get; set; }

        public double FillFraction { get; set; }

        public double ReferencePowerFraction { get; set; }

        public double TargetPowerFraction { get; set; }

        public double MeasuredPowerFraction { get; set; }

        public double ShapeError { get; set; }
    }

    internal sealed class PlaytestDiagnosticsDto
    {
        public PlaytestConvergenceDto Convergence { get; set; } = new PlaytestConvergenceDto();

        public List<PlaytestCheckDto> Checks { get; set; } = new List<PlaytestCheckDto>();
    }

    internal sealed class PlaytestConvergenceDto
    {
        public string State { get; set; } = "unavailable";

        public int Iterations { get; set; }

        public double Residual { get; set; }

        public double RelativePowerError { get; set; }

        public double LastSolveMilliseconds { get; set; }

        public string SolverLabel { get; set; } = string.Empty;
    }

    internal sealed class PlaytestPhysicsDto
    {
        public string SourceId { get; set; } = string.Empty;

        public string FormulationId { get; set; } = string.Empty;

        public string ShapeMethodId { get; set; } = string.Empty;

        public string AmplitudeMethodId { get; set; } = string.Empty;

        public string ReactivityMethodId { get; set; } = string.Empty;

        public string SolveState { get; set; } = string.Empty;

        public bool IsAuthoritative { get; set; }

        public ulong BindingVersion { get; set; }

        public double ReferencePowerWatts { get; set; }

        public double PowerAmplitude { get; set; }

        public double ActualPowerFraction { get; set; }

        public double TargetPowerWatts { get; set; }

        public double TotalPowerWatts { get; set; }

        public double ElectricalPowerWatts { get; set; }

        public double MeanChannelPowerWatts { get; set; }

        public double MeanBundlePowerWatts { get; set; }

        public double EffectiveK { get; set; }

        public double Reactivity { get; set; }

        public double WeightedPerturbationReactivity { get; set; }

        public double ReactivityNumerator { get; set; }

        public double ReactivityDenominator { get; set; }

        public string ReactivityIdentity { get; set; } = string.Empty;

        public string ReactivityBindingDigestHex { get; set; } = string.Empty;

        public double CoreReactivity { get; set; }

        public double CompensatedNetReactivity { get; set; }

        public double CompensationState { get; set; }

        public double CompensationCommand { get; set; }

        public double CompensationLowerBound { get; set; }

        public double CompensationUpperBound { get; set; }

        public bool CompensationSaturated { get; set; }

        public double CompensationResponseTimeSeconds { get; set; }

        public string CadenceIdentity { get; set; } = string.Empty;

        public string AdjointNormalizationIdentity { get; set; } = string.Empty;

        public string AdjointDigestHex { get; set; } = string.Empty;

        public int AdjointIterationCount { get; set; }

        public double AdjointTransposeResidualRelativeInfinity { get; set; }

        public double PowerBalanceRelativeError { get; set; }

        public string SolverIdentity { get; set; } = string.Empty;

        public int SolverIterationCount { get; set; }

        public double SolverResidualRelativeInfinity { get; set; }
    }

    internal sealed class PlaytestCheckDto
    {
        public string Label { get; set; } = string.Empty;

        public string Value { get; set; } = string.Empty;

        public string Status { get; set; } = "info";
    }

    internal sealed class PlaytestEventDto
    {
        public string EventId { get; set; } = string.Empty;

        public double TimeSeconds { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Detail { get; set; } = string.Empty;

        public string Tone { get; set; } = "info";
    }

}
