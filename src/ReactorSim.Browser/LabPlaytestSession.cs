using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using ReactorSim.Core;

namespace ReactorSim.Browser
{
    internal sealed class LabSolverOptions
    {
        public const int DefaultMaximumIterations = 512;
        public const int DefaultInnerMaximumIterations = 512;
        public const double DefaultTargetPowerW = 0.4;
        public const double DefaultInitialEigenvalue = 1.0;
        public const double DefaultKAbsoluteTolerance = 0.01;
        public const double DefaultKRelativeTolerance = 0.01;
        public const double DefaultResidualTolerance = 1e-12;
        public const double DefaultSourceShapeTolerance = 1e-12;
        public const double DefaultPowerBalanceTolerance = 1e-12;
        public const double DefaultEdgeConductanceM2 = 0.5;

        public int MaximumIterations { get; init; } = DefaultMaximumIterations;

        public int InnerMaximumIterations { get; init; } = DefaultInnerMaximumIterations;

        public double TargetPowerW { get; init; } = DefaultTargetPowerW;

        public double InitialEigenvalue { get; init; } = DefaultInitialEigenvalue;

        public double KAbsoluteTolerance { get; init; } = DefaultKAbsoluteTolerance;

        public double KRelativeTolerance { get; init; } = DefaultKRelativeTolerance;

        public double ResidualTolerance { get; init; } = DefaultResidualTolerance;

        public double SourceShapeTolerance { get; init; } = DefaultSourceShapeTolerance;

        public double PowerBalanceTolerance { get; init; } = DefaultPowerBalanceTolerance;

        public double EdgeConductanceM2 { get; init; } = DefaultEdgeConductanceM2;

        public string ToCanonicalString()
        {
            return string.Join(
                ";",
                MaximumIterations.ToString(CultureInfo.InvariantCulture),
                InnerMaximumIterations.ToString(CultureInfo.InvariantCulture),
                TargetPowerW.ToString("R", CultureInfo.InvariantCulture),
                InitialEigenvalue.ToString("R", CultureInfo.InvariantCulture),
                KAbsoluteTolerance.ToString("R", CultureInfo.InvariantCulture),
                KRelativeTolerance.ToString("R", CultureInfo.InvariantCulture),
                ResidualTolerance.ToString("R", CultureInfo.InvariantCulture),
                SourceShapeTolerance.ToString("R", CultureInfo.InvariantCulture),
                PowerBalanceTolerance.ToString("R", CultureInfo.InvariantCulture),
                EdgeConductanceM2.ToString("R", CultureInfo.InvariantCulture));
        }
    }

    public sealed class LabSpatialSolveSnapshotDto
    {
        public string Status { get; set; } = string.Empty;

        public bool IsConverged { get; set; }

        public bool HasUsableState { get; set; }

        public LabSpatialStateSnapshotDto? FinalState { get; set; }

        public LabSpatialDiagnosticsSnapshotDto Diagnostics { get; set; } =
            new LabSpatialDiagnosticsSnapshotDto();
    }

    public sealed class LabSpatialStateSnapshotDto
    {
        public int Iteration { get; set; }

        public double Eigenvalue { get; set; }

        public double NormalizationScale { get; set; }

        public double TotalPowerW { get; set; }

        public double FissionProductionRate { get; set; }

        public List<double> Group1Flux { get; set; } = new List<double>();

        public List<double> Group2Flux { get; set; } = new List<double>();
    }

    public sealed class LabSpatialDiagnosticsSnapshotDto
    {
        public int IterationCount { get; set; }

        public double? EigenvalueChangeAbsolute { get; set; }

        public double? EigenvalueChangeRelative { get; set; }

        public double? ResidualAbsoluteInfinity { get; set; }

        public double? ResidualRelativeInfinity { get; set; }

        public double? SourceShapeChangeInfinity { get; set; }

        public double? PowerBalanceRelative { get; set; }

        public string InnerSolveStatus { get; set; } = string.Empty;

        public string ConvergenceReason { get; set; } = string.Empty;

        public List<BridgeDiagnosticDto> FailureDiagnostics { get; set; } =
            new List<BridgeDiagnosticDto>();

        public int InvalidCoefficientCount { get; set; }

        public int NegativeFluxCount { get; set; }

        public int NonFiniteValueCount { get; set; }

        public int FailedInnerSolveCount { get; set; }

        public int RejectedUpscatterCount { get; set; }

        public int ClampCount { get; set; }

        public int ForbiddenClampCount { get; set; }
    }

    public sealed class LabBundleSnapshotDto
    {
        public uint Position { get; set; }

        public string BundleId { get; set; } = string.Empty;

        public string FuelTypeId { get; set; } = string.Empty;

        public double CurrentBurnupMwDayPerKg { get; set; }

        public double CurrentBurnupJPerKgHm { get; set; }

        public double InsertedAtSeconds { get; set; }

        public ulong StateVersion { get; set; }
    }

    public sealed class LabChannelSnapshotDto
    {
        public uint ChannelIndex { get; set; }

        public int CoordinateX { get; set; }

        public int CoordinateY { get; set; }

        public string FlowDirection { get; set; } = string.Empty;

        public List<LabBundleSnapshotDto> Bundles { get; set; } = new List<LabBundleSnapshotDto>();
    }

    public sealed class LabCoreSnapshotDto
    {
        public string FixtureId { get; set; } = string.Empty;

        public uint ChannelCount { get; set; }

        public uint BundlePositionCount { get; set; }

        public List<LabChannelSnapshotDto> Channels { get; set; } =
            new List<LabChannelSnapshotDto>();
    }

    public sealed class LabSnapshotDto
    {
        public string FixtureId { get; set; } = string.Empty;

