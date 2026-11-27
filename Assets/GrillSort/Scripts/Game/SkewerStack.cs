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
    /// turn with the stick when the skewer is tilted (<see cref="SetTilt"/>). Authored by the builder; runtime only re-skins.
    /// </summary>
    public sealed class SkewerStack : MonoBehaviour
    {
        public RectTransform rect;
        public Image stick;
        public Image[] foods;

        /// <summary>Piece spacing as a fraction of the piece width: the pieces sit pressed together, overlapping a bit.</summary>
        public const float Tight = .58f;
        /// <summary>Stick extra length past the pieces (handle on top, tip below), and its width (= the tray skewer's).</summary>
        public const float StickExtraTop = 38f, StickExtraBottom = 28f, StickWidth = 39f;   // 70 / 55 before 2026-11-26: shorter ends leave more of the order bubble for the food

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

        /// <summary>
        /// Free pose (Aaron 2026-11-25: the finished skewer itself leaves the grill): piece i centred at local y
        /// <paramref name="ys"/>[i] with width <paramref name="widths"/>[i], stick from <paramref name="stickBottom"/> to
        /// <paramref name="stickTop"/>. Lets this skewer start as an exact copy of a tray's long skewer and morph into
        /// the compact <see cref="Set"/> layout. Call <see cref="Set"/> first (it fixes Count and the sprites).
        /// </summary>
        public void Pose(float[] ys, float[] widths, float stickTop, float stickBottom)
        {
            for (int i = 0; i < Count; i++)
            {
                var rt = foods[i].rectTransform;
                rt.sizeDelta = new Vector2(widths[i], widths[i]);
                rt.anchoredPosition = new Vector2(0f, ys[i]);
            }
            var s = stick.rectTransform;
            s.sizeDelta = new Vector2(StickWidth, stickTop - stickBottom);
            s.anchoredPosition = new Vector2(0f, (stickTop + stickBottom) * .5f);
        }

        /// <summary>Stick ends (local y) of the compact <see cref="Set"/> layout.</summary>
        public float CompactTop => PieceY(0) + PieceWidth * .5f + StickExtraTop;
        public float CompactBottom => PieceY(Count - 1) - PieceWidth * .5f - StickExtraBottom;

        /// <summary>
        /// Tilts the whole skewer by <paramref name="degrees"/> (z). The pieces turn WITH the stick and stay pierced along
        /// it, like on the grill (Aaron 2026-11-27: "when a skewer rotates, the sausages don't rotate with it"; they used
        /// to be counter-rotated to stay upright).
        /// </summary>
        public void SetTilt(float degrees)
        {
            rect.localRotation = Quaternion.Euler(0f, 0f, degrees);
            foreach (var f in foods) f.rectTransform.localRotation = Quaternion.identity;
        }

        /// <summary>Half the axis-aligned size of one piece turned by <paramref name="degrees"/> (a turned square box).</summary>
        float TurnedHalf(float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            return PieceWidth * .5f * (Mathf.Abs(Mathf.Cos(rad)) + Mathf.Abs(Mathf.Sin(rad)));
        }

        /// <summary>
        /// Like <see cref="FitScale"/> but counts the stick too (handle and tip), so nothing pokes out of the box
        /// (Aaron 2026-11-25: the order skewers overlapped each other). <paramref name="centre"/> = the unscaled middle
        /// of the whole skewer in local units: place the skewer at boxCentre - centre * scale to centre it.
        /// </summary>
        public float FitBoxWithStick(float degrees, float w, float h, out Vector2 centre)
        {
            var rot = Quaternion.Euler(0f, 0f, degrees);
            float half = TurnedHalf(degrees);
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            void Add(Vector2 p, float pad)
            {
                minX = Mathf.Min(minX, p.x - pad); maxX = Mathf.Max(maxX, p.x + pad);
                minY = Mathf.Min(minY, p.y - pad); maxY = Mathf.Max(maxY, p.y + pad);
            }
            for (int i = 0; i < Count; i++) Add(rot * new Vector3(0f, PieceY(i), 0f), half);   // pieces turn with the stick
            Add(rot * new Vector3(0f, CompactTop, 0f), StickWidth * .5f);
            Add(rot * new Vector3(0f, CompactBottom, 0f), StickWidth * .5f);
            centre = new Vector2((minX + maxX) * .5f, (minY + maxY) * .5f);
            return Mathf.Min(w / (maxX - minX), h / (maxY - minY));
        }

        /// <summary>
        /// Scale that fits this skewer, tilted by <paramref name="degrees"/>, into a <paramref name="w"/> x <paramref name="h"/>
        /// box (pieces stay upright, so each adds its own width/height around the tilted row).
        /// </summary>
        public float FitScale(float degrees, float w, float h)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float run = (Count - 1) * Spacing;
            float piece = TurnedHalf(degrees) * 2f;
            float spanX = Mathf.Abs(Mathf.Sin(rad)) * run + piece;
            float spanY = Mathf.Abs(Mathf.Cos(rad)) * run + piece;
            return Mathf.Min(w / spanX, h / spanY);
        }
    }
}
