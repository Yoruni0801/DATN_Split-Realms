using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Bao.Enemies;

namespace Bao.Bosses
{
    [DisallowMultipleComponent]
    public class DreadwormGlacialIcicle : MonoBehaviour
    {
        public enum IcicleState
        {
            Telegraph,
            Falling,
            Impacted
        }

        [Header("Combat Resolution")]
        [SerializeField] private int damage = 25;
        [SerializeField] private float fallSpeed = 38f;
        [SerializeField] private float telegraphDuration = 0.85f;
        [SerializeField] private float impactRadius = 2.4f;
        [SerializeField] private float bossFriendlyFireDamagePercent = 0.25f; // 25% shell damage

        [Header("Sprites")]
        [SerializeField] private Sprite[] icicleSprites;

        private DreadwormBoss bossOwner;
        private Vector3 targetGroundPosition;
        private Vector3 spawnPosition;
        private IcicleState currentState = IcicleState.Telegraph;
        private float stateStartTime;

        // Visual components
        private SpriteRenderer icicleRenderer;
        private TrailRenderer icicleTrail;
        private GameObject telegraphRoot;
        private SpriteRenderer groundFrostDecal;
        private SpriteRenderer warningRing;
        private SpriteRenderer warningCoreFill;
        private SpriteRenderer[] tinyWarningSpikes;

        private Material icicleShaderMaterial;
        private Material trailMaterial;
        private Material frostDecalMaterial;
        private Material ringWarningMaterial;
        private Material coreWarningMaterial;

        public static void Spawn(
            DreadwormBoss boss,
            Vector3 groundTarget,
            float customTelegraphDuration,
            int customDamage,
            Sprite[] sprites)
        {
            GameObject obj = new GameObject("Dreadworm_GlacialIcicle");
            obj.transform.position = groundTarget + Vector3.up * 18f;
            DreadwormGlacialIcicle script = obj.AddComponent<DreadwormGlacialIcicle>();
            script.Initialize(boss, groundTarget, customTelegraphDuration, customDamage, sprites);
        }

        public void Initialize(
            DreadwormBoss boss,
            Vector3 groundTarget,
            float customTelegraphDuration,
            int customDamage,
            Sprite[] sprites)
        {
            bossOwner = boss;
            targetGroundPosition = groundTarget;
            spawnPosition = targetGroundPosition + Vector3.up * 19f;
            telegraphDuration = Mathf.Max(0.4f, customTelegraphDuration);
            damage = customDamage;
            if (sprites != null && sprites.Length > 0)
            {
                icicleSprites = sprites;
            }

            transform.position = spawnPosition;
            BuildVisuals();
            SetState(IcicleState.Telegraph);
        }

        private void BuildVisuals()
        {
            // Build Main Icicle Sprite
            icicleRenderer = gameObject.AddComponent<SpriteRenderer>();
            if (icicleSprites != null && icicleSprites.Length > 0)
            {
                icicleRenderer.sprite = icicleSprites[UnityEngine.Random.Range(0, icicleSprites.Length)];
            }
            else
            {
                // Fallback to utility shard
                icicleRenderer.sprite = DreadwormVFXUtility.ShardSprite;
            }

            Shader icicleShader = Shader.Find("Bao/GlacialIcicleShader");
            if (icicleShader == null) icicleShader = Shader.Find("Bao/DreadwormVFX");
            if (icicleShader == null) icicleShader = Shader.Find("Sprites/Default");

            icicleShaderMaterial = new Material(icicleShader);
            icicleRenderer.sharedMaterial = icicleShaderMaterial;
            DreadwormVFXUtility.ConfigureRenderer(icicleRenderer, 15);
            transform.localScale = new Vector3(1.85f, 2.2f, 1f);

            // Build High-Speed Frost Trail
            icicleTrail = gameObject.AddComponent<TrailRenderer>();
            Shader trailShader = Shader.Find("Bao/DreadwormVFX");
            if (trailShader == null) trailShader = Shader.Find("Sprites/Default");
            trailMaterial = new Material(trailShader);
            trailMaterial.SetTexture("_MainTex", DreadwormVFXUtility.StreakTexture);
            trailMaterial.SetColor("_TintColor", new Color(0.45f, 1.85f, 2.5f, 1f));

            icicleTrail.sharedMaterial = trailMaterial;
            icicleTrail.time = 0.28f;
            icicleTrail.startWidth = 1.35f;
            icicleTrail.endWidth = 0.05f;
            icicleTrail.autodestruct = false;
            icicleTrail.emitting = false;
            DreadwormVFXUtility.ConfigureRenderer(icicleTrail, 14);

            // Build Telegraph Root & Sub-layers
            telegraphRoot = new GameObject("TelegraphRoot");
            telegraphRoot.transform.SetParent(VFXContainerUtility.GetContainer(), true);
            telegraphRoot.transform.position = targetGroundPosition;
            DreadwormVFXUtility.ConfigureSortingGroup(telegraphRoot, 8);

            // Ground Frost Decal
            groundFrostDecal = CreateSpriteLayer(
                telegraphRoot,
                "GroundFrostDecal",
                DreadwormVFXUtility.SoftDiscSprite,
                new Color(0.12f, 0.45f, 0.95f, 0.55f),
                false,
                8);

            // Warning Inner Core Fill
            warningCoreFill = CreateSpriteLayer(
                telegraphRoot,
                "WarningCoreFill",
                DreadwormVFXUtility.SoftDiscSprite,
                new Color(0.25f, 1.45f, 2.25f, 0.85f),
                true,
                9);

            // Outer Warning Ring
            warningRing = CreateSpriteLayer(
                telegraphRoot,
                "WarningRing",
                DreadwormVFXUtility.RingSprite,
                new Color(0.45f, 1.95f, 2.55f, 1f),
                true,
                10);

            // Tiny Ice Spikes protruding around ring (4 mini spikes)
            tinyWarningSpikes = new SpriteRenderer[4];
            for (int i = 0; i < 4; i++)
            {
                tinyWarningSpikes[i] = CreateSpriteLayer(
                    telegraphRoot,
                    "MiniSpike_" + i,
                    DreadwormVFXUtility.ShardSprite,
                    new Color(1.2f, 2.0f, 2.4f, 1f),
                    true,
                    11);
            }
        }

