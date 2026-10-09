using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using ReactorSim.Core;

namespace ReactorSim.Game
{
    public sealed class GameLiquidZoneTubePresentationSnapshot
    {
        private GameLiquidZoneTubePresentationSnapshot(PracticeLiquidZoneTubeV1 tube, PracticeLiquidZoneRrsMappingV1 mapping)
        {
            ZoneId = tube.ZoneId;
            GridColumn = 10.5 + tube.HorizontalCentreM / PracticeAdjustersV1.LatticePitchM;
            GridRowStart = 10.5 - tube.VerticalMaximumM / PracticeAdjustersV1.LatticePitchM;
            GridRowEnd = 10.5 - tube.VerticalMinimumM / PracticeAdjustersV1.LatticePitchM;
            AxialPosition = tube.AxialCentreM / PracticeAdjustersV1.BundleLengthM - .5;
            var cells = mapping.Nodes.Where(n => n.WaterColumn != null && n.AbsorberZoneId == ZoneId).ToArray();
            AffectedChannels = Array.AsReadOnly(cells.Select(n => n.Node.ChannelId.Value).Distinct().OrderBy(c => c).ToArray());
            BundlePositions = Array.AsReadOnly(cells.Select(n => n.Node.Position.Value).Distinct().OrderBy(p => p).ToArray());
        }
        public uint ZoneId { get; }
        public double GridColumn { get; }
        public double GridRowStart { get; }
        public double GridRowEnd { get; }
        public double AxialPosition { get; }
        public IReadOnlyList<uint> AffectedChannels { get; }
        public IReadOnlyList<uint> BundlePositions { get; }
        private static readonly ConditionalWeakTable<PracticeLiquidZoneRrsMappingV1, Layout> Layouts =
            new ConditionalWeakTable<PracticeLiquidZoneRrsMappingV1, Layout>();
        internal static IReadOnlyList<GameLiquidZoneTubePresentationSnapshot> For(PracticeLiquidZoneRrsMappingV1 mapping) =>
            Layouts.GetValue(mapping, m => new Layout(m)).Tubes;
        private sealed class Layout
        {
            internal Layout(PracticeLiquidZoneRrsMappingV1 mapping)
            {
                Tubes = Array.AsReadOnly(PracticeLiquidZoneTubesV1.Compartments
                    .Select(t => new GameLiquidZoneTubePresentationSnapshot(t, mapping))
                    .Where(t => t.AffectedChannels.Count > 0).ToArray());
            }
            internal IReadOnlyList<GameLiquidZoneTubePresentationSnapshot> Tubes { get; }
        }
    }
}
