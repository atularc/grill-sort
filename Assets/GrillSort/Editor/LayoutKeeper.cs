using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace GrillSort.EditorTools
{
    /// <summary>
    /// Keeps hand-made layout tweaks across "Grill Sort > Run Full Setup" (user 2026-11-22: "I will fix [the spacing]
    /// myself, just make sure to not mess it up in future when you run the full setup").
    ///
    /// Run Full Setup rebuilds Game.unity from code. Around each rebuild:
    ///   1. <see cref="BeforeBuild"/> saves unsaved edits in the open Game scene, reads every UI object in it and compares
    ///      it with the snapshot of what the builder produced last time (Data/LayoutBuilt.json). Every property that
    ///      differs was changed by hand: it is added to Data/LayoutOverrides.json (kept for good).
    ///   2. The builder builds the scene as usual (code changes still land).
    ///   3. <see cref="AfterBuild"/> re-applies all overrides on top, saves the scene and writes the new snapshot.
    /// Tracked per object: anchored position, size, scale, rotation, anchors, pivot, Image/Text colour, TMP font size,
    /// layout-group spacing. Objects are matched by hierarchy path (name + index among same-named siblings).
    /// "Grill Sort > Forget Manual Layout Tweaks" clears the overrides.
    /// </summary>
    public static class LayoutKeeper
    {
        const string ScenePath = "Assets/GrillSort/Scenes/Game.unity";
        const string BuiltFile = "Assets/GrillSort/Data/LayoutBuilt.json";
        const string OverridesFile = "Assets/GrillSort/Data/LayoutOverrides.json";
        const float Eps = .01f;

        [Flags] enum P { Pos = 1, Size = 2, Scale = 4, Rot = 8, Anchors = 16, Pivot = 32, Color = 64, Font = 128, Spacing = 256 }

        [Serializable]
        sealed class Entry
        {
            public string path;
            public int mask;   // overrides: which properties are hand-set
            public Vector2 pos, size, aMin, aMax, pivot;
            public Vector3 scale;
            public float rotZ;
            public Color color = new Color(-1, -1, -1, -1);
            public float font = -1, spacing = -1;
            public bool driven;
        }

        [Serializable] sealed class Table { public List<Entry> items = new List<Entry>(); }

        public static void BeforeBuild()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path == ScenePath && scene.isDirty) EditorSceneManager.SaveScene(scene);
            if (!File.Exists(ScenePath) || !File.Exists(BuiltFile)) return;   // first run: nothing to compare with yet
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var built = ToDict(Load(BuiltFile));
            var overrides = ToDict(Load(OverridesFile));
            int found = 0;
            foreach (var cur in Read())
            {
                if (!built.TryGetValue(cur.path, out var b)) continue;
                int diff = Diff(b, cur);
                if (cur.driven) diff &= ~Driven;
                if (diff == 0) continue;
                if (!overrides.TryGetValue(cur.path, out var o)) { o = new Entry { path = cur.path }; overrides[cur.path] = o; }
                Copy(cur, o, diff);
                o.mask |= diff;
                found++;
            }
            Save(OverridesFile, overrides.Values);
            if (found > 0) Debug.Log($"[Grill Sort] Layout keeper: {found} hand-edited object(s) remembered ({overrides.Count} kept in total).");
        }

        public static void AfterBuild()
        {
            var overrides = Load(OverridesFile);
            int applied = 0;
            if (overrides.items.Count > 0)
            {
                var byPath = new Dictionary<string, RectTransform>();
                foreach (var rt in AllRects()) byPath[PathOf(rt)] = rt;
                foreach (var o in overrides.items)
                {
                    if (!byPath.TryGetValue(o.path, out var rt)) continue;
                    if (rt.parent != null && rt.parent.GetComponent<LayoutGroup>() != null) o.mask &= ~Driven;
                    if (o.mask == 0) continue;
                    Apply(rt, o);
                    applied++;
                }
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                if (applied > 0) Debug.Log($"[Grill Sort] Layout keeper: re-applied {applied} hand-made tweak(s).");
            }
            var snap = new Table(); snap.items.AddRange(Read());
            File.WriteAllText(BuiltFile, JsonUtility.ToJson(snap));
            AssetDatabase.ImportAsset(BuiltFile);
        }

        [MenuItem("Grill Sort/Forget Manual Layout Tweaks", priority = 30)]
        public static void Forget()
        {
            if (File.Exists(OverridesFile)) { AssetDatabase.DeleteAsset(OverridesFile); Debug.Log("[Grill Sort] Layout keeper: hand-made tweaks forgotten (next Run Full Setup uses the code layout)."); }
        }

        // ------------------------------------------------------------------ helpers
        static IEnumerable<RectTransform> AllRects()
        {
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (var rt in root.GetComponentsInChildren<RectTransform>(true)) yield return rt;
        }

        static string PathOf(Transform t)
        {
            var parts = new List<string>();
            while (t != null)
            {
                int k = 0;
                if (t.parent != null)
                    for (int i = 0; i < t.GetSiblingIndex(); i++) if (t.parent.GetChild(i).name == t.name) k++;
                parts.Add(k == 0 ? t.name : $"{t.name}#{k}");
                t = t.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        static List<Entry> Read()
        {
            var list = new List<Entry>();
            foreach (var rt in AllRects())
            {
                var e = new Entry
                {
                    path = PathOf(rt), pos = rt.anchoredPosition, size = rt.sizeDelta, scale = rt.localScale,
                    rotZ = rt.localEulerAngles.z, aMin = rt.anchorMin, aMax = rt.anchorMax, pivot = rt.pivot,
                };
                var g = rt.GetComponent<Graphic>();
                if (g != null) e.color = g.color;
                var t = rt.GetComponent<TMP_Text>();
                if (t != null) e.font = t.fontSize;
                var lg = rt.GetComponent<HorizontalOrVerticalLayoutGroup>();
                if (lg != null) e.spacing = lg.spacing;
                // laid out by a parent layout group (e.g. the coin and amount inside a claim button row): the group
                // decides its position/size, so those are never treated as hand edits
                e.driven = rt.parent != null && rt.parent.GetComponent<LayoutGroup>() != null;
                list.Add(e);
            }
            return list;
        }

        static bool Ne(Vector2 a, Vector2 b) => (a - b).sqrMagnitude > Eps * Eps;
        static bool Ne(Vector3 a, Vector3 b) => (a - b).sqrMagnitude > Eps * Eps;
        static bool Ne(float a, float b) => Mathf.Abs(Mathf.DeltaAngle(a, b)) > Eps && Mathf.Abs(a - b) > Eps;

        /// <summary>Position / size / anchors / pivot of a child of a layout group are set by the group, not by hand.</summary>
        const int Driven = (int)(P.Pos | P.Size | P.Anchors | P.Pivot);

        static int Diff(Entry a, Entry b)
        {
            int m = 0;
            if (Ne(a.pos, b.pos)) m |= (int)P.Pos;
            if (Ne(a.size, b.size)) m |= (int)P.Size;
            if (Ne(a.scale, b.scale)) m |= (int)P.Scale;
            if (Ne(a.rotZ, b.rotZ)) m |= (int)P.Rot;
            if (Ne(a.aMin, b.aMin) || Ne(a.aMax, b.aMax)) m |= (int)P.Anchors;
            if (Ne(a.pivot, b.pivot)) m |= (int)P.Pivot;
            if (Mathf.Abs(a.color.r - b.color.r) + Mathf.Abs(a.color.g - b.color.g) + Mathf.Abs(a.color.b - b.color.b) + Mathf.Abs(a.color.a - b.color.a) > .004f) m |= (int)P.Color;
            if (Mathf.Abs(a.font - b.font) > Eps) m |= (int)P.Font;
            if (Mathf.Abs(a.spacing - b.spacing) > Eps) m |= (int)P.Spacing;
            return m;
        }

        static void Copy(Entry from, Entry to, int m)
        {
            if ((m & (int)P.Pos) != 0) to.pos = from.pos;
            if ((m & (int)P.Size) != 0) to.size = from.size;
            if ((m & (int)P.Scale) != 0) to.scale = from.scale;
            if ((m & (int)P.Rot) != 0) to.rotZ = from.rotZ;
            if ((m & (int)P.Anchors) != 0) { to.aMin = from.aMin; to.aMax = from.aMax; }
            if ((m & (int)P.Pivot) != 0) to.pivot = from.pivot;
            if ((m & (int)P.Color) != 0) to.color = from.color;
            if ((m & (int)P.Font) != 0) to.font = from.font;
            if ((m & (int)P.Spacing) != 0) to.spacing = from.spacing;
        }

        static void Apply(RectTransform rt, Entry o)
        {
            int m = o.mask;
            if ((m & (int)P.Anchors) != 0) { rt.anchorMin = o.aMin; rt.anchorMax = o.aMax; }
            if ((m & (int)P.Pivot) != 0) rt.pivot = o.pivot;
            if ((m & (int)P.Size) != 0) rt.sizeDelta = o.size;
            if ((m & (int)P.Pos) != 0) rt.anchoredPosition = o.pos;
            if ((m & (int)P.Scale) != 0) rt.localScale = o.scale;
            if ((m & (int)P.Rot) != 0) { var e = rt.localEulerAngles; e.z = o.rotZ; rt.localEulerAngles = e; }
            if ((m & (int)P.Color) != 0) { var g = rt.GetComponent<Graphic>(); if (g != null) g.color = o.color; }
            if ((m & (int)P.Font) != 0) { var t = rt.GetComponent<TMP_Text>(); if (t != null) t.fontSize = o.font; }
            if ((m & (int)P.Spacing) != 0) { var lg = rt.GetComponent<HorizontalOrVerticalLayoutGroup>(); if (lg != null) lg.spacing = o.spacing; }
            EditorUtility.SetDirty(rt);
        }

        static Table Load(string path) => File.Exists(path) ? JsonUtility.FromJson<Table>(File.ReadAllText(path)) ?? new Table() : new Table();

        static Dictionary<string, Entry> ToDict(Table t)
        {
            var d = new Dictionary<string, Entry>();
            foreach (var e in t.items) d[e.path] = e;
            return d;
        }

        static void Save(string path, IEnumerable<Entry> items)
        {
            var t = new Table(); t.items.AddRange(items);
            File.WriteAllText(path, JsonUtility.ToJson(t, true));
            AssetDatabase.ImportAsset(path);
        }
    }
}
