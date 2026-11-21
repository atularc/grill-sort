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

        static Material _matPlain, _matBrownOutline, _matGreenOutline;
        static GameArt _art;

        [MenuItem("Grill Sort/Run Full Setup", priority = 0)]
        public static void RunFullSetup()
        {
            if (!EnsureTmpEssentials()) return;
            ConfigureTextures();
            MakeFonts();
            _art = MakeGameArt();
            var bank = MakeLevelBank();
            MakeAtlases();
            BuildScene(bank);
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
                { Art + "UI/Panels/bar_hud.png", new Vector4(130, 0, 130, 0) },
                { Art + "UI/Buttons/btn_green.png", new Vector4(110, 0, 110, 0) },
                { Art + "UI/Buttons/btn_orange.png", new Vector4(110, 0, 110, 0) },
                { Art + "UI/Buttons/btn_red.png", new Vector4(110, 0, 110, 0) },
                { Art + "Generated/bar_fill.png", new Vector4(52, 0, 52, 0) },
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
                bool isBg = path.Contains("/Backgrounds/");
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

        // Cartoon art set (client direction 2026-11-18). The old realistic Food/Grilled, Customers (animals) and
        // grill/skewer art stays in the project, unused.
        /// <summary>Food icon size inside the order bubble (was 112 in the mockup; user asked for smaller, 2026-11-18).</summary>
        const float OrderIcon = 88f;
        /// <summary>Message strip (tips + toasts) just above the booster tiles (tile centre 130, half height ~85).</summary>
        const float MsgW = 860f, MsgH = 92f, MsgY = 276f;
        // Final food (2026-11-18): Aaron chose style B and asked for less shine. FoodFinal_v2 = half the shine (STYLE=FINAL2);
        // FoodFinal (v1, shine removed: user found it too matte) is the backup.
        // FoodCartoon (A), FoodCartoonB, FoodCartoonC stay on disk as unused alternatives.
        // FoodFinal_v3 (2026-11-19): Aaron's sign-off note, grill marks softer and lighter, none on some foods
        // (Tools/ArtPipeline/soften_marks.py). FoodFinal_v2 is the backup.
        const string FoodDir = "FoodFinal_v3/Grilled/", ShadowDir = "Generated/ShadowsFinal/", CustomerDir = "CustomersPeople/";
        static readonly string[] CustomerNames = { "grandpa", "ponytail", "redhead", "curly", "dad", "grandma", "pigtails", "quiff" };

        static readonly string[] AtlasFolders = { Art + "UI/", Art + CustomerDir, Art + "VFX/", Art + FoodDir, Art + ShadowDir };
        static readonly string[] AtlasFiles = { "badge.png", "disc.png", "rounded.png", "bar_fill.png", "bar_track.png", "glow.png", "soft_rect.png" };

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
                    grilled = Load<Sprite>($"{Art}{FoodDir}{n}.png"),
                    medium = Load<Sprite>($"{Art}Food/Medium/{n}.png"),
                    raw = Load<Sprite>($"{Art}Food/Raw/{n}.png"),
                    shadow = Load<Sprite>($"{Art}{ShadowDir}{n}.png"),
                    shadowRaw = Load<Sprite>($"{Art}Generated/Shadows/{n}_raw.png"),
                    width = FoodWidth[n],
                });
            }
            a.foods = foods.ToArray();
            a.customers = CustomerNames.Select(n => Load<Sprite>($"{Art}{CustomerDir}{n}.png"));
            a.themes = new[]
            {
                // Aaron 2026-11-18: bright backgrounds for normal levels, darker ones saved for later levels.
                // Mean brightness: Food Truck 212, Beach 185, Winter 146, Wood 73, Night Market 69.
                // Aaron 2026-11-19: keep the Beach background from the demo -> Beach for the whole 50-level build.
                Theme("Beach", "Beach Shack", 500, 1),
                Theme("FoodTruck", "Food Truck", 527, 51),
                Theme("Winter", "Winter Market", 538, 61),
                Theme("Wood", "Wood Stall", 528, 71),
                Theme("NightMarket", "Night Market", 520, 81),
            };
            // the coal glow is split into its own layer so it can flicker (Aaron 2026-11-18; Tools/ArtPipeline/split_grill_glow.py)
            a.grill = Load<Sprite>(Art + "Generated/grill_cartoon_base.png");
            a.grillGlow = Load<Sprite>(Art + "Generated/grill_cartoon_glow.png");
            a.grillDark = Load<Sprite>(Art + "Generated/grill_cartoon_dim_base.png"); // brighter than the v3 grill_cartoon_dark (Aaron: bottom too dark)
            a.grillDarkGlow = Load<Sprite>(Art + "Generated/grill_cartoon_dim_glow.png");
            a.grillHelper = Load<Sprite>(Art + "Generated/grill_helper_base.png"); // helper trays: steel frame (Tools/ArtPipeline/make_helper_grill.py)
            a.skewer = Load<Sprite>(Art + "Board/skewer_cartoon.png");
            a.starOn = Load<Sprite>(Art + "UI/Icons/icon_star.png");
            a.starOff = a.starOn;
            string au = Root + "Audio/";
            a.pick = Load<AudioClip>(au + "pick.wav"); a.drop = Load<AudioClip>(au + "drop.wav");
            a.invalid = Load<AudioClip>(au + "invalid.wav"); a.sizzle = Load<AudioClip>(au + "sizzle.wav");
            a.serve = Load<AudioClip>(au + "serve.wav"); a.coin = Load<AudioClip>(au + "coin.wav");
            a.win = Load<AudioClip>(au + "win.wav"); a.click = Load<AudioClip>(au + "click.wav");
            EditorUtility.SetDirty(a);
            return a;
        }

        static T[] Select<T>(this string[] names, System.Func<string, T> f)
        {
            var r = new T[names.Length];
            for (int i = 0; i < names.Length; i++) r[i] = f(names[i]);
            return r;
        }

        static GameArt.ThemeDef Theme(string folder, string display, float counterTop, int unlock)
        {
            string p = $"{Art}Backgrounds/{folder}/";
            return new GameArt.ThemeDef
            {
                name = folder,
                displayName = display,
                background = Load<Sprite>(p + "background.png"),
                backgroundCalm = File.Exists(p + "background-calm.png") ? Load<Sprite>(p + "background-calm.png") : null,
                // brightened planks where made (Tools/ArtPipeline/brighten_board.py; Wood, Aaron: bottom too dark)
                front = Load<Sprite>(p + (File.Exists(p + "layer_front_bright.png") ? "layer_front_bright.png" : "layer_front.png")),
                counterTop = counterTop,
                unlockLevel = unlock,
            };
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
            Atlas("UI", new[] { Art + "UI", Art + CustomerDir.TrimEnd('/'), Art + "VFX" }, 2048, new[] { Art + "Generated/badge.png", Art + "Generated/disc.png", Art + "Generated/rounded.png", Art + "Generated/bar_fill.png", Art + "Generated/bar_track.png", Art + "Generated/glow.png", Art + "Generated/soft_rect.png" });
            Atlas("Food", new[] { Art + FoodDir.TrimEnd('/'), Art + ShadowDir.TrimEnd('/') }, 2048, null);
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
        }

        // ------------------------------------------------------------------ scene
        static Sprite S(string rel) => Load<Sprite>(Art + rel);

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
            am.sfx = audioGo.AddComponent<AudioSource>(); am.sfx.playOnAwake = false;
            am.loop = audioGo.AddComponent<AudioSource>(); am.loop.playOnAwake = false; am.loop.loop = true;

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
            layout.counterTop = theme.counterTop;

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
                cv.bubble = Rect("Bubble", slot, new Vector2(196, 119), new Vector2(0, CustomerView.BubbleY));
                var bsh = Img("Shadow", cv.bubble, S("Generated/glow.png"), new Vector2(190, 40), new Vector2(0, -52));
                bsh.color = new Color(.08f, .03f, 0f, .35f);
                ImgW("Bubble", cv.bubble, bubbleSp, 196, Vector2.zero);
                cv.order = Img("Order", cv.bubble, _art.foods[i].grilled, new Vector2(OrderIcon, OrderIcon), new Vector2(0, 10));
                cv.order.preserveAspect = true;
                cv.check = ImgW("Check", cv.bubble, check, 62, new Vector2(80, 46));
                cv.check.gameObject.SetActive(false);
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
                gv.grill = Img("Grill", root, _art.grillDark, new Vector2(GrillView.GrillW, GrillView.GrillH), Vector2.zero);
                gv.coalGlow = Img("CoalGlow", root, _art.grillDarkGlow, new Vector2(GrillView.GrillW, GrillView.GrillH), Vector2.zero);
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
            var bar = Img("OrdersBar", safe, S("Generated/bar_track.png"), new Vector2(barW, 185 * barScale), new Vector2(0, -186), top);
            var fill = Sliced("Fill", bar.transform, S("Generated/bar_fill.png"), new Vector2(200, 106 * barScale), Vector2.zero);
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
            var tipBg = Sliced("Bg", tipRt, S("Generated/rounded.png"), new Vector2(MsgW, MsgH), Vector2.zero);
            tipBg.color = new Color(.16f, .07f, .02f, .78f);
            ui.tipText = Text("Text", tipRt, "Tip", 30, Cream, new Vector2(MsgW - 40, MsgH - 8), Vector2.zero, _matPlain);

            var toastRt = Rect("Toast", safe, new Vector2(MsgW, MsgH), new Vector2(0, MsgY), bottom);
            ui.toast = toastRt.gameObject.AddComponent<CanvasGroup>();
            ui.toast.blocksRaycasts = false;
            ui.toast.alpha = 0f;
            var toastBg = Sliced("Bg", toastRt, S("Generated/rounded.png"), new Vector2(MsgW, MsgH), Vector2.zero);
            toastBg.color = new Color(.16f, .07f, .02f, .88f);
            ui.toastText = Text("Text", toastRt, "Toast", 32, Cream, new Vector2(MsgW - 40, MsgH - 8), Vector2.zero, _matPlain);

            // ---------------- popups
            var popups = Stretch("Popups", croot);
            BuildWinPopup(ui, popups);
            BuildPausePopup(ui, popups);
            BuildStuckPopup(ui, popups);
            BuildMessagePopup(ui, popups);

            // ---------------- game
            var gameGo = new GameObject("Game");
            var gc = gameGo.AddComponent<GameController>();
            gc.art = _art; gc.bank = bank; gc.ui = ui; gc.audioManager = am;

            layout.activeGrills = 5;
            layout.Apply();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
        }

        static (CanvasGroup group, RectTransform panel) Popup(string name, Transform parent, float width, string title, out TextMeshProUGUI titleText)
        {
            var root = Stretch(name, parent);
            var cg = root.gameObject.AddComponent<CanvasGroup>();
            var dim = Img("Dim", root, null, Vector2.zero, Vector2.zero, null, true);
            var drt = dim.rectTransform; drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.offsetMin = drt.offsetMax = Vector2.zero;
            dim.color = new Color(.05f, .02f, 0f, .66f);
            var sp = S("UI/Panels/popup_panel.png");
            float h = width * sp.rect.height / sp.rect.width;
            var panel = Rect("Panel", root, new Vector2(width, h), new Vector2(0, 20));
            var pimg = panel.gameObject.AddComponent<Image>(); pimg.sprite = sp; pimg.raycastTarget = true;
            float sc = width / sp.rect.width;
            titleText = Text("Title", panel, title, 64, Color.white, new Vector2(width * .7f, 110), new Vector2(0, h / 2 - 100 * sc), _matBrownOutline);
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

        static void BuildWinPopup(GameUi ui, Transform parent)
        {
            var (cg, panel) = Popup("WinPopup", parent, 860, "Level 1", out var title);
            ui.winPopup = cg; ui.winPanel = panel; ui.winTitle = title;
            ImgW("Word", panel, S("UI/Words/word_level_complete.png"), 540, new Vector2(0, 205));
            var star = S("UI/Icons/icon_star.png");
            ui.winStars = new[]
            {
                ImgW("Star0", panel, star, 150, new Vector2(-180, -5)),
                ImgW("Star1", panel, star, 185, new Vector2(0, 20)),
                ImgW("Star2", panel, star, 150, new Vector2(180, -5)),
            };
            var row = Rect("Reward", panel, new Vector2(400, 90), new Vector2(0, -135));
            ImgW("Coin", row, S("UI/Icons/icon_coin.png"), 80, new Vector2(-70, 0));
            ui.winCoins = Text("Amount", row, "+25", 60, Brown, new Vector2(200, 90), new Vector2(60, 2), _matPlain);
            ui.winCoins.alignment = TextAlignmentOptions.Left;
            ui.winDouble = WideButton("Double", panel, "btn_orange", "x2 Ad", new Vector2(250, 100), new Vector2(-138, -250), out _);
            ui.winNext = WideButton("Next", panel, "btn_green", "Next", new Vector2(250, 100), new Vector2(138, -250), out _);
        }

        static void BuildPausePopup(GameUi ui, Transform parent)
        {
            var (cg, panel) = Popup("PausePopup", parent, 800, "Paused", out _);
            ui.pausePopup = cg; ui.pausePanel = panel;
            var size = new Vector2(460, 100);
            ui.pauseResume = WideButton("Resume", panel, "btn_green", "Resume", size, new Vector2(0, 170), out _);
            ui.pauseRestart = WideButton("Restart", panel, "btn_orange", "Restart", size, new Vector2(0, 50), out _);
            ui.pauseSound = WideButton("Sound", panel, "btn_orange", "Sound: On", size, new Vector2(0, -70), out var st);
            ui.pauseHaptics = WideButton("Haptics", panel, "btn_orange", "Vibration: On", size, new Vector2(0, -190), out var ht);
            ui.pauseSoundText = st; ui.pauseHapticsText = ht;
        }

        static void BuildStuckPopup(GameUi ui, Transform parent)
        {
            var (cg, panel) = Popup("StuckPopup", parent, 800, "No moves!", out _);
            ui.stuckPopup = cg; ui.stuckPanel = panel;
            Text("Body", panel, "The grill is jammed.\nPick a way out:", 40, Brown, new Vector2(620, 100), new Vector2(0, 205), _matPlain);
            var size = new Vector2(440, 92);
            ui.stuckUndo = WideButton("Undo", panel, "btn_orange", "Undo", size, new Vector2(0, 100), out _);
            ui.stuckShuffle = WideButton("Shuffle", panel, "btn_orange", "Shuffle", size, new Vector2(0, -8), out _);
            ui.stuckExtra = WideButton("Extra", panel, "btn_green", "Extra Skewer (Ad)", size, new Vector2(0, -116), out _);
            ui.stuckRetry = WideButton("Retry", panel, "btn_red", "Retry", size, new Vector2(0, -224), out _);
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
