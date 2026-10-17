using System.Collections.Generic;
using UnityEngine;

namespace Quoc.QuyVuong.Boss_Phase3
{
    [RequireComponent(typeof(BoxCollider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class GroundLineHazard : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float speed = 15f;
        [Tooltip("Chỉ dùng để dọn hazard nếu nó bay ra khỏi arena mà không chạm gì.")]
        [SerializeField] private float failsafeLifetime = 8f;

        [Header("Damage Settings")]
        [SerializeField] private int damage = 20;

        [Header("Impact Settings")]
        [SerializeField] private GameObject dusthornPrefab;
        [SerializeField] private int maxActiveDusthorns = 2;
        [SerializeField] private bool spawnDusthornOnImpact;
        [SerializeField] private LayerMask environmentLayers;

        private static readonly HashSet<GameObject> ActiveDusthorns = new();
        private static int PendingDusthornSpawns;
        private readonly List<Material> runtimeMaterials = new();
        private float moveDirectionX = 1f;
        private bool isInitialized;
        private bool hasImpacted;
        private ParticleSystem continuousWaveParticle;
        private LineRenderer fractureCrown;
        private Rigidbody2D body;
        private BoxCollider2D hitbox;

        private void Awake()
        {
            // Prefab VFX gốc không có collider/Rigidbody được serialize. Tự bảo đảm
            // hai component này tồn tại để hazard luôn nhận va chạm ở runtime.
            hitbox = GetComponent<BoxCollider2D>();
            if (hitbox == null)
            {
                hitbox = gameObject.AddComponent<BoxCollider2D>();
                hitbox.size = new Vector2(1.2f, 1.2f);
            }
            hitbox.isTrigger = true;

            body = GetComponent<Rigidbody2D>();
            if (body == null) body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.simulated = true;
            body.gravityScale = 0f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        public void Initialize(float directionX, int damageAmount, float moveSpeed = 15f)
        {
            Initialize(directionX, damageAmount, moveSpeed, null, 0, false, 0);
        }

        public void Initialize(float directionX, int damageAmount, float moveSpeed, GameObject mobPrefab,
            int mobLimit, bool spawnMob, int environmentLayerMask)
        {
            moveDirectionX = Mathf.Sign(directionX);
            if (Mathf.Approximately(moveDirectionX, 0f)) moveDirectionX = 1f;
            damage = damageAmount;
            speed = moveSpeed;
            dusthornPrefab = mobPrefab;
            maxActiveDusthorns = mobLimit;
            spawnDusthornOnImpact = spawnMob;
            environmentLayers = environmentLayerMask;
            isInitialized = true;

            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * moveDirectionX;
            transform.localScale = scale;

            SetupContinuousGroundWave();
            ConfigureAuthoredVfx();
            SetupGroundRiftVisual();

            // Ensure hazard & particle visuals render in front of background
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                if (r != null && r.sortingOrder < 20)
                {
                    r.sortingOrder = 25;
                }
            }

            CancelInvoke(nameof(ExpireAtEndpoint));
            Invoke(nameof(ExpireAtEndpoint), Mathf.Max(1f, failsafeLifetime));
        }

        /// <summary>
        /// GroundLine gốc gồm các burst dài 0.5-0.8 giây. Cho chúng lặp trong suốt
        /// tuổi thọ hazard để phần nhìn không biến mất trong khi hitbox vẫn bay.
        /// </summary>
        private void ConfigureAuthoredVfx()
        {
            ParticleSystem[] particles = GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem particle in particles)
            {
                if (particle == null || particle == continuousWaveParticle) continue;
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particle.main;

                // Hai hệ gốc có speed 80 nên khi loop biến thành ống neon. Giữ lại
                // texture đẹp của chúng nhưng ép về một cụm năng lượng ngắn quanh đầu hazard.
                bool isRootEnergy = particle.transform == transform;
                bool isBigGlow = particle.name.Equals("BigGlow", System.StringComparison.OrdinalIgnoreCase);
                if (isRootEnergy || isBigGlow)
                {
                    main.loop = true;
                    main.playOnAwake = false;
                    main.duration = isBigGlow ? 0.62f : 0.48f;
                    main.startLifetime = isBigGlow
                        ? new ParticleSystem.MinMaxCurve(0.18f, 0.32f)
                        : new ParticleSystem.MinMaxCurve(0.22f, 0.38f);
                    main.startSpeed = isBigGlow
                        ? new ParticleSystem.MinMaxCurve(0.04f, 0.22f)
                        : new ParticleSystem.MinMaxCurve(0.25f, 0.9f);
                    main.startSize = isBigGlow
                        ? new ParticleSystem.MinMaxCurve(1.1f, 1.75f)
                        : new ParticleSystem.MinMaxCurve(0.72f, 1.24f);
                    main.startColor = isBigGlow
                        ? new Color(0.82f, 0.45f, 0.16f, 0.30f)
                        : new Color(0.95f, 0.68f, 0.28f, 0.78f);
                    particle.Play(true);
                    continue;
                }

                main.loop = true;
                main.playOnAwake = false;
                particle.Play(true);
            }
        }

        /// <summary>
        /// Tạo hệ thống hạt liên tục bám theo sóng địa chấn dọc suốt đường di chuyển,
        /// gồm các tia lửa nham thạch, bụi đá cày xới mặt đất và luồng sáng rực rỡ.
        /// </summary>
        private void SetupContinuousGroundWave()
        {
            GameObject waveVfxChild = new GameObject("ContinuousGroundWave_VFX");
            waveVfxChild.transform.SetParent(transform, false);
            waveVfxChild.transform.localPosition = new Vector3(0f, -0.05f, -0.05f);

            continuousWaveParticle = waveVfxChild.AddComponent<ParticleSystem>();
            continuousWaveParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = continuousWaveParticle.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 1.0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 1.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.24f, 0.62f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;

            // Màu vàng hổ phách / nham thạch rực lửa phù hợp với lance_m9
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1.0f, 0.84f, 0.46f), 0.0f),
                    new GradientColorKey(new Color(0.68f, 0.40f, 0.15f), 0.5f),
                    new GradientColorKey(new Color(0.19f, 0.12f, 0.08f), 1.0f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.85f, 0.0f),
                    new GradientAlphaKey(0.68f, 0.6f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            main.startColor = new ParticleSystem.MinMaxGradient(gradient);

            var emission = continuousWaveParticle.emission;
            emission.enabled = true;
            emission.rateOverTime = 32f;
            emission.rateOverDistance = 3f;

            var shape = continuousWaveParticle.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.38f, 0.14f, 0.1f);

            var sizeOverLifetime = continuousWaveParticle.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0.0f, 0.4f);
            sizeCurve.AddKey(0.3f, 1.0f);
            sizeCurve.AddKey(1.0f, 0.0f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

            ParticleSystemRenderer psRenderer = waveVfxChild.GetComponent<ParticleSystemRenderer>();
            if (psRenderer != null)
            {
                psRenderer.sortingOrder = 29;
                Material debrisMaterial = GroundLineVfxUtility.CreateMaterial(
                    new Color(0.20f, 0.10f, 0.045f, 0.78f),
                    new Color(1f, 0.72f, 0.27f, 0.94f), 1.85f);
                if (debrisMaterial != null)
                {
                    runtimeMaterials.Add(debrisMaterial);
                    psRenderer.sharedMaterial = debrisMaterial;
                }
            }

            continuousWaveParticle.Play();
        }

