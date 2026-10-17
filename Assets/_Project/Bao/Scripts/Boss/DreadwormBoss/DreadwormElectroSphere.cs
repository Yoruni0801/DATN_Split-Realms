using System.Collections;
using Bao.Enemies;
using UnityEngine;

namespace Bao.Bosses
{
    [RequireComponent(typeof(Collider2D))]
    public class DreadwormElectroSphere : MonoBehaviour
    {
        private float speed;
        private int damage;
        private Vector2 sweepDirection;
        private float groundY;
        private float chargeDuration = 1f;
        private readonly float lifeTime = 15f;

        private bool isSweeping;
        private bool isArmed;
        private bool wasParried;
        private Vector2 parriedVelocity;
        private ParryableProjectileAdapter parryAdapter;
        private Collider2D sphereCollider;
        private Vector3 originalScale;

        private GameObject auraRoot;
        private SpriteRenderer coreGlow;
        private SpriteRenderer ringA;
        private SpriteRenderer ringB;
        private SpriteRenderer groundHalo;
        private TrailRenderer trail;
        private ParticleSystem sparks;
        private LineRenderer[] dischargeArcs;
        private ParticleSystem groundSparks;

        private Material coreMaterial;
        private Material ringMaterial;
        private Material stormMaterial;
        private Material trailMaterial;
        private Material sparkMaterial;
        private Material arcMaterial;
        private Material groundSparkMaterial;

        private float arcUpdateTimer;
        private const float ArcUpdateInterval = 0.045f;

        private Rigidbody2D rb;

        private void Awake()
        {
            sphereCollider = GetComponent<Collider2D>();
            originalScale = transform.localScale;

            // Đảm bảo luôn có Rigidbody2D để OnTriggerEnter2D hoạt động đúng
            rb = GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody2D>();
            }
            rb.gravityScale = 0f;
            rb.linearDamping = 0f;
            rb.angularDamping = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            // Kinematic: Physics engine không apply lực/gravity, nhưng trigger VẪN fire
            // Dùng MovePosition để di chuyển kinematic rb (trigger-safe)
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            ConfigureExistingRenderers();
            CreateVisuals();
        }

        private void Update()
        {
            if (parryAdapter != null && parryAdapter.ShouldUseParriedMotion)
            {
                if (!wasParried)
                {
                    wasParried = true;
                    transform.localScale = originalScale;

                    float parriedSpeed = Mathf.Max(speed * 1.5f, 15f);
                    Vector2 targetDirection = -sweepDirection;

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
                }

                // Di chuyển trực tiếp theo parriedVelocity hướng về boss
                if (rb != null)
                    rb.MovePosition(rb.position + parriedVelocity * Time.deltaTime);
                else
                    transform.position += (Vector3)parriedVelocity * Time.deltaTime;

                RotateAura(360f);
                DisableDischargeArcs();
                return;
            }

            if (isSweeping)
            {
                // Kinematic rb: dùng MovePosition thay vì transform.Translate
                if (rb != null)
                    rb.MovePosition(rb.position + sweepDirection * speed * Time.deltaTime);
                else
                    transform.Translate(sweepDirection * speed * Time.deltaTime, Space.World);
                parryAdapter?.TrackMotion(sweepDirection * speed);
                RotateAura(260f);

                if (groundHalo != null)
                {
                    float pulse = 0.86f + Mathf.Sin(Time.time * 13f) * 0.14f;
                    groundHalo.transform.position = new Vector3(
                        transform.position.x,
                        groundY,
                        transform.position.z);
                    groundHalo.transform.localScale = new Vector3(2.8f * pulse, 0.55f * pulse, 1f);
                }

                UpdateGroundDischarge();
            }
            else
            {
                DisableDischargeArcs();
            }
        }

