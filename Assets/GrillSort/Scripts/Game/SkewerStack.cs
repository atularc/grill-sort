using UnityEngine;
using UnityEngine.UI;

namespace GrillSort
{
    /// <summary>
    /// A small skewer with an order's pieces on it (Aaron 2026-11-21: "the orders could be skewers. When the pieces are
    /// merged, they combine into one small skewer that flies up"). Used twice with the SAME internal layout: in every
    /// order bubble, and as the skewer that flies from a finished tray up to the customer, so the flight simply
    /// interpolates position / rotation / scale and lands exactly on the bubble's skewer.
    /// Local layout (unscaled, unrotated): the stick runs along local Y, piece 0 at the top (like the trays). The pieces
    /// stay upright when the skewer is tilted (<see cref="SetTilt"/>). Authored by the builder; runtime only re-skins.
    /// </summary>
    public sealed class SkewerStack : MonoBehaviour
    {
        public RectTransform rect;
        public Image stick;
        public Image[] foods;

        /// <summary>Piece spacing as a fraction of the piece width: the pieces sit pressed together, overlapping a bit.</summary>
        public const float Tight = .58f;
        /// <summary>Stick extra length past the pieces (handle on top, tip below), and its width (= the tray skewer's).</summary>
        public const float StickExtraTop = 70f, StickExtraBottom = 55f, StickWidth = 39f;

        public int Count { get; private set; }
        public float PieceWidth { get; private set; }
        public float Spacing => PieceWidth * Tight;

        /// <summary>Local Y of piece <paramref name="i"/> (0 = top).</summary>
        public float PieceY(int i) => Spacing * ((Count - 1) * .5f - i);

        /// <summary>Length of the row of pieces along the stick, outer edge to outer edge.</summary>
        public float RowLength => (Count - 1) * Spacing + PieceWidth;

        /// <summary>Shows <paramref name="count"/> pieces of one food, each <paramref name="width"/> wide (local units).</summary>
        public void Set(GameArt.FoodArt fa, int count, float width)
        {
            Count = Mathf.Clamp(count, 1, foods.Length);
            PieceWidth = width;
            for (int i = 0; i < foods.Length; i++)
            {
                bool on = i < Count;
                foods[i].gameObject.SetActive(on);
                if (!on) continue;
                foods[i].sprite = fa.grilled;
                foods[i].color = Color.white;
                var rt = foods[i].rectTransform;
                rt.sizeDelta = new Vector2(width, width);
                rt.anchoredPosition = new Vector2(0f, PieceY(i));
            }
            float top = PieceY(0) + width * .5f + StickExtraTop, bottom = PieceY(Count - 1) - width * .5f - StickExtraBottom;
            var s = stick.rectTransform;
            s.sizeDelta = new Vector2(StickWidth, top - bottom);
            s.anchoredPosition = new Vector2(0f, (top + bottom) * .5f);
        }

        /// <summary>Tilts the stick by <paramref name="degrees"/> (z) and keeps every piece upright.</summary>
        public void SetTilt(float degrees)
        {
            rect.localRotation = Quaternion.Euler(0f, 0f, degrees);
            var up = Quaternion.Euler(0f, 0f, -degrees);
            foreach (var f in foods) f.rectTransform.localRotation = up;
        }

        /// <summary>
        /// Scale that fits this skewer, tilted by <paramref name="degrees"/>, into a <paramref name="w"/> x <paramref name="h"/>
        /// box (pieces stay upright, so each adds its own width/height around the tilted row).
        /// </summary>
        public float FitScale(float degrees, float w, float h)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float run = (Count - 1) * Spacing;
            float spanX = Mathf.Abs(Mathf.Sin(rad)) * run + PieceWidth;
            float spanY = Mathf.Abs(Mathf.Cos(rad)) * run + PieceWidth;
            return Mathf.Min(w / spanX, h / spanY);
        }
    }
}
