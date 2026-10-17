using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bao.Enemies
{
    [RequireComponent(typeof(RiftcoreEnemy))]
    public class RiftcoreVFXController : MonoBehaviour
    {
        [Header("Riftcore Palette")]
        [SerializeField] private Shader voidShader;
        [SerializeField] private Color voidPortalColor = new Color(0.73f, 0.75f, 0.29f, 1f);
        [SerializeField] private Color coreWhiteColor = new Color(1f, 0.98f, 0.82f, 1f);
        [SerializeField] private Color voidShadowColor = new Color(0.12f, 0.025f, 0.18f, 1f);
        [SerializeField] private Color shellDustColor = new Color(0.92f, 0.82f, 0.57f, 1f);
        [SerializeField] private float transitionSpeed = 7f;

        [Header("Charge")]
        [SerializeField] private Vector3 coreLocalOffset = new Vector3(0f, 1.42f, 0f);
        [SerializeField] private float emitInterval = 0.035f;
        [SerializeField] private float inwardRadius = 1.25f;
        [SerializeField] private float criticalStart = 0.78f;
        [SerializeField] private float shellSqueezeDistance = 0.24f;
        [SerializeField] private float shellRecoilDistance = 0.58f;
        [SerializeField] private float shellCriticalJitter = 0.025f;

        [Header("Ground Rift")]
        [SerializeField] private float groundYOffset = 0.08f;
        [SerializeField] private float crackAmplitude = 0.13f;
        [SerializeField] private float crackBaseWidth = 0.055f;

        private RiftcoreEnemy riftcore;
        private Material[] voidMaterials;
        private Material spriteMaterial;
        private Material particleMaterial;
        private Sprite sparkleSprite;
        private Sprite ringSprite;
        private Sprite beamSprite;
        private Sprite fragmentSprite;
        private Texture2D sparkleTexture;
        private Texture2D ringTexture;
        private Texture2D beamTexture;
        private Texture2D fragmentTexture;
        private ParticleSystem chargeParticles;
        private ParticleSystem impactParticles;
        private ParticleSystem fragmentParticles;
        private SpriteRenderer coreHalo;
        private Transform leftShell;
        private Transform rightShell;
        private Vector3 leftShellBasePosition;
        private Vector3 rightShellBasePosition;
        private Vector3 leftShellBaseScale;
        private Vector3 rightShellBaseScale;
        private RiftLine[] leftRifts;
        private RiftLine[] rightRifts;
        private bool chargeActive;
        private bool shellImpactActive;
        private float chargeProgress;
        private float chargeParticleTimer;
        private float currentCollapse = 1f;
        private float explosionFlash;
        private float impactTimer;

        private sealed class RiftLine
        {
            public LineRenderer Renderer;
            public Vector3[] Points;
            public float Phase;
            public float BranchBias;
        }

        private void Awake()
        {
            riftcore = GetComponent<RiftcoreEnemy>();
            if (voidShader == null)
                voidShader = Shader.Find("Bao/RiftcoreVoidShader");

            SetupMaterials();
            CreateProceduralAssets();
            CreateParticleSystems();
            CreateCoreHalo();
            CreateGroundRifts();
            FindActiveShells();
        }

        public void BeginCharge(float duration)
        {
            chargeProgress = 0f;
            chargeParticleTimer = 0f;
            chargeActive = true;
            impactTimer = 0f;
            shellImpactActive = false;
            ResetShellsImmediate();

            if (coreHalo != null)
            {
                coreHalo.enabled = true;
                coreHalo.color = new Color(voidPortalColor.r, voidPortalColor.g, voidPortalColor.b, 0f);
            }
        }

        public void SetChargeProgress(float normalizedProgress)
        {
            chargeProgress = Mathf.Clamp01(normalizedProgress);
        }

        public void EndCharge()
        {
            chargeActive = false;
            chargeProgress = 0f;
            if (!shellImpactActive)
                ResetShellsImmediate();
        }

        public void TriggerExplosionFlash()
        {
            explosionFlash = 1f;
        }

        public void TriggerTwinRiftImpact()
        {
            chargeActive = false;
            chargeProgress = 1f;
            explosionFlash = 1f;
            impactTimer = 0.28f;

            if (coreHalo != null)
                coreHalo.enabled = false;

            StartCoroutine(AnimateShellRecoil());
            StartCoroutine(AnimateDirectionalBeam(Vector2.left));
            StartCoroutine(AnimateDirectionalBeam(Vector2.right));
            // Keep the hit readable: one bright core ring and two restrained accents,
            // instead of several large, soft rings blending into a hazy disc.
            StartCoroutine(AnimateImpactRing(0f, 0.58f, 0.16f, coreWhiteColor));
            StartCoroutine(AnimateImpactRing(0.028f, 0.92f, 0.22f, voidPortalColor));
            StartCoroutine(AnimateImpactRing(0.06f, 1.16f, 0.26f, voidShadowColor));
            EmitImpactParticles(Vector2.left);
            EmitImpactParticles(Vector2.right);
            EmitCoreBurst();
        }

        public void StyleLegacySideEffect(GameObject instance)
        {
            if (instance == null)
                return;

            foreach (ParticleSystem system in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                string lower = system.name.ToLowerInvariant();
                ParticleSystem.MainModule main = system.main;

                if (lower.Contains("smoke"))
                {
                    Color shadow = voidShadowColor;
                    shadow.a = 0.42f;
                    Color dust = shellDustColor;
                    dust.a = 0.28f;
                    main.startColor = new ParticleSystem.MinMaxGradient(shadow, dust);
                }
                else if (lower.Contains("ground"))
                {
                    Color dust = shellDustColor;
                    dust.a = 0.72f;
                    Color portal = voidPortalColor;
                    portal.a = 0.82f;
                    main.startColor = new ParticleSystem.MinMaxGradient(dust, portal);
                }
                else
                {
                    Color portal = voidPortalColor;
                    portal.a = 0.78f;
                    Color white = coreWhiteColor;
                    white.a = 0.9f;
                    main.startColor = new ParticleSystem.MinMaxGradient(portal, white);
                }
            }
        }

        private void Update()
        {
            float targetCollapse = chargeActive ? Mathf.Lerp(1f, 8f, chargeProgress) : 1f;
            currentCollapse = Mathf.Lerp(currentCollapse, targetCollapse, Time.deltaTime * transitionSpeed);
            explosionFlash = Mathf.MoveTowards(explosionFlash, 0f, Time.deltaTime * 4.5f);
            impactTimer = Mathf.Max(0f, impactTimer - Time.deltaTime);

            UpdateShader();
            UpdateCoreHalo();
            UpdateGroundRifts();
            UpdateChargeParticles();
            UpdateShellCompression();
        }

        private void SetupMaterials()
        {
            if (voidShader == null)
                return;

            List<Material> materials = new List<Material>();
            foreach (SpriteRenderer renderer in riftcore.GetVisualSpriteRenderers())
            {
                Material material = new Material(voidShader);
                material.SetColor("_VoidColor", voidPortalColor);
                material.SetFloat("_CollapseLevel", 1f);
                material.SetFloat("_ChargeProgress", 0f);
                material.SetFloat("_ExplosionFlash", 0f);

                if (renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_MainTex"))
                    material.SetTexture("_MainTex", renderer.sharedMaterial.GetTexture("_MainTex"));

                renderer.material = material;
                materials.Add(material);
            }

            voidMaterials = materials.ToArray();
        }

        private void CreateProceduralAssets()
        {
            Shader spriteShader = Shader.Find("Sprites/Default");
            if (spriteShader == null)
                return;

            spriteMaterial = new Material(spriteShader);
            particleMaterial = new Material(spriteShader);
            sparkleTexture = CreateSoftDotTexture(16);
            ringTexture = CreateRingTexture(128);
            beamTexture = CreateBeamTexture(128, 48);
            fragmentTexture = CreateTriangleTexture(24);
            sparkleSprite = Sprite.Create(sparkleTexture, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 32f);
            ringSprite = Sprite.Create(ringTexture, new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f), 64f);
            beamSprite = Sprite.Create(beamTexture, new Rect(0, 0, 128, 48), new Vector2(0f, 0.5f), 48f);
            fragmentSprite = Sprite.Create(fragmentTexture, new Rect(0, 0, 24, 24), new Vector2(0.5f, 0.5f), 32f);
        }

        private void CreateParticleSystems()
        {
            chargeParticles = CreateManualParticleSystem("Riftcore_InwardDust", sparkleSprite, 160, 40);
            impactParticles = CreateManualParticleSystem("Riftcore_ImpactDust", sparkleSprite, 180, 43);
            fragmentParticles = CreateManualParticleSystem("Riftcore_VoidFragments", fragmentSprite, 96, 44);
        }

        private ParticleSystem CreateManualParticleSystem(string name, Sprite sprite, int maxParticles, int sortingOrder)
        {
            GameObject go = new GameObject(name);
            Transform container = VFXContainerUtility.GetContainer();
            if (container != null)
                go.transform.SetParent(container);

            ParticleSystem system = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;
            main.startLifetime = 0.7f;
            main.startSpeed = 0f;
            main.startSize = 0.1f;
            main.gravityModifier = 0f;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;

            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = sortingOrder;
            Material material = new Material(particleMaterial);
            material.mainTexture = sprite.texture;
            renderer.sharedMaterial = material;
            system.Play();
            return system;
        }

        private void CreateCoreHalo()
        {
            GameObject halo = new GameObject("Riftcore_CoreHalo");
            Transform container = VFXContainerUtility.GetContainer();
            if (container != null)
                halo.transform.SetParent(container);

            coreHalo = halo.AddComponent<SpriteRenderer>();
            coreHalo.sprite = ringSprite;
            coreHalo.sharedMaterial = spriteMaterial;
            coreHalo.sortingOrder = 42;
            coreHalo.enabled = false;
        }

        private void CreateGroundRifts()
        {
            leftRifts = CreateRiftSide("Left", -1f);
            rightRifts = CreateRiftSide("Right", 1f);
        }

        private RiftLine[] CreateRiftSide(string sideName, float side)
        {
            RiftLine[] lines = new RiftLine[3];
            for (int i = 0; i < lines.Length; i++)
            {
                GameObject go = new GameObject($"Riftcore_{sideName}GroundRift_{i + 1}");
                Transform container = VFXContainerUtility.GetContainer();
                if (container != null)
                    go.transform.SetParent(container);

                LineRenderer renderer = go.AddComponent<LineRenderer>();
                renderer.useWorldSpace = true;
                renderer.positionCount = 7;
                renderer.numCapVertices = 2;
                renderer.numCornerVertices = 2;
                renderer.sharedMaterial = spriteMaterial;
                renderer.sortingOrder = 35 + i;
                renderer.enabled = false;
                lines[i] = new RiftLine
                {
                    Renderer = renderer,
                    Points = new Vector3[7],
                    Phase = Random.Range(0f, 10f),
                    BranchBias = side * i * 0.055f
                };
            }
            return lines;
        }

        private void FindActiveShells()
        {
            List<Transform> shells = new List<Transform>();
            foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(false))
            {
                if (renderer.name.ToLowerInvariant().Contains("shell"))
                    shells.Add(renderer.transform);
            }

            if (shells.Count < 2)
                return;

            shells.Sort((a, b) => a.position.x.CompareTo(b.position.x));
            leftShell = shells[0];
            rightShell = shells[shells.Count - 1];
            leftShellBasePosition = leftShell.localPosition;
            rightShellBasePosition = rightShell.localPosition;
            leftShellBaseScale = leftShell.localScale;
            rightShellBaseScale = rightShell.localScale;
        }

        private void UpdateShader()
        {
            if (voidMaterials == null)
                return;

            foreach (Material material in voidMaterials)
            {
                if (material == null)
                    continue;

                material.SetFloat("_CollapseLevel", currentCollapse);
                material.SetFloat("_ChargeProgress", chargeActive ? chargeProgress : 0f);
                material.SetFloat("_ExplosionFlash", explosionFlash);
            }
        }

        private void UpdateCoreHalo()
        {
            if (coreHalo == null)
                return;

            if (!chargeActive)
            {
                coreHalo.enabled = false;
                return;
            }

            coreHalo.enabled = true;
            coreHalo.transform.position = CoreWorldPosition;
            float critical = Mathf.InverseLerp(criticalStart, 1f, chargeProgress);
            float pulse = 0.84f + Mathf.Sin(Time.time * Mathf.Lerp(4f, 13f, chargeProgress)) * Mathf.Lerp(0.06f, 0.16f, chargeProgress);
            float size = Mathf.Lerp(0.28f, 0.62f, chargeProgress) * pulse;
            coreHalo.transform.localScale = new Vector3(size, size, 1f);
            Color color = Color.Lerp(voidPortalColor, coreWhiteColor, critical);
            color.a = Mathf.Lerp(0.2f, 0.88f, chargeProgress);
            coreHalo.color = color;
        }

        private void UpdateChargeParticles()
        {
            if (!chargeActive || chargeParticles == null || chargeProgress >= 0.955f)
                return;

            float interval = Mathf.Lerp(emitInterval * 1.5f, emitInterval * 0.42f, chargeProgress);
            chargeParticleTimer += Time.deltaTime;
            while (chargeParticleTimer >= interval)
            {
                chargeParticleTimer -= interval;
                EmitInwardParticle();
                if (chargeProgress > 0.7f && Random.value > 0.45f)
                    EmitInwardParticle();
            }
        }

        private void EmitInwardParticle()
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = inwardRadius * Random.Range(0.75f, 1.35f);
            Vector2 radial = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 tangent = new Vector2(-radial.y, radial.x);
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
            {
                position = CoreWorldPosition + (Vector3)(radial * radius),
                velocity = -radial * Mathf.Lerp(1.15f, 4.4f, chargeProgress) + tangent * Mathf.Lerp(0.25f, 1.05f, chargeProgress),
                startLifetime = Mathf.Lerp(0.7f, 0.34f, chargeProgress),
                startSize = Random.Range(0.045f, 0.11f) * Mathf.Lerp(0.85f, 1.35f, chargeProgress),
                startColor = Color.Lerp(voidPortalColor, coreWhiteColor, chargeProgress * 0.9f)
            };
            chargeParticles.Emit(emit, 1);
        }

        private void UpdateShellCompression()
        {
            if (shellImpactActive || leftShell == null || rightShell == null)
                return;

            if (!chargeActive)
            {
                ResetShellsImmediate();
                return;
            }

            float squeeze = EaseInOutCubic(chargeProgress);
            float critical = Mathf.InverseLerp(criticalStart, 1f, chargeProgress);
            Vector3 jitter = chargeProgress < 0.955f
                ? new Vector3(Random.Range(-shellCriticalJitter, shellCriticalJitter), Random.Range(-shellCriticalJitter * 0.55f, shellCriticalJitter * 0.55f), 0f) * critical
                : Vector3.zero;
            leftShell.localPosition = leftShellBasePosition + Vector3.right * (shellSqueezeDistance * squeeze) + jitter;
            rightShell.localPosition = rightShellBasePosition + Vector3.left * (shellSqueezeDistance * squeeze) - jitter;
            Vector3 scale = new Vector3(1f + 0.035f * squeeze, 1f - 0.025f * squeeze, 1f);
            leftShell.localScale = Vector3.Scale(leftShellBaseScale, scale);
            rightShell.localScale = Vector3.Scale(rightShellBaseScale, scale);
        }

        private void UpdateGroundRifts()
        {
            if (!chargeActive && impactTimer <= 0f)
            {
                SetRiftEnabled(leftRifts, false);
                SetRiftEnabled(rightRifts, false);
                return;
            }

            float reveal = chargeActive ? Mathf.SmoothStep(0f, 1f, chargeProgress) : 1f;
            float alpha = chargeActive ? Mathf.Lerp(0.18f, 0.9f, chargeProgress) : impactTimer / 0.28f;
            float width = chargeActive ? Mathf.Lerp(0.65f, 1.15f, chargeProgress) : Mathf.Lerp(0.25f, 1.65f, impactTimer / 0.28f);
            UpdateRiftSide(leftRifts, -1f, reveal, alpha, width);
            UpdateRiftSide(rightRifts, 1f, reveal, alpha, width);
        }

        private void UpdateRiftSide(RiftLine[] lines, float side, float reveal, float alpha, float width)
        {
            if (lines == null)
                return;

            Vector3 ground = transform.position + Vector3.up * groundYOffset;
            float length = Mathf.Max(0.5f, riftcore.AoeRange);
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                RiftLine line = lines[lineIndex];
                line.Renderer.enabled = true;
                line.Renderer.startWidth = crackBaseWidth * width * (lineIndex == 0 ? 1.35f : 0.7f);
                line.Renderer.endWidth = crackBaseWidth * width * 0.22f;
                Color start = Color.Lerp(voidPortalColor, coreWhiteColor, 0.38f);
                start.a = alpha;
                Color end = Color.Lerp(voidPortalColor, voidShadowColor, 0.32f);
                end.a = alpha * 0.28f;
                line.Renderer.startColor = start;
                line.Renderer.endColor = end;

                float lineLength = length * Mathf.Lerp(1f, 0.72f, lineIndex * 0.35f);
                for (int i = 0; i < line.Points.Length; i++)
                {
                    float t = i / (float)(line.Points.Length - 1);
                    float visibleT = t * reveal;
                    float jag = (Mathf.Sin(line.Phase + i * 2.17f + lineIndex * 1.6f) + Mathf.Sin(line.Phase * 1.7f + i * 4.13f) * 0.45f) * crackAmplitude * Mathf.Sin(visibleT * Mathf.PI);
                    line.Points[i] = ground + new Vector3(side * lineLength * visibleT, jag + line.BranchBias * visibleT * visibleT, 0f);
                }
                line.Renderer.SetPositions(line.Points);
            }
        }

        private IEnumerator AnimateShellRecoil()
        {
            if (leftShell == null || rightShell == null)
                yield break;

            shellImpactActive = true;
            Vector3 leftStart = leftShell.localPosition;
            Vector3 rightStart = rightShell.localPosition;
            Vector3 leftPeak = leftShellBasePosition + Vector3.left * shellRecoilDistance;
            Vector3 rightPeak = rightShellBasePosition + Vector3.right * shellRecoilDistance;
            float elapsed = 0f;
            while (elapsed < 0.11f)
            {
                elapsed += Time.deltaTime;
                float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / 0.11f), 3f);
                leftShell.localPosition = Vector3.LerpUnclamped(leftStart, leftPeak, eased);
                rightShell.localPosition = Vector3.LerpUnclamped(rightStart, rightPeak, eased);
                leftShell.localScale = leftShellBaseScale * Mathf.Lerp(1.02f, 1.08f, eased);
                rightShell.localScale = rightShellBaseScale * Mathf.Lerp(1.02f, 1.08f, eased);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < 0.34f)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / 0.34f);
                float spring = Mathf.Sin(t * Mathf.PI * 3.2f) * Mathf.Exp(-5.2f * t);
                leftShell.localPosition = Vector3.Lerp(leftPeak, leftShellBasePosition, t) + Vector3.left * (spring * 0.09f);
                rightShell.localPosition = Vector3.Lerp(rightPeak, rightShellBasePosition, t) + Vector3.right * (spring * 0.09f);
                yield return null;
            }

            ResetShellsImmediate();
            shellImpactActive = false;
        }

        private IEnumerator AnimateDirectionalBeam(Vector2 direction)
        {
            // Three deliberately narrow layers read as an energy slash.  The former
            // broad shadow layer was the source of the long, washed-out band.
            GameObject shadow = CreateBeamLayer(direction, voidShadowColor, 0.48f, 45);
            GameObject gold = CreateBeamLayer(direction, voidPortalColor, 0.27f, 46);
            GameObject white = CreateBeamLayer(direction, coreWhiteColor, 0.10f, 47);
            float elapsed = 0f;
            while (elapsed < 0.34f)
            {
                elapsed += Time.deltaTime;
                AnimateBeamLayer(shadow, direction, elapsed, 0.24f, 0.48f);
                AnimateBeamLayer(gold, direction, elapsed, 0.19f, 0.27f);
                AnimateBeamLayer(white, direction, elapsed, 0.11f, 0.10f);
                yield return null;
            }
            Destroy(shadow);
            Destroy(gold);
            Destroy(white);
        }

        private GameObject CreateBeamLayer(Vector2 direction, Color color, float height, int sortingOrder)
        {
            GameObject go = new GameObject($"Riftcore_SideBurst_{(direction.x < 0f ? "L" : "R")}");
            Transform container = VFXContainerUtility.GetContainer();
            if (container != null)
                go.transform.SetParent(container);
            go.transform.position = CoreWorldPosition + Vector3.down * 0.08f;
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = beamSprite;
            renderer.sharedMaterial = spriteMaterial;
            renderer.sortingOrder = sortingOrder;
            color.a = 0f;
            renderer.color = color;
            go.transform.localScale = new Vector3(direction.x * 0.03f, height, 1f);
            return go;
        }

        private void AnimateBeamLayer(GameObject layer, Vector2 direction, float elapsed, float duration, float height)
        {
            if (layer == null)
                return;
            float t = Mathf.Clamp01(elapsed / duration);
            float extend = 1f - Mathf.Pow(1f - t, 4f);
            float targetWidth = Mathf.Max(0.1f, riftcore.AoeRange / 4.8f);
            layer.transform.localScale = new Vector3(direction.x * Mathf.Lerp(0.03f, targetWidth, extend), height * Mathf.Lerp(1f, 0.68f, t), 1f);
            SpriteRenderer renderer = layer.GetComponent<SpriteRenderer>();
            Color color = renderer.color;
            color.a = Mathf.Clamp01(Mathf.InverseLerp(0f, 0.12f, t) * (1f - Mathf.InverseLerp(0.5f, 1f, t))) * (height > 0.35f ? 0.38f : 0.96f);
            renderer.color = color;
        }

        private IEnumerator AnimateImpactRing(float delay, float targetScale, float duration, Color color)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);
            GameObject go = new GameObject("Riftcore_ImpactRing");
            Transform container = VFXContainerUtility.GetContainer();
            if (container != null)
                go.transform.SetParent(container);
            go.transform.position = CoreWorldPosition;
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = ringSprite;
            renderer.sharedMaterial = spriteMaterial;
            renderer.sortingOrder = 48;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scale = Mathf.Lerp(0.12f, targetScale, 1f - Mathf.Pow(1f - t, 3f));
                go.transform.localScale = new Vector3(scale * 1.18f, scale, 1f);
                Color current = color;
                current.a *= (1f - t) * (color == voidShadowColor ? 0.35f : 1f);
                renderer.color = current;
                yield return null;
            }
            Destroy(go);
        }

        private void EmitImpactParticles(Vector2 direction)
        {
            Vector3 origin = CoreWorldPosition + Vector3.down * 0.34f;
            for (int i = 0; i < 28; i++)
            {
                Color color = i % 4 == 0 ? shellDustColor : i % 5 == 0 ? voidShadowColor : voidPortalColor;
                color.a = Random.Range(0.6f, 1f);
                impactParticles.Emit(new ParticleSystem.EmitParams
                {
                    position = origin + new Vector3(Random.Range(-0.12f, 0.12f), Random.Range(-0.1f, 0.18f), 0f),
                    velocity = new Vector2(direction.x * Random.Range(2.2f, 6.4f), Random.Range(-0.25f, 1.5f)),
                    startLifetime = Random.Range(0.28f, 0.68f),
                    startSize = Random.Range(0.045f, 0.13f),
                    startColor = color
                }, 1);
            }

            for (int i = 0; i < 11; i++)
            {
                Color color = i % 3 == 0 ? voidShadowColor : shellDustColor;
                color.a = 0.95f;
                fragmentParticles.Emit(new ParticleSystem.EmitParams
                {
                    position = origin,
                    velocity = new Vector2(direction.x * Random.Range(2.8f, 6.8f), Random.Range(-0.65f, 2.25f)),
                    startLifetime = Random.Range(0.35f, 0.8f),
                    startSize = Random.Range(0.07f, 0.17f),
                    startColor = color,
                    rotation = Random.Range(0f, 360f)
                }, 1);
            }
        }

        private void EmitCoreBurst()
        {
            for (int i = 0; i < 24; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                impactParticles.Emit(new ParticleSystem.EmitParams
                {
                    position = CoreWorldPosition,
                    velocity = direction * Random.Range(1.2f, 4.4f),
                    startLifetime = Random.Range(0.18f, 0.42f),
                    startSize = Random.Range(0.035f, 0.095f),
                    startColor = Color.Lerp(coreWhiteColor, voidPortalColor, Random.value * 0.45f)
                }, 1);
            }
        }

        private void ResetShellsImmediate()
        {
            if (leftShell != null)
            {
                leftShell.localPosition = leftShellBasePosition;
                leftShell.localScale = leftShellBaseScale;
            }
            if (rightShell != null)
            {
                rightShell.localPosition = rightShellBasePosition;
                rightShell.localScale = rightShellBaseScale;
            }
        }

        private void SetRiftEnabled(RiftLine[] lines, bool enabled)
        {
            if (lines == null)
                return;
            foreach (RiftLine line in lines)
                if (line.Renderer != null)
                    line.Renderer.enabled = enabled;
        }

        private Vector3 CoreWorldPosition => transform.TransformPoint(coreLocalOffset);

        private static float EaseInOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
        }

        private static Texture2D CreateSoftDotTexture(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, y), center) / radius), 1.6f)));
            texture.Apply();
            return texture;
        }

        private static Texture2D CreateRingTexture(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                    float ring = 1f - Mathf.Clamp01(Mathf.Abs(distance - 0.66f) / 0.045f);
                    float innerEdge = 1f - Mathf.Clamp01(Mathf.Abs(distance - 0.58f) / 0.018f);
                    float restrainedGlow = 1f - Mathf.Clamp01(Mathf.Abs(distance - 0.66f) / 0.11f);
                    float alpha = Mathf.Clamp01(ring * 0.94f + innerEdge * 0.7f + restrainedGlow * 0.08f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            texture.Apply();
            return texture;
        }

        private static Texture2D CreateBeamTexture(int width, int height)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            float centerY = (height - 1) * 0.5f;
            for (int y = 0; y < height; y++)
            {
                float vertical = Mathf.Abs((y - centerY) / Mathf.Max(1f, centerY));
                for (int x = 0; x < width; x++)
                {
                    float t = x / (float)(width - 1);
                    float taper = Mathf.Lerp(0.8f, 0.04f, Mathf.Pow(t, 1.35f));
                    float body = 1f - Mathf.SmoothStep(taper * 0.58f, taper, vertical);
                    float core = 1f - Mathf.SmoothStep(taper * 0.16f, taper * 0.34f, vertical);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(body * 0.42f + core * 0.85f)));
                }
            }
            texture.Apply();
            return texture;
        }

        private static Texture2D CreateTriangleTexture(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < size; y++)
            {
                float t = y / (float)(size - 1);
                float halfWidth = t * 0.48f;
                for (int x = 0; x < size; x++)
                {
                    float edge = halfWidth - Mathf.Abs(x / (float)(size - 1) - 0.5f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.SmoothStep(0f, 0.08f, edge))));
                }
            }
            texture.Apply();
            return texture;
        }

        private void OnDestroy()
        {
            if (chargeParticles != null) Destroy(chargeParticles.gameObject);
            if (impactParticles != null) Destroy(impactParticles.gameObject);
            if (fragmentParticles != null) Destroy(fragmentParticles.gameObject);
            if (coreHalo != null) Destroy(coreHalo.gameObject);
            DestroyRifts(leftRifts);
            DestroyRifts(rightRifts);
            if (voidMaterials != null)
                foreach (Material material in voidMaterials)
                    Destroy(material);
            if (spriteMaterial != null) Destroy(spriteMaterial);
            if (particleMaterial != null) Destroy(particleMaterial);
            if (sparkleSprite != null) Destroy(sparkleSprite);
            if (ringSprite != null) Destroy(ringSprite);
            if (beamSprite != null) Destroy(beamSprite);
            if (fragmentSprite != null) Destroy(fragmentSprite);
            if (sparkleTexture != null) Destroy(sparkleTexture);
            if (ringTexture != null) Destroy(ringTexture);
            if (beamTexture != null) Destroy(beamTexture);
            if (fragmentTexture != null) Destroy(fragmentTexture);
        }

        private static void DestroyRifts(RiftLine[] lines)
        {
            if (lines == null)
                return;
            foreach (RiftLine line in lines)
                if (line.Renderer != null)
                    Destroy(line.Renderer.gameObject);
        }
    }
}