        public double SimulationTimeSeconds { get; set; }

        public uint FreshBundlesAvailable { get; set; }

        public uint RefuellingOperationCount { get; set; }

        public int LastRefuelledChannel { get; set; } = -1;

        public string? LastRefuellingDirectionId { get; set; }

        public ushort LastRefuellingShiftCount { get; set; }

        public LabCoreSnapshotDto Core { get; set; } = new LabCoreSnapshotDto();

        public LabSpatialSolveSnapshotDto SpatialSolve { get; set; } =
            new LabSpatialSolveSnapshotDto();
    }

    internal sealed class LabSolveAttempt
    {
        private LabSolveAttempt(
            SpatialSolveResult? result,
            BridgeDiagnosticDto? failure)
        {
            Result = result;
            Failure = failure;
        }

        public SpatialSolveResult? Result { get; }

        public BridgeDiagnosticDto? Failure { get; }

        public bool IsSuccessful
        {
            get { return Result != null && Result.HasUsableState && Failure == null; }
        }

        public static LabSolveAttempt Success(SpatialSolveResult result)
        {
            return new LabSolveAttempt(result, null);
        }

        public static LabSolveAttempt Failed(BridgeDiagnosticDto failure)
        {
            return new LabSolveAttempt(null, failure);
        }
    }

    internal sealed class LabPlaytestSession
    {
        public const string FixtureId = "lab-2x8-synthetic-v1";
        public const string OldFuelTypeId = "LAB-FUEL-SYNTHETIC";
        public const string FreshFuelTypeId = "LAB-FRESH-SYNTHETIC";
        public const ushort RefuelShiftCount = 4;
        public const uint InitialFreshBundles = 32;
        private const uint BundlePositionCount = 8;
        private const uint ChannelCount = 2;
        private const double JoulesPerMegaWattDayPerKilogram = 8.64e10;

        private readonly CoreTopology _topology;
        private readonly SpatialStencil _stencil;
        private BundleInventory _inventory;
        private SpatialSolveResult _spatialSolve;
        private LabSolverOptions _options;
        private uint _freshBundlesAvailable;
        private uint _refuellingOperationCount;
        private int _lastRefuelledChannel = -1;
        private string? _lastRefuellingDirectionId;
        private ushort _lastRefuellingShiftCount;
        private ulong _nextBundleSequence = 10_000;

        private LabPlaytestSession(
            CoreTopology topology,
            SpatialStencil stencil,
            BundleInventory inventory,
            SpatialSolveResult spatialSolve,
            LabSolverOptions options)
        {
            _topology = topology;
            _stencil = stencil;
            _inventory = inventory;
            _spatialSolve = spatialSolve;
            _options = options;
            _freshBundlesAvailable = InitialFreshBundles;
        }

        public static string ModeId
        {
            get { return "lab"; }
        }

        public string ComputeStateDigest()
        {
            StringBuilder canonical = new StringBuilder();
            canonical.Append(FixtureId);
            canonical.Append('|');
            canonical.Append(_options.ToCanonicalString());
            canonical.Append('|');
            canonical.Append(_freshBundlesAvailable.ToString(CultureInfo.InvariantCulture));
            canonical.Append('|');
            canonical.Append(_refuellingOperationCount.ToString(CultureInfo.InvariantCulture));
            canonical.Append('|');
            canonical.Append(_lastRefuelledChannel.ToString(CultureInfo.InvariantCulture));
            canonical.Append('|');
            canonical.Append(_lastRefuellingDirectionId ?? string.Empty);
            canonical.Append('|');
            canonical.Append(_lastRefuellingShiftCount.ToString(CultureInfo.InvariantCulture));
            AppendInventoryCanonical(canonical, _inventory);
            AppendSolveCanonical(canonical, _spatialSolve);
            return PlaytestProtocolV1.ComputeDigest(canonical.ToString());
        }

        public LabSnapshotDto CreateSnapshot()
        {
            return new LabSnapshotDto
            {
                FixtureId = FixtureId,
                SimulationTimeSeconds = 0.0,
                FreshBundlesAvailable = _freshBundlesAvailable,
                RefuellingOperationCount = _refuellingOperationCount,
                LastRefuelledChannel = _lastRefuelledChannel,
                LastRefuellingDirectionId = _lastRefuellingDirectionId,
                LastRefuellingShiftCount = _lastRefuellingShiftCount,
                Core = CreateCoreSnapshot(),
                SpatialSolve = CreateSpatialSolveSnapshot(_spatialSolve)
            };
        }

        public BridgeCommandExecution Dispatch(string type, JsonElement command)
        {
            switch (type)
            {
                case "refuel":
                    return TryRefuel(command);
                case "solve":
                    return TrySolve(command);
                default:
                    return BridgeCommandExecution.Failure(
                        PlaytestProtocolV1.Diagnostic(
                            "Lab.Command.Unsupported",
                            "type",
                            "Lab supports refuel and solve commands."));
            }
        }

        public static bool TryCreate(
            LabSolverOptions options,
            out LabPlaytestSession? session,
            out BridgeDiagnosticDto? failure)
        {
            CoreTopology topology = CreateTopology();
            ContractValidationResult<SpatialStencil> stencilResult = SpatialStencil.TryCreate(topology);
            if (!stencilResult.IsValid)
            {
                session = null;
                failure = PlaytestProtocolV1.Diagnostic(stencilResult.FirstDiagnostic);
                return false;
            }

            SpatialStencil stencil = stencilResult.Value;
            ContractValidationResult<BundleInventory> inventoryResult =
                BundleInventory.TryCreate(topology, CreateInitialBundles(topology));
            if (!inventoryResult.IsValid)
            {
                session = null;
                failure = PlaytestProtocolV1.Diagnostic(inventoryResult.FirstDiagnostic);
                return false;
            }

            LabSolveAttempt solve = TrySolve(stencil, inventoryResult.Value, options);
            if (!solve.IsSuccessful)
            {
                session = null;
                failure = solve.Failure ?? PlaytestProtocolV1.Diagnostic(
                    "Lab.Solve.Failed",
                    "spatialSolve",
                    "The Lab fixture did not produce a usable converged spatial state.");
                return false;
            }

            session = new LabPlaytestSession(
                topology,
                stencil,
                inventoryResult.Value,
                solve.Result!,
                options);
            failure = null;
            return true;
        }

