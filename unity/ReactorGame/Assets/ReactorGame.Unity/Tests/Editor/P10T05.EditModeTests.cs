using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ReactorGame.Unity.P10T05
{
    public sealed class P10T05EditModeTests
    {
        [Test]
        public void TimelineBindsRendersHistoryAndCommandEvents()
        {
            GameObject gameObject = new GameObject("P10-T05-timeline-edit-mode");
            try
            {
                gameObject.AddComponent<Phase10ShellView>();
                Phase8UnityRuntimeAdapter adapter =
                    gameObject.AddComponent<Phase8UnityRuntimeAdapter>();
                Phase10TimelineView timeline =
                    gameObject.AddComponent<Phase10TimelineView>();
                FakeRuntimePort port = new FakeRuntimePort(CreateSnapshot(12.0, 0.95));

                adapter.Bind(port);
                timeline.Bind(adapter);

                Assert.That(timeline.IsBuilt, Is.True);
                Assert.That(timeline.IsBound, Is.True);
                Assert.That(timeline.SnapshotCount, Is.EqualTo(1));
                Assert.That(timeline.PlotBarCount, Is.EqualTo(3));
                Assert.That(timeline.StatusText, Does.Contain("bound"));
                Assert.That(timeline.SummaryText, Does.Contain("Samples: 1/24"));

                port.Snapshot = CreateSnapshot(13.0, 0.91);
                Assert.That(adapter.TryRefreshSnapshot(out string diagnostic), Is.True, diagnostic);
                Assert.That(timeline.SnapshotCount, Is.EqualTo(2));
                Assert.That(timeline.PlotBarCount, Is.EqualTo(6));

                Phase8UnityCommandResultV1 result = adapter.AdvanceWallMilliseconds(100);
                Assert.That(result.Accepted, Is.True);
                Assert.That(timeline.EventCount, Is.EqualTo(1));
                Assert.That(timeline.SnapshotCount, Is.EqualTo(3));

                port.RejectCommands = true;
                result = adapter.AdvanceWallMilliseconds(100);
                Assert.That(result.Accepted, Is.False);
                Assert.That(timeline.EventCount, Is.EqualTo(2));
                Assert.That(timeline.EventLogText, Does.Contain("Fake.Rejected"));
                Assert.That(timeline.EventLogText, Does.Contain("The test runtime rejected this command."));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void RebindingDoesNotDuplicateSnapshotOrCommandListeners()
        {
            GameObject gameObject = new GameObject("P10-T05-duplicate-edit-mode");
            try
            {
                gameObject.AddComponent<Phase10ShellView>();
                Phase8UnityRuntimeAdapter adapter =
                    gameObject.AddComponent<Phase8UnityRuntimeAdapter>();
                Phase10TimelineView timeline =
                    gameObject.AddComponent<Phase10TimelineView>();
                FakeRuntimePort port = new FakeRuntimePort(CreateSnapshot(1.0, 0.8));

                adapter.Bind(port);
                timeline.Bind(adapter);
                timeline.Bind(adapter);

                port.Snapshot = CreateSnapshot(2.0, 0.82);
                Assert.That(adapter.TryRefreshSnapshot(out string diagnostic), Is.True, diagnostic);
                Assert.That(timeline.SnapshotCount, Is.EqualTo(2));

                Assert.That(adapter.AdvanceWallMilliseconds(100).Accepted, Is.True);
                Assert.That(timeline.EventCount, Is.EqualTo(1));

                timeline.Unbind();
                port.Snapshot = CreateSnapshot(3.0, 0.84);
                Assert.That(adapter.TryRefreshSnapshot(out diagnostic), Is.True, diagnostic);
                Assert.That(timeline.SnapshotCount, Is.EqualTo(0));
                Assert.That(timeline.EventCount, Is.EqualTo(0));
                Assert.That(timeline.StatusText, Does.Contain("waiting"));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void TimelineHistoryIsBoundedAndInvalidBindingFailsClosed()
        {
            GameObject gameObject = new GameObject("P10-T05-capacity-edit-mode");
            try
            {
                gameObject.AddComponent<Phase10ShellView>();
                Phase8UnityRuntimeAdapter adapter =
                    gameObject.AddComponent<Phase8UnityRuntimeAdapter>();
                Phase10TimelineView timeline =
                    gameObject.AddComponent<Phase10TimelineView>();
                FakeRuntimePort port = new FakeRuntimePort(CreateSnapshot(0.0, 0.5));

                adapter.Bind(port);
                timeline.Bind(adapter);
                for (int index = 1; index <= Phase10TimelineView.DefaultSnapshotCapacity + 5; index++)
                {
                    port.Snapshot = CreateSnapshot(index, 0.5 + index * 0.01);
                    Assert.That(adapter.TryRefreshSnapshot(out string diagnostic), Is.True, diagnostic);
                }

                Assert.That(
                    timeline.SnapshotCount,
                    Is.EqualTo(Phase10TimelineView.DefaultSnapshotCapacity));
                Assert.That(timeline.PlotBarCount, Is.EqualTo(3 * Phase10TimelineView.DefaultSnapshotCapacity));

                GameObject invalidObject = new GameObject("P10-T05-invalid-bind-edit-mode");
                try
                {
                    Phase8UnityRuntimeAdapter invalidAdapter =
                        invalidObject.AddComponent<Phase8UnityRuntimeAdapter>();
                    Phase10TimelineView invalidTimeline =
                        invalidObject.AddComponent<Phase10TimelineView>();
                    Assert.That(
                        () => invalidTimeline.Bind(invalidAdapter),
                        Throws.InvalidOperationException.With.Message.Contains("requires a bound adapter"));
                }
                finally
                {
                    Object.DestroyImmediate(invalidObject);
                }
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        private static Phase8UnityPresentationSnapshotV1 CreateSnapshot(
            double simulationTime,
            double power)
        {
            return new Phase8UnityPresentationSnapshotV1(
                "p8-t02-tutorial-startup",
                "tutorial",
                "play-accelerated-10x",
                10.0,
                100,
                600.0,
                simulationTime,
                simulationTime / 10.0,
                power,
                0.10,
                0.75,
                1.0,
                2,
                0,
                1,
                913.667,
                1,
                "Running",
                false);
        }

        private sealed class FakeRuntimePort : IPhase8RuntimePort
        {
            public FakeRuntimePort(Phase8UnityPresentationSnapshotV1 snapshot)
            {
                Snapshot = snapshot;
            }

            public Phase8UnityPresentationSnapshotV1 Snapshot { get; set; }

            public List<Phase8UnityInputCommandV1> Commands { get; } =
                new List<Phase8UnityInputCommandV1>();

            public bool RejectCommands { get; set; }

            public Phase8UnityCommandResultV1 Execute(Phase8UnityInputCommandV1 command)
            {
                Commands.Add(command);
                if (RejectCommands)
                {
                    return Phase8UnityCommandResultV1.RejectedResult(
                        command,
                        "Fake.Rejected",
                        "The test runtime rejected this command.",
                        Snapshot);
                }

                return Phase8UnityCommandResultV1.AcceptedResult(command, Snapshot);
            }
        }
    }
}
