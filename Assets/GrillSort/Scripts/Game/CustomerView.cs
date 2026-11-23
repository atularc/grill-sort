using UnityEngine;
using UnityEngine.UI;

namespace GrillSort
{
    /// <summary>One customer behind the counter, standing above their own tray (authored in the scene).</summary>
    public sealed class CustomerView : MonoBehaviour
    {
        public RectTransform rect;
        public RectTransform body;     // animal (moves when arriving / leaving)
        public Image animal;
        public RectTransform bubble;
        /// <summary>The order: a small skewer with the pieces this customer wants (Aaron 2026-11-21: "the orders could be skewers").</summary>
        public SkewerStack order;
        public Image check;
        public CanvasGroup group;
        /// <summary>A small icon that pops above the head (heart, sweat drop, anger mark, yummy face).</summary>
        public Image emote;

        // CustomersPeople_v2 put every eye line at the same height (284 of 512), lower than the old set: body raised to match
        public const float BodyY = 102f, BubbleY = 302f;   // 102 (was 118): the bust bottoms sit ~20 px behind the counter, so nobody floats (v4 art)
        /// <summary>Order bubble size and the box its skewer fits in (design px).</summary>
        public const float BubbleW = 224f, OrderBoxW = 172f, OrderBoxH = 90f, OrderBoxY = 12f;
        /// <summary>Tilt of the order skewer in the bubble: nearly flat, handle on the right.</summary>
        public const float OrderTilt = -80f;
        /// <summary>Where the emote pops: next to the head, on the right, under the bubble (customer-slot px).</summary>
        public static readonly Vector2 EmotePos = new Vector2(98f, 212f);

        [System.NonSerialized] public int food = -1;
        [System.NonSerialized] public bool busy;
        /// <summary>Tray this customer's order is sorted into.</summary>
        [System.NonSerialized] public int tray = -1;
        /// <summary>Which person (index into GameArt.customers / customersHappy / customersAngry).</summary>
        [System.NonSerialized] public int face;
        /// <summary>Seconds since this customer last had something happen (served piece, arrival): drives impatience.</summary>
        [System.NonSerialized] public float waited;
        [System.NonSerialized] public int who;   // id of the person standing here (GameController.Moods keeps their mood by it)
        /// <summary>True while a hop / cheer animates the body (the idle breathing leaves it alone).</summary>
        [System.NonSerialized] public bool anim;
        [System.NonSerialized] public Coroutine moodCo;
    }
}
