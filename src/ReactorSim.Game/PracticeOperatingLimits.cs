using System;

namespace ReactorSim.Game
{
    /// <summary>Gameplay limits, independent of physical controller exhaustion.</summary>
    internal static class PracticeOperatingLimits
    {
        internal const double MaximumChannelPowerWatts = 7_300_000;
        internal const double MaximumBundlePowerWatts = 935_000;

        internal static string EndReason(double averageLevel, double globalTilt,
            double maximumChannelPowerWatts = 0, double maximumBundlePowerWatts = 0)
        {
            // Summing fourteen levels can put an exact boundary a few ulps outside it.
            const double levelTolerance = 1e-12;
            if (averageLevel < 0.10 - levelTolerance) return "LZC average level below 10%";
            if (averageLevel > 0.90 + levelTolerance) return "LZC average level above 90%";
            if (Math.Abs(globalTilt) > 0.20) return "Global tilt exceeds 20%";
            if (maximumChannelPowerWatts > MaximumChannelPowerWatts)
                return "Channel power exceeds 7,300 kW";
            if (maximumBundlePowerWatts > MaximumBundlePowerWatts)
                return "Bundle power exceeds 935 kW";
            return string.Empty;
        }
    }
}
