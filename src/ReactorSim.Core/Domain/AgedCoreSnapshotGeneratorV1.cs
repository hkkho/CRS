using System;
using System.Linq;

namespace ReactorSim.Core
{
    /// <summary>
    /// Project-authored RFSP-inspired instantaneous eight-bundle-shift surrogate.
    /// Uses prescribed axial exposure, not a converged RFSP time-average history.
    /// </summary>
    public static class AgedCoreSnapshotGeneratorV1
    {
        public const string ModelId = "patterned-channel-age-eight-shift-with-flow-cycle190-v3";
        // Calibrated to this game's surrogate pack, not a natural-U plant target.
        public const double ChannelRefuellingIntervalFullPowerDays = 190.0;
        public const double MeanBundleResidenceFullPowerDays = 1.5 * ChannelRefuellingIntervalFullPowerDays;
        public const double TargetDischargeBurnupMwDayPerKg = 6.2621359223300965;
        private static readonly double[] Increments = CreateIncrements();

        public static double BundleBurnupMwDayPerKg(int positionFromInlet, double age,
            double targetDischargeBurnupMwDayPerKg = TargetDischargeBurnupMwDayPerKg)
        {
            if (positionFromInlet < 0 || positionFromInlet >= 12)
                throw new ArgumentOutOfRangeException(nameof(positionFromInlet));
            if (double.IsNaN(age) || double.IsInfinity(age) || age < 0 || age > 1)
                throw new ArgumentOutOfRangeException(nameof(age));
            if (double.IsNaN(targetDischargeBurnupMwDayPerKg) || double.IsInfinity(targetDischargeBurnupMwDayPerKg) || targetDischargeBurnupMwDayPerKg <= 0)
                throw new ArgumentOutOfRangeException(nameof(targetDischargeBurnupMwDayPerKg));
            // Positions 9-12 retain the former positions 1-4 at their EOC exposure.
            double beginning = positionFromInlet < 8 ? 0 : Increments[positionFromInlet - 8];
            return (beginning + age * Increments[positionFromInlet]) *
                (targetDischargeBurnupMwDayPerKg / TargetDischargeBurnupMwDayPerKg);
        }

        public static double[] CreateChannelAges(ulong seed)
        {
            var ages = new double[SyntheticGameCoreStateV1.ChannelCount];
            var mapping = PracticeLiquidZoneRrsMappingV1.TryCreateCandu6();
            if (!mapping.IsValid) throw new InvalidOperationException(mapping.FirstDiagnostic.ToString());
            // Stratify within each transverse RRS region, then separate young/old
            // ranks by checkerboard parity to suppress adjacent fresh clusters.
            ulong random = seed;
            for (uint region = 0; region < 7; region++)
            {
                uint[] channels = mapping.Value.Nodes
                    .Where(node => node.Node.Position.Value == 0 && node.LogicalZoneId % 7 == region)
                    .Select(node => node.Node.ChannelId.Value).OrderBy(channel => channel).ToArray();
                uint[][] groups = channels.GroupBy(channel =>
                {
                    var grid = Candu6CoreTopologyFactoryV1.GetPosition(channel);
                    return (grid.Column + grid.CartesianY) & 1;
                }).OrderBy(group => group.Key).Select(group => group.ToArray()).ToArray();
                if ((Next(ref random) & 1) != 0) Array.Reverse(groups);
                int rank = 0;
                foreach (uint[] group in groups)
                {
                    for (int i = group.Length - 1; i > 0; i--)
                    {
                        int j = (int)(Next(ref random) % (ulong)(i + 1));
                        uint swap = group[i]; group[i] = group[j]; group[j] = swap;
                    }
                    foreach (uint channel in group) ages[channel] = (rank++ + 0.5) / channels.Length;
                }
            }
            return ages;
        }

        private static double[] CreateIncrements()
        {
            var weights = new double[12];
            for (int k = 0; k < 12; k++) weights[k] = 0.55 + 0.45 * Math.Sin(Math.PI * (k + 0.5) / 12.0);
            // Sum(delta B)/8 equals the mean burnup of the eight discharged
            // bundles at age=1, including retained fuel's previous dwell.
            double scale = 8 * TargetDischargeBurnupMwDayPerKg / weights.Sum();
            for (int k = 0; k < 12; k++) weights[k] *= scale;
            return weights;
        }

        private static ulong Next(ref ulong state)
        {
            // SplitMix64: specified integer arithmetic is stable across .NET/WASM.
            unchecked
            {
                state += 0x9e3779b97f4a7c15UL;
                ulong z = state;
                z = (z ^ (z >> 30)) * 0xbf58476d1ce4e5b9UL;
                z = (z ^ (z >> 27)) * 0x94d049bb133111ebUL;
                return z ^ (z >> 31);
            }
        }
    }
}
