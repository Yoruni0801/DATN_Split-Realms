using System;
using UnityEngine;

namespace Bao.IceSigil
{
    public enum IceCarvingDifficulty
    {
        Easy,
        Normal,
        Hard,
        Master
    }

    [CreateAssetMenu(fileName = "IceCarvingTemplate", menuName = "Bao/Ice Sigil/Shape Template")]
    public sealed class ShapeTemplateData : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Ice Sigil";
        public IceCarvingDifficulty difficulty = IceCarvingDifficulty.Normal;

        [Header("Masks")]
        public int maskResolution = 256;
        public Texture2D targetLineMask;
        public Texture2D validCarveBandMask;
        public Texture2D innerAreaMask;

        [Header("Authoring Fallback")]
        public Vector2[] normalizedPolyline;
        public bool closedShape = true;

        [Header("Carving")]
        [Range(2f, 32f)] public float brushRadiusPixels = 9f;
        [Range(4f, 48f)] public float validBandRadiusPixels = 18f;
        [Range(0.01f, 0.2f)] public float evaluationInterval = 0.2f;
        [Range(0.5f, 8f)] public float sampleRevealTime = 3f;

        [Header("Pass Conditions")]
        [Range(0f, 1f)] public float requiredCoverage = 0.88f;
        [Range(0f, 1f)] public float maxOutsideDamage = 0.08f;
        [Range(0f, 1f)] public float requiredContinuity = 0.90f;
        [Range(0f, 1f)] public float requiredDepthCoverage = 0.80f;
        [Range(0f, 1f)] public float requiredClosedRatio = 0.78f;

        [Header("Prying")]
        public Vector2[] normalizedPryPoints;
        [Range(1, 5)] public int requiredPrySuccesses = 3;
        [Range(0f, 1f)] public float baseSafeForceMin = 0.42f;
        [Range(0f, 1f)] public float baseSafeForceMax = 0.68f;