        private static LabSolveAttempt TrySolve(
            SpatialStencil stencil,
            BundleInventory inventory,
            LabSolverOptions options)
        {
            SpatialCoefficientSet? coefficients;
            ContractValidationResult<SpatialCoefficientSet> coefficientsResult =
                CreateCoefficients(stencil, inventory, options, out coefficients);
            if (!coefficientsResult.IsValid)
            {
                return LabSolveAttempt.Failed(
                    PlaytestProtocolV1.Diagnostic(coefficientsResult.FirstDiagnostic));
            }

            ContractValidationResult<SpatialLinearSolvePolicy> linearPolicy =
                SpatialLinearSolvePolicy.TryCreate(
                    SpatialLinearSolvePolicy.DeterministicJacobiMethodId,
                    SpatialLinearSolvePolicy.DeterministicJacobiMethodVersion,
                    1e-14,
                    1e-14,
                    options.InnerMaximumIterations);
            if (!linearPolicy.IsValid)
            {
                return LabSolveAttempt.Failed(
                    PlaytestProtocolV1.Diagnostic(linearPolicy.FirstDiagnostic));
            }

            double[] initialGroup1 = Enumerable.Repeat(1.0, stencil.NodeCount).ToArray();
            double[] initialGroup2 = Enumerable.Repeat(1.0, stencil.NodeCount).ToArray();
            ContractValidationResult<SpatialEigenIteration> iteration =
                SpatialEigenIteration.TryCreate(
                    stencil,
                    coefficients!,
                    linearPolicy.Value,
                    options.TargetPowerW,
                    options.InitialEigenvalue,
                    initialGroup1,
                    initialGroup2);
            if (!iteration.IsValid)
            {
                return LabSolveAttempt.Failed(
                    PlaytestProtocolV1.Diagnostic(iteration.FirstDiagnostic));
            }

            ContractValidationResult<SpatialConvergencePolicy> convergencePolicy =
                SpatialConvergencePolicy.TryCreate(
                    options.KAbsoluteTolerance,
                    options.KRelativeTolerance,
                    options.ResidualTolerance,
                    options.SourceShapeTolerance,
                    options.PowerBalanceTolerance,
                    options.MaximumIterations);
            if (!convergencePolicy.IsValid)
            {
                return LabSolveAttempt.Failed(
                    PlaytestProtocolV1.Diagnostic(convergencePolicy.FirstDiagnostic));
            }

            ContractValidationResult<SpatialEigenSolve> solve = SpatialEigenSolve.TryCreate(
                iteration.Value,
                convergencePolicy.Value);
            if (!solve.IsValid)
            {
                return LabSolveAttempt.Failed(
                    PlaytestProtocolV1.Diagnostic(solve.FirstDiagnostic));
            }

            ContractValidationResult<SpatialSolveResult> result = solve.Value.TrySolve();
            if (!result.IsValid)
            {
                return LabSolveAttempt.Failed(
                    PlaytestProtocolV1.Diagnostic(result.FirstDiagnostic));
            }

            SpatialSolveResult spatialResult = result.Value;
            if (!spatialResult.HasUsableState)
            {
                ContractDiagnostic? diagnostic = spatialResult.Diagnostics.FailureDiagnostic;
                return LabSolveAttempt.Failed(
                    diagnostic == null
                        ? PlaytestProtocolV1.Diagnostic(
                            "SpatialEigenSolve.Nonconverged",
                            "spatialSolve",
                            "The Lab spatial solve did not converge and has no usable state.")
                        : PlaytestProtocolV1.Diagnostic(diagnostic));
            }

            if (!AreFinite(spatialResult.FinalState!))
            {
                return LabSolveAttempt.Failed(
                    PlaytestProtocolV1.Diagnostic(
                        "Lab.Solve.NonFinite",
                        "spatialSolve.finalState",
                        "The Lab spatial solve produced a non-finite value and was rejected."));
            }

            return LabSolveAttempt.Success(spatialResult);
        }

        private BridgeCommandExecution TrySolve(JsonElement command)
        {
            LabSolverOptions requestedOptions = _options;
            BridgeDiagnosticDto? optionsFailure = TryReadSolverOptions(
                command,
                _options,
                "solver",
                out requestedOptions);
            if (optionsFailure != null)
            {
                return BridgeCommandExecution.Failure(optionsFailure);
            }

            LabSolveAttempt solve = TrySolve(_stencil, _inventory, requestedOptions);
            if (!solve.IsSuccessful)
            {
                return BridgeCommandExecution.Failure(
                    solve.Failure ?? PlaytestProtocolV1.Diagnostic(
                        "Lab.Solve.Failed",
                        "spatialSolve",
                        "The Lab spatial solve was rejected without changing Lab state."));
            }

            _options = requestedOptions;
            _spatialSolve = solve.Result!;
            return BridgeCommandExecution.Success(
                "Lab spatial solve converged.",
                CreateSpatialSolveSnapshot(_spatialSolve));
        }

