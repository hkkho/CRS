using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ReactorSim.Core;

namespace ReactorSim.Game
{
    /// <summary>
    /// Explicit physics metrics exposed to presentation consumers. The
    /// current practice path is backed by deterministic static k-eigenmode
    /// shape recomputation plus a regulated steady-state scalar amplitude;
    /// Reactivity remains a state-level value derived from k. The legacy
    /// source/solver property names remain available for public v1
    /// compatibility.
    /// </summary>
    public sealed class GamePhysicsPresentationSnapshot
    {
        internal GamePhysicsPresentationSnapshot(
            string sourceId,
            string formulationId,
            string shapeMethodId,
            string amplitudeMethodId,
            string reactivityMethodId,
            string solveState,
            bool isAuthoritative,
            ulong bindingVersion,
            double referencePowerWatts,
            double powerAmplitude,
            double actualPowerFraction,
            double targetPowerWatts,
            double totalPowerWatts,
            double meanChannelPowerWatts,
            double meanBundlePowerWatts,
            double effectiveK,
            double reactivity,
            double powerBalanceRelativeError,
            string solverIdentity,
            int solverIterationCount,
            double solverResidualRelativeInfinity,
            double coreReactivity,
            double compensatedNetReactivity,
            double compensationState,
            double compensationCommand,
            double compensationLowerBound,
            double compensationUpperBound,
            bool compensationSaturated,
            double compensationResponseTimeSeconds,
            string cadenceIdentity,
            string adjointNormalizationIdentity,
            string adjointDigestHex,
            int adjointIterationCount,
            double adjointTransposeResidualRelativeInfinity)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                throw new ArgumentException("Physics metrics require a source identity.", nameof(sourceId));
            }

            RequireIdentity(formulationId, "formulation identity", nameof(formulationId));
            RequireIdentity(shapeMethodId, "shape method identity", nameof(shapeMethodId));
            RequireIdentity(amplitudeMethodId, "amplitude method identity", nameof(amplitudeMethodId));
            RequireIdentity(reactivityMethodId, "reactivity method identity", nameof(reactivityMethodId));

            if (string.IsNullOrWhiteSpace(solveState))
            {
                throw new ArgumentException("Physics metrics require a solve state.", nameof(solveState));
            }