        public int Resolution => Mathf.Clamp(maskResolution, 128, 1024);
    }

    public sealed class ShapeTemplateRuntime
    {
        public string DisplayName { get; private set; }
        public IceCarvingDifficulty Difficulty { get; private set; }
        public int Resolution { get; private set; }
        public byte[] TargetLineMask { get; private set; }
        public byte[] ValidCarveBandMask { get; private set; }
        public byte[] InnerAreaMask { get; private set; }
        public Vector2[] PryPoints { get; private set; }
        public bool ClosedShape { get; private set; }

        public float BrushRadiusPixels { get; private set; }
        public float EvaluationInterval { get; private set; }
        public float SampleRevealTime { get; private set; }
        public float RequiredCoverage { get; private set; }
        public float MaxOutsideDamage { get; private set; }
        public float RequiredContinuity { get; private set; }
        public float RequiredDepthCoverage { get; private set; }
        public float RequiredClosedRatio { get; private set; }
        public int RequiredPrySuccesses { get; private set; }
        public float BaseSafeForceMin { get; private set; }
        public float BaseSafeForceMax { get; private set; }

        public static ShapeTemplateRuntime FromDataOrPolyline(
            ShapeTemplateData data,
            Vector2[] normalizedPolyline,
            Vector2[] normalizedPryPoints,
            int fallbackResolution,
            int variantIndex)
        {
            ShapeTemplateRuntime runtime = new();

            if (data != null)
            {
                runtime.DisplayName = string.IsNullOrWhiteSpace(data.displayName) ? data.name : data.displayName;
                runtime.Difficulty = data.difficulty;
                runtime.Resolution = data.Resolution;
                runtime.ClosedShape = data.closedShape;
                runtime.BrushRadiusPixels = data.brushRadiusPixels;
                runtime.EvaluationInterval = Mathf.Max(0.05f, data.evaluationInterval);
                runtime.SampleRevealTime = Mathf.Max(0.25f, data.sampleRevealTime);
                runtime.RequiredCoverage = data.requiredCoverage;
                runtime.MaxOutsideDamage = data.maxOutsideDamage;
                runtime.RequiredContinuity = data.requiredContinuity;
                runtime.RequiredDepthCoverage = data.requiredDepthCoverage;
                runtime.RequiredClosedRatio = data.requiredClosedRatio;
                runtime.RequiredPrySuccesses = Mathf.Clamp(data.requiredPrySuccesses, 1, 5);
                runtime.BaseSafeForceMin = data.baseSafeForceMin;
                runtime.BaseSafeForceMax = data.baseSafeForceMax;

                Vector2[] authoringLine = data.normalizedPolyline != null && data.normalizedPolyline.Length >= 2
                    ? data.normalizedPolyline
                    : normalizedPolyline;
                runtime.BuildMasks(data.targetLineMask, data.validCarveBandMask, data.innerAreaMask, authoringLine, data.validBandRadiusPixels);
                runtime.PryPoints = BuildPryPoints(data.normalizedPryPoints, normalizedPryPoints, authoringLine);
                return runtime;
            }

            runtime.DisplayName = $"Ice Sigil {variantIndex + 1}";
            runtime.Difficulty = IceCarvingDifficulty.Easy;
            runtime.Resolution = Mathf.Clamp(fallbackResolution, 128, 256);
            runtime.ClosedShape = true;
            runtime.BrushRadiusPixels = 20f;
            runtime.EvaluationInterval = 0.35f;
            runtime.SampleRevealTime = 3f;
            runtime.RequiredCoverage = 0.42f;
            runtime.MaxOutsideDamage = 0.45f;
            runtime.RequiredContinuity = 0.30f;
            runtime.RequiredDepthCoverage = 0.20f;
            runtime.RequiredClosedRatio = 0.0f;
            runtime.RequiredPrySuccesses = 1;
            runtime.BaseSafeForceMin = 0.25f;
            runtime.BaseSafeForceMax = 0.85f;
            runtime.BuildMasks(null, null, null, normalizedPolyline, 78f);
            runtime.PryPoints = BuildPryPoints(null, normalizedPryPoints, normalizedPolyline);
            return runtime;
        }

        private void BuildMasks(Texture2D targetTexture, Texture2D validTexture, Texture2D innerTexture, Vector2[] polyline, float validBandRadius)
        {
            int pixelCount = Resolution * Resolution;
            TargetLineMask = new byte[pixelCount];
            ValidCarveBandMask = new byte[pixelCount];
            InnerAreaMask = new byte[pixelCount];

            bool loadedTarget = TryReadAlphaMask(targetTexture, TargetLineMask);
            bool loadedValid = TryReadAlphaMask(validTexture, ValidCarveBandMask);
            bool loadedInner = TryReadAlphaMask(innerTexture, InnerAreaMask);

            if (!loadedTarget)
            {
                RasterizePolyline(TargetLineMask, polyline, Mathf.Max(10f, BrushRadiusPixels * 1.35f), true);
            }

            if (!loadedValid)
            {
                RasterizePolyline(ValidCarveBandMask, polyline, Mathf.Max(BrushRadiusPixels + 4f, validBandRadius), true);
            }

            if (!loadedInner && ClosedShape && polyline != null && polyline.Length >= 3)
            {
                FillPolygon(InnerAreaMask, polyline);
            }
        }

        private bool TryReadAlphaMask(Texture2D texture, byte[] destination)
        {
            if (texture == null || destination == null)
            {
                return false;
            }

            try
            {
                Color32[] pixels = texture.GetPixels32();
                if (pixels.Length == 0)
                {
                    return false;
                }

                for (int y = 0; y < Resolution; y++)
                {
                    int srcY = Mathf.Clamp(Mathf.RoundToInt(y / (Resolution - 1f) * (texture.height - 1)), 0, texture.height - 1);
                    for (int x = 0; x < Resolution; x++)
                    {
                        int srcX = Mathf.Clamp(Mathf.RoundToInt(x / (Resolution - 1f) * (texture.width - 1)), 0, texture.width - 1);
                        destination[y * Resolution + x] = pixels[srcY * texture.width + srcX].a;
                    }
                }

                return true;
            }
            catch (UnityException)
            {
                Debug.LogWarning($"Ice carving template texture '{texture.name}' is not readable. Falling back to authored polyline.");
                return false;
            }
        }

        private void RasterizePolyline(byte[] mask, Vector2[] polyline, float radius, bool close)
        {
            if (polyline == null || polyline.Length < 2)
            {
                polyline = DefaultNormalizedPolyline();
                close = true;
            }

            for (int i = 0; i < polyline.Length - 1; i++)
            {
                DrawSegment(mask, polyline[i], polyline[i + 1], radius);
            }

            if (close)
            {
                DrawSegment(mask, polyline[^1], polyline[0], radius);
            }
        }

        private void DrawSegment(byte[] mask, Vector2 from, Vector2 to, float radius)
        {
            Vector2 a = UvToPixel(from);
            Vector2 b = UvToPixel(to);
            float distance = Vector2.Distance(a, b);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / Mathf.Max(1f, radius * 0.45f)));
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, i / (float)steps);
                PaintCircle(mask, Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), radius, 255);
            }
        }

        private void Dilate(byte[] source, byte[] destination, float radius)
        {
            Array.Clear(destination, 0, destination.Length);
            int r = Mathf.CeilToInt(radius);
            for (int y = 0; y < Resolution; y++)
            {
                for (int x = 0; x < Resolution; x++)
                {
                    if (source[y * Resolution + x] == 0)
                    {
                        continue;
                    }

                    PaintCircle(destination, x, y, r, 255);
                }
            }
        }

        private void FillPolygon(byte[] mask, Vector2[] polyline)
        {
            for (int y = 0; y < Resolution; y++)
            {
                float v = (y + 0.5f) / Resolution;
                for (int x = 0; x < Resolution; x++)
                {
                    float u = (x + 0.5f) / Resolution;
                    if (PointInPolygon(new Vector2(u, v), polyline))
                    {
                        mask[y * Resolution + x] = 255;
                    }
                }
            }
        }

        private static bool PointInPolygon(Vector2 p, Vector2[] polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[j];
                bool intersects = (a.y > p.y) != (b.y > p.y) &&
                                  p.x < (b.x - a.x) * (p.y - a.y) / Mathf.Max(0.0001f, b.y - a.y) + a.x;
                if (intersects)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private void PaintCircle(byte[] mask, int cx, int cy, float radius, byte value)
        {
            int r = Mathf.CeilToInt(radius);
            float radiusSq = radius * radius;
            for (int y = cy - r; y <= cy + r; y++)
            {
                if (y < 0 || y >= Resolution)
                {
                    continue;
                }

                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x < 0 || x >= Resolution)
                    {
                        continue;
                    }

                    float dx = x - cx;
                    float dy = y - cy;
                    if (dx * dx + dy * dy <= radiusSq)
                    {
                        mask[y * Resolution + x] = value;
                    }
                }
            }
        }

        private Vector2 UvToPixel(Vector2 uv)
        {
            return new Vector2(
                Mathf.Clamp01(uv.x) * (Resolution - 1),
                Mathf.Clamp01(uv.y) * (Resolution - 1));
        }

        private static Vector2[] BuildPryPoints(Vector2[] templatePoints, Vector2[] scenePoints, Vector2[] polyline)
        {
            if (templatePoints != null && templatePoints.Length > 0)
            {
                return templatePoints;
            }

            if (scenePoints != null && scenePoints.Length > 0)
            {
                return scenePoints;
            }

            if (polyline == null || polyline.Length == 0)
            {
                return new[]
                {
                    new Vector2(0.5f, 0.85f),
                    new Vector2(0.78f, 0.5f),
                    new Vector2(0.5f, 0.16f),
                    new Vector2(0.22f, 0.5f)
                };
            }

            Bounds bounds = new(polyline[0], Vector3.zero);
            for (int i = 1; i < polyline.Length; i++)
            {
                bounds.Encapsulate(polyline[i]);
            }

            return new[]
            {
                new Vector2(bounds.center.x, bounds.max.y),
                new Vector2(bounds.max.x, bounds.center.y),
                new Vector2(bounds.center.x, bounds.min.y),
                new Vector2(bounds.min.x, bounds.center.y)
            };
        }

        private static Vector2[] DefaultNormalizedPolyline()
        {
            return new[]
            {
                new Vector2(0.5f, 0.82f),
                new Vector2(0.68f, 0.66f),
                new Vector2(0.72f, 0.44f),
                new Vector2(0.58f, 0.24f),
                new Vector2(0.42f, 0.24f),
                new Vector2(0.28f, 0.44f),
                new Vector2(0.32f, 0.66f)
            };
        }
    }
}
