using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ReactorGame.Unity.P10T02
{
    public sealed class P10T02PlayModeTests
    {
        [UnityTest]
        public IEnumerator BootstrapSceneContainsResponsiveShellAndNavigation()
        {
            SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
            yield return null;

            Phase10ShellView shell = Object.FindFirstObjectByType<Phase10ShellView>();
            Assert.That(shell, Is.Not.Null);
            Assert.That(shell.IsBuilt, Is.True);
            Assert.That(shell.ActivePage, Is.EqualTo(Phase10ShellPageV1.Dashboard));

            Assert.That(shell.NavigateTo(Phase10ShellPageV1.CoreMap), Is.True);
            yield return null;

            Assert.That(shell.ActivePage, Is.EqualTo(Phase10ShellPageV1.CoreMap));
            Assert.That(shell.TryGetNavigationButton(
                Phase10ShellPageV1.CoreMap,
                out _), Is.True);
        }
    }
}
