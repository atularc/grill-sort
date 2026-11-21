using System;
using UnityEngine;

namespace GrillSort
{
    /// <summary>Every sprite/clip the game swaps at runtime, wired by "Grill Sort > Run Full Setup" (no Resources folder).</summary>
    [CreateAssetMenu(menuName = "Grill Sort/Game Art")]
    public sealed class GameArt : ScriptableObject
    {
        [Serializable]
        public sealed class FoodArt
        {
            public string name;
            public Sprite grilled, medium, raw;
            public Sprite shadow, shadowRaw;
            /// <summary>Display width in design px at 1080p (from the mockup).</summary>
            public float width = 150f;
        }

        [Serializable]
        public sealed class ThemeDef
        {
            public string name;
            public string displayName;
            public Sprite background, backgroundCalm, front;
            /// <summary>Counter top in px of the 1024x1536 source image.</summary>
            public float counterTop = 528f;
            /// <summary>Bottom of the counter front / curtain in px of the source (0 = unknown: fixed gap).</summary>
            public float counterBottom;
            public int unlockLevel = 1;
            public Sprite Back => backgroundCalm != null ? backgroundCalm : background;
        }

        public FoodArt[] foods;
        public Sprite[] customers;
        public ThemeDef[] themes;

        [Header("Board")]
        public Sprite grill;
        public Sprite grillDark;
        /// <summary>The glowing coal bed, drawn UNDER <see cref="grill"/> / <see cref="grillDark"/> (which have the glow cut out) so it can flicker.</summary>
        public Sprite grillGlow;
        public Sprite grillDarkGlow;
        /// <summary>Helper tray (no customer): steel frame instead of red enamel, drawn over <see cref="grillDarkGlow"/>.</summary>
        public Sprite grillHelper;
        public Sprite skewer;

        [Header("UI swaps")]
        public Sprite starOn;
        public Sprite starOff;

        [Header("Audio")]
        public AudioClip pick;
        public AudioClip drop, invalid, sizzle, serve, coin, win, click;

        public ThemeDef ThemeForLevel(int level)
        {
            ThemeDef best = themes[0];
            foreach (var t in themes) if (t.unlockLevel <= level && t.unlockLevel >= best.unlockLevel) best = t;
            return best;
        }
    }
}
