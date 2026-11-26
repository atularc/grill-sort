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
    public sealed partial class GameController : MonoBehaviour
    {
        [Header("Data")]
        public GameArt art;
        public LevelBank bank;
        [Header("5-tier demo build (Aaron 2026-11-25)")]
        [Tooltip("Plays the 5 designed tier levels (Data/DemoLevelBank) as Level 1-5: Easy, Medium, Hard, Very Hard, Super Hard. Home and Daily Rewards are off; after level 5 it loops.")]
        public bool tierDemo = false;
        public LevelBank demoBank;
        bool DemoOn => tierDemo && demoBank != null && demoBank.levels.Count > 0;
        public GameUi ui;
        public AudioManager audioManager;

        [Header("Rules")]
        [Tooltip("AnyWithAbove (default, client decision 2026-11-18): grab any piece, everything above comes along. TopGroup = classic Water Sort: the whole top run of the same food moves. SingleTop = only the top piece moves.")]
        public MoveRule moveRule = MoveRule.AnyWithAbove;
        [Tooltip("Cooking stages (2026-11-23): pieces are RAW while sorting; a finished tray flares up and the pieces turn GRILLED before they are served. Off = grilled food all the time (the client-approved look). OFF since the user's 2026-11-23 call: the food should look the same as before.")]
        public bool cookOnComplete = false;
        [Tooltip("Debug: play every serve flight at this time scale (e.g. 0.05) to look at it frame by frame. 0 = normal speed.")]
        public float serveSlowMotion = 0f;
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
        [Header("Loading screen")]
        [Tooltip("Show the loading screen between levels too (like the client's FishUp!), after Next on the win popup")]
        public bool loadingBetweenLevels = true;
        [Tooltip("Seconds the loading bar takes at game start")] public float loadingStartSeconds = 1.9f;
        [Tooltip("Seconds the loading bar takes between levels (shorter: our levels load instantly; set ~1.9 to match FishUp!)")]
        public float loadingBetweenSeconds = .7f;
        [Tooltip("Between levels the logo/mascot animations play this much faster (1 = same as the start screen)")]
        public float loadingBetweenSpeed = 2f;

        int _levelNumber, _moves, _served, _totalOrders, _skewers;
        int _nextAnimal;

        /// <summary>
        /// Mirror of the row on screen (tray stacks, orders, customer faces). The Board serves orders instantly; the view
        /// catches up one serve at a time (tray + customer leave, the rest slide left, the next tray slides in), so this
        /// keeps the faces with their trays while they slide.
        /// </summary>
        // hid: wrapped pieces (same length as stack); lockPlus1: food that unlocks this tray + 1 (0 = not locked)
        struct VisTray { public int[] stack; public int target; public int face; public int who; public bool[] hid; public int lockPlus1; }   // who: this customer's id (their mood + waiting time, GameController.Moods)
        readonly List<VisTray> _vis = new List<VisTray>();
        int NextFace() => _nextAnimal++ % art.customers.Length;
        int _selected = -1, _selStart;
        bool _busy, _extraUsed, _won, _autoPlay;
        float _idle;
        /// <summary>True while the loading screen covers the game: the level intro, customers and tips wait for it.</summary>
        bool _loadingScreen;
        [Tooltip("Levels with the tutorial hand: it shows every move until the player makes it")]
        public int tutorialLevels = 2;
        Coroutine _hintCo, _tipCo, _toastCo;
        readonly Stack<(Board board, int moves)> _history = new Stack<(Board, int)>();
        int _shownCoins;
        int _activeCustomers;

        /// <summary>The loading screen or the home screen covers the game: the level intro, customers and tips wait.</summary>
        bool Covered => _loadingScreen || _homeUp;

        public int LevelNumber => _levelNumber;
        public bool Busy => _busy;
        public Board CurrentBoard => _board;

        // ------------------------------------------------------------------ setup
        void Start()
        {
            Application.targetFrameRate = 60;
            Services.Ads.Init();
            Haptics.Enabled = SaveData.Haptic;
            if (audioManager != null)
            {
                audioManager.Enabled = true;
                audioManager.MusicEnabled = true;
                audioManager.SetMusicSlider(SaveData.MusicVol);
                audioManager.SetSfxSlider(SaveData.SfxVol);
                audioManager.StartMusic(CurrentTrack());   // starts with the loading screen, fades in
            }

            for (int i = 0; i < ui.grills.Length; i++)
            {
                var gv = ui.grills[i];
                gv.index = i; gv.onTap = OnGrillTap;
                gv.onDragStart = OnDragStart; gv.onDragMove = OnDragMove; gv.onDragEnd = OnDragEnd;
            }
            ui.pauseButton.onClick.AddListener(() => { Click(); OpenPopup(ui.pausePopup, ui.pausePanel); RefreshPauseTexts(); });
            ui.pauseResume.onClick.AddListener(() => { Click(); ClosePopup(ui.pausePopup); });
            ui.pauseClose.onClick.AddListener(() => { Click(); ClosePopup(ui.pausePopup); });
            ui.pauseRestore.onClick.AddListener(() =>
            {
                Click();
                Services.Iap.Restore(ok => ShowMessage("Restore Purchases", ok ? "Your purchases are back!" : "There was nothing to restore."));
            });
            ui.pauseRestart.onClick.AddListener(() => { Click(); ClosePopup(ui.pausePopup); LoadLevel(_levelNumber); });
            // volume sliders: live while dragging, saved; a short tick on the sound slider so you hear the new level
            ui.musicSlider.SetValueWithoutNotify(SaveData.MusicVol);
            ui.sfxSlider.SetValueWithoutNotify(SaveData.SfxVol);
            ui.musicSlider.onValueChanged.AddListener(v => { audioManager.SetMusicSlider(v); SaveData.MusicVol = v; RefreshPauseTexts(); });
            ui.sfxSlider.onValueChanged.AddListener(v =>
            {
                audioManager.SetSfxSlider(v); SaveData.SfxVol = v; RefreshPauseTexts();
                if (Time.unscaledTime - _lastSliderTick > .12f) { _lastSliderTick = Time.unscaledTime; audioManager.Play(art.click, .6f); }
            });
            ui.pauseHaptics.onClick.AddListener(() => { SaveData.Haptic = !SaveData.Haptic; Haptics.Enabled = SaveData.Haptic; Haptics.Medium(); Click(); RefreshPauseTexts(); });
            for (int i = 0; i < ui.boosters.Length; i++) { int k = i; ui.boosters[i].onClick.AddListener(() => UseBooster(k)); }
            // stuck popup (Continue? -> rescue with coins or an ad; X -> Give up page)
            ui.stuckBuy.onClick.AddListener(() =>
            {
                if (SaveData.Coins < StuckPriceFor) { Toast("Not enough coins"); Shake(ui.stuckBuy.transform); return; }
                Click();
                SaveData.Coins -= StuckPriceFor; _shownCoins = SaveData.Coins; ui.coinText.text = _shownCoins.ToString("N0");
                ClosePopup(ui.stuckPopup); Rescue();
            });
            ui.stuckAd.onClick.AddListener(() => { Click(); ClosePopup(ui.stuckPopup); Services.Ads.ShowRewarded("stuck_rescue", ok => { if (ok) Rescue(); else StuckLater(); }); });
            ui.stuckUndo.onClick.AddListener(() => { Click(); ClosePopup(ui.stuckPopup); DoUndo(); _stuckShown = false; });
            ui.stuckClose.onClick.AddListener(() => { Click(); ShowStuckPage(false); });
            ui.stuckKeep.onClick.AddListener(() => { Click(); ShowStuckPage(true); });
            ui.stuckRetry.onClick.AddListener(() => { Click(); ClosePopup(ui.stuckPopup); LoadLevel(_levelNumber); });
            ui.winNext.onClick.AddListener(() => CollectWin(1, ui.winNext.transform));
            // "Claim xN": the pointer stops on press, the multiplier under it is what the ad pays
            ui.winDouble.onClick.AddListener(() =>
            {
                if (_claiming || !_won) return;
                _pointerRun = false;
                int m = _winMult;
                Services.Ads.ShowRewarded("win_double", ok => CollectWin(ok ? m : 1, ui.winDouble.transform));
            });
            ui.messageOk.onClick.AddListener(() => { Click(); ClosePopup(ui.messagePopup); });
            InitMeta();

            foreach (var p in new[] { ui.winPopup, ui.pausePopup, ui.stuckPopup, ui.messagePopup }) HidePopupNow(p);
            foreach (var p in MetaPopups) HidePopupNow(p);
            ui.tip.alpha = 0f; ui.toast.alpha = 0f;
            _shownCoins = SaveData.Coins;
            ui.coinText.text = _shownCoins.ToString("N0");

            int start = SaveData.Level;
            if (useTestLevel) start = Mathf.Max(1, testLevel);
            else if (limitToTenLevels && start > 10) start = 1;
            _loadedTestLevel = useTestLevel ? start : -1;
            ui.hand.gameObject.SetActive(false);
            _loadingScreen = ui.loading != null;
            if (UseHome(start)) ShowHomeNow();   // the loading screen lifts onto the home screen (the level waits behind it)
            else HideHomeNow();
            LoadLevel(start);   // set up behind the loading screen; the intro plays when it lifts
            if (_loadingScreen) StartCoroutine(LoadingScreen());
            else audioManager.SetAmbience(art.sizzleLoop, true);
        }

        // ------------------------------------------------------------------ loading screen
        // Like the client's other game (2026-11-21): themed background, the logo drops in letter by letter, food hops onto
        // a skewer while the bar fills, then the skewer flies off, the letters pop away and the game shows.
        IEnumerator LoadingScreen() => LoadingScreen(loadingStartSeconds);

        /// <summary>
        /// Between levels (user 2026-11-21, like FishUp!): the loading screen fades in over the finished level, the next
        /// level is set up behind it, then the usual loading sequence plays and lifts. Runs on <see cref="GameUi"/>
        /// because LoadLevel stops every coroutine on this component.
        /// </summary>
        IEnumerator AfterLoading(float delay, System.Action then)
        {
            while (Covered) yield return null;
            yield return new WaitForSeconds(delay);
            then();
        }

        IEnumerator LoadingBetween(int next)
        {
            var L = ui.loading;
            L.gameObject.SetActive(true); L.alpha = 0f; L.blocksRaycasts = true;
            audioManager.SetAmbience(art.sizzleLoop, false);
            ui.loadingLogo.localScale = Vector3.zero; ui.loadingMascot.localScale = Vector3.zero;
            ui.loadingBarFill.gameObject.SetActive(false);
            ui.loadingText.text = "Loading";
            yield return Tween.Fade(L, 1f, .3f);
            _loadingScreen = true;
            LoadLevel(next);   // behind the screen; the intro waits for _loadingScreen
            StartCoroutine(LoadingScreen(loadingBetweenSeconds, loadingBetweenSpeed));
        }

        IEnumerator LoadingScreen(float barSeconds, float speed = 1f)
        {
            var L = ui.loading;
            L.gameObject.SetActive(true); L.alpha = 1f; L.blocksRaycasts = true;
            var theme = art.ThemeForLevel(_levelNumber);
            ui.loadingBg.sprite = theme.loadingArt != null ? theme.loadingArt : art.loadingKeyArt;   // per theme / season, or the default scene
            var logo = ui.loadingLogo; var mascot = ui.loadingMascot;
            var mascotHome = mascot.anchoredPosition;
            logo.localScale = Vector3.zero;
            mascot.localScale = Vector3.zero;
            GrillView.SetAlpha(ui.loadingRays, 0f);
            ui.loadingBarFill.sizeDelta = new Vector2(0f, ui.loadingBarFill.sizeDelta.y);
            ui.loadingBarFill.gameObject.SetActive(false);
            var ambient = StartCoroutine(LoadingAmbient());

            // in: the logo drops in with a squash, the mascot pops up next to it
            yield return new WaitForSeconds(.15f / speed);
            audioManager.Play(art.pop, .8f, .85f);
            StartCoroutine(Tween.Run(.5f / speed, k => GrillView.SetAlpha(ui.loadingRays, k * .22f)));
            StartCoroutine(Tween.Run(.55f / speed, k =>
            {
                float s = Ease.OutBack(k, 2.2f);
                float sq = Mathf.Sin(k * Mathf.PI * 2f) * .08f * (1f - k);   // squash and stretch as it lands
                logo.localScale = new Vector3(s * (1f + sq), s * (1f - sq), 1f);
            }));
            yield return new WaitForSeconds(.45f / speed);
            audioManager.Play(art.pop, .7f, 1.25f);
            StartCoroutine(Tween.Run(.4f / speed, k => { float s = Ease.OutBack(k, 2.5f); mascot.localScale = new Vector3(s, s, 1f); }));
            yield return new WaitForSeconds(.4f / speed);
            var idle = StartCoroutine(LoadingIdle(logo, mascot, mascotHome));

            // the bar fills
            float dur = Mathf.Max(.3f, barSeconds);
            float t = 0f, minW = ui.loadingBarFill.sizeDelta.y;   // the fill's rounded ends need at least its height
            while (t < dur)
            {
                t += Time.deltaTime;
                float p = Ease.InOutCubic(Mathf.Clamp01(t / dur));
                ui.loadingBarFill.gameObject.SetActive(p > .02f);
                ui.loadingBarFill.sizeDelta = new Vector2(Mathf.Lerp(minW, ui.loadingBarMax, p), ui.loadingBarFill.sizeDelta.y);
                ui.loadingText.text = "Loading" + new string('.', 1 + (int)(t * 3f) % 3);
                yield return null;
            }
            ui.loadingText.text = "Let's grill!";
            yield return new WaitForSeconds(.35f / speed);
            StopCoroutine(idle);

            // out: the mascot hops off screen, the logo pops away, then fade into the game
            audioManager.Play(art.whoosh, .8f, .9f);
            StartCoroutine(MascotOff(mascot, mascotHome));
            StartCoroutine(Tween.Delay(.2f / speed, () => StartCoroutine(Tween.Run(.3f / speed, k =>
            {
                float s = k < .3f ? 1f + k * .4f : Mathf.Lerp(1.12f, 0f, (k - .3f) / .7f);
                logo.localScale = new Vector3(s, s, 1f);
            }))));
            yield return new WaitForSeconds(.55f / speed);
            _loadingScreen = false;   // the level intro starts under the fade
            audioManager.SetAmbience(art.sizzleLoop, true);   // soft grill sizzle under the music while playing
            yield return Tween.Fade(L, 0f, .35f / speed);
            StopCoroutine(ambient);
            L.blocksRaycasts = false;
            L.gameObject.SetActive(false);
            mascot.anchoredPosition = mascotHome; mascot.localRotation = Quaternion.identity;
            logo.localScale = Vector3.one; logo.localRotation = Quaternion.identity;
        }

        /// <summary>While loading: the logo breathes and sways a little, the mascot bounces and waves.</summary>
        IEnumerator LoadingIdle(RectTransform logo, RectTransform mascot, Vector2 mascotHome)
        {
            float t = 0f;
            while (true)
            {
                t += Time.deltaTime;
                float b = 1f + Mathf.Sin(t * 2.6f) * .025f;
                logo.localScale = new Vector3(b, b, 1f);
                logo.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 1.4f) * 1.5f);
                float hop = Mathf.Abs(Mathf.Sin(t * 4.2f));
                mascot.anchoredPosition = mascotHome + new Vector2(0f, hop * 34f);
                float sq = (1f - hop) * .08f;   // squash on landing
                mascot.localScale = new Vector3(1f + sq, 1f - sq, 1f);
                mascot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 4.2f) * 5f);
                yield return null;
            }
        }

        /// <summary>The mascot crouches, then hops off to the right in two bounces.</summary>
        IEnumerator MascotOff(RectTransform m, Vector2 home)
        {
            yield return Tween.Run(.12f, k => m.localScale = new Vector3(1f + k * .12f, 1f - k * .14f, 1f));
            yield return Tween.Run(.55f, k =>
            {
                float x = k * 900f;
                float y = Mathf.Abs(Mathf.Sin(k * Mathf.PI * 2f)) * 160f * (1f - k * .3f);
                m.anchoredPosition = home + new Vector2(x, y);
                m.localScale = Vector3.one;
                m.localRotation = Quaternion.Euler(0f, 0f, -k * 25f);
            }, Ease.InCubic);
        }

        /// <summary>Loading screen life: turning rays, steam and embers off the painted grill, twinkling sparkles.</summary>
        IEnumerator LoadingAmbient()
        {
            var st = ui.loadingSteam; var em = ui.loadingEmbers; var sp = ui.loadingSparkles;
            var stHome = new Vector2[st.Length]; var emHome = new Vector2[em.Length];
            for (int i = 0; i < st.Length; i++) stHome[i] = st[i].rectTransform.anchoredPosition;
            for (int i = 0; i < em.Length; i++) emHome[i] = em[i].rectTransform.anchoredPosition;
            var rays = ui.loadingRays.rectTransform;
            float t = 0f;
            while (true)
            {
                t += Time.deltaTime;
                rays.localRotation = Quaternion.Euler(0f, 0f, -t * 10f);
                for (int i = 0; i < st.Length; i++)
                {
                    float ph = (t * .45f + i * .27f) % 1f;
                    st[i].rectTransform.anchoredPosition = stHome[i] + new Vector2(Mathf.Sin((t + i) * 2.2f) * 16f, ph * 220f);
                    GrillView.SetAlpha(st[i], Mathf.Sin(ph * Mathf.PI) * .5f);
                }
                for (int i = 0; i < em.Length; i++)
                {
                    float ph = (t * (.35f + (i % 3) * .08f) + i * .137f) % 1f;
                    em[i].rectTransform.anchoredPosition = emHome[i] + new Vector2(Mathf.Sin((t * 1.7f + i) * 1.3f) * 30f, ph * 480f);
                    GrillView.SetAlpha(em[i], Mathf.Sin(ph * Mathf.PI) * .95f);
                }
                for (int i = 0; i < sp.Length; i++)
                {
                    float ph = (t * .7f + i * .19f) % 1f;
                    var rt = sp[i].rectTransform;
                    float a = ph < .5f ? Mathf.Sin(ph * 2f * Mathf.PI) : 0f;
                    rt.localScale = Vector3.one * (.5f + a * .6f);
                    rt.localRotation = Quaternion.Euler(0f, 0f, t * 60f + i * 40f);
                    GrillView.SetAlpha(sp[i], Mathf.Max(0f, a));
                }
                yield return null;
            }
        }

        public void LoadLevel(int number)
        {
            StopAllCoroutines();
            _hintCo = _tipCo = null;
            _selectCo = _selectGlowCo = null;
            _handCo = null; HideHand();
            _stuckCheckId++; _stuckShown = false; _deadMoves = -1;   // cancel a pending stuck check from the previous level
            if (!_loadingScreen && audioManager != null) audioManager.SetAmbience(art.sizzleLoop, true);   // e.g. Restart / no loading screen
            // a level load can come while the win / stuck popup is still animating in (StopAllCoroutines above cuts the
            // pop-ins short): close them now and put every popped element back to full size, so nothing stays stuck
            // half-open with a hidden "Next" button (2026-11-21)
            foreach (var pp in new[] { ui.winPopup, ui.stuckPopup }) HidePopupNow(pp);
            foreach (var t in new Transform[] { ui.winWord, ui.winBubble, ui.winMultBar, ui.winDouble.transform, ui.winNext.transform }) t.localScale = Vector3.one;
            _pointerRun = false; _claiming = false;
            _levelNumber = Mathf.Max(1, number);
            if (DemoOn) _level = demoBank.levels[(_levelNumber - 1) % demoBank.levels.Count];
            else
            {
                _level = bank.Get(_levelNumber, moveRule);
                bank.Prefetch(_levelNumber + 1, moveRule); // past the baked levels: generate the next one in the background
            }
            _board = _level.ToBoard();
            _skewers = _board.SkewerCount;
            StartPlan();
            GrillView.SetCapacity(_board.Capacity);
            _dragging = false;
            _moves = 0; _served = 0; _selected = -1; _busy = false; _extraUsed = false; _won = false; _idle = 0f;
            _history.Clear();
            _totalOrders = _board.OrdersLeft;   // row + queue
            _nextAnimal = (_levelNumber * 3) % art.customers.Length; // different faces from level to level
            _vis.Clear();
            _moods.Clear();
            int firstRow = 0;
            for (int s = 0; s < _skewers; s++)
            {
                int t0 = _board.Target(s);
                // the first customers don't all lose patience at the same moment: each one starts a few seconds later
                _vis.Add(new VisTray { stack = _board.Stack(s), target = t0, face = t0 >= 0 ? NextFace() : -1, who = t0 >= 0 ? NewCustomer(-patienceStagger * firstRow++) : 0, hid = _board.HiddenStack(s), lockPlus1 = _board.Lock(s) + 1 });
            }

            ApplyTheme();

            for (int i = 0; i < ui.grills.Length; i++)
            {
                var g = ui.grills[i];
                bool on = i < _skewers;
                g.gameObject.SetActive(on);
                if (!on) continue;
                g.ResetFx();
                g.rect.localScale = Vector3.one;   // trays that left last level were shrunk to 0
                g.helper = _board.Target(i) < 0;   // free tray (Extra tray booster): no customer, steel frame
                g.SetLit(false, art);
                g.Show(_board.Stack(i), art, cookOnComplete); g.SetFoils(_board.HiddenStack(i)); g.SetLock(_board.Lock(i), art);
            }
            ui.layout.activeGrills = _skewers;
            ui.layout.Apply();
            foreach (var f in ui.flyers) f.gameObject.SetActive(false);

            SetupCustomers();
            ui.levelText.text = $"Level {_levelNumber}";
            ShowTierTag();
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
            ui.layout.counterBottom = theme.counterBottom;
            ui.layout.Apply();
            int idx = System.Array.IndexOf(art.themes, theme);
            if (idx != SaveData.LastTheme)
            {
                if (idx > SaveData.LastTheme && _levelNumber > 1)
                    StartCoroutine(AfterLoading(.6f, () => Toast($"New stall unlocked: {theme.displayName}!")));
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
            while (Covered) yield return null;
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
        // one customer above each order tray (Sort Streak style): their order is sorted into the tray right below them
        void SetupCustomers()
        {
            BindCustomers();
            for (int i = 0; i < _activeCustomers; i++) StartCoroutine(Arrive(ui.customers[i], .15f + i * .12f));
        }

        /// <summary>Customer k stands above the k-th tray with an order (an Extra tray has none). Instant, no animation.</summary>
        void BindCustomers()
        {
            var trays = new List<int>();
            for (int s = 0; s < _vis.Count; s++) if (_vis[s].target >= 0) trays.Add(s);
            _activeCustomers = Mathf.Min(ui.customers.Length, trays.Count);
            var map = new int[ui.customers.Length];
            for (int i = 0; i < map.Length; i++) map[i] = i < trays.Count ? trays[i] : -1;
            ui.layout.customerTrays = map;
            ui.layout.activeCustomers = _activeCustomers;
            ui.layout.MarkDirty();
            ui.layout.Apply();
            // bubbles shrink when customers stand close, so neighbouring bubbles never touch (Aaron 2026-11-25)
            float bubbleScale = 1f;
            for (int i = 1; i < _activeCustomers; i++)
            {
                var a = ui.customers[i - 1].rect; var b = ui.customers[i].rect;
                // centre distance in the customer slot's own units (the slot itself is scaled by the layout)
                float d = Mathf.Abs(b.anchoredPosition.x - a.anchoredPosition.x) / Mathf.Max(.01f, b.localScale.x);
                bubbleScale = Mathf.Min(bubbleScale, (d - CustomerView.BubbleGap) / CustomerView.BubbleW);
            }
            bubbleScale = Mathf.Clamp(bubbleScale, .6f, 1f);
            foreach (var cv in ui.customers) cv.bubbleScale = bubbleScale;
            for (int i = 0; i < ui.customers.Length; i++)
            {
                var c = ui.customers[i];
                bool on = i < _activeCustomers;
                c.gameObject.SetActive(on);
                c.busy = false;
                c.tray = on ? map[i] : -1;
                if (!on) { c.food = -1; continue; }
                var t = _vis[c.tray];
                c.food = t.target;
                c.face = Mathf.Max(0, t.face) % art.customers.Length;
                c.who = t.who; c.anim = false;
                if (c.moodCo != null) { StopCoroutine(c.moodCo); c.moodCo = null; }
                if (c.emote != null) c.emote.gameObject.SetActive(false);
                SetMood(c, MoodOf(c).mood);   // re-binding after a serve keeps everyone's face (it used to reset to waiting)
                SetOrder(c);
                c.check.gameObject.SetActive(false);
                c.body.localScale = Vector3.one;
                c.body.anchoredPosition = new Vector2(0f, CustomerView.BodyY);
                c.bubble.localScale = c.BubbleSize;
                c.group.alpha = 1f;
            }
        }

        /// <summary>Shows the row mirror on the grills (instant): stacks, steel frame for free trays, layout.</summary>
        void BindRow()
        {
            int n = _vis.Count;
            for (int i = 0; i < ui.grills.Length; i++)
            {
                var g = ui.grills[i];
                bool on = i < n;
                g.gameObject.SetActive(on);
                if (!on) continue;
                g.ResetFx();
                g.helper = _vis[i].target < 0;
                g.SetLit(false, art);
                g.Show(_vis[i].stack, art, cookOnComplete);
                g.SetFoils(_vis[i].hid);
                g.SetLock(_vis[i].lockPlus1 - 1, art);
                g.rect.localScale = Vector3.one;
            }
            ui.layout.activeGrills = n;
            ui.layout.MarkDirty();
            ui.layout.Apply();
        }

        /// <summary>Rebuilds the mirror from the Board (after Undo / Shuffle / Extra tray), keeping faces where orders match.</summary>
        void SyncVisFromBoard()
        {
            var old = new List<VisTray>(_vis);
            _vis.Clear();
            for (int s = 0; s < _board.SkewerCount; s++)
            {
                int t = _board.Target(s);
                bool same = s < old.Count && old[s].target == t;
                int face = same ? old[s].face : (t >= 0 ? NextFace() : -1);
                int who = same ? old[s].who : (t >= 0 ? NewCustomer(0f) : 0);
                _vis.Add(new VisTray { stack = _board.Stack(s), target = t, face = face, who = who, hid = _board.HiddenStack(s), lockPlus1 = _board.Lock(s) + 1 });
            }
        }

        bool VisMatchesBoard()
        {
            if (_vis.Count != _board.SkewerCount) return false;
            for (int s = 0; s < _vis.Count; s++)
            {
                if (_vis[s].target != _board.Target(s) || _vis[s].stack.Length != _board.Count(s)) return false;
                for (int i = 0; i < _vis[s].stack.Length; i++) if (_vis[s].stack[i] != _board.Get(s, i)) return false;
            }
            return true;
        }

        /// <summary>The order bubble shows a small skewer with the pieces this customer wants (Aaron 2026-11-21).</summary>
        void SetOrder(CustomerView c)
        {
            var fa = art.foods[c.food];
            var o = c.order;
            o.gameObject.SetActive(true);
            o.Set(fa, _board.Pieces, fa.width * GrillView.FoodScale);   // same layout the finished tray merges into
            o.SetTilt(CustomerView.OrderTilt);
            // the whole skewer (stick ends too) fits inside the bubble, centred (Aaron 2026-11-25: they overlapped)
            float k = o.FitBoxWithStick(CustomerView.OrderTilt, CustomerView.OrderFitW, CustomerView.OrderFitH, out var mid);
            o.rect.localScale = new Vector3(k, k, 1f);
            o.rect.anchoredPosition = new Vector2(0f, CustomerView.OrderBoxY) - mid * k;
        }

        IEnumerator Arrive(CustomerView c, float delay)
        {
            c.busy = true;
            c.body.anchoredPosition = new Vector2(0f, CustomerView.BodyY - 330f);
            c.bubble.localScale = Vector3.zero;
            c.group.alpha = 1f;
            while (Covered) yield return null;
            yield return new WaitForSeconds(delay);
            yield return Tween.Move(c.body, new Vector2(0f, CustomerView.BodyY), .35f, t => Ease.OutBack(t, 1.2f));
            yield return Tween.Scale(c.bubble, c.BubbleSize, .3f, t => Ease.OutBack(t));
            c.busy = false;
        }

        IEnumerator Leave(CustomerView c)
        {
            c.busy = true;
            yield return new WaitForSeconds(.55f);
            StartCoroutine(Tween.Scale(c.bubble, Vector3.zero, .2f, Ease.InCubic));
            yield return Tween.Move(c.body, new Vector2(0f, CustomerView.BodyY - 330f), .3f, Ease.InCubic);
            c.food = -1;
            c.busy = false;
        }

        CustomerView CustomerForTray(int tray)
        {
            foreach (var c in ui.customers)
                if (c.gameObject.activeSelf && c.tray == tray) return c;
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
            if (_busy || _won || _dragging || AnyPopupOpen() || _autoPlay || Covered) { _idle = 0f; return; }
            _idle += Time.deltaTime;
            bool tutorial = _levelNumber <= tutorialLevels;
            float wait = tutorial ? .9f : idleHintSeconds;
            if (_levelNumber <= 10 && _idle >= wait && _hintCo == null && _selected < 0)
            {
                _idle = -4f; // don't spam
                if (!_hintSearching) StartCoroutine(IdleHint(tutorial ? 9999f : 3.6f));
            }
        }

        // fire: the coal glow layer flickers like real embers (slow breathing + fast flicker), the soft ember light behind
        // the grill follows it; smoke: light wisps rising from anywhere along each grill (Aaron 2026-11-18)
        void LateUpdate()
        {
            if (_board == null) return;
            CustomersLive();
            float t = Time.time, dt = Time.deltaTime;
            for (int i = 0; i < _board.SkewerCount && i < ui.grills.Length; i++)
            {
                var gv = ui.grills[i];
                // gentle (Aaron 2026-11-21): slow breathing, a little soft wobble, each grill on its own phase
                float slow = Mathf.PerlinNoise(t * .9f, i * 3.7f);
                float soft = Mathf.PerlinNoise(t * 2.4f, i * 1.3f + 20f);
                float heat = Mathf.Clamp01(slow * .8f + soft * .2f);
                if (gv.coalGlow != null)
                {
                    // the bed stays opaque (the sand would show through the coal gaps): it breathes in brightness, and
                    // two shimmer layers (different patches of the bed) pulse out of step on top, so the embers come
                    // alive patch by patch (user 2026-11-21: the first version only dimmed the whole bed)
                    float k = .56f + heat * .3f;
                    gv.coalGlow.color = new Color(k, k * (.9f + heat * .1f), k * (.84f + heat * .16f), 1f);
                    float a = Mathf.Clamp01(Mathf.PerlinNoise(t * 1.3f, i * 2.1f + 40f) * 1.6f - .35f);
                    float b = Mathf.Clamp01(Mathf.PerlinNoise(t * 1.1f + 9f, i * 1.7f + 70f) * 1.6f - .35f);
                    if (gv.coalHotA != null) GrillView.SetAlpha(gv.coalHotA, a);
                    if (gv.coalHotB != null) GrillView.SetAlpha(gv.coalHotB, b);
                }
                if (gv.ember != null) GrillView.SetAlpha(gv.ember, .16f + heat * .26f);
                if (gv.smokes == null || gv.smokes.Length == 0) continue;
                if (i >= _smokeTimers.Length) continue;
                _smokeTimers[i] -= dt;
                if (_smokeTimers[i] > 0f) continue;
                _smokeTimers[i] = Random.Range(.45f, 1.1f);
                foreach (var sm in gv.smokes)
                    if (!sm.gameObject.activeSelf) { StartCoroutine(SmokeWisp(sm)); break; }
            }
        }

        readonly float[] _smokeTimers = new float[16];

        IEnumerator SmokeWisp(Image s)
        {
            var rt = s.rectTransform;
            s.gameObject.SetActive(true);
            // start anywhere along the coal bed, rise a little, grow and fade: soft and light
            float x = Random.Range(-45f, 45f), y0 = Random.Range(-330f, 300f) * GrillView.VStretch;
            float drift = Random.Range(-35f, 35f), rise = Random.Range(110f, 170f);
            float dur = Random.Range(2.2f, 3f), peak = Random.Range(.09f, .14f);
            rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-20f, 20f));
            yield return Tween.Run(dur, k =>
            {
                rt.anchoredPosition = new Vector2(x + drift * k, y0 + k * rise);
                rt.localScale = Vector3.one * (.55f + k * .7f);
                s.color = new Color(1f, .98f, .95f, Mathf.Sin(k * Mathf.PI) * peak);
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
            if (_busy || _won || _dragging || AnyPopupOpen() || g >= _board.SkewerCount) return;
            _idle = 0f;
            StopHint();

            if (_selected < 0)
            {
                if (_board.IsEmpty(g)) { StartCoroutine(Tween.Shake(ui.grills[g].rect, 8f, .2f)); return; }
                if (_board.IsLocked(g)) { StartCoroutine(Tween.Shake(ui.grills[g].rect, 8f, .2f)); audioManager.Play(art.invalid, .6f); Toast(LockedMsg); return; }
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
                Annoy(g);
                Toast(WhyInvalid(_selected, idx, g));
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
                    v.slots[i].anchoredPosition = new Vector2(0f, GrillView.SlotY[i] - Lift * .6f * e + bob);
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

        // ------------------------------------------------------------------ drag (Sort Streak style: drag any piece, the pieces above come along)
        bool _dragging;
        int _dragFrom, _dragStart, _dragCount, _dragHover = -1;
        Vector2 _dragGrab;

        Vector2 ScreenToFly(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(ui.flyLayer, screen, null, out var p);
            return p;
        }

        void OnDragStart(int g, int slot, Vector2 screen)
        {
            if (_busy || _won || _dragging || AnyPopupOpen() || g >= _board.SkewerCount) return;
            _idle = 0f;
            StopHint();
            if (_selected >= 0) Deselect();
            if (_board.IsEmpty(g)) { StartCoroutine(Tween.Shake(ui.grills[g].rect, 8f, .2f)); return; }
            if (_board.IsLocked(g)) { StartCoroutine(Tween.Shake(ui.grills[g].rect, 8f, .2f)); audioManager.Play(art.invalid, .6f); Toast(LockedMsg); return; }
            int start = _board.MoveStart(moveRule, g, moveRule == MoveRule.AnyWithAbove ? Mathf.Min(slot, _board.Count(g) - 1) : -1);
            var v = ui.grills[g];
            _dragging = true; _dragFrom = g; _dragStart = start; _dragCount = _board.Count(g) - start; _dragHover = -1;
            // lift the block out of the tray into the fly layer (drawn above every grill)
            for (int k = 0; k < _dragCount; k++)
            {
                int si = start + k;
                PrepareFlyer(k, art.foods[_board.Get(g, si)], cookOnComplete);
                SetFlyerFoil(k, _board.IsHidden(g, si));
                var fl = ui.flyers[k];
                fl.anchoredPosition = (Vector2)ui.flyLayer.InverseTransformPoint(v.slots[si].position);
                fl.localScale = Vector3.one; fl.localRotation = Quaternion.identity;
                ui.flyerShadows[k].rectTransform.anchoredPosition = SelShadowOffset;
                fl.gameObject.SetActive(true);
                v.HideSlot(si);
            }
            _dragGrab = ScreenToFly(screen) - ui.flyers[0].anchoredPosition;
            _dragGrab.y -= 30f; // the grabbed piece sits just above the finger, the rest hangs below it
            v.SetLit(true, art);
            StartCoroutine(Tween.Fade(v.glow, .5f, .18f));
            audioManager.Play(art.pick, .8f, 1f + Random.Range(-.05f, .05f));
            Haptics.Light();
            OnDragMove(screen);
        }

        void OnDragMove(Vector2 screen)
        {
            if (!_dragging) return;
            var p = ScreenToFly(screen) - _dragGrab;
            float t = Time.time;
            for (int k = 0; k < _dragCount; k++)
            {
                var fl = ui.flyers[k];
                float dy = GrillView.SlotY[_dragStart + k] - GrillView.SlotY[_dragStart];
                fl.anchoredPosition = p + new Vector2(Mathf.Sin(t * 9f + k) * 2f, dy);
                fl.localScale = Vector3.one * SelScale;
            }
            // light up the tray the block would land on
            int hover = DropTarget(screen);
            if (hover != _dragHover)
            {
                if (_dragHover >= 0) GrillView.SetAlpha(ui.grills[_dragHover].highlight, 0f);
                _dragHover = hover;
                if (hover >= 0 && hover != _dragFrom && _board.TryMakeMove(moveRule, _dragFrom, _dragStart, hover, out _))
                    GrillView.SetAlpha(ui.grills[hover].highlight, .45f);
            }
        }

        /// <summary>Tray under the finger (or the nearest tray by x when the finger is over the board), -1 if none.</summary>
        int DropTarget(Vector2 screen)
        {
            int best = -1; float bd = float.MaxValue;
            for (int g = 0; g < _board.SkewerCount; g++)
            {
                var r = ui.grills[g].rect;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(r, screen, null, out var local);
                if (Mathf.Abs(local.y) > GrillView.GrillH * .5f + 160f) continue;  // well above / below the board
                float d = Mathf.Abs(local.x);
                if (d < bd) { bd = d; best = g; }
            }
            return bd <= StageLayout.Spacing * .6f ? best : -1;
        }

        void OnDragEnd(Vector2 screen)
        {
            if (!_dragging) return;
            _dragging = false;
            if (_dragHover >= 0) GrillView.SetAlpha(ui.grills[_dragHover].highlight, 0f);
            int to = DropTarget(screen);
            var src = ui.grills[_dragFrom];
            UnheatGrill(src);
            if (to >= 0 && to != _dragFrom && _board.TryMakeMove(moveRule, _dragFrom, _dragStart, to, out var m))
            {
                StartCoroutine(DoMove(m, true));
                return;
            }
            if (to >= 0 && to != _dragFrom)
            {
                audioManager.Play(art.invalid, .7f);
                Haptics.Light();
                StartCoroutine(Tween.Shake(ui.grills[to].rect));
                Annoy(to);
                Toast(WhyInvalid(_dragFrom, _dragStart, to));
            }
            StartCoroutine(ReturnDrag(_dragFrom, _dragStart, _dragCount));
        }

        /// <summary>Same wording as Sort Streak's hints: wrong food at the bottom, or not enough room.</summary>
        string WhyInvalid(int from, int start, int to)
        {
            int s0 = _board.MoveStart(moveRule, from, start);
            int group = _board.Count(from) - s0;
            bool matches = _board.IsEmpty(to) || _board.Top(to) == _board.Get(from, s0);
            if (!matches) return "Match the bottom food to drop here!";
            if (_board.IsLocked(to)) return LockedMsg;
            if (_board.Capacity - _board.Count(to) < group) return "This tray can't hold that many!";
            if (!_board.FillsUniform(from, s0, group, to)) return "Fill a tray to the top with one food only!";
            if (!_board.IsEmpty(to) && _board.HasHidden(from, s0, group)) return "Wrapped food can only go on an empty tray!";
            return "Can't drop there!";
        }

        /// <summary>Invalid drop: the block flies back to where it came from.</summary>
        IEnumerator ReturnDrag(int g, int start, int count)
        {
            _busy = true;
            var v = ui.grills[g];
            int flying = count;
            for (int k = 0; k < count; k++)
            {
                int food = _board.Get(g, start + k);
                StartCoroutine(FlyFrom(k, ui.flyers[k].anchoredPosition, ui.flyers[k].localScale.x, v, start + k, food, k * .03f, () => flying--));
            }
            while (flying > 0) yield return null;
            v.Show(_board.Stack(g), art, cookOnComplete); v.SetFoils(_board.HiddenStack(g)); v.SetLock(_board.Lock(g), art);
            _busy = false;
        }

        // ------------------------------------------------------------------ moves
        IEnumerator DoMove(Move m, bool fromDrag = false)
        {
            _busy = true;
            if (_levelNumber > tutorialLevels) HideTip(); // the player got it (tutorial levels keep their banner)
            var src = ui.grills[m.From]; var dst = ui.grills[m.To];
            if (!fromDrag) { StopSelectFx(); UnheatGrill(src); }
            _selected = -1;
            int dstBefore = _board.Count(m.To);
            int lifted = fromDrag ? _dragStart : m.Index;   // lowest piece that was picked up
            var moved = new int[m.Count];
            for (int k = 0; k < m.Count; k++) moved[k] = _board.Get(m.From, m.Index + k);
            var stay = new List<(int slot, int food)>();
            for (int i = lifted; i < m.Index; i++) stay.Add((i, _board.Get(m.From, i)));

            _history.Push((_board.Clone(), _moves));
            // how both trays look right after the move, before any order is served (the view shows this first)
            var preSrc = new int[m.Index];
            for (int i = 0; i < m.Index; i++) preSrc[i] = _board.Get(m.From, i);
            int dstN = _board.Count(m.To);
            var preDst = new int[dstN + m.Count];
            for (int i = 0; i < dstN; i++) preDst[i] = _board.Get(m.To, i);
            for (int k = 0; k < m.Count; k++) preDst[dstN + k] = moved[k];
            var hidSrc = new bool[m.Index];
            for (int i = 0; i < m.Index; i++) hidSrc[i] = _board.IsHidden(m.From, i);
            var hidDst = new bool[dstN + m.Count];
            for (int i = 0; i < dstN; i++) hidDst[i] = _board.IsHidden(m.To, i);
            for (int k = 0; k < m.Count; k++) hidDst[dstN + k] = _board.IsHidden(m.From, m.Index + k);
            bool revSrc = m.Index > 0 && hidSrc[m.Index - 1];
            if (revSrc) hidSrc[m.Index - 1] = false;
            bool revDst = hidDst.Length > 0 && hidDst[hidDst.Length - 1];
            if (revDst) hidDst[hidDst.Length - 1] = false;
            var served = _board.Apply(m);
            if (verboseLog) Debug.Log($"[Grill Sort] move {m} -> {_board} served {served.Count}");
            _moves++;

            int flying = 0;
            if (fromDrag)
            {
                // the dragged block is already in the fly layer: pieces that didn't fit (TopGroup) go home, the rest land
                for (int k = 0; k < stay.Count; k++)
                {
                    flying++;
                    StartCoroutine(FlyFrom(k, ui.flyers[k].anchoredPosition, ui.flyers[k].localScale.x, src, stay[k].slot, stay[k].food, 0f, () => flying--));
                }
                for (int k = 0; k < m.Count; k++)
                {
                    int f = stay.Count + k;
                    flying++;
                    StartCoroutine(FlyFrom(f, ui.flyers[f].anchoredPosition, ui.flyers[f].localScale.x, dst, dstBefore + k, moved[k], k * .04f, () => flying--));
                }
            }
            else
            {
                // pieces that stay lifted but did not move (TopGroup partial fit) drop back
                for (int i = 0; i < m.Index; i++)
                    if (src.slots[i].anchoredPosition.y != GrillView.SlotY[i] || src.slots[i].localScale.x != 1f)
                        StartCoroutine(SettleSlot(src, i));
                // fly the moved pieces, top first
                for (int k = m.Count - 1, f = 0; k >= 0; k--, f++)
                {
                    int si = m.Index + k, ti = dstBefore + k;
                    flying++;
                    StartCoroutine(FlyPiece(f, src, si, dst, ti, moved[k], f * .06f, () => flying--));
                }
            }
            while (flying > 0) yield return null;

            src.Show(preSrc, art, cookOnComplete);
            dst.Show(preDst, art, cookOnComplete);
            src.SetFoils(hidSrc); dst.SetFoils(hidDst);
            if (revSrc) StartCoroutine(RevealPop(src, m.Index - 1));
            if (revDst) StartCoroutine(RevealPop(dst, hidDst.Length - 1));
            SetVis(m.From, preSrc, hidSrc); SetVis(m.To, preDst, hidDst);
            if (served.Count == 0) Progress(m.To);

            // serve whatever the move completed, one after the other (both ends can fill up in one move); each serve
            // removes the tray, slides the rest left and brings in the next queued tray
            int arrivals = 0;
            foreach (var e in served) if (e.Arrived) arrivals++;
            int q = _board.QueueNext - arrivals;
            foreach (var e in served) yield return ServeOrder(e, e.Arrived ? q++ : -1);
            if (served.Count > 0 && !VisMatchesBoard()) { SyncVisFromBoard(); BindRow(); BindCustomers(); }

            if (_served >= _totalOrders) { yield return Win(); yield break; }
            _busy = false;
            CheckStuck();
        }

        float _lastDropSound = -1f, _nextDropSound;
        int _dropStep;

        IEnumerator DropSound(float delay, float pitch)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            audioManager.PlayVariant(art.drops, .85f, pitch);
        }

        IEnumerator FlyPiece(int flyer, GrillView src, int si, GrillView dst, int ti, int food, float delay, System.Action done)
        {
            yield return new WaitForSeconds(delay);
            var p0 = (Vector2)ui.flyLayer.InverseTransformPoint(src.slots[si].position);
            float s0 = src.slots[si].localScale.x;
            PrepareFlyer(flyer, art.foods[food], cookOnComplete);
            SetFlyerFoil(flyer, src.foils != null && si < src.foils.Length && src.foils[si].gameObject.activeSelf);
            src.HideSlot(si);
            yield return FlyFrom(flyer, p0, s0, dst, ti, food, 0f, done);
        }

        /// <summary>Flies flyer <paramref name="flyer"/> from <paramref name="p0"/> (fly-layer space) into slot <paramref name="ti"/> of <paramref name="dst"/>.</summary>
        IEnumerator FlyFrom(int flyer, Vector2 p0, float s0, GrillView dst, int ti, int food, float delay, System.Action done)
        {
            var fl = ui.flyers[flyer];
            var fa = art.foods[food];
            fl.anchoredPosition = p0; fl.localScale = Vector3.one * s0; fl.gameObject.SetActive(true);
            if (delay > 0f) yield return new WaitForSeconds(delay);
            var p2 = (Vector2)ui.flyLayer.InverseTransformPoint(dst.rect.TransformPoint(new Vector3(0f, GrillView.SlotY[ti], 0f)));
            float dist = Vector2.Distance(p0, p2);
            var p1 = new Vector2((p0.x + p2.x) * .5f, Mathf.Max(p0.y, p2.y) + Mathf.Min(150f, dist * .4f));
            float d = Mathf.Clamp(.12f + dist / 2600f, .14f, .42f);
            yield return Tween.Run(d, k =>
            {
                float u = 1f - k;
                fl.anchoredPosition = u * u * p0 + 2f * u * k * p1 + k * k * p2;
                fl.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(k * Mathf.PI) * (p2.x > p0.x ? -14f : 14f) * Mathf.Clamp01(dist / 200f));
                fl.localScale = Vector3.one * Mathf.Lerp(s0, 1f, k);
            }, Ease.InOutCubic);
            fl.localScale = Vector3.one;
            fl.gameObject.SetActive(false);
            fl.localRotation = Quaternion.identity;
            dst.SetFood(ti, fa, cookOnComplete);
            dst.ResetSlot(ti);
            StartCoroutine(Tween.Bounce(dst.slots[ti], .16f, .26f));
            // like Skewer Jam: every landing piece gives its own wooden tock, ~0.12 s apart and stepping up a little,
            // so a dragged stack lands as a satisfying "tock-tock-tock" cascade (user 2026-11-22)
            float at = Mathf.Max(Time.time, _nextDropSound);
            int step = Time.time - _lastDropSound < .6f ? _dropStep + 1 : 0;
            _dropStep = step; _lastDropSound = Time.time; _nextDropSound = at + .12f;
            StartCoroutine(DropSound(at - Time.time, 1f + Mathf.Min(step, 5) * .045f));
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
            SetFlyerFoil(i, false);
        }

        // ------------------------------------------------------------------ completion & serving
        void SetVis(int s, int[] stack) { var t = _vis[s]; t.stack = stack; _vis[s] = t; }
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
        }

        /// <summary>
        /// One order served (Sort Streak style): the finished tray glows, the pieces fly up to the customer on a small
        /// skewer, the customer cheers, then tray and customer leave, the trays after it slide one slot left and the
        /// next queued tray (new customer + mixed food) slides in from the right.
        /// </summary>
        IEnumerator ServeOrder(Served e, int queueIndex)
        {
            int g = e.Slot;
            var v = ui.grills[g];
            var fa = art.foods[e.Food];
            int pieces = _board.Pieces;
            audioManager.Play(art.sizzle, .55f);   // softer: the serve chime is the star
            Haptics.Medium();
            // bring the finished tray to the front so its glow, sparkles, steam and "Perfect!" are not hidden behind the
            // trays to its right (user 2026-11-21); the fly layer stays on top of everything
            v.transform.SetAsLastSibling();
            ui.flyLayer.SetAsLastSibling();
            v.SetLit(true, art);
            StartCoroutine(Tween.Fade(v.glow, .9f, .25f));
            StartCoroutine(SteamLoop(v, 1.6f));
            StartCoroutine(Sparkles(v));
            StartCoroutine(PopWord(v.perfect));
            for (int i = 0; i < pieces; i++) StartCoroutine(Sizzle(v.slots[i], i * .05f));

            if (cookOnComplete) yield return CookTray(v, fa, pieces);
            else yield return new WaitForSeconds(.55f);

            // the customer standing above this tray ordered it
            var c = CustomerForTray(g);
            if (c != null)
            {
                yield return ServeSkewer(v, fa, pieces, c);
                yield return Cheer(c, false);
            }
            else for (int i = 0; i < pieces; i++) v.HideSlot(i);

            _served++;
            _history.Clear();
            UpdateOrders(true);
            // (the serve chime already played when the skewer landed)
            Haptics.Medium();
            // the level's last order: no row shuffle (the user only saw the leftover helper tray for seconds), but the
            // tray and customer still leave the same way as every other serve before Well Done (Aaron 2026-11-26: "on
            // the last grill, the disappearing part of the animation seems to be cut off")
            if (_served >= _totalOrders)
            {
                yield return LeaveTray(g, c);
                _vis.RemoveAt(g);   // the view now matches the board, so nothing re-lays the row out (the helper used to jump)
                yield break;
            }
            yield return ShiftRow(g, e.Arrived, queueIndex, c);
            UnlockFor(e.Food);
        }

        /// <summary>The served tray shrinks away and its customer sinks behind the counter.</summary>
        IEnumerator LeaveTray(int g, CustomerView leaving)
        {
            var gone = ui.grills[g];
            if (leaving != null)
            {
                StartCoroutine(Tween.Scale(leaving.bubble, Vector3.zero, .2f, Ease.InCubic));
                StartCoroutine(Tween.Move(leaving.body, new Vector2(0f, CustomerView.BodyY - 330f), .28f, Ease.InCubic));
            }
            StartCoroutine(Tween.Fade(gone.glow, 0f, .2f));
            yield return Tween.Run(.26f, k => { float sc = 1f - Ease.InCubic(k); gone.rect.localScale = new Vector3(sc, sc, 1f); });
            gone.gameObject.SetActive(false);
            yield return new WaitForSeconds(.04f);   // let the customer finish sinking (.28 s) before anything else moves
        }

        /// <summary>Tray g and its customer leave; trays after it slide left; the next queued tray slides in from the right.</summary>
        IEnumerator ShiftRow(int g, bool arrived, int queueIndex, CustomerView leaving)
        {
            int nBefore = _vis.Count;
            // 1) the served tray shrinks away, its customer sinks behind the counter
            yield return LeaveTray(g, leaving);

            _vis.RemoveAt(g);
            if (arrived && queueIndex >= 0)
            {
                int t = _board.QueueTarget(queueIndex);
                var qh = _board.QueueHidden(queueIndex);
                if (qh.Length > 0) qh[qh.Length - 1] = false;   // the bottom piece is always visible
                _vis.Add(new VisTray { stack = _board.QueueStack(queueIndex), target = t, face = t >= 0 ? NextFace() : -1, who = t >= 0 ? NewCustomer(0f) : 0, hid = qh, lockPlus1 = _board.QueueLock(queueIndex) + 1 });
            }
            int nAfter = _vis.Count;

            // 2) everything else slides to its new place (and the row re-centres if it got shorter)
            var lay = ui.layout;
            var tweens = new List<(RectTransform rt, float from, float to)>();
            for (int j = 0; j < nBefore; j++)
            {
                if (j == g) continue;
                int nj = j > g ? j - 1 : j;
                var rt = ui.grills[j].rect;
                tweens.Add((rt, rt.anchoredPosition.x, lay.TrayX(nj, nAfter)));
            }
            foreach (var c in ui.customers)
            {
                if (!c.gameObject.activeSelf || c == leaving || c.tray < 0) continue;
                int nj = c.tray > g ? c.tray - 1 : c.tray;
                tweens.Add((c.rect, c.rect.anchoredPosition.x, lay.CustomerX(nj, nAfter)));
            }
            if (arrived) audioManager.Play(art.whoosh, .25f, .95f);
            yield return Tween.Run(.34f, k =>
            {
                float e2 = Ease.InOutCubic(k);
                foreach (var (rt, from, to) in tweens) rt.anchoredPosition = new Vector2(Mathf.LerpUnclamped(from, to, e2), rt.anchoredPosition.y);
            });

            // 3) the views follow the new row (same picture, now bound to the right trays)
            BindRow();
            BindCustomers();

            // 4) the new tray slides in from the right, its customer pops up behind the counter
            if (arrived)
            {
                int i = nAfter - 1;
                var rt = ui.grills[i].rect;
                float home = rt.anchoredPosition.x;
                rt.anchoredPosition = new Vector2(home + 900f, rt.anchoredPosition.y);
                CustomerView nc = null;
                foreach (var c in ui.customers) if (c.gameObject.activeSelf && c.tray == i) nc = c;
                if (nc != null) StartCoroutine(Arrive(nc, .12f));
                yield return Tween.Run(.42f, k => rt.anchoredPosition = new Vector2(Mathf.LerpUnclamped(home + 900f, home, Ease.OutBack(k, 1.1f)), rt.anchoredPosition.y));
                rt.anchoredPosition = new Vector2(home, rt.anchoredPosition.y);
                audioManager.Play(art.pop, .3f, 1.05f);
            }
        }

        /// <summary>
        /// Aaron 2026-11-25: "once a skewer is completed, it would look cleaner if it detached fully from the grill, then
        /// used that same skewer for the fly-up animation." The flying skewer (<see cref="GameUi.serveSkewer"/>) takes the
        /// exact pose of the tray's own long skewer and food, and the tray's skewer is hidden in the same frame, so the
        /// player sees the real skewer: it dips, pulls straight up until its tip clears the grill (the food slides
        /// together on the way and the stick shortens to the order skewer's layout), then flies in an arc to the
        /// customer, tilting and shrinking until it sits exactly on the order skewer in their bubble.
        /// </summary>
        IEnumerator ServeSkewer(GrillView v, GameArt.FoodArt fa, int pieces, CustomerView c)
        {
            var sk = ui.serveSkewer;
            var fly = ui.flyLayer;
            sk.Set(fa, pieces, fa.width * GrillView.FoodScale);   // compact layout = the bubble's order skewer

            // 1) hand-off: the flying skewer copies the tray's skewer and food exactly (tray-local units; scale s0 maps
            //    them to the fly layer), then the tray's own skewer and food are hidden in the same frame
            var ysA = new float[pieces]; var wA = new float[pieces];
            for (int i = 0; i < pieces; i++)
            {
                var f = v.foods[i].rectTransform;
                ysA[i] = v.rect.InverseTransformPoint(f.position).y;
                wA[i] = f.rect.width * f.lossyScale.x / v.rect.lossyScale.x;
            }
            var skr = v.skewer.rectTransform;
            float topA = skr.anchoredPosition.y + skr.rect.height * .5f, botA = skr.anchoredPosition.y - skr.rect.height * .5f;
            var ysB = new float[pieces]; var wB = new float[pieces];
            for (int i = 0; i < pieces; i++) { ysB[i] = sk.PieceY(i); wB[i] = sk.PieceWidth; }
            float topB = sk.CompactTop, botB = sk.CompactBottom;
            var ys = new float[pieces]; var ws = new float[pieces];
            void Morph(float t)
            {
                for (int i = 0; i < pieces; i++) { ys[i] = Mathf.LerpUnclamped(ysA[i], ysB[i], t); ws[i] = Mathf.Lerp(wA[i], wB[i], t); }
                sk.Pose(ys, ws, Mathf.LerpUnclamped(topA, topB, t), Mathf.LerpUnclamped(botA, botB, t));
            }

            Vector2 p0 = fly.InverseTransformPoint(v.rect.position);
            float s0 = v.rect.lossyScale.x / fly.lossyScale.x;
            sk.SetTilt(0f);
            Morph(0f);
            sk.rect.anchoredPosition = p0;
            sk.rect.localScale = new Vector3(s0, s0, 1f);
            sk.rect.SetAsLastSibling();
            sk.gameObject.SetActive(true);
            v.skewer.enabled = false;
            for (int i = 0; i < pieces; i++) v.HideSlot(i);

            float oldTimeScale = Time.timeScale;
            if (serveSlowMotion > 0f) Time.timeScale = serveSlowMotion;

            // 2) a small dip (getting a grip), then the skewer comes out of the grill (the food slides together and the
            //    stick shortens on the way), then it flies to the customer and lands on the order skewer in their bubble
            float grillTop = v.grill.rectTransform.anchoredPosition.y + v.grill.rectTransform.rect.height * .5f;
            var target = c.order.rect;
            Vector2 p2 = fly.InverseTransformPoint(target.position);
            float s2 = target.lossyScale.x / fly.lossyScale.x;
            float tilt2 = CustomerView.OrderTilt;
            void Place(Vector2 p, float scale, float tilt)
            {
                sk.rect.anchoredPosition = p;
                sk.rect.localScale = new Vector3(scale, scale, 1f);
                sk.SetTilt(tilt);
            }
            Vector2 Bez(Vector2 a, Vector2 b, Vector2 cc, float k) { float u = 1f - k; return u * u * a + 2f * u * k * b + k * k * cc; }

            yield return Tween.Run(.07f, k => sk.rect.anchoredPosition = p0 + new Vector2(0f, -14f * s0 * Mathf.Sin(k * Mathf.PI * .5f)));
            audioManager.Play(art.pop, .45f, 1.1f);
            Haptics.Light();
            Vector2 pDip = sk.rect.anchoredPosition;

            // Aaron 2026-11-26 picked "Lift and Turn" from 4 options (straight lift, lift and turn, quick zip, side swing;
            // videos in Build/ServeStyles): it comes out while turning to lie flat (the bubble's angle) and shrinking, so
            // it clears the grill as a short horizontal skewer just above the grill (below the faces), then hops up into
            // the bubble. The straight lift covered the customer's face.
            float sMid = Mathf.Lerp(s0, s2, .45f);
            var pOut = p0 + new Vector2(0f, (grillTop + 40f) * s0);
            yield return Tween.Run(.34f, k =>
            {
                float e = Ease.OutCubic(k);
                Place(Vector2.LerpUnclamped(pDip, pOut, Ease.OutBack(k, 1.1f)), Mathf.Lerp(s0, sMid, e), Mathf.Lerp(0f, tilt2, e));
                Morph(e);
            });
            Morph(1f);
            var p1 = new Vector2((pOut.x + p2.x) * .5f, Mathf.Max(pOut.y, p2.y) + 90f);
            audioManager.Play(art.whoosh, .35f, 1.05f);
            yield return Tween.Run(.36f, k => Place(Bez(pOut, p1, p2, k), Mathf.Lerp(sMid, s2, k), tilt2), Ease.InOutCubic);
            Place(p2, s2, tilt2);
            if (serveSlowMotion > 0f) Time.timeScale = oldTimeScale;
            sk.gameObject.SetActive(false);
            audioManager.Play(art.serve, 1f);   // THE order-served sound (one clean chime)
            StartCoroutine(Tween.Bounce(c.bubble, .14f, .22f, c.bubbleScale));
        }

        IEnumerator Cheer(CustomerView c, bool leave = true)
        {
            c.anim = true;
            SetMood(c, Mood.Happy);
            ShowEmote(c, art.emoteHeart, 1.1f);
            c.check.gameObject.SetActive(true);
            c.check.rectTransform.localScale = Vector3.zero;
            StartCoroutine(Tween.Scale(c.check.rectTransform, Vector3.one, .35f, Ease.OutElastic));
            // no coin here: it clashed with the serve chime
            var b = c.body;
            yield return Tween.Run(.45f, k =>
            {
                float hop = Mathf.Sin(k * Mathf.PI * 2f) * 26f * (1f - k);
                b.anchoredPosition = new Vector2(0f, CustomerView.BodyY + Mathf.Abs(hop));
                float sq = Mathf.Sin(k * Mathf.PI * 4f) * .06f * (1f - k);
                b.localScale = new Vector3(1f + sq, 1f - sq, 1f);
            });
            b.localScale = Vector3.one;
            c.anim = false;
            if (leave) StartCoroutine(Leave(c));
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

        // ------------------------------------------------------------------ win (FishUp!-style "Well Done!")
        /// <summary>Multipliers on the bar under the reward, left to right (the mascot pointer slides across them).</summary>
        public static readonly int[] WinMults = { 2, 3, 5, 3, 2 };
        int _winMult = 2;
        bool _pointerRun, _claiming;

        IEnumerator Win()
        {
            _won = true;
            _busy = true;
            _claiming = false;
            audioManager.SetAmbience(art.sizzleLoop, false);
            // save progress the moment the level is beaten (not only when "Claim" is pressed), so closing the app on
            // the win screen doesn't replay a finished level
            if (!useTestLevel && !(limitToTenLevels && _levelNumber + 1 > 10) && SaveData.Level <= _levelNumber)
                SaveData.Level = _levelNumber + 1;
            yield return new WaitForSeconds(.35f);   // was 1.1 s: just long enough to see the last customer cheer
            int par = Mathf.Max(1, _level.par);
            if (moveRule == MoveRule.SingleTop && _level.rule != MoveRule.SingleTop) par = par * 3 / 2; // single moves take longer
            int stars = _moves <= par + 2 ? 3 : _moves <= par * 3 / 2 + 2 ? 2 : 1;
            _winReward = (10 + stars * 5) * (_level.hard ? 2 : 1);   // Hard levels pay double (Sort Streak)
            ui.winCoins.text = _winReward.ToString();
            ui.winCoinPillText.text = _shownCoins.ToString("N0");
            UpdateWinMult(0);

            var pops = new Transform[] { ui.winWord, ui.winBubble, ui.winMultBar, ui.winNext.transform, ui.winDouble.transform };
            foreach (var t in pops) t.localScale = Vector3.zero;
            foreach (var sp in ui.winSparkles) GrillView.SetAlpha(sp, 0f);
            foreach (var c in ui.winFlyCoins) c.gameObject.SetActive(false);
            GrillView.SetAlpha(ui.winRays, 0f);

            var P = ui.winPopup;
            P.gameObject.SetActive(true); P.alpha = 1f; P.blocksRaycasts = true; P.interactable = false;
            GrillView.SetAlpha(ui.winDim, 0f);
            StartCoroutine(Tween.Run(.3f, k => GrillView.SetAlpha(ui.winDim, k * .95f)));

            // 2) "Well Done!" drops in, the coin bubble swells up with sparkles and rays
            audioManager.Play(art.win, .9f);
            Haptics.Heavy();
            var wHome = ui.winWord.anchoredPosition;
            StartCoroutine(Tween.Run(.45f, k =>
            {
                ui.winWord.anchoredPosition = Vector2.LerpUnclamped(wHome + new Vector2(0f, 160f), wHome, Ease.OutBack(k, 1.6f));
                float s = Mathf.Min(1f, k * 2f); ui.winWord.localScale = new Vector3(s, s, 1f);
            }));
            yield return new WaitForSeconds(.2f);
            StartCoroutine(Tween.Scale(ui.winBubble, Vector3.one, .45f, t => Ease.OutBack(t, 1.8f)));
            StartCoroutine(Tween.Run(.5f, k => GrillView.SetAlpha(ui.winRays, k * .32f)));
            StartCoroutine(WinAmbient());
            audioManager.Play(art.coin, .6f);
            yield return new WaitForSeconds(.3f);

            // 3) the multiplier bar and the two claim buttons pop up; the pointer starts sliding
            StartCoroutine(Tween.Scale(ui.winMultBar, Vector3.one, .35f, t => Ease.OutBack(t)));
            yield return new WaitForSeconds(.12f);
            StartCoroutine(Tween.Scale(ui.winNext.transform, Vector3.one, .35f, t => Ease.OutBack(t)));
            yield return new WaitForSeconds(.08f);
            StartCoroutine(Tween.Scale(ui.winDouble.transform, Vector3.one, .35f, t => Ease.OutBack(t)));
            _pointerRun = true;
            StartCoroutine(WinPointer());
            yield return new WaitForSeconds(.3f);
            P.interactable = true;
            if (_autoPlay) { yield return new WaitForSeconds(.3f); CollectWin(1, ui.winNext.transform); }
        }

        /// <summary>The sausage slides back and forth along the bar; the "Claim xN" button follows the segment under it.</summary>
        IEnumerator WinPointer()
        {
            var bar = ui.winMultBar; var ptr = ui.winPointer;
            float w = 800f, seg = w / WinMults.Length, t = 0f;
            while (_pointerRun && ui.winPopup.gameObject.activeSelf)
            {
                t += Time.deltaTime;
                float x = Mathf.PingPong(t * 520f, w - 40f) - (w - 40f) * .5f;
                ptr.anchoredPosition = new Vector2(x, ptr.anchoredPosition.y);
                ptr.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 14f) * 6f);
                int i = Mathf.Clamp(Mathf.FloorToInt((x + w * .5f) / seg), 0, WinMults.Length - 1);
                UpdateWinMult(i);
                yield return null;
            }
            ptr.localRotation = Quaternion.identity;
        }

        void UpdateWinMult(int segment)
        {
            int m = WinMults[segment];
            if (m == _winMult && ui.winDoubleLabel.text.EndsWith(m.ToString())) return;
            _winMult = m;
            ui.winDoubleLabel.text = $"Claim x{m}";
            ui.winDoubleAmount.text = (_winReward * m).ToString();
            if (_pointerRun) audioManager.Play(art.click, .25f, 1f + segment * .08f);
        }

        /// <summary>Rays turn, the glow breathes, sparkles twinkle round the coin bubble while the screen is open.</summary>
        IEnumerator WinAmbient()
        {
            var rays = ui.winRays.rectTransform; var glow = ui.winGlow.rectTransform;
            float t = 0f;
            while (ui.winPopup.gameObject.activeSelf)
            {
                t += Time.deltaTime;
                rays.localRotation = Quaternion.Euler(0f, 0f, -t * 14f);
                float b = 1f + Mathf.Sin(t * 2.4f) * .05f;
                glow.localScale = new Vector3(b, b, 1f);
                ui.winBubble.localScale = Vector3.one * (1f + Mathf.Sin(t * 2f) * .015f);
                for (int i = 0; i < ui.winSparkles.Length; i++)
                {
                    float ph = (t * .8f + i * .17f) % 1f;
                    float a = ph < .5f ? Mathf.Sin(ph * 2f * Mathf.PI) : 0f;
                    var rt = ui.winSparkles[i].rectTransform;
                    rt.localScale = Vector3.one * (.5f + a * .6f);
                    rt.localRotation = Quaternion.Euler(0f, 0f, t * 70f + i * 40f);
                    GrillView.SetAlpha(ui.winSparkles[i], a);
                }
                yield return null;
            }
        }

        int _winReward;

        void CollectWin(int mult, Transform from)
        {
            if (!_won || _claiming) return;
            _claiming = true;
            _pointerRun = false;
            ui.winPopup.interactable = false;
            Click();
            int reward = _winReward * mult;
            int before = _shownCoins;
            SaveData.Coins += reward;
            _shownCoins = SaveData.Coins;
            ui.StartCoroutine(ClaimAndGo(from, before, SaveData.Coins));
        }

        /// <summary>Coins burst out of the pressed button and fly into the coin pill, which counts up; then the next level.</summary>
        IEnumerator ClaimAndGo(Transform from, int coinsFrom, int coinsTo)
        {
            if (!_autoPlay)
            {
                var coins = ui.winFlyCoins;
                var target = ui.winCoinPillIcon;
                int landed = 0;
                StartCoroutine(Tween.Bounce(from, .12f, .2f));
                for (int i = 0; i < coins.Length; i++) ui.StartCoroutine(FlyCoin(coins[i], from.position, target, i * .045f, () =>
                {
                    landed++;
                    ui.winCoinPillText.text = Mathf.RoundToInt(Mathf.Lerp(coinsFrom, coinsTo, landed / (float)coins.Length)).ToString("N0");
                    audioManager.Play(art.star, .45f, 1.1f + landed * .03f);
                    ui.StartCoroutine(Tween.Bounce(target, .18f, .15f));
                }));
                audioManager.Play(art.coin, .8f);
                while (landed < coins.Length) yield return null;
                yield return new WaitForSeconds(.35f);
            }
            ui.coinText.text = coinsTo.ToString("N0");
            ClosePopup(ui.winPopup);
            int next = _levelNumber + 1;
            if (DemoOn && next > demoBank.levels.Count)
            {
                SaveData.Level = 1;
                StartCoroutine(Tween.Delay(.3f, () =>
                {
                    LoadLevel(1);
                    ShowMessage("Demo complete!", "That's all 5 tiers: Easy, Medium, Hard, Very Hard and Super Hard.\nThanks for playing!");
                }));
                yield break;
            }
            if (limitToTenLevels && !useTestLevel && next > 10)
            {
                SaveData.Level = 1;
                StartCoroutine(Tween.Delay(.3f, () =>
                {
                    LoadLevel(1);
                    ShowMessage("Demo complete!", "Thanks for playing the first 10 levels.\nThe full game has many more.");
                }));
                yield break;
            }
            if (!useTestLevel) SaveData.Level = next;
            if (UseHome(next) && !_autoPlay) { ui.StartCoroutine(HomeBetween(next)); yield break; }
            if (loadingBetweenLevels && ui.loading != null && !_autoPlay) ui.StartCoroutine(LoadingBetween(next));
            else StartCoroutine(Tween.Delay(.3f, () => LoadLevel(next)));
        }

        IEnumerator FlyCoin(RectTransform c, Vector3 from, RectTransform target, float delay, System.Action landed)
        {
            yield return new WaitForSeconds(delay);
            c.gameObject.SetActive(true);
            var rnd = (Vector3)(Random.insideUnitCircle * 120f) * c.lossyScale.x;
            var burst = from + rnd + new Vector3(0f, 60f, 0f) * c.lossyScale.x;
            c.position = from; c.localScale = Vector3.one * .6f;
            yield return Tween.Run(.18f, k => { c.position = Vector3.Lerp(from, burst, Ease.OutCubic(k)); c.localScale = Vector3.one * Mathf.Lerp(.6f, 1.05f, k); });
            var p0 = c.position; var p2 = target.position;
            var p1 = (p0 + p2) * .5f + new Vector3(-160f, 0f, 0f) * c.lossyScale.x;
            yield return Tween.Run(.5f, k =>
            {
                float u = 1f - k;
                c.position = u * u * p0 + 2f * u * k * p1 + k * k * p2;
                c.localScale = Vector3.one * Mathf.Lerp(1.05f, .7f, k);
            }, Ease.InOutCubic);
            c.gameObject.SetActive(false);
            landed();
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
            if (_busy || _won || _dragging) return;
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
            if (i == 1 && _hintSearching) StartCoroutine(Tween.Bounce(ui.boosters[i].transform, .15f, .3f));   // Hint charges itself when its move shows
            if (used)
            {
                SaveData.SetBooster(i, SaveData.Booster(i) - 1);
                RefreshBoosters();
                StartCoroutine(Tween.Bounce(ui.boosters[i].transform, .15f, .3f));
            }
        }

        static string BoosterName(int i) => i switch { 0 => "Undo", 1 => "Hint", 2 => "Shuffle", _ => "Extra Tray" };

        bool DoUndo()
        {
            if (_history.Count == 0) { Toast("Nothing to undo"); return false; }
            StopHint();
            Deselect();
            var (b, mv) = _history.Pop();
            _board = b; _moves = mv;
            SyncVisFromBoard();
            RedrawAll(true);
            CheckStuck(false);   // back to a winnable board clears the warning; still dead = warn again
            return true;
        }

        bool _hintSearching;

        /// <summary>
        /// Hint booster: the search runs on a worker thread (it can take ~0.5 s on hard boards) and the booster is only
        /// charged when a move is shown. Only a PROVEN dead end opens the stuck popup; a search that merely ran out of
        /// budget says so instead (it used to open "Out of Moves!" on winnable boards, user 2026-11-23).
        /// Returns false: UseBooster must not charge it; HintWhenReady does.
        /// </summary>
        // ---- hints and autoplay follow one proven solution (2026-11-26: on the bigger demo boards the quick NextMove
        // search could flip a move back and forth). The plan maps each board on the solution path to its next move;
        // when the player leaves the path, a fresh full solve from there extends the plan.
        readonly Dictionary<string, Move> _plan = new Dictionary<string, Move>();
        static string PlanKey(Board b) => b.ToString() + "|" + b.QueueNext;

        static Dictionary<string, Move> BuildPlan(Board start, MoveRule rule)
        {
            var map = new Dictionary<string, Move>();
            var r = Solver.Solve(start.Clone(), rule, 150000, 700000);
            if (!r.Solvable) return map;
            var b = start.Clone();
            foreach (var m in r.Moves)
            {
                map[PlanKey(b)] = m;
                if (!b.TryMakeMove(rule, m.From, m.Index, m.To, out var mm)) break;
                b.Apply(mm);
            }
            return map;
        }

        /// <summary>The next move of a proven solution from board <paramref name="b"/> (thread-safe).</summary>
        Move? SmartNext(Board b, MoveRule rule, out bool dead)
        {
            dead = false;
            string k = PlanKey(b);
            lock (_plan) if (_plan.TryGetValue(k, out var m)) return m;
            var fresh = BuildPlan(b, rule);
            if (fresh.TryGetValue(k, out var m2))
            {
                lock (_plan) foreach (var kv in fresh) _plan[kv.Key] = kv.Value;
                return m2;
            }
            return Solver.NextMove(b, rule, out dead);
        }

        void StartPlan()
        {
            lock (_plan) _plan.Clear();
            var snap = _board.Clone(); var rule = moveRule;
            System.Threading.Tasks.Task.Run(() =>
            {
                var map = BuildPlan(snap, rule);
                lock (_plan) foreach (var kv in map) _plan[kv.Key] = kv.Value;
            });
        }

        bool DoHint()
        {
            if (_hintSearching) return false;
            Deselect();
            StopHint();
            _hintSearching = true;
            var snapshot = _board.Clone();
            var rule = moveRule;
            var task = System.Threading.Tasks.Task.Run(() =>
            {
                var m = SmartNext(snapshot, rule, out bool dead);
                return (m, dead);
            });
            StartCoroutine(HintWhenReady(task, snapshot.ToString() + "|" + snapshot.QueueNext, _levelNumber));
            return false;
        }

        /// <summary>The free idle hint (levels up to 10): searched on a worker thread too, shown only if nothing changed.</summary>
        IEnumerator IdleHint(float duration)
        {
            _hintSearching = true;
            var snapshot = _board.Clone();
            string key = snapshot.ToString() + "|" + snapshot.QueueNext;
            int level = _levelNumber;
            var rule = moveRule;
            var task = System.Threading.Tasks.Task.Run(() => SmartNext(snapshot, rule, out _));
            while (!task.IsCompleted) yield return null;
            _hintSearching = false;
            if (task.IsFaulted || !task.Result.HasValue) yield break;
            if (level != _levelNumber || _won || _busy || _dragging || _selected >= 0 || _hintCo != null || AnyPopupOpen()) yield break;
            if (key != _board.ToString() + "|" + _board.QueueNext) yield break;
            _hintCo = StartCoroutine(HintAnim(task.Result.Value, duration));
        }

        IEnumerator HintWhenReady(System.Threading.Tasks.Task<(Move? m, bool dead)> task, string boardKey, int level)
        {
            while (!task.IsCompleted) yield return null;
            _hintSearching = false;
            if (task.IsFaulted) { Debug.LogException(task.Exception); yield break; }
            // the player moved / undid / changed level while it searched: that hint is for another board
            if (level != _levelNumber || _won || boardKey != _board.ToString() + "|" + _board.QueueNext) yield break;
            while (_busy || _dragging) yield return null;
            var (m, dead) = task.Result;
            if (m.HasValue)
            {
                SaveData.SetBooster(1, SaveData.Booster(1) - 1);
                RefreshBoosters();
                StopHint();
                _hintCo = StartCoroutine(HintAnim(m.Value));
            }
            else if (dead) ShowStuck();
            else Toast("No hint found here. Try Undo!");
        }

        bool DoShuffle()
        {
            Deselect();
            StopHint();
            var counts = new int[_board.SkewerCount];
            var pieces = new List<int>();
            // locked trays keep their food; everything else is shuffled (and uncovered: a shuffle shows all wrapped food)
            for (int s = 0; s < _board.SkewerCount; s++) { counts[s] = _board.IsLocked(s) ? 0 : _board.Count(s); if (!_board.IsLocked(s)) pieces.AddRange(_board.Stack(s)); }
            if (pieces.Count == 0) return false;
            var rng = new Mulberry32((uint)(System.DateTime.Now.Ticks & 0xffffffff));
            Board best = null;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                rng.Shuffle(pieces);
                var nb = new Board(_board.Capacity, _board.Pieces);
                for (int qq = _board.QueueNext; qq < _board.QueueCount; qq++) nb.AddQueued(_board.QueueStack(qq), _board.QueueTarget(qq), _board.QueueHidden(qq), _board.QueueLock(qq));
                int p = 0; bool bad = false;
                for (int s = 0; s < counts.Length; s++)
                {
                    if (_board.IsLocked(s)) nb.AddSkewer(_board.Stack(s), _board.Target(s), _board.HiddenStack(s), _board.Lock(s));
                    else nb.AddSkewer(pieces.GetRange(p, counts[s]), _board.Target(s)); // trays keep their customers
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
            SyncVisFromBoard();
            RedrawAll(true);
            CheckStuck(false);
            return true;
        }

        // ------------------------------------------------------------------ stuck detection (user 2026-11-22, 2026-11-23)
        // After every move the solver checks in the background whether the board can still be won. The check is exact
        // (audited 2026-11-23 on all 50 baked levels: 4139 positions, never a false alarm, never a miss), but in ~80% of
        // dead ends food can STILL be moved, even onto a tray that then gets served, so popping "Out of Moves!" right
        // away was confusing (user 2026-11-23). Now:
        //  - nothing can move at all -> the "You're Stuck!" popup at once;
        //  - food can still move but the orders can't all be filled -> a warning toast + the Undo booster pulses; the
        //    popup only comes if the player makes StuckGraceMoves more moves without getting out of it.
        // The popup never opens mid-drag / mid-move.
        const int StuckPriceFor = 150;   // coins for the rescue (the client's games ask 190)
        const int StuckGraceMoves = 2;
        bool _stuckShown;
        int _stuckCheckId;
        int _deadMoves = -1;   // -1 = the board can be won; 0.. = moves made since the dead end was first spotted

        /// <param name="playerMove">false after Undo / a booster / the rescue: re-checks the board without counting it as
        /// one of the grace moves.</param>
        void CheckStuck(bool playerMove = true)
        {
            if (_won || _autoPlay) return;
            int id = ++_stuckCheckId;
            var snapshot = _board.Clone();
            var rule = moveRule;
            var task = System.Threading.Tasks.Task.Run(() => Solver.IsStuck(snapshot, rule));
            StartCoroutine(WaitStuck(task, id, playerMove));
        }

        IEnumerator WaitStuck(System.Threading.Tasks.Task<bool> task, int id, bool playerMove)
        {
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted || id != _stuckCheckId) yield break;   // a newer move superseded it
            if (!task.Result) { _deadMoves = -1; yield break; }       // still winnable (e.g. after Undo)
            yield return new WaitForSeconds(.45f);
            while (_busy || _dragging) yield return null;
            if (_won || id != _stuckCheckId || AnyPopupOpen()) yield break;
            bool firstSeen = _deadMoves < 0;
            if (firstSeen) _deadMoves = 0;
            else if (playerMove) _deadMoves++;
            if (_board.HasAnyMove(moveRule) && _deadMoves < StuckGraceMoves)
            {
                WarnDead(firstSeen);
                yield break;
            }
            ShowStuck();
        }

        /// <summary>Food can still move, but the orders can't all be filled any more: say so and point at Undo.</summary>
        void WarnDead(bool first)
        {
            Toast(first ? "These orders can't all be filled now. Try Undo!" : "Still stuck. Tap Undo to go back!", 3.5f);
            audioManager.Play(art.invalid, .5f, .9f);
            if (ui.boosters.Length > 0) StartCoroutine(PulseUndo());
        }

        IEnumerator PulseUndo()
        {
            var t = ui.boosters[0].transform;   // booster 0 = Undo
            for (int i = 0; i < 3; i++)
            {
                yield return Tween.Bounce(t, .18f, .3f);
                yield return new WaitForSeconds(.15f);
            }
        }

        void ShowStuck()
        {
            _stuckShown = true;
            StopHint();
            bool tray = !_extraUsed && _board.SkewerCount < ui.grills.Length;
            ui.stuckIcon.sprite = tray ? art.iconAddTray : art.iconShuffle;
            ui.stuckCaption.text = tray ? "Add 1 Tray and Continue" : "Shuffle the Grill and Continue";
            ui.stuckPrice.text = StuckPriceFor.ToString();
            if (ui.stuckReason != null)
                ui.stuckReason.text = _board.HasAnyMove(moveRule)
                    ? "You can still move food, but these orders can't all be filled anymore."
                    : "No food can be moved anywhere.";
            ShowStuckPage(true);
            AllWorried();
            audioManager.Play(art.fail, .8f);
            OpenPopup(ui.stuckPopup, ui.stuckPanel);
        }

        void ShowStuckPage(bool offer)
        {
            ui.stuckOffer.gameObject.SetActive(offer);
            ui.stuckGiveUp.gameObject.SetActive(!offer);
            ui.stuckTitle.text = offer ? "You're Stuck!" : "Give Up?";
            if (ui.stuckReason != null) ui.stuckReason.gameObject.SetActive(offer);
            var rt = offer ? ui.stuckOffer : ui.stuckGiveUp;
            rt.localScale = Vector3.one * .9f;
            ui.StartCoroutine(Tween.Scale(rt, Vector3.one, .2f, k => Ease.OutBack(k)));
        }

        /// <summary>The rescue: an extra tray while one is free, otherwise a shuffle (guaranteed solvable).</summary>
        void Rescue()
        {
            _stuckShown = false;
            if (!_extraUsed && _board.SkewerCount < ui.grills.Length) AddExtraSkewer();
            else if (!DoShuffle()) { StuckLater(); return; }
            CheckStuck(false);
        }

        void StuckLater() => StartCoroutine(Tween.Delay(.4f, () => { if (!_won && !AnyPopupOpen()) ShowStuck(); }));

        void Shake(Transform t) => StartCoroutine(Tween.Shake((RectTransform)t, 14f, .3f));

        bool AddExtraSkewer()
        {
            if (_extraUsed || _board.SkewerCount >= ui.grills.Length) { Toast("Only one extra tray per level"); return false; }
            Deselect();
            _extraUsed = true;
            int g = _board.AddSkewer();
            _vis.Add(new VisTray { stack = new int[0], target = -1, face = -1, hid = new bool[0] });
            var v = ui.grills[g];
            v.gameObject.SetActive(true);
            v.ResetFx(); v.helper = true; v.SetLit(false, art); v.Show(_board.Stack(g), art, cookOnComplete); v.SetFoils(_board.HiddenStack(g)); v.SetLock(-1, art);
            ui.layout.activeGrills = _board.SkewerCount;
            ui.layout.Apply();
            v.rect.localScale = Vector3.zero;
            StartCoroutine(Tween.Scale(v.rect, Vector3.one, .4f, t => Ease.OutBack(t)));
            audioManager.Play(art.pop, .8f, .9f);
            CheckStuck(false);   // the extra space may open a way out
            return true;
        }

        void RedrawAll(bool pop)
        {
            for (int i = 0; i < _board.SkewerCount; i++)
            {
                var v = ui.grills[i];
                v.Show(_board.Stack(i), art, cookOnComplete); v.SetFoils(_board.HiddenStack(i)); v.SetLock(_board.Lock(i), art);
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
        IEnumerator HintAnim(Move m, float duration = 3.6f)
        {
            var a = ui.grills[m.From]; var b = ui.grills[m.To];
            if (_handCo != null) StopCoroutine(_handCo);
            _handCo = StartCoroutine(HandLoop(m));
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float p = (Mathf.Sin(t * 6f) + 1f) * .5f;
                GrillView.SetAlpha(a.highlight, .2f + p * .5f);
                GrillView.SetAlpha(b.highlight, .2f + (1f - p) * .5f);
                for (int i = m.Index; i < _board.Count(m.From); i++)
                    a.slots[i].anchoredPosition = new Vector2(0f, GrillView.SlotY[i] - p * 22f);
                yield return null;
            }
            StopAllHandFx();
            ClearHintVisuals(m);
            _hintCo = null;
        }

        // ------------------------------------------------------------------ tutorial hand
        // The glove shows the move: it presses on the piece to grab, drags to the tray, releases, and repeats.
        Vector2 HandPoint(RectTransform target) => ui.handLayer.InverseTransformPoint(target.position);

        // Like the client's FishUp! tutorial: the soft 3D glove rests on the piece and presses it (two little bobs),
        // then, because our move is a drag, it carries a see-through copy of the piece to the tray and lets go.
        /// <summary>Tutorial glove size in canvas units (about one food piece wide plus the cuff, like the reference).</summary>
        public const float HandSize = 250f;   // about as big as the piece it taps, like the reference
        /// <summary>
        /// Glove facing: +1 = finger up-left (body to the lower right of the fingertip, like the reference), -1 = mirrored.
        /// Re-checked every frame from where the hand IS: it turns around (smoothly) only while its body would leave the
        /// right edge of the canvas, and turns back when there is room again, so it is never cut off on either side.
        /// </summary>
        float _handSx = 1f;
        void HandScale(float k)
        {
            float halfW2 = ui.handLayer.rect.width * .5f;
            float bodyRight = HandSize * (1f - ui.hand.pivot.x) - 6f;   // how far the glove reaches right of its fingertip
            float target = ui.hand.anchoredPosition.x + bodyRight > halfW2 - 6f ? -1f : 1f;
            _handSx = Mathf.MoveTowards(_handSx, target, Time.deltaTime * 14f);   // a quick turn, not a slow squash
            float sx = Mathf.Abs(_handSx) < .15f ? .15f * Mathf.Sign(target) : _handSx;   // never collapse to a line
            ui.hand.localScale = new Vector3(sx * k, k, 1f);
        }

        IEnumerator HandLoop(Move m)
        {
            var hand = ui.hand; var g = ui.handGroup; var ghost = ui.handGhost; var grt = ghost.rectTransform;
            hand.gameObject.SetActive(true);
            g.alpha = 0f;
            float halfW = ui.handLayer.rect.width * .5f;
            while (true)
            {
                var from = ui.grills[m.From]; var to = ui.grills[m.To];
                Vector2 p0 = HandPoint(from.slots[m.Index]);
                int land = Mathf.Min(_board.Count(m.To), GrillView.Capacity - 1);
                Vector2 p1 = HandPoint(to.slots[land]);
                var fa = art.foods[_board.Get(m.From, m.Index)];
                ghost.sprite = cookOnComplete && fa.raw != null ? fa.raw : fa.grilled;
                float gs = from.slots[m.Index].lossyScale.x / ui.handLayer.lossyScale.x;
                float gw = fa.width * GrillView.FoodScale;
                grt.sizeDelta = new Vector2(gw, gw);
                // come in from the side the glove's body is on (it faces away from the nearest edge)
                float dir = p0.x + HandSize * (1f - ui.hand.pivot.x) > halfW - 12f ? -1f : 1f;
                _handSx = dir;
                var rest = p0 + new Vector2(dir * 80f, -80f);

                // come in onto the piece
                hand.anchoredPosition = rest; HandScale(1f);
                yield return Tween.Run(.3f, k => { g.alpha = k; hand.anchoredPosition = Vector2.Lerp(rest, p0, Ease.OutCubic(k)); });
                // press, press (like a tap)
                for (int i = 0; i < 2; i++)
                {
                    yield return Tween.Run(.14f, k => HandScale(Mathf.Lerp(1f, .86f, k)));
                    if (i == 1) StartCoroutine(HandRing(p0));
                    yield return Tween.Run(.14f, k => HandScale(Mathf.Lerp(.86f, i == 1 ? .9f : 1f, k)));
                }
                // pick up: the ghost lifts out of the slot
                grt.anchoredPosition = p0; grt.localScale = Vector3.one * gs;
                yield return Tween.Run(.15f, k => { GrillView.SetAlpha(ghost, k * .8f); grt.localScale = Vector3.one * gs * Mathf.Lerp(1f, 1.12f, k); });
                // drag to the tray
                yield return Tween.Run(.8f, k =>
                {
                    float e = Ease.InOutCubic(k);
                    var p = Vector2.Lerp(p0, p1, e) + new Vector2(0f, Mathf.Sin(e * Mathf.PI) * 70f);
                    hand.anchoredPosition = p; grt.anchoredPosition = p;
                    HandScale(.9f);   // keeps checking the edge while it moves (turns around near the right edge)
                });
                // let go
                StartCoroutine(HandRing(p1));
                yield return Tween.Run(.15f, k => { HandScale(Mathf.Lerp(.9f, 1f, k)); grt.localScale = Vector3.one * gs * Mathf.Lerp(1.12f, 1f, k); });
                yield return new WaitForSeconds(.3f);
                yield return Tween.Run(.25f, k => { g.alpha = 1f - k; GrillView.SetAlpha(ghost, (1f - k) * .8f); });
                yield return new WaitForSeconds(.35f);
            }
        }

        IEnumerator HandRing(Vector2 at)
        {
            var r = ui.handRing; var rt = r.rectTransform;
            rt.anchoredPosition = at;
            yield return Tween.Run(.4f, k =>
            {
                rt.localScale = Vector3.one * Mathf.Lerp(.3f, 1.3f, k);
                GrillView.SetAlpha(r, (1f - k) * .8f);
            });
            GrillView.SetAlpha(r, 0f);
        }

        Coroutine _handCo;

        /// <summary>Stops the hand loop (it runs as its own coroutine next to the hint) and hides the hand.</summary>
        void StopAllHandFx()
        {
            if (_handCo != null) { StopCoroutine(_handCo); _handCo = null; }
            HideHand();
        }

        void HideHand()
        {
            ui.hand.gameObject.SetActive(false);
            GrillView.SetAlpha(ui.handRing, 0f);
            GrillView.SetAlpha(ui.handGhost, 0f);
        }

        void StopHint()
        {
            if (_hintCo == null) return;
            StopCoroutine(_hintCo);
            _hintCo = null;
            StopAllHandFx();
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
            "Drag food to fill each tray!",
            "Drop food onto the same food!",
            "Fill a tray to serve the customer!",
            "Serve orders to bring in new ones!",
        };

        // tips (levels 1-3): shown once per level ever, in the message strip above the boosters, and gone after the
        // first move (or a few seconds) so they never sit on top of play
        /// <summary>The tier (demo) or "HARD" tag under the level pill: red on Hard levels, green otherwise (Sort Streak: Hard every 5th level).</summary>
        void ShowTierTag()
        {
            if (ui.tierTag == null) return;
            string tag = !string.IsNullOrEmpty(_level.tier) && DemoOn ? _level.tier : _level.hard ? "Hard" : "";
            ui.tierTag.gameObject.SetActive(!string.IsNullOrEmpty(tag));
            if (string.IsNullOrEmpty(tag)) return;
            ui.tierTagText.text = tag.ToUpperInvariant();
            ui.tierTag.color = _level.hard ? new Color32(215, 38, 61, 255) : new Color32(46, 160, 87, 255);
            float w = Mathf.Max(120f, ui.tierTagText.GetPreferredValues(ui.tierTagText.text).x + 40f);
            ui.tierTag.rectTransform.sizeDelta = new Vector2(w, ui.tierTag.rectTransform.sizeDelta.y);
            ui.tierTagText.rectTransform.sizeDelta = new Vector2(w, ui.tierTagText.rectTransform.sizeDelta.y);
        }

        /// <summary>Opening line for a demo tier / a Hard level (honest wording, no made-up player stats).</summary>
        string SpecialTip()
        {
            if (DemoOn)
                switch (_level.tier)
                {
                    case "Easy": return "Easy: drag food to fill each customer's tray!";
                    case "Medium": return "Wrapped food shows what it is when it reaches the bottom!";
                    case "Hard": return "Hard level! Think before every move. Coins x2!";
                    case "Very Hard": return "Very Hard! The locked tray opens when you serve the food on its sign. Coins x2!";
                    case "Super Hard": return "Super Hard! Trays of 6 and look-alike food. Good luck! Coins x2!";
                }
            return _level.hard ? "Hard level! Think before every move. Coins x2!" : null;
        }

        void ShowTip()
        {
            if (_tipCo != null) StopCoroutine(_tipCo);
            ui.tip.alpha = 0f;
            string special = SpecialTip();
            if (special != null)
            {
                ui.tipText.text = special;
                _tipCo = StartCoroutine(TipAnim());
                return;
            }
            if (_levelNumber > Tips.Length) return;
            string key = "tip_seen_" + _levelNumber;
            if (PlayerPrefs.GetInt(key, 0) == 1 && !useTestLevel) return;
            PlayerPrefs.SetInt(key, 1);
            ui.tipText.text = Tips[_levelNumber - 1];
            _tipCo = StartCoroutine(TipAnim());
        }

        IEnumerator TipAnim()
        {
            while (Covered) yield return null;
            yield return new WaitForSeconds(.6f);
            var rt = (RectTransform)ui.tip.transform;
            rt.localScale = Vector3.one * .7f;
            StartCoroutine(Tween.Scale(rt, Vector3.one, .35f, t => Ease.OutBack(t)));
            yield return Tween.Fade(ui.tip, 1f, .3f);
            if (_levelNumber <= tutorialLevels)
            {
                while (!_won) yield return null;   // tutorial levels: stays up the whole level
                yield return Tween.Fade(ui.tip, 0f, .3f);
                _tipCo = null;
                yield break;
            }
            yield return new WaitForSeconds(6f);
            yield return Tween.Fade(ui.tip, 0f, .4f);
            _tipCo = null;
        }

        void HideTip()
        {
            if (_tipCo != null) { StopCoroutine(_tipCo); _tipCo = null; }
            if (ui.tip.alpha > 0f) StartCoroutine(Tween.Fade(ui.tip, 0f, .2f));
        }

        public void Toast(string msg) => Toast(msg, 1.3f);

        /// <summary>A message in the strip above the boosters, held for <paramref name="hold"/> seconds.</summary>
        public void Toast(string msg, float hold)
        {
            // one message at a time in the strip: a tutorial banner steps aside for the toast and comes back after it
            if (_levelNumber <= tutorialLevels && _tipCo != null) StartCoroutine(TipAside());
            else HideTip();
            if (_toastCo != null) ui.StopCoroutine(_toastCo);
            ui.toastText.text = msg;
            _toastCo = ui.StartCoroutine(ToastAnim(hold));
        }

        IEnumerator TipAside()
        {
            ui.tip.alpha = 0f;
            yield return new WaitForSeconds(1.75f);
            while (_toastCo != null) yield return null;
            if (!_won && _tipCo != null) yield return Tween.Fade(ui.tip, 1f, .25f);
        }

        IEnumerator ToastAnim(float hold)
        {
            var rt = (RectTransform)ui.toast.transform;
            rt.localScale = Vector3.one * .8f;
            ui.StartCoroutine(Tween.Scale(rt, Vector3.one, .25f, t => Ease.OutBack(t)));
            yield return Tween.Fade(ui.toast, 1f, .15f);
            yield return new WaitForSeconds(hold);
            yield return Tween.Fade(ui.toast, 0f, .25f);
            _toastCo = null;
        }

        // ------------------------------------------------------------------ popups
        bool AnyPopupOpen() => IsOpen(ui.winPopup) || IsOpen(ui.pausePopup) || IsOpen(ui.stuckPopup) || IsOpen(ui.messagePopup) || MetaPopupOpen();
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

        float _lastSliderTick;

        void RefreshPauseTexts()
        {
            ui.musicValue.text = $"{Mathf.RoundToInt(ui.musicSlider.value * 100)}%";
            ui.sfxValue.text = $"{Mathf.RoundToInt(ui.sfxSlider.value * 100)}%";
            ui.pauseHapticsText.text = SaveData.Haptic ? "ON" : "OFF";
            ui.pauseHapticsImage.sprite = SaveData.Haptic ? ui.hapticsOnSprite : ui.hapticsOffSprite;
            // Restart only makes sense in a level. On Home it's hidden and Continue takes the whole row, the same width as
            // Restore Purchases (a lone half-width button on the right looked odd, user 2026-11-23).
            bool restart = !_homeUp;
            ui.pauseRestart.gameObject.SetActive(restart);
            var restore = (RectTransform)ui.pauseRestore.transform;
            var cont = (RectTransform)ui.pauseResume.transform;
            float full = restore.sizeDelta.x, half = (full - 24f) / 2f;
            cont.sizeDelta = new Vector2(restart ? half : full, cont.sizeDelta.y);
            cont.anchoredPosition = new Vector2(restore.anchoredPosition.x + (restart ? half / 2f + 12f : 0f), cont.anchoredPosition.y);
        }

        AudioClip CurrentTrack()
        {
            if (art.musicTracks == null || art.musicTracks.Length == 0) return art.music;
            var c = art.musicTracks[0];   // one track now (the per-track pick was only for choosing)
            return c != null ? c : art.music;
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
            if (_homeUp) HideHomeNow();
            for (int n = 0; n < levels; n++)
            {
                int lvl = _levelNumber;
                while (_busy) yield return null;
                int steps = 0;
                while (!_won && steps < 400)
                {
                    while (_busy || AnyPopupOpen()) { if (_won) break; yield return null; }
                    if (_won) break;
                    var mv = SmartNext(_board, moveRule, out _);
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
