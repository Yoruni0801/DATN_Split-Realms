#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace DATN.Duy.VillageFire.Editor
{
    internal sealed class VillageFireTextureImporter : AssetPostprocessor
    {
        private const string PainterlyAtlasSuffix = "VillageFire/Resources/T_VillageFire_PainterlyAtlas_v2.png";
        private const string LegacyAtlasSuffix = "VillageFire/Resources/T_VillageFire_RoofAtlas.png";

        private void OnPreprocessTexture()
        {
            string normalizedPath = assetPath.Replace('\\', '/');
            if (!normalizedPath.EndsWith(PainterlyAtlasSuffix) && !normalizedPath.EndsWith(LegacyAtlasSuffix))
                return;

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
        }

        [InitializeOnLoadMethod]
        private static void EnsureAtlasSettings()
        {
            EditorApplication.delayCall += () =>
            {
                const string path = "Assets/_Project/Bao/FX 1/VillageFire/Resources/T_VillageFire_PainterlyAtlas_v2.png";
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    return;

                bool changed = importer.textureType != TextureImporterType.Default ||
                               importer.alphaSource != TextureImporterAlphaSource.FromInput ||
                               !importer.alphaIsTransparency ||
                               !importer.sRGBTexture ||
                               importer.mipmapEnabled ||
                               importer.wrapMode != TextureWrapMode.Clamp ||
                               importer.filterMode != FilterMode.Bilinear ||
                               importer.textureCompression != TextureImporterCompression.Uncompressed ||
                               importer.npotScale != TextureImporterNPOTScale.None ||
                               importer.maxTextureSize != 2048;
                if (!changed)
                    return;

                importer.textureType = TextureImporterType.Default;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            };
        }
    }
}
#endif
