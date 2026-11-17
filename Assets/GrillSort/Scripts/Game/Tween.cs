using System;
using System.Collections;
using UnityEngine;

namespace GrillSort
{
    public static class Ease
    {
        public static float OutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        public static float InOutCubic(float t) => t < .5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        public static float InCubic(float t) => t * t * t;
        public static float OutBack(float t, float s = 1.70158f) { t -= 1f; return t * t * ((s + 1f) * t + s) + 1f; }
        public static float OutElastic(float t)
        {
            if (t <= 0f) return 0f; if (t >= 1f) return 1f;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - .75f) * (2f * Mathf.PI / 3f)) + 1f;
        }
    }

    /// <summary>Tiny coroutine tweens (no third-party package, keeps the build small).</summary>
    public static class Tween
    {
        public static IEnumerator Run(float duration, Action<float> step, Func<float, float> ease = null)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                step(ease != null ? ease(k) : k);
                yield return null;
            }
            step(1f);
        }

        public static IEnumerator Move(RectTransform rt, Vector2 to, float d, Func<float, float> ease = null)
        {
            var from = rt.anchoredPosition;
            return Run(d, k => rt.anchoredPosition = Vector2.LerpUnclamped(from, to, k), ease ?? Ease.OutCubic);
        }

        public static IEnumerator Scale(Transform tr, Vector3 to, float d, Func<float, float> ease = null)
        {
            var from = tr.localScale;
            return Run(d, k => tr.localScale = Vector3.LerpUnclamped(from, to, k), ease ?? Ease.OutCubic);
        }

        public static IEnumerator Fade(CanvasGroup cg, float to, float d)
        {
            float from = cg.alpha;
            return Run(d, k => cg.alpha = Mathf.Lerp(from, to, k));
        }

        public static IEnumerator Fade(UnityEngine.UI.Graphic g, float to, float d)
        {
            float from = g.color.a;
            return Run(d, k => { var c = g.color; c.a = Mathf.Lerp(from, to, k); g.color = c; });
        }

        /// <summary>Squash-and-stretch landing bounce.</summary>
        public static IEnumerator Bounce(Transform tr, float amount = .14f, float d = .28f)
        {
            return Run(d, k =>
            {
                float s = Mathf.Sin(k * Mathf.PI) * (1f - k) * amount * 2.2f;
                tr.localScale = new Vector3(1f + s, 1f - s, 1f);
            });
        }

        /// <summary>Horizontal "no" shake.</summary>
        public static IEnumerator Shake(RectTransform rt, float amp = 18f, float d = .35f)
        {
            var basePos = rt.anchoredPosition;
            yield return Run(d, k => rt.anchoredPosition = basePos + new Vector2(Mathf.Sin(k * Mathf.PI * 6f) * amp * (1f - k), 0f));
            rt.anchoredPosition = basePos;
        }

        public static IEnumerator Delay(float s, Action then)
        {
            yield return new WaitForSeconds(s);
            then();
        }
    }
}
