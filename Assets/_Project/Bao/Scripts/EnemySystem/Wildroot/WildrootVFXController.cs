using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bao.Enemies
{
    [RequireComponent(typeof(WildrootEnemy))]
    public class WildrootVFXController : MonoBehaviour
    {
        [Header("Shader Setup")]
        [SerializeField] private Shader magicFloraShader;
        [SerializeField] private Color glowColor = new Color(1.0f, 0.9f, 0.6f, 1f);
        [SerializeField] private float transitionSpeed = 3.0f;

        [Header("Magical Particles")]
        [SerializeField] private Color pollenColor = new Color(1.0f, 0.9f, 0.5f, 0.8f);
        [SerializeField] private Color petalColor = new Color(1.0f, 0.95f, 0.8f, 0.9f);

        [Header("Thorn Whip Attack")]
        [SerializeField] private Color whipCoreColor = new Color(0.93f, 0.72f, 0.22f, 0.95f);
        [SerializeField] private Color whipEdgeColor = new Color(0.47f, 0.78f, 0.34f, 0.82f);
        [SerializeField, Min(0.01f)] private float bonePulseDelay = 0.014f;
        [SerializeField, Min(0.05f)] private float chargeDuration = 0.14f;
        [SerializeField, Min(0.05f)] private float thrustDuration = 0.24f;
        [SerializeField, Min(0.1f)] private float thornLanceLength = 3.7f;

        private const int WhipBoneCount = 8;

        private WildrootEnemy wildroot;
        private SpriteRenderer[] renderers;
        private Material[] floralMaterials;
        private float currentGlow;

        private ParticleSystem pollenParticles;
        private ParticleSystem petalParticles;
        private ParticleSystem chargeParticles;
        private ParticleSystem impactParticles;

        private Transform[] whipBones;
        private SpriteRenderer[] bonePulseRenderers;
        private SpriteRenderer tipFlashRenderer;
        private SpriteRenderer thornLanceRenderer;
        private SpriteRenderer thornEchoRenderer;
        private Material whipMaterial;
        private float chargeStartedAt = -1f;
        private bool thrustTriggered;
        private bool whipBonesReady;

        private static Texture2D cachedPollenTex;
        private static Texture2D cachedPetalTex;
        private static Texture2D cachedThornTex;
        private static Sprite cachedPollenSprite;
        private static Sprite cachedThornSprite;

        private void Awake()
        {
            wildroot = GetComponent<WildrootEnemy>();
            renderers = wildroot.GetVisualSpriteRenderers();

            if (magicFloraShader == null)
            {
                magicFloraShader = Shader.Find("Bao/PoisonMossShader");
            }

            SetupMaterials();
            GenerateTextures();
            SetupMagicalParticles();
            SetupWhipAttackVFX();
        }

        private void SetupMaterials()
        {
            if (magicFloraShader == null) return;

            floralMaterials = new Material[renderers.Length];
            for (int index = 0; index < renderers.Length; index++)
            {
                Material material = new Material(magicFloraShader);
                material.SetColor("_GlowColor", glowColor);
                material.SetFloat("_TriggerGlow", 0.0f);

                if (renderers[index].sharedMaterial != null && renderers[index].sharedMaterial.HasProperty("_MainTex"))
                {
                    material.SetTexture("_MainTex", renderers[index].sharedMaterial.GetTexture("_MainTex"));
                }

                renderers[index].material = material;
                floralMaterials[index] = material;
            }
        }

        private void SetupMagicalParticles()
        {
            pollenParticles = CreateParticleSystem(
                "PollenFireflies",
                cachedPollenTex,
                pollenColor,
                new Vector2(0.04f, 0.12f),
                new Vector2(0.05f, 0.2f),
                6f,
                true,
                true);

            petalParticles = CreateParticleSystem(
                "FloatingPetals",
                cachedPetalTex,
                petalColor,
                new Vector2(0.1f, 0.25f),
                new Vector2(0.1f, 0.3f),
                2f,
                true,
                true);

            ParticleSystem.MainModule petalMain = petalParticles.main;
            petalMain.gravityModifier = 0.02f;
            petalMain.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);

            ParticleSystem.RotationOverLifetimeModule petalRotation = petalParticles.rotationOverLifetime;
            petalRotation.enabled = true;
            petalRotation.z = new ParticleSystem.MinMaxCurve(-2f, 2f);

            pollenParticles.Play();
            petalParticles.Play();
        }

        private void SetupWhipAttackVFX()
        {
            whipBonesReady = TryCacheWhipBones();
            if (!whipBonesReady) return;

            whipMaterial = new Material(Shader.Find("Sprites/Default"));
            bonePulseRenderers = new SpriteRenderer[WhipBoneCount];

            for (int index = 0; index < WhipBoneCount; index++)
            {
                Transform pulseTransform = CreateWhipVisual("Thorn Energy Node", whipBones[index]);
                if (index < WhipBoneCount - 1)
                {
                    pulseTransform.localPosition = whipBones[index + 1].localPosition * 0.5f;
                }

                SpriteRenderer pulseRenderer = pulseTransform.gameObject.AddComponent<SpriteRenderer>();
                pulseRenderer.sprite = cachedPollenSprite;
                pulseRenderer.sharedMaterial = whipMaterial;
                pulseRenderer.sortingOrder = 30;
                pulseRenderer.color = Color.clear;
                pulseTransform.localScale = Vector3.zero;
                bonePulseRenderers[index] = pulseRenderer;
            }

            tipFlashRenderer = CreateWhipRenderer("Thorn Tip Flash", cachedPollenSprite, 34);
            thornLanceRenderer = CreateWhipRenderer("Thorn Lance", cachedThornSprite, 32);
            thornEchoRenderer = CreateWhipRenderer("Thorn Lance Echo", cachedThornSprite, 31);
        }

        private bool TryCacheWhipBones()
        {
            whipBones = new Transform[WhipBoneCount];
            Transform currentBone = FindDeepChild(transform, "Spike bone 1");
            if (currentBone == null)
            {
                return false;
            }

            whipBones[0] = currentBone;
            for (int index = 1; index < WhipBoneCount; index++)
            {
                currentBone = FindDirectChild(currentBone, $"Spike bone {index + 1}")
                    ?? FindDeepChild(currentBone, $"Spike bone {index + 1}");

                if (currentBone == null)
                {
                    return false;
                }

                whipBones[index] = currentBone;
            }

            return true;
        }

        private static Transform FindDirectChild(Transform parent, string childName)
        {
            for (int index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
                if (child.name == childName) return child;
            }

            return null;
        }

        private static Transform FindDeepChild(Transform parent, string childName)
        {
            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == childName) return child;
            }

            return null;
        }

        private Transform CreateWhipVisual(string visualName, Transform parent)
        {
            GameObject visualObject = new GameObject(visualName);
            visualObject.transform.SetParent(parent, false);
            return visualObject.transform;
        }

        private SpriteRenderer CreateWhipRenderer(string visualName, Sprite sprite, int sortingOrder)
        {
            Transform visualTransform = CreateWhipVisual(visualName, transform);
            SpriteRenderer renderer = visualTransform.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = whipMaterial;
            renderer.sortingOrder = sortingOrder;
            renderer.color = Color.clear;
            visualTransform.localScale = Vector3.zero;
            return renderer;
        }

        private void GenerateTextures()
        {
            if (cachedPollenTex == null)
            {
                cachedPollenTex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                cachedPollenTex.filterMode = FilterMode.Bilinear;
                cachedPollenTex.wrapMode = TextureWrapMode.Clamp;

                Vector2 center = new Vector2(15.5f, 15.5f);
                for (int y = 0; y < 32; y++)
                {
                    for (int x = 0; x < 32; x++)
                    {
                        float distance = Vector2.Distance(new Vector2(x, y), center);
                        float alpha = Mathf.Pow(Mathf.Clamp01(1.0f - distance / 15.5f), 2.3f);
                        cachedPollenTex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                }

                cachedPollenTex.Apply();
                cachedPollenSprite = Sprite.Create(cachedPollenTex, new Rect(0f, 0f, 32f, 32f), new Vector2(0.5f, 0.5f), 32f);
            }

            if (cachedPetalTex == null)
            {
                cachedPetalTex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                cachedPetalTex.filterMode = FilterMode.Bilinear;
                cachedPetalTex.wrapMode = TextureWrapMode.Clamp;

                for (int y = 0; y < 32; y++)
                {
                    for (int x = 0; x < 32; x++)
                    {
                        float horizontal = (x - 15.5f) / 10f;
                        float vertical = (y - 15.5f) / 14f;
                        float distance = horizontal * horizontal + vertical * vertical;
                        float alpha = Mathf.Pow(Mathf.Clamp01(1.0f - distance), 1.5f);
                        cachedPetalTex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                }

                cachedPetalTex.Apply();
            }

            if (cachedThornTex == null)
            {
                cachedThornTex = new Texture2D(128, 32, TextureFormat.RGBA32, false);
                cachedThornTex.filterMode = FilterMode.Bilinear;
                cachedThornTex.wrapMode = TextureWrapMode.Clamp;

                for (int y = 0; y < 32; y++)
                {
                    for (int x = 0; x < 128; x++)
                    {
                        float u = x / 127f;
                        float v = Mathf.Abs((y - 15.5f) / 15.5f);
                        float halfWidth = Mathf.Lerp(0.09f, 0.72f, Mathf.SmoothStep(0f, 0.22f, u))
                            * Mathf.Pow(1f - u, 0.52f);
                        float edge = 1f - Mathf.SmoothStep(halfWidth * 0.68f, halfWidth, v);
                        float core = 1f - Mathf.SmoothStep(0f, halfWidth * 0.72f, v);
                        float tipGlow = Mathf.SmoothStep(0.65f, 1f, u) * (1f - v);
                        float alpha = Mathf.Clamp01(edge * (0.75f + core * 0.25f) + tipGlow * 0.2f);
                        cachedThornTex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                }

                cachedThornTex.Apply();
                cachedThornSprite = Sprite.Create(cachedThornTex, new Rect(0f, 0f, 128f, 32f), new Vector2(0f, 0.5f), 32f);
            }
        }

        private ParticleSystem CreateParticleSystem(
            string systemName,
            Texture2D texture,
            Color color,
            Vector2 size,
            Vector2 speed,
            float rate,
            bool useNoise,
            bool loop)
        {
            GameObject particleObject = new GameObject(systemName);
            particleObject.transform.SetParent(transform, false);
            particleObject.transform.localPosition = new Vector3(0f, 0.5f, 0f);

            ParticleSystem particleSystem = particleObject.AddComponent<ParticleSystem>();
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystemRenderer particleRenderer = particleObject.GetComponent<ParticleSystemRenderer>();
            Material particleMaterial = new Material(Shader.Find("Sprites/Default"));
            particleMaterial.mainTexture = texture;
            particleRenderer.material = particleMaterial;
            particleRenderer.sortingOrder = 28;

            ParticleSystem.MainModule main = particleSystem.main;
            main.duration = 2f;
            main.loop = loop;
            main.startLifetime = new ParticleSystem.MinMaxCurve(loop ? 2f : 0.32f, loop ? 4f : 0.72f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = color;
            main.gravityModifier = loop ? -0.02f : 0.1f;
            main.simulationSpace = loop ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.World;
            main.maxParticles = loop ? 150 : 80;
            main.playOnAwake = false;

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.enabled = loop;
            emission.rateOverTime = rate;

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = loop ? ParticleSystemShapeType.Box : ParticleSystemShapeType.Cone;
            shape.scale = loop ? new Vector3(3.5f, 1f, 1f) : new Vector3(0.35f, 0.35f, 0f);
            shape.angle = loop ? 0f : 20f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(color, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(loop ? 1f : 0.9f, 0.16f),
                    new GradientAlphaKey(loop ? 1f : 0.45f, 0.62f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.25f),
                new Keyframe(0.22f, 1f),
                new Keyframe(1f, 0.12f)));

            if (useNoise)
            {
                ParticleSystem.NoiseModule noise = particleSystem.noise;
                noise.enabled = true;
                noise.strength = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
                noise.frequency = 0.5f;
                noise.scrollSpeed = 0.4f;
                noise.damping = true;
            }

            return particleSystem;
        }

        private void CreateWhipBurstParticles()
        {
            chargeParticles = CreateParticleSystem(
                "Thorn Whip Charge Motes",
                cachedPollenTex,
                whipEdgeColor,
                new Vector2(0.07f, 0.17f),
                new Vector2(1.4f, 2.7f),
                0f,
                true,
                false);

            impactParticles = CreateParticleSystem(
                "Thorn Whip Impact Spores",
                cachedPetalTex,
                whipCoreColor,
                new Vector2(0.12f, 0.32f),
                new Vector2(2.4f, 5.2f),
                0f,
                true,
                false);
        }

        public void BeginWhipCharge()
        {
            if (!whipBonesReady) return;

            chargeStartedAt = Time.time;
            thrustTriggered = false;
            SetWhipVisualsHidden();

            if (chargeParticles == null || impactParticles == null)
            {
                CreateWhipBurstParticles();
            }

            if (chargeParticles != null)
            {
                chargeParticles.transform.position = whipBones[0].position;
                chargeParticles.Emit(20);
            }
        }

        public void PlayWhipThrust()
        {
            if (!whipBonesReady || thrustTriggered) return;

            thrustTriggered = true;
            UpdateThornLance(0f);

            if (chargeParticles != null)
            {
                chargeParticles.transform.position = whipBones[WhipBoneCount - 1].position;
                chargeParticles.Emit(12);
            }
        }

        public void PlayWhipImpact()
        {
            if (!whipBonesReady) return;

            if (!thrustTriggered)
            {
                PlayWhipThrust();
            }

            Vector3 tipPosition = whipBones[WhipBoneCount - 1].position;
            Vector2 direction = GetWhipTipDirection();
            Vector3 impactPosition = tipPosition + (Vector3)direction * 0.7f;

            if (impactParticles != null)
            {
                impactParticles.transform.position = impactPosition;
                impactParticles.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                impactParticles.Emit(28);
            }

            StartCoroutine(PlayImpactFlash(impactPosition));
        }

        private IEnumerator PlayImpactFlash(Vector3 impactPosition)
        {
            GameObject impactFlash = new GameObject("Thorn Puncture Flash");
            impactFlash.transform.SetParent(transform, true);
            impactFlash.transform.position = impactPosition;

            SpriteRenderer renderer = impactFlash.AddComponent<SpriteRenderer>();
            renderer.sprite = cachedPollenSprite;
            renderer.sharedMaterial = whipMaterial;
            renderer.sortingOrder = 35;

            float elapsed = 0f;
            const float duration = 0.22f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;
                float scale = Mathf.Lerp(0.25f, 1.8f, Mathf.Sin(progress * Mathf.PI));
                impactFlash.transform.localScale = new Vector3(scale, scale, 1f);
                renderer.color = Color.Lerp(whipCoreColor, new Color(whipEdgeColor.r, whipEdgeColor.g, whipEdgeColor.b, 0f), progress);
                yield return null;
            }

            Destroy(impactFlash);
        }

        private void Update()
        {
            bool isTrapSnapping = wildroot.StateMachine?.CurrentState is WildrootCloseState;
            float whipGlow = IsWhipCharging() ? 1.5f : 0f;
            float targetGlow = isTrapSnapping ? 5f + whipGlow : 1f;
            currentGlow = Mathf.Lerp(currentGlow, targetGlow, Time.deltaTime * transitionSpeed);

            if (floralMaterials != null)
            {
                foreach (Material material in floralMaterials)
                {
                    if (material != null)
                    {
                        material.SetFloat("_TriggerGlow", currentGlow);
                    }
                }
            }

            if (isTrapSnapping && currentGlow > 4f && Random.Range(0, 20) == 0)
            {
                TriggerFloralBurst();
            }
        }

        private void LateUpdate()
        {
            UpdateWhipAttackVisuals();
        }

        private void UpdateWhipAttackVisuals()
        {
            if (!whipBonesReady || chargeStartedAt < 0f) return;

            float elapsed = Time.time - chargeStartedAt;
            if (!thrustTriggered && elapsed >= chargeDuration)
            {
                PlayWhipThrust();
            }

            UpdateBonePulses(elapsed);

            if (thrustTriggered)
            {
                float thrustProgress = Mathf.Clamp01((elapsed - chargeDuration) / thrustDuration);
                UpdateThornLance(thrustProgress);

                if (thrustProgress >= 1f)
                {
                    chargeStartedAt = -1f;
                    SetWhipVisualsHidden();
                }
            }
        }

        private void UpdateBonePulses(float elapsed)
        {
            for (int index = 0; index < bonePulseRenderers.Length; index++)
            {
                SpriteRenderer pulseRenderer = bonePulseRenderers[index];
                if (pulseRenderer == null) continue;

                float localTime = elapsed - index * bonePulseDelay;
                float intensity = Mathf.Clamp01(1f - Mathf.Abs(localTime - 0.035f) / 0.06f);
                if (thrustTriggered)
                {
                    intensity *= Mathf.Clamp01(1f - (elapsed - chargeDuration) / 0.12f);
                }

                float scale = Mathf.Lerp(0.14f, 0.76f, Mathf.Sin(intensity * Mathf.PI * 0.5f));
                pulseRenderer.transform.localScale = Vector3.one * scale;
                pulseRenderer.color = Color.Lerp(Color.clear, Color.Lerp(whipEdgeColor, whipCoreColor, intensity), intensity);
            }
        }

        private void UpdateThornLance(float thrustProgress)
        {
            Vector2 direction = GetWhipTipDirection();
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Vector3 tipPosition = whipBones[WhipBoneCount - 1].position;
            float intensity = 1f - Mathf.SmoothStep(0f, 1f, thrustProgress);
            float lanceLength = thornLanceLength * Mathf.Lerp(0.55f, 1f, intensity);

            thornLanceRenderer.transform.position = tipPosition + (Vector3)direction * 0.15f;
            thornLanceRenderer.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            thornLanceRenderer.transform.localScale = new Vector3(lanceLength, 1.15f * intensity, 1f);
            thornLanceRenderer.color = new Color(whipCoreColor.r, whipCoreColor.g, whipCoreColor.b, 0.95f * intensity);

            thornEchoRenderer.transform.position = tipPosition - (Vector3)direction * 0.12f + Vector3.up * 0.08f;
            thornEchoRenderer.transform.rotation = Quaternion.Euler(0f, 0f, angle + 4f);
            thornEchoRenderer.transform.localScale = new Vector3(lanceLength * 0.82f, 0.55f * intensity, 1f);
            thornEchoRenderer.color = new Color(whipEdgeColor.r, whipEdgeColor.g, whipEdgeColor.b, 0.6f * intensity);

            float tipScale = Mathf.Lerp(0.35f, 1.25f, intensity);
            tipFlashRenderer.transform.position = tipPosition;
            tipFlashRenderer.transform.localScale = Vector3.one * tipScale;
            tipFlashRenderer.color = new Color(whipCoreColor.r, whipCoreColor.g, whipCoreColor.b, intensity);
        }

        private Vector2 GetWhipTipDirection()
        {
            Vector2 direction = whipBones[WhipBoneCount - 1].position - whipBones[WhipBoneCount - 2].position;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = transform.right;
            }

            return direction.normalized;
        }

        private bool IsWhipCharging()
        {
            return chargeStartedAt >= 0f;
        }

        private void SetWhipVisualsHidden()
        {
            if (bonePulseRenderers != null)
            {
                foreach (SpriteRenderer pulseRenderer in bonePulseRenderers)
                {
                    if (pulseRenderer == null) continue;
                    pulseRenderer.color = Color.clear;
                    pulseRenderer.transform.localScale = Vector3.zero;
                }
            }

            HideWhipRenderer(tipFlashRenderer);
            HideWhipRenderer(thornLanceRenderer);
            HideWhipRenderer(thornEchoRenderer);
        }

        private static void HideWhipRenderer(SpriteRenderer renderer)
        {
            if (renderer == null) return;
            renderer.color = Color.clear;
            renderer.transform.localScale = Vector3.zero;
        }

        private void TriggerFloralBurst()
        {
            if (pollenParticles != null) pollenParticles.Emit(Random.Range(8, 15));
            if (petalParticles != null) petalParticles.Emit(Random.Range(3, 8));
        }

        private void OnDestroy()
        {
            if (whipMaterial != null)
            {
                Destroy(whipMaterial);
            }
        }
    }
}