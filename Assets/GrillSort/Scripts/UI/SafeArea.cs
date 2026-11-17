using UnityEngine;

namespace GrillSort
{
    /// <summary>
    /// Fits this RectTransform to Screen.safeArea (notches, punch holes, home indicator) and refits whenever the
    /// screen or safe area changes. Put it on every UI layer that holds controls or text.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeArea : MonoBehaviour
    {
        Rect _last;
        Vector2Int _lastScreen;

        void OnEnable() => Apply(true);
        void Update() => Apply(false);

        void Apply(bool force)
        {
            var sa = Screen.safeArea;
            var scr = new Vector2Int(Screen.width, Screen.height);
            if (!force && sa == _last && scr == _lastScreen) return;
            if (scr.x <= 0 || scr.y <= 0) return;
            _last = sa; _lastScreen = scr;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(sa.xMin / scr.x, sa.yMin / scr.y);
            rt.anchorMax = new Vector2(sa.xMax / scr.x, sa.yMax / scr.y);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
