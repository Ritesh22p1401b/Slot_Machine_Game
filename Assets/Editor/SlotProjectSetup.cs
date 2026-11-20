using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SlotMachine.EditorTools
{
    /// <summary>
    /// Batch-mode entry points (Unity -executeMethod) and the Web build.
    /// Typical command-line order:
    ///   SlotMachine.EditorTools.SlotProjectSetup.ImportTmpEssentials
    ///   SlotMachine.EditorTools.SlotProjectSetup.SetupProject
    ///   SlotMachine.EditorTools.SlotProjectSetup.BuildWeb
    /// </summary>
    public static class SlotProjectSetup
    {
        public const string WebBuildFolder = "Build/WebGL";
        private const string TmpEssentialsPackage = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";

        [MenuItem("Tools/Slot Machine/0. Import TMP Essentials", priority = 0)]
        public static void ImportTmpEssentials()
        {
            if (AssetDatabase.IsValidFolder("Assets/TextMesh Pro"))
            {
                Debug.Log("[SlotSetup] TMP Essential Resources already imported.");
                return;
            }

            string fullPath = Path.GetFullPath(TmpEssentialsPackage);
            if (Application.isBatchMode)
            {
                // Package import is asynchronous: run this method WITHOUT -quit; it exits when done.
                AssetDatabase.importPackageCompleted += _ =>
                {
                    Debug.Log("[SlotSetup] Imported TMP Essential Resources.");
                    EditorApplication.Exit(0);
                };
                AssetDatabase.importPackageFailed += (_, error) =>
                {
                    Debug.LogError($"[SlotSetup] TMP Essential Resources import failed: {error}");
                    EditorApplication.Exit(1);
                };
            }

#pragma warning disable CS0618 // replacement API is not documented for Unity 6.x LTS users yet
            AssetDatabase.ImportPackage(fullPath, false);
#pragma warning restore CS0618
        }

        [MenuItem("Tools/Slot Machine/Run Steps 1-3 (Art, Data, Scene)", priority = 20)]
        public static void SetupProject()
        {
            ArtImportSetup.ConfigureAll();
            SlotSceneBuilder.CreateDataAssets();
            SlotSceneBuilder.BuildScene();
            ConfigurePlayerSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[SlotSetup] Project setup complete.");
        }

        public static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "SlotDemo";
            PlayerSettings.productName = "Jackpot Slot Machine";
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.runInBackground = true;
            // Gzip + decompression fallback works on any static host (no special server headers needed).
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.template = "PROJECT:Responsive";
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.dataCaching = true;
        }

        [MenuItem("Tools/Slot Machine/4. Build Web (Production)", priority = 4)]
        public static void BuildWeb()
        {
            ConfigurePlayerSettings();
            EditorUserBuildSettings.development = false;

            string output = Path.GetFullPath(WebBuildFolder);
            if (Directory.Exists(output))
                Directory.Delete(output, true);
            Directory.CreateDirectory(output);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { SlotSceneBuilder.ScenePath },
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log($"[SlotSetup] Web build {summary.result}: {summary.totalSize / (1024f * 1024f):F1} MB, {summary.totalErrors} errors, output {output}");

            if (summary.result != BuildResult.Succeeded)
            {
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
                throw new Exception($"Web build failed: {summary.result}");
            }
        }
    }
}
