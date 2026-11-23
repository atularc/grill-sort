using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace GrillSort
{
    /// <summary>
    /// Living customers + the cooking moment (2026-11-23, review: "the customers never react", "use the cooking stages").
    /// Every person has three faces drawn as one picture (CustomersPeople_v3: waiting, delighted, impatient) and a small
    /// emote that pops above the head. Only sprites are swapped and existing objects tweened; nothing is created.
    /// </summary>
    public sealed partial class GameController
    {
        public enum Mood { Wait, Happy, Angry }

        [Header("Customers")]
        [Tooltip("Seconds a customer waits without progress on their tray before they look impatient (not on tutorial levels)")]
        public float patienceSeconds = 22f;

        void SetMood(CustomerView c, Mood m)
        {
            var set = m == Mood.Happy ? art.customersHappy : m == Mood.Angry ? art.customersAngry : art.customers;
            if (set == null || set.Length == 0) set = art.customers;
            c.animal.sprite = set[c.face % set.Length];
        }

        /// <summary>Shows a mood for a while, then back to waiting (unless the customer is cheering / leaving by then).</summary>
        void MoodFor(CustomerView c, Mood m, float seconds)
        {
            if (c.moodCo != null) StopCoroutine(c.moodCo);
            c.moodCo = StartCoroutine(MoodAnim(c, m, seconds));
        }

        IEnumerator MoodAnim(CustomerView c, Mood m, float seconds)
        {
            SetMood(c, m);
            yield return new WaitForSeconds(seconds);
            if (!c.anim && c.gameObject.activeSelf) SetMood(c, Mood.Wait);
            c.moodCo = null;
        }

        void ShowEmote(CustomerView c, Sprite s, float hold)
        {
            if (c.emote == null || s == null) return;
            StartCoroutine(EmoteAnim(c.emote, s, hold));
        }

        IEnumerator EmoteAnim(Image e, Sprite s, float hold)
        {
            var rt = e.rectTransform;
            e.sprite = s;
            e.gameObject.SetActive(true);
            GrillView.SetAlpha(e, 1f);
            rt.anchoredPosition = CustomerView.EmotePos;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.zero;
            yield return Tween.Scale(rt, Vector3.one, .28f, t => Ease.OutBack(t, 2.4f));
            float t0 = 0f;
            while (t0 < hold)
            {
                t0 += Time.deltaTime;
                rt.anchoredPosition = CustomerView.EmotePos + new Vector2(0f, Mathf.Min(1f, t0 / hold) * 14f);
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t0 * 9f) * 8f * Mathf.Max(0f, 1f - t0 / hold));
                yield return null;
            }
            yield return Tween.Run(.2f, k => { GrillView.SetAlpha(e, 1f - k); rt.localScale = Vector3.one * (1f + k * .2f); });
            e.gameObject.SetActive(false);
        }

        /// <summary>A wrong drop on someone's tray: they frown and an anger mark pops.</summary>
        void Annoy(int tray)
        {
            var c = CustomerForTray(tray);
            if (c == null || c.busy || c.anim) return;
            MoodFor(c, Mood.Angry, 1.1f);
            ShowEmote(c, art.emoteAngry, .7f);
        }

        /// <summary>Their tray got closer to the order (only their food on it): a happy face; one piece to go = yummy face.</summary>
        void Progress(int tray)
        {
            var c = CustomerForTray(tray);
            if (c == null || c.busy || c.anim || c.food < 0) return;
            int n = _board.Count(tray);
            if (n < 2) return;
            for (int i = 0; i < n; i++) if (_board.Get(tray, i) != c.food) return;
            c.waited = 0f;
            MoodFor(c, Mood.Happy, .9f);
            if (n == _board.Pieces - 1) ShowEmote(c, art.emoteYummy, .8f);
        }

        /// <summary>Out of moves: every customer looks worried until the popup closes.</summary>
        void AllWorried()
        {
            foreach (var c in ui.customers)
            {
                if (!c.gameObject.activeSelf || c.busy) continue;
                if (c.moodCo != null) { StopCoroutine(c.moodCo); c.moodCo = null; }
                SetMood(c, Mood.Angry);
                ShowEmote(c, art.emoteSweat, 1.2f);
            }
            StartCoroutine(CalmWhenClosed());
        }

        IEnumerator CalmWhenClosed()
        {
            while (IsOpen(ui.stuckPopup)) yield return null;
            foreach (var c in ui.customers) if (c.gameObject.activeSelf && !c.anim) { SetMood(c, Mood.Wait); c.waited = 0f; }
        }

        /// <summary>Every frame: idle breathing (each on its own phase) and impatience when nothing happens for them.</summary>
        void CustomersLive()
        {
            float t = Time.time, dt = Time.deltaTime;
            bool clock = !_busy && !_won && !_dragging && !Covered && !_autoPlay && !AnyPopupOpen() && _levelNumber > tutorialLevels;
            for (int i = 0; i < ui.customers.Length; i++)
            {
                var c = ui.customers[i];
                if (!c.gameObject.activeSelf || c.busy || c.anim) continue;
                float s = Mathf.Sin(t * 2.1f + i * 1.7f);
                c.body.localScale = new Vector3(1f + s * .012f, 1f - s * .012f, 1f);
                if (!clock) continue;
                c.waited += dt;
                if (c.waited > patienceSeconds + i * 4f)
                {
                    c.waited = -10f;   // the next sigh comes later
                    MoodFor(c, Mood.Angry, 1.6f);
                    ShowEmote(c, art.emoteSweat, 1.1f);
                }
            }
        }

        // ------------------------------------------------------------------ cooking (cookOnComplete)
        /// <summary>
        /// The tray is full and right: the coals flare, the raw pieces puff and turn grilled one after the other with a
        /// sizzle, then the serve continues. Raw art: FoodRaw_v1 (one drawing per food, shaped like its grilled piece).
        /// </summary>
        IEnumerator CookTray(GrillView v, GameArt.FoodArt fa, int pieces)
        {
            yield return new WaitForSeconds(.5f);   // after the completion jiggle (Sizzle) settles
            var hl = v.highlight;
            var hlColor = hl.color;
            hl.color = new Color(1f, .62f, .22f, 0f);   // orange flare from the coals
            StartCoroutine(Tween.Run(.75f, k => GrillView.SetAlpha(hl, Mathf.Sin(k * Mathf.PI) * .85f)));
            audioManager.Play(art.sizzle, .85f, 1.08f);
            Haptics.Light();
            for (int i = pieces - 1; i >= 0; i--)   // bottom piece first, up the skewer
            {
                StartCoroutine(Puff(v.slots[i]));
                StartCoroutine(CookPiece(v, i, fa));
                yield return new WaitForSeconds(.07f);
            }
            yield return new WaitForSeconds(.4f);
            hl.color = hlColor;
            GrillView.SetAlpha(hl, 0f);
        }

        IEnumerator CookPiece(GrillView v, int i, GameArt.FoodArt fa)
        {
            var rt = v.slots[i];
            yield return Tween.Run(.1f, k => rt.localScale = Vector3.one * (1f + k * .14f));
            v.foods[i].sprite = fa.grilled;
            v.foodShadows[i].sprite = fa.shadow;
            yield return Tween.Run(.22f, k => rt.localScale = Vector3.one * Mathf.LerpUnclamped(1.14f, 1f, Ease.OutBack(k, 2f)));
            rt.localScale = Vector3.one;
        }
    }
}
