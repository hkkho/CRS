using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using ReactorSim.Core;

namespace ReactorSim.Game
{
    internal static class GamePresentationProjector
    {
        internal static GameCorePresentationSnapshot Build(
            SyntheticGameCoreStateV1 state,
            double powerAmplitude,
            EquilibriumCoreProjectionV1 projection,
            PracticeLiquidZoneRrsV1 rrs, GameXenonPresentationSnapshot xenon,
            double targetFraction, ulong version, PracticeXenonStateV1 poison, Func<uint, string> ineligibility)
        {
            var channelStates = new IReadOnlyList<BundleState>[
                (int)GameCorePresentationConstants.ChannelCount];
            for (uint channelIndex = 0;
                 channelIndex < GameCorePresentationConstants.ChannelCount;
                 channelIndex++)
            {
                IReadOnlyList<BundleState> bundles = state.GetChannel(channelIndex);
                channelStates[(int)channelIndex] = bundles;
            }

            double amplitude = Clamp(powerAmplitude, 0.0, 1.5);
            double physicalShapeScale = amplitude;
            double totalPowerWatts = projection.ShapePowerWatts * physicalShapeScale;
            double actualPowerFraction = totalPowerWatts /
                                         PracticeGameSessionFactory.PracticeReferencePowerWatts;
            double meanChannelPowerWatts = totalPowerWatts /
                                           GameCorePresentationConstants.ChannelCount;
            var channelPowerWatts = new double[
                (int)GameCorePresentationConstants.ChannelCount];
            for (int index = 0; index < projection.ShapeNodePowerWatts.Count; index++)
            {
                uint channelIndex = (uint)(index /
                    (int)GameCorePresentationConstants.BundlePositionCount);
                channelPowerWatts[(int)channelIndex] +=
                    projection.ShapeNodePowerWatts[index] * physicalShapeScale;
            }

            var channels = new List<GameChannelPresentationSnapshot>(
                (int)GameCorePresentationConstants.ChannelCount);
            int nodeIndex = 0;
            for (uint channelIndex = 0;
                 channelIndex < GameCorePresentationConstants.ChannelCount;
                 channelIndex++)
            {
                PracticeCoreGridPosition grid = PracticeCoreLayout.GetPosition(channelIndex);
                IReadOnlyList<BundleState> bundles = channelStates[(int)channelIndex];
                var bundleSnapshots = new List<GameBundlePresentationSnapshot>(
                    (int)GameCorePresentationConstants.BundlePositionCount);
                double burnupTotal = 0.0;
                double channelThermalFlux = 0.0;
                double axialThermalFluxMoment = 0.0;
                for (int bundleIndex = 0; bundleIndex < bundles.Count; bundleIndex++)
                {
                    BundleState bundle = bundles[bundleIndex];
                    double burnup = bundle.CurrentBurnupJPerKgHm /
                                    GameCorePresentationConstants.JoulesPerMegaWattDayPerKilogram;
                    double bundlePower =
                        projection.ShapeNodePowerWatts[nodeIndex] * physicalShapeScale;
                    double thermalFlux = projection.SpatialSolve.Group2Flux[nodeIndex++];
                    burnupTotal += burnup;
                    channelThermalFlux += thermalFlux;
                    axialThermalFluxMoment += thermalFlux *
                        (2.0 * bundle.Position.Value /
                         (GameCorePresentationConstants.BundlePositionCount - 1) - 1.0);
                    bundleSnapshots.Add(
                        new GameBundlePresentationSnapshot(
                            bundle.Position.Value,
                            bundle.BundleId.ToString(),
                            bundle.MaterialVariantId.Value,
                            burnup,
                            bundlePower,
                            bundle.InsertedAtSeconds,
                            bundle.StateVersion));
                }

                double channelPower = channelPowerWatts[(int)channelIndex];
                double localPower = meanChannelPowerWatts <= 0.0
                    ? 1.0
                    : channelPower / meanChannelPowerWatts;
                double localTilt = channelThermalFlux <= 0.0
                    ? 0.0
                    : axialThermalFluxMoment / channelThermalFlux;
                channels.Add(
                    new GameChannelPresentationSnapshot(
                        channelIndex,
                        grid.Column,
                        grid.Row,
                        burnupTotal / GameCorePresentationConstants.BundlePositionCount,
                        channelPower,
                        localPower,
                        localTilt,
                        PracticeCoreLayout.GetFlowDirection(grid),
                        bundleSnapshots,
                        xenon.GetChannel(channelIndex), ineligibility(channelIndex)));
            }

            FullCoreDiffusionSolveResultV1 spatial = projection.SpatialSolve;
            double targetPowerAmplitude = Clamp(
                targetFraction,
                0.0,
                1.5);
            var physics = new GamePhysicsPresentationSnapshot(
                EquilibriumCoreSolverIdentityV1.ModelId,
                EquilibriumCoreSolverIdentityV1.FormulationId,
                EquilibriumCoreSolverIdentityV1.ShapeMethodId,
                EquilibriumCoreSolverIdentityV1.AmplitudeMethodId,
                EquilibriumCoreSolverIdentityV1.ReactivityMethodId,
                "converged",
                true,
                version,
                PracticeGameSessionFactory.PracticeReferencePowerWatts,
                amplitude,
                actualPowerFraction,
                PracticeGameSessionFactory.PracticeReferencePowerWatts * targetPowerAmplitude,
                totalPowerWatts,
                meanChannelPowerWatts,
                totalPowerWatts /
                    (GameCorePresentationConstants.ChannelCount *
                     GameCorePresentationConstants.BundlePositionCount),
                spatial.EffectiveK,
                spatial.Reactivity,
                projection.WeightedPerturbationReactivity,
                projection.ReactivityNumerator,
                projection.ReactivityDenominator,
                projection.ReactivityIdentity,
                projection.ReactivityBindingDigestHex,
                spatial.PowerBalanceRelativeError,
                projection.SolverIdentity,
                spatial.IterationCount,
                spatial.ResidualRelativeInfinity,
                rrs.CoreReactivity,
                rrs.CompensatedNetReactivity,
                rrs.AverageFillFraction,
                rrs.AverageFillFraction,
                0.0,
                1.0,
                rrs.LowExhaustion || rrs.HighExhaustion,
                0.0,
                rrs.CadenceIdentity,
                "equilibrium-static-only-v1",
                projection.ReactivityBindingDigestHex,
                0,
                0.0);
            return new GameCorePresentationSnapshot(
                channels,
                physics,
                xenon,
                new GameRrsPresentationSnapshot(rrs, poison),
                ComputeSignedAxialTiltFraction(projection.SpatialSolve.Group2Flux));
        }
        internal static GameXenonPresentationSnapshot Poison(
            int selectedChannelIndex,
            double simulationTimeSeconds, PracticeXenonStateV1 xenon, PracticeXenonStateV1 coupled,
            EquilibriumCoreSolverV1 solver, IReadOnlyList<NodeKey> nonfuel)
        {
            var channelDiagnostics = new List<GameXenonChannelPresentationSnapshot>(
                (int)GameCorePresentationConstants.ChannelCount);

            for (uint channelIndex = 0;
                 channelIndex < GameCorePresentationConstants.ChannelCount;
                 channelIndex++)
            {
                double meanI = 0, maxI = 0, meanXe = 0, maxXe = 0;
                for (int position = 0; position < 12; position++)
                {
                    int node = (int)channelIndex * 12 + position;
                    meanI += xenon.Iodine[node] / 12;
                    meanXe += xenon.Xenon[node] / 12;
                    maxI = Math.Max(maxI, xenon.Iodine[node]);
                    maxXe = Math.Max(maxXe, xenon.Xenon[node]);
                }
                channelDiagnostics.Add(
                    new GameXenonChannelPresentationSnapshot(
                        channelIndex,
                        meanI, maxI, meanXe, maxXe,
                        0.0, 0.0,
                        meanXe * PracticeXenonDataV1.SigmaGroup2M2,
                        maxXe * PracticeXenonDataV1.SigmaGroup2M2));
            }

            return new GameXenonPresentationSnapshot(
                PracticeXenonDataV1.Identity,
                DigestHex(xenon.StateDigest),
                xenon.StateVersion,
                simulationTimeSeconds,
                checked((int)(GameCorePresentationConstants.ChannelCount *
                    GameCorePresentationConstants.BundlePositionCount)),
                "practice-xenon-fixed-reference-spatial-coupling-v1",
                true,
                DigestHex(new Digest32(solver.DataPack.Descriptor.ContentDigest.ToArray())),
                DigestHex(coupled.BuildOverlay(nonfuel).OverlayDigest),
                DigestHex(solver.CurrentSpatialSolve.CoefficientBindingDigest),
                channelDiagnostics.Average(c => c.MeanI135NumberDensityM3),
                channelDiagnostics.Max(c => c.MaxI135NumberDensityM3),
                channelDiagnostics.Average(c => c.MeanXe135NumberDensityM3),
                channelDiagnostics.Max(c => c.MaxXe135NumberDensityM3),
                0.0,
                0.0,
                channelDiagnostics.Average(c => c.MeanDynamicAbsorptionGroup2PerM),
                channelDiagnostics.Max(c => c.MaxDynamicAbsorptionGroup2PerM),
                channelDiagnostics,
                selectedChannelIndex,
                coupled.SimulationTimeSeconds,
                DigestHex(coupled.StateDigest));
        }
        private static string DigestHex(Digest32 digest)
        {
            var builder = new StringBuilder(digest.Bytes.Count * 2 + 7);
            builder.Append("sha256:");
            foreach (byte value in digest.Bytes)
            {
                builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
        internal static double ComputeSignedAxialTiltFraction(
            IReadOnlyList<double> nodeThermalFlux)
        {
            double totalFlux = 0.0;
            double axialMoment = 0.0;
            for (int index = 0; index < nodeThermalFlux.Count; index++)
            {
                double flux = nodeThermalFlux[index];
                int position = index % (int)GameCorePresentationConstants.BundlePositionCount;
                totalFlux += flux;
                axialMoment += flux *
                    (2.0 * position /
                     (GameCorePresentationConstants.BundlePositionCount - 1) - 1.0);
            }

            return totalFlux <= 0.0 ? 0.0 : axialMoment / totalFlux;
        }
        private static double Clamp(double value, double minimum, double maximum) => Math.Min(maximum, Math.Max(minimum, value));
    }
}
