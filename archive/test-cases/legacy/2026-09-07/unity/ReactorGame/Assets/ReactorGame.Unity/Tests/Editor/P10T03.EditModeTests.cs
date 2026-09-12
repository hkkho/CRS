using NUnit.Framework;
using UnityEngine;

namespace ReactorGame.Unity.P10T03
{
    public sealed class P10T03EditModeTests
    {
        [Test]
        public void DashboardBindsRendersAndUnsubscribesFromSnapshots()
        {
            GameObject gameObject = new GameObject("P10-T03-dashboard-edit-mode");
            try
            {
                Phase10ShellView shell = gameObject.AddComponent<Phase10ShellView>();
                shell.BuildVisualShell();
                Phase8UnityRuntimeAdapter adapter =
                    gameObject.AddComponent<Phase8UnityRuntimeAdapter>();
                FakeRuntimePort port = new FakeRuntimePort(CreateSnapshot(
                    "p10-t03-start",
                    0.95,
                    0.0,
                    0.0,
                    10.0,
                    "Running",
                    false));
                adapter.Bind(port);

                Phase10DashboardView view =
                    gameObject.AddComponent<Phase10DashboardView>();
                view.Bind(adapter);

                Assert.That(view.IsBuilt, Is.True);
                Assert.That(view.IsBound, Is.True);
                Assert.That(view.Snapshot, Is.SameAs(port.Snapshot));
                StringAssert.Contains("p10-t03-start", view.ScenarioText);
                StringAssert.Contains("x10", view.PacingText);
                StringAssert.Contains("100 ms", view.PacingText);
                StringAssert.Contains("95.0%", view.PowerText);
                StringAssert.Contains("Running", view.OutcomeText);

                Phase8UnityPresentationSnapshotV1 updated = CreateSnapshot(
                    "p10-t03-updated",
                    0.80,
                    12.5,
                    1.25,
                    12.0,
                    "Complete",
                    true);
                port.SetSnapshot(updated);
                Assert.That(adapter.TryRefreshSnapshot(out string diagnostic), Is.True, diagnostic);
                Assert.That(view.Snapshot, Is.SameAs(updated));
                StringAssert.Contains("p10-t03-updated", view.ScenarioText);
                StringAssert.Contains("80.0%", view.PowerText);
                StringAssert.Contains("Complete", view.OutcomeText);
                StringAssert.Contains("Paused", view.PlaybackText);

                view.Unbind();
                Assert.That(view.IsBound, Is.False);
                Assert.That(view.Snapshot, Is.Null);
                StringAssert.Contains("unavailable", view.OutcomeText);
                adapter.Unbind();
                port.SetSnapshot(CreateSnapshot(
                    "p10-t03-unbound",
                    0.50,
                    20.0,
                    2.0,
                    14.0,
                    "Running",
                    false));
                Assert.That(adapter.TryRefreshSnapshot(out diagnostic), Is.False);
                Assert.That(diagnostic, Does.Contain("no bound runtime port"));
                Assert.That(view.IsBound, Is.False);
                Assert.That(view.Snapshot, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void DashboardRequiresAValidAdapterSnapshot()
        {
            GameObject gameObject = new GameObject("P10-T03-dashboard-invalid-edit-mode");
            try
            {
                gameObject.AddComponent<Phase10ShellView>().BuildVisualShell();
                Phase8UnityRuntimeAdapter adapter =
                    gameObject.AddComponent<Phase8UnityRuntimeAdapter>();
                Phase10DashboardView view =
                    gameObject.AddComponent<Phase10DashboardView>();

                Assert.That(
                    () => view.Bind(adapter),
                    Throws.InvalidOperationException);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private static Phase8UnityPresentationSnapshotV1 CreateSnapshot(
            string scenarioId,
            double normalizedPower,
            double simulationTime,
            double wallTime,
            double score,
            string outcomeId,
            bool paused)
        {
            return new Phase8UnityPresentationSnapshotV1(
                scenarioId,
                "tutorial",
                "default-10x",
                10.0,
                100,
                600.0,
                simulationTime,
                wallTime,
                normalizedPower,
                0.10,
                1.0,
                0.98,
                2,
                1,
                3,
                score,
                4,
                outcomeId,
                paused);
        }

        private sealed class FakeRuntimePort : IPhase8RuntimePort
        {
            public FakeRuntimePort(Phase8UnityPresentationSnapshotV1 snapshot)
            {
                Snapshot = snapshot;
            }

            public Phase8UnityPresentationSnapshotV1 Snapshot { get; private set; }

            public void SetSnapshot(Phase8UnityPresentationSnapshotV1 snapshot)
            {
                Snapshot = snapshot;
            }

            public Phase8UnityCommandResultV1 Execute(Phase8UnityInputCommandV1 command)
            {
                return Phase8UnityCommandResultV1.AcceptedResult(command, Snapshot);
            }
        }
    }
}
