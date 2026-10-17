using System.IO;
using Atria.App;
using Atria.Core.Data;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Atria.EditorTools
{
    /// <summary>One-click project setup so scenes and player settings need no hand-edited YAML.</summary>
    public static class AtriaEditorMenu
    {
        public const string ScenePath = "Assets/Atria/Scenes/Main.unity";
        public const string BuildingJsonPath = "Assets/Atria/Resources/Atria/Data/building.json";
        public const string ApkPath = "Builds/Android/Atria.apk";
        public const string AndroidPackageName = "com.ujvall.atria";

        [MenuItem("Atria/1. Create Main Scene", priority = 1)]
        public static void CreateMainScene()
        {
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Atria", ScenePath + " already exists. Replace it?", "Replace", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Atria").AddComponent<AtriaApp>();
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.white;
            RenderSettings.skybox = null;
            RenderSettings.fog = false;

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("[Atria] Created " + ScenePath + " and set it as the only scene in Build Settings. Press Play to run.");
        }

        [MenuItem("Atria/2. Configure Android Player Settings", priority = 2)]
        public static void ConfigureAndroid()
        {
            NamedBuildTarget android = NamedBuildTarget.Android;
            PlayerSettings.companyName = "Atria";
            PlayerSettings.productName = "Atria";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetApplicationIdentifier(android, AndroidPackageName);

            // Portrait-only: the search flow and the scrubber are designed for one-handed use.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Low);
            PlayerSettings.stripEngineCode = true;
            // Fully offline: do not request the INTERNET permission.
            PlayerSettings.Android.forceInternetPermission = false;

            PlayerSettings.colorSpace = ColorSpace.Gamma; // cheapest path on low-end GPUs; visuals are vertex-coloured

            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.shadows = ShadowQuality.Disable;
                QualitySettings.antiAliasing = 0;
                QualitySettings.realtimeReflectionProbes = false;
                QualitySettings.softParticles = false;
                QualitySettings.vSyncCount = 0;
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Atria] Android player settings applied (package " + AndroidPackageName + ", portrait, IL2CPP, ARM64+ARMv7, API 26+, no INTERNET permission).");
        }

        [MenuItem("Atria/3. Build Android APK", priority = 3)]
        public static void BuildApk()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                EditorUtility.DisplayDialog("Atria", "Android Build Support is not installed for this editor.\n\nUnity Hub > Installs > (this version) > Add modules > Android Build Support (with OpenJDK and Android SDK & NDK Tools).", "OK");
                return;
            }
            if (!File.Exists(ScenePath)) CreateMainScene();

            EditorUserBuildSettings.buildAppBundle = false;
            Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[Atria] Android build {report.summary.result}: {ApkPath} ({report.summary.totalSize / (1024f * 1024f):0.0} MB, {report.summary.totalErrors} errors)");
        }

        [MenuItem("Atria/Validate Building Data", priority = 20)]
        public static void ValidateBuildingData()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(BuildingJsonPath);
            if (asset == null) { Debug.LogError("[Atria] Missing " + BuildingJsonPath); return; }
            try
            {
                BuildingData building = BuildingDataLoader.FromJson(asset.text);
                ValidationReport report = BuildingValidator.Validate(building);
                string summary = $"[Atria] {building.DisplayName} (status: {building.DataStatus}): {report}";
                if (report.IsValid) Debug.Log(summary); else Debug.LogError(summary);
            }
            catch (System.FormatException e)
            {
                Debug.LogError("[Atria] " + e.Message);
            }
        }
    }
}
