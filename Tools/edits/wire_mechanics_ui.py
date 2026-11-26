"""Wires wrapped food + locked trays into the views, the scene builder and the game flow (2026-11-26)."""
import io, os

ROOT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "GrillSort")


def edit(rel, pairs):
    p = os.path.join(ROOT, rel)
    s = io.open(p, encoding="utf-8", newline="").read()
    crlf = "\r\n" in s
    s = s.replace("\r\n", "\n")
    for a, b in pairs:
        assert a in s, (rel, a[:80])
        s = s.replace(a, b, 1)
    if crlf:
        s = s.replace("\n", "\r\n")
    io.open(p, "w", encoding="utf-8", newline="").write(s)


# ---------------------------------------------------------------- GameArt / GameUi
edit("Scripts/Game/GameArt.cs", [
("""        public Sprite skewer;""",
"""        public Sprite skewer;
        /// <summary>Wrapped "?" food (foil) and the chains over a locked tray (demo mechanics 2026-11-26).</summary>
        public Sprite foilWrap, lockChains, iconLock;"""),
])
edit("Scripts/UI/GameUi.cs", [
("""        public Image[] flyerFoods;""",
"""        public Image[] flyerFoods;
        /// <summary>Foil over a flying piece that is still wrapped.</summary>
        public Image[] flyerFoils;"""),
])

# ---------------------------------------------------------------- GrillView
edit("Scripts/Game/GrillView.cs", [
("""        public Image[] sparkles;
        public Image perfect;""",
"""        public Image[] sparkles;
        public Image perfect;
        /// <summary>Foil over each food slot: the piece is wrapped ("?") until it becomes the tray's bottom piece.</summary>
        public Image[] foils;
        /// <summary>Locked tray: chains over the tray, a dim layer and a sign showing the food whose order opens it.</summary>
        public RectTransform lockRoot;
        public Image lockChains, lockDim, lockSign, lockFood, lockIcon;
        [NonSerialized] public int lockShown = -1;"""),
("""                var t = new System.Collections.Generic.List<Graphic> { shadow, ember, glow, highlight, grill, coalGlow, coalHotA, coalHotB, skewer };""",
"""                var t = new System.Collections.Generic.List<Graphic> { shadow, ember, glow, highlight, grill, coalGlow, coalHotA, coalHotB, skewer, lockChains, lockDim };"""),
("""                if (perfect != null) m.Add(perfect.rectTransform);""",
"""                if (perfect != null) m.Add(perfect.rectTransform);
                if (lockSign != null) m.Add(lockSign.rectTransform);"""),
("""        public void HideSlot(int i) { foods[i].enabled = false; foodShadows[i].enabled = false; }""",
"""        public void HideSlot(int i)
        {
            foods[i].enabled = false; foodShadows[i].enabled = false;
            if (foils != null && i < foils.Length) foils[i].gameObject.SetActive(false);
        }

        /// <summary>Shows the foil on every wrapped slot (hid[i] = piece i is wrapped).</summary>
        public void SetFoils(bool[] hid)
        {
            if (foils == null) return;
            for (int i = 0; i < foils.Length; i++)
            {
                bool on = hid != null && i < hid.Length && hid[i] && foods[i].enabled;
                var f = foils[i];
                f.gameObject.SetActive(on);
                if (!on) continue;
                f.rectTransform.sizeDelta = foods[i].rectTransform.sizeDelta * 1.12f;
                f.rectTransform.localScale = Vector3.one;
                f.color = Color.white;
            }
        }

        /// <summary>Shows the lock (chains + sign with <paramref name="food"/>), or hides it when food &lt; 0.</summary>
        public void SetLock(int food, GameArt art)
        {
            lockShown = food;
            if (lockRoot == null) return;
            lockRoot.gameObject.SetActive(food >= 0);
            if (food < 0) return;
            lockRoot.localScale = Vector3.one;
            lockRoot.anchoredPosition = Vector2.zero;
            foreach (var g in new Graphic[] { lockChains, lockDim, lockSign, lockFood, lockIcon }) if (g != null) { var c = g.color; c.a = g == lockDim ? .28f : 1f; g.color = c; }
            if (lockFood != null) lockFood.sprite = art.foods[food].grilled;
        }"""),
])