        private void UpdateGroundDischarge()
        {
            if (dischargeArcs == null || dischargeArcs.Length == 0)
                return;

            arcUpdateTimer -= Time.deltaTime;
            if (arcUpdateTimer > 0f)
                return;

            arcUpdateTimer = ArcUpdateInterval;

            Vector3 spherePos = transform.position;
            float sweepSign = Mathf.Sign(sweepDirection.x);
            if (Mathf.Abs(sweepSign) < 0.01f) sweepSign = 1f;

            for (int i = 0; i < dischargeArcs.Length; i++)
            {
                LineRenderer arc = dischargeArcs[i];
                if (arc == null) continue;

                // Randomly flicker some arcs
                if (Random.value < 0.18f)
                {
                    arc.enabled = false;
                    continue;
                }

                arc.enabled = true;
                float angleOffset = (i / (float)dischargeArcs.Length) * Mathf.PI * 0.85f - (Mathf.PI * 0.42f);
                float xOffset = Mathf.Sin(angleOffset) * 0.75f - (sweepSign * Random.Range(0.2f, 1.35f));
                float yGroundTarget = groundY + Random.Range(-0.04f, 0.08f);

                Vector3 startPoint = spherePos + new Vector3(Mathf.Sin(angleOffset) * 0.45f, -0.2f + Mathf.Cos(angleOffset) * 0.15f, 0f);
                Vector3 endPoint = new Vector3(spherePos.x + xOffset, yGroundTarget, spherePos.z);

                int pointCount = arc.positionCount;
                for (int p = 0; p < pointCount; p++)
                {
                    float t = p / (float)(pointCount - 1);
                    Vector3 basePos = Vector3.Lerp(startPoint, endPoint, t);

                    if (p > 0 && p < pointCount - 1)
                    {
                        float jitterMagnitude = Mathf.Sin(t * Mathf.PI) * Random.Range(0.08f, 0.22f);
                        Vector3 perp = Vector3.Cross((endPoint - startPoint).normalized, Vector3.forward);
                        basePos += perp * jitterMagnitude * (Random.value > 0.5f ? 1f : -1f);
                    }
                    arc.SetPosition(p, basePos);
                }

                float arcAlpha = Random.Range(0.55f, 0.95f);
                Color c = i % 2 == 0 ? DreadwormVFXUtility.PlasmaElectricCyan : DreadwormVFXUtility.GlintFrostApex;
                c.a = arcAlpha;
                arc.startColor = c;
                arc.endColor = new Color(c.r * 0.6f, c.g * 0.8f, c.b, 0.2f);
            }

            if (groundSparks != null && !groundSparks.isPlaying)
            {
                groundSparks.Play();
            }
            if (groundSparks != null)
            {
                groundSparks.transform.position = new Vector3(spherePos.x, groundY, spherePos.z);
            }
        }