        private BridgeCommandExecution TryRefuel(JsonElement command)
        {
            BridgeDiagnosticDto? diagnostic = TryReadRefuelArguments(
                command,
                out uint channelIndex,
                out string directionId,
                out ushort shiftCount,
                out string fuelTypeId);
            if (diagnostic != null)
            {
                return BridgeCommandExecution.Failure(diagnostic);
            }

            if (channelIndex >= ChannelCount)
            {
                return BridgeCommandExecution.Failure(
                    PlaytestProtocolV1.Diagnostic(
                        "Lab.Refuel.Channel.OutOfRange",
                        "channelIndex",
                        "The Lab fixture contains only channels 0 and 1."));
            }

            if (shiftCount != RefuelShiftCount)
            {
                return BridgeCommandExecution.Failure(
                    PlaytestProtocolV1.Diagnostic(
                        "Lab.Refuel.ShiftCount.Unsupported",
                        "shiftCount",
                        "The Lab fixture exposes the existing four-bundle scheme only."));
            }

            if (!string.Equals(fuelTypeId, FreshFuelTypeId, StringComparison.Ordinal))
            {
                return BridgeCommandExecution.Failure(
                    PlaytestProtocolV1.Diagnostic(
                        "Lab.Refuel.FuelType.Unsupported",
                        "fuelTypeId",
                        "Use LAB-FRESH-SYNTHETIC for the explicit Lab fixture."));
            }

            if (_freshBundlesAvailable < shiftCount)
            {
                return BridgeCommandExecution.Failure(
                    PlaytestProtocolV1.Diagnostic(
                        "Lab.Refuel.FreshInventory.Insufficient",
                        "freshBundlesAvailable",
                        "The Lab fixture does not have enough fresh bundles."));
            }

            ChannelTopology channel = _topology.GetChannel(new ChannelId(channelIndex));
            string expectedDirection = channel.FlowDirection == FlowDirection.EndAtoEndB
                ? "toward-end-b"
                : "toward-end-a";
            if (!string.Equals(directionId, expectedDirection, StringComparison.Ordinal))
            {
                return BridgeCommandExecution.Failure(
                    PlaytestProtocolV1.Diagnostic(
                        "Lab.Refuel.Direction.Mismatch",
                        "directionId",
                        "The refuelling direction must follow the channel flow direction."));
            }

            ContractValidationResult<RefuelSchemeDefinition> scheme =
                RefuelSchemeDefinition.TryCreate(
                    "lab-s4-v1",
                    RefuelShiftCount,
                    FreshFuelTypeId,
                    RefuelSchemeDefinition.CurrentSchemaVersion,
                    Enumerable.Range(4, 4).Select(value => new BundlePosition((uint)value)),
                    Enumerable.Range(0, 4).Select(value => new BundlePosition((uint)value)));
            if (!scheme.IsValid)
            {
                return BridgeCommandExecution.Failure(
                    PlaytestProtocolV1.Diagnostic(scheme.FirstDiagnostic));
            }

            ContractValidationResult<RefuelSchemePositionPlan> plan =
                scheme.Value.TryCreatePositionPlan(BundlePositionCount, channel.FlowDirection);
            if (!plan.IsValid)
            {
                return BridgeCommandExecution.Failure(
                    PlaytestProtocolV1.Diagnostic(plan.FirstDiagnostic));
            }

            BundleState[] inserted = CreateInsertedBundles(plan.Value, channelIndex);
            ContractValidationResult<RefuelShiftResult> transition = RefuelShiftTransition.TryApply(
                _inventory,
                new ChannelId(channelIndex),
                plan.Value,
                inserted,
                0.0,
                0.0);
            if (!transition.IsValid)
            {
                return BridgeCommandExecution.Failure(
                    PlaytestProtocolV1.Diagnostic(transition.FirstDiagnostic));
            }

            LabSolveAttempt solve = TrySolve(_stencil, transition.Value.ResultingInventory, _options);
            if (!solve.IsSuccessful)
            {
                return BridgeCommandExecution.Failure(
                    solve.Failure ?? PlaytestProtocolV1.Diagnostic(
                        "Lab.Refuel.Solve.Rejected",
                        "spatialSolve",
                        "The refuelling candidate did not produce a usable converged solve; no state changed."));
            }

            _inventory = transition.Value.ResultingInventory;
            _spatialSolve = solve.Result!;
            _freshBundlesAvailable -= shiftCount;
            _refuellingOperationCount = checked(_refuellingOperationCount + 1);
            _nextBundleSequence = checked(_nextBundleSequence + (ulong)inserted.Length);
            _lastRefuelledChannel = checked((int)channelIndex);
            _lastRefuellingDirectionId = directionId;
            _lastRefuellingShiftCount = shiftCount;
            return BridgeCommandExecution.Success(
                "Lab refuelling committed through RefuelShiftTransition; the coupled spatial solve converged.",
                CreateSpatialSolveSnapshot(_spatialSolve));
        }

