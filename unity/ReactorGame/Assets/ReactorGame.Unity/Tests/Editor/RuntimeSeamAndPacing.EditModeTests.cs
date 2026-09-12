using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ReactorGame.Unity.Tests
{
    public sealed class RuntimeSeamAndPacingEditModeTests
    {
        [Test]
        public void RealRuntimePortMapsAdvanceAndRefuelThroughGameSession()
        {
            UnityRuntimePort port = new UnityRuntimePort();
            Phase8UnityPresentationSnapshotV1 before = port.Snapshot;

            Phase8UnityCommandResultV1 advance = port.Execute(
                Phase8UnityInputCommandV1.AdvanceWallMilliseconds(1, 100));

            Assert.That(advance.Accepted, Is.True, advance.DiagnosticMessage);
            Assert.That(advance.Kind, Is.EqualTo(Phase8UnityCommandKindV1.AdvanceWallMilliseconds));
            Assert.That(
                advance.Snapshot.SimulationTimeSeconds,
                Is.EqualTo(before.SimulationTimeSeconds + 1.0).Within(1e-9));
            Assert.That(
                advance.Snapshot.WallElapsedSeconds,
                Is.EqualTo(before.WallElapsedSeconds + 0.1).Within(1e-9));

            Phase8UnityPresentationSnapshotV1 beforeRefuel = advance.Snapshot;
            const uint channelIndex = 190;
            const ushort shiftCount = 4;
            const string directionId = "toward-end-b";
            const string fuelTypeId = "NAT-U-SYNTHETIC";
            Phase8UnityCommandResultV1 refuel = port.Execute(
                Phase8UnityInputCommandV1.RefuelChannel(
                    2,
                    channelIndex,
                    directionId,
                    shiftCount,
                    fuelTypeId));

            Assert.That(refuel.Accepted, Is.True, refuel.DiagnosticMessage);
            Assert.That(refuel.Kind, Is.EqualTo(Phase8UnityCommandKindV1.RefuelChannel));
            Assert.That(
                refuel.Snapshot.RefuellingOperationCount,
                Is.EqualTo(beforeRefuel.RefuellingOperationCount + 1));
            Assert.That(
                refuel.Snapshot.FreshBundlesAvailable,
                Is.EqualTo(beforeRefuel.FreshBundlesAvailable - shiftCount));
            Assert.That(refuel.Snapshot.LastRefuelledChannel, Is.EqualTo((int)channelIndex));
            Assert.That(refuel.Snapshot.LastRefuellingDirectionId, Is.EqualTo(directionId));
            Assert.That(refuel.Snapshot.LastRefuellingShiftCount, Is.EqualTo(shiftCount));
            Assert.That(refuel.Snapshot.Core, Is.Not.Null);
            Assert.That(refuel.Snapshot.Core.Xenon.SelectedChannelIndex, Is.EqualTo((int)channelIndex));
            Assert.That(refuel.Snapshot.Core.Xenon.SelectedChannel, Is.Not.Null);
            Assert.That(refuel.Snapshot.Core.Xenon.SelectedChannel.ChannelIndex, Is.EqualTo(channelIndex));
            StringAssert.Contains(
                "Channel 190 refuelled toward End B",
                refuel.Message);
            StringAssert.Contains(
                "4 NAT-U-SYNTHETIC bundles",
                refuel.Message);
        }

        [Test]
        public void CoreMapPersistsAcceptedOutcomeAndPreservesItOnRejection()
        {
            ControllerFixture fixture = CreateControllerFixture("refuelling-outcome");
            try
            {
                CoreMapView coreMap = fixture.Controller.CoreMap;
                Phase8UnityRuntimeAdapter adapter =
                    fixture.Controller.GetComponent<Phase8UnityRuntimeAdapter>();
                const int channelIndex = 190;
                const ushort shiftCount = 4;

                Assert.That(coreMap.SelectChannel(channelIndex), Is.True);
                Assert.That(coreMap.SelectTowardEndB(), Is.True);
                Phase8UnityPresentationSnapshotV1 before = adapter.Snapshot;

                Phase8UnityCommandResultV1 accepted = coreMap.RefuelSelected();

                Assert.That(accepted.Accepted, Is.True, accepted.DiagnosticMessage);
                Assert.That(coreMap.LastAcceptedRefuellingOutcome, Is.Not.Null);
                CoreMapRefuellingOutcome outcome = coreMap.LastAcceptedRefuellingOutcome;
                Assert.That(outcome.Sequence, Is.EqualTo(accepted.Sequence));
                Assert.That(outcome.ChannelIndex, Is.EqualTo(accepted.Snapshot.LastRefuelledChannel));
                Assert.That(outcome.DirectionId, Is.EqualTo(accepted.Snapshot.LastRefuellingDirectionId));
                Assert.That(outcome.ShiftCount, Is.EqualTo(accepted.Snapshot.LastRefuellingShiftCount));
                Assert.That(outcome.AuthoritativeMessage, Is.EqualTo(accepted.Message));
                Assert.That(
                    outcome.FreshBundlesSpent,
                    Is.EqualTo(before.FreshBundlesAvailable - accepted.Snapshot.FreshBundlesAvailable));
                Assert.That(
                    outcome.FreshBundlesRemaining,
                    Is.EqualTo(accepted.Snapshot.FreshBundlesAvailable));
                Assert.That(
                    outcome.ScoreDelta,
                    Is.EqualTo(accepted.Snapshot.ScoreTotal - before.ScoreTotal).Within(1e-12));
                Assert.That(
                    outcome.ActualPowerDelta,
                    Is.EqualTo(accepted.Snapshot.ActualPowerFraction - before.ActualPowerFraction)
                        .Within(1e-12));
                Assert.That(
                    outcome.RrsAverageReserveDelta,
                    Is.EqualTo(
                        accepted.Snapshot.Rrs.AverageFillFraction - before.Rrs.AverageFillFraction)
                        .Within(1e-12));
                Assert.That(
                    outcome.RrsHeadroomDelta,
                    Is.EqualTo(
                        Math.Min(
                            accepted.Snapshot.Rrs.AverageFillFraction,
                            1.0 - accepted.Snapshot.Rrs.AverageFillFraction) -
                        Math.Min(before.Rrs.AverageFillFraction, 1.0 - before.Rrs.AverageFillFraction))
                        .Within(1e-12));
                StringAssert.Contains(accepted.Message, coreMap.RefuelOutcomeText);

                Phase8UnityCommandResultV1 rejected = adapter.RefuelChannel(
                    (uint)channelIndex,
                    CoreMapView.TowardEndBDirectionId,
                    shiftCount,
                    "NOT-A-SUPPORTED-FUEL");

                Assert.That(rejected.Accepted, Is.False);
                Assert.That(coreMap.LastAcceptedRefuellingOutcome, Is.SameAs(outcome));
                StringAssert.Contains(rejected.DiagnosticCode, coreMap.RefuelFeedbackText);
                StringAssert.Contains(rejected.DiagnosticMessage, coreMap.RefuelFeedbackText);
                StringAssert.Contains(accepted.Message, coreMap.RefuelOutcomeText);

                Assert.That(fixture.Controller.RestartPracticeSession(), Is.True);
                Assert.That(coreMap.IsBound, Is.True);
                Assert.That(coreMap.LastAcceptedRefuellingOutcome, Is.Null);
                StringAssert.Contains("No accepted refuelling outcome", coreMap.RefuelOutcomeText);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void ControllerUsesFixedPacingRetainsRemainderAndCapsCatchUp()
        {
            ControllerFixture fixture = CreateControllerFixture("fixed-pacing");
            try
            {
                Assert.That(
                    fixture.Controller.RuntimePort.Snapshot.WallControlTickMilliseconds,
                    Is.EqualTo(100U));
                fixture.Controller.Tick(0.04);
                Assert.That(fixture.AdvanceResults, Is.Empty);

                fixture.Controller.Tick(0.06);
                Assert.That(fixture.AdvanceResults, Has.Count.EqualTo(1));
                Assert.That(
                    fixture.AdvanceResults[0].Snapshot.WallElapsedSeconds,
                    Is.EqualTo(0.1).Within(1e-9));
                Assert.That(
                    fixture.AdvanceResults[0].Snapshot.SimulationTimeSeconds,
                    Is.EqualTo(1.0).Within(1e-9));

                int beforeSpike = fixture.AdvanceResults.Count;
                fixture.Controller.Tick(1.0);

                Assert.That(
                    fixture.AdvanceResults.Count - beforeSpike,
                    Is.EqualTo(5));
                for (int index = beforeSpike; index < fixture.AdvanceResults.Count; index++)
                {
                    Phase8UnityCommandResultV1 result = fixture.AdvanceResults[index];
                    Assert.That(result.Accepted, Is.True, result.DiagnosticMessage);
                    Assert.That(result.Snapshot.WallControlTickMilliseconds, Is.EqualTo(100U));
                    Assert.That(
                        result.Snapshot.WallElapsedSeconds,
                        Is.EqualTo(0.1 * (index + 1)).Within(1e-9));
                }

                Assert.That(
                    fixture.Controller.RuntimePort.Snapshot.SimulationTimeSeconds,
                    Is.EqualTo(6.0).Within(1e-9));
                Assert.That(
                    fixture.Controller.RuntimePort.Snapshot.WallElapsedSeconds,
                    Is.EqualTo(0.6).Within(1e-9));
            }
            finally
            {
                fixture.Dispose();
            }

            ControllerFixture singleCall = CreateControllerFixture("partition-single-call");
            ControllerFixture partitioned = CreateControllerFixture("partitioned-calls");
            try
            {
                singleCall.Controller.Tick(0.5);
                for (int index = 0; index < 5; index++)
                {
                    partitioned.Controller.Tick(0.1);
                }

                Assert.That(singleCall.AdvanceResults, Has.Count.EqualTo(5));
                Assert.That(partitioned.AdvanceResults, Has.Count.EqualTo(5));
                AssertEquivalentVisibleSnapshots(
                    singleCall.Controller.RuntimePort.Snapshot,
                    partitioned.Controller.RuntimePort.Snapshot);
            }
            finally
            {
                singleCall.Dispose();
                partitioned.Dispose();
            }
        }

        private static ControllerFixture CreateControllerFixture(string name)
        {
            GameObject gameObject = new GameObject(
                "runtime-seam-and-pacing-" + name,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            try
            {
                gameObject.AddComponent<Phase10ShellView>();
                gameObject.AddComponent<Phase8UnityRuntimeAdapter>();
                gameObject.AddComponent<Phase10DashboardView>();
                gameObject.AddComponent<Phase10ControlsView>();
                gameObject.AddComponent<Phase10TimelineView>();
                UnityGameController controller = gameObject.AddComponent<UnityGameController>();
                controller.Initialize();
                Assert.That(controller.IsInitialized, Is.True);

                return new ControllerFixture(
                    gameObject,
                    controller,
                    gameObject.GetComponent<Phase8UnityRuntimeAdapter>());
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                throw;
            }
        }

        private static void AssertEquivalentVisibleSnapshots(
            Phase8UnityPresentationSnapshotV1 expected,
            Phase8UnityPresentationSnapshotV1 actual)
        {
            Assert.That(actual.SimulationTimeSeconds, Is.EqualTo(expected.SimulationTimeSeconds).Within(1e-9));
            Assert.That(actual.WallElapsedSeconds, Is.EqualTo(expected.WallElapsedSeconds).Within(1e-9));
            Assert.That(actual.NormalizedPowerFraction, Is.EqualTo(expected.NormalizedPowerFraction).Within(1e-12));
            Assert.That(actual.ActualPowerFraction, Is.EqualTo(expected.ActualPowerFraction).Within(1e-12));
            Assert.That(actual.AbsoluteTiltFraction, Is.EqualTo(expected.AbsoluteTiltFraction).Within(1e-12));
            Assert.That(actual.ControlMarginFraction, Is.EqualTo(expected.ControlMarginFraction).Within(1e-12));
            Assert.That(actual.DeviceAvailableFraction, Is.EqualTo(expected.DeviceAvailableFraction).Within(1e-12));
            Assert.That(actual.ScoreTotal, Is.EqualTo(expected.ScoreTotal).Within(1e-9));
            Assert.That(actual.TurnSummaryCount, Is.EqualTo(expected.TurnSummaryCount));
            Assert.That(actual.PendingActionCount, Is.EqualTo(expected.PendingActionCount));
            Assert.That(actual.RefuellingOperationCount, Is.EqualTo(expected.RefuellingOperationCount));
            Assert.That(actual.FreshBundlesAvailable, Is.EqualTo(expected.FreshBundlesAvailable));
            Assert.That(actual.OutcomeId, Is.EqualTo(expected.OutcomeId));
            Assert.That(actual.IsPaused, Is.EqualTo(expected.IsPaused));
            Assert.That(actual.Core.Xenon.StateDigestHex, Is.EqualTo(expected.Core.Xenon.StateDigestHex));
            Assert.That(
                actual.Core.Physics.ReactivityBindingDigestHex,
                Is.EqualTo(expected.Core.Physics.ReactivityBindingDigestHex));
            Assert.That(
                actual.Core.Physics.TotalPowerWatts,
                Is.EqualTo(expected.Core.Physics.TotalPowerWatts).Within(1e-6));
        }

        private sealed class ControllerFixture : IDisposable
        {
            private readonly GameObject _gameObject;

            internal ControllerFixture(
                GameObject gameObject,
                UnityGameController controller,
                Phase8UnityRuntimeAdapter adapter)
            {
                _gameObject = gameObject;
                Controller = controller;
                AdvanceResults = new List<Phase8UnityCommandResultV1>();
                adapter.CommandCompleted += OnCommandCompleted;
            }

            internal UnityGameController Controller { get; }

            internal List<Phase8UnityCommandResultV1> AdvanceResults { get; }

            public void Dispose()
            {
                if (_gameObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(_gameObject);
                }
            }

            private void OnCommandCompleted(Phase8UnityCommandResultV1 result)
            {
                if (result.Kind == Phase8UnityCommandKindV1.AdvanceWallMilliseconds)
                {
                    AdvanceResults.Add(result);
                }
            }
        }
    }
}
