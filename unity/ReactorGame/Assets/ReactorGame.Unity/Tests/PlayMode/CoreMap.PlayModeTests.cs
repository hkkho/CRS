using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ReactorGame.Unity.CoreMap
{
    public sealed class CoreMapPlayModeTests
    {
        [UnityTest]
        public IEnumerator BootstrapSceneContainsPlayableCoreMap()
        {
            SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
            yield return null;

            Phase10ShellView shell = Object.FindFirstObjectByType<Phase10ShellView>();
            UnityGameController controller =
                Object.FindFirstObjectByType<UnityGameController>();
            CoreMapView coreMap = Object.FindFirstObjectByType<CoreMapView>();

            Assert.That(shell, Is.Not.Null);
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.IsInitialized, Is.True);
            Assert.That(coreMap, Is.Not.Null);
            Assert.That(coreMap.IsBuilt, Is.True);
            Assert.That(coreMap.IsBound, Is.True);
            Assert.That(coreMap.ChannelButtonCount, Is.EqualTo(380));
            Assert.That(coreMap.BundleDetailCount, Is.EqualTo(12));
            Assert.That(coreMap.SelectedChannelIndex, Is.EqualTo(190));
            Assert.That(shell.NavigateTo(Phase10ShellPageV1.CoreMap), Is.True);
            Assert.That(shell.ActivePage, Is.EqualTo(Phase10ShellPageV1.CoreMap));

            Assert.That(coreMap.SelectChannel(190), Is.True);
            Phase8UnityCommandResultV1 preview = coreMap.PreviewTowardEndB();
            Assert.That(preview.Accepted, Is.True, preview.DiagnosticMessage);
            Assert.That(preview.Message, Does.Contain("Preview: Channel 190"));

            Phase8UnityCommandResultV1 commit = coreMap.CommitTowardEndB();
            Assert.That(commit.Accepted, Is.True, commit.DiagnosticMessage);
            Assert.That(commit.Snapshot.RefuellingOperationCount, Is.EqualTo(1));
            Assert.That(coreMap.StatusText, Does.Contain("accepted"));
        }
    }
}
