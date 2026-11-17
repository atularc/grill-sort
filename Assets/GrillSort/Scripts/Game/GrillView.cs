using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GrillSort
{
    /// <summary>
    /// One grill + skewer + 4 food slots, fully authored in the scene by the builder. Runtime only swaps sprites,
    /// toggles objects and moves the slot transforms.
    /// </summary>
    public sealed class GrillView : MonoBehaviour, IPointerClickHandler
    {
        public int index;
        public RectTransform rect;
        public Image shadow, ember, glow, highlight, grill, skewer;
        public Image smoke;
        public RectTransform[] slots;     // bottom -> top
        public Image[] foods;
        public Image[] foodShadows;
        public Image[] steam;
        public Image[] sparkles;
        public Image perfect;

        /// <summary>Slot centers (bottom -> top) in grill-local design px, from the mockup.</summary>
        public static readonly float[] SlotY = { -268f, -105f, 58f, 221f };
        public const float GrillW = 180f, GrillH = 770f, FoodScale = 1.0f, ShadowRatio = 712f / 512f;
        public static readonly Vector2 ShadowOffset = new Vector2(8f, -18f);

        public Action<int, int> onTap;   // (grill, tapped slot index)

        public int[] Shown { get; private set; } = new int[0];

        public void OnPointerClick(PointerEventData e)
        {
            if (onTap == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera, out var local);
            int best = 0; float bd = float.MaxValue;
            for (int i = 0; i < SlotY.Length; i++)
            {
                float d = Mathf.Abs(local.y - SlotY[i]);
                if (d < bd) { bd = d; best = i; }
            }
            onTap(index, best);
        }

        public void SetFood(int slot, GameArt.FoodArt art, bool raw)
        {
            var img = foods[slot]; var sh = foodShadows[slot];
            if (art == null) { img.enabled = false; sh.enabled = false; return; }
            img.enabled = sh.enabled = true;
            img.sprite = raw ? art.raw : art.grilled;
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
                SetFood(i, i < stack.Length ? art.foods[stack[i]] : null, raw);
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

        public void SetLit(bool lit, GameArt art) => grill.sprite = lit ? art.grill : art.grillDark;

        public void ResetFx()
        {
            SetAlpha(glow, 0f); SetAlpha(highlight, 0f);
            foreach (var s in steam) s.gameObject.SetActive(false);
            foreach (var s in sparkles) s.gameObject.SetActive(false);
            perfect.gameObject.SetActive(false);
            if (smoke != null) smoke.gameObject.SetActive(false);
            for (int i = 0; i < slots.Length; i++) ResetSlot(i);
        }

        public static void SetAlpha(Graphic g, float a) { var c = g.color; c.a = a; g.color = c; }
    }
}
