using System.Collections;
using UnityEngine;
using Spine.Unity;
using Quoc.QuyVuong;

namespace Bao.Bosses.QuyVuongPhase4
{
    /// <summary>
    /// Base class cho các skill của Quỷ Vương Phase 4 (Magma Golem form)
    /// Hỗ trợ cả QuyVuongBossPhase lẫn fallback SkeletonAnimation
    /// </summary>
    public abstract class QuyVuongPhase4SkillBase : MonoBehaviour
    {
        [Header("Skill Base Settings")]
        [SerializeField] protected string skillName = "Phase 4 Skill";
        [SerializeField] protected float skillCooldown = 3f;
        [SerializeField] protected bool debugMode = false;

        [Header("Phase 4 Model References")]
        [SerializeField] protected QuyVuongBossPhase phase4Model;
        [SerializeField] protected SkeletonAnimation skeletonAnimation;
        [SerializeField] protected Transform meleePoint;
        [SerializeField] protected Transform rangedPoint;
        [SerializeField] protected Transform groundPoint;

        public Transform MeleePoint => meleePoint != null ? meleePoint : (phase4Model != null ? phase4Model.MeleePoint : transform);
        public Transform RangedPoint => rangedPoint != null ? rangedPoint : (phase4Model != null ? phase4Model.RangedPoint : transform);
        public Transform GroundPoint => groundPoint != null ? groundPoint : (phase4Model != null ? phase4Model.GroundPoint : transform);

        protected virtual void Awake()
        {
            ResolveReferences();
        }

        public void ResolveReferences()
        {
            if (phase4Model == null)
            {
                var phases = GetComponentsInChildren<QuyVuongBossPhase>(true);
                foreach (var p in phases)
                {
                    if (p.gameObject.name.Contains("Phase4") || p.gameObject.name.Contains("Lua"))
                    {
                        phase4Model = p;
                        break;
                    }
                }
                if (phase4Model == null && phases.Length > 0)
                {
                    phase4Model = phases[phases.Length - 1];
                }
            }

            if (phase4Model != null)
            {
                if (skeletonAnimation == null) skeletonAnimation = phase4Model.SkeletonAnimation;
                if (meleePoint == null) meleePoint = phase4Model.MeleePoint;
                if (rangedPoint == null) rangedPoint = phase4Model.RangedPoint;
                if (groundPoint == null) groundPoint = phase4Model.GroundPoint;
            }
            else if (skeletonAnimation == null)
            {
                skeletonAnimation = GetComponentInChildren<SkeletonAnimation>(true);
            }
        }

        /// <summary>
        /// Thực thi skill - override trong subclass
        /// </summary>
        public abstract IEnumerator ExecuteSkill(Transform playerTransform);

        public void PlayAttackAnimation()
        {
            if (phase4Model != null)
            {
                phase4Model.PlayAttack();
            }
            else if (skeletonAnimation != null && skeletonAnimation.Skeleton != null)
            {
                skeletonAnimation.AnimationState?.SetAnimation(0, "Skill", false);
            }
        }

        public void PlayMoveAnimation()
        {
            if (phase4Model != null)
            {
                phase4Model.PlayMove();
            }
            else if (skeletonAnimation != null && skeletonAnimation.Skeleton != null)
            {
                skeletonAnimation.AnimationState?.SetAnimation(0, "Run", true);
            }
        }

        public void PlayIdleAnimation()
        {
            if (phase4Model != null)
            {
                phase4Model.PlayIdle();
            }
            else if (skeletonAnimation != null && skeletonAnimation.Skeleton != null)
            {
                skeletonAnimation.AnimationState?.SetAnimation(0, "Idle", true);
            }
        }

        public void SetBossColor(Color color)
        {
            if (phase4Model != null)
            {
                phase4Model.SetColor(color);
            }
            else if (skeletonAnimation != null && skeletonAnimation.Skeleton != null)
            {
                skeletonAnimation.Skeleton.SetColor(color);
            }
        }

        public void SetBossAlpha(float alpha)
        {
            Color c = new Color(1f, 1f, 1f, alpha);
            SetBossColor(c);
        }

        public void SetBossBodyActive(bool active)
        {
            if (skeletonAnimation != null)
            {
                var mr = skeletonAnimation.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = active;
            }
            if (phase4Model != null && phase4Model.SkeletonAnimation != null)
            {
                var mr = phase4Model.SkeletonAnimation.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = active;
            }
            SetBossAlpha(active ? 1f : 0f);
        }

        protected GameObject SpawnVFX(GameObject vfxPrefab, Vector3 position, Quaternion rotation, float lifetime = 3f)
        {
            if (vfxPrefab == null) return null;
            GameObject vfx = Instantiate(vfxPrefab, position, rotation);
            Destroy(vfx, lifetime);
            return vfx;
        }

        protected void TriggerCameraShake(float intensity, float duration)
        {
            var shaker = FindAnyObjectByType<Phase4CameraShaker>();
            if (shaker == null && Camera.main != null)
            {
                shaker = Camera.main.gameObject.AddComponent<Phase4CameraShaker>();
            }
            if (shaker != null)
            {
                shaker.Shake(intensity, duration);
            }
        }

        protected IEnumerator Hitstop(float duration)
        {
            float originalTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(duration);
            Time.timeScale = originalTimeScale;
        }

        protected Vector2 GetDirectionToPlayer(Transform playerTransform)
        {
            if (playerTransform == null) return Vector2.right;
            return (playerTransform.position - transform.position).normalized;
        }

        public void FlipTowardsPlayer(Transform playerTransform)
        {
            if (playerTransform == null) return;

            float directionX = playerTransform.position.x - transform.position.x;
            if (Mathf.Abs(directionX) > 0.05f)
            {
                transform.localScale = new Vector3(
                    (directionX > 0 ? -1 : 1) * Mathf.Abs(transform.localScale.x),
                    transform.localScale.y,
                    transform.localScale.z
                );

                if (phase4Model != null && phase4Model.transform != transform)
                {
                    phase4Model.SetFacingDirection(directionX);
                }
            }
        }

        public static void ApplyPlayerDamage(GameObject target, int damage, float knockback = 0f)
        {
            if (target == null) return;

            var playerHealth = target.GetComponent<PlayerHealth>() ?? target.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null)
            {
                ((Bao.Enemies.IDamageable)playerHealth).TakeDamage(damage);
                return;
            }

            var baoDamageable = target.GetComponent<Bao.Enemies.IDamageable>() ?? target.GetComponentInParent<Bao.Enemies.IDamageable>();
            if (baoDamageable != null)
            {
                baoDamageable.TakeDamage(damage);
                return;
            }

            var enemyDamageable = target.GetComponent<EnemySystem.IDamageable>() ?? target.GetComponentInParent<EnemySystem.IDamageable>();
            if (enemyDamageable != null)
            {
                enemyDamageable.TakeDamage(damage);
                return;
            }
        }

        protected void DebugLog(string message)
        {
            if (debugMode)
            {
                Debug.Log($"[{skillName}] {message}");
            }
        }
    }
}
