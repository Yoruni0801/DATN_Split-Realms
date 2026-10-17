using System;
using UnityEngine;

namespace Bao.IceSigil
{
    public readonly struct CarveStrokeSample
    {
        public readonly int touchedPixels;
        public readonly int outsidePixels;
        public readonly int repeatedPixels;
        public readonly int deepPixels;
        public readonly Vector2 uv;

        public bool HasPaint => touchedPixels > 0;
        public float OutsideRatio => touchedPixels > 0 ? outsidePixels / (float)touchedPixels : 0f;
        public float RepeatRatio => touchedPixels > 0 ? repeatedPixels / (float)touchedPixels : 0f;

        public CarveStrokeSample(int touchedPixels, int outsidePixels, int repeatedPixels, int deepPixels, Vector2 uv)
        {
            this.touchedPixels = touchedPixels;
            this.outsidePixels = outsidePixels;
            this.repeatedPixels = repeatedPixels;
            this.deepPixels = deepPixels;
            this.uv = uv;
        }
    }

    public sealed class CarvingMaskPainter
    {
        private const float IdealSpeedMin = 180f;
        private const float IdealSpeedMax = 760f;

        private readonly int resolution;
        private readonly Color32[] overlayPixels;
        private readonly Texture2D overlayTexture;
        private Vector2 previousUv;
        private bool hasPreviousUv;
        private bool overlayDirty = true;

        public byte[] PlayerCarveMask { get; }
        public byte[] DepthMask { get; }
        public int[] VisitCount { get; }
        public Texture2D OverlayTexture => overlayTexture;

        public CarvingMaskPainter(int resolution)
        {
            this.resolution = Mathf.Clamp(resolution, 128, 1024);
            int length = this.resolution * this.resolution;
            PlayerCarveMask = new byte[length];
            DepthMask = new byte[length];
            VisitCount = new int[length];
            overlayPixels = new Color32[length];
            overlayTexture = new Texture2D(this.resolution, this.resolution, TextureFormat.RGBA32, false)
            {
                name = "Runtime_PlayerCarveMask",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
        }

        public void Clear()
        {
            Array.Clear(PlayerCarveMask, 0, PlayerCarveMask.Length);
            Array.Clear(DepthMask, 0, DepthMask.Length);
            Array.Clear(VisitCount, 0, VisitCount.Length);
            Array.Clear(overlayPixels, 0, overlayPixels.Length);
            hasPreviousUv = false;
            overlayDirty = true;
            ApplyOverlay();
        }

        public void BeginStroke(Vector2 uv)
        {
            previousUv = ClampUv(uv);
            hasPreviousUv = true;
        }

        public void EndStroke()
        {
            hasPreviousUv = false;
        }

        public CarveStrokeSample PaintTo(Vector2 uv, float screenSpeed, ShapeTemplateRuntime template)
        {
            uv = ClampUv(uv);
            if (!hasPreviousUv)
            {
                BeginStroke(uv);
            }

            Vector2 fromPixel = UvToPixel(previousUv);
            Vector2 toPixel = UvToPixel(uv);
            float radius = Mathf.Max(2f, template.BrushRadiusPixels);
            float distance = Vector2.Distance(fromPixel, toPixel);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / Mathf.Max(1f, radius * 0.42f)));

            int touched = 0;
            int outside = 0;
            int repeated = 0;
            int deep = 0;
            byte depthIncrement = GetDepthIncrement(screenSpeed);

            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(fromPixel, toPixel, i / (float)steps);
                Stamp(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), radius, depthIncrement, template, ref touched, ref outside, ref repeated, ref deep);
            }

            previousUv = uv;
            overlayDirty = true;
            return new CarveStrokeSample(touched, outside, repeated, deep, uv);
        }

        public Texture2D BuildOverlay(ShapeTemplateRuntime template, byte[] damageMask)
        {
            if (!overlayDirty)
            {
                return overlayTexture;
            }

            for (int i = 0; i < overlayPixels.Length; i++)
            {
                byte depth = DepthMask[i];
                byte damage = damageMask != null && i < damageMask.Length ? damageMask[i] : (byte)0;
                if (depth == 0 && damage == 0)
                {
                    overlayPixels[i] = new Color32(0, 0, 0, 0);
                    continue;
                }

                bool valid = template.ValidCarveBandMask[i] > 0;
                bool deep = depth >= ShapeEvaluator.DepthReadyThreshold;
                byte alpha = (byte)Mathf.Clamp(depth + damage * 0.65f, 34f, 230f);

                if (damage > depth)
                {
                    overlayPixels[i] = new Color32(230, 250, 255, (byte)Mathf.Clamp(damage, 80, 240));
                }
                else if (!valid)
                {
                    overlayPixels[i] = new Color32(255, 175, 150, alpha);
                }
                else if (deep)
                {
                    overlayPixels[i] = new Color32(178, 255, 255, alpha);
                }
                else
                {
                    overlayPixels[i] = new Color32(92, 220, 255, alpha);
                }
            }

            ApplyOverlay();
            overlayDirty = false;
            return overlayTexture;
        }

        private void ApplyOverlay()
        {
            overlayTexture.SetPixels32(overlayPixels);
            overlayTexture.Apply(false, false);
        }

        private void Stamp(
            int cx,
            int cy,
            float radius,
            byte depthIncrement,
            ShapeTemplateRuntime template,
            ref int touched,
            ref int outside,
            ref int repeated,
            ref int deep)
        {
            int r = Mathf.CeilToInt(radius);
            float radiusSq = radius * radius;
            for (int y = cy - r; y <= cy + r; y++)
            {
                if (y < 0 || y >= resolution)
                {
                    continue;
                }

                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x < 0 || x >= resolution)
                    {
                        continue;
                    }

                    float dx = x - cx;
                    float dy = y - cy;
                    if (dx * dx + dy * dy > radiusSq)
                    {
                        continue;
                    }

                    int index = y * resolution + x;
                    touched++;
                    if (template.ValidCarveBandMask[index] == 0)
                    {
                        outside++;
                    }

                    if (VisitCount[index] > 5)
                    {
                        repeated++;
                    }

                    int newDepth = Mathf.Clamp(DepthMask[index] + depthIncrement, 0, 255);
                    DepthMask[index] = (byte)newDepth;
                    PlayerCarveMask[index] = 255;
                    VisitCount[index]++;
                    if (newDepth >= ShapeEvaluator.DepthReadyThreshold)
                    {
                        deep++;
                    }
                }
            }
        }

        private byte GetDepthIncrement(float screenSpeed)
        {
            if (screenSpeed > IdealSpeedMax)
            {
                return 24;
            }

            if (screenSpeed < IdealSpeedMin)
            {
                return 44;
            }

            return 36;
        }

        private Vector2 UvToPixel(Vector2 uv)
        {
            return new Vector2(
                Mathf.Clamp01(uv.x) * (resolution - 1),
                Mathf.Clamp01(uv.y) * (resolution - 1));
        }

        private static Vector2 ClampUv(Vector2 uv)
        {
            return new Vector2(Mathf.Clamp01(uv.x), Mathf.Clamp01(uv.y));
        }
    }
}
