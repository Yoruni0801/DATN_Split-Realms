using UnityEngine;
using UnityEngine.U2D.Animation;

namespace Bao.Enemies
{
    [RequireComponent(typeof(BonewheelEnemy))]
    public class BonewheelVFXController : MonoBehaviour
    {
        [Header("Shader Settings")]
        [SerializeField] private Shader stealthShader;
        [SerializeField] private Color stealthColor = new Color(0.7f, 0.6f, 0.4f, 1f);
        [SerializeField] private float transitionSpeed = 8f;

        [Header("Spike Sprite Attack")]
        [SerializeField] private Shader whipEnergyShader;
        [SerializeField] private Color whipCoreColor = new Color(0.82f, 0.62f, 0.24f, 1f);
        [SerializeField] private Color whipEdgeColor = new Color(0.48f, 0.43f, 0.27f, 1f);
        [SerializeField, Min(0.08f)] private float chargeDuration = 0.42f;
        [SerializeField, Min(0.05f)] private float thrustDuration = 0.2f;
        [SerializeField, Min(0.12f)] private float tipStingLength = 0.32f;
        [SerializeField, Min(0.04f)] private float tipStingWidth = 0.14f;
        [SerializeField, Min(0.12f)] private float impactMarkSize = 0.46f;

        private BonewheelEnemy bonewheel;
        private SpriteRenderer[] renderers;
        private Material[] stealthMaterials;
        private float targetDissolve;
        private float currentDissolve;
        private Vector3 lastPosition;

        private ParticleSystem teleportParticles;
        private bool wasInvisible;
        private static Texture2D cachedDustTex;

        private SpriteRenderer spikeRenderer;
        private SpriteSkin spikeSkin;
        private Material spikeEnergyMaterial;
        private Material attackShapeMaterial;
        private SpriteRenderer tipStingRenderer;
        private SpriteRenderer impactMarkRenderer;
        private Transform spikeBone7;
        private Transform spikeBone8;
        private float chargeStartTime = -1f;
        private float thrustStartTime = -1f;
        private float impactStartTime = -1f;
        private Vector3 impactPosition;
        private float impactAngle;
        private float pulseDirection = 1f;
        private bool whipReady;

        private static Texture2D cachedTipStingTex;
        private static Texture2D cachedImpactMarkTex;
        private static Sprite cachedTipStingSprite;
        private static Sprite cachedImpactMarkSprite;

        private void Awake()
        {
            bonewheel = GetComponent<BonewheelEnemy>();
            renderers = bonewheel.GetVisualSpriteRenderers();

            if (stealthShader == null)
            {
                stealthShader = Shader.Find("Bao/StealthHologramShader");
            }

            if (whipEnergyShader == null)
            {
                whipEnergyShader = Shader.Find("Bao/BonewheelWhipEnergy");
            }

            SetupStealthMaterials();
            lastPosition = transform.position;
            teleportParticles = CreateTeleportParticles();
            SetupSpikeAttackVFX();
        }

        private void SetupStealthMaterials()
        {
            if (stealthShader == null) return;

            stealthMaterials = new Material[renderers.Length];
            for (int index = 0; index < renderers.Length; index++)
            {
                Material material = new Material(stealthShader);
                material.SetColor("_EdgeColor", stealthColor);
                material.SetFloat("_Dissolve", 0f);

                if (renderers[index].sharedMaterial != null && renderers[index].sharedMaterial.HasProperty("_MainTex"))
                {
                    material.SetTexture("_MainTex", renderers[index].sharedMaterial.GetTexture("_MainTex"));
                }

                renderers[index].material = material;
                stealthMaterials[index] = material;
            }
        }

