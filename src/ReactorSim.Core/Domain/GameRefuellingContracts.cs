using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace ReactorSim.Core
{
    public enum GameRefuellingDirectionV1 : byte
    {
        TowardEndA = 0,
        TowardEndB = 1
    }

    public sealed class GameRefuellingResultV1
    {
        internal GameRefuellingResultV1(
            SyntheticGameCoreStateV1 resultingState,
            uint channelIndex,
            GameRefuellingDirectionV1 direction,
            ushort shiftCount,
            string fuelTypeId,
            IEnumerable<BundleState> insertedBundles,
            IEnumerable<BundleState> dischargedBundles)
        {
            ResultingState = resultingState;
            ChannelIndex = channelIndex;
            Direction = direction;
            ShiftCount = shiftCount;
            FuelTypeId = fuelTypeId;
            InsertedBundles = new ReadOnlyCollection<BundleState>(insertedBundles.ToArray());
            DischargedBundles = new ReadOnlyCollection<BundleState>(dischargedBundles.ToArray());
        }

        public SyntheticGameCoreStateV1 ResultingState { get; }

        public uint ChannelIndex { get; }

        public GameRefuellingDirectionV1 Direction { get; }

        public ushort ShiftCount { get; }

        public string FuelTypeId { get; }

        public IReadOnlyList<BundleState> InsertedBundles { get; }

        public IReadOnlyList<BundleState> DischargedBundles { get; }
    }

    /// <summary>
    /// Deterministic bundle inventory used by the playable game slice. It owns
    /// bundle movement and identities; the shared full-core model consumes its
    /// live records for spatial coefficient binding and power projection.
    /// </summary>
    public sealed class SyntheticGameCoreStateV1
    {
        public const uint ChannelCount = 380;
        public const uint BundlePositionCount = 12;
        public const uint DefaultFreshBundleCount = 128;

        private const ulong FirstFreshBundleSequence = 100000;
        private const double JoulesPerMegaWattDayPerKilogram = 8.64e10;

        private readonly BundleState[][] _channels;

        private SyntheticGameCoreStateV1(
            BundleState[][] channels,
            uint freshBundlesAvailable,
            uint refuellingOperationCount,
            ulong nextFreshBundleSequence,
            int lastRefuelledChannel,
            GameRefuellingDirectionV1 lastDirection,
            ushort lastShiftCount)
        {
            _channels = channels;
            FreshBundlesAvailable = freshBundlesAvailable;
            RefuellingOperationCount = refuellingOperationCount;
            NextFreshBundleSequence = nextFreshBundleSequence;
            LastRefuelledChannel = lastRefuelledChannel;
            LastDirection = lastDirection;
            LastShiftCount = lastShiftCount;
        }

        public uint FreshBundlesAvailable { get; }

        public uint RefuellingOperationCount { get; }

        public ulong NextFreshBundleSequence { get; }

        public int LastRefuelledChannel { get; }

        public GameRefuellingDirectionV1 LastDirection { get; }

        public ushort LastShiftCount { get; }

        public static SyntheticGameCoreStateV1 CreatePractice()
        {
            var channels = new BundleState[ChannelCount][];
            ulong bundleSequence = 1;
            for (uint channel = 0; channel < ChannelCount; channel++)
            {
                channels[channel] = new BundleState[BundlePositionCount];
                Candu6GridPositionV1 gridPosition =
                    Candu6CoreTopologyFactoryV1.GetPosition(channel);
                double centeredX = (gridPosition.Column - 10.5) / 11.5;
                double centeredY = (gridPosition.CartesianY - 10.5) / 11.5;
                double radialDistance = Math.Sqrt(
                    centeredX * centeredX + centeredY * centeredY) /
                    Math.Sqrt(2.0);
                double radialShape = Math.Max(
                    0.0,
                    Math.Min(1.0, 1.0 - radialDistance));
                for (uint position = 0; position < BundlePositionCount; position++)
                {
                    double axialShape = 1.0 - Math.Abs(position - 5.5) / 12.0;
                    double burnupMwDayPerKg = 3.0 + 4.0 * radialShape + 2.0 * axialShape;
                    channels[channel][position] = new BundleState(
                        StableIdFor(bundleSequence++),
                        new ChannelId(channel),
                        new BundlePosition(position),
                        new MaterialVariantId("NAT-U-SYNTHETIC"),
                        burnupMwDayPerKg * JoulesPerMegaWattDayPerKilogram,
                        0.0,
                        19.2,
                        0.0);
                }
            }

            return new SyntheticGameCoreStateV1(
                channels,
                DefaultFreshBundleCount,
                0,
                FirstFreshBundleSequence,
                -1,
                default(GameRefuellingDirectionV1),
                0);
        }

        public BundleState GetBundle(uint channelIndex, uint positionIndex)
        {
            if (channelIndex >= ChannelCount)
            {
                throw new ArgumentOutOfRangeException(nameof(channelIndex));
            }

            if (positionIndex >= BundlePositionCount)
            {
                throw new ArgumentOutOfRangeException(nameof(positionIndex));
            }

            return _channels[channelIndex][positionIndex];
        }

        public IReadOnlyList<BundleState> GetChannel(uint channelIndex)
        {
            if (channelIndex >= ChannelCount)
            {
                throw new ArgumentOutOfRangeException(nameof(channelIndex));
            }

            return new ReadOnlyCollection<BundleState>(_channels[channelIndex].ToArray());
        }

        /// <summary>
        /// Enumerates the immutable live inventory in the same channel-major,
        /// bundle-position order used by the full-core spatial stencil.
        /// </summary>
        public IEnumerable<BundleState> EnumerateBundles()
        {
            for (uint channelIndex = 0; channelIndex < ChannelCount; channelIndex++)
            {
                for (uint positionIndex = 0;
                     positionIndex < BundlePositionCount;
                     positionIndex++)
                {
                    yield return _channels[channelIndex][positionIndex];
                }
            }
        }

        /// <summary>
        /// Returns an immutable debug inventory adjustment. The channel
        /// contents and refuelling history remain unchanged.
        /// </summary>
        public SyntheticGameCoreStateV1 WithFreshBundles(uint additionalBundles)
        {
            if (additionalBundles == 0)
            {
                return this;
            }

            return new SyntheticGameCoreStateV1(
                _channels,
                checked(FreshBundlesAvailable + additionalBundles),
                RefuellingOperationCount,
                NextFreshBundleSequence,
                LastRefuelledChannel,
                LastDirection,
                LastShiftCount);
        }

        /// <summary>
        /// Applies one deterministic burnup integration result in canonical
        /// channel-major, bundle-position order. The caller supplies actual
        /// bundle energy increments in joules; this transition only updates
        /// the immutable inventory and invalidates any accepted power binding
        /// through BundleState.WithEnergy.
        /// </summary>
        public ContractValidationResult<SyntheticGameCoreStateV1> TryAddFissionEnergy(
            IReadOnlyList<double> deltaFissionEnergyJ)
        {
            if (deltaFissionEnergyJ == null)
            {
                return ContractValidationResult<SyntheticGameCoreStateV1>.Invalid(
                    "SyntheticCore.Burnup.Energy.Missing",
                    "delta_fission_energy_j",
                    "A burnup integration requires one energy increment for every bundle position.");
            }

            int expectedCount = checked((int)(ChannelCount * BundlePositionCount));
            if (deltaFissionEnergyJ.Count != expectedCount)
            {
                return ContractValidationResult<SyntheticGameCoreStateV1>.Invalid(
                    "SyntheticCore.Burnup.Energy.CountMismatch",
                    "delta_fission_energy_j",
                    "Burnup integration requires exactly 4560 channel-major bundle energy increments.");
            }

            BundleState[][] nextChannels = new BundleState[ChannelCount][];
            int index = 0;
            for (uint channelIndex = 0; channelIndex < ChannelCount; channelIndex++)
            {
                BundleState[] source = _channels[channelIndex];
                BundleState[] target = new BundleState[BundlePositionCount];
                for (uint position = 0; position < BundlePositionCount; position++)
                {
                    double delta = deltaFissionEnergyJ[index++];
                    if (!PowerHistoryRecordV1.IsCanonicalNonnegative(delta))
                    {
                        return ContractValidationResult<SyntheticGameCoreStateV1>.Invalid(
                            "SyntheticCore.Burnup.Energy.Invalid",
                            "delta_fission_energy_j[" + (index - 1).ToString(CultureInfo.InvariantCulture) + "]",
                            "Burnup energy increments must be finite, canonical, and nonnegative SI joules.");
                    }

                    double cumulative = source[position].CumulativeFissionEnergyJ + delta;
                    if (!PowerHistoryRecordV1.IsCanonicalNonnegative(cumulative))
                    {
                        return ContractValidationResult<SyntheticGameCoreStateV1>.Invalid(
                            "SyntheticCore.Burnup.Energy.Overflow",
                            "delta_fission_energy_j[" + (index - 1).ToString(CultureInfo.InvariantCulture) + "]",
                            "Burnup integration would make cumulative fission energy non-finite.");
                    }

                    target[position] = delta == 0.0
                        ? source[position]
                        : source[position].WithEnergy(cumulative);
                }

                nextChannels[channelIndex] = target;
            }

            return ContractValidationResult<SyntheticGameCoreStateV1>.Valid(
                new SyntheticGameCoreStateV1(
                    nextChannels,
                    FreshBundlesAvailable,
                    RefuellingOperationCount,
                    NextFreshBundleSequence,
                    LastRefuelledChannel,
                    LastDirection,
                    LastShiftCount));
        }

        public ContractValidationResult<GameRefuellingResultV1> TryRefuel(
            uint channelIndex,
            GameRefuellingDirectionV1 direction,
            ushort shiftCount,
            string fuelTypeId,
            double simulationTimeSeconds)
        {
            if (channelIndex >= ChannelCount)
            {
                return Invalid(
                    "GameRefuelling.Channel.OutOfRange",
                    "channelIndex",
                    "Choose a channel from 0 through 379.");
            }

            if (direction != GameRefuellingDirectionV1.TowardEndA &&
                direction != GameRefuellingDirectionV1.TowardEndB)
            {
                return Invalid(
                    "GameRefuelling.Direction.Invalid",
                    "direction",
                    "Choose a valid fuelling direction.");
            }

            if (shiftCount != 4 && shiftCount != 8)
            {
                return Invalid(
                    "GameRefuelling.ShiftCount.Unsupported",
                    "shiftCount",
                    "The practice core supports four- or eight-bundle shifts.");
            }

            if (string.IsNullOrWhiteSpace(fuelTypeId))
            {
                return Invalid(
                    "GameRefuelling.FuelType.Missing",
                    "fuelTypeId",
                    "Choose a fresh-fuel type.");
            }

            if (double.IsNaN(simulationTimeSeconds) ||
                double.IsInfinity(simulationTimeSeconds) ||
                simulationTimeSeconds < 0.0)
            {
                return Invalid(
                    "GameRefuelling.Time.Invalid",
                    "simulationTimeSeconds",
                    "Simulation time must be finite and nonnegative.");
            }

            if (FreshBundlesAvailable < shiftCount)
            {
                return Invalid(
                    "GameRefuelling.FreshInventory.Insufficient",
                    "freshBundlesAvailable",
                    "There are not enough fresh bundles for this shift.");
            }

            BundleState[][] nextChannels = (BundleState[][])_channels.Clone();
            BundleState[] source = _channels[channelIndex];
            var target = new BundleState[BundlePositionCount];
            var inserted = new BundleState[shiftCount];
            var discharged = new BundleState[shiftCount];

            uint insertedStart = direction == GameRefuellingDirectionV1.TowardEndB
                ? 0
                : BundlePositionCount - shiftCount;
            for (uint index = 0; index < shiftCount; index++)
            {
                uint position = insertedStart + index;
                BundleState fresh = new BundleState(
                    StableIdFor(NextFreshBundleSequence + index),
                    new ChannelId(channelIndex),
                    new BundlePosition(position),
                    new MaterialVariantId(fuelTypeId.Trim()),
                    0.0,
                    0.0,
                    19.2,
                    simulationTimeSeconds);
                target[position] = fresh;
                inserted[index] = fresh;
            }

            if (direction == GameRefuellingDirectionV1.TowardEndB)
            {
                for (uint position = 0; position < BundlePositionCount - shiftCount; position++)
                {
                    target[position + shiftCount] = Move(source[position], channelIndex, position + shiftCount);
                }

                for (uint index = 0; index < shiftCount; index++)
                {
                    discharged[index] = source[BundlePositionCount - shiftCount + index];
                }
            }
            else
            {
                for (uint position = shiftCount; position < BundlePositionCount; position++)
                {
                    target[position - shiftCount] = Move(source[position], channelIndex, position - shiftCount);
                }

                for (uint index = 0; index < shiftCount; index++)
                {
                    discharged[index] = source[index];
                }
            }

            nextChannels[channelIndex] = target;
            var nextState = new SyntheticGameCoreStateV1(
                nextChannels,
                FreshBundlesAvailable - shiftCount,
                checked(RefuellingOperationCount + 1),
                checked(NextFreshBundleSequence + shiftCount),
                checked((int)channelIndex),
                direction,
                shiftCount);

            return ContractValidationResult<GameRefuellingResultV1>.Valid(
                new GameRefuellingResultV1(
                    nextState,
                    channelIndex,
                    direction,
                    shiftCount,
                    fuelTypeId.Trim(),
                    inserted,
                    discharged));
        }

        private static BundleState Move(BundleState source, uint channelIndex, uint positionIndex)
        {
            return new BundleState(
                source.BundleId,
                new ChannelId(channelIndex),
                new BundlePosition(positionIndex),
                source.MaterialVariantId,
                source.InitialBurnupJPerKgHm,
                source.CumulativeFissionEnergyJ,
                source.HeavyMetalMassKg,
                source.InsertedAtSeconds,
                source.NuclideState,
                source.PowerWatts,
                source.PowerSnapshotId,
                source.PowerHistory,
                source.CoefficientBinding,
                checked(source.StateVersion + 1));
        }

        private static StableId StableIdFor(ulong sequence)
        {
            return StableId.Parse(
                "00000000-0000-0000-0000-" +
                sequence.ToString("D12", CultureInfo.InvariantCulture));
        }

        private static ContractValidationResult<GameRefuellingResultV1> Invalid(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<GameRefuellingResultV1>.Invalid(code, path, message);
        }
    }
}
