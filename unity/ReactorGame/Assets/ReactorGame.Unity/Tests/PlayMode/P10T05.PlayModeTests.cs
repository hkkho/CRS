using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ReactorGame.Unity.P10T05
{
    public sealed class P10T05PlayModeTests
    {
        [UnityTest]
        public IEnumerator BootstrapSceneContainsGraphicalTimelineSurface()
        {
            SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
            yield return null;

            Phase10ShellView shell = Object.FindFirstObjectByType<Phase10ShellView>();
            Phase10TimelineView timeline = Object.FindFirstObjectByType<Phase10TimelineView>();

            Assert.That(shell, Is.Not.Null);
            Assert.That(timeline, Is.Not.Null);
            Assert.That(timeline.IsBuilt, Is.True);
            Assert.That(timeline.IsBound, Is.True);
            Assert.That(timeline.SnapshotCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(timeline.StatusText, Does.Contain("Status: bound"));
            Assert.That(
                shell.TryGetPageRoot(Phase10ShellPageV1.Timeline, out _),
                Is.True);
            Assert.That(shell.NavigateTo(Phase10ShellPageV1.Timeline), Is.True);
            Assert.That(shell.ActivePage, Is.EqualTo(Phase10ShellPageV1.Timeline));
        }
    }
}
