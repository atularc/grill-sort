using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GrillSort.EditorTools
{
    /// <summary>Builds the 5-tier demo APK for Aaron: the same game with GameController.tierDemo on (2026-11-26).</summary>
    public static class DemoBuild
    {
        const string Scene = "Assets/GrillSort/Scenes/Game.unity";
        const string Out = "Build/Grill Sort - 5 Tier Demo.apk";

        [MenuItem("Grill Sort/Build 5 Tier Demo APK")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[DemoBuild] stop Play mode first"); return; }
            var scene = EditorSceneManager.OpenScene(Scene);
            var gc = Object.FindFirstObjectByType<GameController>();
            if (gc == null || gc.demoBank == null || gc.demoBank.levels.Count == 0) { Debug.LogError("[DemoBuild] no GameController or demo levels (run Design Demo Levels + Run Full Setup)"); return; }
            bool oldDemo = gc.tierDemo, oldTest = gc.useTestLevel;
            gc.tierDemo = true; gc.useTestLevel = false;
            EditorUtility.SetDirty(gc);
            EditorSceneManager.SaveScene(scene);
            try
            {
                Directory.CreateDirectory("Build");
                var opts = new BuildPlayerOptions
                {
                    scenes = new[] { Scene },
                    locationPathName = Out,
                    target = BuildTarget.Android,
                    options = BuildOptions.None,
                };
                var report = BuildPipeline.BuildPlayer(opts);
                Debug.Log($"[DemoBuild] {report.summary.result}: {Out} ({report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalErrors} errors)");
            }
            finally
            {
                gc.tierDemo = oldDemo; gc.useTestLevel = oldTest;
                EditorUtility.SetDirty(gc);
                EditorSceneManager.SaveScene(scene);
            }
        }
    }
}