        private void DisableDischargeArcs()
        {
            if (dischargeArcs != null)
            {
                for (int i = 0; i < dischargeArcs.Length; i++)
                {
                    if (dischargeArcs[i] != null)
                        dischargeArcs[i].enabled = false;
                }
            }
            if (groundSparks != null && groundSparks.isPlaying)
            {
                groundSparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void OnDestroy()
        {
            if (dischargeArcs != null)
            {
                for (int i = 0; i < dischargeArcs.Length; i++)
                {
                    if (dischargeArcs[i] != null && dischargeArcs[i].gameObject != null)
                        Destroy(dischargeArcs[i].gameObject);
                }
                dischargeArcs = null;
            }

            if (groundSparks != null && groundSparks.gameObject != null)
            {
                Destroy(groundSparks.gameObject);
                groundSparks = null;
            }

            DestroyOwnedMaterial(coreMaterial);
            DestroyOwnedMaterial(ringMaterial);
            DestroyOwnedMaterial(stormMaterial);
            DestroyOwnedMaterial(trailMaterial);
            DestroyOwnedMaterial(sparkMaterial);
            DestroyOwnedMaterial(arcMaterial);
            DestroyOwnedMaterial(groundSparkMaterial);
        }

        public void InitializeAdvanced(
            Vector2 direction,
            float moveSpeed,
            int sphereDamage,
            float targetGroundY,
            float chargeTime)
        {
            sweepDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
            speed = Mathf.Max(0.1f, moveSpeed);
            damage = sphereDamage;
            groundY = targetGroundY;
            chargeDuration = Mathf.Max(0.15f, chargeTime);
            parryAdapter = ParryableProjectileAdapter.Ensure(this);

            if (sphereCollider != null)
            {
                sphereCollider.isTrigger = true;
                sphereCollider.enabled = false;
            }

            Destroy(gameObject, lifeTime);
            StartCoroutine(SequenceRoutine());
        }

        private IEnumerator SequenceRoutine()
        {
            float elapsed = 0f;
            transform.localScale = Vector3.zero;
            if (trail != null)
                trail.emitting = false;

            while (elapsed < chargeDuration)
            {
                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / chargeDuration);
                float eased = DreadwormVFXUtility.EaseOutCubic(normalized);
                float overshoot = Mathf.Sin(normalized * Mathf.PI) * 0.08f;
                transform.localScale = originalScale * (eased + overshoot);
                UpdateChargeVisuals(normalized);
                yield return null;
            }

            transform.localScale = originalScale;
            if (sparks != null)
                sparks.Emit(22);
            yield return new WaitForSeconds(0.12f);

            isArmed = true;
            if (sphereCollider != null)
                sphereCollider.enabled = true;
            if (trail != null)
            {
                trail.Clear();
                trail.emitting = true;
            }

            float fallSpeed = speed * 1.8f;
            Vector3 fallDirection = (Vector3.down * 1.2f + (Vector3)sweepDirection).normalized;

            while ((parryAdapter == null || !parryAdapter.ShouldUseParriedMotion) &&
                   transform.position.y > groundY)
            {
                if (rb != null)
                    rb.MovePosition(rb.position + (Vector2)(fallDirection * fallSpeed * Time.deltaTime));
                else
                    transform.position += fallDirection * fallSpeed * Time.deltaTime;
                RotateAura(320f);
                yield return null;
            }

            if (parryAdapter != null && parryAdapter.ShouldUseParriedMotion)
                yield break;

            transform.position = new Vector3(transform.position.x, groundY, transform.position.z);
            SpawnImpactBurst(transform.position, true);
            if (CameraController.Instance != null)
                CameraController.Instance.ShakeCamera(0.09f, 0.07f);

            isSweeping = true;
            DreadwormBossAudioController.Instance?.PlaySphereSweep(transform.position);
            if (groundHalo != null)
                groundHalo.gameObject.SetActive(true);
        }

        private void UpdateChargeVisuals(float normalized)
        {
            float pulse = 0.84f + Mathf.Sin(Time.time * 11f) * 0.16f;
            float eased = DreadwormVFXUtility.EaseInOut(normalized);

            if (coreGlow != null)
            {
                coreGlow.transform.localScale = Vector3.one * Mathf.Lerp(0.55f, 1.55f, eased) * pulse;
                coreGlow.color = new Color(0.65f, 0.95f, 1f, Mathf.Lerp(0.18f, 0.92f, eased));
            }

            if (ringA != null)
            {
                ringA.transform.localScale = Vector3.one * Mathf.Lerp(3.2f, 1.35f, eased);
                ringA.color = new Color(0.32f, 0.72f, 1f, Mathf.Lerp(0.12f, 0.82f, eased));
            }

            if (ringB != null)
            {
                ringB.transform.localScale = new Vector3(
                    Mathf.Lerp(4.1f, 1.8f, eased),
                    Mathf.Lerp(2.7f, 1.15f, eased),
                    1f);
                ringB.color = new Color(0.18f, 0.42f, 1f, Mathf.Lerp(0.08f, 0.68f, eased));
            }

            RotateAura(Mathf.Lerp(90f, 330f, eased));
        }

        private void RotateAura(float degreesPerSecond)
        {
            if (ringA != null)
                ringA.transform.Rotate(0f, 0f, degreesPerSecond * Time.deltaTime);
            if (ringB != null)
                ringB.transform.Rotate(0f, 0f, -degreesPerSecond * 0.68f * Time.deltaTime);
        }

        private void ConfigureExistingRenderers()
        {
            SpriteRenderer[] spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < spriteRenderers.Length; i++)
                DreadwormVFXUtility.ConfigureRenderer(spriteRenderers[i], 11 + i);
        }

