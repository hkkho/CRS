using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ReactorGame.Unity.DebugMenu
{
    public sealed class DebugMenuEditModeTests
    {
        [Test]
        public void DebugMenuBindsTogglesAndForwardsOwnerActions()
        {
            GameObject gameObject = new GameObject(
                "debug-menu-edit-mode",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            try
            {
                gameObject.AddComponent<Phase10ShellView>();
                Phase8UnityRuntimeAdapter adapter =
                    gameObject.AddComponent<Phase8UnityRuntimeAdapter>();
                DebugMenuView debugMenu = gameObject.AddComponent<DebugMenuView>();

                adapter.Bind(new UnityRuntimePort());
                debugMenu.Bind(adapter);

                Assert.That(debugMenu.IsBuilt, Is.True);
                Assert.That(debugMenu.IsBound, Is.True);
                Assert.That(debugMenu.IsVisible, Is.False);
                Assert.That(debugMenu.ActionButtonCount, Is.EqualTo(14));
                Assert.That(debugMenu.StatusText, Does.Contain("bound"));
                Assert.That(debugMenu.DigestText, Does.Contain("seed=1001"));

                debugMenu.ToggleVisibility();
                Assert.That(debugMenu.IsVisible, Is.True);
                debugMenu.ToggleVisibility();
                Assert.That(debugMenu.IsVisible, Is.False);

                uint initialInventory = adapter.Snapshot.FreshBundlesAvailable;
                Phase8UnityCommandResultV1 grant = debugMenu.GrantFreshBundles();
                Assert.That(grant.Accepted, Is.True, grant.DiagnosticMessage);
                Assert.That(grant.Snapshot.FreshBundlesAvailable,
                    Is.EqualTo(initialInventory + DebugMenuView.GrantFuelBundleCount));

                Phase8UnityCommandResultV1 playback = debugMenu.SetPlayback60x();
                Assert.That(playback.Accepted, Is.True, playback.DiagnosticMessage);
                Assert.That(playback.Snapshot.AccelerationFactor, Is.EqualTo(60.0));
                Assert.That(debugMenu.CopyStateDigest(), Is.True);
                Assert.That(GUIUtility.systemCopyBuffer, Does.Contain("mode=debug-accelerated-60x"));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