        private void SetupSpikeAttackVFX()
        {
            Transform spikeTransform = FindSpikeSpriteTransform();
            if (spikeTransform == null)
            {
                Debug.LogWarning("Bonewheel VFX could not find Spike Grandfather/Spike.", this);
                return;
            }

            spikeRenderer = spikeTransform.GetComponent<SpriteRenderer>();
            spikeSkin = spikeTransform.GetComponent<SpriteSkin>();
            spikeBone7 = FindDeepChild(transform, "Spike bone 7");
            spikeBone8 = FindDeepChild(transform, "Spike bone 8");

            if (spikeRenderer == null || spikeSkin == null || spikeBone7 == null || spikeBone8 == null || whipEnergyShader == null)
            {
                Debug.LogWarning("Bonewheel VFX could not bind the Spike SpriteSkin and tip bones.", this);
                return;
            }

            whipReady = true;
            spikeEnergyMaterial = new Material(whipEnergyShader);
            spikeEnergyMaterial.SetTexture("_MainTex", spikeRenderer.sprite.texture);
            spikeEnergyMaterial.SetColor("_CoreColor", whipCoreColor);
            spikeEnergyMaterial.SetColor("_EdgeColor", whipEdgeColor);
            spikeEnergyMaterial.SetFloat("_PulseWidth", 0.075f);

            GameObject overlayObject = new GameObject("Spike Energy Overlay");
            overlayObject.transform.SetParent(spikeTransform.parent, false);
            overlayObject.transform.localPosition = spikeTransform.localPosition;
            overlayObject.transform.localRotation = spikeTransform.localRotation;
            overlayObject.transform.localScale = spikeTransform.localScale;

            SpriteRenderer overlayRenderer = overlayObject.AddComponent<SpriteRenderer>();
            overlayRenderer.sprite = spikeRenderer.sprite;
            overlayRenderer.sharedMaterial = spikeEnergyMaterial;
            overlayRenderer.sortingLayerID = spikeRenderer.sortingLayerID;
            overlayRenderer.sortingOrder = spikeRenderer.sortingOrder + 1;
            overlayRenderer.color = Color.white;

            SpriteSkin overlaySkin = overlayObject.AddComponent<SpriteSkin>();
            overlaySkin.SetRootBone(spikeSkin.rootBone);
            overlaySkin.SetBoneTransforms(spikeSkin.boneTransforms);
            overlaySkin.alwaysUpdate = true;

            attackShapeMaterial = new Material(Shader.Find("Sprites/Default"));
            tipStingRenderer = CreateAttackShapeRenderer("Spike Tip Sting", GetTipStingSprite(), spikeRenderer.sortingOrder + 3);
            impactMarkRenderer = CreateAttackShapeRenderer("Spike Puncture Mark", GetImpactMarkSprite(), spikeRenderer.sortingOrder + 6);
            SetAttackVisualsHidden();
        }