        private SpriteRenderer CreateSpriteLayer(
            GameObject root,
            string name,
            Sprite sprite,
            Color tint,
            bool additive,
            int orderOffset)
        {
            GameObject layer = new GameObject(name);
            layer.transform.SetParent(root.transform, false);
            SpriteRenderer sr = layer.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = DreadwormVFXUtility.CreateMaterial(tint, additive, sprite != null ? sprite.texture : null, "Mat_" + name);
            sr.color = tint;
            DreadwormVFXUtility.ConfigureRenderer(sr, orderOffset);
            return sr;
        }

        private void SetState(IcicleState newState)
        {
            currentState = newState;
            stateStartTime = Time.time;

            switch (currentState)
            {
                case IcicleState.Telegraph:
                    icicleRenderer.enabled = false;
                    icicleTrail.emitting = false;
                    telegraphRoot.SetActive(true);
                    SpawnTelegraphSnowGather();
                    DreadwormBossAudioController.Instance?.PlayIcicleTelegraph(targetGroundPosition);
                    break;

                case IcicleState.Falling:
                    icicleRenderer.enabled = true;
                    icicleTrail.Clear();
                    icicleTrail.emitting = true;
                    // Rotate downwards with slight jitter
                    float fallAngle = UnityEngine.Random.Range(-4f, 4f);
                    transform.rotation = Quaternion.Euler(0f, 0f, 180f + fallAngle);
                    transform.position = spawnPosition;
                    break;

                case IcicleState.Impacted:
                    icicleRenderer.enabled = false;
                    icicleTrail.emitting = false;
                    if (telegraphRoot != null)
                    {
                        Destroy(telegraphRoot);
                        telegraphRoot = null;
                    }
                    TriggerImpact();
                    break;
            }
        }

        private void Update()
        {
            switch (currentState)
            {
                case IcicleState.Telegraph:
                    UpdateTelegraph();
                    break;

                case IcicleState.Falling:
                    UpdateFalling();
                    break;
            }
        }

