using System.Collections.Generic;
using UnityEngine;

namespace Quoc.QuyVuong.Boss_Phase3
{
    internal static class SkyLeapVfxUtility
    {
        private const string AncientShaderPath = "Shaders/AncientEarthSigil";

        public static Material CreateAncientMaterial(
            Color edgeColor,
            Color carvedColor,
            float intensity,
            float roughness = 0.48f)
        {
            Shader shader = Resources.Load<Shader>(AncientShaderPath);
            Material material;
            if (shader != null)
            {
                material = new Material(shader)
                {
                    name = "SkyLeap_AncientEarth_Runtime_Material",
                    hideFlags = HideFlags.DontSave
                };
            }
            else
            {
                material = GroundLineVfxUtility.CreateMaterial(edgeColor, carvedColor, intensity);
            }

            if (material == null) return null;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", edgeColor);
            if (material.HasProperty("_CoreColor")) material.SetColor("_CoreColor", carvedColor);
            if (material.HasProperty("_Intensity")) material.SetFloat("_Intensity", intensity);
            if (material.HasProperty("_ChipScale")) material.SetFloat("_ChipScale", 18f);
            if (material.HasProperty("_Roughness")) material.SetFloat("_Roughness", roughness);
            if (material.HasProperty("_BreathSpeed")) material.SetFloat("_BreathSpeed", 0.42f);
            if (material.HasProperty("_FlowSpeed")) material.SetFloat("_FlowSpeed", 0.18f);
            if (material.HasProperty("_PulseSpeed")) material.SetFloat("_PulseSpeed", 0.55f);
            return material;
        }

        public static Material CreateFlatPigmentMaterial(Color tint)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) return CreateAncientMaterial(tint, tint, 0.65f, 0.7f);

