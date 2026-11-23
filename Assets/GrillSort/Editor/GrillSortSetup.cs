using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.U2D;
using UnityEngine.UI;
using static GrillSort.EditorTools.UiKit;

namespace GrillSort.EditorTools
{
    /// <summary>
    /// "Grill Sort > Run Full Setup": wires the GameArt asset, makes the TMP font assets, sprite atlases and
    /// texture settings, and authors the whole Game scene (every Canvas object lives in the scene; nothing is
    /// created or loaded at runtime).
    /// </summary>
    public static class GrillSortSetup
    {
        const string Root = "Assets/GrillSort/";
        const string Art = Root + "Art/";
        const string Data = Root + "Data/";
        const string ScenePath = Root + "Scenes/Game.unity";

        static readonly Color Brown = new Color32(92, 46, 18, 255);
        static readonly Color Cream = new Color32(255, 246, 225, 255);
        static readonly Color Navy = new Color32(28, 46, 104, 255);   // tutorial banner text (like the reference)

        static Material _matPlain, _matBrownOutline, _matGreenOutline;
        static GameArt _art;

        [MenuItem("Grill Sort/Run Full Setup", priority = 0)]
        public static void RunFullSetup()
        {
            if (!EnsureTmpEssentials()) return;
            ConfigureTextures();
            ConfigureAudio();
            MakeFonts();
            _art = MakeGameArt();
            var bank = MakeLevelBank();
            MakeAtlases();
            LayoutKeeper.BeforeBuild();   // remember hand-made layout tweaks in the current Game scene
            BuildScene(bank);
            LayoutKeeper.AfterBuild();    // ... and put them back on top of the rebuilt scene
            ConfigureProject();
            AssetDatabase.SaveAssets();
            Debug.Log("[Grill Sort] Full setup done. Scene: " + ScenePath + (bank.levels.Count == 0 ? "  (No levels yet: run Grill Sort > Bake And Verify Levels)" : $"  ({bank.levels.Count} levels)"));
        }

        // ------------------------------------------------------------------ TMP essentials
        static bool EnsureTmpEssentials()
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/TextMesh Pro/Resources/TMP Settings.asset") != null) return true;
            Debug.Log("[Grill Sort] Importing TMP Essential Resources (TextMeshPro needs them). Run the setup again when the import finishes.");
            TMP_PackageResourceImporter.ImportResources(true, false, false);
            return false;
        }

