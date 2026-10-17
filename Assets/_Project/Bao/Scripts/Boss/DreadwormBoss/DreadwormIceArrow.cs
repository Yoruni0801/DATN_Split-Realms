using UnityEngine;

namespace Bao.Bosses
{
    [RequireComponent(typeof(Collider2D))]
    public class DreadwormIceArrow : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float speed = 12f;
        [SerializeField] private int damage = 15;
        [SerializeField] private float lifeTime = 5f;
        [SerializeField] private GameObject hitEffectPrefab;
        [SerializeField] private float spriteRotationOffset = -56f;
        [SerializeField, Range(0.45f, 0.9f)] private float suspendedScale = 0.68f;

        private Rigidbody2D rb;
        private Vector2 moveDirection;
        private Vector2 parriedVelocity;
        private bool isInitialized;
        private bool isFired = true;
        private bool wasParried;
        private ParticleSystem frostParticles;
        private ParryableProjectileAdapter parryAdapter;

        private TrailRenderer iceTrail;
        private GameObject chargeVisualRoot;
        private SpriteRenderer chargeCore;
        private SpriteRenderer chargeRingA;
        private SpriteRenderer chargeRingB;
        private Material trailMaterial;
        private Material particleMaterial;
        private Material ringMaterial;
        private Material coreMaterial;
        private Vector3 originalScale;
        private float suspendStartTime;
        private float suspendDuration = 1f;
        private float sequenceOffset;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();

            // Đảm bảo luôn có Rigidbody2D để trigger collisions hoạt động đúng
            // Kinematic: physics không apply lực/gravity, trigger VẪN fire với MovePosition
            if (rb == null)
                rb = gameObject.AddComponent<Rigidbody2D>();

            rb.gravityScale = 0f;
            rb.linearDamping = 0f;
            rb.angularDamping = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            originalScale = transform.localScale;
            CreateFlyingVFX();
            ConfigureProjectileSorting();

            Collider2D projectileCollider = GetComponent<Collider2D>();
            if (projectileCollider != null)
                projectileCollider.isTrigger = true;