            Material material = new Material(shader)
            {
                name = "SkyLeap_Flat_Earth_Pigment",
                color = tint,
                hideFlags = HideFlags.DontSave
            };
            return material;
        }

        public static Sprite CreateWeatheredGroundMark(out Texture2D texture)
        {
            const int width = 192;
            const int height = 96;
            texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "SkyLeap_Weathered_Ground_Mark_Texture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = ((x + 0.5f) / width - 0.5f) * 2f;
                    float ny = ((y + 0.5f) / height - 0.5f) * 2f;
                    float radius = Mathf.Sqrt(nx * nx + ny * ny);
                    float angle = Mathf.Atan2(ny, nx);
                    float grain = Hash01(x, y);
                    float chippedEdge = Mathf.Sin(angle * 7f + 0.7f) * 0.035f
                        + Mathf.Sin(angle * 13f - 0.4f) * 0.018f;

                    Color32 pixel = new Color32(0, 0, 0, 0);
                    if (radius < 0.84f + chippedEdge && grain > 0.10f)
                    {
                        byte alpha = (byte)Mathf.RoundToInt(Mathf.Lerp(12f, 29f, grain));
                        pixel = new Color32(54, 31, 18, alpha);
                    }

                    float sectorFloat = (angle + Mathf.PI) / (Mathf.PI * 2f) * 7f;
                    int sector = Mathf.FloorToInt(sectorFloat);
                    float sectorCenter = sector + 0.5f;
                    float distanceFromCenter = Mathf.Abs(sectorFloat - sectorCenter);
                    float slabInner = 0.61f + Mathf.Sin(sector * 2.31f) * 0.035f;
                    float slabOuter = 0.91f + chippedEdge + Mathf.Cos(sector * 1.77f) * 0.025f;
                    bool insideStoneSlab = radius > slabInner && radius < slabOuter
                        && distanceFromCenter < 0.34f + Mathf.Sin(sector * 1.9f) * 0.035f;
                    if (insideStoneSlab && grain > 0.17f)
                    {
                        byte stoneAlpha = (byte)Mathf.RoundToInt(Mathf.Lerp(82f, 148f, grain));
                        byte warmth = (byte)Mathf.RoundToInt(20f * grain);
                        pixel = new Color32((byte)(92 + warmth), (byte)(51 + warmth / 2), 23, stoneAlpha);
                    }

                    for (int crackIndex = 0; crackIndex < 6; crackIndex++)
                    {
                        float crackAngle = 0.19f + crackIndex * Mathf.PI * 2f / 6f
                            + Mathf.Sin(crackIndex * 2.4f) * 0.12f;
                        float along = nx * Mathf.Cos(crackAngle) + ny * Mathf.Sin(crackAngle);
                        float across = Mathf.Abs(-nx * Mathf.Sin(crackAngle) + ny * Mathf.Cos(crackAngle));
                        float crooked = Mathf.Abs(Mathf.Sin(along * 19f + crackIndex) * 0.012f);
                        if (along > 0.12f && along < 0.88f && across < 0.012f + crooked)
                            pixel = new Color32(31, 14, 7, (byte)Mathf.RoundToInt(115f + grain * 45f));
                    }

                    pixels[y * width + x] = pixel;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                64f,
                0,
                SpriteMeshType.FullRect);
            sprite.name = "SkyLeap_Weathered_Ground_Mark";
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        private static float Hash01(int x, int y)
        {
            uint value = (uint)(x * 374761393 + y * 668265263);
            value = (value ^ (value >> 13)) * 1274126177u;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) / 16777215f;
        }

        public static void SetRoughSeal(
            LineRenderer line,
            float radiusX,
            float radiusY,
            int corners,
            float roughness,
            float phase = 0f)
        {
            if (line == null) return;
            int count = Mathf.Max(5, corners);
            line.loop = true;
            line.positionCount = count;
            for (int i = 0; i < count; i++)
            {
                float angle = phase + i * Mathf.PI * 2f / count;
                float chipped = 1f
                    + Mathf.Sin(i * 2.73f + phase * 1.9f) * roughness
                    + Mathf.Sin(i * 5.17f + 0.8f) * roughness * 0.35f;
                line.SetPosition(i, new Vector3(
                    Mathf.Cos(angle) * radiusX * chipped,
                    Mathf.Sin(angle) * radiusY * chipped,
                    0f));
            }
        }

        public static SkyLeapAerialTrail CreateAerialTrail(Transform owner)
        {
            GameObject effectObject = new GameObject("SkyLeap_Ancient_Stone_Wake");
            effectObject.transform.position = owner != null ? owner.position : Vector3.zero;
            SkyLeapAerialTrail trail = effectObject.AddComponent<SkyLeapAerialTrail>();
            trail.Initialize(owner);
            return trail;
        }

        public static void SpawnTakeoff(Vector3 position)
        {
            GameObject effectObject = new GameObject("SkyLeap_Takeoff_VFX");
            effectObject.transform.position = new Vector3(position.x, position.y, -0.16f);
            effectObject.AddComponent<SkyLeapTakeoffVfx>().Build();
        }

        public static void SpawnLockPulse(Vector3 position, float radius)
        {
            GameObject effectObject = new GameObject("SkyLeap_Target_Lock_Pulse");
            effectObject.transform.position = new Vector3(position.x, position.y, -0.14f);
            effectObject.AddComponent<SkyLeapLockPulseVfx>().Build(radius);
        }

        public static void SpawnImpact(Vector3 position, float radius)
        {
            GameObject effectObject = new GameObject("SkyLeap_Spearfall_Impact_VFX");
            effectObject.transform.position = new Vector3(position.x, position.y, -0.18f);
            effectObject.AddComponent<SkyLeapImpactVfx>().Build(radius);
        }

    }

    internal sealed class SkyLeapAerialTrail : MonoBehaviour
    {
        private readonly List<Material> runtimeMaterials = new();
        private readonly List<LineRenderer> fallingRunes = new();
        private Transform owner;
        private ParticleSystem stoneDust;
        private bool descending;
        private bool fading;
        private float elapsed;
        private float fadeElapsed;

        public void Initialize(Transform followTarget)
        {
            owner = followTarget;
            transform.position = owner != null ? owner.position : transform.position;

            Material earthMaterial = SkyLeapVfxUtility.CreateAncientMaterial(
                new Color(0.11f, 0.048f, 0.018f, 0.78f),
                new Color(0.43f, 0.22f, 0.07f, 0.90f), 0.74f, 0.68f);
            Material runeMaterial = SkyLeapVfxUtility.CreateAncientMaterial(
                new Color(0.27f, 0.105f, 0.025f, 0.78f),
                new Color(0.76f, 0.49f, 0.16f, 0.94f), 0.96f, 0.50f);
            if (earthMaterial != null) runtimeMaterials.Add(earthMaterial);
            if (runeMaterial != null) runtimeMaterials.Add(runeMaterial);

            BuildFallingRunes(runeMaterial);
            CreateStoneDust(earthMaterial);
        }

        public void BeginDescent()
        {
            descending = true;
            if (stoneDust != null)
            {
                ParticleSystem.EmissionModule emission = stoneDust.emission;
                emission.rateOverTime = 52f;
            }
        }

        public void StopAndFade()
        {
            if (fading) return;
            fading = true;
            owner = null;
            fadeElapsed = 0f;
            if (stoneDust != null)
                stoneDust.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (!fading && owner != null)
            {
                transform.position = new Vector3(owner.position.x, owner.position.y, -0.20f);
                if (stoneDust != null) stoneDust.transform.position = transform.position;
            }

            float alpha = 1f;
            if (fading)
            {
                fadeElapsed += Time.deltaTime;
                alpha = 1f - Mathf.Clamp01(fadeElapsed / 0.30f);
                if (fadeElapsed >= 0.30f)
                {
                    Destroy(gameObject);
                    return;
                }
            }

            UpdateFallingRunes(alpha);
        }

        private void BuildFallingRunes(Material material)
        {
            for (int i = 0; i < 4; i++)
            {
                LineRenderer rune = GroundLineVfxUtility.CreateLine(
                    transform, $"Falling_Ancestor_Rune_{i}", material, 27, 0.095f);
                rune.numCapVertices = 1;
                rune.numCornerVertices = 1;
                rune.positionCount = 4;
                float size = 0.13f + i * 0.012f;
                rune.SetPosition(0, new Vector3(-size, size * 0.15f, 0f));
                rune.SetPosition(1, new Vector3(-size * 0.18f, size, 0f));
                rune.SetPosition(2, new Vector3(size, size * 0.22f, 0f));
                rune.SetPosition(3, new Vector3(size * 0.18f, -size * 0.72f, 0f));
                fallingRunes.Add(rune);
            }
        }

        private void UpdateFallingRunes(float alpha)
        {
            for (int i = 0; i < fallingRunes.Count; i++)
            {
                LineRenderer rune = fallingRunes[i];
                float cycleSpeed = descending ? 2.8f : 1.25f;
                float fall = Mathf.Repeat(elapsed * cycleSpeed + i * 0.31f, 1f);
                float side = (i - 1.5f) * 0.32f + Mathf.Sin(elapsed * 2.1f + i * 1.7f) * 0.07f;
                float height = Mathf.Lerp(descending ? 1.20f : 0.72f, -0.58f, fall);
                rune.transform.localPosition = new Vector3(side, height, 0f);
                rune.transform.localRotation = Quaternion.Euler(
                    0f, 0f, Mathf.Sin(elapsed * 1.7f + i * 2.3f) * 16f + i * 9f);
                float appear = Mathf.Sin(fall * Mathf.PI);
                float scale = (descending ? 1.18f : 0.86f) * Mathf.Lerp(0.72f, 1f, appear);
                rune.transform.localScale = Vector3.one * scale;
                Color color = new Color(0.68f, 0.40f, 0.12f,
                    alpha * appear * (descending ? 0.76f : 0.46f));
                rune.startColor = rune.endColor = color;
            }
        }

        private void CreateStoneDust(Material material)
        {
            GameObject particleObject = new GameObject("Falling_Weathered_Stone_Dust");
            particleObject.transform.SetParent(transform, false);
            stoneDust = particleObject.AddComponent<ParticleSystem>();
            stoneDust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = stoneDust.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 0.8f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.62f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.16f, 0.82f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.055f, 0.18f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.68f, 0.39f, 0.13f, 0.66f),
                new Color(0.18f, 0.075f, 0.028f, 0.58f));
            main.gravityModifier = 0.68f;
            main.maxParticles = 92;

            ParticleSystem.EmissionModule emission = stoneDust.emission;
            emission.rateOverTime = 18f;
            ParticleSystem.ShapeModule shape = stoneDust.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.88f, 0.40f, 0.05f);

            ParticleSystem.NoiseModule noise = stoneDust.noise;
            noise.enabled = true;
            noise.strength = 0.24f;
            noise.frequency = 0.34f;
            noise.scrollSpeed = 0.16f;

            ParticleSystem.ColorOverLifetimeModule color = stoneDust.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.66f, 0.39f, 0.14f), 0f),
                    new GradientColorKey(new Color(0.22f, 0.095f, 0.035f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.68f, 0.16f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = gradient;

            ParticleSystemRenderer renderer = particleObject.GetComponent<ParticleSystemRenderer>();
            renderer.sortingOrder = 26;
            if (material != null) renderer.sharedMaterial = material;
            stoneDust.Play(true);
        }

        private void OnDestroy()
        {
            GroundLineVfxUtility.DestroyMaterials(runtimeMaterials);
        }
    }

    internal sealed class SkyLeapTakeoffVfx : MonoBehaviour
    {
        private readonly List<Material> runtimeMaterials = new();
        private float elapsed;
        private const float Lifetime = 0.72f;

        public void Build()
        {
            Material dustMaterial = SkyLeapVfxUtility.CreateFlatPigmentMaterial(Color.white);
            if (dustMaterial != null) runtimeMaterials.Add(dustMaterial);

            ParticleSystem rocks = GroundLineVfxUtility.CreateBurst(
                transform, "Takeoff_Heavy_Stone_Chunks", dustMaterial,
                new Color(0.36f, 0.20f, 0.08f, 0.88f), 24, 0.85f, 4.0f,
                0.08f, 0.24f, 0.34f, 0.72f, 1.05f, 24, 0.52f);
            ApplyEarthFade(rocks,
                new Color(0.44f, 0.25f, 0.095f),
                new Color(0.13f, 0.055f, 0.018f), 0.88f);

            ParticleSystem dust = GroundLineVfxUtility.CreateBurst(
                transform, "Takeoff_Cave_Floor_Dust", dustMaterial,
                new Color(0.40f, 0.29f, 0.19f, 0.52f), 42, 0.22f, 2.15f,
                0.12f, 0.38f, 0.28f, 0.76f, 0.38f, 23, 0.76f);
            ApplyEarthFade(dust,
                new Color(0.50f, 0.37f, 0.25f),
                new Color(0.16f, 0.085f, 0.045f), 0.46f);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed >= Lifetime) Destroy(gameObject);
        }

        private static void ApplyEarthFade(ParticleSystem particles, Color start, Color end, float peakAlpha)
        {
            if (particles == null) return;
            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(peakAlpha, 0.12f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = gradient;
        }

        private void OnDestroy()
        {
            GroundLineVfxUtility.DestroyMaterials(runtimeMaterials);
        }
    }

    internal sealed class SkyLeapLockPulseVfx : MonoBehaviour
    {
        private readonly List<Material> runtimeMaterials = new();
        private float elapsed;
        private const float Lifetime = 0.58f;

        public void Build(float effectRadius)
        {
            float radius = Mathf.Max(0.6f, effectRadius);
            Material material = SkyLeapVfxUtility.CreateFlatPigmentMaterial(Color.white);
            if (material != null) runtimeMaterials.Add(material);

            ParticleSystem dust = GroundLineVfxUtility.CreateBurst(
                transform, "Ancient_Seal_Dust_Stamp", material,
                new Color(0.34f, 0.20f, 0.11f, 0.46f), 26, 0.16f, 1.05f,
                0.10f, 0.28f, 0.24f, 0.55f, 0.42f, 23, radius * 0.34f);
            ApplyEarthFade(dust,
                new Color(0.49f, 0.31f, 0.16f),
                new Color(0.18f, 0.09f, 0.04f), 0.50f);

            ParticleSystem chips = GroundLineVfxUtility.CreateBurst(
                transform, "Ancestor_Seal_Stone_Chips", material,
                new Color(0.39f, 0.22f, 0.09f, 0.72f), 12, 0.45f, 1.85f,
                0.055f, 0.16f, 0.22f, 0.48f, 0.86f, 25, radius * 0.22f);
            ApplyEarthFade(chips,
                new Color(0.48f, 0.28f, 0.10f),
                new Color(0.13f, 0.055f, 0.018f), 0.72f);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed >= Lifetime) Destroy(gameObject);
        }

        private static void ApplyEarthFade(
            ParticleSystem particles,
            Color start,
            Color end,
            float peakAlpha)
        {
            if (particles == null) return;
            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(start, 0f),
                    new GradientColorKey(end, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(peakAlpha, 0.12f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = gradient;
        }

        private void OnDestroy()
        {
            GroundLineVfxUtility.DestroyMaterials(runtimeMaterials);
        }
    }

    internal sealed class SkyLeapImpactVfx : MonoBehaviour
    {
        private readonly List<Material> runtimeMaterials = new();
        private readonly List<LineRenderer> fractures = new();
        private SpriteRenderer groundScar;
        private Texture2D scarTexture;
        private Sprite scarSprite;
        private Vector3 groundScarScale;
        private float radius;
        private float elapsed;
        private const float Lifetime = 1.15f;

        public void Build(float impactRadius)
        {
            radius = Mathf.Max(0.8f, impactRadius);
            Material fractureMaterial = SkyLeapVfxUtility.CreateFlatPigmentMaterial(Color.white);
            Material dustMaterial = SkyLeapVfxUtility.CreateFlatPigmentMaterial(Color.white);
            if (fractureMaterial != null) runtimeMaterials.Add(fractureMaterial);
            if (dustMaterial != null) runtimeMaterials.Add(dustMaterial);

            BuildGroundScar();
            BuildFractures(fractureMaterial);

            ParticleSystem rocks = GroundLineVfxUtility.CreateBurst(
                transform, "Impact_Heavy_Stone_Chunks", dustMaterial,
                new Color(0.31f, 0.17f, 0.075f, 0.92f), 31, 1.05f, 5.4f,
                0.10f, 0.34f, 0.34f, 0.92f, 1.42f, 29, 0.40f);
            ApplyEarthFade(rocks,
                new Color(0.42f, 0.24f, 0.095f),
                new Color(0.13f, 0.06f, 0.022f), 0.92f);

            ParticleSystem dust = GroundLineVfxUtility.CreateBurst(
                transform, "Impact_Ancient_Cave_Dust", dustMaterial,
                new Color(0.34f, 0.23f, 0.15f, 0.52f), 58, 0.34f, 2.9f,
                0.18f, 0.52f, 0.42f, 1.02f, 0.30f, 22, 0.92f);
            ApplyEarthFade(dust,
                new Color(0.48f, 0.35f, 0.24f),
                new Color(0.17f, 0.095f, 0.055f), 0.48f);
        }

        private void BuildGroundScar()
        {
            GameObject scarObject = new GameObject("Impact_Weathered_Ground_Scar");
            scarObject.transform.SetParent(transform, false);
            groundScar = scarObject.AddComponent<SpriteRenderer>();
            scarSprite = SkyLeapVfxUtility.CreateWeatheredGroundMark(out scarTexture);
            groundScar.sprite = scarSprite;
            groundScar.sortingOrder = 24;
            Vector2 spriteSize = scarSprite.bounds.size;
            groundScarScale = new Vector3(
                radius * 2.20f / Mathf.Max(0.01f, spriteSize.x),
                radius * 0.70f / Mathf.Max(0.01f, spriteSize.y),
                1f);
            scarObject.transform.localScale = groundScarScale * 0.72f;
            groundScar.color = new Color(0.78f, 0.61f, 0.44f, 0.82f);
        }

        private void BuildFractures(Material material)
        {
            const int count = 11;
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count + Mathf.Sin(i * 2.17f) * 0.16f;
                Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * 0.29f, 0f);
                Vector3 tangent = new Vector3(-direction.y, direction.x, 0f).normalized;
                float length = radius * Mathf.Lerp(0.64f, 1.20f, Mathf.Abs(Mathf.Sin(i * 1.73f)));
                LineRenderer crack = GroundLineVfxUtility.CreateLine(
                    transform, $"Impact_Dark_Fracture_{i:00}", material, 25, 0.065f);
                crack.numCapVertices = 1;
                crack.numCornerVertices = 1;
                crack.positionCount = 5;
                crack.SetPosition(0, direction * 0.14f);
                crack.SetPosition(1, direction * length * 0.26f + tangent * 0.08f);
                crack.SetPosition(2, direction * length * 0.52f - tangent * 0.12f);
                crack.SetPosition(3, direction * length * 0.78f + tangent * 0.07f);
                crack.SetPosition(4, direction * length);
                fractures.Add(crack);
            }
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / Lifetime);
            float fast = Mathf.Clamp01(elapsed / 0.18f);
            float stamp = 1f - Mathf.Pow(1f - fast, 3f);

            if (groundScar != null)
            {
                groundScar.transform.localScale = groundScarScale * Mathf.Lerp(0.72f, 1.04f, stamp);
                float scarFade = 1f - Mathf.Clamp01((elapsed - 0.48f) / 0.67f);
                groundScar.color = new Color(0.70f, 0.52f, 0.35f, scarFade * 0.78f);
            }

            float crackReveal = Mathf.Clamp01(elapsed / 0.10f);
            float crackFade = 1f - Mathf.Clamp01((elapsed - 0.48f) / 0.67f);
            for (int i = 0; i < fractures.Count; i++)
            {
                float reveal = Mathf.Clamp01(crackReveal * 1.6f - i * 0.035f);
                fractures[i].transform.localScale = new Vector3(reveal, reveal, 1f);
                Color color = new Color(0.15f, 0.06f, 0.02f, crackFade * 0.86f);
                fractures[i].startColor = fractures[i].endColor = color;
            }

            if (elapsed >= Lifetime) Destroy(gameObject);
        }

        private static void ApplyEarthFade(
            ParticleSystem particles,
            Color start,
            Color end,
            float peakAlpha)
        {
            if (particles == null) return;
            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(start, 0f),
                    new GradientColorKey(end, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(peakAlpha, 0.10f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = gradient;
        }

        private void OnDestroy()
        {
            GroundLineVfxUtility.DestroyMaterials(runtimeMaterials);
            if (scarSprite != null) Destroy(scarSprite);
            if (scarTexture != null) Destroy(scarTexture);
        }
    }

}
