using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace GrillSort
{
    /// <summary>
    /// Meta layer (2026-11-23, review: "no reason to come back tomorrow"): a Home screen between levels showing YOUR stall,
    /// decorations bought with coins that appear on it, a 7-day daily reward and the progress to the next stall (theme).
    /// All of it is authored by the builder; runtime only toggles, re-skins and tweens.
    /// Flow: loading -> Home -> Play -> level -> Well Done -> Claim -> Home (level N+1 already set up behind it).
    /// Levels before <see cref="homeFromLevel"/> skip the home screen so new players go straight into play.
    /// </summary>
    public sealed partial class GameController
    {
        [Header("Home screen (meta)")]
        [Tooltip("Show the home screen (stall + decorations + daily reward) at start and between levels")]
        public bool homeScreen = true;
        [Tooltip("First level that comes with the home screen (earlier levels go straight from level to level)")]
        public int homeFromLevel = 3;

        /// <summary>Stall decorations, in the order of GameUi.homeDecor / the Decorate list.</summary>
        public static readonly string[] DecorNames = { "String Lights", "Tropical Plant", "Menu Board", "Tiki Torch", "BBQ Smoker", "Neon Sign" };
        public static readonly int[] DecorCost = { 100, 150, 200, 250, 350, 500 };

        /// <summary>Daily rewards, day 1..7: coins, or a booster (index into the booster bar) x count.</summary>
        public static readonly (int coins, int booster, int count)[] DailyRewards =
        {
            (50, -1, 0), (80, -1, 0), (0, 1, 2), (120, -1, 0), (0, 3, 1), (200, -1, 0), (400, -1, 0),
        };
        public static string DailyLabel(int i)
        {
            var r = DailyRewards[i];
            return r.booster < 0 ? r.coins.ToString() : $"{BoosterName(r.booster)} x{r.count}";
        }

        /// <summary>Short label for a calendar tile (every tile is the same width, so "Extra Tray x1" becomes "Tray x1").</summary>
        public static string DailyTileLabel(int i) => DailyLabel(i).Replace("Extra ", "");

        bool _homeUp;
        Coroutine _homeIdle;
        int _homeLevel;

        CanvasGroup[] MetaPopups => ui.dailyPopup != null ? new[] { ui.dailyPopup, ui.decorPopup } : new CanvasGroup[0];
        bool MetaPopupOpen() { foreach (var p in MetaPopups) if (IsOpen(p)) return true; return false; }

        bool UseHome(int level) => homeScreen && !tierDemo && ui.home != null && !useTestLevel && level >= homeFromLevel;

        void InitMeta()
        {
            if (ui.home == null) return;
            ui.homePlay.onClick.AddListener(() => { if (!_homeUp || MetaPopupOpen()) return; Click(); ui.StartCoroutine(PlayFromHome()); });
            ui.homeSettings.onClick.AddListener(() => { Click(); OpenPopup(ui.pausePopup, ui.pausePanel); RefreshPauseTexts(); });
            ui.homeDaily.onClick.AddListener(() => { Click(); OpenDaily(); });
            ui.homeDecorate.onClick.AddListener(() => { Click(); OpenDecor(); });
            ui.dailyClose.onClick.AddListener(() => { Click(); ClosePopup(ui.dailyPopup); });
            ui.dailyClaim.onClick.AddListener(ClaimDaily);
            ui.decorClose.onClick.AddListener(() => { Click(); ClosePopup(ui.decorPopup); });
            for (int i = 0; i < ui.decorBuy.Length; i++) { int k = i; ui.decorBuy[i].onClick.AddListener(() => BuyDecor(k)); }
        }

        // ------------------------------------------------------------------ show / hide
        void ShowHomeNow()
        {
            _homeUp = true;
            RefreshHome(SaveData.Level);
            var h = ui.home;
            h.gameObject.SetActive(true); h.alpha = 1f; h.blocksRaycasts = true; h.interactable = true;
            StartHomeIdle();
            ui.StartCoroutine(DailyNudge());
        }

        void HideHomeNow()
        {
            _homeUp = false;
            if (ui.home == null) return;
            if (_homeIdle != null) { ui.StopCoroutine(_homeIdle); _homeIdle = null; }
            ui.home.alpha = 0f; ui.home.blocksRaycasts = false;
            ui.home.gameObject.SetActive(false);
        }

        /// <summary>After "Claim": the home screen fades in over the win screen, the next level is set up behind it.</summary>
        IEnumerator HomeBetween(int next)
        {
            var h = ui.home;
            _homeUp = true;
            RefreshHome(next);
            ui.homeCoinText.text = SaveData.Coins.ToString("N0");
            h.gameObject.SetActive(true); h.alpha = 0f; h.blocksRaycasts = true; h.interactable = true;
            audioManager.SetAmbience(art.sizzleLoop, false);
            yield return Tween.Fade(h, 1f, .35f);
            HidePopupNow(ui.winPopup);
            LoadLevel(next);   // behind the home screen; the intro waits for Covered
            StartHomeIdle();
            // a little welcome back: the decorate button bounces when something new can be built
            if (CanAffordDecor()) ui.StartCoroutine(Tween.Bounce(ui.homeDecorate.transform, .2f, .4f));
            ui.StartCoroutine(DailyNudge());
        }

        IEnumerator PlayFromHome()
        {
            ui.home.blocksRaycasts = false;
            ui.StartCoroutine(Tween.Bounce(ui.homePlay.transform, .12f, .2f));
            yield return new WaitForSeconds(.12f);
            _homeUp = false;   // the level intro starts under the fade
            audioManager.SetAmbience(art.sizzleLoop, true);
            yield return Tween.Fade(ui.home, 0f, .3f);
            if (_homeIdle != null) { ui.StopCoroutine(_homeIdle); _homeIdle = null; }
            ui.home.gameObject.SetActive(false);
        }

        /// <summary>A daily reward is waiting: the popup opens by itself once the home screen is showing.</summary>
        IEnumerator DailyNudge()
        {
            while (_loadingScreen) yield return null;
            yield return new WaitForSeconds(.7f);
            if (_homeUp && SaveData.DailyReady && !AnyPopupOpen()) OpenDaily();
        }

        void RefreshHome(int level)
        {
            _homeLevel = level;
            var theme = art.ThemeForLevel(level);
            ui.homeBg.sprite = theme.Back;
            ui.homeFront.sprite = theme.front;
            ui.homePlayText.text = $"Level {level}";
            ui.homeWorldName.text = theme.displayName;
            GameArt.ThemeDef nextTheme = null;
            foreach (var t in art.themes) if (t.unlockLevel > level && (nextTheme == null || t.unlockLevel < nextTheme.unlockLevel)) nextTheme = t;
            float p = 1f;
            if (nextTheme != null)
            {
                ui.homeWorldNext.text = $"Next stall: {nextTheme.displayName} at level {nextTheme.unlockLevel}";
                p = Mathf.Clamp01((level - theme.unlockLevel) / (float)Mathf.Max(1, nextTheme.unlockLevel - theme.unlockLevel));
            }
            else ui.homeWorldNext.text = "Every stall is open!";
            ui.homeWorldFill.gameObject.SetActive(p > .01f);
            float minW = ui.homeWorldFill.sizeDelta.y;
            ui.homeWorldFill.sizeDelta = new Vector2(Mathf.Lerp(minW, ui.homeWorldFillMax, p), ui.homeWorldFill.sizeDelta.y);
            ui.homeCoinText.text = SaveData.Coins.ToString("N0");
            int built = 0;
            for (int i = 0; i < ui.homeDecor.Length; i++)
            {
                // not built yet: a faint silhouette marks the spot (the player sees what the stall can become)
                bool on = SaveData.HasDecor(i);
                ui.homeDecor[i].gameObject.SetActive(true);
                ui.homeDecor[i].color = on ? Color.white : Silhouette;
                ui.homeDecor[i].rectTransform.localScale = Vector3.one;
                if (on) built++;
            }
            ui.homeDecorCount.text = $"{built}/{ui.homeDecor.Length}";
            for (int i = 0; i < ui.homeCustomers.Length; i++)
            {
                int f = (level * 3 + i * 5) % art.customers.Length;
                ui.homeCustomers[i].sprite = art.customers[f];
            }
            RefreshHomeDots();
        }

        bool CanAffordDecor()
        {
            for (int i = 0; i < DecorCost.Length; i++) if (!SaveData.HasDecor(i) && SaveData.Coins >= DecorCost[i]) return true;
            return false;
        }

        void RefreshHomeDots()
        {
            ui.homeDailyDot.gameObject.SetActive(SaveData.DailyReady);
            ui.homeDecorateDot.gameObject.SetActive(CanAffordDecor());
        }

        void StartHomeIdle()
        {
            if (_homeIdle != null) ui.StopCoroutine(_homeIdle);
            _homeIdle = ui.StartCoroutine(HomeIdle());
        }

        /// <summary>Home life: the mascot hops, the customers breathe and now and then one beams, sparkles twinkle, the buttons with news wiggle.</summary>
        IEnumerator HomeIdle()
        {
            var m = ui.homeMascot; var home = m.anchoredPosition;
            float t = 0f, nextSmile = 2f;
            int smiling = -1; float smileEnd = 0f;
            while (true)
            {
                t += Time.deltaTime;
                float hop = Mathf.Abs(Mathf.Sin(t * 3.2f));
                m.anchoredPosition = home + new Vector2(0f, hop * 26f);
                float sq = (1f - hop) * .07f;
                m.localScale = new Vector3(1f + sq, 1f - sq, 1f);
                m.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 3.2f) * 4f);
                for (int i = 0; i < ui.homeCustomers.Length; i++)
                {
                    float s = Mathf.Sin(t * 2.1f + i * 1.9f);
                    ui.homeCustomers[i].rectTransform.localScale = new Vector3(1f + s * .012f, 1f - s * .012f, 1f);
                }
                if (t > nextSmile)
                {
                    if (smiling >= 0) ui.homeCustomers[smiling].sprite = art.customers[(_homeLevel * 3 + smiling * 5) % art.customers.Length];
                    smiling = Random.Range(0, ui.homeCustomers.Length);
                    var happy = art.customersHappy != null && art.customersHappy.Length > 0 ? art.customersHappy : art.customers;
                    ui.homeCustomers[smiling].sprite = happy[(_homeLevel * 3 + smiling * 5) % happy.Length];
                    smileEnd = t + 1.2f; nextSmile = t + Random.Range(2.5f, 4.5f);
                }
                if (smiling >= 0 && t > smileEnd)
                {
                    ui.homeCustomers[smiling].sprite = art.customers[(_homeLevel * 3 + smiling * 5) % art.customers.Length];
                    smiling = -1;
                }
                for (int i = 0; i < ui.homeSparkles.Length; i++)
                {
                    var sp = ui.homeSparkles[i];
                    if (_sparkBurst) break;
                    float ph = (t * .55f + i * .23f) % 1f;
                    float a = ph < .4f ? Mathf.Sin(ph / .4f * Mathf.PI) : 0f;
                    sp.rectTransform.localScale = Vector3.one * (.4f + a * .7f);
                    sp.rectTransform.localRotation = Quaternion.Euler(0f, 0f, t * 50f + i * 30f);
                    GrillView.SetAlpha(sp, a * .9f);
                }
                if (ui.homeDecorateDot.gameObject.activeSelf)
                    ui.homeDecorate.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 10f) * 7f * Mathf.Max(0f, Mathf.Sin(t * 1.3f) - .6f) * 2.5f);
                else ui.homeDecorate.transform.localRotation = Quaternion.identity;
                yield return null;
            }
        }

        bool _sparkBurst;
        static readonly Color Silhouette = new Color(.16f, .08f, .02f, .2f);

        // ------------------------------------------------------------------ decorate
        void OpenDecor()
        {
            RefreshDecorList();
            OpenPopup(ui.decorPopup, ui.decorPanel);
        }

        static readonly Color32 DecorRowOpen = new Color32(246, 232, 204, 255), DecorRowBuilt = new Color32(222, 238, 196, 255);

        void RefreshDecorList()
        {
            int count = 0;
            for (int i = 0; i < ui.decorBuy.Length; i++)
            {
                bool built = SaveData.HasDecor(i);
                if (built) count++;
                ui.decorBuy[i].gameObject.SetActive(!built);
                ui.decorBuilt[i].gameObject.SetActive(built);
                var row = ui.decorBuilt[i].transform.parent.GetComponent<Image>();   // the row plate: built ones turn pale green
                if (row != null) row.color = built ? DecorRowBuilt : DecorRowOpen;
                ui.decorPrice[i].text = DecorCost[i].ToString();
                var img = ui.decorBuy[i].GetComponent<Image>();
                img.color = SaveData.Coins >= DecorCost[i] ? Color.white : new Color(.62f, .62f, .62f, 1f);
            }
            if (ui.decorProgress != null)
                ui.decorProgress.text = count == ui.decorBuy.Length ? "Your stall is complete!" : $"Stall built: {count} / {ui.decorBuy.Length}";
        }

        void BuyDecor(int i)
        {
            if (SaveData.HasDecor(i)) return;
            if (SaveData.Coins < DecorCost[i])
            {
                audioManager.Play(art.invalid, .7f);
                Haptics.Light();
                ui.StartCoroutine(Tween.Shake((RectTransform)ui.decorBuy[i].transform, 12f, .3f));
                return;
            }
            Click();
            int before = SaveData.Coins;
            SaveData.Coins -= DecorCost[i];
            _shownCoins = SaveData.Coins; ui.coinText.text = _shownCoins.ToString("N0");
            SaveData.Decor |= 1 << i;
            ClosePopup(ui.decorPopup);
            ui.StartCoroutine(BuildDecor(i, before, SaveData.Coins));
        }

        /// <summary>The new decoration pops onto the stall with a squash, sparkles burst around it, the coins count down.</summary>
        IEnumerator BuildDecor(int i, int coinsFrom, int coinsTo)
        {
            yield return new WaitForSeconds(.3f);
            ui.StartCoroutine(Tween.Run(.5f, k => ui.homeCoinText.text = Mathf.RoundToInt(Mathf.Lerp(coinsFrom, coinsTo, k)).ToString("N0")));
            var d = ui.homeDecor[i]; var rt = d.rectTransform;
            d.gameObject.SetActive(true);
            d.color = Color.white;
            rt.localScale = Vector3.zero;
            audioManager.Play(art.whoosh, .5f, 1.1f);
            yield return Tween.Run(.5f, k =>
            {
                float s = Ease.OutBack(k, 2.4f);
                float sq = Mathf.Sin(k * Mathf.PI * 2f) * .1f * (1f - k);
                rt.localScale = new Vector3(s * (1f + sq), s * (1f - sq), 1f);
            });
            rt.localScale = Vector3.one;
            audioManager.Play(art.pop, .9f, 1f);
            audioManager.Play(art.star, .7f, 1.15f);
            Haptics.Medium();
            // sparkle burst around the new decoration (the twinkling sparkles are borrowed for a moment)
            _sparkBurst = true;
            var centre = (Vector2)ui.home.transform.InverseTransformPoint(rt.TransformPoint(rt.rect.center));
            for (int k2 = 0; k2 < ui.homeSparkles.Length; k2++) ui.StartCoroutine(Burst(ui.homeSparkles[k2], centre, k2));
            yield return new WaitForSeconds(.8f);
            _sparkBurst = false;
            RefreshHome(_homeLevel);
            int built = 0; for (int k3 = 0; k3 < ui.homeDecor.Length; k3++) if (SaveData.HasDecor(k3)) built++;
            if (built == ui.homeDecor.Length) ShowMessage("Stall Complete!", "Your stall looks amazing!\nCustomers love it.");
        }

        IEnumerator Burst(Image sp, Vector2 centre, int i)
        {
            var rt = sp.rectTransform;
            var home = rt.anchoredPosition;
            var parent = (RectTransform)rt.parent;
            Vector2 c = parent.InverseTransformPoint(ui.home.transform.TransformPoint(centre));
            float ang = i * Mathf.PI * 2f / ui.homeSparkles.Length;
            var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            yield return Tween.Run(.7f, k =>
            {
                rt.anchoredPosition = c + dir * Mathf.Lerp(30f, 190f, Ease.OutCubic(k));
                rt.localScale = Vector3.one * (k < .3f ? k / .3f * 1.1f : 1.1f - (k - .3f));
                rt.localRotation = Quaternion.Euler(0f, 0f, k * 180f);
                GrillView.SetAlpha(sp, k < .6f ? 1f : (1f - k) / .4f);
            });
            rt.anchoredPosition = home;
            GrillView.SetAlpha(sp, 0f);
        }

        // ------------------------------------------------------------------ daily reward
        /// <summary>Days already claimed in this 7-day round (7 = the whole week was just finished today).</summary>
        static int DailyClaimed => !SaveData.DailyReady && SaveData.DailyIndex == 0 && SaveData.DailyLast != 0 ? 7 : SaveData.DailyIndex;

        void OpenDaily()
        {
            RefreshDaily();
            OpenPopup(ui.dailyPopup, ui.dailyPanel);
            if (SaveData.DailyReady) ui.StartCoroutine(DailyPulse());
        }

        void RefreshDaily()
        {
            int claimed = DailyClaimed;
            bool ready = SaveData.DailyReady;
            for (int i = 0; i < ui.dailyTiles.Length; i++)
            {
                bool done = i < claimed, today = ready && i == SaveData.DailyIndex;
                ui.dailyChecks[i].gameObject.SetActive(done);
                ui.dailyChecks[i].rectTransform.localScale = Vector3.one;
                ui.dailyTiles[i].color = today ? new Color32(255, 214, 92, 255) : done ? new Color32(214, 200, 172, 255) : new Color32(246, 232, 204, 255);
                var ic = ui.dailyIcons[i].color; ic.a = done ? .45f : 1f; ui.dailyIcons[i].color = ic;
                ui.dailyTiles[i].rectTransform.localScale = Vector3.one;
            }
            ui.dailyClaimText.text = ready ? "Claim!" : "Come back tomorrow!";
            ui.dailyClaim.GetComponent<Image>().color = ready ? Color.white : new Color(.62f, .62f, .62f, 1f);
        }

        IEnumerator DailyPulse()
        {
            int i = SaveData.DailyIndex;
            var rt = ui.dailyTiles[i].rectTransform;
            float t = 0f;
            while (IsOpen(ui.dailyPopup) && SaveData.DailyReady && SaveData.DailyIndex == i)
            {
                t += Time.deltaTime;
                float s = 1f + Mathf.Sin(t * 5f) * .04f;
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        void ClaimDaily()
        {
            if (!SaveData.DailyReady) { Click(); ClosePopup(ui.dailyPopup); return; }
            int i = SaveData.DailyIndex;
            var r = DailyRewards[i];
            int before = SaveData.Coins;
            if (r.booster < 0) SaveData.Coins += r.coins;
            else SaveData.SetBooster(r.booster, SaveData.Booster(r.booster) + r.count);
            SaveData.DailyLast = SaveData.Today;
            SaveData.DailyIndex = (i + 1) % DailyRewards.Length;
            _shownCoins = SaveData.Coins; ui.coinText.text = _shownCoins.ToString("N0");
            RefreshBoosters();
            ui.StartCoroutine(ClaimDailyAnim(i, before, SaveData.Coins));
        }

        IEnumerator ClaimDailyAnim(int i, int coinsFrom, int coinsTo)
        {
            ui.dailyPopup.interactable = false;
            audioManager.Play(art.coin, .85f);
            Haptics.Medium();
            var tile = ui.dailyTiles[i].rectTransform;
            yield return Tween.Bounce(tile, .2f, .3f);
            var chk = ui.dailyChecks[i];
            chk.gameObject.SetActive(true);
            chk.rectTransform.localScale = Vector3.zero;
            audioManager.Play(art.star, .7f, 1.2f);
            yield return Tween.Scale(chk.rectTransform, Vector3.one, .35f, Ease.OutElastic);
            ui.dailyTiles[i].color = new Color32(214, 200, 172, 255);
            yield return Tween.Run(.5f, k => ui.homeCoinText.text = Mathf.RoundToInt(Mathf.Lerp(coinsFrom, coinsTo, k)).ToString("N0"));
            ui.StartCoroutine(Tween.Bounce(ui.homeCoinIcon, .2f, .3f));
            yield return new WaitForSeconds(.45f);
            ui.dailyPopup.interactable = true;
            ClosePopup(ui.dailyPopup);
            RefreshHomeDots();
        }
    }
}