        private static BridgeDiagnosticDto? TryReadRefuelArguments(
            JsonElement command,
            out uint channelIndex,
            out string directionId,
            out ushort shiftCount,
            out string fuelTypeId)
        {
            channelIndex = 0;
            directionId = string.Empty;
            shiftCount = 0;
            fuelTypeId = string.Empty;

            if (!PlaytestInput.TryGetProperty(command, out JsonElement channel, "channelIndex", "channel_id"))
            {
                return PlaytestProtocolV1.Diagnostic(
                    "Lab.Refuel.Channel.Missing",
                    "channelIndex",
                    "A Lab refuelling command requires channelIndex.");
            }

            if (!channel.TryGetUInt32(out channelIndex))
            {
                return PlaytestProtocolV1.Diagnostic(
                    "Lab.Refuel.Channel.Invalid",
                    "channelIndex",
                    "channelIndex must be a nonnegative JSON integer.");
            }

            if (!PlaytestInput.TryGetProperty(command, out JsonElement direction, "directionId", "direction"))
            {
                return PlaytestProtocolV1.Diagnostic(
                    "Lab.Refuel.Direction.Missing",
                    "directionId",
                    "A Lab refuelling command requires directionId.");
            }

            if (direction.ValueKind != JsonValueKind.String || direction.GetString() == null)
            {
                return PlaytestProtocolV1.Diagnostic(
                    "Lab.Refuel.Direction.Invalid",
                    "directionId",
                    "directionId must be a string.");
            }

            directionId = PlaytestInput.NormalizeType(direction.GetString()!);
            if (directionId != "toward-end-a" && directionId != "toward-end-b")
            {
                return PlaytestProtocolV1.Diagnostic(
                    "Lab.Refuel.Direction.Invalid",
                    "directionId",
                    "directionId must be toward-end-a or toward-end-b.");
            }

            if (!PlaytestInput.TryGetProperty(command, out JsonElement shift, "shiftCount", "shift_count"))
            {
                return PlaytestProtocolV1.Diagnostic(
                    "Lab.Refuel.ShiftCount.Missing",
                    "shiftCount",
                    "A Lab refuelling command requires shiftCount.");
            }

            if (!shift.TryGetUInt16(out shiftCount))
            {
                return PlaytestProtocolV1.Diagnostic(
                    "Lab.Refuel.ShiftCount.Invalid",
                    "shiftCount",
                    "shiftCount must be a nonnegative JSON integer.");
            }

            if (!PlaytestInput.TryGetProperty(command, out JsonElement fuelType, "fuelTypeId", "fuel_type_id"))
            {
                return PlaytestProtocolV1.Diagnostic(
                    "Lab.Refuel.FuelType.Missing",
                    "fuelTypeId",
                    "A Lab refuelling command requires fuelTypeId.");
            }

            if (fuelType.ValueKind != JsonValueKind.String || fuelType.GetString() == null)
            {
                return PlaytestProtocolV1.Diagnostic(
                    "Lab.Refuel.FuelType.Invalid",
                    "fuelTypeId",
                    "fuelTypeId must be a string.");
            }

            fuelTypeId = fuelType.GetString()!.Trim();
            return null;
        }

        private LabCoreSnapshotDto CreateCoreSnapshot()
        {
            return CreateCoreSnapshot(_inventory);
        }

        private LabCoreSnapshotDto CreateCoreSnapshot(BundleInventory inventory)
        {
            var channels = new List<LabChannelSnapshotDto>(_topology.Channels.Count);
            foreach (ChannelTopology channel in _topology.Channels.OrderBy(value => value.ChannelId.Value))
            {
                var bundles = new List<LabBundleSnapshotDto>((int)BundlePositionCount);
                for (uint position = 0; position < BundlePositionCount; position++)
                {
                    BundleState? bundle = inventory.Get(
                        new NodeKey(channel.ChannelId, new BundlePosition(position)));
                    if (bundle == null)
                    {
                        continue;
                    }

                    double currentBurnup = bundle.CurrentBurnupJPerKgHm;
                    bundles.Add(new LabBundleSnapshotDto
                    {
                        Position = position,
                        BundleId = bundle.BundleId.ToString(),
                        FuelTypeId = bundle.MaterialVariantId.Value,
                        CurrentBurnupMwDayPerKg = currentBurnup / JoulesPerMegaWattDayPerKilogram,
                        CurrentBurnupJPerKgHm = currentBurnup,
                        InsertedAtSeconds = bundle.InsertedAtSeconds,
                        StateVersion = bundle.StateVersion
                    });
                }

                channels.Add(new LabChannelSnapshotDto
                {
                    ChannelIndex = channel.ChannelId.Value,
                    CoordinateX = channel.CoordinateX,
                    CoordinateY = channel.CoordinateY,
                    FlowDirection = channel.FlowDirection.ToString(),
                    Bundles = bundles
                });
            }

            return new LabCoreSnapshotDto
            {
                FixtureId = FixtureId,
                ChannelCount = ChannelCount,
                BundlePositionCount = BundlePositionCount,
                Channels = channels
            };
        }

        private static LabSpatialSolveSnapshotDto CreateSpatialSolveSnapshot(
            SpatialSolveResult solve)
        {
            SpatialSolveDiagnostics diagnostics = solve.Diagnostics;
            var failureDiagnostics = new List<BridgeDiagnosticDto>();
            if (diagnostics.FailureDiagnostic != null)
            {
                failureDiagnostics.Add(PlaytestProtocolV1.Diagnostic(diagnostics.FailureDiagnostic));
            }

            SpatialEigenIterationState? finalState = solve.FinalState;
            LabSpatialStateSnapshotDto? state = finalState == null
                ? null
                : new LabSpatialStateSnapshotDto
                {
                    Iteration = finalState.Iteration,
                    Eigenvalue = finalState.Eigenvalue,
                    NormalizationScale = finalState.NormalizationScale,
                    TotalPowerW = finalState.TotalPowerW,
                    FissionProductionRate = finalState.FissionProductionRate,
                    Group1Flux = finalState.Group1Flux.ToList(),
                    Group2Flux = finalState.Group2Flux.ToList()
                };

            return new LabSpatialSolveSnapshotDto
            {
                Status = PlaytestProtocolV1.EnumId(solve.Status),
                IsConverged = solve.IsConverged,
                HasUsableState = solve.HasUsableState,
                FinalState = state,
                Diagnostics = new LabSpatialDiagnosticsSnapshotDto
                {
                    IterationCount = diagnostics.IterationCount,
                    EigenvalueChangeAbsolute = diagnostics.EigenvalueChangeAbsolute,
                    EigenvalueChangeRelative = diagnostics.EigenvalueChangeRelative,
                    ResidualAbsoluteInfinity = diagnostics.ResidualAbsoluteInfinity,
                    ResidualRelativeInfinity = diagnostics.ResidualRelativeInfinity,
                    SourceShapeChangeInfinity = diagnostics.SourceShapeChangeInfinity,
                    PowerBalanceRelative = diagnostics.PowerBalanceRelative,
                    InnerSolveStatus = PlaytestProtocolV1.EnumId(diagnostics.InnerSolveStatus),
                    ConvergenceReason = diagnostics.ConvergenceReason,
                    FailureDiagnostics = failureDiagnostics,
                    InvalidCoefficientCount = diagnostics.InvalidCoefficientCount,
                    NegativeFluxCount = diagnostics.NegativeFluxCount,
                    NonFiniteValueCount = diagnostics.NonFiniteValueCount,
                    FailedInnerSolveCount = diagnostics.FailedInnerSolveCount,
                    RejectedUpscatterCount = diagnostics.RejectedUpscatterCount,
                    ClampCount = diagnostics.ClampCount,
                    ForbiddenClampCount = diagnostics.ForbiddenClampCount
                }
            };
        }

