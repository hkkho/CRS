using System.Collections.Generic;
using System.Linq;
using ReactorSim.Core;

namespace ReactorSim.Game
{
    public sealed class RefuellingMovementBundle
    {
        internal RefuellingMovementBundle(BundleState bundle, uint? before, uint? after)
        {
            BundleId = bundle.BundleId.ToString(); BeforePosition = before; AfterPosition = after;
            BurnupMwdPerKg = bundle.CurrentBurnupJPerKgHm / 8.64e10;
        }
        public string BundleId { get; }
        public uint? BeforePosition { get; }
        public uint? AfterPosition { get; }
        public double BurnupMwdPerKg { get; }
    }
    public sealed class RefuellingMovement
    {
        internal RefuellingMovement(SyntheticGameCoreStateV1 before, GameRefuellingResultV1 result, RefuellingScoreBreakdown score)
        {
            OperationId = result.ResultingState.RefuellingOperationCount;
            ChannelIndex = result.ChannelIndex;
            Plan = new GameRefuellingPlanV1(result.Direction, result.ShiftCount);
            Score = score;
            var next = result.ResultingState.GetChannel(result.ChannelIndex).ToDictionary(b => b.BundleId);
            Bundles = System.Array.AsReadOnly(before.GetChannel(result.ChannelIndex)
                .Select(b => new RefuellingMovementBundle(b, b.Position.Value, next.TryGetValue(b.BundleId, out var moved) ? (uint?)moved.Position.Value : null))
                .Concat(result.InsertedBundles.Select(b => new RefuellingMovementBundle(b, null, b.Position.Value))).ToArray());
        }
        public uint OperationId { get; }
        public uint ChannelIndex { get; }
        public GameRefuellingPlanV1 Plan { get; }
        public RefuellingScoreBreakdown Score { get; }
        public IReadOnlyList<RefuellingMovementBundle> Bundles { get; }
    }
}