        private void CreateVisuals()
        {
            coreMaterial = DreadwormVFXUtility.CreateMaterial(
                DreadwormVFXUtility.GlintFrostApex,
                true,
                DreadwormVFXUtility.SoftDiscTexture,
                "Dreadworm_SphereCore");
            ringMaterial = DreadwormVFXUtility.CreateMaterial(
                DreadwormVFXUtility.PlasmaElectricCyan,
                true,
                DreadwormVFXUtility.RingTexture,
                "Dreadworm_SphereRing");
            stormMaterial = DreadwormVFXUtility.CreateMaterial(
                DreadwormVFXUtility.StormBlue,
                true,
                DreadwormVFXUtility.RingTexture,
                "Dreadworm_SphereStorm");
            trailMaterial = DreadwormVFXUtility.CreateMaterial(
                DreadwormVFXUtility.PlasmaElectricCyan,
                true,
                DreadwormVFXUtility.StreakTexture,
                "Dreadworm_SphereTrail");
            sparkMaterial = DreadwormVFXUtility.CreateMaterial(
                DreadwormVFXUtility.GlintFrostApex,
                true,
                DreadwormVFXUtility.ShardTexture,
                "Dreadworm_SphereSparks");
            arcMaterial = DreadwormVFXUtility.CreateMaterial(
                DreadwormVFXUtility.PlasmaElectricCyan,
                true,
                DreadwormVFXUtility.StreakTexture,
                "Dreadworm_SphereArcs");
            groundSparkMaterial = DreadwormVFXUtility.CreateMaterial(
                DreadwormVFXUtility.PlasmaElectricCyan,
                true,
                DreadwormVFXUtility.ShardTexture,
                "Dreadworm_SphereGroundSparks");

            auraRoot = new GameObject("DreadwormSphere_Aura");
            auraRoot.transform.SetParent(transform, false);

            coreGlow = CreateLayer(
                auraRoot,
                "CoreGlow",
                DreadwormVFXUtility.SoftDiscSprite,
                coreMaterial,
                18);
            ringA = CreateLayer(
                auraRoot,
                "OrbitRing_A",
                DreadwormVFXUtility.RingSprite,
                ringMaterial,
                17);
            ringB = CreateLayer(
                auraRoot,
                "OrbitRing_B",
                DreadwormVFXUtility.RingSprite,
                stormMaterial,
                16);
            groundHalo = CreateLayer(
                auraRoot,
                "GroundSweepHalo",
                DreadwormVFXUtility.RingSprite,
                ringMaterial,
                14);
            groundHalo.gameObject.SetActive(false);

            GameObject trailObject = new GameObject("StormTrail");
            trailObject.transform.SetParent(transform, false);
            trail = trailObject.AddComponent<TrailRenderer>();
            trail.time = 0.55f;
            trail.minVertexDistance = 0.06f;
            trail.startWidth = 1.35f;
            trail.endWidth = 0.02f;
            trail.sharedMaterial = trailMaterial;
            trail.textureMode = LineTextureMode.Stretch;
            trail.numCapVertices = 4;
            Gradient trailGradient = new Gradient();
            trailGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1.8f, 2.2f, 2.5f), 0f),
                    new GradientColorKey(new Color(0.45f, 1.85f, 2.35f), 0.45f),
                    new GradientColorKey(new Color(0.12f, 0.35f, 1.8f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.95f, 0f),
                    new GradientAlphaKey(0.7f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                });
            trail.colorGradient = trailGradient;
            DreadwormVFXUtility.ConfigureRenderer(trail, 15);

            sparks = DreadwormVFXUtility.CreateParticleSystem(
                auraRoot,
                "StormSparks",
                sparkMaterial,
                19);
            var main = sparks.main;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.22f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.maxParticles = 110;
            var emission = sparks.emission;
            emission.rateOverTime = 35f;
            var shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.75f;
            shape.radiusThickness = 0.5f;
            var noise = sparks.noise;
            noise.enabled = true;
            noise.strength = 0.42f;
            noise.frequency = 1.4f;
            var color = sparks.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(0.45f, 1.85f, 2.35f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = gradient;
            sparks.Play();

            // Create procedural ground discharge lightning arcs (6 lines)
            dischargeArcs = new LineRenderer[6];
            for (int i = 0; i < dischargeArcs.Length; i++)
            {
                GameObject arcObj = new GameObject("GroundDischargeArc_" + i);
                arcObj.transform.SetParent(VFXContainerUtility.GetContainer(), true);
                LineRenderer arc = arcObj.AddComponent<LineRenderer>();
                arc.useWorldSpace = true;
                arc.positionCount = 6;
                arc.sharedMaterial = arcMaterial;
                arc.textureMode = LineTextureMode.Stretch;
                arc.numCapVertices = 2;
                arc.startWidth = 0.12f;
                arc.endWidth = 0.02f;
                DreadwormVFXUtility.ConfigureRenderer(arc, 16);
                arc.enabled = false;
                dischargeArcs[i] = arc;
            }

            // Create ground leakage sparks
            groundSparks = DreadwormVFXUtility.CreateParticleSystem(
                VFXContainerUtility.GetContainer().gameObject,
                "GroundDischargeSparks",
                groundSparkMaterial,
                17);
            var gMain = groundSparks.main;
            gMain.duration = 1f;
            gMain.loop = true;
            gMain.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            gMain.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4.5f);
            gMain.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            gMain.gravityModifier = 0.8f;
            var gEmission = groundSparks.emission;
            gEmission.rateOverTime = 42f;
            var gShape = groundSparks.shape;
            gShape.shapeType = ParticleSystemShapeType.Hemisphere;
            gShape.radius = 0.85f;
            gShape.rotation = new Vector3(-90f, 0f, 0f);
            groundSparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private static SpriteRenderer CreateLayer(
            GameObject parent,
            string objectName,
            Sprite sprite,
            Material material,
            int sortingOffset)
        {
            GameObject layer = new GameObject(objectName);
            layer.transform.SetParent(parent.transform, false);
            SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.color = Color.white;
            DreadwormVFXUtility.ConfigureRenderer(renderer, sortingOffset);
            return renderer;
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (!isArmed)
                return;

            // Khi đã được parry (reflected về boss): bypass guard và damage boss trực tiếp
            if (parryAdapter != null && parryAdapter.ShouldUseParriedMotion)
            {
                // Bỏ qua Player và trigger colliders (sensor zones)
                if (collision.CompareTag("Player")) return;
                if (collision.isTrigger) return;

                // Damage bất kỳ IDamageable nào khi đã reflected
                PlayerHitResolver.TryApplyDamageToEnemy(collision, damage);

                // Luôn destroy khi hit bất kỳ solid collider nào
                SpawnImpactBurst(transform.position, false);
                Destroy(gameObject);
                return;
            }

            // Chưa bị parry: flow bình thường
            if (parryAdapter != null &&
                parryAdapter.IgnorePlayerCollision &&
                collision.CompareTag("Player"))
            {
                return;
            }

            if (parryAdapter != null && parryAdapter.TryHandleEnemyCollision(collision, damage))
            {
                SpawnImpactBurst(transform.position, false);
                Destroy(gameObject);
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
                    sweepDirection * speed);

                if (resolution != IncomingAttackResolution.Parried)
                {
                    SpawnImpactBurst(transform.position, false);
                    Destroy(gameObject);
                }
            }
        }

        private static void SpawnImpactBurst(Vector3 position, bool groundImpact)
        {
            DreadwormBossAudioController.Instance?.PlaySphereImpact(position);

            GameObject root = new GameObject(
                groundImpact ? "Dreadworm_SphereGroundImpact" : "Dreadworm_SphereHit");
            root.transform.SetParent(VFXContainerUtility.GetContainer(), true);
            root.transform.position = position;

            Material burstMaterial = DreadwormVFXUtility.CreateMaterial(
                DreadwormVFXUtility.GlintFrostApex,
                true,
                DreadwormVFXUtility.ShardTexture,
                "Dreadworm_SphereImpactShards");
            Material ringBurstMaterial = DreadwormVFXUtility.CreateMaterial(
                DreadwormVFXUtility.PlasmaElectricCyan,
                true,
                DreadwormVFXUtility.RingTexture,
                "Dreadworm_SphereImpactRing");
            Material stormBurstMaterial = DreadwormVFXUtility.CreateMaterial(
                DreadwormVFXUtility.StormBlue,
                true,
                DreadwormVFXUtility.RingTexture,
                "Dreadworm_SphereImpactStormRing");

            ParticleSystem shards = DreadwormVFXUtility.CreateParticleSystem(
                root,
                "StormShards",
                burstMaterial,
                20);
            var main = shards.main;
            main.duration = 0.16f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.72f);
            main.startSpeed = groundImpact
                ? new ParticleSystem.MinMaxCurve(3.5f, 8f)
                : new ParticleSystem.MinMaxCurve(4f, 9.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.32f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = groundImpact ? 1.2f : 0.15f;
            var emission = shards.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, (short)(groundImpact ? 28 : 20), (short)(groundImpact ? 38 : 28))
            });
            var shape = shards.shape;
            shape.shapeType = groundImpact
                ? ParticleSystemShapeType.Hemisphere
                : ParticleSystemShapeType.Circle;
            shape.radius = 0.28f;
            shape.radiusThickness = 1f;
            ConfigureFade(shards);

            ParticleSystem ring = DreadwormVFXUtility.CreateParticleSystem(
                root,
                "StormShockwave",
                ringBurstMaterial,
                18);
            var ringMain = ring.main;
            ringMain.duration = 0.1f;
            ringMain.loop = false;
            ringMain.startLifetime = 0.42f;
            ringMain.startSpeed = 0f;
            ringMain.startSize = groundImpact ? 1.25f : 0.8f;
            var ringEmission = ring.emission;
            ringEmission.rateOverTime = 0f;
            ringEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var size = ring.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.3f),
                    new Keyframe(1f, groundImpact ? 6.5f : 4.5f)));
            ConfigureFade(ring);

            ParticleSystem stormRing = DreadwormVFXUtility.CreateParticleSystem(
                root,
                "StormShockwaveOuter",
                stormBurstMaterial,
                17);
            var stormMain = stormRing.main;
            stormMain.duration = 0.1f;
            stormMain.loop = false;
            stormMain.startLifetime = 0.5f;
            stormMain.startSpeed = 0f;
            stormMain.startSize = groundImpact ? 0.9f : 0.6f;
            var stormEmission = stormRing.emission;
            stormEmission.rateOverTime = 0f;
            stormEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var stormSize = stormRing.sizeOverLifetime;
            stormSize.enabled = true;
            stormSize.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.2f),
                    new Keyframe(1f, groundImpact ? 7.8f : 5.2f)));
            ConfigureFade(stormRing);

            shards.Play();
            ring.Play();
            stormRing.Play();
            DreadwormTransientMaterialCleanup cleanup =
                root.AddComponent<DreadwormTransientMaterialCleanup>();
            cleanup.Initialize(1.25f, burstMaterial, ringBurstMaterial, stormBurstMaterial);
        }

        private static void ConfigureFade(ParticleSystem particleSystem)
        {
            var color = particleSystem.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1.8f, 2.2f, 2.5f), 0f),
                    new GradientColorKey(new Color(0.45f, 1.85f, 2.35f), 0.55f),
                    new GradientColorKey(new Color(0.12f, 0.35f, 1.8f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.85f, 0.45f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = gradient;
        }

        private static void DestroyOwnedMaterial(Material material)
        {
            if (material != null)
                Destroy(material);
        }
    }
}