# ---------------------------------------------------------------- scene builder
edit("Editor/GrillSortSetup.cs", [
("""            a.skewer = Load<Sprite>(Art + "Board/skewer_cartoon.png");""",
"""            a.skewer = Load<Sprite>(Art + "Board/skewer_cartoon.png");
            a.foilWrap = Load<Sprite>(Art + "Generated/foil_wrap.png");
            a.lockChains = Load<Sprite>(Art + "Generated/lock_chains.png");
            a.iconLock = S("UI/Icons/icon_lock.png");"""),
("""                gv.slots = new RectTransform[slotCount]; gv.foods = new Image[slotCount]; gv.foodShadows = new Image[slotCount];""",
"""                gv.slots = new RectTransform[slotCount]; gv.foods = new Image[slotCount]; gv.foodShadows = new Image[slotCount];
                gv.foils = new Image[slotCount];"""),
("""                    gv.foods[s] = Img("Food", slot, fa.grilled, new Vector2(w, w), Vector2.zero);
                    gv.slots[s] = slot;
                }""",
"""                    gv.foods[s] = Img("Food", slot, fa.grilled, new Vector2(w, w), Vector2.zero);
                    gv.foils[s] = Img("Foil", slot, _art.foilWrap, new Vector2(w, w) * 1.12f, Vector2.zero);
                    gv.foils[s].gameObject.SetActive(false);
                    gv.slots[s] = slot;
                }
                // locked tray (Sort Streak's locked flowers): dim layer + chains over the whole tray, a sign with the food
                var lockRoot = Rect("Lock", root, new Vector2(GrillView.GrillW, GrillView.GrillH), Vector2.zero);
                gv.lockRoot = lockRoot;
                gv.lockDim = Img("Dim", lockRoot, S("Generated/soft_rect.png"), new Vector2(GrillView.GrillW - 10, GrillView.GrillH - 10), Vector2.zero);
                gv.lockDim.color = new Color(.05f, .03f, .08f, .28f);
                gv.lockChains = Img("Chains", lockRoot, _art.lockChains, new Vector2(GrillView.GrillW, GrillView.GrillH), Vector2.zero);
                gv.lockSign = Img("Sign", lockRoot, S("Generated/disc.png"), new Vector2(132, 132), new Vector2(0, 40));
                gv.lockSign.color = new Color(1f, .97f, .9f, 1f);
                gv.lockFood = Img("Food", gv.lockSign.rectTransform, _art.foods[0].grilled, new Vector2(96, 96), new Vector2(0, 4));
                gv.lockIcon = ImgW("Padlock", gv.lockSign.rectTransform, _art.iconLock, 62, new Vector2(46, -44));
                lockRoot.gameObject.SetActive(false);"""),
("""                ui.flyerFoods[f] = Img("Food", fr, fa.grilled, new Vector2(w, w), Vector2.zero);""",
"""                ui.flyerFoods[f] = Img("Food", fr, fa.grilled, new Vector2(w, w), Vector2.zero);
                ui.flyerFoils[f] = Img("Foil", fr, _art.foilWrap, new Vector2(w, w) * 1.12f, Vector2.zero);
                ui.flyerFoils[f].gameObject.SetActive(false);"""),
("""            ui.flyers = new RectTransform[flyerCount]; ui.flyerFoods = new Image[flyerCount]; ui.flyerShadows = new Image[flyerCount];""",
"""            ui.flyers = new RectTransform[flyerCount]; ui.flyerFoods = new Image[flyerCount]; ui.flyerShadows = new Image[flyerCount];
            ui.flyerFoils = new Image[flyerCount];"""),
("""        static readonly string[] AtlasFiles = { "badge.png",""",
"""        static readonly string[] AtlasFiles = { "foil_wrap.png", "lock_chains.png", "badge.png","""),
("""Art + "Generated/glow_hd.png", Art + "Generated/rays_hd.png", Art + "Generated/card_cream.png" });""",
"""Art + "Generated/glow_hd.png", Art + "Generated/rays_hd.png", Art + "Generated/card_cream.png", Art + "Generated/foil_wrap.png", Art + "Generated/lock_chains.png" });"""),
])

