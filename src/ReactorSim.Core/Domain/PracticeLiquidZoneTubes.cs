using System;
using System.Collections.Generic;
using System.Linq;

namespace ReactorSim.Core
{
    /// <summary>Six device-centred homogenized assemblies, fourteen compartments.
    /// St-Aubin/Marleau 2018 Figs. 1-2: two axial planes, three transverse columns.
    /// Vertical endpoints rounded to lattice boundaries are authored approximations.</summary>
    public sealed class PracticeLiquidZoneTubeV1
    {
        internal PracticeLiquidZoneTubeV1(uint zone, double xPitch, double bottomPitch, double topPitch, double zBundles)
        {
            ZoneId = zone;
            HorizontalCentreM = xPitch * PracticeAdjustersV1.LatticePitchM;
            VerticalMinimumM = bottomPitch * PracticeAdjustersV1.LatticePitchM;
            VerticalMaximumM = topPitch * PracticeAdjustersV1.LatticePitchM;
            AxialCentreM = zBundles * PracticeAdjustersV1.BundleLengthM;
        }
        public uint ZoneId { get; }
        public double HorizontalCentreM { get; }
        public double VerticalMinimumM { get; }
        public double VerticalMaximumM { get; }
        public double AxialCentreM { get; }
    }

    /// <summary>Water surface within one homogenized tube compartment and fuel cell.</summary>
    public sealed class PracticeLiquidZoneWaterColumnV1
    {
        internal PracticeLiquidZoneWaterColumnV1(double bottom, double top, double cellBottom, double cellTop)
        { BottomM = bottom; TopM = top; CellBottomM = cellBottom; CellTopM = cellTop; }
        public double BottomM { get; }
        public double TopM { get; }
        public double CellBottomM { get; }
        public double CellTopM { get; }
        public double FilledOverlapFraction(double fill) => PracticeLiquidZoneTubesV1.Overlap(
            CellBottomM, CellTopM, BottomM, BottomM + fill * (TopM - BottomM)) /
            PracticeLiquidZoneTubesV1.Overlap(CellBottomM, CellTopM, BottomM, TopM);
    }

    public static class PracticeLiquidZoneTubesV1
    {
        public const string LayoutId = "candu6-six-tubes-fourteen-compartments-moving-water-v1";
        public static IReadOnlyList<PracticeLiquidZoneTubeV1> Compartments { get; } = Array.AsReadOnly(
            new[] { 3.0, 9.0 }.SelectMany((z, half) => new[]
            {
                new PracticeLiquidZoneTubeV1((uint)(7 * half), -6, -7, 1, z),
                new PracticeLiquidZoneTubeV1((uint)(7 * half + 1), -6, 1, 9, z),
                new PracticeLiquidZoneTubeV1((uint)(7 * half + 2), 0, -10, -4, z),
                new PracticeLiquidZoneTubeV1((uint)(7 * half + 3), 0, -4, 4, z),
                new PracticeLiquidZoneTubeV1((uint)(7 * half + 4), 0, 4, 11, z),
                new PracticeLiquidZoneTubeV1((uint)(7 * half + 5), 6, -7, 1, z),
                new PracticeLiquidZoneTubeV1((uint)(7 * half + 6), 6, 1, 9, z)
            }).ToArray());

        internal static double Overlap(double a0, double a1, double b0, double b1) =>
            Math.Max(0, Math.Min(a1, b1) - Math.Max(a0, b0));

        internal static (PracticeLiquidZoneTubeV1? Tube, double Fraction, PracticeLiquidZoneWaterColumnV1? Water) Binding(NodeKey node)
        {
            double pitch = PracticeAdjustersV1.LatticePitchM, length = PracticeAdjustersV1.BundleLengthM;
            var p = Candu6CoreTopologyFactoryV1.GetPosition(node.ChannelId.Value);
            double x = (p.Column - 10.5) * pitch, y = (10.5 - p.DisplayRow) * pitch;
            foreach (var tube in Compartments)
            {
                double transverse = Overlap(x - pitch / 2, x + pitch / 2,
                    tube.HorizontalCentreM - pitch / 2, tube.HorizontalCentreM + pitch / 2) / pitch;
                double vertical = Overlap(y - pitch / 2, y + pitch / 2, tube.VerticalMinimumM, tube.VerticalMaximumM) / pitch;
                double axial = Overlap(node.Position.Value * length, (node.Position.Value + 1) * length,
                    tube.AxialCentreM - length / 2, tube.AxialCentreM + length / 2) / length;
                double fraction = transverse * vertical * axial;
                if (fraction > 1e-12) return (tube, fraction,
                    new PracticeLiquidZoneWaterColumnV1(tube.VerticalMinimumM, tube.VerticalMaximumM, y - pitch / 2, y + pitch / 2));
            }
            return (null, 0, null);
        }
    }
}