        private static bool AreFinite(SpatialEigenIterationState state)
        {
            if (!double.IsFinite(state.Eigenvalue) ||
                !double.IsFinite(state.NormalizationScale) ||
                !double.IsFinite(state.TotalPowerW) ||
                !double.IsFinite(state.FissionProductionRate))
            {
                return false;
            }

            return state.Group1Flux.All(double.IsFinite) && state.Group2Flux.All(double.IsFinite);
        }

        private static ContractValidationResult<SpatialCoefficientSet> CreateCoefficients(
            SpatialStencil stencil,
            BundleInventory inventory,
            LabSolverOptions options,
            out SpatialCoefficientSet? coefficients)
        {
            var nodes = new List<SpatialNodeCoefficients>(stencil.NodeCount);
            foreach (SpatialNodeStencil node in stencil.Nodes)
            {
                BundleState? bundle = inventory.Get(node.Node);
                if (bundle == null)
                {
                    coefficients = null;
                    return ContractValidationResult<SpatialCoefficientSet>.Invalid(
                        "Lab.Solve.Inventory.Bundle.Missing",
                        node.Node.ToString(),
                        "Every Lab spatial node must contain one bundle.");
                }

                bool fresh = string.Equals(
                    bundle.MaterialVariantId.Value,
                    FreshFuelTypeId,
                    StringComparison.Ordinal);
                nodes.Add(CreateNodeCoefficients(node.Node, fresh));
            }

            var edges = new List<SpatialEdgeConductance>();
            var seenEdges = new HashSet<string>(StringComparer.Ordinal);
            foreach (SpatialNodeStencil node in stencil.Nodes)
            {
                foreach (SpatialNeighborTerm neighbor in node.NeighborTerms)
                {
                    NodeKey first = node.Node.CompareTo(neighbor.TargetNode) <= 0
                        ? node.Node
                        : neighbor.TargetNode;
                    NodeKey second = node.Node.CompareTo(neighbor.TargetNode) <= 0
                        ? neighbor.TargetNode
                        : node.Node;
                    string key = first + "|" + second;
                    if (seenEdges.Add(key))
                    {
                        edges.Add(new SpatialEdgeConductance(
                            first,
                            second,
                            options.EdgeConductanceM2,
                            options.EdgeConductanceM2));
                    }
                }
            }

            var boundaries = new List<SpatialBoundaryConductance>();
            foreach (SpatialNodeStencil node in stencil.Nodes)
            {
                foreach (SpatialBoundaryTerm boundary in node.BoundaryTerms)
                {
                    boundaries.Add(new SpatialBoundaryConductance(
                        node.Node,
                        boundary.Face,
                        0.0,
                        0.0));
                }
            }

            ContractValidationResult<SpatialCoefficientSet> result = SpatialCoefficientSet.TryCreate(
                stencil,
                nodes,
                edges,
                boundaries);
            coefficients = result.IsValid ? result.Value : null;
            return result;
        }

        private static SpatialNodeCoefficients CreateNodeCoefficients(NodeKey node, bool fresh)
        {
            double absorptionGroup1 = fresh ? 0.31 : 0.30;
            double absorptionGroup2 = fresh ? 0.205 : 0.20;
            double fissionGroup1 = fresh ? 0.105 : 0.10;
            double nuFissionGroup1 = fresh ? 0.158 : 0.15;
            return new SpatialNodeCoefficients(
                node,
                1.0,
                absorptionGroup1,
                absorptionGroup2,
                0.1,
                fissionGroup1,
                0.1,
                nuFissionGroup1,
                0.25,
                1.0,
                0.0,
                1.0);
        }

        private static IEnumerable<BundleState> CreateInitialBundles(CoreTopology topology)
        {
            for (uint channel = 0; channel < topology.ChannelCount; channel++)
            {
                for (uint position = 0; position < topology.BundlePositionCount; position++)
                {
                    ulong sequence = 2000UL + (channel * topology.BundlePositionCount) + position + 1;
                    yield return new BundleState(
                        StableId.Parse(
                            "00000000-0000-0000-0000-" +
                            sequence.ToString("D12", CultureInfo.InvariantCulture)),
                        new ChannelId(channel),
                        new BundlePosition(position),
                        new MaterialVariantId(OldFuelTypeId),
                        0.0,
                        0.0,
                        1000.0,
                        0.0);
                }
            }
        }

