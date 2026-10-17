using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Bao.Enemies
{
    [RequireComponent(typeof(DusthornEnemy))]
    public class DusthornVFXController : MonoBehaviour
    {
        [Header("Shaders")]
        [SerializeField] private Shader cycloneShader;
        [SerializeField] private Shader bodyShader;

        [Header("Palette")]
        [SerializeField] private Color boneHighlight = new Color(1.00f, 0.95f, 0.82f, 1f);   // ivory
        [SerializeField] private Color sandMid = new Color(0.84f, 0.68f, 0.47f, 1f);         // warm sand
        [SerializeField] private Color sandDark = new Color(0.52f, 0.39f, 0.28f, 1f);        // shadow sand
        [SerializeField] private Color dustColor = new Color(0.76f, 0.64f, 0.50f, 0.72f);
        [SerializeField] private Color rockColor = new Color(0.36f, 0.28f, 0.22f, 1f);

        [Header("Bone Cyclone")]
        [SerializeField] private Vector2 cycloneLocalOffset = new Vector2(0f, -0.05f);
        [SerializeField] private float cycloneRadius = 1.25f;
        [SerializeField] private float cycloneChargeSize = 0.9f;
        [SerializeField] private float cycloneRollSize = 1.22f;
        [SerializeField] private float cycloneAlpha = 0.85f;
        [SerializeField] private float cycloneSpinSpeed = 6.0f;
        [SerializeField] private float cycloneStretch = 0.7f;

        [Header("Windup")]
        [SerializeField] private float windupMoteInterval = 0.045f;
        [SerializeField] private int windupStartBurst = 8;
        [SerializeField] private float windupRadius = 1.1f;

        [Header("Rolling Particles")]
        [SerializeField] private int groundDustRate = 34;
        [SerializeField] private int airDustRate = 16;
        [SerializeField] private int streakRate = 18;
        [SerializeField] private float burstInterval = 0.1f;

        [Header("Impact")]
        [SerializeField] private int impactBoneShards = 8;
        [SerializeField] private int impactDustPuffs = 10;
        [SerializeField] private float impactShakeDuration = 0.10f;
        [SerializeField] private float impactShakeStrength = 0.06f;

        [Header("Optional Camera Shake")]
        [SerializeField] private DusthornCameraShake2D cameraShake;

        private DusthornEnemy dusthorn;
        private SpriteRenderer[] renderers;
        private Material[] bodyMaterials;

        private Transform vfxContainer;
        private DusthornTransientVfxRunner transientVfxRunner;
        private GameObject cycloneInstance;
        private Material cycloneMaterial;
        private SpriteRenderer cycloneRenderer;

        private ParticleSystem groundDustPS;
        private ParticleSystem airDustPS;
        private ParticleSystem speedStreakPS;

        private Material spriteMaterial;
        private Material additiveMaterial;

        private Sprite softCircleSprite;
        private Sprite ringSprite;
        private Sprite shardSprite;
        private Sprite rockSprite;
        private Sprite streakSprite;

        private readonly List<Object> runtimeAssets = new List<Object>();
        private readonly List<GameObject> transientVfx = new List<GameObject>();

        private bool isRolling;
        private float rollDirection = 1f;
        private float rollDuration = 0f;
        private float rollTimer = 0f;
        private float windup01 = 0f;
        private float rollVisual01 = 0f;
        private float bodySweep = 0f;
        private float burstTimer = 0f;
        private Coroutine windupRoutine;
        private Coroutine afterImageRoutine;
        private Coroutine deathRoutine;
        private bool rollInterrupted;

        private int baseSortingOrder;

        private void Awake()
        {
            dusthorn = GetComponent<DusthornEnemy>();
            renderers = dusthorn.GetVisualSpriteRenderers();
            vfxContainer = VFXContainerUtility.GetContainer();
            if (vfxContainer != null)
            {
                transientVfxRunner = vfxContainer.GetComponent<DusthornTransientVfxRunner>();
                if (transientVfxRunner == null)
                    transientVfxRunner = vfxContainer.gameObject.AddComponent<DusthornTransientVfxRunner>();
            }

            if (cycloneShader == null)
                cycloneShader = Shader.Find("Bao/RollWindShieldShader");

            if (bodyShader == null)
                bodyShader = Shader.Find("Bao/DusthornBoneArmorShader");

            CacheSortingOrder();
            BuildRuntimeAssets();
            SetupBodyMaterials();
            CreateCycloneObject();
            CreateGroundDustPS();
            CreateAirDustPS();
            CreateSpeedStreakPS();

            if (cameraShake == null && Camera.main != null)
                cameraShake = Camera.main.GetComponent<DusthornCameraShake2D>();
        }

        private void OnDisable()
        {
            ForceStopRollVisuals(true);
            deathRoutine = null;
        }

        private void OnDestroy()
        {
            CleanupTransientVfx(true);
            if (cycloneInstance != null)
                Destroy(cycloneInstance);

            if (bodyMaterials != null)
            {
                foreach (Material mat in bodyMaterials)
                    if (mat != null)
                        Destroy(mat);
            }

            Destroy(cycloneMaterial);
            DestroyParticleMaterial(groundDustPS);
            DestroyParticleMaterial(airDustPS);
            DestroyParticleMaterial(speedStreakPS);

            foreach (Object asset in runtimeAssets)
                if (asset != null)
                    Destroy(asset);
        }

        private void Update()
        {
            if (dusthorn == null || dusthorn.StateMachine == null)
                return;

            bool alive = !dusthorn.IsDead;
            DusthornRollState rollState = dusthorn.StateMachine.CurrentState as DusthornRollState;
            bool inRollState = alive && rollState != null;
            bool rollingState = inRollState && rollState.IsRolling;

            if (!inRollState)
                windup01 = Mathf.MoveTowards(windup01, 0f, Time.deltaTime * 3.2f);
            else if (!rollingState)
                windup01 = rollState.WindupProgress;

            if (!rollingState)
                rollVisual01 = Mathf.MoveTowards(rollVisual01, 0f, Time.deltaTime * 5f);

            if (isRolling)
            {
                rollTimer += Time.deltaTime;
                burstTimer += Time.deltaTime;
            }

            UpdateCycloneVisual(inRollState, rollingState);
            UpdateBodyShader(rollingState);
            UpdateParticleIntensity(rollingState);

            if (isRolling && burstTimer >= burstInterval)
            {
                burstTimer = 0f;
                SpawnRollingContactBurst();
            }
        }

        // ---------------------------------------------------------------------
        // Public hooks
        // ---------------------------------------------------------------------

        public void TriggerWindupStart(float duration)
        {
            rollInterrupted = false;
            if (windupRoutine != null)
                StopCoroutine(windupRoutine);

            windupRoutine = StartCoroutine(WindupRoutine(Mathf.Max(0.08f, duration)));
        }

        public void TriggerRollStart(float direction, float duration)
        {
            rollInterrupted = false;
            isRolling = true;
            rollDirection = direction == 0f ? 1f : Mathf.Sign(direction);
            rollDuration = Mathf.Max(0.1f, duration);
            rollTimer = 0f;
            burstTimer = 0f;
            rollVisual01 = 1f;
            windup01 = 1f;

            if (cycloneInstance != null)
                cycloneInstance.SetActive(true);

            SpawnRollStartBurst();

            if (afterImageRoutine != null)
                StopCoroutine(afterImageRoutine);

            afterImageRoutine = StartCoroutine(RollAfterImageLoop());
        }

        public void TriggerRollEnd()
        {
            if (rollInterrupted)
                return;

            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            {
                ForceStopRollVisuals(true);
                return;
            }

            if (!isRolling && rollVisual01 <= 0.01f)
                return;

            isRolling = false;
            windup01 = 0f;

            if (afterImageRoutine != null)
            {
                StopCoroutine(afterImageRoutine);
                afterImageRoutine = null;
            }

            StartCoroutine(RollEndFadeRoutine());
            SpawnRecoveryDust();
        }

        public void TriggerPlayerImpact(Vector3 point)
        {
            SpawnRing(point, 0.75f, boneHighlight, 0.18f, true, baseSortingOrder + 9);
            SpawnBurstDust(point, impactDustPuffs, 0.55f, true);
            SpawnBoneShardBurst(point, impactBoneShards, 0.8f, 0.28f);
            cameraShake?.Shake(impactShakeDuration, impactShakeStrength);
        }

        public void TriggerParryBreak()
        {
            rollInterrupted = true;
            ForceStopRollVisuals(true);
            cameraShake?.Shake(0.08f, 0.05f);
        }

        public void TriggerDeathCollapse()
        {
            if (deathRoutine != null)
                return;

            deathRoutine = StartCoroutine(DeathCollapseRoutine());
        }

        // ---------------------------------------------------------------------
        // Setup
        // ---------------------------------------------------------------------

        private void CacheSortingOrder()
        {
            baseSortingOrder = 0;

            if (renderers == null)
                return;

            foreach (SpriteRenderer sr in renderers)
            {
                if (sr != null)
                    baseSortingOrder = Mathf.Max(baseSortingOrder, sr.sortingOrder);
            }
        }

        private void BuildRuntimeAssets()
        {
            Shader spriteShader = Shader.Find("Sprites/Default");
            if (spriteShader != null)
            {
                spriteMaterial = new Material(spriteShader) { name = "Dusthorn_SpriteVFX_Runtime" };
                runtimeAssets.Add(spriteMaterial);

                additiveMaterial = new Material(spriteShader) { name = "Dusthorn_AdditiveVFX_Runtime" };
                runtimeAssets.Add(additiveMaterial);
            }

            softCircleSprite = CreateSoftCircleSprite(64);
            ringSprite = CreateRingSprite(96);
            shardSprite = CreateShardSprite(48, 72);
            rockSprite = CreateRockSprite(52);
            streakSprite = CreateStreakSprite(80, 20);

            runtimeAssets.Add(softCircleSprite);
            runtimeAssets.Add(ringSprite);
            runtimeAssets.Add(shardSprite);
            runtimeAssets.Add(rockSprite);
            runtimeAssets.Add(streakSprite);

            runtimeAssets.Add(softCircleSprite.texture);
            runtimeAssets.Add(ringSprite.texture);
            runtimeAssets.Add(shardSprite.texture);
            runtimeAssets.Add(rockSprite.texture);
            runtimeAssets.Add(streakSprite.texture);
        }

        private void SetupBodyMaterials()
        {
            if (bodyShader == null || renderers == null)
                return;

            bodyMaterials = new Material[renderers.Length];

            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer sr = renderers[i];
                if (sr == null)
                    continue;

                Material source = sr.sharedMaterial;
                Material mat = new Material(bodyShader)
                {
                    name = $"DusthornBody_Runtime_{i}"
                };

                if (source != null && source.HasProperty("_MainTex") && mat.HasProperty("_MainTex"))
                    mat.SetTexture("_MainTex", source.GetTexture("_MainTex"));

                if (mat.HasProperty("_WarmEdgeColor")) mat.SetColor("_WarmEdgeColor", boneHighlight);
                if (mat.HasProperty("_ShadowTint")) mat.SetColor("_ShadowTint", sandDark);
                if (mat.HasProperty("_SandTint")) mat.SetColor("_SandTint", sandMid);

                sr.material = mat;
                bodyMaterials[i] = mat;
            }
        }

        private void CreateCycloneObject()
        {
            if (cycloneShader == null)
                return;

            cycloneInstance = new GameObject("DusthornBoneCyclone");
            if (vfxContainer != null)
            {
                cycloneInstance.transform.SetParent(vfxContainer, true);
                cycloneInstance.transform.position = transform.TransformPoint(cycloneLocalOffset);
            }
            else
            {
                cycloneInstance.transform.SetParent(transform, false);
                cycloneInstance.transform.localPosition = cycloneLocalOffset;
            }
            cycloneInstance.transform.localScale = Vector3.one * cycloneRadius * 2f;

            cycloneRenderer = cycloneInstance.AddComponent<SpriteRenderer>();
            cycloneMaterial = new Material(cycloneShader);
            cycloneRenderer.material = cycloneMaterial;
            cycloneRenderer.sortingOrder = baseSortingOrder + 2;
            cycloneRenderer.sprite = CreateCycloneBaseSprite();
            cycloneInstance.SetActive(false);
        }

        private Sprite CreateCycloneBaseSprite()
        {
            Texture2D tex = NewTexture(48, 48);
            for (int y = 0; y < 48; y++)
            {
                for (int x = 0; x < 48; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(23.5f, 23.5f));
                    float alpha = dist <= 23.5f ? 1f : 0f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, 48, 48), new Vector2(0.5f, 0.5f), 48);
            runtimeAssets.Add(sprite);
            runtimeAssets.Add(tex);
            return sprite;
        }

        private void CreateGroundDustPS()
        {
            groundDustPS = CreateParticleSystem("GroundDustPS", new Vector3(0f, -0.52f, 0f), baseSortingOrder - 3, softCircleSprite.texture);
            var main = groundDustPS.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.32f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.45f);
            main.startColor = dustColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 180;
            main.playOnAwake = false;

            var emission = groundDustPS.emission;
            emission.rateOverTime = groundDustRate;

            var shape = groundDustPS.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.35f, 0.06f, 0.01f);

            var color = groundDustPS.colorOverLifetime;
            color.enabled = true;
            color.color = CreateGradient(Color.white, new Color(0.85f, 0.78f, 0.72f), 0.0f, 0.65f, 0f);

            var size = groundDustPS.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.45f),
                new Keyframe(0.35f, 1f),
                new Keyframe(1f, 1.55f)));

            var velocity = groundDustPS.velocityOverLifetime;
            // Unity requires all Velocity over Lifetime axes to share one curve mode.
            // X/Y use TwoConstants, so keep Z in TwoConstants as well even in 2D.
            velocity.x = new ParticleSystem.MinMaxCurve(-0.8f, -2.8f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.1f, 0.6f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.enabled = true;
        }

        private void CreateAirDustPS()
        {
            airDustPS = CreateParticleSystem("AirDustPS", new Vector3(0f, -0.2f, 0f), baseSortingOrder + 1, softCircleSprite.texture);
            var main = airDustPS.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 3.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.startColor = new Color(dustColor.r, dustColor.g, dustColor.b, 0.55f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;
            main.playOnAwake = false;

            var emission = airDustPS.emission;
            emission.rateOverTime = airDustRate;

            var shape = airDustPS.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.35f;
            shape.radiusThickness = 0.35f;

            var color = airDustPS.colorOverLifetime;
            color.enabled = true;
            color.color = CreateGradient(Color.white, new Color(0.92f, 0.84f, 0.75f), 0.0f, 0.55f, 0f);

            var velocity = airDustPS.velocityOverLifetime;
            // Keep all three axes in TwoConstants mode; Z is unused by this 2D VFX.
            velocity.x = new ParticleSystem.MinMaxCurve(-1.4f, -3.8f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.4f, 0.8f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.enabled = true;
        }

        private void CreateSpeedStreakPS()
        {
            speedStreakPS = CreateParticleSystem("SpeedStreakPS", new Vector3(-0.15f, -0.08f, 0f), baseSortingOrder + 3, streakSprite.texture);
            var main = speedStreakPS.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.09f, 0.16f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4.5f, 7.8f);
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(0.45f, 0.95f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
            main.startColor = new Color(boneHighlight.r, boneHighlight.g, boneHighlight.b, 0.24f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;
            main.playOnAwake = false;

            var emission = speedStreakPS.emission;
            emission.rateOverTime = streakRate;

            var shape = speedStreakPS.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.22f, 0.18f, 0.01f);

            var color = speedStreakPS.colorOverLifetime;
            color.enabled = true;
            color.color = CreateGradient(Color.white, boneHighlight, 0f, 0.32f, 0f);

            var renderer = speedStreakPS.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2.0f;
            renderer.velocityScale = 0.55f;
            renderer.alignment = ParticleSystemRenderSpace.Velocity;
        }

        private ParticleSystem CreateParticleSystem(string objectName, Vector3 localPos, int sortingOrder, Texture texture)
        {
            GameObject obj = new GameObject(objectName);
            obj.transform.SetParent(transform);
            obj.transform.localPosition = localPos;

            ParticleSystem ps = obj.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
            Shader particleShader = Shader.Find("Sprites/Default");
            if (particleShader == null)
                particleShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");

            if (particleShader != null)
            {
                renderer.material = new Material(particleShader);
                renderer.material.mainTexture = texture;
            }

            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = sortingOrder;
            renderer.minParticleSize = 0.001f;
            renderer.maxParticleSize = 2f;
            return ps;
        }

        // ---------------------------------------------------------------------
        // Visual updates
        // ---------------------------------------------------------------------

        private void UpdateCycloneVisual(bool inRollState, bool rollingState)
        {
            if (cycloneInstance == null || cycloneMaterial == null)
                return;

            bool active = inRollState || isRolling || rollVisual01 > 0.01f;
            cycloneInstance.SetActive(active);
            if (!active)
                return;

            float activeRoll = rollingState || isRolling ? 1f : 0f;
            float bodySpeed = dusthorn != null && dusthorn.Rigidbody2D != null
                ? Mathf.Abs(dusthorn.Rigidbody2D.linearVelocity.x)
                : 0f;
            float speed01 = dusthorn != null ? Mathf.Clamp01(bodySpeed / Mathf.Max(0.01f, dusthorn.RollSpeed)) : 0f;

            float targetScale = Mathf.Lerp(cycloneChargeSize, cycloneRollSize, Mathf.Max(activeRoll, speed01));
            if (cycloneInstance.transform.parent == transform)
                cycloneInstance.transform.localPosition = cycloneLocalOffset;
            else
                cycloneInstance.transform.position = transform.TransformPoint(cycloneLocalOffset);
            cycloneInstance.transform.localScale = Vector3.one * cycloneRadius * 2f * targetScale;

            float alpha = cycloneAlpha * Mathf.Lerp(0.35f + windup01 * 0.35f, 1f, activeRoll);
            cycloneMaterial.SetColor("_Tint", boneHighlight);
            cycloneMaterial.SetColor("_CoreColor", sandMid);
            cycloneMaterial.SetColor("_OuterColor", new Color(boneHighlight.r, boneHighlight.g, boneHighlight.b, 0.85f));
            cycloneMaterial.SetFloat("_Alpha", alpha);
            cycloneMaterial.SetFloat("_Radius", 0.72f);
            cycloneMaterial.SetFloat("_Thickness", Mathf.Lerp(0.11f, 0.18f, activeRoll));
            cycloneMaterial.SetFloat("_ArcCount", 4.5f);
            cycloneMaterial.SetFloat("_ArcSharpness", Mathf.Lerp(1.8f, 3.2f, activeRoll));
            cycloneMaterial.SetFloat("_NoiseScale", 6f);
            cycloneMaterial.SetFloat("_SpinSpeed", cycloneSpinSpeed * (0.55f + speed01 * 1.2f));
            cycloneMaterial.SetFloat("_RollDirection", rollDirection);
            cycloneMaterial.SetFloat("_Stretch", cycloneStretch * Mathf.Lerp(0.25f, 1f, speed01));
            cycloneMaterial.SetFloat("_Charge", windup01);
            cycloneMaterial.SetFloat("_Speed01", speed01);

            cycloneRenderer.flipX = rollDirection < 0f;
        }

        private void UpdateBodyShader(bool rollingState)
        {
            if (bodyMaterials == null)
                return;

            bodySweep += Time.deltaTime * Mathf.Lerp(0.35f, 2.1f, rollingState ? 1f : windup01);
            float sweep = Mathf.Repeat(bodySweep, 1f);

            foreach (Material mat in bodyMaterials)
            {
                if (mat == null)
                    continue;

                if (mat.HasProperty("_WarmEdgeColor")) mat.SetColor("_WarmEdgeColor", boneHighlight);
                if (mat.HasProperty("_SandTint")) mat.SetColor("_SandTint", sandMid);
                if (mat.HasProperty("_ShadowTint")) mat.SetColor("_ShadowTint", sandDark);
                if (mat.HasProperty("_Charge")) mat.SetFloat("_Charge", windup01);
                if (mat.HasProperty("_Roll")) mat.SetFloat("_Roll", rollingState || isRolling ? 1f : 0f);
                if (mat.HasProperty("_SweepPos")) mat.SetFloat("_SweepPos", sweep);
                if (mat.HasProperty("_HighlightStrength")) mat.SetFloat("_HighlightStrength", Mathf.Lerp(0.85f, 1.55f, windup01 + (rollingState ? 0.45f : 0f)));
                if (mat.HasProperty("_EdgeGlow")) mat.SetFloat("_EdgeGlow", Mathf.Lerp(0.28f, 0.85f, windup01 + (rollingState ? 0.35f : 0f)));
            }
        }

        private void UpdateParticleIntensity(bool rollingState)
        {
            if (groundDustPS == null || airDustPS == null || speedStreakPS == null)
                return;

            if (rollingState || isRolling)
            {
                PlayParticles(groundDustPS);
                PlayParticles(airDustPS);
                PlayParticles(speedStreakPS);

                float bodySpeed = dusthorn != null && dusthorn.Rigidbody2D != null
                    ? Mathf.Abs(dusthorn.Rigidbody2D.linearVelocity.x)
                    : 0f;
                float speed01 = dusthorn != null ? Mathf.Clamp01(bodySpeed / Mathf.Max(0.01f, dusthorn.RollSpeed)) : 1f;

                float directionSign = -rollDirection;

                var gEmission = groundDustPS.emission;
                gEmission.rateOverTime = Mathf.Lerp(groundDustRate * 0.55f, groundDustRate * 1.3f, speed01);

                var gVelocity = groundDustPS.velocityOverLifetime;
                gVelocity.x = new ParticleSystem.MinMaxCurve(directionSign * 0.8f, directionSign * Mathf.Lerp(2f, 4.5f, speed01));
                gVelocity.y = new ParticleSystem.MinMaxCurve(0.08f, Mathf.Lerp(0.35f, 0.9f, speed01));
                gVelocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

                var aEmission = airDustPS.emission;
                aEmission.rateOverTime = Mathf.Lerp(airDustRate * 0.35f, airDustRate * 1.2f, speed01);

                var aVelocity = airDustPS.velocityOverLifetime;
                aVelocity.x = new ParticleSystem.MinMaxCurve(directionSign * 1.2f, directionSign * Mathf.Lerp(3f, 5.8f, speed01));
                aVelocity.y = new ParticleSystem.MinMaxCurve(-0.2f, Mathf.Lerp(0.5f, 1.2f, speed01));
                aVelocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

                var sEmission = speedStreakPS.emission;
                sEmission.rateOverTime = Mathf.Lerp(streakRate * 0.25f, streakRate * 1.4f, speed01);
            }
            else
            {
                StopParticles(groundDustPS, false);
                StopParticles(airDustPS, false);
                StopParticles(speedStreakPS, false);
            }
        }

        // ---------------------------------------------------------------------
        // Windup and roll sequences
        // ---------------------------------------------------------------------

        private IEnumerator WindupRoutine(float duration)
        {
            Vector3 center = transform.position + new Vector3(0f, -0.2f, 0f);

            for (int i = 0; i < windupStartBurst; i++)
                SpawnWindupMote(center, true);

            float elapsed = 0f;
            float spawnTimer = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                spawnTimer += Time.deltaTime;
                windup01 = Mathf.Clamp01(elapsed / duration);

                float interval = Mathf.Lerp(windupMoteInterval * 1.35f, windupMoteInterval * 0.55f, windup01);

                while (spawnTimer >= interval)
                {
                    spawnTimer -= interval;
                    SpawnWindupMote(center, false);

                    if (windup01 > 0.72f && Random.value < 0.28f)
                        SpawnPebbleHover(center);
                }

                yield return null;
            }

            windup01 = 1f;
            windupRoutine = null;
        }

        private void SpawnWindupMote(Vector3 center, bool burst)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(windupRadius * 0.5f, windupRadius);
            Vector3 start = center + new Vector3(Mathf.Cos(angle) * radius, Random.Range(-0.18f, 0.24f), 0f);
            Vector3 target = center + new Vector3(Random.Range(-0.12f, 0.12f), Random.Range(-0.08f, 0.12f), 0f);

            Color color = Random.value < 0.15f
                ? boneHighlight
                : Color.Lerp(dustColor, sandMid, Random.Range(0.2f, 0.75f));

            GameObject go = CreateSpriteVFX("Dusthorn_WindupMote", softCircleSprite, color, false, baseSortingOrder + 4);
            go.transform.position = start;
            go.transform.localScale = Vector3.one * (burst ? Random.Range(0.05f, 0.09f) : Random.Range(0.035f, 0.07f));

            StartCoroutine(AnimateWindupMote(go, start, target, color, burst ? 0.30f : 0.24f));
        }

        private IEnumerator AnimateWindupMote(GameObject go, Vector3 start, Vector3 target, Color color, float duration)
        {
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            Vector3 initialScale = go.transform.localScale;
            Vector3 control = Vector3.Lerp(start, target, 0.55f) + new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(0f, 0.3f), 0f);
            float elapsed = 0f;

            while (go != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 2f);

                Vector3 a = Vector3.Lerp(start, control, eased);
                Vector3 b = Vector3.Lerp(control, target, eased);
                go.transform.position = Vector3.Lerp(a, b, eased);
                go.transform.localScale = initialScale * Mathf.Lerp(1.2f, 0.1f, t);

                if (sr != null)
                {
                    Color c = color;
                    c.a = Mathf.Sin(t * Mathf.PI) * 0.9f;
                    sr.color = c;
                }

                yield return null;
            }

            DestroyTransientVfx(go);
        }

        private void SpawnPebbleHover(Vector3 center)
        {
            Vector3 start = center + new Vector3(Random.Range(-0.55f, 0.55f), -0.18f, 0f);
            GameObject go = CreateSpriteVFX("Dusthorn_Pebble", rockSprite, rockColor, false, baseSortingOrder + 3);
            go.transform.position = start;
            go.transform.localScale = Vector3.one * Random.Range(0.07f, 0.14f);
            StartCoroutine(AnimatePebbleHover(go, start));
        }

        private IEnumerator AnimatePebbleHover(GameObject go, Vector3 start)
        {
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            Vector3 end = start + new Vector3(Random.Range(-0.06f, 0.06f), Random.Range(0.15f, 0.32f), 0f);
            float rot = Random.Range(-220f, 220f);
            float duration = Random.Range(0.18f, 0.32f);
            float elapsed = 0f;

            while (go != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                go.transform.position = Vector3.Lerp(start, end, t);
                go.transform.Rotate(0f, 0f, rot * Time.deltaTime);

                if (sr != null)
                {
                    Color c = rockColor;
                    c.a = Mathf.Lerp(1f, 0f, Mathf.SmoothStep(0.55f, 1f, t));
                    sr.color = c;
                }

                yield return null;
            }

            DestroyTransientVfx(go);
        }

        private void SpawnRollStartBurst()
        {
            Vector3 point = transform.position + new Vector3(0f, -0.28f, 0f);
            SpawnRing(point, 0.95f, sandMid, 0.18f, true, baseSortingOrder + 5);
            SpawnBurstDust(point, 10, 0.62f, true);
            SpawnRockBurst(point, 6, 0.72f);
        }

        private void SpawnRollingContactBurst()
        {
            Vector3 point = transform.position + new Vector3(-rollDirection * 0.18f, -0.52f, 0f);
            SpawnBurstDust(point, 4, 0.36f, false);

            if (Random.value < 0.8f)
                SpawnRockBurst(point, Random.Range(1, 3), 0.3f);

            if (Random.value < 0.9f)
                SpawnBoneShardBurst(point, Random.Range(1, 3), 0.28f, 0.14f);
        }

        private void SpawnRecoveryDust()
        {
            Vector3 point = transform.position + new Vector3(0f, -0.5f, 0f);
            SpawnBurstDust(point, 7, 0.5f, false);
            SpawnRing(point, 0.78f, dustColor, 0.2f, false, baseSortingOrder + 2);
        }

        private IEnumerator RollEndFadeRoutine()
        {
            float elapsed = 0f;
            float duration = 0.22f;
            float startVisual = Mathf.Max(0.4f, rollVisual01);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                rollVisual01 = Mathf.Lerp(startVisual, 0f, t);
                yield return null;
            }

            rollVisual01 = 0f;

            if (cycloneInstance != null)
                cycloneInstance.SetActive(false);
        }

        private IEnumerator RollAfterImageLoop()
        {
            while (isRolling)
            {
                SpawnAfterImage();
                yield return new WaitForSeconds(0.08f);
            }

            afterImageRoutine = null;
        }

        private void SpawnAfterImage()
        {
            if (renderers == null)
                return;

            foreach (SpriteRenderer src in renderers)
            {
                if (src == null || src.sprite == null)
                    continue;

                GameObject go = new GameObject("Dusthorn_AfterImage");
                RegisterTransientVfx(go);
                if (vfxContainer != null)
                    go.transform.SetParent(vfxContainer, true);

                go.transform.position = src.transform.position;
                go.transform.rotation = src.transform.rotation;
                go.transform.localScale = src.transform.lossyScale;

                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = src.sprite;
                sr.flipX = src.flipX;
                sr.sortingOrder = src.sortingOrder - 1;
                sr.sharedMaterial = spriteMaterial;
                sr.color = new Color(boneHighlight.r, boneHighlight.g, boneHighlight.b, 0.12f);

                StartCoroutine(AnimateAfterImage(go, sr));
            }
        }

        private IEnumerator AnimateAfterImage(GameObject go, SpriteRenderer sr)
        {
            Vector3 startScale = go.transform.localScale;
            float elapsed = 0f;
            const float duration = 0.16f;

            while (go != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                go.transform.localScale = Vector3.Lerp(startScale, startScale * 0.95f, t);

                if (sr != null)
                {
                    Color c = sr.color;
                    c.a = Mathf.Lerp(0.12f, 0f, t);
                    sr.color = c;
                }

                yield return null;
            }

            DestroyTransientVfx(go);
        }

        private IEnumerator DeathCollapseRoutine()
        {
            Vector3 center = transform.position + new Vector3(0f, -0.1f, 0f);

            for (int i = 0; i < 3; i++)
            {
                SpawnRing(center, 0.6f + i * 0.12f, boneHighlight, 0.12f, true, baseSortingOrder + 7);
                yield return new WaitForSeconds(0.05f);
            }

            SpawnBoneShardBurst(center, 16, 0.95f, 0.42f);
            SpawnRockBurst(center, 10, 0.8f);
            SpawnBurstDust(center, 16, 0.92f, true);
            cameraShake?.Shake(0.14f, 0.08f);

            deathRoutine = null;
        }

        // ---------------------------------------------------------------------
        // Burst helpers
        // ---------------------------------------------------------------------

        private void SpawnRing(Vector3 center, float radius, Color color, float duration, bool bright, int sortingOrder)
        {
            GameObject go = CreateSpriteVFX("Dusthorn_Ring", ringSprite, color, bright, sortingOrder);
            go.transform.position = center;
            go.transform.localScale = Vector3.one * 0.12f;
            StartCoroutine(AnimateRing(go, color, radius, duration));
        }

        private IEnumerator AnimateRing(GameObject go, Color color, float radius, float duration)
        {
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            Vector3 startScale = Vector3.one * 0.12f;
            Vector3 endScale = Vector3.one * radius * 2f;
            float elapsed = 0f;

            while (go != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);

                go.transform.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);

                if (sr != null)
                {
                    Color c = color;
                    c.a = Mathf.Pow(1f - t, 1.5f);
                    sr.color = c;
                }

                yield return null;
            }

            DestroyTransientVfx(go);
        }

        private void SpawnBurstDust(Vector3 center, int count, float radius, bool bright)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 dir2 = Random.insideUnitCircle.normalized;
                Vector3 velocity = new Vector3(
                    dir2.x * Random.Range(radius * 0.9f, radius * 2.2f),
                    Mathf.Abs(dir2.y) * Random.Range(0.35f, 1.15f) + 0.1f,
                    0f);

                Color c = Color.Lerp(dustColor, sandMid, Random.Range(0.15f, 0.65f));

                GameObject go = CreateSpriteVFX("Dusthorn_DustBurst", softCircleSprite, c, bright, baseSortingOrder + 4);
                go.transform.position = center + (Vector3)(Random.insideUnitCircle * 0.05f);
                go.transform.localScale = Vector3.one * Random.Range(0.06f, 0.16f);

                StartCoroutine(AnimateSpritePuff(go, c, velocity, Random.Range(0.32f, 0.58f), true));
            }
        }

        private void SpawnRockBurst(Vector3 center, int count, float power)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 velocity = new Vector3(
                    Random.Range(-1f, 1f) * power,
                    Random.Range(0.6f, 1.65f) * power,
                    0f);

                GameObject go = CreateSpriteVFX("Dusthorn_Rock", rockSprite, rockColor, false, baseSortingOrder + 6);
                go.transform.position = center + new Vector3(Random.Range(-0.06f, 0.06f), 0f, 0f);
                go.transform.localScale = Vector3.one * Random.Range(0.06f, 0.14f);
                go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

                StartCoroutine(AnimateBallisticSprite(go, velocity, rockColor, Random.Range(0.42f, 0.82f), 5.2f));
            }
        }

        private void SpawnBoneShardBurst(Vector3 center, int count, float power, float baseLifetime)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 velocity = new Vector3(
                    Random.Range(-1f, 1f) * power,
                    Random.Range(0.65f, 1.95f) * power,
                    0f);

                Color c = Color.Lerp(sandMid, boneHighlight, Random.Range(0.15f, 0.9f));
                GameObject go = CreateSpriteVFX("Dusthorn_BoneShard", shardSprite, c, true, baseSortingOrder + 8);
                go.transform.position = center + new Vector3(Random.Range(-0.04f, 0.04f), Random.Range(-0.02f, 0.06f), 0f);
                go.transform.localScale = new Vector3(Random.Range(0.05f, 0.1f), Random.Range(0.1f, 0.18f), 1f);
                go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

                StartCoroutine(AnimateBallisticSprite(go, velocity, c, Random.Range(baseLifetime, baseLifetime + 0.16f), 4.8f));
            }
        }

        private IEnumerator AnimateSpritePuff(GameObject go, Color color, Vector3 velocity, float duration, bool expand)
        {
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            Vector3 startScale = go.transform.localScale;
            float elapsed = 0f;

            while (go != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                velocity *= Mathf.Exp(-3.1f * Time.deltaTime);
                velocity.y += 0.18f * Time.deltaTime;

                go.transform.position += velocity * Time.deltaTime;
                go.transform.localScale = startScale * Mathf.Lerp(0.7f, expand ? 2.25f : 1.4f, t);

                if (sr != null)
                {
                    Color c = color;
                    c.a = Mathf.Lerp(color.a, 0f, t);
                    sr.color = c;
                }

                yield return null;
            }

            DestroyTransientVfx(go);
        }

        private IEnumerator AnimateBallisticSprite(GameObject go, Vector3 velocity, Color color, float duration, float gravity)
        {
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            Vector3 startScale = go.transform.localScale;
            float spin = Random.Range(-720f, 720f);
            float elapsed = 0f;

            while (go != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                velocity.y -= gravity * Time.deltaTime;
                go.transform.position += velocity * Time.deltaTime;
                go.transform.Rotate(0f, 0f, spin * Time.deltaTime);
                go.transform.localScale = startScale * Mathf.Lerp(1f, 0.45f, t);

                if (sr != null && t > 0.55f)
                {
                    Color c = color;
                    c.a = 1f - Mathf.InverseLerp(0.55f, 1f, t);
                    sr.color = c;
                }

                yield return null;
            }

            DestroyTransientVfx(go);
        }

        // ---------------------------------------------------------------------
        // Runtime helpers
        // ---------------------------------------------------------------------

        private GameObject CreateSpriteVFX(string objectName, Sprite sprite, Color color, bool bright, int sortingOrder)
        {
            GameObject go = new GameObject(objectName);
            RegisterTransientVfx(go);
            if (vfxContainer != null)
                go.transform.SetParent(vfxContainer, true);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            sr.sharedMaterial = bright ? additiveMaterial : spriteMaterial;

            return go;
        }

        private void RegisterTransientVfx(GameObject go)
        {
            if (go == null)
                return;

            transientVfx.RemoveAll(item => item == null);
            transientVfx.Add(go);
            Destroy(go, 2f);
        }

        private void DestroyTransientVfx(GameObject go)
        {
            if (go == null)
                return;

            transientVfx.Remove(go);
            DespawnTransientVfx(go);
        }

        private void DespawnTransientVfx(GameObject go)
        {
            if (go == null)
                return;

            go.SetActive(false);

            if (!gameObject.activeInHierarchy)
            {
                DestroyImmediate(go);
                return;
            }

            if (transientVfxRunner != null)
            {
                transientVfxRunner.Despawn(go);
                return;
            }

            Destroy(go);
        }

        private void CleanupTransientVfx(bool immediate)
        {
            for (int i = transientVfx.Count - 1; i >= 0; i--)
            {
                GameObject go = transientVfx[i];
                transientVfx.RemoveAt(i);
                if (go == null)
                    continue;

                if (immediate)
                    DespawnTransientVfx(go);
                else
                    Destroy(go, 0.05f);
            }
        }

        private void ForceStopRollVisuals(bool cleanupTransient)
        {
            StopAllCoroutines();
            windupRoutine = null;
            afterImageRoutine = null;

            isRolling = false;
            windup01 = 0f;
            rollVisual01 = 0f;
            rollTimer = 0f;
            burstTimer = 0f;

            StopParticles(groundDustPS, true);
            StopParticles(airDustPS, true);
            StopParticles(speedStreakPS, true);

            if (cycloneInstance != null)
                cycloneInstance.SetActive(false);

            ResetBodyShader();

            if (cleanupTransient)
                CleanupTransientVfx(true);
        }

        private void ResetBodyShader()
        {
            if (bodyMaterials == null)
                return;

            foreach (Material mat in bodyMaterials)
            {
                if (mat == null)
                    continue;

                if (mat.HasProperty("_Charge")) mat.SetFloat("_Charge", 0f);
                if (mat.HasProperty("_Roll")) mat.SetFloat("_Roll", 0f);
                if (mat.HasProperty("_HighlightStrength")) mat.SetFloat("_HighlightStrength", 0.85f);
                if (mat.HasProperty("_EdgeGlow")) mat.SetFloat("_EdgeGlow", 0.28f);
            }
        }

        private static Gradient CreateGradient(Color startColor, Color endColor, float startAlpha, float middleAlpha, float endAlpha)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(startColor, 0f),
                    new GradientColorKey(endColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(startAlpha, 0f),
                    new GradientAlphaKey(middleAlpha, 0.35f),
                    new GradientAlphaKey(endAlpha, 1f)
                });
            return gradient;
        }

        private static void PlayParticles(ParticleSystem particles)
        {
            if (particles != null && !particles.isPlaying)
                particles.Play();
        }

        private static void StopParticles(ParticleSystem particles, bool clear)
        {
            if (particles == null)
                return;

            particles.Stop(true, clear
                ? ParticleSystemStopBehavior.StopEmittingAndClear
                : ParticleSystemStopBehavior.StopEmitting);
        }

        private static void DestroyParticleMaterial(ParticleSystem particles)
        {
            if (particles == null)
                return;

            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            if (renderer != null && renderer.sharedMaterial != null)
                Destroy(renderer.sharedMaterial);
        }

        private static Sprite CreateSoftCircleSprite(int size)
        {
            Texture2D tex = NewTexture(size, size);
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float maxRadius = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / maxRadius;
                    float alpha = Mathf.Clamp01(1f - d);
                    alpha = alpha * alpha * (3f - 2f * alpha);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite CreateRingSprite(int size)
        {
            Texture2D tex = NewTexture(size, size);
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.38f;
            float thickness = size * 0.055f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center);
                    float edgeDistance = Mathf.Abs(d - radius);
                    float alpha = Mathf.Clamp01(1f - edgeDistance / thickness);
                    alpha = alpha * alpha;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite CreateShardSprite(int width, int height)
        {
            Texture2D tex = NewTexture(width, height);

            for (int y = 0; y < height; y++)
            {
                float ny = (y / (float)(height - 1)) * 2f - 1f;
                float halfWidth = Mathf.Lerp(0f, 0.95f, 1f - Mathf.Abs(ny));

                for (int x = 0; x < width; x++)
                {
                    float nx = Mathf.Abs((x / (float)(width - 1)) * 2f - 1f);
                    float body = Mathf.Clamp01((halfWidth - nx) * 8f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, body));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.42f), height);
        }

        private static Sprite CreateRockSprite(int size)
        {
            Texture2D tex = NewTexture(size, size);
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.34f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 delta = new Vector2(x, y) - center;
                    float angle = Mathf.Atan2(delta.y, delta.x);
                    float wobble = Mathf.Sin(angle * 5f) * 0.08f + Mathf.Sin(angle * 9f + 0.7f) * 0.045f;
                    float localRadius = radius * (1f + wobble);
                    float d = delta.magnitude;
                    float alpha = Mathf.Clamp01((localRadius - d) * 0.75f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite CreateStreakSprite(int width, int height)
        {
            Texture2D tex = NewTexture(width, height);

            for (int y = 0; y < height; y++)
            {
                float ny = Mathf.Abs((y / (float)(height - 1)) * 2f - 1f);
                float vertical = Mathf.Clamp01(1f - ny);

                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)(width - 1);
                    float alpha = Mathf.Pow(1f - nx, 1.35f) * vertical;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.02f, 0.5f), width);
        }

        private static Texture2D NewTexture(int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "Dusthorn_ProceduralVFX"
            };

            Color clear = new Color(1f, 1f, 1f, 0f);
            Color[] pixels = new Color[width * height];

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = clear;

            tex.SetPixels(pixels);
            return tex;
        }
    }
}

namespace Bao.Enemies
{
    internal sealed class DusthornTransientVfxRunner : MonoBehaviour
    {
        public void Despawn(GameObject go)
        {
            if (go == null)
                return;

            go.SetActive(false);
            StartCoroutine(DestroyNextFrame(go));
        }

        private IEnumerator DestroyNextFrame(GameObject go)
        {
            yield return null;

            if (go != null)
                Destroy(go);
        }
    }
}
