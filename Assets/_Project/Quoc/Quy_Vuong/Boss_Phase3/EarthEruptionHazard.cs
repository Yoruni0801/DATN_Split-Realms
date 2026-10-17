using System.Collections;
using UnityEngine;

namespace Quoc.QuyVuong.Boss_Phase3
{
    /// <summary>
    /// Vùng đất trồi (Earth Eruption Hazard & Warning Zone) cho Skill 3 của Boss Phase 3.
    /// Flow: Warning (Telegraph) -> Delay -> Eruption (Đá trồi lên gây damage) -> Destroy.
    /// </summary>
    public class EarthEruptionHazard : MonoBehaviour
    {
        [Header("Telegraph & Warning Visual (Expose Inspector)")]
        [Tooltip("GameObject / Decal / Ground Marker hiển thị vùng nguy hiểm cảnh báo")]
        [SerializeField] private GameObject warningMarker;

        [Tooltip("Particle warning sạc năng lượng/vòng ma thuật cảnh báo")]
        [SerializeField] private ParticleSystem warningParticle;

        [Header("Eruption Rock VFX (Expose Inspector)")]
        [Tooltip("GameObject / Particle đá trồi lên gây sát thương")]
        [SerializeField] private GameObject eruptionRockPrefab;

        [Tooltip("Particle bụi/nổ khi đá trồi")]
        [SerializeField] private ParticleSystem eruptionParticle;

        [Header("Hitbox & Damage Settings")]
        [SerializeField] private Collider2D eruptionDamageCollider;
        [SerializeField] private int damage = 25;
        [SerializeField] private float damageActiveDuration = 0.4f;
        [SerializeField] private Vector2 damageBoxSize = new Vector2(2.0f, 3.0f);
        [SerializeField] private Vector2 damageBoxOffset = new Vector2(0f, 1.5f);

        private bool hasDealtDamage = false;

        private void Awake()
        {
            if (eruptionDamageCollider == null)
            {
                eruptionDamageCollider = GetComponent<Collider2D>();
            }

            if (eruptionDamageCollider != null)
            {
                eruptionDamageCollider.enabled = false;
            }
        }

        /// <summary>
        /// Kích hoạt chuỗi Earth Eruption: Warning Delay -> Rock Erupt
        /// </summary>
        public void ExecuteEruptionSequence(float warningDelay, int damageAmount)
        {
            damage = damageAmount;
            hasDealtDamage = false;
            StartCoroutine(EruptionCoroutine(warningDelay));
        }

