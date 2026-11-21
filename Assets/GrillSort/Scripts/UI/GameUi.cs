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

        [Header("Win popup")]
        public CanvasGroup winPopup;
        public RectTransform winPanel;
        public Image[] winStars;
        public TMP_Text winCoins;
        public TMP_Text winTitle;
        public Button winNext;
        public Button winDouble;
        public Image winGlow, winRays;
        public RectTransform winWord, winReward;
        public Image[] winSparkles;

        [Header("Pause popup")]
        public CanvasGroup pausePopup;
        public RectTransform pausePanel;
        public Button pauseResume, pauseRestart, pauseSound, pauseHaptics;
        public TMP_Text pauseSoundText, pauseHapticsText;

        [Header("Stuck popup")]
        public CanvasGroup stuckPopup;
        public RectTransform stuckPanel;
        public Button stuckUndo, stuckShuffle, stuckExtra, stuckRetry;

        [Header("Message popup")]
        public CanvasGroup messagePopup;
        public RectTransform messagePanel;
        public TMP_Text messageTitle, messageBody;
        public Button messageOk;
    }
}
