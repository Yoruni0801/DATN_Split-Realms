using System.Collections;
using UnityEngine;

namespace Bao.Bosses
{
    [System.Serializable]
    public sealed class NidusRootSpikeVisualSettings
    {
        [Tooltip("Để trống để dùng Assets/Resources/Nidus_RootThorn.png")]
        public Sprite rootSprite;
        [Tooltip("Có thể kéo trực tiếp PNG vào đây. Dùng khi texture chưa được Unity import dưới dạng Sprite.")]
        public Texture2D rootTexture;
        [Tooltip("Dịch gốc sprite so với điểm spawn trên mặt đất.")]
        public Vector2 spawnOffset = Vector2.zero;
        [Min(0.1f)] public float widthMultiplier = 1f;
        [Min(0.1f)] public float heightMultiplier = 1f;
        [Min(0.04f)] public float emergeDuration = 0.16f;
        [Range(-45f, 45f)] public float rotation = 0f;
        [Min(1)] public int normalRootCount = 1;
        [Min(1)] public int prisonRootCount = 3;
        public Color tint = Color.white;
    }

    /// <summary>
    /// Lightweight one-shot root hazard used by Root Spike Wave and Root Prison.
    /// No solid collider is used: gameplay hit detection is explicit and readable.
    /// </summary>
    public sealed class NidusRootSpikeHazard : MonoBehaviour
    {
        private NidusCocoonBoss owner;
        private int damage;
        private float hitRadius;
        private float warningDuration;
        private float activeDuration;
        private float rootHeight;
        private bool large;
        private bool hasHitPlayer;
        private bool shakeOnErupt;
        private NidusRootSpikeVisualSettings visualSettings;
        private Coroutine routine;
        private GameObject telegraph;
        private GameObject burst;

        public static NidusRootSpikeHazard Spawn(
            NidusCocoonBoss owner,
            Vector3 groundPosition,
            float warningDuration,
            int damage,
            float hitRadius,
            float activeDuration = 0.28f,
            float rootHeight = 2.2f,
            bool large = false,
            bool shakeOnErupt = false)
        {
            GameObject go = new GameObject(large ? "Nidus_RootPrisonHazard" : "Nidus_RootSpikeHazard");
            go.transform.position = groundPosition;
            NidusRootSpikeHazard hazard = go.AddComponent<NidusRootSpikeHazard>();
            hazard.Initialize(owner, warningDuration, damage, hitRadius, activeDuration, rootHeight, large, shakeOnErupt);
            return hazard;
        }

        public void Initialize(
            NidusCocoonBoss owner,
            float warningDuration,
            int damage,
            float hitRadius,
            float activeDuration,
            float rootHeight,
            bool large,
            bool shakeOnErupt = false)
        {
            this.owner = owner;
            this.warningDuration = Mathf.Max(0.12f, warningDuration);
            this.damage = Mathf.Max(0, damage);
            this.hitRadius = Mathf.Max(0.15f, hitRadius);
            this.activeDuration = Mathf.Max(0.08f, activeDuration);
            this.rootHeight = Mathf.Max(0.8f, rootHeight);
            this.large = large;
            this.shakeOnErupt = shakeOnErupt;
            visualSettings = owner != null ? owner.RootSpikeVisualSettings : null;

            owner?.RegisterRootHazard(this);
            routine = StartCoroutine(LifetimeRoutine());
        }

        private IEnumerator LifetimeRoutine()
        {
            telegraph = NidusOrganicSkillVFX.SpawnRootTelegraph(
                transform.position,
                hitRadius * (large ? 1.28f : 1f),
                warningDuration,
                large);

            yield return new WaitForSeconds(warningDuration);

            if (telegraph != null)
            {
                Destroy(telegraph);
                telegraph = null;
            }

            if (owner == null || owner.IsDead)
            {
                CleanupAndDestroy();
                yield break;
            }

            burst = NidusOrganicSkillVFX.SpawnRootBurst(
                transform.position,
                hitRadius,
                rootHeight,
                activeDuration + 0.25f,
                large,
                visualSettings);

            if (shakeOnErupt && CameraController.Instance != null)
                CameraController.Instance.ShakeCamera(0.08f, 0.025f);

            float elapsed = 0f;
            while (elapsed < activeDuration)
            {
                if (!hasHitPlayer)
                {
                    TryDamagePlayer();
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            CleanupAndDestroy();
        }

        private void TryDamagePlayer()
        {
            if (owner == null || owner.IsDead)
                return;

            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, hitRadius);
            foreach (Collider2D hit in hits)
            {
                if (hit == null || !hit.CompareTag("Player"))
                    continue;

                hasHitPlayer = true;
                Vector2 direction = ((Vector2)hit.transform.position - (Vector2)transform.position);
                if (direction.sqrMagnitude < 0.001f)
                    direction = Vector2.up;

                PlayerHitResolver.TryApplyHitToPlayer(
                    hit,
                    damage,
                    ParryableAttackType.Melee,
                    EnemyParryResponder.Ensure(owner),
                    owner.transform,
                    direction.normalized,
                    canBeBlocked: false);
                break;
            }
        }

        public void ForceCleanup()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }
            CleanupAndDestroy();
        }

        private void CleanupAndDestroy()
        {
            if (telegraph != null)
            {
                Destroy(telegraph);
                telegraph = null;
            }

            owner?.UnregisterRootHazard(this);
            owner = null;

            if (gameObject != null)
                Destroy(gameObject);
        }

        private void OnDestroy()
        {
            owner?.UnregisterRootHazard(this);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, hitRadius);
        }
#endif
    }
}
