using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;



#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.U2D.Sprites;

namespace Pixelate
{
    public class AutoSpriteSlicer
    {
        public static void Slice(Texture2D texture, Vector2Int cellSize, Vector2 pivot, bool slice, bool pixelated, int frameCount = 1)
        {
            Slice(texture, cellSize, pivot, slice, pixelated, 64f, frameCount);
        }

        public static void Slice(Texture2D texture, Vector2Int cellSize, Vector2 pivot, bool slice, bool pixelated, float pixelsPerUnit, int frameCount = 1)
        {
            Vector2Int sourceTextureSize = texture != null
                ? new Vector2Int(texture.width, texture.height)
                : Vector2Int.zero;

            Slice(texture, cellSize, pivot, slice, pixelated, pixelsPerUnit, frameCount, sourceTextureSize);
        }

        public static void Slice(Texture2D texture, Vector2Int cellSize, Vector2 pivot, bool slice, bool pixelated, int frameCount, Vector2Int sourceTextureSize)
        {
            Slice(texture, cellSize, pivot, slice, pixelated, 64f, frameCount, sourceTextureSize);
        }

        public static void Slice(Texture2D texture, Vector2Int cellSize, Vector2 pivot, bool slice, bool pixelated, float pixelsPerUnit, int frameCount, Vector2Int sourceTextureSize)
        {
            if (texture == null)
            {
                return;
            }

            string path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            ApplySettings(path, texture, cellSize, pivot, slice, pixelated, pixelsPerUnit, frameCount, sourceTextureSize);
        }

        public static void PrepareExistingAssetForOverwrite(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return;
            }

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.SaveAndReimport();
        }

        static void ApplySettings(string path, Texture2D texture, Vector2Int cellSize, Vector2 pivot, bool slice, bool pixelated, float pixelsPerUnit, int frameCount, Vector2Int sourceTextureSize)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.streamingMipmaps = false;
            importer.vtOnly = false;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.spritePixelsPerUnit = Mathf.Max(0.01f, pixelsPerUnit);

            int largestGeneratedDimension = Mathf.Max(sourceTextureSize.x, sourceTextureSize.y);
            if (largestGeneratedDimension > 0)
            {
                importer.maxTextureSize = Mathf.Max(importer.maxTextureSize, Mathf.NextPowerOfTwo(largestGeneratedDimension));
            }

            if (pivot != new Vector2(-1, -1))
            {
                importer.spritePivot = pivot;
            }

            if (slice)
            {
                importer.spriteImportMode = SpriteImportMode.Multiple;
            }
            else
            {
                importer.spriteImportMode = SpriteImportMode.Single;
            }

            if (pixelated)
                importer.filterMode = FilterMode.Point;
            else
                importer.filterMode = FilterMode.Bilinear;


            if (pixelated)
                importer.textureCompression = TextureImporterCompression.Uncompressed;
            else
                importer.textureCompression = TextureImporterCompression.Compressed;

            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteMeshType = SpriteMeshType.FullRect;
            textureSettings.spriteExtrude = 1;
            textureSettings.spriteGenerateFallbackPhysicsShape = false;

            if (pivot != new Vector2(-1, -1))
            {
                textureSettings.spriteAlignment = (int)SpriteAlignment.Custom;
            }

            importer.SetTextureSettings(textureSettings);

            if (slice)
            {
                SliceSprite(importer, path, texture, cellSize, pivot, frameCount, sourceTextureSize);
            }
            else
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }

        static void SliceSprite(TextureImporter importer, string path, Texture2D texture, Vector2Int cellSize, Vector2 pivot, int frameCount, Vector2Int sourceTextureSize)
        {
            if (cellSize.x <= 0 || cellSize.y <= 0)
            {
                Debug.LogError("Sprite slicing failed because the capture cell size is invalid. Set both Width and Height above 0, then capture again.");
                return;
            }

            int textureWidth = sourceTextureSize.x > 0 ? sourceTextureSize.x : texture.width;
            int textureHeight = sourceTextureSize.y > 0 ? sourceTextureSize.y : texture.height;
            int safeFrameCount = Mathf.Max(1, frameCount);
            int gridColumns = Mathf.Max(1, textureWidth / cellSize.x);
            int gridRows = Mathf.Max(1, textureHeight / cellSize.y);
            int maxFramesInAtlas = gridColumns * gridRows;
            safeFrameCount = Mathf.Min(safeFrameCount, maxFramesInAtlas);

            string filenameNoExtension = Path.GetFileNameWithoutExtension(path);
            var metas = new List<SpriteMetaData>();
            for (int frameIndex = 0; frameIndex < safeFrameCount; frameIndex++)
            {
                int column = frameIndex % gridColumns;
                int rowFromTop = frameIndex / gridColumns;
                int x = column * cellSize.x;
                int y = textureHeight - ((rowFromTop + 1) * cellSize.y);

                var meta = new SpriteMetaData
                {
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = pivot,
                    rect = new Rect(x, y, cellSize.x, cellSize.y),
                    name = filenameNoExtension + "_" + frameIndex
                };
                metas.Add(meta);
            }

            if (TryApplySpriteRectsWithDataProvider(texture, metas))
            {
                importer.SaveAndReimport();
                return;
            }

            #pragma warning disable CS0618
            importer.spritesheet = metas.ToArray();
            #pragma warning restore CS0618
            importer.SaveAndReimport();
        }

        static bool TryApplySpriteRectsWithDataProvider(Texture2D texture, List<SpriteMetaData> metas)
        {
            var dataProviderFactory = new SpriteDataProviderFactories();
            dataProviderFactory.Init();

            ISpriteEditorDataProvider dataProvider = dataProviderFactory.GetSpriteEditorDataProviderFromObject(texture);
            if (dataProvider == null)
            {
                return false;
            }

            dataProvider.InitSpriteEditorDataProvider();

            var spriteRects = new List<SpriteRect>(metas.Count);
            var nameFileIdPairs = new List<SpriteNameFileIdPair>(metas.Count);

            for (int i = 0; i < metas.Count; i++)
            {
                SpriteMetaData meta = metas[i];
                GUID spriteGuid = GUID.Generate();

                spriteRects.Add(new SpriteRect
                {
                    name = meta.name,
                    rect = meta.rect,
                    alignment = SpriteAlignment.Custom,
                    pivot = meta.pivot,
                    spriteID = spriteGuid
                });

                var nameFileIdPair = new SpriteNameFileIdPair
                {
                    name = meta.name
                };
                nameFileIdPair.SetFileGUID(spriteGuid);
                nameFileIdPairs.Add(nameFileIdPair);
            }

            dataProvider.SetSpriteRects(spriteRects.ToArray());

            if (dataProvider.HasDataProvider(typeof(ISpriteNameFileIdDataProvider)))
            {
                ISpriteNameFileIdDataProvider nameFileIdDataProvider = dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
                nameFileIdDataProvider.SetNameFileIdPairs(nameFileIdPairs);
            }

            dataProvider.Apply();
            return true;
        }

    }
}
#endif
