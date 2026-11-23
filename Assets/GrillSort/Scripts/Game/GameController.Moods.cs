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

        // Moods are STICKY (user 2026-11-23: "when the expression changes it should not go back to normal, only when they
        // get their item, and it should follow the time consumed"):
        //  - every customer has a clock from the moment their order appears (not reset by progress);
        //  - normal face -> happy once their tray gets their food (stays happy) -> impatient after patienceSeconds
        //    (stays impatient, with a sweat / anger pop every impatientNagSeconds);
        //  - a wrong drop on their tray or running out of moves makes them impatient at once (stays);
        //  - only being served changes it again (they cheer and leave; the next person starts normal).
        // The state lives by customer id (VisTray.who), so it survives the row re-binding after every serve.

        [Header("Customers")]
        [Tooltip("Seconds after their order appears before a customer turns impatient (the clock is stopped on tutorial levels and while a popup / the Home screen is up)")]
        public float patienceSeconds = 40f;
        [Tooltip("The first customers of a level start their clock this many seconds apart, so they don't all turn at once")]
        public float patienceStagger = 6f;
        [Tooltip("An impatient customer pops a sweat / anger emote this often")]
        public float impatientNagSeconds = 14f;

        sealed class CustomerMood { public Mood mood; public float waited, nag; }
        readonly System.Collections.Generic.Dictionary<int, CustomerMood> _moods = new System.Collections.Generic.Dictionary<int, CustomerMood>();
        int _lastCustomer;

        /// <summary>A new person in the line (their clock starts at <paramref name="clock"/>, negative = a head start).</summary>
        int NewCustomer(float clock)
        {
            int id = ++_lastCustomer;
            _moods[id] = new CustomerMood { mood = Mood.Wait, waited = clock };
            return id;
        }

        CustomerMood MoodOf(CustomerView c)
        {
            if (!_moods.TryGetValue(c.who, out var m)) { m = new CustomerMood(); _moods[c.who] = m; }
            return m;
        }

        /// <summary>Changes the face and remembers it (it stays until they're served).</summary>
        void Feel(CustomerView c, Mood m)
        {
            MoodOf(c).mood = m;
            if (!c.anim) SetMood(c, m);
        }

        void SetMood(CustomerView c, Mood m)
        {
            var set = m == Mood.Happy ? art.customersHappy : m == Mood.Angry ? art.customersAngry : art.customers;
            if (set == null || set.Length == 0) set = art.customers;
            c.animal.sprite = set[c.face % set.Length];
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

        /// <summary>A wrong drop on someone's tray: they turn impatient (and stay so) and an anger mark pops.</summary>
        void Annoy(int tray)
        {
            var c = CustomerForTray(tray);
            if (c == null || c.busy || c.anim) return;
            Feel(c, Mood.Angry);
            MoodOf(c).nag = 0f;
            ShowEmote(c, art.emoteAngry, .7f);
        }

        /// <summary>
        /// Their tray got closer to the order (only their food on it): happy (stays) unless they're already impatient;
        /// one piece to go = a yummy pop either way.
        /// </summary>
        void Progress(int tray)
        {
            var c = CustomerForTray(tray);
            if (c == null || c.busy || c.anim || c.food < 0) return;
            int n = _board.Count(tray);
            if (n < 2) return;
            for (int i = 0; i < n; i++) if (_board.Get(tray, i) != c.food) return;
            if (MoodOf(c).mood != Mood.Angry) Feel(c, Mood.Happy);
            if (n == _board.Pieces - 1) ShowEmote(c, art.emoteYummy, .8f);
        }

        /// <summary>Out of moves: every customer turns impatient (and stays so until served).</summary>
        void AllWorried()
        {
            foreach (var c in ui.customers)
            {
                if (!c.gameObject.activeSelf || c.busy) continue;
                if (c.moodCo != null) { StopCoroutine(c.moodCo); c.moodCo = null; }
                Feel(c, Mood.Angry);
                MoodOf(c).nag = 0f;
                ShowEmote(c, art.emoteSweat, 1.2f);
            }
        }

        /// <summary>Every frame: idle breathing (each on its own phase) and each customer's clock.</summary>
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
                if (!clock || c.food < 0) continue;
                var m = MoodOf(c);
                m.waited += dt;
                if (m.mood != Mood.Angry)
                {
                    if (m.waited < patienceSeconds) continue;
                    Feel(c, Mood.Angry);   // waited too long: impatient from now until served
                    m.nag = 0f;
                    ShowEmote(c, art.emoteSweat, 1.1f);
                    continue;
                }
                m.nag += dt;
                if (m.nag >= impatientNagSeconds)
                {
                    m.nag = 0f;
                    ShowEmote(c, Random.value < .5f ? art.emoteAngry : art.emoteSweat, 1f);
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
