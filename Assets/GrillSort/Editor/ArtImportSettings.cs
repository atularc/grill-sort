using UnityEditor;

namespace GrillSort.EditorTools
{
    /// <summary>
    /// First-time import defaults for everything under Assets/GrillSort/Art:
    /// single sprites, transparent, no mipmaps, compressed, max 2048.
    /// Only applied when a texture has no import settings yet, so manual tweaks later are kept.
    /// </summary>
    public class ArtImportSettings : AssetPostprocessor
    {
        const string Root = "Assets/GrillSort/Art/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root) || !assetImporter.importSettingsMissing) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.spritePixelsPerUnit = 100;
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.Compressed;
        }
    }
}
