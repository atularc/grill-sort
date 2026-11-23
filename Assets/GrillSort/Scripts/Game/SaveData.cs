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
        public static bool Music { get => PlayerPrefs.GetInt(K + "music", 1) == 1; set { PlayerPrefs.SetInt(K + "music", value ? 1 : 0); PlayerPrefs.Save(); } }
        /// <summary>Volume sliders, 0..1 (user 2026-11-22). Both default to 40%.</summary>
        public static float MusicVol { get => PlayerPrefs.GetFloat(K + "musicVol", .4f); set { PlayerPrefs.SetFloat(K + "musicVol", value); PlayerPrefs.Save(); } }
        public static float SfxVol { get => PlayerPrefs.GetFloat(K + "sfxVol", .4f); set { PlayerPrefs.SetFloat(K + "sfxVol", value); PlayerPrefs.Save(); } }
        public static int MusicTrack { get => PlayerPrefs.GetInt(K + "track", 0); set { PlayerPrefs.SetInt(K + "track", value); PlayerPrefs.Save(); } }
        public static bool Haptic { get => PlayerPrefs.GetInt(K + "haptic", 1) == 1; set { PlayerPrefs.SetInt(K + "haptic", value ? 1 : 0); PlayerPrefs.Save(); } }
        public static int LastTheme { get => PlayerPrefs.GetInt(K + "theme", 0); set { PlayerPrefs.SetInt(K + "theme", value); PlayerPrefs.Save(); } }

        // ---- meta (2026-11-23): stall decorations (bit i = decoration i built), daily reward streak
        public static int Decor { get => PlayerPrefs.GetInt(K + "decor", 0); set { PlayerPrefs.SetInt(K + "decor", value); PlayerPrefs.Save(); } }
        public static bool HasDecor(int i) => (Decor & (1 << i)) != 0;
        /// <summary>Next daily reward to claim (0..6), and the day (yyyymmdd) the last one was claimed.</summary>
        public static int DailyIndex { get => PlayerPrefs.GetInt(K + "dailyIdx", 0); set { PlayerPrefs.SetInt(K + "dailyIdx", value); PlayerPrefs.Save(); } }
        public static int DailyLast { get => PlayerPrefs.GetInt(K + "dailyLast", 0); set { PlayerPrefs.SetInt(K + "dailyLast", value); PlayerPrefs.Save(); } }
        public static int Today { get { var d = System.DateTime.Now; return d.Year * 10000 + d.Month * 100 + d.Day; } }
        public static bool DailyReady => DailyLast != Today;

        static readonly int[] DefaultBoosters = { 3, 2, 1, 1 };
        public static int Booster(int i) => PlayerPrefs.GetInt(K + "boost" + i, DefaultBoosters[i]);
        public static void SetBooster(int i, int v) { PlayerPrefs.SetInt(K + "boost" + i, Mathf.Max(0, v)); PlayerPrefs.Save(); }
    }
}
