using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ReactorGame.Unity.P10T01
{
    public sealed class P10T01EditModeTests
    {
        [Test]
        public void AdapterForwardsExplicitWallTimeAndPublishesSnapshot()
        {
            GameObject gameObject = new GameObject("P10-T01-adapter-edit-mode");
            try
            {
                Phase8UnityRuntimeAdapter adapter = gameObject.AddComponent<Phase8UnityRuntimeAdapter>();
                FakeRuntimePort port = new FakeRuntimePort(CreateSnapshot());
                int snapshotNotifications = 0;
                adapter.SnapshotChanged += snapshot => snapshotNotifications++;

                adapter.Bind(port);
                Phase8UnityCommandResultV1 result = adapter.AdvanceWallMilliseconds(100);

                Assert.That(result.Accepted, Is.True);
                Assert.That(result.Kind, Is.EqualTo(Phase8UnityCommandKindV1.AdvanceWallMilliseconds));
                Assert.That(result.Sequence, Is.EqualTo(1UL));
                Assert.That(port.Commands.Count, Is.EqualTo(1));
                Assert.That(port.Commands[0].WallMilliseconds, Is.EqualTo(100UL));
                Assert.That(adapter.Snapshot, Is.SameAs(port.Snapshot));
                Assert.That(snapshotNotifications, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void DuplicateSequenceIsRejectedBeforeRuntimeExecution()
        {
            GameObject gameObject = new GameObject("P10-T01-duplicate-edit-mode");
            try
            {
                Phase8UnityRuntimeAdapter adapter = gameObject.AddComponent<Phase8UnityRuntimeAdapter>();
                FakeRuntimePort port = new FakeRuntimePort(CreateSnapshot());
                adapter.Bind(port);
                Phase8UnityInputCommandV1 command = Phase8UnityInputCommandV1.Pause(42);

                Phase8UnityCommandResultV1 first = adapter.Dispatch(command);
                Phase8UnityCommandResultV1 second = adapter.Dispatch(command);

                Assert.That(first.Accepted, Is.True);
                Assert.That(second.Accepted, Is.False);
                Assert.That(second.DiagnosticCode, Is.EqualTo("UnityAdapter.Command.DuplicateSequence"));
                Assert.That(port.Commands.Count, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void AcceptedResultRequiresAValidSnapshot()
        {
            Phase8UnityInputCommandV1 command = Phase8UnityInputCommandV1.Pause(1);

            Assert.That(
                () => Phase8UnityCommandResultV1.AcceptedResult(command, null),
                Throws.ArgumentNullException.With.Property("ParamName").EqualTo("snapshot"));
        }

        private static Phase8UnityPresentationSnapshotV1 CreateSnapshot()
        {
            return new Phase8UnityPresentationSnapshotV1(
                "p8-t02-tutorial-startup",
                "tutorial",
                "default-10x",
                10.0,
                100,
                600.0,
                0.0,
                0.0,
                0.95,
                0.10,
                1.0,
                1.0,
                2,
                0,
                0,
                0.0,
                0,
                "Running",
                false);
        }

        private sealed class FakeRuntimePort : IPhase8RuntimePort
        {
            public FakeRuntimePort(Phase8UnityPresentationSnapshotV1 snapshot)
            {
                Snapshot = snapshot;
            }

            public Phase8UnityPresentationSnapshotV1 Snapshot { get; }

            public List<Phase8UnityInputCommandV1> Commands { get; } =
                new List<Phase8UnityInputCommandV1>();

            public Phase8UnityCommandResultV1 Execute(Phase8UnityInputCommandV1 command)
            {
                Commands.Add(command);
                return Phase8UnityCommandResultV1.AcceptedResult(command, Snapshot);
            }
        }
    }
}
