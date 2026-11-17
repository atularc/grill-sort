using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GrillSort.EditorTools
{
    /// <summary>
    /// Grabs the Game view (what the player sees, HUD and popups included) into Screenshots/ at the project root.
    /// F12 = normal size, Shift+F12 = 2x (sharper, for store art). Works in and out of Play mode.
    /// </summary>
    public static class GrillSortScreenshot
    {
        const string Folder = "Screenshots";

        [MenuItem("Grill Sort/Screenshot/Take Screenshot _F12", priority = 20)]
        public static void Take() => Capture(1);

        [MenuItem("Grill Sort/Screenshot/Take Screenshot 2x #F12", priority = 21)]
        public static void Take2x() => Capture(2);

        [MenuItem("Grill Sort/Screenshot/Open Screenshots Folder", priority = 40)]
        public static void OpenFolder()
        {
            Directory.CreateDirectory(Folder);
            EditorUtility.RevealInFinder(Path.GetFullPath(Folder) + Path.DirectorySeparatorChar);
        }

        public static string Capture(int superSize)
        {
            Directory.CreateDirectory(Folder);
            string level = "";
            var gc = UnityEngine.Object.FindAnyObjectByType<GameController>();
            if (gc != null && Application.isPlaying) level = $"_L{gc.LevelNumber}";
            string path = $"{Folder}/GrillSort{level}_{DateTime.Now:yyyyMMdd_HHmmss}{(superSize > 1 ? "_2x" : "")}.png";

            // make sure the Game view draws a frame so the capture happens (needed outside Play mode)
            var gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType != null) EditorWindow.GetWindow(gameViewType, false, null, false).Repaint();

            ScreenCapture.CaptureScreenshot(path, superSize);
            EditorApplication.QueuePlayerLoopUpdate();
            Debug.Log($"[Grill Sort] Screenshot saved: {Path.GetFullPath(path)}");
            return path;
        }
    }
}
