using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ReactorSim.Core
{
    /// <summary>Fixed, fully inserted interstitial adjusters. No fuel is displaced.
    /// IAEA-TECDOC-1994 pp. 20 and Table 10 supply positions and steel segments.
    /// Incremental absorption strengths are authored pack calibration values.</summary>
    public sealed class PracticeAdjusterRodV1
    {
        internal PracticeAdjusterRodV1(int id, double x, double z)
        { Id = id; HorizontalCentreM = x; AxialCentreM = z; }
        public int Id { get; }
        public double HorizontalCentreM { get; }
        public double AxialCentreM { get; }
    }

    public sealed class PracticeAdjusterCellV1
    {
        internal PracticeAdjusterCellV1(int rodId, NodeKey node, double fraction, double weight)
        { RodId = rodId; Node = node; VolumeFraction = fraction; AbsorptionWeight = weight; }
        public int RodId { get; }
        public NodeKey Node { get; }
        public double VolumeFraction { get; }
        public double AbsorptionWeight { get; }
    }

    public static class PracticeAdjustersV1
    {
        public const string LayoutId = "candu6-21-adjusters-iaea1994-overlap-v1";
        public const double LatticePitchM = 0.28575;
        public const double BundleLengthM = 0.4953;
        public const double VerticalMinimumM = -6 * LatticePitchM;
        public const double VerticalMaximumM = 6 * LatticePitchM;
        // Steel cross-sectional area ratio, Table 10: solid shim plus steel tube.
        // An absorption proxy, not a transport-generated incremental cross section.
        public static readonly double OuterToInnerSteelAreaRatio =
            (0.710 * 0.710 + 3.690 * 3.690 - 3.607 * 3.607) /
            (0.650 * 0.650 + 3.725 * 3.725 - 3.607 * 3.607);
        public static IReadOnlyList<PracticeAdjusterRodV1> Rods { get; } =
            Array.AsReadOnly(new[] { 4.5, 6.0, 7.5 }.SelectMany((z, plane) =>
                Enumerable.Range(0, 7).Select(column => new PracticeAdjusterRodV1(
                    plane * 7 + column + 1, (column - 3) * 2 * LatticePitchM,
                    z * BundleLengthM))).ToArray());
        public static IReadOnlyList<PracticeAdjusterCellV1> Cells { get; } = BuildCells();

        private static ReadOnlyCollection<PracticeAdjusterCellV1> BuildCells()
        {
            var result = new List<PracticeAdjusterCellV1>();
            foreach (var rod in Rods)
                for (uint channel = 0; channel < Candu6CoreTopologyFactoryV1.ChannelCount; channel++)
                {
                    var position = Candu6CoreTopologyFactoryV1.GetPosition(channel);
                    double x = (position.Column - 10.5) * LatticePitchM;
                    double y = (10.5 - position.DisplayRow) * LatticePitchM;
                    double xFraction = Overlap(x - LatticePitchM / 2, x + LatticePitchM / 2,
                        rod.HorizontalCentreM - LatticePitchM / 2, rod.HorizontalCentreM + LatticePitchM / 2) / LatticePitchM;
                    double inner = Overlap(y - LatticePitchM / 2, y + LatticePitchM / 2,
                        -3 * LatticePitchM, 3 * LatticePitchM) / LatticePitchM;
                    double total = Overlap(y - LatticePitchM / 2, y + LatticePitchM / 2,
                        VerticalMinimumM, VerticalMaximumM) / LatticePitchM;
                    if (xFraction < 1e-12 || total < 1e-12) continue;
                    for (uint axial = 0; axial < Candu6CoreTopologyFactoryV1.BundlePositionCount; axial++)
                    {
                        double zFraction = Overlap(axial * BundleLengthM, (axial + 1) * BundleLengthM,
                            rod.AxialCentreM - BundleLengthM / 2, rod.AxialCentreM + BundleLengthM / 2) / BundleLengthM;
                        if (zFraction < 1e-12) continue;
                        result.Add(new PracticeAdjusterCellV1(rod.Id,
                            new NodeKey(new ChannelId(channel), new BundlePosition(axial)),
                            xFraction * total * zFraction,
                            xFraction * (inner + (total - inner) * OuterToInnerSteelAreaRatio) * zFraction));
                    }
                }
            return new ReadOnlyCollection<PracticeAdjusterCellV1>(result);
        }

        private static double Overlap(double a0, double a1, double b0, double b1) =>
            Math.Max(0, Math.Min(a1, b1) - Math.Max(a0, b0));

        public static StaticAbsorptionOverlayV1 CreateOverlay(double group1PerM, double group2PerM)
        {
            if (!ContractValidation.IsFinite(group1PerM) || !ContractValidation.IsFinite(group2PerM) ||
                group1PerM < 0 || group2PerM < 0)
                throw new ArgumentOutOfRangeException(nameof(group1PerM));
            var result = StaticAbsorptionOverlayV1.TryCreate(LayoutId,
                Cells.GroupBy(c => c.Node).Select(g => new StaticAbsorptionOverlayEntryV1(g.Key,
                    group1PerM * g.Sum(c => c.AbsorptionWeight), group2PerM * g.Sum(c => c.AbsorptionWeight))));
            if (!result.IsValid) throw new InvalidOperationException(result.FirstDiagnostic.ToString());
            return result.Value;
        }
    }
}
