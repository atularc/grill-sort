using UnityEngine;
using UnityEngine.UI;

namespace GrillSort
{
    /// <summary>
    /// 1 canvas unit = 1 design pixel of the 1080x1920 mockup: phones (taller than 9:16) match width, wider screens
    /// (tablets, foldables) match height so nothing gets oversized.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class CanvasAspect : MonoBehaviour
    {
        CanvasScaler _scaler;
        void Update()
        {
            if (_scaler == null) _scaler = GetComponent<CanvasScaler>();
            if (Screen.height <= 0) return;
            float m = (float)Screen.width / Screen.height > 1080f / 1920f ? 1f : 0f;
            if (!Mathf.Approximately(_scaler.matchWidthOrHeight, m)) _scaler.matchWidthOrHeight = m;
        }
    }
}
