using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ReactorGame.Unity.Milestone1
{
    public sealed class Milestone1RuntimeEditModeTests
    {
        [Test]
        public void ControllerBindsRealSessionAndAdvancesOneControlTick()
        {
            GameObject gameObject = new GameObject(
                "milestone-1-runtime",
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
                Assert.That(controller.RuntimePort.Snapshot.SimulationTimeSeconds, Is.EqualTo(0.0));

                controller.Tick(0.1);

                Assert.That(controller.RuntimePort.Snapshot.SimulationTimeSeconds, Is.EqualTo(1.0));
                Assert.That(controller.RuntimePort.Snapshot.WallElapsedSeconds, Is.EqualTo(0.1));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void RuntimePortMapsPlayerRefuellingCommandAndSnapshot()
        {
            UnityRuntimePort port = new UnityRuntimePort();

            Phase8UnityCommandResultV1 result = port.Execute(
                Phase8UnityInputCommandV1.RefuelChannel(
                    1,
                    190,
                    Phase10ControlsView.TowardEndBDirectionId,
                    4,
                    "NAT-U-SYNTHETIC"));

            Assert.That(result.Accepted, Is.True, result.DiagnosticMessage);
            Assert.That(result.Snapshot.RefuellingOperationCount, Is.EqualTo(1));
            Assert.That(result.Snapshot.FreshBundlesAvailable, Is.EqualTo(124));
            Assert.That(result.Snapshot.LastRefuelledChannel, Is.EqualTo(190));
            Assert.That(result.Message, Does.Contain("Channel 190 refuelled toward End B"));
        }
    }
}