            parryAdapter = ParryableProjectileAdapter.Ensure(this);
        }

        private void Start()
        {
            if (isFired)
                Destroy(gameObject, lifeTime);
        }

        private void Update()
        {
            if (!isFired)
            {
                UpdateSuspendedVisuals();
                return;
            }

            if (parryAdapter != null && parryAdapter.ShouldUseParriedMotion)
            {
                UpdateParriedMotion();
                return;
            }

            if (rb == null)
            {
                Vector2 motion = isInitialized
                    ? moveDirection * speed
                    : (Vector2)transform.right * speed;
                transform.position += (Vector3)motion * Time.deltaTime;
                parryAdapter?.TrackMotion(motion);
            }
            else
            {
                // Kinematic rb: dùng MovePosition để trigger hoạt động đúng
                Vector2 motion = isInitialized
                    ? moveDirection * speed
                    : (Vector2)transform.right * speed;
                rb.MovePosition(rb.position + motion * Time.deltaTime);
                parryAdapter?.TrackMotion(motion);
            }
        }

        private void OnDestroy()
        {
            DestroyMaterial(trailMaterial);
            DestroyMaterial(particleMaterial);
            DestroyMaterial(ringMaterial);
            DestroyMaterial(coreMaterial);
        }

        public void Suspend(Vector2 initialDirection)
        {
            Suspend(initialDirection, 1f, 0f);
        }

        public void Suspend(Vector2 initialDirection, float chargeTime, float visualSequenceOffset)
        {
            isFired = false;
            isInitialized = true;
            suspendStartTime = Time.time;
            suspendDuration = Mathf.Max(0.15f, chargeTime);
            sequenceOffset = Mathf.Clamp01(visualSequenceOffset);

            // rb is kinematic; stop any residual motion by zeroing stored velocity
            // (harmless no-op for kinematic, but keeps parryAdapter tracking clean)

            if (iceTrail != null)
            {
                iceTrail.Clear();
                iceTrail.emitting = false;
            }

            if (frostParticles != null)
            {
                var emission = frostParticles.emission;
                emission.enabled = false;
            }

            if (chargeVisualRoot != null)
                chargeVisualRoot.SetActive(true);

            transform.localScale = originalScale * 0.2f;
            ApplyVisualRotation(initialDirection);
        }

        public void Fire(Vector2 direction, float customSpeed, int customDamage)
        {
            moveDirection = direction.normalized;
            speed = customSpeed;
            damage = customDamage;
            isInitialized = true;
            isFired = true;
            transform.localScale = originalScale;

            if (chargeVisualRoot != null)
                chargeVisualRoot.SetActive(false);

            if (iceTrail != null)
            {
                iceTrail.Clear();
                iceTrail.emitting = true;
            }

            if (frostParticles != null)
            {
                var emission = frostParticles.emission;
                emission.enabled = true;
                frostParticles.Emit(14);
            }

            ApplyVisualRotation(moveDirection);

            // Kinematic rb: movement do Update() handle qua MovePosition
            // Chỉ cần TrackMotion để parryAdapter biết velocity hiện tại
            parryAdapter?.TrackMotion(moveDirection * speed);

            Destroy(gameObject, lifeTime);
        }

        public void Initialize(Vector2 direction, float customSpeed, int customDamage)
        {
            Fire(direction, customSpeed, customDamage);
        }

        private void UpdateSuspendedVisuals()
        {
            float rawProgress = Mathf.Clamp01((Time.time - suspendStartTime) / suspendDuration);
            float delayedStart = sequenceOffset * 0.12f;
            float localProgress = Mathf.InverseLerp(delayedStart, 1f, rawProgress);
            float eased = DreadwormVFXUtility.EaseOutCubic(localProgress);
            float pulse = 0.85f + Mathf.Sin(Time.time * 14f + sequenceOffset * 5f) * 0.18f;

            transform.localScale = Vector3.Lerp(
                originalScale * 0.2f,
                originalScale * suspendedScale,
                eased) * pulse;

            if (chargeCore != null)
            {
                chargeCore.transform.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.65f, eased) * pulse;
                chargeCore.color = new Color(1.85f, 2.2f, 2.5f, Mathf.Lerp(0.2f, 0.98f, eased));
            }

            if (chargeRingA != null)
            {
                chargeRingA.transform.localRotation = Quaternion.Euler(0f, 0f, Time.time * 180f);
                chargeRingA.transform.localScale = Vector3.one * Mathf.Lerp(2.6f, 1.25f, eased) * pulse;
                chargeRingA.color = new Color(0.45f, 1.85f, 2.35f, Mathf.Lerp(0.2f, 0.95f, eased));
            }

            if (chargeRingB != null)
            {
                chargeRingB.transform.localRotation = Quaternion.Euler(0f, 0f, -Time.time * 120f);
                chargeRingB.transform.localScale = new Vector3(
                    Mathf.Lerp(3.6f, 1.75f, eased),
                    Mathf.Lerp(2.4f, 1.18f, eased),
                    1f);
                chargeRingB.color = new Color(0.18f, 0.65f, 1.8f, Mathf.Lerp(0.15f, 0.85f, eased));
            }
        }

        private void UpdateParriedMotion()
        {
            if (!wasParried)
            {
                wasParried = true;

                // Khôi phục scale gốc nếu adapter đảo dấu scale.x
                transform.localScale = originalScale;

                float parriedSpeed = Mathf.Max(speed * 1.6f, 16f);
                Vector2 targetDirection = -moveDirection;

                DreadwormBoss boss = Object.FindFirstObjectByType<DreadwormBoss>();
                if (boss != null && !boss.IsDead)
                {
                    targetDirection = ((Vector2)boss.CenterPosition - (Vector2)transform.position).normalized;
                }

                parriedVelocity = targetDirection * parriedSpeed;

                if (rb != null)
                    rb.linearVelocity = parriedVelocity;

                if (parryAdapter != null)
                    parryAdapter.TrackMotion(parriedVelocity);

                ApplyVisualRotation(targetDirection);

                if (frostParticles != null)
                {
                    var emission = frostParticles.emission;
                    emission.enabled = true;
                    frostParticles.Emit(20);
                }
            }

            // Di chuyển trực tiếp theo parriedVelocity hướng về boss
            if (rb != null)
                rb.MovePosition(rb.position + parriedVelocity * Time.deltaTime);
            else
                transform.position += (Vector3)parriedVelocity * Time.deltaTime;

            if (parriedVelocity.sqrMagnitude > 0.001f)
                ApplyVisualRotation(parriedVelocity.normalized);
        }

        private void ApplyVisualRotation(Vector2 direction)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle + spriteRotationOffset, Vector3.forward);
        }

        private void CreateFlyingVFX()
        {
            trailMaterial = DreadwormVFXUtility.CreateMaterial(
                DreadwormVFXUtility.PlasmaElectricCyan,
                true,
                DreadwormVFXUtility.StreakTexture,
                "Dreadworm_IceArrowTrail");
            particleMaterial = DreadwormVFXUtility.CreateMaterial(
                DreadwormVFXUtility.GlintFrostApex,
                true,
                DreadwormVFXUtility.ShardTexture,
                "Dreadworm_IceArrowParticles");
            ringMaterial = DreadwormVFXUtility.CreateMaterial(
                DreadwormVFXUtility.PlasmaElectricCyan,
                true,
                DreadwormVFXUtility.RingTexture,
                "Dreadworm_IceArrowRing");
            coreMaterial = DreadwormVFXUtility.CreateMaterial(
                DreadwormVFXUtility.GlintFrostApex,
                true,
                DreadwormVFXUtility.SoftDiscTexture,
                "Dreadworm_IceArrowCore");

            GameObject trailObject = new GameObject("IceTrail");
            trailObject.transform.SetParent(transform, false);
            iceTrail = trailObject.AddComponent<TrailRenderer>();
            iceTrail.time = 0.38f;
            iceTrail.minVertexDistance = 0.04f;
            iceTrail.startWidth = 0.88f;
            iceTrail.endWidth = 0.015f;
            iceTrail.sharedMaterial = trailMaterial;
            iceTrail.textureMode = LineTextureMode.Stretch;
            iceTrail.numCapVertices = 4;

            Gradient trailGradient = new Gradient();
            trailGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1.8f, 2.2f, 2.5f), 0f),
                    new GradientColorKey(new Color(0.45f, 1.85f, 2.35f), 0.35f),
                    new GradientColorKey(new Color(0.12f, 0.45f, 1.8f), 0.75f),
                    new GradientColorKey(new Color(0.05f, 0.12f, 0.65f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.85f, 0.32f),
                    new GradientAlphaKey(0f, 1f)
                });
            iceTrail.colorGradient = trailGradient;
            DreadwormVFXUtility.ConfigureRenderer(iceTrail, 13);

            GameObject particleObject = new GameObject("FrostWake");
            particleObject.transform.SetParent(transform, false);
            frostParticles = particleObject.AddComponent<ParticleSystem>();
            frostParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = frostParticles.main;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.32f, 0.68f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.32f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;

            var emission = frostParticles.emission;
            emission.rateOverTime = 42f;

            var shape = frostParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 24f;
            shape.radius = 0.18f;
            particleObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var rotation = frostParticles.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-12f, 12f);

            var colorOverLifetime = frostParticles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient frostGradient = new Gradient();
            frostGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(0.45f, 1.85f, 2.35f), 0.5f),
                    new GradientColorKey(new Color(0.15f, 0.45f, 1.5f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.8f, 0.45f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = frostGradient;

            ParticleSystemRenderer particleRenderer = frostParticles.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = particleMaterial;
            particleRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            particleRenderer.lengthScale = 2.2f;
            particleRenderer.velocityScale = 0.25f;
            DreadwormVFXUtility.ConfigureRenderer(particleRenderer, 14);
            frostParticles.Play();

            chargeVisualRoot = new GameObject("ArrowChargeVisual");
            chargeVisualRoot.transform.SetParent(transform, false);

            chargeCore = CreateChargeLayer(
                "ChargeCore",
                DreadwormVFXUtility.SoftDiscSprite,
                coreMaterial,
                16);
            chargeRingA = CreateChargeLayer(
                "ChargeRing_A",
                DreadwormVFXUtility.RingSprite,
                ringMaterial,
                15);
            chargeRingB = CreateChargeLayer(
                "ChargeRing_B",
                DreadwormVFXUtility.RingSprite,
                ringMaterial,
                15);
            chargeVisualRoot.SetActive(false);
        }

        private SpriteRenderer CreateChargeLayer(
            string objectName,
            Sprite sprite,
            Material material,
            int orderOffset)
        {
            GameObject layer = new GameObject(objectName);
            layer.transform.SetParent(chargeVisualRoot.transform, false);
            SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            DreadwormVFXUtility.ConfigureRenderer(renderer, orderOffset);
            return renderer;
        }

        private void ConfigureProjectileSorting()
        {
            SpriteRenderer[] spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < spriteRenderers.Length; i++)
                DreadwormVFXUtility.ConfigureRenderer(spriteRenderers[i], 12 + i);
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            // Khi đã được parry (reflected về boss): bypass guard Enemy/Boss và damage boss trực tiếp
            if (wasParried)
            {
                // Bỏ qua Player (không damage đồng đội)
                if (collision.CompareTag("Player"))
                    return;

                // Bỏ qua trigger-only colliders (sensor zones) — nhưng KHÔNG bỏ qua boss body
                // Boss body: isTrigger=false, nên vẫn pass qua đây
                if (collision.isTrigger)
                    return;

                // Damage bất kỳ IDamageable nào (kể cả Boss/Enemy) khi đã reflected
                PlayerHitResolver.TryApplyDamageToEnemy(collision, damage);

                // Luôn destroy khi hit bất kỳ solid collider nào (boss, wall, ground)
                SpawnHitEffect();
                Destroy(gameObject);
                return;
            }

            // Chưa bị parry: flow bình thường
            if (parryAdapter != null && parryAdapter.TryHandleEnemyCollision(collision, damage))
            {
                SpawnHitEffect();
                Destroy(gameObject);
                return;
            }

            // Bỏ qua va chạm với chính boss (arrow do boss bắn ra)
            if (collision.CompareTag("Enemy") ||
                collision.CompareTag("Boss") ||
                collision.name.Contains("Dreadworm"))
            {
                return;
            }

            if (parryAdapter != null &&
                parryAdapter.IgnorePlayerCollision &&
                collision.CompareTag("Player"))
            {
                return;
            }

            if (collision.CompareTag("Player"))
            {
                IncomingAttackResolution resolution = PlayerHitResolver.TryApplyHitToPlayer(
                    collision,
                    damage,
                    ParryableAttackType.Projectile,
                    parryAdapter,
                    transform,
                    rb != null ? (Vector2)(moveDirection * speed) : moveDirection * speed);

                if (resolution != IncomingAttackResolution.Parried)
                {
                    SpawnHitEffect();
                    Destroy(gameObject);
                }
            }
            else if (!collision.isTrigger)
            {
                // Va chạm với ground/wall/solid — destroy với hiệu ứng vỡ
                SpawnHitEffect();
                Destroy(gameObject);
            }
        }

        private void SpawnHitEffect()
        {
            DreadwormBossAudioController.Instance?.PlayIceArrowHit(transform.position);

            if (hitEffectPrefab != null)
            {
                GameObject effect = Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
                Renderer[] effectRenderers = effect.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < effectRenderers.Length; i++)
                    DreadwormVFXUtility.ConfigureRenderer(effectRenderers[i], 14 + i);
                return;
            }

            GameObject root = new GameObject("Dreadworm_IceShatter");
            root.transform.position = transform.position;

            Material shardBurstMaterial = DreadwormVFXUtility.CreateMaterial(
                DreadwormVFXUtility.GlintFrostApex,
                true,
                DreadwormVFXUtility.ShardTexture,
                "Dreadworm_IceShatterShards");
            Material cyanBurstMaterial = DreadwormVFXUtility.CreateMaterial(
                DreadwormVFXUtility.PlasmaElectricCyan,
                true,
                DreadwormVFXUtility.ShardTexture,
                "Dreadworm_IceShatterCyan");
            Material ringBurstMaterial = DreadwormVFXUtility.CreateMaterial(
                DreadwormVFXUtility.PlasmaElectricCyan,
                true,
                DreadwormVFXUtility.RingTexture,
                "Dreadworm_IceShatterRing");

            // Layer 1: High speed sharp crystal shards
            ParticleSystem shards = DreadwormVFXUtility.CreateParticleSystem(
                root,
                "Shards",
                shardBurstMaterial,
                18);
            var main = shards.main;
            main.duration = 0.15f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.32f, 0.75f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4.5f, 9.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.42f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0.35f;
            var emission = shards.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 22, 32) });
            var shape = shards.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.22f;
            shape.radiusThickness = 1f;
            var rotation = shards.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-14f, 14f);
            ConfigureFade(shards);

            // Layer 2: Plasma sparks burst
            ParticleSystem cyanSparks = DreadwormVFXUtility.CreateParticleSystem(
                root,
                "CyanSparks",
                cyanBurstMaterial,
                17);
            var cyanMain = cyanSparks.main;
            cyanMain.duration = 0.12f;
            cyanMain.loop = false;
            cyanMain.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
            cyanMain.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6.5f);
            cyanMain.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.2f);
            cyanMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var cyanEmission = cyanSparks.emission;
            cyanEmission.rateOverTime = 0f;
            cyanEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 14, 20) });
            var cyanShape = cyanSparks.shape;
            cyanShape.shapeType = ParticleSystemShapeType.Circle;
            cyanShape.radius = 0.3f;
            ConfigureFade(cyanSparks);

            // Layer 3: Expanding Shockwave ring
            ParticleSystem ring = DreadwormVFXUtility.CreateParticleSystem(
                root,
                "ImpactRing",
                ringBurstMaterial,
                16);
            var ringMain = ring.main;
            ringMain.duration = 0.1f;
            ringMain.loop = false;
            ringMain.startLifetime = 0.38f;
            ringMain.startSpeed = 0f;
            ringMain.startSize = 0.9f;
            var ringEmission = ring.emission;
            ringEmission.rateOverTime = 0f;
            ringEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var ringSize = ring.sizeOverLifetime;
            ringSize.enabled = true;
            ringSize.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.3f),
                    new Keyframe(1f, 4.8f)));
            ConfigureFade(ring);

            shards.Play();
            cyanSparks.Play();
            ring.Play();

            DreadwormTransientMaterialCleanup cleanup =
                root.AddComponent<DreadwormTransientMaterialCleanup>();
            cleanup.Initialize(1.2f, shardBurstMaterial, cyanBurstMaterial, ringBurstMaterial);
        }

        private static void ConfigureFade(ParticleSystem particleSystem)
        {
            var color = particleSystem.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(0.22f, 0.68f, 1f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.85f, 0.42f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = gradient;
        }

        private static void DestroyMaterial(Material material)
        {
            if (material != null)
                Destroy(material);
        }
    }

    internal sealed class DreadwormTransientMaterialCleanup : MonoBehaviour
    {
        private Material[] materials;

        public void Initialize(float lifetime, params Material[] ownedMaterials)
        {
            materials = ownedMaterials;
            Destroy(gameObject, lifetime);
        }

        private void OnDestroy()
        {
            if (materials == null)
                return;

            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null)
                    Destroy(materials[i]);
            }
        }
    }
}
