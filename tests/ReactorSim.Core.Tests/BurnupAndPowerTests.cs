using System;
using System.Collections.Generic;
using System.Linq;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class BurnupAndPowerTests
{
    [Fact]
    public void KnownSiPowerProducesOneEnergyRecordAndMonotoneBurnup()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        BundleState sourceBundle = fixture.Inventory.Get(Node(0, 0))!;
        BundleInventory oneBundleInventory = Require(
            BundleInventory.TryCreate(fixture.Topology, new[] { sourceBundle }));
        const double powerWatts = 250.0;
        const double elapsedSeconds = 12.5;

        AcceptedBundlePowerSnapshotV1 snapshot = CreateSnapshot(
            oneBundleInventory.EnumerateOccupied()
                .Select(bundle => CreatePowerSample(bundle, powerWatts)),
            snapshotTimeSeconds: 10.0,
            coreStateVersion: 4);

        ContractValidationResult<BurnupIntervalResultV1> result =
            BurnupIntervalTransition.TryApply(
                oneBundleInventory,
                currentCoreStateVersion: 4,
                currentTimeSeconds: 10.0,
                targetTimeSeconds: 10.0 + elapsedSeconds,
                currentStateDigest: snapshot.StateDigest,
                powerSnapshot: snapshot);

        AssertValid(result);
        BurnupIntervalResultV1 applied = result.Value;
        BurnupIntervalRecordV1 record = Assert.Single(applied.Records);
        BundleState resultingBundle = applied.ResultingInventory.Get(sourceBundle.Node)!;

        double expectedEnergyJ = powerWatts * elapsedSeconds;
        double expectedBurnupJPerKgHm =
            sourceBundle.InitialBurnupJPerKgHm +
            expectedEnergyJ / sourceBundle.HeavyMetalMassKg;

        Assert.Equal(elapsedSeconds, record.DeltaTimeSeconds, 12);
        Assert.Equal(powerWatts, record.PowerWatts, 12);
        Assert.Equal(expectedEnergyJ, record.DeltaFissionEnergyJ, 12);
        Assert.Equal(expectedEnergyJ, record.NewCumulativeFissionEnergyJ, 12);
        Assert.Equal(expectedBurnupJPerKgHm, record.NewBurnupJPerKgHm, 12);
        Assert.Equal(expectedEnergyJ, resultingBundle.CumulativeFissionEnergyJ, 12);
        Assert.Equal(expectedBurnupJPerKgHm, resultingBundle.CurrentBurnupJPerKgHm, 12);
        Assert.True(resultingBundle.CurrentBurnupJPerKgHm > sourceBundle.CurrentBurnupJPerKgHm);
        Assert.Same(oneBundleInventory, applied.SourceInventory);
        Assert.Same(sourceBundle, fixture.Inventory.Get(sourceBundle.Node));
        Assert.True(applied.PowerSnapshotInvalidated);
    }

    [Fact]
    public void MixedPowerIntervalAdvancesOnlyPositiveBundlesAndRejectsInvalidInputsAtomically()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        BundleState[] sourceBundles = fixture.Inventory.EnumerateOccupied().ToArray();
        double[] powers = { 0.0, 3.0, 0.0, 7.5, 0.0, 11.0 };
        AcceptedBundlePowerSnapshotV1 validSnapshot = CreateSnapshot(
            sourceBundles.Select((bundle, index) => CreatePowerSample(bundle, powers[index])),
            snapshotTimeSeconds: 0.0,
            coreStateVersion: 0,
            digestMarker: 21);

        ContractValidationResult<BurnupIntervalResultV1> applied =
            BurnupIntervalTransition.TryApply(
                fixture.Inventory,
                currentCoreStateVersion: 0,
                currentTimeSeconds: 0.0,
                targetTimeSeconds: 4.0,
                currentStateDigest: validSnapshot.StateDigest,
                powerSnapshot: validSnapshot);

        AssertValid(applied);
        Assert.Equal(sourceBundles.Length, applied.Value.Records.Count);
        for (int index = 0; index < sourceBundles.Length; index++)
        {
            BundleState before = sourceBundles[index];
            BundleState after = applied.Value.ResultingInventory.Get(before.Node)!;
            double expectedEnergy = powers[index] * 4.0;

            Assert.Equal(expectedEnergy, after.CumulativeFissionEnergyJ, 12);
            Assert.Equal(
                before.CurrentBurnupJPerKgHm + expectedEnergy / before.HeavyMetalMassKg,
                after.CurrentBurnupJPerKgHm,
                12);
            Assert.True(after.CurrentBurnupJPerKgHm >= before.CurrentBurnupJPerKgHm);
            if (powers[index] == 0.0)
            {
                Assert.Equal(before.CumulativeFissionEnergyJ, after.CumulativeFissionEnergyJ, 12);
                Assert.Equal(before.CurrentBurnupJPerKgHm, after.CurrentBurnupJPerKgHm, 12);
            }
            else
            {
                Assert.NotSame(before, after);
            }
        }

        StableId[] originalIds = sourceBundles.Select(bundle => bundle.BundleId).ToArray();
        double[] originalEnergy = sourceBundles
            .Select(bundle => bundle.CumulativeFissionEnergyJ)
            .ToArray();

        AcceptedBundlePowerSnapshotV1 staleTime = CreateSnapshot(
            sourceBundles.Select((bundle, index) => CreatePowerSample(bundle, powers[index])),
            snapshotTimeSeconds: 1.0,
            coreStateVersion: 0,
            digestMarker: 22);
        AssertInvalid(
            BurnupIntervalTransition.TryApply(
                fixture.Inventory,
                0,
                0.0,
                4.0,
                staleTime.StateDigest,
                staleTime),
            "BurnupInterval.PowerSnapshot.Time.Stale");

        AcceptedBundlePowerSnapshotV1 staleDigest = CreateSnapshot(
            sourceBundles.Select((bundle, index) => CreatePowerSample(bundle, powers[index])),
            snapshotTimeSeconds: 0.0,
            coreStateVersion: 0,
            digestMarker: 23);
        AssertInvalid(
            BurnupIntervalTransition.TryApply(
                fixture.Inventory,
                0,
                0.0,
                4.0,
                CreateDigest(24),
                staleDigest),
            "BurnupInterval.PowerSnapshot.StateDigest.Stale");

        AcceptedBundlePowerSnapshotV1 incomplete = CreateSnapshot(
            sourceBundles.Skip(1).Select((bundle, index) => CreatePowerSample(bundle, powers[index + 1])),
            snapshotTimeSeconds: 0.0,
            coreStateVersion: 0,
            digestMarker: 25);
        AssertInvalid(
            BurnupIntervalTransition.TryApply(
                fixture.Inventory,
                0,
                0.0,
                4.0,
                incomplete.StateDigest,
                incomplete),
            "BurnupInterval.PowerSnapshot.BundlePower.CountMismatch");

        AcceptedBundlePowerSnapshotV1 overflowing = CreateSnapshot(
            sourceBundles.Select(bundle => CreatePowerSample(bundle, double.MaxValue)),
            snapshotTimeSeconds: 0.0,
            coreStateVersion: 0,
            digestMarker: 26);
        AssertInvalid(
            BurnupIntervalTransition.TryApply(
                fixture.Inventory,
                0,
                0.0,
                2.0,
                overflowing.StateDigest,
                overflowing),
            "BurnupInterval.EnergyIncrement.Invalid");

        ContractValidationResult<BundlePowerSampleV1> negativePower =
            BundlePowerSampleV1.TryCreate(
                sourceBundles[0].BundleId,
                sourceBundles[0].ChannelId,
                sourceBundles[0].Position,
                -1.0);
        ContractValidationResult<BundlePowerSampleV1> nonFinitePower =
            BundlePowerSampleV1.TryCreate(
                sourceBundles[0].BundleId,
                sourceBundles[0].ChannelId,
                sourceBundles[0].Position,
                double.NaN);
        AssertInvalid(negativePower, "BundlePowerSample.Power.Invalid");
        AssertInvalid(nonFinitePower, "BundlePowerSample.Power.Invalid");

        Assert.Equal(originalIds, fixture.Inventory.EnumerateOccupied().Select(bundle => bundle.BundleId));
        Assert.Equal(originalEnergy, fixture.Inventory.EnumerateOccupied().Select(bundle => bundle.CumulativeFissionEnergyJ));
    }

    [Fact]
    public void FullCoreProjectionKeepsSiPowerAmplitudeShapeAndRhoSeparate()
    {
        const double referencePowerWatts = 1_000_000_000.0;
        SyntheticGameCoreStateV1 state = SyntheticGameCoreStateV1.CreatePractice();
        FullCoreDiffusionDataPackV1 diffusionPack = Require(
            FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6());
        FullCoreDiffusionModelV1 model = Require(
            FullCoreDiffusionModelV1.TryCreateCandu6(diffusionPack));
        IqsKineticsDataPackV1 kineticsPack = Require(
            IqsKineticsDataPackV1.TryLoadEmbeddedCandu6());
        IqsFullCoreSolver solver = Require(
            IqsFullCoreSolver.TryCreate(
                model,
                kineticsPack,
                state.EnumerateBundles(),
                referencePowerWatts));

        IqsSpatialCandidateV1 projection = solver.CurrentProjection;
        FullCoreDiffusionSolveResultV1 spatial = projection.SpatialSolve;

        Assert.Equal(1.0, solver.Amplitude, 12);
        Assert.Equal(referencePowerWatts, spatial.TotalPowerWatts, 3);
        Assert.Equal(referencePowerWatts, projection.ShapePowerWatts, 3);
        Assert.Equal(model.NodeCount, projection.ShapeNodePowerWatts.Count);
        Assert.Equal(
            projection.ShapePowerWatts,
            projection.ShapeNodePowerWatts.Sum(),
            3);
        Assert.Equal(
            spatial.TotalPowerWatts,
            spatial.NodePowerWatts.Sum(),
            3);
        IqsSpatialCandidateV1 acceptedProjection = solver.CurrentProjection;
        AssertInvalid(
            solver.TrySolveCandidate(Array.Empty<BundleState>()),
            "FullCoreDiffusionSolve.Inventory.Bundle.Missing");
        Assert.Same(acceptedProjection, solver.CurrentProjection);

        Assert.True(spatial.EffectiveK > 0.0);
        Assert.True(double.IsFinite(spatial.EffectiveK));
        Assert.Equal(
            (spatial.EffectiveK - 1.0) / spatial.EffectiveK,
            spatial.Reactivity,
            14);
        Assert.InRange(spatial.PowerBalanceRelativeError, 0.0, 1e-10);
        Assert.All(
            projection.ShapeNodePowerWatts,
            power => Assert.True(double.IsFinite(power) && power >= 0.0));
        Assert.All(
            spatial.NodePowerWatts,
            power => Assert.True(double.IsFinite(power) && power >= 0.0));
    }

    [Fact]
    public void FreshAndBurnedRepresentativeBundlesProduceDistinctNormalizedResponses()
    {
        const double targetPowerWatts = 1_000_000_000.0;
        const uint representativeChannel = 189;
        const uint representativePosition = 6;
        SyntheticGameCoreStateV1 initial = SyntheticGameCoreStateV1.CreatePractice();
        FullCoreDiffusionModelV1 model = CreateFullCoreModel();

        FullCoreDiffusionSolveResultV1 baseline = Require(
            model.TrySolve(initial.EnumerateBundles(), targetPowerWatts));
        BundleState original = initial.GetBundle(
            representativeChannel,
            representativePosition);
        BundleState fresh = ReplaceBundle(original, 0.0);
        BundleState burned = ReplaceBundle(original, 1.5e12);
        BundleState[] freshBundles = ReplaceInEnumeration(
            initial.EnumerateBundles(),
            fresh);
        BundleState[] burnedBundles = ReplaceInEnumeration(
            initial.EnumerateBundles(),
            burned);

        FullCoreDiffusionSolveResultV1 freshSolve = Require(
            model.TrySolve(
                freshBundles,
                targetPowerWatts,
                baseline.EffectiveK,
                baseline.Group1Flux,
                baseline.Group2Flux));
        FullCoreDiffusionSolveResultV1 burnedSolve = Require(
            model.TrySolve(
                burnedBundles,
                targetPowerWatts,
                baseline.EffectiveK,
                baseline.Group1Flux,
                baseline.Group2Flux));

        Assert.Equal(targetPowerWatts, freshSolve.TotalPowerWatts, 3);
        Assert.Equal(targetPowerWatts, burnedSolve.TotalPowerWatts, 3);
        Assert.InRange(freshSolve.PowerBalanceRelativeError, 0.0, 1e-10);
        Assert.InRange(burnedSolve.PowerBalanceRelativeError, 0.0, 1e-10);
        Assert.True(freshSolve.EffectiveK > burnedSolve.EffectiveK);
        Assert.True(
            Math.Abs(
                freshSolve.NodePowerWatts[
                    checked((int)(representativeChannel * SyntheticGameCoreStateV1.BundlePositionCount + representativePosition))] -
                burnedSolve.NodePowerWatts[
                    checked((int)(representativeChannel * SyntheticGameCoreStateV1.BundlePositionCount + representativePosition))]) >
            0.0);
        Assert.All(
            freshSolve.NodePowerWatts,
            power => Assert.True(double.IsFinite(power) && power >= 0.0));
        Assert.All(
            burnedSolve.NodePowerWatts,
            power => Assert.True(double.IsFinite(power) && power >= 0.0));
        Assert.NotEqual(freshSolve.CoefficientBindingDigest, burnedSolve.CoefficientBindingDigest);
    }

    private static FullCoreDiffusionModelV1 CreateFullCoreModel()
    {
        FullCoreDiffusionDataPackV1 pack = Require(
            FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6());
        return Require(FullCoreDiffusionModelV1.TryCreateCandu6(pack));
    }

    private static BundleState[] ReplaceInEnumeration(
        IEnumerable<BundleState> bundles,
        BundleState replacement)
    {
        return bundles
            .Select(bundle => bundle.BundleId == replacement.BundleId ? replacement : bundle)
            .ToArray();
    }

    private static BundleState ReplaceBundle(BundleState source, double initialBurnupJPerKgHm)
    {
        return new BundleState(
            source.BundleId,
            source.ChannelId,
            source.Position,
            source.MaterialVariantId,
            initialBurnupJPerKgHm,
            0.0,
            source.HeavyMetalMassKg,
            source.InsertedAtSeconds);
    }

    private static BundlePowerSampleV1 CreatePowerSample(BundleState bundle, double powerWatts)
    {
        return Require(
            BundlePowerSampleV1.TryCreate(
                bundle.BundleId,
                bundle.ChannelId,
                bundle.Position,
                powerWatts));
    }

    private static AcceptedBundlePowerSnapshotV1 CreateSnapshot(
        IEnumerable<BundlePowerSampleV1> samples,
        double snapshotTimeSeconds,
        ulong coreStateVersion,
        byte digestMarker = 17)
    {
        return Require(
            AcceptedBundlePowerSnapshotV1.TryCreate(
                StableId.Parse("00000000-0000-0000-0000-0000000007a1"),
                snapshotTimeSeconds,
                coreStateVersion,
                CreateDigest(digestMarker),
                samples));
    }

    private static NodeKey Node(uint channelIndex, uint positionIndex)
    {
        return new NodeKey(new ChannelId(channelIndex), new BundlePosition(positionIndex));
    }

    private static Digest32 CreateDigest(byte marker)
    {
        return new Digest32(Enumerable.Repeat(marker, 32).ToArray());
    }

    private static T Require<T>(ContractValidationResult<T> result)
    {
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        return result.Value;
    }

    private static void AssertValid<T>(ContractValidationResult<T> result)
    {
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
    }

    private static void AssertInvalid<T>(ContractValidationResult<T> result, string expectedCode)
    {
        Assert.False(result.IsValid);
        Assert.Equal(expectedCode, result.FirstDiagnostic.Code);
    }
}
