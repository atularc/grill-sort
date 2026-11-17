using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GrillSort
{
    /// <summary>
    /// Runs the game on the scene the builder authored. Nothing is instantiated or loaded at runtime:
    /// it only toggles, re-skins and tweens objects that already exist in the Canvas.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        [Header("Data")]
        public GameArt art;
        public LevelBank bank;
        public GameUi ui;
        public AudioManager audioManager;

        [Header("Rules")]
        [Tooltip("TopGroup = classic Water Sort (default): the whole top run of the same food moves. AnyWithAbove = grab any piece, everything above comes along. SingleTop = only the top piece moves.")]
        public MoveRule moveRule = MoveRule.SingleTop;
        [Tooltip("Pieces show Raw while sorting and cook Raw > Medium > Grilled when a skewer completes.")]
        public bool cookOnComplete = false;
        [Tooltip("Demo / review builds: only levels 1-10 can be played.")]
        public bool limitToTenLevels = false;
        [Tooltip("Free idle hint on levels <= 10 after this many seconds.")]
        public float idleHintSeconds = 6f;
        [Tooltip("Log every move / booster to the console (debugging).")]
        public bool verboseLog = false;

        public enum SelectStyle { Full, LiftOnly, LiftGrowShadow, HotGrill, FoodGlow, Spotlight }
        [Tooltip("How a picked-up piece looks. Full = lift+grow+float+floating shadow+hot grill. FoodGlow = warm glow under the piece (v5 mockup idea). Spotlight = other grills dim.")]
        public SelectStyle selectStyle = SelectStyle.Full;

        [Header("Testing")]
        [Tooltip("Start straight at Test Level instead of the saved level. Progress is not saved while this is on. Changing the number during Play mode loads that level right away.")]
        public bool useTestLevel = false;
        [Min(1)] public int testLevel = 1;
        int _loadedTestLevel = -1;

        public const float Lift = 70f;
        static readonly string[] BoosterPlacement = { "undo", "hint", "shuffle", "extra" };

        Board _board;
        LevelData _level;
        int _levelNumber, _moves, _served, _totalOrders, _skewers;
        readonly List<int> _pending = new List<int>();
        int _nextAnimal;
        int _selected = -1, _selStart;
        bool _busy, _extraUsed, _won, _autoPlay;
        float _idle;
        Coroutine _hintCo, _tipCo, _toastCo;
        readonly Stack<(Board board, int moves)> _history = new Stack<(Board, int)>();
        int _shownCoins;
        int _activeCustomers;

        public int LevelNumber => _levelNumber;
        public bool Busy => _busy;
        public Board CurrentBoard => _board;

        // ------------------------------------------------------------------ setup
        void Start()
        {
            Application.targetFrameRate = 60;
            Services.Ads.Init();
            Haptics.Enabled = SaveData.Haptic;
            if (audioManager != null) audioManager.Enabled = SaveData.Sound;

            for (int i = 0; i < ui.grills.Length; i++) { ui.grills[i].index = i; ui.grills[i].onTap = OnGrillTap; }
            ui.pauseButton.onClick.AddListener(() => { Click(); OpenPopup(ui.pausePopup, ui.pausePanel); RefreshPauseTexts(); });
            ui.pauseResume.onClick.AddListener(() => { Click(); ClosePopup(ui.pausePopup); });
            ui.pauseRestart.onClick.AddListener(() => { Click(); ClosePopup(ui.pausePopup); LoadLevel(_levelNumber); });
            ui.pauseSound.onClick.AddListener(() => { SaveData.Sound = !SaveData.Sound; audioManager.Enabled = SaveData.Sound; Click(); RefreshPauseTexts(); });
            ui.pauseHaptics.onClick.AddListener(() => { SaveData.Haptic = !SaveData.Haptic; Haptics.Enabled = SaveData.Haptic; Haptics.Medium(); Click(); RefreshPauseTexts(); });
            for (int i = 0; i < ui.boosters.Length; i++) { int k = i; ui.boosters[i].onClick.AddListener(() => UseBooster(k)); }
            ui.stuckUndo.onClick.AddListener(() => { ClosePopup(ui.stuckPopup); UseBooster(0); });
            ui.stuckShuffle.onClick.AddListener(() => { ClosePopup(ui.stuckPopup); UseBooster(2); });
            ui.stuckExtra.onClick.AddListener(() =>
            {
                ClosePopup(ui.stuckPopup);
                Services.Ads.ShowRewarded("stuck_extra", ok => { if (ok) { if (!AddExtraSkewer()) Toast("No room for another skewer"); } });
            });
            ui.stuckRetry.onClick.AddListener(() => { Click(); ClosePopup(ui.stuckPopup); LoadLevel(_levelNumber); });
            ui.winNext.onClick.AddListener(() => CollectWin(1));
            ui.winDouble.onClick.AddListener(() => Services.Ads.ShowRewarded("win_double", ok => CollectWin(ok ? 2 : 1)));
            ui.messageOk.onClick.AddListener(() => { Click(); ClosePopup(ui.messagePopup); });

            foreach (var p in new[] { ui.winPopup, ui.pausePopup, ui.stuckPopup, ui.messagePopup }) HidePopupNow(p);
            ui.tip.alpha = 0f; ui.toast.alpha = 0f;
            _shownCoins = SaveData.Coins;
            ui.coinText.text = _shownCoins.ToString("N0");

            int start = SaveData.Level;
            if (useTestLevel) start = Mathf.Max(1, testLevel);
            else if (limitToTenLevels && start > 10) start = 1;
            _loadedTestLevel = useTestLevel ? start : -1;
            LoadLevel(start);
        }

        public void LoadLevel(int number)
        {
            StopAllCoroutines();
            _hintCo = _tipCo = null;
            _selectCo = _selectGlowCo = null;
            _levelNumber = Mathf.Max(1, number);
            _level = bank.Get(_levelNumber, moveRule);
            _board = _level.ToBoard();
            _skewers = _board.SkewerCount;
            _moves = 0; _served = 0; _selected = -1; _busy = false; _extraUsed = false; _won = false; _idle = 0f;
            _history.Clear();
            _totalOrders = _level.orders.Length;
            _pending.Clear(); _pending.AddRange(_level.orders);
            _nextAnimal = 0;

            ApplyTheme();

            for (int i = 0; i < ui.grills.Length; i++)
            {
                var g = ui.grills[i];
                bool on = i < _skewers;
                g.gameObject.SetActive(on);
                if (!on) continue;
                g.ResetFx();
                g.SetLit(false, art);
                g.Show(_board.Stack(i), art, cookOnComplete);
            }
            ui.layout.activeGrills = _skewers;
            ui.layout.Apply();
            foreach (var f in ui.flyers) f.gameObject.SetActive(false);

            SetupCustomers();
            ui.levelText.text = $"Level {_levelNumber}";
            UpdateOrders(false);
            RefreshBoosters();
            ShowTip();
            StartCoroutine(IntroAnim());
        }

        void ApplyTheme()
        {
            var theme = art.ThemeForLevel(_levelNumber);
            ui.background.sprite = theme.Back;
            ui.front.sprite = theme.front;
            ui.layout.counterTop = theme.counterTop;
            ui.layout.Apply();
            int idx = System.Array.IndexOf(art.themes, theme);
            if (idx != SaveData.LastTheme)
            {
                if (idx > SaveData.LastTheme && _levelNumber > 1)
                    StartCoroutine(Tween.Delay(.6f, () => Toast($"New stall unlocked: {theme.displayName}!")));
                SaveData.LastTheme = idx;
            }
        }

        IEnumerator IntroAnim()
        {
            _busy = true;
            for (int i = 0; i < _skewers; i++)
            {
                var g = ui.grills[i];
                for (int s = 0; s < g.slots.Length; s++) g.slots[s].localScale = Vector3.zero;
            }
            for (int i = 0; i < _skewers; i++)
            {
                var g = ui.grills[i];
                for (int s = 0; s < g.slots.Length; s++)
                {
                    var slot = g.slots[s];
                    StartCoroutine(DropIn(slot, GrillView.SlotY[s], (i * .05f) + s * .07f));
                }
            }
            yield return new WaitForSeconds(.25f + _skewers * .05f + .35f);
            _busy = false;
            if (_levelNumber <= 3) _idle = idleHintSeconds - 2.5f; // hand-holding: hint comes quickly
        }

        IEnumerator DropIn(RectTransform slot, float y, float delay)
        {
            yield return new WaitForSeconds(delay);
            slot.anchoredPosition = new Vector2(0f, y + 90f);
            slot.localScale = Vector3.one;
            yield return Tween.Move(slot, new Vector2(0f, y), .22f, Ease.InCubic);
            StartCoroutine(Tween.Bounce(slot, .12f, .25f));
        }

        // ------------------------------------------------------------------ customers / orders
        void SetupCustomers()
        {
            _activeCustomers = Mathf.Min(ui.customers.Length, _totalOrders);
            for (int i = 0; i < ui.customers.Length; i++)
            {
                var c = ui.customers[i];
                bool on = i < _activeCustomers;
                c.gameObject.SetActive(on);
                c.busy = false;
                if (!on) continue;
                float x = (i - (_activeCustomers - 1) * .5f) * 260f;
                c.rect.anchoredPosition = new Vector2(x, 0f);
                AssignNext(c);
                StartCoroutine(Arrive(c, .15f + i * .12f));
            }
        }

        void AssignNext(CustomerView c)
        {
            if (_pending.Count == 0) { c.food = -1; return; }
            c.food = _pending[0];
            _pending.RemoveAt(0);
            c.animal.sprite = art.customers[_nextAnimal++ % art.customers.Length];
            c.order.sprite = art.foods[c.food].grilled;
            c.check.gameObject.SetActive(false);
        }

        IEnumerator Arrive(CustomerView c, float delay)
        {
            c.busy = true;
            c.body.anchoredPosition = new Vector2(0f, CustomerView.BodyY - 330f);
            c.bubble.localScale = Vector3.zero;
            c.group.alpha = 1f;
            yield return new WaitForSeconds(delay);
            yield return Tween.Move(c.body, new Vector2(0f, CustomerView.BodyY), .35f, t => Ease.OutBack(t, 1.2f));
            yield return Tween.Scale(c.bubble, Vector3.one, .3f, t => Ease.OutBack(t));
            c.busy = false;
        }

        IEnumerator LeaveAndNext(CustomerView c)
        {
            c.busy = true;
            yield return new WaitForSeconds(.55f);
            StartCoroutine(Tween.Scale(c.bubble, Vector3.zero, .2f, Ease.InCubic));
            yield return Tween.Move(c.body, new Vector2(0f, CustomerView.BodyY - 330f), .3f, Ease.InCubic);
            if (_pending.Count > 0)
            {
                AssignNext(c);
                yield return Arrive(c, .1f);
            }
            else
            {
                c.food = -1;
                c.busy = false;
            }
        }

        CustomerView FindCustomerFor(int food)
        {
            foreach (var c in ui.customers)
                if (c.gameObject.activeSelf && c.food == food && !c.busy) return c;
            return null;
        }

        void UpdateOrders(bool animate)
        {
            ui.ordersText.text = $"Orders {_served} / {_totalOrders}";
            float w = _totalOrders == 0 ? 0f : ui.ordersFillMax * _served / _totalOrders;
            float minW = 70f;
            var fill = ui.ordersFill;
            fill.gameObject.SetActive(_served > 0);
            float target = Mathf.Max(minW, w);
            if (animate && fill.gameObject.activeSelf)
            {
                float from = fill.sizeDelta.x;
                if (_served == 1) from = minW;
                StartCoroutine(Tween.Run(.4f, k => fill.sizeDelta = new Vector2(Mathf.Lerp(from, target, k), fill.sizeDelta.y), Ease.OutCubic));
            }
            else fill.sizeDelta = new Vector2(target, fill.sizeDelta.y);
        }

        // ------------------------------------------------------------------ input
        void Update()
        {
            if (_busy || _won || AnyPopupOpen() || _autoPlay) { _idle = 0f; return; }
            _idle += Time.deltaTime;
            if (_levelNumber <= 10 && _idle >= idleHintSeconds && _hintCo == null && _selected < 0)
            {
                _idle = -4f; // don't spam
                var mv = Solver.NextMove(_board, moveRule);
                if (mv.HasValue) _hintCo = StartCoroutine(HintAnim(mv.Value));
            }
        }

        // ember flicker + occasional smoke wisps so the grills feel hot
        void LateUpdate()
        {
            if (_board == null) return;
            float t = Time.time;
            for (int i = 0; i < _board.SkewerCount && i < ui.grills.Length; i++)
            {
                var e = ui.grills[i].ember;
                if (e == null) continue;
                float f = Mathf.PerlinNoise(t * 1.3f, i * 3.7f);
                GrillView.SetAlpha(e, .2f + f * .22f);
            }
            _smokeTimer -= Time.deltaTime;
            if (_smokeTimer <= 0f)
            {
                _smokeTimer = Random.Range(.5f, 1.2f);
                int g = Random.Range(0, _board.SkewerCount);
                var v = ui.grills[g];
                if (v.smoke != null && !v.smoke.gameObject.activeSelf) StartCoroutine(SmokeWisp(v));
            }
        }

        float _smokeTimer = 1f;

        IEnumerator SmokeWisp(GrillView v)
        {
            var s = v.smoke; var rt = s.rectTransform;
            s.gameObject.SetActive(true);
            float x = Random.Range(-30f, 30f), y0 = Random.Range(250f, 330f);
            float drift = Random.Range(-30f, 30f);
            yield return Tween.Run(2.2f, k =>
            {
                rt.anchoredPosition = new Vector2(x + drift * k, y0 + k * 160f);
                rt.localScale = Vector3.one * (.7f + k * .6f);
                GrillView.SetAlpha(s, Mathf.Sin(k * Mathf.PI) * .2f);
            });
            s.gameObject.SetActive(false);
        }

#if UNITY_EDITOR
        // type a new Test Level in the Inspector during Play mode and it loads immediately
        void OnValidate()
        {
            if (!Application.isPlaying || _board == null || !useTestLevel) return;
            int n = Mathf.Max(1, testLevel);
            if (n == _loadedTestLevel) return;
            _loadedTestLevel = n;
            UnityEditor.EditorApplication.delayCall += () => { if (this != null) LoadLevel(n); };
        }
#endif

        void OnGrillTap(int g, int slot)
        {
            if (_busy || _won || AnyPopupOpen() || g >= _board.SkewerCount) return;
            _idle = 0f;
            StopHint();

            if (_selected < 0)
            {
                if (_board.IsEmpty(g)) { StartCoroutine(Tween.Shake(ui.grills[g].rect, 8f, .2f)); return; }
                int start = _board.MoveStart(moveRule, g, moveRule == MoveRule.AnyWithAbove ? slot : -1);
                Select(g, start);
                return;
            }

            if (g == _selected) { Deselect(); return; }

            int idx = moveRule == MoveRule.AnyWithAbove ? _selStart : -1;
            if (_board.TryMakeMove(moveRule, _selected, idx, g, out var m))
            {
                StartCoroutine(DoMove(m));
            }
            else
            {
                audioManager.Play(art.invalid, .7f);
                Haptics.Light();
                StartCoroutine(Tween.Shake(ui.grills[g].rect));
                Deselect();
            }
        }

        // ---- selected look: piece lifts, grows and floats; its shadow drops away; the grill heats up (no outlines/halos on food)
        const float SelScale = 1.12f;
        static readonly Vector2 SelShadowOffset = new Vector2(16f, -48f);
        Coroutine _selectCo;
        Coroutine _selectGlowCo;

        void Select(int g, int start)
        {
            _selected = g; _selStart = start;
            var v = ui.grills[g];
            if (_selectCo != null) StopCoroutine(_selectCo);
            _selectCo = StartCoroutine(SelectFx(v, start, _board.Count(g)));
            if (selectStyle == SelectStyle.Full || selectStyle == SelectStyle.HotGrill)
            {
                v.SetLit(true, art);
                if (_selectGlowCo != null) StopCoroutine(_selectGlowCo);
                _selectGlowCo = StartCoroutine(Tween.Fade(v.glow, .5f, .18f));
            }
            if (selectStyle == SelectStyle.Spotlight) Spotlight(g, true);
            audioManager.Play(art.pick, .8f, 1f + Random.Range(-.05f, .05f));
            Haptics.Light();
        }

        IEnumerator SelectFx(GrillView v, int start, int end)
        {
            float t = 0f;
            const float rise = .16f;
            while (true)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / rise);
                float e = Ease.OutBack(k, 2f);
                float idle = Mathf.Max(0f, t - rise);
                var st = selectStyle;
                bool fancy = st != SelectStyle.LiftOnly && st != SelectStyle.HotGrill;
                if (!fancy) idle = 0f;
                for (int i = start; i < end; i++)
                {
                    float bob = Mathf.Sin(idle * 4.5f + i * .7f) * 7f * Mathf.Clamp01(idle * 4f);
                    v.slots[i].anchoredPosition = new Vector2(0f, GrillView.SlotY[i] + Lift * e + bob);
                    v.slots[i].localScale = Vector3.one * (fancy ? Mathf.LerpUnclamped(1f, SelScale, e) : 1f);
                    v.slots[i].localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(idle * 3.2f + i) * 2.5f * Mathf.Clamp01(idle * 4f));
                    var sh = v.foodShadows[i];
                    // shadow stays on the grill: counter the lift a bit so it reads as a float, and soften it
                    if (st == SelectStyle.FoodGlow)
                    {
                        sh.rectTransform.anchoredPosition = Vector2.Lerp(GrillView.ShadowOffset, Vector2.zero, k);
                        sh.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.2f, k);
                        sh.color = Color.Lerp(Color.white, new Color(1f, .78f, .3f, .95f), k);
                    }
                    else if (fancy)
                    {
                        sh.rectTransform.anchoredPosition = Vector2.Lerp(GrillView.ShadowOffset, SelShadowOffset, k) - new Vector2(0f, bob * .6f);
                        GrillView.SetAlpha(sh, Mathf.Lerp(1f, .7f, k));
                    }
                }
                yield return null;
            }
        }

        void StopSelectFx()
        {
            if (_selectCo != null) { StopCoroutine(_selectCo); _selectCo = null; }
        }

        void Spotlight(int selected, bool on)
        {
            var dim = new Color(.55f, .55f, .55f, 1f);
            for (int g = 0; g < _board.SkewerCount; g++)
            {
                var v = ui.grills[g];
                var c = on && g != selected ? dim : Color.white;
                v.grill.color = c; v.skewer.color = c;
                foreach (var f in v.foods) f.color = c;
            }
        }

        void UnheatGrill(GrillView v)
        {
            if (selectStyle == SelectStyle.Spotlight) Spotlight(-1, false);
            if (_selectGlowCo != null) StopCoroutine(_selectGlowCo);
            _selectGlowCo = StartCoroutine(Tween.Fade(v.glow, 0f, .18f));
            v.SetLit(false, art);
        }

        void Deselect()
        {
            if (_selected < 0) return;
            var v = ui.grills[_selected];
            StopSelectFx();
            for (int i = 0; i < v.slots.Length; i++) StartCoroutine(SettleSlot(v, i));
            UnheatGrill(v);
            _selected = -1;
        }

        IEnumerator SettleSlot(GrillView v, int i)
        {
            var rt = v.slots[i]; var sh = v.foodShadows[i];
            Vector2 p0 = rt.anchoredPosition, s0 = sh.rectTransform.anchoredPosition;
            float sc0 = rt.localScale.x, a0 = sh.color.a, ss0 = sh.rectTransform.localScale.x;
            Color c0 = sh.color;
            var r0 = rt.localRotation;
            yield return Tween.Run(.16f, k =>
            {
                rt.anchoredPosition = Vector2.LerpUnclamped(p0, new Vector2(0f, GrillView.SlotY[i]), k);
                rt.localScale = Vector3.one * Mathf.LerpUnclamped(sc0, 1f, k);
                rt.localRotation = Quaternion.Slerp(r0, Quaternion.identity, k);
                sh.rectTransform.anchoredPosition = Vector2.Lerp(s0, GrillView.ShadowOffset, k);
                sh.rectTransform.localScale = Vector3.one * Mathf.Lerp(ss0, 1f, k);
                sh.color = Color.Lerp(c0, Color.white, k);
            }, Ease.OutCubic);
        }

        // ------------------------------------------------------------------ moves
        IEnumerator DoMove(Move m)
        {
            _busy = true;
            var src = ui.grills[m.From]; var dst = ui.grills[m.To];
            StopSelectFx();
            UnheatGrill(src);
            _selected = -1;
            int dstBefore = _board.Count(m.To);
            var moved = new int[m.Count];
            for (int k = 0; k < m.Count; k++) moved[k] = _board.Get(m.From, m.Index + k);

            _history.Push((_board.Clone(), _moves));
            int completed = _board.Apply(m);
            if (verboseLog) Debug.Log($"[Grill Sort] move {m} -> {_board} completed={completed}");
            _moves++;

            // pieces that stay lifted but did not move (TopGroup partial fit) drop back
            for (int i = 0; i < m.Index; i++)
                if (src.slots[i].anchoredPosition.y != GrillView.SlotY[i] || src.slots[i].localScale.x != 1f)
                    StartCoroutine(SettleSlot(src, i));

            // fly the moved pieces, top first
            int flying = 0;
            for (int k = m.Count - 1, f = 0; k >= 0; k--, f++)
            {
                int si = m.Index + k, ti = dstBefore + k;
                flying++;
                StartCoroutine(FlyPiece(f, src, si, dst, ti, moved[k], f * .06f, () => flying--));
            }
            while (flying > 0) yield return null;

            src.Show(_board.Stack(m.From), art, cookOnComplete);
            if (completed < 0) dst.Show(_board.Stack(m.To), art, cookOnComplete);

            if (completed >= 0) yield return Complete(m.To, completed);

            if (_served >= _totalOrders) { yield return Win(); yield break; }
            _busy = false;
            if (!_board.HasAnyMove(moveRule)) StartCoroutine(Tween.Delay(.5f, () => { if (!_busy && !_won) OpenPopup(ui.stuckPopup, ui.stuckPanel); }));
        }

        IEnumerator FlyPiece(int flyer, GrillView src, int si, GrillView dst, int ti, int food, float delay, System.Action done)
        {
            var fl = ui.flyers[flyer];
            var fa = art.foods[food];
            yield return new WaitForSeconds(delay);
            var p0 = (Vector2)ui.flyLayer.InverseTransformPoint(src.slots[si].position);
            var p2 = (Vector2)ui.flyLayer.InverseTransformPoint(dst.rect.TransformPoint(new Vector3(0f, GrillView.SlotY[ti], 0f)));
            PrepareFlyer(flyer, fa, cookOnComplete);
            float s0 = src.slots[si].localScale.x;
            fl.anchoredPosition = p0; fl.localScale = Vector3.one * s0; fl.gameObject.SetActive(true);
            src.HideSlot(si);
            var p1 = new Vector2((p0.x + p2.x) * .5f, Mathf.Max(p0.y, p2.y) + 150f);
            float d = Mathf.Clamp(.2f + Vector2.Distance(p0, p2) / 2600f, .22f, .42f);
            yield return Tween.Run(d, k =>
            {
                float u = 1f - k;
                fl.anchoredPosition = u * u * p0 + 2f * u * k * p1 + k * k * p2;
                fl.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(k * Mathf.PI) * (p2.x > p0.x ? -14f : 14f));
                fl.localScale = Vector3.one * Mathf.Lerp(s0, 1f, k);
            }, Ease.InOutCubic);
            fl.localScale = Vector3.one;
            fl.gameObject.SetActive(false);
            fl.localRotation = Quaternion.identity;
            dst.SetFood(ti, fa, cookOnComplete);
            dst.ResetSlot(ti);
            StartCoroutine(Tween.Bounce(dst.slots[ti], .16f, .26f));
            audioManager.Play(art.drop, .8f, 1f + ti * .06f);
            Haptics.Light();
            done();
        }

        void PrepareFlyer(int i, GameArt.FoodArt fa, bool raw)
        {
            float w = fa.width * GrillView.FoodScale;
            ui.flyerFoods[i].sprite = raw ? fa.raw : fa.grilled;
            ui.flyerFoods[i].rectTransform.sizeDelta = new Vector2(w, w);
            ui.flyerShadows[i].sprite = raw && fa.shadowRaw != null ? fa.shadowRaw : fa.shadow;
            ui.flyerShadows[i].rectTransform.sizeDelta = new Vector2(w, w) * GrillView.ShadowRatio;
            ui.flyerShadows[i].rectTransform.anchoredPosition = GrillView.ShadowOffset * 1.6f;
            ui.flyerFoods[i].color = Color.white; ui.flyerShadows[i].color = Color.white;
        }

        // ------------------------------------------------------------------ completion & serving
        IEnumerator Complete(int g, int food)
        {
            var v = ui.grills[g];
            var fa = art.foods[food];
            audioManager.Play(art.sizzle, .9f);
            Haptics.Medium();
            v.SetLit(true, art);
            StartCoroutine(Tween.Fade(v.glow, .9f, .25f));
            StartCoroutine(SteamLoop(v, 1.6f));
            StartCoroutine(Sparkles(v));
            StartCoroutine(PopWord(v.perfect));
            for (int i = 0; i < v.slots.Length; i++) StartCoroutine(Sizzle(v.slots[i], i * .05f));

            if (cookOnComplete)
            {
                yield return new WaitForSeconds(.25f);
                foreach (var stage in new[] { fa.medium, fa.grilled })
                {
                    for (int i = 0; i < v.slots.Length; i++) StartCoroutine(Puff(v.slots[i]));
                    yield return new WaitForSeconds(.12f);
                    for (int i = 0; i < v.slots.Length; i++) { v.foods[i].sprite = stage; v.foodShadows[i].sprite = fa.shadow; }
                    yield return new WaitForSeconds(.23f);
                }
            }
            else yield return new WaitForSeconds(.55f);

            // find the customer who ordered it (swap a waiting order in if they are not at the counter yet)
            CustomerView c = FindCustomerFor(food);
            float wait = 0f;
            while (c == null)
            {
                c = FindCustomerFor(food);
                if (c != null) break;
                foreach (var cc in ui.customers)
                {
                    if (!cc.gameObject.activeSelf || cc.busy || cc.food < 0) continue;
                    int pi = _pending.IndexOf(food);
                    if (pi >= 0) { _pending[pi] = cc.food; cc.food = food; StartCoroutine(SwapOrder(cc, food)); c = cc; }
                    break;
                }
                if (c != null) break;
                yield return null;
                wait += Time.deltaTime;
                if (wait > 4f) break; // safety
            }

            if (c != null)
            {
                int done = 0;
                for (int i = v.slots.Length - 1, f = 0; i >= 0; i--, f++)
                    StartCoroutine(ServePiece(f, v, i, fa, c, f * .07f, () => done++));
                while (done < v.slots.Length) yield return null;
                StartCoroutine(Cheer(c));
            }
            else for (int i = 0; i < v.slots.Length; i++) v.HideSlot(i);

            _served++;
            _history.Clear();
            UpdateOrders(true);
            audioManager.Play(art.serve, .9f);
            Haptics.Medium();
            StartCoroutine(Tween.Fade(v.glow, 0f, .4f));
            v.SetLit(false, art);
            v.Show(_board.Stack(g), art, cookOnComplete);
        }

        IEnumerator SwapOrder(CustomerView c, int food)
        {
            c.busy = true;
            yield return Tween.Scale(c.bubble, new Vector3(.2f, .2f, 1f), .12f, Ease.InCubic);
            c.order.sprite = art.foods[food].grilled;
            yield return Tween.Scale(c.bubble, Vector3.one, .2f, t => Ease.OutBack(t));
            c.busy = false;
        }

        IEnumerator ServePiece(int flyer, GrillView v, int slot, GameArt.FoodArt fa, CustomerView c, float delay, System.Action done)
        {
            yield return new WaitForSeconds(delay);
            var fl = ui.flyers[flyer];
            PrepareFlyer(flyer, fa, false);
            var p0 = (Vector2)ui.flyLayer.InverseTransformPoint(v.slots[slot].position);
            var p2 = (Vector2)ui.flyLayer.InverseTransformPoint(c.order.rectTransform.position);
            fl.anchoredPosition = p0; fl.localScale = Vector3.one; fl.gameObject.SetActive(true);
            v.HideSlot(slot);
            float targetW = c.order.rectTransform.rect.width * c.order.rectTransform.lossyScale.x;
            float flyW = ui.flyerFoods[flyer].rectTransform.rect.width * ui.flyLayer.lossyScale.x;
            float endScale = flyW > 0 ? targetW / flyW : .5f;
            var p1 = new Vector2((p0.x + p2.x) * .5f + 120f, Mathf.Max(p0.y, p2.y) + 220f);
            yield return Tween.Run(.42f, k =>
            {
                float u = 1f - k;
                fl.anchoredPosition = u * u * p0 + 2f * u * k * p1 + k * k * p2;
                float s = Mathf.Lerp(1f, endScale, k);
                fl.localScale = new Vector3(s, s, 1f);
                var col = new Color(1f, 1f, 1f, k > .8f ? (1f - k) * 5f : 1f);
                ui.flyerFoods[flyer].color = col; ui.flyerShadows[flyer].color = new Color(1f, 1f, 1f, col.a * (1f - k));
            }, Ease.InOutCubic);
            fl.gameObject.SetActive(false);
            StartCoroutine(Tween.Bounce(c.bubble, .12f, .2f));
            done();
        }

        IEnumerator Cheer(CustomerView c)
        {
            c.check.gameObject.SetActive(true);
            c.check.rectTransform.localScale = Vector3.zero;
            StartCoroutine(Tween.Scale(c.check.rectTransform, Vector3.one, .35f, Ease.OutElastic));
            audioManager.Play(art.coin, .6f);
            var b = c.body;
            yield return Tween.Run(.45f, k =>
            {
                float hop = Mathf.Sin(k * Mathf.PI * 2f) * 26f * (1f - k);
                b.anchoredPosition = new Vector2(0f, CustomerView.BodyY + Mathf.Abs(hop));
                float sq = Mathf.Sin(k * Mathf.PI * 4f) * .06f * (1f - k);
                b.localScale = new Vector3(1f + sq, 1f - sq, 1f);
            });
            b.localScale = Vector3.one;
            StartCoroutine(LeaveAndNext(c));
        }

        IEnumerator SteamLoop(GrillView v, float duration)
        {
            float t = 0f;
            for (int i = 0; i < v.steam.Length; i++) v.steam[i].gameObject.SetActive(true);
            var basePos = new Vector2[v.steam.Length];
            for (int i = 0; i < v.steam.Length; i++) basePos[i] = v.steam[i].rectTransform.anchoredPosition;
            while (t < duration)
            {
                t += Time.deltaTime;
                for (int i = 0; i < v.steam.Length; i++)
                {
                    float ph = (t * .9f + i * .45f) % 1f;
                    var rt = v.steam[i].rectTransform;
                    rt.anchoredPosition = basePos[i] + new Vector2(Mathf.Sin((t + i) * 3f) * 10f, ph * 90f);
                    float fade = Mathf.Clamp01(1f - t / duration * .8f);
                    GrillView.SetAlpha(v.steam[i], Mathf.Sin(ph * Mathf.PI) * .85f * fade);
                    rt.localScale = Vector3.one * (.8f + ph * .4f);
                }
                yield return null;
            }
            for (int i = 0; i < v.steam.Length; i++) { v.steam[i].rectTransform.anchoredPosition = basePos[i]; v.steam[i].gameObject.SetActive(false); }
        }

        IEnumerator Sparkles(GrillView v)
        {
            for (int i = 0; i < v.sparkles.Length; i++)
            {
                StartCoroutine(OneSparkle(v.sparkles[i], i * .08f));
            }
            yield break;
        }

        IEnumerator OneSparkle(Image s, float delay)
        {
            yield return new WaitForSeconds(delay);
            s.gameObject.SetActive(true);
            var rt = s.rectTransform;
            yield return Tween.Run(.9f, k =>
            {
                float sc = k < .3f ? Ease.OutBack(k / .3f) : 1f - (k - .3f) / .7f * .6f;
                rt.localScale = Vector3.one * sc;
                rt.localRotation = Quaternion.Euler(0f, 0f, k * 90f);
                GrillView.SetAlpha(s, k < .6f ? 1f : (1f - k) / .4f);
            });
            s.gameObject.SetActive(false);
        }

        IEnumerator PopWord(Image w)
        {
            w.gameObject.SetActive(true);
            var rt = w.rectTransform;
            var basePos = rt.anchoredPosition;
            GrillView.SetAlpha(w, 1f);
            rt.localScale = Vector3.zero;
            yield return Tween.Scale(rt, Vector3.one, .35f, t => Ease.OutBack(t, 2.2f));
            yield return new WaitForSeconds(.45f);
            yield return Tween.Run(.35f, k => { rt.anchoredPosition = basePos + new Vector2(0f, k * 40f); GrillView.SetAlpha(w, 1f - k); });
            rt.anchoredPosition = basePos;
            w.gameObject.SetActive(false);
        }

        IEnumerator Sizzle(RectTransform slot, float delay)
        {
            yield return new WaitForSeconds(delay);
            yield return Tween.Run(.5f, k =>
            {
                float j = Mathf.Sin(k * Mathf.PI * 8f) * (1f - k);
                slot.localScale = new Vector3(1f + j * .05f, 1f - j * .05f, 1f);
                slot.localRotation = Quaternion.Euler(0f, 0f, j * 4f);
            });
            slot.localScale = Vector3.one; slot.localRotation = Quaternion.identity;
        }

        IEnumerator Puff(RectTransform slot)
        {
            yield return Tween.Run(.2f, k => slot.localScale = Vector3.one * (1f + Mathf.Sin(k * Mathf.PI) * .12f));
        }

        // ------------------------------------------------------------------ win
        IEnumerator Win()
        {
            _won = true;
            _busy = true;
            yield return new WaitForSeconds(1.1f);
            audioManager.Play(art.win, .9f);
            Haptics.Heavy();
            int par = Mathf.Max(1, _level.par);
            if (moveRule == MoveRule.SingleTop && _level.rule != MoveRule.SingleTop) par = par * 3 / 2; // single moves take longer
            int stars = _moves <= par + 2 ? 3 : _moves <= par * 3 / 2 + 2 ? 2 : 1;
            _winReward = 10 + stars * 5;
            ui.winTitle.text = $"Level {_levelNumber}";
            ui.winCoins.text = $"+{_winReward}";
            for (int i = 0; i < ui.winStars.Length; i++)
            {
                ui.winStars[i].sprite = i < stars ? art.starOn : art.starOff;
                ui.winStars[i].color = i < stars ? Color.white : new Color(.35f, .25f, .2f, .55f);
                ui.winStars[i].rectTransform.localScale = Vector3.zero;
            }
            OpenPopup(ui.winPopup, ui.winPanel);
            yield return new WaitForSeconds(.35f);
            for (int i = 0; i < ui.winStars.Length; i++)
            {
                StartCoroutine(Tween.Scale(ui.winStars[i].rectTransform, Vector3.one, .45f, Ease.OutElastic));
                if (i < stars) { audioManager.Play(art.coin, .5f, 1f + i * .15f); Haptics.Light(); }
                yield return new WaitForSeconds(.18f);
            }
            if (_autoPlay) { yield return new WaitForSeconds(.4f); CollectWin(1); }
        }

        int _winReward;

        void CollectWin(int mult)
        {
            if (!_won) return;
            Click();
            int reward = _winReward * mult;
            SaveData.Coins += reward;
            ui.StartCoroutine(CountCoins());
            ClosePopup(ui.winPopup);
            int next = _levelNumber + 1;
            if (limitToTenLevels && !useTestLevel && next > 10)
            {
                SaveData.Level = 1;
                StartCoroutine(Tween.Delay(.3f, () =>
                {
                    LoadLevel(1);
                    ShowMessage("Demo complete!", "Thanks for playing the first 10 levels.\nThe full game has many more.");
                }));
                return;
            }
            if (!useTestLevel) SaveData.Level = next;
            StartCoroutine(Tween.Delay(.3f, () => LoadLevel(next)));
        }

        IEnumerator CountCoins()
        {
            int from = _shownCoins, to = SaveData.Coins;
            _shownCoins = to;
            yield return Tween.Run(.6f, k => ui.coinText.text = Mathf.RoundToInt(Mathf.Lerp(from, to, k)).ToString("N0"));
            StartCoroutine(Tween.Bounce(ui.coinIcon, .2f, .3f));
        }

        // ------------------------------------------------------------------ boosters
        void RefreshBoosters()
        {
            for (int i = 0; i < ui.boosterCounts.Length; i++)
            {
                int n = SaveData.Booster(i);
                ui.boosterCounts[i].text = n > 0 ? n.ToString() : "+";
            }
        }

        void UseBooster(int i)
        {
            if (_busy || _won) return;
            if (verboseLog) Debug.Log($"[Grill Sort] booster {BoosterName(i)} from {System.Environment.StackTrace}");
            Click();
            if (SaveData.Booster(i) <= 0)
            {
                Services.Ads.ShowRewarded("booster_" + BoosterPlacement[i], ok =>
                {
                    if (!ok) return;
                    SaveData.SetBooster(i, SaveData.Booster(i) + 1);
                    RefreshBoosters();
                    Toast("+1 " + BoosterName(i));
                });
                return;
            }
            bool used = i switch
            {
                0 => DoUndo(),
                1 => DoHint(),
                2 => DoShuffle(),
                _ => AddExtraSkewer(),
            };
            if (used)
            {
                SaveData.SetBooster(i, SaveData.Booster(i) - 1);
                RefreshBoosters();
                StartCoroutine(Tween.Bounce(ui.boosters[i].transform, .15f, .3f));
            }
        }

        static string BoosterName(int i) => i switch { 0 => "Undo", 1 => "Hint", 2 => "Shuffle", _ => "Extra Skewer" };

        bool DoUndo()
        {
            if (_history.Count == 0) { Toast("Nothing to undo"); return false; }
            StopHint();
            Deselect();
            var (b, mv) = _history.Pop();
            _board = b; _moves = mv;
            RedrawAll(true);
            return true;
        }

        bool DoHint()
        {
            Deselect();
            var mv = Solver.NextMove(_board, moveRule);
            if (!mv.HasValue) { Toast("No way out. Try Shuffle!"); return false; }
            StopHint();
            _hintCo = StartCoroutine(HintAnim(mv.Value));
            return true;
        }

        bool DoShuffle()
        {
            Deselect();
            StopHint();
            var counts = new int[_board.SkewerCount];
            var pieces = new List<int>();
            for (int s = 0; s < _board.SkewerCount; s++) { counts[s] = _board.Count(s); pieces.AddRange(_board.Stack(s)); }
            if (pieces.Count == 0) return false;
            var rng = new Mulberry32((uint)(System.DateTime.Now.Ticks & 0xffffffff));
            Board best = null;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                rng.Shuffle(pieces);
                var nb = new Board(_board.Capacity);
                int p = 0; bool bad = false;
                for (int s = 0; s < counts.Length; s++)
                {
                    nb.AddSkewer(pieces.GetRange(p, counts[s]));
                    p += counts[s];
                    if (nb.IsComplete(s)) bad = true;
                }
                if (bad) continue;
                var r = Solver.Solve(nb, moveRule, 20000, 40000);
                if (r.Solvable) { best = nb; break; }
            }
            if (best == null) { Toast("Couldn't shuffle. Try Undo!"); return false; }
            _board = best;
            _history.Clear();
            RedrawAll(true);
            return true;
        }

        bool AddExtraSkewer()
        {
            if (_extraUsed || _board.SkewerCount >= ui.grills.Length) { Toast("Only one extra skewer per level"); return false; }
            Deselect();
            _extraUsed = true;
            int g = _board.AddSkewer();
            var v = ui.grills[g];
            v.gameObject.SetActive(true);
            v.ResetFx(); v.SetLit(false, art); v.Show(_board.Stack(g), art, cookOnComplete);
            ui.layout.activeGrills = _board.SkewerCount;
            ui.layout.Apply();
            v.rect.localScale = Vector3.zero;
            StartCoroutine(Tween.Scale(v.rect, Vector3.one, .4f, t => Ease.OutBack(t)));
            audioManager.Play(art.drop, .8f, .8f);
            return true;
        }

        void RedrawAll(bool pop)
        {
            for (int i = 0; i < _board.SkewerCount; i++)
            {
                var v = ui.grills[i];
                v.Show(_board.Stack(i), art, cookOnComplete);
                if (!pop) continue;
                for (int s = 0; s < v.slots.Length; s++)
                {
                    v.slots[s].localScale = Vector3.one * .3f;
                    StartCoroutine(Tween.Scale(v.slots[s], Vector3.one, .3f + s * .03f, t => Ease.OutBack(t)));
                }
            }
            audioManager.Play(art.pick, .6f, .9f);
        }

        // ------------------------------------------------------------------ hints & tips
        IEnumerator HintAnim(Move m)
        {
            var a = ui.grills[m.From]; var b = ui.grills[m.To];
            float t = 0f;
            while (t < 3f)
            {
                t += Time.deltaTime;
                float p = (Mathf.Sin(t * 6f) + 1f) * .5f;
                GrillView.SetAlpha(a.highlight, .2f + p * .5f);
                GrillView.SetAlpha(b.highlight, .2f + (1f - p) * .5f);
                for (int i = m.Index; i < _board.Count(m.From); i++)
                    a.slots[i].anchoredPosition = new Vector2(0f, GrillView.SlotY[i] + p * 22f);
                yield return null;
            }
            ClearHintVisuals(m);
            _hintCo = null;
        }

        void StopHint()
        {
            if (_hintCo == null) return;
            StopCoroutine(_hintCo);
            _hintCo = null;
            for (int g = 0; g < _board.SkewerCount; g++)
            {
                if (g == _selected) continue;
                var v = ui.grills[g];
                GrillView.SetAlpha(v.highlight, 0f);
                for (int i = 0; i < v.slots.Length; i++) v.slots[i].anchoredPosition = new Vector2(0f, GrillView.SlotY[i]);
            }
        }

        void ClearHintVisuals(Move m)
        {
            GrillView.SetAlpha(ui.grills[m.From].highlight, 0f);
            GrillView.SetAlpha(ui.grills[m.To].highlight, 0f);
            var a = ui.grills[m.From];
            for (int i = 0; i < a.slots.Length; i++) a.slots[i].anchoredPosition = new Vector2(0f, GrillView.SlotY[i]);
        }

        static readonly string[] Tips =
        {
            "Tap a skewer to pick up the top food,\nthen tap another skewer to drop it.",
            "Food only goes onto the same food\nor onto an empty skewer.",
            "Fill a skewer with 4 of the same food\nto serve a hungry customer!",
        };

        void ShowTip()
        {
            if (_tipCo != null) StopCoroutine(_tipCo);
            ui.tip.alpha = 0f;
            if (_levelNumber > Tips.Length) return;
            ui.tipText.text = Tips[_levelNumber - 1];
            _tipCo = StartCoroutine(TipAnim());
        }

        IEnumerator TipAnim()
        {
            yield return new WaitForSeconds(.6f);
            yield return Tween.Fade(ui.tip, 1f, .3f);
        }

        public void Toast(string msg)
        {
            if (_toastCo != null) ui.StopCoroutine(_toastCo);
            ui.toastText.text = msg;
            _toastCo = ui.StartCoroutine(ToastAnim());
        }

        IEnumerator ToastAnim()
        {
            var rt = (RectTransform)ui.toast.transform;
            rt.localScale = Vector3.one * .8f;
            ui.StartCoroutine(Tween.Scale(rt, Vector3.one, .25f, t => Ease.OutBack(t)));
            yield return Tween.Fade(ui.toast, 1f, .15f);
            yield return new WaitForSeconds(1.6f);
            yield return Tween.Fade(ui.toast, 0f, .3f);
            _toastCo = null;
        }

        // ------------------------------------------------------------------ popups
        bool AnyPopupOpen() => IsOpen(ui.winPopup) || IsOpen(ui.pausePopup) || IsOpen(ui.stuckPopup) || IsOpen(ui.messagePopup);
        static bool IsOpen(CanvasGroup p) => p.gameObject.activeSelf && p.blocksRaycasts;

        void OpenPopup(CanvasGroup p, RectTransform panel)
        {
            p.gameObject.SetActive(true);
            p.alpha = 0f; p.blocksRaycasts = true; p.interactable = true;
            ui.StartCoroutine(Tween.Fade(p, 1f, .2f));
            panel.localScale = Vector3.one * .6f;
            ui.StartCoroutine(Tween.Scale(panel, Vector3.one, .35f, t => Ease.OutBack(t)));
        }

        void ClosePopup(CanvasGroup p)
        {
            if (!p.gameObject.activeSelf) return;
            p.blocksRaycasts = false; p.interactable = false;
            ui.StartCoroutine(CloseAnim(p));
        }

        IEnumerator CloseAnim(CanvasGroup p)
        {
            yield return Tween.Fade(p, 0f, .15f);
            p.gameObject.SetActive(false);
        }

        static void HidePopupNow(CanvasGroup p) { p.alpha = 0f; p.blocksRaycasts = false; p.interactable = false; p.gameObject.SetActive(false); }

        void ShowMessage(string title, string body)
        {
            ui.messageTitle.text = title;
            ui.messageBody.text = body;
            OpenPopup(ui.messagePopup, ui.messagePanel);
        }

        void RefreshPauseTexts()
        {
            ui.pauseSoundText.text = SaveData.Sound ? "Sound: On" : "Sound: Off";
            ui.pauseHapticsText.text = SaveData.Haptic ? "Vibration: On" : "Vibration: Off";
        }

        void Click() => audioManager.Play(art.click, .6f);

        // ------------------------------------------------------------------ debug / verification
        /// <summary>Plays the current level to the end with solver moves (used by the CLI verification).</summary>
        [ContextMenu("Auto Play Level")]
        public void StartAutoPlay() { StartAutoPlayLevels(1); }

        // runs on the UI component so LoadLevel's StopAllCoroutines doesn't end it
        public void StartAutoPlayLevels(int count) { ui.StartCoroutine(AutoPlay(count)); }

        public int AutoPlayedLevels { get; private set; }
        public string AutoPlayLog { get; private set; } = "";

        IEnumerator AutoPlay(int levels)
        {
            _autoPlay = true;
            for (int n = 0; n < levels; n++)
            {
                int lvl = _levelNumber;
                while (_busy) yield return null;
                int steps = 0;
                while (!_won && steps < 400)
                {
                    while (_busy || AnyPopupOpen()) { if (_won) break; yield return null; }
                    if (_won) break;
                    var mv = Solver.NextMove(_board, moveRule);
                    if (!mv.HasValue) { AutoPlayLog += $"L{lvl}: no move found\n"; _autoPlay = false; yield break; }
                    var m = mv.Value;
                    OnGrillTap(m.From, m.Index);
                    yield return new WaitForSeconds(.12f);
                    OnGrillTap(m.To, 0);
                    steps++;
                    yield return new WaitForSeconds(.1f);
                }
                AutoPlayLog += $"L{lvl}: solved in {_moves} moves (par {_level.par})\n";
                AutoPlayedLevels++;
                while (_levelNumber == lvl) yield return null;
                yield return new WaitForSeconds(1.2f);
            }
            _autoPlay = false;
        }
    }
}
