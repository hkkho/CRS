using System;

namespace ReactorSim.Game
{
    /// <summary>Gameplay limits, independent of physical controller exhaustion.</summary>
    internal static class PracticeOperatingLimits
    {
        internal static string EndReason(double averageLevel, double globalTilt)
        {
            // Summing fourteen levels can put an exact boundary a few ulps outside it.
            const double levelTolerance = 1e-12;
            if (averageLevel < 0.10 - levelTolerance) return "LZC average level below 10%";
            if (averageLevel > 0.90 + levelTolerance) return "LZC average level above 90%";
            if (Math.Abs(globalTilt) > 0.20) return "Global tilt exceeds 20%";
            return string.Empty;
        }
    }
}
