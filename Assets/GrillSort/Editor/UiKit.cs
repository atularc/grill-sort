using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GrillSort.EditorTools
{
    /// <summary>Editor-only helpers the scene builder uses to author uGUI objects (never used at runtime).</summary>
    public static class UiKit
    {
        public static TMP_FontAsset Font;

        public static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 pos, Vector2? anchor = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var a = anchor ?? new Vector2(.5f, .5f);
            rt.anchorMin = rt.anchorMax = a;
            rt.pivot = new Vector2(.5f, .5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return rt;
        }

        public static RectTransform Stretch(string name, Transform parent)
        {
            var rt = Rect(name, parent, Vector2.zero, Vector2.zero);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Img(string name, Transform parent, Sprite sprite, Vector2 size, Vector2 pos, Vector2? anchor = null, bool raycast = false)
        {
            var rt = Rect(name, parent, size, pos, anchor);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = raycast;
            return img;
        }

        /// <summary>Image sized by width, keeping the sprite aspect.</summary>
        public static Image ImgW(string name, Transform parent, Sprite sprite, float width, Vector2 pos, Vector2? anchor = null, bool raycast = false)
        {
            float h = sprite != null ? width * sprite.rect.height / sprite.rect.width : width;
            var img = Img(name, parent, sprite, new Vector2(width, h), pos, anchor, raycast);
            img.preserveAspect = true;
            return img;
        }

        public static Image ImgH(string name, Transform parent, Sprite sprite, float height, Vector2 pos, Vector2? anchor = null)
        {
            float w = sprite != null ? height * sprite.rect.width / sprite.rect.height : height;
            var img = Img(name, parent, sprite, new Vector2(w, height), pos, anchor);
            img.preserveAspect = true;
            return img;
        }

        /// <summary>9-sliced image; borders scaled so a sprite of native height <paramref name="nativeH"/> fits <paramref name="size"/>.y.</summary>
        public static Image Sliced(string name, Transform parent, Sprite sprite, Vector2 size, Vector2 pos, Vector2? anchor = null, bool raycast = false)
        {
            var img = Img(name, parent, sprite, size, pos, anchor, raycast);
            img.type = Image.Type.Sliced;
            if (sprite != null) img.pixelsPerUnitMultiplier = Mathf.Max(.01f, sprite.rect.height / size.y);
            return img;
        }

        public static TextMeshProUGUI Text(string name, Transform parent, string text, float size, Color color, Vector2 rectSize, Vector2 pos,
            Material mat = null, Vector2? anchor = null, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var rt = Rect(name, parent, rectSize, pos, anchor);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = Font;
            if (mat != null) t.fontSharedMaterial = mat;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(Image target)
        {
            target.raycastTarget = true;
            var b = target.gameObject.AddComponent<Button>();
            b.targetGraphic = target;
            b.transition = Selectable.Transition.None;
            var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
            target.gameObject.AddComponent<PressScale>();
            return b;
        }

        public static T Load<T>(string path) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null) Debug.LogError("[Grill Sort] Missing asset: " + path);
            return a;
        }
    }
}
