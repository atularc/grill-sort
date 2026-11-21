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
        public Image order;
        public Image check;
        public CanvasGroup group;

        public const float BodyY = 80f, BubbleY = 290f;

        [System.NonSerialized] public int food = -1;
        [System.NonSerialized] public bool busy;
        /// <summary>Tray this customer's order is sorted into.</summary>
        [System.NonSerialized] public int tray = -1;
    }
}
