using System.Collections.Generic;
using ReactorSim.Game;

namespace ReactorSim.Browser
{
    // Stable geometry comes from the client's verified base sequence. Bundle
    // columns use channel-major order and identities are sent only when changed.
    internal sealed class PlaytestDemoReadingsDto
    {
        private const int ChannelCount = (int)GameCorePresentationConstants.ChannelCount;
        private const int NodeCount = ChannelCount * (int)GameCorePresentationConstants.BundlePositionCount;

        public PlaytestDemoChannelReadingsDto[] Channels { get; } = new PlaytestDemoChannelReadingsDto[ChannelCount];
        public double[] BundleBurnupMwdPerKg { get; } = new double[NodeCount];
        public double[] BundlePowerWatts { get; } = new double[NodeCount];
        public double[] BundleLocalPowerFractions { get; } = new double[NodeCount];
        public double[] Group1Flux { get; } = new double[NodeCount];
        public double[] Group2Flux { get; } = new double[NodeCount];
        public ulong[] BundleStateVersions { get; } = new ulong[NodeCount];
        public bool[] BundleIsFresh { get; } = new bool[NodeCount];
        public List<PlaytestDemoBundleIdentityDto> BundleIdentities { get; } = new List<PlaytestDemoBundleIdentityDto>();
    }

    internal sealed class PlaytestDemoChannelReadingsDto
    {
        public double AverageBurnupMwdPerKg { get; set; }
        public double PowerWatts { get; set; }
        public double LocalPowerFraction { get; set; }
        public double LocalTiltFraction { get; set; }
        public PlaytestXenonChannelDto Xenon { get; set; } = new PlaytestXenonChannelDto();
    }

    internal sealed class PlaytestDemoBundleIdentityDto
    {
        public int Index { get; set; }
        public string BundleId { get; set; } = string.Empty;
        public string FuelTypeId { get; set; } = string.Empty;
        public double InsertedAtSeconds { get; set; }
    }

    public sealed partial class PlaytestRuntime
    {
        private static PlaytestDemoReadingsDto CreateDemoReadings(
            BridgeRuntime runtime,
            GameSessionSnapshot game,
            GameSessionSnapshot previous)
        {
            var readings = new PlaytestDemoReadingsDto();
            foreach (var channel in game.Core.Channels)
            {
                int channelIndex = (int)channel.ChannelIndex;
                readings.Channels[channelIndex] = new PlaytestDemoChannelReadingsDto
                {
                    AverageBurnupMwdPerKg = channel.AverageBurnupMwDayPerKg,
                    PowerWatts = channel.PowerWatts,
                    LocalPowerFraction = channel.LocalPowerFraction,
                    LocalTiltFraction = channel.LocalTiltFraction,
                    Xenon = CreateChannelXenon(channel)
                };
                var previousBundles = previous.Core.Channels[channelIndex].Bundles;
                foreach (var bundle in channel.Bundles)
                {
                    int index = checked(channelIndex * (int)GameCorePresentationConstants.BundlePositionCount + (int)bundle.Position);
                    readings.BundleBurnupMwdPerKg[index] = bundle.CurrentBurnupMwDayPerKg;
                    readings.BundlePowerWatts[index] = bundle.PowerWatts;
                    readings.BundleLocalPowerFractions[index] = BundlePowerFraction(bundle.PowerWatts, game.Physics.MeanBundlePowerWatts);
                    readings.Group1Flux[index] = runtime.PlaySession.GetCellGroup1Flux(channel.ChannelIndex, bundle.Position);
                    readings.Group2Flux[index] = runtime.PlaySession.GetCellGroup2Flux(channel.ChannelIndex, bundle.Position);
                    readings.BundleStateVersions[index] = bundle.StateVersion;
                    readings.BundleIsFresh[index] = bundle.IsFresh;

                    var previousBundle = previousBundles[(int)bundle.Position];
                    if (bundle.BundleId != previousBundle.BundleId || bundle.FuelTypeId != previousBundle.FuelTypeId ||
                        bundle.InsertedAtSeconds != previousBundle.InsertedAtSeconds)
                    {
                        readings.BundleIdentities.Add(new PlaytestDemoBundleIdentityDto
                        {
                            Index = index,
                            BundleId = bundle.BundleId,
                            FuelTypeId = bundle.FuelTypeId,
                            InsertedAtSeconds = bundle.InsertedAtSeconds
                        });
                    }
                }
            }
            return readings;
        }
    }
}