        private BundleState[] CreateInsertedBundles(
            RefuelSchemePositionPlan plan,
            uint channelIndex)
        {
            var inserted = new BundleState[plan.InsertedPositions.Count];
            if (_nextBundleSequence > ulong.MaxValue - (ulong)plan.InsertedPositions.Count)
            {
                throw new InvalidOperationException("The Lab bundle identity sequence exhausted its range.");
            }

            ChannelId channel = new ChannelId(channelIndex);
            for (int index = 0; index < plan.InsertedPositions.Count; index++)
            {
                inserted[index] = new BundleState(
                    StableId.Parse(
                        "00000000-0000-0000-0000-" +
                        (_nextBundleSequence + (ulong)index + 1).ToString(
                            "D12",
                            CultureInfo.InvariantCulture)),
                    channel,
                    plan.InsertedPositions[index],
                    new MaterialVariantId(FreshFuelTypeId),
                    0.0,
                    0.0,
                    1000.0,
                    0.0);
            }

            return inserted;
        }

        private static void AppendInventoryCanonical(StringBuilder builder, BundleInventory inventory)
        {
            foreach (BundleState bundle in inventory.EnumerateOccupied()
                         .OrderBy(value => value.ChannelId.Value)
                         .ThenBy(value => value.Position.Value))
            {
                builder.Append('|');
                builder.Append(bundle.BundleId);
                builder.Append(':');
                builder.Append(bundle.ChannelId.Value.ToString(CultureInfo.InvariantCulture));
                builder.Append(':');
                builder.Append(bundle.Position.Value.ToString(CultureInfo.InvariantCulture));
                builder.Append(':');
                builder.Append(bundle.MaterialVariantId.Value);
                builder.Append(':');
                builder.Append(bundle.CurrentBurnupJPerKgHm.ToString("R", CultureInfo.InvariantCulture));
                builder.Append(':');
                builder.Append(bundle.StateVersion.ToString(CultureInfo.InvariantCulture));
            }
        }

