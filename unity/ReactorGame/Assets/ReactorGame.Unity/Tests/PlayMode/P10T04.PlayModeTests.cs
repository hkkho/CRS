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
            Assert.That(adapter.IsBound, Is.True);
            Assert.That(controls.IsBound, Is.True);
            Assert.That(controls.IsBuilt, Is.True);
            Assert.That(
                controls.StatusText,
                Does.Contain("Controls ready"));
            InputField[] inputs = controls.GetComponentsInChildren<InputField>(true);
            Assert.That(inputs, Has.Length.EqualTo(5));
            Assert.That(inputs[0].text, Is.EqualTo("0.95"));
            Assert.That(inputs[1].text, Is.EqualTo("0.1"));
            Assert.That(inputs[2].text, Is.EqualTo("190"));
            Assert.That(inputs[3].text, Is.EqualTo("4"));
            Assert.That(inputs[4].text, Is.EqualTo("NAT-U-SYNTHETIC"));
            Button advanceButton;
            Assert.That(
                controls.TryGetActionButton(
                    Phase10ControlActionV1.AdvanceControlTick,
                    out advanceButton),
                Is.True);
            Assert.That(advanceButton.interactable, Is.True);
            Assert.That(
                shell.TryGetPageRoot(Phase10ShellPageV1.Controls, out _),
                Is.True);
        }
    }
}
