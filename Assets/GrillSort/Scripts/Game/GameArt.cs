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
            /// <summary>Optional loading scene for this theme (or a season); empty = <see cref="GameArt.loadingKeyArt"/>.</summary>
            public Sprite loadingArt;
            public Sprite Back => backgroundCalm != null ? backgroundCalm : background;
        }

        public FoodArt[] foods;
        /// <summary>Customer faces (waiting), and the same people delighted / impatient (CustomersPeople_v3, one drawing per person).</summary>
        public Sprite[] customers;
        public Sprite[] customersHappy, customersAngry;
        [Header("Customer emotes")]
        public Sprite emoteHeart;
        public Sprite emoteSweat, emoteAngry, emoteYummy;
        public ThemeDef[] themes;

        [Header("Board")]
        public Sprite grill;
        public Sprite grillDark;
        /// <summary>The glowing coal bed, drawn UNDER <see cref="grill"/> / <see cref="grillDark"/> (which have the glow cut out) so it can flicker.</summary>
        public Sprite grillGlow;
        public Sprite grillDarkGlow;
        /// <summary>Shimmer layers over the glow bed (different random patches, pulsed out of step): lit / normal grill.</summary>
        public Sprite grillHotA, grillHotB, grillDarkHotA, grillDarkHotB;
        [Header("Loading screen")]
        /// <summary>Painted full-screen loading scene, used unless the current theme has its own <see cref="ThemeDef.loadingArt"/>.</summary>
        public Sprite loadingKeyArt;
        public Sprite logo, mascot;
        /// <summary>Helper tray (no customer): steel frame instead of red enamel, drawn over <see cref="grillDarkGlow"/>.</summary>
        public Sprite grillHelper;
        public Sprite skewer;
        /// <summary>Wrapped "?" food (foil) and the chains over a locked tray (demo mechanics 2026-11-26).</summary>
        public Sprite foilWrap, lockChains, iconLock;

        [Header("UI swaps")]
        public Sprite iconAddTray, iconShuffle;
        public Sprite starOn;
        public Sprite starOff;

        [Header("Audio")]
        public AudioClip pick;
        public AudioClip drop, invalid, sizzle, serve, coin, win, click;
        /// <summary>CC0 sound set (2026-11-21, Tools/ArtPipeline/make_audio.py, licences in ArtSource/Audio/LICENSES.md).</summary>
        public AudioClip[] drops;
        public AudioClip star, whoosh, pop, fail, sizzleLoop, music;
        /// <summary>Background music choices (index 0 = <see cref="music"/>); the pause menu's Music button cycles them.</summary>
        public AudioClip[] musicTracks;
        public string[] musicNames;

        public ThemeDef ThemeForLevel(int level)
        {
            ThemeDef best = themes[0];
            foreach (var t in themes) if (t.unlockLevel <= level && t.unlockLevel >= best.unlockLevel) best = t;
            return best;
        }
    }
}
