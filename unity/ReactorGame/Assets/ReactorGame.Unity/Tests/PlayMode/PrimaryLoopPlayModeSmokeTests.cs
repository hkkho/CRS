using System;
using System.Collections;
using NUnit.Framework;
using ReactorSim.Game;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ReactorGame.Unity.PlayModeTests
{
    public sealed class PrimaryLoopPlayModeSmokeTests
    {
        private const string BootstrapSceneName = "Bootstrap";
        private const ushort ShiftCount = 4;

        [UnityTest]
        public IEnumerator BootstrapPrimaryLoopRemainsAuthoritativeAndUsable()
        {
            SceneManager.LoadScene(BootstrapSceneName, LoadSceneMode.Single);

            UnityGameController controller = null;
            for (int frame = 0; frame < 120; frame++)
            {
                controller = UnityEngine.Object.FindFirstObjectByType<UnityGameController>();
                if (controller != null && controller.IsInitialized)
                {
                    break;
                }

                yield return null;
            }

            Assert.That(controller, Is.Not.Null, "Bootstrap must create its game controller.");
            Assert.That(controller.IsInitialized, Is.True, "Bootstrap must finish runtime initialization.");
            Assert.That(controller.RuntimePort, Is.Not.Null);

            Phase8UnityRuntimeAdapter adapter =
                UnityEngine.Object.FindFirstObjectByType<Phase8UnityRuntimeAdapter>();
            CoreMapView coreMap = UnityEngine.Object.FindFirstObjectByType<CoreMapView>();
            DebugMenuView debugMenu = UnityEngine.Object.FindFirstObjectByType<DebugMenuView>();

            Assert.That(adapter, Is.Not.Null);
            Assert.That(adapter.IsBound, Is.True);
            Assert.That(coreMap, Is.Not.Null);
            Assert.That(coreMap.IsBuilt, Is.True);
            Assert.That(coreMap.IsBound, Is.True);
            Assert.That(debugMenu, Is.Not.Null);
            Assert.That(debugMenu.IsBuilt, Is.True);
            Assert.That(debugMenu.IsBound, Is.True);
            Assert.That(debugMenu.IsVisible, Is.False);

            Phase8UnityPresentationSnapshotV1 initial = adapter.Snapshot;
            AssertAuthoritativeFiniteSnapshot(initial, coreMap);
            Assert.That(debugMenu.Snapshot, Is.SameAs(initial));
            Assert.That(debugMenu.StatusText, Does.Contain("bound"));

            int selectedChannelIndex = coreMap.SelectedChannelIndex;
            Assert.That(
                coreMap.SelectedChannelText,
                Does.Contain("Channel " + selectedChannelIndex));
            Assert.That(coreMap.SelectChannel(selectedChannelIndex), Is.True);

            controller.AutoAdvance = false;

            Assert.That(coreMap.SelectTowardEndA(), Is.True);
            Phase8UnityCommandResultV1 refuelA = coreMap.RefuelSelected();
            AssertAccepted(refuelA, Phase8UnityCommandKindV1.RefuelChannel);
            Assert.That(refuelA.Snapshot.Core, Is.Not.Null);
            Assert.That(
                refuelA.Snapshot.Core.GetChannel((uint)selectedChannelIndex).Bundles.Count,
                Is.EqualTo((int)GameCorePresentationConstants.BundlePositionCount));
            Assert.That(refuelA.Snapshot.RefuellingOperationCount, Is.EqualTo(1u));
            Assert.That(refuelA.Snapshot.FreshBundlesAvailable, Is.EqualTo(initial.FreshBundlesAvailable - ShiftCount));
            Assert.That(refuelA.Snapshot.LastRefuelledChannel, Is.EqualTo(selectedChannelIndex));
            Assert.That(refuelA.Snapshot.LastRefuellingDirectionId, Is.EqualTo(CoreMapView.TowardEndADirectionId));
            Assert.That(coreMap.RefuelFeedbackText, Does.Contain("Refuel accepted"));
            Assert.That(coreMap.StatusText, Does.Contain("accepted"));

            int secondChannelIndex = selectedChannelIndex == 0 ? 1 : selectedChannelIndex - 1;
            Assert.That(coreMap.SelectChannel(secondChannelIndex), Is.True);
            Assert.That(
                coreMap.SelectedChannelText,
                Does.Contain("Channel " + secondChannelIndex));

            Assert.That(coreMap.SelectTowardEndB(), Is.True);
            Phase8UnityCommandResultV1 refuelB = coreMap.RefuelSelected();
            AssertAccepted(refuelB, Phase8UnityCommandKindV1.RefuelChannel);
            Assert.That(refuelB.Sequence, Is.EqualTo(refuelA.Sequence + 1UL));
            Assert.That(refuelB.Snapshot.Core, Is.Not.Null);
            Assert.That(
                refuelB.Snapshot.Core.GetChannel((uint)secondChannelIndex).Bundles.Count,
                Is.EqualTo((int)GameCorePresentationConstants.BundlePositionCount));
            Assert.That(refuelB.Snapshot.RefuellingOperationCount, Is.EqualTo(2u));
            Assert.That(refuelB.Snapshot.FreshBundlesAvailable, Is.EqualTo(initial.FreshBundlesAvailable - (ShiftCount * 2u)));
            Assert.That(refuelB.Snapshot.LastRefuelledChannel, Is.EqualTo(secondChannelIndex));
            Assert.That(refuelB.Snapshot.LastRefuellingDirectionId, Is.EqualTo(CoreMapView.TowardEndBDirectionId));
            Assert.That(coreMap.RefuelFeedbackText, Does.Contain("Refuel accepted"));
            Assert.That(coreMap.StatusText, Does.Contain("accepted"));

            Phase8UnityCommandResultV1 pause = debugMenu.Pause();
            AssertAccepted(pause, Phase8UnityCommandKindV1.Pause);
            Assert.That(pause.Snapshot.IsPaused, Is.True);
            double pausedSimulationTime = controller.RuntimePort.Snapshot.SimulationTimeSeconds;

            controller.AutoAdvance = true;
            controller.Tick(1.0);
            Assert.That(
                controller.RuntimePort.Snapshot.SimulationTimeSeconds,
                Is.EqualTo(pausedSimulationTime));

            Phase8UnityCommandResultV1 resume = debugMenu.Resume();
            AssertAccepted(resume, Phase8UnityCommandKindV1.Resume);
            Assert.That(resume.Snapshot.IsPaused, Is.False);

            controller.Tick(0.1);
            Phase8UnityPresentationSnapshotV1 resumed = controller.RuntimePort.Snapshot;
            Assert.That(resumed.SimulationTimeSeconds, Is.GreaterThan(pausedSimulationTime));
            Assert.That(debugMenu.StatusText, Does.Contain("accepted"));

            debugMenu.ToggleVisibility();
            Assert.That(debugMenu.IsVisible, Is.True);
            Assert.That(debugMenu.StateText, Does.Contain("DEBUG STATE"));
            Assert.That(debugMenu.DigestText, Does.Contain("scenario="));
            debugMenu.ToggleVisibility();
            Assert.That(debugMenu.IsVisible, Is.False);
        }

        private static void AssertAuthoritativeFiniteSnapshot(
            Phase8UnityPresentationSnapshotV1 snapshot,
            CoreMapView coreMap)
        {
            Assert.That(snapshot, Is.Not.Null);
            Assert.That(snapshot.OutcomeId, Is.EqualTo("Running"));
            Assert.That(snapshot.Core, Is.Not.Null);
            Assert.That(snapshot.Core.ChannelCount, Is.EqualTo(GameCorePresentationConstants.ChannelCount));
            Assert.That(snapshot.Core.Physics, Is.Not.Null);
            Assert.That(snapshot.Core.Physics.IsAuthoritative, Is.True);
            Assert.That(snapshot.Core.Physics.SolveState, Is.Not.Empty);
            AssertFinite(snapshot.SimulationTimeSeconds, "simulation time");
            AssertFinite(snapshot.WallElapsedSeconds, "wall time");
            AssertFinite(snapshot.NormalizedPowerFraction, "setpoint");
            AssertFinite(snapshot.ActualPowerFraction, "actual power");
            AssertFinite(snapshot.AbsoluteTiltFraction, "tilt");
            AssertFinite(snapshot.ScoreTotal, "score");
            AssertFinite(snapshot.Core.Physics.TotalPowerWatts, "total power");
            AssertFinite(snapshot.Core.Physics.EffectiveK, "effective k");
            AssertFinite(snapshot.Core.Physics.Reactivity, "reactivity");

            Assert.That(coreMap.SelectedChannelIndex, Is.InRange(
                0,
                (int)GameCorePresentationConstants.ChannelCount - 1));
            GameChannelPresentationSnapshot selectedChannel =
                snapshot.Core.GetChannel((uint)coreMap.SelectedChannelIndex);
            Assert.That(selectedChannel.Bundles.Count, Is.EqualTo((int)GameCorePresentationConstants.BundlePositionCount));
            AssertFinite(selectedChannel.PowerWatts, "selected channel power");
            AssertFinite(selectedChannel.AverageBurnupMwDayPerKg, "selected channel burnup");

            foreach (GameChannelPresentationSnapshot channel in snapshot.Core.Channels)
            {
                Assert.That(channel, Is.Not.Null);
                AssertFinite(channel.PowerWatts, "channel power");
                AssertFinite(channel.AverageBurnupMwDayPerKg, "channel burnup");
                Assert.That(channel.Bundles.Count, Is.EqualTo((int)GameCorePresentationConstants.BundlePositionCount));
                foreach (GameBundlePresentationSnapshot bundle in channel.Bundles)
                {
                    Assert.That(bundle, Is.Not.Null);
                    AssertFinite(bundle.CurrentBurnupMwDayPerKg, "bundle burnup");
                    AssertFinite(bundle.PowerWatts, "bundle power");
                    AssertFinite(bundle.InsertedAtSeconds, "bundle insertion time");
                }
            }
        }

        private static void AssertAccepted(
            Phase8UnityCommandResultV1 result,
            Phase8UnityCommandKindV1 expectedKind)
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Kind, Is.EqualTo(expectedKind));
            Assert.That(result.Accepted, Is.True, result.DiagnosticMessage);
            Assert.That(result.Snapshot, Is.Not.Null);
        }

        private static void AssertFinite(double value, string label)
        {
            Assert.That(
                !double.IsNaN(value) && !double.IsInfinity(value),
                Is.True,
                label + " must be finite.");
        }
    }
}
