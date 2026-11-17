using UnityEngine;
using UnityEngine.UI;

namespace GrillSort
{
    /// <summary>One customer slot behind the counter (authored in the scene).</summary>
    public sealed class CustomerView : MonoBehaviour
    {
        public RectTransform rect;
        public RectTransform body;     // animal (moves when arriving / leaving)
        public Image animal;
        public RectTransform bubble;
        public Image order;
        public Image check;
        public CanvasGroup group;

        public static readonly float[] SlotX = { -390f, -130f, 130f, 390f };
        public const float BodyY = 80f, BubbleY = 290f;

        [System.NonSerialized] public int food = -1;
        [System.NonSerialized] public bool busy;
    }
}