        private void SetupGroundRiftVisual()
        {
            Material riftMaterial = GroundLineVfxUtility.CreateMaterial(
                new Color(0.22f, 0.105f, 0.035f, 0.78f),
                new Color(1f, 0.72f, 0.25f, 0.94f), 2.1f);
            if (riftMaterial != null) runtimeMaterials.Add(riftMaterial);

            // Một đường nứt ngắn ôm sát đầu hazard, không để lại dải phía sau.
            fractureCrown = GroundLineVfxUtility.CreateLine(
                transform, "Earth_Rift_Fracture_Crown", riftMaterial, 31, 0.045f);
            fractureCrown.positionCount = 7;
            fractureCrown.SetPosition(0, new Vector3(-0.62f, -0.10f, -0.1f));
            fractureCrown.SetPosition(1, new Vector3(-0.40f, 0.05f, -0.1f));
            fractureCrown.SetPosition(2, new Vector3(-0.20f, -0.02f, -0.1f));
            fractureCrown.SetPosition(3, new Vector3(0f, 0.13f, -0.1f));
            fractureCrown.SetPosition(4, new Vector3(0.20f, -0.01f, -0.1f));
            fractureCrown.SetPosition(5, new Vector3(0.42f, 0.07f, -0.1f));
            fractureCrown.SetPosition(6, new Vector3(0.66f, -0.10f, -0.1f));
            fractureCrown.startColor = new Color(0.29f, 0.15f, 0.065f, 0.35f);
            fractureCrown.endColor = new Color(0.82f, 0.55f, 0.20f, 0.60f);
        }