        private static void AppendSolveCanonical(StringBuilder builder, SpatialSolveResult solve)
        {
            builder.Append('|');
            builder.Append(PlaytestProtocolV1.EnumId(solve.Status));
            SpatialEigenIterationState? state = solve.FinalState;
            if (state == null)
            {
                return;
            }

            builder.Append(':');
            builder.Append(state.Iteration.ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(state.Eigenvalue.ToString("R", CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(state.TotalPowerW.ToString("R", CultureInfo.InvariantCulture));
            foreach (double flux in state.Group1Flux)
            {
                builder.Append(':');
                builder.Append(flux.ToString("R", CultureInfo.InvariantCulture));
            }

            foreach (double flux in state.Group2Flux)
            {
                builder.Append(':');
                builder.Append(flux.ToString("R", CultureInfo.InvariantCulture));
            }
        }

        private static CoreTopology CreateTopology()
        {
            var channels = new List<ChannelTopology>((int)ChannelCount);
            for (uint channelIndex = 0; channelIndex < ChannelCount; channelIndex++)
            {
                FlowDirection flowDirection = channelIndex == 0
                    ? FlowDirection.EndAtoEndB
                    : FlowDirection.EndBtoEndA;
                var neighbors = new List<NeighborRecord>();
                var boundaries = new List<BoundaryFaceRecord>();
                ChannelId channel = new ChannelId(channelIndex);

                for (uint position = 0; position + 1 < BundlePositionCount; position++)
                {
                    neighbors.Add(new NeighborRecord(
                        channel,
                        new BundlePosition(position),
                        channel,
                        new BundlePosition(position + 1),
                        NeighborDirection.TowardEndB));
                    neighbors.Add(new NeighborRecord(
                        channel,
                        new BundlePosition(position + 1),
                        channel,
                        new BundlePosition(position),
                        NeighborDirection.TowardEndA));
                }

                for (uint position = 0; position < BundlePositionCount; position++)
                {
                    ChannelId otherChannel = new ChannelId(channelIndex == 0 ? 1u : 0u);
                    neighbors.Add(new NeighborRecord(
                        channel,
                        new BundlePosition(position),
                        otherChannel,
                        new BundlePosition(position),
                        channelIndex == 0 ? NeighborDirection.East : NeighborDirection.West));
                    boundaries.Add(new BoundaryFaceRecord(
                        channel,
                        new BundlePosition(position),
                        TopologyFace.North,
                        BoundaryClassification.Reflective));
                    boundaries.Add(new BoundaryFaceRecord(
                        channel,
                        new BundlePosition(position),
                        TopologyFace.South,
                        BoundaryClassification.Reflective));
                    boundaries.Add(new BoundaryFaceRecord(
                        channel,
                        new BundlePosition(position),
                        channelIndex == 0 ? TopologyFace.West : TopologyFace.East,
                        BoundaryClassification.Reflective));
                }

                boundaries.Add(new BoundaryFaceRecord(
                    channel,
                    new BundlePosition(0),
                    TopologyFace.EndA,
                    BoundaryClassification.Reflective));
                boundaries.Add(new BoundaryFaceRecord(
                    channel,
                    new BundlePosition(BundlePositionCount - 1),
                    TopologyFace.EndB,
                    BoundaryClassification.Reflective));

                channels.Add(new ChannelTopology(
                    channel,
                    (int)channelIndex,
                    0,
                    flowDirection,
                    flowDirection == FlowDirection.EndAtoEndB
                        ? new BundlePosition(0)
                        : new BundlePosition(BundlePositionCount - 1),
                    flowDirection == FlowDirection.EndAtoEndB
                        ? new BundlePosition(BundlePositionCount - 1)
                        : new BundlePosition(0),
                    neighbors,
                    boundaries));
            }

            ContractValidationResult<CoreTopology> result = CoreTopology.TryCreate(
                ChannelCount,
                BundlePositionCount,
                channels);
            if (!result.IsValid)
            {
                throw new InvalidOperationException(result.FirstDiagnostic.ToString());
            }

            return result.Value;
        }

        internal static BridgeDiagnosticDto? TryReadSolverOptions(
            JsonElement command,
            LabSolverOptions baseline,
            string propertyName,
            out LabSolverOptions options)
        {
            options = baseline;
            if (!PlaytestInput.TryGetProperty(command, out JsonElement solver, propertyName))
            {
                return null;
            }

            if (solver.ValueKind != JsonValueKind.Object)
            {
                return PlaytestProtocolV1.Diagnostic(
                    "Lab.Solver.Invalid",
                    propertyName,
                    "solver must be a JSON object.");
            }

            int maximumIterations = baseline.MaximumIterations;
            int innerMaximumIterations = baseline.InnerMaximumIterations;
            double targetPowerW = baseline.TargetPowerW;
            double initialEigenvalue = baseline.InitialEigenvalue;
            double kAbsoluteTolerance = baseline.KAbsoluteTolerance;
            double kRelativeTolerance = baseline.KRelativeTolerance;
            double residualTolerance = baseline.ResidualTolerance;
            double sourceShapeTolerance = baseline.SourceShapeTolerance;
            double powerBalanceTolerance = baseline.PowerBalanceTolerance;
            double edgeConductanceM2 = baseline.EdgeConductanceM2;

            BridgeDiagnosticDto? diagnostic = null;
            diagnostic ??= TryReadPositiveInt(
                solver,
                "maximumIterations",
                "maximum_iterations",
                ref maximumIterations);
            diagnostic ??= TryReadPositiveInt(
                solver,
                "innerMaximumIterations",
                "inner_maximum_iterations",
                ref innerMaximumIterations);
            diagnostic ??= TryReadPositiveFinite(
                solver,
                "targetPowerW",
                "target_power_w",
                ref targetPowerW);
            diagnostic ??= TryReadPositiveFinite(
                solver,
                "initialEigenvalue",
                "initial_eigenvalue",
                ref initialEigenvalue);
            diagnostic ??= TryReadPositiveFinite(
                solver,
                "kAbsoluteTolerance",
                "k_absolute_tolerance",
                ref kAbsoluteTolerance);
            diagnostic ??= TryReadPositiveFinite(
                solver,
                "kRelativeTolerance",
                "k_relative_tolerance",
                ref kRelativeTolerance);
            diagnostic ??= TryReadPositiveFinite(
                solver,
                "residualTolerance",
                "residual_tolerance",
                ref residualTolerance);
            diagnostic ??= TryReadPositiveFinite(
                solver,
                "sourceShapeTolerance",
                "source_shape_tolerance",
                ref sourceShapeTolerance);
            diagnostic ??= TryReadPositiveFinite(
                solver,
                "powerBalanceTolerance",
                "power_balance_tolerance",
                ref powerBalanceTolerance);
            diagnostic ??= TryReadNonnegativeFinite(
                solver,
                "edgeConductanceM2",
                "edge_conductance_m2",
                ref edgeConductanceM2);
            if (diagnostic != null)
            {
                return diagnostic;
            }

            options = new LabSolverOptions
            {
                MaximumIterations = maximumIterations,
                InnerMaximumIterations = innerMaximumIterations,
                TargetPowerW = targetPowerW,
                InitialEigenvalue = initialEigenvalue,
                KAbsoluteTolerance = kAbsoluteTolerance,
                KRelativeTolerance = kRelativeTolerance,
                ResidualTolerance = residualTolerance,
                SourceShapeTolerance = sourceShapeTolerance,
                PowerBalanceTolerance = powerBalanceTolerance,
                EdgeConductanceM2 = edgeConductanceM2
            };
            return null;
        }

        private static BridgeDiagnosticDto? TryReadPositiveInt(
            JsonElement objectValue,
            string camelName,
            string snakeName,
            ref int target)
        {
            if (!PlaytestInput.TryGetProperty(objectValue, out JsonElement value, camelName, snakeName))
            {
                return null;
            }

            if (!value.TryGetInt32(out int parsed) || parsed <= 0)
            {
                return PlaytestProtocolV1.Diagnostic(
                    "Lab.Solver.Integer.Invalid",
                    camelName,
                    "The solver iteration limit must be a strictly positive JSON integer.");
            }

            target = parsed;
            return null;
        }

        private static BridgeDiagnosticDto? TryReadPositiveFinite(
            JsonElement objectValue,
            string camelName,
            string snakeName,
            ref double target)
        {
            if (!PlaytestInput.TryGetProperty(objectValue, out JsonElement value, camelName, snakeName))
            {
                return null;
            }

            if (!value.TryGetDouble(out double parsed) || !double.IsFinite(parsed) || parsed <= 0.0)
            {
                return PlaytestProtocolV1.Diagnostic(
                    "Lab.Solver.Number.Invalid",
                    camelName,
                    "The solver value must be finite and strictly positive.");
            }

            target = parsed;
            return null;
        }

        private static BridgeDiagnosticDto? TryReadNonnegativeFinite(
            JsonElement objectValue,
            string camelName,
            string snakeName,
            ref double target)
        {
            if (!PlaytestInput.TryGetProperty(objectValue, out JsonElement value, camelName, snakeName))
            {
                return null;
            }

            if (!value.TryGetDouble(out double parsed) || !double.IsFinite(parsed) || parsed < 0.0)
            {
                return PlaytestProtocolV1.Diagnostic(
                    "Lab.Solver.Number.Invalid",
                    camelName,
                    "The solver value must be finite and nonnegative.");
            }

            target = parsed;
            return null;
        }
    }
}