        private void UpdateTelegraph()
        {
            float elapsed = Time.time - stateStartTime;
            float progress = Mathf.Clamp01(elapsed / telegraphDuration);
            float eased = DreadwormVFXUtility.EaseInOut(progress);
            float pulse = 0.85f + Mathf.Sin(Time.time * 16f) * 0.18f;

            if (groundFrostDecal != null)
            {
                groundFrostDecal.transform.localScale = new Vector3(impactRadius * 2.2f, impactRadius * 0.55f, 1f) * (0.6f + eased * 0.4f);
                Color c = groundFrostDecal.color;
                c.a = Mathf.Lerp(0.15f, 0.65f, eased);
                groundFrostDecal.color = c;
            }

            if (warningCoreFill != null)
            {
                // Expanding inner charge
                warningCoreFill.transform.localScale = new Vector3(impactRadius * 2.0f, impactRadius * 0.45f, 1f) * eased;
                Color c = warningCoreFill.color;
                c.a = Mathf.Lerp(0.1f, 0.9f, eased) * pulse;
                warningCoreFill.color = c;
            }

            if (warningRing != null)
            {
                // Contracting warning ring into the impact point
                float ringScale = Mathf.Lerp(1.4f, 1.0f, eased);
                warningRing.transform.localScale = new Vector3(impactRadius * 2.0f * ringScale, impactRadius * 0.5f * ringScale, 1f);
                warningRing.transform.rotation = Quaternion.Euler(0f, 0f, Time.time * 80f);
            }

            if (tinyWarningSpikes != null)
            {
                for (int i = 0; i < tinyWarningSpikes.Length; i++)
                {
                    if (tinyWarningSpikes[i] == null) continue;
                    float angle = (i * 90f + Time.time * 45f) * Mathf.Deg2Rad;
                    Vector3 offset = new Vector3(Mathf.Cos(angle) * impactRadius * 0.9f, Mathf.Sin(angle) * impactRadius * 0.25f, 0f);
                    tinyWarningSpikes[i].transform.localPosition = offset;
                    tinyWarningSpikes[i].transform.localScale = new Vector3(0.4f, 0.8f * eased, 1f);
                }
            }

            if (elapsed >= telegraphDuration)
            {
                SetState(IcicleState.Falling);
            }
        }

        private void UpdateFalling()
        {
            Vector3 currentPos = transform.position;
            Vector3 nextPos = Vector3.MoveTowards(currentPos, targetGroundPosition, fallSpeed * Time.deltaTime);
            transform.position = nextPos;

            if (Vector3.Distance(nextPos, targetGroundPosition) <= 0.25f)
            {
                SetState(IcicleState.Impacted);
            }
        }

        private void SpawnTelegraphSnowGather()
        {
            GameObject gatherObj = new GameObject("TelegraphSnowGather");
            gatherObj.transform.SetParent(telegraphRoot.transform, false);
            gatherObj.transform.localPosition = Vector3.zero;

            ParticleSystem ps = DreadwormVFXUtility.CreateParticleSystem(
                gatherObj,
                "InwardFrost",
                DreadwormVFXUtility.CreateMaterial(DreadwormVFXUtility.FrostCyan, true, DreadwormVFXUtility.SoftDiscTexture, "Mat_FrostInward"),
                12);

            var main = ps.main;
            main.duration = telegraphDuration;
            main.loop = false;
            main.startLifetime = 0.35f;
            main.startSpeed = -4.5f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            main.maxParticles = 50;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = impactRadius * 1.2f;

            var emission = ps.emission;
            emission.rateOverTime = 40f;

            ps.Play();
        }

        private void TriggerImpact()
        {
            Vector3 impactPos = targetGroundPosition;
            DreadwormBossAudioController.Instance?.PlayIcicleImpact(impactPos);

            // 1. Play AAA Multi-Tier Impact VFX
            SpawnImpactVFX(impactPos);

            // 2. Resolve Combat Damage (Player Damage & Boss Friendly-Fire Lure)
            ResolveHitDamage(impactPos);

            // 3. Destroy self after trails and bursts conclude
            Destroy(gameObject, 1.5f);
        }

        private void SpawnImpactVFX(Vector3 point)
        {
            GameObject impactRoot = new GameObject("Glacial_Impact_Burst");
            impactRoot.transform.SetParent(VFXContainerUtility.GetContainer(), true);
            impactRoot.transform.position = point;

            // Shards Burst
            ParticleSystem shards = DreadwormVFXUtility.CreateParticleSystem(
                impactRoot,
                "IceShards",
                DreadwormVFXUtility.CreateMaterial(DreadwormVFXUtility.FrostCyan, true, DreadwormVFXUtility.ShardTexture, "Mat_ImpactShards"),
                18);
            var sMain = shards.main;
            sMain.duration = 0.2f;
            sMain.loop = false;
            sMain.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.85f);
            sMain.startSpeed = new ParticleSystem.MinMaxCurve(6f, 14f);
            sMain.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.55f);
            sMain.gravityModifier = 2.4f;
            sMain.maxParticles = 60;

