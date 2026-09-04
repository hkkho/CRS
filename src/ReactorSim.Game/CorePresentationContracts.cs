using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ReactorSim.Core;

namespace ReactorSim.Game
{
    /// <summary>
    /// Explicit physics metrics exposed to presentation consumers. The
    /// reduced source is a deterministic migration fallback; it is not a
    /// calibrated plant model and does not expose a per-bundle reactivity
    /// value. Reactivity is a state-level value derived from k.
    /// </summary>
    public sealed class GamePhysicsPresentationSnapshot
    {
        internal GamePhysicsPresentationSnapshot(
            string sourceId,
            string solveState,
            bool isAuthoritative,
            ulong bindingVersion,
            double referencePowerWatts,
            double powerAmplitude,
            double targetPowerWatts,
            double totalPowerWatts,
            double meanChannelPowerWatts,
            double meanBundlePowerWatts,
            double effectiveK,
            double reactivity,
            double powerBalanceRelativeError)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                throw new ArgumentException("Physics metrics require a source identity.", nameof(sourceId));
            }

            if (string.IsNullOrWhiteSpace(solveState))
            {
                throw new ArgumentException("Physics metrics require a solve state.", nameof(solveState));
            }

            RequireFinitePositive(referencePowerWatts, nameof(referencePowerWatts));
            RequireFiniteNonnegative(powerAmplitude, nameof(powerAmplitude));
            RequireFiniteNonnegative(targetPowerWatts, nameof(targetPowerWatts));
            RequireFiniteNonnegative(totalPowerWatts, nameof(totalPowerWatts));
            RequireFiniteNonnegative(meanChannelPowerWatts, nameof(meanChannelPowerWatts));
            RequireFiniteNonnegative(meanBundlePowerWatts, nameof(meanBundlePowerWatts));
            RequireFinitePositive(effectiveK, nameof(effectiveK));
            RequireFinite(reactivity, nameof(reactivity));
            RequireFiniteNonnegative(powerBalanceRelativeError, nameof(powerBalanceRelativeError));

            SourceId = sourceId;
            SolveState = solveState;
            IsAuthoritative = isAuthoritative;
            BindingVersion = bindingVersion;
            ReferencePowerWatts = referencePowerWatts;
            PowerAmplitude = powerAmplitude;
            TargetPowerWatts = targetPowerWatts;
            TotalPowerWatts = totalPowerWatts;
            MeanChannelPowerWatts = meanChannelPowerWatts;
            MeanBundlePowerWatts = meanBundlePowerWatts;
            EffectiveK = effectiveK;
            Reactivity = reactivity;
            PowerBalanceRelativeError = powerBalanceRelativeError;
        }

        public string SourceId { get; }

        public string SolveState { get; }

        public bool IsAuthoritative { get; }

        public ulong BindingVersion { get; }

        public double ReferencePowerWatts { get; }

        public double PowerAmplitude { get; }

        public double TargetPowerWatts { get; }

        public double TotalPowerWatts { get; }

        public double MeanChannelPowerWatts { get; }

        public double MeanBundlePowerWatts { get; }

        public double EffectiveK { get; }

        public double Reactivity { get; }

        public double PowerBalanceRelativeError { get; }

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
        private static readonly int[] RowLengths =
        {
            6, 12, 14, 16, 18, 18, 20, 20,
            22, 22, 22, 22, 22, 22,
            20, 20, 18, 18, 16, 14, 12, 6
        };

        private static readonly PracticeCoreGridPosition[] Positions = CreatePositions();
        private static readonly Dictionary<int, uint> ChannelIndices = CreateChannelIndices();

        public static PracticeCoreGridPosition GetPosition(uint channelIndex)
        {
            if (channelIndex >= GameCorePresentationConstants.ChannelCount)
            {
                throw new ArgumentOutOfRangeException(nameof(channelIndex));
            }

            return Positions[(int)channelIndex];
        }

        public static bool TryGetChannelIndex(
            int column,
            int row,
            out uint channelIndex)
        {
            return ChannelIndices.TryGetValue(
                checked(row * GameCorePresentationConstants.GridWidth + column),
                out channelIndex);
        }

        public static int GetRowLength(int row)
        {
            if (row < 0 || row >= RowLengths.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(row));
            }

            return RowLengths[row];
        }

        public static FlowDirection GetFlowDirection(PracticeCoreGridPosition position)
        {
            return (position.Column + position.Row) % 2 == 0
                ? FlowDirection.EndAtoEndB
                : FlowDirection.EndBtoEndA;
        }

        private static PracticeCoreGridPosition[] CreatePositions()
        {
            var positions = new List<PracticeCoreGridPosition>(
                (int)GameCorePresentationConstants.ChannelCount);
            for (int row = 0; row < RowLengths.Length; row++)
            {
                int rowLength = RowLengths[row];
                int firstColumn = (GameCorePresentationConstants.GridWidth - rowLength) / 2;
                for (int offset = 0; offset < rowLength; offset++)
                {
                    positions.Add(new PracticeCoreGridPosition(firstColumn + offset, row));
                }
            }

            if (positions.Count != GameCorePresentationConstants.ChannelCount)
            {
                throw new InvalidOperationException(
                    "The deterministic practice core layout must contain 380 channels.");
            }

            return positions.ToArray();
        }

        private static Dictionary<int, uint> CreateChannelIndices()
        {
            var result = new Dictionary<int, uint>();
            for (uint index = 0; index < Positions.Length; index++)
            {
                PracticeCoreGridPosition position = Positions[(int)index];
                result.Add(
                    checked(position.Row * GameCorePresentationConstants.GridWidth + position.Column),
                    index);
            }

            return result;
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
