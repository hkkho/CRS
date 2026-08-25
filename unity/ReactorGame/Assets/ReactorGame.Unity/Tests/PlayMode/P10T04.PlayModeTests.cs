using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ReactorGame.Unity.P10T04
{
    public sealed class P10T04PlayModeTests
    {
        [UnityTest]
        public IEnumerator BootstrapSceneContainsGraphicalControlsSurface()
        {
            SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
            yield return null;

            Phase10ShellView shell = Object.FindFirstObjectByType<Phase10ShellView>();
            Phase8UnityRuntimeAdapter adapter = Object.FindFirstObjectByType<Phase8UnityRuntimeAdapter>();
            Phase10ControlsView controls = Object.FindFirstObjectByType<Phase10ControlsView>();

            Assert.That(shell, Is.Not.Null);
            Assert.That(adapter, Is.Not.Null);
            Assert.That(controls, Is.Not.Null);
            Assert.That(adapter.IsBound, Is.False);
            Assert.That(controls.IsBound, Is.False);
            Assert.That(controls.IsBuilt, Is.True);
            Assert.That(
                controls.StatusText,
                Does.Contain("waiting for runtime binding"));
            Button advanceButton;
            Assert.That(
                controls.TryGetActionButton(
                    Phase10ControlActionV1.AdvanceControlTick,
                    out advanceButton),
                Is.True);
            Assert.That(advanceButton.interactable, Is.False);
            Assert.That(
                shell.TryGetPageRoot(Phase10ShellPageV1.Controls, out _),
                Is.True);
        }
    }
}
