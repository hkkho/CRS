using System;
using System.Collections.Generic;
using System.Linq;
using ReactorSim.Core;

namespace ReactorSim.Game
{
    /// <summary>Immutable display geometry derived from Core's active device map.
    /// Grid coordinates use channel centres; axial position is zero-based.</summary>
    public sealed class GameAdjusterPresentationSnapshot
    {
        internal GameAdjusterPresentationSnapshot(PracticeAdjusterRodV1 rod)
        {
            Id = rod.Id;
            GridColumn = 10.5 + rod.HorizontalCentreM / PracticeAdjustersV1.LatticePitchM;
            GridRowStart = 10.5 - PracticeAdjustersV1.VerticalMaximumM / PracticeAdjustersV1.LatticePitchM;
            GridRowEnd = 10.5 - PracticeAdjustersV1.VerticalMinimumM / PracticeAdjustersV1.LatticePitchM;
            AxialPosition = rod.AxialCentreM / PracticeAdjustersV1.BundleLengthM - 0.5;
            var cells = PracticeAdjustersV1.Cells.Where(c => c.RodId == rod.Id).ToArray();
            AffectedChannels = Array.AsReadOnly(cells.Select(c => c.Node.ChannelId.Value).Distinct().OrderBy(c => c).ToArray());
            BundlePositions = Array.AsReadOnly(cells.Select(c => c.Node.Position.Value).Distinct().OrderBy(p => p).ToArray());
        }
        public int Id { get; }
        public double GridColumn { get; }
        public double GridRowStart { get; }
        public double GridRowEnd { get; }
        public double AxialPosition { get; }
        public IReadOnlyList<uint> AffectedChannels { get; }
        public IReadOnlyList<uint> BundlePositions { get; }
        internal static IReadOnlyList<GameAdjusterPresentationSnapshot> Nominal { get; } =
            Array.AsReadOnly(PracticeAdjustersV1.Rods.Select(r => new GameAdjusterPresentationSnapshot(r)).ToArray());
    }
}