        private void Update()
        {
            if (!isInitialized || hasImpacted || fractureCrown == null) return;
            float pulse = 0.86f + Mathf.Sin(Time.time * 13f) * 0.08f;
            fractureCrown.widthMultiplier = 0.042f * pulse;
        }

        private void FixedUpdate()
        {
            if (!isInitialized || hasImpacted) return;

            Vector2 nextPosition = body.position +
                                   Vector2.right * (moveDirectionX * speed * Time.fixedDeltaTime);
            body.MovePosition(nextPosition);
        }

        private void ExpireAtEndpoint()
        {
            if (!hasImpacted) Impact(transform.position, false, false);
        }

        private void OnTriggerEnter2D(Collider2D collision) => HandleCollision(collision);
        private void OnCollisionEnter2D(Collision2D collision) => HandleCollision(collision.collider);

        private void HandleCollision(Collider2D collider)
        {
            if (hasImpacted || collider == null || PlayerHitResolver.IsThreatSensorCollider(collider)) return;

            if (IsPlayer(collider))
            {
                PlayerHitResolver.TryApplyHitToPlayer(
                    collider,
                    damage,
                    ParryableAttackType.Melee,
                    transform,
                    new Vector2(moveDirectionX, 0.2f).normalized);
                // Skill 1 triệu hồi mob tại điểm GroundLine kết thúc, kể cả khi
                // điểm kết thúc là do đụng player trước khi tới tường.
                Impact(collider.ClosestPoint(transform.position), true, spawnDusthornOnImpact);
                return;
            }

            if (IsEnvironment(collider))
            {
                Impact(collider.ClosestPoint(transform.position), false, spawnDusthornOnImpact);
            }
        }

        private bool IsPlayer(Collider2D collider)
        {
            return collider.CompareTag("Player") || collider.GetComponentInParent<PlayerHealth>() != null;
        }

        private bool IsEnvironment(Collider2D collider)
        {
            if (environmentLayers.value != 0 && ((environmentLayers.value & (1 << collider.gameObject.layer)) != 0))
            {
                return true;
            }

            string tag = collider.gameObject.tag;
            string objectName = collider.gameObject.name.ToLowerInvariant();
            return tag == "Ground" || tag == "Wall" || tag == "Environment" ||
                   objectName.Contains("ground") || objectName.Contains("wall") || objectName.Contains("platform");
        }

        private void Impact(Vector3 impactPosition, bool hitPlayer, bool summonDusthorn)
        {
            if (hasImpacted) return;
            hasImpacted = true;
            CancelInvoke(nameof(ExpireAtEndpoint));

            if (hitbox != null) hitbox.enabled = false;
            if (body != null) body.simulated = false;
            StopTravelVfx();

            GroundLineImpactBurst.Spawn(impactPosition, hitPlayer, !hitPlayer);
            if (summonDusthorn) SpawnDusthorn(impactPosition);

            // Cho trail và particle đang sống kịp tan tự nhiên thay vì biến mất giật cục.
            Destroy(gameObject, 0.38f);
        }

        private void StopTravelVfx()
        {
            ParticleSystem[] particles = GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem particle in particles)
            {
                if (particle != null)
                    particle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void SpawnDusthorn(Vector3 position)
        {
            if (dusthornPrefab == null || maxActiveDusthorns <= 0) return;
            CleanupDusthornRegistry();
            if (ActiveDusthorns.Count + PendingDusthornSpawns >= maxActiveDusthorns) return;

            PendingDusthornSpawns++;
            GameObject emergenceObject = new GameObject("Dusthorn_Emergence_VFX");
            GroundLineDusthornEmergence emergence = emergenceObject.AddComponent<GroundLineDusthornEmergence>();
            emergence.Initialize(dusthornPrefab, position, 0.48f,
                RegisterSpawnedDusthorn, ReleasePendingDusthorn);
        }

        private static void RegisterSpawnedDusthorn(GameObject dusthorn)
        {
            PendingDusthornSpawns = Mathf.Max(0, PendingDusthornSpawns - 1);
            if (dusthorn != null) ActiveDusthorns.Add(dusthorn);
        }

        private static void ReleasePendingDusthorn()
        {
            PendingDusthornSpawns = Mathf.Max(0, PendingDusthornSpawns - 1);
        }

        private static void CleanupDusthornRegistry()
        {
            ActiveDusthorns.RemoveWhere(dusthorn => dusthorn == null);
        }

        private void OnDestroy()
        {
            GroundLineVfxUtility.DestroyMaterials(runtimeMaterials);
        }
    }
}
