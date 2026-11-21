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

        // CustomersPeople_v2 put every eye line at the same height (284 of 512), lower than the old set: body raised to match
        public const float BodyY = 118f, BubbleY = 302f;
        /// <summary>Order bubble size and the box its skewer fits in (design px).</summary>
        public const float BubbleW = 224f, OrderBoxW = 172f, OrderBoxH = 90f, OrderBoxY = 12f;
        /// <summary>Tilt of the order skewer in the bubble: nearly flat, handle on the right.</summary>
        public const float OrderTilt = -80f;

        [System.NonSerialized] public int food = -1;
        [System.NonSerialized] public bool busy;
        /// <summary>Tray this customer's order is sorted into.</summary>
        [System.NonSerialized] public int tray = -1;
    }
}
