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
        public Button pauseResume, pauseRestart, pauseSound, pauseMusic, pauseHaptics, pauseClose, pauseRestore;
        /// <summary>Toggle tiles and their red "off" slashes.</summary>
        public Image pauseSoundTile, pauseMusicTile, pauseHapticsTile, pauseSoundOff, pauseMusicOff, pauseHapticsOff;

        [Header("Stuck popup")]
        public CanvasGroup stuckPopup;
        public RectTransform stuckPanel;
        public Button stuckUndo, stuckShuffle, stuckExtra, stuckRetry;

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

        [Header("Message popup")]
        public CanvasGroup messagePopup;
        public RectTransform messagePanel;
        public TMP_Text messageTitle, messageBody;
        public Button messageOk;
    }
}
