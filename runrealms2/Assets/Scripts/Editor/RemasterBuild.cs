using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace RunRealms2.Editor
{
    public static class RemasterBuild
    {
        private const string ScenePath = "Assets/Generated/RemasterMain.unity";
        private const string IconPath = "Assets/Generated/RemasterIcon.png";

        [MenuItem("RUN REALMS 2/Build Android APK")]
        public static void BuildAndroid()
        {
            ConfigurePlayer();
            EnsureGeneratedAssets();

            var output = Environment.GetEnvironmentVariable("BUILD_PATH");
            if (string.IsNullOrWhiteSpace(output)) output = Path.GetFullPath("Build/Android/RUN-REALMS-2.0-Remaster.apk");
            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? "Build/Android");

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.CompressWithLz4HC | BuildOptions.StrictMode
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException($"RUN//REALMS 2 Android build failed: {report.summary.result} with {report.summary.totalErrors} errors.");
            }

            Debug.Log($"RUN//REALMS 2 built successfully: {output} ({report.summary.totalSize:N0} bytes)");
        }

        [MenuItem("RUN REALMS 2/Generate Preview Scene")]
        public static void EnsureGeneratedAssets()
        {
            Directory.CreateDirectory("Assets/Generated");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var marker = new GameObject("Runtime bootstrap creates the remaster world");
            marker.transform.position = Vector3.zero;
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceUpdate);
            CreateIcon();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "RUN REALMS Studio";
            PlayerSettings.productName = "RUN//REALMS 2.0 Remaster";
            PlayerSettings.bundleVersion = "2.0.0";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.runrealms.remaster");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.MTRendering = true;
            PlayerSettings.graphicsJobs = true;
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup.Android, Il2CppCompilerConfiguration.Release);
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.High);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.useAPKExpansionFiles = false;
            PlayerSettings.Android.optimizedFramePacing = true;
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
        }

        private static void CreateIcon()
        {
            const int size = 512;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            var background = new Color(0.02f, 0.027f, 0.047f, 1f);
            var cyan = RuntimeAssets.Hex("23D7FF");
            var magenta = RuntimeAssets.Hex("FF3BD4");
            var white = Color.white;
            var pixels = new Color[size * size];

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var uv = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                    var center = uv - Vector2.one * 0.5f;
                    var radius = center.magnitude;
                    var portal = Mathf.Abs(radius - 0.31f) < 0.035f;
                    var slash = Mathf.Abs(center.x + center.y * 0.45f) < 0.026f && Mathf.Abs(center.y) < 0.38f;
                    var runnerBody = Mathf.Abs(center.x + 0.025f) < 0.045f && center.y > -0.1f && center.y < 0.18f;
                    var head = Vector2.Distance(center, new Vector2(-0.03f, 0.25f)) < 0.065f;
                    var legA = DistanceToSegment(center, new Vector2(-0.02f, -0.1f), new Vector2(-0.18f, -0.31f)) < 0.038f;
                    var legB = DistanceToSegment(center, new Vector2(0.02f, -0.1f), new Vector2(0.2f, -0.24f)) < 0.038f;
                    var armA = DistanceToSegment(center, new Vector2(-0.02f, 0.1f), new Vector2(-0.2f, 0.02f)) < 0.034f;
                    var armB = DistanceToSegment(center, new Vector2(0.02f, 0.1f), new Vector2(0.18f, 0.24f)) < 0.034f;
                    var glow = Mathf.Clamp01(1f - Mathf.Abs(radius - 0.31f) / 0.12f) * 0.25f;
                    var color = Color.Lerp(background, cyan, glow);
                    if (portal) color = Color.Lerp(cyan, magenta, Mathf.InverseLerp(-0.5f, 0.5f, center.y));
                    if (slash) color = cyan * 1.3f;
                    if (runnerBody || head || legA || legB || armA || armB) color = white;
                    pixels[y * size + x] = color;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            File.WriteAllBytes(IconPath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(IconPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = false;
            importer.SaveAndReimport();
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Android, new[] { icon });
        }

        private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            var segment = end - start;
            var t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / Mathf.Max(0.0001f, segment.sqrMagnitude));
            return Vector2.Distance(point, start + segment * t);
        }
    }
}
