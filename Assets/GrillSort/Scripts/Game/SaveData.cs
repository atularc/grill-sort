using UnityEngine;

namespace GrillSort
{
    /// <summary>Player progress in PlayerPrefs (fine for the prototype).</summary>
    public static class SaveData
    {
        const string K = "gs.";
        public static int Level { get => PlayerPrefs.GetInt(K + "level", 1); set { PlayerPrefs.SetInt(K + "level", value); PlayerPrefs.Save(); } }
        public static int Coins { get => PlayerPrefs.GetInt(K + "coins", 250); set { PlayerPrefs.SetInt(K + "coins", value); PlayerPrefs.Save(); } }
        public static bool Sound { get => PlayerPrefs.GetInt(K + "sound", 1) == 1; set { PlayerPrefs.SetInt(K + "sound", value ? 1 : 0); PlayerPrefs.Save(); } }
        public static bool Haptic { get => PlayerPrefs.GetInt(K + "haptic", 1) == 1; set { PlayerPrefs.SetInt(K + "haptic", value ? 1 : 0); PlayerPrefs.Save(); } }
        public static int LastTheme { get => PlayerPrefs.GetInt(K + "theme", 0); set { PlayerPrefs.SetInt(K + "theme", value); PlayerPrefs.Save(); } }

        static readonly int[] DefaultBoosters = { 3, 2, 1, 1 };
        public static int Booster(int i) => PlayerPrefs.GetInt(K + "boost" + i, DefaultBoosters[i]);
        public static void SetBooster(int i, int v) { PlayerPrefs.SetInt(K + "boost" + i, Mathf.Max(0, v)); PlayerPrefs.Save(); }
    }
}
