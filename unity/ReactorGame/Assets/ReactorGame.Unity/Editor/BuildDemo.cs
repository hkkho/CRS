using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ReactorGame.Unity.Editor
{
    /// <summary>
    /// Builds the visible Bootstrap demo without Unity Test Runner hooks.
    /// Test players use a separate, headless validation command because their
    /// result channel can open a local editor connection on Windows.
    /// </summary>
    public static class BuildDemo
    {
        private const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";

        public static void Run()
        {
            string outputPath = GetRequiredCommandLineArgument("-canduDemoOutputPath");
            string outputDirectory = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new InvalidOperationException(
                    "The demo output path must include a parent directory.");
            }

            Directory.CreateDirectory(outputDirectory);
            BuildReport report = BuildPipeline.BuildPlayer(
                new BuildPlayerOptions
                {
                    scenes = new[] { BootstrapScenePath },
                    locationPathName = outputPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "The offline graphical demo build failed: " +
                    report.summary.result);
            }

            Debug.Log(
                "Offline graphical demo built without Unity Test Runner networking: " +
                outputPath);
        }

        private static string GetRequiredCommandLineArgument(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, name);
            if (index < 0 || index + 1 >= arguments.Length ||
                string.IsNullOrWhiteSpace(arguments[index + 1]))
            {
                throw new InvalidOperationException(
                    "Required command-line argument " + name + " was not provided.");
            }

            return Path.GetFullPath(arguments[index + 1]);
        }
    }
}