        private Transform FindSpikeSpriteTransform()
        {
            Transform grandfather = FindDirectChild(transform, "Spike Grandfather");
            return grandfather != null ? FindDirectChild(grandfather, "Spike") : null;
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

        private SpriteRenderer CreateAttackShapeRenderer(string objectName, Sprite sprite, int sortingOrder)
        {
            GameObject visualObject = new GameObject(objectName);
            visualObject.transform.SetParent(VFXContainerUtility.GetContainer(), false);
            visualObject.layer = gameObject.layer;
            SpriteRenderer renderer = visualObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = attackShapeMaterial;
            renderer.sortingLayerID = spikeRenderer.sortingLayerID;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        public void BeginWhipCharge(float timeUntilHit)
        {
            if (!whipReady) return;

            chargeStartTime = Time.time + Mathf.Max(0f, timeUntilHit - chargeDuration);
            thrustStartTime = -1f;
            impactStartTime = -1f;
            pulseDirection = GetPulseDirection();
            SetAttackVisualsHidden();
        }

        public void PlayWhipThrust()
        {
            if (!whipReady) return;

            chargeStartTime = -1f;
            thrustStartTime = Time.time;
            spikeEnergyMaterial.SetFloat("_PulseCenter", pulseDirection > 0f ? 1f : 0f);
            spikeEnergyMaterial.SetFloat("_PulseDirection", pulseDirection);
            spikeEnergyMaterial.SetFloat("_Intensity", 1.18f);
            UpdateTipSting(0f);
        }

        public void PlayWhipImpact(Vector3 hitPosition)
        {
            if (!whipReady) return;

            impactStartTime = Time.time;
            impactPosition = hitPosition;
            Vector2 direction = GetWhipTipDirection();
            impactAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            spikeEnergyMaterial.SetFloat("_PulseCenter", pulseDirection > 0f ? 1f : 0f);
            spikeEnergyMaterial.SetFloat("_PulseDirection", pulseDirection);
            spikeEnergyMaterial.SetFloat("_Intensity", 1.38f);
            UpdateImpactMark(0f);
        }

        public void CancelWhipAttack()
        {
            chargeStartTime = -1f;
            thrustStartTime = -1f;
            impactStartTime = -1f;
            SetAttackVisualsHidden();
        }

        private void Update()
        {
            float baseAlpha = renderers.Length > 0 && renderers[0] != null ? renderers[0].color.a : 1f;
            targetDissolve = 1f - baseAlpha;
            currentDissolve = Mathf.MoveTowards(currentDissolve, targetDissolve, Time.deltaTime * transitionSpeed);

            if (stealthMaterials != null)
            {
                foreach (Material material in stealthMaterials)
                {
                    if (material != null) material.SetFloat("_Dissolve", currentDissolve);
                }
            }

            bool isInvisible = targetDissolve > 0.5f;
            if (isInvisible && !wasInvisible) PlayTeleportVFX();
            if (Vector3.Distance(transform.position, lastPosition) > 1f) PlayTeleportVFX();
            wasInvisible = isInvisible;
            lastPosition = transform.position;
            UpdateAttackVisuals();
        }

        private void UpdateAttackVisuals()
        {
            bool hasTransientVisual = false;

            if (thrustStartTime >= 0f)
            {
                UpdateThrustVisual();
                hasTransientVisual = true;
            }

            if (impactStartTime >= 0f)
            {
                UpdateImpactFlash();
                hasTransientVisual = true;
            }

            if (hasTransientVisual) return;
            if (chargeStartTime < 0f || Time.time < chargeStartTime) return;

            float chargeProgress = Mathf.Clamp01((Time.time - chargeStartTime) / chargeDuration);
            spikeEnergyMaterial.SetFloat("_PulseCenter", pulseDirection > 0f ? chargeProgress : 1f - chargeProgress);
            spikeEnergyMaterial.SetFloat("_PulseDirection", pulseDirection);
            spikeEnergyMaterial.SetFloat("_Intensity", 0.78f + chargeProgress * 0.34f);

            if (chargeProgress >= 1f)
            {
                chargeStartTime = -1f;
                SetAttackVisualsHidden();
            }
        }

        private void UpdateThrustVisual()
        {
            float progress = Mathf.Clamp01((Time.time - thrustStartTime) / thrustDuration);
            spikeEnergyMaterial.SetFloat("_Intensity", Mathf.Lerp(1.18f, 0f, Mathf.SmoothStep(0f, 1f, progress)));
            UpdateTipSting(progress);

            if (progress >= 1f)
            {
                thrustStartTime = -1f;
                HideRenderer(tipStingRenderer);
                if (impactStartTime < 0f) SetAttackVisualsHidden();
            }
        }

        private void UpdateTipSting(float progress)
        {
            if (tipStingRenderer == null) return;

            Vector2 direction = GetWhipTipDirection();
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float grow = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.18f, progress));
            float fade = 1f - Mathf.SmoothStep(0.38f, 1f, progress);
            float alpha = Mathf.Lerp(0.92f, 0.48f, progress) * fade;
            float length = Mathf.Lerp(tipStingLength * 0.34f, tipStingLength, grow);
            float width = Mathf.Lerp(tipStingWidth * 0.58f, tipStingWidth, grow);

            tipStingRenderer.transform.position = GetWhipTipPosition(direction);
            tipStingRenderer.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            tipStingRenderer.transform.localScale = new Vector3(length, width, 1f);
            tipStingRenderer.color = new Color(whipCoreColor.r, whipCoreColor.g, whipCoreColor.b, alpha);
        }

        private Vector2 GetWhipTipDirection()
        {
            Vector2 direction = spikeBone8.position - spikeBone7.position;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right * bonewheel.FacingDirection;
        }

