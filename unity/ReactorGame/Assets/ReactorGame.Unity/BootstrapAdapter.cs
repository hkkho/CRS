using System;
using System.IO;
using ReactorSim.Core;
using UnityEngine;

namespace ReactorGame.Unity
{
    public sealed class BootstrapAdapter : MonoBehaviour
    {
        private const string MarkerPathArgument = "-canduSerializationSmokeMarkerPath";
        private const string MarkerTokenArgument = "-canduSerializationSmokeMarkerToken";

        public static string RunSerializationCompatibilityProbe()
        {
            return JsonSerializationCompatibilityProbe.Run();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void RunPlayerSerializationSmokeWhenRequested()
        {
            string markerPath = GetCommandLineArgument(MarkerPathArgument);
            string markerToken = GetCommandLineArgument(MarkerTokenArgument);
            if (markerPath == null && markerToken == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(markerPath) || string.IsNullOrWhiteSpace(markerToken))
            {
                throw new InvalidOperationException(
                    $"{MarkerPathArgument} and {MarkerTokenArgument} must be provided together.");
            }

            string payload = RunSerializationCompatibilityProbe();
            File.WriteAllText(markerPath, markerToken + "\n" + payload);
            Debug.Log("P0-T08 built-player serialization execution probe passed.");
            Application.Quit(0);
        }

        private static string GetCommandLineArgument(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, name);
            return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
        }
    }
}
