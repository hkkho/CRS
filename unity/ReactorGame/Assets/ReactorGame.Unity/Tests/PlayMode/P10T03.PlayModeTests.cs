using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ReactorGame.Unity.P10T03
{
    public sealed class P10T03PlayModeTests
    {
        [UnityTest]
        public IEnumerator BootstrapSceneContainsExplicitDashboardBindingPoint()
        {
            SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
            yield return null;

            Phase10ShellView shell = Object.FindFirstObjectByType<Phase10ShellView>();
            Phase8UnityRuntimeAdapter adapter =
                Object.FindFirstObjectByType<Phase8UnityRuntimeAdapter>();
            Phase10DashboardView view =
                Object.FindFirstObjectByType<Phase10DashboardView>();

            Assert.That(shell, Is.Not.Null);
            Assert.That(adapter, Is.Not.Null);
            Assert.That(view, Is.Not.Null);
            Assert.That(adapter.IsBound, Is.False);
            Assert.That(view.IsBound, Is.False);
            Assert.That(shell.TryGetPageRoot(
                Phase10ShellPageV1.Dashboard,
                out RectTransform dashboardRoot), Is.True);
            Assert.That(dashboardRoot, Is.Not.Null);
        }
    }
}
