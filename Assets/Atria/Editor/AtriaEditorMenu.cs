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
        public const string PrpDraftPath = "Assets/Atria/MappingData/prp_g_left_draft.json";
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
        public static void ValidateBuildingData() => ValidateFile(BuildingJsonPath);

        /// <summary>Validates whichever building JSON is selected in the Project window (e.g. a mapping draft).</summary>
        [MenuItem("Atria/Validate Selected JSON", priority = 21)]
        public static void ValidateSelectedJson() => ValidateFile(AssetDatabase.GetAssetPath(Selection.activeObject));

        [MenuItem("Atria/Validate Selected JSON", true)]
        private static bool CanValidateSelectedJson() => AssetDatabase.GetAssetPath(Selection.activeObject).EndsWith(".json");

        private static void ValidateFile(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (asset == null) { Debug.LogError("[Atria] Missing " + path); return; }
            try
            {
                BuildingData building = BuildingDataLoader.FromJson(asset.text);
                ValidationReport report = BuildingValidator.Validate(building);
                string summary = $"[Atria] {path}: {building.DisplayName} (status: {building.DataStatus}): {report}";
                if (report.IsValid) Debug.Log(summary); else Debug.LogError(summary);
                if (report.MissingMeasurements.Count > 0)
                    Debug.Log($"[Atria] Still to survey ({report.MissingMeasurements.Count}):\n  " + string.Join("\n  ", report.MissingMeasurements));
            }
            catch (System.FormatException e)
            {
                Debug.LogError($"[Atria] {path}: {e.Message}");
            }
        }

        // Dataset selection is stored on the scene's Atria object and the scene is saved, so it survives restarts.
        [MenuItem("Atria/Dataset: Use PRP Draft (Block C ground floor)", priority = 40)]
        public static void UsePrpDraft()
        {
            var draft = AssetDatabase.LoadAssetAtPath<TextAsset>(PrpDraftPath);
            if (draft == null) { EditorUtility.DisplayDialog("Atria", "PRP draft not found at " + PrpDraftPath, "OK"); return; }
            SetDataset(draft);
        }

        [MenuItem("Atria/Dataset: Use Demo (default)", priority = 41)]
        public static void UseDemo() => SetDataset(null);

        private static void SetDataset(TextAsset dataset)
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Atria", "Stop Play mode first: changes made during Play are discarded.", "OK");
                return;
            }
            var app = Object.FindFirstObjectByType<AtriaApp>();
            if (app == null)
            {
                EditorUtility.DisplayDialog("Atria", "No Atria object in the open scene. Open " + ScenePath + " or run Atria > 1. Create Main Scene.", "OK");
                return;
            }
            Undo.RecordObject(app, "Select Atria dataset");
            app.BuildingDataOverride = dataset;
            EditorUtility.SetDirty(app);
            Scene scene = app.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = !string.IsNullOrEmpty(scene.path) && EditorSceneManager.SaveScene(scene);
            Selection.activeObject = app;
            string which = dataset == null ? "default demo (Resources/Atria/Data/building.json)" : AssetDatabase.GetAssetPath(dataset);
            Debug.Log($"[Atria] Dataset set to {which}. " + (saved ? $"Saved in {scene.path}. Press Play." : "Scene NOT saved: save it (Ctrl+S) to keep this selection."));
        }
    }
}
