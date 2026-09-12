using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ReactorGame.Unity.DebugMenu
{
    public sealed class DebugMenuPlayModeTests
    {
        [UnityTest]
        public IEnumerator BootstrapSceneContainsHiddenBoundDebugMenu()
        {
            SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
            yield return null;

            UnityGameController controller =
                Object.FindFirstObjectByType<UnityGameController>();
            DebugMenuView debugMenu = Object.FindFirstObjectByType<DebugMenuView>();

            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.IsInitialized, Is.True);
            Assert.That(debugMenu, Is.Not.Null);
            Assert.That(debugMenu.IsBuilt, Is.True);
            Assert.That(debugMenu.IsBound, Is.True);
            Assert.That(debugMenu.IsVisible, Is.False);
            Assert.That(debugMenu.ActionButtonCount, Is.EqualTo(14));

            debugMenu.ToggleVisibility();
            Assert.That(debugMenu.IsVisible, Is.True);
            Assert.That(debugMenu.StatusText, Does.Contain("bound"));
            debugMenu.ToggleVisibility();
            Assert.That(debugMenu.IsVisible, Is.False);
        }
    }
}
