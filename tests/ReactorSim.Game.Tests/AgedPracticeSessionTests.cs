using System;
using System.Linq;
using ReactorSim.Core;
using ReactorSim.Game;
using Xunit;

namespace ReactorSim.Game.Tests;

public sealed class AgedPracticeSessionTests
{
    [Fact]
    public void RefuellingRaisesZonesAndBurnupDrainsThemAsItExitsTheAcceptanceBand()
    {
        var session = PracticeGameSessionFactory.CreateBrowserPlaytest(1001);
        while (session.Snapshot.SimulationTimeSeconds < 14 * 3600)
        {
            var advance = session.AdvanceWallMilliseconds(1000);
            Assert.True(advance.Accepted, advance.DiagnosticMessage);
        }
        var energyBefore = session.CoreState.EnumerateBundles().ToDictionary(b => b.BundleId, b => b.CumulativeFissionEnergyJ);
        double beforeFill = session.CurrentLiquidZoneRrs.AverageFillFraction;
        foreach (var channel in new[] { (id: 75U, direction: "toward-end-a"), (id: 324U, direction: "toward-end-b") })
        {
            var fuel = session.RefuelChannel(channel.id, channel.direction, 8, "NAT-U-SYNTHETIC");
            Assert.True(fuel.Accepted, fuel.DiagnosticMessage);
            Assert.Equal(14 * 3600, session.Snapshot.SimulationTimeSeconds);
            Assert.InRange(Math.Abs(session.CurrentLiquidZoneRrs.CompensatedNetReactivity), 0,
                PracticeLiquidZoneRrsIdentityV1.CriticalityTolerance);
        }
        foreach (var bundle in session.CoreState.EnumerateBundles())
            Assert.Equal(energyBefore.TryGetValue(bundle.BundleId, out double retainedEnergy) ? retainedEnergy : 0,
                bundle.CumulativeFissionEnergyJ);
        double fuelledFill = session.CurrentLiquidZoneRrs.AverageFillFraction;
        double fuelledRho = session.CurrentLiquidZoneRrs.CoreReactivity;
        // The flatter power-limit pack gives this old-channel pair less worth
        // than the former peaked core; fresh fuel still raises zone levels.
        Assert.InRange(fuelledFill - beforeFill, 0.03, 0.10);
        Assert.True(session.Snapshot.Shift.UnlimitedFreshFuel);
        Assert.Equal(0U, session.Snapshot.FreshBundlesAvailable);
        var burned = session.AdvanceWallMilliseconds(1000);
        Assert.True(burned.Accepted, burned.DiagnosticMessage);
        Assert.Equal(14.5 * 3600, session.Snapshot.SimulationTimeSeconds);
        Assert.True(session.CurrentLiquidZoneRrs.CoreReactivity < fuelledRho);
        // Small burnup changes can remain inside both acceptance bands and
        // retain fills. Regulation must resume as the accumulated deficit grows.
        for (int boundary = 0; boundary < 24 && session.CurrentLiquidZoneRrs.AverageFillFraction >= fuelledFill; boundary++)
        {
            var continued = session.AdvanceWallMilliseconds(1000);
            Assert.True(continued.Accepted, continued.DiagnosticMessage);
        }
        Assert.True(session.CurrentLiquidZoneRrs.AverageFillFraction < fuelledFill);
        Assert.InRange(Math.Abs(session.CurrentLiquidZoneRrs.CompensatedNetReactivity), 0,
            PracticeLiquidZoneRrsIdentityV1.CriticalityTolerance);
    }

    [Fact]
    public void ReferencePowerSeparatesThermalBurnupFromElectricalOutput()
    {
        var session = PracticeGameSessionFactory.CreateBrowserPlaytest();
        Assert.Equal(2_064_000_000.0, session.Snapshot.Physics.ReferencePowerWatts);
        Assert.InRange(System.Math.Abs(session.Snapshot.Physics.TotalPowerWatts - 2_064_000_000.0), 0, 0.01);
        Assert.InRange(System.Math.Abs(session.Snapshot.Physics.ElectricalPowerWatts - 650_000_000.0), 0, 0.01);
    }

    [Theory]
    [InlineData(0UL)]
    [InlineData(1001UL)]
    [InlineData(1002UL)]
    [InlineData(4294967295UL)]
    public void SeededBrowserStartsHaveRegulatingHeadroomAndAcceptRefuelling(ulong seed)
    {
        var session = PracticeGameSessionFactory.CreateBrowserPlaytest(seed);
        Assert.Equal(seed, session.Snapshot.Seed);
        Assert.True(session.Snapshot.RrsReserveFraction > 0);
        Assert.False(session.Snapshot.IsGameOver);
        Assert.All(session.Snapshot.Core.Channels, channel =>
        {
            Assert.InRange(channel.PowerWatts, 0, 7_300_000);
            Assert.All(channel.Bundles, bundle => Assert.InRange(bundle.PowerWatts, 0, 935_000));
        });
        Assert.InRange(System.Math.Abs(session.CurrentLiquidZoneRrs.CompensatedNetReactivity), 0, 0.002);
        Assert.True(double.IsFinite(session.Snapshot.AxialTiltFraction));
        var refuel = session.RefuelChannel(189, "toward-end-b", 8, "NAT-U-SYNTHETIC");
        Assert.True(refuel.Accepted, refuel.DiagnosticMessage);
        Assert.Equal(0U, refuel.Snapshot.FreshBundlesAvailable);
        Assert.True(refuel.Snapshot.Shift.UnlimitedFreshFuel);
    }
}
