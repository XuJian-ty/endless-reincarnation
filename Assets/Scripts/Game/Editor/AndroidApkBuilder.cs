using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Editor
{
    internal static class AndroidApkBuilder
    {
        private const string DefaultOutputPath = @"D:\UnityBuilds\EndlessReincarnation\EndlessReincarnation.apk";

        public static void Build()
        {
            string outputPath = Environment.GetEnvironmentVariable("ENDLESS_APK_OUTPUT");
            if (string.IsNullOrWhiteSpace(outputPath))
                outputPath = DefaultOutputPath;

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
                throw new BuildFailedException("No enabled scenes are configured in EditorBuildSettings.");

            string outputDirectory = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrWhiteSpace(outputDirectory))
                throw new BuildFailedException($"Invalid APK output path: {outputPath}");

            Directory.CreateDirectory(outputDirectory);
            EditorUserBuildSettings.buildAppBundle = false;

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    $"Android build failed: {report.summary.result}, errors: {report.summary.totalErrors}");
            }

            Debug.Log($"Android APK built successfully: {outputPath} ({report.summary.totalSize} bytes)");
        }
    }
}