            RequireFinitePositive(referencePowerWatts, nameof(referencePowerWatts));
            RequireFiniteNonnegative(powerAmplitude, nameof(powerAmplitude));
            RequireFiniteNonnegative(actualPowerFraction, nameof(actualPowerFraction));
            RequireFiniteNonnegative(targetPowerWatts, nameof(targetPowerWatts));
            RequireFiniteNonnegative(totalPowerWatts, nameof(totalPowerWatts));
            RequireFiniteNonnegative(meanChannelPowerWatts, nameof(meanChannelPowerWatts));
            RequireFiniteNonnegative(meanBundlePowerWatts, nameof(meanBundlePowerWatts));
            RequireFinitePositive(effectiveK, nameof(effectiveK));
            RequireFinite(reactivity, nameof(reactivity));
            RequireFinite(coreReactivity, nameof(coreReactivity));
            RequireFinite(compensatedNetReactivity, nameof(compensatedNetReactivity));
            RequireFinite(compensationState, nameof(compensationState));
            RequireFinite(compensationCommand, nameof(compensationCommand));
            RequireFinite(compensationLowerBound, nameof(compensationLowerBound));
            RequireFinite(compensationUpperBound, nameof(compensationUpperBound));
            if (compensationLowerBound >= compensationUpperBound ||
                compensationState < compensationLowerBound ||
                compensationState > compensationUpperBound ||
                compensationCommand < compensationLowerBound ||
                compensationCommand > compensationUpperBound)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(compensationState),
                    "Compensation state and command must remain within ordered explicit bounds.");
            }

            RequireFinitePositive(
                compensationResponseTimeSeconds,
                nameof(compensationResponseTimeSeconds));
            RequireFiniteNonnegative(powerBalanceRelativeError, nameof(powerBalanceRelativeError));
            if (string.IsNullOrWhiteSpace(solverIdentity))
            {
                throw new ArgumentException(
                    "Physics metrics require a solver identity.",
                    nameof(solverIdentity));
            }

            if (string.IsNullOrWhiteSpace(cadenceIdentity))
            {
                throw new ArgumentException(
                    "Physics metrics require a deterministic cadence identity.",
                    nameof(cadenceIdentity));
            }

            if (string.IsNullOrWhiteSpace(adjointNormalizationIdentity))
            {
                throw new ArgumentException(
                    "Physics metrics require an adjoint normalization identity.",
                    nameof(adjointNormalizationIdentity));
            }

            if (string.IsNullOrWhiteSpace(adjointDigestHex))
            {
                throw new ArgumentException(
                    "Physics metrics require an adjoint digest.",
                    nameof(adjointDigestHex));
            }

            if (solverIterationCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(solverIterationCount));
            }

            if (adjointIterationCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(adjointIterationCount));
            }

            RequireFiniteNonnegative(
                solverResidualRelativeInfinity,
                nameof(solverResidualRelativeInfinity));
            RequireFiniteNonnegative(
                adjointTransposeResidualRelativeInfinity,
                nameof(adjointTransposeResidualRelativeInfinity));

            SourceId = sourceId;
            FormulationId = formulationId;
            ShapeMethodId = shapeMethodId;
            AmplitudeMethodId = amplitudeMethodId;
            ReactivityMethodId = reactivityMethodId;
            SolveState = solveState;
            IsAuthoritative = isAuthoritative;
            BindingVersion = bindingVersion;
            ReferencePowerWatts = referencePowerWatts;
            PowerAmplitude = powerAmplitude;
            ActualPowerFraction = actualPowerFraction;
            TargetPowerWatts = targetPowerWatts;
            TotalPowerWatts = totalPowerWatts;
            MeanChannelPowerWatts = meanChannelPowerWatts;
            MeanBundlePowerWatts = meanBundlePowerWatts;
            EffectiveK = effectiveK;
            Reactivity = reactivity;
            CoreReactivity = coreReactivity;
            CompensatedNetReactivity = compensatedNetReactivity;
            CompensationState = compensationState;
            CompensationCommand = compensationCommand;
            CompensationLowerBound = compensationLowerBound;
            CompensationUpperBound = compensationUpperBound;
            CompensationSaturated = compensationSaturated;
            CompensationResponseTimeSeconds = compensationResponseTimeSeconds;
            CadenceIdentity = cadenceIdentity;
            AdjointNormalizationIdentity = adjointNormalizationIdentity;
            AdjointDigestHex = adjointDigestHex;
            AdjointIterationCount = adjointIterationCount;
            AdjointTransposeResidualRelativeInfinity = adjointTransposeResidualRelativeInfinity;
            PowerBalanceRelativeError = powerBalanceRelativeError;
            SolverIdentity = solverIdentity;
            SolverIterationCount = solverIterationCount;
            SolverResidualRelativeInfinity = solverResidualRelativeInfinity;
        }

        public string SourceId { get; }

        public string FormulationId { get; }

        public string ShapeMethodId { get; }

        public string AmplitudeMethodId { get; }

        public string ReactivityMethodId { get; }

        public string SolveState { get; }

        public bool IsAuthoritative { get; }

        public ulong BindingVersion { get; }

        public double ReferencePowerWatts { get; }

        /// <summary>
        /// Scalar regulated steady-state practice amplitude, independent of
        /// the operator setpoint. The active gameplay cadence is identified
        /// by CadenceIdentity; this is not a sub-second transient claim.
        /// </summary>
        public double PowerAmplitude { get; }

        /// <summary>
        /// Normalized fission power after applying the regulated steady-state
        /// amplitude and normalized static-eigenmode shape to the operator
        /// setpoint.
        /// </summary>
        public double ActualPowerFraction { get; }

        public double TargetPowerWatts { get; }

        public double TotalPowerWatts { get; }

        public double MeanChannelPowerWatts { get; }

        public double MeanBundlePowerWatts { get; }

        public double EffectiveK { get; }

        public double Reactivity { get; }

        /// <summary>
        /// Static/core reactivity from the authoritative spatial candidate.
        /// Reactivity remains the legacy alias for this value.
        /// </summary>
        public double CoreReactivity { get; }

        /// <summary>
        /// Core reactivity plus the bounded scalar compensation state. This
        /// is the value the practice regulator is driving toward zero.
        /// </summary>
        public double CompensatedNetReactivity { get; }

        public double CompensationState { get; }

        /// <summary>
        /// Compatibility-facing physical-unit alias for CompensationState.
        /// </summary>
        public double CompensationReactivity
        {
            get { return CompensationState; }
        }

        public double CompensationCommand { get; }

        public double CompensationLowerBound { get; }

        public double CompensationUpperBound { get; }

        public bool CompensationSaturated { get; }

        public double CompensationResponseTimeSeconds { get; }

        public string CadenceIdentity { get; }

        public string AdjointNormalizationIdentity { get; }

        public string AdjointDigestHex { get; }

        public int AdjointIterationCount { get; }

        public double AdjointTransposeResidualRelativeInfinity { get; }

        public double PowerBalanceRelativeError { get; }

        public string SolverIdentity { get; }

        public int SolverIterationCount { get; }

        public double SolverResidualRelativeInfinity { get; }

        private static void RequireFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, "Physics metrics must be finite.");
            }
        }

        private static void RequireFiniteNonnegative(double value, string parameterName)
        {
            RequireFinite(value, parameterName);
            if (value < 0.0)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Physics metrics must be nonnegative.");
            }
        }

        private static void RequireFinitePositive(double value, string parameterName)
        {
            RequireFinite(value, parameterName);
            if (value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(parameterName, "This physics metric must be strictly positive.");
            }
        }

        private static void RequireIdentity(
            string value,
            string description,
            string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "Physics metrics require a " + description + ".",
                    parameterName);
            }
        }
    }

    /// <summary>
    /// Immutable presentation projection of the synthetic 380-channel core.
    /// It contains only values needed by the game surface; Core remains the
    /// owner of bundle transitions and physical state.
    /// </summary>
    public sealed class GameCorePresentationSnapshot
    {
        internal GameCorePresentationSnapshot(
            IEnumerable<GameChannelPresentationSnapshot> channels,
            GamePhysicsPresentationSnapshot physics)
        {
            if (channels == null)
            {
                throw new ArgumentNullException(nameof(channels));
            }

            Physics = physics ?? throw new ArgumentNullException(nameof(physics));

            GameChannelPresentationSnapshot[] copy = channels.ToArray();
            if (copy.Length != GameCorePresentationConstants.ChannelCount)
            {
                throw new ArgumentException(
                    "A core presentation requires exactly 380 channels.",
                    nameof(channels));
            }

            if (copy.Any(channel => channel == null))
            {
                throw new ArgumentException(
                    "A core presentation may not contain null channels.",
                    nameof(channels));
            }

            Channels = new ReadOnlyCollection<GameChannelPresentationSnapshot>(copy);
        }

        public IReadOnlyList<GameChannelPresentationSnapshot> Channels { get; }

        public GamePhysicsPresentationSnapshot Physics { get; }

        public uint ChannelCount
        {
            get { return checked((uint)Channels.Count); }
        }

        public GameChannelPresentationSnapshot GetChannel(uint channelIndex)
        {
            if (channelIndex >= ChannelCount)
            {
                throw new ArgumentOutOfRangeException(nameof(channelIndex));
            }

            return Channels[(int)channelIndex];
        }
    }

    public sealed class GameChannelPresentationSnapshot
    {
        internal GameChannelPresentationSnapshot(
            uint channelIndex,
            int gridColumn,
            int gridRow,
            double averageBurnupMwDayPerKg,
            double powerWatts,
            double localPowerFraction,
            double localTiltFraction,
            FlowDirection flowDirection,
            IEnumerable<GameBundlePresentationSnapshot> bundles)
        {
            if (bundles == null)
            {
                throw new ArgumentNullException(nameof(bundles));
            }

            GameBundlePresentationSnapshot[] copy = bundles.ToArray();
            if (copy.Length != GameCorePresentationConstants.BundlePositionCount)
            {
                throw new ArgumentException(
                    "A channel presentation requires exactly 12 bundles.",
                    nameof(bundles));
            }

            ChannelIndex = channelIndex;
            GridColumn = gridColumn;
            GridRow = gridRow;
            AverageBurnupMwDayPerKg = averageBurnupMwDayPerKg;
            PowerWatts = powerWatts;
            LocalPowerFraction = localPowerFraction;
            LocalTiltFraction = localTiltFraction;
            FlowDirection = flowDirection;
            Bundles = new ReadOnlyCollection<GameBundlePresentationSnapshot>(copy);
        }

        public uint ChannelIndex { get; }

        public int GridColumn { get; }

        public int GridRow { get; }

        public double AverageBurnupMwDayPerKg { get; }

        public double PowerWatts { get; }

        public double LocalPowerFraction { get; }

        public double LocalTiltFraction { get; }

        public FlowDirection FlowDirection { get; }

        public IReadOnlyList<GameBundlePresentationSnapshot> Bundles { get; }
    }

    public sealed class GameBundlePresentationSnapshot
    {
        internal GameBundlePresentationSnapshot(
            uint position,
            string bundleId,
            string fuelTypeId,
            double currentBurnupMwDayPerKg,
            double powerWatts,
            double insertedAtSeconds,
            ulong stateVersion)
        {
            if (string.IsNullOrWhiteSpace(bundleId))
            {
                throw new ArgumentException(
                    "A bundle presentation requires a bundle identity.",
                    nameof(bundleId));
            }

            if (string.IsNullOrWhiteSpace(fuelTypeId))
            {
                throw new ArgumentException(
                    "A bundle presentation requires a fuel type.",
                    nameof(fuelTypeId));
            }

            Position = position;
            BundleId = bundleId;
            FuelTypeId = fuelTypeId;
            CurrentBurnupMwDayPerKg = currentBurnupMwDayPerKg;
            PowerWatts = powerWatts;
            InsertedAtSeconds = insertedAtSeconds;
            StateVersion = stateVersion;
        }

        public uint Position { get; }

        public string BundleId { get; }

        public string FuelTypeId { get; }

        public double CurrentBurnupMwDayPerKg { get; }

        public double PowerWatts { get; }

        public double InsertedAtSeconds { get; }

        public ulong StateVersion { get; }

        public bool IsFresh
        {
            get { return Math.Abs(CurrentBurnupMwDayPerKg) < 1e-12; }
        }
    }

    public static class GameCorePresentationConstants
    {
        public const uint ChannelCount = 380;
        public const uint BundlePositionCount = 12;
        public const int GridWidth = 22;
        public const int GridHeight = 22;
        public const double JoulesPerMegaWattDayPerKilogram = 8.64e10;
    }

    /// <summary>
    /// Deterministic synthetic face layout. The row lengths describe a
    /// rounded 22x22 CANDU-6 face and sum to the approved 380 channels. The
    /// row lengths retain the stepped CANDU-6 outline rather than filling a
    /// rectangular 20-channel middle band.
    /// Channel identifiers are assigned row-major within this layout.
    /// </summary>
    internal static class PracticeCoreLayout
    {
        public static PracticeCoreGridPosition GetPosition(uint channelIndex)
        {
            Candu6GridPositionV1 position = Candu6CoreTopologyFactoryV1.GetPosition(channelIndex);
            return new PracticeCoreGridPosition(position.Column, position.DisplayRow);
        }

        public static bool TryGetChannelIndex(
            int column,
            int row,
            out uint channelIndex)
        {
            return Candu6CoreTopologyFactoryV1.TryGetChannelIndex(
                column,
                row,
                out channelIndex);
        }

        public static int GetRowLength(int row)
        {
            return Candu6CoreTopologyFactoryV1.GetRowLength(row);
        }

        public static FlowDirection GetFlowDirection(PracticeCoreGridPosition position)
        {
            return Candu6CoreTopologyFactoryV1.GetFlowDirection(
                position.Column,
                position.Row);
        }
    }

    internal readonly struct PracticeCoreGridPosition
    {
        internal PracticeCoreGridPosition(int column, int row)
        {
            Column = column;
            Row = row;
        }

        internal int Column { get; }

        internal int Row { get; }
    }
}
