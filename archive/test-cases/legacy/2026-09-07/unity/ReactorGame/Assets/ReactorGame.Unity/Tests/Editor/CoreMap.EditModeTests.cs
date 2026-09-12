using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ReactorGame.Unity.CoreMap
{
    public sealed class CoreMapEditModeTests
    {
        [Test]
        public void CoreMapRendersSelectsPreviewsAndCommitsAChannel()
        {
            GameObject gameObject = new GameObject(
                "core-map-edit-mode",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            try
            {
                gameObject.AddComponent<Phase10ShellView>();
                Phase8UnityRuntimeAdapter adapter =
                    gameObject.AddComponent<Phase8UnityRuntimeAdapter>();
                CoreMapView coreMap = gameObject.AddComponent<CoreMapView>();

                adapter.Bind(new UnityRuntimePort());
                coreMap.Bind(adapter);

                Assert.That(coreMap.IsBuilt, Is.True);
                Assert.That(coreMap.IsBound, Is.True);
                Assert.That(coreMap.ChannelButtonCount, Is.EqualTo(380));
                Assert.That(coreMap.BundleDetailCount, Is.EqualTo(12));
                Assert.That(coreMap.SelectedChannelIndex, Is.EqualTo(190));
                Assert.That(coreMap.SelectedChannelText, Does.Contain("Channel 190"));
                Assert.That(coreMap.Snapshot, Is.Not.Null);

                Button channelButton;
                Assert.That(coreMap.TryGetChannelButton(0, out channelButton), Is.True);
                Assert.That(channelButton.interactable, Is.True);
                channelButton.onClick.Invoke();
                Assert.That(coreMap.SelectedChannelIndex, Is.EqualTo(0));
                Assert.That(coreMap.SelectedChannelText, Does.Contain("Channel 0"));

                Phase8UnityCommandResultV1 preview = coreMap.PreviewTowardEndA();
                Assert.That(preview, Is.Not.Null);
                Assert.That(preview.Accepted, Is.True, preview.DiagnosticMessage);
                Assert.That(preview.Message, Does.Contain("Preview: Channel 0"));
                Assert.That(coreMap.PreviewText, Does.Contain("Preview: Channel 0"));

                Phase8UnityCommandResultV1 commit = coreMap.CommitTowardEndA();
                Assert.That(commit.Accepted, Is.True, commit.DiagnosticMessage);
                Assert.That(commit.Snapshot.RefuellingOperationCount, Is.EqualTo(1));
                Assert.That(commit.Snapshot.FreshBundlesAvailable, Is.EqualTo(124));
                Assert.That(coreMap.PreviewText, Does.Contain("committed"));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