        private IEnumerator EruptionCoroutine(float warningDelay)
        {
            // Bước 1 & 2: Hiển thị Warning Zone
            if (warningMarker != null) warningMarker.SetActive(true);
            if (warningParticle != null)
            {
                warningParticle.Play();
            }
            else
            {
                // Tự tạo VFX Cảnh Báo mặc định nếu chưa gán trong Inspector
                CreateDefaultWarningVFX();
            }

            // Bước 3: Delay chờ Player di chuyển né
            yield return new WaitForSeconds(warningDelay);

            // Tắt warning visual
            if (warningParticle != null) warningParticle.Stop();
            if (warningMarker != null) warningMarker.SetActive(false);

            // Bước 4: Eruption - Đá trồi lên
            if (eruptionRockPrefab != null)
            {
                eruptionRockPrefab.SetActive(true);
            }
            if (eruptionParticle != null)
            {
                eruptionParticle.Play();
            }
            else
            {
                // Tự tạo VFX Đá Trồi mặc định nếu chưa gán trong Inspector
                CreateDefaultEruptionVFX();
            }

            // Bật hitbox gây sát thương
            if (eruptionDamageCollider != null)
            {
                eruptionDamageCollider.enabled = true;
            }

            // Kiểm tra chủ động Overlap2D xung quanh vị trí eruption để đảm bảo trúng Player kể cả khi Player đã đứng sẵn trong vùng
            CheckActiveOverlapHit();

            float elapsed = 0f;
            while (elapsed < damageActiveDuration)
            {
                if (!hasDealtDamage)
                {
                    CheckActiveOverlapHit();
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Tắt damage collider sau thời gian active
            if (eruptionDamageCollider != null)
            {
                eruptionDamageCollider.enabled = false;
            }

            // Tự hủy sau khi hoàn tất animation eruption (1.5s)
            yield return new WaitForSeconds(1.5f);
            Destroy(gameObject);
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            HandleHit(collision);
        }

        private void OnTriggerStay2D(Collider2D collision)
        {
            HandleHit(collision);
        }

        private void CheckActiveOverlapHit()
        {
            if (hasDealtDamage) return;

            // 1. OverlapBoxAll tìm Collider thuộc Player
            Vector2 center = (Vector2)transform.position + damageBoxOffset;
            Collider2D[] hits = Physics2D.OverlapBoxAll(center, damageBoxSize, 0f);
            foreach (var hit in hits)
            {
                if (hit != null && (hit.CompareTag("Player") || hit.GetComponentInParent<PlayerHealth>() != null))
                {
                    HandleHit(hit);
                    if (hasDealtDamage) return;
                }
            }

            // 2. Fallback: Kiểm tra khoảng cách trực tiếp tới vị trí Player (nếu Physics Overlap bị lỡ)
            Transform playerT = GetPlayerTransform();
            if (playerT != null)
            {
                float dist = Vector2.Distance(transform.position, playerT.position);
                if (dist <= 2.5f)
                {
                    TryDamagePlayerTransform(playerT);
                }
            }
        }

        private Transform GetPlayerTransform()
        {
            if (PlayerRegistry.Current != null) return PlayerRegistry.Current.transform;

            GameObject pObj = GameObject.FindGameObjectWithTag("Player");
            if (pObj != null) return pObj.transform;

            var health = FindAnyObjectByType<PlayerHealth>();
            if (health != null) return health.transform;

            return null;
        }

        private void HandleHit(Collider2D collider)
        {
            if (hasDealtDamage || collider == null) return;
            if (PlayerHitResolver.IsThreatSensorCollider(collider)) return;

            if (collider.CompareTag("Player") || collider.GetComponentInParent<PlayerHealth>() != null)
            {
                IncomingAttackResolution res = PlayerHitResolver.TryApplyHitToPlayer(
                    collider,
                    damage,
                    ParryableAttackType.Melee,
                    transform,
                    Vector2.up
                );

                if (res != IncomingAttackResolution.Ignored)
                {
                    hasDealtDamage = true;
                }
                else
                {
                    // Fallback trực tiếp qua PlayerHealth nếu TryApplyHitToPlayer bị Ignored
                    var health = collider.GetComponentInParent<PlayerHealth>();
                    if (health != null && !health.IsDead)
                    {
                        health.TakeDamage(damage, transform);
                        hasDealtDamage = true;
                    }
                }
            }
        }

        private void TryDamagePlayerTransform(Transform playerT)
        {
            if (hasDealtDamage || playerT == null) return;

            Collider2D playerCollider = playerT.GetComponent<Collider2D>();
            if (playerCollider == null) playerCollider = playerT.GetComponentInChildren<Collider2D>();

            if (playerCollider != null)
            {
                IncomingAttackResolution res = PlayerHitResolver.TryApplyHitToPlayer(
                    playerCollider,
                    damage,
                    ParryableAttackType.Melee,
                    transform,
                    Vector2.up
                );

                if (res != IncomingAttackResolution.Ignored)
                {
                    hasDealtDamage = true;
                    return;
                }
            }

            var health = playerT.GetComponent<PlayerHealth>();
            if (health == null) health = playerT.GetComponentInParent<PlayerHealth>();
            if (health != null && !health.IsDead)
            {
                health.TakeDamage(damage, transform);
                hasDealtDamage = true;
            }
        }

        #region Default Procedural VFX Generation
        /// <summary>
        /// Tạo vòng tròn cảnh báo ma thuật / năng lượng màu đỏ cam đất khi warningParticle/warningMarker bị trống trong Inspector.
        /// </summary>
        private void CreateDefaultWarningVFX()
        {
            GameObject warningObj = new GameObject("Default_WarningVFX");
            warningObj.transform.SetParent(transform, false);
            warningObj.transform.localPosition = Vector3.zero;

            ParticleSystem ps = warningObj.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 1.0f;
            main.loop = true;
            main.startLifetime = 0.5f;
            main.startSpeed = 0.5f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startColor = new Color(1.0f, 0.35f, 0.05f, 0.8f); // Đỏ cam rực rỡ cảnh báo
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 30f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.0f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(1f, 0.4f, 0.1f), 0.0f), new GradientColorKey(new Color(1f, 0.1f, 0f), 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.8f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            colorOverLifetime.color = grad;

            ParticleSystemRenderer renderer = warningObj.GetComponent<ParticleSystemRenderer>();
            Shader spriteShader = Shader.Find("Sprites/Default");
            if (spriteShader != null)
            {
                renderer.material = new Material(spriteShader);
            }

            ps.Play();
        }

        /// <summary>
        /// Tạo hiệu ứng bùng nổ đá trồi màu nâu đất & cam lửa khi eruptionParticle bị trống trong Inspector.
        /// </summary>
        private void CreateDefaultEruptionVFX()
        {
            GameObject eruptionObj = new GameObject("Default_EruptionVFX");
            eruptionObj.transform.SetParent(transform, false);
            eruptionObj.transform.localPosition = Vector3.zero;

            ParticleSystem ps = eruptionObj.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4.0f, 8.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.6f, 0.35f, 0.15f, 1.0f), // Màu nâu đất
                new Color(1.0f, 0.4f, 0.05f, 1.0f)   // Cam lửa nhiệt lượng đất trồi
            );
            main.gravityModifier = 1.5f; // Mảnh đá trồi lên rồi rơi xuống
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 35) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.5f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(0.8f, 0.4f, 0.1f), 0.0f), new GradientColorKey(new Color(0.3f, 0.15f, 0.05f), 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            colorOverLifetime.color = grad;

            ParticleSystemRenderer renderer = eruptionObj.GetComponent<ParticleSystemRenderer>();
            Shader spriteShader = Shader.Find("Sprites/Default");
            if (spriteShader != null)
            {
                renderer.material = new Material(spriteShader);
            }

            ps.Play();
        }
        #endregion
    }
}
