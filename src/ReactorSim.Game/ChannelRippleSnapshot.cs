using System;
using System.Collections.Generic;
using System.Linq;
using ReactorSim.Core;

namespace ReactorSim.Game
{
    public sealed class ChannelRippleSnapshot
    {
        internal ChannelRippleSnapshot(PracticeChannelPowerReference reference, IReadOnlyList<double> powers, double amplitude, double axialTiltFraction)
        {
            ReferenceId = PracticeChannelPowerReference.ModelId;
            DataPackVersion = reference.DataPackVersion;
            CoefficientBindingDigestHex = string.Concat(reference.CoefficientBindingDigest.Bytes.Select(b => b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture)));
            ReferenceThermalPowerWatts = reference.ThermalPowerWatts;
            ReferenceChannelPowerWatts = reference.ChannelPowerWatts;
            ChannelRippleFractions = Array.AsReadOnly(powers.Select((p, i) => p * amplitude / reference.ChannelPowerWatts[i]).ToArray());
            RmsDeviationFraction = PracticeScoring.RmsRipple(powers, reference.ChannelPowerWatts, amplitude);
            MaximumRipple = ChannelRippleFractions.Max();
            RipplePointsPerHour = PracticeScoring.RipplePointsPerHour(MaximumRipple);
            TiltPointsPerHour = PracticeScoring.TiltPointsPerHour(axialTiltFraction);
        }
        public string ReferenceId { get; }
        public string DataPackVersion { get; }
        public string CoefficientBindingDigestHex { get; }
        public double ReferenceThermalPowerWatts { get; }
        public double MaximumChannelPowerWatts { get; } = PracticeOperatingLimits.MaximumChannelPowerWatts;
        public double MaximumBundlePowerWatts { get; } = PracticeOperatingLimits.MaximumBundlePowerWatts;
        public IReadOnlyList<double> ReferenceChannelPowerWatts { get; }
        public IReadOnlyList<double> ChannelRippleFractions { get; }
        public double RmsDeviationFraction { get; }
        public double MaximumRipple { get; }
        public double RipplePointsPerHour { get; }
        public double TiltPointsPerHour { get; }
        public double PointsPerHour => RipplePointsPerHour + TiltPointsPerHour;
    }
}
