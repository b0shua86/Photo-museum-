// Editor automation: configures the project for URP + macOS (so a project authored
// without ever opening the Editor still builds correctly), creates the single
// bootstrap scene programmatically, guarantees the runtime shaders ship, and
// builds an arm64 .app. Runs its config step on every load (idempotent) so the
// human path — open the project, press Build — also just works.
//
//   Menu:  Camera Obscura ▸ Build macOS App
//   CLI:   Unity -batchmode -quit -projectPath <proj> \
//            -executeMethod CameraObscura.BuildScript.BuildMac -logFile -
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using TMPro;

namespace CameraObscura
{
    [InitializeOnLoad]
    public static class BuildScript
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string SettingsDir = "Assets/Settings";
        const string PipelinePath = SettingsDir + "/URP-Pipeline.asset";
        const string RendererPath = SettingsDir + "/URP-Renderer.asset";

        static BuildScript()
        {
            // Configure on load (deferred so the asset database is ready).
            EditorApplication.delayCall += () => { try { EnsureProjectConfig(); } catch (System.Exception e) { Debug.LogWarning("[BuildScript] config on load: " + e.Message); } };
        }

        [MenuItem("Camera Obscura/Configure Project")]
        public static void Configure() { EnsureProjectConfig(); Debug.Log("[BuildScript] project configured."); }

        [MenuItem("Camera Obscura/Build macOS App")]
        public static void BuildMacMenu() { BuildMac(); }

        public static void BuildMac()
        {
            EnsureProjectConfig();

            // TMP essentials import asynchronously; the player must NOT be packaged
            // before TMP_Settings exists or every TextMeshPro NREs at runtime. If
            // it isn't ready yet, abort cleanly — re-running the build (a few
            // seconds later, once the import has settled) then succeeds.
            if (!TmpReady)
            {
                Debug.LogWarning("[BuildScript] TMP Essential Resources are still importing. " +
                    "Re-run the build in a few seconds (or use Window ▸ TextMeshPro ▸ Import TMP Essential Resources, then build).");
                if (Application.isBatchMode) EditorApplication.Exit(2);
                return;
            }

            string outDir = Path.Combine(Directory.GetCurrentDirectory(), "Build");
            Directory.CreateDirectory(outDir);
            string appPath = Path.Combine(outDir, "Camera Obscura.app");

            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX);

            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = appPath,
                target = BuildTarget.StandaloneOSX,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };

            Debug.Log("[BuildScript] building → " + appPath);
            var report = BuildPipeline.BuildPlayer(opts);
            var summary = report.summary;
            Debug.Log($"[BuildScript] build result: {summary.result}, {summary.totalErrors} errors, {summary.totalWarnings} warnings, output: {summary.outputPath}");
            if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.LogError("[BuildScript] BUILD FAILED");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
            else if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        public static void EnsureProjectConfig()
        {
            EnsureTMP();
            EnsureUrp();
            EnsurePlayerSettings();
            EnsureScene();
            EnsureBuildScenes();
            EnsureAlwaysIncludedShaders();
            AssetDatabase.SaveAssets();
        }

        public static bool TmpReady => TMP_Settings.instance != null;

        [MenuItem("Camera Obscura/Import TMP Essentials")]
        public static void EnsureTMP()
        {
            // TextMeshPro NREs on Awake without its Settings asset; import the
            // essential resources (Settings + default LiberationSans SDF font +
            // shaders) once, silently. ImportPackage(path, interactive:false) is
            // the long-stable API; the import settles before the next build pass.
            if (TMP_Settings.instance != null) return;
            // Use TMP's own importer — it locates the package (in PackageCache or
            // built-ins) and imports Essentials silently (interactive:false).
            TMP_PackageResourceImporter.ImportResources(true, false, false);
            AssetDatabase.Refresh();
            Debug.Log("[BuildScript] importing TMP Essential Resources…");
        }

        static void EnsureUrp()
        {
            if (!AssetDatabase.IsValidFolder(SettingsDir))
                AssetDatabase.CreateFolder("Assets", "Settings");

            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (urp == null)
            {
                var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, RendererPath);
                urp = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(urp, PipelinePath);
                AssetDatabase.SaveAssets();
                Debug.Log("[BuildScript] created URP pipeline + renderer assets.");
            }

            try { urp.msaaSampleCount = 4; } catch { }
            try { urp.supportsHDR = true; } catch { }
            try { urp.supportsCameraDepthTexture = true; } catch { }
            try { urp.supportsCameraOpaqueTexture = true; } catch { }
            // reflectionProbeBlending / BoxProjection are read-only properties backed
            // by serialized fields — set them through SerializedObject.
            var urpSo = new SerializedObject(urp);
            SetBool(urpSo, "m_ReflectionProbeBlending", true);
            SetBool(urpSo, "m_ReflectionProbeBoxProjection", true);
            urpSo.ApplyModifiedProperties();

            GraphicsSettings.defaultRenderPipeline = urp;
            int levels = QualitySettings.names.Length;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < levels; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urp;
            }
            QualitySettings.SetQualityLevel(current, false);
        }

        static void SetBool(SerializedObject so, string prop, bool value)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.boolValue = value;
        }

        static void EnsurePlayerSettings()
        {
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.productName = "Camera Obscura";
            PlayerSettings.companyName = "Camera Obscura";
            try { PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.cameraobscura.museum"); }
            catch { PlayerSettings.applicationIdentifier = "com.cameraobscura.museum"; }
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.runInBackground = true;
            try { PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x); } catch { }
            try { PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Unity_4_8); } catch { }
        }

        static void EnsureScene()
        {
            if (File.Exists(ScenePath)) return;
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("Bootstrap");
            go.AddComponent<Bootstrap>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[BuildScript] created bootstrap scene at " + ScenePath);
        }

        static void EnsureBuildScenes()
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static void EnsureAlwaysIncludedShaders()
        {
            string[] names =
            {
                "Universal Render Pipeline/Lit",
                "Universal Render Pipeline/Unlit",
                "Universal Render Pipeline/Simple Lit",
                "Sprites/Default",
                "TextMeshPro/Distance Field",
                "TextMeshPro/Mobile/Distance Field",
                "TextMeshPro/Sprite",
            };
            var gs = GraphicsSettings.GetGraphicsSettings();
            var so = new SerializedObject(gs);
            var prop = so.FindProperty("m_AlwaysIncludedShaders");
            if (prop == null) return;

            var have = new HashSet<string>();
            for (int i = 0; i < prop.arraySize; i++)
            {
                var s = prop.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                if (s != null) have.Add(s.name);
            }
            foreach (var n in names)
            {
                if (have.Contains(n)) continue;
                var shader = Shader.Find(n);
                if (shader == null) continue;
                int idx = prop.arraySize;
                prop.InsertArrayElementAtIndex(idx);
                prop.GetArrayElementAtIndex(idx).objectReferenceValue = shader;
            }
            so.ApplyModifiedProperties();
        }
    }
}
#endif
