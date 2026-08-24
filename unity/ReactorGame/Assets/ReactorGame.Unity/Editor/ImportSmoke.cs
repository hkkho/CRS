using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ReactorGame.Unity.Editor
{
    public static class ImportSmoke
    {
        private const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";

        public static void Run()
        {
            string markerPath = GetRequiredCommandLineArgument("-canduSmokeMarkerPath");
            string markerToken = GetRequiredCommandLineArgument("-canduSmokeMarkerToken");

            Assembly coreAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .SingleOrDefault(assembly => assembly.GetName().Name == "ReactorSim.Core")
                ?? Assembly.Load("ReactorSim.Core");

            if (coreAssembly.GetName().Name != "ReactorSim.Core")
            {
                throw new InvalidOperationException("The ReactorSim.Core plugin did not load.");
            }

            if (typeof(BootstrapAdapter).Assembly.GetName().Name != "ReactorGame.Unity")
            {
                throw new InvalidOperationException("The ReactorGame.Unity adapter assembly did not compile.");
            }

            string serializationPayload = BootstrapAdapter.RunSerializationCompatibilityProbe();
            if (!string.Equals(
                    serializationPayload,
                    "{\"probe\":\"reactor-sim\",\"value\":42}",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The Core JSON token execution probe failed.");
            }

            Scene scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new InvalidOperationException("The bootstrap scene did not open.");
            }

            File.WriteAllText(markerPath, markerToken);
            Debug.Log("P0-T08 Unity import/compile/serialization smoke passed.");
        }

        private static string GetRequiredCommandLineArgument(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, name);
            if (index < 0 || index + 1 >= arguments.Length || string.IsNullOrWhiteSpace(arguments[index + 1]))
            {
                throw new InvalidOperationException($"Required command-line argument {name} was not provided.");
            }

            return arguments[index + 1];
        }
    }
}
