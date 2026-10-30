using System;

namespace ReactorSim.Core
{
    /// <summary>Authored effective reflector: zero flux on a concentric X-Y circle.
    /// Each exposed Cartesian face uses its ray's intersection with that circle.</summary>
    public sealed class CircularRadialBoundary
    {
        internal CircularRadialBoundary(double pitchM, double coreRadiusM, double extrapolationDistanceM)
        {
            PitchM = pitchM;
            CoreRadiusM = coreRadiusM;
            ExtrapolationDistanceM = extrapolationDistanceM;
        }

        public const string ConditionId = "circular-extrapolated-zero-flux-v1";
        public double PitchM { get; }
        public double CoreRadiusM { get; }
        public double ExtrapolationDistanceM { get; }
        public double ExtrapolatedRadiusM => CoreRadiusM + ExtrapolationDistanceM;

        internal double DistanceM(ChannelTopology channel, TopologyFace face)
        {
            // Canonical 22 x 22 lattice is centred between coordinates 10 and 11.
            double x = (channel.CoordinateX - 10.5) * PitchM;
            double y = (channel.CoordinateY - 10.5) * PitchM;
            double r = ExtrapolatedRadiusM;
            switch (face)
            {
                case TopologyFace.East: return Math.Sqrt(r * r - y * y) - x;
                case TopologyFace.West: return Math.Sqrt(r * r - y * y) + x;
                case TopologyFace.North: return Math.Sqrt(r * r - x * x) - y;
                case TopologyFace.South: return Math.Sqrt(r * r - x * x) + y;
                default: throw new ArgumentOutOfRangeException(nameof(face));
            }
        }

        internal TwoGroupConductanceV1 Conductance(ChannelTopology channel, TopologyFace face,
            TwoGroupConductanceV1 interior)
        {
            // C_interior = DA/pitch. Circle distance includes the half-cell;
            // do not add it a second time or apply the axial Marshak condition.
            double scale = PitchM / DistanceM(channel, face);
            return new TwoGroupConductanceV1(interior.Group1M2 * scale, interior.Group2M2 * scale);
        }
    }
}
