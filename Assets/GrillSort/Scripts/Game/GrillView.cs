using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GrillSort
{
    /// <summary>
    /// One grill (= one order tray) + skewer + food slots, fully authored in the scene by the builder. Runtime only
    /// swaps sprites, toggles objects and moves the slot transforms. Input: tap-tap or drag (Sort Streak style).
    /// </summary>
    public sealed class GrillView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public int index;
        public RectTransform rect;
        public Image shadow, ember, glow, highlight, grill, skewer;
        /// <summary>Red coal glow bed UNDER the grill (the grill art has the glow cut out) - flickers in GameController. Keep it opaque.</summary>
        public Image coalGlow;
        /// <summary>Embers shimmering over the glow bed: two layers covering different patches, pulsed out of step.</summary>
        public Image coalHotA, coalHotB;
        /// <summary>Smoke wisps, spread along the whole grill.</summary>
        public Image[] smokes;
        public const int SmokeCount = 4;
        public Image[] flames;            // small cartoon flames licking up between the food (flicker in GameController)

        /// <summary>Flame spots (x, y, width) in grill-local design px: in the gaps between food slots, alternating sides.</summary>
        public static readonly Vector3[] FlameSpots =
        {
            new Vector3(-50f, -350f, 40f), new Vector3(48f, -187f, 46f), new Vector3(-46f, -24f, 42f),
            new Vector3(50f, 140f, 46f), new Vector3(-48f, 300f, 40f),
        };
        public RectTransform[] slots;     // bottom -> top
        public Image[] foods;
        public Image[] foodShadows;
        public Image[] steam;
        public Image[] sparkles;
        public Image perfect;

        /// <summary>
        /// Slot centers in grill-local design px for the current tray size (<see cref="SetCapacity"/>). Sort Streak
        /// style: food hangs from the top, so slot 0 (the fixed end) is at the top and the last piece (the free end,
        /// the one you can drop onto) is at the bottom. 4 slots = the mockup spacing (163); 5-6 share the coal bed.
        /// Always <see cref="MaxSlots"/> long so loops over the authored slots stay in range.
        /// </summary>
        public static float[] SlotY { get; private set; } = Layout(4, out _);
        /// <summary>Slots in use per tray this level.</summary>
        public static int Capacity { get; private set; } = 4;
        /// <summary>Food display scale for the current tray size.</summary>
        public static float FoodScale { get; private set; } = 1f;
        public const int MaxSlots = 6;
        public const float GrillW = 180f, GrillH = 770f, ShadowRatio = 712f / 512f;
        public static readonly Vector2 ShadowOffset = new Vector2(8f, -18f);
        const float TopY = 268f, BottomY = -290f;   // span for trays of 5+ slots
        /// <summary>Centre of a full stack in grill units (0 = tray centre; nudge to balance the hook at the top).</summary>
        public static float StackCenterY = 0f;

        /// <summary>
        /// Fit to the screen (user 2026-11-22: with 7 trays everything got small and left a big empty band at the bottom).
        /// VStretch makes every tray taller (art, skewer, slot spacing; the food itself stays round) to use spare height;
        /// FoodBoost makes the food a little bigger inside narrower trays. Set by StageLayout.
        /// </summary>
        public static float VStretch { get; private set; } = 1f;
        public static float FoodBoost { get; private set; } = 1f;

        public static void SetCapacity(int capacity)
        {
            Capacity = Mathf.Clamp(capacity, 1, MaxSlots);
            SlotY = Layout(Capacity, out float scale);
            FoodScale = scale;
        }

        /// <summary>Returns true when the fit changed (callers then re-apply it to every grill).</summary>
        public static bool SetFit(float vstretch, float foodBoost)
        {
            if (Mathf.Approximately(vstretch, VStretch) && Mathf.Approximately(foodBoost, FoodBoost)) return false;
            VStretch = vstretch; FoodBoost = foodBoost;
            SetCapacity(Capacity);
            return true;
        }

        static float[] Layout(int capacity, out float scale)
        {
            float step = capacity <= 4 ? 163f : (TopY - BottomY) / (capacity - 1);
            scale = Mathf.Min(1f, step / 138f) * FoodBoost;
            // a full tray's stack is centred in the tray (user 2026-11-22: equal gap above the first and below the last
            // piece); food still hangs from the top, slot 0 highest
            float top = StackCenterY + step * (capacity - 1) * .5f;
            var y = new float[MaxSlots];
            for (int i = 0; i < MaxSlots; i++) y[i] = (top - step * i) * VStretch;
            return y;
        }

        // authored (unstretched) heights / positions, captured the first time the fit is applied
        Graphic[] _tall; float[] _tallH, _tallY; RectTransform[] _moved; float[] _movedY; float _rootH;
        readonly float[] _foodW = new float[MaxSlots];

        /// <summary>Applies <see cref="VStretch"/> / <see cref="FoodScale"/> to this grill's art, effects and food.</summary>
        public void ApplyFit()
        {
            if (_tall == null)
            {
                var t = new System.Collections.Generic.List<Graphic> { shadow, ember, glow, highlight, grill, coalGlow, coalHotA, coalHotB, skewer };
                t.RemoveAll(g => g == null);
                _tall = t.ToArray();
                _tallH = new float[_tall.Length]; _tallY = new float[_tall.Length];
                for (int i = 0; i < _tall.Length; i++) { _tallH[i] = _tall[i].rectTransform.sizeDelta.y; _tallY[i] = _tall[i].rectTransform.anchoredPosition.y; }
                var m = new System.Collections.Generic.List<RectTransform>();
                foreach (var g in steam) m.Add(g.rectTransform);
                foreach (var g in sparkles) m.Add(g.rectTransform);
                if (perfect != null) m.Add(perfect.rectTransform);
                _moved = m.ToArray(); _movedY = new float[_moved.Length];
                for (int i = 0; i < _moved.Length; i++) _movedY[i] = _moved[i].anchoredPosition.y;
                _rootH = rect.sizeDelta.y;
            }
            float k = VStretch;
            for (int i = 0; i < _tall.Length; i++)
            {
                var r = _tall[i].rectTransform;
                r.sizeDelta = new Vector2(r.sizeDelta.x, _tallH[i] * k);
                r.anchoredPosition = new Vector2(r.anchoredPosition.x, _tallY[i] * k);
            }
            for (int i = 0; i < _moved.Length; i++) _moved[i].anchoredPosition = new Vector2(_moved[i].anchoredPosition.x, _movedY[i] * k);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, _rootH * k);
            for (int i = 0; i < slots.Length; i++)
            {
                if (_foodW[i] > 0f && foods[i].enabled)
                {
                    float w = _foodW[i] * FoodScale;
                    foods[i].rectTransform.sizeDelta = new Vector2(w, w);
                    foodShadows[i].rectTransform.sizeDelta = new Vector2(w * ShadowRatio, w * ShadowRatio);
                }
                ResetSlot(i);
            }
        }

        public Action<int, int> onTap;   // (grill, tapped slot index)
        /// <summary>Drag callbacks: (grill, slot under the finger, screen position) / screen position / screen position.</summary>
        public Action<int, int, Vector2> onDragStart;
        public Action<Vector2> onDragMove, onDragEnd;

        public int[] Shown { get; private set; } = new int[0];

        public void OnPointerClick(PointerEventData e)
        {
            if (onTap == null) return;
            onTap(index, SlotAt(e.position, e.pressEventCamera));
        }

        /// <summary>Slot nearest to a screen point (by height).</summary>
        public int SlotAt(Vector2 screen, Camera cam)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, cam, out var local);
            int best = 0; float bd = float.MaxValue;
            for (int i = 0; i < Capacity; i++)
            {
                float d = Mathf.Abs(local.y - SlotY[i]);
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        public void OnBeginDrag(PointerEventData e) => onDragStart?.Invoke(index, SlotAt(e.pressPosition, e.pressEventCamera), e.position);
        public void OnDrag(PointerEventData e) => onDragMove?.Invoke(e.position);
        public void OnEndDrag(PointerEventData e) => onDragEnd?.Invoke(e.position);

        public void SetFood(int slot, GameArt.FoodArt art, bool raw)
        {
            var img = foods[slot]; var sh = foodShadows[slot];
            if (art == null) { img.enabled = false; sh.enabled = false; return; }
            img.enabled = sh.enabled = true;
            img.sprite = raw ? art.raw : art.grilled;
            _foodW[slot] = art.width;
            sh.sprite = raw && art.shadowRaw != null ? art.shadowRaw : art.shadow;
            float w = art.width * FoodScale;
            img.rectTransform.sizeDelta = new Vector2(w, w);
            sh.rectTransform.sizeDelta = new Vector2(w * ShadowRatio, w * ShadowRatio);
            sh.rectTransform.anchoredPosition = ShadowOffset;
            sh.color = Color.white;
            sh.rectTransform.localScale = Vector3.one;
        }

        public void Show(int[] stack, GameArt art, bool raw)
        {
            Shown = stack;
            for (int i = 0; i < slots.Length; i++)
            {
                SetFood(i, i < stack.Length && i < Capacity ? art.foods[stack[i]] : null, raw);
                ResetSlot(i);
            }
        }

        public void ResetSlot(int i)
        {
            slots[i].anchoredPosition = new Vector2(0f, SlotY[i]);
            slots[i].localScale = Vector3.one;
            slots[i].localRotation = Quaternion.identity;
        }

        public void HideSlot(int i) { foods[i].enabled = false; foodShadows[i].enabled = false; }

        /// <summary>Helper tray (no customer, just for space): steel frame.</summary>
        [NonSerialized] public bool helper;

        public void SetLit(bool lit, GameArt art)
        {
            bool hot = lit && !(helper && art.grillHelper != null);
            grill.sprite = helper && art.grillHelper != null ? art.grillHelper : lit ? art.grill : art.grillDark;
            if (coalGlow != null) coalGlow.sprite = hot ? art.grillGlow : art.grillDarkGlow;
            if (coalHotA != null) coalHotA.sprite = hot ? art.grillHotA : art.grillDarkHotA;
            if (coalHotB != null) coalHotB.sprite = hot ? art.grillHotB : art.grillDarkHotB;
        }

        public void ResetFx()
        {
            SetAlpha(glow, 0f); SetAlpha(highlight, 0f);
            foreach (var s in steam) s.gameObject.SetActive(false);
            foreach (var s in sparkles) s.gameObject.SetActive(false);
            perfect.gameObject.SetActive(false);
            if (smokes != null) foreach (var s in smokes) s.gameObject.SetActive(false);
            for (int i = 0; i < slots.Length; i++) ResetSlot(i);
        }

        public static void SetAlpha(Graphic g, float a) { var c = g.color; c.a = a; g.color = c; }
    }
}