            var sEmission = shards.emission;
            sEmission.rateOverTime = 0f;
            sEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 22, 32) });

            var sShape = shards.shape;
            sShape.shapeType = ParticleSystemShapeType.Cone;
            sShape.angle = 65f;
            sShape.radius = 0.25f;
            impactRoot.transform.rotation = Quaternion.Euler(-90f, 0f, 0f); // Fire upwards

            shards.Play();

            // Snow Burst Cloud & Lingering Mist
            ParticleSystem mist = DreadwormVFXUtility.CreateParticleSystem(
                impactRoot,
                "SnowMist",
                DreadwormVFXUtility.CreateMaterial(new Color(0.85f, 1.6f, 2.0f, 0.45f), false, DreadwormVFXUtility.SoftDiscTexture, "Mat_MistCloud"),
                16);
            var mMain = mist.main;
            mMain.duration = 0.2f;
            mMain.loop = false;
            mMain.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            mMain.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4.5f);
            mMain.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
            mMain.gravityModifier = -0.05f;
            mMain.maxParticles = 30;

            var mEmission = mist.emission;
            mEmission.rateOverTime = 0f;
            mEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 14, 20) });

            mist.Play();

            // Ground Decal Spikes & Frost Stomp
            SpawnGroundFrostShockwave(point);

            // Camera Shake
            DreadwormVFXController.ShakeCamera(0.12f, 0.08f);

            Destroy(impactRoot, 1.6f);
        }

        private void SpawnGroundFrostShockwave(Vector3 point)
        {
            GameObject shockObj = new GameObject("FrostShockwave");
            shockObj.transform.SetParent(VFXContainerUtility.GetContainer(), true);
            shockObj.transform.position = point;

            SpriteRenderer sr = shockObj.AddComponent<SpriteRenderer>();
            sr.sprite = DreadwormVFXUtility.RingSprite;
            sr.sharedMaterial = DreadwormVFXUtility.CreateMaterial(DreadwormVFXUtility.PlasmaElectricCyan, true, DreadwormVFXUtility.RingTexture, "Mat_FrostRing");
            sr.color = new Color(0.6f, 2.2f, 2.8f, 1f);
            DreadwormVFXUtility.ConfigureRenderer(sr, 15);

            StartCoroutine(AnimateShockwave(shockObj, sr, impactRadius * 2.2f, 0.35f));
        }

        private IEnumerator AnimateShockwave(GameObject root, SpriteRenderer sr, float targetScale, float duration)
        {
            float elapsed = 0f;
            Vector3 baseScale = new Vector3(0.2f, 0.06f, 1f);
            Vector3 finalScale = new Vector3(targetScale, targetScale * 0.3f, 1f);

            while (elapsed < duration && root != null && sr != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float ease = DreadwormVFXUtility.EaseOutCubic(t);
                root.transform.localScale = Vector3.Lerp(baseScale, finalScale, ease);
                Color c = sr.color;
                c.a = Mathf.Lerp(1f, 0f, t);
                sr.color = c;
                yield return null;
            }

            if (root != null) Destroy(root);
        }

        private void ResolveHitDamage(Vector3 point)
        {
            Collider2D[] colliders = Physics2D.OverlapCircleAll(point, impactRadius);
            bool hitBossShell = false;

            foreach (var col in colliders)
            {
                if (col == null) continue;

                // 1. Check Player Hit
                if (col.CompareTag("Player"))
                {
                    PlayerHitResolver.TryApplyHitToPlayer(
                        col,
                        damage,
                        ParryableAttackType.Projectile,
                        bossOwner != null ? EnemyParryResponder.Ensure(bossOwner) : null,
                        transform,
                        ((Vector2)col.transform.position - (Vector2)point).normalized + Vector2.up * 0.3f);
                }

                // 2. Check Boss Ice Shell Hit (Friendly-Fire Lure Mechanic!)
                if (bossOwner != null && !hitBossShell)
                {
                    if (col.transform == bossOwner.transform || col.transform.IsChildOf(bossOwner.transform))
                    {
                        if (bossOwner.CurrentState is DreadwormFrozenSanctuaryState sanctuaryState)
                        {
                            hitBossShell = true;
                            sanctuaryState.ApplyLureDamagePercent(bossFriendlyFireDamagePercent, point);
                        }
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (telegraphRoot != null)
            {
                Destroy(telegraphRoot);
            }
            if (icicleShaderMaterial != null) Destroy(icicleShaderMaterial);
            if (trailMaterial != null) Destroy(trailMaterial);
            if (frostDecalMaterial != null) Destroy(frostDecalMaterial);
            if (ringWarningMaterial != null) Destroy(ringWarningMaterial);
            if (coreWarningMaterial != null) Destroy(coreWarningMaterial);
        }
    }
}