        private Vector3 GetWhipTipPosition(Vector2 direction)
        {
            return spikeBone8.position + (Vector3)direction * 0.025f;
        }

        private float GetPulseDirection()
        {
            Vector2 direction = GetWhipTipDirection();
            float dot = Vector2.Dot(spikeRenderer.transform.right, direction);
            return dot >= 0f ? 1f : -1f;
        }

        private void UpdateImpactFlash()
        {
            float progress = Mathf.Clamp01((Time.time - impactStartTime) / 0.18f);
            spikeEnergyMaterial.SetFloat("_Intensity", Mathf.Lerp(1.38f, 0f, progress));
            UpdateImpactMark(progress);

            if (progress >= 1f)
            {
                impactStartTime = -1f;
                HideRenderer(impactMarkRenderer);
                if (thrustStartTime < 0f) SetAttackVisualsHidden();
            }
        }

        private void UpdateImpactMark(float progress)
        {
            if (impactMarkRenderer == null) return;

            float expansion = Mathf.SmoothStep(0f, 1f, progress);
            float alpha = 1f - Mathf.SmoothStep(0.18f, 1f, progress);
            impactMarkRenderer.transform.position = impactPosition;
            impactMarkRenderer.transform.rotation = Quaternion.Euler(0f, 0f, impactAngle);
            impactMarkRenderer.transform.localScale = Vector3.one * Mathf.Lerp(impactMarkSize * 0.42f, impactMarkSize, expansion);
            impactMarkRenderer.color = new Color(0.96f, 0.78f, 0.38f, alpha);
        }

        private void SetAttackVisualsHidden()
        {
            if (spikeEnergyMaterial != null) spikeEnergyMaterial.SetFloat("_Intensity", 0f);
            HideRenderer(tipStingRenderer);
            HideRenderer(impactMarkRenderer);
        }

        private static void HideRenderer(SpriteRenderer renderer)
        {
            if (renderer == null) return;
            renderer.color = Color.clear;
            renderer.transform.localScale = Vector3.zero;
        }

        private static Sprite GetTipStingSprite()
        {
            if (cachedTipStingSprite != null) return cachedTipStingSprite;

            cachedTipStingTex = new Texture2D(96, 48, TextureFormat.ARGB32, false);
            for (int y = 0; y < cachedTipStingTex.height; y++)
            {
                for (int x = 0; x < cachedTipStingTex.width; x++)
                {
                    float u = x / (float)(cachedTipStingTex.width - 1);
                    float normalizedY = Mathf.Abs((y - (cachedTipStingTex.height - 1) * 0.5f) / ((cachedTipStingTex.height - 1) * 0.5f));
                    float halfWidth = Mathf.Lerp(0.34f, 0.018f, u);
                    float crossSection = 1f - Mathf.Clamp01(normalizedY / halfWidth);
                    float startFade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.01f, 0.12f, u));
                    float tipFade = 1f - Mathf.SmoothStep(0.86f, 1f, u);
                    float alpha = crossSection * crossSection * startFade * tipFade;
                    cachedTipStingTex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            cachedTipStingTex.filterMode = FilterMode.Bilinear;
            cachedTipStingTex.Apply();
            cachedTipStingSprite = Sprite.Create(cachedTipStingTex, new Rect(0f, 0f, 96f, 48f), new Vector2(0f, 0.5f), 96f);
            return cachedTipStingSprite;
        }

