using System.Collections;
using NUnit.Framework;
using ReactorGame.Unity;
using UnityEngine;
using UnityEngine.TestTools;

namespace ReactorGame.Unity.G2C01
{
    public sealed class G2C01PlayModeTests
    {
        private const string ExpectedSerializationProbe = "{\"probe\":\"reactor-sim\",\"value\":42}";

        [UnityTest]
        public IEnumerator RuntimeAndSerializationProbeAreAvailable()
        {
            yield return null;

            Assert.That(Application.isPlaying, Is.True);
            Assert.That(
                BootstrapAdapter.RunSerializationCompatibilityProbe(),
                Is.EqualTo(ExpectedSerializationProbe));
        }
    }
}
