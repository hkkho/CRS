using NUnit.Framework;
using ReactorGame.Unity;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace ReactorGame.Unity.G2C01
{
    public sealed class G2C01EditModeTests
    {
        private const string ExpectedSerializationProbe = "{\"probe\":\"reactor-sim\",\"value\":42}";
        private const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";

        [Test]
        public void BootstrapSceneAndSerializationProbeAreAvailable()
        {
            Assert.That(
                BootstrapAdapter.RunSerializationCompatibilityProbe(),
                Is.EqualTo(ExpectedSerializationProbe));

            Scene scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            Assert.That(scene.IsValid(), Is.True);
            Assert.That(scene.isLoaded, Is.True);
            Assert.That(scene.path, Is.EqualTo(BootstrapScenePath));
        }
    }
}