        private static Sprite GetImpactMarkSprite()
        {
            if (cachedImpactMarkSprite != null) return cachedImpactMarkSprite;

            cachedImpactMarkTex = new Texture2D(128, 128, TextureFormat.ARGB32, false);
            for (int y = 0; y < cachedImpactMarkTex.height; y++)
            {
                for (int x = 0; x < cachedImpactMarkTex.width; x++)
                {
                    Vector2 point = new Vector2(
                        x / (float)(cachedImpactMarkTex.width - 1) * 2f - 1f,
                        y / (float)(cachedImpactMarkTex.height - 1) * 2f - 1f);
                    float alpha = Mathf.Max(
                        GetSlitAlpha(point, new Vector2(0.08f, 0f), 0f, 0.98f, 0.085f),
                        GetSlitAlpha(point, new Vector2(-0.08f, 0.13f), 29f, 0.72f, 0.064f),
                        GetSlitAlpha(point, new Vector2(-0.08f, -0.13f), -29f, 0.72f, 0.064f));
                    float core = 1f - Mathf.Clamp01(point.magnitude / 0.12f);
                    alpha = Mathf.Max(alpha, core * 0.72f);
                    cachedImpactMarkTex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha));
                }
            }
            cachedImpactMarkTex.filterMode = FilterMode.Bilinear;
            cachedImpactMarkTex.Apply();
            cachedImpactMarkSprite = Sprite.Create(cachedImpactMarkTex, new Rect(0f, 0f, 128f, 128f), new Vector2(0.5f, 0.5f), 128f);
            return cachedImpactMarkSprite;
        }

        private static float GetSlitAlpha(Vector2 point, Vector2 center, float angleDegrees, float length, float width)
        {
            float radians = -angleDegrees * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians);
            float sine = Mathf.Sin(radians);
            Vector2 offset = point - center;
            Vector2 local = new Vector2(offset.x * cosine - offset.y * sine, offset.x * sine + offset.y * cosine);
            float normalizedLength = Mathf.Abs(local.x) / (length * 0.5f);
            float taper = width * Mathf.Clamp01(1f - normalizedLength * 0.72f);
            float crossSection = 1f - Mathf.Clamp01(Mathf.Abs(local.y) / Mathf.Max(0.001f, taper));
            float endFade = 1f - Mathf.SmoothStep(0.78f, 1f, normalizedLength);
            return crossSection * crossSection * endFade;
        }

        private static Texture2D CreateRadialTexture(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), Vector2.one * (size - 1) * 0.5f) / (size * 0.5f);
                    float alpha = Mathf.Clamp01(1f - distance);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha));
                }
            }
            texture.filterMode = FilterMode.Bilinear;
            texture.Apply();
            return texture;
        }

        private void PlayTeleportVFX()
        {
            if (teleportParticles == null) return;
            Vector3 position = renderers.Length > 0 && renderers[0] != null ? renderers[0].bounds.center : transform.position;
            teleportParticles.transform.position = position;
            teleportParticles.Play();
        }

        private ParticleSystem CreateTeleportParticles()
        {
            GameObject particleObject = new GameObject("TeleportDustParticles");
            particleObject.transform.SetParent(VFXContainerUtility.GetContainer(), false);
            particleObject.transform.position = transform.position;

            ParticleSystem particleSystem = particleObject.AddComponent<ParticleSystem>();
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particleSystem.main;
            main.duration = 1f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
            main.startColor = new Color(0.7f, 0.6f, 0.4f, 0.7f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 100;
            main.playOnAwake = false;
            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 25) });
            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.6f;
            ParticleSystem.ColorOverLifetimeModule colors = particleSystem.colorOverLifetime;
            colors.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(new Color(0.8f, 0.7f, 0.5f), 0f), new GradientColorKey(new Color(0.4f, 0.3f, 0.2f), 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            colors.color = gradient;
            ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Sprites/Default"));
            renderer.material.mainTexture = GetDustTexture();
            renderer.sortingOrder = 15;
            return particleSystem;
        }

        private Texture2D GetDustTexture()
        {
            if (cachedDustTex != null) return cachedDustTex;
            cachedDustTex = CreateRadialTexture(32);
            return cachedDustTex;
        }

        private void OnDestroy()
        {
            if (teleportParticles != null) Destroy(teleportParticles.gameObject);
            if (tipStingRenderer != null) Destroy(tipStingRenderer.gameObject);
            if (impactMarkRenderer != null) Destroy(impactMarkRenderer.gameObject);
            if (spikeEnergyMaterial != null) Destroy(spikeEnergyMaterial);
            if (attackShapeMaterial != null) Destroy(attackShapeMaterial);
        }
    }
}
