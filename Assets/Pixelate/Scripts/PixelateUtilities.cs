using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Pixelate
{
    public class PixelateUtilities
    {
        public const string DefaultExportFolderAssetPath = "Assets";
        public const string DefaultExportFolderDisplayPath = "Assets/";

        private const string PaletteSnapShaderName = "Pixelate/AutoPalletteSnapping";

        public static Dictionary<Color32, int> GetOpaquePaletteCounts(Texture2D tex)
        {
            Dictionary<Color32, int> colorApparitions = new Dictionary<Color32, int>();
            if (tex == null)
            {
                return colorApparitions;
            }

            Color32[] pixelData = tex.GetPixels32(0);
            for (int i = 0; i < pixelData.Length; i++)
            {
                if (pixelData[i].a > 250)
                {
                    if (colorApparitions.ContainsKey(pixelData[i]))
                    {
                        colorApparitions[pixelData[i]] = colorApparitions[pixelData[i]] + 1;
                    }
                    else
                    {
                        colorApparitions.Add(pixelData[i], 1);
                    }
                }
            }

            return colorApparitions;
        }


        public static float ColorDiff(Color32 x, Color32 y)
        {
            float rdif = x.r - y.r;
            float gdif = x.g - y.g;
            float bdif = x.b - y.b;
            float adif = x.a - y.a;
            return Mathf.Sqrt((rdif * rdif) + (gdif * gdif) + (bdif * bdif) + (adif * adif));
        }


        public static List<string> GetPaletteValidationIssues(Texture2D paletteTex)
        {
            var issues = new List<string>();

            if (paletteTex == null)
            {
                issues.Add("Assign a custom palette texture.");
                return issues;
            }

            if (paletteTex.height != 1)
            {
                issues.Add("Texture height must be 1 pixel.");
            }

#if UNITY_EDITOR
            issues.AddRange(GetPaletteImportSettingIssues(paletteTex));
#endif

            return issues;
        }

#if UNITY_EDITOR
        public static List<string> GetPaletteImportSettingIssues(Texture2D paletteTex)
        {
            var issues = new List<string>();

            if (paletteTex == null)
            {
                return issues;
            }

            string manualPalettePath = AssetDatabase.GetAssetPath(paletteTex);
            if (string.IsNullOrEmpty(manualPalettePath))
            {
                issues.Add("Texture must be saved inside this Unity project.");
                return issues;
            }

            TextureImporter texImporter = TextureImporter.GetAtPath(manualPalettePath) as TextureImporter;
            if (texImporter == null)
            {
                return issues;
            }

            if (IsPaletteTextureTypeCompatible(texImporter.textureType) == false)
            {
                issues.Add("Texture Type must be Default or Sprite (2D and UI).");
            }
            if (texImporter.sRGBTexture == false)
            {
                issues.Add("sRGB (Color Texture) must be enabled.");
            }
            if ((Mathf.IsPowerOfTwo(paletteTex.width) == false) && (texImporter.npotScale != TextureImporterNPOTScale.None))
            {
                issues.Add("Non-Power of 2 must be None.");
            }
            if (texImporter.streamingMipmaps == true)
            {
                issues.Add("Streaming Mip Maps must be disabled.");
            }
            if (texImporter.vtOnly == true)
            {
                issues.Add("Virtual Texture Only must be disabled.");
            }
            if (texImporter.mipmapEnabled == true)
            {
                issues.Add("Generate Mip Maps must be disabled.");
            }
            if (texImporter.wrapMode != TextureWrapMode.Clamp)
            {
                issues.Add("Wrap Mode must be Clamp.");
            }
            if (texImporter.filterMode != FilterMode.Point)
            {
                issues.Add("Filter Mode must be Point (no filter).");
            }
            if (texImporter.textureCompression != TextureImporterCompression.Uncompressed)
            {
                issues.Add("Compression must be None.");
            }

            return issues;
        }

        public static string FormatPaletteValidationWarning(List<string> issues)
        {
            if (issues == null || issues.Count == 0)
            {
                return string.Empty;
            }

            return "Custom palette import settings are invalid:\n- " + string.Join("\n- ", issues);
        }

        public static bool HasFixablePaletteImportSettingIssues(Texture2D paletteTex)
        {
            if (TryGetPaletteTextureImporter(paletteTex, out TextureImporter texImporter, out _) == false)
            {
                return false;
            }

            return IsPaletteTextureTypeCompatible(texImporter.textureType) == false
                || texImporter.sRGBTexture == false
                || ((Mathf.IsPowerOfTwo(paletteTex.width) == false) && texImporter.npotScale != TextureImporterNPOTScale.None)
                || texImporter.streamingMipmaps == true
                || texImporter.vtOnly == true
                || texImporter.mipmapEnabled == true
                || texImporter.wrapMode != TextureWrapMode.Clamp
                || texImporter.filterMode != FilterMode.Point
                || texImporter.textureCompression != TextureImporterCompression.Uncompressed;
        }

        public static bool FixPaletteImportSettings(Texture2D paletteTex)
        {
            if (TryGetPaletteTextureImporter(paletteTex, out TextureImporter texImporter, out _) == false)
            {
                return false;
            }

            ApplyPaletteImportSettings(texImporter, preserveSpriteTextureType: true);
            texImporter.SaveAndReimport();
            return true;
        }

        public static void ApplyPaletteImportSettings(TextureImporter texImporter, bool preserveSpriteTextureType)
        {
            if (texImporter == null)
            {
                return;
            }

            if (preserveSpriteTextureType == false || IsPaletteTextureTypeCompatible(texImporter.textureType) == false)
            {
                texImporter.textureType = TextureImporterType.Default;
                texImporter.spriteImportMode = SpriteImportMode.Single;
            }

            texImporter.sRGBTexture = true;
            texImporter.npotScale = TextureImporterNPOTScale.None;
            texImporter.streamingMipmaps = false;
            texImporter.vtOnly = false;
            texImporter.mipmapEnabled = false;
            texImporter.wrapMode = TextureWrapMode.Clamp;
            texImporter.filterMode = FilterMode.Point;
            texImporter.textureCompression = TextureImporterCompression.Uncompressed;
            texImporter.alphaSource = TextureImporterAlphaSource.FromInput;
        }

        private static bool TryGetPaletteTextureImporter(Texture2D paletteTex, out TextureImporter texImporter, out string assetPath)
        {
            texImporter = null;
            assetPath = paletteTex != null ? AssetDatabase.GetAssetPath(paletteTex) : string.Empty;
            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            texImporter = TextureImporter.GetAtPath(assetPath) as TextureImporter;
            return texImporter != null;
        }

        private static bool IsPaletteTextureTypeCompatible(TextureImporterType textureType)
        {
            return textureType == TextureImporterType.Default
                || textureType == TextureImporterType.Sprite;
        }
#endif

        public static bool IsPaletteValid(Texture2D paletteTex)
        {
            return GetPaletteValidationIssues(paletteTex).Count == 0;
        }

        static public Texture2D DrawPalette32(Color32[] source)
        {
            Texture2D output = new Texture2D(source.Length, 1, TextureFormat.ARGB32, false, false);
            output.filterMode = FilterMode.Point;
            output.wrapMode = TextureWrapMode.Clamp;
            output.SetPixels32(source);
            output.Apply();
            return output;
        }
        private struct PalElement
        {
            public Color32 col;
            public int count;
            public float edgeImportance;
        }

        private struct PaletteCandidate
        {
            public PalElement element;
            public Vector3 lab;
            public float saturation;
            public float structure;
            public float importance;
        }

        static int ComparePalElementDescending(PalElement a, PalElement b)
        {
            return b.count - a.count;
        }

        public static Color32[] GenerateFallbackPalette(Texture2D source, int paletteCount, float consolidationThreshold)
        {
            Dictionary<Color32, int> paletteCounts = GetOpaquePaletteCounts(source);
            if (paletteCounts.Count == 0)
            {
                return new[] { new Color32(0, 0, 0, 255) };
            }

            Dictionary<Color32, float> edgeImportance = GetOpaquePaletteEdgeImportance(source);
            List<PalElement> consolidated = ConsolidatePaletteElements(paletteCounts, edgeImportance, consolidationThreshold);
            if (consolidated.Count <= paletteCount)
            {
                return ToColorArray(consolidated);
            }

            return SelectRepresentativePalette(consolidated, paletteCount);
        }

        private static List<PalElement> ConsolidatePaletteElements(Dictionary<Color32, int> paletteIn, Dictionary<Color32, float> edgeImportance, float thresh)
        {
            PalElement[] paletteArr = new PalElement[paletteIn.Count];

            int index = 0;
            foreach (KeyValuePair<Color32, int> pair in paletteIn)
            {
                paletteArr[index].col = pair.Key;
                paletteArr[index].count = pair.Value;
                paletteArr[index].edgeImportance = edgeImportance != null && edgeImportance.TryGetValue(pair.Key, out float contribution) ? contribution : 0f;
                index++;
            }

            System.Array.Sort(paletteArr, ComparePalElementDescending);
            List<PalElement> paletteList = new List<PalElement>();
            bool[] merged = new bool[paletteArr.Length];

            for (int i = 0; i < paletteArr.Length; i++)
            {
                if (merged[i])
                {
                    continue;
                }

                merged[i] = true;
                Color32 seedColor = paletteArr[i].col;
                float rWeightedSum = 0f;
                float gWeightedSum = 0f;
                float bWeightedSum = 0f;
                int totalCount = 0;
                float totalEdgeImportance = 0f;

                totalCount += paletteArr[i].count;
                totalEdgeImportance += paletteArr[i].edgeImportance;
                rWeightedSum += paletteArr[i].col.r * paletteArr[i].count;
                gWeightedSum += paletteArr[i].col.g * paletteArr[i].count;
                bWeightedSum += paletteArr[i].col.b * paletteArr[i].count;

                for (int j = i + 1; j < paletteArr.Length; j++)
                {
                    if (merged[j] || PixelateUtilities.ColorDiff(seedColor, paletteArr[j].col) > thresh)
                    {
                        continue;
                    }

                    merged[j] = true;
                    totalCount += paletteArr[j].count;
                    totalEdgeImportance += paletteArr[j].edgeImportance;
                    rWeightedSum += paletteArr[j].col.r * paletteArr[j].count;
                    gWeightedSum += paletteArr[j].col.g * paletteArr[j].count;
                    bWeightedSum += paletteArr[j].col.b * paletteArr[j].count;
                }

                paletteList.Add(new PalElement
                {
                    col = new Color32(
                        (byte)Mathf.Clamp(Mathf.RoundToInt(rWeightedSum / Mathf.Max(1, totalCount)), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(gWeightedSum / Mathf.Max(1, totalCount)), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(bWeightedSum / Mathf.Max(1, totalCount)), 0, 255),
                        255),
                    count = totalCount,
                    edgeImportance = totalEdgeImportance,
                });
            }

            paletteList.Sort(ComparePalElementDescending);
            return paletteList;
        }

        private static Color32[] SelectRepresentativePalette(List<PalElement> paletteList, int paletteCount)
        {
            List<PaletteCandidate> candidates = BuildPaletteCandidates(paletteList);
            List<PalElement> selected = BuildWeightedKMeansPalette(candidates, paletteCount);
            return ToColorArray(selected);
        }

        public static int GetRecommendedPaletteColorCount(Texture2D source, float consolidationThreshold, PixelateCaptureManager.AutoPaletteDetail detail)
        {
            return PixelateCaptureManager.ResolveAutoPaletteDetailColorCount(
                detail,
                PixelateCaptureManager.MediumAutoPaletteColorCount);
        }

        private static Color32[] ToColorArray(List<PalElement> paletteList)
        {
            Color32[] output = new Color32[paletteList.Count];
            for (int i = 0; i < paletteList.Count; i++)
            {
                output[i] = paletteList[i].col;
            }

            return output;
        }

        private static Dictionary<Color32, float> GetOpaquePaletteEdgeImportance(Texture2D tex)
        {
            Dictionary<Color32, float> edgeImportance = new Dictionary<Color32, float>();
            if (tex == null)
            {
                return edgeImportance;
            }

            int width = tex.width;
            int height = tex.height;
            Color32[] pixelData = tex.GetPixels32(0);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width) + x;
                    Color32 current = pixelData[index];
                    if (current.a <= 250)
                    {
                        continue;
                    }

                    if (x + 1 < width)
                    {
                        AddEdgeContribution(edgeImportance, current, pixelData[index + 1]);
                    }

                    if (y + 1 < height)
                    {
                        AddEdgeContribution(edgeImportance, current, pixelData[index + width]);
                    }
                }
            }

            return edgeImportance;
        }

        private static void AddEdgeContribution(Dictionary<Color32, float> edgeImportance, Color32 a, Color32 b)
        {
            if (a.a <= 250 || b.a <= 250 || a.Equals(b))
            {
                return;
            }

            float contribution = Mathf.Max(1f, ColorDiff(a, b) / 48f);
            AddEdgeImportance(edgeImportance, a, contribution);
            AddEdgeImportance(edgeImportance, b, contribution);
        }

        private static void AddEdgeImportance(Dictionary<Color32, float> edgeImportance, Color32 color, float contribution)
        {
            if (edgeImportance.TryGetValue(color, out float currentContribution))
            {
                edgeImportance[color] = currentContribution + contribution;
                return;
            }

            edgeImportance.Add(color, contribution);
        }

        private static List<PaletteCandidate> BuildPaletteCandidates(List<PalElement> paletteList)
        {
            List<PaletteCandidate> candidates = new List<PaletteCandidate>(paletteList.Count);
            float totalCount = 0f;
            float totalEdgeImportance = 0f;
            for (int i = 0; i < paletteList.Count; i++)
            {
                totalCount += paletteList[i].count;
                totalEdgeImportance += paletteList[i].edgeImportance;
            }

            totalCount = Mathf.Max(1f, totalCount);
            totalEdgeImportance = Mathf.Max(1f, totalEdgeImportance);

            for (int i = 0; i < paletteList.Count; i++)
            {
                Color color = paletteList[i].col;
                Color.RGBToHSV(color, out _, out float saturation, out _);
                float coverage = paletteList[i].count / totalCount;
                float structure = paletteList[i].edgeImportance / totalEdgeImportance;
                float importance = (coverage * 0.6f) + (structure * 0.4f);

                candidates.Add(new PaletteCandidate
                {
                    element = paletteList[i],
                    lab = RgbToLab(color),
                    saturation = saturation,
                    structure = structure,
                    importance = importance,
                });
            }

            return candidates;
        }

        private static List<PalElement> BuildWeightedKMeansPalette(List<PaletteCandidate> candidates, int paletteCount)
        {
            int clusterCount = Mathf.Clamp(paletteCount, 1, candidates.Count);
            List<Vector3> centroids = InitializePerceptualCentroids(candidates, clusterCount);
            int[] assignments = new int[candidates.Count];
            for (int i = 0; i < assignments.Length; i++)
            {
                assignments[i] = -1;
            }

            const int maxIterations = 12;
            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                bool changed = false;
                for (int i = 0; i < candidates.Count; i++)
                {
                    int nearestCentroid = 0;
                    float nearestDistance = float.MaxValue;
                    for (int j = 0; j < centroids.Count; j++)
                    {
                        float distance = Vector3.SqrMagnitude(candidates[i].lab - centroids[j]);
                        if (distance < nearestDistance)
                        {
                            nearestDistance = distance;
                            nearestCentroid = j;
                        }
                    }

                    if (assignments[i] != nearestCentroid)
                    {
                        assignments[i] = nearestCentroid;
                        changed = true;
                    }
                }

                Vector3[] centroidSums = new Vector3[centroids.Count];
                float[] centroidWeights = new float[centroids.Count];
                for (int i = 0; i < candidates.Count; i++)
                {
                    float weight = GetClusterWeight(candidates[i]);
                    int centroidIndex = assignments[i];
                    centroidSums[centroidIndex] += candidates[i].lab * weight;
                    centroidWeights[centroidIndex] += weight;
                }

                for (int j = 0; j < centroids.Count; j++)
                {
                    if (centroidWeights[j] > 0f)
                    {
                        centroids[j] = centroidSums[j] / centroidWeights[j];
                    }
                }

                if (changed == false)
                {
                    break;
                }
            }

            return PickCentroidRepresentatives(candidates, centroids, assignments, paletteCount);
        }

        private static List<Vector3> InitializePerceptualCentroids(List<PaletteCandidate> candidates, int clusterCount)
        {
            List<Vector3> centroids = new List<Vector3>(clusterCount);
            List<int> chosenIndices = new List<int>(clusterCount);

            int firstIndex = 0;
            float bestImportance = float.MinValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].importance > bestImportance)
                {
                    bestImportance = candidates[i].importance;
                    firstIndex = i;
                }
            }

            chosenIndices.Add(firstIndex);
            centroids.Add(candidates[firstIndex].lab);

            while (centroids.Count < clusterCount)
            {
                int bestIndex = -1;
                float bestScore = float.MinValue;
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (chosenIndices.Contains(i))
                    {
                        continue;
                    }

                    float minDistance = float.MaxValue;
                    for (int j = 0; j < chosenIndices.Count; j++)
                    {
                        float distance = Vector3.Distance(candidates[i].lab, candidates[chosenIndices[j]].lab);
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                        }
                    }

                    float score = minDistance * (0.35f + (0.65f * candidates[i].importance));
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestIndex = i;
                    }
                }

                if (bestIndex < 0)
                {
                    break;
                }

                chosenIndices.Add(bestIndex);
                centroids.Add(candidates[bestIndex].lab);
            }

            return centroids;
        }

        private static float GetClusterWeight(PaletteCandidate candidate)
        {
            float countWeight = Mathf.Max(1f, candidate.element.count);
            float structureBoost = 1f + (candidate.structure * 0.75f);
            return countWeight * structureBoost;
        }

        private static List<PalElement> PickCentroidRepresentatives(List<PaletteCandidate> candidates, List<Vector3> centroids, int[] assignments, int paletteCount)
        {
            List<PalElement> selected = new List<PalElement>(paletteCount);
            bool[] used = new bool[candidates.Count];

            for (int centroidIndex = 0; centroidIndex < centroids.Count; centroidIndex++)
            {
                int bestIndex = -1;
                float bestScore = float.MinValue;
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (used[i] || assignments[i] != centroidIndex)
                    {
                        continue;
                    }

                    float distance = Vector3.Distance(candidates[i].lab, centroids[centroidIndex]);
                    float score = (candidates[i].importance * 0.55f) + (candidates[i].structure * 0.15f) + (candidates[i].saturation * 0.10f) + Mathf.Clamp01(distance / 32f) * -0.80f;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestIndex = i;
                    }
                }

                if (bestIndex >= 0)
                {
                    used[bestIndex] = true;
                    selected.Add(candidates[bestIndex].element);
                }
            }

            while (selected.Count < paletteCount)
            {
                int bestIndex = -1;
                float bestScore = float.MinValue;
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (used[i])
                    {
                        continue;
                    }

                    float minDistance = float.MaxValue;
                    for (int j = 0; j < selected.Count; j++)
                    {
                        float distance = Vector3.Distance(candidates[i].lab, RgbToLab(selected[j].col));
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                        }
                    }

                    float score = (candidates[i].importance * 0.55f) + ((minDistance / 60f) * 0.45f);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestIndex = i;
                    }
                }

                if (bestIndex < 0)
                {
                    break;
                }

                used[bestIndex] = true;
                selected.Add(candidates[bestIndex].element);
            }

            selected.Sort(ComparePalElementDescending);
            return selected;
        }

        private static Vector3 RgbToLab(Color color)
        {
            float r = SrgbToLinear(color.r);
            float g = SrgbToLinear(color.g);
            float b = SrgbToLinear(color.b);

            float x = (r * 0.4124f) + (g * 0.3576f) + (b * 0.1805f);
            float y = (r * 0.2126f) + (g * 0.7152f) + (b * 0.0722f);
            float z = (r * 0.0193f) + (g * 0.1192f) + (b * 0.9505f);

            const float referenceX = 0.95047f;
            const float referenceY = 1.00000f;
            const float referenceZ = 1.08883f;

            float fx = PivotLab(x / referenceX);
            float fy = PivotLab(y / referenceY);
            float fz = PivotLab(z / referenceZ);

            return new Vector3(
                Mathf.Max(0f, (116f * fy) - 16f),
                500f * (fx - fy),
                200f * (fy - fz));
        }

        private static float SrgbToLinear(float channel)
        {
            if (channel <= 0.04045f)
            {
                return channel / 12.92f;
            }

            return Mathf.Pow((channel + 0.055f) / 1.055f, 2.4f);
        }

        private static float PivotLab(float channel)
        {
            if (channel > 0.008856f)
            {
                return Mathf.Pow(channel, 1f / 3f);
            }

            return (7.787f * channel) + (16f / 116f);
        }


        static public void LimitPalette(ref Texture2D target, ref Texture2D palette, ref RenderTexture blitRendTexture)
        {
            if (target == null || palette == null)
            {
                return;
            }

            Shader snapPaletteShader = Shader.Find(PaletteSnapShaderName);
            if (snapPaletteShader == null)
            {
                Debug.LogError($"Pixelate could not find the palette snapping shader '{PaletteSnapShaderName}'. Reimport Pixelate or restore the shader before using palette capture.");
                return;
            }

            if (blitRendTexture && blitRendTexture.IsCreated())
            {
                RenderTexture.active = null;
                blitRendTexture.Release();
            }
            blitRendTexture = new RenderTexture(target.width, target.height, 0, RenderTextureFormat.ARGB32, 0);
            blitRendTexture.filterMode = FilterMode.Bilinear;
            blitRendTexture.antiAliasing = 16;
            blitRendTexture.Create();

            var savedActive = RenderTexture.active;
            Material blitMat = new Material(snapPaletteShader);
            blitMat.SetTexture("_MainTex", target);
            blitMat.SetTexture("_Pallette", palette);
            Graphics.Blit(target, blitRendTexture, blitMat);


            RenderTexture.active = blitRendTexture;
            target.ReadPixels(new Rect(0, 0, blitRendTexture.width, blitRendTexture.height), 0, 0);
            target.Apply();
            RenderTexture.active = savedActive;
            blitRendTexture.Release();
            Object.DestroyImmediate(blitMat);
        }


        static public void CalculateCellSizes(ref int cellsPerRow, ref int rowN, ref Vector2Int finalTextureSize, bool singleFrameMode, int cellWidth, int cellHeight, int totalCellCount, bool showExtraDebugInfo)
        {
            if (singleFrameMode)
            {
                finalTextureSize = new Vector2Int(cellWidth, cellHeight);
            }
            else
            {
                // Computing what size our output texture needs to be. 
                int currentSizeCheck = 64;
                while (true)
                {
                    cellsPerRow = currentSizeCheck / cellWidth;
                    rowN = currentSizeCheck / cellHeight;
                    if (cellsPerRow * rowN >= totalCellCount)
                    {
                        finalTextureSize = new Vector2Int(currentSizeCheck, currentSizeCheck);
                        break;
                    }
                    else
                    {
                        currentSizeCheck *= 2;
                    }
                }
            }
        }

        public static int ToFlat(int i, int j, int width)
        {
            return i * width + j;
        }




        public static string GetSavePathNoOverwrite(string sourceAssetPath)
        {
#if UNITY_EDITOR
            int extraVersionsCounter = 0;
            string bPath, ext;
            SplitAwayExtension(sourceAssetPath, out bPath, out ext);
            while (true)
            {
                string path = extraVersionsCounter == 0 ? sourceAssetPath : bPath + "_" + extraVersionsCounter.ToString() + ext;

                Texture2D found = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (found == null)
                {
                    return path;
                }
                extraVersionsCounter++;
            }
#else
            return sourceAssetPath;
#endif
        }

        public static int GetSaveCounterNoOverwrite(string sourceAssetPath)
        {
#if UNITY_EDITOR
            int extraVersionsCounter = 0;
            string bPath, ext;
            SplitAwayExtension(sourceAssetPath, out bPath, out ext);
            while (true)
            {
                string path = extraVersionsCounter == 0 ? sourceAssetPath : bPath + "_" + extraVersionsCounter.ToString() + ext;

                Texture2D found = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (found == null)
                {
                    return extraVersionsCounter;
                }
                extraVersionsCounter++;
            }
#else
            return 0;
#endif
        }

        public static void SplitAwayExtension(string s, out string basePathName, out string extension)
        {
            for (int i = s.Length - 1; i >= 0; i--)
            {
                if (s[i] == '.')
                {
                    basePathName = s.Substring(0, i);
                    extension = s.Substring(i);
                    return;
                }
            }

            Debug.LogError("String doesn't have an extension");
            basePathName = "";
            extension = "";
            return;
        }

        public static bool TryGetProjectFolderAssetPath(string folderPath, out string assetFolderPath, out string validationMessage)
        {
            assetFolderPath = string.Empty;
            validationMessage = string.Empty;

            if (TryGetAssetRelativeFolderPath(folderPath, out assetFolderPath, out validationMessage, out bool handledAssetRelativePath))
            {
                return true;
            }

            if (handledAssetRelativePath)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(folderPath))
            {
                validationMessage = "Choose a sprite export folder inside this Unity project's Assets folder before capturing.";
                return false;
            }

            string normalizedFolderPath;
            string normalizedAssetsPath;
            try
            {
                normalizedFolderPath = Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                normalizedAssetsPath = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch (System.ArgumentException)
            {
                validationMessage = "Choose a sprite export folder inside this Unity project's Assets folder. Pixelate does not export outside the Unity project.";
                return false;
            }
            catch (System.NotSupportedException)
            {
                validationMessage = "Choose a sprite export folder inside this Unity project's Assets folder. Pixelate does not export outside the Unity project.";
                return false;
            }

            bool isInsideAssets = normalizedFolderPath.Equals(normalizedAssetsPath, System.StringComparison.OrdinalIgnoreCase)
                || normalizedFolderPath.StartsWith(normalizedAssetsPath + Path.DirectorySeparatorChar, System.StringComparison.OrdinalIgnoreCase)
                || normalizedFolderPath.StartsWith(normalizedAssetsPath + Path.AltDirectorySeparatorChar, System.StringComparison.OrdinalIgnoreCase);

            if (!isInsideAssets)
            {
                validationMessage = "Choose a sprite export folder inside this Unity project's Assets folder. Pixelate does not export outside the Unity project.";
                return false;
            }

            assetFolderPath = "Assets" + normalizedFolderPath.Substring(normalizedAssetsPath.Length).Replace('\\', '/');
            if (string.IsNullOrEmpty(assetFolderPath))
            {
                assetFolderPath = "Assets";
            }

#if UNITY_EDITOR
            if (AssetDatabase.IsValidFolder(assetFolderPath) == false)
            {
                validationMessage = "The selected export folder no longer exists in the Unity project. Choose or create a valid folder inside Assets before capturing.";
                return false;
            }
#endif

            return true;
        }

        public static string GetAbsoluteProjectFolderPathOrDefault(string folderPath)
        {
            if (TryGetProjectFolderAssetPath(folderPath, out string assetFolderPath, out _))
            {
                return GetAbsolutePathFromAssetFolderPath(assetFolderPath);
            }

            return Application.dataPath;
        }

        public static string GetAbsolutePathFromAssetFolderPath(string assetFolderPath)
        {
            string normalizedAssetPath = string.IsNullOrWhiteSpace(assetFolderPath)
                ? DefaultExportFolderAssetPath
                : assetFolderPath.Replace('\\', '/').TrimEnd('/');

            if (normalizedAssetPath == DefaultExportFolderAssetPath)
            {
                return Application.dataPath;
            }

            if (normalizedAssetPath.StartsWith(DefaultExportFolderAssetPath + "/", System.StringComparison.Ordinal))
            {
                string relativeAssetPath = normalizedAssetPath.Substring((DefaultExportFolderAssetPath + "/").Length)
                    .Replace('/', Path.DirectorySeparatorChar);
                return Path.Combine(Application.dataPath, relativeAssetPath);
            }

            return Application.dataPath;
        }

        private static bool TryGetAssetRelativeFolderPath(string folderPath, out string assetFolderPath, out string validationMessage, out bool handled)
        {
            assetFolderPath = string.Empty;
            validationMessage = string.Empty;
            handled = false;

            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return false;
            }

            string normalizedPath = folderPath.Replace('\\', '/').TrimEnd('/');
            if (normalizedPath != DefaultExportFolderAssetPath
                && normalizedPath.StartsWith(DefaultExportFolderAssetPath + "/", System.StringComparison.Ordinal) == false)
            {
                return false;
            }

            handled = true;
#if UNITY_EDITOR
            if (AssetDatabase.IsValidFolder(normalizedPath))
            {
                assetFolderPath = normalizedPath;
                return true;
            }

            validationMessage = "The selected export folder no longer exists in the Unity project. Choose or create a valid folder inside Assets before capturing.";
            return false;
#else
            assetFolderPath = normalizedPath;
            return true;
#endif
        }

    }
}
