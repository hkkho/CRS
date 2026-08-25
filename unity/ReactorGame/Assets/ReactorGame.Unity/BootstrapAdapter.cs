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
        private const string TimelineMarkerPathArgument = "-canduTimelineSmokeMarkerPath";
        private const string TimelineMarkerTokenArgument = "-canduTimelineSmokeMarkerToken";

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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RunNormalPlayerTimelineSmokeWhenRequested()
        {
            string markerPath = GetCommandLineArgument(TimelineMarkerPathArgument);
            string markerToken = GetCommandLineArgument(TimelineMarkerTokenArgument);
            if (markerPath == null && markerToken == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(markerPath) ||
                string.IsNullOrWhiteSpace(markerToken))
            {
                throw new InvalidOperationException(
                    $"{TimelineMarkerPathArgument} and {TimelineMarkerTokenArgument} must be provided together.");
            }

            Phase10ShellView shell = FindFirstObjectByType<Phase10ShellView>();
            Phase10TimelineView timeline = FindFirstObjectByType<Phase10TimelineView>();
            if (shell == null || timeline == null)
            {
                throw new InvalidOperationException(
                    "The normal-player Timeline smoke could not find the Bootstrap shell and Timeline view.");
            }

            shell.BuildVisualShell();
            if (!timeline.IsBuilt ||
                !shell.TryGetPageRoot(Phase10ShellPageV1.Timeline, out _))
            {
                throw new InvalidOperationException(
                    "The normal-player Timeline smoke found an unbuilt Timeline surface.");
            }

            File.WriteAllText(markerPath, markerToken + "\nTimelineBuilt");
            Debug.Log("P10-T05 normal-player graphical Timeline smoke passed.");
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
