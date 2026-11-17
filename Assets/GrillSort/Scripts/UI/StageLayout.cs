using UnityEngine;

namespace GrillSort
{
    /// <summary>
    /// Lays out the authored scene for any screen shape (no objects are created):
    /// background + front layer cover-fit from the top, customers sit on the counter line,
    /// and the grills are arranged and scaled to fit between the counter and the booster bar.
    /// Design units = px of the 1080x1920 mockup.
    /// </summary>
    [ExecuteAlways]
    public sealed class StageLayout : MonoBehaviour
    {
        public RectTransform world;        // full-screen container (not safe-area)
        public RectTransform background;   // 1024x1536 source
        public RectTransform front;        // same rect as background
        public RectTransform customers;    // counter-line anchor (behind the front layer)
        public RectTransform counterOverlay; // counter-line anchor in front of everything (tips)
        public RectTransform grills;       // grill container
        public RectTransform[] grillSlots; // all authored grills
        public RectTransform safeArea;

        [Tooltip("Counter top in px of the 1024x1536 source")] public float counterTop = 528f;
        public int activeGrills = 5;

        public const float GrillSpacing = 200f, GrillHeight = 770f, RowGap = 90f;
        const float BoosterReserve = 230f;   // design px above the safe bottom kept for the booster bar
        const float BoardGap = 202f;         // design px between counter top and grill top (mockup)

        Vector2 _lastSize; int _lastActive = -1; float _lastCounter; Rect _lastSafe;

        public float StageScale { get; private set; } = 1.25f;

        void LateUpdate()
        {
            if (world == null) return;
            var size = world.rect.size;
            if (size == _lastSize && _lastActive == activeGrills && Mathf.Approximately(_lastCounter, counterTop) && _lastSafe == Screen.safeArea) return;
            Apply();
        }

        public void Apply()
        {
            if (world == null) return;
            var size = world.rect.size;
            _lastSize = size; _lastActive = activeGrills; _lastCounter = counterTop; _lastSafe = Screen.safeArea;
            float W = size.x, H = size.y;
            if (W <= 0 || H <= 0) return;

            // background: cover-fit, anchored to the top so the counter stays put
            float s = Mathf.Max(W / 864f, H / 1536f);
            StageScale = s;
            foreach (var rt in new[] { background, front })
            {
                if (rt == null) continue;
                rt.anchorMin = rt.anchorMax = new Vector2(.5f, 1f);
                rt.pivot = new Vector2(.5f, 1f);
                rt.sizeDelta = new Vector2(1024f * s, 1536f * s);
                rt.anchoredPosition = Vector2.zero;
            }

            // customers: on the counter line, never wider than the screen
            float counterY = counterTop * s;
            float cs = Mathf.Min(s, W / 864f) / 1.25f;
            foreach (var rt in new[] { customers, counterOverlay })
            {
                if (rt == null) continue;
                rt.anchorMin = rt.anchorMax = new Vector2(.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -counterY);
                rt.localScale = new Vector3(cs, cs, 1f);
            }

            // grills: between the counter front and the booster bar
            if (grills == null) return;
            float safeBottom = 0f;
            if (Screen.height > 0) safeBottom = Screen.safeArea.yMin / Screen.height * H;
            float top = counterY + BoardGap * s / 1.25f;
            float bottom = H - safeBottom - BoosterReserve;
            float availH = Mathf.Max(200f, bottom - top);
            float availW = W - 40f;

            int n = Mathf.Max(1, activeGrills);
            // one row vs two rows: take whichever shows bigger grills
            float s1 = Mathf.Min(1f, availW / (n * GrillSpacing), availH / GrillHeight);
            int perRow2 = Mathf.CeilToInt(n / 2f);
            float s2 = n < 4 ? 0f : Mathf.Min(1f, availW / (perRow2 * GrillSpacing), availH / (GrillHeight * 2f + RowGap));
            bool twoRows = s2 > s1 + .02f;
            float gs = twoRows ? s2 : s1;
            int rows = twoRows ? 2 : 1;

            grills.anchorMin = grills.anchorMax = new Vector2(.5f, 1f);
            grills.pivot = new Vector2(.5f, .5f);
            float contentH = rows * GrillHeight + (rows - 1) * RowGap;
            float used = contentH * gs;
            grills.anchoredPosition = new Vector2(0f, -(top + used * .5f + Mathf.Max(0f, availH - used) * .45f));
            grills.localScale = new Vector3(gs, gs, 1f);

            int idx = 0;
            for (int r = 0; r < rows; r++)
            {
                int inRow = rows == 1 ? n : (r == 0 ? perRow2 : n - perRow2);
                float y = rows == 1 ? 0f : (r == 0 ? 1 : -1) * (GrillHeight + RowGap) * .5f;
                for (int c = 0; c < inRow; c++, idx++)
                {
                    if (idx >= grillSlots.Length) break;
                    float x = (c - (inRow - 1) * .5f) * GrillSpacing;
                    grillSlots[idx].anchoredPosition = new Vector2(x, y);
                }
            }
        }
    }
}
