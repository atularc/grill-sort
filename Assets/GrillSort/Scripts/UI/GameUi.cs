using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GrillSort
{
    /// <summary>References to every authored UI element (filled by the scene builder).</summary>
    public sealed class GameUi : MonoBehaviour
    {
        [Header("Stage")]
        public Image background;
        public Image front;
        public StageLayout layout;
        public GrillView[] grills;
        public CustomerView[] customers;
        public RectTransform flyLayer;
        public RectTransform[] flyers;
        public Image[] flyerFoods;
        /// <summary>Difficulty tag under the level pill (EASY ... SUPER HARD, or HARD on Hard levels).</summary>
        public Image tierTag;
        public TMPro.TextMeshProUGUI tierTagText;
        /// <summary>Foil over a flying piece that is still wrapped.</summary>
        public Image[] flyerFoils;
        public Image[] flyerShadows;
        /// <summary>The small skewer a finished order flies up to its customer on (same layout as the bubble skewers).</summary>
        public SkewerStack serveSkewer;

        [Header("HUD")]
        public Button pauseButton;
        public TMP_Text levelText;
        public TMP_Text coinText;
        public RectTransform coinIcon;
        public TMP_Text ordersText;
        public RectTransform ordersFill;
        public float ordersFillMax = 300f;

        [Header("Boosters (undo, hint, shuffle, extra)")]
        public Button[] boosters;
        public TMP_Text[] boosterCounts;

        [Header("Tips / toast")]
        public CanvasGroup tip;
        public TMP_Text tipText;
        public CanvasGroup toast;
        public TMP_Text toastText;

        [Header("Win screen (FishUp!-style Well Done)")]
        public CanvasGroup winPopup;
        public RectTransform winPanel;
        public Image winDim;
        public RectTransform winWord, winBubble, winMultBar, winPointer;
        public Image winGlow, winRays;
        public Image[] winSparkles;
        public Button winNext, winDouble;
        public TMP_Text winCoins, winDoubleLabel, winDoubleAmount;
        public RectTransform winCoinPill, winCoinPillIcon;
        public TMP_Text winCoinPillText;
        public RectTransform[] winFlyCoins;

        [Header("Pause popup")]
        public CanvasGroup pausePopup;
        public RectTransform pausePanel;
        public Button pauseResume, pauseRestart, pauseHaptics, pauseClose, pauseRestore;
        public TMP_Text pauseHapticsText;
        public Image pauseHapticsImage;
        public Sprite hapticsOnSprite, hapticsOffSprite;
        /// <summary>Volume sliders (0..1) and their % labels.</summary>
        public Slider musicSlider, sfxSlider;
        public TMP_Text musicValue, sfxValue;

        [Header("Stuck popup")]
        public CanvasGroup stuckPopup;
        public RectTransform stuckPanel;
        public TMP_Text stuckTitle, stuckCaption, stuckPrice, stuckReason;
        public Image stuckIcon;
        public RectTransform stuckOffer, stuckGiveUp;
        public Button stuckClose, stuckBuy, stuckAd, stuckUndo, stuckRetry, stuckKeep;

        [Header("Loading screen (game start)")]
        public CanvasGroup loading;
        /// <summary>Full-screen painted scene, cover-fit (sprite set at runtime: theme override or the default key art).</summary>
        public Image loadingBg;
        public RectTransform loadingLogo, loadingMascot;
        public Image loadingRays;
        public Image[] loadingSteam, loadingEmbers, loadingSparkles;
        public RectTransform loadingBarFill;
        public float loadingBarMax = 500f;
        public TMP_Text loadingText;

        [Header("Tutorial hand")]
        public RectTransform handLayer;
        public RectTransform hand;
        public Image handImage;
        public Image handRing;
        /// <summary>Fades the glove and its soft shadow together.</summary>
        public CanvasGroup handGroup;
        /// <summary>A see-through copy of the piece the hand drags, so the move is obvious.</summary>
        public Image handGhost;

        [Header("Home screen (meta, 2026-11-23)")]
        public CanvasGroup home;
        /// <summary>The stall: theme background + counter front, cover-fit like the game; decorations sit in its normalised coordinates.</summary>
        public Image homeBg, homeFront;
        public Image[] homeDecor;
        public Image[] homeCustomers;
        public RectTransform homeMascot;
        public Image[] homeSparkles;
        public Button homePlay, homeDaily, homeDecorate, homeSettings;
        public TMP_Text homePlayText, homeWorldName, homeWorldNext, homeCoinText, homeDecorCount;
        public RectTransform homeCoinIcon, homeWorldFill;
        public float homeWorldFillMax = 400f;
        public Image homeDailyDot, homeDecorateDot;

        [Header("Daily reward popup")]
        public CanvasGroup dailyPopup;
        public RectTransform dailyPanel;
        public Image[] dailyTiles, dailyIcons, dailyChecks;
        public TMP_Text[] dailyAmounts, dailyDays;
        public Button dailyClaim, dailyClose;
        public TMP_Text dailyClaimText;

        [Header("Decorate popup")]
        public CanvasGroup decorPopup;
        public RectTransform decorPanel;
        public Button[] decorBuy;
        public TMP_Text[] decorPrice;
        public Image[] decorBuilt;
        public TMP_Text decorProgress;   // "Stall built: 3 / 6" under the ribbon
        public Button decorClose;

        /// <summary>The wooden shelf the booster tiles stand on.</summary>
        public Image boosterShelf;

        [Header("Message popup")]
        public CanvasGroup messagePopup;
        public RectTransform messagePanel;
        public TMP_Text messageTitle, messageBody;
        public Button messageOk;
    }
}
