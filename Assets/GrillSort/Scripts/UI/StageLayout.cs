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
        public RectTransform[] customerSlots; // customer i stands above grill customerTrays[i]
        /// <summary>Tray under each customer (helper trays have no customer).</summary>
        public int[] customerTrays = { 0, 1, 2, 3, 4 };
        /// <summary>Customers in play (the first N grills are order trays; an extra tray has no customer).</summary>
        public int activeCustomers = 4;
        public RectTransform safeArea;

        [Tooltip("Counter top in px of the 1024x1536 source")] public float counterTop = 528f;
        [Tooltip("Bottom of the counter front (curtain) in px of the 1024x1536 source; 0 = use the old fixed gap")] public float counterBottom = 0f;
        public int activeGrills = 5;

        public const float GrillSpacing = 200f, GrillHeight = 770f, RowGap = 90f;
        /// <summary>Column spacing in use (tighter with many trays, like Sort Streak's frameless ropes).</summary>
        public static float Spacing { get; private set; } = GrillSpacing;
        [Tooltip("Most a tray may be stretched taller to use spare screen height (1 = never). User 2026-11-22: keep the trays their normal length")] public float maxStretch = 1f;
        [Tooltip("Food size boost when there are more than 5 trays")] public float manyTraysFoodBoost = 1.08f;
        /// <summary>Width a customer + order bubble needs at scale 1 (design px).</summary>
        const float CustomerWidth = 250f;
        const float BoosterReserve = 330f;   // design px above the safe bottom kept for the booster bar + message strip
        const float BoardGap = 202f;         // design px between counter top and grill top (mockup), when counterBottom is 0
        const float CurtainGap = 14f;        // design px between the bottom of the (compact) curtain and the grill top

        Vector2 _lastSize; int _lastActive = -1, _lastCustomers = -1; float _lastCounter; Rect _lastSafe; bool _dirty;
        public void MarkDirty() => _dirty = true;

        public float StageScale { get; private set; } = 1.25f;

        // ---- positions for a row of n trays (used to slide trays when an order is served, Sort Streak style)
        float _W = 1080f, _availH = 800f, _cs = 1f, _gs = 1f;
        /// <summary>
        /// The most trays the row has had this level. Size, column spacing and food boost are fitted to this, not to the
        /// current count, so when served trays leave near the end the row only re-centres and the skewers keep their size
        /// (Aaron 2026-11-27: "when the grills are reduced near the end of the level, the skewer size changes slightly").
        /// Only grows (Extra tray); <see cref="ResetFit"/> at level load.
        /// </summary>
        [System.NonSerialized] public int fitTrays;
        public void ResetFit() { fitTrays = 0; _dirty = true; }
        int FitN(int n) => Mathf.Max(Mathf.Max(1, n), fitTrays);
        public static float SpacingFor(int n) => n > 5 ? 186f : GrillSpacing;
        public float ScaleFor(int n)
        {
            int f = FitN(n);
            bool many = f > 5;
            float aw = _W - (many ? 16f : 40f);
            return Mathf.Min(1f, aw / (f * SpacingFor(f)), _availH / GrillHeight);
        }
        /// <summary>X of tray i in a row of n, in the Grills container's units.</summary>
        public float TrayX(int i, int n) => (i - (n - 1) * .5f) * SpacingFor(FitN(n));
        /// <summary>X of the customer above tray i in a row of n, in the Customers container's units.</summary>
        public float CustomerX(int i, int n) => TrayX(i, n) * _gs / _cs;

        void LateUpdate()
        {
            if (world == null) return;
            var size = world.rect.size;
            if (!_dirty && size == _lastSize && _lastActive == activeGrills && _lastCustomers == activeCustomers && Mathf.Approximately(_lastCounter, counterTop + counterBottom * 10000f) && _lastSafe == Screen.safeArea) return;
            Apply();
        }

        public void Apply()
        {
            if (world == null) return;
            var size = world.rect.size;
            _dirty = false;
            _lastSize = size; _lastActive = activeGrills; _lastCustomers = activeCustomers; _lastCounter = counterTop + counterBottom * 10000f; _lastSafe = Screen.safeArea;
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
            // the compact counter (Aaron 2026-11-21: table + curtain smaller) frees the rows under it for the trays
            float top = counterBottom > 0f ? counterBottom * s + CurtainGap * s / 1.25f : counterY + BoardGap * s / 1.25f;
            float bottom = H - safeBottom - BoosterReserve;
            float availH = Mathf.Max(200f, bottom - top);
            int n = Mathf.Max(1, activeGrills);
            fitTrays = Mathf.Max(fitTrays, n);
            int fn = FitN(n);   // size is fitted to the level's widest row (see fitTrays); positions use n
            // fit (user 2026-11-22): with many trays use tighter columns and smaller side margins, a slightly bigger
            // food, and let the trays grow taller into the spare height instead of leaving an empty band at the bottom
            bool many = fn > 5;
            Spacing = SpacingFor(fn);
            float availW = W - (many ? 16f : 40f);
            // always one row: every customer stands right above their own tray (Sort Streak style)
            float gs = Mathf.Min(1f, availW / (fn * Spacing), availH / GrillHeight);
            _W = W; _availH = availH; _cs = cs; _gs = gs;
            float k = Mathf.Clamp(availH * .97f / (GrillHeight * gs), 1f, maxStretch);
            if (GrillView.SetFit(k, many ? manyTraysFoodBoost : 1f) && grillSlots != null)
                foreach (var gsl in grillSlots) { var gv = gsl != null ? gsl.GetComponent<GrillView>() : null; if (gv != null) gv.ApplyFit(); }
            int rows = 1, perRow2 = n;

            grills.anchorMin = grills.anchorMax = new Vector2(.5f, 1f);
            grills.pivot = new Vector2(.5f, .5f);
            float contentH = rows * GrillHeight + (rows - 1) * RowGap;
            float used = contentH * gs * k;
            // with the compact counter the trays sit right under the curtain; spare height stays above the boosters
            // spare height split evenly above and below the trays (user 2026-11-22: no big empty band at the bottom)
            float spare = Mathf.Max(0f, availH - used) * .5f;
            grills.anchoredPosition = new Vector2(0f, -(top + used * .5f + spare));
            grills.localScale = new Vector3(gs, gs, 1f);

            int idx = 0;
            for (int r = 0; r < rows; r++)
            {
                int inRow = rows == 1 ? n : (r == 0 ? perRow2 : n - perRow2);
                float y = rows == 1 ? 0f : (r == 0 ? 1 : -1) * (GrillHeight + RowGap) * .5f;
                for (int c = 0; c < inRow; c++, idx++)
                {
                    if (idx >= grillSlots.Length) break;
                    float x = (c - (inRow - 1) * .5f) * Spacing;
                    grillSlots[idx].anchoredPosition = new Vector2(x, y);
                }
            }

            // customers: centred over their trays, shrunk when the trays are close together
            if (customerSlots == null) return;
            float trayGap = Spacing * gs;            // screen-design px between tray centres
            float fit = Mathf.Min(1f, trayGap * 1.22f / (CustomerWidth * cs));
            for (int i = 0; i < customerSlots.Length; i++)
            {
                var c = customerSlots[i];
                if (c == null) continue;
                int tray = customerTrays != null && i < customerTrays.Length ? customerTrays[i] : i;
                if (i >= activeCustomers || tray < 0 || tray >= grillSlots.Length) continue;
                float x = grillSlots[tray].anchoredPosition.x * gs / cs;
                c.anchoredPosition = new Vector2(x, 0f);
                c.localScale = new Vector3(fit, fit, 1f);
            }
        }
    }
}
