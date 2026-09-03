using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ReactorSim.Core;

namespace ReactorSim.Game
{
    /// <summary>
    /// Immutable presentation projection of the synthetic 380-channel core.
    /// It contains only values needed by the game surface; Core remains the
    /// owner of bundle transitions and physical state.
    /// </summary>
    public sealed class GameCorePresentationSnapshot
    {
        internal GameCorePresentationSnapshot(
            IEnumerable<GameChannelPresentationSnapshot> channels)
        {
            if (channels == null)
            {
                throw new ArgumentNullException(nameof(channels));
            }

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
            LocalPowerFraction = localPowerFraction;
            LocalTiltFraction = localTiltFraction;
            FlowDirection = flowDirection;
            Bundles = new ReadOnlyCollection<GameBundlePresentationSnapshot>(copy);
        }

        public uint ChannelIndex { get; }

        public int GridColumn { get; }

        public int GridRow { get; }

        public double AverageBurnupMwDayPerKg { get; }

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
            InsertedAtSeconds = insertedAtSeconds;
            StateVersion = stateVersion;
        }

        public uint Position { get; }

        public string BundleId { get; }

        public string FuelTypeId { get; }

        public double CurrentBurnupMwDayPerKg { get; }

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

        public static PracticeCoreGridPosition GetPosition(uint channelIndex)
        {
            if (channelIndex >= GameCorePresentationConstants.ChannelCount)
            {
                throw new ArgumentOutOfRangeException(nameof(channelIndex));
            }

            return Positions[(int)channelIndex];
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
