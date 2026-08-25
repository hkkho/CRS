using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ReactorGame.Unity.P10T01
{
    public sealed class P10T01PlayModeTests
    {
        [UnityTest]
        public IEnumerator AdapterRequiresExplicitCommandToAdvance()
        {
            GameObject gameObject = new GameObject("P10-T01-adapter-play-mode");
            try
            {
                Phase8UnityRuntimeAdapter adapter = gameObject.AddComponent<Phase8UnityRuntimeAdapter>();
                FakeRuntimePort port = new FakeRuntimePort(CreateSnapshot());
                adapter.Bind(port);

                yield return null;

                Assert.That(port.Commands.Count, Is.EqualTo(0));
                Assert.That(adapter.Snapshot.SimulationTimeSeconds, Is.EqualTo(0.0));

                Phase8UnityCommandResultV1 result = adapter.QueuePowerTarget(0.95);

                Assert.That(result.Accepted, Is.True);
                Assert.That(result.Kind, Is.EqualTo(Phase8UnityCommandKindV1.QueuePowerTarget));
                Assert.That(port.Commands.Count, Is.EqualTo(1));
                Assert.That(port.Commands[0].TargetFraction, Is.EqualTo(0.95));
            }
            finally
            {
                Object.Destroy(gameObject);
            }
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
