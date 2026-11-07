using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ReactorSim.Core;
using ReactorSim.Game;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class PracticeRrsFluxAuditTests(ITestOutputHelper output)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly ulong[] AuditSeeds = { 1001, 1002, 1013 };
    private static readonly int[] AuditHours = { 0, 14 };
    private static readonly ulong[] RegressionSeeds = { 1001 };
    private static readonly int[] RegressionHours = { 0 };
    private static readonly uint[][] AuditOrders = { new uint[] { 75 }, new uint[] { 324 } };
    private static readonly uint[][] RegressionOrders =
        { new uint[] { 75 }, new uint[] { 324 }, new uint[] { 210, 229 } };
    [Fact]
    public void PausedOldChannelRefuellingRunsImmediateBoundedRrsWithReboundPoisonInBothFlows()
    {
        var rows = new List<object>();
        double[]? nominalFlux = null;
        // Keep ordinary regression runs focused; opt in to the longer matrix
        // when reproducing the quantitative audit documented in docs/physics.
        bool fullAudit = Environment.GetEnvironmentVariable("CANDU_RRS_FLUX_AUDIT_MATRIX") == "1";
        var cases = from seed in fullAudit ? AuditSeeds : RegressionSeeds
                    from hour in fullAudit ? AuditHours : RegressionHours
                    from channels in fullAudit ? AuditOrders : RegressionOrders
                    select (seed, hour, channels);
        foreach (var (seed, hour, channels) in cases)
        {
            var session = PracticeGameSessionFactory.CreateBrowserPlaytest(seed, dailyTurns: false);
            if (hour > 0)
            {
                var advance = session.AdvanceWallMilliseconds((ulong)(hour * 2000));
                Assert.True(advance.Accepted, advance.DiagnosticMessage);
                Assert.Equal(hour * 3600.0, session.Snapshot.SimulationTimeSeconds);
            }
            Assert.True(session.Pause().Accepted);
            if (channels.Length > 1)
            {
                Assert.All(channels, channel => Assert.Equal(5U, session.CurrentLiquidZoneRrs.Mapping
                    .GetLogicalZoneId(new NodeKey(new ChannelId(channel), new BundlePosition(0)))));
            }
            foreach (uint channel in channels)
            {
                rows.Add(AuditRefuelling(session, seed, hour, channel, fullAudit, ref nominalFlux));
            }
        }
        output.WriteLine(JsonSerializer.Serialize(rows, JsonOptions));
    }

    private static object AuditRefuelling(GameSession session, ulong seed, int hour, uint channel,
        bool fullAudit, ref double[]? nominalFlux)
    {
        var beforeSnapshot = session.Snapshot;
        var beforeCore = session.CoreState;
        var before = session.CurrentLiquidZoneRrs;
        var beforeProjection = session.CurrentEquilibriumProjection;
        var model = Require(FullCoreDiffusionModelV1.TryCreateCandu6(beforeProjection.SpatialSolve.DataPack));
        nominalFlux ??= ZoneMeans(before.Mapping, Require(model.TrySolvePracticeTimeAverage(
            PracticeGameSessionFactory.PracticeReferencePowerWatts)).Group2Flux);
        var solver = Require(EquilibriumCoreSolverV1.TryCreate(model, beforeCore.EnumerateBundles(),
            PracticeGameSessionFactory.PracticeReferencePowerWatts));
        var flow = Candu6CoreTopologyFactoryV1.GetFlowDirection(Candu6CoreTopologyFactoryV1.GetPosition(channel));
        string direction = flow == FlowDirection.EndAtoEndB ? "toward-end-b" : "toward-end-a";
        var result = session.RefuelChannel(channel, direction, 8, "NAT-U-SYNTHETIC");
        Assert.True(result.Accepted, result.DiagnosticMessage);
        var after = session.CurrentLiquidZoneRrs;

        // Same inventory, rebound poison, and pre-refuelling water fills:
        // isolates the immediate control action from the refuelling perturbation.
        var prepared = Require(solver.TryPrepareCandidates(session.CoreState.EnumerateBundles(),
            session.CurrentXenonState.BuildOverlay()));
        var frozen = Require(solver.TrySolveCandidate(prepared, beforeProjection.SpatialSolve,
            before.AbsorptionOverlay));
        double[] command = after.ZoneFills.Zip(before.ZoneFills, (a, b) => a - b).ToArray();
        Assert.Equal(beforeSnapshot.SimulationTimeSeconds, result.Snapshot.SimulationTimeSeconds);
        Assert.Equal(beforeSnapshot.SimulationTimeSeconds, after.SimulationTimeSeconds);
        Assert.Equal(beforeSnapshot.ScoreTotal + result.Snapshot.LastRefuellingScore!.NetPoints,
            result.Snapshot.ScoreTotal);
        Assert.Equal(beforeSnapshot.Shift.ThermalEnergyMwh, result.Snapshot.Shift.ThermalEnergyMwh);
        Assert.True(result.Snapshot.IsPaused);
        Assert.Contains(command, movement => Math.Abs(movement) > 1e-8);
        Assert.All(command, movement => Assert.InRange(Math.Abs(movement), 0,
            PracticeLiquidZoneRrsIdentityV1.MaxFillMovementPerEvent + 1e-12));
        Assert.InRange(after.CandidateSolveCount, 2, PracticeLiquidZoneRrsIdentityV1.MaximumCandidateSolveCount);
        Assert.Equal(before.TargetZonalPowerFractions, after.TargetZonalPowerFractions);
        double frozenPower = frozen.ShapeNodePowerWatts.Sum();
        double frozenShapeError = before.Mapping.NodeIndicesByZone.Select((nodes, zone) =>
            Math.Abs(before.TargetZonalPowerFractions[zone] - nodes.Sum(i => frozen.ShapeNodePowerWatts[i]) / frozenPower))
            .Max();
        double[] controlledShares = ZonePowerShares(after.Mapping, session.CurrentEquilibriumProjection.ShapeNodePowerWatts);
        double[] controlledShapeErrors = before.TargetZonalPowerFractions.Zip(controlledShares,
            (target, measured) => target - measured).ToArray();
        Assert.Equal(PracticeLiquidZoneRrsIdentityV1.LogicalZoneCount, (uint)controlledShares.Length);
        Assert.InRange(controlledShapeErrors.Zip(after.ZonalShapeErrors,
                (measured, reported) => Math.Abs(measured - reported)).Max(),
            0, 1e-12);
        double controlledShapeError = controlledShapeErrors.Max(Math.Abs);
        Assert.All(after.ZoneFills, fill => Assert.InRange(fill, 0, 1));
        Assert.Equal(Math.Abs(after.CompensatedNetReactivity) <= PracticeLiquidZoneRrsIdentityV1.CriticalityTolerance &&
            controlledShapeError <= PracticeLiquidZoneRrsIdentityV1.ControllerTolerance,
            after.ControllerConverged);
        if (!fullAudit)
        {
            Assert.True(Math.Abs(frozen.RelativeReactivity) > PracticeLiquidZoneRrsIdentityV1.CriticalityTolerance);
            Assert.InRange(Math.Abs(after.CompensatedNetReactivity), 0, PracticeLiquidZoneRrsIdentityV1.CriticalityTolerance);
            Assert.True(controlledShapeError < frozenShapeError);
            Assert.InRange(controlledShapeError, 0, PracticeLiquidZoneRrsIdentityV1.ControllerTolerance);
            Assert.True(after.ControllerConverged);
            Assert.True(command.Max() - command.Min() > 1e-6);
        }
        return new
        {
            seed,
            hour,
            channel,
            direction,
            averageFillBefore = before.AverageFillFraction,
            averageFillAfter = after.AverageFillFraction,
            commonFillMovement = command.Average(),
            differentialFillMovementRange = command.Max() - command.Min(),
            command,
            frozenReactivityMk = frozen.RelativeReactivity * 1000,
            controlledReactivityMk = after.CompensatedNetReactivity * 1000,
            frozenShapeError,
            controlledShapeError,
            beforeFluxRippleRms = FluxRippleRms(ZoneMeans(before.Mapping, beforeProjection.ShapeGroup2), nominalFlux),
            frozenFluxRippleRms = FluxRippleRms(ZoneMeans(before.Mapping, frozen.ShapeGroup2), nominalFlux),
            controlledFluxRippleRms = FluxRippleRms(ZoneMeans(before.Mapping,
                session.CurrentEquilibriumProjection.ShapeGroup2), nominalFlux),
            candidateSolveCount = after.CandidateSolveCount,
            decisionCode = after.DecisionCode,
            controllerConverged = after.ControllerConverged
        };
    }

    private static double[] ZoneMeans(PracticeLiquidZoneRrsMappingV1 mapping, IReadOnlyList<double> flux) =>
        mapping.NodeIndicesByZone.Select(indices => indices.Average(i => flux[i])).ToArray();

    private static double[] ZonePowerShares(PracticeLiquidZoneRrsMappingV1 mapping, IReadOnlyList<double> power)
    {
        double total = power.Sum();
        return mapping.NodeIndicesByZone.Select(indices => indices.Sum(i => power[i]) / total).ToArray();
    }

    private static double FluxRippleRms(IReadOnlyList<double> measured, IReadOnlyList<double> nominal)
    {
        double[] ratios = measured.Zip(nominal, (a, b) => a / b).ToArray();
        double average = ratios.Average();
        return Math.Sqrt(ratios.Average(r => Math.Pow(r / average - 1, 2)));
    }

    private static T Require<T>(ContractValidationResult<T> result)
    {
        Assert.True(result.IsValid, result.IsValid ? null : result.FirstDiagnostic.ToString());
        return result.Value;
    }
}