        // ------------------------------------------------------------------ textures
        static void ConfigureTextures()
        {
            var borders = new Dictionary<string, Vector4>
            {
                { Art + UiDir + "Panels/bar_hud.png", new Vector4(130, 0, 130, 0) },
                { Art + UiDir + "Buttons/btn_green.png", new Vector4(110, 0, 110, 0) },
                { Art + UiDir + "Buttons/btn_orange.png", new Vector4(110, 0, 110, 0) },
                { Art + UiDir + "Buttons/btn_red.png", new Vector4(110, 0, 110, 0) },
                { Art + "Generated/" + BarFill, new Vector4(52, 0, 52, 0) },
                { Art + "Generated/rounded.png", new Vector4(50, 50, 50, 50) },
            };
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "Art" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;
                bool dirty = false;
                if (ti.textureType != TextureImporterType.Sprite) { ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; dirty = true; }
                if (ti.mipmapEnabled) { ti.mipmapEnabled = false; dirty = true; }
                if (borders.TryGetValue(path, out var b) && ti.spriteBorder != b) { ti.spriteBorder = b; dirty = true; }
                // 9-sliced / stretched sprites are packed untrimmed (Full Rect): the atlas trims transparent margins, but a
                // sliced Image still draws the whole rect, so the cut-off margin sampled the atlas padding, which holds the
                // neighbours' edge colours (purple/navy lines above the HUD pills, 2026-11-21)
                if (borders.ContainsKey(path) || path.EndsWith("/bar_track.png") || path.EndsWith("/bar_track_clean.png"))
                {
                    var st = new TextureImporterSettings();
                    ti.ReadTextureSettings(st);
                    if (st.spriteMeshType != SpriteMeshType.FullRect) { st.spriteMeshType = SpriteMeshType.FullRect; ti.SetTextureSettings(st); dirty = true; }
                }
                bool isBg = path.Contains("/Backgrounds/") || path.Contains("/Loading/keyart");   // full-screen art: up to 2048
                int max = isBg ? 2048 : path.Contains("/Shadows") ? 256 : 1024;
                if (path.Contains("/Board/")) max = 2048;
                if (ti.maxTextureSize != max) { ti.maxTextureSize = max; dirty = true; }
                // atlased sprites stay uncompressed at the source; the atlas compresses them (ASTC on mobile)
                bool atlased = IsAtlased(path);
                var comp = atlased ? TextureImporterCompression.Uncompressed : TextureImporterCompression.Compressed;
                if (ti.textureCompression != comp) { ti.textureCompression = comp; dirty = true; }
                foreach (var plat in new[] { "Android", "iPhone" })
                {
                    var ps = ti.GetPlatformTextureSettings(plat);
                    var fmt = isBg ? TextureImporterFormat.ASTC_8x8 : TextureImporterFormat.ASTC_6x6;
                    if (atlased)
                    {
                        if (ps.overridden) { ps.overridden = false; ti.SetPlatformTextureSettings(ps); dirty = true; }
                    }
                    else if (!ps.overridden || ps.format != fmt || ps.maxTextureSize != max)
                    {
                        ps.overridden = true; ps.format = fmt; ps.maxTextureSize = max;
                        ti.SetPlatformTextureSettings(ps); dirty = true;
                    }
                }
                if (dirty) ti.SaveAndReimport();
            }
        }

        // ------------------------------------------------------------------ audio import (small build)
        static void ConfigureAudio()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Root + "Audio/SFX", Root + "Audio/Music" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ai = AssetImporter.GetAtPath(path) as AudioImporter;
                if (ai == null) continue;
                bool isMusic = path.Contains("/Music/");
                var st = ai.defaultSampleSettings;
                // effects: mono, decompressed in memory (instant, no CPU while playing); music: streamed from disk
                st.loadType = isMusic ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                st.compressionFormat = AudioCompressionFormat.Vorbis;
                st.quality = isMusic ? .45f : .7f;
                st.sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate;
                ai.defaultSampleSettings = st;
                ai.forceToMono = !isMusic;
                ai.loadInBackground = isMusic;
                ai.SaveAndReimport();
            }
        }

        // Cartoon art set (client direction 2026-11-18). The old realistic Food/Grilled, Customers (animals) and
        // grill/skewer art stays in the project, unused.
        /// <summary>Message strip (tips + toasts) just above the booster tiles (tile centre 130, half height ~85).</summary>
        const float MsgW = 860f, MsgH = 108f, MsgY = 282f;
        // Final food (2026-11-18): Aaron chose style B and asked for less shine. FoodFinal_v2 = half the shine (STYLE=FINAL2);
        // FoodFinal (v1, shine removed: user found it too matte) is the backup.
        // FoodCartoon (A), FoodCartoonB, FoodCartoonC stay on disk as unused alternatives.
        // FoodFinal_v3 (2026-11-19): Aaron's sign-off note, grill marks softer and lighter, none on some foods
        // (Tools/ArtPipeline/soften_marks.py). FoodFinal_v2 is the backup.
        // CustomersPeople_v2 (2026-11-21, Aaron: characters' style off from the rest, head sizes vary): redrawn in the food's
        // style and normalised to one face size / eye line (Tools/ArtPipeline/cut_people_v2.py). CustomersPeople = backup.
        // CustomersPeople_v3 (2026-11-23): the same 8 people with three moods each, drawn as one picture per person
        // (<name>.png waiting, <name>_happy.png, <name>_angry.png; Tools/ArtPipeline/cut_people_v3.py). v2 = backup.
        const string FoodDir = "FoodFinal_v3/Grilled/", ShadowDir = "Generated/ShadowsFinal/", CustomerDir = "CustomersPeople_v3/";
        // Raw stage (2026-11-23, cooking stages as the hook): FoodRaw_v1 in the game food's style (Tools/ArtPipeline/cut_food_v5.py).
        // The old stylized-realistic Food/Raw + Food/Medium stay on disk, unused (no Medium in the cartoon set: raw -> grilled).
        const string RawDir = "FoodRaw_v1/Raw/", RawShadowDir = "Generated/ShadowsRaw_v1/", MetaDir = "Meta/";
        // per-food replacements (2026-11-21, Aaron: tomato "too juicy, not quite like a grilled tomato yet"): a food found
        // here is used instead of the FoodDir one, with its shadow from FoodOverrideShadowDir (Tools/ArtPipeline/cut_tomato_v4.py)
        // FoodFinal_v5 (2026-11-23, review: squid, bacon and beef read as uncooked in play): clearly cooked redraws,
        // softened marks (cut_food_v5.py). Checked first, then v4 (tomato).
        static readonly (string food, string shadow)[] FoodOverrides =
        {
            ("FoodFinal_v5/Grilled/", "Generated/ShadowsFinal_v5/"),
            ("FoodFinal_v4/Grilled/", "Generated/ShadowsFinal_v4/"),
        };
        static readonly string[] CustomerNames = { "grandpa", "ponytail", "redhead", "curly", "dad", "grandma", "pigtails", "quiff" };

        // UI_Soft (2026-11-21): Aaron asked for a softer, less glossy UI ("still 3D cartoon"): the glare is toned down
        // by Tools/ArtPipeline/soften_ui.py. Art/UI/ (and Generated/bar_fill.png) stay as the glossy backups.
        const string UiDir = "UI_Soft/", BarFill = "bar_fill_soft.png";

        static readonly string[] AtlasFolders = { Art + UiDir, Art + CustomerDir, Art + "VFX/", Art + FoodDir, Art + ShadowDir, Art + RawDir, Art + RawShadowDir, Art + MetaDir,
            Art + FoodOverrides[0].food, Art + FoodOverrides[0].shadow, Art + FoodOverrides[1].food, Art + FoodOverrides[1].shadow };
        static readonly string[] AtlasFiles = { "badge.png", "disc.png", "rounded.png", BarFill, "bar_track.png", "glow.png", "soft_rect.png", "rays.png", "bar_track_clean.png", "glow_hd.png", "rays_hd.png" };

        static bool IsAtlased(string path)
        {
            foreach (var f in AtlasFolders) if (path.StartsWith(f)) return true;
            foreach (var f in AtlasFiles) if (path == Art + "Generated/" + f) return true;
            return false;
        }

        // ------------------------------------------------------------------ fonts
        static void MakeFonts()
        {
            string fa = Root + "Fonts/Fredoka-Bold SDF.asset";
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fa);
            if (font == null)
            {
                var ttf = Load<UnityEngine.Font>(Root + "Fonts/Fredoka-Bold.ttf");
                font = TMP_FontAsset.CreateFontAsset(ttf, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                font.name = "Fredoka-Bold SDF";
                AssetDatabase.CreateAsset(font, fa);
                font.atlasTextures[0].name = "Fredoka-Bold SDF Atlas";
                AssetDatabase.AddObjectToAsset(font.atlasTextures[0], font);
                font.material.name = "Fredoka-Bold SDF Material";
                AssetDatabase.AddObjectToAsset(font.material, font);
                font.TryAddCharacters(" ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789.,:;!?'\"/+-x()&%#@");
                EditorUtility.SetDirty(font);
                AssetDatabase.SaveAssets();
            }
            UiKit.Font = font;
            _matPlain = font.material;
            _matBrownOutline = FontMat("Fredoka Outline Brown", font, new Color32(78, 38, 16, 255), .22f);
            _matGreenOutline = FontMat("Fredoka Outline Green", font, new Color32(40, 70, 20, 255), .2f);
        }

        static Material FontMat(string name, TMP_FontAsset font, Color outline, float width)
        {
            string path = Root + "Fonts/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(font.material) { name = name }; AssetDatabase.CreateAsset(m, path); }
            m.shader = font.material.shader;
            m.SetTexture("_MainTex", font.atlasTextures[0]);
            m.EnableKeyword("OUTLINE_ON");
            m.SetColor("_OutlineColor", outline);
            m.SetFloat("_OutlineWidth", width);
            m.SetFloat("_FaceDilate", width * .9f);
            m.EnableKeyword("UNDERLAY_ON");
            m.SetColor("_UnderlayColor", new Color(outline.r, outline.g, outline.b, .8f));
            m.SetFloat("_UnderlayOffsetY", -.6f);
            m.SetFloat("_UnderlayDilate", width);
            m.SetFloat("_UnderlaySoftness", .1f);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ data assets
        static readonly Dictionary<string, float> FoodWidth = new Dictionary<string, float>
        {
            { "sausage", 170 }, { "chicken", 150 }, { "shrimp", 156 }, { "meatball", 146 }, { "corn", 160 }, { "tomato", 140 },
            { "mushroom", 150 }, { "broccoli", 156 }, { "pepper", 156 }, { "onion", 150 }, { "pineapple", 150 }, { "beef", 148 },
            { "zucchini", 150 }, { "salmon", 150 }, { "squid", 160 }, { "potato", 146 }, { "bacon", 156 }, { "halloumi", 150 },
        };

        static GameArt MakeGameArt()
        {
            Directory.CreateDirectory(Data);
            string path = Data + "GameArt.asset";
            var a = AssetDatabase.LoadAssetAtPath<GameArt>(path);
            if (a == null) { a = ScriptableObject.CreateInstance<GameArt>(); AssetDatabase.CreateAsset(a, path); }

            var foods = new List<GameArt.FoodArt>();
            foreach (var n in FoodCatalog.Names)
            {
                foods.Add(new GameArt.FoodArt
                {
                    name = n,
                    grilled = Load<Sprite>($"{Art}{FoodFile(n)}"),
                    medium = null,
                    raw = Load<Sprite>($"{Art}{RawDir}{n}.png"),
                    shadow = Load<Sprite>($"{Art}{ShadowFile(n)}"),
                    shadowRaw = Load<Sprite>($"{Art}{RawShadowDir}{n}.png"),
                    width = FoodWidth[n],
                });
            }
            a.foods = foods.ToArray();
            a.customers = CustomerNames.Select(n => Load<Sprite>($"{Art}{CustomerDir}{n}.png"));
            a.customersHappy = CustomerNames.Select(n => Load<Sprite>($"{Art}{CustomerDir}{n}_happy.png"));
            a.customersAngry = CustomerNames.Select(n => Load<Sprite>($"{Art}{CustomerDir}{n}_angry.png"));
            a.emoteHeart = S(MetaDir + "emote_heart.png"); a.emoteSweat = S(MetaDir + "emote_sweat.png");
            a.emoteAngry = S(MetaDir + "emote_angry.png"); a.emoteYummy = S(MetaDir + "emote_yummy.png");
            a.themes = new[]
            {
                // Aaron 2026-11-18: bright backgrounds for normal levels, darker ones saved for later levels.
                // Mean brightness: Food Truck 212, Beach 185, Winter 146, Wood 73, Night Market 69.
                // Aaron 2026-11-19: keep the Beach background from the demo -> Beach for the whole 50-level build.
                // counter bottom = bottom of the compact curtain (Tools/ArtPipeline/compact_counter.py, Aaron 2026-11-21)
                Theme("Beach", "Beach Shack", 500, 619, 1),
                Theme("FoodTruck", "Food Truck", 527, 606, 51),
                Theme("Winter", "Winter Market", 538, 612, 61),
                Theme("Wood", "Wood Stall", 528, 612, 71),
                Theme("NightMarket", "Night Market", 520, 598, 81),
            };
            // the red coal glow is its own layer UNDER the coals so it can flicker (Aaron 2026-11-21; Tools/ArtPipeline/
            // split_grill_layers.py): *_bed = the glowing coal bed, *_front = frame + coals + bars with the glow cut out.
            // (The 2026-11-18 split, grill_cartoon*_base/_glow, only lifted the bright spots and drew them on top.)
            a.grill = Load<Sprite>(Art + "Generated/grill_cartoon_front.png");
            a.grillGlow = Load<Sprite>(Art + "Generated/grill_cartoon_bed.png");
            a.grillDark = Load<Sprite>(Art + "Generated/grill_cartoon_dim_front.png"); // brighter than the v3 grill_cartoon_dark (Aaron: bottom too dark)
            a.grillDarkGlow = Load<Sprite>(Art + "Generated/grill_cartoon_dim_bed.png");
            a.grillHelper = Load<Sprite>(Art + "Generated/grill_helper_front.png"); // helper trays: steel frame
            // ember shimmer (user 2026-11-21: the glow "still feels like before"): two patch layers over the bed
            a.grillHotA = Load<Sprite>(Art + "Generated/grill_cartoon_hot_a.png");
            a.grillHotB = Load<Sprite>(Art + "Generated/grill_cartoon_hot_b.png");
            a.grillDarkHotA = Load<Sprite>(Art + "Generated/grill_cartoon_dim_hot_a.png");
            a.grillDarkHotB = Load<Sprite>(Art + "Generated/grill_cartoon_dim_hot_b.png");
            // loading screen v2 (Tools/ArtPipeline/make_loading_art.py): painted scene, logo image, mascot.
            // Loading/hero_grill.png + logo_plate.png (v1, the "collage" look the user rejected) stay unused.
            a.loadingKeyArt = Load<Sprite>(Art + "Loading/keyart_default.png");
            a.logo = Load<Sprite>(Art + "Loading/logo.png");
            a.mascot = Load<Sprite>(Art + "Loading/mascot.png");
            a.skewer = Load<Sprite>(Art + "Board/skewer_cartoon.png");
            a.starOn = S("UI/Icons/icon_star.png");
            a.iconAddTray = S("UI/Icons/icon_add_skewer.png"); a.iconShuffle = S("UI/Icons/icon_shuffle.png");
            a.starOff = a.starOn;
            // CC0 sound set + music (Tools/ArtPipeline/make_audio.py; licences: ArtSource/Audio/LICENSES.md). The old
            // synthesized placeholders (Audio/*.wav) stay on disk, unused.
            string au = Root + "Audio/SFX/";
            a.pick = Load<AudioClip>(au + "pick.ogg"); a.drop = Load<AudioClip>(au + "drop_0.ogg");
            a.drops = new[] { a.drop, Load<AudioClip>(au + "drop_1.ogg"), Load<AudioClip>(au + "drop_2.ogg") };
            a.invalid = Load<AudioClip>(au + "invalid.ogg"); a.sizzle = Load<AudioClip>(au + "sizzle.ogg");
            a.serve = Load<AudioClip>(au + "serve.ogg"); a.coin = Load<AudioClip>(au + "coin.ogg");
            a.win = Load<AudioClip>(au + "win.ogg"); a.click = Load<AudioClip>(au + "click.ogg");
            a.star = Load<AudioClip>(au + "star.ogg"); a.whoosh = Load<AudioClip>(au + "whoosh.ogg");
            a.pop = Load<AudioClip>(au + "pop.ogg"); a.fail = Load<AudioClip>(au + "fail.ogg");
            a.sizzleLoop = Load<AudioClip>(au + "sizzle_loop.ogg");
            a.music = Load<AudioClip>(Root + "Audio/Music/bgm_main.ogg");
            // one track (user pick: "Which Brand of Mustard Shall I Buy?", looped seamlessly by make_audio.py)
            a.musicTracks = new[] { a.music }; a.musicNames = new[] { "On" };
            EditorUtility.SetDirty(a);
            return a;
        }

        static string FoodFile(string n)
        {
            foreach (var o in FoodOverrides) if (File.Exists($"{Art}{o.food}{n}.png")) return $"{o.food}{n}.png";
            return $"{FoodDir}{n}.png";
        }

        static string ShadowFile(string n)
        {
            foreach (var o in FoodOverrides) if (File.Exists($"{Art}{o.food}{n}.png")) return $"{o.shadow}{n}.png";
            return $"{ShadowDir}{n}.png";
        }

        static T[] Select<T>(this string[] names, System.Func<string, T> f)
        {
            var r = new T[names.Length];
            for (int i = 0; i < names.Length; i++) r[i] = f(names[i]);
            return r;
        }

        static GameArt.ThemeDef Theme(string folder, string display, float counterTop, float counterBottom, int unlock)
        {
            string p = $"{Art}Backgrounds/{folder}/";
            return new GameArt.ThemeDef
            {
                name = folder,
                displayName = display,
                background = Load<Sprite>(p + "background.png"),
                backgroundCalm = File.Exists(p + "background-calm.png") ? Load<Sprite>(p + "background-calm.png") : null,
                // compact counter + curtain (compact_counter.py, Aaron 2026-11-21: smaller), made from the brightened
                // planks where those exist (brighten_board.py; Wood, Aaron: bottom too dark)
                front = Load<Sprite>(p + FrontFile(p)),
                counterTop = counterTop,
                counterBottom = File.Exists(p + "layer_front_compact.png") ? counterBottom : 0f,
                unlockLevel = unlock,
            };
        }

        static string FrontFile(string p)
        {
            foreach (var f in new[] { "layer_front_compact.png", "layer_front_bright.png" }) if (File.Exists(p + f)) return f;
            return "layer_front.png";
        }

        public static LevelBank MakeLevelBank()
        {
            Directory.CreateDirectory(Data);
            string path = Data + "LevelBank.asset";
            var b = AssetDatabase.LoadAssetAtPath<LevelBank>(path);
            if (b == null) { b = ScriptableObject.CreateInstance<LevelBank>(); AssetDatabase.CreateAsset(b, path); }
            return b;
        }

        // ------------------------------------------------------------------ atlases
        static void MakeAtlases()
        {
            Directory.CreateDirectory(Root + "Atlases");
            // customers (24 faces) and the meta art get their own atlases: together with the UI they no longer fit one page
            Atlas("Customers", new[] { Art + CustomerDir.TrimEnd('/') }, 2048, null);
            Atlas("Meta", new[] { Art + MetaDir.TrimEnd('/') }, 2048, null);
            Atlas("UI", new[] { Art + UiDir.TrimEnd('/'), Art + "VFX" }, 2048, new[] { Art + "Generated/badge.png", Art + "Generated/disc.png", Art + "Generated/rounded.png", Art + "Generated/" + BarFill, Art + "Generated/bar_track.png", Art + "Generated/glow.png", Art + "Generated/soft_rect.png", Art + "Generated/rays.png", Art + "Generated/bar_track_clean.png", Art + "Generated/glow_hd.png", Art + "Generated/rays_hd.png" });
            var food = new List<string> { Art + FoodDir.TrimEnd('/'), Art + ShadowDir.TrimEnd('/'), Art + RawDir.TrimEnd('/'), Art + RawShadowDir.TrimEnd('/') };
            foreach (var o in FoodOverrides) { food.Add(Art + o.food.TrimEnd('/')); food.Add(Art + o.shadow.TrimEnd('/')); }
            Atlas("Food", food.ToArray(), 2048, null);
        }

        static void Atlas(string name, string[] folders, int max, string[] files)
        {
            string path = $"{Root}Atlases/{name}.spriteatlasv2";
            // rebuild every run so the packed folders always match the current art (sprites reference
            // textures, not the atlas, so a fresh atlas asset is safe)
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            {
                var asset = new SpriteAtlasAsset();
                var objs = new List<Object>();
                foreach (var f in folders) objs.Add(AssetDatabase.LoadAssetAtPath<DefaultAsset>(f));
                if (files != null) foreach (var f in files) objs.Add(AssetDatabase.LoadAssetAtPath<Texture2D>(f));
                asset.Add(objs.ToArray());
                SpriteAtlasAsset.Save(asset, path);
                AssetDatabase.ImportAsset(path);
            }
            var imp = AssetImporter.GetAtPath(path) as SpriteAtlasImporter;
            if (imp == null) return;
            var pack = imp.packingSettings; pack.enableRotation = false; pack.enableTightPacking = false; pack.padding = 8; imp.packingSettings = pack; // padding >= ASTC 6x6 block: a 4 px gap let compression blocks mix neighbours (purple lines above the HUD pills)
            var tex = imp.textureSettings; tex.generateMipMaps = false; imp.textureSettings = tex;
            foreach (var plat in new[] { "Android", "iPhone" })
            {
                var ps = imp.GetPlatformSettings(plat);
                ps.overridden = true; ps.maxTextureSize = max; ps.format = TextureImporterFormat.ASTC_6x6;
                imp.SetPlatformSettings(ps);
            }
            imp.SaveAndReimport();
        }

        // ------------------------------------------------------------------ project
        static void ConfigureProject()
        {
            PlayerSettings.productName = "Grill Sort";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.Android, ManagedStrippingLevel.High);
            PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.iOS, ManagedStrippingLevel.High);
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            SetAppIcon();
        }

        // App icon (user pick 2026-11-21): the "Grill Sort" text logo on the loading-screen sky (icon_game_v4_text_logo;
        // the earlier "three trays" icon is ArtSource/Icon/icon_game_v1_three_trays.png), composed by
        // Tools/ArtPipeline/compose_icon.py (ArtSource/Icon/icon_game_v1_three_trays.png). Lives outside Art/ so it is
        // imported as a plain texture, not a sprite.
        const string IconDir = Root + "Icon/";

        static void SetAppIcon()
        {
            Texture2D Tex(string file)
            {
                string path = IconDir + file;
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) return null;
                if (ti.textureType != TextureImporterType.Default || ti.mipmapEnabled || ti.textureCompression != TextureImporterCompression.Uncompressed || ti.maxTextureSize != 1024)
                {
                    ti.textureType = TextureImporterType.Default; ti.mipmapEnabled = false;
                    ti.textureCompression = TextureImporterCompression.Uncompressed; ti.maxTextureSize = 1024;
                    ti.alphaIsTransparency = true; ti.SaveAndReimport();
                }
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            var icon = Tex("app_icon.png");
            if (icon == null) { Debug.LogWarning("[Grill Sort] No app icon at " + IconDir); return; }
            // default icon: iOS and every size Unity derives from it
            PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            // Android: legacy + round use the full icon; adaptive (Android 8+) uses the padded background layer
            var android = UnityEditor.Build.NamedBuildTarget.Android;
            foreach (var kind in new[] { UnityEditor.Android.AndroidPlatformIconKind.Legacy, UnityEditor.Android.AndroidPlatformIconKind.Round })
            {
                var icons = PlayerSettings.GetPlatformIcons(android, kind);
                foreach (var ic in icons) ic.SetTexture(icon);
                PlayerSettings.SetPlatformIcons(android, kind, icons);
            }
            var bg = Tex("app_icon_adaptive_bg.png"); var fg = Tex("app_icon_adaptive_fg.png");
            if (bg != null && fg != null)
            {
                var kind = UnityEditor.Android.AndroidPlatformIconKind.Adaptive;
                var icons = PlayerSettings.GetPlatformIcons(android, kind);
                foreach (var ic in icons) ic.SetTextures(bg, fg);
                PlayerSettings.SetPlatformIcons(android, kind, icons);
            }
        }

        // ------------------------------------------------------------------ scene
        /// <summary>Loads a sprite under Art/; "UI/..." paths read the softened set (<see cref="UiDir"/>).</summary>
        static Sprite S(string rel) => Load<Sprite>(Art + (rel.StartsWith("UI/") ? UiDir + rel.Substring(3) : rel));

        static void BuildScene(LevelBank bank)
        {
            Directory.CreateDirectory(Root + "Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // camera (clears behind the overlay canvas)
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(38, 20, 10, 255);
            camGo.transform.position = new Vector3(0, 0, -10);
            camGo.AddComponent<AudioListener>();

            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var audioGo = new GameObject("Audio");
            var am = audioGo.AddComponent<AudioManager>();
            // 8 effect voices (each sound gets its own pitch), music and the sizzle ambience
            am.voices = new AudioSource[8];
            for (int i = 0; i < am.voices.Length; i++) { am.voices[i] = audioGo.AddComponent<AudioSource>(); am.voices[i].playOnAwake = false; }
            am.music = audioGo.AddComponent<AudioSource>(); am.music.playOnAwake = false; am.music.loop = true; am.music.priority = 0;
            am.ambience = audioGo.AddComponent<AudioSource>(); am.ambience.playOnAwake = false; am.ambience.loop = true;

            // canvas
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = 5;
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;
            canvasGo.AddComponent<CanvasAspect>();
            var ui = canvasGo.AddComponent<GameUi>();
            var croot = canvasGo.transform;

            // ---------------- world (full screen, under the notch)
            var world = Stretch("World", croot);
            var layout = world.gameObject.AddComponent<StageLayout>();
            layout.world = world;
            var theme = _art.themes[0];
            var bg = Img("Background", world, theme.Back, new Vector2(1280, 1920), Vector2.zero);
            var customers = Rect("Customers", world, new Vector2(1080, 10), Vector2.zero);
            var front = Img("FrontLayer", world, theme.front, new Vector2(1280, 1920), Vector2.zero);
            var grills = Rect("Grills", world, new Vector2(1000, 800), Vector2.zero);
            var counterOverlay = Rect("CounterOverlay", world, new Vector2(1080, 10), Vector2.zero);
            ui.background = bg; ui.front = front; ui.layout = layout;
            layout.background = bg.rectTransform; layout.front = front.rectTransform;
            layout.customers = customers; layout.counterOverlay = counterOverlay; layout.grills = grills;
            layout.counterTop = theme.counterTop; layout.counterBottom = theme.counterBottom;

            // customers (positions relative to the counter line, design px)
            var bubbleSp = S("UI/Panels/bubble_order.png"); var check = S("UI/Icons/icon_check.png");
            // one customer above each order tray (Sort Streak style, Aaron 2026-11-18); StageLayout lines them up
            ui.customers = new CustomerView[LevelGenerator.MaxSkewers];
            layout.customerSlots = new RectTransform[LevelGenerator.MaxSkewers];
            for (int i = 0; i < ui.customers.Length; i++)
            {
                var slot = Rect($"Customer{i}", customers, new Vector2(260, 10), new Vector2((i - 2) * 200f, 0));
                var cv = slot.gameObject.AddComponent<CustomerView>();
                cv.rect = slot;
                cv.group = slot.gameObject.AddComponent<CanvasGroup>();
                cv.group.blocksRaycasts = false;
                cv.animal = Img("Body", slot, _art.customers[i], new Vector2(250, 250), new Vector2(0, CustomerView.BodyY));
                cv.body = cv.animal.rectTransform;
                float bk = CustomerView.BubbleW / 196f;   // bubble a bit bigger than the mockup's 196 to hold a small skewer
                cv.bubble = Rect("Bubble", slot, new Vector2(CustomerView.BubbleW, 119 * bk), new Vector2(0, CustomerView.BubbleY));
                var bsh = Img("Shadow", cv.bubble, S("Generated/glow.png"), new Vector2(190 * bk, 40 * bk), new Vector2(0, -52 * bk));
                bsh.color = new Color(.08f, .03f, 0f, .35f);
                ImgW("Bubble", cv.bubble, bubbleSp, CustomerView.BubbleW, Vector2.zero);
                // the order is a small skewer with the wanted pieces (Aaron 2026-11-21); GameController sizes it per level
                cv.order = BuildSkewer("Order", cv.bubble, new Vector2(0, CustomerView.OrderBoxY), (i * 3) % _art.foods.Length);
                cv.order.SetTilt(CustomerView.OrderTilt);
                float k = cv.order.FitScale(CustomerView.OrderTilt, CustomerView.OrderBoxW, CustomerView.OrderBoxH);
                cv.order.rect.localScale = new Vector3(k, k, 1f);
                cv.check = ImgW("Check", cv.bubble, check, 62 * bk, new Vector2(80, 46) * bk);
                cv.check.gameObject.SetActive(false);
                // mood emote (heart / sweat / anger / yummy) next to the head, above everything in the slot
                cv.emote = ImgW("Emote", slot, _art.emoteHeart, 84, CustomerView.EmotePos);
                cv.emote.gameObject.SetActive(false);
                ui.customers[i] = cv;
                layout.customerSlots[i] = slot;
            }

            // grills: customer trays + 2 helper trays + 1 for the Extra tray booster
            int grillCount = LevelGenerator.MaxGrills;
            ui.grills = new GrillView[grillCount];
            layout.grillSlots = new RectTransform[grillCount];
            var softRect = S("Generated/soft_rect.png"); var glowSp = S("Generated/glow.png");
            var steam = S("VFX/fx_steam.png"); var spark = S("VFX/fx_sparkle.png"); var perfect = S("UI/Words/word_perfect.png");
            for (int g = 0; g < grillCount; g++)
            {
                var root = Rect($"Grill{g}", grills, new Vector2(196, 800), new Vector2((g - 2) * 200, 0));
                var hit = root.gameObject.AddComponent<Image>();
                hit.color = new Color(0, 0, 0, 0); hit.raycastTarget = true;
                var gv = root.gameObject.AddComponent<GrillView>();
                gv.index = g; gv.rect = root;
                gv.shadow = Img("Shadow", root, softRect, new Vector2(GrillView.GrillW * 1.65f, 929), new Vector2(2, -20));
                gv.shadow.color = new Color(1, 1, 1, .42f);
                gv.ember = Img("Ember", root, glowSp, new Vector2(300, 940), Vector2.zero);
                gv.ember.color = new Color32(255, 110, 30, 80);
                gv.glow = Img("Glow", root, glowSp, new Vector2(420, 1080), Vector2.zero);
                gv.glow.color = new Color32(255, 205, 70, 0);
                gv.highlight = Img("Highlight", root, glowSp, new Vector2(330, 980), Vector2.zero);
                gv.highlight.color = new Color(1f, .97f, .85f, 0f);
                // glow bed first, the grill (frame, coals, bars) on top: the glow shows through the gaps between the coals
                gv.coalGlow = Img("CoalGlow", root, _art.grillDarkGlow, new Vector2(GrillView.GrillW, GrillView.GrillH), Vector2.zero);
                gv.coalHotA = Img("CoalHotA", root, _art.grillDarkHotA, new Vector2(GrillView.GrillW, GrillView.GrillH), Vector2.zero);
                gv.coalHotB = Img("CoalHotB", root, _art.grillDarkHotB, new Vector2(GrillView.GrillW, GrillView.GrillH), Vector2.zero);
                gv.grill = Img("Grill", root, _art.grillDark, new Vector2(GrillView.GrillW, GrillView.GrillH), Vector2.zero);
                // fire sparks removed (Aaron 2026-11-18: too busy, add back later as particles)
                gv.flames = new Image[0];
                float skH = GrillView.GrillH - 20; float skW = skH * 97f / 1869f;
                gv.skewer = Img("Skewer", root, _art.skewer, new Vector2(skW, skH), new Vector2(0, 6));
                int slotCount = LevelGenerator.MaxCapacity;
                gv.slots = new RectTransform[slotCount]; gv.foods = new Image[slotCount]; gv.foodShadows = new Image[slotCount];
                GrillView.SetCapacity(slotCount);
                for (int s = 0; s < slotCount; s++)
                {
                    var slot = Rect($"Slot{s}", root, new Vector2(180, 160), new Vector2(0, GrillView.SlotY[s]));
                    var fa = _art.foods[(g + s) % _art.foods.Length];
                    float w = fa.width * GrillView.FoodScale;
                    gv.foodShadows[s] = Img("Shadow", slot, fa.shadow, new Vector2(w, w) * GrillView.ShadowRatio, GrillView.ShadowOffset);
                    gv.foods[s] = Img("Food", slot, fa.grilled, new Vector2(w, w), Vector2.zero);
                    gv.slots[s] = slot;
                }
                gv.steam = new[]
                {
                    ImgH("Steam0", root, steam, 150, new Vector2(-34, 317)),
                    ImgH("Steam1", root, steam, 170, new Vector2(30, 347)),
                };
                var sp = new (float x, float y, float w)[] { (-92, 247, 70), (96, 97, 56), (-88, -173, 60), (92, -313, 74), (60, 367, 46) };
                gv.sparkles = new Image[sp.Length];
                for (int k = 0; k < sp.Length; k++) gv.sparkles[k] = ImgW($"Sparkle{k}", root, spark, sp[k].w, new Vector2(sp[k].x, sp[k].y));
                gv.perfect = ImgW("Perfect", root, perfect, 300, new Vector2(30, -381));
                // smoke wisps spread along the whole grill (Aaron 2026-11-18: lighter, along the length)
                gv.smokes = new Image[GrillView.SmokeCount];
                for (int k = 0; k < gv.smokes.Length; k++)
                {
                    gv.smokes[k] = ImgH($"Smoke{k}", root, S("VFX/fx_smoke.png"), 150, Vector2.zero);
                    gv.smokes[k].color = new Color(1, 1, 1, 0);
                    gv.smokes[k].gameObject.SetActive(false);
                }
                foreach (var st in gv.steam) st.gameObject.SetActive(false);
                foreach (var st in gv.sparkles) st.gameObject.SetActive(false);
                gv.perfect.gameObject.SetActive(false);
                root.gameObject.SetActive(g < 5);
                ui.grills[g] = gv;
                layout.grillSlots[g] = root;
            }

            // fly layer: pieces that travel between trays / to customers (a dragged stack can be a whole tray)
            var fly = Stretch("FlyLayer", grills);
            fly.anchorMin = fly.anchorMax = new Vector2(.5f, .5f); fly.sizeDelta = Vector2.zero;
            ui.flyLayer = fly;
            int flyerCount = LevelGenerator.MaxCapacity;
            ui.flyers = new RectTransform[flyerCount]; ui.flyerFoods = new Image[flyerCount]; ui.flyerShadows = new Image[flyerCount];
            for (int f = 0; f < flyerCount; f++)
            {
                var fr = Rect($"Flyer{f}", fly, new Vector2(180, 160), Vector2.zero);
                var fa = _art.foods[f];
                float w = fa.width * GrillView.FoodScale;
                ui.flyerShadows[f] = Img("Shadow", fr, fa.shadow, new Vector2(w, w) * GrillView.ShadowRatio, GrillView.ShadowOffset);
                ui.flyerFoods[f] = Img("Food", fr, fa.grilled, new Vector2(w, w), Vector2.zero);
                ui.flyers[f] = fr;
                fr.gameObject.SetActive(false);
            }
            // the small skewer a finished order flies up on (drawn above the trays and the counter)
            ui.serveSkewer = BuildSkewer("ServeSkewer", fly, Vector2.zero, 0);
            ui.serveSkewer.gameObject.SetActive(false);


            // ---------------- safe area: HUD + boosters + toast
            var safe = Stretch("SafeArea", croot);
            safe.gameObject.AddComponent<SafeArea>();
            layout.safeArea = safe;
            var top = new Vector2(.5f, 1f);
            var hud = S("UI/Panels/bar_hud.png");

            var pause = ImgW("Pause", safe, S("UI/Buttons/btn_pause.png"), 112, new Vector2(92, -106), new Vector2(0, 1));
            ui.pauseButton = Button(pause);

            var lvlPill = Sliced("LevelPill", safe, hud, new Vector2(300, 93), new Vector2(0, -100), top);
            ui.levelText = Text("Text", lvlPill.transform, "Level 1", 52, Brown, new Vector2(300, 93), new Vector2(0, 2), _matPlain);
            ui.levelText.textWrappingMode = TextWrappingModes.NoWrap;

            var coinPill = Sliced("CoinPill", safe, hud, new Vector2(250, 78), new Vector2(-180, -100), new Vector2(1, 1));
            ui.coinText = Text("Text", coinPill.transform, "250", 44, Brown, new Vector2(170, 78), new Vector2(18, 2), _matPlain);
            ui.coinText.textWrappingMode = TextWrappingModes.NoWrap;
            var coinIcon = ImgW("Coin", coinPill.transform, S("UI/Icons/icon_coin.png"), 86, new Vector2(-110, 2));
            ui.coinIcon = coinIcon.rectTransform;

            float barW = 330f, barScale = barW / 842f;
            var bar = Img("OrdersBar", safe, S("Generated/bar_track_clean.png"), new Vector2(barW, 185 * barScale), new Vector2(0, -186), top);
            var fill = Sliced("Fill", bar.transform, S("Generated/" + BarFill), new Vector2(200, 106 * barScale), Vector2.zero);
            var frt = fill.rectTransform;
            frt.anchorMin = frt.anchorMax = new Vector2(0, .5f); frt.pivot = new Vector2(0, .5f);
            frt.anchoredPosition = new Vector2(43 * barScale, (92.5f - 91.5f) * barScale);
            ui.ordersFill = frt;
            ui.ordersFillMax = (842 - 86) * barScale;
            ui.ordersText = Text("Text", bar.transform, "Orders 0 / 4", 30, Color.white, new Vector2(barW, 60), new Vector2(0, 1), _matGreenOutline);
            ui.ordersText.textWrappingMode = TextWrappingModes.NoWrap;

            // boosters
            var tile = S("UI/Buttons/btn_tile_yellow.png"); var badge = S("Generated/badge.png");
            string[] icons = { "icon_undo", "icon_hint", "icon_shuffle", "icon_add_skewer" };
            int[] counts = { 3, 2, 1, 1 };
            ui.boosters = new UnityEngine.UI.Button[4]; ui.boosterCounts = new TMP_Text[4];
            var bottom = new Vector2(.5f, 0f);
            // a wooden shelf the booster tiles stand on (2026-11-23, review: the strip under the trays looked empty)
            ui.boosterShelf = ImgW("BoosterShelf", safe, S(MetaDir + "shelf.png"), 1010, new Vector2(0, 46), bottom);
            for (int i = 0; i < 4; i++)
            {
                float x = -300 + i * 200;
                var sh = Img($"BoosterShadow{i}", safe, softRect, new Vector2(170 * 1.5f, 160 * 1.25f), new Vector2(x + 3, 130 - 16), bottom);
                sh.color = new Color(1, 1, 1, .45f);
                var t = ImgW($"Booster{i}", safe, tile, 170, new Vector2(x, 130), bottom);
                ImgW("Icon", t.transform, S($"UI/Icons/{icons[i]}.png"), 104, new Vector2(0, 6));
                var bd = Img("Badge", t.transform, badge, new Vector2(60, 60), new Vector2(66, -58));
                ui.boosterCounts[i] = Text("Count", bd.transform, counts[i].ToString(), 32, Color.white, new Vector2(60, 60), new Vector2(0, 1), _matPlain);
                ui.boosterCounts[i].textWrappingMode = TextWrappingModes.NoWrap;
                ui.boosters[i] = Button(t);
            }

            // one message strip just above the boosters, shared by tips (levels 1-3) and short messages, so nothing
            // ever covers the trays, the food or the customers (StageLayout keeps the grills above it)
            var tipRt = Rect("Tip", safe, new Vector2(MsgW, MsgH), new Vector2(0, MsgY), bottom);
            ui.tip = tipRt.gameObject.AddComponent<CanvasGroup>();
            ui.tip.blocksRaycasts = false;
            ui.tip.alpha = 0f;
            // tutorial banner like the client's FishUp! ("Tap fish to fill the bowl!"): a white pill with bold navy text
            var tipSh = Sliced("Shadow", tipRt, S("Generated/rounded.png"), new Vector2(MsgW, MsgH), new Vector2(0, -8));
            tipSh.color = new Color(.05f, .1f, .25f, .22f);
            var tipBg = Sliced("Bg", tipRt, S("Generated/rounded.png"), new Vector2(MsgW, MsgH), Vector2.zero);
            tipBg.color = Color.white;
            ui.tipText = Text("Text", tipRt, "Tip", 40, Navy, new Vector2(MsgW - 50, MsgH - 8), new Vector2(0, 2), _matPlain);

            var toastRt = Rect("Toast", safe, new Vector2(MsgW, MsgH), new Vector2(0, MsgY), bottom);
            ui.toast = toastRt.gameObject.AddComponent<CanvasGroup>();
            ui.toast.blocksRaycasts = false;
            ui.toast.alpha = 0f;
            var toastSh = Sliced("Shadow", toastRt, S("Generated/rounded.png"), new Vector2(MsgW, MsgH), new Vector2(0, -8));
            toastSh.color = new Color(.05f, .1f, .25f, .22f);
            var toastBg = Sliced("Bg", toastRt, S("Generated/rounded.png"), new Vector2(MsgW, MsgH), Vector2.zero);
            toastBg.color = Color.white;
            ui.toastText = Text("Text", toastRt, "Toast", 36, Navy, new Vector2(MsgW - 50, MsgH - 8), new Vector2(0, 2), _matPlain);

            // ---------------- tutorial hand (above the board and HUD, under the popups)
            BuildHand(ui, croot);

            // ---------------- home screen (meta): over the game, under the popups and the loading screen
            BuildHome(ui, croot);

            // ---------------- popups
            var popups = Stretch("Popups", croot);
            BuildWinPopup(ui, popups);
            BuildPausePopup(ui, popups);
            BuildStuckPopup(ui, popups);
            BuildMessagePopup(ui, popups);
            BuildDailyPopup(ui, popups);
            BuildDecorPopup(ui, popups);

            // ---------------- loading screen (on top of everything, visible from the first frame)
            BuildLoading(ui, croot);

            // ---------------- game
            var gameGo = new GameObject("Game");
            var gc = gameGo.AddComponent<GameController>();
            gc.art = _art; gc.bank = bank; gc.ui = ui; gc.audioManager = am;

            layout.activeGrills = 5;
            layout.Apply();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
        }

        // Loading screen (2026-11-21, user: like the client's other game, FishUp!). v1 (theme background + grill/sign
        // stickers + text letters) was rejected as a collage, so v2 is built like theirs: ONE painted full-screen scene
        // (Loading/keyart_default.png; a theme can override it with ThemeDef.loadingArt), a real logo image (placeholder
        // until the name is final) and a mascot (sausage chef) that pops in and hops off. Steam and embers rise from the
        // painted grill; they are children of the cover-fit background, placed in its normalised coordinates, so they stay
        // on the grill on any aspect. Logo / mascot / bar sit inside the safe area.
        // The iKame splash is Unity's own startup splash (Project Settings > Player > Splash Image), set up once by hand /
        // by Claude 2026-11-22: logo = iKame Flame Mascot Logo, background = Loading/splash_bg.png (purple + glow, from the
        // client's reference). Run Full Setup does NOT touch it, so changes made there stay.
        static void BuildLoading(GameUi ui, Transform croot)
        {
            var root = Stretch("Loading", croot);
            ui.loading = root.gameObject.AddComponent<CanvasGroup>();
            var bgBack = Img("Back", root, null, Vector2.zero, Vector2.zero, null, true);   // blocks input while loading
            var brt = bgBack.rectTransform; brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = brt.offsetMax = Vector2.zero;
            bgBack.color = new Color32(38, 20, 10, 255);
            ui.loadingBg = Img("Background", root, _art.loadingKeyArt, new Vector2(1024, 1536), Vector2.zero);
            var fit = ui.loadingBg.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = 1024f / 1536f;
            var bg = ui.loadingBg.rectTransform;
            // steam + embers over the painted grill (normalised art coords, from the bottom-left)
            var steam = S("VFX/fx_steam.png");
            ui.loadingSteam = new Image[4];
            for (int i = 0; i < ui.loadingSteam.Length; i++)
            {
                ui.loadingSteam[i] = ImgH($"Steam{i}", bg, steam, 230, Vector2.zero, new Vector2(.34f + i * .1f, .6f));
                ui.loadingSteam[i].color = new Color(1, 1, 1, 0);
            }
            var ember = S("Generated/glow.png");
            ui.loadingEmbers = new Image[12];
            for (int i = 0; i < ui.loadingEmbers.Length; i++)
            {
                ui.loadingEmbers[i] = Img($"Ember{i}", bg, ember, Vector2.one * (20 + (i * 7) % 18), Vector2.zero, new Vector2(.3f + (i * .037f) % .4f, .56f));
                ui.loadingEmbers[i].color = new Color(1f, .62f, .18f, 0f);
            }

            var content = Stretch("Content", root);
            content.gameObject.AddComponent<SafeArea>();
            var top = new Vector2(.5f, 1f); var bottom = new Vector2(.5f, 0f);

            ui.loadingRays = Img("Rays", content, S("Generated/rays.png"), new Vector2(1500, 1500), new Vector2(0, -400), top);
            ui.loadingRays.color = new Color(1f, .92f, .6f, .22f);   // subtle: the user found the screen too busy
            ui.loadingLogo = Img("Logo", content, _art.logo, new Vector2(566, 560), new Vector2(-24, -390), top).rectTransform;   // logo.png is 1008x998 (grill-lid topper); clear of the grill
            ui.loadingMascot = Img("Mascot", content, _art.mascot, new Vector2(210, 346), new Vector2(330, -640), top).rectTransform;
            ui.loadingMascot.pivot = new Vector2(.5f, 0f);   // bounces / squashes from the feet
            var spark = S("VFX/fx_sparkle.png");
            var sparkSpots = new Vector2[] { new(-420, -260), new(400, -230), new(-360, -760), new(260, -820), new(-120, -170), new(120, -880) };
            ui.loadingSparkles = new Image[sparkSpots.Length];
            for (int i = 0; i < sparkSpots.Length; i++)
            {
                ui.loadingSparkles[i] = ImgW($"Sparkle{i}", content, spark, i % 2 == 0 ? 70 : 52, sparkSpots[i], top);
                ui.loadingSparkles[i].color = new Color(1, 1, 1, 0);
            }

            // loading bar on the table at the bottom (clean track: bar_track had a baked-in ghost fill)
            float barW = 660f, k = barW / 842f;
            var bar = Img("Bar", content, S("Generated/bar_track_clean.png"), new Vector2(barW, 185 * k), new Vector2(0, 230), bottom);
            var fill = Sliced("Fill", bar.transform, S("Generated/" + BarFill), new Vector2(120, 106 * k), Vector2.zero);
            var frt = fill.rectTransform;
            frt.anchorMin = frt.anchorMax = new Vector2(0, .5f); frt.pivot = new Vector2(0, .5f);
            frt.anchoredPosition = new Vector2(43 * k, (92.5f - 91.5f) * k);
            ui.loadingBarFill = frt;
            ui.loadingBarMax = (842 - 86) * k;
            ui.loadingText = Text("Text", content, "Loading", 54, Color.white, new Vector2(600, 80), new Vector2(0, 350), _matBrownOutline, bottom);
            ui.loadingText.textWrappingMode = TextWrappingModes.NoWrap;
        }

        // Tutorial hand (2026-11-21): a cartoon glove that shows the move (press on the piece, drag to the tray, release).
        // Pivot = the fingertip (Tools/ArtPipeline/cut_hand.py), so it points exactly at the target.
        // v4 glove (back of the hand, finger up-left at 45 degrees, like the client's FishUp! tap hand): fingertip pivot
        // from Tools/ArtPipeline/cut_hand.py
        static readonly Vector2 HandPivot = new Vector2(.078f, .9f);   // Pngtree glove (user pick), glow removed: Tools/ArtPipeline/cut_hand_pngtree.py

        static void BuildHand(GameUi ui, Transform croot)
        {
            var layer = Stretch("HandLayer", croot);
            ui.handLayer = layer;
            ui.handRing = Img("Ring", layer, S("Generated/glow.png"), new Vector2(150, 150), Vector2.zero);
            ui.handRing.color = new Color(1, 1, 1, 0);
            ui.handGhost = Img("Ghost", layer, _art.foods[0].grilled, new Vector2(150, 150), Vector2.zero);
            ui.handGhost.color = new Color(1, 1, 1, 0);
            // the glove: a soft shadow under it (it is white, and sits on light food), both faded by one CanvasGroup
            var hand = Rect("Hand", layer, new Vector2(GameController.HandSize, GameController.HandSize), Vector2.zero);
            hand.pivot = HandPivot;
            ui.handGroup = hand.gameObject.AddComponent<CanvasGroup>();
            ui.handGroup.blocksRaycasts = false; ui.handGroup.interactable = false;
            var sh = Img("Shadow", hand, S("UI/Icons/icon_hand.png"), new Vector2(GameController.HandSize, GameController.HandSize), new Vector2(8, -14));
            sh.color = new Color(.1f, .12f, .25f, .28f);
            ui.handImage = Img("Glove", hand, S("UI/Icons/icon_hand.png"), new Vector2(GameController.HandSize, GameController.HandSize), Vector2.zero);
            ui.hand = hand;
            ui.hand.gameObject.SetActive(false);
        }

        /// <summary>A small skewer (stick + one image per piece), see <see cref="SkewerStack"/>.</summary>
        static SkewerStack BuildSkewer(string name, Transform parent, Vector2 pos, int food)
        {
            var rt = Rect(name, parent, new Vector2(10, 10), pos);
            var sk = rt.gameObject.AddComponent<SkewerStack>();
            sk.rect = rt;
            sk.stick = Img("Stick", rt, _art.skewer, new Vector2(SkewerStack.StickWidth, 400), Vector2.zero);
            sk.foods = new Image[LevelGenerator.MaxCapacity];
            for (int i = 0; i < sk.foods.Length; i++)
                sk.foods[i] = Img($"Food{i}", rt, _art.foods[food].grilled, new Vector2(150, 150), Vector2.zero);
            sk.Set(_art.foods[food], 4, _art.foods[food].width);
            return sk;
        }

        static (CanvasGroup group, RectTransform panel) Popup(string name, Transform parent, float width, string title, out TextMeshProUGUI titleText)
        {
            var root = Stretch(name, parent);
            var cg = root.gameObject.AddComponent<CanvasGroup>();
            var dim = Img("Dim", root, null, Vector2.zero, Vector2.zero, null, true);
            var drt = dim.rectTransform; drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.offsetMin = drt.offsetMax = Vector2.zero;
            dim.color = new Color(.05f, .02f, 0f, .76f);   // .66 let the trays show through too much behind popups
            var sp = S("UI/Panels/popup_panel.png");
            float h = width * sp.rect.height / sp.rect.width;
            var panel = Rect("Panel", root, new Vector2(width, h), new Vector2(0, 20));
            var pimg = panel.gameObject.AddComponent<Image>(); pimg.sprite = sp; pimg.raycastTarget = true;
            float sc = width / sp.rect.width;
            titleText = Text("Title", panel, title, 64, Color.white, new Vector2(width * .7f, 110), new Vector2(0, h / 2 - 84 * sc), _matBrownOutline);   // ribbon band centre is 81.5 px from the top of popup_panel (measured)
            titleText.textWrappingMode = TextWrappingModes.NoWrap;
            root.gameObject.SetActive(false);
            return (cg, panel);
        }

        static UnityEngine.UI.Button WideButton(string name, Transform parent, string sprite, string label, Vector2 size, Vector2 pos, out TextMeshProUGUI text)
        {
            var img = Sliced(name, parent, S("UI/Buttons/" + sprite + ".png"), size, pos, null, true);
            text = Text("Label", img.transform, label, size.y * .38f, Color.white, size - new Vector2(40, 10), new Vector2(0, 4), _matBrownOutline);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return Button(img);
        }

        // Win screen, modelled on the client's FishUp! "Well Done!" (user 2026-11-21; their extras left out: no "Add Slot"
        // bar, no locked-level row, no ad banner; our old wooden panel and stars removed too). Full screen over the dimmed
        // level: our logo pops and bursts, "Well Done!" drops in, a glowing bubble of coins, a multiplier bar (x2 x3 x5 x3
        // x2) with the sausage mascot sliding along it, "Claim" and "Claim xN" (ad). Coins fly into the coin pill.

        static void BuildWinPopup(GameUi ui, Transform parent)
        {
            var root = Stretch("WinPopup", parent);
            ui.winPopup = root.gameObject.AddComponent<CanvasGroup>();
            ui.winDim = Img("Dim", root, null, Vector2.zero, Vector2.zero, null, true);
            var drt = ui.winDim.rectTransform; drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.offsetMin = drt.offsetMax = Vector2.zero;
            ui.winDim.color = new Color(.04f, .025f, .02f, .95f);   // linear colour space: needs ~.95 to read as a strong dim

            var safe = Stretch("Safe", root);
            safe.gameObject.AddComponent<SafeArea>();
            var content = Rect("Content", safe, new Vector2(1080, 1920), Vector2.zero);
            ui.winPanel = content;

            // reward bubble: turning rays, warm glow, a pile of coins, sparkles
            var bubble = Rect("Bubble", content, new Vector2(460, 460), new Vector2(0, 170));
            ui.winBubble = bubble;
            // smooth 1024 px glow + rays (user 2026-11-22: the stretched whitish disc looked low quality); no disc
            ui.winRays = Img("Rays", bubble, S("Generated/rays_hd.png"), new Vector2(950, 950), Vector2.zero);
            ui.winRays.color = new Color(1f, .86f, .42f, .45f);
            ui.winGlow = Img("Glow", bubble, S("Generated/glow_hd.png"), new Vector2(900, 900), Vector2.zero);
            ui.winGlow.color = new Color(1f, .72f, .22f, .85f);
            var core = Img("Core", bubble, S("Generated/glow_hd.png"), new Vector2(460, 460), Vector2.zero);
            core.color = new Color(1f, .93f, .62f, .55f);   // warm bright centre behind the coins
            var coin = S("UI/Icons/icon_coin.png");
            var pile = new (float x, float y, float w, float r)[] { (-70, -55, 170, -12), (75, -60, 165, 10), (0, -20, 185, 0), (-35, 60, 160, -6), (55, 45, 150, 14) };
            foreach (var p in pile)
            {
                var c = ImgW("Coin", bubble, coin, p.w * 1.2f, new Vector2(p.x, p.y) * 1.2f);
                c.rectTransform.localRotation = Quaternion.Euler(0, 0, p.r);
            }
            var spark = S("VFX/fx_sparkle.png");
            var spots = new Vector2[] { new(-230, 180), new(215, 200), new(-250, -130), new(240, -155), new(-70, 275), new(110, -265) };
            ui.winSparkles = new Image[spots.Length];
            for (int i = 0; i < spots.Length; i++)
            {
                ui.winSparkles[i] = ImgW($"Sparkle{i}", bubble, spark, i % 2 == 0 ? 80 : 56, spots[i]);
                ui.winSparkles[i].color = new Color(1, 1, 1, 0);
            }

            // "Well Done!": big bubbly text in the logo colours (TMP, no new art)
            var word = Text("WellDone", content, "Well Done!", 158, Color.white, new Vector2(1000, 220), new Vector2(0, 600), _matBrownOutline);
            word.textWrappingMode = TextWrappingModes.NoWrap;
            word.enableVertexGradient = true;
            word.colorGradient = new VertexGradient(new Color32(255, 236, 110, 255), new Color32(255, 236, 110, 255), new Color32(255, 150, 30, 255), new Color32(255, 150, 30, 255));
            ui.winWord = word.rectTransform;

            // (no logo on the win screen: user 2026-11-21 "it should just show the win screen")

            // multiplier bar: white rounded frame, five coloured segments clipped to the rounded inner shape
            // even layout (user: spacing): bar and buttons share the same 820 width, so left/right margins match; vertical gaps
            // Well Done -> coins ~90, coins -> pointer ~40, bar -> buttons ~80
            var bar = Rect("MultBar", content, new Vector2(820, 124), new Vector2(0, -285));
            ui.winMultBar = bar;
            var frame = Sliced("Frame", bar, S("Generated/rounded.png"), new Vector2(820, 124), Vector2.zero);
            frame.color = Color.white;
            var inner = Sliced("Inner", bar, S("Generated/rounded.png"), new Vector2(800, 104), Vector2.zero);
            inner.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var cols = new Color32[] { new(46, 120, 232, 255), new(255, 196, 40, 255), new(226, 46, 46, 255), new(255, 196, 40, 255), new(46, 120, 232, 255) };
            float segW = 800f / GameController.WinMults.Length;
            for (int i = 0; i < GameController.WinMults.Length; i++)
            {
                float x = -400 + segW * (i + .5f);
                var seg = Img($"Seg{i}", inner.transform, null, new Vector2(segW + 1, 104), new Vector2(x, 0));
                seg.color = cols[i];
                var lab = Text($"x{i}", bar, $"x{GameController.WinMults[i]}", 62, Color.white, new Vector2(segW, 104), new Vector2(x, 2), _matBrownOutline);
                lab.textWrappingMode = TextWrappingModes.NoWrap;
            }
            // the sausage mascot slides along the bar and points at the current multiplier
            var ptr = Img("Pointer", bar, _art.mascot, new Vector2(74, 122), new Vector2(0, 54));
            ptr.rectTransform.pivot = new Vector2(.5f, 0f);
            ui.winPointer = ptr.rectTransform;

            // claim buttons: a title row and a coin + amount row, each laid out as ONE centred group (user: the coin and
            // the number sat far apart and the AD tag crowded the title)
            var bsz = new Vector2(400, 160);
            ui.winNext = ClaimButton(content, "Claim", "btn_green", bsz, new Vector2(-210, -505), false, out _, out var claimAmt);
            ui.winDouble = ClaimButton(content, "ClaimX", "btn_orange", bsz, new Vector2(210, -505), true, out var dblTitle, out var dblAmt);
            ui.winCoins = claimAmt; ui.winDoubleLabel = dblTitle; ui.winDoubleAmount = dblAmt;

            // coin pill (same place as the HUD one, above the dim) that the coins fly into and that counts up
            var pill = Sliced("CoinPill", safe, S("UI/Panels/bar_hud.png"), new Vector2(250, 78), new Vector2(-180, -100), new Vector2(1, 1));
            ui.winCoinPill = pill.rectTransform;
            ui.winCoinPillText = Text("Text", pill.transform, "250", 44, Brown, new Vector2(170, 78), new Vector2(18, 2), _matPlain);
            ui.winCoinPillText.textWrappingMode = TextWrappingModes.NoWrap;
            ui.winCoinPillIcon = ImgW("Coin", pill.transform, coin, 86, new Vector2(-110, 2)).rectTransform;

            // flying coins
            ui.winFlyCoins = new RectTransform[14];
            for (int i = 0; i < ui.winFlyCoins.Length; i++)
            {
                var fc = ImgW($"FlyCoin{i}", root, coin, 72, Vector2.zero);
                ui.winFlyCoins[i] = fc.rectTransform;
                fc.gameObject.SetActive(false);
            }
            root.gameObject.SetActive(false);
        }

        /// <summary>
        /// A claim button: [AD] title on top, coin + amount below. Each row is a HorizontalLayoutGroup that MEASURES its
        /// children (text preferred width, fixed icon width), so the coin never touches the number and every row stays
        /// centred whatever the amount. The two rows are centred on the button face (the sprite's 3D lip is at the bottom).
        /// </summary>
        static UnityEngine.UI.Button ClaimButton(Transform parent, string name, string sprite, Vector2 size, Vector2 pos, bool ad,
            out TextMeshProUGUI title, out TextMeshProUGUI amount)
        {
            var img = Sliced(name, parent, S("UI/Buttons/" + sprite + ".png"), size, pos, null, true);
            // measured on a capture: the coloured face is the top 120 px of the 160 px button (a darker 3D lip below), so its
            // centre is 10 px above the sprite centre. Content = title (cap ~26) + 8 gap + coin row (52) = ~86, giving
            // ~17 px above and below (user: top and bottom spacing must be equal).
            const float faceY = -3f;   // centred on the WHOLE pill as the eye sees it (incl. the 3D lip), checked visually
            RectTransform Row(string n, float y, float h, float spacing)
            {
                var r = Rect(n, img.transform, new Vector2(size.x - 40, h), new Vector2(0, y));
                var lg = r.gameObject.AddComponent<HorizontalLayoutGroup>();
                lg.childAlignment = TextAnchor.MiddleCenter; lg.spacing = spacing;
                lg.childControlWidth = true; lg.childControlHeight = false;
                lg.childForceExpandWidth = lg.childForceExpandHeight = false;
                return r;
            }
            void Fixed(Component c, float w) { var le = c.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = w; le.minWidth = w; }
            var top = Row("TitleRow", faceY + 30, 46, 10);
            if (ad)
            {
                var tag = Sliced("AdTag", top, S("Generated/rounded.png"), new Vector2(60, 36), Vector2.zero);
                tag.color = new Color32(214, 40, 40, 255);
                Text("Text", tag.transform, "AD", 21, Color.white, new Vector2(60, 36), new Vector2(0, 1), _matPlain);
                Fixed(tag, 60);
            }
            title = Text("Label", top, ad ? "Claim x2" : "Claim", 36, Color.white, new Vector2(10, 50), Vector2.zero, _matBrownOutline);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            var bottom = Row("AmountRow", faceY - 17, 60, 12);
            var coin = ImgW("Coin", bottom, S("UI/Icons/icon_coin.png"), 52, Vector2.zero);
            Fixed(coin, 52);
            amount = Text("Amount", bottom, "40", 54, Color.white, new Vector2(10, 60), Vector2.zero, _matBrownOutline);
            amount.textWrappingMode = TextWrappingModes.NoWrap;
            return Button(img);
        }

        // Settings panel v2 (user 2026-11-22: "I need sliders for music and sfx which we can manage in game directly ...
        // match our design, increase the size of the setting page if needed"). Bigger panel (900 wide). Rows on the cream:
        // Music slider, Sound slider, Vibration on/off, Restore Purchases, Restart + Continue. Sliders: dark rounded track,
        // warm fill, cream knob with a brown ring, the value in % on the right.
        // Grid measured on popup_panel at 900 wide: cream area x -347..344, y +302..-351. Content column = cream minus a 40 px
        // margin each side (x -307..304, 610 wide, centre -1). Vertical: 42 px top margin, three 88 px setting rows 100 apart,
        // 40 px gap, Restore (108), 24 px gap, Restart + Continue (108), ~43 px bottom margin.
        const float SetCX = -1f, SetW = 610f, SetL = -307f, SetR = 304f, SetRowH = 88f;

        static void BuildPausePopup(GameUi ui, Transform parent)
        {
            var (cg, panel) = Popup("PausePopup", parent, 900, "Settings", out _);
            ui.pausePopup = cg; ui.pausePanel = panel;
            float h = panel.sizeDelta.y;
            var close = ImgW("Close", panel, S("UI/Icons/btn_close.png"), 108, new Vector2(406, h / 2 - 72), null, true);
            ui.pauseClose = Button(close);

            ui.musicSlider = VolumeRow(panel, "Music", "icon_music", 216, out var mv);
            ui.sfxSlider = VolumeRow(panel, "Sound", "icon_sound", 116, out var sv);
            ui.musicValue = mv; ui.sfxValue = sv;

            // vibration: icon | label (left-aligned with the sliders) | ON/OFF pill on the right edge
            var vRow = Rect("VibrationRow", panel, new Vector2(SetW, SetRowH), new Vector2(SetCX, 16));
            ImgW("Icon", vRow, S("UI/Icons/icon_vibrate.png"), 64, new Vector2(SetL + 36 - SetCX, 0));
            var vl = Text("Label", vRow, "Vibration", 44, Brown, new Vector2(300, SetRowH), new Vector2(SetL + 87 + 150 - SetCX, 2), _matPlain);
            vl.alignment = TextAlignmentOptions.Left; vl.textWrappingMode = TextWrappingModes.NoWrap;
            ui.pauseHaptics = WideButton("Toggle", vRow, "btn_green", "ON", new Vector2(170, 84), new Vector2(SetR - 85 - SetCX, 0), out var vt);
            ui.pauseHapticsText = vt;
            ui.pauseHapticsImage = ui.pauseHaptics.GetComponent<Image>();
            ui.hapticsOnSprite = S("UI/Buttons/btn_green.png"); ui.hapticsOffSprite = S("UI/Buttons/btn_red.png");

            ui.pauseRestore = WideButton("Restore", panel, "btn_orange", "Restore Purchases", new Vector2(SetW, 108), new Vector2(SetCX, -122), out _);
            float bw = (SetW - 24f) / 2f;
            ui.pauseRestart = WideButton("Restart", panel, "btn_red", "Restart", new Vector2(bw, 108), new Vector2(SetCX - bw / 2 - 12, -254), out _);
            ui.pauseResume = WideButton("Continue", panel, "btn_green", "Continue", new Vector2(bw, 108), new Vector2(SetCX + bw / 2 + 12, -254), out _);
        }

        /// <summary>One volume row on the settings grid: icon (left edge) | slider | value (right-aligned to the right edge).</summary>
        static Slider VolumeRow(Transform panel, string name, string icon, float y, out TextMeshProUGUI value)
        {
            var row = Rect(name + "Row", panel, new Vector2(SetW, SetRowH), new Vector2(SetCX, y));
            ImgW("Icon", row, S($"UI/Icons/{icon}.png"), 64, new Vector2(SetL + 36 - SetCX, 0));
            // slider from x -220 to 180 (400 wide); value text right-aligned on the column's right edge
            const float sw = 400f, sx = -20f;
            var sl = Rect("Slider", row, new Vector2(sw, 66), new Vector2(sx - SetCX, 0));
            var slider = sl.gameObject.AddComponent<Slider>();
            var track = Sliced("Track", sl, S("Generated/rounded.png"), new Vector2(sw, 34), Vector2.zero, null, true);
            track.color = new Color32(92, 46, 18, 255);
            var inner = Sliced("Inner", sl, S("Generated/rounded.png"), new Vector2(sw - 8, 26), Vector2.zero);
            inner.color = new Color32(60, 28, 10, 255);
            var fillArea = Rect("FillArea", sl, new Vector2(sw - 8, 26), Vector2.zero);
            var fill = Sliced("Fill", fillArea, S("Generated/rounded.png"), new Vector2(0, 26), Vector2.zero);
            fill.color = new Color32(255, 176, 40, 255);
            var frt = fill.rectTransform; frt.anchorMin = new Vector2(0, 0); frt.anchorMax = new Vector2(0, 1); frt.sizeDelta = Vector2.zero; frt.anchoredPosition = Vector2.zero;
            var handleArea = Rect("HandleArea", sl, new Vector2(sw - 60, 66), Vector2.zero);
            var knob = Img("Handle", handleArea, S("Generated/disc.png"), new Vector2(60, 60), Vector2.zero, null, true);
            knob.color = new Color32(92, 46, 18, 255);
            var knobIn = Img("Inner", knob.transform, S("Generated/disc.png"), new Vector2(46, 46), Vector2.zero);
            knobIn.color = new Color32(255, 240, 205, 255);
            var hrt = knob.rectTransform; hrt.anchorMin = new Vector2(0, 0); hrt.anchorMax = new Vector2(0, 1); hrt.sizeDelta = new Vector2(60, 0);
            slider.fillRect = frt; slider.handleRect = hrt; slider.targetGraphic = knob;
            slider.direction = Slider.Direction.LeftToRight; slider.minValue = 0; slider.maxValue = 1; slider.value = .4f;
            slider.transition = Selectable.Transition.None;
            var nav = slider.navigation; nav.mode = Navigation.Mode.None; slider.navigation = nav;
            value = Text("Value", row, "40%", 40, Brown, new Vector2(100, SetRowH), new Vector2(SetR - 50 - SetCX, 2), _matPlain);
            value.alignment = TextAlignmentOptions.Right; value.textWrappingMode = TextWrappingModes.NoWrap;
            return slider;
        }

        // "Stuck" popup, built like the client's Mahjong / Screwdom "Continue?" (user 2026-11-22: the old 4-button list was
        // too basic). Two pages in one popup:
        //   Offer  : card with the rescue item (Extra tray, or Shuffle once the tray is used) "x1" + caption, then
        //            [coin price] and [AD Free]; small "Undo last move" link; red X -> Give up page.
        //   Give up: card "You'll restart this level", [Retry] [Keep Playing].
        // Grid on popup_panel at 860 wide: cream x -332..329, y +289..-335 (measured), 44 px margins.
        public const int StuckPrice = 150;

        static void BuildStuckPopup(GameUi ui, Transform parent)
        {
            var (cg, panel) = Popup("StuckPopup", parent, 860, "Out of Moves!", out var title);
            ui.stuckPopup = cg; ui.stuckPanel = panel; ui.stuckTitle = title;
            float h = panel.sizeDelta.y;
            ui.stuckClose = Button(ImgW("Close", panel, S("UI/Icons/btn_close.png"), 104, new Vector2(388, h / 2 - 70), null, true));
            const float cx = -1.5f, cw = 572f;

            // ---- offer page
            var offer = Rect("Offer", panel, new Vector2(860, 700), Vector2.zero);
            ui.stuckOffer = offer;
            var card = Sliced("Card", offer, S("Generated/rounded.png"), new Vector2(cw, 280), new Vector2(cx, 108));
            card.color = new Color32(238, 222, 190, 255);
            var tile = Sliced("Tile", offer, S("Generated/rounded.png"), new Vector2(190, 160), new Vector2(cx, 150));
            tile.color = new Color32(214, 186, 140, 255);
            ui.stuckIcon = ImgW("Icon", offer, S("UI/Icons/icon_add_skewer.png"), 128, new Vector2(cx, 152));
            var x1 = Text("x1", offer, "x1", 46, Color.white, new Vector2(100, 60), new Vector2(cx + 92, 92), _matBrownOutline);
            x1.textWrappingMode = TextWrappingModes.NoWrap;
            ui.stuckCaption = Text("Caption", offer, "Add 1 Tray and Continue", 36, Brown, new Vector2(cw - 40, 56), new Vector2(cx, 18), _matPlain);
            ui.stuckCaption.textWrappingMode = TextWrappingModes.NoWrap;

            float bw = (cw - 24f) / 2f;
            var buy = Sliced("Buy", offer, S("UI/Buttons/btn_green.png"), new Vector2(bw, 116), new Vector2(cx - bw / 2 - 12, -112), null, true);
            ImgW("Coin", buy.transform, S("UI/Icons/icon_coin.png"), 62, new Vector2(-62, 4));
            ui.stuckPrice = Text("Price", buy.transform, StuckPrice.ToString(), 54, Color.white, new Vector2(150, 80), new Vector2(34, 4), _matBrownOutline);
            ui.stuckPrice.textWrappingMode = TextWrappingModes.NoWrap;
            ui.stuckBuy = Button(buy);
            var ad = Sliced("Free", offer, S("UI/Buttons/btn_orange.png"), new Vector2(bw, 116), new Vector2(cx + bw / 2 + 12, -112), null, true);
            var tag = Sliced("AdTag", ad.transform, S("Generated/rounded.png"), new Vector2(70, 42), new Vector2(-66, 4));
            tag.color = new Color32(214, 40, 40, 255);
            Text("Text", tag.transform, "AD", 24, Color.white, new Vector2(70, 42), new Vector2(0, 1), _matPlain);
            var ft = Text("Label", ad.transform, "Free", 50, Color.white, new Vector2(150, 80), new Vector2(40, 4), _matBrownOutline);
            ft.textWrappingMode = TextWrappingModes.NoWrap;
            ui.stuckAd = Button(ad);

            var undo = Text("Undo", offer, "<u>Undo last move</u>", 36, new Color32(140, 80, 30, 255), new Vector2(360, 64), new Vector2(cx, -232), _matPlain);
            undo.raycastTarget = true;
            ui.stuckUndo = undo.gameObject.AddComponent<UnityEngine.UI.Button>();
            ui.stuckUndo.transition = Selectable.Transition.None;

            // ---- give-up page
            var quit = Rect("GiveUp", panel, new Vector2(860, 700), Vector2.zero);
            ui.stuckGiveUp = quit;
            var qcard = Sliced("Card", quit, S("Generated/rounded.png"), new Vector2(cw, 280), new Vector2(cx, 108));
            qcard.color = new Color32(238, 222, 190, 255);
            ImgW("Icon", quit, S("UI/Icons/icon_heart.png"), 120, new Vector2(cx, 152));
            var qt = Text("Caption", quit, "You'll restart this level", 38, new Color32(200, 40, 30, 255), new Vector2(cw - 40, 56), new Vector2(cx, 18), _matPlain);
            qt.textWrappingMode = TextWrappingModes.NoWrap;
            ui.stuckRetry = WideButton("Retry", quit, "btn_red", "Retry", new Vector2(bw, 116), new Vector2(cx - bw / 2 - 12, -112), out _);
            ui.stuckKeep = WideButton("Keep", quit, "btn_green", "Resume", new Vector2(bw, 116), new Vector2(cx + bw / 2 + 12, -112), out _);
            quit.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ home screen (meta, 2026-11-23)
        // YOUR stall between levels (review: no reason to come back): the current theme's stall (background + counter,
        // cover-fit exactly like the game), two customers behind the counter, the sausage mascot on it, and the
        // decorations bought in "Decorate" placed in the art's own coordinates (px of the 1024x1536 source, y from the
        // top) so they stay on the stall on every aspect. Bottom: progress to the next stall + Play. Top: settings, coins,
        // Daily (left) and Decorate (right) with red dots when something is waiting.
        /// <summary>Decorations in the art's px (x, y of the bottom centre from the top, height), order = GameController.DecorNames.</summary>
        static readonly (string file, float x, float y, float h, bool hang)[] HomeDecor =
        {
            ("deco_lights", 512, 168, 108, true),    // string lights across the top of the stall (between Daily and Decorate)
            ("deco_plant", 205, 850, 250, false),    // left, on the sand
            ("deco_menu", 330, 990, 205, false),     // left front
            ("deco_torch", 842, 800, 370, false),    // right, tall
            ("deco_smoker", 690, 1000, 225, false),  // right front
            ("deco_sign", 512, 566, 124, true),      // the stall's badge on the middle of the counter front
        };

        static void BuildHome(GameUi ui, Transform croot)
        {
            var root = Stretch("Home", croot);
            ui.home = root.gameObject.AddComponent<CanvasGroup>();
            var back = Img("Back", root, null, Vector2.zero, Vector2.zero, null, true);   // blocks touches to the game
            var brt = back.rectTransform; brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = brt.offsetMax = Vector2.zero;
            back.color = new Color32(38, 20, 10, 255);

            var theme = _art.themes[0];
            var stall = Rect("Stall", root, new Vector2(1024, 1536), Vector2.zero);
            var fit = stall.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; fit.aspectRatio = 1024f / 1536f;
            Image Full(string name, Sprite sp)
            {
                var im = Img(name, stall, sp, Vector2.zero, Vector2.zero);
                var rt = im.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
                return im;
            }
            // the stall is drawn at 1.25x of the art (1280x1920 at 1080x1920): sizes below are in art px * 1.25
            const float k = 1.25f;
            Vector2 At(float x, float y) => new Vector2(x / 1024f, 1f - y / 1536f);
            ui.homeBg = Full("Background", theme.Back);
            ui.homeDecor = new Image[HomeDecor.Length];
            // hanging decorations go behind the customers? no: lights + sign hang above their heads, drawn first
            for (int i = 0; i < HomeDecor.Length; i++)
            {
                var d = HomeDecor[i];
                if (!d.hang || d.y > theme.counterTop) continue;   // low items (the counter badge) go in front of the counter
                ui.homeDecor[i] = DecorImg(stall, d, At(d.x, d.y), k);
            }
            ui.homeCustomers = new Image[2];
            float counterY = theme.counterTop;
            for (int i = 0; i < 2; i++)
            {
                var c = Img($"Customer{i}", stall, _art.customers[i * 3], new Vector2(270, 270), Vector2.zero, At(i == 0 ? 352 : 672, counterY));
                c.rectTransform.pivot = new Vector2(.5f, .04f);   // like the game: the bust's cut edge just behind the counter top
                ui.homeCustomers[i] = c;
            }
            ui.homeFront = Full("Front", theme.front);
            var mascot = Img("Mascot", stall, _art.mascot, new Vector2(150, 247), new Vector2(0, -6), At(512, counterY));
            mascot.rectTransform.pivot = new Vector2(.5f, 0f);
            ui.homeMascot = mascot.rectTransform;
            for (int i = 0; i < HomeDecor.Length; i++)
            {
                var d = HomeDecor[i];
                if (d.hang && d.y <= theme.counterTop) continue;
                ui.homeDecor[i] = DecorImg(stall, d, At(d.x, d.y), k);
            }
            var spark = S("VFX/fx_sparkle.png");
            var spots = new (float x, float y)[] { (250, 610), (820, 560), (420, 930), (600, 880), (160, 420), (880, 420), (512, 690), (300, 300) };
            ui.homeSparkles = new Image[spots.Length];
            for (int i = 0; i < spots.Length; i++)
            {
                ui.homeSparkles[i] = ImgW($"Sparkle{i}", stall, spark, i % 2 == 0 ? 64 : 46, Vector2.zero, At(spots[i].x, spots[i].y));
                ui.homeSparkles[i].color = new Color(1, 1, 1, 0);
            }

            // ---- UI in the safe area
            var safe = Stretch("Safe", root);
            safe.gameObject.AddComponent<SafeArea>();
            var top = new Vector2(.5f, 1f); var bottom = new Vector2(.5f, 0f);
            var hud = S("UI/Panels/bar_hud.png");
            ui.homeSettings = Button(ImgW("Settings", safe, S("UI/Buttons/btn_settings.png"), 112, new Vector2(92, -106), new Vector2(0, 1), true));
            var coinPill = Sliced("CoinPill", safe, hud, new Vector2(250, 78), new Vector2(-180, -100), new Vector2(1, 1));
            ui.homeCoinText = Text("Text", coinPill.transform, "250", 44, Brown, new Vector2(170, 78), new Vector2(18, 2), _matPlain);
            ui.homeCoinText.textWrappingMode = TextWrappingModes.NoWrap;
            ui.homeCoinIcon = ImgW("Coin", coinPill.transform, S("UI/Icons/icon_coin.png"), 86, new Vector2(-110, 2)).rectTransform;

            // side buttons under the HUD: Daily (left), Decorate (right), each a round tile with a label and a red dot
            ui.homeDaily = SideButton(safe, "Daily", MetaDir + "icon_daily.png", "Daily", new Vector2(112, -300), new Vector2(0, 1), out ui.homeDailyDot, out _);
            ui.homeDecorate = SideButton(safe, "Decorate", MetaDir + "icon_decorate.png", "Decorate", new Vector2(-112, -300), new Vector2(1, 1), out ui.homeDecorateDot, out ui.homeDecorCount);

            // stall progress card + Play
            var card = Sliced("WorldCard", safe, hud, new Vector2(800, 222), new Vector2(0, 500), bottom);
            ui.homeWorldName = Text("Name", card.transform, theme.displayName, 46, Brown, new Vector2(640, 56), new Vector2(0, 66), _matPlain);
            ui.homeWorldName.textWrappingMode = TextWrappingModes.NoWrap;
            float barW = 380f, bk = barW / 842f;
            var track = Img("Bar", card.transform, S("Generated/bar_track_clean.png"), new Vector2(barW, 185 * bk), new Vector2(0, -8));
            var fill = Sliced("Fill", track.transform, S("Generated/" + BarFill), new Vector2(200, 106 * bk), Vector2.zero);
            var frt = fill.rectTransform;
            frt.anchorMin = frt.anchorMax = new Vector2(0, .5f); frt.pivot = new Vector2(0, .5f);
            frt.anchoredPosition = new Vector2(43 * bk, (92.5f - 91.5f) * bk);
            ui.homeWorldFill = frt; ui.homeWorldFillMax = (842 - 86) * bk;
            ui.homeWorldNext = Text("Next", card.transform, "Next stall", 28, new Color32(96, 50, 18, 255), new Vector2(700, 40), new Vector2(0, -66), _matPlain);
            ui.homeWorldNext.textWrappingMode = TextWrappingModes.NoWrap;

            var play = Sliced("Play", safe, S("UI/Buttons/btn_green.png"), new Vector2(600, 190), new Vector2(0, 250), bottom, true);
            ui.homePlayText = Text("Label", play.transform, "Level 1", 76, Color.white, new Vector2(540, 150), new Vector2(0, 8), _matBrownOutline);
            ui.homePlayText.textWrappingMode = TextWrappingModes.NoWrap;
            ui.homePlay = Button(play);
            root.gameObject.SetActive(false);
        }

        static Image DecorImg(RectTransform stall, (string file, float x, float y, float h, bool hang) d, Vector2 anchor, float k)
        {
            var im = ImgH(d.file, stall, S(MetaDir + d.file + ".png"), d.h * k, Vector2.zero, anchor);
            im.rectTransform.pivot = d.hang ? new Vector2(.5f, .5f) : new Vector2(.5f, 0f);
            im.gameObject.SetActive(false);
            return im;
        }

        static UnityEngine.UI.Button SideButton(Transform parent, string name, string icon, string label, Vector2 pos, Vector2 anchor, out Image dot, out TMP_Text count)
        {
            var bg = Img(name, parent, S("Generated/disc.png"), new Vector2(150, 150), pos, anchor, true);
            bg.color = new Color32(255, 246, 225, 255);
            var ring = Img("Ring", bg.transform, S("Generated/disc.png"), new Vector2(136, 136), Vector2.zero);
            ring.color = new Color32(255, 214, 120, 255);
            ImgW("Icon", bg.transform, S(icon), 112, new Vector2(0, 4));
            var lab = Text("Label", bg.transform, label, 34, Color.white, new Vector2(220, 50), new Vector2(0, -92), _matBrownOutline);
            lab.textWrappingMode = TextWrappingModes.NoWrap;
            dot = Img("Dot", bg.transform, S("Generated/badge.png"), new Vector2(46, 46), new Vector2(52, 52));
            count = null;
            if (name == "Decorate")
            {
                var pill = Sliced("Count", bg.transform, S("Generated/rounded.png"), new Vector2(84, 40), new Vector2(0, -136));
                pill.color = new Color32(92, 46, 18, 220);
                count = Text("Text", pill.transform, "0/6", 28, Color.white, new Vector2(84, 40), new Vector2(0, 1), _matPlain);
                count.textWrappingMode = TextWrappingModes.NoWrap;
            }
            return Button(bg);
        }

        // Daily reward: 7 days (4 + 3 tiles), today glows and pulses, claimed days get a check. Grid on popup_panel at
        // 900 wide: cream x -347..344, y +302..-351.
        static void BuildDailyPopup(GameUi ui, Transform parent)
        {
            var (cg, panel) = Popup("DailyPopup", parent, 900, "Daily Reward", out _);
            ui.dailyPopup = cg; ui.dailyPanel = panel;
            float h = panel.sizeDelta.y;
            ui.dailyClose = Button(ImgW("Close", panel, S("UI/Icons/btn_close.png"), 108, new Vector2(406, h / 2 - 72), null, true));
            string[] icons = { "reward_coins_small", "reward_coins_small", "reward_gift", "reward_coins_big", "reward_gift", "reward_coins_big", "reward_chest" };
            int n = icons.Length;
            ui.dailyTiles = new Image[n]; ui.dailyIcons = new Image[n]; ui.dailyChecks = new Image[n];
            ui.dailyAmounts = new TMP_Text[n]; ui.dailyDays = new TMP_Text[n];
            var check = S("UI/Icons/icon_check.png");
            for (int i = 0; i < n; i++)
            {
                bool row1 = i < 4;
                bool big = i == n - 1;
                float x = row1 ? -246 + i * 164 : new[] { -195f, -31f, 164f }[i - 4];
                float y = row1 ? 168 : -48;
                var size = big ? new Vector2(210, 196) : new Vector2(148, 196);
                var tile = Sliced($"Day{i + 1}", panel, S("Generated/rounded.png"), size, new Vector2(x - 1, y));
                tile.color = new Color32(246, 232, 204, 255);
                ui.dailyTiles[i] = tile;
                ui.dailyDays[i] = Text("Day", tile.transform, $"Day {i + 1}", 30, Brown, new Vector2(size.x, 40), new Vector2(0, 70), _matPlain);
                ui.dailyDays[i].textWrappingMode = TextWrappingModes.NoWrap;
                ui.dailyIcons[i] = ImgW("Icon", tile.transform, S(MetaDir + icons[i] + ".png"), big ? 112 : 96, new Vector2(0, big ? 2 : 8));
                ui.dailyAmounts[i] = Text("Amount", tile.transform, GameController.DailyLabel(i), i == 2 || i == 4 ? 26 : 36, Color.white, new Vector2(size.x - 8, 44), new Vector2(0, -68), _matBrownOutline);
                ui.dailyAmounts[i].textWrappingMode = TextWrappingModes.NoWrap;
                ui.dailyChecks[i] = ImgW("Check", tile.transform, check, 74, new Vector2(size.x / 2 - 22, -size.y / 2 + 26));
                ui.dailyChecks[i].gameObject.SetActive(false);
            }
            ui.dailyClaim = WideButton("Claim", panel, "btn_green", "Claim!", new Vector2(520, 120), new Vector2(-1, -250), out var ct);
            ui.dailyClaimText = ct;
        }

        // Decorate ("My Stall"): one row per decoration: picture | name | [coin price] or a check when built.
        static void BuildDecorPopup(GameUi ui, Transform parent)
        {
            var (cg, panel) = Popup("DecorPopup", parent, 900, "My Stall", out _);
            ui.decorPopup = cg; ui.decorPanel = panel;
            float h = panel.sizeDelta.y;
            ui.decorClose = Button(ImgW("Close", panel, S("UI/Icons/btn_close.png"), 108, new Vector2(406, h / 2 - 72), null, true));
            int n = HomeDecor.Length;
            ui.decorBuy = new UnityEngine.UI.Button[n]; ui.decorPrice = new TMP_Text[n]; ui.decorBuilt = new Image[n];
            const float rowH = 96f, y0 = 246f;
            for (int i = 0; i < n; i++)
            {
                float y = y0 - i * (rowH + 6);
                var row = Sliced($"Row{i}", panel, S("Generated/rounded.png"), new Vector2(660, rowH), new Vector2(-1, y));
                row.color = new Color32(246, 232, 204, 255);
                var pic = Img("Pic", row.transform, S(MetaDir + HomeDecor[i].file + ".png"), new Vector2(84, 84), new Vector2(-270, 0));
                pic.preserveAspect = true;
                var name = Text("Name", row.transform, GameController.DecorNames[i], 38, Brown, new Vector2(300, rowH), new Vector2(-50, 2), _matPlain);
                name.alignment = TextAlignmentOptions.Left; name.textWrappingMode = TextWrappingModes.NoWrap;
                var buy = Sliced("Buy", row.transform, S("UI/Buttons/btn_green.png"), new Vector2(190, 80), new Vector2(222, 2), null, true);
                ImgW("Coin", buy.transform, S("UI/Icons/icon_coin.png"), 48, new Vector2(-50, 3));
                ui.decorPrice[i] = Text("Price", buy.transform, GameController.DecorCost[i].ToString(), 40, Color.white, new Vector2(110, 60), new Vector2(26, 4), _matBrownOutline);
                ui.decorPrice[i].textWrappingMode = TextWrappingModes.NoWrap;
                ui.decorBuy[i] = Button(buy);
                ui.decorBuilt[i] = ImgW("Built", row.transform, S("UI/Icons/icon_check.png"), 70, new Vector2(222, 2));
                ui.decorBuilt[i].gameObject.SetActive(false);
            }
        }

        static void BuildMessagePopup(GameUi ui, Transform parent)
        {
            var (cg, panel) = Popup("MessagePopup", parent, 760, "Hello", out var title);
            ui.messagePopup = cg; ui.messagePanel = panel; ui.messageTitle = title;
            ui.messageBody = Text("Body", panel, "Message", 42, Brown, new Vector2(600, 300), new Vector2(0, 60), _matPlain);
            ui.messageOk = WideButton("Ok", panel, "btn_green", "OK", new Vector2(280, 100), new Vector2(0, -200), out _);
        }
    }
}
