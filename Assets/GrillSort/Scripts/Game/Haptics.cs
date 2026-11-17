using UnityEngine;

namespace GrillSort
{
    /// <summary>Short taps on Android (VibrationEffect). iOS needs a native Taptic plugin later; until then it is silent.</summary>
    public static class Haptics
    {
        public static bool Enabled = true;
#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject _vibrator;
        static int _sdk;
        static void Ensure()
        {
            if (_vibrator != null) return;
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            using (var ver = new AndroidJavaClass("android.os.Build$VERSION")) _sdk = ver.GetStatic<int>("SDK_INT");
        }
#endif
        public static void Light() => Pulse(8, 60);
        public static void Medium() => Pulse(16, 120);
        public static void Heavy() => Pulse(30, 200);

        static void Pulse(long ms, int amplitude)
        {
            if (!Enabled) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                Ensure();
                if (_vibrator == null) return;
                if (_sdk >= 26)
                {
                    using (var fx = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (var effect = fx.CallStatic<AndroidJavaObject>("createOneShot", ms, amplitude))
                        _vibrator.Call("vibrate", effect);
                }
                else _vibrator.Call("vibrate", ms);
            }
            catch { /* device without vibrator */ }
#endif
        }

        // Referenced so Unity adds the VIBRATE permission to the Android manifest. Never called.
        static void PermissionMarker() { Handheld.Vibrate(); }
    }
}