# ---------------------------------------------------------------- GameController
edit("Scripts/Game/GameController.cs", [
("""        struct VisTray { public int[] stack; public int target; public int face; public int who; }""",
"""        // hid: wrapped pieces (same length as stack); lockPlus1: food that unlocks this tray + 1 (0 = not locked)
        struct VisTray { public int[] stack; public int target; public int face; public int who; public bool[] hid; public int lockPlus1; }"""),
("""                _vis.Add(new VisTray { stack = _board.Stack(s), target = t0, face = t0 >= 0 ? NextFace() : -1, who = t0 >= 0 ? NewCustomer(-patienceStagger * firstRow++) : 0 });""",
"""                _vis.Add(new VisTray { stack = _board.Stack(s), target = t0, face = t0 >= 0 ? NextFace() : -1, who = t0 >= 0 ? NewCustomer(-patienceStagger * firstRow++) : 0, hid = _board.HiddenStack(s), lockPlus1 = _board.Lock(s) + 1 });"""),
("""                _vis.Add(new VisTray { stack = _board.Stack(s), target = t, face = face, who = who });""",
"""                _vis.Add(new VisTray { stack = _board.Stack(s), target = t, face = face, who = who, hid = _board.HiddenStack(s), lockPlus1 = _board.Lock(s) + 1 });"""),
("""                _vis.Add(new VisTray { stack = _board.QueueStack(queueIndex), target = t, face = t >= 0 ? NextFace() : -1, who = t >= 0 ? NewCustomer(0f) : 0 });""",
"""                var qh = _board.QueueHidden(queueIndex);
                if (qh.Length > 0) qh[qh.Length - 1] = false;   // the bottom piece is always visible
                _vis.Add(new VisTray { stack = _board.QueueStack(queueIndex), target = t, face = t >= 0 ? NextFace() : -1, who = t >= 0 ? NewCustomer(0f) : 0, hid = qh, lockPlus1 = _board.QueueLock(queueIndex) + 1 });"""),
("""            _vis.Add(new VisTray { stack = new int[0], target = -1, face = -1 });""",
"""            _vis.Add(new VisTray { stack = new int[0], target = -1, face = -1, hid = new bool[0] });"""),
("""                g.Show(_vis[i].stack, art, cookOnComplete);""",
"""                g.Show(_vis[i].stack, art, cookOnComplete);
                g.SetFoils(_vis[i].hid);
                g.SetLock(_vis[i].lockPlus1 - 1, art);"""),
("""        void SetVis(int s, int[] stack) { var t = _vis[s]; t.stack = stack; _vis[s] = t; }""",
"""        void SetVis(int s, int[] stack) { var t = _vis[s]; t.stack = stack; _vis[s] = t; }
        void SetVis(int s, int[] stack, bool[] hid) { var t = _vis[s]; t.stack = stack; t.hid = hid; _vis[s] = t; }

        const string LockedMsg = "This tray is locked. Serve the food on its sign first!";

        /// <summary>A wrapped piece just became the bottom one: the foil pops off.</summary>
        IEnumerator RevealPop(GrillView v, int i)
        {
            if (v.foils == null || i < 0 || i >= v.foils.Length) yield break;
            var f = v.foils[i];
            f.rectTransform.sizeDelta = v.foods[i].rectTransform.sizeDelta * 1.12f;
            f.gameObject.SetActive(true);
            audioManager.Play(art.pop, .6f, 1.25f);
            Haptics.Light();
            StartCoroutine(Tween.Bounce(v.slots[i], .16f, .3f));
            yield return Tween.Run(.32f, k =>
            {
                f.rectTransform.localScale = Vector3.one * (1f + .45f * k);
                f.color = new Color(1f, 1f, 1f, 1f - k);
            }, Ease.OutCubic);
            f.gameObject.SetActive(false);
            f.color = Color.white; f.rectTransform.localScale = Vector3.one;
        }

        /// <summary>Serving <paramref name="food"/> opens every tray locked by it: the chains drop away.</summary>
        void UnlockFor(int food)
        {
            for (int j = 0; j < _vis.Count; j++)
            {
                if (_vis[j].lockPlus1 - 1 != food) continue;
                var t = _vis[j]; t.lockPlus1 = 0; _vis[j] = t;
                if (j < ui.grills.Length) StartCoroutine(UnlockAnim(ui.grills[j]));
            }
        }

        IEnumerator UnlockAnim(GrillView v)
        {
            if (v.lockRoot == null || !v.lockRoot.gameObject.activeSelf) { v.SetLock(-1, art); yield break; }
            audioManager.Play(art.whoosh, .5f, .9f);
            Haptics.Medium();
            StartCoroutine(Tween.Bounce(v.lockSign.rectTransform, .25f, .3f));
            yield return new WaitForSeconds(.25f);
            var gs = new Graphic[] { v.lockChains, v.lockDim, v.lockSign, v.lockFood, v.lockIcon };
            var a0 = new float[gs.Length];
            for (int i = 0; i < gs.Length; i++) a0[i] = gs[i].color.a;
            yield return Tween.Run(.45f, k =>
            {
                v.lockRoot.anchoredPosition = new Vector2(0f, -160f * k * k);
                for (int i = 0; i < gs.Length; i++) { var c = gs[i].color; c.a = a0[i] * (1f - k); gs[i].color = c; }
            });
            v.SetLock(-1, art);
            audioManager.Play(art.pop, .5f, 1.1f);
        }

        void SetFlyerFoil(int i, bool on)
        {
            if (ui.flyerFoils == null || i >= ui.flyerFoils.Length) return;
            var f = ui.flyerFoils[i];
            f.gameObject.SetActive(on);
            if (!on) return;
            f.rectTransform.sizeDelta = ui.flyerFoods[i].rectTransform.sizeDelta * 1.12f;
            f.rectTransform.localScale = Vector3.one; f.color = Color.white;
        }"""),
# flyers keep their foil
("""            ui.flyerFoods[i].color = Color.white; ui.flyerShadows[i].color = Color.white;""",
"""            ui.flyerFoods[i].color = Color.white; ui.flyerShadows[i].color = Color.white;
            SetFlyerFoil(i, false);"""),
("""                PrepareFlyer(k, art.foods[_board.Get(g, si)], cookOnComplete);
                var fl = ui.flyers[k];""",
"""                PrepareFlyer(k, art.foods[_board.Get(g, si)], cookOnComplete);
                SetFlyerFoil(k, _board.IsHidden(g, si));
                var fl = ui.flyers[k];"""),
("""            PrepareFlyer(flyer, art.foods[food], cookOnComplete);
            src.HideSlot(si);""",
"""            PrepareFlyer(flyer, art.foods[food], cookOnComplete);
            SetFlyerFoil(flyer, src.foils != null && si < src.foils.Length && src.foils[si].gameObject.activeSelf);
            src.HideSlot(si);"""),
# capture wrapped state before the move, show it (and the reveal) after the flight
("""            for (int k = 0; k < m.Count; k++) preDst[dstN + k] = moved[k];
            var served = _board.Apply(m);""",
"""            for (int k = 0; k < m.Count; k++) preDst[dstN + k] = moved[k];
            var hidSrc = new bool[m.Index];
            for (int i = 0; i < m.Index; i++) hidSrc[i] = _board.IsHidden(m.From, i);
            var hidDst = new bool[dstN + m.Count];
            for (int i = 0; i < dstN; i++) hidDst[i] = _board.IsHidden(m.To, i);
            for (int k = 0; k < m.Count; k++) hidDst[dstN + k] = _board.IsHidden(m.From, m.Index + k);
            bool revSrc = m.Index > 0 && hidSrc[m.Index - 1];
            if (revSrc) hidSrc[m.Index - 1] = false;
            bool revDst = hidDst.Length > 0 && hidDst[hidDst.Length - 1];
            if (revDst) hidDst[hidDst.Length - 1] = false;
            var served = _board.Apply(m);"""),
("""            src.Show(preSrc, art, cookOnComplete);
            dst.Show(preDst, art, cookOnComplete);
            SetVis(m.From, preSrc); SetVis(m.To, preDst);""",
"""            src.Show(preSrc, art, cookOnComplete);
            dst.Show(preDst, art, cookOnComplete);
            src.SetFoils(hidSrc); dst.SetFoils(hidDst);
            if (revSrc) StartCoroutine(RevealPop(src, m.Index - 1));
            if (revDst) StartCoroutine(RevealPop(dst, hidDst.Length - 1));
            SetVis(m.From, preSrc, hidSrc); SetVis(m.To, preDst, hidDst);"""),
# serving opens locks
("""            yield return ShiftRow(g, e.Arrived, queueIndex, c);
        }""",
"""            yield return ShiftRow(g, e.Arrived, queueIndex, c);
            UnlockFor(e.Food);
        }"""),
# refusals
("""            if (_board.Capacity - _board.Count(to) < group) return "This tray can't hold that many!";
            if (!_board.FillsUniform(from, s0, group, to)) return "Fill a tray to the top with one food only!";""",
"""            if (_board.IsLocked(to)) return LockedMsg;
            if (_board.Capacity - _board.Count(to) < group) return "This tray can't hold that many!";
            if (!_board.FillsUniform(from, s0, group, to)) return "Fill a tray to the top with one food only!";
            if (!_board.IsEmpty(to) && _board.HasHidden(from, s0, group)) return "Wrapped food can only go on an empty tray!";"""),
("""            if (_selected < 0)
            {
                if (_board.IsEmpty(g)) { StartCoroutine(Tween.Shake(ui.grills[g].rect, 8f, .2f)); return; }""",
"""            if (_selected < 0)
            {
                if (_board.IsEmpty(g)) { StartCoroutine(Tween.Shake(ui.grills[g].rect, 8f, .2f)); return; }
                if (_board.IsLocked(g)) { StartCoroutine(Tween.Shake(ui.grills[g].rect, 8f, .2f)); audioManager.Play(art.invalid, .6f); Toast(LockedMsg); return; }"""),
("""            if (_board.IsEmpty(g)) { StartCoroutine(Tween.Shake(ui.grills[g].rect, 8f, .2f)); return; }
            int start = _board.MoveStart(moveRule, g, moveRule == MoveRule.AnyWithAbove ? Mathf.Min(slot, _board.Count(g) - 1) : -1);""",
"""            if (_board.IsEmpty(g)) { StartCoroutine(Tween.Shake(ui.grills[g].rect, 8f, .2f)); return; }
            if (_board.IsLocked(g)) { StartCoroutine(Tween.Shake(ui.grills[g].rect, 8f, .2f)); audioManager.Play(art.invalid, .6f); Toast(LockedMsg); return; }
            int start = _board.MoveStart(moveRule, g, moveRule == MoveRule.AnyWithAbove ? Mathf.Min(slot, _board.Count(g) - 1) : -1);"""),
])
print("ok")
